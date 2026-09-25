# B 动手做项目 · 步骤清单

> 配套阅读：[`B模块作战手册.md`](B模块作战手册.md)（讲**为什么**这么做）
> 本文只回答一个问题：**现在手该点哪里。**

---

## 一、你现在在哪

| 环节 | 状态 |
|---|---|
| Unity 2022.3 + VS Code 调试 | 已通 |
| B 的 6 个脚本 | 已在 `Assets/Scripts/B/` |
| 一键搭场景工具 | 已在 `Assets/Editor/BTestSceneBuilder.cs` |
| **在 Unity 里真正跑起来** | **还没做过 —— 今天做这个** |

- Unity 工程路径：`E:\My project`
- 今天要新建的场景：`Assets/Scenes/B_Test.unity`
- 默认场景 `SampleScene` 里目前只有相机和灯光，**没有** B 的任何物体

---

## 第 1 步：新建自己的场景

**为什么不直接在 SampleScene 里点菜单？**
`SampleScene` 是 Unity 自带的示例场景，将来 A、C 合并代码时会拿它当基准。你在里面堆东西，合并时必冲突。作战手册的规矩是：**只在自己的 `B_Test` 场景里改**。

操作：

1. Unity 顶部菜单 `File` → `New Scene`（快捷键 `Ctrl + N`）
2. 弹窗里选 **`Basic (Built-in)`** → 点 `Create`
3. 菜单 `File` → `Save As...`（快捷键 `Ctrl + Shift + S`）
4. 在保存框里：
   - 左边先点中 `Assets` 下的 `Scenes` 文件夹
   - 文件名填 **`B_Test`**（不用自己加 `.unity`，Unity 会加）
   - 点 `Save`

**完成标志**：Project 窗口的 `Assets/Scenes` 下多出一个 `B_Test` 场景，Unity 标题栏显示 `B_Test`。

---

## 第 2 步：建两个 Layer

**为什么？** 射线如果不限定层，会打到地面和自己身上，结果是「一直报告前方有车、距离 0 米」。

操作：

1. 顶部菜单 `Tools` → `B模块` → 点 **`1. 创建 Obstacle 与 Vehicle 层`**
2. 打开 Console 看结果（菜单 `Window` → `General` → `Console`，快捷键 `Ctrl + Shift + C`）
   应该出现两行普通信息：
   ```
   [B模块] 已创建 Layer 8 = Obstacle
   [B模块] 已创建 Layer 9 = Vehicle
   ```

**验证**：`Edit` → `Project Settings` → 左侧 `Tags and Layers` → 展开 `Layers` → `User Layer 8` 应是 `Obstacle`，`User Layer 9` 应是 `Vehicle`。

**注意**：如果 Console 出现黄色警告「第 8 层已经被占用」，先截图发我，不要自己乱改层号。

---

## 第 3 步：一键搭出测试场景

1. 顶部菜单 `Tools` → `B模块` → 点 **`2. 搭建 B 测试场景（会清空同名旧物体）`**
2. Console 出现「[B模块] 场景搭好了！按 Play 试试。」

**完成标志**：Hierarchy（左上角物体列表）里出现：

```
Ground
Path
 ├ WP0
 ├ WP1
 ├ ...
 └ WP7
B_TestCar
Obstacles
 ├ Obstacle_正面
 └ Obstacle_侧边
PathViz
Directional Light
Main Camera
```

---

## 第 4 步：按 ▶ Play

1. 点窗口正上方那个 **▶** 三角按钮
2. 切到 `Game` 标签页看画面

**预期现象**：

- `B_TestCar` 这个方块自己沿着 8 个青色点组成的椭圆跑圈
- 跑到障碍物 Cube 前面会减速、停下
- 被挡住一会儿之后会绕开继续跑

再切到 `Scene` 标签页（点左上角 `Scene`），应该能看到：

- 8 个青色小球 + 把它们连起来的线
- 1 个黄色小球 = 车当前正在追的那个点
- 从车头射出去的一条线：前方无障碍是**绿色**，有障碍是**红色**

3. 再点一次 ▶ 停止（按钮会变回三角）

---

## 第 5 步：验收自查

