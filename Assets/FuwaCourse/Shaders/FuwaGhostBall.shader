Shader "FuwaCourse/GhostBall"
{
    // 他人の玉（分身）用の、毛のないツルツルの半透明の玉。人数分出るので1パスだけ。
    // 色は FuwaGhost が人ごとに _Color（本体）と _TipColor（ふちの光）を入れる
    Properties
    {
        _Color ("Body Color", Color) = (0.8, 0.9, 1, 1)
        _TipColor ("Rim Color", Color) = (0.9, 0.95, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.35
        _Inflate ("Inflate", Range(0, 0.3)) = 0.08
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.9
        _Shade ("Shade", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color, _TipColor;
            half _Alpha, _Inflate, _RimPower, _RimStrength, _Shade;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata i)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // 毛があった頃と同じくらいの大きさに見えるよう、少しふくらませる
                float4 p = i.vertex + float4(i.normal * _Inflate, 0);
                float3 wp = mul(unity_ObjectToWorld, p).xyz;
                o.pos = UnityWorldToClipPos(wp);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = _WorldSpaceCameraPos - wp;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 v = normalize(i.v);
                // 上が明るく下が少し暗い、やわらかい陰（ライトの向きに頼らないので、どの場所でも同じ見え方）
                half shade = lerp(1 - _Shade, 1, n.y * 0.5 + 0.5);
                half rim = pow(1 - saturate(dot(n, v)), _RimPower) * _RimStrength;
                fixed3 col = _Color.rgb * shade + _TipColor.rgb * rim;
                return fixed4(col, saturate(_Alpha + rim * 0.6));
            }
            ENDCG
        }
    }
}
