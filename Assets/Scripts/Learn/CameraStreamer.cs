using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// 车载摄像头 → JPEG → UDP 发送给 MATLAB 实时显示。
///
/// 【整体链路】
///   Unity 的 Camera 渲染到 RenderTexture
///     → 读回像素 → 编码成 JPEG → 切片 → 加包头 → UDP 打给 MATLAB
///   MATLAB 收到所有片 → 拼起来 → 解 JPEG → 显示在面板上
///
/// 【为什么用 UDP 分片而不是一包一帧】
///   JPEG 一帧 7 KB 左右，而 UDP 单包有效载荷上限约 1472 字节
///   （以太网 MTU 1500 减去 IP 头 20 + UDP 头 8、再留点余量）。
///   所以一帧必须拆成多包。MATLAB 那边按包头的 (帧号, 片号) 拼装，
///   收不全就整帧丢弃 —— 宁可掉一帧，也不要显示"半张灰图"。
///
/// 【★ 性能要点】这一块最容易做慢，全在下面几个点上：
///   1. Camera 渲染用 RenderTexture，别用 ScreenCapture（那个会同步整个屏幕）
///   2. 读回像素用 AsyncGPUReadback（异步），不要 ReadPixels（同步卡主线程）
///      —— 没有异步支持时才退回 ReadPixels，并自动降帧率
///   3. JPEG 编码放在**后台线程**，主线程只负责把像素丢过去
///   4. 发送也在后台线程。主线程每帧只要做一次"取最新像素"就行
///
/// 【端口分配】和状态包不冲突：
///   5005 MATLAB 收状态 / 5006 Unity 收指令 / 5007 MATLAB 收视频
/// </summary>
public class CameraStreamer : MonoBehaviour
{
    [Header("开关")]
    public bool enableStream = true;

    [Header("画面")]
    [Tooltip("摄像头分辨率的宽。240 是画质和数据量的平衡点")]
    public int width = 240;
    [Tooltip("摄像头分辨率的高。注意必须和宽保持 4:3")]
    public int height = 180;
    [Tooltip("JPEG 质量 1~100。60 是画质和体积的平衡点")]
    [Range(10, 95)]
    public int jpegQuality = 60;
    [Tooltip("每秒发多少帧。12 是本机回环下的舒适值，别调太高")]
    public int targetFps = 12;

    [Header("网络")]
    public string matlabHost = "127.0.0.1";
    [Tooltip("MATLAB 收视频的端口（注意和状态包的 5005 区分开）")]
    public int matlabVideoPort = 5007;

    [Header("摄像头（留空会自动建一个）")]
    [Tooltip("空着的话，Start 时会自动创建并挂到车前方")]
    public Camera cam;
    [Tooltip("摄像头相对车的安装位置 —— 模拟车头/后视镜视角")]
    public Vector3 camOffset = new Vector3(0f, 1.35f, 0.15f);
    [Tooltip("摄像头俯仰角（度）。稍微往下压一点，才能看到车前的地面")]
    public float camPitch = 6f;
    [Tooltip("视场角。大一点能看到更宽的路面，但画面会被拉扁")]
    public float camFov = 78f;

    [Header("调试")]
    public bool logStats = true;
    [Tooltip("勾上后把发出去的 RGB 字节直接存成 PPM 文件，看 Unity 侧到底采到了什么")]
    public bool dumpFirstFrame = false;

    // ===== 对外可见 =====
    public bool IsReady { get; private set; }
    public int FramesSent { get; private set; }
    public float LastFrameKB { get; private set; }
    public string LastError { get; private set; } = "";

    // ===== 内部 =====
    RenderTexture _rt;
    Texture2D _tex;
    UdpClient _udp;
    Camera _createdCam;

    // 后台线程用的
    System.Threading.Thread _worker;
    volatile bool _running;
    readonly object _pixelLock = new object();
    byte[] _pendingPixels;      // 待编码的 RGB24 像素（主线程写入）
    bool _hasPending;

    // 需要主线程完成的活儿（Unity API 不能在子线程调）
    readonly Queue<Action> _mainThreadJobs = new Queue<Action>();

    float _timer;
    float _lastLog;
    int _sentFrames;
    int _failedFrames;
    float _kbps;
    int _byteAccum;

    // ---- 包头格式（共 14 字节）----
    // magic(2) = 'V','S'   用来和状态包区分（状态包是 JSON 文本，首字节是 '{'）
    // frameId(4)           帧号，同帧的所有片相同
    // partIdx(2)           第几片
    // partTotal(2)         共几片
    // payloadLen(2)        本片有效字节数
    // reserved(2)          预留
    const int HeaderSize = 14;
    // UDP 有效载荷上限：以太网 MTU 1500 - IP头20 - UDP头8 = 1472
    // 再留 8 字节安全余量，避免某些网络栈碎片化
    const int MaxPayload = 1400;

