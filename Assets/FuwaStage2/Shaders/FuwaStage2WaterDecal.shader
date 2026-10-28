// 水面の上に重ねる泡・流れの板（2026-10-27 てじゃ「滝と水面の境目をもっと見栄え良く」「滝が出来るほうもなんかいいかんじに」）
// uv＝板のローカルの (x, z) をメートルで（builder が平らな格子を作る）。
// _Mode=0 滝つぼ：z=0 が滝の前の面、+z が下流。滝の幅の所はもこもこの白い泡、まわりは白っぽく泡立った水と、外へ広がる泡の輪
// _Mode=1 落ち口：z=0 が落ち口、+z が上流（池の側）。水が落ち口へ吸い寄せられる筋（落ち口に近いほど速く、白く）と、落ち口のきわの明るい帯
Shader "FuwaStage2/WaterDecal"
{
    Properties
    {
        [Enum(Splash, 0, Lip, 1)] _Mode ("Mode", Float) = 0
        _Foam ("Foam Color", Color) = (0.96, 1, 1, 1)
        _Aerated ("Aerated Water Color", Color) = (0.55, 0.85, 0.97, 1)
        _HalfW ("Falls Half Width (m)", Float) = 3
        _Reach ("Reach (m)", Float) = 4
        _Speed ("Speed", Float) = 0.7
        _Band ("Lip: Flat Band Length (m)", Float) = 0
        // 板の横のはし（x の範囲）でなだらかに消す（2026-10-28 柱の滝の滝つぼ：板のはしがまっすぐな線に見えた）。fade.x/y＝範囲、z＝消す長さ（0＝しない）
        _XFade ("Side Fade (x0, x1, len)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        // 滝の膜（Transparent、深さを書く厚い滝）のあと。前は滝より先に描いていて、滝の上の平らな所で泡が上書きされて消え、池とのさかい目が見えた（2026-10-27）
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Offset -1, -1
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Foam, _Aerated;
            float _Mode, _HalfW, _Reach, _Speed, _Band;
            float4 _XFade;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm(float2 p) { return Noise(p) * 0.55 + Noise(p * 2.1 + 5.2) * 0.3 + Noise(p * 4.3 + 1.7) * 0.15; }

            fixed4 frag (v2f i) : SV_Target
            {
                float x = i.uv.x, z = i.uv.y, t = _Time.y;
                float3 col; float a;
                if (_Mode < 0.5)
                {
                    // 滝の幅の外へのはみ出し と 前後の距離 → 楕円の距離（下流へは長く、滝の裏へは短く）
                    float dx = max(0, abs(x) - _HalfW);
                    float dz = z > 0 ? z / 1.6 : -z * 2.0;
                    float d = length(float2(dx, dz));
                    // ふちを波打たせる
                    float wob = (Noise(float2(x * 0.9, z * 0.9) + t * 0.3) - 0.5) * 0.9;
                    float dd = max(0, d + wob);
                    // もこもこの泡：外へ流れながらわき上がるノイズ。滝に近いほど泡が多い
                    float2 q = float2(x * 1.6, z * 1.6 - t * _Speed * 1.4);
                    float n = Fbm(q + float2(Noise(q * 0.5 + t * 0.4) * 1.5, 0));
                    float foam = smoothstep(0.30 + dd * 0.22, 0.42 + dd * 0.22, n);
                    float core = 1 - smoothstep(0.15, 0.9, dd);   // 滝のすぐ下は真っ白
                    foam = max(foam, core * smoothstep(0.25, 0.45, n + 0.25));
                    // 外へ広がる泡の輪（切れ切れ）
                    float ph = frac(dd * 0.75 - t * _Speed * 0.5);
                    float ring = smoothstep(0.0, 0.05, ph) * (1 - smoothstep(0.05, 0.16, ph));
                    ring *= smoothstep(0.45, 0.6, Noise(float2(atan2(z, x) * 4.0, dd * 0.6 - t * 0.3) + float2(x, z) * 0.7));
                    float fade = 1 - smoothstep(_Reach * 0.45, _Reach, dd);
                    // 泡立って白っぽい水（滝のまわりは濃い青より明るい）
                    float aer = (1 - smoothstep(0.0, _Reach * 0.8, dd)) * 0.75;
                    col = lerp(_Aerated.rgb, _Foam.rgb, saturate(foam + ring));
                    a = saturate(max(aer, saturate(foam + ring * 0.8))) * fade;
                }
                else
                {
                    // 落ち口へ吸い寄せられる流れ：落ち口に近いほど速い（座標を縮めて流す）
                    float side = 1 - smoothstep(_HalfW - 0.6, _HalfW + 0.2, abs(x));
                    float u = sqrt(max(z, 0) + 0.05);   // 近いほど詰まる＝速く見える
                    float2 q1 = float2(x * 2.4, u * 6.0 + t * _Speed * 2.0);
                    float2 q2 = float2(x * 3.7 + 3.3, u * 8.0 + t * _Speed * 2.6);
                    float n = Noise(q1) * 0.6 + Noise(q2) * 0.4;
                    float near = 1 - smoothstep(0.0, _Reach, z);
                    float streak = smoothstep(0.66 - near * 0.12, 0.74 - near * 0.12, n) * near;
                    // 落ち口のきわの明るい帯・平らな所をおおう帯はやめた（帯のはし＝落ち口で板が切れて、白い線に見えた。2026-10-27 てじゃ「上の白いエフェクトの境目のせいで白い線」）。
                    // 筋も落ち口の手前 0.5m でなだらかに消す（板のはしが見えないように）
                    float lipFade = smoothstep(0.0, 0.5, z);
                    col = lerp(_Aerated.rgb, _Foam.rgb, saturate(streak));
                    a = saturate(streak * 0.5) * side * lipFade;
                }
                if (_XFade.z > 0.001)
                {
                    // ふちを波打たせて消す（まっすぐな線にならないように）
                    float w = _XFade.z * (0.6 + 0.8 * Noise(float2(z * 1.3 - t * 0.4, x * 0.3)));
                    a *= smoothstep(0, w, min(x - _XFade.x, _XFade.y - x));
                }
                fixed4 c = fixed4(col, a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
