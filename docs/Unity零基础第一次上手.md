# Unity 零基础第一次上手（照着点就行）

> 写给完全没用过 Unity 的人。全程约 2 小时（其中 30~60 分钟是等下载）。
> 每一步都写清楚"点哪里"和"你应该看到什么"。

---

## 你现在的状态（我已经帮你查过，2026-09-22 23:20）

| 检查项 | 结果 |
|---|---|
| Unity Hub | ✅ 已安装（`C:\Program Files\Unity Hub\Unity Hub.exe`） |
| Unity 编辑器 | ✅ **已装好**：`E:\新建文件夹 (8)\2022.3.62f3c1\Editor\Unity.exe`（2022.3.62f3c1 LTS） |
| 许可证 | ✅ 已激活，日志显示 `Successfully resolved entitlement details` |
| Unity 工程 | ✅ **已建好**：`E:\My project\`（3D 模板，含 SampleScene） |
| 项目脚本 | ✅ **已帮你复制进去**：`E:\My project\Assets\Scripts\` 和 `Assets\Editor\` |
| 代码编辑器 | ⚠️ 没检测到 VS / VS Code，写代码前建议装一个 |

> 我的 Unity 装在 E 盘，不在 C 盘——第一次我查错了位置，以这里为准。
> `C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1` 是个空目录，安装残留，不用管。

**结论：第 1、2、6 步都做完了，你直接从第 3 步往下走。**

---

## 第 1 步：确认能打开 Unity（1 分钟）

编辑器路径：`E:\新建文件夹 (8)\2022.3.62f3c1\Editor\Unity.exe`

日常打开方式：**开始菜单搜 Unity Hub → Projects → 点 `My project`**。
（别直接双击工程文件夹里的东西，统一从 Hub 进。）

> **第一次打开会慢**：日志显示首次加载花了 **237 秒**（约 4 分钟），因为它在建索引。
> 之后就快了（十几秒）。**加载期间界面会卡住不动，这是正常的，别强退。**

> **文件夹名字建议改一下**：`E:\My project` 和 `E:\新建文件夹 (8)` 这两个名字，前者带空格、后者是中文，
> 目前能跑，但以后接自动化工具（比如命令行编译、CI）容易出问题。
> 想改的话：关掉 Unity → 把文件夹改名/移到 `E:\UnityProjects\AutoCarDigitalTwin` → 在 Hub 里重新 Add 一次。

---

## 第 2 步：项目已经建好了（跳过）

已经有的工程：`E:\My project\`，用的是 Unity 自带的 **3D 模板**（`com.unity.template.3d-8.1.3`）。
里面已经有一个场景 `Assets/Scenes/SampleScene.unity`。

**如果你想自己再建一个**（练手用）：
1. Unity Hub 左侧点 **Projects** → 右上角 **New project**
2. 顶部选 **3D (Built-in Render Pipeline)**（2022.3 里显示为 **3D Core**）
3. Project name 填英文，Location 别用中文路径、别放桌面、别放 OneDrive
4. 点 **Create project**

---

## 第 3 步：认识界面（5 分钟）

打开后你会看到六个区域，**先只记这六个**：

| 区域 | 中文名 | 干什么用的 |
|---|---|---|
| 左上 | **Hierarchy 层级** | 场景里现在有哪些物体 |
| 中间 | **Scene 场景视图** | 在这里摆物体（编辑用） |
| 中间另一个标签 | **Game 游戏视图** | 按 Play 后"玩家看到的画面" |
| 右侧 | **Inspector 属性** | 选中物体后，在这里改它的参数 |
| 左下 | **Project 项目** | 所有文件都在这里 |
| 右下 | **Console 控制台** | **报错和提示都在这，你要经常看它** |

> 找不着某个面板：顶部菜单 **Window → General →** 里面有 Console / Hierarchy / Inspector / Project，点一下就回来了。

---

## 第 4 步：10 分钟熟悉操作（一定要动手，别只看）

### 4.1 转视角（在 Scene 视图里操作）

| 操作 | 效果 |
|---|---|
| 按住**鼠标右键** + 移动鼠标 | 转视角 |
| 按住右键 + **W A S D** | 前后左右飞 |
| **滚轮** | 拉近拉远 |
| 按住右键 + **Q / E** | 上下移动 |

### 4.2 建一个立方体

**Hierarchy 面板空白处右键 → 3D Object → Cube**

你会看到中间 Scene 视图里出现一个白色方块，Hierarchy 里多了一行 `Cube`。

### 4.3 移动它

1. 在 Hierarchy 里点一下 `Cube`（选中）
2. 按键盘 **W**（移动工具）—— 方块上出现三个箭头
3. 拖红色箭头（X 轴）、绿色箭头（Y 轴）、蓝色箭头（Z 轴）
4. 或者在右侧 **Inspector → Transform → Position** 里直接改数字，**这个更精确**

### 4.4 其他常用键

| 按键 | 作用 |
|---|---|
| **W** | 移动工具 |
| **E** | 旋转工具 |
| **R** | 缩放工具 |
| **F** | 聚焦到选中物体（找不着东西时按它） |
| **Ctrl + D** | 复制选中物体 |
| **Delete** | 删除选中物体 |
| **Ctrl + S** | **保存场景（非常重要！不保存关掉就没了）** |

> 练一练：建 3 个 Cube 摆成一排，然后用 `Ctrl+S` 保存场景。**养成随手 Ctrl+S 的习惯**，这是新手丢工作的第一大原因。

---

## 第 5 步：挂你的第一个脚本（15 分钟）

**这是"我写的代码怎么才能动起来"的关键一步。**

1. **Project 面板**空白处右键 → **Create → C# Script**
2. 命名 `HelloWorld`（**注意：Unity 里脚本名必须和类名完全一致，大小写都不能错**），回车
3. 双击它 —— 会打开 Visual Studio
4. 把里面的内容**全部删掉**，替换成：

```csharp
using UnityEngine;

