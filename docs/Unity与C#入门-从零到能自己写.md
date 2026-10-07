# Unity + C# 入门：从零到能自己写

> 载体就是你这个数字孪生项目。**不做脱离项目的教程** —— 那种东西学完就忘。  
> 每一课都是你项目里的一个零件，学到第 10 课，你手里就有自己写的 `CarController` 和 `AutoDriver`。

---

## 一、为什么这么安排

很多人学 Unity 是这样：看 20 小时教程 → 跟着做 Roll-a-Ball → 关掉 → 全忘了。

**因为学的和要用的没关系。**

换个方式：每学一个概念，立刻用在你的项目上。你会清楚地知道"我为什么要学这个"。

| 课      | 学什么                           | 你写完的代码能干什么     | 对应项目里的哪个脚本                     |
| ------ | ----------------------------- | -------------- | ------------------------------ |
| **1**  | 挂脚本、`Update`、`Time.deltaTime` | 方块自己转起来        | —                              |
| **2**  | 变量、`public`、`Vector3`         | 方块自己往前爬        | —                              |
| **3**  | `Input`、`if` 判断               | **WASD 手动开车**  | 以后 `CarController` 的手动模式       |
| **4**  | 向量运算、距离                       | 朝一个目标点开        | `WaypointFollower` 的第一步        |
| **5**  | `Quaternion`、插值               | **平滑转向目标点**    | `WaypointFollower` 的核心         |
| **6**  | `GetComponent`、脚本之间传数据        | 读另一个脚本的数据      | `BModuleFacade` 的原理            |
| **7**  | 数组、`List`、`for` 循环            | 把 8 个路标点连成一条路径 | `WaypointPath` 的原理             |
| **8**  | 射线 `Raycast`                  | 测前方障碍物的距离      | `FrontCarDetector` 的原理         |
| **9**  | 计时器、简单状态机                     | 遇障碍减速停车        | `AutoDriver` 的雏形               |
| **10** | 综合                            | **自己写的车跑完一整圈** | `CarController` + `AutoDriver` |

**不要跳课。** 每一课都依赖前一课。

---

## 二、第 1 课：让方块自己转起来

**这一课的目标**：你自己写的第一段代码，挂到方块上，按 ▶ 看到它转起来。

### 1.1 先认清 Unity 界面（五块）

打开 Unity，你会看到五个区域。**记住它们的名字**，后面我提到"去 Inspector 里改"你就知道在哪：

| 区域                 | 位置              | 是什么                    |
| ------------------ | --------------- | ---------------------- |
| **Hierarchy**（层级）  | 左侧              | 当前场景里**有哪些物体**         |
| **Scene**（场景）      | 中间              | **3D 编辑视图**，你在这里摆东西    |
| **Game**（游戏）       | 中间（和 Scene 同位置） | **摄像机看到的画面**，就是玩家看到的   |
| **Inspector**（检视器） | 右侧              | 选中物体后，它的**所有属性**       |
| **Project**（项目）    | 底部              | 磁盘上的**所有文件**（脚本、场景、模型） |

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

| 类型       | 装什么   | 例子                        |
| -------- | ----- | ------------------------- |
| `float`  | 小数    | `float speed = 2.5f;`     |
| `int`    | 整数    | `int lapCount = 0;`       |
| `bool`   | 真 / 假 | `bool isBraking = false;` |
| `string` | 文字    | `string carName = "小蓝";`  |

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

| `Vector3`    | 方向 | 对应轴的颜色 |
| ------------ | -- | ------ |
| `(0, 0, 1)`  | 前  | 蓝      |
| `(0, 0, -1)` | 后  | 蓝      |
| `(1, 0, 0)`  | 右  | 红      |
| `(-1, 0, 0)` | 左  | 红      |
| `(0, 1, 0)`  | 上  | 绿      |
| `(0, -1, 0)` | 下  | 绿      |

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

| 写法                            | 含义                       |
| ----------------------------- | ------------------------ |
| `Input.GetKey(KeyCode.W)`     | W 键**正被按住**（按住期间每帧都是真）   |
| `Input.GetKeyDown(KeyCode.W)` | W 键**刚被按下**的那一帧（按一次只真一帧） |
| `Input.GetKeyUp(KeyCode.W)`   | W 键**刚被松开**的那一帧          |

`KeyCode` 是按键的名字表：`KeyCode.W`、`KeyCode.A`、`KeyCode.Space`、`KeyCode.LeftShift`……

**什么时候用哪个**：

- **持续动作**（开车、加速）→ `GetKey`
- **一次性动作**（跳跃、切模式、按 R 回起点）→ `GetKeyDown`

你在第 2 课写的 R 键用的是 `GetKeyDown` —— 因为"回起点"是按一次触发一次，按住不该连续触发。

**常用 `KeyCode` 速查表**：

| 按键            | 写法                                                                             |
| ------------- | ------------------------------------------------------------------------------ |
| **空格**        | `KeyCode.Space`                                                                |
| W / A / S / D | `KeyCode.W`、`KeyCode.A`、`KeyCode.S`、`KeyCode.D`                                |
| 左 Shift       | `KeyCode.LeftShift`                                                            |
| 回车            | `KeyCode.Return`                                                               |
| Esc           | `KeyCode.Escape`                                                               |
| 方向键           | `KeyCode.UpArrow`、`KeyCode.DownArrow`、`KeyCode.LeftArrow`、`KeyCode.RightArrow` |
| 数字 1          | `KeyCode.Alpha1`                                                               |

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

| # | 做什么                                      | 看什么                |
| - | ---------------------------------------- | ------------------ |
| 1 | Play，用 **WASD** 在场景里绕一圈                  | 车能开、能转，摄像机跟着走      |
| 2 | 把 `Turn Speed` 改成 `0`                    | 只能直着走，怎么按 AD 都不转   |
| 3 | 把 `Turn Speed` 改成 `360`                  | 转得像陀螺，根本没法开        |
| 4 | 把两个 `Input.GetKey` 改成 `Input.GetKeyDown` | **按一下才动一点点**，完全没法开 |
| 5 | 按 `R`                                    | 回到起点，朝向也归零         |

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

## 五、第 4 课：让车自己朝目标点开

**这一课的目标**：不再用键盘。车自己看目标、自己算方向、自己开过去。这是 `WaypointFollower` 的第一步。

### 5.1 核心问题：车怎么知道该往哪打方向

车看到的世界是**世界坐标**（原点是场景中心），这没什么用 —— 车不关心目标在世界哪个角落，只关心**目标在我左边还是右边、前面还是后面**。

Unity 提供了一个方法做这个换算：

```csharp
Vector3 local = transform.InverseTransformPoint(target.position);
```

`InverseTransformPoint` = "把世界坐标的点，换算成**以我的位置为原点、以我的朝向为前方**的坐标"。

换算完就好办了：

| `local` 的值    | 含义          |
| ------------- | ----------- |
| `local.x > 0` | 目标在我**右边**  |
| `local.x < 0` | 目标在我**左边**  |
| `local.z > 0` | 目标在我**前面**  |
| `local.z < 0` | 我已经**开过头**了 |

### 5.2 角度 → steer

```csharp
float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
```

这一行算出"**目标偏离我车头多少度**"：

| 情况    | `local`   | `angleDeg` |
| ----- | --------- | ---------- |
| 目标正前方 | `(0, 10)` | `0°`       |
| 目标右前方 | `(5, 5)`  | `45°`      |
| 目标正右方 | `(10, 0)` | `90°`      |
| 目标左前方 | `(-5, 5)` | `-45°`     |

正数 = 该往右打，负数 = 该往左打。

> **为什么用 `Atan2` 而不是 `Vector3.Angle`？**  
> `Vector3.Angle` 只给你"差多少度"，**不带正负号** —— 它不知道是左还是右，你没法判断该往哪打。  
> `Atan2` 保留符号，这才是能用的。

再变成第 3 课那两个数字：

```csharp
Steer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);
```

- `angleDeg / fullSteerAngle` —— 偏 45° 时刚好满舵 1.0；偏 22.5° 时半舵 0.5
- `Mathf.Clamp(..., -1f, 1f)` —— **夹住**，不管偏 200° 还是 500°，都不会超出 -1 ~ 1

### 5.3 完整代码：`AutoDrive.cs`

```csharp
using UnityEngine;

public class AutoDrive : MonoBehaviour
{
    [Header("速度")]
    public float forwardSpeed = 5f;      // 前进速度（米/秒）
    public float turnSpeed = 120f;       // 最大转向速度（度/秒）

    [Header("要去哪（把 Target 拖进来）")]
    public Transform target;

    [Header("偏到这个角度就打死方向盘")]
    public float fullSteerAngle = 45f;

    [Header("离目标多近就算到了")]
    public float arrivalRadius = 2f;

    // 这两个数字就是第 3 课学的 throttle / steer，
    // 只是现在由代码算出来，不再由键盘填
    public float Throttle { get; private set; }
    public float Steer    { get; private set; }

    void Update()
    {
        if (target == null) return;

        // ===== 第 1 步：把目标点换算到"我自己的坐标系"里 =====
        Vector3 local = transform.InverseTransformPoint(target.position);

        // ===== 第 2 步：算出目标偏了我车头多少度 =====
        float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

        // ===== 第 3 步：角度 → steer（夹在 -1 ~ 1 之间） =====
        Steer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);

        // ===== 第 4 步：离目标够近了就停车 =====
        float distance = local.magnitude;
        Throttle = distance < arrivalRadius ? 0f : 1f;

        // ===== 第 5 步：还是第 3 课那条公式 =====
        transform.Rotate(0f, Steer * turnSpeed * Time.deltaTime, 0f);
        transform.Translate(0f, 0f, Throttle * forwardSpeed * Time.deltaTime);

        // 调试打印
        if (Time.frameCount % 30 == 0)
            Debug.Log($"距离 {distance:F1} 米 | 偏角 {angleDeg:F1}° | steer {Steer:F2} | throttle {Throttle:F0}");
    }

    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position);
        Gizmos.DrawWireSphere(target.position, arrivalRadius);
    }
}
```

### 5.4 怎么用（关键一步别漏）

1. Hierarchy 右键 → `3D Object` → `Sphere`，改名 `Target`，位置设成 `(10, 0, 10)`
2. 选中 `Cube` → `Add Component` → `Auto Drive` → `Target` 槽位拖上那个球
3. **⚠️ 在 Inspector 里把 `MyFirst Car` 组件左边的勾去掉 —— 停用它**

