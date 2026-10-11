// 光锥投影着色器 —— 把车载摄像头的平面画面，投成"打在地面上的一束光"。
//
// 【它解决的核心问题】
//   摄像头画面是一张矩形的平面图（240×180）。直接贴到一个立方体上，
//   看起来就是"车前面贴了一张海报"，完全不像光。
//   要像"光锥投射"，必须同时做对三件事：
//
//   ① **透视变形（梯形）**：近处窄、远处宽。
//      远处宽是因为光锥散开了；这靠 UV 插值实现 ——
//      几何体本身做成梯形（近边短、远边长），UV 仍然给 0~1 的规则矩形，
//      光栅化时 UV 自然被拉伸成梯形。**不用在 shader 里算，靠顶点位置就行**。
//
//   ② **边缘柔化**：光斑的边界不能是硬邦邦的一条直线。
//      真实光锥边缘是渐隐的（还有一圈比中心暗的"晕"）。
//      用 uv 到边界的距离做一个衰减乘子。
//
//   ③ **叠加而不是遮挡**：光是"加上去的"，不是"盖上去的"。
//      所以用 Blend One One（纯加法混合）—— 底下的路面纹理、
//      车道线、障碍物都还能看见，光只是让它们变亮。
//      如果用普通的 SrcAlpha/OneMinusSrcAlpha，画面会把路面盖住，
//      看起来就变成"一张不透明的贴纸"，立刻穿帮。
//
// 【另外两个让"光"更像光的小细节】
//   · **扫描线**：光柱上叠一层缓慢流动的横纹，像投影仪的光栅。
//   · **噪点**：轻微的高频抖动。纯数学的渐变太"干净"了，反而假。
//
// 【怎么用】
//   材质上设：
//     _MainTex  摄像头画面（RenderTexture）
//     _Color    光的颜色（一般是偏青白 / 暖白）
//     _Intensity 亮度倍数
//     _EdgeFade 边缘柔化范围（0.05~0.5）
//     _ScanSpeed 扫描线流动速度

Shader "Custom/BeamProjection"
{
    Properties
    {
        _MainTex    ("摄像头画面", 2D) = "white" {}
        _Color      ("光颜色", Color) = (0.65, 0.88, 1.0, 1.0)
        _Intensity  ("亮度", Range(0, 4)) = 1.35
        _EdgeFade   ("边缘柔化", Range(0.01, 0.6)) = 0.22
        _ScanSpeed  ("扫描线速度", Range(0, 4)) = 0.35
        _ScanAmount ("扫描线强度", Range(0, 1)) = 0.14
        _NoiseAmt   ("噪点强度", Range(0, 0.4)) = 0.045
        _Alpha      ("整体不透明度", Range(0, 1)) = 0.72
        // ★ 这个开关是给"地面光斑"用的：
        //   光柱（立体的锥体）要两边都看见（Cull Off），
        //   但地面光斑只有一个面朝上，背面不该渲染。
        //   留成属性方便复用同一份 shader。
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("剔除模式", Float) = 2
    }

    SubShader
    {
        // ★ Queue 用 Transparent：要在地面之后画，否则会被地面盖住。
        //   IgnoreProjector 不是必须的，但加上更安全。
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        // ★★ 关键：纯加法混合。
        //   One One = 源色直接加到背景上。光就是这么工作的 ——
        //   它不遮挡任何东西，只让东西变亮。
        //   用 SrcAlpha/OneMinusSrcAlpha 就会把路面盖住，穿帮。
        Blend One One

        // 半透明物体不写深度（否则后面的东西会被剔掉）
        ZWrite Off

        // ★ 不能写 ZTest Always，否则光会穿透车身和障碍物画在最上面。
        //   LEqual 保证光被前面的东西正常遮挡。
        ZTest LEqual

        Cull [_Cull]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float2 uv      : TEXCOORD0;
                // 世界坐标，给噪点和扫描线用 —— 这样花纹是"钉在空间里"的，
                // 车动的时候光斑上的纹路不会跟着滑（跟着滑会显得很假）
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Intensity;
            float _EdgeFade;
            float _ScanSpeed;
            float _ScanAmount;
            float _NoiseAmt;
            float _Alpha;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // 便宜的伪随机。不需要好，只要"够乱"。
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // ---- 1. 取摄像头画面 ----
                fixed3 cam = tex2D(_MainTex, uv).rgb;

                // ---- 2. 边缘柔化 ----
                //   算出 uv 到最近边界的距离，在 _EdgeFade 范围内渐隐。
                //   min 取四个边里最近的那个 —— 所以四个边都会被柔化。
                float2 edgeDist = min(uv, 1.0 - uv);
                float edge = min(edgeDist.x, edgeDist.y);
                // smoothstep 让它在边界处平滑掉到 0，而不是线性截断
                float edgeMask = smoothstep(0.0, _EdgeFade, edge);

                // ---- 3. 扫描线 ----
                //   沿光的长度方向（uv.y）流动的横纹。
                //   _Time.y 是 Unity 内置的秒数，乘速度就是流动效果。
                float scan = sin((uv.y * 34.0) - _Time.y * _ScanSpeed * 6.28318);
                scan = scan * 0.5 + 0.5;            // -1~1 → 0~1
                float scanMul = 1.0 - _ScanAmount * (1.0 - scan);

                // ---- 4. 噪点 ----
                //   用世界坐标而不是 uv，纹路就固定在空间里。
                float n = hash21(i.worldPos.xz * 37.0 + _Time.y * 1.7);
                float noiseMul = 1.0 + (n - 0.5) * 2.0 * _NoiseAmt;

                // ---- 5. 合成 ----
                //   cam 是画面本身，乘 1.6 让它亮一点（摄像机原图偏暗，
                //   光柱里的画面本来就该比原图更"发光"）。
                fixed3 col = cam * _Color.rgb * _Intensity * 1.6;

                col *= edgeMask * scanMul * noiseMul;

                // ★ 中心比边缘亮：光斑中心是光最密的地方。
                //   这里用 uv.y 做"远端更亮"（光走远了散开、散射更强）。
                float centerBoost = lerp(0.75, 1.15, uv.y);
                col *= centerBoost;

                // ---- 6. 输出 ----
                //   alpha 通道在 Blend One One 下不参与混合，
                //   但乘上它能让"整体不透明度"滑杆生效。
                return fixed4(col * _Alpha, 1.0);
            }
            ENDCG
        }
    }

    // 实在没法编译时的兜底，免得整个投影变成刺眼的洋红
    Fallback "Legacy Shaders/Transparent/VertexLit"
}
