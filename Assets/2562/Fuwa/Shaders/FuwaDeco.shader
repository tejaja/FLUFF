// Vketブース版 Deco（元は FLUFF の FuwaCourse/Deco。サーフェスシェーダーは multi_compile を自動で足すので、頂点・フラグメントで書き直し）。
// 道のふちの飾り（草・花・小石・ツタ）用：両面描画、裏から見た時は法線を反転してふつうに明るく。
// 光は FuwaBooth.cginc の固定の太陽と空の色
// 風でゆれる（2026-10-16 てじゃ「動きほしい、風ふかせて」）：uv3.x＝ゆれの重み（根元0〜先1）、uv3.y＝位相。uv3 は FuwaBoothWind が飾りのメッシュの複製に入れる。
// 同じメッシュの草どうしがそろって揺れないよう、ワールドの位置でも位相をずらす（静的バッチングでもワールド座標は残る）
Shader "Custom/2562/Fuwa/Deco"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Wrap ("Soft Light (wrap)", Range(0, 1)) = 0.4
        [Toggle] _UseTint ("色を頂点の uv6 から（材質まとめ用）", Float) = 0
        [Header(Sway)]
        _SwayAmp ("ゆれ幅 (m、0=ゆれない)", Float) = 0
        _SwaySpeed ("ゆれの速さ", Float) = 1.6
        _SwayDir ("ゆれる向き（ブースの座標）", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGINCLUDE
        #include "FuwaBooth.cginc"
        float _SwayAmp, _SwaySpeed; float4 _SwayDir;
        // _SwayDir はブースの座標の向き。tangent＝ブースの +X（FuwaBoothDir）
        float3 DecoSway(float3 world, float4 uv3, float4 tangent)
        {
            if (_SwayAmp <= 0 || uv3.x <= 0) return world;
            float t = _Time.y * _SwaySpeed + uv3.y + dot(world.xz, float2(1.7, 1.1));
            float s = sin(t) * 0.65 + sin(t * 1.73 + 1.1) * 0.35;
            float3 d = normalize(FuwaBoothDir(_SwayDir.xyz, tangent));
            float3 dw = d * (0.35 + 0.65 * (s * 0.5 + 0.5)) + cross(float3(0, 1, 0), d) * sin(t * 0.83 + 2.0) * 0.3;
            return world + dw * _SwayAmp * uv3.x;
        }
        ENDCG
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "FuwaBooth.cginc"
            fixed4 _Color;
            half _Wrap, _UseTint;
            // 材質まとめ（2026-10-17 Vket の SetPass 制限）：色ちがいの材質を1つにして、色は頂点の uv6（リニアの色）に入れる
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; float4 uv3 : TEXCOORD3; float4 tint : TEXCOORD6; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; half3 tint : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 w = DecoSway(mul(unity_ObjectToWorld, v.vertex).xyz, v.uv3, v.tangent);
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1));
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.tint = _UseTint > 0.5 ? v.tint.rgb : half3(1, 1, 1);
                return o;
            }
            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.wn) * (facing > 0 ? 1 : -1);
                half d = saturate((dot(n, FUWA_BOOTH_SUN_DIR) + _Wrap) / (1 + _Wrap));
                // サーフェスシェーダーの時と同じ：太陽（ふんわり）＋空の明るさ
                return fixed4(_Color.rgb * i.tint * (FUWA_BOOTH_SUN_COLOR * d + FuwaBoothAmbient(n)), 1);
            }
            ENDCG
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; float4 uv3 : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { V2F_SHADOW_CASTER; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); v.vertex.xyz = mul(unity_WorldToObject, float4(DecoSway(mul(unity_ObjectToWorld, v.vertex).xyz, v.uv3, v.tangent), 1)).xyz; TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) return o; }
            float4 frag (v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
}
