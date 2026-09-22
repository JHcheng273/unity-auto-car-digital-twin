#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键搭建 B 的测试场景（只在你自己的 B_Test 场景里用，别在 Main 场景点）
///
/// 用法：Unity 顶部菜单 → Tools → B 模块 → 按顺序点两个菜单项
///
/// 注意：这个文件必须放在 Assets/Editor 目录下，否则打包时会报错
/// </summary>
public static class BTestSceneBuilder
{
    const string MENU = "Tools/B模块/";

    // ==========================================================
    // 1. 创建 Layer
    // ==========================================================
    [MenuItem(MENU + "1. 创建 Obstacle 与 Vehicle 层")]
    public static void CreateLayers()
    {
        TryCreateLayer(8, "Obstacle");
        TryCreateLayer(9, "Vehicle");
        Debug.Log("[B模块] Layer 创建完成。去 Edit > Project Settings > Tags and Layers 确认一下。");
    }

    static void TryCreateLayer(int index, string name)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogWarning("[B模块] 找不到 TagManager.asset，请手动在 Project Settings 里建 Layer");
            return;
        }

        var so = new SerializedObject(assets[0]);
        var layers = so.FindProperty("layers");
        if (layers == null || index >= layers.arraySize)
        {
            Debug.LogWarning("[B模块] 读不到 layers 数组，请手动建 Layer");
            return;
        }

        var slot = layers.GetArrayElementAtIndex(index);
        if (!string.IsNullOrEmpty(slot.stringValue) && slot.stringValue != name)
        {
            Debug.LogWarning($"[B模块] 第 {index} 层已经被占用（{slot.stringValue}），没动它。" +
                             $"请手动把「{name}」放到别的空层，并记住层号。");
            return;
        }

        slot.stringValue = name;
        so.ApplyModifiedProperties();   // 注意：是 ApplyModifiedProperties，不是 ApplyModifications
        Debug.Log($"[B模块] 已创建 Layer {index} = {name}");
    }

    // ==========================================================
    // 2. 搭场景
    // ==========================================================
    [MenuItem(MENU + "2. 搭建 B 测试场景（会清空同名旧物体）")]
    public static void BuildTestScene()
    {
        // 先清掉旧的，避免重复点击堆一堆
        DeleteIfExists("Ground");
        DeleteIfExists("Path");
        DeleteIfExists("B_TestCar");
        DeleteIfExists("PathViz");
        DeleteIfExists("Obstacles");

        // ---- 地面 ----
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10f, 1f, 10f);   // 100m x 100m

        // ---- 路径：8 个点摆成一个椭圆 ----
        var pathGO = new GameObject("Path");
        var pathComp = pathGO.AddComponent<WaypointPath>();
        pathComp.loop = true;
        pathComp.arrivalRadius = 3f;

        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            var wp = new GameObject("WP" + i);
            wp.transform.SetParent(pathGO.transform);
            wp.transform.position = new Vector3(Mathf.Cos(a) * 25f, 0.5f, Mathf.Sin(a) * 15f);
        }
        pathComp.CollectChildren();

        // ---- 车（先用 Cube 顶替，A 的真车做好后替换掉）----
        var car = GameObject.CreatePrimitive(PrimitiveType.Cube);
        car.name = "B_TestCar";
        car.transform.position = pathComp.GetPoint(0) + Vector3.up * 0.5f;
        car.transform.localScale = new Vector3(1.5f, 1f, 3f);
        // 只取水平方向，避免车身被"抬头/低头"（会让射线斜着打出去）
        Vector3 look = pathComp.GetPoint(1);
        look.y = car.transform.position.y;
        car.transform.LookAt(look);

        var follower = car.AddComponent<WaypointFollower>();
        follower.path = pathComp;

        var detector = car.AddComponent<FrontCarDetector>();
        detector.detectionRange = 20f;
        detector.triggerDistance = 15f;
        detector.heightOffset = 0.5f;

        // 只检测 Obstacle / Vehicle（层建好了才生效）
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        if (obstacleLayer >= 0)
            detector.detectLayers = LayerMask.GetMask("Obstacle", "Vehicle");
        else
            Debug.LogWarning("[B模块] 没找到 Obstacle 层，射线会打到地面。先点菜单第 1 项建 Layer。");

        var rig = car.AddComponent<BTestRig>();
        rig.follower = follower;
        rig.detector = detector;
        rig.moveSpeed = 6f;

        var facade = car.AddComponent<BModuleFacade>();   // 给 C 的接口
        facade.detector = detector;                        // 必须手动接上，否则 C 读到的是空值
        facade.follower = follower;

        // ---- 障碍物 ----
        var obsRoot = new GameObject("Obstacles");
        MakeObstacle(obsRoot.transform, "Obstacle_正面", pathComp.GetPoint(3), obstacleLayer);
        MakeObstacle(obsRoot.transform, "Obstacle_侧边", pathComp.GetPoint(6) + new Vector3(3f, 0f, 0f), obstacleLayer);

        // ---- 路径可视化 ----
        var vizGO = new GameObject("PathViz");
        var viz = vizGO.AddComponent<PathVisualizer>();
        viz.path = pathComp;
        viz.follower = follower;

        // ---- 灯光（没有就加一个）----
        if (Object.FindObjectOfType<Light>() == null)
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // ---- 摄像机拉到能看全的位置 ----
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 45f, -45f);
            cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        }

        Selection.activeGameObject = car;
        // EditorSceneManager 在 UnityEditor.SceneManagement 里，这里写全名最保险
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[B模块] 场景搭好了！按 Play 试试。\n" +
                  "  · 方块应该自己沿 8 个青色点跑圈\n" +
                  "  · 遇到灰色障碍物会减速停下\n" +
                  "  · Scene 视图能看到绿色/红色射线\n" +
                  "  · 跑不起来先看 Console 的红色报错");
    }

    static void MakeObstacle(Transform parent, string name, Vector3 pos, int layer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = pos + Vector3.up * 1f;
        go.transform.localScale = new Vector3(2f, 2f, 2f);
        if (layer >= 0) go.layer = layer;
    }

    static void DeleteIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }
}
#endif