- [ ] Console 里没有红色报错
- [ ] 方块能沿路径跑完整圈
- [ ] 遇到障碍物会减速/停下
- [ ] Scene 视图能看到射线颜色从绿变红

四项全打勾，**今天的活就干完了**——你的模块已经跑在 A 和 C 前面了。

---

## 二、出问题怎么办（按现象查）

| 现象 | 原因 | 怎么修 |
|---|---|---|
| 方块完全不动 | `Path` 引用没接上 | 选中 `B_TestCar` → Inspector → `Waypoint Follower` 组件 → 把 `Path` 拖进 `Path` 框 |
| 方块原地打转 | `steerGain` 太大 | `Waypoint Follower` → `Steer Gain` 从 2 改成 1.5 |
| 方块转不过弯、跑出圈 | `steerGain` 太小 | 改到 3 |
| 射线一直报「有前车」且距离是 0 | 打到自己车身了 | `Front Car Detector` → `Detect Layers` 只勾 `Obstacle` 和 `Vehicle`，别勾 `Default` |
| 射线打到地面 | 起点太低 | `Front Car Detector` → `Height Offset` 从 0.5 改到 1 |
| Scene 视图看不到球和线 | Gizmos 关了 | Scene 视图右上角点一下 `Gizmos` 按钮让它亮起 |
| Console 一堆黄色警告 | 不影响运行 | 先无视，**只有红色才要管** |
| 报 NullReferenceException | Inspector 里的引用没拖 | 双击 Console 里的红字会跳到出错那一行，回 Inspector 把框拖满 |
| 在终点附近抖动 | 到达半径太小 | 选中 `Path` → `Waypoint Path` → `Arrival Radius` 从 3 改到 4 |

**Console 是排查一切的入口**：红色 = 报错必须解决，黄色 = 警告可无视，白色 = 普通日志。

---

## 三、今天剩下的时间做什么

| 时间 | 做什么 |
|---|---|
| 30 分钟 | 上面 5 步，把场景跑起来 |
| 1 小时 | 读 `docs/学习指南-读懂这个项目.md`，对照 `WaypointFollower.cs` 看 `InverseTransformPoint` 和 `Atan2` 那 3 行 |
| 2 小时 | **改参数做实验**：把 `Steer Gain` 分别改成 0.5 / 2 / 5，各跑一次，记下现象。这就是报告里「参数整定表」的原始数据 |
| 1 小时 | 把 `Front Car Detector` 的 `Detection Range` 从 20 改到 10、5，看射线变红的位置怎么变 |
| 30 分钟 | 保存场景、停止 Play、提交代码（见下一节） |

**别跳过「改参数做实验」这一步。** 报告里老师最想看的就是「你试过哪些值、为什么选这个」，这段只能靠你自己跑出来。

---

## 四、代码怎么同步回仓库

Unity 工程在 `E:\My project`，git 仓库在 `C:\Users\cheng\WorkBuddy AI\2026-09-21-22-43-24`，两边是分开的。所以改完脚本要同步。

**目前的分工**：脚本基本由我（AI）来写，写完我会同时放进两边，你不用手动复制。

**如果你自己改了脚本**，告诉我一声，我帮你同步 + 提交。

**提交规范**（项目约定，用来证明个人贡献）：

```
B: 简短的改动说明
```

比如 `B: 新增 B_Test 测试场景，路径跟踪参数整定`。

---

## 五、接下来 3 步（先别急着做，跑通今天再说）

1. **换真车**：等 A 把车做好，把 `B_TestCar` 换成真车，`WaypointFollower` / `FrontCarDetector` 直接挂到真车上
2. **交给 C**：`B_TestCar` 上已经挂了 `BModuleFacade`，把 `Detector` 和 `Follower` 拖进它的两个框，然后告诉 C「可以读我的 `Steering` / `NeedBrake` 了」
3. **加可视化**：给 `FrontCarDetector` 的 `Beam Renderer` 拖一个 `LineRenderer`，这样 Game 视图也能看到射线

---

## 六、每天的收尾动作（养成习惯）

1. `Ctrl + S` 保存场景（Unity 标题栏没有 `*` 号 = 已保存）
2. 停止 Play —— **别在 Play 状态下改场景，改完一停止全部还原，白改**
3. 把当天做的事发群里：本周做完什么 / 下周做什么 / 卡在哪
