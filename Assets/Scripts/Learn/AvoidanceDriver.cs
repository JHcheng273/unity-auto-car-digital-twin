using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 绕障状态机 —— 急停 + A* 重新规划 + 绕过去后回归原路径。
///
/// 【它是怎么接进现有系统的】
///   现有调用链是：AutoDrive → CarController.SetInput(steer, motor, brake)
///   这个脚本**不去改 AutoDrive**（那是已定稿的代码，改了风险大），
///   而是在它后面插一层：AutoDrive 每帧算完输入后，本脚本再决定要不要覆盖。
///
///   执行顺序用 `LateUpdate` 保证：LateUpdate 天然晚于所有 Update，
///   所以不用去配 Execution Order 也不会打架。
///
/// 【五个状态】
///   Normal    —— 什么都不做，AutoDrive 自己开
///   Braking   —— 前方太近，刹死。同时开始算绕行路径
///   Detouring —— 沿着 A* 算出来的路径开，绕开障碍物
///   Recovering—— 绕过最后一个绕行点，回原路径的下一个航点
///
/// 【为什么急停和绕行要分开】
///   撞上之前必须先停下。一边算路径一边往前冲，算得慢一点就撞了。
///   所以 Braking 状态先把车刹稳，路径在同一帧就算好（A* 只要几毫秒），
///   停稳后立刻切 Detouring —— 视觉上就是"猛地一停，然后绕过去"。
///
/// 【★ 2026-10-11 修了「加障碍物后穿模」】
///   两个原因叠加：
///     ① 检测只有一条射线，覆盖 ±0.8 米，而车体实际会撞上的范围是 ±1.6 米
///        → 障碍物偏一点就看不见（详见 MyFrontDetector.cs 的注释）
///     ② 本脚本只在 Detouring 状态检查"障碍物有没有变"，
///        Normal 状态下你按 O 键加障碍物，它**要等射线打到才反应**
///     修法：
///     · 检测升级成 5 射线扇形（MyFrontDetector）
///     · 障碍物变动统一在 LateUpdate **最开头**处理，覆盖全部状态
///       → 见 OnObstaclesChanged()，加完障碍物下一帧就有反应
///     · IsBlockingPath 改用检测器给的横向偏移，不再靠猜
/// </summary>
public class AvoidanceDriver : MonoBehaviour
{
    public enum AvoidState { Normal, Braking, Detouring, Recovering }

    [Header("触发")]
    [Tooltip("小于这个距离 → 急停（米）。★ 要比 ObstacleManager 的 minSafetyDistance 小，" +
             "否则障碍物一生成就已经在急停圈里了")]
    public float emergencyDistance = 4.5f;
    [Tooltip("小于这个距离 → 提前开始规划绕行（米）")]
    public float prepareDistance = 9f;
    [Tooltip("障碍物横向要挡住多少才值得绕？挡不住的（在旁边）直接过")]
    [Range(0.2f, 2f)]
    public float lateralClearance = 1.2f;

    [Header("栅格（A* 用的地图）")]
    [Tooltip("栅格覆盖范围：以场景中心为原点，边长多少米")]
    public float gridExtent = 60f;
    [Tooltip("每格多大。越小路径越精细，但格子数按平方涨。0.75 左右比较平衡")]
    public float cellSize = 0.75f;
    [Tooltip("障碍物膨胀量（米）。留出半个车宽的余量，路径才不会贴着障碍物擦过去")]
    public float obstaclePadding = 1.0f;

    [Header("绕行")]
    [Tooltip("绕过去之后往前多走多少米才算「绕干净了」，可以回原路径")]
    public float clearAhead = 6f;
    [Tooltip("绕行时最高车速（米/秒）。绕障时慢一点更稳")]
    public float detourSpeed = 2.4f;
    [Tooltip("绕行时朝路径点打方向的比例上限")]
    [Range(0.3f, 1f)]
    public float detourSteerLimit = 0.85f;
    [Tooltip("卡住超过这么多秒 → 放弃绕行，直接跳过当前航点")]
    public float stuckTimeout = 8f;

