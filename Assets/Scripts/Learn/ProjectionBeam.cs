using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 车载摄像头画面的「立体光锥投影」。
///
/// 【它长什么样】
///   从车头（车灯高度）往前下方打出一根半透明的光柱 —— 越远越宽，
///   落到地面时摊成一片梯形的光斑。摄像头拍到的画面就投在这束光里。
///   因为用的是加法混合，光斑下面的车道线、路面纹理、障碍物都还能看见，
///   光只是盖在上面让它们变亮 —— 这才是"光"该有的样子。
///
/// 【为什么要做成"真的几何体"而不是一张贴图】
///   一张平贴的 Quad 从侧面看会变成一条线，俯视又完全看不见，
///   怎么摆都不像光。做成真几何体（锥体 + 地面梯形）之后：
///     · 从侧后方看 → 能看到光柱的立体形状
///     · 从上方俯视   → 能看到地面上的光斑
///     · 车转弯时     → 光柱跟着车头转，有真实的方位感
///
/// 【★ 它是怎么和 CameraStreamer 配合的】
///   CameraStreamer 把车载摄像头渲染到一张 RenderTexture（就是"画面"）。
///   这个脚本把那张 RenderTexture 当贴图，贴到光锥上。
///   所以两个脚本的关系是：
///       CameraStreamer 负责「拍」（产出画面）
///       ProjectionBeam  负责「投」（把画面变成光）
///   ★ 关键：CameraStreamer 必须**继续采集**（否则画面是黑的），
///     但它的画面**不再直接推给 MATLAB** —— 推给 MATLAB 的是
///     「观察相机拍到的光锥」。
///
/// 【几何参数为什么这么取值】车长 3.2 米、宽 1.6 米（见 CarBuilder）
///   · 起点在车头前 0.3 米、高 0.75 米（车灯高度附近）
///   · 长度 7.5 米 → 落到地面时离车头约 8 米，正好是"一个反应距离"
///   · 近处宽 1.2 米（比车窄一点，像聚光灯的窄端）
///   · 远处宽 4.8 米（散开 4 倍，视觉上像光锥）
/// </summary>
[RequireComponent(typeof(CameraStreamer))]
public class ProjectionBeam : MonoBehaviour
{
    [Header("投影开关")]
    [Tooltip("关掉就不画光锥了（MATLAB 那边会看到纯场景画面）")]
    public bool showBeam = true;

    [Header("几何尺寸（米）")]
    [Tooltip("光锥起点离车头多远。别贴到 0，否则会和车头模型打架")]
    public float startAhead = 0.30f;
    [Tooltip("光锥起点高度。约等于车灯高度")]
    public float startHeight = 0.75f;
    [Tooltip("光锥长度（斜边长）。7.23 配合下面的倾角，正好让光斑落在离车头 7.5 米处")]
    public float beamLength = 7.23f;

    [Tooltip("近端宽度（靠车这头）")]
    public float nearWidth = 1.20f;
    [Tooltip("远端宽度（靠车头前方那头）—— 越宽越像散开的光")]
    public float farWidth = 4.80f;

    [Header("倾角")]
    [Tooltip("光锥下压角度（度）。0 = 水平打出去，正值 = 往下打。\n" +
             "★ 5.6° 是「由落地点反推」出来的值：\n" +
             "   起点高 0.75 m、前伸 0.30 m，要落到离车头 7.5 m 处 → atan(0.70/7.20) ≈ 5.6°。\n" +
             "   不建议凭感觉设角度 —— 设大了光锥会穿地（被自动夹紧到平着打），\n" +
             "   表现为「滑杆怎么调都不动」。")]
    [Range(0f, 60f)]
    public float tiltDown = 5.6f;

    [Header("外观")]
    [Tooltip("光的颜色。偏青白最像投影仪，暖白像车灯")]
    public Color beamColor = new Color(0.62f, 0.88f, 1.00f, 1f);
    [Tooltip("亮度倍数")]
    [Range(0.2f, 3f)]
    public float intensity = 1.35f;
    [Tooltip("整体不透明度。太高会把路面盖住")]
    [Range(0.1f, 1f)]
    public float alpha = 0.72f;
    [Tooltip("边缘柔化。光斑边界渐隐的范围")]
    [Range(0.02f, 0.5f)]
    public float edgeFade = 0.22f;

    [Header("光柱与地面光斑")]
    [Tooltip("画不画立体的光柱（锥体）。关掉就只剩地面光斑")]
    public bool drawVolume = true;
    [Tooltip("画不画地面光斑（梯形）。这是俯视时最明显的部分")]
    public bool drawFloorPatch = true;
    [Tooltip("地面光斑离地高度。给 0.02 足够避开 Z-fighting，\n" +
             "太大会看起来飘在空中")]
    public float floorLift = 0.02f;

