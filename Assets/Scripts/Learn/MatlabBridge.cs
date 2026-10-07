using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Unity ↔ MATLAB 的 UDP 桥（数字孪生通信层）—— Learn 版
///
/// 和仓库里 Assets/Scripts/Shared/MatlabUdpBridge.cs 是同一套协议，
/// 区别只有一处：数据源从"正式版脚本"（FrontCarDetector / WaypointFollower）
/// 换成了你手写的 Learn 版（MyFrontDetector / AutoDrive）。
///
/// 挂法：挂在 Cube（车）上。detector / drive 留空会自动从自己身上找。
///
/// 关键设计：
///   1. 收数据放在后台线程，主线程只从队列里取 —— Unity 的 API 不能在子线程调用
///   2. 发送频率默认 20Hz，与帧率解耦，别每帧发
///   3. MATLAB 掉线超过 timeout 秒，IsConnected 变 false，控制逻辑要降级回本地
/// </summary>

// ========================= 数据契约 =========================
// 改这两个类的字段名，必须同时改 MATLAB 侧的 unity_bridge.m，否则 JSON 对不上

[Serializable]
public class UnityStatePacket
{
    public float t;          // 仿真时间（秒）
    public float x, y, z;    // 位置（米）
    public float yaw;        // 朝向（度）
    public float speed;      // 速度（米/秒）
    public float steer;      // 当前转向 -1(左满) ~ +1(右满)
    public float front_dist; // 前方障碍距离（米），没检测到 = 量程值
    public int wp;           // 当前 waypoint 下标
    public int lap;          // 圈数
    public bool has_front;   // 前方是否有障碍
}

[Serializable]
public class MatlabCommandPacket
{
    public float v_des;      // 期望速度（米/秒）
    public float steer_des;  // 期望转向；填 999 表示"转向交给 Unity 自己算"
    public bool brake;       // 强制刹车
    public int seq;          // 序号，用来看丢包
}

// ========================= 桥接本体 =========================

public class MatlabBridge : MonoBehaviour
{
    [Header("网络")]
    [Tooltip("MATLAB 所在机器；同一台电脑就填 127.0.0.1")]
    public string matlabHost = "127.0.0.1";
    [Tooltip("MATLAB 监听端口（Unity 往这发）")]
    public int matlabListenPort = 5005;
    [Tooltip("Unity 监听端口（MATLAB 往这发）")]
    public int unityListenPort = 5006;
    [Tooltip("发送频率。20 够用，别超过 50")]
    public int sendHz = 20;
    [Tooltip("超过这么久没收到 MATLAB 的消息就认为掉线")]
    public float timeout = 1.5f;

    [Header("数据源（留空会自动从自己身上找）")]
    public Transform car;
    public MyFrontDetector detector;
    public AutoDrive drive;

    [Header("调试")]
    [Tooltip("勾选后每秒在 Console 打印收发包数，用来确认通信通没通")]
    public bool logStats = true;

    [Header("开关")]
    [Tooltip("关掉就完全不联网，纯本地跑，方便对比")]
    public bool enableNetwork = true;
    [Tooltip("勾选后，MATLAB 给的 v_des 才真正生效（AutoDrive 要读它才行）")]
    public bool acceptMatlabCommand = false;

    // ===== 对外接口 =====
    public bool IsConnected { get; private set; }
    public UnityStatePacket LastSent { get; private set; } = new UnityStatePacket();
    public MatlabCommandPacket Command { get; private set; } = new MatlabCommandPacket();
    public int PacketsReceived { get; private set; }

    UdpClient _sendClient;
    UdpClient _recvClient;
    Thread _recvThread;
    readonly ConcurrentQueue<string> _inbox = new ConcurrentQueue<string>();
    float _sendTimer;
    float _lastRecvTime;
    float _lastStatsLog;
    int _sentCount;
    bool _running;

    void Start()
    {
        if (!enableNetwork) { Debug.Log("[MatlabBridge] 未启用网络，纯本地模式"); return; }

        if (car == null) car = transform;
        if (detector == null) detector = GetComponent<MyFrontDetector>();
        if (drive == null) drive = GetComponent<AutoDrive>();

        try
        {
            _sendClient = new UdpClient();
            _recvClient = new UdpClient(unityListenPort);
            _recvClient.Client.ReceiveTimeout = 200;
            _running = true;
            _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
            _recvThread.Start();
            Debug.Log($"[MatlabBridge] 已启动：发往 {matlabHost}:{matlabListenPort}，监听 {unityListenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MatlabBridge] 启动失败（端口被占？）：{e.Message}");
            enableNetwork = false;
        }
    }

