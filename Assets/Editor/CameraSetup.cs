#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键给车装上"车载摄像头"，把画面推给 MATLAB。
///
/// 【怎么用】
///   Unity 顶部菜单栏 → 工具（Tools）→ 车载摄像头 → 挂载到场景
///
/// 它会：
///   1. 给车（挂着 AutoDrive 的那个物体）添加 CameraStreamer 组件
///   2. 自动创建一个子物体 CarCamera（车载摄像头），位置在车头前上方
///   3. 把 CameraStreamer 的 cam 引用指向它
///
/// 【为什么第二次运行不会重复加摄像头】
///   如果车上已经有 CameraStreamer、而且它的 cam 已经连好了，
///   就直接复用，不再新建 —— 否则反复点菜单会在场景里堆一排 CarCamera。
///
/// 【★ 它和主相机的关系】
///   车载摄像头是**独立的一台相机**，只往 RenderTexture 渲染，
///   不会显示在 Game 窗口里，也不影响你现在看画面。
///   所以不用担心"加了摄像头之后主视角变了"。
/// </summary>
public static class CameraSetup
{
    // 摄像头默认参数 —— 和 CameraStreamer.cs 里的默认值保持一致
    const int   DEF_W        = 240;
    const int   DEF_H        = 180;
    const int   DEF_FPS      = 12;
    const int   DEF_QUALITY  = 60;
    const int   DEF_PORT     = 5007;
    const float DEF_OFFSET_Y = 1.35f;
    const float DEF_OFFSET_Z = 0.15f;
    const float DEF_PITCH    = 6f;
    const float DEF_FOV      = 78f;

    [MenuItem("工具（Tools）/车载摄像头/① 挂载到场景", priority = 0)]
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

        // ---------- 1. CameraStreamer 组件 ----------
        CameraStreamer cs = carGo.GetComponent<CameraStreamer>();
        if (cs == null)
        {
            cs = Undo.AddComponent<CameraStreamer>(carGo);
            Debug.Log($"[摄像头设置] 已给「{carGo.name}」添加 CameraStreamer");
        }

        // ---------- 2. 车载摄像头（子物体）----------
        // 先看 cs.cam 是不是已经连好了（之前跑过这个菜单）
        if (cs.cam != null)
        {
            Debug.Log($"[摄像头设置] 已经连好了摄像头「{cs.cam.name}」，跳过创建。");
        }
        else
        {
            // 看看车上有没有现成的、名字叫 CarCamera 的子物体
            Transform existing = carGo.transform.Find("CarCamera");
            GameObject camGo;

            if (existing != null && existing.GetComponent<Camera>() != null)
            {
                camGo = existing.gameObject;
                Debug.Log("[摄像头设置] 找到了已有的 CarCamera 子物体，直接复用。");
            }
            else
            {
                camGo = new GameObject("CarCamera");
                Undo.RegisterCreatedObjectUndo(camGo, "创建 CarCamera");
                camGo.transform.SetParent(carGo.transform, false);
                Debug.Log("[摄像头设置] 已创建子物体 CarCamera");
            }

            camGo.transform.localPosition = new Vector3(0f, DEF_OFFSET_Y, DEF_OFFSET_Z);
            camGo.transform.localRotation = Quaternion.Euler(DEF_PITCH, 0f, 0f);

            Camera cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = Undo.AddComponent<Camera>(camGo);
            cam.fieldOfView = DEF_FOV;
            cam.clearFlags  = CameraClearFlags.Skybox;
            cam.allowHDR    = false;    // 关掉 HDR，读回像素时格式更简单、更快
            cam.allowMSAA   = false;    // 关抗锯齿，省一半读回带宽
            cam.depth       = 1;        // 排在主相机之后，不抢主视图

            cs.cam = cam;
            Debug.Log($"[摄像头设置] 摄像头位置 {camGo.transform.localPosition}，" +
                      $"俯角 {DEF_PITCH}°，视场角 {DEF_FOV}°");
        }