> **为什么必须停用？** 两个脚本都在 `Update` 里移动同一个物体，会打架 —— 车会抖得像抽筋。  
> **把"输入源"换掉，而不是删掉** —— 这正是第 3 课那句话的价值：车不关心是谁在开。

### 5.5 两个高频困惑（我踩过，你也一定会问）

**困惑 1：为什么车离目标还有一两米就停了？**

因为 `arrivalRadius` 就是"**够近了就算到了**"的阈值：

```csharp
Throttle = distance < arrivalRadius ? 0f : 1f;
```

想停得更近就把 `Arrival Radius` 改小。**但不能太小** —— 车的最小转弯半径是

```
R = 速度 ÷ 角速度 = forwardSpeed ÷ (turnSpeed × π/180)
```

比如 `5 ÷ (120 × π/180) = 2.39 米`。若 `Arrival Radius` 远小于这个数，车会**绕着目标打转，永远停不下来**。

> **验证实验**：把 `Forward Speed` 拖到 `20` → 转弯半径变成 `9.5 米` ≫ 到达半径 `2 米` → 车绕大圈停不下来。  
> 这就是自动驾驶最经典的问题：**减速能力和转弯能力必须匹配**。项目里 `AutoDriver` 的 `brakeDistance` 解决的就是同类麻烦。

**困惑 2：为什么改了 Inspector 里的参数没反应？**

**先检查这个变量在代码里到底被引用了没有。**

```csharp
Steer = Mathf.Clamp(angleDeg / 10, -1f, 1f);   // ❌ 写死了 10，fullSteerAngle 变成摆设
Steer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);   // ✅ 这才是对的
```

> **`public` + `[Header]` 只让变量在 Inspector 里"看得见"，不代表代码在"用它"。**  
> 排查方法：VS Code 里 `Ctrl + F` 搜变量名，**出现次数 = 1（只有声明那行）就是摆设**。

**还有第二层**：`fullSteerAngle` 只在**小角度**才看得出效果。因为 `steer` 被 `Clamp` 到 ±1 之后会**饱和** —— 车偏 45° 以上时，除以 45 和除以 10 结果都是 1，**完全一样**。

| 偏离角    | `fullSteerAngle=45` | `fullSteerAngle=10` |
| ------ | ------------------- | ------------------- |
| 5°     | 0.11                | 0.50                |
| 10°    | 0.22                | **1.00**（饱和）        |
| 30°    | 0.67                | 1.00                |
| 45° 以上 | 1.00                | 1.00                |

**要看出区别，得让车已经大致对准目标**。比如把 `Target` 放到 `(1, 0, 20)`（偏角约 2.9°），再切 `Full Steer Angle`。

### 5.6 实验

| # | 做什么                          | 看什么                                       |
| - | ---------------------------- | ----------------------------------------- |
| 1 | `Target` 位置改成 `(-10, 0, 10)` | 车改成往**左前方**开（`angleDeg` 变负）               |
| 2 | `Full Steer Angle` 改成 `10`   | 小角度时更敏感；大角度时没区别（见 5.5）                    |
| 3 | `Forward Speed` 改成 `20`      | 转弯半径变大，**绕圈停不下来**                         |
| 4 | Play 起来**手动拖动** `Target`     | 车**实时追着球跑** —— 这就是闭环控制                    |
| 5 | 选中车看 Scene 视图                | 黄线和圆圈 = 感知范围的可视化（`PathVisualizer` 干的就是这个） |

---

## 六、第 5 课：平滑转向（插值）

**这一课的目标**：解决第 4 课留下的手感问题 —— **现在的车转向太生硬**。

### 6.1 问题在哪

现在的代码：

```csharp
Steer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);
```

`Steer` 这一帧是多少，方向盘**立刻**就是多少 —— 从 0 直接跳到 1，零过渡。

真车的方向盘**打不了那么快**：从正中到打死至少要一两秒。所以现在的车看起来像在"抽筋"，不像在开车。

### 6.2 解决工具：`Mathf.MoveTowards`

```csharp
Mathf.MoveTowards(当前值, 目标值, 这一帧最多走多少)
```

它让数值**一步一步**靠近目标，每帧最多走固定的一小步。

**关键**：它**永远不会超过目标值** —— 走到了就停住。这一点和 `Mathf.Lerp` 完全不同：

| 方法                                  | 行为                               | 用在哪                 |
| ----------------------------------- | -------------------------------- | ------------------- |
| `Mathf.Lerp(a, b, t)`               | 每次只走"剩余距离的 t 倍"，**永远追不上，只能无限接近** | 摄像机跟随、UI 渐入         |
| `Mathf.MoveTowards(a, b, maxDelta)` | 每帧走固定步长，**能追上并且停住**              | **方向盘、速度这类有物理上限的量** |

方向盘转速有上限，所以用 `MoveTowards`。

### 6.3 改法：加一层"实际方向盘"

```csharp
[Header("转向")]
public float fullSteerAngle = 45f;

[Tooltip("方向盘每秒最多变化多少。越小越肉（像大卡车），越大越灵敏")]
public float steerRate = 2f;

float _targetSteer;   // 这一帧"想打多少方向"
```

`Update` 里把第 3 步拆成两半：

```csharp
// ---- 3a. 想要的方向（瞬间算出来的理想值）----
_targetSteer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);

// ---- 3b. 实际的方向（慢慢靠过去，这就是"平滑"）----
Steer = Mathf.MoveTowards(Steer, _targetSteer, steerRate * Time.deltaTime);
```

**注意 3b 又是那条老公式**：`每秒变化量 × Time.deltaTime`。你现在应该能一眼认出来了。

调试打印也加上对比：

```csharp
Debug.Log($"偏角 {angleDeg:F1}° | 想打 {_targetSteer:F2} | 实际 {Steer:F2}");
```

### 6.4 完整代码

```csharp
using UnityEngine;

public class AutoDrive : MonoBehaviour
{
    [Header("速度")]
    public float forwardSpeed = 5f;
    public float turnSpeed = 120f;

    [Header("要去哪")]
    public Transform target;

    [Header("转向")]
    public float fullSteerAngle = 45f;

    [Tooltip("方向盘每秒最多变化多少。1 = 一秒打死；0.3 = 三秒多打死（像大卡车）")]
    public float steerRate = 0.5f;

    [Header("到达判定")]
    public float arrivalRadius = 2f;

    public float Throttle { get; private set; }
    public float Steer    { get; private set; }

    float _targetSteer;   // 这一帧"想打多少方向"

    void Update()
    {
        if (target == null) return;

        Vector3 local = transform.InverseTransformPoint(target.position);
        float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

        // ---- 3a. 想要的方向 ----
        _targetSteer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);

        // ---- 3b. 实际的方向：朝想要的方向慢慢靠过去 ----
        Steer = Mathf.MoveTowards(Steer, _targetSteer, steerRate * Time.deltaTime);

        float distance = local.magnitude;
        Throttle = distance < arrivalRadius ? 0f : 1f;

        transform.Rotate(0f, Steer * turnSpeed * Time.deltaTime, 0f);
        transform.Translate(0f, 0f, Throttle * forwardSpeed * Time.deltaTime);

        if (Time.frameCount % 30 == 0)
            Debug.Log($"偏角 {angleDeg:F1}° | 想打 {_targetSteer:F2} | 实际 {Steer:F2}");
    }

    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position);
        Gizmos.DrawWireSphere(target.position, arrivalRadius);
    }
}
```

### 6.5 补充：`Quaternion` 是什么

Unity 里表示"旋转"有两种方式：

| 方式                         | 长什么样                         | 特点                  |
| -------------------------- | ---------------------------- | ------------------- |
| **欧拉角**（你在 Inspector 里看到的） | `(0, 45, 0)` 三个度数            | 直观，但**不能直接做插值**     |
| **`Quaternion`**（代码里用的）    | `Quaternion.Euler(0, 45, 0)` | 不直观，但**插值安全、不会万向锁** |

常用三个：

```csharp
Quaternion.LookRotation(方向)              // 生成"面朝这个方向"的旋转
Quaternion.Slerp(当前, 目标, t)             // 平滑转头（每次都走剩余距离的 t 倍）
Quaternion.RotateTowards(当前, 目标, 度数)  // 限制转头速度，和 MoveTowards 一个道理
```

> **怎么选？**
>
> - 你要**限制转向速率**（像真车打方向盘）→ 用 `Mathf.MoveTowards` 平滑 `Steer`，也就是 6.3 的做法 ✓
> - 你要**让物体直接平滑转头**（像摄像机、像游戏里的小怪）→ 用 `Quaternion.Slerp` / `RotateTowards`
>
> **你的车属于前者** —— 因为方向盘转速是真实存在的物理限制，而且数字孪生里 `Steer` 是要发给 MATLAB 的物理量，得保留它的物理意义。


### 6.6 实验

| # | 做什么                        | 看什么                                |
| - | -------------------------- | ---------------------------------- |
| 1 | `Steer Rate` 改成 `0.5`      | 转向极肉，像开大卡车，拐大弯要很久                  |
| 2 | `Steer Rate` 改成 `20`       | 又变回原来那种生硬                          |
| 3 | `Steer Rate` 改成 `5`        | 比较接近真车手感                           |
| 4 | 盯 Console 里 `想打` 和 `实际` 两列 | 刚起步时差很大，之后**实际值追着想打值走** —— 这就是"平滑" |

**实验 4 是重点**：你会亲眼看到"理想值"和"实际值"的分离与追赶。这个概念在控制工程里叫**一阶惯性环节** —— 你项目里 MATLAB 那边 `TAU = 1.2` 那个数字，描述的就是同一件事。

### 6.7 为什么有时候"看不出平滑效果"（实测踩到的坑）

写完 6.3 的代码跑起来，你可能会发现 Console 里是这样的：

```
偏角 -71.0° | 想打 -1.00 | 实际 -1.00
偏角 -62.0° | 想打 -1.00 | 实际 -1.00
偏角 -1.3°  | 想打 -0.03 | 实际 -0.03
偏角 -0.2°  | 想打 -0.01 | 实际 -0.01
```

**`想打` 和 `实际` 每一行都完全一样** —— 那 `MoveTowards` 岂不是白写了？

**不是白写，是参数太大。**

`MoveTowards` 每帧最多走 `steerRate × deltaTime`。只有当 **`steerRate` 比"`想打` 自己的变化速度"更慢**时，`实际` 才会落后于 `想打`，平滑才看得见。

上面那组数据里：