    [Header("急停")]
    [Tooltip("刹车后车速低于这个值就算「停稳了」（米/秒）")]
    public float stoppedSpeed = 0.25f;
    [Tooltip("停稳后等这么久再起步绕（秒），让你看清「它真的停了」")]
    public float pauseAfterStop = 0.35f;

    [Header("引用（留空自动找）")]
    public AutoDrive      drive;
    public CarController  car;
    public MyFrontDetector detector;
    public ObstacleManager obstacles;
    public MatlabBridge   bridge;

    [Header("调试")]
    public bool logStats = true;
    [Tooltip("在 Scene 视图里画出栅格和绕行路径")]
    public bool drawPath = true;

    // ---------- 对外可见 ----------
    public AvoidState State { get; private set; } = AvoidState.Normal;
    public IReadOnlyList<Vector3> CurrentPath => _path;

    // ---------- 内部 ----------
    AStarGrid _grid;
    List<Vector3> _path;          // 当前绕行路径（世界坐标）
    int    _pathIndex;            // 走到路径的第几个点了
    Vector3 _goalWhenPlanned;     // 规划时瞄的是哪个航点 —— 它变了说明该重规划
    bool   _overrideThisFrame;    // 本帧是否要覆盖 AutoDrive 的输出

    float  _stoppedTime;          // 停稳多久了
    float  _stuckTime;            // 绕行中卡住多久了
    float  _lastLogTime;
    int    _replanCount;

    void Start()
    {
        if (drive     == null) drive     = GetComponent<AutoDrive>();
        if (car       == null) car       = GetComponent<CarController>();
        if (detector  == null) detector  = GetComponent<MyFrontDetector>();
        if (bridge    == null) bridge    = GetComponent<MatlabBridge>();

        if (obstacles == null) obstacles = FindObjectOfType<ObstacleManager>();

        if (drive == null || car == null)
        {
            Debug.LogError("[AvoidanceDriver] 找不到 AutoDrive 或 CarController，已禁用", this);
            enabled = false;
            return;
        }

        // 栅格：以场景原点为中心，向四周铺开
        int cols = Mathf.Max(4, Mathf.RoundToInt(gridExtent * 2f / cellSize));
        int rows = cols;
        _grid = new AStarGrid(
            new Vector3(-gridExtent, 0f, -gridExtent),
            cellSize, cols, rows);

        if (logStats)
            Debug.Log($"[AvoidanceDriver] 就绪。栅格 {cols}×{rows}（格边长 {cellSize} 米，" +
                      $"覆盖 {gridExtent * 2}×{gridExtent * 2} 米）\n" +
                      $"  急停距离 {emergencyDistance} 米，提前规划距离 {prepareDistance} 米");
    }

