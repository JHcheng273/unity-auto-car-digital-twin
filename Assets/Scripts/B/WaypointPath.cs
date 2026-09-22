using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 路径点容器（成员 B 负责）
/// 用法：建一个空物体叫 "Path"，把 WP0~WP7 拖成它的子物体，挂这个脚本，点右键菜单"收集子物体为路径点"。
/// </summary>
public class WaypointPath : MonoBehaviour
{
    [Header("路径点（按顺序）")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("设置")]
    [Tooltip("到终点后是否回到 WP0 循环跑")]
    public bool loop = true;
    [Tooltip("距离目标点小于这个值就认为到达")]
    public float arrivalRadius = 3f;

    [Header("调试显示")]
    public bool showGizmos = true;
    public Color gizmoColor = Color.cyan;

    public int Count => waypoints.Count;

    /// <summary>取第 i 个路径点的位置（越界自动夹紧，防止报错）</summary>
    public Vector3 GetPoint(int index)
    {
        if (waypoints == null || waypoints.Count == 0) return transform.position;
        int i = Mathf.Clamp(index, 0, waypoints.Count - 1);
        if (waypoints[i] == null) return transform.position;
        return waypoints[i].position;
    }

    /// <summary>下一个点的下标</summary>
    public int NextIndex(int index)
    {
        if (waypoints == null || waypoints.Count == 0) return 0;
        if (index + 1 >= waypoints.Count) return loop ? 0 : index; // 不循环就停最后一个
        return index + 1;
    }

    [ContextMenu("收集子物体为路径点")]
    public void CollectChildren()
    {
        waypoints = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child == transform) continue;
            waypoints.Add(child);
        }
        // 按名字排序，保证 WP0, WP1, WP2 ... 的顺序
        waypoints = waypoints
            .OrderBy(t => t.name, System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        Debug.Log($"[WaypointPath] 收集到 {waypoints.Count} 个路径点：" +
                  string.Join(", ", waypoints.Select(w => w.name)));
    }

    void OnValidate()
    {
        // 没手动拖过就自动收集一次，省得新手忘了拖
        if ((waypoints == null || waypoints.Count == 0) && transform.childCount > 0)
            CollectChildren();
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || waypoints == null || waypoints.Count < 2) return;

        Gizmos.color = gizmoColor;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            Vector3 a = waypoints[i].position;

            // 画一个球代表路径点
            Gizmos.DrawSphere(a, 0.4f);

            // 画连线
            if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
                Gizmos.DrawLine(a, waypoints[i + 1].position);
            else if (loop && waypoints[0] != null)
                Gizmos.DrawLine(a, waypoints[0].position);

            // 画到达半径
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.25f);
            Gizmos.DrawWireSphere(a, arrivalRadius);
            Gizmos.color = gizmoColor;
        }
    }
}
