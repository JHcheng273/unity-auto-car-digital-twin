using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 障碍物管理 —— 运行中随时添加 / 删除障碍物，不用回到编辑器摆。
///
/// 【怎么用】
///   把这个脚本挂在场景里任意一个空物体上（比如新建一个叫 "ObstacleManager" 的），
///   运行时：
///     O 键        → 在车前方随机位置生成一个障碍物（可以挡在正前方）
///     Shift + O   → 生成一个"大家伙"（更宽，逼着车绕远）
///     鼠标左键    → 在鼠标点中的地面位置生成障碍物
///     鼠标右键    → 删掉离鼠标最近的那个障碍物
///     1 / 2 / 3 / 4 / 5 → 清空 / 挡住正前方 / 摆3个 / 摆5个 / 摆成一条墙
///
/// 【为什么障碍物要"登记"起来】
///   绕障算法需要知道场上有哪些障碍、各自多大，才能把它们标到栅格上。
///   所以这里用一个列表统一管理，生成时登记、删除时注销。
///
/// 【★ 2026-10-11 修了一个"穿模"的坑】
///   原来生成时会强制把障碍物推到车头侧面 1.5 米以外（理由是不想贴脸）。
///   但当时的车前向检测只有**一条**射线（覆盖 ±0.8 米），
///   于是推出去的那些障碍物**正好落进检测盲区** ——
///   按 O 键加的障碍物车根本看不见，直接开过去，看起来就是"穿模"。
///   现在两头都修了：
///     · 检测改成 5 射线扇形（覆盖 ±2.4 米）—— MyFrontDetector.cs
///     · 生成不再强制推向侧面，改用"最小安全距离"防止贴脸
/// </summary>
public class ObstacleManager : MonoBehaviour
{
    [Header("生成位置（相对车）")]
    [Tooltip("在车前方这个距离范围内随机取一个位置")]
    public float spawnMinAhead = 6f;
    public float spawnMaxAhead = 16f;
    [Tooltip("横向偏移范围（左右各多少米）")]
    public float spawnLateral = 3.5f;
    [Tooltip("★ 允许障碍物挡在正前方（横向偏移可以很小）。" +
             "关掉的话障碍物会被强制推到侧面 1.5 米外 —— " +
             "那样正好落在检测盲区里，车看不见它就直接穿过去了")]
    public bool allowBlockingFront = true;
    [Tooltip("生成时至少离车这么远（米）。要大于 AvoidanceDriver 的急停距离，" +
             "否则障碍物一出现就贴脸，刹都来不及")]
    public float minSafetyDistance = 5.0f;

    [Header("障碍物尺寸")]
    public float boxWidth  = 1.6f;
    public float boxHeight = 1.6f;
    public float boxDepth  = 1.2f;
    [Tooltip("按 Shift 生成的大号障碍物尺寸")]
    public float bigScale = 1.9f;

    [Header("数量")]
    [Tooltip("进入 Play 时先摆几个（0 = 一个都不摆，全靠手动加）")]
    public int initialCount = 3;
    [Tooltip("场上最多允许几个（防止乱按按出一地）")]
    public int maxCount = 12;

    [Header("引用（留空会自动找）")]
    [Tooltip("车。留空就在场景里找带 AutoDrive 的物体")]
    public Transform car;

    [Header("调试")]
    public bool logStats = true;

    /// <summary>场上所有活着的障碍物 —— 绕障算法从这儿拿数据</summary>
    public readonly List<GameObject> Obstacles = new List<GameObject>();
    /// <summary>每个障碍物的半径（用于栅格膨胀），与 Obstacles 一一对应</summary>
    public readonly List<float> ObstacleRadius = new List<float>();

    /// <summary>刚刚新增过障碍物 —— 绕着它的车需要重新规划路径</summary>
    public bool Dirty { get; private set; }

    Material _matBody, _matStripe;
    Camera   _cam;
    int      _spawnCounter;