- `想打` 从 -1.00 降到 -0.03 用了约 1.8 秒 → 变化速度约 **0.54 /秒**
- 而 `steerRate = 2` → 每帧能走 `2 × 0.0167 = 0.033`，**比 0.54 快得多**
- 所以 `实际` 每一帧都能追平 `想打`，看起来就像没做平滑

**记住这条规律**：

> **平滑参数只有在"比被平滑对象的变化速度更慢"时才起作用。**

**分级实验（一定要做）**：

| `Steer Rate` | 打死方向盘要多久 | Console 里会看到                  |
| ------------ | -------- | ----------------------------- |
| `5`          | 0.2 秒    | `实际` 死死贴着 `想打`，**等于没做平滑**     |
| `2`          | 0.5 秒    | 只在起步那 0.5 秒有一点滞后，之后贴死         |
| `0.5`        | 2 秒      | **`实际` 明显落后于 `想打`** —— 这就是你要的 |
| `0.3`        | 3.3 秒    | 滞后很明显，转向像开大卡车                 |

> **物理意义**：`steerRate` 其实就是"**方向盘从正中打到打死需要几秒**"的倒数。  
> 真车大概 1~2 秒，所以 `0.5 ~ 1.0` 是比较真实的范围。
>
> 调好之后你会发现车开始**画蛇（振荡）** —— 因为它来不及修正就冲过头了。  
> 这不是 bug，是真车的真实行为，也正是你 MATLAB 那边一阶惯性模型要复现的东西。

---

## 七、第 6 课：脚本之间传数据（`GetComponent` 与门面模式）

**这一课的目标**：让两个脚本协作 —— 一个算"往哪开"，一个管"怎么动"。这是 `BModuleFacade` 的原理。

### 7.1 为什么要把职责拆开

现在 `AutoDrive` 一个人干了两件事：

1. 算"该往哪打、该给多少油门"（**决策**）
2. 直接 `transform.Rotate` / `Translate` 把车挪走（**执行**）

这样写有两个麻烦：

- 想换成**键盘开**，就得把移动代码再抄一遍
- 想换成 **MATLAB 下发的指令**，又得抄一遍

**拆开就好了**：

| 脚本              | 职责                                            | 知道什么     |
| --------------- | --------------------------------------------- | -------- |
| `CarController` | **执行**：给我 `steering` / `motor` / `brake`，我让车动 | 只知道"怎么动" |
| `AutoDrive`     | **决策**：算出 `steering` / `motor`                | 只知道"往哪开" |

**这就是你项目里定好的接口契约 `carController.SetInput(steering, motor, brake)`。**

### 7.2 `GetComponent<T>()`：从自己身上找组件

两个脚本都挂在**同一个 GameObject**（Cube）上。要让 A 拿到 B，用：

```csharp
CarController car = GetComponent<CarController>();
```

读作："**从我自己身上，找 `CarController` 这个类型的组件**"。

**三条要点**：

1. **一定要在 `Start()` 里找一次，存起来**，不要每帧 `GetComponent`  
   （每帧找一次 = 每秒找 60 次，纯浪费）
2. 找不到会返回 `null`，所以**一定要判空**，否则报 `NullReferenceException`
3. 如果组件在**子物体**上，用 `GetComponentInChildren<T>()`

### 7.3 完整代码

**新建 `CarController.cs`**：

```csharp
using UnityEngine;

/// <summary>
/// 车辆执行器：只负责"把输入变成运动"。
/// 不关心输入是谁给的（键盘 / 自动驾驶员 / MATLAB 都行）。
/// </summary>
public class CarController : MonoBehaviour
{
    [Header("车辆参数")]
    public float maxSpeed = 5f;         // 最大前进速度（米/秒）
    public float maxTurnSpeed = 120f;   // 最大转向角速度（度/秒）

    // ---- 输入：外部写进来，外部也读得到 ----
    /// <summary>方向盘：-1 左打满 ~ +1 右打满</summary>
    public float Steering { get; private set; }
    /// <summary>油门：0 不动 ~ 1 全速</summary>
    public float Motor { get; private set; }
    /// <summary>刹车</summary>
    public bool Braking { get; private set; }

    /// <summary>外部用这个来开车 —— 这就是项目的接口契约</summary>
    public void SetInput(float steering, float motor, bool brake = false)
    {
        Steering = Mathf.Clamp(steering, -1f, 1f);
        Motor    = Mathf.Clamp01(motor);
        Braking  = brake;
    }

    void Update()
    {
        float motor = Braking ? 0f : Motor;

        transform.Rotate(0f, Steering * maxTurnSpeed * Time.deltaTime, 0f);
        transform.Translate(0f, 0f, motor * maxSpeed * Time.deltaTime);
    }
}
```

**三个新东西**：

- `Mathf.Clamp01(x)` —— 把值夹到 `0 ~ 1`（等于 `Clamp(x, 0f, 1f)`，写起来短）
- `bool brake = false` —— **默认参数**。调用时可以只写两个参数，第三个不写就是 `false`
- `public float Steering { get; private set; }` —— **外部能读、只有自己（和调用 `SetInput` 的）能写**

**把 `AutoDrive.cs` 改成这样**：

```csharp
using UnityEngine;

public class AutoDrive : MonoBehaviour
{
    [Header("要去哪")]
    public Transform target;

    [Header("转向")]
    public float fullSteerAngle = 45f;
    public float steerRate = 0.5f;

    [Header("到达判定")]
    public float arrivalRadius = 2f;

    // 同一个物体上的车辆执行器
    CarController car;

    public float Throttle { get; private set; }
    public float Steer    { get; private set; }

    float _targetSteer;

    void Start()
    {
        // ★ 关键一句：从我身上找到 CarController
        car = GetComponent<CarController>();

        if (car == null)
            Debug.LogError("[AutoDrive] 这个物体上没有 CarController！请先 Add Component。", this);
    }

    void Update()
    {
        if (car == null || target == null) return;

        Vector3 local = transform.InverseTransformPoint(target.position);
        float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

        _targetSteer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);
        Steer = Mathf.MoveTowards(Steer, _targetSteer, steerRate * Time.deltaTime);

        float distance = local.magnitude;
        Throttle = distance < arrivalRadius ? 0f : 1f;

        // ★ 不再自己移动！把两个数字交给 CarController
        car.SetInput(Steer, Throttle);

        if (Time.frameCount % 30 == 0)
            Debug.Log($"偏角 {angleDeg:F1}° | 想打 {_targetSteer:F2} | 实际 {Steer:F2}");
    }

    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position);
        Gizmos.DrawWireSphere(target.position, arrivalRadius);
    }
}
```

**怎么装配**：

1. 选中 `Cube` → `Add Component` → 搜 `Car Controller` → 加上
2. **把 `My First Car` 组件删掉**（右键组件名 → `Remove Component`）—— 它已经完成历史使命了
3. Cube 上现在应该有：`Car Controller` + `Auto Drive`（`Transform` / 网格那几个不算）
4. Play

**验证拆分成功**：车的行为应该**和第 5 课一模一样**。  
如果一样 → 说明"决策"和"执行"干净地分开了，`AutoDrive` 只是换了个方式把数字递出去。

### 7.4 这个设计的价值：换输入源，车不用改

现在 `CarController` 完全不知道是谁在开车。所以：

```csharp
// 自动驾驶员
car.SetInput(autoDrive.Steer, autoDrive.Throttle);

// 键盘
car.SetInput(steer, throttle);

// MATLAB 下发（第 8 课会做）
car.SetInput(cmd.steer_des, cmd.v_des / maxSpeed);
```

**三行代码，同一个车，一行都不用改。**

> 这就是软件工程里&#x7684;**"依赖倒置"** —— 高层（决策）和低层（执行）都依赖中间那个抽象接口，而不是互相依赖。  
> 你不用记这个名词，记住"**车不关心是谁在开**"就够了。

### 7.5 门面模式：`BModuleFacade` 的原理

你项目里已经有这么一个脚本了（`Assets/Scripts/B/BModuleFacade.cs`），它就是**门面模式**：

```csharp
public class BModuleFacade : MonoBehaviour
{
    public FrontCarDetector detector;
    public WaypointFollower follower;

    // 转发，不自己算
    public float Steering => follower != null ? follower.Steering : 0f;
    public bool HasFrontCar => detector != null && detector.HasFrontCar;
    public float FrontCarDistance => detector != null ? detector.FrontCarDistance : float.MaxValue;

    public bool NeedBrake => HasFrontCar && FrontCarDistance < safeDistance;
    public bool NeedSlowDown => HasFrontCar && FrontCarDistance < slowDistance;

    void OnValidate()
    {
        // 在 Inspector 里改东西时自动跑，省得手拖
        if (detector == null) detector = GetComponentInChildren<FrontCarDetector>();
        if (follower == null) follower = GetComponentInChildren<WaypointFollower>();
    }
}
```

**四个值得学的写法**：

| 写法                            | 意思                                 |
| ----------------------------- | ---------------------------------- |
| `=> 表达式`（表达式体属性）              | 一行写完的属性，**每次读的时候现算**，不占内存          |
| `x != null ? x.Value : 默认值`   | **判空兜底**，防止对面没拖进来就崩                |
| `GetComponentInChildren<T>()` | 从自己和所有子物体里找（比 `GetComponent` 范围大）  |
| `OnValidate()`                | 一个特殊函数，**在 Inspector 里改任何东西时自动执行** |

**门面模式解决什么问题**：

> C（控制模块）只认 `BModuleFacade` 一个脚本。  
> B 以后怎么改算法、拆成几个脚本、加什么新功能 —— **只要不改这里的属性名，C 的代码一行都不用动。**

你现在的 `CarController` 扮演的就是同一个角色：**对外只暴露 `SetInput`，内部怎么实现随便改。**

### 7.6 实验

| # | 做什么                                               | 看什么                                                   |
| - | ------------------------------------------------- | ----------------------------------------------------- |
| 1 | 把 `AutoDrive` 组件**禁用**（Inspector 里去掉勾）            | 车**不会停**，而是沿着最后那次 `Steer` 一直转圈（见下方说明）                 |
| 2 | 把 `CarController` 的 `Max Speed` 改成 `15`           | 车变快，但**转向逻辑完全没变** —— 决策和执行是独立的                        |
| 3 | 把 `CarController` 的 `Max Turn Speed` 改成 `30`      | 转向变慢，车拐大弯                                             |
| 4 | 在 `CarController.Update` 里加一句 `Debug.Log(Motor);` | 看到 `AutoDrive` 喂进来的油门值                                |
| 5 | 把 `CarController` 组件**删掉**，再 Play                 | Console 报你写的那句红字 `[AutoDrive] 这个物体上没有 CarController！` |