public class HelloWorld : MonoBehaviour
{
    // Start 只在游戏开始时执行一次
    void Start()
    {
        Debug.Log("Unity 我来了！");
    }

    // Update 每一帧执行一次（约每秒 60 次）
    void Update()
    {
        transform.Rotate(0f, 30f * Time.deltaTime, 0f);   // 每秒转 30 度
    }
}
```

5. **Ctrl + S 保存**，切回 Unity（Unity 会自动编译，右下角有个小圈在转）
6. 在 Hierarchy 里选中 `Cube`，把 Project 里的 `HelloWorld` 脚本**拖到 Inspector 面板上**
   （或者 Inspector 底部 **Add Component** → 搜 `HelloWorld`）
7. 按 **▶**（工具栏中间的播放按钮）

**你应该看到**：Console 面板出现 `Unity 我来了！`，同时 Cube 在慢慢自转。

**再按一次 ▶ 停止**（运行中的修改不会保存，这是 Unity 的规矩）。

> 做到这里，你已经掌握了 Unity 最核心的循环：**建物体 → 挂脚本 → Play → 看到变化**。
> 后面所有东西都是这个循环的重复。

---

## 第 6 步：脚本已经帮你放好了（跳过，只需确认）

**2026-09-22 23:25 已帮你复制完成**，现在 `E:\My project\Assets\` 下是这样：

```
E:\My project\Assets\
├── Editor\
│   └── BTestSceneBuilder.cs      ← 一键搭场景的编辑器脚本
├── Scenes\
│   └── SampleScene.unity
└── Scripts\
    ├── B\                        ← 你的 6 个核心脚本
    │   ├── WaypointPath.cs
    │   ├── WaypointFollower.cs
    │   ├── FrontCarDetector.cs
    │   ├── PathVisualizer.cs
    │   ├── BModuleFacade.cs
    │   └── BTestRig.cs
    └── Shared\
        └── MatlabUdpBridge.cs    ← MATLAB 联动用，暂时不用管