    [Header("地面检测")]
    [Tooltip("往下打射线找地面的层。留空 = 用 Default")]
    public LayerMask groundMask = ~0;
    [Tooltip("射线最远找多远")]
    public float groundRayLength = 12f;

    [Header("调试")]
    public bool logStats = true;
    [Tooltip("在 Scene 视图里画光锥的线框，方便对位置")]
    public bool drawGizmos = true;

    // ===== 内部 =====
    CameraStreamer _streamer;
    GameObject _volumeGo;        // 立体光柱
    GameObject _floorGo;         // 地面光斑
    MeshRenderer _volumeRend;
    MeshRenderer _floorRend;
    Material _volumeMat;
    Material _floorMat;
    Texture _camTex;             // 摄像头画面（来自 CameraStreamer 的 RenderTexture）

    // 每帧算出来的几何（也给 Gizmos 用）
    Vector3 _nearLeft, _nearRight, _farLeft, _farRight;
    Vector3 _origin;

    void Start()
    {
        _streamer = GetComponent<CameraStreamer>();
        if (_streamer == null)
        {
            Debug.LogError("[ProjectionBeam] 车上没有 CameraStreamer，光锥没有画面可投。", this);
            enabled = false;
            return;
        }

        BuildGeometry();
        if (logStats)
        {
            Debug.Log($"[ProjectionBeam] ✔ 光锥已建立：起点高 {startHeight} m、前伸 {startAhead} m，" +
                      $"长 {beamLength} m，宽 {nearWidth}→{farWidth} m，下压 {tiltDown}°");
        }
    }

    /// <summary>
    /// 造两个几何体。都在车的**局部空间**里，所以车一转它们自动跟着转，
    /// 不需要每帧更新 Transform —— 性能上好很多。
    /// </summary>
    void BuildGeometry()
    {
        // ---- 材质 ----
        Shader sh = Shader.Find("Custom/BeamProjection");
        if (sh == null)
        {
            Debug.LogError("[ProjectionBeam] 找不到 Custom/BeamProjection 着色器。\n" +
                           "确认 Assets/Shaders/BeamProjection.shader 存在、且 Unity 编译通过（Console 无红字）。", this);
            enabled = false;
            return;
        }

        _volumeMat = new Material(sh);
        _volumeMat.name = "BeamVolumeMat";
        _floorMat = new Material(sh);
        _floorMat.name = "BeamFloorMat";
        ApplyMaterialLook(_volumeMat, UnityEngine.Rendering.CullMode.Off);   // 光柱两面都要看得见
        ApplyMaterialLook(_floorMat, UnityEngine.Rendering.CullMode.Back);   // 光斑只看正面

        // ---- 立体光柱 ----
        if (drawVolume)
        {
            _volumeGo = new GameObject("BeamVolume");
            _volumeGo.transform.SetParent(transform, false);
            MeshFilter mf = _volumeGo.AddComponent<MeshFilter>();
            _volumeRend = _volumeGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildVolumeMesh();
            _volumeRend.sharedMaterial = _volumeMat;
            _volumeRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _volumeRend.receiveShadows = false;
        }

        // ---- 地面光斑 ----
        if (drawFloorPatch)
        {
            _floorGo = new GameObject("BeamFloorPatch");
            _floorGo.transform.SetParent(transform, false);
            MeshFilter mf = _floorGo.AddComponent<MeshFilter>();
            _floorRend = _floorGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildFloorMesh();
            _floorRend.sharedMaterial = _floorMat;
            _floorRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _floorRend.receiveShadows = false;
            // ★ 光斑必须排在光柱前面渲染一次（其实两者都在 Transparent 队列，
            //   这里显式调高 renderQueue 保证光斑最后画、盖在光柱之上，视觉更"接地"）
            _floorMat.renderQueue = 3001;
        }

        _volumeMat.renderQueue = 3000;
    }

    void ApplyMaterialLook(Material m, UnityEngine.Rendering.CullMode cull)
    {
        m.SetColor("_Color", beamColor);
        m.SetFloat("_Intensity", intensity);
        m.SetFloat("_Alpha", alpha);
        m.SetFloat("_EdgeFade", edgeFade);
        m.SetFloat("_ScanSpeed", 0.35f);
        m.SetFloat("_ScanAmount", 0.14f);
        m.SetFloat("_NoiseAmt", 0.045f);
        m.SetFloat("_Cull", (float)(int)cull);
    }

    // ==================== 几何构造 ====================