    /// <summary>
    /// ★ 用 LateUpdate 而不是 Update —— 确保在 AutoDrive.Update() 之后跑。
    ///   脚本执行顺序没配好时，这一条是"最后一道保险"。
    /// </summary>
    void LateUpdate()
    {
        if (drive == null) return;

        _overrideThisFrame = false;

        // ★★ 放在状态分发**之前** —— 这是"添加障碍物后立刻响应"的关键。
        //   原来只在 Detouring 分支里查 obstacles.Dirty，后果是：
        //     · 车在 Normal 状态开得好好的，你按 O 键加个障碍物 →
        //       这一帧没人管 Dirty，要等到下一帧射线打到才反应；
        //       而射线以前只有一条（±0.8 米），障碍物偏一点就打不到 →
        //       **车完全无视，直接穿过去**。
        //   现在：只要障碍物列表变了，先重新标栅格；如果新障碍物
        //   已经挡在必经之路上、且距离够近，**立刻**进急停流程。
        if (obstacles != null && obstacles.Dirty)
        {
            obstacles.ClearDirty();
            OnObstaclesChanged();
        }

        switch (State)
        {
            case AvoidState.Normal:     TickNormal();     break;
            case AvoidState.Braking:    TickBraking();    break;
            case AvoidState.Detouring:  TickDetouring();  break;
            case AvoidState.Recovering: TickRecovering(); break;
        }

        // 覆盖 AutoDrive 刚设的输入
        if (_overrideThisFrame)
            car.SetInput(_steerOut, _motorOut, _brakeOut);

        // 调试日志（每 2 秒一条，别刷屏）
        if (logStats && Time.time - _lastLogTime > 2f)
        {
            _lastLogTime = Time.time;
            string extra = State == AvoidState.Detouring
                ? $"  路径点 {_pathIndex}/{(_path == null ? 0 : _path.Count)}"
                : "";
            string lat = detector != null && detector.HitRayIndex >= 0
                ? $"  横向{detector.LateralOffset:+0.0;-0.0;0.0}米"
                : "";
            Debug.Log($"[AvoidanceDriver] 状态={State}  障碍距离=" +
                      $"{(detector != null ? detector.ObstacleDistance.ToString("F1") : "无检测器")} 米{lat}{extra}");
        }
    }

    /// <summary>
    /// ★ 障碍物列表刚变过 —— 立刻重新标栅格；如果正被堵着，马上重规划。
    /// 这是把"过几秒才反应"变成"下一帧就反应"的关键函数。
    /// </summary>
    void OnObstaclesChanged()
    {
        // 栅格必须马上重铺，否则 A* 还是照旧地图算
        RefreshGrid();

        // 车正在绕行 → 旧路径作废，立刻重算（不然会撞上新加的障碍物）
        if (State == AvoidState.Detouring)
        {
            if (logStats) Debug.Log("[AvoidanceDriver] 障碍物有变动 → 立刻重新规划绕行路径");
            TryPlanDetour();
            return;
        }

        // 车在正常行驶 / 回归路上 → 看一眼新障碍物是不是正好挡住去路
        if (State == AvoidState.Normal || State == AvoidState.Recovering)
        {
            if (detector == null) return;

            // ★ 判定用 IsBlocking(车半宽 + 一点余量)，
            //   而不是要求"正对车头"—— 只要在车体宽度内会撞上就要处理。
            bool blocking = detector.IsInCollisionWidth(0.8f, 0.8f);

            if (blocking && detector.ObstacleDistance < prepareDistance)
            {
                if (logStats)
                    Debug.Log($"[AvoidanceDriver] 新障碍物挡住去路" +
                              $"（{detector.ObstacleDistance:F1} 米，" +
                              $"横向 {detector.LateralOffset:+0.0;-0.0;0.0} 米）→ 立即处理");

                if (detector.ObstacleDistance < emergencyDistance)
                    EnterBraking();          // 太近，先刹住
                else
                    TryPlanDetour();         // 还来得及，先把绕行路径算好
            }
        }
    }

    /// <summary>把当前所有障碍物重新标到栅格上（每个规划周期都要铺一次）</summary>
    void RefreshGrid()
    {
        if (_grid == null || obstacles == null) return;
        _grid.ClearAll();
        obstacles.StampOnto(_grid, obstaclePadding);
    }

    float _steerOut, _motorOut;
    bool  _brakeOut;

    // ==================== Normal ====================

    void TickNormal()
    {
        // Unity 本地模式才接管；MATLAB 接管速度时避障归 MATLAB 管，不插手
        if (IsMatlabDriving()) return;
        if (detector == null) return;

        float d = detector.ObstacleDistance;

        // ★ 判定"挡不挡路"用两个条件取或：
        //   ① IsBlockingPath()      —— 障碍物在车头中线附近（lateralClearance 以内）
        //   ② IsBlockingBody()      —— 障碍物虽偏，但在车体宽度内（±1.6 米）会撞上
        //   只用 ① 的话，障碍物横向偏 1.3 米（车会蹭到）时会被判成"不用管"直接开过去。
        //   加上 ② 之后，只有真正在旁边的（偏移 > 1.6 米）才会放行。
        if (!IsBlockingPath() && !IsBlockingBody()) return;

        if (d < emergencyDistance)
        {
            EnterBraking();
            return;
        }

        // 还没到急停距离，但已经很近了 → 提前把路径算好，等真刹停时能立刻走
        if (d < prepareDistance && (_path == null || _path.Count == 0))
        {
            TryPlanDetour();
        }
    }

