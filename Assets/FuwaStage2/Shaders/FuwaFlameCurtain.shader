// 2–2 ジグザグの炎の壁（2026-10-31）。ぼやけた粒のかたまりの代わりに、くっきりしたトゥーンの炎の板。uv.x は長さ（m）、uv.y は下 0〜上 1。
// 模様は上へ流れて、上にいくほど切れて細くなる。光を受けず自分で光る。深度に書かない（うそ影や風の模様が炎に乗らないように、影のパスもなし）
Shader "FuwaCourse/FlameCurtain"
{
    Properties
    {
        [HDR] _Hot ("内側の色", Color) = (1.2, 0.86, 0.3, 1)
        [HDR] _Mid ("まん中の色", Color) = (1.2, 0.46, 0.07, 1)
        [HDR] _Tip ("先の色", Color) = (1.0, 0.15, 0.04, 1)
        _Speed ("動きの速さ", Float) = 1
        _Height ("炎の高さの割合", Range(0.3,1)) = 1
        _Sway ("ゆらぎ", Range(0,1)) = 0.35
        _Base ("舌の谷の高さ（小さいほど舌が長い）", Range(0,1)) = 0.64
        _BandScale ("色の境目の波の細かさ", Float) = 1
        _BandByH ("色の帯も帯ごとの高さに合わせる（縦線が気にならない物だけ）", Range(0,1)) = 0
        _BandLow ("下のふちの色の広さ", Range(0,0.5)) = 0.1
        _BandHot ("内側の色の上のはし", Range(0,1)) = 0.42
        _BandMid ("まん中の色の上のはし", Range(0,2)) = 0.74
        _Lean ("上のほうが左右にゆれる大きさ（オブジェクトの長さ。2026-11-01 てじゃ「上下ゆれしかない」）", Range(0,1)) = 0
        _LowScale ("下のはしの舌の長さ", Range(0,1)) = 1
        _BandSoft ("色の境目のぼかし（2026-10-31 てじゃ「境目がパッキリ」）", Range(0.001,0.4)) = 0.001
        _Rim ("炎の形にそった外のふちの太さ（高さの割合。頂点カラー G が 1 の帯だけ）", Range(0,0.2)) = 0
        [NoScaleOffset] _FlowTex ("上へ流す模様（R）", 2D) = "gray" {}
        _Flow ("模様を上へ流して形を削る（2026-11-01 てじゃ「揺れるだけじゃ違和感」）", Range(0,1)) = 0
        _FlowSpeed ("流れる速さ", Float) = 0.45
        _FlowX ("模様の細かさ 横", Float) = 0.4
        _FlowY ("模様の細かさ 縦", Float) = 2
        _FlowBody ("炎の体の濃さ（大きいほど上まで埋まる）", Float) = 1.25
        _FlowAmp ("削り方の強さ", Float) = 2
        _FlowCore ("内側の色の深さ", Float) = 1
        _FlowTop ("上のふちの大きな波（0〜1。てじゃ「上辺が平らすぎる」）", Range(0,1)) = 0
        _Burn ("根元から燃えつきる（0 ふつう〜1 消える。火炎放射の余韻）", Range(0,1)) = 0
        _BandSelf ("色の帯を炎の形に合わせる（動いても外のふちの色が切れない。板の炎の縦線が気にならない物だけ）", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        AlphaToMask On   // ふちをやわらかく（MSAA があるとき）
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "FuwaFireFar.cginc"

            half4 _Hot, _Mid, _Tip;
            float _Speed, _Height, _Sway, _Base, _BandScale, _BandByH, _BandLow, _BandHot, _BandMid, _BandSelf, _BandSoft, _Rim, _LowScale, _Lean, _Flow, _FlowSpeed, _FlowX, _FlowY, _FlowBody, _FlowAmp, _FlowCore, _FlowTop, _Burn;
            sampler2D _FlowTex;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float h : TEXCOORD2; float lx : TEXCOORD3; float3 wp : TEXCOORD4; float rim : TEXCOORD5; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                // 炎の形（舌）は上下にしか動かないので、形ごと左右へゆらす。根元は動かず上ほど大きく。帯ごとにずらしてばらばらに
                if (_Lean > 0.0)
                {
                    float lt = _Time.y * _Speed, ph = v.uv.x * 0.35, hh = v.uv.y * v.uv.y;
                    v.vertex.x += (sin(lt * 1.9 + ph) + 0.55 * sin(lt * 4.3 + ph * 2.3 + 1.1)) * _Lean * hh;
                    v.vertex.z += (sin(lt * 1.5 + ph * 1.4 + 2.0) + 0.55 * sin(lt * 3.7 + ph * 1.9)) * _Lean * hh;
                }
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.rim = v.color.g;
                o.h = v.color.r;   // その場所の炎の高さの割合（帯のはしは低い）
                o.lx = v.vertex.x;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed; float y = i.uv.y; half far = FuwaFireFar(i.wp);
                // 上へ流れるうねり（上ほど大きく）
                float x = i.uv.x + (sin(y * 6.0 - t * 3.2 + i.uv.x * 1.1) * 0.55 + sin(y * 11.0 - t * 5.0 + i.uv.x * 2.3) * 0.22) * _Sway * (0.2 + y * 2.0);   // ゆらぎはゆっくり控えめに（2026-10-31 てじゃ「ゆらゆらがくどい」）
                // 炎の舌：|sin| の山をかさねた高さ（テクスチャを使わないのでふちがなめらか）
                float a = 1.0 - abs(sin(x * 1.25 + t * 0.9)), b = 1.0 - abs(sin(x * 2.9 - t * 1.4 + 1.3)), c3 = 1.0 - abs(sin(x * 6.1 + t * 2.1 + 2.7));
                float detail = 1.0 - far * 0.85;   // 遠いほど細かい舌をなくす
                float top = (_Base + (1.0 - _Base) * (0.6 * a * a + (0.28 * b + 0.12 * c3) * detail + 0.2 * (1.0 - detail))) * _Height * i.h;   // 低い所でも当たりの高さの 2/3 近くまで（低くすると、炎の上の何もない所でアウトになって見える）
                float dTop = top - y; half alpha = saturate(dTop / max(fwidth(dTop), 0.0001) + 0.5);
                // 下のはしも舌の形で切る（まっすぐ切れて板に見えないように）
                float low = (0.09 * (1.0 - abs(sin(x * 1.9 - t * 0.7))) + 0.05 * (1.0 - abs(sin(x * 4.7 + t)))) * _LowScale + 0.005;
                float dLow = y - low; alpha = min(alpha, saturate(dLow / max(fwidth(dLow), 0.0001) + 0.5));
                if (_Flow > 0.5)
                {
                    // 模様が下から上へ流れて形を削る。先はちぎれて、色の境目も形といっしょに上がっていく
                    float px = i.uv.x * _FlowX, py = y * _FlowY;   // うねりは足さない（てじゃ「うねうねしてる毛みたい」）。模様は縦横の大きさをそろえて細い筋にしない
                    // 場所ごとに流れを前後させて、全体がそろって動かないように（てじゃ「動きが揃ってて怖い」）
                    float ph = sin(t * 0.6 + i.uv.x * 0.7) * 0.2 + sin(t * 1.13 + i.uv.x * 1.9) * 0.1;
                    float n = tex2D(_FlowTex, float2(px * 0.35, py * 0.35 - t * _FlowSpeed + ph)).r * 0.82 + tex2D(_FlowTex, float2(px * 0.9 + 0.37, py * 0.9 - t * _FlowSpeed * 1.5 + ph)).r * 0.18;
                    float tv = 1.0 - _FlowTop * (0.5 + 0.5 * sin(x * 0.83 + t * 0.31 + 2.0 * sin(x * 0.29 - t * 0.13)));
                    float e = y / max(i.h * _Height * tv, 0.01);
                    float v = (1.0 - e) * _FlowBody + (n - 0.5) * _FlowAmp * (0.2 + e * 1.15) * (1.0 - far * 0.6) - smoothstep(0.85, 1.0, y) * 1.5 - smoothstep(y - 0.15, y + 0.15, _Burn * 1.3) * 2.0 - _Burn * 0.8;
                    float fa = saturate(v / max(fwidth(v), 0.0001) + 0.5); fa = min(fa, saturate(dLow / max(fwidth(dLow), 0.0001) + 0.5));
                    clip(fa - 0.01);
                    float fu = 1.0 - saturate(v / _FlowCore);
                    half3 fc = lerp(_Hot.rgb, _Mid.rgb, smoothstep(_BandHot - _BandSoft, _BandHot + _BandSoft, fu));
                    fc = lerp(fc, _Tip.rgb, smoothstep(_BandMid - _BandSoft, _BandMid + _BandSoft, fu));
                    fc = FuwaFireCalm(fc, far);
                    fixed4 fo = fixed4(fc, fa);
                    UNITY_APPLY_FOG(i.fogCoord, fo);
                    return fo;
                }
                clip(alpha - 0.01);
                // 色の帯：内側が熱い。下のはしは赤。全部の帯で同じ形にする（壁の長さの向きの位置で決める）。
                //  帯ごとの形で色を決めると、横切る帯が細いオレンジの縦線に見えた（2026-10-31 てじゃ）
                float lx = i.lx * _BandScale; float bx = lx + (sin(y * 6.0 - t * 3.2 + lx * 1.1) * 0.55 + sin(y * 11.0 - t * 5.0 + lx * 2.3) * 0.22) * _Sway * (0.2 + y * 2.0);
                float ba = 1.0 - abs(sin(bx * 1.25 + t * 0.9)), bb = 1.0 - abs(sin(bx * 2.9 - t * 1.4 + 1.3)), bc = 1.0 - abs(sin(bx * 6.1 + t * 2.1 + 2.7));
                float btop = (_Base + (1.0 - _Base) * (0.6 * ba * ba + 0.28 * bb + 0.12 * bc)) * _Height * lerp(1.0, i.h, _BandByH);
                float blow = (0.09 * (1.0 - abs(sin(bx * 1.9 - t * 0.7))) + 0.05 * (1.0 - abs(sin(bx * 4.7 + t)))) * _LowScale + 0.005;
                btop = lerp(btop, top, _BandSelf); blow = lerp(blow, low, _BandSelf);
                float u = (y - blow) / max(btop - blow, 0.01);
                half3 c = lerp(_Hot.rgb, _Mid.rgb, smoothstep(_BandHot - _BandSoft, _BandHot + _BandSoft, u));
                c = lerp(c, _Tip.rgb, smoothstep(_BandMid - _BandSoft, _BandMid + _BandSoft, u));
                c = lerp(_Tip.rgb, c, smoothstep(_BandLow - _BandSoft, _BandLow + _BandSoft, u));
                // 外のふち：色の帯とは別に炎の形にそって塗る。帯は壁の線の位置で決めているので（縦線よけ）、動くと帯がふちまで出ることがある。
                //  横切る帯は G=0 で塗らない（塗ると低い帯のふちが赤い縦線に見えた）。fwidth で太さをそろえるとギザギザになった
                if (i.rim > 0.5) c = lerp(_Tip.rgb, c, smoothstep(_Rim * 0.4, _Rim * 1.6, dTop));
                c = FuwaFireCalm(c, far);
                fixed4 o = fixed4(c, alpha);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