        // ---------- 3. 把分辨率/帧率/端口都设成默认值 ----------
        // 为什么这里要"重设一遍"：脚本里的默认值只对**新加的**组件生效。
        // 如果车上早就有一个 CameraStreamer（比如之前手动加过），
        // 它保留的是创建那一刻的序列化值，改脚本事后不会自动更新。
        // 显式写一遍最保险。
        cs.enableStream    = true;
        cs.width           = DEF_W;
        cs.height          = DEF_H;
        cs.targetFps       = DEF_FPS;
        cs.jpegQuality     = DEF_QUALITY;
        cs.matlabHost      = "127.0.0.1";
        cs.matlabVideoPort = DEF_PORT;
        cs.logStats        = true;

        EditorUtility.SetDirty(cs);
        Undo.RecordObject(carGo, "摄像头设置");

        Debug.Log($"[摄像头设置] ✔ 完成：{DEF_W}×{DEF_H} @ {DEF_FPS} FPS，" +
                  $"JPEG 质量 {DEF_QUALITY}，发往 127.0.0.1:{DEF_PORT}");
        Debug.Log("[摄像头设置] 记得 Ctrl+S 保存场景，然后按 Play。");
    }

    [MenuItem("工具（Tools）/车载摄像头/② 检查挂载状态", priority = 1)]
    public static void Check()
    {
        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        if (drive == null)
        {
            Debug.LogWarning("[摄像头检查] 场景里找不到挂着 AutoDrive 的物体。");
            return;
        }

        GameObject carGo = drive.gameObject;
        CameraStreamer cs = carGo.GetComponent<CameraStreamer>();

        Debug.Log("===== 车载摄像头挂载检查 =====");
        if (cs == null)
        {
            Debug.LogWarning($"✘「{carGo.name}」上没有 CameraStreamer 组件。");
            Debug.Log("  → 运行 工具（Tools）→ 车载摄像头 → ① 挂载到场景");
            return;
        }

        Debug.Log($"✔ 车：{carGo.name}");
        Debug.Log($"  组件启用：{(cs.enabled ? "是" : "★否（不会发画面）")}");
        Debug.Log($"  推流开关 Enable Stream：{(cs.enableStream ? "是" : "★否（不会发画面）")}");
        Debug.Log($"  分辨率：{cs.width}×{cs.height}");
        Debug.Log($"  帧率：{cs.targetFps} FPS    JPEG 质量：{cs.jpegQuality}");
        Debug.Log($"  目标：{cs.matlabHost}:{cs.matlabVideoPort}");

        if (cs.cam == null)
        {
            Debug.LogWarning("  ★ cam 引用是空的 —— 会自动建一个，但建议重跑一次挂载命令");
        }
        else
        {
            bool camEnabled = cs.cam.enabled;
            Debug.Log($"  摄像头：{cs.cam.name}（{(camEnabled ? "启用" : "★未启用")}）");
        }

        // 顺便查一下场景里有没有多余的 CarCamera 子物体（跑多了菜单会留一堆）
        int extra = 0;
        foreach (Transform t in carGo.transform)
        {
            if (t.name.StartsWith("CarCamera") && (cs.cam == null || t.gameObject != cs.cam.gameObject))
                extra++;
        }
        if (extra > 0)
            Debug.LogWarning($"  ★ 车上还有 {extra} 个多余的 CarCamera 子物体，建议手动删掉");
    }

    [MenuItem("工具（Tools）/车载摄像头/③ 清掉摄像头", priority = 2)]
    public static void Remove()
    {
        AutoDrive drive = Object.FindObjectOfType<AutoDrive>();
        if (drive == null) { Debug.LogWarning("[摄像头] 找不到车。"); return; }

        GameObject carGo = drive.gameObject;
        bool did = false;

        // 删 CameraStreamer 组件
        CameraStreamer cs = carGo.GetComponent<CameraStreamer>();
        if (cs != null)
        {
            Undo.DestroyObjectImmediate(cs);
            Debug.Log("[摄像头] 已移除 CameraStreamer 组件");
            did = true;
        }

        // 删它创建出来的 CarCamera 子物体
        foreach (Transform t in carGo.transform)
        {
            if (t.name.StartsWith("CarCamera"))
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                Debug.Log($"[摄像头] 已删除子物体 {t.name}");
                did = true;
            }
        }

        if (!did) Debug.Log("[摄像头] 车上本来就没有摄像头组件。");
        else Debug.Log("[摄像头] 完成。（MATLAB 那边不用改，下次 Play 就是没有画面的状态）");
    }
}
#endif
