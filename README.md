# Unity 自动驾驶小车数字孪生

一个从零搭起来的自动驾驶仿真项目：Unity 里的车**自己沿路标点跑**、**遇障减速与避让**、
**用状态机管理行为**，同时把车辆状态以 20Hz 通过 UDP 发给 MATLAB，
由 MATLAB 侧的**孪生模型独立推演**车速与里程并回传控制指令。

Unity 2022.3 LTS · 无硬件 · 纯软件仿真

---

## 项目状态

| 模块 | 内容 | 状态 |
|---|---|---|
| 感知 | 单射线 Raycast 前向障碍检测，输出原始距离与报警标志 | ✅ 已跑通 |
| 决策 | 三档油门（畅通 / 减速 / 刹停）+ 状态机（Driving / Blocked / Finished）+ 被堵超时放弃路标点 | ✅ 已跑通 |
| 执行 | 运动学小车：`SetInput(steering, motor)` → 位移与转向 | ✅ 已跑通 |
| 可视化 | 屏幕数据面板（状态 / 目标点 / 圈数 / 速度 / 前方障碍） | ✅ 已跑通 |
| 数字孪生 | Unity ↔ MATLAB 双向 UDP，孪生模型推演 + 三张实时曲线 + 精度指标 + CSV 落盘 | ✅ 已跑通 |

**完整链路**：车能自己跑完一整圈（WP0→WP3），全程数据实时回传 MATLAB，
结束后自动打印**速度偏差 RMS** 与**累计里程差异百分比**。

---

## 系统架构

```
┌─────────────────────────── Unity（物理实体）───────────────────────────┐
│                                                                        │
│   感知                    决策                      执行                 │
│  MyFrontDetector  ──►   AutoDrive         ──►   CarController          │
│  (Physics.Raycast)      (状态机 + 三档油门)      (位移 / 转向)           │
│                              │                                         │
│                              ▼                                         │
│                          HudDisplay                                    │
│                          (屏幕数据面板)                                 │
└──────────────────────────────┬─────────────────────────────────────────┘
                               │
                    MatlabBridge（UDP · 20Hz）
                               │
        状态 JSON ──► 127.0.0.1:5005        指令 JSON ──► 127.0.0.1:5006
                               │
┌──────────────────────────────▼─────── MATLAB（数字孪生体）──────────────┐
│   jsondecode → 按前车距离决策 → 一阶惯性模型推演 v_model / s_model       │
│   → 三张实时曲线 → 速度偏差 RMS / 里程差异 → 自动存 CSV                  │
└────────────────────────────────────────────────────────────────────────┘
```

**数据连接层**：UDP + JSON。两个方向**互相独立**——UDP 没有"连接"概念，
`IsConnected` 是 Unity 自己按「最近 1.5 秒有没有收到包」算出来的。

---

## 代码清单

### 主用脚本 — `Assets/Scripts/Learn/`

逐课手写的版本，**当前场景实际使用的就是这一套**。

| 文件 | 层次 | 职责 |
|---|---|---|
| `MyFrontDetector.cs` | 感知 | `Physics.Raycast` 打前方，暴露 `ObstacleDistance` / `HasObstacle` / `ObstacleName`；底层只给原始数据，判断留给上层 |
| `AutoDrive.cs` | 决策 | `enum State { Driving, Blocked, Finished }` + `switch` 状态机；三档油门 `slowDistance` / `safeDistance` / `slowFactor`；被堵超过 `blockedTimeout` 秒则放弃当前路标点 |
| `CarController.cs` | 执行 | 把 `steering` / `motor` 变成 `transform` 的位移与转向 |
| `MyWaypointPath.cs` | 数据 | 路标点列表：`GetPoint` / `NextIndex` / `Count` / `loop` |
| `HudDisplay.cs` | 可视化 | 把车的"内心活动"画到屏幕左上角（Legacy Text） |
| `FollowCamera.cs` | 可视化 | 相机跟随 |
| `MatlabBridge.cs` | 通讯 | Unity ↔ MATLAB 的 UDP 桥：后台线程收、主线程解析、20Hz 上行、超时降级 |

### 模块脚手架 — `Assets/Scripts/B/`

早期按"三人分工"设计时的正式版接口层，保留作为**接口契约的权威定义**。

| 文件 | 职责 |
|---|---|
| `WaypointPath.cs` / `WaypointFollower.cs` | 路径点容器与路径跟踪 |
| `FrontCarDetector.cs` | 前车检测（含 Layer 过滤） |
| `BModuleFacade.cs` | 对外唯一接口，其他模块只认这一个脚本 |
| `PathVisualizer.cs` | 已跑 / 未跑路径可视化 |
| `BTestRig.cs` | 测试装置（卡死检测与侧向绕行） |

### 编辑器工具 — `Assets/Editor/`

| 文件 | 职责 |
|---|---|
| `BTestSceneBuilder.cs` | Unity 菜单 `Tools → B模块`：一键建 Layer、一键搭测试场景 |

---

## 怎么跑起来

本仓库**不是**可直接打开的 Unity 工程（不含 `ProjectSettings` / `Packages`）。
正确做法：用 Unity Hub 新建 **3D (Core)** 工程（2022.3 LTS），
把本仓库 `Assets/` 下的内容拷进工程的 `Assets/`。

