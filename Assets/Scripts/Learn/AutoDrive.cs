using UnityEngine;

public class AutoDrive : MonoBehaviour
{
    [Header("要去哪")]
    public Transform target;

    [Header("转向")]
    public float fullSteerAngle = 45f;
    public float steerRate = 0.5f;

    [Header("到达判定")]
    public float arrivalRadius = 2f;

    // 同一个物体上的车辆执行器
    CarController car;

    public float Throttle { get; private set; }
    public float Steer    { get; private set; }

    float _targetSteer;

    void Start()
    {
        // ★ 关键一句：从我身上找到 CarController
        car = GetComponent<CarController>();

        if (car == null)
            Debug.LogError("[AutoDrive] 这个物体上没有 CarController！请先 Add Component。", this);
    }

    void Update()
    {
        if (car == null || target == null) return;

        Vector3 local = transform.InverseTransformPoint(target.position);
        float angleDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

        _targetSteer = Mathf.Clamp(angleDeg / fullSteerAngle, -1f, 1f);
        Steer = Mathf.MoveTowards(Steer, _targetSteer, steerRate * Time.deltaTime);

        float distance = local.magnitude;
        Throttle = distance < arrivalRadius ? 0f : 1f;

        // ★ 不再自己移动！把两个数字交给 CarController
        car.SetInput(Steer, Throttle);

        if (Time.frameCount % 30 == 0)
            Debug.Log($"偏角 {angleDeg:F1}° | 想打 {_targetSteer:F2} | 实际 {Steer:F2}");
    }

    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position);
        Gizmos.DrawWireSphere(target.position, arrivalRadius);
    }
}

