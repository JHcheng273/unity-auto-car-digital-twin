using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把光板 Cube 变成一辆像样的车。
///
/// 用法：
///   1. 层级（Hierarchy）里选中那个挂着 AutoDrive / CarController 的 Cube
///   2. 添加组件（Add Component）→ CarBuilder
///   3. 点组件标题右上角 ⋮ → 「造一辆车」
///
/// 重要：这个脚本只加「看得见的外观」，不动 AutoDrive / CarController /
///       MyFrontDetector / MatlabBridge —— 车的行驶逻辑一点没变。
///
/// 注意：所有零件都挂在车的根节点下、彼此平级（不是父子嵌套）。
///       父子嵌套的话，父物体的 Scale 会乘到子物体上，轮子会被压成椭圆。
/// </summary>
[ExecuteInEditMode]
public class CarBuilder : MonoBehaviour
{
    [Header("车身尺寸（米）")]
    public float bodyWidth = 1.60f;
    public float bodyHeight = 0.50f;
    public float bodyLength = 3.20f;
    [Tooltip("底盘离地高度")]
    public float clearance = 0.30f;

    [Header("颜色")]
    public Color bodyColor = new Color(0.10f, 0.28f, 0.62f);
    public Color roofColor = new Color(0.07f, 0.20f, 0.46f);
    public Color glassColor = new Color(0.10f, 0.13f, 0.18f, 0.75f);
    public Color tireColor = new Color(0.055f, 0.055f, 0.065f);
    public Color rimColor = new Color(0.72f, 0.74f, 0.78f);
    public Color headLightColor = new Color(1.00f, 0.96f, 0.84f);
    public Color tailLightColor = new Color(1.00f, 0.16f, 0.10f);

    [Header("轮子")]
    public float wheelRadius = 0.30f;
    public float wheelThickness = 0.22f;
    [Tooltip("轮子前后位置（占车长的比例，0.5 = 最两端）")]
    [Range(0.2f, 0.5f)]
    public float wheelAxle = 0.34f;
    [Tooltip("轮子转动的方向，反了就改成 -1")]
    public float spinSign = 1f;

    [Header("收尾")]
    [Tooltip("造完把原来那个光板 Cube 的渲染器关掉")]
    public bool hideOriginalMesh = true;

    // 自己造出来的材质，重新生成时要销毁，不然会一直泄漏
    [HideInInspector] public List<Material> generatedMaterials = new List<Material>();

    static readonly string[] WheelNames = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
    static readonly float[] WheelSideX = { -1f, 1f, -1f, 1f };
    static readonly float[] WheelSideZ = { 1f, 1f, -1f, -1f };

