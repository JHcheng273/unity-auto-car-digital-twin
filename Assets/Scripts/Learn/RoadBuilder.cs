using UnityEngine;

/// <summary>
/// 从 MyWaypointPath 的路标点自动生成「看得见的路面」，可选带车道虚线。
///
/// 用法：
///   1. Hierarchy 空白处右键 → 创建空对象（Create Empty），命名 Road
///   2. 给 Road 挂上这个脚本（添加组件 Add Component → RoadBuilder）
///   3. 把挂 MyWaypointPath 的物体（Path）拖进「数据源」框
///   4. （可选）把路面材质拖进「路面材质」框，把白线材质拖进「车道线材质」框
///   5. 点组件标题右上角 ⋮ → 「生成路面」
///   6. 想改宽度/厚度，改完再点一次（会先清掉上一次生成的）
///
/// 说明：
///   - 生成出来的是压扁的 Cube，脚本会顺手删掉它们的 Collider
///     —— 车是运动学模型不需要碰撞，留着反而可能挡住前向射线。
///   - 有贴图时会按「贴图重复长度」自动平铺，避免长路段被拉伸成条纹。
///   - 车道虚线是沿中线摆的一串小扁 Cube，只在勾选后生成。
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

    [Header("路面材质（可选，留空就是默认白）")]
    public Material roadMaterial;
    [Tooltip("贴图多少米重复一次；填 0 = 不平铺（保持拉伸）")]
    public float textureTileLength = 4f;

    [Header("车道虚线（可选，要先勾上）")]
    public bool drawLaneLine = false;
    public Material laneLineMaterial;
    [Tooltip("每段虚线长（米）")]
    public float dashLength = 2f;
    [Tooltip("虚线之间的间隔（米）")]
    public float dashGap = 2f;
    [Tooltip("虚线宽度（米）")]
    public float dashWidth = 0.15f;

    [ContextMenu("生成路面")]
    public void Build()
    {
        Clear();

        if (path == null || path.Count < 2)
        {
            Debug.LogWarning("[RoadBuilder] 还没设置 Path，或者路标点少于 2 个", this);
            return;
        }

        int segCount = 0;
        int dashCount = 0;

        // 每一对相邻路标点之间铺一段
        for (int i = 0; i < path.Count; i++)
        {
            int j = path.NextIndex(i);
            if (j == i) break;                  // loop 没勾 → 到最后一个点了

            Vector3 a = path.GetPoint(i);
            Vector3 b = path.GetPoint(j);

            Vector3 dir = new Vector3(b.x - a.x, 0f, b.z - a.z);
            float len = dir.magnitude;
            if (len < 0.01f) continue;

            // 长度多给一点，拐角处重叠不留缝
            float fullLen = len + roadWidth * 0.6f;

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

            seg.transform.localScale = new Vector3(roadWidth, thickness, fullLen);

            StripCollider(seg);

            // 材质：有贴图就建一个实例，按段长设平铺次数（每段长度不同，不能共用）
            MeshRenderer mr = seg.GetComponent<MeshRenderer>();
            if (roadMaterial != null)
            {
                if (textureTileLength > 0.01f && roadMaterial.mainTexture != null)
                {
                    Material inst = new Material(roadMaterial);
                    inst.mainTextureScale = new Vector2(
                        roadWidth / textureTileLength,
                        fullLen / textureTileLength);
                    mr.sharedMaterial = inst;
                }
                else
                {
                    mr.sharedMaterial = roadMaterial;
                }
            }

            if (drawLaneLine && laneLineMaterial != null)
                dashCount += BuildDashes(i, a, dir, len);

            segCount++;
        }

        Debug.Log($"[RoadBuilder] 生成了 {segCount} 段路面、{dashCount} 段虚线（共 {path.Count} 个路标点）", this);
    }

    /// <summary>沿一段路的中线摆一串虚线</summary>
    int BuildDashes(int segIndex, Vector3 a, Vector3 dir, float len)
    {
        Vector3 d = dir.normalized;
        float step = dashLength + dashGap;
        if (step < 0.1f) return 0;

        int count = 0;
        for (float s = 0f; s < len; s += step)
        {
            float l = Mathf.Min(dashLength, len - s);
            if (l < 0.2f) break;                       // 尾巴太短就不要了

            Vector3 center = a + d * (s + l * 0.5f);

            GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = $"Lane_{segIndex}_{count}";
            dash.transform.SetParent(transform, false);

            // 路面顶面在 y = thickness，虚线再抬 0.006 避免和路面打架
            dash.transform.position = new Vector3(center.x, thickness + 0.006f, center.z);
            dash.transform.rotation = Quaternion.LookRotation(dir);
            dash.transform.localScale = new Vector3(dashWidth, 0.01f, l);

            StripCollider(dash);
            dash.GetComponent<MeshRenderer>().sharedMaterial = laneLineMaterial;

            count++;
        }
        return count;
    }

    /// <summary>清掉上一次生成的 Road_* / Lane_*，顺手销毁自己 new 出来的材质实例</summary>
    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (!child.name.StartsWith("Road_") && !child.name.StartsWith("Lane_")) continue;

            MeshRenderer mr = child.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material m = mr.sharedMaterial;
                // 只销毁自己 new 的实例，别把用户拖进来的原材质删了
                if (m != null && m != roadMaterial && m != laneLineMaterial)
                    DestroyObj(m);
            }
            DestroyObj(child);
        }
    }

    void StripCollider(GameObject go)
    {
        Collider col = go.GetComponent<Collider>();
        if (col != null) DestroyObj(col);
    }

    void DestroyObj(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