    // ==================== Braking ====================

    void EnterBraking()
    {
        SetState(AvoidState.Braking);
        _stoppedTime = 0f;

        // ★ 进入刹车的同时就把路径算出来 —— A* 只要几毫秒，
        //   这样停稳的瞬间就能起步，不用再等计算
        TryPlanDetour();
    }

    void TickBraking()
    {
        // 刹死
        _steerOut = SteerTowardPathOrTarget();
        _motorOut = 0f;
        _brakeOut = true;
        _overrideThisFrame = true;

        // 障碍物自己没了（被删掉/移开）→ 直接回去开
        if (detector != null && detector.ObstacleDistance >= prepareDistance && !IsBlockingPath())
        {
            if (logStats) Debug.Log("[AvoidanceDriver] 障碍物已消失，回到正常行驶");
            SetState(AvoidState.Normal);
            return;
        }

        // 停稳判定
        if (car.CurrentSpeed <= stoppedSpeed)
        {
            _stoppedTime += Time.deltaTime;

            if (_stoppedTime >= pauseAfterStop)
            {
                if (_path != null && _path.Count > 0)
                {
                    _pathIndex = 0;
                    _stuckTime = 0f;
                    SetState(AvoidState.Detouring);
                    if (logStats)
                        Debug.Log($"[AvoidanceDriver] 已刹停，开始绕行（路径 {_path.Count} 个点）");
                }
                else
                {
                    // 没算出路径 → 再试一次；还不行就只能等（比如障碍把路全堵死了）
                    if (!TryPlanDetour())
                    {
                        if (_stoppedTime > stuckTimeout)
                        {
                            Debug.LogWarning("[AvoidanceDriver] 绕不过去，跳过当前航点");
                            drive.ForceSkipWaypoint();
                            SetState(AvoidState.Normal);
                        }
                    }
                }
            }
        }
        else
        {
            _stoppedTime = 0f;   // 还在动 → 重新计时
        }
    }

    // ==================== Detouring ====================

