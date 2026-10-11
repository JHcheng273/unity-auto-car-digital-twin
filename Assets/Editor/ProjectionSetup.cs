#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键给车装上「光锥投影」，并把推给 MATLAB 的画面切成光锥特写。
///
/// 【怎么用】
///   Unity 顶部菜单栏 → 工具（Tools）→ 光锥投影 → ① 挂载到场景
///
/// 【它做了四件事】
///   1. 给车（挂着 AutoDrive 的那个物体）加 ProjectionBeam 组件
///   2. 把 CameraStreamer 的「推送内容」切成 BeamObserver（光锥特写）
///   3. 显式写一遍观察相机的位置 / 朝向 / 视场角
///      （★ 脚本默认值只对新建组件生效，场景里已有的组件保留旧值，
///        所以一定要在这里再写一遍 —— 这个坑踩过好几次）
///   4. 检查 BeamProjection 着色器编译了没
///
/// 【和"车载摄像头"菜单的关系】
///   必须先跑「车载摄像头 → ① 挂载到场景」（那一步会建 CarCamera，
///   光锥要靠它的画面才有内容）。这个菜单检测到没有就提示你先去跑那步。
/// </summary>
public static class ProjectionSetup
{
    // ---- 默认参数（要和 ProjectionBeam.cs / CameraStreamer.cs 的默认值一致）----
    // ★ 这两个数字是「由落地点反推」的，不是拍脑袋：
    //   起点高 0.75 m、前伸 0.30 m，要落到离车头 7.5 m 处
    //   → 下压角 atan(0.70/7.20) ≈ 5.6°，斜边长 sqrt(7.20²+0.70²) ≈ 7.23 m
    //   tools/test_beam_geometry.py 验过：这样不会穿地，也不会被夹紧改写。
    const float DEF_START_AHEAD  = 0.30f;
    const float DEF_START_HEIGHT = 0.75f;
    const float DEF_BEAM_LENGTH  = 7.23f;
    const float DEF_NEAR_W       = 1.20f;
    const float DEF_FAR_W        = 4.80f;
    const float DEF_TILT_DOWN    = 5.6f;
    const float DEF_INTENSITY    = 1.35f;
    const float DEF_ALPHA        = 0.72f;
    const float DEF_EDGE_FADE    = 0.22f;

    const float DEF_OBS_X    = -2.60f;
    const float DEF_OBS_Y    =  3.20f;
    const float DEF_OBS_Z    = -4.40f;
    const float DEF_LOOK_X   =  0.00f;
    const float DEF_LOOK_Y   =  0.25f;
    const float DEF_LOOK_Z   =  3.60f;
    const float DEF_OBS_FOV  = 55f;

    [MenuItem("工具（Tools）/光锥投影/① 挂载到场景", priority = 0)]
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

        // ---------- 0. 前置检查：先得有车载摄像头 ----------
        CameraStreamer cs = carGo.GetComponent<CameraStreamer>();
        if (cs == null)
        {
            if (EditorUtility.DisplayDialog("还没有车载摄像头",
                "光锥的内容来自车载摄像头拍到的画面，所以要先有它。\n\n" +
                "要我现在帮你先跑一遍「车载摄像头 → ① 挂载到场景」吗？", "好，先装摄像头", "我自己来"))
            {
                CameraSetup.Setup();
                cs = carGo.GetComponent<CameraStreamer>();
            }
            if (cs == null)
            {
                Debug.LogWarning("[光锥设置] 车上没有 CameraStreamer，已中止。\n" +
                                 "  → 请先运行 工具（Tools）→ 车载摄像头 → ① 挂载到场景");
                return;
            }
        }

        // ---------- 1. 检查着色器 ----------
        Shader sh = Shader.Find("Custom/BeamProjection");
        if (sh == null)
        {
            Debug.LogError("[光锥设置] 找不到着色器 Custom/BeamProjection。\n" +
                           "  · 确认 Assets/Shaders/BeamProjection.shader 存在\n" +
                           "  · 看 Console 有没有它的编译红字\n" +
                           "  · 点一下 Unity 窗口让它重新扫描文件（Unity 只在窗口获得焦点时才扫）");
            EditorUtility.DisplayDialog("缺少着色器",
                "找不到 Custom/BeamProjection 着色器。\n\n" +
                "请先点一下 Unity 窗口让它编译，\n再看 Console 有没有红字报错。", "知道了");
            return;
        }
        Debug.Log("[光锥设置] ✔ 找到着色器 Custom/BeamProjection");

        // ---------- 2. ProjectionBeam 组件 ----------
        ProjectionBeam pb = carGo.GetComponent<ProjectionBeam>();
        if (pb == null)
        {
            pb = Undo.AddComponent<ProjectionBeam>(carGo);
            Debug.Log($"[光锥设置] 已给「{carGo.name}」添加 ProjectionBeam");
        }
        else
        {
            Debug.Log($"[光锥设置] 「{carGo.name}」上已有 ProjectionBeam，复用并重设参数。");
        }