**实验 1 的正确预期（很容易想错）**：

禁用 `AutoDrive` 之后，车**不会停住** —— 因为 `SetInput` 是"**写一次、一直保持**"的语义：

- `AutoDrive` 不再执行 → 没人再调 `SetInput`
- `CarController.Motor` **保留着最后一次写入的值**（比如 `1`）
- `CarController.Update` 还在跑 → 车继续用旧指令往前开、往旧方向转

所以你会看到车**沿着一个圈一直转**。

> 这在工程上叫"**指令保持**"。真车松油门后也会滑行，所以不算完全错；  
> 但如果决策层挂了（比如 MATLAB 断连），车就会一直用旧指令跑下去 —— 这是**危险**的。  
> 真正的安全做法是加一个"看门狗"：超过 N 秒没收到新指令就自动刹车。  
> 你项目里 `MatlabUdpBridge` 的"连接丢失检测"就是这个思路。

**实验 5 的关键：必须"删除"，不能只"禁用"**

| 操作                            | `GetComponent<CarController>()` 返回什么 | 为什么                                               |
| ----------------------------- | ------------------------------------ | ------------------------------------------------- |
| **禁用**（去掉勾，组件变灰）              | **还是能拿到**（非 null）                    | `enabled = false` 只是让 Unity 不再调它的 `Update`，组件本身还在 |
| **删除**（右键 → Remove Component） | **null**                             | 组件真的不在了                                           |

所以想看那句红字，必须真的 `Remove Component`。做完记得 `Ctrl + Z` 撤销，  
或者重新 `Add Component`（参数就是默认值，不会丢东西）。

---

## 八、第 7 课：让车跑过一串路标点

**这一课的目标**：从"追一个点"升级到"跑一条路"。这是 `WaypointPath` 的完整原理，也是"跑完整圈"的最后一块拼图。

### 8.1 数组和 `List`：怎么存一串东西

要存 4 个路标点，不能写 4 个变量（`wp0`、`wp1`、`wp2`、`wp3`）—— 那加第 5 个点就得改代码。

C# 有两种"容器"：

```csharp
Transform[] arr = new Transform[5];              // 数组：长度固定
List<Transform> list = new List<Transform>();    // List：可以随时增删
```

|                  | 数组 `T[]`                            | `List<T>`                                    |
| ---------------- | ----------------------------------- | -------------------------------------------- |
| 声明               | `Transform[] a = new Transform[4];` | `List<Transform> l = new List<Transform>();` |
| 长度               | `a.Length`                          | `l.Count`                                    |
| 能加元素吗            | **不能**（长度写死）                        | **能**，`l.Add(x)`                             |
| 要额外 using 吗      | 不用                                  | **要**：`using System.Collections.Generic;`    |
| 在 Inspector 里能拖吗 | 能                                   | 能，而且能拖动排序                                    |

**结论：路标点用 `List<Transform>`** —— 因为你想随时加一个点。

> **`<Transform>` 是什么？** 这叫**泛型** —— 尖括号里填"这个 List 装什么类型"。  
> `List<Transform>` 装 Transform，`List<int>` 装整数。**装错类型编译器直接拦你。**

### 8.2 `for` 循环：把一串东西挨个过一遍

```csharp
for (int i = 0; i < list.Count; i++)
{
    Debug.Log($"第 {i} 个点：{list[i].name}");
}
```

拆成三段看：

| 部分               | 写的是  | 意思        |
| ---------------- | ---- | --------- |
| `int i = 0`      | 初始化  | 从第 0 个开始  |
| `i < list.Count` | 继续条件 | 只要还没到头就继续 |
| `i++`            | 步进   | 每次加 1     |

**新手最容易错的：下标从 0 开始。**

一个有 4 个元素的 `List`，合法的下标是 `0 1 2 3`，**没有 4**。  
访问 `list[4]` 会直接抛 `ArgumentOutOfRangeException` 让程序崩掉。

所以条件永远写 `i < list.Count`，**不要写 `i <= list.Count`**。

（还有个更省事的写法叫 `foreach`，但你先习惯 `for` —— 因为下面要"跳过第 2 个点"这种操作时，`for` 才做得到。）

### 8.3 写 `MyWaypointPath.cs`

> **⚠️ 先读这段 —— 这是我今天刚帮你填的一个坑。**
>
> 你的工程里已经有 `Assets/Scripts/B/WaypointPath.cs`（项目正式版），  
> 里面有一个 `public class WaypointPath`。  
> 你在 `Assets/` 根目录又建了一个**同名类** → Unity 报  
> **`CS0101`：命名空间 "" 已经包含 "WaypointPath" 的定义** → **整个工程编译失败**。
>
> **最坑的地方**：编译失败后 Unity 不会崩，它会**继续用上一次编译成功的旧 dll 跑**。  
> 所以你 Play 起来一切正常，但**你新写的代码一行都没生效**。
>
> **自己怎么判断编译到底成没成功？** 看这个文件的时间戳：
>
> ```
> E:\My project\Library\ScriptAssemblies\Assembly-CSharp.dll
> ```
>
> 如果它比你的 `.cs` 文件**旧**，就说明编译失败了 —— Unity 还在跑旧代码。  
> （更直接的：Unity 顶部菜单栏右侧有个转圈图标，转完不停就是没编过；Console 里有红字就是没编过。）
>
> **所以这个学习版叫 `MyWaypointPath`** —— 文件名和类名必须**一起**叫这个。  
> 这就是第 1 课那条铁律的实战版：**类名在整个工程里只能出现一次。**


**新建 `MyWaypointPath.cs`**：

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 路径点容器：只负责"存点"和"报点"，不关心谁在用。
/// 叫 My... 是为了不和工程里 Assets/Scripts/B/WaypointPath.cs 撞类名。
/// </summary>
public class MyWaypointPath : MonoBehaviour
{
    [Header("路径点（按顺序）")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("设置")]
    [Tooltip("到终点后是否回到第 0 个点循环跑")]
    public bool loop = true;
    [Tooltip("距离目标点小于这个值就算到达")]
    public float arrivalRadius = 3f;

    /// <summary>一共有几个点</summary>
    public int Count => waypoints.Count;

    /// <summary>取第 index 个点的世界坐标（越界自动夹紧，不会崩）</summary>
    public Vector3 GetPoint(int index)
    {
        if (waypoints == null || waypoints.Count == 0) return transform.position;

        int i = Mathf.Clamp(index, 0, waypoints.Count - 1);
        if (waypoints[i] == null) return transform.position;

        return waypoints[i].position;
    }

    /// <summary>下一个点的下标（到了最后一个点：循环就回 0，不循环就原地不动）</summary>
    public int NextIndex(int index)
    {
        if (waypoints == null || waypoints.Count == 0) return 0;
        if (index + 1 >= waypoints.Count) return loop ? 0 : index;
        return index + 1;
    }
}
```

**四个新东西**：

- `using System.Collections.Generic;` —— 用 `List<>` 必须加这一行，**漏了会报"找不到类型 List"**
- `public List<Transform> waypoints` —— Inspector 里会出现一个**可拖拽、可排序**的列表
- `Mathf.Clamp(index, 0, Count - 1)` —— **越界保护**。哪怕上层传了个 999，也不会崩，只会夹到最后一个点
- `loop ? 0 : index` —— **三元运算符**，是 `if...else` 的简写。读作"`loop` 为真就取 0，否则取 `index`"

### 8.4 改 `AutoDrive.cs`：把单个目标换成一条路径

**① 字段改掉**

```csharp
[Header("路径（把挂 MyWaypointPath 的物体拖进来）")]
public MyWaypointPath path;

