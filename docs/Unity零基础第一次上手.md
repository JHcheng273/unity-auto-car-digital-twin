# Unity 零基础第一次上手（照着点就行）

> 写给完全没用过 Unity 的人。全程约 2 小时（其中 30~60 分钟是等下载）。
> 每一步都写清楚"点哪里"和"你应该看到什么"。

---

## 你现在的状态（我已经帮你查过，2026-09-22 23:26）

| 检查项 | 结果 |
|---|---|
| Unity Hub | ✅ 已安装（`C:\Program Files\Unity Hub\Unity Hub.exe`） |
| Unity 编辑器 | ✅ **已装好**：`E:\新建文件夹 (8)\2022.3.62f3c1\Editor\Unity.exe`（2022.3.62f3c1 LTS） |
| 许可证 | ✅ 已激活，日志显示 `Successfully resolved entitlement details` |
| Unity 工程 | ✅ **已建好**：`E:\My project\`（3D 模板，含 SampleScene） |
| 项目脚本 | ✅ **已帮你复制进去并编译通过**（8 个 .cs，日志零报错） |
| 中文语言包 | ✅ **已帮你装好**（官方 `language-zh-hans`，2.3 MB，见文末附录） |
| 代码编辑器 | ✅ **VS Code 1.139.0 已装好**（含 C# Dev Kit + Unity 扩展），只需在 Unity 里指一下（见文末） |

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

> ✅ **已知问题已修（2026-09-22 23:25）**：`BTestSceneBuilder.cs` 原来有两处编译错误
> （`ApplyModifications` 写错、`EditorSceneManager` 少了命名空间），导致 `Tools` 菜单里
> **看不到「B模块」**。已经改好并验证：`Assembly-CSharp-Editor.dll` 编译成功、日志零报错。
> 如果你还是看不到菜单，按 `Ctrl+R` 强制刷新一次，或重启 Unity。

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

## 附：把界面换成中文（语言包我已经帮你装好了）

**先说清楚三件事是分开的**，很多人搞混：

| 你想改的 | 开关在哪 |
|---|---|
| **Unity Hub** 的界面（项目列表那个窗口） | Hub → 设置 → Appearance → Language（**你已经是中文了**） |
| **打开项目后的 Unity 编辑器**（有 Scene / Hierarchy / Inspector 那个） | 需装语言包 + Edit → Preferences → Languages ← **你要的是这个** |
| 游戏运行时给玩家看的 UI | 和上面两个**毫无关系**，是项目代码里的事 |

> 已确认的事实（从你本机文件里读出来的，不是网上抄的）：
> - 语言包模块 ID：`language-zh-hans`，显示名「简体中文」，分类 `Language packs (Preview)`
> - 官方下载地址：`https://new-translate.cdn.unity.cn/v1/live/54/2022.3/zh-hans`（2,388,191 字节）
> - 你这版一共提供 4 种：简体中文 / 繁體中文 / 日本語 / 한국어
> - Unity Hub 支持 5 种语言（English / 日本語 / 한국어 / 简体中文 / 繁體中文），你的 Hub 已是 `zh_CN`

### ✅ 语言包：已装好（2026-09-23 23:08 修正）

```
E:\新建文件夹 (8)\2022.3.62f3c1\Editor\Data\Localization\
└── zh-hans.po     ← 2,388,191 字节，13,974 条翻译
```

校验过：字节数和 Hub 官方清单登记的**完全一致**，抽查 `"Cancel"→"取消"`、`"Edit"→"编辑"`、`"Window"→"窗口"`。

> ⚠️ **踩过的坑（记下来，别再犯）**：文件名**必须带 `.po` 后缀**。
> 我第一次照 Hub 下载链接（`.../2022.3/zh-hans`）猜成无后缀的 `zh-hans`，Unity 完全不认，
> Preferences → Languages 的下拉框里就**不会出现「简体中文」**。
> Unity 官方开发者社区明确写的就是 `zh-hans.po`。

> 注意：这个目录**本来不存在**，是新建的。想完全撤销 → 直接删掉 `Localization` 整个文件夹即可，不影响 Unity。

### 你现在只需要做这一步（在 Unity 编辑器里）

