# Unity ↔ MATLAB 数字孪生联动方案

> 配套代码：`Assets/Scripts/Shared/MatlabUdpBridge.cs`、`matlab/unity_bridge.m`

---

## 0. 先说结论：这是个加分项，不是必选项

| | |
|---|---|
| **收益** | 报告里能写"数字孪生五维模型"、答辩有实时曲线图可演示、显得有工程味 |
| **成本** | 约 8~12 人时，且调试期一定会出现"数据对不上"的玄学问题 |
| **风险** | 拖累核心闭环（车 + 射线 + Waypoint + UI）的进度 |
| **建议** | **前 6 周一个字都别碰 MATLAB**。第 5 周先从"单向记录"做起，第 6~7 周再上双向控制 |

一句话原则：**核心闭环是 1，MATLAB 是后面的 0。没有 1，再多 0 也没用。**

---

## 1. 分工怎么分

现在的分工（A 车辆 / B 感知 / C 控制集成）**不用动**，MATLAB 作为第 5 周起的"可选扩展"插进去：

| 角色 | 额外职责 | 人时 |
|---|---|---|
| **C（通信负责人）** | 维护 `MatlabUdpBridge.cs`；定义协议字段；处理超时降级；把 `v_des` 接进自己的速度控制 | 4~5h |
| **B（你）** | 只负责**把数据喂给桥**：确认 `FrontCarDistance`、`Steering`、`CurrentWaypointIndex` 是有效值。**不用写一行 MATLAB** | 1h |
| **A** | 提供车辆位姿和真实速度（`Rigidbody.velocity`），确认坐标系一致 | 0.5h |
| **MATLAB 侧** | 谁熟 MATLAB 谁写。如果三个人都不会，**建议直接放弃双向，改做离线分析** | 3~6h |

**关键点：B 的活最轻。** 因为你的 `BModuleFacade` 已经把感知数据收敛成一个接口了，桥接脚本直接读它就行——这就是当初定接口的红利。

如果组内只有一个人会 MATLAB，让他**只写 MATLAB 侧 + 参与定协议**，Unity 侧桥接仍归 C（避免他同时改两边，出问题分不清是谁的锅）。

---

## 2. 连接方式：为什么选 UDP

| 方案 | 复杂度 | 实时性 | 评价 |
|---|---|---|---|
| **UDP + JSON** | ★★ | 10~50Hz | **推荐**。几行代码，同机/局域网都行，掉包不影响仿真继续跑 |
| TCP + JSON | ★★ | 10~50Hz | 更可靠但要处理粘包断连；对课设来说 UDP 够了 |
| 写 CSV 文件再读 | ★ | 离线 | 只能做"离线孪生"，不能实时控制；**但这是最好的保底方案** |
| MATLAB COM 自动化 | ★★★★ | 低 | Windows 上把 MATLAB 当 COM server 调用，卡、难调、不讲人话 |
| ROS / ROS Toolbox | ★★★★★ | 高 | 学 ROS 比做这个项目本身还久，别碰 |
| Simulink 导出 FMU/DLL | ★★★★★ | 高 | 需要额外工具箱和代码生成许可，本科生课设不值当 |

**结论：UDP + JSON。端口 5005（上行）/ 5006（下行），同机填 `127.0.0.1`。**

---

## 3. 数据契约（改字段必须全组同步）

### Unity → MATLAB（状态上行，20Hz）

| 字段 | 含义 | 来源 |
|---|---|---|
| `t` | 仿真时间（秒） | `Time.time` |
| `x y z` | 位置（米） | 车身 Transform |
| `yaw` | 朝向（度） | `eulerAngles.y` |
| `speed` | 速度（米/秒） | `Rigidbody.velocity.magnitude` |
| `steer` | 当前转向 −1~+1 | **B 的 `WaypointFollower.Steering`** |
| `front_dist` | 前车距离（米） | **B 的 `FrontCarDetector.FrontCarDistance`** |
| `wp` / `lap` | 当前路径点 / 圈数 | **B 的 `WaypointFollower`** |
| `has_front` | 是否有前车 | **B 的 `FrontCarDetector.HasFrontCar`** |