```

### 你要做的只有一件事：切回 Unity，看 Console

Unity 检测到新文件会自动编译，**右下角有个小圆圈在转**。

- **没有红色** = 编译通过 ✅ 直接进第 7 步
- **有红色报错** = 把报错原文复制给我，我改

> ⚠️ `Editor` 文件夹的名字不能改，Unity 靠这个名字识别"这是编辑器专用脚本"。
>
> ⚠️ 以后想自己加脚本：**必须放在 `Assets\` 里**。放在工程外面 Unity 是看不见的。
>
> ⚠️ **不要直接把仓库文件夹当 Unity 工程打开**——仓库里没有 `ProjectSettings` 和 `Packages`，Unity 打不开。

---

## 第 7 步：一键搭场景，跑起来（5 分钟）

1. 顶部菜单 **Tools → B模块 → 1. 创建 Obstacle 与 Vehicle 层**
2. **Tools → B模块 → 2. 搭建 B 测试场景**
3. 按 **▶**

**你应该看到**：一个方块沿着 8 个青色小球组成的椭圆自己跑圈；路上有灰色障碍物，靠近时会减速停下。

Scene 视图里还能看到从车头射出的绿/红射线。按 **Ctrl+S** 保存场景，命名 `B_Test`。

> 这个场景**只用来测你自己的模块**，别去动 `Main` 场景（那是队友 C 负责的）。

---

## 第 8 步：快捷键速查（贴在显示器上）

| 按键 | 作用 |
|---|---|
| `W` / `E` / `R` | 移动 / 旋转 / 缩放工具 |
| `F` | 聚焦到选中物体 |
| `Ctrl + D` | 复制 |
| `Delete` | 删除 |
| `Ctrl + S` | **保存场景** |
| `Ctrl + Shift + S` | 另存为（换名字前先想清楚） |
| `Ctrl + P` | 播放 / 停止 |
| `Ctrl + Shift + P` | 暂停（**调试神器**，暂停后可以看 Inspector 里的数值） |
| 右键 + `WASD` | Scene 视图飞行 |

---

## 十个"我点了没反应"（新手必踩）

| 现象 | 原因 | 解决 |
|---|---|---|
| 双击脚本没反应 | 没装代码编辑器 | 回第 1 步勾上 Visual Studio 模块 |
| 改了代码没生效 | 没保存 / 编译报错 | VS 里 `Ctrl+S`，回 Unity 看 Console 有没有红色 |
| 按 ▶ 什么都没发生 | 场景没保存 / 有编译错误 | `Ctrl+S`，先清掉 Console 的红字 |
| 找不到刚建的物体 | 视角跑偏了 | 在 Hierarchy 里选中它，按 **F** |
| Scene 里能看到，Game 里看不到 | 摄像机没对着它 | 选中 Main Camera，按 F 看它在哪 |
| 方块一直往下掉 | 没有地面 | 建一个 Plane，或删掉物体的 Rigidbody |
| 面板不见了 | 不小心关了 | **Window → General →** 找回来 |
| `The type or namespace name 'XXX' could not be found` | 脚本没拷全 | 确认 `Assets/Scripts/B/` 和 `Assets/Scripts/Shared/` 都在 |
| Hub 里项目打不开 | 编辑器没装好 | 回第 1 步重装编辑器 |
| 中文显示成乱码 | 脚本编码不是 UTF-8 | 用 VS 另存为 UTF-8 |

---

## 学完这一轮，你已经会的

- [x] 打开 Unity、新建工程
- [x] 看懂界面六块区域
- [x] 转视角、建物体、移动旋转缩放
- [x] 写脚本、挂脚本、按 Play 看效果
- [x] 看 Console 报错
- [x] 把外部脚本导进工程
- [x] 跑起来一个完整的自动驾驶测试场景

**这就是第 0 周的全部内容。** 接下来按 [`B模块作战手册.md`](B模块作战手册.md) 走第 1 周。

---

## 如果卡住了

把下面三样发给我，基本一眼能定位：

1. **你点了什么**
2. **期望看到什么**
3. **实际看到什么**（Console 报错原文 / 截图）

> 别自己硬扛超过 20 分钟。Unity 的坑大多是"某个开关没开"，问一句 30 秒的事。
