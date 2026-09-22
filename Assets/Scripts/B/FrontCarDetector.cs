using UnityEngine;

/// <summary>
/// 单射线前车 / 障碍物检测（成员 B 负责）
/// 输出：HasFrontCar、FrontCarDistance、FrontCarName
///
/// 挂法：拖到车身上，或拖到一个放在车头保险杠位置的空物体上。
/// 注意：用 Layer 过滤，别让射线打到自己（见 detectLayers 说明）。
/// </summary>
public class FrontCarDetector : MonoBehaviour
{
    [Header("射线设置")]
    [Tooltip("射线起点。留空 = 用自身位置")]
    public Transform rayOrigin;
    [Tooltip("射线方向参考。留空 = 用自身前方（蓝轴 Z+）")]
    public Transform directionSource;
    [Tooltip("起点抬高一点，避免射线打到地面")]
    public float heightOffset = 0.5f;
    [Tooltip("最远检测距离（米）")]
    public float detectionRange = 20f;
    [Tooltip("每秒检测几次。30 够用，想省性能调到 10")]
    public int samplesPerSecond = 30;

    [Header("过滤")]
    [Tooltip("只检测这些层。强烈建议：只勾 Obstacle 和 Vehicle，不要勾 Default，否则会打到地面和自己")]
    public LayerMask detectLayers = ~0;
    [Tooltip("小于这个距离才算“有前车”（报警用）")]
    public float triggerDistance = 15f;

    [Header("可视化")]
    public bool showDebugRay = true;
    [Tooltip("可选：拖一个 LineRenderer 进来，这样 Game 视图也能看到射线（Debug.DrawLine 只在 Scene 视图显示）")]
    public LineRenderer beamRenderer;

    // ============ 对外接口（C 会读这些）============
    public bool HasFrontCar { get; private set; }
    public float FrontCarDistance { get; private set; }
    public string FrontCarName { get; private set; } = "";
    public Vector3 LastHitPoint { get; private set; }

    float _timer;

    void Start()
    {
        // 没检测到东西时给一个"路面畅通"的大值，避免 UI 显示 Infinity
        FrontCarDistance = detectionRange;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float interval = samplesPerSecond > 0 ? 1f / samplesPerSecond : 0f;
        if (_timer < interval) return;
        _timer = 0f;

        Detect();
    }

    void Detect()
    {
        Vector3 origin = (rayOrigin != null ? rayOrigin.position : transform.position)
                         + transform.up * heightOffset;
        Vector3 dir = (directionSource != null ? directionSource.forward : transform.forward).normalized;

        // 每帧先归位
        HasFrontCar = false;
        FrontCarDistance = detectionRange;   // 没打到 = 前方畅通
        FrontCarName = "";
        RaycastHit hit;

        bool hitSomething = Physics.Raycast(
            origin, dir, out hit, detectionRange,
            detectLayers, QueryTriggerInteraction.Ignore);

        if (hitSomething)
        {
            // 打到自己或自己的子物体，直接忽略
            if (hit.transform.IsChildOf(transform) || hit.transform == transform)
            {
                DrawRay(origin, dir, detectionRange, false);
                return;
            }

            FrontCarDistance = hit.distance;
            FrontCarName = hit.transform.name;
            LastHitPoint = hit.point;
            HasFrontCar = hit.distance <= triggerDistance;

            DrawRay(origin, dir, hit.distance, HasFrontCar);
        }
        else
        {
            DrawRay(origin, dir, detectionRange, false);
        }
    }

    /// <summary>画射线：无障碍=绿，有障碍=红</summary>
    void DrawRay(Vector3 origin, Vector3 dir, float length, bool danger)
    {
        Color c = danger ? Color.red : Color.green;

        if (showDebugRay)
            Debug.DrawLine(origin, origin + dir * length, c);

        if (beamRenderer != null)
        {
            beamRenderer.useWorldSpace = true;
            beamRenderer.positionCount = 2;
            beamRenderer.SetPosition(0, origin);
            beamRenderer.SetPosition(1, origin + dir * length);
            beamRenderer.startColor = c;
            beamRenderer.endColor = c;
        }
    }

    // 便捷方法：给 C 用，判断要不要刹车
    public bool IsTooClose(float safeDistance) => HasFrontCar && FrontCarDistance < safeDistance;
}
