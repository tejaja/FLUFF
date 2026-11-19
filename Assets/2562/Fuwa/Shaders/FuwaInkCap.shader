// Vketブース版 InkCap（元は FLUFF の FuwaCourse/InkCap）。光は FuwaBooth.cginc の固定の太陽と空の色、multi_compile なし・影なし。
// 頂点カラー（sRGBで書いた色）＋やわらかい光。黒いインクの所（暗い色）だけツヤを出して、ぬれて溶けてる感じにする。
// 傘は厚みのない膜なので両面描画（裏から見た時は法線を反転）。
Shader "Custom/2562/Fuwa/InkCap"
{
    Properties
    {
        _Wrap ("Soft Light (wrap)", Range(0, 1)) = 0.5
        _Ambient ("Ambient", Range(0, 1)) = 0.35
        _InkGloss ("インクのツヤ", Range(0, 2)) = 0.9
        _InkTint ("インクのツヤの色", Color) = (0.75, 0.6, 1, 1)
        _DripAmp ("しずくの伸び縮み (m)", Float) = 0.09
        _DripSpeed ("しずくの揺れの速さ", Float) = 1.7
        [Header(Sway)]
        _SwayAmp ("ゆれ幅 (m、0=ゆれない)", Float) = 0
        _SwaySpeed ("ゆれの速さ", Float) = 1.2
        _SwayDir ("ゆれる向き（ブースの座標）", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGINCLUDE
        #include "UnityCG.cginc"
        #include "FuwaBooth.cginc"
        float _DripAmp, _DripSpeed;
        float _SwayAmp, _SwaySpeed; float4 _SwayDir;
        // しずくの伸び縮みと風のゆれ（色のパスと深度のパスで同じ形にする）。_SwayDir はブースの座標、tangent＝ブースの +X（FuwaBoothDir）
        float4 InkCapDeform(float4 vertex, float2 uv, float4 uv3, float4 tangent)
        {
            // しずく（uv.x＝付け根0〜先1、uv.y＝位相）：先ほど大きく、たらーんと伸びたり縮んだり
            if (uv.x > 0.001)
            {
                float w = uv.x * uv.x;
                float s = sin(_Time.y * _DripSpeed + uv.y) * 0.6 + sin(_Time.y * _DripSpeed * 2.3 + uv.y * 1.7) * 0.4;
                vertex.y -= _DripAmp * w * (0.5 + 0.5 * s);
            }
            // 風でゆれる（uv3.x＝ゆれの重み 0=根元〜1=先、uv3.y＝位相）。uv3 の無いメッシュはゆれない
            if (_SwayAmp > 0 && uv3.x > 0)
            {
                float t = _Time.y * _SwaySpeed + uv3.y;
                float s = sin(t) * 0.65 + sin(t * 1.73 + 1.1) * 0.35;
                float3 sd = normalize(FuwaBoothDir(_SwayDir.xyz, tangent));
                float3 dw = sd * (0.35 + 0.65 * (s * 0.5 + 0.5)) + cross(float3(0, 1, 0), sd) * sin(t * 0.83 + 2.0) * 0.3;
                vertex.xyz += mul((float3x3)unity_WorldToObject, dw) * _SwayAmp * uv3.x;
            }
            return vertex;
        }
        ENDCG
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "FuwaBooth.cginc"

            half _Wrap, _Ambient, _InkGloss;
            fixed4 _InkTint;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 uv3 : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 wn : TEXCOORD0;
                float3 wp : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                v.vertex = InkCapDeform(v.vertex, v.uv, v.uv3, v.tangent);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i, float facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.wn) * (facing < 0 ? -1 : 1);
                float3 l = FUWA_BOOTH_SUN_DIR;
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                fixed3 col = GammaToLinearSpace(i.color.rgb);
                half ink = saturate(1 - dot(i.color.rgb, 1.0 / 3.0) * 3.0);   // 暗い所ほど1
                half3 amb = FuwaBoothAmbient(float3(0, 1, 0));
                half d = saturate((dot(n, l) + _Wrap) / (1 + _Wrap));
                half spec = pow(saturate(dot(n, normalize(l + v))), 40) * ink * _InkGloss;
                half fres = pow(1 - saturate(dot(v, n)), 3);
                fixed3 c = (col * d + spec * _InkTint.rgb) * FUWA_BOOTH_SUN_COLOR
                         + col * amb * _Ambient * 2
                         + fres * ink * _InkTint.rgb * 0.15;
                return fixed4(c, 1);
            }
            ENDCG
        }
        // 深度用（しずくの伸び縮みと風のゆれに合わせて動かす）
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; float2 uv : TEXCOORD0; float4 uv3 : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { V2F_SHADOW_CASTER; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                v.vertex = InkCapDeform(v.vertex, v.uv, v.uv3, v.tangent);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            float4 frag(v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
}
