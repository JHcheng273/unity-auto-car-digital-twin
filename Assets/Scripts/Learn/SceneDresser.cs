using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一键把「太空里的一条路」布置成「城市里的一条路」。
///
/// 会做的事：
///   1. 沿路两侧铺人行道 + 路缘石
///   2. 沿路立路灯（左右交替，暖白色发光灯头）
///   3. 在路外随机种树
///   4. 在更远处随机盖楼（带窗户贴图）
///   5. 把地面 Plane 放大并换成草地贴图
///   6. 换天空盒、加雾、调环境光、摆太阳角度
///
/// 用法：
///   1. 层级（Hierarchy）空白处右键 → 创建空对象（Create Empty），命名 Dressing
///   2. 添加组件（Add Component）→ SceneDresser
///   3. 把挂 MyWaypointPath 的物体（Path）拖进「数据源」框
///   4. 点组件标题右上角 ⋮ → 「布置场景」
///   5. 想换个布局，改「随机种子」再点一次
///
/// 注意：路宽要跟 RoadBuilder 里填的一致，否则人行道会压在路面上。
/// </summary>
[ExecuteInEditMode]
public class SceneDresser : MonoBehaviour
{
    [Header("数据源（把挂 MyWaypointPath 的物体拖进来）")]
    public MyWaypointPath path;

    [Header("路宽（必须和 RoadBuilder 里填的一样）")]
    public float roadWidth = 3f;

    [Header("人行道 + 路缘石")]
    public bool buildSidewalk = true;
    public float sidewalkWidth = 1.6f;
    public float sidewalkHeight = 0.12f;
    public float curbHeight = 0.17f;

    [Header("路灯")]
    public bool buildLamps = true;
    public float lampSpacing = 12f;
    public float lampHeight = 5.0f;
    public float lampOffset = 1.0f;
    public bool lampAlternate = true;

    [Header("树")]
    public bool buildTrees = true;
    public int treeCount = 60;
    public float treeNear = 6f;
    public float treeFar = 22f;
    public float treeMinGap = 3.5f;

    [Header("建筑")]
    public bool buildBuildings = true;
    public int buildingCount = 26;
    public float buildingNear = 13f;
    public float buildingFar = 40f;

    [Header("地面")]
    public bool dressGround = true;
    [Tooltip("场景里地面的物体名（Unity 默认叫 Plane）")]
    public string groundName = "Plane";
    [Tooltip("地面放大倍数（Plane 原始尺寸是 10x10 米）")]
    public float groundScale = 24f;
    [Tooltip("草地贴图多少米重复一次")]
    public float groundTile = 4f;

    [Header("环境（天空 / 雾 / 环境光 / 太阳）")]
    public bool dressEnvironment = true;
    public Color skyTint = new Color(0.50f, 0.68f, 0.92f);
    public Color groundTint = new Color(0.34f, 0.38f, 0.32f);
    public Color fogColor = new Color(0.72f, 0.79f, 0.86f);
    public float fogStart = 45f;
    public float fogEnd = 190f;
    public float sunIntensity = 1.25f;

    [Header("随机种子（换个布局就改这个数）")]
    public int seed = 2026;

    [HideInInspector] public List<Material> generatedMaterials = new List<Material>();

    // 路径上的一点：位置 + 前进方向 + 右手方向
    struct Sample
    {
        public Vector3 pos;
        public Vector3 dir;
        public Vector3 right;
    }

    static readonly string[] RootNames =
    {
        "Dressing_Sidewalk", "Dressing_Lamps", "Dressing_Trees", "Dressing_Buildings"
    };

    // ============================================================
    //  主入口
    // ============================================================
    [ContextMenu("布置场景")]
    public void Build()
    {
        if (path == null || path.Count < 2)
        {
            Debug.LogWarning("[SceneDresser] 还没设置 Path，或者路标点少于 2 个", this);
            return;
        }

        ClearOld();

        List<Sample> samples = SamplePath(0.5f);
        if (samples.Count < 2)
        {
            Debug.LogWarning("[SceneDresser] 路径太短，采不到点", this);
            return;
        }

        if (buildSidewalk) BuildSidewalks(samples);
        if (buildLamps) BuildLamps(samples);
        if (buildTrees) BuildTrees(samples);
        if (buildBuildings) BuildBuildings(samples);
        if (dressGround) DressGround();
        if (dressEnvironment) DressEnvironment();

        Debug.Log("[SceneDresser] 布景完成 —— 人行道/路灯/树/建筑/地面/环境 都处理过了", this);
    }