### MATLAB → Unity（指令下行）

| 字段 | 含义 |
|---|---|
| `v_des` | 期望速度（米/秒） |
| `steer_des` | 期望转向；**填 999 = "转向交给 Unity"** |
| `brake` | 是否强制刹车 |
| `seq` | 序号，用来看丢包 |

> 为什么 `steer_des` 要给个 999 这种约定：横向控制（路径跟踪）在 Unity 里已经做好了，没必要搬到 MATLAB。
> **MATLAB 只管纵向速度规划**，职责清晰、调试简单、报告里也好解释。

---

## 4. 跑通步骤（照着做，半小时）

1. Unity 里把 `MatlabUdpBridge.cs` 挂到车上，拖好 `car` / `carRigidbody` / `detector` / `follower`
2. 先**不要**勾 `acceptMatlabCommand`（先只收不发指令，确认链路通）
3. MATLAB 里打开 `matlab/unity_bridge.m`，直接运行 → 出现图窗等待数据
4. Unity 按 Play → MATLAB 图窗开始画曲线 = **链路通了**
5. 确认无误后，Unity 侧勾上 `acceptMatlabCommand`，C 把 `bridge.GetDesiredSpeed()` 接进自己的速度控制
6. 关掉图窗，MATLAB 自动把日志存成 `twin_log_*.csv`，直接拿去写报告

---

## 5. 时序与实时性（这块最容易翻车）

- **物理用 FixedUpdate（默认 50Hz），通信用 20Hz。** 别在 `FixedUpdate` 里发 UDP，一次发包 ≈ 0.1ms，但系统调用会卡帧
- **收数据必须在子线程，解析在主线程。** Unity 的 API（`Transform`、`Rigidbody`）**不能在子线程碰**，否则随机崩溃。脚本里用 `ConcurrentQueue` 做了隔离
- **仿真时间用 `Time.time`，不要用墙钟。** 两边时钟不同步，混用会出现"速度算出来是负的"这种鬼故事
- **必须做超时降级。** MATLAB 崩了/关了，小车不能一起死——`IsConnected == false` 时立刻切回本地控制。这是答辩必问项：**"你的系统 MATLAB 挂了会怎么样？"**

---

## 6. 报告里怎么写"数字孪生"

老师最爱看的是**概念映射**，照这个表填，一段话就能撑起一章：

| 数字孪生要素 | 你的实现 |
|---|---|
| 物理实体 | Unity 中的小车（WheelCollider + Rigidbody 物理） |
| 虚拟模型 | MATLAB 中的纵向速度规划算法 |
| 双向数据流 | UDP 20Hz：状态上行 / 指令下行 |
| 实时映射 | Unity 状态变化 → MATLAB 曲线同步刷新 |
| 闭环反馈 | MATLAB 决策 → 回传 → 改变车辆行为 |
| 数据沉淀 | 全程日志落 CSV，可回放分析 |

**加分句**："本系统采用松耦合的 UDP 通信架构，算法层与仿真层解耦，任一端异常时系统可自动降级为本地控制，保证仿真连续性。" —— 这句话能顶半页。

---

## 7. 如果做不了，怎么降级

| 情况 | 方案 |
|---|---|
| 没人会 MATLAB | 换 **Python**（`socket` + `matplotlib`），代码结构一模一样，学习成本更低 |
| 装不了 MATLAB / 无 License | **离线孪生**：Unity 把数据写成 CSV（C 原计划第 5 周就要做这个），MATLAB/Python/Excel 事后分析画图。**报告里照样能叫数字孪生**，只是非实时 |
| 网络不通（防火墙/校园网隔离） | 用 `127.0.0.1` 同机回环；还不行就退回 CSV 离线方案 |
| 时间不够 | 砍掉，专心保核心闭环。**没有 MATLAB 不影响及格** |

---

## 8. 常见坑

