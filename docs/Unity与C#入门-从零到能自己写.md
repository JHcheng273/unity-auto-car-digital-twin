# Unity + C# 入门：从零到能自己写

> 载体就是你这个数字孪生项目。**不做脱离项目的教程** —— 那种东西学完就忘。
> 每一课都是你项目里的一个零件，学到第 10 课，你手里就有自己写的 `CarController` 和 `AutoDriver`。

---

## 一、为什么这么安排

很多人学 Unity 是这样：看 20 小时教程 → 跟着做 Roll-a-Ball → 关掉 → 全忘了。

**因为学的和要用的没关系。**

换个方式：每学一个概念，立刻用在你的项目上。你会清楚地知道"我为什么要学这个"。

| 课 | 学什么 | 你写完的代码能干什么 | 对应项目里的哪个脚本 |
|---|---|---|---|
| **1** | 挂脚本、`Update`、`Time.deltaTime` | 方块自己转起来 | — |
| **2** | 变量、`public`、`Vector3` | 方块自己往前爬 | — |
| **3** | `Input`、`if` 判断 | **WASD 手动开车** | 以后 `CarController` 的手动模式 |
| **4** | 向量运算、距离 | 朝一个目标点开 | `WaypointFollower` 的第一步 |
| **5** | `Quaternion`、插值 | **平滑转向目标点** | `WaypointFollower` 的核心 |
| **6** | `GetComponent`、脚本之间传数据 | 读另一个脚本的数据 | `BModuleFacade` 的原理 |
| **7** | 数组、`List`、`for` 循环 | 把 8 个路标点连成一条路径 | `WaypointPath` 的原理 |
| **8** | 射线 `Raycast` | 测前方障碍物的距离 | `FrontCarDetector` 的原理 |
| **9** | 计时器、简单状态机 | 遇障碍减速停车 | `AutoDriver` 的雏形 |
| **10** | 综合 | **自己写的车跑完一整圈** | `CarController` + `AutoDriver` |

**不要跳课。** 每一课都依赖前一课。

---

## 二、第 1 课：让方块自己转起来

**这一课的目标**：你自己写的第一段代码，挂到方块上，按 ▶ 看到它转起来。

### 1.1 先认清 Unity 界面（五块）

打开 Unity，你会看到五个区域。**记住它们的名字**，后面我提到"去 Inspector 里改"你就知道在哪：

| 区域 | 位置 | 是什么 |
|---|---|---|
| **Hierarchy**（层级） | 左侧 | 当前场景里**有哪些物体** |
| **Scene**（场景） | 中间 | **3D 编辑视图**，你在这里摆东西 |
| **Game**（游戏） | 中间（和 Scene 同位置） | **摄像机看到的画面**，就是玩家看到的 |
| **Inspector**（检视器） | 右侧 | 选中物体后，它的**所有属性** |
| **Project**（项目） | 底部 | 磁盘上的**所有文件**（脚本、场景、模型） |

Scene 和 Game 是**同一个位置的两个标签页**，点左上角的 `Scene` / `Game` 切换。

### 1.2 建一个方块

1. Hierarchy 空白处 **右键** → `3D Object` → `Cube`
2. 方块出现在 Scene 里，Hierarchy 里多了一项 `Cube`
3. 点中 Hierarchy 里的 `Cube`，右边 Inspector 会显示它的属性

**关键理解**：Hierarchy 里的每一项叫 **GameObject（游戏物体）**，Inspector 里的每一块叫 **Component（组件）**。

一个 GameObject 本身是**空的**，它之所以是个方块，是因为挂了 `Mesh Renderer`（管显示）和 `Box Collider`（管碰撞）这些**组件**。

> **这是 Unity 最重要的心智模型**：*物体是壳，组件是能力*。
> 后面你写脚本，本质就是**写一个新组件，挂到物体上**。

### 1.3 写你的第一个脚本

1. 在 Project 窗口（底部），点进 `Assets` 文件夹
2. **右键** → `Create` → `C# Script`
3. 名字改成 **`MyFirstCar`**（注意：**首字母大写、不要有空格**，回车确认）
4. **双击它**，VS Code 会打开

