using UnityEngine;

/// <summary>
/// 材质工厂：统一在这里创建 Standard 材质（纯色 / 带贴图 / 自发光 / 半透明玻璃）。
///
/// 为什么单独抽出来：CarBuilder 和 SceneDresser 都要造一堆材质，
/// 属性名写错一次（比如把 _Glossiness 写成 _Smoothness）就会静默失效，
/// 集中在一处改最省事。
///
/// 贴图放在 Assets/Resources/Textures/ 下，文件名就是 LoadTexture 的参数。
/// </summary>
public static class MaterialLib
{
    static Shader _std;

    public static Shader StdShader
    {
        get
        {
            if (_std == null) _std = Shader.Find("Standard");
            if (_std == null) _std = Shader.Find("Legacy Shaders/Diffuse");
            return _std;
        }
    }

    /// <summary>加载 Assets/Resources/Textures/&lt;name&gt;.png</summary>
    public static Texture2D LoadTexture(string name)
    {
#if UNITY_EDITOR
        // 编辑器里走 AssetDatabase 最稳：不管 Unity 把 PNG 导成 Texture 还是 Sprite 都能拿到
        Texture2D byPath = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Resources/Textures/" + name + ".png");
        if (byPath != null) return byPath;
#endif
        Texture2D t = Resources.Load<Texture2D>("Textures/" + name);
        if (t != null) return t;

        Sprite s = Resources.Load<Sprite>("Textures/" + name);
        if (s != null) return s.texture;

        return null;
    }

    /// <summary>纯色材质。metallic 金属度 0~1，smoothness 光滑度 0~1</summary>
    public static Material Solid(string name, Color color, float metallic = 0f, float smoothness = 0.5f)
    {
        Material m = new Material(StdShader);
        m.name = name;
        m.color = color;
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smoothness);
        return m;
    }

    /// <summary>带贴图的材质。tint 建议接近白色，否则会把贴图染色压暗</summary>
    public static Material Textured(string name, string texName, Color tint,
                                    Vector2 tiling, float smoothness = 0.3f, float metallic = 0f)
    {
        Material m = new Material(StdShader);
        m.name = name;
        m.color = tint;

        Texture2D t = LoadTexture(texName);
        if (t != null)
        {
            m.mainTexture = t;
            m.mainTextureScale = tiling;
        }
        else
        {
            Debug.LogWarning("[MaterialLib] 找不到贴图 Textures/" + texName + "，已退化成纯色");
        }

        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smoothness);
        return m;
    }

    /// <summary>自发光材质（车灯、路灯头）</summary>
    public static Material Emissive(string name, Color color, float intensity)
    {
        Material m = Solid(name, color, 0f, 0.75f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(
            color.r * intensity, color.g * intensity, color.b * intensity, 1f));
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return m;
    }

    /// <summary>半透明玻璃。color 的 alpha 要小于 1 才透明</summary>
    public static Material Glass(string name, Color color)
    {
        Material m = Solid(name, color, 0.15f, 0.92f);

        // Standard shader 切到 Transparent 模式要手动设这一串
        m.SetFloat("_Mode", 3f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;

        return m;
    }
}