    /// <summary>
    /// 立体光柱：一个**四棱锥台**（近端是个小矩形，远端是个大矩形）。
    ///
    /// 【为什么用四棱锥台而不是圆锥】
    ///   投影画面本身是矩形的（4:3），用矩形锥能让画面不变形地贴上去。
    ///   圆锥贴矩形画面会需要额外的裁剪逻辑，得不偿失。
    ///
    /// 【UV 怎么给】
    ///   四个侧面的 UV 都从 (0,0) 到 (1,1) —— 也就是每个面都"完整"显示一次画面。
    ///   这是刻意的：从任何角度看，都能看到完整的投影内容，
    ///   而不会因为只露出画面的一部分而看起来像"穿帮"。
    ///
    /// 【坐标空间】全部在车的局部空间，+Z 是车头方向。
    /// </summary>
    Mesh BuildVolumeMesh()
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // 近端四个角（车这头）
        float nearY = startHeight;
        float nearZ = startAhead;
        float nHalf = nearWidth * 0.5f;

        // 远端四个角：沿 Z 前进 beamLength，同时按 tiltDown 下沉
        float rad = tiltDown * Mathf.Deg2Rad;
        float farZ = startAhead + beamLength * Mathf.Cos(rad);
        float farY = startHeight - beamLength * Mathf.Sin(rad);
        float fHalf = farWidth * 0.5f;

        // ★ 远端不能穿到地面以下。
        //   下压角度大 + 光锥长的时候很容易穿地，穿地后光柱会插进路面里、
        //   从下面看是一坨怪东西。这里夹一下，保证远端至少离地 0.05。
        float minY = 0.05f;
        if (farY < minY)
        {
            float fixedSin = (startHeight - minY) / beamLength;
            fixedSin = Mathf.Clamp(fixedSin, 0f, 1f);
            rad = Mathf.Asin(fixedSin);
            farZ = startAhead + beamLength * Mathf.Cos(rad);
            farY = minY;
        }

        _nearLeft  = new Vector3(-nHalf, nearY, nearZ);
        _nearRight = new Vector3( nHalf, nearY, nearZ);
        _farLeft   = new Vector3(-fHalf, farY,  farZ);
        _farRight  = new Vector3( fHalf, farY,  farZ);
        _origin    = new Vector3(0f, nearY, nearZ);

        // ---- 四个侧面 ----
        // 每个面：4 个顶点 + 2 个三角形，UV 铺满 0~1
        // 侧面 ABCD 顺序：nearLeft → nearRight → farRight → farLeft
        AddQuad(verts, uvs, tris,
                _nearLeft, _nearRight, _farRight, _farLeft);

        // ---- 远端封口（斜的"屏幕"那一面）----
        //   没有它的话，从正前方看会看到光柱"漏底"。
        AddQuad(verts, uvs, tris,
                _farLeft, _farRight, _farRight, _farLeft, flip: true);

        Mesh mesh = new Mesh { name = "BeamVolumeMesh" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 地面光斑：躺在路面上的梯形（近窄远宽），四个角对应光锥落地的位置。
    /// 这是俯视时最显眼的部分 —— MATLAB 看到的"光锥"主要就是它。
    /// </summary>
    Mesh BuildFloorMesh()
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        // 用和光柱一致的参数重算一遍四角，保证两者严丝合缝
        float rad = tiltDown * Mathf.Deg2Rad;
        float farZ = startAhead + beamLength * Mathf.Cos(rad);
        float farY = startHeight - beamLength * Mathf.Sin(rad);
        if (farY < 0.05f)
        {
            float s = Mathf.Clamp((startHeight - 0.05f) / beamLength, 0f, 1f);
            rad = Mathf.Asin(s);
            farZ = startAhead + beamLength * Mathf.Cos(rad);
        }

        // ★ 光斑要从**车头附近**一直铺到光锥落地点。
        //   如果只从光锥落地点开始铺，车和光斑之间会有一大段空白，很怪。
        //   所以近边取在车头前一点点（和光锥起点同一个 Z），
        //   近边宽度按光柱起点宽度稍微收一点。
        float z0 = startAhead;
        float z1 = farZ;
        float w0 = nearWidth * 0.85f;   // 近边略微收窄，看起来像光刚落地
        float w1 = farWidth;

        float y = floorLift;   // 贴地，抬一点点避免和路面 Z-fighting

        Vector3 nl = new Vector3(-w0 * 0.5f, y, z0);
        Vector3 nr = new Vector3( w0 * 0.5f, y, z0);
        Vector3 fr = new Vector3( w1 * 0.5f, y, z1);
        Vector3 fl = new Vector3(-w1 * 0.5f, y, z1);

        // ★ 顶点的 V 坐标（uv.y）要和光柱对得上：
        //   光斑的近边 = 画面的底部（uv.y=0），远边 = 画面顶部（uv.y=1）。
        //   这样俯视时看到的画面方向和光柱里是一致的，不会上下颠倒。
        AddQuad(verts, uvs, tris, nl, nr, fr, fl);

        Mesh mesh = new Mesh { name = "BeamFloorMesh" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// 往网格里塞一个四边形（两个三角形）。
    /// UV 固定铺满 0~1 —— 透视变形是靠「顶点位置本身是梯形」实现的，
    /// 不需要在 UV 上做任何手脚。
    /// </summary>
    static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
                        Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                        bool flip = false)
    {
        int baseIdx = verts.Count;

        verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);

        // a=近左 b=近右 c=远右 d=远左
        // uv：a(0,0) b(1,0) c(1,1) d(0,1)  —— 画面底部在近端
        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(0f, 1f));

