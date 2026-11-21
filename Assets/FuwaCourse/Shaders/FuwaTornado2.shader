// つむじかぜ（竜巻）の本体 その2（2026-11-21 てじゃ「1-1の竜巻がなんか竜巻っぽくない」）。
// 前の FuwaCourse/Tornado は「まっすぐな円すい＋ふちほど明るい」で、スポットライトの光やガラスのコップに見えていた。
//  ・形：根元をしぼって上でぐっと広がるラッパ形。ふちは回りながら上がる「もこもこ」の出っぱりで波打つ（頂点で動かす）
//  ・色：ふちを光らせるのをやめ、雲っぽいムラ（3D ノイズ）の白の濃淡が、ねじれながら回って上がる。輪郭はやわらかく消す
//  ・根元：少し濃く、砂ぼこりの色をまぜる
// メッシュは同じ FuwaTornadoFunnel（uv.x＝まわりの角度0〜1、uv.y＝高さ0〜1、半径は根元0.35→上1）
Shader "FuwaCourse/Tornado2"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color（ムラの暗いところ）", Color) = (0.78, 0.84, 0.95, 1)
        _DustColor ("Dust Color（根元）", Color) = (0.93, 0.88, 0.80, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.6
        _Narrow ("根元の半径の倍率（1 より大きいと根元が太く）", Range(0.2, 2)) = 0.55
        _Flare ("広がり方（大きいほど上で急に広がる）", Range(0.3, 3)) = 1.6
        _Lumpy ("もこもこの出っぱり（半径の割合）", Range(0, 0.5)) = 0.16
        _LumpFreq ("もこもこの細かさ", Float) = 4
        _Twist ("Twist (turns from bottom to top)", Float) = 0.9
        _Spin ("Spin Speed (回/秒)", Float) = 0.45
        _Rise ("Rise Speed", Float) = 0.5
        _NoiseScale ("ムラの細かさ", Float) = 2.2
        _Cloud ("ムラの強さ", Range(0, 1)) = 0.75
        _Streak ("筋の強さ", Range(0, 1)) = 0.35
        _Bands ("筋の本数", Float) = 6
        _EdgeSoft ("輪郭のぼかし", Range(0.01, 1)) = 0.35
        _BaseDust ("根元の濃さ", Range(0, 1)) = 0.5
        _SwayAmp ("Sway Amp (m at top)", Float) = 0.3
        _SwaySpeed ("Sway Speed", Float) = 0.9
        _BottomFade ("Bottom Fade", Range(0.01, 0.5)) = 0.08
        _TopFade ("Top Fade", Range(0.01, 0.6)) = 0.35
        _NearDist ("Fade Start Distance", Float) = 5
        _FarDist ("Fade End Distance", Float) = 13
        _FarAlpha ("Far Alpha", Range(0, 1)) = 0.45
        _CamFadeNear ("カメラの近くで消える：この距離(m)より近いと見えない", Float) = 0.5
        _CamFadeFar ("カメラの近くで消える：この距離(m)から全部見える（0 で消さない）", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _Color, _ShadeColor, _DustColor;
            half _Alpha, _Narrow, _Flare, _Lumpy, _LumpFreq, _Twist, _Spin, _Rise, _NoiseScale, _Cloud, _Streak, _Bands, _EdgeSoft, _BaseDust, _BottomFade, _TopFade;
            float _SwayAmp, _SwaySpeed, _NearDist, _FarDist;
            half _FarAlpha;
            float _CamFadeNear, _CamFadeFar;

            float hash31(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
            float vnoise(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                float n000 = hash31(i), n100 = hash31(i + float3(1, 0, 0)), n010 = hash31(i + float3(0, 1, 0)), n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1)), n101 = hash31(i + float3(1, 0, 1)), n011 = hash31(i + float3(0, 1, 1)), n111 = hash31(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y), lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
            }
            float fbm(float3 p) { return vnoise(p) * 0.55 + vnoise(p * 2.03 + 7.1) * 0.3 + vnoise(p * 4.1 + 3.7) * 0.15; }

            // まわりの角度（回転・ねじれ込み）
            float Ang(float u, float h) { return (u + h * _Twist - _Time.y * _Spin) * 6.2832; }

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; float lump : TEXCOORD3; UNITY_FOG_COORDS(4) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float h = v.uv.y;
                float3 p = v.vertex.xyz;
                // ラッパ形：根元をしぼり、上で広げる（元のメッシュの半径に倍率）
                float prof = lerp(_Narrow, 1, pow(saturate(h), _Flare));
                // もこもこ：回りながら上がる出っぱり（上ほど大きい）
                float a = Ang(v.uv.x, h);
                float lump = sin(a * 3 + h * 4 - _Time.y * _Rise * 3) * 0.6 + sin(a * 5 - h * 7 + 1.3) * 0.4;
                lump *= _Lumpy * (0.35 + 0.65 * h);
                p.xz *= prof * (1 + lump);
                // くねくね（上ほど大きい、根元はほぼ動かない）
                float t = _Time.y * _SwaySpeed; float k = h * h;
                float3 sway = float3(sin(t + h * 2.2) + 0.5 * sin(t * 1.7 + 1.3), 0, cos(t * 0.8 + h * 1.7) + 0.5 * sin(t * 1.3 + 0.4)) * k;
                float3 sc = float3(length(unity_ObjectToWorld._m00_m10_m20), 1, length(unity_ObjectToWorld._m02_m12_m22));
                p += sway * _SwayAmp / max(sc, 0.001);
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.uv = v.uv;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                o.lump = lump / max(_Lumpy, 0.001);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float h = i.uv.y;
                float a = Ang(i.uv.x, h);
                // 雲のムラ：角度を円にして 3D ノイズ（つなぎ目なし）。上へ流れる
                float rr = _NoiseScale * (0.6 + 0.6 * h);
                float3 q = float3(cos(a) * rr, h * _NoiseScale * 2.2 - _Time.y * _Rise, sin(a) * rr);
                float cloud = fbm(q);
                cloud = smoothstep(0.25, 0.75, cloud);
                // ねじれた筋（うすく）
                float band = pow(0.5 + 0.5 * sin(a * _Bands / 6.2832 * 6.2832 + h * 3), 3);
                float dens = lerp(1, cloud, _Cloud) * (1 - _Streak) + band * _Streak;
                dens = saturate(dens * (0.75 + 0.35 * i.lump));
                // 根元は濃く（砂ぼこり）
                float base = (1 - smoothstep(0.0, 0.35, h)) * _BaseDust;
                dens = saturate(dens + base * 0.6);
                // 輪郭（視線と面がすれる所）はやわらかく消す（前の「ふちが光る」の逆）
                float3 vd = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = abs(dot(normalize(i.wn), vd));
                float edge = smoothstep(0.0, _EdgeSoft, ndv);
                float fade = smoothstep(0, _BottomFade, h) * smoothstep(1, 1 - _TopFade, h);
                float alpha = dens * edge * fade * _Alpha;
                alpha *= facing > 0 ? 1 : 0.55;   // 奥の面はうすく
                float3 org = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                alpha *= lerp(1, _FarAlpha, smoothstep(_NearDist, _FarDist, distance(_WorldSpaceCameraPos, org)));
                if (_CamFadeFar > 0.001) alpha *= smoothstep(_CamFadeNear, max(_CamFadeFar, _CamFadeNear + 0.01), distance(_WorldSpaceCameraPos, i.wp));
                // 色：ムラの薄い所は少し青い影、上は明るく、根元は砂ぼこりの色
                float3 col = lerp(_ShadeColor.rgb, _Color.rgb, saturate(cloud * 0.8 + 0.2 + h * 0.25));
                col = lerp(col, _DustColor.rgb, base);
                fixed4 c = fixed4(col, saturate(alpha));
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
