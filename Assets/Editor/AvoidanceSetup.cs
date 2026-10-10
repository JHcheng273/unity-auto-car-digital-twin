#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键给场景搭好绕障系统。
///
/// 【怎么用】
///   Unity 顶部菜单栏 → 工具（Tools）→ 绕障系统 → 挂载到场景
///
/// 它会做三件事：
///   1. 在场景里建一个空物体 ObstacleManager，挂上 ObstacleManager 脚本
///   2. 给车（Cube）挂上 AvoidanceDriver 脚本
///   3. 把各个引用（车、检测器、障碍管理器）自动连好
///
/// 为什么要有这个：手动拖引用容易漏，而且 AvoidanceDriver 要挂在车上、
/// ObstacleManager 要挂在另一个物体上，位置搞错了会有莫名其妙的问题。
///
/// 【执行顺序】LateUpdate 已经保证了本脚本晚于 AutoDrive 执行，
///             所以不需要手动配 Execution Order。但菜单里也提供了一个
///             "检查执行顺序"的选项来验证。
/// </summary>
public static class AvoidanceSetup
{
    [MenuItem("工具（Tools）/绕障系统/① 挂载到场景", priority = 0)]
    public static void Setup()
    {
        // ---------- 找车 ----------
        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        if (drive == null)
        {
            EditorUtility.DisplayDialog("找不到车",
                "场景里没有挂着 AutoDrive 的物体。\n\n" +
                "请先把 mycar.unity 打开，再运行这个命令。", "知道了");
            return;
        }

        GameObject carGo = drive.gameObject;
        int added = 0, existed = 0;

        // ---------- 1. 车的绕障组件 ----------
        AvoidanceDriver avoid = carGo.GetComponent<AvoidanceDriver>();
        if (avoid == null)
        {
            avoid = Undo.AddComponent<AvoidanceDriver>(carGo);
            added++;
            Debug.Log($"[绕障设置] 已给「{carGo.name}」添加 AvoidanceDriver");
        }
        else existed++;

        // ---------- 2. 障碍管理器（单独一个物体）----------
        ObstacleManager om = Object.FindObjectOfType<ObstacleManager>();
        GameObject omGo;
        if (om == null)
        {
            omGo = new GameObject("ObstacleManager");
            Undo.RegisterCreatedObjectUndo(omGo, "创建 ObstacleManager");
            om = Undo.AddComponent<ObstacleManager>(omGo);
            omGo.transform.position = Vector3.zero;
            added++;
            Debug.Log("[绕障设置] 已创建 ObstacleManager 物体");
        }
        else
        {
            omGo = om.gameObject;
            existed++;
        }

        // ---------- 3. 自动连引用 ----------
        avoid.drive      = drive;
        avoid.car        = carGo.GetComponent<CarController>();
        avoid.detector   = carGo.GetComponent<MyFrontDetector>();
        avoid.bridge     = carGo.GetComponent<MatlabBridge>();
        avoid.obstacles  = om;

        om.car = carGo.transform;

        // ---------- 4. 检查关键依赖 ----------
        var missing = new System.Collections.Generic.List<string>();
        if (avoid.car      == null) missing.Add("CarController");
        if (avoid.detector == null) missing.Add("MyFrontDetector");

        string msg = $"车「{carGo.name}」上加好了绕障系统。\n\n" +
                     $"· AvoidanceDriver → 挂在车上\n" +
                     $"· ObstacleManager → 挂在 {omGo.name}\n" +
                     $"· 引用已自动连好\n\n" +
                     "【运行时按键】\n" +
                     "  O 键        随机加一个障碍\n" +
                     "  Shift + O   加个大的\n" +
                     "  鼠标左键    点到哪加到哪\n" +
                     "  鼠标右键    删掉最近的那个\n" +
                     "  1 / 2 / 3   清空 / 重摆 3 个 / 重摆 5 个";

        if (missing.Count > 0)
            msg += "\n\n⚠ 车上缺少这些组件，绕障会用不了：\n  " + string.Join("\n  ", missing);

        EditorUtility.DisplayDialog("绕障系统", msg, "好");

        EditorUtility.SetDirty(carGo);
        EditorUtility.SetDirty(omGo);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(carGo.scene);

        Debug.Log($"[绕障设置] 完成：新增 {added} 个组件，原本已有 {existed} 个。" +
                  "记得 Ctrl+S 保存场景。");
    }

