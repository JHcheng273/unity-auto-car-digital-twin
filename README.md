# Unity 自动驾驶小车数字孪生

三人团队 · 每人每周 6 小时 · 8 周 · 零硬件 · Unity 2022.3 LTS

---

## 分工与目录

| 成员 | 模块 | 代码位置 |
|---|---|---|
| A | 车辆与场景（WheelCollider、CarController、场景搭建） | `Assets/Scripts/A/` |
| **B** | **感知与规划（单射线检测、Waypoint、路径跟踪、可视化）** | **`Assets/Scripts/B/`** |
| C | 控制与可视化（速度控制、UI、报警、集成） | `Assets/Scripts/C/` |

- `Assets/Scripts/` — 每人一个子目录，**只改自己目录里的文件**
- `Assets/Scenes/Main.unity` — 主场景，**只有 C 能改**
- `Assets/Scenes/B_Test.unity` — B 的测试场景，随便改
- `docs/` — 文档、报告素材

---

## 接口契约（改名字前必须在群里说）

```csharp
// A 提供
carController.SetInput(float steering, float motor, float brake);

// B 提供（走 BModuleFacade，成员 C 只需要认这一个脚本）
bool  HasFrontCar
float FrontCarDistance     // 没检测到时返回 detectionRange，不会是 Infinity
int   CurrentWaypointIndex
float Steering             // -1 左满舵 ~ +1 右满舵
bool  NeedBrake
bool  NeedSlowDown

// C 负责
TelemetryPanel / 报警 / 主场景集成
```

---

## Git 工作流

### 分支

```
main    稳定版，只有 C 能往里合并
dev-A   A 的开发分支
dev-B   B 的开发分支
dev-C   C 的开发分支
```

```bash
git checkout dev-B          # 每天开工第一件事
git pull origin dev-B       # 拿到最新
# ... 干活 ...
git add .
git commit -m "B: 路径跟踪加入冲过头处理"
git push origin dev-B
```

### 三条铁律（不遵守就等着通宵解冲突）

1. **不碰别人的目录**。改了 A 的脚本？先问。
2. **不碰 `Main.unity`**。要测集成，自己建 `B_Test.unity`。
3. **周五必合并**。C 负责把 dev-A / dev-B / dev-C 合到 main，越晚越痛。

### 提交信息格式

```
A: 调整后轮摩擦力参数
B: FrontCarDetector 加入 Layer 过滤
C: TelemetryPanel 接入 BModuleFacade
```

前缀带字母，`git log` 一眼能看出谁干了什么——**这是你证明自己没摸鱼的证据**。

---

## 快速开始（成员 B，30 分钟跑起来）

1. Unity Hub 新建 **3D Core** 项目，版本 **2022.3 LTS**
2. 把 `Assets/Scripts/B/` 整个目录拷进项目的 `Assets/Scripts/` 下
3. 场景里建一个 Plane 当地面
4. 建空物体 `Path`，下面建 8 个子空物体 `WP0`~`WP7`，沿路摆开
5. `Path` 挂 `WaypointPath`，组件右键 → **收集子物体为路径点** → Scene 视图出现青色球
6. 建 Cube 命名 `B_TestCar`，挂 `WaypointFollower`（path 拖 Path）+ `BTestRig`（follower 拖自己）
7. 按 Play → 方块自己沿路径跑
8. 路上扔几个 Cube 当障碍（Layer 设为 `Obstacle`），车挂 `FrontCarDetector`，`detectLayers` 只勾 `Obstacle`
9. 空物体挂 `PathVisualizer` → 出现灰（已跑）/ 青（未跑）两条线

详细见 [`docs/B模块作战手册.md`](docs/B模块作战手册.md)

---

## 里程碑

| 周次 | 里程碑 | 验收标准 |
|---|---|---|
| 第 0 周 | 环境就绪 | 三人都能新建项目、挂脚本、按 Play |
| 第 2 周 | 车辆能手动开 | WASD 能动，不翻车 |
| 第 3 周 | 核心闭环跑通 | 自动沿路径走 + 遇障碍减速 + UI 有数据 |
| 第 4 周 | 可视化完整 | 路径、射线、UI、多视角 |
| 第 6 周 | 最终集成 | Main 场景完整演示 |
| 第 8 周 | 答辩 | 报告、PPT、视频齐全 |

---

## 开源参考

见 [`docs/参考项目清单.md`](docs/参考项目清单.md)。
**参考思路可以，整段复制代码不行**——报告里必须注明参考来源，答辩会问。

---

## 自学路线（单人做完整个项目）

见 [`docs/自学通关路线.md`](docs/自学通关路线.md)：十周计划表、十个必懂概念自测、
调试能力阶梯、卡住预案、单人版取舍顺序。
**核心判断：单人做需要 90~110 人时，按每周 10 小时 × 10 周规划。**

---

## MATLAB 数字孪生联动（可选扩展，第 5 周起）

Unity 当物理实体，MATLAB 当虚拟模型（算法层），中间一根 UDP 线双向传数据：

```
Unity ──状态(20Hz JSON)──▶ MATLAB
Unity ◀──期望速度/刹车──── MATLAB
```

- 方案与分工：[`docs/MATLAB联动方案.md`](docs/MATLAB联动方案.md)
- Unity 侧桥接：`Assets/Scripts/Shared/MatlabUdpBridge.cs`（归属 C，B 只喂数据）
- MATLAB 侧：`matlab/unity_bridge.m`（含实时曲线 + 自动存 CSV）

**前 6 周不要碰。** 核心闭环是 1，MATLAB 是后面的 0。
