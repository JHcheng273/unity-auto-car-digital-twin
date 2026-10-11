#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键搭好「支路路网 + A* 全局寻路」。
///
/// 【怎么用】
///   Unity 顶部菜单栏 → 工具（Tools）→ 路网系统 → ① 挂载到场景
///
/// 它会做四件事：
///   1. 建一个空物体 RoadNetwork，按 mycar.unity 的真实坐标填好 13 个路口 + 20 条支路
///   2. 把路面铺出来（每个路口一个圆盘 + 每条支路一条带子），你能看见路网
///   3. 给车挂上 MouseTargetSelector（鼠标点哪去哪）
///   4. 把 AutoDrive 的 network 引用连好
///
/// 【为什么路口坐标是写死的】
///   内环那 4 个点就是场景里 WP0~WP3 的坐标，外环是我按场地尺寸加的。
///   写死比"运行时猜"可靠 —— 你打开 mycar.unity 就能看到这些路口正好落在路上。
///
/// 【重复点菜单会怎样】
///   不会重复建。已经有 RoadNetwork 就复用，已经有路面就重铺一遍（先删旧的）。
/// </summary>
public static class RoadNetworkSetup
{
    const string ROAD_ROOT_NAME = "RoadNetwork";

    [MenuItem("工具（Tools）/路网系统/① 挂载到场景", priority = 0)]
    public static void Setup()
    {
        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        if (drive == null)
        {
            EditorUtility.DisplayDialog("找不到车",
                "场景里没有挂着 AutoDrive 的物体。\n\n" +
                "请先把 mycar.unity 打开，再运行这个命令。", "知道了");
            return;
        }

        // ---------- 1. RoadNetwork 物体 ----------
        RoadNetwork net = Object.FindObjectOfType<RoadNetwork>();
        GameObject netGo;

        if (net == null)
        {
            netGo = new GameObject(ROAD_ROOT_NAME);
            Undo.RegisterCreatedObjectUndo(netGo, "创建 RoadNetwork");
            netGo.transform.position = Vector3.zero;
            net = Undo.AddComponent<RoadNetwork>(netGo);
            Debug.Log("[路网设置] 已创建 RoadNetwork 物体");
        }
        else
        {
            netGo = net.gameObject;
            Debug.Log($"[路网设置] 复用了已有的 RoadNetwork（在「{netGo.name}」上）");
        }

        // ---------- 2. 填路网数据 ----------
        Undo.RecordObject(net, "填路网数据");
        net.FillDefaultNetwork();
        EditorUtility.SetDirty(net);

        // ---------- 3. 铺路面（看得见）----------
        int discCount, ribbonCount;
        BuildRoadVisuals(netGo.transform, net, out discCount, out ribbonCount);

        // ---------- 4. 给车挂 MouseTargetSelector ----------
        MouseTargetSelector sel = drive.GetComponent<MouseTargetSelector>();
        if (sel == null)
        {
            sel = Undo.AddComponent<MouseTargetSelector>(drive.gameObject);
            Debug.Log($"[路网设置] 已给「{drive.gameObject.name}」添加 MouseTargetSelector");
        }

        Undo.RecordObject(sel, "连引用");
        sel.network = net;
        sel.drive   = drive;
        EditorUtility.SetDirty(sel);

        // ---------- 5. AutoDrive 连上路网 ----------
        Undo.RecordObject(drive, "连引用");
        drive.network = net;
        EditorUtility.SetDirty(drive);

        // 车现在离哪个路口最近？告诉用户，方便核对
        int near = net.NearestJunction(drive.transform.position);
        float nearDist = near >= 0
            ? Vector3.Distance(net.GetPosition(near), drive.transform.position)
            : -1f;

        string msg =
            $"路网搭好了。\n\n" +
            $"· 路口 {net.junctions.Count} 个，支路 {net.branches.Count} 条\n" +
            $"· 铺了 {discCount} 个路口圆盘 + {ribbonCount} 条支路带子\n" +
            $"· RoadNetwork → 挂在「{netGo.name}」\n" +
            $"· MouseTargetSelector → 挂在「{drive.gameObject.name}」\n" +
            $"· 车最近的路口：" +
            (near >= 0 ? $"{net.GetName(near)}（{nearDist:F1} 米）" : "无") +
            "\n\n【运行时操作】\n" +
            "  鼠标左键   点地面 → 去离点击处最近的那个路口\n" +
            "  鼠标右键   取消目标，回到默认环线\n" +
            "  数字键 1~6 直接去预设的 6 个远端路口\n\n" +
            "【想改路网】\n" +
            "  选中 RoadNetwork 物体 → 检查器里直接改路口坐标 / 加支路；\n" +
            "  勾某个路口的 Blocked = 封路，A* 会自动改走别的支路。";

        EditorUtility.DisplayDialog("路网系统", msg, "好");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(netGo.scene);
        Debug.Log("[路网设置] 完成。记得 Ctrl+S 保存场景。\n" + msg);
    }

