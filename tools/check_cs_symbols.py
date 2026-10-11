"""
C# 符号自检（精简版）
====================
【为什么需要这个】
  check_cs_syntax.py 只查括号平衡，抓不到"符号不存在"这类错。实测吃过一次：
      CameraStreamer.cs(169,13): error CS0103:
      The name 'SetupObserverCamera' does not exist in the current context
  Unity 编译要等窗口获得焦点，反馈链路很长，所以离线提前筛一遍有价值。

【★ 为什么砍到只剩一条检查（血泪教训）】
  第一版还想做"跨文件属性引用检查"，结果 70 处报告里 69 处是误报：
    · `StringBuilder.Append(` / `Debug.DrawLine(` / `List.RemoveAt(` ——
      这些是 .NET/Unity API，正则认不出
    · `RoadNetwork.cs` 里的 `pb` 是局部 `Vector3`，被全局映射误判成 `ProjectionBeam`
  **噪音这么大的检查器比没有更糟** —— 会训练人忽略它的输出。
  所以现在只保留唯一可靠的一条：**同一文件内，被调用的自定义方法必须有定义**。

【它查什么】
  找出 `Xxx(` 形式、首字母大写、且**不在 API 白名单里**的调用，
  看同一个文件里有没有 `... Xxx(` 的定义。
  这是纯文本匹配，可靠度高、误报低。

【它不查什么（明确说明，别指望）】
  · 参数个数/类型对不对
  · 跨文件引用的成员存在不存在
  · 泛型、重载、继承来的成员
  真正的保证**永远是 Unity 编译通过**，这个脚本只是提前缩小排查范围。
"""

import re
import os

PROJ = r"E:\My project\Assets"
FILES = [
    r"Scripts\Learn\CameraStreamer.cs",
    r"Scripts\Learn\ProjectionBeam.cs",
    r"Editor\ProjectionSetup.cs",
    r"Scripts\Learn\AutoDrive.cs",
    r"Scripts\Learn\AvoidanceDriver.cs",
    r"Scripts\Learn\RoadNetwork.cs",
    r"Scripts\Learn\MouseTargetSelector.cs",
    r"Editor\RoadNetworkSetup.cs",
]

# API 白名单：这些是 .NET / Unity 提供的，不算"自定义方法"
API = set("""
Add AddRange Append AppendLine AppendFormat Insert Remove RemoveAt RemoveAll
Clear Contains IndexOf LastIndexOf Sort Reverse ToArray ToList Find FindAll
Exists ForEach Trim TrimStart TrimEnd Split Substring Replace StartsWith
EndsWith ToUpper ToLower Format Join Concat IsNullOrEmpty IsNullOrWhiteSpace
Push Pop Peek Enqueue Dequeue ToString GetHashCode Equals CompareTo Clone
GetType Cast Select Where OrderBy OrderByDescending ToDictionary Sum Count
Any All First FirstOrDefault Last Single Min Max Average
Log LogWarning LogError LogFormat Assert
Destroy DestroyImmediate Instantiate CreatePrimitive Find FindObjectOfType
GetComponent GetComponentInChildren GetComponents GetComponentsInChildren
AddComponent SetActive SetParent SetDirty RecordObject RegisterCreatedObjectUndo
DestroyObjectImmediate AddObjectToAsset SaveAssets Refresh LoadAssetAtPath
CreateAsset FindAssets DisplayDialog MarkSceneDirty
DrawLine DrawWireSphere DrawCube DrawRay DrawIcon Gizmos
Render ReadPixels Apply GetRawTextureData Create Release SetVertices SetUVs
SetTriangles SetNormals RecalculateNormals RecalculateBounds SetColor SetFloat
SetInt SetTexture SetVector EnableKeyword DisableKeyword SetOverrideTag
Clamp Clamp01 Lerp Min Max Abs Sign Sqrt Sin Cos Tan Atan Atan2 Asin Acos
Pow Exp Floor Ceil Round Repeat PingPong SmoothStep MoveTowards DeltaAngle
Angle Distance Dot Cross Normalize NormalizeSafe Project ProjectOnPlane
Cos Sin Sinh Cosh
GetKey GetKeyDown GetKeyUp GetMouseButton GetMouseButtonDown GetMouseButtonUp
ScreenToWorldPoint ScreenPointToRay WordToScreenPoint WorldToScreenPoint
Raycast RaycastAll OverlapSphere SphereCast BoxCast Linecast
LookAt LookRotation Rotate RotateAround Translate TransformPoint
InverseTransformPoint TransformDirection InverseTransformDirection
SetPositionAndRotation SimpleMove MovePosition MoveRotation AddForce
GetPoint GetLength GetPosition GetName GetNormal SetOwner
ConfigureEditor ConfigurePreview ConfigurePathOptimization
RoundToInt FloorToInt CeilToInt ClosestPowerOfTwo
Write WriteLine Read ReadAsync WriteAsync Send SendAsync Receive
EncodeArrayToJPG EncodeToJPG EncodeToPNG
Begin Start End Stop Play Pause
""".split())

