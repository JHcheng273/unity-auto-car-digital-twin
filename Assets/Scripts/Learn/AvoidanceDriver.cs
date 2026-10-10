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
///   执行顺序用「脚本执行顺序」保证：本脚本必须晚于 AutoDrive 执行。
///   在 Inspector 里点组件右上角 ⋮ → 执行顺序（Execution Order），
///   把本脚本拖到 AutoDrive 后面。或者游戏一运行本脚本会自动检查并警告。
///
/// 【四个状态】
///   Normal    —— 什么都不做，AutoDrive 自己开
///   Braking   —— 前方太近，刹死。同时开始算绕行路径
///   Detouring —— 沿着 A* 算出来的路径开，绕开障碍物
///   Recovering—— 绕过最后一个绕行点，回原路径的下一个航点
///
/// 【为什么急停和绕行要分开】
///   撞上之前必须先停下。一边算路径一边往前冲，算得慢一点就撞了。
///   所以 Braking 状态先把车刹稳，路径在同一帧就算好（A* 只要几毫秒），
///   停稳后立刻切 Detouring —— 视觉上就是"猛地一停，然后绕过去"。
/// </summary>
public class AvoidanceDriver : MonoBehaviour
{
    public enum AvoidState { Normal, Braking, Detouring, Recovering }

    [Header("触发")]
    [Tooltip("小于这个距离 → 急停（米）")]
    public float emergencyDistance = 4.0f;
    [Tooltip("小于这个距离 → 提前开始规划绕行（米）")]
    public float prepareDistance = 9.0f;
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
            Debug.Log($"[AvoidanceDriver] 状态={State}  障碍距离=" +
                      $"{(detector != null ? detector.ObstacleDistance.ToString("F1") : "无检测器")} 米{extra}");
        }
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

        // 障碍物不在正前方路线上 → 不用管
        if (!IsBlockingPath()) return;

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

        // 障碍物列表变了（用户又按了 O 键）→ 重新规划
        if (obstacles != null && obstacles.Dirty)
        {
            obstacles.ClearDirty();
            if (logStats) Debug.Log("[AvoidanceDriver] 障碍物有变动 → 重新规划");
            TryPlanDetour();
            return;
        }

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
        _grid.ClearAll();
        obstacles.StampOnto(_grid, obstaclePadding);

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
            Debug.Log($"[AvoidanceDriver] A* 规划成功（第 {_replanCount} 次）：" +
                      $"{path.Count} 个点，长 {len:F1} 米，到航点 WP{drive.CurrentWaypointIndex}");
        }

        return true;
    }

    // ==================== 判定 ====================

    /// <summary>
    /// 前方那个障碍物是不是真的"挡在路上"？
    /// 用射线检测到的点，看它相对车头中线的横向偏移。
    /// 障碍物在路边（偏得多）就不用绕，直接开过去。
    /// </summary>
    bool IsBlockingPath()
    {
        if (detector == null) return false;
        if (detector.ObstacleName == "") return false;      // 射线什么都没打到

        // 沿着车头方向，在检测到的距离处取一个点，看它偏中线多远
        Vector3 probe = transform.position + transform.forward * detector.ObstacleDistance;
        Vector3 local = transform.InverseTransformPoint(probe);

        return Mathf.Abs(local.x) < lateralClearance;
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