        // ---------- 3. 显式写一遍参数 ----------
        // ★ 为什么必须写：脚本里的默认值只对**新加的**组件生效。
        //   如果车上早就有一个 ProjectionBeam（比如之前手动加过、或改过参数），
        //   它保留的是创建那一刻的序列化值，改脚本事后不会自动更新。
        pb.showBeam     = true;
        pb.startAhead   = DEF_START_AHEAD;
        pb.startHeight  = DEF_START_HEIGHT;
        pb.beamLength   = DEF_BEAM_LENGTH;
        pb.nearWidth    = DEF_NEAR_W;
        pb.farWidth     = DEF_FAR_W;
        pb.tiltDown     = DEF_TILT_DOWN;
        pb.intensity    = DEF_INTENSITY;
        pb.alpha        = DEF_ALPHA;
        pb.edgeFade     = DEF_EDGE_FADE;
        pb.drawVolume   = true;
        pb.drawFloorPatch = true;
        pb.floorLift    = 0.02f;
        pb.logStats     = true;
        pb.drawGizmos   = true;
        EditorUtility.SetDirty(pb);

        // ---------- 4. 把推流切到光锥视角 ----------
        cs.streamSource   = CameraStreamer.StreamSource.BeamObserver;
        cs.observerOffset = new Vector3(DEF_OBS_X, DEF_OBS_Y, DEF_OBS_Z);
        cs.observerLookAt = new Vector3(DEF_LOOK_X, DEF_LOOK_Y, DEF_LOOK_Z);
        cs.observerFov    = DEF_OBS_FOV;
        EditorUtility.SetDirty(cs);

        // ---------- 5. 场景脏标记 ----------
        Undo.RecordObject(carGo, "光锥投影设置");
        EditorUtility.SetDirty(carGo);