# 类型/命名空间前缀（出现在 `Xxx.Yyy(` 里，不算自定义方法）
TYPE_PREFIX = (
    "UnityEngine.", "UnityEditor.", "System.", "Mathf.", "Vector2.", "Vector3.",
    "Quaternion.", "Random.", "Time.", "Application.", "Debug.", "Color.",
    "Physics.", "LayerMask.", "Screen.", "Resources.", "Object.", "GameObject.",
    "EditorUtility.", "Undo.", "AssetDatabase.", "Shader.", "Material.",
    "Camera.", "Texture", "RenderTexture.", "Mesh.", "Gizmos.", "Path.",
    "Directory.", "File.", "String.", "Enum.", "GraphicsFormat.",
    "RenderTextureFormat.", "TextureFormat.", "CameraClearFlags.",
    "CullMode.", "BlendMode.", "ShadowCastingMode.", "MaterialGlobalIlluminationFlags.",
    "string.", "List<", "Dictionary<", "Queue<", "Vector3Int.",
)
# 局部工具类的静态调用（自己写的，允许）
LOCAL_STATIC = ("Solid", "Textured", "Emissive", "Glass", "StdShader", "LoadTexture")


def strip_comments_strings(src):
    """去掉注释和字符串字面量 —— 免得里面的词被当成代码"""
    out = []
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c == '/' and i + 1 < n and src[i + 1] == '/':
            while i < n and src[i] != '\n':
                i += 1
            continue
        if c == '/' and i + 1 < n and src[i + 1] == '*':
            i += 2
            while i + 1 < n and not (src[i] == '*' and src[i + 1] == '/'):
                i += 1
            i += 2
            continue
        if c == '@' and i + 1 < n and src[i + 1] == '"':
            i += 2
            while i < n:
                if src[i] == '"':
                    if i + 1 < n and src[i + 1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                i += 1
            continue
        if c == '"':
            i += 1
            while i < n:
                if src[i] == '\\':
                    i += 2
                    continue
                if src[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if c == "'":
            i += 1
            while i < n:
                if src[i] == '\\':
                    i += 2
                    continue
                if src[i] == "'":
                    i += 1
                    break
                i += 1
            continue
        out.append(c)
        i += 1
    return ''.join(out)


def strip_attributes(code):
    """
    去掉属性语法 `[Header("x")]` / `[Tooltip("y")]` / `[MenuItem("...")]`。
    ★ 这些是最主要的误报源：它们是 C# Attribute，不是方法调用。
      规则：`[` 开头、内部不含 `{` `;`、以 `]` 结尾的一段，当属性删掉。
    """
    return re.sub(r'\[[^\[\]{};]*\]', ' ', code)


def definitions(code):
    """本文件里定义的方法名（含 Unity 生命周期方法）"""
    # 返回类型 + 方法名(   —— 覆盖 void/int/float/bool/string/Mesh/自定义类型
    return set(re.findall(
        r'(?:^|\n)\s*(?:\[[^\]]*\]\s*)*'
        r'(?:public|private|protected|internal)?\s*'
        r'(?:static\s+|override\s+|virtual\s+|abstract\s+|partial\s+)*'
        r'[\w<>\[\],.]+\s+'
        r'([A-Za-z_]\w*)\s*\([^)]*\)\s*(?:\{|;|=>)',
        code))


def calls(code):
    """所有 `Xxx(` 形式的调用，返回 (名字, 行号)"""
    out = []
    for m in re.finditer(r'(?<![\w.])([A-Z][A-Za-z0-9_]*)\s*\(', code):
        ln = code[:m.start()].count('\n') + 1
        # 前面跟着点号的是成员调用，跳过（不是本文件的静态定义）
        pre = code[max(0, m.start() - 1):m.start()]
        if pre == '.':
            continue
        out.append((m.group(1), ln, code[max(0, m.start() - 30):m.start()]))
    return out


def main():
    print("=" * 74)
    print("C# 符号自检：调用的自定义方法是否有定义")
    print("=" * 74)

    all_ok = True
    for rel in FILES:
        path = os.path.join(PROJ, rel)
        name = os.path.basename(rel)
        print(f"\n--- {name} ---")

        if not os.path.exists(path):
            print("  (文件不存在，跳过)")
            continue

        code = strip_comments_strings(open(path, encoding='utf-8').read())
        code = strip_attributes(code)
        defs = definitions(code)

        bad = []
        for cname, ln, pre in calls(code):
            if cname in defs:
                continue
            if cname in API:
                continue
            if any(cname.startswith(p) for p in TYPE_PREFIX):
                continue
            if cname in LOCAL_STATIC:
                continue
            # `new GameObject(...)` 这类构造调用
            if re.search(r'new\s+$', pre):
                continue
            bad.append((cname, ln))

        if bad:
            all_ok = False
            print(f"  ★ {len(bad)} 处调用了但本文件没有定义：")
            seen = set()
            for cname, ln in bad:
                if cname in seen:
                    continue
                seen.add(cname)
                print(f"      第 {ln} 行   {cname}(   ← 确认是不是拼错了")
        else:
            print(f"  ✔ 所有自定义方法调用都有定义（本文件共 {len(defs)} 个方法）")

    print("\n" + "=" * 74)
    print("✔ 没发现可疑符号" if all_ok else "★ 有病疑项，人工确认（也可能是误报）")
    print("★ 这只是正则匹配 —— 真正的保证永远是 Unity 编译通过")
    print("=" * 74)


if __name__ == '__main__':
    main()