    /// <summary>
    /// 把路网画出来：每个路口一个扁圆盘，每条支路一条扁带子。
    /// 纯视觉，不带碰撞体（车是运动学模型，碰撞体只会挡射线）。
    /// </summary>
    static void BuildRoadVisuals(Transform root, RoadNetwork net,
                                 out int discCount, out int ribbonCount)
    {
        // 先清掉上一次铺的
        var old = new List<GameObject>();
        for (int i = 0; i < root.childCount; i++)
        {
            GameObject c = root.GetChild(i).gameObject;
            if (c.name.StartsWith("NetRoad_") || c.name.StartsWith("NetJunc_"))
                old.Add(c);
        }
        for (int i = 0; i < old.Count; i++)
            Undo.DestroyObjectImmediate(old[i]);

        Material asphalt = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/RoadAsphaltMat.mat");
        if (asphalt == null)
        {
            // 没现成的就现造一个（深灰、带一点粗糙度）
            asphalt = new Material(Shader.Find("Standard"));
            asphalt.name = "NetRoadMat";
            asphalt.color = new Color(0.26f, 0.26f, 0.28f);
        }

        Material junctionMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/RoadAsphaltMat.mat");
        Color junctionTint = new Color(0.42f, 0.44f, 0.50f);

        float y = 0.028f;        // 略高于地面，别和 Plane 打架

        discCount = 0;
        ribbonCount = 0;

        // ---- 支路带子 ----
        for (int i = 0; i < net.branches.Count; i++)
        {
            RoadNetwork.Branch b = net.branches[i];
            if (b.from < 0 || b.from >= net.junctions.Count) continue;
            if (b.to   < 0 || b.to   >= net.junctions.Count) continue;

            Vector3 a = net.GetPosition(b.from);
            Vector3 c = net.GetPosition(b.to);
            Vector3 d = new Vector3(c.x - a.x, 0f, c.z - a.z);
            float len = d.magnitude;
            if (len < 0.05f) continue;

            GameObject ribbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ribbon.name = $"NetRoad_{b.from}_{b.to}";
            ribbon.transform.SetParent(root, false);
            ribbon.transform.position = new Vector3((a.x + c.x) * 0.5f, y, (a.z + c.z) * 0.5f);
            ribbon.transform.rotation = Quaternion.LookRotation(d);

            // 宽 3.4 米（比车宽一点），厚 0.04，长度多给一点保证接缝不留空
            ribbon.transform.localScale = new Vector3(3.4f, 0.04f, len + 3.4f);

            StripCollider(ribbon);
            SetMat(ribbon, asphalt);
            ribbonCount++;
        }

        // ---- 路口圆盘 ----
        for (int i = 0; i < net.junctions.Count; i++)
        {
            Vector3 p = net.GetPosition(i);

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = $"NetJunc_{i}_{net.GetName(i)}";
            disc.transform.SetParent(root, false);
            disc.transform.position = new Vector3(p.x, y, p.z);
            // Cylinder 原始高 2，y 缩放取 0.02 → 实际厚 0.04
            disc.transform.localScale = new Vector3(5.0f, 0.02f, 5.0f);

            StripCollider(disc);

            // 路口用一个略亮的材质实例，好和支路区分
            Material jm = new Material(asphalt);
            jm.name = $"NetJuncMat_{i}";
            jm.color = junctionTint;
            SetMat(disc, jm);

            // 在路口正上方放一个小方块做"路牌"，方便一眼看到哪是哪
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"NetJunc_{i}_Marker";
            marker.transform.SetParent(root, false);
            marker.transform.position = new Vector3(p.x, 0.6f, p.z);
            marker.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            StripCollider(marker);

            Material mm = new Material(asphalt);
            mm.name = $"NetJuncMarkerMat_{i}";
            // 被封的路口标红，其余标蓝
            mm.color = net.junctions[i].blocked
                ? new Color(0.85f, 0.22f, 0.18f)
                : new Color(0.22f, 0.55f, 0.95f);
            SetMat(marker, mm);

            discCount++;
        }
    }

    static void SetMat(GameObject go, Material m)
    {
        if (m == null) return;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = m;
    }

    static void StripCollider(GameObject go)
    {
        Collider c = go.GetComponent<Collider>();
        if (c != null) Undo.DestroyObjectImmediate(c);
    }

    // ============================================================

    [MenuItem("工具（Tools）/路网系统/② 检查路网状态", priority = 1)]
    public static void Check()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== 路网系统自检 =====");
        sb.AppendLine();

        RoadNetwork net = Object.FindObjectOfType<RoadNetwork>();
        if (net == null)
        {
            sb.AppendLine("[X] 找不到 RoadNetwork —— 跑一下「① 挂载到场景」");
            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog("路网自检", "场景里没有 RoadNetwork。详情看 Console。", "好");
            return;
        }