删掉里面所有内容，替换成这段：

```csharp
using UnityEngine;

public class MyFirstCar : MonoBehaviour
{
    public float turnSpeed = 30f;

    void Start()
    {
        Debug.Log("脚本启动了！");
    }

    void Update()
    {
        transform.Rotate(0f, turnSpeed * Time.deltaTime, 0f);
    }
}
```

按 `Ctrl + S` 保存。

### 1.4 挂上去

回到 Unity（**等右下角转圈结束**，它在编译你的代码）。

1. 把 Project 窗口里的 `MyFirstCar` 脚本**拖到** Hierarchy 里的 `Cube` 上
2. 或者：选中 `Cube` → Inspector 最下面 `Add Component` → 搜 `My First Car`

**成功标志**：Inspector 里出现 `My First Car` 这一块，里面有个 `Turn Speed` 输入框，值是 30。

> **为什么 Inspector 里能看到 `turnSpeed`？**
> 因为它是 `public` 的。**public = 公开，会在 Inspector 里露出来**，你可以不改代码就调它。这是 Unity 最方便的设计之一。

### 1.5 按 ▶ Play

点窗口正上方的 **▶** 三角按钮。

**你会看到**：
- Scene 和 Game 视图里，方块开始**绕自己的竖轴旋转**
- Console 里出现一行 `脚本启动了！`

再点一次 ▶ 停止。

> **注意**：Play 模式下改的东西，**停止后全部还原**。这是新手最容易白干活的地方。

### 1.6 逐行讲解

```csharp
using UnityEngine;          // ① 引入 Unity 的工具箱
```
没有这行，你就用不了 `transform`、`Debug` 这些 Unity 的东西。

```csharp
public class MyFirstCar : MonoBehaviour
```
- `class MyFirstCar` = 定义一个新类，**名字必须和文件名一模一样**
- `: MonoBehaviour` = **继承 MonoBehaviour**。这是"能挂到物体上"的门票——不继承它，Unity 不认。

```csharp
    public float turnSpeed = 30f;
```
- `float` = 小数类型（`30f` 的 `f` 表示这是个 float，不写会报错）
- `public` = 在 Inspector 里露出来

```csharp
    void Start() { ... }
```
**只在游戏开始那一刻执行一次**。适合做初始化。

```csharp
    void Update() { ... }
```
**每一帧都执行一次**（60 帧/秒的游戏里，一秒执行 60 次）。**所有"持续变化"的事都写在这里。**

```csharp
        transform.Rotate(0f, turnSpeed * Time.deltaTime, 0f);
```
拆开看：
- `transform` = **这个脚本所在物体自己的 Transform 组件**（不用你去查找，Unity 自动给）
- `Rotate(x, y, z)` = 绕三个轴转，单位是**度**。这里只给了 y（竖轴），所以是**原地转圈**
- `turnSpeed * Time.deltaTime` = 每秒转 30 度

**`Time.deltaTime` 是什么？为什么必须乘？**
它 = **上一帧花了多少秒**（60 帧时约 0.0167 秒）。

不乘它的话：`Rotate(0, 30, 0)` 意思是"**每帧**转 30 度" → 60 帧就是每秒 1800 度。
乘上之后：`30 × 0.0167 = 0.5 度/帧` → 60 帧正好每秒 30 度。

**结论：只要涉及"每秒多少"，就必须乘 `Time.deltaTime`**，否则你的游戏在快电脑上跑得飞快、慢电脑上像蜗牛。

### 1.7 三个小实验（一定要做）

改完记得 `Ctrl + S`，回 Unity 等编译，再 Play。

**实验 1：改速度**
把 `30f` 改成 `180f`。方块应该转得快 6 倍。→ 体会"参数控制行为"。

**实验 2：删掉 `Time.deltaTime`**
```csharp
transform.Rotate(0f, turnSpeed * 1f, 0f);   // 故意不乘 deltaTime
```
Play 看现象，然后在 Console 里观察——你会发现问题：**转速和帧率绑定了**。
再按 `Ctrl + Shift + P`... 不对，是在 Unity 里 `Window` → `Analysis` → `Profiler` 看帧率，或者简单点：**把 Game 视图的 `VSync` 关掉看转速变化**。

