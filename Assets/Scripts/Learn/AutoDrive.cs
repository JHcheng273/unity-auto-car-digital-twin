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
