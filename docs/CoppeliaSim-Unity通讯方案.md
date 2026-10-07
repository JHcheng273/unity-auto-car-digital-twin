# CoppeliaSim ↔ Unity 通讯方案

> 定位：**CoppeliaSim = 物理体**（真实动力学），**Unity = 数字体**（可视化 + 决策）。
> 目标：先跑通最小通讯链路（冒烟测试），再往上叠业务逻辑。

---

## 一、为什么不用 ZMQ（网上教程最常讲的方案）

CoppeliaSim 官方的 **ZeroMQ Remote API** 只提供这些客户端：

| 语言 | 官方支持 |
|---|---|
| Python | ✅ |
| C++ | ✅ |
| Java | ✅ |
| MATLAB | ✅ |
| Octave | ✅ |
| Lua（仅 CoppeliaSim 内部两个实例互连） | ✅ |
| Rust（第三方） | ⚠️ |
| **C# / Unity** | ❌ **没有** |

Unity 要用 ZMQ，就得自己在 C# 里实现它的握手协议（CBOR 编码 + 方法名反射），
对零基础来说是个大坑。**不推荐。**

### 走这条路

CoppeliaSim **自带 LuaSocket 扩展库**，也就是说它的 Lua 脚本能直接收发 TCP/UDP。
于是链路变成：

```
CoppeliaSim（Lua 子脚本，收发 UDP）  ←──UDP/JSON──→  Unity（C# UdpClient）
```

**两个进程、一个端口、不需要装任何第三方库。**

---

## 二、环境准备

1. 下载 CoppeliaSim（选 **Edu 版**，教育用途免费）：https://www.coppeliarobotics.com/downloads
2. 解压到**没有空格、没有中文**的路径，例如 `D:\CoppeliaSim_Edu`（官方文档明确提醒：路径带空格会让部分客户端编译失败）
3. 双击 `coppeliaSim.exe` 启动

> 本机目前**没有安装** CoppeliaSim（已确认）。

---

## 三、冒烟测试（目标：15 分钟看到两边互发消息）

### 端口约定

| 进程 | 监听端口 | 发往 |
|---|---|---|
| CoppeliaSim | 5005 | 127.0.0.1:5006 |
| Unity | 5006 | 127.0.0.1:5005 |

### 3.1 CoppeliaSim 侧

1. 新建场景：`File → New scene`
2. 左侧 Hierarchy 里右键 `World` → `Add → Dummy`（随便加个物体就行）
3. 右键这个 Dummy → `Add → Associated child script → Non-threaded`
4. 双击脚本打开编辑器，**全选替换**成下面内容：

```lua
sim = require('sim')
socket = require('socket')

local UNITY_IP   = '127.0.0.1'
local UNITY_PORT = 5006      -- Unity 的收件端口
local MY_PORT    = 5005      -- 本机（CoppeliaSim）的收件端口

local udp
local tSend = 0

function sysCall_init()
    udp = socket.udp()
    udp:setsockname('*', MY_PORT)
    udp:settimeout(0)        -- 0 = 非阻塞。这一行绝对不能省
    sim.addLog(sim.verbosity_scriptinfos, '[Bridge] UDP 监听 ' .. MY_PORT)
end

function sysCall_sensing()
    -- ① 收 Unity 发来的指令
    local data = udp:receivefrom()
    if data then
        sim.addLog(sim.verbosity_scriptinfos, '[Bridge] 收到: ' .. data)
    end

    -- ② 每 0.05 秒回一个状态包（20 Hz）
    local t = sim.getSimulationTime()
    if t - tSend >= 0.05 then
        tSend = t
        local obj = sim.getObject('.')
        local p = sim.getObjectPosition(obj, -1)
        local msg = string.format('{"x":%.3f,"y":%.3f,"z":%.3f,"t":%.3f}',
                                  p[1], p[2], p[3], t)
        udp:sendto(msg, UNITY_IP, UNITY_PORT)
    end
end

function sysCall_cleanup()
    if udp then udp:close() end
end
```

5. **按 ▶ 开始仿真**（`sysCall_sensing` 只在仿真运行时被调用，这点和 Unity 的 `Update` 一样）