    void TickDetouring()
    {
        if (_path == null || _pathIndex >= _path.Count)
        {
            SetState(AvoidState.Recovering);
            return;
        }

        Vector3 target = _path[_pathIndex];

        // 到达当前绕行点 → 下一个
        Vector3 local = transform.InverseTransformPoint(target);
        if (local.magnitude < 1.6f || (local.z < 0f && local.magnitude < 3.2f))
        {
            _pathIndex++;
            if (_pathIndex >= _path.Count)
            {
                SetState(AvoidState.Recovering);
                return;
            }
            target = _path[_pathIndex];
        }

        // 转向：朝绕行点打方向
        Vector3 loc = transform.InverseTransformPoint(target);
        float angle = Mathf.Atan2(loc.x, loc.z) * Mathf.Rad2Deg;
        float steer = Mathf.Clamp(angle / drive.fullSteerAngle, -1f, 1f);
        _steerOut = Mathf.Clamp(steer, -detourSteerLimit, detourSteerLimit);

        // 速度：按剩余距离远近给油门。接近路径点时慢下来，免得冲过头
        float remain = loc.magnitude;
        float want   = Mathf.Min(detourSpeed, Mathf.Max(0.6f, remain * 0.9f));
        _motorOut = Mathf.Clamp01(want / Mathf.Max(car.maxSpeed, 0.01f));
        _brakeOut = false;
        _overrideThisFrame = true;

        // 卡住检测
        if (car.CurrentSpeed < 0.15f) _stuckTime += Time.deltaTime;
        else                          _stuckTime = 0f;

        if (_stuckTime > stuckTimeout)
        {
            Debug.LogWarning($"[AvoidanceDriver] 绕行中卡住 {_stuckTime:F1} 秒 → 重新规划");
            _stuckTime = 0f;
            if (!TryPlanDetour())
            {
                drive.ForceSkipWaypoint();
                SetState(AvoidState.Normal);
            }
        }

        // （障碍物变动现在统一在 LateUpdate 开头处理 —— 那里更早、覆盖所有状态，
        //   不会再走到这里才反应。原来这里的那段 Dirty 检查已经挪走了。）

        // ★ 目标航点变了（车已经跑到了下一个点）→ 旧路径作废，得重新算。
        //   不判这一条的话，车会一直沿着"通向上一个航点"的旧路径开，
        //   看起来就是"绕过去之后跑偏了、回不到正路上"。
        Vector3 nowGoal = drive.CurrentTargetPoint;
        if (Vector3.Distance(nowGoal, _goalWhenPlanned) > 1.0f)
        {
            if (logStats)
                Debug.Log($"[AvoidanceDriver] 目标航点变了 → 重新规划" +
                          $"（旧目标 ({_goalWhenPlanned.x:F1}, {_goalWhenPlanned.z:F1})，" +
                          $"新目标 ({nowGoal.x:F1}, {nowGoal.z:F1})）");
            TryPlanDetour();
        }
    }

    // ==================== Recovering ====================

    void TickRecovering()
    {
        // ★ 回归路上又冒出新障碍（比如用户刚按了 O 键）→ 立刻重新急停绕障。
        //   不判这一条的话，车会闷头往障碍上撞。
        if (detector != null && IsBlockingPath() && detector.ObstacleDistance < emergencyDistance)
        {
            if (logStats) Debug.Log("[AvoidanceDriver] 回归路上又遇到障碍 → 重新急停");
            EnterBraking();
            return;
        }

        // 判断"绕干净了没有"：车正前方 clearAhead 米内还有没有障碍
        bool clear = true;
        if (detector != null)
            clear = detector.ObstacleDistance > clearAhead || !IsBlockingPath();

        if (clear)
        {
            if (logStats) Debug.Log("[AvoidanceDriver] 已绕过，回到 AutoDrive 控制");
            _path = null;
            SetState(AvoidState.Normal);
        }
        else
        {
            // 还没绕干净 → 继续朝原目标点开，但限速（保守一点）
            _steerOut = SteerTowardPathOrTarget();
            _motorOut = Mathf.Clamp01(detourSpeed / Mathf.Max(car.maxSpeed, 0.01f));
            _brakeOut = false;
            _overrideThisFrame = true;
        }
    }

    // ==================== 规划 ====================