1. **先彻底关掉 Unity，再重新打开**（语言包是启动时才加载的，不重启读不到）
2. 顶部菜单 **Edit → Preferences**
3. 左侧列表选 **Languages**
4. 勾选 **Editor Language (Experimental)** ← **必须先勾这个**，不勾下拉框是灰的
5. 在 **Editor language** 下拉框里选 **简体中文**
6. **再重启一次 Unity** ← 不能省，不重启不生效

> ⚠️ **如果第 5 步的下拉框里没有「简体中文」**（99% 是文件名不对）：
> 1. 确认 `Localization` 里只有一个文件，名字**必须**是 `zh-hans.po`
> 2. 还是不行就走官方路线：Unity Hub → 安装 → `2022.3.62f3c1` → **齿轮 → 添加模块**
>    → **Language packs (Preview)** → 勾 **简体中文** → 安装
> 3. 装完回到上面的第 1~6 步
>
> 想彻底撤销：直接删掉 `Editor\Data\Localization` 整个文件夹，Unity 不受任何影响。

### 几个要提前知道的事

| 事项 | 说明 |
|---|---|
| 标签带 **(Experimental)** | 这是**实验性**功能，部分文字仍是英文，属正常现象，不是你没装好 |
| 菜单名会变 | 重启后 `Edit` 变「编辑」、`Window` 变「窗口」、`GameObject` 变「游戏对象」 |
| 队友的截图对不上 | 如果 C 用的是英文界面，你俩菜单名会不一样——**建议全组统一**，要么都中文要么都英文 |
| 想改回英文 | 同一个界面，取消勾选 `Editor Language`，重启即可（语言包不用卸载） |
| 代码里的报错 | **C# 报错信息默认还是英文**，这是另一套开关（`kEnableCompilerMessagesLocalization`），先别动它 |

> ⚠️ 注意：网上那些"Unity 汉化包""Unity 中文补丁"是**第三方改的**，来路不明、版本对不上就会崩。
> 上面是**官方**语言包，别去下别的东西。

### 选了「简体中文」但界面还是英文？（2026-09-25 实测排查）

先说结论：**你的设置其实已经存进去了**。我用你电脑上的真实记录确认过：

| 检查项 | 实际值 | 说明 |
|---|---|---|
| 语言包文件 | `E:\新建文件夹 (8)\2022.3.62f3c1\Editor\Data\Localization\zh-hans.po`（2.3 MB / 13974 条） | ✅ 位置、文件名都对 |
| 总开关 | `Editor.kEnableEditorLocalization = 1` | ✅ 已勾选 |
| 已选语言 | `Editor.kEditorLocale = ChineseSimplified` | ✅ 已选中简体中文 |

所以问题**不在设置**，而在下面三个坑，按概率从高到低：

**坑 1：Unity 根本没真正退出（最常见）**
语言包是**编辑器启动时才读一次**的。你可能只是关了窗口/切了场景，Unity 进程还在后台活着 —— 这时从 Hub 再点打开，会**复用旧进程**，语言包永远不重新加载。

自查：按 `Ctrl + Shift + Esc` 打开任务管理器 → 看「进程」里还有没有 `Unity.exe`。
有就说明没退干净 → 选中它 → **结束任务**，然后从 Hub 重新打开项目。

**坑 2：改完设置后没重启**
顺序必须是：勾选开关 → 选语言 → **重启**。改完立刻看界面一定是英文，这是正常的。

**坑 3：选成了「Chinese」而不是「Chinese Simplified」**
简体/繁体是两个不同的包。下拉框里要选带 **Simplified / 简体** 的那个。

**一次到位的正确顺序：**

1. Unity 顶部菜单 `File → Exit`（不是关窗口的红叉）
2. 任务管理器确认 `Unity.exe` 一个都不剩
3. 从 Unity Hub 重新打开 `My project`
4. `Edit → Preferences → Languages` 再看一眼：开关勾着、`Editor language = 简体中文`
5. 如果这里显示正确但界面还是英文 → 说明包没被识别，走下面的兜底

**兜底：让 Unity Hub 自己装（最省事，Hub 会放对文件名和位置）**

1. 打开 **Unity Hub** → 左侧 **安装 (Installs)**
2. 找到 `2022.3.62f3c1` → 点右边的 **齿轮图标** → **添加模块 (Add modules)**
3. 展开 **Language packs (Preview)** → 勾 **简体中文（Chinese Simplified）**
4. 点 **安装**，等它下载完
5. 装完 **重启 Unity 编辑器**

