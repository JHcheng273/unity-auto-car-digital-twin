using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 路径可视化（成员 B 负责，第 4 周）
/// 已行驶 = 灰色，未行驶 = 青色，用 LineRenderer 画。
///
/// 挂法：随便挂一个空物体上就行，LineRenderer 它会自动创建。
/// 如果线是紫红色（材质丢失），手动给 LineRenderer 拖一个材质（Sprites/Default 或 Unlit/Color）。
/// </summary>
public class PathVisualizer : MonoBehaviour
{
    [Header("引用")]
    public WaypointPath path;
    public WaypointFollower follower;

    [Header("外观")]
    public Color traveledColor = new Color(0.6f, 0.6f, 0.6f, 1f);  // 灰
    public Color remainingColor = new Color(0f, 0.9f, 0.9f, 1f);    // 青
    [Tooltip("线抬高一点，避免和地面重叠闪烁")]
    public float heightOffset = 0.25f;
    public float lineWidth = 0.25f;

    LineRenderer _traveledLine;
    LineRenderer _remainingLine;

    void Awake()
    {
        _traveledLine = EnsureLine("TraveledPath", traveledColor);
        _remainingLine = EnsureLine("RemainingPath", remainingColor);
    }

    LineRenderer EnsureLine(string childName, Color color)
    {
        Transform t = transform.Find(childName);
        if (t != null)
        {
            var exist = t.GetComponent<LineRenderer>();
            if (exist != null) return exist;
        }

        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        if (shader != null) lr.material = new Material(shader);

        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.useWorldSpace = true;
        lr.loop = false;
        return lr;
    }

    void Update()
    {
        if (path == null || path.Count == 0) return;

        Vector3 carPos = follower != null ? follower.transform.position : transform.position;
        int cur = follower != null ? follower.CurrentWaypointIndex : 0;

        // ---- 已行驶：WP0 -> ... -> WP(cur-1) -> 车当前位置 ----
        var traveled = new List<Vector3>();
        for (int i = 0; i < cur; i++) traveled.Add(Lift(path.GetPoint(i)));
        traveled.Add(Lift(carPos));
        SetPoints(_traveledLine, traveled);

        // ---- 未行驶：车当前位置 -> WP(cur) -> ... -> 最后一个 ----
        var remaining = new List<Vector3> { Lift(carPos) };
        for (int i = cur; i < path.Count; i++) remaining.Add(Lift(path.GetPoint(i)));
        if (path.loop && path.Count > 0) remaining.Add(Lift(path.GetPoint(0))); // 闭环回起点
        SetPoints(_remainingLine, remaining);
    }

    Vector3 Lift(Vector3 p) => p + Vector3.up * heightOffset;

    void SetPoints(LineRenderer lr, List<Vector3> pts)
    {
        if (lr == null) return;
        if (pts.Count < 2)
        {
            lr.positionCount = 0;
            return;
        }
        lr.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++) lr.SetPosition(i, pts[i]);
    }
}