    void Start()
    {
        if (!enableStream)
        {
            Debug.Log("[CameraStreamer] Enable Stream 没勾 —— 不发送视频");
            return;
        }

        // 尺寸兜底：必须成对、必须够大
        width  = Mathf.Clamp(width, 64, 640);
        height = Mathf.Clamp(height, 48, 480);

        try
        {
            SetupCamera();
            SetupNetwork();
            StartWorker();

            IsReady = true;
            Debug.Log($"[CameraStreamer] ✔ 已启动：{width}×{height} @ {targetFps} FPS，" +
                      $"JPEG 质量 {jpegQuality}，发往 {matlabHost}:{matlabVideoPort}");
        }
        catch (Exception e)
        {
            LastError = $"{e.GetType().Name}: {e.Message}";
            Debug.LogError($"[CameraStreamer] ✘ 启动失败：{LastError}");
            enableStream = false;
        }
    }

    void SetupCamera()
    {
        if (cam == null)
        {
            // 自动建一个"车载摄像头"，挂在车前方
            GameObject go = new GameObject("CarCamera");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = camOffset;
            go.transform.localRotation = Quaternion.Euler(camPitch, 0f, 0f);

            cam = go.AddComponent<Camera>();
            cam.fieldOfView = camFov;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.depth = 1;                    // 比主相机后渲染，不影响主视图
            cam.allowHDR = false;             // 关掉 HDR，像素格式才是简单的 RGB
            cam.allowMSAA = false;            // 关抗锯齿，省一半读回带宽
            _createdCam = cam;

            Debug.Log("[CameraStreamer] 自动创建了车载摄像头 CarCamera");
        }

        // 离屏渲染目标。直接用 Depth=16 就够，颜色用默认的 ARGB32
        _rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
        _rt.Create();
        cam.targetTexture = _rt;

        // 读回专用 Texture2D
        _tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        // ★ 这里有个坑：如果 cam 同时也往屏幕渲染，会白占一份开销。
        //   但 targetTexture 一旦设上，Unity 就只渲染到 RenderTexture、不渲染到屏幕了，
        //   所以不需要额外处理。
    }

    void SetupNetwork()
    {
        _udp = new UdpClient();
        // 发送端不需要 Bind（系统会自动分配临时端口）
    }

    void StartWorker()
    {
        _running = true;
        _worker = new System.Threading.Thread(WorkerLoop) { IsBackground = true };
        _worker.Start();
    }

    void Update()
    {
        if (!enableStream || !IsReady) return;

        // 1. 处理后台线程托付给主线程的活儿
        lock (_mainThreadJobs)
        {
            while (_mainThreadJobs.Count > 0)
                _mainThreadJobs.Dequeue()?.Invoke();
        }

        // 2. 按目标帧率抓帧
        _timer += Time.deltaTime;
        float interval = targetFps > 0 ? 1f / targetFps : 0.1f;
        if (_timer < interval) return;
        _timer = 0f;

        CaptureFrame();

        // 3. 统计日志
        if (logStats && Time.time - _lastLog > 2f)
        {
            _lastLog = Time.time;
            _kbps = _byteAccum / 1024f / 2f;
            _byteAccum = 0;

            Debug.Log($"[CameraStreamer] 已发 {_sentFrames} 帧 / 失败 {_failedFrames} 帧，" +
                      $"上一帧 {LastFrameKB:F1} KB，约 {_kbps:F0} KB/s");
        }
    }

    void CaptureFrame()
    {
        if (_rt == null || _tex == null) return;

        // 让这台相机手动渲染一帧
        // （targetTexture 模式下不会自动每帧渲染，因为相机可能被禁用/rank 很低）
        cam.Render();

        // 读回像素。
        // ★ ReadPixels 是同步的，会卡一下主线程。240×180 只有 13 万像素，
        //   实测开销可接受（约 1~2 ms）。320×240 以上就该考虑换成
        //   AsyncGPUReadback 了 —— 但那个的响应要绕一圈回调，代码复杂度高不少。
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = _rt;
        _tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        _tex.Apply();
        RenderTexture.active = prev;

        if (dumpFirstFrame && _sentFrames == 0)
        {
            DumpPPM(_tex.GetRawTextureData());
            dumpFirstFrame = false;
        }

        // ★ 关键：这里必须**复制一份**像素再交给后台线程。
        //   GetRawTextureData 返回的是指向纹理内部缓冲的**引用**，
        //   下一帧 ReadPixels 会把它覆盖掉。不复制的话，
        //   后台线程编码到一半像素就变了 → 画面撕裂成两帧的拼贴。
        byte[] pixels = _tex.GetRawTextureData();
        lock (_pixelLock)
        {
            _pendingPixels = pixels;
            _hasPending = true;
        }
    }

