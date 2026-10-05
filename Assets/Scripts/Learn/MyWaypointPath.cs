using System.Collections.Generic;
using UnityEngine;

// 这是我（B）跟着教程自己写的学习版路径脚本。
// 注意：工程里 Assets/Scripts/B/WaypointPath.cs 是项目正式版，类名不能撞车，
// 所以这里叫 MyWaypointPath。
public class MyWaypointPath : MonoBehaviour
{
    [Header("路径点（按顺序）")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("设置")]
    public bool loop = true;
    public float arrivalRadius = 3f;

    public int Count => waypoints.Count;

    public Vector3 GetPoint(int index)
    {
        if (waypoints == null || waypoints.Count == 0) return transform.position;
        int i = Mathf.Clamp(index, 0, waypoints.Count - 1);   // 越界自动夹紧，不会崩
        if (waypoints[i] == null) return transform.position;
        return waypoints[i].position;
    }

    public int NextIndex(int index)
    {
        if (waypoints == null || waypoints.Count == 0) return 0;
        if (index + 1 >= waypoints.Count) return loop ? 0 : index;
        return index + 1;
    }
}