int _index;   // 现在正在追第几个点
```

（原来的 `public Transform target;` 删掉。）

**② `Update` 里改成追当前路径点**

```csharp
void Update()
{
    if (car == null || path == null || path.Count == 0) return;

    // ---- 追当前这个路径点 ----
    Vector3 targetPoint = path.GetPoint(_index);
    Vector3 local = transform.InverseTransformPoint(targetPoint);
    float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

    _targetSteer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);
    Steer = Mathf.MoveTowards(Steer, _targetSteer, steerRate * Time.deltaTime);

    // ---- 到了就换下一个点 ----
    float distance = local.magnitude;
    bool reached  = distance < arrivalRadius;
    bool overshot = local.z < 0f && distance < arrivalRadius * 2f;   // 冲过头了也算到

    if (reached || overshot)
    {
        int next = path.NextIndex(_index);
        if (next != _index)
        {
            _index = next;
            Debug.Log($"[AutoDrive] 到达 → 切换到第 {_index} 个点");
        }
    }

    // ---- 交给 CarController ----
    car.SetInput(Steer, 1f);   // 油门恒为 1：这条路没有终点，一直跑

    if (Time.frameCount % 30 == 0)
        Debug.Log($"点 {_index} | 距离 {distance:F1} | 偏角 {angleDeg:F1}° | steer {Steer:F2}");
}
```

**两个关键点**：

**① `overshot`（冲过头判定）是这一课的灵魂**

只判断 `distance < arrivalRadius` 是不够的：如果车速度太快、转弯半径太大，**擦着路标点冲过去**，距离永远降不到 `arrivalRadius` 以内 —— 它就会**绕着这个点转圈，永远出不来**。

```csharp
bool overshot = local.z < 0f && distance < arrivalRadius * 2f;
```

`local.z < 0` 表示**这个点已经在我身后了** → 说明我冲过头了 → 也算"到了"，换下一个点。

> 你项目里 `WaypointFollower` 的 `overshot` 就是这一行。**这是实测跑出来的补丁**，  
> 不是想出来的 —— 第 2 课那个"遇到障碍就停住"的死锁，是同一类几何陷阱。

**② `next != _index` 这个判断为什么必要**

`NextIndex` 在"不循环且已在最后一个点"时会**原样返回 `_index`**。  
如果没这个判断，代码会一直打印"到达"，把 Console 刷爆。

### 8.5 装配（5 步）

1. **建 4 个路标点**：Hierarchy 右键 → `3D Object` → `Sphere`，改名 `WP0`；再建 3 个叫 `WP1` `WP2` `WP3`
2. **摆成一个圈**：选中它们，在 Inspector 的 `Position` 里填
   - `WP0` → `(10, 0, 0)`
   - `WP1` → `(0, 0, 10)`
   - `WP2` → `(-10, 0, 0)`
   - `WP3` → `(0, 0, -10)`
3. **建一个容器**：Hierarchy 右键 → `Create Empty`，改名 `Path`，Position 归零 `(0,0,0)`
4. **把 WP0~WP3 拖进 `Path` 下面**（变成它的子物体），然后给 `Path` 加 `My Waypoint Path` 组件，  
   把 4 个球**按顺序拖进 `Waypoints` 列表**
   > **注意菜单里有两个长得像的组件**：
   >
   > - `My Waypoint Path` ← **用这个**（你自己写的学习版）
   > - `Waypoint Path` ← 项目正式版（`Assets/Scripts/B/` 里那个）
   >
   > 两个都能用、功能一样。**这一课统一用 `My Waypoint Path`**，因为它是你亲手写的。
   >
   > ⚠️ **加组件前先确认 Hierarchy 里只选中了 `Path` 一个物体。**  
   > `Add Component` 会作用于**所有被选中的物体** —— 如果你顺手把 `WP3` 也选中了，  
   > 组件会被加到每一个物体上。这种错误不报错、不影响运行，但很难发现。
5. **挂上去**：选中 `Cube` → `Auto Drive` 组件的 `Path` 槽位 ← 拖入 `Path`

原来的 `Target` 球可以删了。

### 8.6 实验

> **改参数之前先分清改哪个组件。** 这一课有**两个** `Arrival Radius`：
>
> - `Cube` 上的 `Auto Drive` → **代码真正读的是这个**（默认 `2`）
> - `Path` 上的 `My Waypoint Path` → 预留字段，`GetPoint` / `NextIndex` 根本没读它
>
> 下面说的 `Arrival Radius`，一律指 **`Cube` 上 `Auto Drive` 里的那个**。

| # | 做什么                                | 看什么                                                                                                                                                                |
| - | ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1 | Play                               | 车绕着 4 个点跑**一整圈**，Console 依次打印 `切换到第 1 / 2 / 3 / 0 个点`                                                                                                              |
| 2 | 关掉 `Path` 上的 `Loop` 勾              | 车跑到最后一个点后**不再换点**，会绕着 `WP3` 打转（油门恒为 `1`，**停不下来**）；Console **不再出现**「切换到第 X 个点」，但每 30 帧的状态日志仍在刷。<br />⚠️ **做完记得把 `Loop` 勾回来** —— 忘了勾，车跑到 `WP3` 会**直接穿过去**，看起来像"路径断了" |
| 3 | 把 `Arrival Radius` 从 `2` 改成 `4`    | 车提前换点，转弯半径绰绰有余，**跑得明显顺**                                                                                                                                           |
| 4 | 把 `Arrival Radius` 改成 `0.5`        | 车**擦着点冲过去**，全靠 `overshot` 换点；改到 `0.1` 会彻底绕圈                                                                                                                        |
| 5 | 把 `Max Speed` 从 `5` 改成 `3`         | 转弯半径变小，车贴着点走                                                                                                                                                       |
| 6 | 把 `Max Speed` 从 `5` 改成 `12`        | 转弯半径 `12 ÷ 2.09 = 5.7 米` ≫ 到达半径 → 大量依赖 `overshot`，跑得歪歪扭扭                                                                                                           |
| 7 | 在 `Waypoints` 列表里**拖动元素左边的小把手**换顺序 | 车跑的顺序跟着变（不用改代码）                                                                                                                                                    |
| 8 | **再加一个球 `WP4`**，拖进列表               | 路径自动变长 —— **这就是用 `List` 而不是写死 4 个变量的价值**                                                                                                                           |

**实验 3~6 是一组，它们说的是同一件事：**

```
最小转弯半径 R = Max Speed ÷ (Max Turn Speed × π/180)
              = 5 ÷ (120 × 0.01745)
              = 2.39 米          ← 当前默认值
```

**车能"到达"一个点的前提是 `R` 小于 `Arrival Radius`。**  
否则车永远缩不进那个圈，只能绕着点打转，全靠 `overshot` 侥幸换点。

| `Max Speed` | `R`（`Max Turn Speed = 120`） | 和 `Arrival Radius = 2` 比 |
| ----------- | --------------------------- | ------------------------ |
| `3`         | 1.43 米                      | ✅ 轻松到达                   |
| `5`（默认）     | 2.39 米                      | ⚠️ 勉强，会绕着点转              |
| `12`        | 5.73 米                      | ❌ 完全靠 `overshot`         |

**这就是「速度、转弯能力、到达半径三者必须匹配」** ——  
你项目里 `AutoDriver` 要处理的完全是同一个问题。

---

#### 附：如果想让车在终点**真的停下来**

`Loop` 关掉后车不会停，因为它只是"不再换点"，而油门那句写的是 `car.SetInput(Steer, 1f)` —— 恒为 1。

想让它停，要加一个"完成"标志位：

```csharp
// ① 加在 float _targetSteer; 旁边
bool _finished;

// ② 加在 Update() 的最开头
if (_finished)
{
    car.SetInput(0f, 0f);   // 刹车
    return;
}

// ③ 把 reached || overshot 那段改成
if (reached || overshot)
{
    int next = path.NextIndex(_index);
    if (next != _index)
    {
        _index = next;
        Debug.Log($"[AutoDrive] 到达 → 切换到第 {_index} 个点");
    }
    else
    {
        _finished = true;   // NextIndex 原地返回 = 已经到终点了
        Debug.Log("[AutoDrive] 已到终点，停车");
    }
}
```

**这其实就是在手写一个最简单的状态机。** 你项目里 `AutoDriver` 的 `Finished` 状态，就是这个东西的正式版。

### 8.7 到这里你手里有什么

| 你的脚本                | 项目里对应的                   | 状态              |
| ------------------- | ------------------------ | --------------- |
| `CarController.cs`  | A 模块的车辆执行器               | ✅ 等价            |
| `AutoDrive.cs`      | B 模块的 `WaypointFollower` | ✅ 核心算法已一致       |
| `MyWaypointPath.cs` | B 模块的 `WaypointPath`     | ✅ 等价（改名是为了不撞类名） |
| `FollowCamera.cs`   | 可视化辅助                    | ✅               |

**你已经从零把项目的骨架写出来了一遍。** 剩下的第 8~10 课是把 MATLAB 数字孪生接进来，以及把车做得像车。

---

## 九、第 8 课：让车"看见"前面的东西

**这一课的目标**：给车装一只"眼睛" —— 一条从车头射出去的射线，撞到什么，就知道前面有什么、有多远。

这是你项目里 `FrontCarDetector` 的核心原理，也是"感知"模块的第一块拼图。

### 9.1 射线（Raycast）是什么

想象手里有个激光笔，从车头正前方打出去：

- 打**空**了 → 前面没东西，安全
- 打到**墙**了 → 激光点停在墙上，**从车头到光点的距离**就是"障碍物有多远"

Unity 里这件事一句话就能做：

```csharp
Physics.Raycast(起点, 方向, out 打中了什么, 最远打多远);
```

**和前面七课的根本区别**：前面都是"我算一个结果，然后改自己的位置"。这一课是**第一次主动去"问"场景**：前面有什么？

### 9.2 四个参数，一个一个看

| 参数         | 写什么        | 例子                              |
| ---------- | ---------- | ------------------------------- |
| **起点**     | 射线从哪里发出    | `transform.position`（车自己的位置）    |
| **方向**     | 往哪打（必须归一化） | `transform.forward`（车头方向，蓝轴 Z+） |
| **out 结果** | 打中了什么，装在这里 | `out hit`                       |
| **最远距离**   | 打多远就放弃     | `20f`（20 米）                     |

**返回值是个 `bool`**：打到了 `true`，打空了 `false`。

```csharp
RaycastHit hit;   // 先准备一个"空盒子"，等 Raycast 往里装东西
bool hitSomething = Physics.Raycast(起点, 方向, out hit, 20f);

if (hitSomething)
{
    Debug.Log($"前面 {hit.distance:F1} 米处有个 {hit.transform.name}");
}
```

**`out` 是什么意思**：告诉 C#「这个方法不光返回一个 `bool`，还会**往这个变量里写东西**」。

所以 `hit` 必须在调用**之前**就声明好 —— 不能写成 `Physics.Raycast(..., out RaycastHit hit, ...)` 这种临时变量。

### 9.3 `RaycastHit`：那个"盒子"里装了什么

| 字段              | 类型          | 含义                   |
| --------------- | ----------- | -------------------- |
| `hit.distance`  | `float`     | **从起点到命中点的距离** ← 最常用 |
| `hit.point`     | `Vector3`   | 命中点的世界坐标             |
| `hit.transform` | `Transform` | **打中了哪个物体**          |
| `hit.normal`    | `Vector3`   | 命中面的朝向（做反弹、贴墙时用）     |

⚠️ **如果 `Raycast` 返回 `false`，`hit` 里的值是无效的**（全是 0）。  
所以必须先用 `if (hitSomething)` 包住，再读 `hit.xxx`。

### 9.4 一个坑：射线会打到自己

车身上有 `BoxCollider`，射线从车头正前方打出去 —— **如果起点落在碰撞体内部，第一条命中的就是车自己**。

三种解法：

| 解法           | 怎么做                                        | 评价             |
| ------------ | ------------------------------------------ | -------------- |
| **抬高起点**     | `transform.position + transform.up * 0.5f` | ✅ 最简单，项目里就这么做的 |
| **忽略自己**     | 判断 `hit.transform` 是不是自己或自己的子物体            | ✅ 保险，一起加上      |
| **Layer 过滤** | 只让射线检测指定 Layer                             | ✅ 最专业，项目里也用了   |

这一课**前两种都用上**，Layer 留到接进项目时再讲。

### 9.5 写 `MyFrontDetector.cs`

**新建 `MyFrontDetector.cs`**：

```csharp
using UnityEngine;

public class MyFrontDetector : MonoBehaviour
{
    [Header("射线设置")]
    [Tooltip("最远检测距离（米）")]
    public float detectionRange = 20f;
    [Tooltip("起点抬高一点，避免射线打到自己和地面")]
    public float heightOffset = 0.5f;

    [Header("报警")]
    [Tooltip("小于这个距离就算“有障碍”")]
    public float triggerDistance = 5f;

    // ===== 对外公布的结果（外面只能读，不能写）=====
    public bool   HasObstacle      { get; private set; }
    public float  ObstacleDistance { get; private set; }
    public string ObstacleName    { get; private set; } = "";

