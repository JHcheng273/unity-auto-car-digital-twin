# 成员 B（感知与规划）零基础作战手册

> 配套脚本在 `Assets/Scripts/B/` 目录，直接拖进 Unity 就能用。
> 开源参考见 [`参考项目清单.md`](参考项目清单.md)。
> 这份手册的目标：**让你第 1 周结束就有能跑的东西**，而不是看两周教程。

---

## 0. 先说结论：B 其实是三人里最爽的位置

| 模块 | 依赖谁 | 被谁依赖 | 上手难度 |
|---|---|---|---|
| A 车辆场景 | 不依赖人 | 被 B、C 依赖 | ★★★★ WheelCollider 调参是玄学 |
| **B 感知规划** | **几乎不依赖人** | 被 C 依赖 | ★★ 几个数学函数 + 一条射线 |
| C 控制可视化 | 依赖 A 和 B | 集成压力最大 | ★★★ |

**B 的核心优势：不依赖 A 的车也能测。**
射线打到 Cube 就是检测成功，方块沿路径点走就是跟踪成功。A 的车卡住了？你照样推进度。
这是你最大的筹码——**别人被卡住的时候，你在出成果**。

你的三个交付物，翻译成人话：

1. **前车检测** = 从车头往前射一条看不见的线，撞到东西就报告距离
2. **Waypoint** = 在路上摆 8 个隐形的点当"路标"
3. **路径跟踪** = 每帧算一次"我该往左打还是往右打"，输出 -1 ~ +1

就这三件事。没有物理，没有美术，没有渲染管线。

---

## 1. 接口契约（先定死，能省掉一半扯皮）

你在第 2 周结束前，必须让 C 能拿到这些。**属性名一个字都不要改**：

```csharp
// ===== B 提供（通过 BModuleFacade 暴露）=====
bool  HasFrontCar          // 前方是否有车/障碍
float FrontCarDistance     // 前车距离（米），没检测到时返回 detectionRange，不会是 Infinity
int   CurrentWaypointIndex // 当前在追第几个点
float Steering             // -1 左满舵 ~ +1 右满舵
bool  ReachedDestination   // 是否跑完（不循环时）
bool  NeedBrake            // 距离 < safeDistance，该刹车了
bool  NeedSlowDown         // 距离 < slowDistance，该减速了

// ===== C 负责（你不用管）=====
carController.SetInput(steering, motor, brake)
```

**把 `BModuleFacade.cs` 挂到车上，把 detector 和 follower 拖进去，你的活就交出去了。**
C 之后只需要 `bModule.Steering`、`bModule.NeedBrake`。你内部算法怎么改，C 都不用动。

---

## 2. 第 0 周：6 小时怎么花（超具体）

别看 Unity 教程视频，直接照做：

| 时间 | 做什么 | 完成标志 |
|---|---|---|
| 1h | 装 Unity Hub + **Unity 2022.3 LTS**（和 A、C 版本必须一致！） | 能新建项目 |
| 0.5h | 装 Visual Studio 2022 Community（装 Unity 时勾选就行） | 双击 .cs 能打开 VS |
| 1.5h | 认识界面：Hierarchy（左边物体列表）、Inspector（右边属性）、Scene/Game（中间）、Project（下面文件） | 能说出这四块叫啥 |
| 1.5h | 建 Cube → 加 Rigidbody → 按 Play → 它掉下去 | **看到方块掉下去** |
| 1.5h | 新建 C# 脚本，写 `void Update(){ transform.Translate(Vector3.forward*0.01f); }` 挂到 Cube 上 | **方块自己往前爬** |
| 0.5h | `git clone`，`git checkout -b dev-B` | 有自己的分支 |

**第 0 周唯一目标：你会"建物体 → 挂脚本 → 按 Play → 看到变化"这个循环。**
这个循环会了，后面所有东西都是它的重复。

> Roll-a-Ball 官方教程做不完别慌，做到"小球能滚"就够了。你的模块用不上碰撞和计分。

---

## 3. 逐周任务（B 视角，只列你自己的）

### 第 1 周：射线 + 摆点（6h）