    void Start()
    {
        if (car == null)
        {
            AutoDrive d = FindObjectOfType<AutoDrive>();
            if (d != null) car = d.transform;
        }

        _cam = Camera.main;

        // 障碍物材质（橙色 + 黄色警示条），跟场景里原本那个 Obstacle 区分开
        _matBody   = MaterialLib.Solid("DynObstacleMat",   new Color(0.88f, 0.42f, 0.10f), 0.05f, 0.35f);
        _matStripe = MaterialLib.Solid("DynObstacleStripe", new Color(0.98f, 0.85f, 0.20f), 0.0f, 0.40f);

        for (int i = 0; i < initialCount; i++) SpawnRandom();

        if (logStats)
            Debug.Log($"[ObstacleManager] 就绪，初始摆好 {Obstacles.Count} 个障碍物。\n" +
                      "  ★ 左键=点击处加障碍   右键=不加障碍（让给 MouseTargetSelector 设目标）\n" +
                      "  ★ Delete 键 或 Ctrl+右键 = 删掉离鼠标最近的障碍物\n" +
                      "  O 键=随机加一个   Shift+O=加个大的\n" +
                      "  1=清空  2=挡住正前方(演示急停/绕障)  3=重摆3个  4=重摆5个  5=摆一堵墙");
    }

