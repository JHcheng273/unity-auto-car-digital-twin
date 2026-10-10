using UnityEngine;

public class MyFrontDetector : MonoBehaviour
{
    [Header("射线设置")]
    [Tooltip("最远检测距离（米）")]
    public float detectionRange = 20f;
    [Tooltip("起点抬高一点，避免射线打到自己和地面")]
    public float heightOffset = 0.5f;

    [Header("报警")]
    [Tooltip("小于这个距离就算「有障碍」")]
    public float triggerDistance = 5f;

    [Header("调试")]
    [Tooltip("勾选后每 2 秒在 Console 打一次检测结果。平时关掉，否则会刷屏淹没其它日志")]
    public bool logStats = false;

    public bool   HasObstacle      { get; private set; }
    public float  ObstacleDistance { get; private set; }
    public string ObstacleName    { get; private set; } = "";

    void Start()
    {
        ObstacleDistance = detectionRange;
    }

    void Update()
    {
        // 每帧先归零，再重新检测
        HasObstacle      = false;
        ObstacleDistance = detectionRange;
        ObstacleName     = "";

        Vector3 origin = transform.position + transform.up * heightOffset;
        Vector3 dir    = transform.forward;

        RaycastHit hit;
        if (Physics.Raycast(origin, dir, out hit, detectionRange))
        {
            // 打到自己或自己的子物体 → 忽略这次
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                Debug.DrawLine(origin, origin + dir * detectionRange, Color.green);
                return;
            }

            ObstacleDistance = hit.distance;
            ObstacleName     = hit.transform.name;
            HasObstacle      = hit.distance <= triggerDistance;

            Debug.DrawLine(origin, hit.point, HasObstacle ? Color.red : Color.green);
        }
        else
        {
            Debug.DrawLine(origin, origin + dir * detectionRange, Color.green);
        }
        // 默认不打日志 —— 以前每 30 帧打一次，几秒钟就把 Console 刷爆，
        // 把 MatlabBridge 的「已发 N 包」全淹没了，排查通信问题时根本找不到。
        // 需要时在 Inspector 上勾 logStats。
        if (logStats && Time.time - _lastLog > 2f)
        {
            _lastLog = Time.time;
            Debug.Log($"[MyFrontDetector] 障碍「{ObstacleName}」距离 {ObstacleDistance:F1} 米，" +
                      $"报警 = {HasObstacle}");
        }
    }

    float _lastLog;
}
