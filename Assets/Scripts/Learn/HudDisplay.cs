using UnityEngine;
using UnityEngine.UI;

public class HudDisplay : MonoBehaviour
{
    [Header("要显示的文字（把 HudText 拖进来）")]
    public Text label;

    AutoDrive drive;
    CarController car;
    MyFrontDetector detector;

    void Start()
    {
        drive    = GetComponent<AutoDrive>();
        car      = GetComponent<CarController>();
        detector = GetComponent<MyFrontDetector>();
    }

    void Update()
    {
        if (label == null || drive == null || car == null) return;
        if (drive.path == null) return;

        float speed = car.Motor * car.maxSpeed;

        string front;
        if (detector == null || detector.ObstacleName == "")
            front = "无";
        else
            front = $"{detector.ObstacleName}   {detector.ObstacleDistance:F1} m";

        label.text =
            $"状态     {drive.CurrentState}\n" +
            $"目标点   WP{drive.CurrentWaypointIndex} / 共 {drive.path.Count} 个\n" +
            $"圈数     {drive.LapCount} / {drive.totalLaps}\n" +
            $"速度     {speed:F2} m/s\n" +
            $"油门     {car.Motor:F2}      转向 {car.Steering:F2}\n" +
            $"前方     {front}\n" +
            $"位置     ({transform.position.x:F1}, {transform.position.z:F1})";
    }
}

