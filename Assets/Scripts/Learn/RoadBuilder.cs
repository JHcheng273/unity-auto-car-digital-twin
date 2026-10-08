using UnityEngine;

/// <summary>
/// 从 MyWaypointPath 的路标点自动生成「看得见的路面」。
///
/// 用法：
///   1. Hierarchy 空白处右键 → Create Empty，命名 Road
///   2. 给 Road 挂上这个脚本（Add Component → RoadBuilder）
///   3. 把挂 MyWaypointPath 的物体（Path）拖进 Path 框
///   4. 点组件标题右上角 ⋮ → 「生成路面」
///   5. 想改宽度/厚度，改完再点一次（会先清掉上一次生成的）
///
/// 说明：生成出来的是压扁的 Cube，脚本会顺手删掉它们的 Collider
///      —— 车是运动学模型不需要碰撞，留着反而可能挡住前向射线。
/// </summary>
[ExecuteInEditMode]
public class RoadBuilder : MonoBehaviour
{
    [Header("数据源（把挂 MyWaypointPath 的物体拖进来）")]
    public MyWaypointPath path;

    [Header("路面尺寸")]
    [Tooltip("路面宽度（米）")]
    public float roadWidth = 3f;
    [Tooltip("路面厚度（米）")]
    public float thickness = 0.05f;

    [Header("材质（可选，留空就是默认白）")]
    public Material roadMaterial;

    [ContextMenu("生成路面")]
    public void Build()
    {
        // 1. 先清掉上一次生成的
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child.name.StartsWith("Road_"))
            {
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        if (path == null || path.Count < 2)
        {
            Debug.LogWarning("[RoadBuilder] 还没设置 Path，或者路标点少于 2 个", this);
            return;
        }

        int segCount = 0;

        // 2. 每一对相邻路标点之间铺一段
        for (int i = 0; i < path.Count; i++)
        {
            int j = path.NextIndex(i);
            if (j == i) break;                  // loop 没勾 → 到最后一个点了

            Vector3 a = path.GetPoint(i);
            Vector3 b = path.GetPoint(j);

            Vector3 dir = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float len = dir.magnitude;
            if (len < 0.01f) continue;

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = $"Road_{i}";
            seg.transform.SetParent(transform, false);

            // 位置：两端中点，抬高半个厚度避免和地面 z-fighting
            seg.transform.position = new Vector3(
                (a.x + b.x) * 0.5f,
                thickness * 0.5f,
                (a.z + b.z) * 0.5f);

            // 朝向：让立方体的 +Z（forward）对准这一段的走向
            seg.transform.rotation = Quaternion.LookRotation(dir);

            // 尺寸：宽 × 厚 × 长（长度多给一点，拐角处重叠不留缝）
            seg.transform.localScale = new Vector3(
                roadWidth,
                thickness,
                len + roadWidth * 0.6f);

            // 路面不需要碰撞体
            Collider col = seg.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            if (roadMaterial != null)
                seg.GetComponent<MeshRenderer>().sharedMaterial = roadMaterial;

            segCount++;
        }

        Debug.Log($"[RoadBuilder] 生成了 {segCount} 段路面（共 {path.Count} 个路标点）", this);
    }
}