**一个心理预期：** 就算成功了，也**不是 100% 中文**。2022.3 的编辑器本地化是 **Experimental（实验性）**，菜单、Inspector、Project 窗口大部分会变中文，但一些老字符串、包名、你自己代码里的文字仍然是英文 —— 这是官方半成品，不是你没装好。

> 顺带一提：**Unity Hub 本身已经是中文了**（Hub 的语言配置是 `zh_CN`），它和编辑器是两套独立的语言设置，互不影响。

---

## 十个"我点了没反应"（新手必踩）

| 现象 | 原因 | 解决 |
|---|---|---|
| 双击脚本没反应 | Unity 不知道用哪个编辑器打开 | 按文末「附 2」把 External Script Editor 指到 VS Code |
| VS Code 里没有代码补全 | C# Dev Kit 还在初始化 / 没装扩展 | 等右下角进度走完；或 `Ctrl+Shift+X` 搜 `C# Dev Kit` 确认已启用 |
| 改了代码没生效 | 没保存 / 编译报错 | VS 里 `Ctrl+S`，回 Unity 看 Console 有没有红色 |
| 按 ▶ 什么都没发生 | 场景没保存 / 有编译错误 | `Ctrl+S`，先清掉 Console 的红字 |
| 找不到刚建的物体 | 视角跑偏了 | 在 Hierarchy 里选中它，按 **F** |
| Scene 里能看到，Game 里看不到 | 摄像机没对着它 | 选中 Main Camera，按 F 看它在哪 |
| 方块一直往下掉 | 没有地面 | 建一个 Plane，或删掉物体的 Rigidbody |
| 面板不见了 | 不小心关了 | **Window → General →** 找回来 |
| `The type or namespace name 'XXX' could not be found` | 脚本没拷全 | 确认 `Assets/Scripts/B/` 和 `Assets/Scripts/Shared/` 都在 |
| Hub 里项目打不开 | 编辑器没装好 | 回第 1 步重装编辑器 |
| 中文显示成乱码 | 脚本编码不是 UTF-8 | 用 VS 另存为 UTF-8 |
| 勾了 `Editor Language` 界面还是英文 | 没重启 / 下拉框里没选到语言 | **重启 Unity**；下拉框里选「简体中文」再重启 |
| `Preferences` 里找不到 `Languages` | 编辑器版本没带语言包机制 | 你这个版本有，确认是 `2022.3.62f3c1`，别用别的版本打开 |
| 下拉框里没有「简体中文」 | 语言包文件名和 Unity 期望的不一致 | 删掉 `Localization` 文件夹，改用 Unity Hub 的「添加模块」装（见文末附录） |

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

## 附 2：代码编辑器（VS Code）—— 不用装 Visual Studio

**结论：只用 VS Code 就够了。** Visual Studio 有 5 GB 以上、装的时候还要弹管理员权限窗口，没必要。
你工程里已经自带 `com.unity.ide.vscode@1.2.5` 这个包，Unity 官方支持 VS Code。

### 已经帮你装好的（2026-09-23 23:29）

| 项目 | 状态 |
|---|---|
| VS Code | ✅ **1.139.0**，装在 `C:\Users\cheng\AppData\Local\Programs\Microsoft VS Code\Code.exe` |
| 中文界面 | ✅ **简体中文语言包已装好**（`MS-CEINTL.vscode-language-pack-zh-hans` 1.131.0），`argv.json` 里已设 `"locale": "zh-cn"`，重启 VS Code 后即中文 |
| C# Dev Kit | ✅ `ms-dotnettools.csdevkit` v3.40.204 |
| C# | ✅ `ms-dotnettools.csharp` v2.160.4 |
| Unity | ✅ `visualstudiotoolsforunity.vstuc` v1.3.1（断点调试靠它） |

### 你要做的只有一件事：告诉 Unity 用 VS Code 打开脚本

1. Unity 顶部菜单 **Edit → Preferences**
2. 左侧选 **External Tools**（外部工具）
3. **External Script Editor** 下拉框 → 选 **Visual Studio Code**
4. 如果下拉框里没有 → 选 **Browse...**，手动指到这个文件：

```
C:\Users\cheng\AppData\Local\Programs\Microsoft VS Code\Code.exe
```

