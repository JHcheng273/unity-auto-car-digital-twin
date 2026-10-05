using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    [Header("跟着谁（把车拖进来）")]
    public Transform target;

    [Header("摄像机相对车的位置")]
    public Vector3 offset = new Vector3(0f, 6f, -10f);

    void LateUpdate()
    {
        if (target == null) return;

        transform.position = target.position + offset;
        transform.LookAt(target);
    }
}