> **如果 `socket.udp()` 报错**（个别版本没暴露 UDP），把 `socket.udp()` 换成 `socket.tcp()`，
> 并把 `setsockname/receivefrom/sendto` 换成 `connect/receive/send`。先试 UDP，不行再退 TCP。

### 3.2 Unity 侧

新建 `Assets/MyCoppeliaLink.cs`（文件名 = 类名）：

```csharp
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class MyCoppeliaLink : MonoBehaviour
{
    [Header("端口")]
    public int listenPort    = 5006;   // Unity 收
    public int coppeliaPort  = 5005;   // CoppeliaSim 收

    UdpClient udp;
    IPEndPoint coppelia;

    void Start()
    {
        udp = new UdpClient(listenPort);
        udp.Client.Blocking = false;                       // 非阻塞，别卡住主线程
        coppelia = new IPEndPoint(IPAddress.Loopback, coppeliaPort);
        Debug.Log($"[Bridge] Unity 监听 {listenPort}，发往 {coppeliaPort}");
    }

    void Update()
    {
        // ① 收 CoppeliaSim 的状态
        while (udp.Available > 0)
        {
            IPEndPoint from = null;
            byte[] data = udp.Receive(ref from);
            Debug.Log("[Bridge] 收到: " + Encoding.UTF8.GetString(data));
        }

        // ② 按 T 发一条指令过去
        if (Input.GetKeyDown(KeyCode.T))
        {
            byte[] b = Encoding.UTF8.GetBytes("hello from Unity");
            udp.Send(b, b.Length, coppelia);
            Debug.Log("[Bridge] 已发送 ping");
        }
    }

    void OnDestroy()
    {
        if (udp != null) udp.Close();
    }
}
```

挂到场景里任意一个空物体上，按 ▶。

### 3.3 判断成功

| 现象 | 说明 |
|---|---|
| CoppeliaSim 日志区出现 `[Bridge] UDP 监听 5005` | Lua 侧起来了 |
| Unity Console 每 0.05 秒刷一条 `收到: {"x":...}` | **CoppeliaSim → Unity 通了** ✅ |
| Unity 里按 `T`，CoppeliaSim 日志出现 `收到: hello from Unity` | **Unity → CoppeliaSim 通了** ✅ |

> 第一次运行 Windows 可能弹防火墙提示 —— 允许「专用网络」即可（本机 loopback 通常不弹）。

---

## 四、跑通之后往哪走

冒烟测试只是证明了链路。真正的分工建议：

| 环节 | 放在哪 | 说明 |
|---|---|---|
| 车辆动力学（加减速、转弯、打滑） | **CoppeliaSim** | 用它的物理引擎，比 Unity 手写 `transform.Translate` 真实得多 |
| 传感器（测距、碰撞检测） | **CoppeliaSim** | 用 `sim.readProximitySensor` 或射线 |
| 路径跟随 / 避障决策 | **Unity** | 复用已经写好的 `AutoDrive` 算法 |
| 可视化 / 仪表盘 / 录屏 | **Unity** | 它的强项 |

数据流：

```
Unity 每帧算出 steering / motor
   ↓ UDP 发出去
CoppeliaSim 把指令施加到轮子上，物理引擎推进一帧
   ↓ UDP 把 (x, y, z, 朝向, 速度) 发回来
Unity 把收到的位姿写到自己那台"影子车"上（只显示，不算物理）
```

**关键改动**：Unity 里的 `CarController` 从「自己移动车」变成「把位姿同步给车」。
`AutoDrive` 的算法不用动，只把 `car.SetInput(...)` 换成「发 UDP 包」。

---

## 五、待确认（动手前必须想清楚）

1. **课程是否硬性要求 MATLAB / Simulink？** 如果是，CoppeliaSim 只能作为补充，
   不能替代 —— 那就变成三进程：`Unity ↔ MATLAB ↔ CoppeliaSim`（CoppeliaSim 有官方 MATLAB 客户端，可行但工作量翻倍）。
2. **控制算法放哪边？** 建议留在 Unity（复用已有成果），CoppeliaSim 只管物理。
3. **时间预算**：从零上手 CoppeliaSim + 建模 + 通讯 + 联调，保守估计 **15~25 小时**。
