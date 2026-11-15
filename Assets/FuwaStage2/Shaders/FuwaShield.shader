// 2026-11-15 2–5 上の段：ふわふわのバリア（てじゃ「泡だとなんかあれだし、バリアっぽくしよ」）
//  ふちが光る半透明の球＋うっすら六角形の模様が流れる。衝撃波を受けると当たった側から波紋が広がって光る
//  _Appear（0〜1）：出し入れ（六角形のマスごとにパラパラ出る・消える）。_HitDir（ワールド、球の中心→当たった所）・_HitT（0〜1 進み、1 で終わり）
//  スクリプト（FuwaShield）が MaterialPropertyBlock で動かす。前に出す（premultiplied alpha）
Shader "Fuwa/Shield"
{
    Properties
    {
        _Color ("ふちの色", Color) = (0.45, 0.9, 1, 1)
        _Fill ("中の色", Color) = (0.3, 0.7, 1, 0.08)
        _RimPow ("ふちの細さ", Range(0.5, 8)) = 2.6
        _HexScale ("六角形の細かさ", Float) = 5.5
        _HexLine ("六角形の線の太さ", Range(0.01, 0.3)) = 0.07
        _HexAlpha ("六角形の濃さ", Range(0, 1)) = 0.45
        _Appear ("出ている量（スクリプト）", Range(0, 1)) = 1
        _HitDir ("当たった向き（スクリプト）", Vector) = (0, 0, 1, 0)
        _HitT ("当たりの進み（スクリプト）", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+20" "IgnoreProjector"="True" }
        CGINCLUDE
        #include "UnityCG.cginc"
        half _RimPow, _HexScale, _HexLine, _HexAlpha;
        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(float4, _Color)   // 2026-11-15 床に当たった回数で 水色→黄→赤（スクリプト）
            UNITY_DEFINE_INSTANCED_PROP(float4, _Fill)
            UNITY_DEFINE_INSTANCED_PROP(float, _Appear)
            UNITY_DEFINE_INSTANCED_PROP(float4, _HitDir)
            UNITY_DEFINE_INSTANCED_PROP(float, _HitT)
        UNITY_INSTANCING_BUFFER_END(Props)

        struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct v2f
        {
            float4 pos : SV_POSITION;
            float3 on : TEXCOORD0;   // 物の座標の向き（模様）
            float3 wn : TEXCOORD1;
            float3 wp : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        v2f vert (appdata v)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            o.pos = UnityObjectToClipPos(v.vertex);
            o.on = normalize(v.vertex.xyz);
            o.wn = UnityObjectToWorldNormal(v.normal);
            o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            return o;
        }
        // 六角形のマス：x＝ふちまでの近さ(0 まん中〜0.5 ふち)、yz＝マスの番号
        float3 Hex(float2 p)
        {
            const float2 s = float2(1, 1.7320508);
            float4 c = floor(float4(p, p - float2(0.5, 1)) / s.xyxy) + 0.5;
            float4 h = float4(p - c.xy * s, p - (c.zw + 0.5) * s);
            float4 r = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? float4(h.xy, c.xy) : float4(h.zw, c.zw + 0.5);
            float2 a = abs(r.xy);
            float d = max(dot(a, s * 0.5), a.x);
            return float3(d, r.zw);
        }
        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        // 3方向から張った六角形（球の向きで混ぜる）：線の強さ・マスの乱数
        void HexTri(float3 n, out float lineK, out float cellR)
        {
            float3 w = pow(abs(n), 4); w /= (w.x + w.y + w.z);
            float3 hx = Hex(n.yz * _HexScale), hy = Hex(n.zx * _HexScale), hz = Hex(n.xy * _HexScale);
            float aa = 0.04;
            float lx = smoothstep(0.5 - _HexLine - aa, 0.5 - _HexLine, hx.x);
            float ly = smoothstep(0.5 - _HexLine - aa, 0.5 - _HexLine, hy.x);
            float lz = smoothstep(0.5 - _HexLine - aa, 0.5 - _HexLine, hz.x);
            lineK = lx * w.x + ly * w.y + lz * w.z;
            float3 cr = float3(Hash(hx.yz), Hash(hy.yz + 7.1), Hash(hz.yz + 3.3));
            cellR = w.x > w.y ? (w.x > w.z ? cr.x : cr.z) : (w.y > w.z ? cr.y : cr.z);
        }
        half4 Shade(v2f i, float side)
        {
            UNITY_SETUP_INSTANCE_ID(i);
            float appear = UNITY_ACCESS_INSTANCED_PROP(Props, _Appear);
            float4 hitDir = UNITY_ACCESS_INSTANCED_PROP(Props, _HitDir);
            float hitT = UNITY_ACCESS_INSTANCED_PROP(Props, _HitT);
            half4 rimC = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
            half4 fillC = UNITY_ACCESS_INSTANCED_PROP(Props, _Fill);
            float3 n = normalize(i.wn) * side;
            float3 v = normalize(_WorldSpaceCameraPos - i.wp);
            float fres = pow(1 - saturate(dot(n, v)), _RimPow);
            float lineK, cellR; HexTri(normalize(i.on), lineK, cellR);
            // 出し入れ：マスごとに乱数で出る順番（ふちの線は少し先に出る）
            float show = saturate((appear * 1.25 - cellR) * 6);
            // 模様のゆらめき：ゆっくり上へ流れる明るさの帯
            float flow = 0.55 + 0.45 * sin(_Time.y * 2.2 + i.on.y * 7 + cellR * 6.28);
            // 当たり：当たった所から波紋が広がる＋そのあたりが光る
            float3 hd = normalize(hitDir.xyz + float3(0, 1e-4, 0));
            float ang = acos(clamp(dot(normalize(i.wn), hd), -1, 1)) / 3.14159;   // 0 当たった所 〜 1 反対側
            float hitLive = 1 - hitT;
            float ring = exp(-pow((ang - hitT * 0.9) * 9, 2)) * hitLive;
            float spot = saturate(1 - ang * 2.2) * hitLive * hitLive;
            float a = fillC.a + fres * 0.75 + lineK * _HexAlpha * flow * (0.35 + 0.65 * fres) + (ring * (0.4 + lineK) + spot * 0.6);
            a *= show * appear;
            // 出はじめはマスのふちがちょっと強く光る
            float edgeFlash = saturate((appear * 1.25 - cellR) * 6) * (1 - saturate((appear * 1.25 - cellR) * 2));
            half3 col = lerp(fillC.rgb, rimC.rgb, saturate(fres + lineK * 0.6)) + (ring + spot + edgeFlash) * lerp(half3(1, 1, 1), rimC.rgb, 0.4);
            a = saturate(a + edgeFlash * 0.5 * appear);
            if (side < 0) a *= 0.35;   // 裏がわ（向こうの面）はうすく
            return half4(col * a, a);
        }
        ENDCG
        // 向こうの面（うすく）
        Pass
        {
            Cull Front ZWrite Off Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragB
            #pragma multi_compile_instancing
            half4 fragB (v2f i) : SV_Target { return Shade(i, -1); }
            ENDCG
        }
        // 手前の面
        Pass
        {
            Cull Back ZWrite Off Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragF
            #pragma multi_compile_instancing
            half4 fragF (v2f i) : SV_Target { return Shade(i, 1); }
            ENDCG
        }
    }
    FallBack Off
}
