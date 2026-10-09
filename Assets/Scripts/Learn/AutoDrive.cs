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
