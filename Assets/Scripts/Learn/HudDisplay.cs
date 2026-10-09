using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 车上的简易 HUD（Game 视图左上角那几行字）。
/// 2026-10-09 改动：速度改用 car.CurrentSpeed（真实车速），并加了「控制来源」和「刹车」两行
/// —— 这样你不看 MATLAB 也知道现在是谁在开车。
/// </summary>
public class HudDisplay : MonoBehaviour
{
    [Header("要显示的文字（把 HudText 拖进来）")]
    public Text label;

    AutoDrive drive;
    CarController car;
    MyFrontDetector detector;
    MatlabBridge bridge;

    void Start()
    {
        drive    = GetComponent<AutoDrive>();
        car      = GetComponent<CarController>();
        detector = GetComponent<MyFrontDetector>();
        bridge   = GetComponent<MatlabBridge>();
    }

    void Update()
    {
        if (label == null || drive == null || car == null) return;
        if (drive.path == null) return;

        float speed = car.CurrentSpeed;      // 真实车速，不是 Motor*maxSpeed

        string front;
        if (detector == null || detector.ObstacleName == "")
            front = "无";
        else
            front = $"{detector.ObstacleName}   {detector.ObstacleDistance:F1} m";

        label.text =
            $"状态     {drive.CurrentState}\n" +
            $"控制     {ControlSource()}\n" +
            $"目标点   WP{drive.CurrentWaypointIndex} / 共 {drive.path.Count} 个\n" +
            $"圈数     {drive.LapCount} / {drive.totalLaps}\n" +
            $"速度     {speed:F2} m/s  ({speed * 3.6f:F1} km/h)\n" +
            $"油门     {car.Motor:F2}      转向 {car.Steering:F2}\n" +
            $"刹车     {(car.Braking ? "是" : "否")}\n" +
            $"前方     {front}\n" +
            $"位置     ({transform.position.x:F1}, {transform.position.z:F1})";
    }

    /// <summary>现在到底是谁在开车 —— 排查「为什么车不动」时第一眼看这行</summary>
    string ControlSource()
    {
        if (bridge == null) return "本地（没挂 MatlabBridge）";
        if (!bridge.enableNetwork) return "本地（网络已关）";
        if (!bridge.acceptMatlabCommand) return "本地（未勾选接受指令）";
        if (!bridge.IsConnected) return "本地（MATLAB 掉线）";
        return bridge.IsMatlabDriving ? "MATLAB 接管" : "本地（MATLAB 未接管）";
    }
}