这个实验的结论要记住一辈子：**忘了乘 deltaTime，是所有 Unity 新手最常见的 bug**。

**实验 3：让它往前爬**
把 `Update` 里那行改成：
```csharp
transform.Translate(0f, 0f, 1f * Time.deltaTime);
```
方块会沿着自己的 **+z（蓝轴）** 慢慢往前爬。→ 这就是"车往前走"的最原始形态，第 2 课我们会让它快起来。

---

## 三、第 2 课：让方块自己往前爬（速度可调）

**这一课的目标**：把第 1 课实验 3 里那个写死的 `2f`，变成一个**能在 Inspector 里随时改的速度**，并且搞明白 `Vector3` 是什么。

你现在的代码应该长这样（实验 3 留下的）：

```csharp
transform.Translate(0f, 0f, 2f * Time.deltaTime);
```

`2f` 是写死的。想改速度就得改代码、等编译。这一课我们把它变成可调的。

### 2.1 变量：给数字起个名字

C# 里声明变量的格式是：

```
类型 名字 = 初始值;
```

| 类型 | 装什么 | 例子 |
|---|---|---|
| `float` | 小数 | `float speed = 2.5f;` |
| `int` | 整数 | `int lapCount = 0;` |
| `bool` | 真 / 假 | `bool isBraking = false;` |
| `string` | 文字 | `string carName = "小蓝";` |

**两个新手必踩的坑**：
1. `float` 后面的 `f` 不能省 —— `2.5` 默认是 `double`（精度更高的另一种小数类型），Unity 只认 `float`，不写 `f` 直接报错
2. 名字不能用中文、不能有空格、不能以数字开头（`forwardSpeed` 这种驼峰写法是通用惯例）

### 2.2 `public` 的魔法

在变量前面加 `public`，它就会**出现在 Inspector 里**：

```csharp
public float forwardSpeed = 2f;
```

好处是：**调参不用改代码、不用等编译**。Play 的时候直接拖 Inspector 里的数字，方块立刻变速 —— 这是 Unity 做调试最爽的地方，也是你以后做数字孪生调参的主要手段。

不加 `public`（也就是 `private`）就只能在代码里改，Inspector 里看不到。

### 2.3 `Vector3`：三个数打包成一个

`Translate(0f, 0f, 2f)` 要写三个数很啰嗦。Unity 把它们打包成一个类型，叫 `Vector3`：

```csharp
Vector3 dir = new Vector3(0f, 0f, 1f);
```

`Vector3` 就是**一个装 x、y、z 三个小数的盒子**。`dir.x` 取 x，`dir.y` 取 y，`dir.z` 取 z。

**方向速查表**（记住这张表，后面天天用）：

| `Vector3` | 方向 | 对应轴的颜色 |
|---|---|---|
| `(0, 0, 1)` | 前 | 蓝 |
| `(0, 0, -1)` | 后 | 蓝 |
| `(1, 0, 0)` | 右 | 红 |
| `(-1, 0, 0)` | 左 | 红 |
| `(0, 1, 0)` | 上 | 绿 |
| `(0, -1, 0)` | 下 | 绿 |

> Unity 里 **红 = x 轴，绿 = y 轴，蓝 = z 轴**。你之前看到方块往前爬，就是沿着蓝轴走的。

### 2.4 把代码改成这样

打开 `MyFirstCar.cs`，整段替换成：

```csharp
using UnityEngine;

public class MyFirstCar : MonoBehaviour
{
    [Header("速度设置")]
    public float forwardSpeed = 2f;

    [Header("方向（1 = 正方向，-1 = 反方向）")]
    public Vector3 moveDirection = new Vector3(0f, 0f, 1f);

    void Start()
    {
        Debug.Log("车启动了，速度 = " + forwardSpeed);
    }

    void Update()
    {
        // 通用公式：方向 × 速度 × 每帧时间
        transform.Translate(moveDirection * forwardSpeed * Time.deltaTime);
    }
}
```

`Ctrl + S` 保存，回 Unity 等编译。

**三个新东西**：