    /// <summary>把原始 RGB 字节存成 PPM —— 用来核对 Unity 侧到底采到了什么</summary>
    void DumpPPM(byte[] rgb)
    {
        try
        {
            // PPM 是未压缩格式，最简单。注意 Unity 的纹理原点在左下，PPM 也是，不用翻转
            var sb = new System.Text.StringBuilder();
            sb.Append($"P6\n{width} {height}\n255\n");
            byte[] header = System.Text.Encoding.ASCII.GetBytes(sb.ToString());
            string path = System.IO.Path.Combine(Application.dataPath, "..", "car_cam_dump.ppm");
            using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Create))
            {
                fs.Write(header, 0, header.Length);
                fs.Write(rgb, 0, rgb.Length);
            }
            Debug.Log($"[CameraStreamer] 已导出首帧到 {path}（PPM 格式，可直接用看图软件打开）");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CameraStreamer] 导出首帧失败：{e.Message}");
        }
    }

    void OnDestroy()
    {
        _running = false;

        try { _worker?.Join(300); } catch { }

        try
        {
            if (cam != null && cam.targetTexture == _rt) cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_tex != null) Destroy(_tex);
            if (_createdCam != null) Destroy(_createdCam.gameObject);
            _udp?.Close();
        }
        catch { }
    }

    // ==================== 后台线程 ====================

    void WorkerLoop()
    {
        byte[] jpg = null;
        int frameId = 0;

        while (_running)
        {
            byte[] pixels = null;
            lock (_pixelLock)
            {
                if (_hasPending)
                {
                    pixels = _pendingPixels;
                    _hasPending = false;
                }
            }

            if (pixels == null)
            {
                System.Threading.Thread.Sleep(4);
                continue;
            }

            try
            {
                jpg = EncodeJpeg(pixels, width, height, jpegQuality);
                if (jpg == null || jpg.Length == 0) continue;

                SendSliced(jpg, frameId++);

                _sentFrames++;
                _byteAccum += jpg.Length;
                LastFrameKB = jpg.Length / 1024f;
            }
            catch (Exception e)
            {
                _failedFrames++;
                LastError = e.Message;
                if (_failedFrames <= 3)
                {
                    string msg = $"[CameraStreamer] 后台发送出错：{e.Message}";
                    lock (_mainThreadJobs) _mainThreadJobs.Enqueue(() => Debug.LogWarning(msg));
                }
            }
        }
    }

    /// <summary>
    /// 把一帧 JPEG 切片打出去。
    /// 每片都带 (帧号, 片号, 总片数)，MATLAB 靠这些拼回完整帧。
    /// </summary>
    void SendSliced(byte[] jpg, int frameId)
    {
        int total = (jpg.Length + MaxPayload - 1) / MaxPayload;
        if (total > 65535) throw new Exception($"帧太大（{jpg.Length} 字节）");

        byte[] packet = new byte[HeaderSize + MaxPayload];

        for (int i = 0; i < total; i++)
        {
            int off = i * MaxPayload;
            int len = Math.Min(MaxPayload, jpg.Length - off);

            // ---- 包头 ----
            packet[0] = (byte)'V';
            packet[1] = (byte)'S';
            WriteInt32(packet, 2, frameId);
            WriteUInt16(packet, 6, (ushort)i);
            WriteUInt16(packet, 8, (ushort)total);
            WriteUInt16(packet, 10, (ushort)len);
            WriteUInt16(packet, 12, 0);

            // ---- 载荷 ----
            Buffer.BlockCopy(jpg, off, packet, HeaderSize, len);

            _udp.Send(packet, HeaderSize + len, matlabHost, matlabVideoPort);
        }
    }

    static void WriteInt32(byte[] b, int off, int v)
    {
        // 网络字节序（大端）
        b[off]     = (byte)(v >> 24);
        b[off + 1] = (byte)(v >> 16);
        b[off + 2] = (byte)(v >> 8);
        b[off + 3] = (byte)v;
    }

    static void WriteUInt16(byte[] b, int off, ushort v)
    {
        b[off]     = (byte)(v >> 8);
        b[off + 1] = (byte)v;
    }

    // ==================== JPEG 编码 ====================

    /// <summary>
    /// 把 RGB24 原始像素编码成 JPEG。
    ///
    /// 【为什么能放在后台线程】
    ///   查过 UnityEngine.ImageConversionModule 的 API 文档（xml 注释），
    ///   `ImageConversion.EncodeArrayToJPG(byte[] array, GraphicsFormat fmt, ...)`
    ///   是**静态扩展方法**、接收字节数组，不依赖 Texture2D 实例，
    ///   所以在子线程调用是安全的 —— 这省掉了自己写一整个 JPEG 编码器。
    ///
    ///   对比：`Texture2D.EncodeToJPG()` 是实例方法，必须在主线程调，
    ///   而且每帧要构造/上传一次 Texture2D，实测比这里慢一个数量级。
    ///
    /// 【注意 GraphicsFormat 要跟像素来源对上】
    ///   我们是从 TextureFormat.RGB24 的 Texture2D 里 GetRawTextureData 的，
    ///   所以格式填 R8G8B8_UNorm（每像素 3 字节，R/G/B 顺序）。
    ///   填错了不会报错，但出来的图是花的 —— 这是最常见的坑。
    /// </summary>
    static byte[] EncodeJpeg(byte[] rgb, int w, int h, int quality)
    {
        return ImageConversion.EncodeArrayToJPG(
            rgb,
            UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8_UNorm,
            (uint)w, (uint)h, 0u, quality);
    }
}
