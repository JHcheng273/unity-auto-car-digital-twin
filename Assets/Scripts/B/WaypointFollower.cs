using UnityEngine;

/// <summary>
/// Waypoint 路径跟踪（成员 B 负责）
/// 输出：Steering（-1 左打满 ~ +1 右打满）、CurrentWaypointIndex、DistanceToTarget
///
/// 只负责"往哪打方向"，油门/刹车由 C 决定。
/// </summary>
public class WaypointFollower : MonoBehaviour
{
    [Header("引用")]
    public WaypointPath path;
    [Tooltip("用哪个物体的朝向做判断。一般就是车身自己，留空=自身")]
    public Transform steeringReference;

    [Header("参数")]
    [Tooltip("距离目标点多少米算到达（会覆盖 path 的值，>0 时生效）")]
    public float arrivalRadiusOverride = 0f;
    [Tooltip("转向灵敏度。车转不过弯就调大（1.5~3）")]
    public float steerGain = 2f;
    [Tooltip("打方向的满舵角度。45 表示偏差 45 度时打满")]
    public float fullSteerAngle = 45f;
    [Tooltip("到终点后是否循环（一般跟随 path 的设置）")]
    public bool usePathLoopSetting = true;

    [Header("绕障偏移（由上层/测试台写入，一般不用手改）")]
    [Tooltip("临时目标点偏移。零向量 = 不偏移；上层决定绕障时往哪边偏")]
    public Vector3 targetOffset = Vector3.zero;

    [Header("调试")]
    public bool showDebugLog = false;

    // ============ 对外接口 ============
    public int CurrentWaypointIndex { get; private set; }
    public float Steering { get; private set; }
    public float DistanceToTarget { get; private set; }
    public Vector3 TargetPoint { get; private set; }
    public bool ReachedDestination { get; private set; }
    public int LapCount { get; private set; }

    float ArrivalRadius =>
        arrivalRadiusOverride > 0f ? arrivalRadiusOverride
        : (path != null ? path.arrivalRadius : 3f);

    bool Loop => path != null && (usePathLoopSetting ? path.loop : false);

    void Start()
    {
        if (path == null)
            Debug.LogError("[WaypointFollower] 没拖 WaypointPath！请在 Inspector 里把 Path 拖进来。", this);
    }

    void Update()
    {
        if (path == null || path.Count == 0) return;

        Transform self = steeringReference != null ? steeringReference : transform;
        TargetPoint = path.GetPoint(CurrentWaypointIndex) + targetOffset;

        // --- 1. 算距离 ---
        DistanceToTarget = Vector3.Distance(self.position, TargetPoint);

        // --- 2. 把目标点转到车的局部坐标系 ---
        // local.x > 0 说明目标在右边 → 往右打方向
        // local.z < 0 说明目标在身后 → 说明冲过头了
        Vector3 local = self.InverseTransformPoint(TargetPoint);

        bool reached = DistanceToTarget < ArrivalRadius;
        bool overshot = local.z < 0f && DistanceToTarget < ArrivalRadius * 2f;

        if (reached || overshot)
        {
            AdvanceWaypoint();
            TargetPoint = path.GetPoint(CurrentWaypointIndex) + targetOffset;
            local = self.InverseTransformPoint(TargetPoint);
            DistanceToTarget = Vector3.Distance(self.position, TargetPoint);
            if (showDebugLog)
                Debug.Log($"[WaypointFollower] 到达，切换目标 -> WP{CurrentWaypointIndex}");
        }

        // --- 3. 算转向 ---
        // atan2(x, z) 得到目标相对车头的水平夹角，右为正
        float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        Steering = Mathf.Clamp(angleDeg / fullSteerAngle * steerGain, -1f, 1f);
    }

    void AdvanceWaypoint()
    {
        int next = CurrentWaypointIndex + 1;
        if (next >= path.Count)
        {
            if (Loop)
            {
                CurrentWaypointIndex = 0;
                LapCount++;
            }
            else
            {
                CurrentWaypointIndex = path.Count - 1;
                ReachedDestination = true;
                Steering = 0f;
            }
        }
        else
        {
            CurrentWaypointIndex = next;
        }
    }

    /// <summary>重置到起点（车冲出道路后调用）</summary>
    public void ResetToStart()
    {
        CurrentWaypointIndex = 0;
        LapCount = 0;
        ReachedDestination = false;
        Steering = 0f;
    }

    void OnDrawGizmos()
    {
        if (path == null || path.Count == 0) return;
        // 当前目标点画成黄色大球，方便看车在追哪个点
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(path.GetPoint(CurrentWaypointIndex) + targetOffset, 0.6f);
        Gizmos.DrawLine(transform.position, path.GetPoint(CurrentWaypointIndex) + targetOffset);
    }
}
