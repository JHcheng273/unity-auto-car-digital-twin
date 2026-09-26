using UnityEngine;

/// <summary>
/// B 的自测台（不是最终交付物，是给 B 自己抢跑用的）
///
/// 作用：A 的车还没做好、C 的 UI 还没接上时，B 用一个 Cube 也能验证
///      "射线检测 + 路径跟踪" 是不是对的。
///
/// 用法：
///   1. 新建 Cube，命名 B_TestCar
///   2. 挂 WaypointFollower、FrontCarDetector、BTestRig
///   3. Path 拖进去，按 Play —— 方块自己会沿路径跑，遇到 Cube 障碍会停
///
/// 为什么放 LateUpdate：保证 follower 这一帧已经算完 Steering，不会读到上一帧的旧值。
///
/// 绕障说明（2026-09-26 加）：
///   原来只有"刹车"一条规则，遇到障碍压在路径点上时会死锁 ——
///   车在离障碍 4 米处刹停，但要走目标点 3 米内才算"到达"，永远到不了，就僵住了。
///   现在补一个兜底：被挡超过 stuckTimeout 秒就判定卡死，把目标点往侧面挪一段，
///   绕过去之后再回到原路径。真实项目里这个决策归规划模块，这里只是测试台模拟。
/// </summary>
public class BTestRig : MonoBehaviour
{
    [Header("引用")]
    public WaypointFollower follower;
    public FrontCarDetector detector;

    [Header("假车参数")]
    public float moveSpeed = 6f;
    public float turnSpeed = 90f;     // 度/秒
    [Tooltip("勾选后遇到障碍会减速停车")]
    public bool respectObstacle = true;
    public float brakeDistance = 4f;
    public float slowDistance = 8f;

    [Header("绕障（模拟上层的绕行决策）")]
    [Tooltip("被障碍挡住超过这么多秒，就判定卡死，启动绕行")]
    public float stuckTimeout = 2f;
    [Tooltip("一次绕行持续多少秒，之后回到原路径")]
    public float detourDuration = 2.5f;
    [Tooltip("绕行时把目标点向车身侧面偏移多少米")]
    public float detourOffset = 3.5f;

    [Header("调试")]
    public KeyCode resetKey = KeyCode.R;
    public bool showDebugLog = true;

    float _stuckTimer;      // 被挡住的累计时间
    float _detourTimer;     // 绕行剩余时间
    bool _detourActive;     // 是否正在绕行
    int _detourSide = 1;    // 下次往哪边绕：+1 右，-1 左

    void Update()
    {
        if (Input.GetKeyDown(resetKey))
        {
            if (follower != null) follower.ResetToStart();
            EndDetour();
            _stuckTimer = 0f;
            if (showDebugLog) Debug.Log("[BTestRig] 已重置到起点");
        }
    }

    void LateUpdate()
    {
        if (follower == null) return;

        float speed = moveSpeed;
        bool blocked = false;

        // ---------- 1. 绕行倒计时 ----------
        if (_detourActive)
        {
            _detourTimer -= Time.deltaTime;
            if (_detourTimer <= 0f) EndDetour();
        }

        // ---------- 2. 避障：只在没绕行时生效 ----------
        if (respectObstacle && detector != null && !_detourActive)
        {
            if (detector.FrontCarDistance < brakeDistance)
            {
                speed = 0f;
                blocked = true;                  // 刹住了 = 有可能卡死
            }
            else if (detector.FrontCarDistance < slowDistance)
            {
                speed = moveSpeed * 0.4f;        // 减速通过
            }
        }

        // ---------- 3. 卡死检测：停够久就绕 ----------
        if (blocked)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= stuckTimeout)
            {
                StartDetour();
                speed = moveSpeed * 0.5f;        // 绕行时给一半速度，别继续杵着
            }
        }
        else if (!_detourActive)
        {
            _stuckTimer = 0f;
        }

        if (follower.ReachedDestination) speed = 0f;

        transform.Translate(Vector3.forward * speed * Time.deltaTime);
        transform.Rotate(0f, follower.Steering * turnSpeed * Time.deltaTime, 0f);
    }

    /// <summary>启动绕行：把目标点往车身侧面挪，绕开障碍物</summary>
    void StartDetour()
    {
        _stuckTimer = 0f;
        _detourActive = true;
        _detourTimer = detourDuration;

        // 取"车 → 目标点"的水平方向，它的垂直方向就是车身的右侧
        Vector3 toTarget = follower.TargetPoint - transform.position;
        toTarget.y = 0f;
        Vector3 side = toTarget.sqrMagnitude > 0.01f
            ? Vector3.Cross(Vector3.up, toTarget.normalized)   // up × forward = right
            : transform.right;

        follower.targetOffset = side * detourOffset * _detourSide;

        if (showDebugLog)
            Debug.Log($"[BTestRig] 被挡住 {stuckTimeout} 秒 → 启动绕行，" +
                      $"目标点向{(_detourSide > 0 ? "右" : "左")}偏 {detourOffset} 米");

        _detourSide = -_detourSide;   // 这次往右绕不过去，下次试左边
    }

    /// <summary>结束绕行，回到原路径</summary>
    void EndDetour()
    {
        if (!_detourActive) return;
        _detourActive = false;
        _detourTimer = 0f;
        if (follower != null) follower.targetOffset = Vector3.zero;
        if (showDebugLog) Debug.Log("[BTestRig] 绕行结束，回到原路径");
    }

    void OnDrawGizmosSelected()
    {
        // 把刹车/减速的判定半径画出来，方便对着调参数
        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, brakeDistance);
        Gizmos.color = new Color(1f, 0.7f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, slowDistance);
    }
}
