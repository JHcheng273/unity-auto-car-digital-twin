using UnityEngine;

/// <summary>
/// 车辆执行器：只负责"把输入变成运动"。
/// 不关心输入是谁给的（键盘 / 自动驾驶员 / MATLAB 都行）。
/// </summary>
public class CarController : MonoBehaviour
{
    [Header("车辆参数")]
    public float maxSpeed = 5f;         // 最大前进速度（米/秒）
    public float maxTurnSpeed = 120f;   // 最大转向角速度（度/秒）

    public float Steering { get; private set; }   // -1 左打满 ~ +1 右打满
    public float Motor    { get; private set; }   // 0 不动 ~ 1 全速
    public bool  Braking  { get; private set; }

    /// <summary>外部用这个来开车 —— 这就是项目的接口契约</summary>
    public void SetInput(float steering, float motor, bool brake = false)
    {
        Steering = Mathf.Clamp(steering, -1f, 1f);
        Motor    = Mathf.Clamp01(motor);
        Braking  = brake;
    }

    void Update()
    {
        float motor = Braking ? 0f : Motor;

        transform.Rotate(0f, Steering * maxTurnSpeed * Time.deltaTime, 0f);
        transform.Translate(0f, 0f, motor * maxSpeed * Time.deltaTime);
    }
}