5. 关掉 Preferences。**在 Unity 里双击任何一个 `.cs` 文件，就会用 VS Code 打开**

### 界面换成中文（已经帮你装好了，2026-09-25 00:21）

简体中文语言包 `MS-CEINTL.vscode-language-pack-zh-hans`（1.131.0）已装进
`C:\Users\cheng\.vscode\extensions\ms-ceintl.vscode-language-pack-zh-hans-1.131.0`，
并且 `C:\Users\cheng\AppData\Roaming\Code\argv.json` 里已经写好 `"locale": "zh-cn"`。

**你要做的只有一件事：把 VS Code 整个关掉再打开**（关窗口不算，要在任务管理器里确认 `Code.exe` 一个不剩）。
重开后菜单就是「文件 / 编辑 / 查看 / 运行 / 终端 / 帮助」了。

> 如果没变成中文，手动切一次：`Ctrl+Shift+P` → 输入 `Configure Display Language`（中文界面下叫「配置显示语言」）→ 选 **中文(简体) zh-cn** → 重启。
> 想改回英文：同一个命令选 `en`，或直接把 `argv.json` 里的 `"locale"` 那一行删掉。

> ⚠️ **关于版本**：这台机器上的 VS Code 是 1.139.0，装到的语言包是 1.131.0，兼容范围是 `^1.131.0`，**能用**，但 1.131 之后新增的极少数菜单可能仍是英文 —— 不影响你写代码。
> （原因：微软应用市场在我这边连不上，改从 open-vsx 镜像装的；镜像版本会慢几拍。想要最新版就在 VS Code 扩展面板里搜 `Chinese` 再装一次即可覆盖。）

### 之后你会得到什么

- 写代码有**智能补全**（输入 `transform.` 会自动列出 `position` / `rotation` 等）
- 鼠标悬停能看到 Unity API 的说明
- **断点调试**：VS Code 左侧 Run and Debug → 选 `Unity Editor` → 按 F5 → 回 Unity 按 ▶
  （第一次可能要等几十秒，扩展要初始化）

### 常见状况