| 时间 | 任务 | 具体动作 |
|---|---|---|
| 2h | 学 Raycast | 新建 Cube 当障碍，写个脚本 `Physics.Raycast(origin, dir, out hit, 20f)`，`Debug.Log(hit.distance)`。让一个 Cube 朝另一个 Cube 射，看到 Console 打出数字就算成功 |
| 2h | 摆 Waypoint | 在 A 的路上放 8 个空物体（Create > Create Empty），命名 `WP0`~`WP7`，全部拖到一个叫 `Path` 的空物体下面 |
| 1h | 挂脚本 | `WaypointPath.cs` 挂到 `Path` 上，右键组件 → "收集子物体为路径点"。Scene 视图应该出现青色球和连线 |
| 1h | 学 C# 补基础 | 只看四个东西：**变量/属性**、`Vector3`、`Transform.position/forward`、`if`。够用了 |

**本周验收：Scene 视图里能看到 8 个青色球用线连起来。**

### 第 2 周：两个核心脚本（6h）

| 时间 | 任务 |
|---|---|
| 3h | 挂 `FrontCarDetector.cs`。**先在测试方块上跑通**，别等 A 的车 |
| 3h | 挂 `WaypointFollower.cs` + `BTestRig.cs`，方块开始沿路径跑 |

调试顺序（很重要，别跳）：
1. 方块不动 → 检查 `path` 拖了没、WP 数量 > 0
2. 方块原地打转 → `steerGain` 调小
3. 方块转不过弯 → `steerGain` 调大（2 → 3）
4. 方块在终点抽搐 → `arrivalRadius` 调大一点（3 → 4）

**本周验收：按 Play，方块自己沿 8 个点跑圈，遇到障碍 Cube 会停下。**

### 第 3 周：让它稳（6h）

- **必须做**：建两个 Layer（Layer 8 = `Obstacle`，Layer 9 = `Vehicle`），`detectLayers` 只勾这两个
- 处理三件事：到终点怎么办（循环 or 停）、冲过目标点怎么办（脚本已处理）、车翻了怎么办（不管，A 的活）
- 把 `BModuleFacade.cs` 挂上，发给 C

**本周验收：C 说"我能拿到你的 Steering 和 NeedBrake 了"。**

### 第 4 周：可视化（6h）

- 挂 `PathVisualizer.cs`（灰=已跑，青=未跑）
- 给 `FrontCarDetector` 拖一个 LineRenderer 进 `beamRenderer`，这样 Game 视图也能看到射线
- 射线颜色：无障碍绿色，有障碍红色（脚本已实现）

### 第 5-6 周：稳定性 + 集成

- 射线频率从 30 降到 10（够用，省性能）
- 车冲出道路：把 `follower.ResetToStart()` 暴露给 C 调用
- **不要碰 Main 场景**，只在自己的 `B_Test` 场景里改，改好让 C 合并

### 第 7-8 周：报告（你写这两章）

报告里你负责的部分，按这个结构写，老师爱看：

1. **传感器模型**：为什么用射线而不用碰撞体（射线是主动感知、可预测、性能高，真实激光雷达的简化）
2. **几何原理**：贴一张你自己画的图——车头射线、夹角 θ、距离 d
3. **路径跟踪算法**：贴核心代码 10 行 + 解释 `InverseTransformPoint` 和 `Atan2`
4. **参数整定表**：`arrivalRadius / steerGain / detectionRange` 各试了哪些值、为什么选这个
5. **实验**：不同障碍距离下的响应表格（这就是你的数据，别嫌土）
6. **局限**：单射线看不到斜前方、看不到静止弯道外的车 → 引出未来改进（多射线 / A*）

---

## 4. Unity 操作速查（照着点就行）

```
新建 Layer:   Edit > Project Settings > Tags and Layers > Layers > User Layer 8 填 Obstacle
物体设 Layer:  选中物体 > Inspector 右上角 Layer 下拉
建空物体:     Hierarchy 右键 > Create Empty  （或 GameObject > Create Empty）
建路径点:     Create Empty 后改名为 WP0，按 Ctrl+D 复制，改名 WP1...WP7
加 LineRenderer: 选中物体 > Inspector 最下面 Add Component > 搜 Line Renderer
看 Console:   Window > General > Console   （报错都在这儿）
显示 Gizmos:  Scene 视图右上角 Gizmos 按钮必须亮着，否则射线/路径点看不见
```