        Debug.Log("==================== 光锥投影挂载完成 ====================");
        Debug.Log($"✔ 光锥：起点 前{DEF_START_AHEAD}m / 高{DEF_START_HEIGHT}m，" +
                  $"长 {DEF_BEAM_LENGTH}m，宽 {DEF_NEAR_W}→{DEF_FAR_W}m，下压 {DEF_TILT_DOWN}°");
        Debug.Log($"✔ 推送内容已切为「光锥投影特写」（观察相机 {DEF_OBS_X},{DEF_OBS_Y},{DEF_OBS_Z}" +
                  $" 看向 {DEF_LOOK_X},{DEF_LOOK_Y},{DEF_LOOK_Z}，视场角 {DEF_OBS_FOV}°）");
        Debug.Log("★ 记得 Ctrl+S 保存场景，然后按 Play。");
        Debug.Log("★ 想边调边看效果：Play 的时候直接改车身上 ProjectionBeam 的滑杆，实时生效。");
        Debug.Log("=========================================================");
    }

    [MenuItem("工具（Tools）/光锥投影/② 检查状态", priority = 1)]
    public static void Check()
    {
        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        if (drive == null)
        {
            Debug.LogWarning("[光锥检查] 场景里找不到挂着 AutoDrive 的物体。");
            return;
        }

        GameObject carGo = drive.gameObject;
        ProjectionBeam pb = carGo.GetComponent<ProjectionBeam>();
        CameraStreamer cs = carGo.GetComponent<CameraStreamer>();

        Debug.Log("==================== 光锥投影状态检查 ====================");
        Debug.Log($"车：{carGo.name}");

        // ---- 着色器 ----
        Shader sh = Shader.Find("Custom/BeamProjection");
        Debug.Log(sh != null
            ? "✔ 着色器 Custom/BeamProjection 可用"
            : "✘ 找不到着色器 Custom/BeamProjection —— 看 Console 有无编译红字");

        // ---- ProjectionBeam ----
        if (pb == null)
        {
            Debug.LogWarning("✘ 车上没有 ProjectionBeam 组件");
            Debug.Log("  → 运行 工具（Tools）→ 光锥投影 → ① 挂载到场景");
        }
        else
        {
            Debug.Log($"✔ ProjectionBeam（{(pb.enabled ? "已启用" : "★未启用")}）");
            Debug.Log($"    画光柱 {OneZero(pb.drawVolume)}   画地面光斑 {OneZero(pb.drawFloorPatch)}" +
                      $"   总开关 showBeam {OneZero(pb.showBeam)}");
            Debug.Log($"    尺寸：前伸 {pb.startAhead}m / 高 {pb.startHeight}m / 长 {pb.beamLength}m");
            Debug.Log($"          近宽 {pb.nearWidth}m → 远宽 {pb.farWidth}m，下压 {pb.tiltDown}°");
            Debug.Log($"    外观：亮度 {pb.intensity}，不透明度 {pb.alpha}，边缘柔化 {pb.edgeFade}");

            // 有没有真的把画面接上（只有 Play 时才准）
            if (Application.isPlaying)
            {
                Debug.Log(pb.HasCameraTexture
                    ? "✔ 已拿到摄像头画面（光锥有内容）"
                    : "★ 还没拿到摄像头画面 —— 光锥会是一片黑");
            }
            else
            {
                Debug.Log("  （没在 Play 中，画面连接情况要 Play 之后才能确认）");
            }

            // 有没有多出来的光锥物体
            int nVol = 0, nFloor = 0;
            foreach (Transform t in carGo.transform)
            {
                if (t.name.StartsWith("BeamVolume")) nVol++;
                if (t.name.StartsWith("BeamFloorPatch")) nFloor++;
            }
            if (nVol > 1 || nFloor > 1)
                Debug.LogWarning($"★ 车上有多个光锥物体（光柱 {nVol} 个 / 光斑 {nFloor} 个），" +
                                 "建议删到只剩一个，否则会叠加变亮");
            else
                Debug.Log($"    光锥物体：光柱 {nVol} 个、光斑 {nFloor} 个（正常）");
        }

        // ---- CameraStreamer 推流来源 ----
        if (cs == null)
        {
            Debug.LogWarning("✘ 车上没有 CameraStreamer —— 没法推给 MATLAB");
        }
        else
        {
            string src = cs.streamSource == CameraStreamer.StreamSource.BeamObserver
                       ? "✔ 光锥投影特写（MATLAB 会看到光锥）"
                       : "★ 车载摄像头实拍（MATLAB 看到的是原始路面，不是光锥）";
            Debug.Log($"推送内容：{src}");
            Debug.Log($"    观察相机挂载 {cs.observerOffset}，看向 {cs.observerLookAt}，" +
                      $"视场角 {cs.observerFov}°");
            Debug.Log($"    分辨率 {cs.width}×{cs.height} @ {cs.targetFps} FPS → " +
                      $"{cs.matlabHost}:{cs.matlabVideoPort}");
        }

        Debug.Log("=========================================================");
    }

    [MenuItem("工具（Tools）/光锥投影/③ 切回车载摄像头实拍", priority = 2)]
    public static void BackToCarCamera()
    {
        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        if (drive == null) { Debug.LogWarning("[光锥] 找不到车。"); return; }

        CameraStreamer cs = drive.gameObject.GetComponent<CameraStreamer>();
        if (cs == null) { Debug.LogWarning("[光锥] 车上没有 CameraStreamer。"); return; }

        cs.streamSource = CameraStreamer.StreamSource.CarCamera;
        EditorUtility.SetDirty(cs);
        Debug.Log("[光锥] 推送内容已切回「车载摄像头实拍」。");
        Debug.Log("  · 光锥**还是会画在场景里**（只是不推给 MATLAB 了）；");
        Debug.Log("  · 想连光锥一起关掉，就把车身上 ProjectionBeam 的总开关 showBeam 取消勾选。");
        Debug.Log("  · 记得 Ctrl+S 保存。");
    }

    [MenuItem("工具（Tools）/光锥投影/④ 清掉光锥投影", priority = 3)]
    public static void Remove()
    {
        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        if (drive == null) { Debug.LogWarning("[光锥] 找不到车。"); return; }

        GameObject carGo = drive.gameObject;
        bool did = false;

        // ProjectionBeam 组件
        ProjectionBeam pb = carGo.GetComponent<ProjectionBeam>();
        if (pb != null)
        {
            Undo.DestroyObjectImmediate(pb);
            Debug.Log("[光锥] 已移除 ProjectionBeam 组件");
            did = true;
        }

        // 它建出来的两个物体
        foreach (Transform t in carGo.transform)
        {
            if (t.name.StartsWith("BeamVolume") || t.name.StartsWith("BeamFloorPatch"))
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                Debug.Log($"[光锥] 已删除子物体 {t.name}");
                did = true;
            }
        }

        // 观察相机
        foreach (Transform t in carGo.transform)
        {
            if (t.name.StartsWith("BeamObserverCam"))
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                Debug.Log($"[光锥] 已删除观察相机 {t.name}");
                did = true;
            }
        }

        // 推流切回实拍
        CameraStreamer cs = carGo.GetComponent<CameraStreamer>();
        if (cs != null && cs.streamSource == CameraStreamer.StreamSource.BeamObserver)
        {
            cs.streamSource = CameraStreamer.StreamSource.CarCamera;
            EditorUtility.SetDirty(cs);
            Debug.Log("[光锥] 推送内容已切回「车载摄像头实拍」");
            did = true;
        }

        if (!did) Debug.Log("[光锥] 车上本来就没有光锥投影。");
        else Debug.Log("[光锥] 完成。记得 Ctrl+S 保存场景。");
    }

    static string OneZero(bool b) => b ? "开" : "关";
}
#endif
