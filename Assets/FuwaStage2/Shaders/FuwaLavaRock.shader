// 2–2 の火山から飛んでくる岩（2026-10-31）。暗い岩に、オレンジに光る割れ目。割れ目はゆっくり明滅する。
// 模様はオブジェクト座標（岩と一緒に回る）。まとめ描き（ダイナミックバッチング）を切っている。
Shader "FuwaCourse/LavaRock"
{
    Properties
    {
        [NoScaleOffset] _FlowTex ("溶岩の模様 (G 筋を割れ目に使う、継ぎ目なし)", 2D) = "gray" {}
        _Rock ("岩の色", Color) = (0.2, 0.11, 0.12, 1)
        [HDR] _Crack ("割れ目の色", Color) = (1.45, 0.55, 0.09, 1)
        [HDR] _CrackHot ("割れ目のまん中の色", Color) = (1.4, 1.0, 0.4, 1)
        _Scale ("割れ目の細かさ（球の直径あたり）", Float) = 0.55
        _Width ("割れ目の太さ", Range(0.05,0.6)) = 0.32
        _Under ("下側の赤い照り返し", Range(0,1)) = 0.5
        [HDR] _RimGlow ("ふちの熱の光", Color) = (1.3, 0.34, 0.05, 1)
        _RimPower ("ふちの光の細さ", Range(0.5,6)) = 2.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _FlowTex;
            fixed4 _Rock;
            half4 _Crack, _CrackHot;
            float _Scale;
            half _Width, _Under, _RimPower;
            half4 _RimGlow;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 op : TEXCOORD0; float3 wn : TEXCOORD1; half3 amb : TEXCOORD2; float3 wp : TEXCOORD4; UNITY_FOG_COORDS(3) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.op = v.vertex.xyz;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.amb = ShadeSH9(float4(o.wn, 1));
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                half wrap = saturate((dot(n, normalize(_WorldSpaceLightPos0.xyz)) + 0.5) / 1.5);
                half3 c = _Rock.rgb * (_LightColor0.rgb * wrap + i.amb * 0.6);
                // 下側は溶岩の照り返しで赤く
                c += _Crack.rgb * _Under * 0.22 * saturate(-n.y * 0.8 + 0.1);
                // ふちが熱で赤く光る（暗い空でも空の前でも、燃えている岩に見えるように）
                c += _RimGlow.rgb * pow(1 - saturate(dot(n, normalize(_WorldSpaceCameraPos - i.wp))), _RimPower);

                // 割れ目：3 方向から取った筋（どの面でも切れないように）
                float3 an = abs(normalize(i.op)); an = an * an; an /= an.x + an.y + an.z;
                float3 p = i.op * _Scale;
                half g = tex2D(_FlowTex, p.xy + 0.13).g * an.z + tex2D(_FlowTex, p.zy + 0.51).g * an.x + tex2D(_FlowTex, p.xz + 0.77).g * an.y;
                half th = 1 - _Width;
                half crack = smoothstep(th, th + 0.12, g);
                half core = smoothstep(th + 0.14, th + 0.3, g);
                half pulse = 0.85 + 0.15 * sin(_Time.y * 2.6 + i.op.x * 5);
                c = lerp(c, lerp(_Crack.rgb, _CrackHot.rgb, core) * pulse, crack);
                fixed4 o = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
        UsePass "FuwaCourse/ToonLit/SHADOWCASTER"
    }
    FallBack Off
}
