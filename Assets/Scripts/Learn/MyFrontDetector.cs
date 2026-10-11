using UnityEngine;

/// <summary>
/// 前向障碍检测 —— ★ 已从"单条射线"升级为"五射线扇形"。
///
/// 【为什么必须改（2026-10-11）】
///   原来只用 transform.forward 发**一条**射线。实测盲区高达 50%：
///     · 射线只能覆盖车头中线 ±0.8 m
///     · 但车体真正会撞上的范围是 ±1.6 m
///        （车半宽 0.80 + 障碍物半径 0.80）
///     · 障碍物横向偏 0.9 m 时，射线打不到，但车会撞上去 → **穿模**
///   更糟的是 ObstacleManager 生成障碍物时会强制让它"至少偏 1.5 m"
///   （本意是别贴脸，结果正好把障碍物放进了检测盲区），
///   于是按 O 键加障碍物，车完全看不见它，直接开过去。
///
/// 【现在怎么测】
///   在车头前方横向排 5 条平行射线（间距 0.8 m，覆盖 ±2.4 m）：
///
///        -1.6   -0.8    0    +0.8   +1.6      ← 射线横向位置（米）
///          |      |      |      |      |
///          ↓      ↓      ↓      ↓      ↓
///        ╔═══════════════════════════════╗
///        ║            车 头               ║
///        ╚═══════════════════════════════╝
///
///   取所有命中里**最近的那个**当 ObstacleDistance，
///   同时记下命中的是哪一条 → 就能算出障碍物在左边还是右边（LateralOffset）。
///
/// 【兼容性】
///   所有老字段（detectionRange / triggerDistance / heightOffset /
///   HasObstacle / ObstacleDistance / ObstacleName）**名字和含义都没变**，
///   AutoDrive、AvoidanceDriver、HudDisplay 不用改一行。
///   新增的只是 LateralOffset / HitSide / HitCount 这些只读量。
/// </summary>
public class MyFrontDetector : MonoBehaviour
{
    [Header("射线设置")]
    [Tooltip("最远检测距离（米）")]
    public float detectionRange = 20f;
    [Tooltip("起点抬高一点，避免射线打到自己和地面")]
    public float heightOffset = 0.5f;

    [Header("扇形覆盖（★ 决定会不会漏检）")]
    [Tooltip("射线数量。奇数最好（中间那条正对车头）。5 条覆盖 ±2.4 米，约 0.8 米一条")]
    public int rayCount = 5;
    [Tooltip("射线之间的横向间距（米）。间距 ≤ 障碍物直径才不会从缝里漏过去")]
    public float raySpacing = 0.8f;
    [Tooltip("检测起点相对车头往前推多少米（别让保险杠挡住自己）")]
    public float originOffset = 0.2f;

    [Header("报警")]
    [Tooltip("小于这个距离就算「有障碍」")]
    public float triggerDistance = 5f;

    [Header("调试")]
    [Tooltip("勾选后在 Scene 视图里画出射线（红=命中近，黄=命中远，绿=没打到）")]
    public bool drawRays = true;
    [Tooltip("勾选后每 2 秒在 Console 打一次检测结果。平时关掉，否则会刷屏淹没其它日志")]
    public bool logStats = false;

    // ==================== 对外只读量 ====================

    /// <summary>前方有障碍（距离 ≤ triggerDistance）</summary>
    public bool   HasObstacle      { get; private set; }
    /// <summary>最近的障碍距离（米）。没打到就是 detectionRange</summary>
    public float  ObstacleDistance { get; private set; }
    /// <summary>最近的障碍物体名字。没打到是空串</summary>
    public string ObstacleName    { get; private set; } = "";

    /// <summary>
    /// 最近障碍的横向偏移（米）。负 = 偏左，正 = 偏右。
    /// ★ 这是单射线时代拿不到的 —— 绕障时可以直接知道该往哪边躲。
    /// 粗略值（射线间距量级），不需要很准。
    /// </summary>
    public float  LateralOffset   { get; private set; }
    /// <summary>命中的是第几条射线（0=最左 → rayCount-1=最右）。-1 = 没命中</summary>
    public int    HitRayIndex     { get; private set; } = -1;
    /// <summary>本轮有多少条射线打到了东西（≥2 说明障碍物比较宽）</summary>
    public int    HitCount        { get; private set; }

    /// <summary>障碍物在左边还是右边（用于绕行方向偏好）。0 = 正中</summary>
    public int    HitSide
    {
        get
        {
            if (HitRayIndex < 0) return 0;
            int mid = rayCount / 2;
            return HitRayIndex - mid;
        }
    }

    // ==================== 内部 ====================

    // ★ 预设几个方向的数组，避免每帧新建 List。
    //   射线数量变了会自动重新分配（RebuildOffsets）。
    float[] _offsets;      // 每条射线的横向偏移
    RaycastHit[] _hits;    // 复用同一个数组，省 GC

    void OnEnable()  => RebuildOffsets();
    void OnValidate()
    {
        rayCount   = Mathf.Clamp(rayCount, 1, 21);
        raySpacing = Mathf.Max(raySpacing, 0.1f);
        RebuildOffsets();
    }

    void Start()
    {
        RebuildOffsets();
        ObstacleDistance = detectionRange;
    }

