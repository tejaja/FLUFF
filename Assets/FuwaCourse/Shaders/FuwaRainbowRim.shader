// つぎのしまの看板の後ろの雲を、にじ色に（2026-10-19 てじゃ：赤だと2–2 ひのやまと被るので、ステージ2の6色ぜんぶを含むにじ色に）。
// 看板の中心からの角度で色相を回す（メッシュのローカル x,y）。ゆっくり回る。下の方は少し濃く（ほかの看板の色の雲と同じ影の付け方）
Shader "FuwaCourse/RainbowRim"
{
    Properties
    {
        _Sat ("Saturation", Range(0, 1)) = 0.55
        _Val ("Brightness", Range(0, 1.5)) = 1.0
        _Speed ("Hue Rotate Speed (turns/s)", Float) = 0.03
        _HueOffset ("Hue Offset", Range(0, 1)) = 0
        _Shade ("Light Shade", Range(0, 1)) = 0.2
        _Under ("Bottom Darken", Range(0, 1)) = 0.18
        _UnderGrad ("Bottom Gradient (0-1 of height)", Range(0.01, 1)) = 0.5
        _HalfH ("Half Height (mesh)", Float) = 0.72
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float _Sat, _Val, _Speed, _HueOffset, _Shade, _Under, _UnderGrad, _HalfH;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 op : TEXCOORD0; float3 wn : TEXCOORD1; UNITY_FOG_COORDS(2) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.op = v.vertex.xyz;
                o.wn = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float3 Hue(float h) { float3 k = abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1; return saturate(k); }

            fixed4 frag (v2f i) : SV_Target
            {
                float ang = atan2(i.op.y, i.op.x) / 6.28318 + 0.5;
                float3 col = lerp(1, Hue(ang + _HueOffset + _Time.y * _Speed), _Sat) * _Val;
                float ndl = dot(normalize(i.wn), normalize(_WorldSpaceLightPos0.xyz)) * 0.5 + 0.5;
                col *= lerp(1 - _Shade, 1, ndl);
                float under = 1 - smoothstep(0, _UnderGrad * 2 * _HalfH, i.op.y + _HalfH);
                col *= 1 - _Under * under;
                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