    [MenuItem("工具（Tools）/绕障系统/② 检查挂载状态", priority = 1)]
    public static void Check()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== 绕障系统自检 =====");
        sb.AppendLine();

        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        sb.AppendLine(drive == null
            ? "[X] 找不到 AutoDrive —— 场景打开了吗？"
            : $"[OK] 车：{drive.gameObject.name}");

        if (drive != null)
        {
            GameObject carGo = drive.gameObject;

            var car = carGo.GetComponent<CarController>();
            sb.AppendLine(car == null ? "[X] 缺 CarController" : "[OK] CarController");

            var det = carGo.GetComponent<MyFrontDetector>();
            sb.AppendLine(det == null ? "[X] 缺 MyFrontDetector（急停靠它）" : "[OK] MyFrontDetector");

            var av = carGo.GetComponent<AvoidanceDriver>();
            sb.AppendLine(av == null ? "[X] 缺 AvoidanceDriver（绕障靠它）" : "[OK] AvoidanceDriver");

            if (av != null)
            {
                sb.AppendLine($"      急停距离 {av.emergencyDistance} 米 / 提前规划 {av.prepareDistance} 米");
                sb.AppendLine($"      栅格 {av.cellSize} 米/格，膨胀 {av.obstaclePadding} 米");
            }

            var br = carGo.GetComponent<MatlabBridge>();
            sb.AppendLine(br == null ? "[!] 没挂 MatlabBridge（不影响绕障，只是 MATLAB 接不上）"
                                     : "[OK] MatlabBridge");
        }

        var om = Object.FindObjectOfType<ObstacleManager>();
        sb.AppendLine(om == null
            ? "[X] 缺 ObstacleManager —— 跑一下「① 挂载到场景」"
            : $"[OK] ObstacleManager（在「{om.gameObject.name}」上，初始 {om.initialCount} 个障碍）");

        // ---- 栅格覆盖检查 ----
        if (om != null && drive != null)
        {
            sb.AppendLine();
            sb.AppendLine("----- 栅格覆盖检查 -----");
            float ext = 60f;
            var av = drive.GetComponent<AvoidanceDriver>();
            if (av != null) ext = av.gridExtent;

            sb.AppendLine($"栅格覆盖 X/Z 各 {-ext} ~ {+ext} 米");
            var path = drive.path;
            if (path != null)
            {
                bool allIn = true;
                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 p = path.GetPoint(i);
                    bool inRange = Mathf.Abs(p.x) <= ext && Mathf.Abs(p.z) <= ext;
                    if (!inRange) allIn = false;
                    sb.AppendLine($"  WP{i} ({p.x:F1}, {p.z:F1}) {(inRange ? "OK" : "★ 超出栅格！")}");
                }
                if (!allIn)
                    sb.AppendLine("  ⚠ 有航点在栅格外，绕障到那儿会失败 —— 把 gridExtent 调大");
            }
        }

        Debug.Log(sb.ToString());
        EditorUtility.DisplayDialog("绕障系统自检", "详情看 Console（控制台）窗口。", "好");
    }

    [MenuItem("工具（Tools）/绕障系统/③ 清掉绕障系统", priority = 2)]
    public static void Remove()
    {
        if (!EditorUtility.DisplayDialog("确认",
            "会删掉车上的 AvoidanceDriver 和场景里的 ObstacleManager。\n" +
            "（不影响 AutoDrive / CarController 等原有脚本）", "删", "算了"))
            return;

        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        if (drive != null)
        {
            var av = drive.GetComponent<AvoidanceDriver>();
            if (av != null) Undo.DestroyObjectImmediate(av);
        }

        var om = Object.FindObjectOfType<ObstacleManager>();
        if (om != null) Undo.DestroyObjectImmediate(om.gameObject);

        Debug.Log("[绕障设置] 已清掉绕障系统");
    }
}
#endif