    [ContextMenu("造一辆车")]
    public void Build()
    {
        ClearOld();

        if (transform.localScale != Vector3.one)
        {
            Debug.LogWarning("[CarBuilder] 车的 Scale 不是 (1,1,1)，已自动重置 —— 否则车身尺寸会全部失真。", this);
            transform.localScale = Vector3.one;
        }

        // ---------- 1. 材质 ----------
        Material mBody = Track(MaterialLib.Solid("CarBodyMat", bodyColor, 0.55f, 0.75f));
        Material mRoof = Track(MaterialLib.Solid("CarRoofMat", roofColor, 0.55f, 0.75f));
        Material mGlass = Track(MaterialLib.Glass("CarGlassMat", glassColor));
        Material mTire = Track(MaterialLib.Solid("CarTireMat", tireColor, 0.00f, 0.22f));
        Material mRim = Track(MaterialLib.Solid("CarRimMat", rimColor, 0.85f, 0.80f));
        Material mHead = Track(MaterialLib.Emissive("CarHeadLightMat", headLightColor, 2.5f));
        Material mTail = Track(MaterialLib.Emissive("CarTailLightMat", tailLightColor, 2.0f));

        // ---------- 2. 把原来那个光板藏起来 ----------
        if (hideOriginalMesh)
        {
            MeshRenderer own = GetComponent<MeshRenderer>();
            if (own != null) own.enabled = false;
        }

        // ---------- 3. 车身 ----------
        float topOfBody = clearance + bodyHeight;
        Cube("Body", transform,
            new Vector3(0f, clearance + bodyHeight * 0.5f, 0f),
            Quaternion.identity,
            new Vector3(bodyWidth, bodyHeight, bodyLength),
            mBody);

        // 前后各收一点，露出保险杠的感觉
        Cube("Bumper_F", transform,
            new Vector3(0f, clearance + bodyHeight * 0.28f, bodyLength * 0.5f + 0.03f),
            Quaternion.identity,
            new Vector3(bodyWidth * 0.98f, bodyHeight * 0.34f, 0.10f),
            mRoof);

        Cube("Bumper_R", transform,
            new Vector3(0f, clearance + bodyHeight * 0.28f, -bodyLength * 0.5f - 0.03f),
            Quaternion.identity,
            new Vector3(bodyWidth * 0.98f, bodyHeight * 0.34f, 0.10f),
            mRoof);

        // ---------- 4. 玻璃座舱 + 车顶 ----------
        float cabinH = 0.42f;
        float cabinZ = -bodyLength * 0.05f;

        Cube("Cabin", transform,
            new Vector3(0f, topOfBody + cabinH * 0.5f, cabinZ),
            Quaternion.identity,
            new Vector3(bodyWidth * 0.88f, cabinH, bodyLength * 0.52f),
            mGlass);

        float roofH = 0.10f;
        Cube("Roof", transform,
            new Vector3(0f, topOfBody + cabinH + roofH * 0.5f, cabinZ),
            Quaternion.identity,
            new Vector3(bodyWidth * 0.94f, roofH, bodyLength * 0.56f),
            mRoof);

        // ---------- 5. 四个轮子（轮胎 + 轮毂，都是平级） ----------
        float wx = bodyWidth * 0.5f - 0.02f;
        float wz = bodyLength * wheelAxle;
        Quaternion wheelRot = Quaternion.Euler(0f, 0f, 90f);   // 圆柱轴从 Y 掰到 X

        for (int i = 0; i < 4; i++)
        {
            Vector3 p = new Vector3(WheelSideX[i] * wx, wheelRadius, WheelSideZ[i] * wz);

            // Cylinder 原始尺寸：半径 0.5、高 2（y 从 -1 到 1）
            GameObject tire = Cylinder(WheelNames[i], transform, p, wheelRot,
                new Vector3(wheelRadius * 2f, wheelThickness * 0.5f, wheelRadius * 2f),
                mTire);

            // 轮毂做厚一点点，从轮胎两侧各露出一小圈
            Cylinder("Rim_" + WheelNames[i].Substring(6), transform, p, wheelRot,
                new Vector3(wheelRadius * 1.20f, wheelThickness * 0.64f, wheelRadius * 1.20f),
                mRim);

            // 让轮子真的会转
            WheelSpinner spin = tire.GetComponent<WheelSpinner>();
            if (spin == null) spin = tire.AddComponent<WheelSpinner>();
            spin.radius = wheelRadius;
            spin.spinSign = spinSign;
        }

        // ---------- 6. 车灯 ----------
        float lightY = clearance + bodyHeight * 0.75f;

        for (int i = 0; i < 2; i++)
        {
            float sx = i == 0 ? -1f : 1f;
            string tag = i == 0 ? "L" : "R";

            Sphere("HeadLight_" + tag, transform,
                new Vector3(sx * bodyWidth * 0.30f, lightY, bodyLength * 0.5f + 0.03f),
                Quaternion.identity,
                Vector3.one * 0.26f,
                mHead);

            Cube("TailLight_" + tag, transform,
                new Vector3(sx * bodyWidth * 0.32f, lightY, -bodyLength * 0.5f - 0.03f),
                Quaternion.identity,
                new Vector3(0.34f, 0.13f, 0.08f),
                mTail);
        }

        Debug.Log($"[CarBuilder] 造好了：车长 {bodyLength} 米、宽 {bodyWidth} 米，4 个轮子半径 {wheelRadius} 米", this);
    }

    // ==================== 工具 ====================

    GameObject Cube(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        return Make(PrimitiveType.Cube, name, parent, pos, rot, scale, mat);
    }

    GameObject Cylinder(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        return Make(PrimitiveType.Cylinder, name, parent, pos, rot, scale, mat);
    }

    GameObject Sphere(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        return Make(PrimitiveType.Sphere, name, parent, pos, rot, scale, mat);
    }

    GameObject Make(PrimitiveType type, string name, Transform parent, Vector3 pos,
                    Quaternion rot, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;

        // 外观零件不需要碰撞体：车是运动学模型，留着反而可能挡前向射线
        Collider col = go.GetComponent<Collider>();
        if (col != null) DestroyObj(col);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null && mat != null) mr.sharedMaterial = mat;

        return go;
    }

    void ClearOld()
    {
        for (int i = 0; i < generatedMaterials.Count; i++)
            if (generatedMaterials[i] != null) DestroyObj(generatedMaterials[i]);
        generatedMaterials.Clear();

        // 上一次造出来的零件：从后往前删，避免索引错位
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (IsCarPart(child.name)) DestroyObj(child);
        }
    }

    static bool IsCarPart(string n)
    {
        return n == "Body" || n == "Cabin" || n == "Roof"
            || n.StartsWith("Bumper_") || n.StartsWith("HeadLight_") || n.StartsWith("TailLight_")
            || n.StartsWith("Wheel_") || n.StartsWith("Rim_");
    }

    Material Track(Material m)
    {
        if (m != null) generatedMaterials.Add(m);
        return m;
    }

    void DestroyObj(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
