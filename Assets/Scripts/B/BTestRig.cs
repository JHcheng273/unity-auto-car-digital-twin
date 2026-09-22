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

    [Header("调试")]
    public KeyCode resetKey = KeyCode.R;

    void Update()
    {
        if (Input.GetKeyDown(resetKey) && follower != null) follower.ResetToStart();
    }

    void LateUpdate()
    {
        if (follower == null) return;

        float speed = moveSpeed;

        if (respectObstacle && detector != null)
        {
            if (detector.FrontCarDistance < brakeDistance)
                speed = 0f;                                   // 刹车
            else if (detector.FrontCarDistance < slowDistance)
                speed = moveSpeed * 0.4f;                     // 减速
        }

        if (follower.ReachedDestination) speed = 0f;

        transform.Translate(Vector3.forward * speed * Time.deltaTime);
        transform.Rotate(0f, follower.Steering * turnSpeed * Time.deltaTime, 0f);
    }
}