    void ReceiveLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _recvClient.Receive(ref remote);
                _inbox.Enqueue(Encoding.UTF8.GetString(data));
            }
            catch (SocketException)
            {
                // 超时而已，继续
            }
            catch (Exception)
            {
                if (_running) Thread.Sleep(10);
            }
        }
    }

    void Update()
    {
        if (!enableNetwork) return;

        // 1. 主线程消费队列（子线程不能直接碰 Unity 对象）
        while (_inbox.TryDequeue(out string json))
        {
            try
            {
                var cmd = JsonUtility.FromJson<MatlabCommandPacket>(json);
                if (cmd != null)
                {
                    Command = cmd;
                    PacketsReceived++;
                    _lastRecvTime = Time.time;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MatlabBridge] 解析失败：{json}  ({e.Message})");
            }
        }

        IsConnected = (Time.time - _lastRecvTime) < timeout;

        // 2. 按固定频率上行状态
        _sendTimer += Time.deltaTime;
        float interval = sendHz > 0 ? 1f / sendHz : 0.05f;
        if (_sendTimer >= interval)
        {
            _sendTimer = 0f;
            SendState();
        }

        // 3. 调试统计：每秒报一次收发情况，排查通信问题全靠它
        if (logStats && Time.time - _lastStatsLog > 1f)
        {
            _lastStatsLog = Time.time;
            Debug.Log($"[MatlabBridge] 已发 {_sentCount} 包 / 已收 {PacketsReceived} 包，" +
                      $"MATLAB 连接 = {(IsConnected ? "是" : "否")}");
        }
    }

    void SendState()
    {
        Transform self = car != null ? car : transform;

        var p = new UnityStatePacket
        {
            t = Time.time,
            x = Round(self.position.x),
            y = Round(self.position.y),
            z = Round(self.position.z),
            yaw = Round(self.eulerAngles.y),
            speed = Round(GetSpeed(self)),
            steer = drive != null ? Round(drive.Steer) : 0f,
            front_dist = detector != null ? Round(detector.ObstacleDistance) : -1f,
            wp = drive != null ? drive.CurrentWaypointIndex : -1,
            lap = drive != null ? drive.LapCount : 0,
            has_front = detector != null && detector.HasObstacle
        };
        LastSent = p;

        string json = JsonUtility.ToJson(p);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        try
        {
            _sendClient.Send(bytes, bytes.Length, matlabHost, matlabListenPort);
            _sentCount++;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MatlabBridge] 发送失败：{e.Message}");
        }
    }

    float _lastSampleTime;
    Vector3 _lastPos;
    bool _hasSample;

    /// <summary>
    /// 速度来源：有 Rigidbody 就用物理速度；没有（运动学小车）就用两次采样之间的位置差分。
    /// 注意：本项目的小车是用 transform.Translate 移动的，没有 Rigidbody，
    /// 所以必须走差分分支 —— 否则速度永远是 0，MATLAB 那边会画出一条平线。
    /// </summary>
    float GetSpeed(Transform self)
    {
        float now = Time.time;
        if (!_hasSample)
        {
            _hasSample = true;
            _lastSampleTime = now;
            _lastPos = self.position;
            return 0f;
        }

        float dt = now - _lastSampleTime;
        _lastSampleTime = now;
        if (dt <= 0.0001f) return 0f;

        float v = (self.position - _lastPos).magnitude / dt;
        _lastPos = self.position;
        return v;
    }

    /// <summary>保证 JSON 里不出现 NaN / Infinity（MATLAB 的 jsondecode 会报错）</summary>
    static float Round(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v)) return -1f;
        return (float)Math.Round(v, 3);
    }

    /// <summary>拿到"该用哪个速度"，MATLAB 掉线就返回 -1 表示听本地的</summary>
    public float GetDesiredSpeed()
    {
        if (!enableNetwork || !acceptMatlabCommand || !IsConnected) return -1f;
        return Command.v_des;
    }

    public bool GetBrakeCmd()
    {
        return enableNetwork && acceptMatlabCommand && IsConnected && Command.brake;
    }

    void OnDestroy()
    {
        _running = false;
        try { _recvClient?.Close(); } catch { }
        try { _sendClient?.Close(); } catch { }
    }

    void OnApplicationQuit() => OnDestroy();
}