| 现象 | 怎么办 |
|---|---|
| 第一次打开很慢 | 正常，C# Dev Kit 在建索引，等右下角进度条走完 |
| 没有补全 / 全是红线 | 确认 Unity 里 External Script Editor 已指向 VS Code，然后重启两边 |
| `OmniSharp` 相关报错 | 一般是 Unity 没生成 `.sln` 文件，回 Unity 点 **Assets → Open C# Project** 让它重新生成 |
| 想换回 Visual Studio | 同一个下拉框改回去即可，VS Code 不用卸载 |
| 第一次打开 `E:\My project` 弹 "Unable to execute C# Dev Kit command / Some features execute code and can only run in a trusted workspace" | VS Code **Workspace Trust** 安全机制：陌生文件夹默认不执行代码。点弹窗里 **`Manage Workspace Trust`** → 输入框填 `E:\My project`（带空格）→ 下拉项点选。或者：设置里搜 `workspace trust enabled` 直接关掉（课设电脑 OK，共用机别关）。或者命令行 `code -r "E:\My project"` 直接以信任模式打开。 |
| 右下角弹 "The .NET SDK cannot be located / Error running dotnet --info" 以及右上角弹 "There were problems loading project HelloWorld.cs" | **同一根因**：VS Code 的 C# 扩展需要 .NET SDK 8 才能分析代码，Unity 自带的 Mono 不带 SDK。装一个：**Win+R → cmd** 粘 `winget install Microsoft.DotNet.SDK.8 --accept-package-agreements --accept-source-agreements --silent` 回车；或者浏览器下载 `dotnet-sdk-8.0.x-win-x64.exe` 双击装。装完 `dotnet --version` 看到 8.0.xxx，再**重启 VS Code**（`Developer: Reload Window`）即可恢复。**和 Unity 编辑器互不冲突**，Unity 不读这个 SDK。 |
| 系统装了 .NET SDK 8（`C:\Program Files\dotnet\` 下有 `sdk/`、`dotnet.exe`），VS Code 状态栏还在显示 "Downloading .NET..."，红框仍报"找不到 .NET SDK" | 这是 C# Dev Kit **绕开了系统 SDK、自己另下一个**——它默认去找 PATH 里的 `dotnet`，下载默认也是下自己的副本。在用户设置 `C:\Users\cheng\AppData\Roaming\Code\User\settings.json` 加一行 `"dotnet.dotnetPath": "C:\\Program Files\\dotnet\\dotnet.exe"` 指向系统那个，再 `Developer: Reload Window` 即可。"Downloading..." 自动取消，错误消失。 |
| 按 F5 弹"DebugType 不受支持。.NET 调试程序不支持当前的 DebugType 设置 'full'。若要启用调试，请将 DebugType 更改为 'portable'。是否仍要继续调试？" | 不是错误，是 VS Code 提示：Unity 自动生成的 `.csproj` 里 `DebugType=full`，VS Code 的 .NET 调试器只认 `portable`。**别去改 `.csproj`**（Unity 会重生成）。三个按钮：**点「不再显示」**（项目里写 `launch.json` 兼容，以后再按 F5 都不问）→ 按 F5 → 选 `Unity Editor` → 焦点回 Unity → 按 ▶ Play。 |
| 红框 "Assembly-CSharp.csproj 不支持调试。找不到可启动的目标" | F5 跑的不是 Unity 那条配置，而是 C# Dev Kit 自带的 `.NET Core Attach`。**在顶部下拉框（写着 `debug Any CPU` 那个）手动选一次 `Attach to Unity`** 再按 F5。选过一次 VS Code 会记住。 |
| launch.json 里写了 `"type":"unity"`，F5 报错"配置的类型 'unity' 不受支持" | ⭐ **真正的根因（2026-09-25 10:07 查证）**：`unity` 这个调试类型**根本不存在**。读 `visualstudiotoolsforunity.vstuc-1.3.1\package.json` 的 `contributes.debuggers` 可知，Unity 调试器注册的类型是 **`vstuc`**、请求方式是 **`attach`**（不支持 `launch`）。**正确配置见下方"Unity 调试的正确 launch.json"**。 |
| F5 报 **"Assembly-CSharp.csproj 不支持调试。找不到可启动的目标"（来源：C# 开发工具包）**，或下拉框显示 `debug Any CPU` | ⭐⭐ **2026-09-25 19:35 查明的最终根因**：`visualstudiotoolsforunity.vstuc` 扩展的**文件夹被删了、但 VS Code 的扩展登记表还认为它装着**（`C:\Users\cheng\.vscode\extensions\extensions.json` 里仍有它的记录，指向一个不存在的目录）。于是 `vstuc` 类型解析不了，VS Code 回退到 C# Dev Kit 的动态配置，去找控制台程序 → 报"找不到可启动的目标"。<br>**修法**：`Ctrl+Shift+X` → 搜 `visualstudiotoolsforunity.vstuc` → 显示 **Install** 就点装；**显示"已安装"就点齿轮 → Uninstall → 再 Install** → `Ctrl+Shift+P` → `Developer: Reload Window` → 下拉框选 `Attach to Unity` → F5。<br>**判断扩展是否真的在**：`ls "C:/Users/cheng/.vscode/extensions/"` 必须有 `visualstudiotoolsforunity.vstuc-*` 文件夹。**光看 extensions.json 不算**。 |
| 右下角通知 **"无法打开 E:\My project\My project.slnx。请确保 .NET SDK 版本为 3.0.200 或更高版本以支持 .slnx 文件"** | ⚠️ **`.slnx` 是 Unity 自己生成的，不能删**——`Library/PackageCache/com.unity.ide.visualstudio@2.0.28/Editor/` 里有 `SolutionParser.cs`、`SdkStyleProjectGeneration.cs` 专门生成它，删了 Unity 会重新生成。<br>**修法**：在 `E:\My project\.vscode\settings.json` 里加一行 **`"dotnet.defaultSolution": "My project.sln"`**，让 C# 扩展**用 `.sln` 而不是打不开的 `.slnx`**。<br>另外那条通知如果是**装 .NET 10 之前弹的**，属于过期缓存——点通知右上角的 **×** 关掉，再 `Developer: Reload Window` 就不出现了。 |
| 终端刷一堆 **`error CS0246: 未能找到类型或命名空间名"UnityEngine" / "MonoBehaviour"`**，最后 `终端进程已终止，退出代码: 4` | **这是预期现象，不是你的错。** C# Dev Kit 拿 `dotnet build` 去编译 Unity 生成的 `Assembly-CSharp.csproj`——但 **Unity 工程本来就不该用 dotnet CLI 编译**（脚本由 Unity 自带的 Mono 编译，UnityEngine 那些引用只有 Unity 内部才认）。<br>**它不影响 vstuc 附加调试**，可以无视。 |
| —— | —— |
| **Unity 调试的正确 launch.json**（照抄，别改） | `{"version":"0.2.0","configurations":[{"name":"Attach to Unity","type":"vstuc","request":"attach","projectPath":"${workspaceFolder}"}]}`。要点：① `type` 必须是 **`vstuc`** 不是 `unity`；② `request` 必须是 **`attach`** 不是 `launch`；③ 带 `projectPath`。**用法**：先开 Unity 并打开项目 → VS Code 按 F5 → 选 `Attach to Unity` → 顶部出橙色工具栏=已附加 → 回 Unity 按 ▶ Play。 |
| 扩展面板搜扩展报 **"提取扩展时出错。Failed to fetch"** | VS Code 连不上微软扩展市场。本机**开着系统代理 `127.0.0.1:10808`**，多半是代理软件没开或 VS Code 没走它。<br>**三条路**：① 打开代理软件；② VS Code 设置里加 `"http.proxy": "http://127.0.0.1:10808"`（10808 若是 SOCKS 要换成 HTTP 端口，如 10809）；③ **离线装 vsix**（下面那条）。 |
| **离线安装 vsix（本机已验证可用）** | 微软市场连不上，但**扩展 CDN 域名是通的**。用浏览器打开：<br>`https://visualstudiotoolsforunity.gallery.vsassets.io/_apis/public/gallery/publisher/visualstudiotoolsforunity/extension/vstuc/latest/assetbyname/Microsoft.VisualStudio.Services.VSIXPackage`<br>会下载到 `Microsoft.VisualStudio.Services.VSIXPackage`（约 7.5 MB，可改名 `vstuc.vsix`）→ 然后 `Win+R` → `cmd`：<br>`code --install-extension "%USERPROFILE%\Downloads\vstuc.vsix" --force`<br>⚠️ 注意：URL 里**必须用 `/latest/`**，用版本号 `/1.3.1/` 会失败；这个 CDN 不稳定，**失败就多重试几次**。 |
| Unity 官方扩展 `vstuc` 的硬性前提（2026-09-25 18:54 核对） | **① Unity 工程里必须有 `Visual Studio Editor` 包 ≥ 2.0.20**（不是 `Visual Studio Code Editor`，后者已废弃）。本工程 `Packages/manifest.json` 里已有 `"com.unity.ide.visualstudio": "2.0.28"` ✅ 满足。<br>**② .NET SDK**：本机装的是 **8.0.425**（`C:\Program Files\dotnet\`），已验证够用（10:11 那次成功连上 Unity、Unity 都弹了 Debug Mode 对话框）。**网上说必须要 .NET 10 的说法不适用于这台机器。** |
| launch.json 里只有 `Attach to Unity`，但 F5 仍报"找不到可启动的目标" | C# Dev Kit 会**自带注入**一条 `.NET Core Attach` 配置并**排在默认第一位**。F5 跑的是「当前下拉框选中的那项」，未必是你写的那条。**必须在顶部下拉框（写着 `debug Any CPU` 那个）手动选一次 `Attach to Unity`**，选过之后 VS Code 会记住。 |
| F5 接上后 Unity 弹"C# Debugger Attached：项目里 Debug Mode 没打开" | 这是 Unity 让你**开 Debug Mode**（编译时会带调试符号，体积稍大、性能略低，但能下断点）。两个按钮二选一：<br>① **Enable debugging for this session**（仅本次会话）⭐ 推荐<br>② **Enable debugging for all projects**（所有项目都开）<br>**别点 Cancel**。<br>点完 Unity 自动重新编译脚本，等右下角进度条走完即可。然后在 VS Code 代码行号左边点一下设红点断点 → Unity 按 ▶ Play → 跑到断点会停下。 |
| cmd 里 `code --install-extension ...` 报 `Client network socket disconnected before secure TLS connection was established` | 微软应用市场从本机连不上（环境问题，不是你的网）。**但不需要重装**——扩展都在且完好。真要重装走 open-vsx 镜像（见上文中文语言包那节的下载方式）。 |
| cmd 装扩展最后一行显示 `Extension 'xxx' is already installed` | **不是报错**，是"已经装好了"的确认。中间几行 `DEP0169 url.parse() DeprecationWarning` 是 Node.js 内部警告，忽略。 |

### External Tools 这一页每个选项到底是干嘛的（2026-09-25 加）

打开 **Edit → Preferences → External Tools**，从上到下分三大块。

#### ① 上半屏：生成哪些 Unity 配套项目文件（控制 `.csproj` / `.sln`）

工程根目录 `E:\My project\` 下那堆 `*.csproj`、`*.sln` 就是按这些勾选生成的，给 VS Code / Rider 用。

| 选项 | 含义 | 你的项目要不要勾 |
|---|---|---|
| Embedded packages | `Packages/` 内嵌包也生成项目文件 | ✅ **保持默认勾选** |
| Local packages | `Packages/manifest.json` 里 `file:...` 引用的本地包 | ✅ **保持默认勾选** |
| Registry packages | Unity Package Manager Registry 下载的包 | ⬜ 不勾（用不到） |
| Git packages | `git+url:` 引用的包 | ⬜ 不勾 |
| Built-in packages | Unity 自带的 `com.unity.*` 包 | ⬜ 不勾 |
| Local tarball | 本地 `.tgz` 包 | ⬜ 不勾 |
| Packages from unknown sources | 上面没涵盖的包源 | ⬜ 不勾 |
| Player projects | Build 输出的 Player 工程 | ⬜ 不勾 |

下面那个大按钮 **Regenerate project files**：改了 `Packages/manifest.json` 或发现 VS Code 找不到类型时，按一下。

#### ② 中段：图片 & 差异合并工具

| 选项 | 含义 | 推荐 |
|---|---|---|
| **Image application** | 双击图片用什么程序打开 | 保持 `Open by file extension`（用 Windows 默认关联程序） |
| **Revision Control Diff/Merge** | Git merge 冲突时用什么工具比对 | 课设保持默认就行；真要可视化 diff，见下面"方案 B" |

> 截图里那个黄色提示「No supported VCS diff tools were found」只是说"你没装 GUI diff 工具"，**不影响功能**，不影响写代码、不影响 Git 提交、不影响编译。可以无视。
>
> 想消掉有两条路：
> - **方案 A（推荐）**：保持默认不选——3 人课设改同一段代码几乎不会出冲突，VS Code 内部就能看 diff。
> - **方案 B**：下拉选 `Custom Tool` → Tool Path 填 `C:\Users\cheng\AppData\Local\Programs\Microsoft VS Code\Code.exe` → Two-way diff command line 填 `"C:\Users\cheng\AppData\Local\Programs\Microsoft VS Code\Code.exe" --diff "$(local)" "$(remote)"` → Merge arguments 填 `"C:\Users\cheng\AppData\Local\Programs\Microsoft VS Code\Code.exe" --merge "$1" "$2" "$3" -o "$4"`。**不建议搞**，你们用不到。

#### ③ 下半屏：External Script Editor（你截图没拍到，往下滑就是）

这一段就是上面「你要做的只有一件事」那 5 步操作的页面位置。

| 选项 | 你的值 |
|---|---|
| External Script Editor | `Visual Studio Code`（下拉直接选；或 Browse... 指到 `C:\Users\cheng\AppData\Local\Programs\Microsoft VS Code\Code.exe`） |
| Generate all .csproj files | ❌ 不勾（已经在上半屏的勾选里控制了，重复） |
| Additional arguments | 留空 |
| Editor attaching | `.unity` 文件用什么程序打开（保持默认即可） |

#### 改完怎么验证

回到 Unity → Project 窗口 → 找到 `Assets/Scripts/B/WaypointFollower.cs` → **双击它**。
应该弹出 VS Code、右上角有绿色 ▶、代码里有补全。弹错或空白就截图发我。

---

## 如果卡住了

把下面三样发给我，基本一眼能定位：

1. **你点了什么**
2. **期望看到什么**
3. **实际看到什么**（Console 报错原文 / 截图）

> 别自己硬扛超过 20 分钟。Unity 的坑大多是"某个开关没开"，问一句 30 秒的事。
