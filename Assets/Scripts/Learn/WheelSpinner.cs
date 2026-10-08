using UnityEngine;

/// <summary>
/// 让轮子看起来在滚。
///
/// 为什么需要它：车是运动学模型（CarController 直接 transform.Translate），
/// 轮子只是挂上去的外观零件，不会自己转 —— 不转的话一眼就看出是假的。
///
/// 转速是算出来的，不是猜的：角速度 = 线速度 / 半径。
/// 所以车跑多快，轮子就滚多快，不会出现「车慢轮子飞快」的穿帮。
///
/// 挂载位置：4 个 Wheel_* 上（CarBuilder 会自动挂）。
/// 注意轮子已经 Rotation Z = 90°，本地 Y 轴正好是真正的转轴，
/// 所以这里必须用 Space.Self 绕本地 Y 转。
/// </summary>
public class WheelSpinner : MonoBehaviour
{
    [Tooltip("轮子半径（米），用来算转速")]
    public float radius = 0.30f;

    [Tooltip("转反了就改成 -1")]
    public float spinSign = 1f;

    CarController car;

    void OnEnable()
    {
        FindCar();
    }

    void FindCar()
    {
        car = GetComponentInParent<CarController>();
    }

    void Update()
    {
        // 编辑器里不转（免得摆场景时轮子乱跑）
        if (!Application.isPlaying) return;

        if (car == null)
        {
            FindCar();
            if (car == null) return;
        }

        float v = car.Motor * car.maxSpeed;                       // 当前线速度 米/秒
        float deg = v / Mathf.Max(radius, 0.01f)                  // 角速度 弧度/秒
                    * Mathf.Rad2Deg * Time.deltaTime;

        transform.Rotate(0f, deg * spinSign, 0f, Space.Self);
    }
}
