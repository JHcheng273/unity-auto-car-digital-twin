using System.Collections.Generic;
using UnityEngine;

public class AutoDrive : MonoBehaviour
{
    // ★ 新增 1：车的三种"处境"
    public enum State { Driving, Blocked, Finished }

    [Header("路径（把挂 MyWaypointPath 的物体拖进来）")]
    public MyWaypointPath path;

    // ★ 2026-10-11 新增：全局 A* 寻路
    //   挂了 RoadNetwork 就走「路口图 A*」算出来的路线；
    //   没挂就退回原来的固定环线（path）。两条路都能跑，互不干扰。
    [Header("★ 全局 A* 寻路（留空 = 用上面的固定环线）")]
    [Tooltip("路网。挂上它，车就会在路口图上用 A* 找最短路，而不是死跟固定 4 点环线")]
    public RoadNetwork network;

    [Tooltip("★ 到达路口多远算「到了」（米）。要比 arrivalRadius 小一点")]
    public float junctionReach = 4f;

    [Tooltip("走到头了（到达终点路口）之后怎么办：勾上=停车，取消=继续沿环线绕")]
    public bool stopAtDestination = false;

    [Header("转向")]
    public float fullSteerAngle = 45f;
    public float steerRate = 0.5f;

    [Header("到达判定")]
    public float arrivalRadius = 2f;

    [Header("MATLAB 接管（留空会自动从自己身上找 MatlabBridge）")]
    public MatlabBridge bridge;

    [Header("MATLAB 速度控制（期望速度 → 油门）")]
    [Tooltip("前馈系数：期望速度 / 最大速度，直接给基础油门。稳态精度全靠它，别乱改")]
    [Range(0f, 1.5f)]
    public float speedFeedForward = 1f;
    [Tooltip("比例增益：速度误差 → 油门修正。注意闭环增益 = 本值 × maxSpeed，" +
             "超过 1 就会震荡（0.15 × 5 = 0.75，安全）")]
    public float speedKp = 0.15f;
    [Tooltip("比例项限幅（±多少油门），防止起步瞬间修正量过大导致超调")]
    [Range(0f, 1f)]
    public float speedCorrLimit = 0.30f;

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
    public int CurrentWaypointIndex => _index;

    // ==================== ★ 全局 A* 路线（2026-10-11 新增）====================

    /// <summary>
    /// 当前正在追的那个航点的世界坐标。
    /// 给 AvoidanceDriver 用 —— 它要拿这个当 A* 的目标点。
    /// （只读，不改变任何行为）
    ///
    /// ★ 现在有两条分支：走全局路线时返回"当前要去的路口坐标"，
    ///   否则还是返回固定环线的路标点。对外接口没变，调用方不用改。
    /// </summary>
    public Vector3 CurrentTargetPoint
    {
        get
        {
            if (UseGlobalRoute) return network.GetPosition(_route[_routeIndex]);
            return path != null ? path.GetPoint(_index) : transform.position;
        }
    }

    /// <summary>正在走全局 A* 路线（而不是固定环线）</summary>
    public bool UseGlobalRoute => network != null && _route != null && _route.Count > 0;

    /// <summary>当前路线上走到第几个路口</summary>
    public int RouteIndex => _routeIndex;

    /// <summary>当前路线上有多个路口</summary>
    public int RouteCount => _route == null ? 0 : _route.Count;

    /// <summary>全局路线的目标路口下标（-1 = 没有）</summary>
    public int DestinationJunction { get; private set; } = -1;

    /// <summary>路线的可读描述，HUD 用</summary>
    public string RouteText
    {
        get
        {
            if (!UseGlobalRoute) return path != null ? $"环线 WP{_index}/{path.Count}" : "无";
            return $"{network.PathToString(_route)}  ({_routeIndex}/{_route.Count})";
        }
    }

    // 路线状态
    List<int> _route;          // 路口下标序列
    int _routeIndex;           // 走到第几个
    float _replanTimer;        // 定期重规划用的计时器

    [Header("★ 全局路线重规划")]
    [Tooltip("每隔这么久重新算一次全局路线（秒）。" +
             "为什么需要：车开出去之后「离哪个路口最近」变了，" +
             "不定期重算的话它会一直盯着出发时那条路走")]
    public float replanInterval = 2.0f;

