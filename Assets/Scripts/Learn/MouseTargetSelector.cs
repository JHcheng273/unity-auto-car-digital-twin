using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 右键点击指定目标路口 —— 让"路线随时变化"变成一次点击。
///
/// 【怎么用】
///   运行中：鼠标**右键**点地面上任意位置 → 找离点击处**最近的路口**当新目标
///           → 立刻让 RoadNetwork 用 A* 重算一条从车当前位置到该路口的最短路
///           → 车马上改道走新路线
///   中键 / 数字键 0 → 取消目标，回到"沿默认环线跑"的模式
///   数字键 1~6 → 直接跳到几个常用目标路口（演示时按得快）
///
/// 【★★ 为什么从「左键」改成「右键」（2026-10-11 修）】
///   原来左键**同时**绑了两件事：
///     · ObstacleManager 用左键"在点击处加障碍物"
///     · 本脚本用左键"选目标路口"
///   结果用户左键一点 → **既加了障碍、又改了目标点**，
///   车一边躲刚冒出来的障碍、一边被要求去新目标，看起来就是"矛盾了"。
///   现在分工明确：
///     · **左键 = 加障碍物**（ObstacleManager，不变）
///     · **右键 = 设目标点**（本脚本）
///     · 删障碍物从"右键"挪到 **Delete 键 / Ctrl+右键**
///   这样每个键只干一件事，闭着眼也不会误操作。
///
/// 【为什么是"点最近的路口"而不是"点哪去哪"】
///   车必须走在路上。你点的地方如果在草坪中央，车没有路过去。
///   所以把点击位置吸附到最近的路口 —— 你点哪片区域，就去那个路口。
///   看画面上的绿圈就知道吸附到哪了。
///
/// 【它怎么通知车改道】
///   它只做一件事：调用 network 找到新路线，然后 set 到 AutoDrive 上。
///   不直接控制车 —— 控制权始终在 AutoDrive，这里只是"换一个目的地"。
/// </summary>
public class MouseTargetSelector : MonoBehaviour
{
    [Header("引用（留空自动找）")]
    [Tooltip("路网。留空就在场景里找")]
    public RoadNetwork network;

    [Tooltip("车。留空就在场景里找带 AutoDrive 的物体")]
    public AutoDrive drive;

    [Header("交互")]
    [Tooltip("★ 设目标用哪个鼠标键。默认右键（因为左键被「加障碍物」占了）")]
    public MouseButton targetButton = MouseButton.Right;

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

    /// <summary>用哪个鼠标键设目标</summary>
    public enum MouseButton { Right = 1, Middle = 2 }

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
            string btn = targetButton == MouseButton.Right ? "右键" : "中键";
            Debug.Log($"[MouseTargetSelector] 就绪。路网 {network.JunctionCount} 个路口。\n" +
                      $"  ★ 鼠标{btn} = 点哪去哪（吸附到最近路口）\n" +
                      "  ★ 鼠标左键 = 加障碍物（归 ObstacleManager 管）\n" +
                      "  ★ Delete 键 / Ctrl+右键 = 删掉离鼠标最近的障碍物\n" +
                      "  ★ 数字键 0 = 取消目标，回到默认环线\n" +
                      "  ★ 数字键 1~6 = 直接去预设路口");
        }
    }

    void Update()
    {
        if (network == null) return;

        // ---- 设目标键：选目标 ----
        // ★ 用 GetMouseButtonDown((int)targetButton)：1=右键 2=中键
        if (Input.GetMouseButtonDown((int)targetButton) && _cam != null)
        {
            // Ctrl + 右键 = 删障碍（让 MouseTargetSelector 把事件让给 ObstacleManager，
            // 自己不作响应）—— 否则 Ctrl+右键会"又删障碍又设目标"
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!ctrl && RayToGround(Input.mousePosition, out Vector3 hit))
            {
                _clickPoint = hit;
                _hasClick = true;
                PickNearestJunction(hit);
            }
        }

        // ---- 数字键 0：取消 ----
        if (Input.GetKeyDown(KeyCode.Alpha0))
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

    /// <summary>
    /// 能不能设这个目标（路口存在、且没被封）。
    /// 给菜单 / 其它脚本查询用，避免重复代码。
    /// </summary>
    public bool CanTarget(int junctionIndex)
    {
        if (network == null) return false;
        if (junctionIndex < 0 || junctionIndex >= network.JunctionCount) return false;
        return network.IsJunctionOpen(junctionIndex);
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

        // ★ 用青色画"目标"相关的东西 —— 和左键加的障碍物（橙色）区分开，
        //   一眼就知道哪个是"我要去哪"、哪个是"路上的坎"。
        if (_hasClick)
        {
            Gizmos.color = new Color(0.1f, 0.9f, 0.9f, 0.5f);
            Gizmos.DrawWireSphere(_clickPoint, 0.6f);
            Gizmos.color = new Color(0.1f, 0.9f, 0.9f, 0.25f);
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
