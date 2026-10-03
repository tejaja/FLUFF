// しゃぼん玉（本物の球のメッシュ用：メッシュのパーティクルや球のオブジェクト）。
// ビルボードと違って、VRで首を動かしても形や模様がぐにゃっとしない。
// 縁ほど濃い（フレネル）＋虹色の膜（下ほど厚い）＋光の映りこみのハイライト（世界の向きで固定）。
// 頂点カラーのアルファで全体の濃さ（パーティクルの出る・消える）
Shader "FuwaCourse/Bubble3D"
{
    Properties
    {
        _RimPower ("Rim Power（大きいほどふちだけ）", Range(0.5, 6)) = 2.2
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.8
        _FillAlpha ("Fill Alpha", Range(0, 0.3)) = 0.04
        _Iris ("Iridescence", Range(0, 1)) = 0.8
        _Tint ("Tint（虹をやめて色をつける時。_Iris を0に）", Color) = (1, 1, 1, 1)
        _Highlight ("Highlight", Range(0, 2)) = 1.2
        _LightDir ("映りこむ光の向き（世界）", Vector) = (-0.5, 0.8, -0.35, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half _RimPower, _RimAlpha, _FillAlpha, _Iris, _Highlight;
            half4 _Tint; float4 _LightDir;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; fixed4 color : COLOR; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            half3 Hue(half h) { h = frac(h); return saturate(abs(frac(h + half3(0, 0.6667, 0.3333)) * 6 - 3) - 1); }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.wn);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = saturate(dot(n, v));
                float rim = pow(1 - ndv, _RimPower);
                // 膜は下ほど厚い（世界の上下で決めるので、首をかしげても変わらない）
                float bottom = saturate(0.5 - n.y * 0.5);
                float film = rim * 0.9 + bottom * 0.35 + _Time.y * 0.05;
                half3 iris = lerp(Hue(film), half3(1, 1, 1), lerp(0.45, 0.15, bottom));
                half3 col = lerp(half3(1, 1, 1), iris, _Iris) * _Tint.rgb * (0.85 + 0.25 * rim);
                float a = _FillAlpha + rim * _RimAlpha * lerp(0.7, 1.1, bottom);
                // 光の映りこみ：反射した視線が光の向きに近い所が白く光る（世界の向きで固定）
                float3 r = reflect(-v, n);
                float3 L = normalize(_LightDir.xyz);
                float hl = smoothstep(0.965, 0.993, dot(r, L));
                float hl2 = smoothstep(0.96, 0.99, dot(r, normalize(float3(0.6, -0.3, 0.5)))) * 0.6;
                col += (hl + hl2) * _Highlight;
                a = saturate(a + (hl + hl2) * 0.8);
                fixed4 o = fixed4(col, a * i.color.a);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
}
