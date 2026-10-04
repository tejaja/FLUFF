Shader "FuwaCourse/PuffHalo"
{
    // たんぽぽの綿毛の玉にかぶせる、うっすら白い半透明の玉。ふちほど白く、中心へ向かって放射状の細い筋（毛）がうっすら入る。
    // 球の法線＝中心からの向きとして使うので、まとめたメッシュでもOK（球ごとの中心はいらない）
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.55)
        _RimPow ("Rim Power", Range(0.5, 6)) = 1.6
        _Base ("Center Alpha", Range(0, 1)) = 0.12
        _Fibers ("Fiber Count", Float) = 150
        _FiberAmt ("Fiber Strength", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Cull Back
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

            fixed4 _Color; half _RimPow, _Base, _FiberAmt; float _Fibers;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos); return o;
            }

            float hash11(float x) { return frac(sin(x * 127.1) * 43758.5453); }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = saturate(dot(n, v));
                float rim = pow(1 - ndv, _RimPow);
                // 視線に垂直な面での向き（角度）で、中心から放射状にのびる筋を作る
                float3 b1 = normalize(cross(v, abs(v.y) < 0.95 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 b2 = cross(v, b1);
                float3 t = n - v * dot(n, v);
                float ang = atan2(dot(t, b2), dot(t, b1)) / 6.2831853 + 0.5;   // 0〜1
                // 太さ・濃さ・長さがばらばらの筋を、本数の違う2層で重ねる（規則正しく見えないように）
                float lines = 0;
                for (int k = 0; k < 2; k++)
                {
                    float cnt = _Fibers * (k == 0 ? 1.0 : 0.43);
                    float x = ang * cnt + k * 0.37;
                    float cell = floor(x), f = frac(x);
                    float w = 0.12 + 0.3 * hash11(cell + k * 13.1);
                    float len = 0.55 + 0.45 * hash11(cell + k * 7.3 + 3.1);      // 筋の届く長さ（ふちからの割合）
                    float l = smoothstep(w, 0, abs(f - 0.5)) * (0.35 + 0.65 * hash11(cell + 31.7 + k));
                    l *= smoothstep(len, len - 0.25, ndv);                         // 中心寄りでは途切れる
                    lines = max(lines, l);
                }
                float fiber = lerp(1, 0.35 + lines * 1.3, _FiberAmt);
                // ふちの外側ぎりぎりはぼかして、輪郭をふわっとさせる
                float edge = smoothstep(0.0, 0.3, ndv);
                float a = _Color.a * saturate((_Base + rim * edge) * fiber);
                fixed4 c = fixed4(_Color.rgb, a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
