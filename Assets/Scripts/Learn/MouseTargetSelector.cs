using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 鼠标点击指定目标路口 —— 让"路线随时变化"变成一次点击。
///
/// 【怎么用】
///   运行中：鼠标左键点地面上任意位置 → 找离点击处**最近的路口**当新目标
///           → 立刻让 RoadNetwork 用 A* 重算一条从车当前位置到该路口的最短路
///           → 车马上改道走新路线
///   右键   → 取消目标，回到"沿默认环线跑"的模式
///
///   数字键 1~5 → 直接跳到几个常用目标路口（演示时按得快）
///
/// 【为什么是"点最近的路口"而不是"点哪去哪"】
///   车必须走在路上。你点的地方如果在草坪中央，车没有路过去。
///   所以把点击位置吸附到最近的路口 —— 你点哪片区域，就去那个路口。
///   看画面上的蓝圈就知道吸附到哪了。
///
/// 【它怎么通知车改道】
///   它只做一件事：调用 network 找到新路线，然后 set 到 AutoDrive 上。
///   不直接控制车 —— 控制权始终在 AutoDrive，这里只是"换一张地图/换一个目的地"。
/// </summary>
public class MouseTargetSelector : MonoBehaviour
{
    [Header("引用（留空自动找）")]
    [Tooltip("路网。留空就在场景里找")]
    public RoadNetwork network;

    [Tooltip("车。留空就在场景里找带 AutoDrive 的物体")]
    public AutoDrive drive;

    [Header("交互")]
    [Tooltip("点地面时，离得最近的路口超过这个距离就不认（防止点到很远的地方）")]
    public float maxSnapDistance = 14f;

    [Tooltip("勾上 = 进 Play 时自动沿默认环线跑（不设目标）")]
    public bool autoLoopAtStart = true;

    [Header("快捷键（直接选目标路口下标，-1 = 空）")]
    [Tooltip("按 1~6 分别去这几个路口。填路口下标，-1 表示这个键不用")]
    public int[] hotkeyTargets = new int[] { 9, 10, 11, 12, 5, 8 };

    [Header("调试")]
    public bool logStats = true;
    public bool drawRoute = true;

    // ==================== 对外可见 ====================
    /// <summary>当前目标路口下标。-1 = 没有目标（沿环线跑）</summary>
    public int TargetJunction { get; private set; } = -1;

    /// <summary>上车一次都还没选过目标</summary>
    public bool HasTarget => TargetJunction >= 0;

    // 上次点击的位置，画个标记
    Vector3 _clickPoint;
    bool _hasClick;

    Camera _cam;

    void Start()
    {
        if (network == null) network = FindObjectOfType<RoadNetwork>();
        if (drive   == null) drive   = FindObjectOfType<AutoDrive>();
        _cam = Camera.main;

        if (network == null)
        {
            Debug.LogWarning("[MouseTargetSelector] 场景里没有 RoadNetwork —— " +
                             "跑一下菜单「工具（Tools）→ 路网系统 → ① 挂载到场景」", this);
            enabled = false;
            return;
        }

        if (logStats)
        {
            Debug.Log($"[MouseTargetSelector] 就绪。路网 {network.JunctionCount} 个路口。\n" +
                      "  鼠标左键 = 点哪去哪（吸附到最近路口）\n" +
                      "  鼠标右键 = 取消目标，回到默认环线\n" +
                      "  数字键 1~6 = 直接去预设路口");
        }
    }

    void Update()
    {
        if (network == null) return;

        // ---- 左键：选目标 ----
        if (Input.GetMouseButtonDown(0) && _cam != null)
        {
            if (RayToGround(Input.mousePosition, out Vector3 hit))
            {
                _clickPoint = hit;
                _hasClick = true;
                PickNearestJunction(hit);
            }
        }

        // ---- 右键：取消 ----
        if (Input.GetMouseButtonDown(1))
        {
            if (HasTarget) ClearTarget();
        }

        // ---- 数字键：快速选目标 ----
        for (int k = 0; k < hotkeyTargets.Length && k < 9; k++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + k))
                SetTarget(hotkeyTargets[k]);
        }
    }

    /// <summary>把点击的位置吸附到最近的路口，设为新目标</summary>
    void PickNearestJunction(Vector3 worldPos)
    {
        int idx = network.NearestJunction(worldPos);
        if (idx < 0) return;

        float snap = Vector3.Distance(network.GetPosition(idx), worldPos);

        if (snap > maxSnapDistance)
        {
            if (logStats)
                Debug.Log($"[MouseTargetSelector] 点击处离最近路口「{network.GetName(idx)}」" +
                          $"还有 {snap:F1} 米（上限 {maxSnapDistance} 米）→ 忽略这次点击");
            return;
        }

        SetTarget(idx);
    }

    /// <summary>设置目标路口 → 让车用 A* 重新规划</summary>
    public void SetTarget(int junctionIndex)
    {
        if (junctionIndex < 0 || junctionIndex >= network.JunctionCount)
        {
            if (junctionIndex >= 0 && logStats)
                Debug.LogWarning($"[MouseTargetSelector] 路口下标 {junctionIndex} 不存在（共 {network.JunctionCount} 个）");
            return;
        }

        if (!network.IsJunctionOpen(junctionIndex))
        {
            if (logStats)
                Debug.LogWarning($"[MouseTargetSelector] 路口「{network.GetName(junctionIndex)}」被封了，去不了");
            return;
        }

        TargetJunction = junctionIndex;

        if (drive != null)
        {
            drive.SetGlobalDestination(junctionIndex);
        }
        else if (logStats)
        {
            Debug.LogWarning("[MouseTargetSelector] 场景里没有 AutoDrive —— 选不了目标");
        }
    }

    /// <summary>取消目标 → 回到默认环线</summary>
    public void ClearTarget()
    {
        TargetJunction = -1;
        _hasClick = false;

        if (drive != null) drive.ClearGlobalDestination();

        if (logStats) Debug.Log("[MouseTargetSelector] 已取消目标 → 回到默认环线");
    }

    /// <summary>鼠标射线打到地面 y=0 上的哪个点</summary>
    bool RayToGround(Vector3 screenPos, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        Ray ray = _cam.ScreenPointToRay(screenPos);

        // 用数学平面 y=0 求交，而不是 Physics.Raycast ——
        // 否则会先打到障碍物/车顶上，点起来很难对准
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float dist))
        {
            hitPoint = ray.GetPoint(dist);
            return true;
        }
        return false;
    }

    // ==================== 调试绘制 ====================

    void OnDrawGizmosSelected()
    {
        if (!drawRoute || network == null) return;

        // 吸附半径
        if (_hasClick)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(_clickPoint, 0.6f);
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.25f);
            Gizmos.DrawLine(_clickPoint, _clickPoint + Vector3.up * 3f);
        }

        // 当前目标路口
        if (HasTarget)
        {
            Vector3 p = network.GetPosition(TargetJunction) + Vector3.up * 0.5f;
            Gizmos.color = new Color(0.1f, 0.9f, 0.4f, 1f);
            Gizmos.DrawWireSphere(p, 2.2f);
            Gizmos.DrawLine(p, p + Vector3.up * 4f);
        }
    }
}