| 现象 | 原因 | 解决 |
|---|---|---|
| MATLAB 一直收不到 | 防火墙拦了 UDP | 关防火墙或放行端口；先试 `127.0.0.1` |
| `jsondecode` 报错 | JSON 里有 `NaN` / `Infinity` | 桥接脚本的 `Round()` 已经过滤了；自己加字段时也要过滤 |
| 字段名对不上 | C# 用驼峰、MATLAB 用原名 | 字段名**两边必须完全一致**，改一边就要改另一边 |
| Unity 随机崩溃 | 子线程里碰了 Unity 对象 | 所有 Unity API 调用只能在主线程；用队列传递原始数据 |
| 图窗卡死 | 每包都 `drawnow` | 脚本里用 `drawnow limitrate` + 每 N 包刷新 |
| 车忽快忽慢 | 没做超时降级导致指令抖动 | 确认 `IsConnected` 逻辑；给 `v_des` 加一个变化率限制 |
| 端口被占 | 上次没关干净 | 换端口号，或 `clear u` / 重启 MATLAB |
| 速度单位不对 | Unity 用米/秒，有人当 km/h | 全程统一用 **米/秒**，报告里注明 |

---

## 9. 无线 / 跨机部署（两台电脑）

**结论：可以，而且改动很小。** UDP 本来就是网络协议，`127.0.0.1` 只是"本机"的特例而已。

### 要改的地方（只有两处）

| 位置 | 改成 |
|---|---|
| Unity 侧 Inspector → `MatlabUdpBridge` → **matlabHost** | MATLAB 那台机器的局域网 IP |
| MATLAB 侧 `unity_bridge.m` → **UNITY_IP** | Unity 那台机器的局域网 IP |

端口不用动（5005 / 5006）。

### 查 IP

在对方机器上开 cmd：

```
ipconfig
```

找**无线局域网适配器 WLAN** 下面的 **IPv4 地址**（一般是 `192.168.x.x`）。
两台机器必须在**同一个 WiFi / 同一网段**——一个连有线一个连无线，很可能不在同一子网，就不通。

### 三步验证（按顺序来，别跳）

```bash
ping 192.168.1.35          # 1. 能 ping 通再说别的
```

ping 不通的话，后面的都没意义。常见原因是**防火墙拦了 ICMP**，那就在 MATLAB 机器上放行 UDP 端口：

```powershell
# 管理员 PowerShell，两台机器都要跑（各自放行自己的入站端口）
netsh advfirewall firewall add rule name="MATLAB-UDP-5005" dir=in action=allow protocol=UDP localport=5005
netsh advfirewall firewall add rule name="Unity-UDP-5006"  dir=in action=allow protocol=UDP localport=5006
```

### 无线的三个真坑

| 坑 | 现象 | 解决 |
|---|---|---|
| **校园网 AP 隔离** | 两台都连着校园 WiFi，但 ping 不通 | 路由器开了"客户端隔离"，设备之间禁止互访。**开手机热点**，两台都连它，组成一个小局域网 |
| **丢包** | 曲线偶尔断一小段 | UDP 不重传，这是正常的。协议里有 `seq`，靠它看丢包率；控制逻辑靠超时降级兜底 |
| **抖动 / 延迟** | 车忽快忽慢 | WiFi 延迟一般 1~10ms，20Hz 通信完全够；但对 `v_des` **加一阶低通或变化率限幅**，别让指令跳变直接打到车上 |

### 更炫的玩法（有余力再说）

Unity 打包成 **Android APK** 装到手机上跑，手机通过 WiFi 跟电脑上的 MATLAB 通信——演示效果很炸。
但要额外装 **Unity Android Build Support**（1GB+），对零基础团队不划算，建议先别碰。

### 性能预期

| 指标 | 局域网 WiFi | 说明 |
|---|---|---|
| 单程延迟 | 1~10 ms | 20Hz（周期 50ms）绰绰有余 |
| 丢包率 | 通常 <1%，拥挤时可能 5%+ | 靠 `seq` 监控，靠超时降级兜底 |
| 建议频率 | 20Hz 不变 | 无线别往上加，50Hz 反而更容易抖 |

---

## 10. 最后一句

数字孪生听起来玄，落到代码上就是**一个 UDP socket + 一个 JSON**。
真正值钱的不是这根线，是你能说清楚：**哪边是物理实体、哪边是虚拟模型、数据怎么流、断了会怎样。**

把这四句答明白，老师就不会为难你。