    /// <summary>
    /// 强制跳过当前航点，去追下一个。
    /// 给 AvoidanceDriver 用：绕障实在绕不过去时，跳过这个点免得卡死。
    /// 复用已有的 AdvanceWaypoint，所以圈数统计、Finished 判定都照常生效。
    /// </summary>
    public void ForceSkipWaypoint()
    {
        // 走全局路线时，"跳过"= 跳到下一个路口
        if (UseGlobalRoute)
        {
            int before = _routeIndex;
            _routeIndex++;
            if (_routeIndex >= _route.Count)
            {
                // 到头了 → 重算，或停车
                if (DestinationJunction >= 0 && stopAtDestination)
                {
                    SetState(State.Finished);
                    Debug.Log("[AutoDrive] 已到终点路口 → 停车");
                }
                else
                {
                    ReplanRoute();
                }
            }
            Debug.Log($"[AutoDrive] 被绕障接管层要求跳过路口：{before} → {_routeIndex}");
            return;
        }

        if (path == null || path.Count == 0) return;

        int b = _index;
        AdvanceWaypoint();
        Debug.Log($"[AutoDrive] 被绕障接管层要求跳过：WP{b} → WP{_index}");
    }

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
        if (bridge == null) bridge = GetComponent<MatlabBridge>();

        // ★ 全局路网：没手动指定就自动找
        if (network == null) network = FindObjectOfType<RoadNetwork>();

        if (network != null && network.JunctionCount > 0)
        {
            // 一开始没有目标 → 沿默认环线跑。用户点了目标才会切到全局路线。
            DestinationJunction = -1;
            _route = null;
            Debug.Log($"[AutoDrive] 找到路网（{network.JunctionCount} 个路口）—— " +
                      "点地面可以指定目标，届时用 A* 算最短路");
        }
        else if (path == null || path.Count == 0)
        {
            Debug.LogWarning("[AutoDrive] 既没有 RoadNetwork，也没有 Path —— 车不知道往哪开", this);
        }