- `[Header("...")]` —— 在 Inspector 里加一个**分组标题**。纯装饰，但面板会好看很多，以后参数多了必备
- `moveDirection * forwardSpeed` —— **向量乘数字** = 把方向拉长多少倍，得到"每秒往哪走多少米"
- `"文字" + 变量` —— 把文字和数字拼成一句打印出来，**这是调试最常用的一招**

### 2.5 四个实验

**实验 1：速度实时可调**
Play 起来，然后在 Inspector 里拖 `Forward Speed`（2 → 5 → 10）。方块应该立刻变快。
→ 体会"参数化"的威力：行为由参数决定，不改一行代码。

**实验 2：换方向**
在 Inspector 里把 `Move Direction` 的 x 改成 `1`、z 改成 `0`，方块往**右**爬。
改成 `(0, 1, 0)` → 往**上**飞。改成 `(0, 0, -1)` → 往**后**退。

**实验 3：看 Console 打印**
Play 时 Console 应该有 `车启动了，速度 = 2`。改完速度再 Play，数字跟着变。
→ 以后排查"为什么我的值不对"，就是靠这句打印。

**实验 4：斜着走**
把 `Move Direction` 设成 `(0.7, 0, 0.7)`，方块会**斜着往右前方**走。
因为 `Vector3` 不要求是整数，也不要求长度为 1。

> **顺带一个坑**：`(0.7, 0, 0.7)` 这个向量的长度其实是 1.4 而不是 1，所以实际速度会比 `forwardSpeed` 大 1.4 倍。
> 想严格按"每秒多少米"来走，要用 `dir.normalized`（把长度压成 1）。这个词第 4 课会正式讲。

### 2.6 实验时"方块不见了"怎么办（必读）

做实验 1 把速度拖大之后，你多半会遇到：**Scene 和 Game 里都空了，方块没了。**

**它没丢，只是跑远了。**

`transform.Translate` 是**每帧累加**的，没有任何上限。速度 2 米/秒跑 2 分钟就是 240 米，早飞出摄像机视野了。

**怎么找回来（两个办法）**：

1. **点 ▶ 停止 Play** —— Unity 会把所有物体恢复到 Play 之前的状态，方块自动弹回原位
2. **在 Hierarchy 里点中 `Cube`，把鼠标移到 Scene 窗口里，按 `F`** —— 场景摄像机会立刻飞到那个物体上

> `F` = Frame Selected（聚焦选中物体），是 Unity 最常用的快捷键之一。
> 配套还有：滚轮缩放、**按住右键 + WASD** 在场景里飞。

**一定要建立这个直觉**：

> **Hierarchy 是账本，Scene / Game 只是眼睛。**
> 画面里看不到 ≠ 物体不存在。东西"不见了"的第一件事永远是**看 Hierarchy**，而不是重新建一个。

**还有一件更要紧的事**：做完实验记得 `Ctrl + S` 保存场景（先停止 Play 再保存）。
如果你一直没保存过，那这个方块只活在内存里，Unity 一崩就白建了。

---

## 四、第 3 课：用 WASD 手动开车

**这一课的目标**：写出一辆**你能开**的车。这一课结束，你项目里的 `CarController` 就有了雏形。

### 4.1 `if`：让代码"看情况做事"

到这一课为止，你的代码都是**无条件**的：每帧都转、每帧都走。但开车需要"按了 W 才走"。

```csharp
if (条件)
{
    // 条件为真时才执行这里
}
```

`条件` 必须是一个 `bool`（真 / 假）。比如 `Input.GetKey(KeyCode.W)` 就是"W 键现在被按着吗"。

```csharp
if (Input.GetKey(KeyCode.W))
{
    Debug.Log("你在往前走");
}
```

**注意花括号 `{ }`**：只有花括号里的代码受 `if` 管。不写花括号的话，只有**紧跟着的那一行**受管 —— 这是新手最容易出的 bug。

### 4.2 `Input`：怎么读键盘