    /// <summary>
    /// 用 A* 算一条绕过障碍物、通向下一个航点的路。
    /// 返回是否算成功。
    /// </summary>
    bool TryPlanDetour()
    {
        if (_grid == null || obstacles == null)
        {
            if (obstacles == null && logStats)
                Debug.LogWarning("[AvoidanceDriver] 场景里没有 ObstacleManager，没法标障碍物");
            return false;
        }

        Vector3 goal = drive.CurrentTargetPoint;

        // ---- 1. 重新铺障碍物 ----
        RefreshGrid();

        // ★ 这里**不要**无条件清除起点/终点周围的格子！
        //   踩过的坑：曾经写成"每次规划前把车周围 1.2 米清干净"，
        //   结果障碍物怼在车脸前时，这一清正好把障碍物自己也清掉了，
        //   于是 A* 在障碍物身上"开出"一条通道，车直接穿模开过去。
        //
        //   正确做法是交给 AStarGrid.FindPath 内部的 TryFindNearestFree：
        //   只有当起点/终点**恰好落在障碍格里**时，才就近挪一格。
        //   这样既解决了"起点被自己的膨胀圈住"，又不会把真障碍挖掉。

        // ---- 2. 搜索 ----
        List<Vector3> path = _grid.FindPath(transform.position, goal);

        if (path == null || path.Count < 2)
        {
            if (logStats)
                Debug.LogWarning($"[AvoidanceDriver] A* 没找到路（从 {transform.position} 到 {goal}）" +
                                 "\n  常见原因：① 障碍物把整条路堵死了 ② 目标点在栅格外" +
                                 "\n  提示：勾上 drawPath 在 Scene 视图里看红色格子");
            _path = null;
            return false;
        }

        _path         = path;
        _pathIndex    = 0;
        _goalWhenPlanned = goal;
        _replanCount++;

        if (logStats)
        {
            float len = 0f;
            for (int i = 1; i < path.Count; i++) len += Vector3.Distance(path[i - 1], path[i]);
            // ★ 目标点可能是固定环线的路标点，也可能是全局路线的路口 —— 用 RouteText 两种都覆盖
            Debug.Log($"[AvoidanceDriver] A* 规划成功（第 {_replanCount} 次）：" +
                      $"{path.Count} 个点，长 {len:F1} 米，目标 {drive.RouteText}");
        }

        return true;
    }

    // ==================== 判定 ====================

    /// <summary>
    /// 前方那个障碍物是不是真的"挡在路上"？
    ///
    /// ★ 2026-10-11 改：现在直接用检测器的 LateralOffset（横向偏移），
    ///   不再"沿车头方向前进 ObstacleDistance 米取个点再猜"。
    ///   原因是旧办法有两个毛病：
    ///     ① 它假设障碍物正好在车头中线上，而实际值来自单射线 —— 一旦
    ///        障碍物偏一点（没被射线打到），这个函数永远返回 false；
    ///     ② 新障碍物刚加进来时，射线那帧才建好，取点算出来的横偏会飘。
    ///   多射线检测器能直接给出障碍物横向在哪，判断就准了。
    /// </summary>
    bool IsBlockingPath()
    {
        if (detector == null) return false;
        if (detector.HitRayIndex < 0) return false;         // 没有任何射线打到东西

        return Mathf.Abs(detector.LateralOffset) < lateralClearance;
    }

    /// <summary>
    /// 障碍物是不是"在车体宽度内、会撞上"（比 IsBlockingPath 宽松）。
    /// 用于新增障碍物时的快速判断 —— 偏一点但车会蹭到，也得处理。
    /// </summary>
    bool IsBlockingBody()
    {
        if (detector == null) return false;
        return detector.IsInCollisionWidth(0.8f, 0.8f);
    }

    bool IsMatlabDriving()
    {
        return bridge != null && bridge.IsMatlabDriving;
    }

    /// <summary>朝当前绕行点或原目标点打方向</summary>
    float SteerTowardPathOrTarget()
    {
        Vector3 target;

        if (_path != null && _pathIndex < _path.Count)
            target = _path[_pathIndex];
        else
            target = drive.CurrentTargetPoint;

        Vector3 loc = transform.InverseTransformPoint(target);
        float angle = Mathf.Atan2(loc.x, loc.z) * Mathf.Rad2Deg;
        return Mathf.Clamp(angle / drive.fullSteerAngle, -1f, 1f);
    }

    void SetState(AvoidState s)
    {
        if (State == s) return;
        State = s;
        _stoppedTime = 0f;
        _stuckTime   = 0f;
    }

    // ==================== 调试绘制 ====================

    void OnDrawGizmosSelected()
    {
        if (!drawPath || _grid == null) return;

        _grid.DrawGizmos();

        if (_path != null && _path.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < _path.Count - 1; i++)
                Gizmos.DrawLine(_path[i], _path[i + 1]);

            Gizmos.color = Color.blue;
            for (int i = 0; i < _path.Count; i++)
                Gizmos.DrawSphere(_path[i], 0.25f);
        }
    }
}
