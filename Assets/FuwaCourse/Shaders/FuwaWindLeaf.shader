Shader "FuwaCourse/WindLeaf"
{
    // 風に舞う葉っぱ・花びら（メッシュパーティクル用）。両面描画、色は頂点カラー、上からのやわらかい陰だけ
    // カメラから遠いほどうっすらにする（_FadeStart〜_FadeEnd で 1 → _FarAlpha）
    Properties
    {
        _Shade ("Shade", Range(0, 1)) = 0.35
        _FadeStart ("Fade Start (m)", Float) = 15
        _FadeEnd ("Fade End (m)", Float) = 45
        _FarAlpha ("Far Alpha", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            half _Shade;
            float _FadeStart, _FadeEnd; half _FarAlpha;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                float d = distance(mul(unity_ObjectToWorld, v.vertex).xyz, _WorldSpaceCameraPos);
                o.color.a = lerp(1, _FarAlpha, saturate((d - _FadeStart) / max(0.01, _FadeEnd - _FadeStart)));
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.n) * (facing > 0 ? 1 : -1);
                half shade = lerp(1 - _Shade, 1, saturate(n.y * 0.5 + 0.5));
                return fixed4(i.color.rgb * shade, i.color.a);
            }
            ENDCG
        }
    }
}