| 写法 | 含义 |
|---|---|
| `Input.GetKey(KeyCode.W)` | W 键**正被按住**（按住期间每帧都是真） |
| `Input.GetKeyDown(KeyCode.W)` | W 键**刚被按下**的那一帧（按一次只真一帧） |
| `Input.GetKeyUp(KeyCode.W)` | W 键**刚被松开**的那一帧 |

`KeyCode` 是按键的名字表：`KeyCode.W`、`KeyCode.A`、`KeyCode.Space`、`KeyCode.LeftShift`……

**什么时候用哪个**：

- **持续动作**（开车、加速）→ `GetKey`
- **一次性动作**（跳跃、切模式、按 R 回起点）→ `GetKeyDown`

你在第 2 课写的 R 键用的是 `GetKeyDown` —— 因为"回起点"是按一次触发一次，按住不该连续触发。

**常用 `KeyCode` 速查表**：

| 按键 | 写法 |
|---|---|
| **空格** | `KeyCode.Space` |
| W / A / S / D | `KeyCode.W`、`KeyCode.A`、`KeyCode.S`、`KeyCode.D` |
| 左 Shift | `KeyCode.LeftShift` |
| 回车 | `KeyCode.Return` |
| Esc | `KeyCode.Escape` |
| 方向键 | `KeyCode.UpArrow`、`KeyCode.DownArrow`、`KeyCode.LeftArrow`、`KeyCode.RightArrow` |
| 数字 1 | `KeyCode.Alpha1` |

> **不用背。** 打 `KeyCode.` 之后 VS Code 会**自动弹出候选列表**，你直接打字筛选就行。
> 比如打 `KeyCode.Sp`，它会筛出 `Space`。**这是最实用的偷懒技巧。**

**多条 `if` 的顺序 = 优先级**：

```csharp
if (Input.GetKey(KeyCode.W))     throttle =  1f;
if (Input.GetKey(KeyCode.S))     throttle = -1f;
if (Input.GetKey(KeyCode.Space)) throttle =  0f;   // 写在最后
```

同一次 `Update` 里，**后面的 `if` 会覆盖前面赋的值**。
所以空格写在最后 = "不管你按没按 W，只要按了空格就停下"。
如果空格写在 `W` 前面，那按 W 又会把 `throttle` 改回 `1`，刹车就永远失效了。

> 这个"顺序决定优先级"的写法，在你项目的 `AutoDriver` 里会反复出现：
> 先算"想开多快"，再叠加"前方有车要减速"，再叠加"要撞上了必须刹车" —— **优先级靠后的覆盖前面的**。

### 4.3 核心设计：把键盘变成两个数字

这是**这一课最重要的一句话**：

> **不要让代码直接"读键盘开车"。先读键盘，把它翻译成两个数字，再用这两个数字开车。**

```csharp
float throttle = 0f;   // 油门：+1 前进，-1 后退，0 不动
float steer    = 0f;   // 方向：+1 右转，-1 左转，0 不转
```

**为什么要多这一层？** 因为这样"谁来开车"就变得可以替换：

- 现在：**键盘** 填这两个数字
- 第 9 课：**自动驾驶员** 填这两个数字
- **车本身完全不关心是谁在开**

这正好是你项目里已经定好的接口契约 `carController.SetInput(steering, motor, brake)` —— 你现在正在从零把它推出来。

### 4.4 完整代码

把 `MyFirstCar.cs` 整段替换成：

```csharp
using UnityEngine;

public class MyFirstCar : MonoBehaviour
{
    [Header("速度")]
    public float forwardSpeed = 5f;    // 前进速度（米/秒）
    public float turnSpeed = 90f;      // 转向速度（度/秒）

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // ===== 第 1 步：读键盘，翻译成两个数字 =====
        float throttle = 0f;   // 油门：+1 前进，-1 后退，0 不动
        float steer    = 0f;   // 方向：+1 右转，-1 左转，0 不转

        if (Input.GetKey(KeyCode.W)) throttle =  1f;
        if (Input.GetKey(KeyCode.S)) throttle = -1f;
        if (Input.GetKey(KeyCode.D)) steer    =  1f;
        if (Input.GetKey(KeyCode.A)) steer    = -1f;

        // ===== 第 2 步：按 R 回到起点 =====
        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.position = startPosition;
            transform.rotation = Quaternion.identity;   // identity = 没有旋转，朝向归零
            throttle = 0f;
            steer = 0f;
        }

        // ===== 第 3 步：用这两个数字驱动车子 =====
        transform.Rotate(0f, steer * turnSpeed * Time.deltaTime, 0f);
        transform.Translate(0f, 0f, throttle * forwardSpeed * Time.deltaTime);
    }
}
```

