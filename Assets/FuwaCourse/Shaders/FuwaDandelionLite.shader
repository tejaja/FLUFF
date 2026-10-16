// たんぽぽの軽い版（遠く用の板）：アトラスの絵（今のたんぽぽを撮って焼いたもの）× 頂点カラー、透明な所は切り抜き。
// 光はやわらかく（絵に陰が焼いてあるので弱め）。風のゆれは Kaze_DandelionSway と同じ（uv3.x＝重み、uv3.y＝位相）。両面描画
Shader "FuwaCourse/DandelionLite"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Cutoff ("Cutoff", Range(0, 1)) = 0.45
        _Wrap ("Soft Light (wrap)", Range(0, 1)) = 0.7
        _Ambient ("Ambient", Range(0, 1)) = 0.55
        _LightAmount ("Light Amount", Range(0, 1)) = 0.45
        [Header(Sway)]
        _SwayAmp ("ゆれ幅 (m、0=ゆれない)", Float) = 0
        _SwaySpeed ("ゆれの速さ", Float) = 1.2
        _SwayDir ("ゆれる向き（ワールド）", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            half _Cutoff, _Wrap, _Ambient, _LightAmount;
            float _SwayAmp, _SwaySpeed; float4 _SwayDir;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 uv3 : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };

            float3 Sway(float4 vtx, float4 uv3)
            {
                if (_SwayAmp > 0 && uv3.x > 0)
                {
                    float t = _Time.y * _SwaySpeed + uv3.y;
                    float s = sin(t) * 0.65 + sin(t * 1.73 + 1.1) * 0.35;   // Kaze_DandelionSway（InkCap）と同じ式（切りかえで動きがずれないように）
                    float3 dw = normalize(_SwayDir.xyz) * (0.35 + 0.65 * (s * 0.5 + 0.5)) + cross(float3(0, 1, 0), normalize(_SwayDir.xyz)) * sin(t * 0.83 + 2.0) * 0.3;
                    vtx.xyz += mul((float3x3)unity_WorldToObject, dw) * _SwayAmp * uv3.x;
                }
                return vtx.xyz;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                v.vertex.xyz = Sway(v.vertex, v.uv3);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                clip(tex.a - _Cutoff);
                float3 n = normalize(i.wn) * (facing > 0 ? 1 : -1);
                float3 l = normalize(_WorldSpaceLightPos0.xyz + float3(0, 1e-4, 0));
                half nl = saturate((dot(n, l) + _Wrap) / (1 + _Wrap));
                half3 light = lerp(1, _LightColor0.rgb * nl + ShadeSH9(half4(n, 1)) * _Ambient, _LightAmount);
                fixed4 c = fixed4(tex.rgb * i.color.rgb * light, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; half _Cutoff;
            float _SwayAmp, _SwaySpeed; float4 _SwayDir;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float4 uv3 : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { V2F_SHADOW_CASTER; float2 uv : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                if (_SwayAmp > 0 && v.uv3.x > 0)
                {
                    float t = _Time.y * _SwaySpeed + v.uv3.y;
                    float s = sin(t) * 0.65 + sin(t * 1.73 + 1.1) * 0.35;   // Kaze_DandelionSway（InkCap）と同じ式（切りかえで動きがずれないように）
                    float3 dw = normalize(_SwayDir.xyz) * (0.35 + 0.65 * (s * 0.5 + 0.5)) + cross(float3(0, 1, 0), normalize(_SwayDir.xyz)) * sin(t * 0.83 + 2.0) * 0.3;
                    v.vertex.xyz += mul((float3x3)unity_WorldToObject, dw) * _SwayAmp * v.uv3.x;
                }
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            float4 frag(v2f i) : SV_Target { clip(tex2D(_MainTex, i.uv).a - _Cutoff); SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
}