        if (!flip)
        {
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
        }
        else
        {
            // 反面：绕序反过来，法线朝反向
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
        }
    }

    // ==================== 每帧 ====================

    void Update()
    {
        if (!showBeam) return;
        if (_volumeRend == null && _floorRend == null) return;

        // ---- 把摄像头画面喂给材质 ----
        //   CameraStreamer 在自己 Start 里建好了 RenderTexture，
        //   这里每帧取一次引用（它可能是延迟创建的，所以不能只在 Start 取）。
        Texture tex = _streamer != null ? _streamer.CameraTexture : null;
        if (tex != null && tex != _camTex)
        {
            _camTex = tex;
            if (_volumeMat != null) _volumeMat.SetTexture("_MainTex", tex);
            if (_floorMat != null)  _floorMat.SetTexture("_MainTex", tex);
        }

        // ---- 参数实时跟随 Inspector（方便一边 Play 一边调）----
        if (_volumeMat != null) ApplyMaterialLook(_volumeMat, UnityEngine.Rendering.CullMode.Off);
        if (_floorMat != null)  ApplyMaterialLook(_floorMat, UnityEngine.Rendering.CullMode.Back);

        // ---- 光柱/光斑的开关 ----
        if (_volumeGo != null) _volumeGo.SetActive(drawVolume);
        if (_floorGo != null)  _floorGo.SetActive(drawFloorPatch);
    }

    void OnDestroy()
    {
        if (_volumeMat != null) Destroy(_volumeMat);
        if (_floorMat != null) Destroy(_floorMat);
    }

    // ==================== Scene 视图辅助 ====================

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        // 用和运行时一致的算法算一遍四角
        if (Application.isPlaying && _volumeGo != null)
        {
            // 运行时已经算好了，直接用
        }
        else
        {
            float rad = tiltDown * Mathf.Deg2Rad;
            float farZ = startAhead + beamLength * Mathf.Cos(rad);
            float farY = startHeight - beamLength * Mathf.Sin(rad);
            if (farY < 0.05f)
            {
                float s = Mathf.Clamp((startHeight - 0.05f) / beamLength, 0f, 1f);
                rad = Mathf.Asin(s);
                farZ = startAhead + beamLength * Mathf.Cos(rad);
                farY = 0.05f;
            }
            float nHalf = nearWidth * 0.5f, fHalf = farWidth * 0.5f;
            _nearLeft  = new Vector3(-nHalf, startHeight, startAhead);
            _nearRight = new Vector3( nHalf, startHeight, startAhead);
            _farLeft   = new Vector3(-fHalf, farY, farZ);
            _farRight  = new Vector3( fHalf, farY, farZ);
        }

        Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.9f);
        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        // 近端矩形
        Gizmos.DrawLine(_nearLeft, _nearRight);
        Gizmos.DrawLine(_nearRight, _farRight);
        Gizmos.DrawLine(_farRight, _farLeft);
        Gizmos.DrawLine(_farLeft, _nearLeft);
        // 两条棱
        Gizmos.DrawLine(_nearLeft, _farLeft);
        Gizmos.DrawLine(_nearRight, _farRight);

        Gizmos.matrix = old;
    }

    // ==================== 公开接口 ====================

    /// <summary>外部（菜单/其它脚本）用来强行重建几何。改了尺寸参数后可调它。</summary>
    public void Rebuild()
    {
        if (_volumeGo != null) { Destroy(_volumeGo); _volumeGo = null; _volumeRend = null; }
        if (_floorGo != null)  { Destroy(_floorGo);  _floorGo = null;  _floorRend = null; }
        BuildGeometry();
    }

    /// <summary>光锥是否已经建好（菜单检查用）</summary>
    public bool IsBuilt => (_volumeGo != null || _floorGo != null) && _camTex != null;

    /// <summary>当前有没有拿到摄像头画面</summary>
    public bool HasCameraTexture => _camTex != null;
}