        sb.AppendLine($"[OK] RoadNetwork 在「{net.gameObject.name}」上");
        sb.AppendLine($"      路口 {net.junctions.Count} 个，支路 {net.branches.Count} 条");

        if (net.junctions.Count < 2)
        {
            sb.AppendLine("      ✘ 路口太少 —— 重新跑「① 挂载到场景」填数据");
        }

        // ---- 孤立路口 ----
        var deg = new int[net.junctions.Count];
        for (int i = 0; i < net.branches.Count; i++)
        {
            RoadNetwork.Branch b = net.branches[i];
            if (b.from >= 0 && b.from < deg.Length) deg[b.from]++;
            if (b.to   >= 0 && b.to   < deg.Length) deg[b.to]++;
        }
        for (int i = 0; i < deg.Length; i++)
        {
            if (deg[i] == 0)
                sb.AppendLine($"      ✘ 路口「{net.GetName(i)}」(# {i}) 没连任何支路");
        }

        // ---- 车的挂载 ----
        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        if (drive == null)
        {
            sb.AppendLine();
            sb.AppendLine("[X] 找不到 AutoDrive");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"[OK] 车：「{drive.gameObject.name}」");
            sb.AppendLine($"      AutoDrive.network = " +
                          (drive.network == null ? "✘ 空（A* 用不了）" : $"✔ {drive.network.junctions.Count} 个路口"));
            sb.AppendLine($"      junctionReach     = {drive.junctionReach} 米");
            sb.AppendLine($"      stopAtDestination = {drive.stopAtDestination}");

            MouseTargetSelector sel = drive.GetComponent<MouseTargetSelector>();
            sb.AppendLine(sel == null
                ? "      ✘ 缺 MouseTargetSelector（点不了目标）"
                : $"      ✔ MouseTargetSelector（mouseSnap {sel.maxSnapDistance} 米）");

            // 车最近的路口
            int near = net.NearestJunction(drive.transform.position);
            if (near >= 0)
            {
                float d = Vector3.Distance(net.GetPosition(near), drive.transform.position);
                sb.AppendLine($"      车最近的路口：{net.GetName(near)}（{d:F1} 米）");
                if (d > 12f)
                    sb.AppendLine("        ⚠ 车离所有路口都挺远 —— 起点可能不在路上");
            }
        }

        // ---- 铺了路面没有 ----
        sb.AppendLine();
        RoadNetwork netAny = Object.FindObjectOfType<RoadNetwork>();
        if (netAny != null)
        {
            int vis = 0;
            Transform root = netAny.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                string n = root.GetChild(i).name;
                if (n.StartsWith("NetRoad_") || n.StartsWith("NetJunc_")) vis++;
            }
            sb.AppendLine(vis == 0
                ? "      ⚠ 没铺路面（看不到路网）—— 重跑「① 挂载到场景」"
                : $"      ✔ 铺了 {vis} 个路面零件");
        }

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("路网自检", "详情看 Console（控制台）窗口。", "好");
    }

    [MenuItem("工具（Tools）/路网系统/③ 清掉路网系统", priority = 2)]
    public static void Remove()
    {
        if (!EditorUtility.DisplayDialog("确认",
            "会删掉场景里的 RoadNetwork 物体，以及车上的 MouseTargetSelector。\n" +
            "（不影响 AutoDrive / 绕障系统 / 摄像头）", "删", "算了"))
            return;

        RoadNetwork net = Object.FindObjectOfType<RoadNetwork>();
        if (net != null) Undo.DestroyObjectImmediate(net.gameObject);

        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        if (drive != null)
        {
            MouseTargetSelector sel = drive.GetComponent<MouseTargetSelector>();
            if (sel != null) Undo.DestroyObjectImmediate(sel);

            Undo.RecordObject(drive, "断开路网");
            drive.network = null;
            EditorUtility.SetDirty(drive);
        }

        Debug.Log("[路网设置] 已清掉路网系统");
    }

    // ============================================================

    [MenuItem("工具（Tools）/路网系统/④ 只重铺路面", priority = 3)]
    public static void RebuildVisualsOnly()
    {
        RoadNetwork net = Object.FindObjectOfType<RoadNetwork>();
        if (net == null)
        {
            EditorUtility.DisplayDialog("找不到路网", "先跑「① 挂载到场景」。", "知道了");
            return;
        }

        int d, r;
        BuildRoadVisuals(net.transform, net, out d, out r);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(net.gameObject.scene);
        Debug.Log($"[路网设置] 重铺完成：{d} 个路口圆盘 + {r} 条支路带子");
        EditorUtility.DisplayDialog("重铺路面", $"铺了 {d} 个路口 + {r} 条支路。", "好");
    }
}
#endif