    /// <summary>按 rayCount / raySpacing 重算每条射线的横向偏移</summary>
    void RebuildOffsets()
    {
        int n = Mathf.Clamp(rayCount, 1, 21);
        if (_offsets == null || _offsets.Length != n)
        {
            _offsets = new float[n];
            _hits    = new RaycastHit[n];
        }

        // 对称铺开：n=5 时得到 [-1.6, -0.8, 0, +0.8, +1.6]（spacing=0.8）
        float mid = (n - 1) * 0.5f;
        for (int i = 0; i < n; i++)
            _offsets[i] = (i - mid) * raySpacing;
    }

    void Update()
    {
        // 每帧先归零，再重新检测
        HasObstacle      = false;
        ObstacleDistance = detectionRange;
        ObstacleName     = "";
        LateralOffset    = 0f;
        HitRayIndex      = -1;
        HitCount         = 0;

        int    n      = _offsets.Length;
        float  best   = detectionRange;
        int    bestI  = -1;
        string bestNm = "";
        float  bestLat = 0f;
        int    hits   = 0;

        for (int i = 0; i < n; i++)
        {
            // 起点：车头中线抬 heightOffset，再按本次的横向偏移挪到一边，
            //       最后往前推一点（originOffset），免得保险杠自己挡住射线
            Vector3 origin = transform.position
                           + transform.up      * heightOffset
                           + transform.right   * _offsets[i]
                           + transform.forward * originOffset;
            Vector3 dir    = transform.forward;

            RaycastHit hit;
            if (!Physics.Raycast(origin, dir, out hit, detectionRange)) 
            {
                if (drawRays) Debug.DrawLine(origin, origin + dir * detectionRange, Color.green);
                continue;
            }

            // 打到自己或自己的子物体 → 忽略这一条
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                if (drawRays) Debug.DrawLine(origin, origin + dir * detectionRange, Color.green);
                continue;
            }

            hits++;

            // ★ 取最近的那条当结果
            if (hit.distance < best)
            {
                best    = hit.distance;
                bestI   = i;
                bestNm  = hit.transform.name;
                bestLat = _offsets[i];
            }

            if (drawRays)
                Debug.DrawLine(origin, hit.point,
                    hit.distance <= triggerDistance ? Color.red : Color.yellow);
        }

        HitCount = hits;

        if (bestI >= 0)
        {
            ObstacleDistance = best;
            ObstacleName     = bestNm;
            HitRayIndex      = bestI;
            LateralOffset    = bestLat;
            HasObstacle      = best <= triggerDistance;
        }

        // 默认不打日志 —— 以前每 30 帧打一次，几秒钟就把 Console 刷爆，
        // 把 MatlabBridge 的「已发 N 包」全淹没了，排查通信问题时根本找不到。
        // 需要时在 Inspector 上勾 logStats。
        if (logStats && Time.time - _lastLog > 2f)
        {
            _lastLog = Time.time;
            Debug.Log($"[MyFrontDetector] 障碍「{ObstacleName}」距离 {ObstacleDistance:F1} 米，" +
                      $"横向偏移 {LateralOffset:+0.0;-0.0;0.0} 米，命中 {HitCount}/{_offsets.Length} 条，" +
                      $"报警 = {HasObstacle}");
        }
    }

    float _lastLog;

    // ==================== 给绕障用的查询 ====================

    /// <summary>
    /// 障碍物是不是真的"挡在车要走的路上"？
    /// ★ 比单射线时代准得多：直接用 LateralOffset（障碍物横向偏移），
    ///   不用再"沿车头方向前进 ObstacleDistance 米取个点"去猜。
    /// </summary>
    /// <param name="clearance">横向容差（米）。偏移小于它就认为挡路</param>
    public bool IsBlocking(float clearance)
    {
        if (HitRayIndex < 0) return false;
        return Mathf.Abs(LateralOffset) < clearance;
    }

    /// <summary>
    /// 障碍物在车体宽度内会不会撞上？（比 IsBlocking 宽松）
    /// 用于"虽然偏一点，但车体还是会蹭到"的场景。
    /// </summary>
    /// <param name="bodyHalfWidth">车半宽（米），Learn 版车是 0.8</param>
    /// <param name="obstacleRadius">障碍物半径（米），动态障碍物是 0.8</param>
    public bool IsInCollisionWidth(float bodyHalfWidth = 0.8f, float obstacleRadius = 0.8f)
    {
        if (HitRayIndex < 0) return false;
        return Mathf.Abs(LateralOffset) < (bodyHalfWidth + obstacleRadius);
    }

    // ==================== 调试绘制 ====================

    void OnDrawGizmosSelected()
    {
        if (!drawRays || _offsets == null) return;

        Gizmos.color = new Color(0.2f, 0.8f, 0.3f, 0.8f);
        for (int i = 0; i < _offsets.Length; i++)
        {
            Vector3 origin = transform.position
                           + transform.up      * heightOffset
                           + transform.right   * _offsets[i]
                           + transform.forward * originOffset;
            Gizmos.DrawLine(origin, origin + transform.forward * detectionRange);
        }

        // 标出"车体真正会撞上"的横向范围（±1.6 m）
        Gizmos.color = new Color(0.9f, 0.5f, 0.1f, 0.5f);
        Vector3 c = transform.position + transform.up * heightOffset + transform.forward * 3f;
        Gizmos.DrawLine(c - transform.right * 1.6f, c + transform.right * 1.6f);
    }
}