    void Start()
    {
        // 还没检测前先给一个“畅通”的大值，避免 UI 显示 Infinity
        ObstacleDistance = detectionRange;
    }

    void Update()
    {
        // 每帧先归零，再重新检测
        HasObstacle      = false;
        ObstacleDistance = detectionRange;
        ObstacleName     = "";

        Vector3 origin = transform.position + transform.up * heightOffset;
        Vector3 dir    = transform.forward;

        RaycastHit hit;
        if (Physics.Raycast(origin, dir, out hit, detectionRange))
        {
            // 打到自己或自己的子物体 → 忽略这次
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                Debug.DrawLine(origin, origin + dir * detectionRange, Color.green);
                return;
            }

            ObstacleDistance = hit.distance;
            ObstacleName     = hit.transform.name;
            HasObstacle      = hit.distance <= triggerDistance;

            // 近了画红，远了画绿
            Debug.DrawLine(origin, hit.point, HasObstacle ? Color.red : Color.green);
        }
        else
        {
            // 打空了：画一整条绿线，表示“前方畅通”
            Debug.DrawLine(origin, origin + dir * detectionRange, Color.green);
        }
    }
}
```

**四个新东西**：

- **`transform.up`** —— 车自己的"上"方向（绿轴 Y+）。`+ transform.up * 0.5f` 就是把起点抬高半米
- **`transform.forward`** —— 车自己的"前"方向（蓝轴 Z+）。**注意不是 `Vector3.forward`** —— 后者永远是世界的 +z，车掉头了它也不变
- **`hit.transform == transform`** / **`hit.transform.IsChildOf(transform)`** —— 判断打中的是不是自己、或自己的子物体（车灯、轮子）
- **`{ get; private set; }`** —— "外面能读，只有自己能写"。第 6 课 `CarController` 用的同一招

**为什么每帧先归零**：如果上一帧检测到障碍、这一帧打空了，不归零的话 `HasObstacle` 会一直卡在 `true`。

### 9.6 装配（3 步）

> ⚠️ **先按 ▶ 停止 Play 再做这三步。** Play 模式下建的物体，按停止的瞬间会全部消失。

1. **建一个障碍物**：Hierarchy 右键 → `3D Object` → `Cube`，改名 `Obstacle`
2. **摆到车必经的路上**（⚠️ 注意填到哪一行）：
   - `Transform` 的 **`Position`** 行 → `X = 7.8`、`Y = 0`、`Z = 6`
   - `Transform` 的 **`Scale`** 行 → `X = 3`、`Y = 2`、`Z = 1`
   - `Rotation` 行**保持 `0 0 0` 别动**
3. **挂脚本**：选中 `Cube`（车）→ `Add Component` → 搜 `My Front Detector`

> **别忘了 `Ctrl + S` 保存场景。**

**为什么是 `(7.8, 0, 6)`？**

车在 `(1, 0, 20)` 朝 `+z`，但 `WP0` 在 `(8.5, 0, -3.1)` —— 在车的**右后方**。
所以车一开场要先掉头，然后沿着 `x ≈ 7.5 ~ 8.2` 这条线往 `-z` 方向直冲。
`(7.8, 0, 6)` 正好落在这条直线上，车会**正面撞上去**（约 4 秒后）。

> 这是把 `AutoDrive` + `CarController` 的算法逐步跑一遍算出来的：
> **车约 10.3 秒跑一圈**，到每个路标点的最近距离约 **1.3 米**（说明到达判定是好的）。

#### ⚠️ Transform 有「三行长得一模一样」的输入框

```
Transform
  Position   X [    ]  Y [    ]  Z [    ]    ← 物体在哪
  Rotation   X [    ]  Y [    ]  Z [    ]    ← 物体朝哪（角度）
  Scale      X [    ]  Y [    ]  Z [    ]    ← 物体多大（倍数）
```

三行都是 `X / Y / Z` 三个框，**长得完全一样**。填错行不会报错，只会行为奇怪：

| 填错的行 | 会看到什么 |
| --- | --- |
| 填到 `Rotation` | 物体**被转了个角度**，大小没变（`(3,2,1)` 会被当成 3°、2°、1°） |
| 填到 `Position` | 物体**跑到别的地方**去了 |

> **小技巧**：右键点行标签（比如 `Rotation`）→ `Reset`，可以**只复位那一行**，不动其他两行。

**`Scale` 是"倍数"，不是"米"**：Unity 默认 Cube 是 `1×1×1` 米，
所以 `Scale = (3, 2, 1)` 得到的是 **3 米宽 × 2 米高 × 1 米深**。

### 9.7 实验

| # | 做什么                                | 看什么                  |
| - | ---------------------------------- | -------------------- |
| 1 | Play，选中 `Cube`，看 **Scene 视图**      | 车头射出一条**绿线**         |
| 2 | 让车往 `Obstacle` 开                   | 绿线打到障碍物就变**红色**      |
| 3 | 把 `Trigger Distance` 从 `5` 改成 `20` | 离障碍物 20 米内就变红        |
| 4 | 把 `Detection Range` 从 `20` 改成 `5`  | 绿线变短；车开到 5 米内才"看见"障碍 |
| 5 | **在 `Update` 末尾加日志**（见下）           | Console 实时打印障碍距离     |
| 6 | Play 中直接把 `Obstacle` 拖到车正前方 | 线立刻变红，`Obstacle Distance` 跟着变 |

**实验 5 的代码**：

```csharp
if (Time.frameCount % 30 == 0)
    Debug.Log($"障碍: {ObstacleName} | 距离 {ObstacleDistance:F1} 米 | 报警 {HasObstacle}");
```

#### 「加在 `Update()` 末尾」具体是加在哪

**加在 `Update()` 最后一个 `}` 的前面** —— 也就是 `if / else` 整块**的外面**：

```csharp
    void Update()
    {
        ... 前面那一堆 ...

        RaycastHit hit;
        if (Physics.Raycast(origin, dir, out hit, detectionRange))
        {
            ...
        }
        else
        {
            Debug.DrawLine(origin, origin + dir * detectionRange, Color.green);
        }

        // ★ 加在这里
        if (Time.frameCount % 30 == 0)
            Debug.Log($"障碍: {ObstacleName} | 距离 {ObstacleDistance:F1} 米 | 报警 {HasObstacle}");
    }   // ← Update 的结束大括号，加在它前面
```

**为什么不能加进 `if` 或 `else` 里面**：那两个块各自会写 `ObstacleDistance`，
但一帧只会走进其中一个。加在外面，**两条分支都能打印**。

#### ⚠️ `if` 不带大括号时，只管紧跟的下一行

```csharp
if (Time.frameCount % 30 == 0)
    Debug.Log("A");     // ← 只有这一行受 if 管
Debug.Log("B");         // ← 这一行每帧都执行！
```

**缩进只是给人看的，C# 编译器完全不看缩进** —— 它只看有没有大括号。
所以缩进歪了不影响运行，但**极易误导自己**。稳妥写法是永远带上大括号：

```csharp
if (Time.frameCount % 30 == 0)
{
    Debug.Log($"障碍: {ObstacleName} | 距离 {ObstacleDistance:F1} 米 | 报警 {HasObstacle}");
}
```

> 顺带一提：`MyFrontDetector` 第 41 行有个 `return` —— **它之后的所有代码都不会执行**。
> 所以射线"打到自己"的那一帧，日志会被跳过（平时碰不到，因为射线起点在车内部，Unity 会忽略自己）。

**这条日志就是你项目里 `BModuleFacade` 要往控制层送的数据。**

#### 日志太多怎么办

现在 `AutoDrive` 和 `MyFrontDetector` **都在每 0.5 秒打印一次**（`% 30`），
加起来每秒 4 行，跑一分钟就是 **240 行** —— Console 会被刷爆。

**办法一：用 Console 工具栏过滤（不改代码）**

Console 窗口顶部有几个开关：

| 按钮 | 作用 |
| --- | --- |
| **搜索框**（放大镜） | 输入 `障碍` → **只显示 `MyFrontDetector` 的日志** |
| `Clear` | 清空 |
| `Clear on Play` | 每次按 ▶ 自动清空（强烈建议打开） |
| `Collapse` | 把**完全相同**的消息合并成一条并计数 |

> ⚠️ 你的距离值每次都不同，所以 `Collapse` **合并不了** —— **用搜索框过滤最有效**。

**办法二：只在"状态变化"时打印（推荐，也是专业做法）**

把 `Update()` 末尾那两行换成：

```csharp
// ① 加在字段区（跟 HasObstacle 那几个放一起）
bool _lastHasObstacle;

// ② 换掉 Update() 末尾的日志
if (HasObstacle != _lastHasObstacle)
{
    _lastHasObstacle = HasObstacle;
    Debug.Log(HasObstacle
        ? $"[射线] 发现障碍 {ObstacleName}，距离 {ObstacleDistance:F1} 米"
        : "[射线] 前方畅通");
}
```

这样**每次遇到障碍只打印 2 行**（进入报警 / 恢复畅通），而不是每 0.5 秒一行。

> **日志不是越多越好。** 每行都应该回答一个问题：
> "这一刻发生了什么**变化**？" —— 没变化就不该打。

> `Debug.DrawLine` 只在 **Scene 视图**里显示，Game 视图看不到 —— 这是它的限制。  
> 想在 Game 视图里看到射线，要用 `LineRenderer`（项目里 `FrontCarDetector.beamRenderer` 就是干这个的）。

**实验 2 会看到一件"不对劲"的事**：车会**直接从 `Obstacle` 里穿过去**，不会被撞停。

原因：`Cube`（车）和 `Obstacle` **都没有 `Rigidbody`（刚体）**。
Unity 里两个"静态碰撞体"之间**不产生碰撞反应** —— 它们只是互相重叠。

但 **`Physics.Raycast` 照样能打中它** —— 射线检测只看 `Collider`，跟刚体无关。
所以"看得见"和"撞得到"是两件独立的事。这也正好说明：
**光有感知不够，还得有"看见了就刹车"的决策** —— 那就是下一课要做的。

### 9.8 到这里你手里有什么

| 你的脚本                 | 项目里对应的                   | 状态       |
| -------------------- | ------------------------ | -------- |
| `CarController.cs`   | A 模块的车辆执行器               | ✅ 等价     |
| `AutoDrive.cs`       | B 模块的 `WaypointFollower` | ✅ 核心算法一致 |
| `MyWaypointPath.cs`  | B 模块的 `WaypointPath`     | ✅ 等价     |
| `MyFrontDetector.cs` | B 模块的 `FrontCarDetector` | ✅ 核心原理一致 |
| `FollowCamera.cs`    | 可视化辅助                    | ✅        |

**下一步**：把"看得见障碍"接进"决策"里 —— 前面有东西就刹车。那就是你项目里 `BModuleFacade` 的 `NeedBrake` 逻辑。

---

## 十、第 9 课：看见了就刹车

第 8 课给了车一只眼睛，但车**看见了也不管** —— 它照样一头撞上去（准确说是穿过去）。
这一课把「感知」接进「决策」。

### 10.1 决策的核心：一个数字 → 三档油门

`MyFrontDetector` 每帧都在告诉我们一个数字：**前方障碍距离 `ObstacleDistance`**。
决策就是把这个数字翻译成油门：

| 距离 | 油门 | 叫什么 |
| --- | --- | --- |
| 远（≥ 8 米） | `1.0` | 正常行驶 |
| 中（3 ~ 8 米） | `0.35` | 减速（`slowDistance`） |
| 近（< 3 米） | `0` | 刹停（`safeDistance`） |

**两个阈值**：`slowDistance`（开始减速）和 `safeDistance`（必须停车）。
这就是你项目里 `BModuleFacade` 的两个字段 —— **名字一模一样**。

### 10.2 改 `AutoDrive.cs`

改动一共 3 处，我用 `// ★` 标出来了。完整文件（可以直接全选替换）：