        CurrentState = State.Driving;
        Debug.Log("[状态] → Driving");
    }

    void Update()
    {
        if (car == null) return;

        // 两条路至少要有一条能走
        bool canLoop = path != null && path.Count > 0;
        if (network == null && !canLoop) return;

        // ★ 走全局路线时，定期重算一次。
        //   为什么：车开出去后「离哪个路口最近」变了，起点变了路线也该跟着变。
        //   不重算的话它会一直盯着出发那一刻算的路走，看起来就是"绕远了也不改"。
        if (DestinationJunction >= 0 && network != null)
        {
            // ★★ 除了定时重算，还要**每帧检查**路线是不是已经失效。
            //   踩过的坑（Python 离线复现抓到的）：
            //     车开着开着，你把前面某个路口封了（Inspector 勾 Blocked）——
            //     光靠 2 秒的定时器，这 2 秒里车会**从被封的路口上直接开过去**，
            //     因为 A* 的结果早就算好了，路线里还带着那个路口。
            //   修法：每帧扫一眼当前路线剩余部分有没有被封的，
            //        有就立刻重算 —— 用户一勾 Block，下一帧车就改道。
            bool routeStale = false;
            if (_route != null)
            {
                for (int i = _routeIndex; i < _route.Count; i++)
                {
                    if (!network.IsJunctionOpen(_route[i])) { routeStale = true; break; }
                }
            }

            _replanTimer += Time.deltaTime;
            if (routeStale || _replanTimer >= replanInterval)
            {
                _replanTimer = 0f;
                if (routeStale && logRoute)
                    Debug.Log("[AutoDrive] 路线上有路口被封闭 → 立刻重算");
                ReplanRoute();
            }
        }

        // ★ 新增 4：主循环只做一件事 —— 按当前状态分发
        switch (CurrentState)
        {
            case State.Driving:  TickDriving();  break;
            case State.Blocked:  TickBlocked();  break;
            case State.Finished: TickFinished(); break;
        }
    }

    // ==================== ★ 全局 A* 路线 ====================

    /// <summary>
    /// 给车指定一个"终点路口"，立刻用 A* 算一条从当前位置过去的最短路。
    /// 由 MouseTargetSelector 调用。
    /// </summary>
    public void SetGlobalDestination(int junctionIndex)
    {
        if (network == null)
        {
            Debug.LogWarning("[AutoDrive] 没有 RoadNetwork，设不了全局目标");
            return;
        }

        DestinationJunction = junctionIndex;
        _replanTimer = 0f;

        if (ReplanRoute())
        {
            // 车到终点了就继续开（比如之前停了）
            if (CurrentState == State.Finished) SetState(State.Driving);
        }
    }

    /// <summary>取消全局目标 → 回到固定环线</summary>
    public void ClearGlobalDestination()
    {
        DestinationJunction = -1;
        _route = null;
        _routeIndex = 0;

        if (CurrentState == State.Finished) SetState(State.Driving);

        Debug.Log("[AutoDrive] 取消全局目标 → 回固定环线");
    }

    /// <summary>
    /// 真正调 A* 的地方。
    /// 起点 = 车现在离得最近的路口，终点 = DestinationJunction。
    /// </summary>
    bool ReplanRoute()
    {
        if (network == null || DestinationJunction < 0) return false;

        int from = network.NearestJunction(transform.position);
        if (from < 0) return false;

        // 已经就在终点路口了？说明到了
        if (from == DestinationJunction)
        {
            if (stopAtDestination)
            {
                Debug.Log($"[AutoDrive] 已到达终点路口「{network.GetName(DestinationJunction)}」→ 停车");
                _route = new List<int> { from };
                _routeIndex = 0;
                SetState(State.Finished);
            }
            else
            {
                // 不停车模式：绕一圈再来（换一个目标由用户决定）
                _route = null;
            }
            return true;
        }

        List<int> route = network.FindRoute(from, DestinationJunction);

        if (route == null || route.Count < 2)
        {
            Debug.LogWarning($"[AutoDrive] A* 找不到从「{network.GetName(from)}」" +
                             $"到「{network.GetName(DestinationJunction)}」的路 —— " +
                             "可能是中间的路口被手动封了");
            return false;
        }

        // ★ 关键修正：FindRoute 的第一项是"车当前所在的路口"，
        //   而车往往已经在它旁边了 —— 如果直接拿它当第一个目标，
        //   车会原地打转（目标就在脚下）。所以从第二项开始追。
        //   （只有路线上确实还有下一站时才这么做）
        if (route.Count >= 2)
        {
            Vector3 here = network.GetPosition(route[0]);
            if (Vector3.Distance(transform.position, here) < junctionReach * 0.9f)
                route.RemoveAt(0);
        }

        // ★ 只在路线真的变了的时候打日志，否则每 2 秒一条太吵
        bool changed = _route == null || _route.Count != route.Count;
        if (!changed)
        {
            for (int i = 0; i < route.Count; i++)
                if (route[i] != _route[i]) { changed = true; break; }
        }

        _route = route;
        _routeIndex = 0;

        if (changed && logRoute)
            Debug.Log($"[AutoDrive] ★ A* 全局路线：{network.PathToString(route)}  " +
                      $"（{network.RouteLength(route):F1} 米）");

        return true;
    }

    [Header("★ 全局路线日志")]
    [Tooltip("路线变化时在 Console 打一条。排查「车怎么绕远了」全靠它")]
    public bool logRoute = true;

    // ==================== 三个状态各自的行为 ====================

    // 正常行驶：追点 + 按「谁在开车」决定油门刹车
    void TickDriving()
    {
        ResolveControl(out float steer, out float motor, out bool brake);
        car.SetInput(steer, motor, brake);

        if (AtTarget())
        {
            AdvanceWaypoint();
            return;
        }

        // 前方有东西 → 换状态。
        // 但 MATLAB 接管速度时，避障决策归 MATLAB 管，本地不插手。
        if (!HasMatlabSpeed() && detector != null && detector.ObstacleDistance < slowDistance)
            SetState(State.Blocked);
    }

    // 被挡住：减速 / 刹停，停稳后开始计时
    void TickBlocked()
    {
        // MATLAB 接管了速度 → 本地避障让位，直接回去开车
        if (HasMatlabSpeed())
        {
            SetState(State.Driving);
            return;
        }

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

    /// <summary>MATLAB 是否在接管速度（v_des >= 0 就算接管）</summary>
    bool HasMatlabSpeed()
    {
        return bridge != null && bridge.GetDesiredSpeed() >= 0f;
    }

    /// <summary>MATLAB 是否在接管转向（steer_des 落在 [-1,1] 就算接管）</summary>
    bool HasMatlabSteer()
    {
        if (bridge == null) return false;
        float s = bridge.GetSteerCmd();
        return s >= -1f && s <= 1f;
    }

    /// <summary>
    /// 把 MATLAB 的期望速度换算成油门开度。
    ///
    ///   油门 = 前馈(v_des / maxSpeed) + 限幅后的比例修正
    ///
    /// 为什么必须带前馈：只靠比例的话，稳态时 v = v_des 需要 err = 0，
    /// 而 err = 0 时比例项输出 0，油门就归零了 —— 车会停，然后误差又出现，
    /// 来回抖。前馈项让稳态油门正好等于"维持这个速度需要的量"。
    ///
    /// 为什么比例项要限幅：起步时 v_des − v 很大，比例项会瞬间拉满，
    /// 车冲过头再回落，速度曲线变成锯齿。限幅后只是"温和地补一点"。
    /// </summary>
    float MatlabThrottle(float vDes)
    {
        float ff  = speedFeedForward * vDes / Mathf.Max(car.maxSpeed, 0.01f);
        float err = vDes - car.CurrentSpeed;
        float corr = Mathf.Clamp(err * speedKp, -speedCorrLimit, speedCorrLimit);
        return Mathf.Clamp01(ff + corr);
    }

    /// <summary>
    /// 算出这一帧该给执行器的三个量。
    /// 优先级：MATLAB 有指令就听 MATLAB，没有就用本地的路径跟踪。
    /// </summary>
    void ResolveControl(out float steer, out float motor, out bool brake)
    {
        SteerTowardTarget();

        steer = Steer;      // 本地默认：朝目标点打方向
        motor = 1f;         // 本地默认：全速
        brake = false;

        if (bridge == null) return;

        if (HasMatlabSteer()) steer = bridge.GetSteerCmd();

        float vDes = bridge.GetDesiredSpeed();
        if (vDes >= 0f) motor = MatlabThrottle(vDes);

        brake = bridge.GetBrakeCmd();
    }

    void SetState(State s)
    {
        if (CurrentState == s) return;      // 状态没变 → 什么都不做

        Debug.Log($"[状态] {CurrentState} → {s}");
        CurrentState = s;
        _blockedTime = 0f;                  // ★ 换状态就清零计时器
    }

    void SteerTowardTarget()
    {
        // ★ 两条分支：全局路线 → 朝当前路口打方向；否则朝固定环线路标点
        Vector3 worldTarget = CurrentTargetPoint;

        Vector3 local = transform.InverseTransformPoint(worldTarget);
        float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

        _targetSteer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);
        Steer = Mathf.MoveTowards(Steer, _targetSteer, steerRate * Time.deltaTime);
    }

    bool AtTarget()
    {
        Vector3 worldTarget = CurrentTargetPoint;

        Vector3 local = transform.InverseTransformPoint(worldTarget);
        float distance = local.magnitude;

        // ★ 全局路线用 junctionReach（路口到达半径），环线沿用 arrivalRadius
        float reach = UseGlobalRoute ? junctionReach : arrivalRadius;

        return distance < reach
            || (local.z < 0f && distance < reach * 2f);
    }

    void AdvanceWaypoint()
    {
        // ★ 走全局路线 → 推进到下一个路口
        if (UseGlobalRoute)
        {
            _routeIndex++;

            if (_routeIndex >= _route.Count)
            {
                // 到终点了
                if (stopAtDestination)
                {
                    Debug.Log($"[AutoDrive] ★ 已到达终点路口「{network.GetName(DestinationJunction)}」→ 停车");
                    SetState(State.Finished);
                }
                else
                {
                    // 不停车 → 重算一条（换个方向继续），或者回环线
                    if (DestinationJunction >= 0) ReplanRoute();
                    else                          _route = null;
                }
            }
            return;
        }

        if (path == null || path.Count == 0) return;

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
        // ★ 全局路线：把整条路线画出来，一眼看出 A* 选了什么路
        if (UseGlobalRoute)
        {
            Gizmos.color = Color.magenta;
            for (int i = 0; i < _route.Count; i++)
            {
                Vector3 p = network.GetPosition(_route[i]);
                Gizmos.DrawWireSphere(p, 1.2f);
                if (i > 0)
                    Gizmos.DrawLine(network.GetPosition(_route[i - 1]), p);
            }

            // 已经走过的部分涂暗一点
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            for (int i = 0; i < _routeIndex && i < _route.Count; i++)
                Gizmos.DrawWireSphere(network.GetPosition(_route[i]), 1.6f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, CurrentTargetPoint);
            Gizmos.DrawWireSphere(CurrentTargetPoint, junctionReach);
            return;
        }

        if (path == null || path.Count == 0) return;

        Vector3 targetPoint = path.GetPoint(_index);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, targetPoint);
        Gizmos.DrawWireSphere(targetPoint, arrivalRadius);
    }
}