    void Update()
    {
        // ---- 键盘：随机生成 ----
        if (Input.GetKeyDown(KeyCode.O))
        {
            bool big = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            SpawnRandom(big);
        }

        // ---- 键盘：清空 / 重置 ----
        if (Input.GetKeyDown(KeyCode.Alpha1)) ResetTo(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SpawnBlockingFront();
        if (Input.GetKeyDown(KeyCode.Alpha3)) ResetTo(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ResetTo(5);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SpawnWall();

        // ---- 鼠标左键：在点击处放置障碍 ----
        // ★ 左键**只**干这一件事。设目标点归 MouseTargetSelector 的右键管，
        //   两者不再抢同一个键（之前左键既加障碍又设目标，车当然矛盾）。
        if (Input.GetMouseButtonDown(0) && _cam != null)
        {
            if (RayToGround(Input.mousePosition, out Vector3 hit))
                SpawnAt(hit, false);
            else if (logStats)
                Debug.LogWarning("[ObstacleManager] 鼠标没点到地面上 —— 注意地面物体得有 Collider");
        }

        // ---- 删除障碍：Delete 键，或 Ctrl + 右键 ----
        // ★ 原来删除是"单独的右键"。现在右键是"设目标"，
        //   所以删除让位成 Delete 键 / Ctrl+右键 —— 想删的时候仍然很快。
        bool wantDelete = Input.GetKeyDown(KeyCode.Delete);
        if (!wantDelete && Input.GetMouseButtonDown(1))
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            wantDelete = ctrl;
        }
        if (wantDelete) RemoveNearestToMouse();
    }

    // ==================== 生成 ====================

    /// <summary>在车前方随机位置生成一个</summary>
    public GameObject SpawnRandom(bool big = false)
    {
        if (Obstacles.Count >= maxCount)
        {
            if (logStats) Debug.Log($"[ObstacleManager] 已经有 {maxCount} 个了，先按右键删掉几个再加");
            return null;
        }

        if (car == null) { Debug.LogError("[ObstacleManager] 找不到车！"); return null; }

        // 车前方一段距离 + 左右随机偏移
        float ahead  = Random.Range(spawnMinAhead, spawnMaxAhead);
        float side   = Random.Range(-spawnLateral, spawnLateral);

        // ★★ 这里原来有一句"避开死角"的代码，是穿模的元凶之一：
        //      if (Mathf.Abs(side) < 1.5f) side = ±1.5f;
        //    本意是"别摆在车正前方贴脸"，但效果是**障碍物永远偏在侧面 1.5~3.5 米**，
        //    而当时的单射线只能覆盖 ±0.8 米 —— 于是按 O 键加的障碍物
        //    永远落在检测盲区，车完全看不见，直接穿过去。
        //
        //    正确做法：**允许挡住正前方**（那才是真正想演示的场景），
        //    改成靠"距离下限"保证不贴脸 —— 见下面的 minSafetyDistance。
        //    （检测侧已经升级成 5 射线 ±2.4 米，正前方的障碍物一定看得见）
        Vector3 pos = car.position + car.forward * ahead + car.right * side;
        pos.y = 0f;   // 贴地

        pos = EnforceMinDistance(pos, big);

        return SpawnAt(pos, big);
    }

    /// <summary>
    /// 把生成点推到离车至少 minSafetyDistance 米的地方。
    /// 原因：障碍物要是直接出现在车鼻子底下，急停也来不及，看着就像穿模。
    /// 只沿"车到该点"的方向往外推，不改变左右关系。
    /// </summary>
    Vector3 EnforceMinDistance(Vector3 pos, bool big)
    {
        if (car == null) return pos;

        Vector3 toPos = new Vector3(pos.x - car.position.x, 0f, pos.z - car.position.z);
        float   d     = toPos.magnitude;

        // 大号障碍物体积大，再多留一点余量
        float need = minSafetyDistance + (big ? 1.5f : 0f);

        if (d >= need || d < 1e-4f) return pos;

        Vector3 dir = toPos / d;
        Vector3 np  = car.position + dir * need;
        np.y = 0f;
        return np;
    }

    /// <summary>在指定世界位置生成一个障碍物</summary>
    public GameObject SpawnAt(Vector3 pos, bool big = false)
    {
        if (Obstacles.Count >= maxCount) return null;

        _spawnCounter++;

        float s = big ? bigScale : 1f;
        Vector3 size = new Vector3(boxWidth * s, boxHeight * (big ? 1.25f : 1f), boxDepth * s);

        // 根节点（只负责定位，不带碰撞体）
        GameObject root = new GameObject($"DynObstacle_{_spawnCounter}");
        root.transform.position = new Vector3(pos.x, 0f, pos.z);

        // 主体：带 Collider，前向射线才能打到它
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        body.transform.localScale    = size;
        body.GetComponent<MeshRenderer>().sharedMaterial = _matBody;

        // 顶上一圈黄条，远处一眼能认出是"动态加的"
        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = "Stripe";
        stripe.transform.SetParent(root.transform, false);
        stripe.transform.localPosition = new Vector3(0f, size.y - 0.12f, 0f);
        stripe.transform.localScale    = new Vector3(size.x * 1.06f, 0.16f, size.z * 1.06f);
        stripe.GetComponent<MeshRenderer>().sharedMaterial = _matStripe;
        Destroy(stripe.GetComponent<Collider>());   // 黄条不需要碰撞

        Obstacles.Add(root);
        ObstacleRadius.Add(Mathf.Max(size.x, size.z) * 0.5f);
        Dirty = true;

        if (logStats)
            Debug.Log($"[ObstacleManager] +{root.name} 位于 ({pos.x:F1}, {pos.z:F1})，" +
                      $"尺寸 {size.x:F1}×{size.z:F1}，场上共 {Obstacles.Count} 个");

        return root;
    }

    /// <summary>清空后重摆 count 个</summary>
    public void ResetTo(int count)
    {
        ClearAll();
        for (int i = 0; i < count; i++) SpawnRandom();
        if (logStats) Debug.Log($"[ObstacleManager] 重摆完毕，共 {Obstacles.Count} 个");
    }

    /// <summary>
    /// 直接在车正前方放一个障碍物（横向偏移≈0）。
    /// ★ 这是演示"识别 + 急停 + 绕障"最好用的一个 ——
    ///   因为障碍物结结实实挡在路线上，绕障一定会触发。
    ///   （以前这句会被强制推到侧面 1.5 米，看不到效果）
    /// </summary>
    public GameObject SpawnBlockingFront(float ahead = 10f, bool big = false)
    {
        if (car == null) { Debug.LogError("[ObstacleManager] 找不到车！"); return null; }

        Vector3 pos = car.position + car.forward * ahead;
        pos.y = 0f;
        pos = EnforceMinDistance(pos, big);

        var go = SpawnAt(pos, big);
        if (go != null && logStats)
            Debug.Log("[ObstacleManager] 已在车正前方放了一个（演示急停 + 绕障用这个）");
        return go;
    }

    /// <summary>
    /// 在车正前方横向摆一排障碍物（留一个缺口）。
    /// ★ 演示 A* 的另一个好场景：车不能直着穿过去，必须找缺口绕。
    /// gap 是缺口的横向位置（-1 左 / 0 中 / +1 右）。
    /// </summary>
    public void SpawnWall(float ahead = 12f, int count = 5, int gapIndex = 2)
    {
        if (car == null) { Debug.LogError("[ObstacleManager] 找不到车！"); return; }

        float spacing = 2.0f;
        float mid     = (count - 1) * 0.5f;
        int   made    = 0;

        for (int i = 0; i < count; i++)
        {
            if (i == gapIndex) continue;                  // 留个缺口让车有路可走
            if (Obstacles.Count >= maxCount) break;

            float side = (i - mid) * spacing;
            Vector3 pos = car.position + car.forward * ahead + car.right * side;
            pos.y = 0f;
            pos = EnforceMinDistance(pos, false);

            if (SpawnAt(pos, false) != null) made++;
        }

        if (logStats)
            Debug.Log($"[ObstacleManager] 摆了 {made} 个障碍组成一堵墙（在第 {gapIndex + 1} 个位置留了缺口）");
    }

    // ==================== 删除 ====================

    public void ClearAll()
    {
        for (int i = Obstacles.Count - 1; i >= 0; i--)
            if (Obstacles[i] != null) Destroy(Obstacles[i]);

        Obstacles.Clear();
        ObstacleRadius.Clear();
        Dirty = true;
    }

    void RemoveNearestToMouse()
    {
        if (!RayToGround(Input.mousePosition, out Vector3 hit)) return;
        if (Obstacles.Count == 0) return;

        int    bestI = -1;
        float  bestD = float.MaxValue;
        for (int i = 0; i < Obstacles.Count; i++)
        {
            if (Obstacles[i] == null) continue;
            float d = Vector3.Distance(Obstacles[i].transform.position, hit);
            if (d < bestD) { bestD = d; bestI = i; }
        }

        if (bestI >= 0 && bestD < 40f)
        {
            if (logStats)
                Debug.Log($"[ObstacleManager] -{Obstacles[bestI].name}（离鼠标 {bestD:F1} 米），" +
                          $"剩 {Obstacles.Count - 1} 个");
            Destroy(Obstacles[bestI]);
            Obstacles.RemoveAt(bestI);
            ObstacleRadius.RemoveAt(bestI);
            Dirty = true;
        }
    }

    /// <summary>鼠标射线打到地面上的哪个点</summary>
    bool RayToGround(Vector3 screenPos, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        Ray ray = _cam.ScreenPointToRay(screenPos);

        // 用一张无限大的数学平面 y=0 来求交，而不是 Physics.Raycast。
        // 因为 Physics.Raycast 会先打到障碍物/车/装饰物上，点在障碍物旁边
        // 就会把新障碍物放到障碍物顶上，很难用。
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float dist))
        {
            hitPoint = ray.GetPoint(dist);
            return true;
        }
        return false;
    }

    // ==================== 给绕障算法用 ====================

    /// <summary>
    /// 把当前所有障碍物标到栅格上。
    /// padding 是膨胀量（一般传半个车宽），避免路径贴着障碍物边过。
    /// </summary>
    public void StampOnto(AStarGrid grid, float padding)
    {
        for (int i = 0; i < Obstacles.Count; i++)
        {
            if (Obstacles[i] == null) continue;
            grid.BlockCircle(Obstacles[i].transform.position, ObstacleRadius[i], padding);
        }
    }

    /// <summary>拿走标记 —— 每次重新规划前都要先清掉上一次的痕迹</summary>
    public void ClearDirty() => Dirty = false;
}