---

## 5. 新手 100% 会踩的坑（按出现频率排序）

| # | 现象 | 原因 | 解决 |
|---|---|---|---|
| 1 | 射线一直说"有前车"，距离是 0 | 打到自己车身了 | `detectLayers` 只勾 Obstacle/Vehicle，别勾 Default |
| 2 | 射线打到地面 | 起点太低 | `heightOffset` 调到 0.5 |
| 3 | 路径点看不见 | Gizmos 关了 | Scene 视图右上角点亮 Gizmos |
| 4 | Game 视图看不到射线 | Debug.DrawLine 只在 Scene 显示 | 给 `beamRenderer` 拖一个 LineRenderer |
| 5 | 线是紫红色 | 材质丢了 | 给 LineRenderer 手动拖个材质 |
| 6 | 报 NullReferenceException | Inspector 里的引用没拖 | Console 双击报错会跳到那一行，回去拖引用 |
| 7 | 方块在终点抖 | 到达半径太小，反复切换 | `arrivalRadius` 调大 |
| 8 | 转弯画龙 | steerGain 太大 | 2 → 1.5 慢慢试 |
| 9 | 改了代码没生效 | 没保存 / 编译报错 | VS 里 Ctrl+S，回 Unity 看 Console 有没有红色 |
| 10 | Console 一堆黄警告 | 不影响运行 | 忽略，先跑起来再说 |

---

## 6. 求助 AI 的正确姿势

别问"我的代码错了怎么办"。要贴四样东西：

```
【现象】按 Play 后方块不动，没有报错
【代码】（整个 .cs 文件贴出来，不要截图）
【场景层级】（Hierarchy 截图，或者手写：
    Path
      ├─ WP0
      ├─ WP1
    B_TestCar (挂了 WaypointFollower, BTestRig)
      Inspector: path = Path, arrivalRadius = 3）
【期望 vs 实际】期望沿路径走，实际原地不动
```

**90% 的问题，AI 看一眼就能定位。** 剩下的 10% 通常是 Inspector 里没拖引用。

---

## 7. 脚本清单

| 文件 | 挂在哪 | 作用 |
|---|---|---|
| `WaypointPath.cs` | 路径点父物体 `Path` | 存 WP 列表，Scene 里画球和线 |
| `WaypointFollower.cs` | 车（或测试方块） | 算 Steering，切换到下一个点 |
| `FrontCarDetector.cs` | 车（或车头空物体） | 单射线检测，输出距离 |
| `BModuleFacade.cs` | 车（和上面两个同级） | **给 C 的唯一接口** |
| `PathVisualizer.cs` | 任意空物体 | 已行驶灰色 / 未行驶青色 |
| `BTestRig.cs` | 测试方块 | 自测用，最后**删掉或禁用** |

### 最快跑起来的顺序（30 分钟）

1. 建 Plane 当地面，建 8 个空物体 WP0~WP7 摆成一圈，都拖到 `Path` 下
2. `Path` 挂 `WaypointPath`，右键组件 → 收集路径点 → 看到青色球
3. 建 Cube 叫 `B_TestCar`，挂 `WaypointFollower`（path 拖 Path）+ `BTestRig`（follower 拖自己）
4. 按 Play → **方块开始跑圈**
5. 路上扔几个 Cube 当障碍 → 挂 `FrontCarDetector`，`detectLayers` 只勾 Obstacle → 方块会停
6. 空物体挂 `PathVisualizer`（path、follower 拖进去）→ 出现灰/青两条线

---

## 8. 每周自查（周五合并前过一遍）

- [ ] 我的分支只有 `dev-B`，只改自己的脚本和 `B_Test` 场景
- [ ] Console 里没有红色报错
- [ ] 按 Play 能连跑 2 分钟不崩
- [ ] `BModuleFacade` 的属性名没改过
- [ ] 在群里发了：本周做完什么 / 下周做什么 / 卡在哪

---

## 最后一句话

你的模块**不需要等任何人**。第 1 周就把方块跑起来，你就已经领先进度了。
A 的车卡 WheelCollider 的时候，你已经在调路径跟踪的参数了。这就是 B 的打法。