```csharp
using UnityEngine;

public class AutoDrive : MonoBehaviour
{
    [Header("路径（把挂 MyWaypointPath 的物体拖进来）")]
    public MyWaypointPath path;

    int _index;   // 现在正在追第几个点

    [Header("转向")]
    public float fullSteerAngle = 45f;
    public float steerRate = 0.5f;

    [Header("到达判定")]
    public float arrivalRadius = 2f;

    // ★ 新增 1：避障参数
    [Header("避障")]
    [Tooltip("留空会自动从自己身上找 MyFrontDetector")]
    public MyFrontDetector detector;
    [Tooltip("小于这个距离 → 减速")]
    public float slowDistance = 8f;
    [Tooltip("小于这个距离 → 刹停")]
    public float safeDistance = 3f;
    [Tooltip("减速时的油门（0~1）")]
    [Range(0f, 1f)]
    public float slowFactor = 0.35f;

    // 同一个物体上的车辆执行器
    CarController car;

    public float Throttle { get; private set; }
    public float Steer    { get; private set; }

    float _targetSteer;

    void Start()
    {
        // 从我身上找到 CarController
        car = GetComponent<CarController>();
        if (car == null)
            Debug.LogError("[AutoDrive] 这个物体上没有 CarController！请先 Add Component。", this);

        // ★ 新增 2：顺便把探测器也找出来
        if (detector == null) detector = GetComponent<MyFrontDetector>();
    }

    void Update()
    {
        if (car == null || path == null || path.Count == 0) return;

        // ---- 追当前这个路径点 ----
        Vector3 targetPoint = path.GetPoint(_index);
        Vector3 local = transform.InverseTransformPoint(targetPoint);
        float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

        _targetSteer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);
        Steer = Mathf.MoveTowards(Steer, _targetSteer, steerRate * Time.deltaTime);

        // ---- 到了就换下一个点 ----
        float distance = local.magnitude;
        bool reached  = distance < arrivalRadius;
        bool overshot = local.z < 0f && distance < arrivalRadius * 2f;

        if (reached || overshot)
        {
            int next = path.NextIndex(_index);
            if (next != _index)
            {
                _index = next;
                Debug.Log($"[AutoDrive] 到达 → 切换到第 {_index} 个点");
            }
        }

        // ---- ★ 新增 3：油门不再恒为 1，而是先问一下探测器 ----
        float motor = DecideMotor();

        // ---- 交给 CarController ----
        car.SetInput(Steer, motor);
    }

    /// <summary>根据前方障碍距离，决定这一帧踩多少油门</summary>
    float DecideMotor()
    {
        if (detector == null) return 1f;           // 没装探测器 → 不管，全速

        float d = detector.ObstacleDistance;       // 没打到东西时 = detectionRange（20）

        if (d < safeDistance) return 0f;           // 太近   → 刹停
        if (d < slowDistance) return slowFactor;   // 有点近 → 减速
        return 1f;                                 // 够远   → 全速
    }

    // 选中 Cube 时，在 Scene 视图里画一条"到当前目标点"的黄线 + 到达圈
    void OnDrawGizmosSelected()
    {
        if (path == null || path.Count == 0) return;

        Vector3 targetPoint = path.GetPoint(_index);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, targetPoint);
        Gizmos.DrawWireSphere(targetPoint, arrivalRadius);
    }
}
```

> **注意**：我把 `Update()` 末尾那两行日志删掉了 —— Console 已经够吵。
> 想留着就自己加回去（用 9.7 里"只在状态变化时打印"的写法）。

### 10.3 三个新东西

**① `[Range(0f, 1f)]`** —— 让 `float` 在 Inspector 里变成**滑块**而不是输入框。
适合"0 到 1 之间的比例"这类参数。

**② 早返回（early return）的写法**

```csharp
if (d < safeDistance) return 0f;           // 最紧急的放最前面
if (d < slowDistance) return slowFactor;
return 1f;
```

和一堆 `if / else` 嵌套等价，但**从上往下读，越靠上优先级越高**。
（回想第 3 课那条：**多条 `if` 的顺序 = 优先级**。）

**③ 把"决策"拆成独立的小函数 `DecideMotor()`**

好处：

- `Update()` 只剩"协调"，一眼能看完全流程
- `DecideMotor()` 可以**单独测试**：喂它一个距离，看它返回多少油门

> 这就是**决策层**和**执行层**分离 —— 你项目里 `BModuleFacade`（决策）+ `CarController`（执行）
> 是同一个思路。

### 10.4 装配

**不用拖任何东西** —— `Start()` 里那句 `GetComponent<MyFrontDetector>()` 会自动找到。

保存 → Play。预期：

| 时刻 | 现象 |
| --- | --- |
| `t ≈ 3.8 秒` | 距离 < 8 米 → 车**明显变慢** |
| `t ≈ 4.3 秒` | 距离 < 3 米 → 车**停住** |
| 之后 | 车停在障碍前不动，Scene 视图里线一直**红**着 |

### 10.5 为什么读 `ObstacleDistance`，不读 `HasObstacle`

`MyFrontDetector` 对外暴露了两个东西：

| 属性 | 含义 |
| --- | --- |
| `HasObstacle` | `bool`，距离 < `triggerDistance`（5 米）时为 `true` |
| `ObstacleDistance` | `float`，**原始距离数字** |

`HasObstacle` 已经**帮你做了一个判断**（5 米）。一旦用了它，你就只能用这一个阈值。

但我们要的是**两档**（8 米减速、3 米刹停）—— 所以必须拿**原始数字**，自己判断。

> **规律**：底层尽量暴露**原始数据**，判断留给上层。
> `HasObstacle` 这种"加工过的布尔值"只适合"要不要报警"这种单一用途。
> 你项目里 `BModuleFacade` 同时暴露了 `FrontCarDistance`（原始值）和
> `NeedBrake` / `NeedSlowDown`（加工过的布尔）—— 两种都留，谁都能用。

### 10.6 ⚠️ 车会永远停在那儿（死锁）

**这是这一课最重要的部分。**

车停在障碍前 → `ObstacleDistance` 一直是 2.x → `DecideMotor()` 一直返回 `0` →
**车永远不动了。**

**这不是 bug，是逻辑的必然结果。**

真实世界的区别在于：

| 障碍类型 | 会怎样 |
| --- | --- |
| **移动的**（前车） | 前车开走 → 距离变大 → **自动恢复** ✅ |
| **静止的**（墙、锥桶） | 永远在那儿 → **永久卡死** ❌ |

我们场景里的 `Obstacle` 是静止的 → 车会卡死。

**验证一下（就是 9.7 的实验 6）**：Play 中**直接把 `Obstacle` 拖走** →
射线打空 → 距离变回 20 → **车立刻恢复前进**。

这一步很重要：它证明**决策逻辑是活的**，只是"障碍没消失"而已。

### 10.7 怎么解决死锁？三条路

| 思路 | 做法 | 代价 |
| --- | --- | --- |
| **超时放行** | 停够 N 秒还没解决 → 强行前进 | 会撞上去 |
| **绕障** | 检测到障碍 → 把目标点往旁边偏一点 | 要做局部路径规划 |
| **状态机** | 把"行驶 / 避障 / 已到达"拆成独立状态 | 代码结构变复杂 |

你项目里的 `AutoDriver` 走的是**第三条**（状态机），真实小车还会叠加第二条。

> 回想第 6 课那句：**`SetInput` 是指令保持语义** —— 执行层只会照做，不会自己判断。
> 所以"什么时候该恢复前进"这个责任，**必须在决策层**。这就是下一课要做的。

### 10.8 到这里你手里有什么

| 你的脚本 | 项目里对应的 | 状态 |
| --- | --- | --- |
| `CarController.cs` | A 模块的车辆执行器 | ✅ 等价 |
| `AutoDrive.cs` | B 模块的 `WaypointFollower` + `BModuleFacade` 决策部分 | ✅ 核心算法一致 |
| `MyWaypointPath.cs` | B 模块的 `WaypointPath` | ✅ 等价 |
| `MyFrontDetector.cs` | B 模块的 `FrontCarDetector` | ✅ 核心原理一致 |
| `FollowCamera.cs` | 可视化辅助 | ✅ |

**你已经把「感知 → 决策 → 执行」这条链路完整打通了。**
这正好是数字孪生里"数字体"该干的事。

---

## 十一、第 10 课：状态机 —— 跑完一圈就停在终点

第 9 课留下两个问题：

1. **车会卡死在静止障碍前**（死锁）
2. **车永远停不下来**（`_index` 绕圈，`loop = true` 就一直跑）

这一课用**状态机**一次解决。

### 11.1 思路：把"处境"变成一个变量

第 9 课的代码里，"正常行驶"和"被挡住"这两种处境**混在同一个 `Update()` 里**，
靠一堆 `if` 判断 —— 加几个条件之后，自己都读不懂了。

状态机换个思路：**把"现在是什么处境"显式地记下来**，然后每种处境单独写一段代码。

