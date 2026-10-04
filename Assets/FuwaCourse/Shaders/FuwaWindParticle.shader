Shader "FuwaCourse/WindParticle"
{
    // 風の粒（上昇気流の粒・もくもく・つむじ風の砂ぼこりなど）。テクスチャ×頂点カラーの半透明。
    // カメラから遠いほどうっすらにする（_FadeStart〜_FadeEnd で 1 → _FarAlpha）
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _FadeStart ("Fade Start (m)", Float) = 15
        _FadeEnd ("Fade End (m)", Float) = 45
        _FarAlpha ("Far Alpha", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color;
            float _FadeStart, _FadeEnd; half _FarAlpha;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float d = distance(wp, _WorldSpaceCameraPos);
                half far = lerp(1, _FarAlpha, saturate((d - _FadeStart) / max(0.01, _FadeEnd - _FadeStart)));
                o.color = v.color * _Color;
                o.color.a *= far;
                UNITY_TRANSFER_FOG(o, o.pos); return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                { fixed4 fogc__ = c; UNITY_APPLY_FOG(i.fogCoord, fogc__); return fogc__; }
            }
            ENDCG
        }
    }
}