### A. 只跑 Unity（单机闭环）

1. 打开场景 `Assets/Scenes/MyCar.unity`
2. 按 `▶` Play
3. 预期：车自己沿 WP0→WP1→WP2→WP3 跑圈，左上角数据面板实时刷新，
   状态从 `Driving` 走到 `Finished`，速度归零

### B. 加上 MATLAB 数字孪生联动

**顺序很重要：先 MATLAB，后 Unity。**

1. MATLAB（R2020b 及以上）把当前文件夹切到本仓库的 `matlab/` 目录：
   ```matlab
   cd '仓库路径\matlab'
   ```
2. 运行脚本（**不要加 `.m` 后缀**）：
   ```matlab
   unity_bridge
   ```
   预期：命令窗口出现 `监听 5005，等待 Unity 数据...` 并弹出图窗。
   **命令窗口不返回 `>>` 是正常的**——脚本在等数据（默认跑 120 秒）。
3. 回 Unity，按 `▶` Play
4. 预期：Unity Console 每秒一行 `[MatlabBridge] 已发 N 包 / 已收 M 包，MATLAB 连接 = 是`；
   MATLAB 图窗里三条曲线开始爬
5. 关掉 MATLAB 图窗即停止，数据自动存成 `twin_log_时间戳.csv`

**排查手册**：`docs/MATLAB联动方案.md` 第 8 节（含 `CS0101` 编译冲突、
`udpport` 在 R2025a 的 API 变更、防火墙与端口占用）。

---

## 目录结构

```
.
├── Assets/                      # 拷进 Unity 工程的脚本
│   ├── Editor/BTestSceneBuilder.cs
│   └── Scripts/
│       ├── Learn/               # ★ 逐课手写，当前实际使用
│       ├── B/                   # 早期模块脚手架（接口契约）
│       └── Shared/              # 正式版 UDP 桥（已被 Learn 版取代）
├── matlab/
│   └── unity_bridge.m           # ★ 孪生模型 + 实时曲线 + CSV 落盘
└── docs/                        # 文档与教程
```

> **Unity 工程本身不在此仓库**（体积大且含机器相关配置）。
> 本仓库保存的是**脚本、文档与通讯代码**——也就是可版本管理、可展示的部分。

---

## 文档索引

### 当前主线

| 文档 | 用途 |
|---|---|
| [`docs/一个人做完整项目-路线图.md`](docs/一个人做完整项目-路线图.md) | **范围裁剪后的 7 周计划**、砍功能顺序、答辩问题清单 |
| [`docs/Unity与C#入门-从零到能自己写.md`](docs/Unity与C%23入门-从零到能自己写.md) | **核心教材**：从"挂第一个脚本"到"状态机 + 数据面板 + 曲线图"，逐课可跑 |
| [`docs/MATLAB联动方案.md`](docs/MATLAB联动方案.md) | 数字孪生联动的完整方案、数据契约、常见坑排查 |
| [`docs/CoppeliaSim-Unity通讯方案.md`](docs/CoppeliaSim-Unity通讯方案.md) | 用 CoppeliaSim 替换物理体的备选路线 |

### 参考资料

| 文档 | 用途 |
|---|---|
| [`docs/Unity零基础第一次上手.md`](docs/Unity零基础第一次上手.md) | 补装编辑器 → 新建工程 → 认识界面（约 2 小时） |
| [`docs/学习指南-读懂这个项目.md`](docs/学习指南-读懂这个项目.md) | 五层认知地图、代码精读顺序、七个验证实验、二十道自测题 |
| [`docs/B模块作战手册.md`](docs/B模块作战手册.md) | 感知规划模块的原始设计 |
| [`docs/参考项目清单.md`](docs/参考项目清单.md) | 开源参考项目（**参考思路可以，整段复制不行**） |

---

## 提交记录

共 **29** 次提交，**28** 次带 `B:` 前缀 —— 用来标记个人贡献。

| 阶段 | 代表提交 |
|---|---|
| 环境与脚手架 | `新增自学通关路线`、`BTestSceneBuilder 编译错误修复`、`Unity 界面换成中文` |
| 感知规划模块 | `修复测试台遇障死锁（新增卡死检测与侧向绕行）` |
| 范围重排 | `新增「一个人做完整项目」路线图（6-7 周 / 40 人时版）` |
| 逐课教材 + 手写脚本 | `教材扩写到第7课`、`第10课补 11.8 节`、`新增第11课 —— 数据面板 HUD` |
| 数字孪生联动 | `新增 Learn 版 MatlabBridge.cs`、`修复 unity_bridge.m 在 MATLAB R2025a 上崩溃` |

完整记录：`git log --oneline`

---

## 已知限制

- 小车是**运动学**模型（直接改 `transform`），不做轮胎摩擦与悬挂仿真
- 避障是**单射线 + 三档油门**，不是路径规划（A\* / RRT 不在本课设范围内）
- 被堵超时会**放弃当前路标点**而非重新规划，这是设计取舍（见教材 11.8 节）
- 跨机部署需要放行 UDP 5005 / 5006 入站（见 MATLAB 方案文档第 9 节）