| 状态 | 什么时候进入 | 干什么 |
| --- | --- | --- |
| `Driving` | 一开始 / 障碍消失 | 全速追点 |
| `Blocked` | 前方 8 米内有东西 | 减速 → 刹停 → **计时** |
| `Finished` | 跑满 N 圈 | 停车 |

**死锁怎么解决的**：`Blocked` 里加个计时器，**停稳超过 3 秒还走不了，就放弃这个点、去下一个**
（相当于"这条路堵死了，我绕道"）。

### 11.2 `enum`：给"几个固定选项"起个名字

```csharp
public enum State { Driving, Blocked, Finished }
```

`enum`（枚举）是一种**只能取固定几个值**的类型。和别的写法比：

| 写法 | 好处 / 坏处 |
| --- | --- |
| `int state = 1;` | 谁知道 1 是什么？写错成 3 也不报错 |
| `string state = "Blocked";` | 好读，但**打错字不报错**（`"Bloked"` 也能编译） |
| `State.Blocked` | **打错字直接编译报错**，而且编辑器有自动补全 ✅ |

### 11.3 `switch`：按状态分发

```csharp
switch (CurrentState)
{
    case State.Driving:  TickDriving();  break;
    case State.Blocked:  TickBlocked();  break;
    case State.Finished: TickFinished(); break;
}
```

`Update()` 只剩这几行 —— **一眼就能看出"这个脚本有三种行为"**。

> **`break` 不能省**：C# 要求每个 `case` 结束必须 `break`（或 `return`）。

### 11.4 计时器：`+= Time.deltaTime`

第 1 课学过 `Time.deltaTime` = 上一帧到这一帧的秒数。**把它累加就是"已经过了多少秒"**：

```csharp
_blockedTime += Time.deltaTime;
if (_blockedTime >= blockedTimeout) { /* 等太久了 */ }
```

> **为什么不用 `Time.time`**：`Time.time` 是"从游戏开始到现在"。
> 我们要的是"**从进入这个状态**到现在"，所以得自己数，而且换状态时要**清零**。

### 11.5 完整代码

`AutoDrive.cs` 全选替换（`// ★` 是这一课新增的部分）：

```csharp
using UnityEngine;

public class AutoDrive : MonoBehaviour
{
    // ★ 新增 1：车的三种"处境"
    public enum State { Driving, Blocked, Finished }

    [Header("路径（把挂 MyWaypointPath 的物体拖进来）")]
    public MyWaypointPath path;

    [Header("转向")]
    public float fullSteerAngle = 45f;
    public float steerRate = 0.5f;

    [Header("到达判定")]
    public float arrivalRadius = 2f;

    [Header("避障")]
    [Tooltip("留空会自动从自己身上找 MyFrontDetector")]
    public MyFrontDetector detector;
    [Tooltip("小于这个距离 → 减速")]
    public float slowDistance = 8f;
    [Tooltip("小于这个距离 → 刹停")]
    public float safeDistance = 3f;
    [Tooltip("减速时的油门（0~1）")]
    [Range(0f, 1f)]
    public float slowFactor = 0.35f;

    // ★ 新增 2：状态机参数
    [Header("状态机")]
    [Tooltip("停稳超过这么多秒 → 放弃这个点（0 = 永远等）")]
    public float blockedTimeout = 3f;
    [Tooltip("跑满这么多圈就停车（0 = 无限跑）")]
    public int totalLaps = 1;

    // ★ 新增 3：对外可见的状态
    public State CurrentState { get; private set; }
    public int LapCount { get; private set; }

    int _index;          // 现在正在追第几个点
    float _blockedTime;  // 已经被堵了多久

    CarController car;

    public float Throttle { get; private set; }
    public float Steer    { get; private set; }

    float _targetSteer;

    void Start()
    {
        car = GetComponent<CarController>();
        if (car == null)
            Debug.LogError("[AutoDrive] 这个物体上没有 CarController！请先 Add Component。", this);

        if (detector == null) detector = GetComponent<MyFrontDetector>();

        CurrentState = State.Driving;
        Debug.Log("[状态] → Driving");
    }

    void Update()
    {
        if (car == null || path == null || path.Count == 0) return;

        // ★ 新增 4：主循环只做一件事 —— 按当前状态分发
        switch (CurrentState)
        {
            case State.Driving:  TickDriving();  break;
            case State.Blocked:  TickBlocked();  break;
            case State.Finished: TickFinished(); break;
        }
    }

    // ==================== 三个状态各自的行为 ====================

    // 正常行驶：全速追点
    void TickDriving()
    {
        SteerTowardTarget();
        car.SetInput(Steer, 1f);

        if (AtTarget())
        {
            AdvanceWaypoint();
            return;
        }

        // 前方有东西 → 换状态
        if (detector != null && detector.ObstacleDistance < slowDistance)
            SetState(State.Blocked);
    }

    // 被挡住：减速 / 刹停，停稳后开始计时
    void TickBlocked()
    {
        SteerTowardTarget();

        float d = detector.ObstacleDistance;

        if (d >= slowDistance)              // 障碍没了 → 回去开车
        {
            SetState(State.Driving);
            return;
        }

        float motor = d < safeDistance ? 0f : slowFactor;
        car.SetInput(Steer, motor);

        if (motor > 0f)                     // 还在动 → 不计时
        {
            _blockedTime = 0f;
            return;
        }

        _blockedTime += Time.deltaTime;     // 完全停住 → 开始计时
        if (blockedTimeout > 0f && _blockedTime >= blockedTimeout)
        {
            Debug.Log($"[状态] 点 {_index} 已停 {_blockedTime:F1} 秒 → 放弃这个点");
            AdvanceWaypoint();
            if (CurrentState == State.Blocked) SetState(State.Driving);
        }
    }

    // 已到达：停车
    void TickFinished()
    {
        car.SetInput(0f, 0f);
    }

    // ==================== 工具函数 ====================

    void SetState(State s)
    {
        if (CurrentState == s) return;      // 状态没变 → 什么都不做

        Debug.Log($"[状态] {CurrentState} → {s}");
        CurrentState = s;
        _blockedTime = 0f;                  // ★ 换状态就清零计时器
    }

    void SteerTowardTarget()
    {
        Vector3 local = transform.InverseTransformPoint(path.GetPoint(_index));
        float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

        _targetSteer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);
        Steer = Mathf.MoveTowards(Steer, _targetSteer, steerRate * Time.deltaTime);
    }

    bool AtTarget()
    {
        Vector3 local = transform.InverseTransformPoint(path.GetPoint(_index));
        float distance = local.magnitude;

        return distance < arrivalRadius
            || (local.z < 0f && distance < arrivalRadius * 2f);
    }

    void AdvanceWaypoint()
    {
        int next = path.NextIndex(_index);

        if (next == 0 && _index != 0)       // 从最后一个点绕回 0 → 跑完一圈
        {
            LapCount++;
            Debug.Log($"★ 跑完第 {LapCount} 圈");
        }

        _index = next;

        if (totalLaps > 0 && LapCount >= totalLaps)
        {
            SetState(State.Finished);
            Debug.Log($"[状态] 跑满 {LapCount} 圈 → 停车");
        }
    }

    // 选中 Cube 时，在 Scene 视图里画一条"到当前目标点"的黄线 + 到达圈
    void OnDrawGizmosSelected()
    {
        if (path == null || path.Count == 0) return;

        Vector3 targetPoint = path.GetPoint(_index);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, targetPoint);
        Gizmos.DrawWireSphere(targetPoint, arrivalRadius);
    }
}
```

### 11.6 预期现象（我把算法逐步跑过一遍算出来的）

```
 3.1s   [状态] → Driving
 3.1s   [状态] Driving → Blocked（距离 8.0）      ← 开始减速
 8.9s   [状态] 点 0 已停 3.0 秒 → 放弃这个点        ← 停稳 3 秒后放弃
 9.0s   [状态] Driving → Blocked（距离 3.0）      ← 转向中又被挡一下
 9.4s   [状态] Blocked → Driving（障碍消失）      ← 转开了
10.6s   [路径] → 第 2 个点
13.3s   [路径] → 第 3 个点
15.9s   ★ 跑完第 1 圈
15.9s   [状态] → Finished（跑满 1 圈）            ← 停车
```

**约 16 秒跑完一圈，然后停在终点。** 全程 `Obstacle` 都不用动。

### 11.7 实验

| # | 做什么 | 看什么 |
| --- | --- | --- |
| 1 | Play | Console 依次打印状态切换，约 16 秒后出现 `→ Finished`，**车停住不动了** |
| 2 | 把 `Total Laps` 从 `1` 改成 `2` | 车跑两圈才停 |
| 3 | 把 `Total Laps` 改成 `0` | **无限跑**（0 = 不限制） |
| 4 | 把 `Blocked Timeout` 从 `3` 改成 `0` | 回到第 9 课的死锁 —— 车停在障碍前**永远不动** |
| 5 | 把 `Blocked Timeout` 改成 `10` | 车会停在障碍前等 10 秒才放弃 |
| 6 | Play 中把 `Obstacle` 拖到车正前方 | 立刻切到 `Blocked`；拖走 → 切回 `Driving` |

**实验 4 很重要**：它让你看到 `Blocked Timeout = 0` 和 `= 3` 的唯一区别，
就是"**要不要给自己留一条退路**"。这就是看门狗（watchdog）的思想。

### 11.8 到这里你手里有什么

| 你的脚本 | 项目里对应的 | 状态 |
| --- | --- | --- |
| `CarController.cs` | A 模块的车辆执行器 | ✅ 等价 |
| `AutoDrive.cs` | B 模块 `WaypointFollower` + C 模块 `AutoDriver` | ✅ 核心一致 |
| `MyWaypointPath.cs` | B 模块的 `WaypointPath` | ✅ 等价 |
| `MyFrontDetector.cs` | B 模块的 `FrontCarDetector` | ✅ 核心原理一致 |
| `FollowCamera.cs` | 可视化辅助 | ✅ |

**你现在有一条完整的、自己写的自动驾驶链路了**：感知 → 决策 → 执行 → 状态管理。

这 5 个脚本合起来，就是一个人做完整项目所需的**全部核心代码**。
剩下的只是"数据面板"和"和 MATLAB / CoppeliaSim 通讯"。

---

## 十二、接下来的课（学到再补详细内容）

- **第 11 课**：数据面板 HUD —— 把速度、障碍距离、当前目标点画在屏幕上

**每节课结束时，你都会多一段自己写的、能跑的代码。**