`Ctrl + S` 保存，回 Unity 等编译。

**注意第 3 步的形状** —— 和你在第 2 课学的完全是同一条公式：

```
每秒的变化量 × Time.deltaTime
```

只是"每秒的变化量"从写死的 `2f`，变成了**由输入算出来的** `throttle * forwardSpeed`。

### 4.5 让摄像机跟着车（不然没法开）

现在的问题是：车一开走，摄像机还在原点，你什么都看不见。

新建第二个脚本 `FollowCamera.cs`（和 `MyFirstCar.cs` 放同一个位置）：

```csharp
using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("跟着谁（把车拖进来）")]
    public Transform target;

    [Header("摄像机相对车的位置")]
    public Vector3 offset = new Vector3(0f, 6f, -10f);

    void LateUpdate()
    {
        if (target == null) return;   // 还没拖进来就什么都不做

        transform.position = target.position + offset;
        transform.LookAt(target);
    }
}
```

**新东西**：

- `public Transform target;` —— 一个**空的槽位**，等你在 Inspector 里把车拖进来
- `LateUpdate()` —— 和 `Update` 一样每帧执行，但**在 Update 之后**。摄像机要跟着已经移动完的车，所以用它
- `transform.LookAt(target)` —— 让物体（这里是摄像机）**转头看向**目标
- `if (target == null) return;` —— `return` 表示"这个函数到此为止"，防止你忘了拖槽位导致报错

**怎么把车拖进槽位（重要技能，以后天天用）**：

1. 选中 Hierarchy 里的 **`Main Camera`**
2. Inspector 最下面 `Add Component` → 搜 `Follow Camera` → 点它
3. Inspector 里出现一块 `Follow Camera`，里面有个 **`Target` 槽位**，现在写着 `None (Transform)`
4. **按住 Hierarchy 里的 `Cube`，拖到这个 `Target` 槽位上，松手**
5. 槽位变成 `Cube (Transform)` —— 成功

> **拖槽位是 Unity 最核心的操作之一。** 以后 `WaypointFollower` 要拖路径、`FrontCarDetector` 要选图层，全靠它。
> 拖不上的时候，先检查**类型对不对** —— 槽位要 `Transform`，你拖进去的东西必须**有** `Transform`（所有物体都有）。

### 4.6 实验

| # | 做什么 | 看什么 |
|---|---|---|
| 1 | Play，用 **WASD** 在场景里绕一圈 | 车能开、能转，摄像机跟着走 |
| 2 | 把 `Turn Speed` 改成 `0` | 只能直着走，怎么按 AD 都不转 |
| 3 | 把 `Turn Speed` 改成 `360` | 转得像陀螺，根本没法开 |
| 4 | 把两个 `Input.GetKey` 改成 `Input.GetKeyDown` | **按一下才动一点点**，完全没法开 |
| 5 | 按 `R` | 回到起点，朝向也归零 |

**实验 4 是重点** —— 它让你亲手体会 `GetKey` 和 `GetKeyDown` 的区别。改完记得改回来。

### 4.7 小挑战：加一个空格刹车

**要求**：按住空格时，车立刻停住（但方向还能转）。

提示：在"第 1 步"读完键盘之后、"第 3 步"驱动之前，插一句 `if`。

<details>
<summary>先自己试，想不出来再点开</summary>

```csharp
// 按空格就松开油门
if (Input.GetKey(KeyCode.Space)) throttle = 0f;
```

</details>

---

## 五、接下来的课（学到再补详细内容）

第 4 课：**向量运算、距离** → 让车自己朝一个目标点开。这是 `WaypointFollower` 的第一步，也是"自动驾驶"真正开始的地方。

**每节课结束时，你都会多一段自己写的、能跑的代码。**
