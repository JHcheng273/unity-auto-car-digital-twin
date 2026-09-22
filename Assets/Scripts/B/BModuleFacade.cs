using UnityEngine;

/// <summary>
/// B 模块对外统一接口（成员 B 负责，给 C 调用）
///
/// 存在的意义：C 只认这一个脚本，永远不用碰 B 的内部实现。
/// B 以后改算法，只要不改这里的属性名，C 的代码一行都不用动。
/// </summary>
public class BModuleFacade : MonoBehaviour
{
    [Header("把场景里的两个脚本拖进来")]
    public FrontCarDetector detector;
    public WaypointFollower follower;

    // ===== 感知（来自 FrontCarDetector）=====
    public bool HasFrontCar => detector != null && detector.HasFrontCar;
    /// <summary>没前车时返回最大检测距离（视作路面畅通），不会是 Infinity</summary>
    public float FrontCarDistance => detector != null ? detector.FrontCarDistance : float.MaxValue;
    public string FrontCarName => detector != null ? detector.FrontCarName : "";

    // ===== 规划（来自 WaypointFollower）=====
    /// <summary>转向指令，-1 左满舵，+1 右满舵</summary>
    public float Steering => follower != null ? follower.Steering : 0f;
    public int CurrentWaypointIndex => follower != null ? follower.CurrentWaypointIndex : -1;
    public float DistanceToTarget => follower != null ? follower.DistanceToTarget : 0f;
    public bool ReachedDestination => follower != null && follower.ReachedDestination;
    public int LapCount => follower != null ? follower.LapCount : 0;

    [Header("安全距离（C 可以直接读这个决定刹车）")]
    public float safeDistance = 5f;
    public float slowDistance = 10f;

    /// <summary>true = 必须刹车</summary>
    public bool NeedBrake => HasFrontCar && FrontCarDistance < safeDistance;
    /// <summary>true = 该减速了</summary>
    public bool NeedSlowDown => HasFrontCar && FrontCarDistance < slowDistance;

    void OnValidate()
    {
        // 自动找，省得手拖
        if (detector == null) detector = GetComponentInChildren<FrontCarDetector>();
        if (follower == null) follower = GetComponentInChildren<WaypointFollower>();
    }
}