    // ============================================================
    //  路径采样
    // ============================================================
    List<Sample> SamplePath(float step)
    {
        List<Sample> list = new List<Sample>();

        for (int i = 0; i < path.Count; i++)
        {
            int j = path.NextIndex(i);
            if (j == i) break;                       // loop 没勾 → 到最后一个点了

            Vector3 a = path.GetPoint(i);
            Vector3 b = path.GetPoint(j);
            Vector3 d = new Vector3(b.x - a.x, 0f, b.z - a.z);

            float len = d.magnitude;
            if (len < 0.01f) continue;

            Vector3 dir = d / len;
            // Cross(up, forward) = right，正好是「站在路上往前走时的右手边」
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;

            int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
            for (int k = 0; k < n; k++)
            {
                float t = (float)k / n;
                Vector3 p = a + d * t;
                list.Add(new Sample
                {
                    pos = new Vector3(p.x, 0f, p.z),
                    dir = dir,
                    right = right
                });
            }
        }
        return list;
    }

    // ============================================================
    //  1. 人行道 + 路缘石
    // ============================================================
    void BuildSidewalks(List<Sample> samples)
    {
        Transform root = NewRoot(RootNames[0]);

        Material mWalkBase = Track(MaterialLib.Textured("SidewalkMat", "concrete", Color.white, Vector2.one, 0.18f));
        Material mCurb = Track(MaterialLib.Solid("CurbMat", new Color(0.60f, 0.59f, 0.56f), 0f, 0.28f));

        float walkOffset = roadWidth * 0.5f + sidewalkWidth * 0.5f;
        float curbOffset = roadWidth * 0.5f + 0.09f;
        float curbWidth = 0.18f;

        int segIndex = 0;

        for (int i = 0; i < path.Count; i++)
        {
            int j = path.NextIndex(i);
            if (j == i) break;

            Vector3 a = path.GetPoint(i);
            Vector3 b = path.GetPoint(j);
            Vector3 d = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float len = d.magnitude;
            if (len < 0.01f) continue;

            Vector3 dir = d / len;
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            Quaternion rot = Quaternion.LookRotation(dir);
            float fullLen = len + roadWidth * 0.6f;
            Vector3 mid = new Vector3((a.x + b.x) * 0.5f, 0f, (a.z + b.z) * 0.5f);

            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? 1f : -1f;

                // 人行道：混凝土贴图按实际尺寸平铺，不然长条会被拉成条纹
                Material mWalk = new Material(mWalkBase);
                mWalk.mainTextureScale = new Vector2(
                    Mathf.Max(0.5f, sidewalkWidth / 2.2f),
                    Mathf.Max(0.5f, fullLen / 2.2f));
                Track(mWalk);

                Cube($"Walk_{segIndex}_{s}", root,
                    mid + right * (side * walkOffset) + Vector3.up * (sidewalkHeight * 0.5f),
                    rot,
                    new Vector3(sidewalkWidth, sidewalkHeight, fullLen),
                    mWalk);

                // 路缘石：夹在路面和人行道之间那道高出来的边
                Cube($"Curb_{segIndex}_{s}", root,
                    mid + right * (side * curbOffset) + Vector3.up * (curbHeight * 0.5f),
                    rot,
                    new Vector3(curbWidth, curbHeight, fullLen),
                    mCurb);
            }

            segIndex++;
        }
    }

    // ============================================================
    //  2. 路灯
    // ============================================================
    void BuildLamps(List<Sample> samples)
    {
        Transform root = NewRoot(RootNames[1]);

        Material mPole = Track(MaterialLib.Solid("LampPoleMat", new Color(0.26f, 0.27f, 0.29f), 0.75f, 0.55f));
        Material mHead = Track(MaterialLib.Emissive("LampHeadMat", new Color(1f, 0.94f, 0.78f), 3.0f));

        float acc = 0f;
        bool leftSide = false;
        int count = 0;

        for (int i = 1; i < samples.Count; i++)
        {
            acc += Vector3.Distance(samples[i].pos, samples[i - 1].pos);
            if (acc < lampSpacing) continue;
            acc = 0f;

            Sample s = samples[i];
            float side = (lampAlternate && leftSide) ? -1f : 1f;
            if (lampAlternate) leftSide = !leftSide;

            Vector3 basePos = s.pos + s.right * (side * (roadWidth * 0.5f + lampOffset));
            Vector3 toward = -s.right * side;          // 灯臂朝向路中心

            BuildOneLamp(root, basePos, toward, mPole, mHead, count);
            count++;
        }
    }

    void BuildOneLamp(Transform root, Vector3 basePos, Vector3 toward,
                      Material poleMat, Material headMat, int index)
    {
        toward.y = 0f;
        if (toward.sqrMagnitude < 1e-6f) return;
        toward.Normalize();

        // 灯柱（Cylinder 原始高 2，所以 y 缩放取一半）
        Cylinder($"LampPole_{index}", root,
            basePos + Vector3.up * (lampHeight * 0.5f),
            Quaternion.identity,
            new Vector3(0.14f, lampHeight * 0.5f, 0.14f),
            poleMat);

        // 灯臂
        float armLen = 1.5f;
        Cube($"LampArm_{index}", root,
            basePos + Vector3.up * (lampHeight - 0.12f) + toward * (armLen * 0.5f),
            Quaternion.LookRotation(toward),
            new Vector3(0.10f, 0.10f, armLen),
            poleMat);

        // 灯头（自发光，晚上也能看见）
        Cube($"LampHead_{index}", root,
            basePos + Vector3.up * (lampHeight - 0.24f) + toward * (armLen - 0.28f),
            Quaternion.LookRotation(toward),
            new Vector3(0.46f, 0.14f, 0.85f),
            headMat);
    }

    // ============================================================
    //  3. 树
    // ============================================================
    void BuildTrees(List<Sample> samples)
    {
        Transform root = NewRoot(RootNames[2]);

        Material mTrunk = Track(MaterialLib.Solid("TrunkMat", new Color(0.30f, 0.21f, 0.13f), 0f, 0.20f));

        Color[] greens =
        {
            new Color(0.16f, 0.36f, 0.16f),
            new Color(0.21f, 0.43f, 0.18f),
            new Color(0.13f, 0.30f, 0.14f),
            new Color(0.28f, 0.47f, 0.20f)
        };
        Material[] mLeaves = new Material[greens.Length];
        for (int i = 0; i < greens.Length; i++)
            mLeaves[i] = Track(MaterialLib.Solid("LeafMat" + i, greens[i], 0f, 0.15f));

        System.Random rng = new System.Random(seed);
        List<Vector3> placed = new List<Vector3>();

        int guard = 0;
        int made = 0;

        while (made < treeCount && guard < treeCount * 40)
        {
            guard++;

            Sample s = samples[rng.Next(samples.Count)];
            float side = rng.Next(2) == 0 ? 1f : -1f;
            float dist = treeNear + (float)rng.NextDouble() * (treeFar - treeNear);
            Vector3 p = s.pos + s.right * (side * dist);
            p.y = 0f;

            bool tooClose = false;
            for (int i = 0; i < placed.Count; i++)
            {
                if ((placed[i] - p).sqrMagnitude < treeMinGap * treeMinGap) { tooClose = true; break; }
            }
            if (tooClose) continue;

            placed.Add(p);
            made++;

            float trunkH = 1.6f + (float)rng.NextDouble() * 1.4f;
            float crownR = 0.9f + (float)rng.NextDouble() * 0.7f;

            Cylinder($"TreeTrunk_{made}", root,
                p + Vector3.up * (trunkH * 0.5f),
                Quaternion.identity,
                new Vector3(0.34f, trunkH * 0.5f, 0.34f),
                mTrunk);

            Sphere($"TreeCrown_{made}", root,
                p + Vector3.up * (trunkH + crownR * 0.72f),
                Quaternion.identity,
                new Vector3(crownR * 2f, crownR * 2.1f, crownR * 2f),
                mLeaves[rng.Next(mLeaves.Length)]);
        }
    }

    // ============================================================
    //  4. 建筑
    // ============================================================
    void BuildBuildings(List<Sample> samples)
    {
        Transform root = NewRoot(RootNames[3]);

        Material mFacadeBase = MaterialLib.Textured("FacadeMat", "facade", Color.white, Vector2.one, 0.22f);
        Track(mFacadeBase);

        Color[] tints =
        {
            new Color(1.00f, 1.00f, 1.00f),
            new Color(0.90f, 0.87f, 0.82f),
            new Color(0.80f, 0.83f, 0.88f),
            new Color(0.94f, 0.88f, 0.79f),
            new Color(0.85f, 0.86f, 0.85f)
        };

        System.Random rng = new System.Random(seed + 77);
        List<Vector3> placed = new List<Vector3>();
        List<float> placedSize = new List<float>();

        int guard = 0;
        int made = 0;

        while (made < buildingCount && guard < buildingCount * 40)
        {
            guard++;

            Sample s = samples[rng.Next(samples.Count)];
            float side = rng.Next(2) == 0 ? 1f : -1f;
            float dist = buildingNear + (float)rng.NextDouble() * (buildingFar - buildingNear);
            Vector3 p = s.pos + s.right * (side * dist);

            float w = 7f + (float)rng.NextDouble() * 9f;
            float dp = 7f + (float)rng.NextDouble() * 9f;
            float h = 7f + (float)rng.NextDouble() * 22f;

            bool tooClose = false;
            for (int i = 0; i < placed.Count; i++)
            {
                float need = (Mathf.Max(w, dp) + placedSize[i]) * 0.5f + 3f;
                if ((placed[i] - p).sqrMagnitude < need * need) { tooClose = true; break; }
            }
            if (tooClose) continue;

            placed.Add(p);
            placedSize.Add(Mathf.Max(w, dp));
            made++;

            // 每栋楼一个材质实例：窗户要按楼的实际尺寸平铺，一层楼约 3 米高
            Material m = new Material(mFacadeBase);
            m.color = tints[rng.Next(tints.Length)];
            m.mainTextureScale = new Vector2(
                Mathf.Max(0.25f, w / 20f),
                Mathf.Max(0.25f, h / 24f));
            Track(m);

            Cube($"Building_{made}", root,
                new Vector3(p.x, h * 0.5f, p.z),
                Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f),
                new Vector3(w, h, dp),
                m);
        }
    }

    // ============================================================
    //  5. 地面
    // ============================================================
    void DressGround()
    {
        GameObject g = GameObject.Find(groundName);
        if (g == null)
        {
            Debug.LogWarning($"[SceneDresser] 场景里没找到叫「{groundName}」的物体，地面这步跳过了。" +
                             " 你可以自己建一个 3D 对象 → 平面（Plane），命名成 Plane。", this);
            return;
        }

        g.transform.position = Vector3.zero;
        g.transform.rotation = Quaternion.identity;
        g.transform.localScale = new Vector3(groundScale, 1f, groundScale);

        float size = 10f * groundScale;                 // Plane 原始是 10x10 米
        Material m = Track(MaterialLib.Textured("GroundGrassMat", "grass", Color.white,
            new Vector2(size / groundTile, size / groundTile), 0.12f));

        MeshRenderer mr = g.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = m;

        // 地面不需要碰撞体，车是运动学模型
        Collider c = g.GetComponent<Collider>();
        if (c != null) DestroyObj(c);
    }

    // ============================================================
    //  6. 环境
    // ============================================================
    void DressEnvironment()
    {
        // ---- 天空盒：用程序化天空，比默认的灰蓝好看得多 ----
        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            Material sky = Track(new Material(skyShader));
            sky.name = "DressedSky";
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_SunSizeConvergence", 2f);
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetColor("_SkyTint", skyTint);
            sky.SetColor("_GroundColor", groundTint);
            sky.SetFloat("_Exposure", 1.15f);
            RenderSettings.skybox = sky;
        }
        else
        {
            Debug.LogWarning("[SceneDresser] 找不到 Skybox/Procedural，天空盒跳过");
        }

        // ---- 环境光：天光 / 地平线 / 地面反射三段式 ----
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(skyTint.r * 1.10f, skyTint.g * 1.10f, skyTint.b * 1.10f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.46f, 0.44f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.21f, 0.18f);
        RenderSettings.ambientIntensity = 1.05f;

        // ---- 雾：让远处淡出，解决「空旷得像太空」 ----
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;

        // ---- 太阳 ----
        Light sun = null;
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional) { sun = lights[i]; break; }
        }

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
        }

        // 让天空盒立刻参与环境光计算
        DynamicGI.UpdateEnvironment();
    }

    // ============================================================
    //  工具
    // ============================================================
    Transform NewRoot(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    GameObject Cube(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        return Make(PrimitiveType.Cube, name, parent, pos, rot, scale, mat);
    }

    GameObject Cylinder(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        return Make(PrimitiveType.Cylinder, name, parent, pos, rot, scale, mat);
    }

    GameObject Sphere(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        return Make(PrimitiveType.Sphere, name, parent, pos, rot, scale, mat);
    }

    GameObject Make(PrimitiveType type, string name, Transform parent, Vector3 pos,
                    Quaternion rot, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;

        // 布景零件一律不带碰撞体：车是运动学模型，留着只会挡前向射线
        Collider col = go.GetComponent<Collider>();
        if (col != null) DestroyObj(col);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;

        return go;
    }

    void ClearOld()
    {
        for (int i = 0; i < generatedMaterials.Count; i++)
            if (generatedMaterials[i] != null) DestroyObj(generatedMaterials[i]);
        generatedMaterials.Clear();

        for (int i = 0; i < RootNames.Length; i++)
        {
            Transform t = transform.Find(RootNames[i]);
            if (t != null) DestroyObj(t.gameObject);
        }
    }

    Material Track(Material m)
    {
        if (m != null) generatedMaterials.Add(m);
        return m;
    }

    void DestroyObj(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
