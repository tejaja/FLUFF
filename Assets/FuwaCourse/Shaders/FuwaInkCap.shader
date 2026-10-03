// ヒトヨタケ（重力エリアのキノコ）：頂点カラー（sRGBで書いた色）＋やわらかい光。
// 黒いインクの所（暗い色）だけツヤを出して、ぬれて溶けてる感じにする。
// 傘は厚みのない膜なので両面描画（裏から見た時は法線を反転）。しずくの付け根やふちの裏が抜けて見えないように。
Shader "FuwaCourse/InkCap"
{
    Properties
    {
        _Wrap ("Soft Light (wrap)", Range(0, 1)) = 0.5
        _Ambient ("Ambient", Range(0, 1)) = 0.35
        _InkGloss ("インクのツヤ", Range(0, 2)) = 0.9
        _InkTint ("インクのツヤの色", Color) = (0.75, 0.6, 1, 1)
        _DripAmp ("しずくの伸び縮み (m)", Float) = 0.09
        _DripSpeed ("しずくの揺れの速さ", Float) = 1.7
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            half _Wrap, _Ambient, _InkGloss;
            float _DripAmp, _DripSpeed;
            fixed4 _InkTint;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 wn : TEXCOORD0;
                float3 wp : TEXCOORD1;
                SHADOW_COORDS(2)
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // しずく（uv.x＝付け根0〜先1、uv.y＝位相）：先ほど大きく、たらーんと伸びたり縮んだり
                if (v.uv.x > 0.001)
                {
                    float w = v.uv.x * v.uv.x;
                    float s = sin(_Time.y * _DripSpeed + v.uv.y) * 0.6 + sin(_Time.y * _DripSpeed * 2.3 + v.uv.y * 1.7) * 0.4;
                    v.vertex.y -= _DripAmp * w * (0.5 + 0.5 * s);
                }
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i, float facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.wn) * (facing < 0 ? -1 : 1);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                fixed3 col = GammaToLinearSpace(i.color.rgb);
                half ink = saturate(1 - dot(i.color.rgb, 1.0 / 3.0) * 3.0);   // 暗い所ほど1
                half atten = SHADOW_ATTENUATION(i);
                half d = saturate((dot(n, l) + _Wrap) / (1 + _Wrap));
                half spec = pow(saturate(dot(n, normalize(l + v))), 40) * ink * _InkGloss;
                half fres = pow(1 - saturate(dot(v, n)), 3);
                fixed3 c = (col * d + spec * _InkTint.rgb) * _LightColor0.rgb * atten
                         + col * ShadeSH9(half4(0, 1, 0, 1)) * _Ambient * 2
                         + fres * ink * _InkTint.rgb * 0.15;
                fixed4 o = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
