using UnityEngine;

/// <summary>
/// 车辆执行器：只负责"把输入变成运动"。
/// 不关心输入是谁给的（键盘 / AutoDrive / MATLAB 都行）。
///
/// 【2026-10-09 改动】加了惯性。
///   原来：motor=1 → 车速瞬间变 5 m/s，仪表盘上是一条方波，很假。
///   现在：车速按加速度往上爬、按减速度往下掉，刹车时才会真的"刹住"。
///   不想用惯性：把 useInertia 取消勾选，就回到原来的瞬间到速。
/// </summary>
public class CarController : MonoBehaviour
{
    [Header("车辆参数")]
    [Tooltip("最大前进速度（米/秒）")]
    public float maxSpeed = 5f;
    [Tooltip("最大转向角速度（度/秒）")]
    public float maxTurnSpeed = 120f;

    [Header("加减速（有惯性才像真车）")]
    [Tooltip("取消勾选 = 瞬间达到目标速度（原来的行为）")]
    public bool useInertia = true;
    [Tooltip("踩油门时的加速度（米/秒²）")]
    public float accelRate = 6f;
    [Tooltip("踩刹车时的减速度（米/秒²），比加速大才对")]
    public float brakeRate = 12f;
    [Tooltip("松开油门的自然滑行减速度（米/秒²）")]
    public float coastRate = 2.5f;

    public float Steering { get; private set; }   // -1 左打满 ~ +1 右打满
    public float Motor    { get; private set; }   // 0 不动 ~ 1 全速
    public bool  Braking  { get; private set; }

    /// <summary>
    /// 当前真实车速（米/秒）。
    /// 仪表盘、MATLAB 的速度闭环都用这个，不要再用 Motor*maxSpeed 估算了
    /// —— 那是"目标速度"，不是"实际速度"，减速过程中两者差很多。
    /// </summary>
    public float CurrentSpeed { get; private set; }

    /// <summary>外部用这个来开车 —— 这就是项目的接口契约</summary>
    public void SetInput(float steering, float motor, bool brake = false)
    {
        Steering = Mathf.Clamp(steering, -1f, 1f);
        Motor    = Mathf.Clamp01(motor);
        Braking  = brake;
    }

    void Update()
    {
        float target = Braking ? 0f : Motor * maxSpeed;

        if (useInertia)
        {
            float rate;
            if (target > CurrentSpeed) rate = accelRate;        // 踩油门
            else if (Braking) rate = brakeRate;                 // 踩刹车
            else rate = coastRate;                              // 滑行

            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, target, rate * Time.deltaTime);
        }
        else
        {
            CurrentSpeed = target;
        }

        transform.Rotate(0f, Steering * maxTurnSpeed * Time.deltaTime, 0f);
        transform.Translate(0f, 0f, CurrentSpeed * Time.deltaTime);
    }
}
