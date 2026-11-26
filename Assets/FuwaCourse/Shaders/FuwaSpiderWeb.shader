// 巨大クモの巣を計算で描くシェーダー（画像を使わない）。平らな Quad に貼る。
// 穴の半径 _HoleRadius を変えると、放射の糸・輪っかが穴に合わせて描き直される（FuwaWebHole が動かす）。
// 座標はメートル：_Size = 板の大きさ(幅,高さ)、_HoleCenter = 板の中心から見た穴の中心(x,y)。
// 巣の外側は、放射の糸の先（板の中でいびつに長さを変えた点）を結んだ「いびつな多角形」の糸で終わる。
// 放射の糸の角度と先端の長さは sin だけで決めているので、C#（FuwaWebAnchors）からも同じ位置を計算できる。
// カメラから _FadeNear 以内は見えず、_FadeFar までにだんだん見えてくる（通り抜ける時に視界をふさがない）。
Shader "FuwaCourse/SpiderWeb"
{
    Properties
    {
        _Color ("Color", Color) = (0.42, 0.40, 0.52, 0.94)
        _Size ("Board Size (m)", Vector) = (9.6, 6, 0, 0)
        _HoleCenter ("Hole Center (m)", Vector) = (0, -1.3, 0, 0)
        _HoleRadius ("Hole Radius (m)", Float) = 0.62
        _HoleRadiusMax ("Largest Hole Radius (m, for ring cut)", Float) = 0.85
        _TearA ("Tear Start (m)", Vector) = (0, 0, 0, 0)
        _TearB ("Tear End (m)", Vector) = (0, 0, 0, 0)
        _TearW ("Tear Half Width (m, 0 = none)", Float) = 0
        _LineWidth ("Line Width (m)", Float) = 0.0165
        _Spokes ("Spokes", Float) = 28
        _RingStart ("First Ring Gap (m)", Float) = 0.12
        _RingGrow ("Ring Gap Growth", Float) = 0.17
        _Seed ("Seed", Float) = 11
        _OuterMin ("Outer Edge Min (x board edge)", Range(0.3, 1)) = 0.78
        _OuterMax ("Outer Edge Max (x board edge)", Range(0.3, 1)) = 0.97
        _Round ("Corner Rounding (ellipse scale)", Float) = 1.15
        _FrameN ("Frame Corners (0 = 従来の外形)", Float) = 0
        _F01 ("Frame Corner 0,1 (穴の中心基準 m)", Vector) = (0, 0, 0, 0)
        _F23 ("Frame Corner 2,3", Vector) = (0, 0, 0, 0)
        _F45 ("Frame Corner 4,5", Vector) = (0, 0, 0, 0)
        _F67 ("Frame Corner 6,7", Vector) = (0, 0, 0, 0)
        _FadeNear ("Camera Fade: invisible within (m)", Float) = 0.45
        _FadeFar ("Camera Fade: fully visible beyond (m)", Float) = 1.0
        _FarFadeStart ("Far Fade: start (m, 0=off)", Float) = 0
        _FarFadeEnd ("Far Fade: end (m)", Float) = 0
        _FarFadeMin ("Far Fade: alpha at end", Range(0,1)) = 0
        _CoreColor ("糸の色（中心）", Color) = (0.93, 0.91, 1.0, 1)
        _OutlineColor ("糸の縁取り", Color) = (0.12, 0.09, 0.18, 0.85)
        _OutlineWidth ("縁取りの太さ (x 糸の太さ)", Float) = 0.7
        _EdgeWidth ("いちばん外側の糸の太さ (x 糸の太さ)", Float) = 1.7
        _EdgeGlow ("近づいた時の明滅の色 (aが強さ)", Color) = (1, 0.86, 0.25, 0.75)
        _EdgeHalo ("外側の糸のまわりのにじみ (m)", Float) = 0.03
        _EdgePulseSpeed ("明滅の速さ", Float) = 2.2
        _PulseNear ("この距離より近いと明滅がいちばん強い (m)", Float) = 3
        _PulseFar ("この距離より遠いと明滅しない (m)", Float) = 7
        _DepthTint ("遠い巣の色（空に寄せる）", Color) = (0.66, 0.80, 0.98, 1)
        _DepthNear ("ここまではくっきり (m)", Float) = 4
        _DepthFar ("ここでいちばんかすむ (m)", Float) = 20
        _DepthFade ("いちばん遠い時の濃さ", Range(0, 1)) = 0.45
        _DewChance ("夜露のつぶの割合", Range(0, 1)) = 0.3
        _DewSize ("夜露のつぶの半径 (m)", Float) = 0.012
        _DewColor ("夜露の色", Color) = (1, 0.97, 1, 1)
        _TwinkleSpeed ("キラッとする速さ", Float) = 1.3
        _Spiral ("輪をらせんにする (0/1)", Float) = 1
        _Sag ("横糸のたるみ（中心側へ弓なり、糸の長さの何割）", Range(0, 0.2)) = 0.07
        _SpacingRef ("横糸の間隔を縮め始める糸の長さ m（0=しない）", Float) = 0
        _GapChance ("らせんの糸が切れている割合", Range(0, 0.3)) = 0
        _SplitChance ("途中から生える放射の糸の割合（横糸が折れて途切れて見えるので0推奨）", Range(0, 0.6)) = 0
        _SplitStart ("途中から生える糸の始まり（外周までの何割）", Range(0, 0.9)) = 0.4
        _RimColor ("Hole Rim Color", Color) = (0.95, 0.95, 1.0, 1.0)
        _RimWidth ("Hole Rim Width (x Line Width)", Float) = 2.6
        _RimGlow ("Hole Rim Glow Width (m)", Float) = 0.05
        _RimGlowAlpha ("Hole Rim Glow Alpha", Range(0, 1)) = 0.3
        [NoScaleOffset] _SpokeTex ("縦糸の表（自動で作る：cos, sin, 先端までの長さ）", 2D) = "black" {}
        _SpokeTexOn ("縦糸の表を使う（自動）", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            // 糸の所だけ奥行きを書く（あとから描く半透明のエフェクト＝重力エリアなどが、手前の糸の上に出ないように）
            ZWrite On
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float4 _Size, _HoleCenter;
            float4 _TearA, _TearB; float _TearW;
            float _HoleRadius, _HoleRadiusMax, _LineWidth, _Spokes, _RingStart, _RingGrow, _Seed, _OuterMin, _OuterMax, _Round;
            float _FadeNear, _FadeFar, _FarFadeStart, _FarFadeEnd, _FarFadeMin;
            float _FrameN;
            float4 _F01, _F23, _F45, _F67;
            fixed4 _RimColor;
            float _RimWidth, _RimGlow, _RimGlowAlpha;
            fixed4 _CoreColor, _OutlineColor, _DewColor;
            fixed4 _EdgeGlow;
            float _EdgeHalo, _EdgePulseSpeed, _PulseNear, _PulseFar;
            fixed4 _DepthTint;
            float _DepthNear, _DepthFar, _DepthFade;
            float _OutlineWidth, _EdgeWidth, _DewChance, _DewSize, _TwinkleSpeed;
            float _Spiral, _GapChance, _SplitChance, _SplitStart, _Sag, _SpacingRef;
            sampler2D_float _SpokeTex;   // SpiderWebSpokeTex.cs が作る。横 = 縦糸の本数
            float _SpokeTexOn;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 m : TEXCOORD0; float3 wpos : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.m = (v.uv - 0.5) * _Size.xy;   // meters from board center
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos); return o;
            }

            float hash(float2 p) { return frac(sin(dot(p + _Seed, float2(127.1, 311.7))) * 43758.5453); }

            float spokeIndex(float i) { float n = _Spokes; return fmod(fmod(i, n) + n, n); }

            // strand i angle (with jitter). sin only, so C# can reproduce it
            float spokeAngle(float i)
            {
                float k = spokeIndex(i);
                float j = sin(k * 1.93 + _Seed * 0.77) * 0.18;
                return (i + j) * 6.2831853 / _Spokes;
            }

            // 枠の糸（多角形）の角 k（穴の中心基準）。_FrameN 個を順に結ぶ
            float2 frameCorner(int k)
            {
                // k は 0〜7（呼ぶ側で n 以上は 0 に戻す）。整数の割り算は遅いので使わない
                float4 v = k < 2 ? _F01 : (k < 4 ? _F23 : (k < 6 ? _F45 : _F67));
                return (k & 1) == 0 ? v.xy : v.zw;
            }

            float cross2f(float2 a, float2 b) { return a.x * b.y - a.y * b.x; }

            // 穴の中心から方向 d に進んで、枠の多角形に当たるまでの距離
            float frameRayDist(float2 d)
            {
                float best = 1e4;
                int n = (int)_FrameN;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    if (k < n)
                    {
                        float2 A = frameCorner(k), B = frameCorner(k + 1 < n ? k + 1 : 0);
                        float2 e = B - A;
                        float den = cross2f(d, e);
                        if (abs(den) > 1e-6)
                        {
                            float t = cross2f(A, e) / den;
                            float u = cross2f(A, d) / den;
                            if (t > 0 && u >= -1e-4 && u <= 1 + 1e-4) best = min(best, t);
                        }
                    }
                }
                return best;
            }

            // how far strand i reaches: a jittered fraction of the distance to the board edge
            float outerRadius(float i)
            {
                if (_FrameN > 2.5)
                {
                    float af = spokeAngle(i);
                    return frameRayDist(float2(cos(af), sin(af)));
                }
                float k = spokeIndex(i);
                float a = spokeAngle(i);
                float2 d = float2(cos(a), sin(a));
                float2 hs = _Size.xy * 0.5;
                float2 c = _HoleCenter.xy;
                float tx = abs(d.x) > 1e-4 ? ((d.x > 0 ? hs.x : -hs.x) - c.x) / d.x : 1e4;
                float ty = abs(d.y) > 1e-4 ? ((d.y > 0 ? hs.y : -hs.y) - c.y) / d.y : 1e4;
                // ゆるやかなうねり＋少しのばらつき（隣同士で極端に変わるとトゲトゲの星形になるので）
                float w = 0.65 * sin(k * 0.7 + _Seed * 0.9) + 0.35 * sin(k * 2.399 + _Seed * 1.3);
                float f = lerp(_OuterMin, _OuterMax, 0.5 + 0.5 * w);
                // 板の四隅までは伸ばさず、角を丸く（楕円）して「張った巣」っぽい外形に
                float2 he = hs + abs(c);
                float te = 1.0 / sqrt((d.x * d.x) / (he.x * he.x) + (d.y * d.y) / (he.y * he.y));
                return min(min(tx, ty), te * _Round) * f;
            }

            // ring k radius on strand i (with ±5% wobble)
            // らせんの時は、糸 i を1本進むごとに 1/本数 だけ外へずれる（1周で輪1つ分。本物の巣の「横糸」）
            // sc：横糸の間隔の倍率。穴から枠まで近い縦糸（穴の下側など）ほど間隔をつめて、短い所にも横糸が入るように
            float spacingScale(float R)
            {
                return _SpacingRef > 0 ? clamp((R - max(_HoleRadiusMax, _HoleRadius)) / _SpacingRef, 0.3, 1) : 1;
            }
            float ringRadiusAt(float k, float i, float holeR, float sc)
            {
                float a = _RingStart, b = _RingGrow;
                float kc = k + (_Spiral > 0.5 ? i / _Spokes : 0);
                float s = (a / b) * (pow(1 + b, kc) - 1) * sc;
                return (holeR + s) * lerp(0.95, 1.05, hash(float2(spokeIndex(i), floor(kc + 1e-3))));
            }
            float ringRadius(float k, float i, float sc) { return ringRadiusAt(k, i, _HoleRadius, sc); }

            float segDist(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-6));
                return length(p - a - ab * t);
            }

            float cross2(float2 a, float2 b) { return a.x * b.y - a.y * b.x; }

            // 2026-11-26 軽量化（PICO4で巣に近づくとFPSが落ちる：板が画面いっぱいになると全ピクセルでこの計算が走るので）
            //  見た目は前と同じ。縦糸の角度・先端は1ピクセルで4本ぶんだけ計算して使い回す／見えない所は先に捨てる
            float outerRadiusAD(float i, float a, float2 d)
            {
                if (_FrameN > 2.5) return frameRayDist(d);
                float k = spokeIndex(i);
                float2 hs = _Size.xy * 0.5;
                float2 c = _HoleCenter.xy;
                float tx = abs(d.x) > 1e-4 ? ((d.x > 0 ? hs.x : -hs.x) - c.x) / d.x : 1e4;
                float ty = abs(d.y) > 1e-4 ? ((d.y > 0 ? hs.y : -hs.y) - c.y) / d.y : 1e4;
                float w = 0.65 * sin(k * 0.7 + _Seed * 0.9) + 0.35 * sin(k * 2.399 + _Seed * 1.3);
                float f = lerp(_OuterMin, _OuterMax, 0.5 + 0.5 * w);
                float2 he = hs + abs(c);
                float te = 1.0 / sqrt((d.x * d.x) / (he.x * he.x) + (d.y * d.y) / (he.y * he.y));
                return min(min(tx, ty), te * _Round) * f;
            }

            // 輪 k の糸 i の位置を「穴からの距離 s」と「揺らぎ w」に分ける：半径 = (穴の半径 + s) * w
            void ringParts(float k, float i, float sc, out float s, out float w)
            {
                float a = _RingStart, b = _RingGrow;
                float kc = k + (_Spiral > 0.5 ? i / _Spokes : 0);
                s = (a / b) * (pow(1 + b, kc) - 1) * sc;
                w = lerp(0.95, 1.05, hash(float2(spokeIndex(i), floor(kc + 1e-3))));
            }

            float4 spokeTex(float i) { return tex2Dlod(_SpokeTex, float4((spokeIndex(i) + 0.5) / _Spokes, 0.5, 0, 0)); }

            float spokeDist(float2 p, float idx, float2 dir, float Ro)
            {
                float along = dot(p, dir);
                float st = _HoleRadius * 0.9;
                if (_SplitChance > 0 && hash(float2(spokeIndex(idx) + 0.75, 3.3)) < _SplitChance)
                    st = lerp(_HoleRadius, Ro, _SplitStart * lerp(0.7, 1.2, hash(float2(spokeIndex(idx), 7.7))));
                return (along > st && along < Ro) ? abs(cross2(p, dir)) : 1e3;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);   // VRで左右の目それぞれのカメラ位置を使う
                float2 p = i.m - _HoleCenter.xy;
                float r = length(p);
                float px = max(fwidth(r), 1e-5);

                // カメラのすぐ近く（通り抜ける時に消える所）と、遠くで薄くして消える所は、計算する前に捨てる
                // （最後の不透明度はこの値以下なので、ここで 0.03 未満なら最後の clip でも必ず捨てられる）
                float camD = distance(i.wpos, _WorldSpaceCameraPos);
                float camFade = smoothstep(_FadeNear, max(_FadeFar, _FadeNear + 0.01), camD);
                if (_FarFadeEnd > _FarFadeStart) camFade *= lerp(1, _FarFadeMin, smoothstep(_FarFadeStart, _FarFadeEnd, camD));
                if (camFade < 0.03) discard;

                float hw = _LineWidth * 0.5;
                float dIn = abs(r - _HoleRadius);   // rim around the hole（＋放射の糸）
                float dEdge = 1e3;  // the outline thread itself
                float dRing = 1e3;  // rings (外周のそばの輪は描かない)
                float dDew = 1e3, dewH = 0;   // 夜露のつぶ（放射の糸と輪の交点の一部）
                bool inside = false;

                float ang = atan2(p.y, p.x);
                if (ang < 0) ang += 6.2831853;
                float n = _Spokes;
                float fi = floor(ang / 6.2831853 * n);

                bool frame = _FrameN > 2.5;
                if (frame)
                {
                    // 枠の多角形：直線の枠糸そのものを描き、内側判定も多角形で
                    int fn = (int)_FrameN;
                    bool allIn = true;
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        if (k < fn)
                        {
                            float2 A = frameCorner(k), B = frameCorner(k + 1 < fn ? k + 1 : 0);
                            dEdge = min(dEdge, segDist(p, A, B));
                            if (cross2(B - A, p - A) * cross2(B - A, -A) < 0) allIn = false;
                        }
                    }
                    inside = allIn;
                }

                // 枠の外は枠の糸だけ（縦糸・輪の計算はしない）
                [branch] if (!frame || inside)
                {
                    // この角度のまわりの縦糸4本（fi-1 〜 fi+2）を1回だけ計算
                    float2 dS0, dS1, dS2, dS3;
                    float rS0, rS1, rS2, rS3;
                    [branch] if (_SpokeTexOn > 0.5)
                    {
                        // 表から読む（縦糸ごとの値は巣ごとに決まっているので、C# で先に計算してある）
                        float4 t0 = spokeTex(fi - 1), t1 = spokeTex(fi), t2 = spokeTex(fi + 1), t3 = spokeTex(fi + 2);
                        dS0 = t0.xy; dS1 = t1.xy; dS2 = t2.xy; dS3 = t3.xy;
                        rS0 = t0.z; rS1 = t1.z; rS2 = t2.z; rS3 = t3.z;
                    }
                    else
                    {
                        float aS0 = spokeAngle(fi - 1), aS1 = spokeAngle(fi), aS2 = spokeAngle(fi + 1), aS3 = spokeAngle(fi + 2);
                        dS0 = float2(cos(aS0), sin(aS0)); dS1 = float2(cos(aS1), sin(aS1));
                        dS2 = float2(cos(aS2), sin(aS2)); dS3 = float2(cos(aS3), sin(aS3));
                        rS0 = outerRadiusAD(fi - 1, aS0, dS0); rS1 = outerRadiusAD(fi, aS1, dS1);
                        rS2 = outerRadiusAD(fi + 1, aS2, dS2); rS3 = outerRadiusAD(fi + 2, aS3, dS3);
                    }

                    // which sector are we in (between spoke s0 and s1)?
                    bool m1 = cross2(dS1, p) >= 0 && cross2(p, dS2) >= 0;
                    bool m2 = cross2(dS2, p) >= 0 && cross2(p, dS3) >= 0;
                    float s0 = fi - 1 + (m2 ? 2 : (m1 ? 1 : 0));
                    float2 d0 = m2 ? dS2 : (m1 ? dS1 : dS0), d1 = m2 ? dS3 : (m1 ? dS2 : dS1);
                    float R0 = m2 ? rS2 : (m1 ? rS1 : rS0), R1 = m2 ? rS3 : (m1 ? rS2 : rS1);

                    if (!frame)
                    {
                        float2 P0 = d0 * R0, P1 = d1 * R1;
                        // inside the outline = same side of edge P0-P1 as the center
                        float side = cross2(P1 - P0, p - P0);
                        float sideC = cross2(P1 - P0, -P0);
                        inside = side * sideC >= 0;
                        dEdge = segDist(p, P0, P1);
                    }

                    [branch] if (inside && r > _HoleRadius)
                    {
                        // spokes near this angle, each only up to its own tip
                        dIn = min(dIn, min(min(spokeDist(p, fi - 1, dS0, rS0), spokeDist(p, fi, dS1, rS1)),
                                           min(spokeDist(p, fi + 1, dS2, rS2), spokeDist(p, fi + 2, dS3, rS3))));
                        // rings between the two spokes of this sector
                        float sc0 = spacingScale(R0), sc1 = spacingScale(R1);
                        float wAng = saturate(acos(clamp(dot(p / r, d0), -1, 1)) / max(acos(clamp(dot(d0, d1), -1, 1)), 1e-4));
                        float sd = (r - _HoleRadius) / lerp(sc0, sc1, wAng);
                        float kf = log(1 + sd * _RingGrow / _RingStart) / log(1 + _RingGrow);
                        float k0 = floor(kf - (_Spiral > 0.5 ? s0 / _Spokes : 0));
                        float hm = max(_HoleRadiusMax, _HoleRadius);
                        float scMin = min(sc0, sc1);
                        [unroll] for (int kk = -1; kk <= 2; kk++)   // 輪は糸ごとに±5%ずれるので、1つ内側の輪も見る
                        {
                            float k = max(1, k0 + kk);
                            float sA, wA, sB, wB;
                            ringParts(k, s0, sc0, sA, wA);
                            ringParts(k, s0 + 1, sc1, sB, wB);
                            float ra = (_HoleRadius + sA) * wA, rb = (_HoleRadius + sB) * wB;
                            // 外周の糸のすぐそばの輪は描かない（外周との間に細かい切れ端ができるので）。
                            // 判定は「穴がいちばん大きい時の輪の位置」で決めるので、穴が呼吸しても輪が出たり消えたりしない。
                            float gap = _RingStart * pow(1 + _RingGrow, k) * scMin;
                            float room = min(R0 - (hm + sA) * wA, R1 - (hm + sB) * wB);
                            bool show = room > gap * 0.6;
                            // ところどころ切れている横糸
                            if (_GapChance > 0)
                            {
                                float kc0 = k + (_Spiral > 0.5 ? s0 / _Spokes : 0);
                                if (hash(float2(spokeIndex(s0) + 0.25, floor(kc0 + 1e-3) * 1.7)) < _GapChance) show = false;
                            }
                            if (show)
                            {
                                // 横糸は少したるんで中心側へ弓なりに（まっすぐな多角形だと作り物っぽいので）
                                float2 A = d0 * ra, B = d1 * rb, AB = B - A;
                                float tt = saturate(dot(p - A, AB) / max(dot(AB, AB), 1e-6));
                                float2 outN = normalize(A + B);
                                dRing = min(dRing, segDist(p + outN * (_Sag * length(AB) * 4 * tt * (1 - tt)), A, B));
                                if (_DewChance > 0)
                                {
                                    float h0 = hash(float2(spokeIndex(s0) + 0.5, k)), h1 = hash(float2(spokeIndex(s0 + 1) + 0.5, k));
                                    float q0 = length(p - A), q1 = length(p - B);
                                    if (h0 < _DewChance && q0 < dDew) { dDew = q0; dewH = h0; }
                                    if (h1 < _DewChance && q1 < dDew) { dDew = q1; dewH = h1; }
                                }
                            }
                        }
                    }
                }

                float d = inside ? min(dIn, dRing) : 1e3;
                float lw = px * 1.2;
                // 白っぽい糸＋暗い縁取り（空の上でも道の上でも見える）。いちばん外側の糸（当たり判定のふち）だけ少し太く
                float ow = hw * _OutlineWidth;
                float hwE = hw * _EdgeWidth, owE = hwE * _OutlineWidth * 0.8;
                float coreA = max(1 - smoothstep(hw - lw * 0.5, hw + lw * 0.5, d), 1 - smoothstep(hwE - lw * 0.5, hwE + lw * 0.5, dEdge));
                float lineA = max(1 - smoothstep(hw + ow - lw * 0.5, hw + ow + lw * 0.5, d), 1 - smoothstep(hwE + owE - lw * 0.5, hwE + owE + lw * 0.5, dEdge));
                if (r < _HoleRadius - hw - ow - lw) { coreA = 0; lineA = 0; }
                // 巣の糸がうっすら明滅（色が少しだけ光る）。いちばん外側の枠はまわりにもにじむ
                // 近づいた巣だけ明滅する（遠くの巣は光らない）
                float pulse = (0.5 + 0.5 * sin(_Time.y * _EdgePulseSpeed)) * (1 - smoothstep(_PulseNear, max(_PulseFar, _PulseNear + 0.01), camD));
                float edgeCore = 1 - smoothstep(hwE - lw * 0.5, hwE + lw * 0.5, dEdge);
                fixed3 core = lerp(_CoreColor.rgb, _EdgeGlow.rgb, pulse * _EdgeGlow.a * lerp(0.75, 1.0, edgeCore));   // 巣の糸ぜんぶ（外側の枠は少し強め）
                fixed3 col = lerp(_OutlineColor.rgb, core, coreA);
                float a = max(coreA * _CoreColor.a, lineA * _OutlineColor.a);
                float halo = (1 - smoothstep(hwE + owE, hwE + owE + max(_EdgeHalo, px), dEdge)) * pulse * _EdgeGlow.a * 0.35;
                col = lerp(col, _EdgeGlow.rgb, saturate(halo * (1 - a) / max(max(a, halo), 1e-4)));
                a = max(a, halo);
                // 夜露のつぶ：交点にごく小さい光るしずく、ときどきキラッ（遠くでは糸より少し太いくらい）
                if (inside)
                {
                    float dewR = max(_DewSize, hw * 1.7);   // 糸よりひとまわり太い玉
                    float tw = pow(saturate(sin(_Time.y * _TwinkleSpeed + dewH * 97.0)), 16);
                    float dewA = 1 - smoothstep(dewR - lw * 0.5, dewR + lw * 0.5, dDew);
                    float haloA = (1 - smoothstep(dewR, dewR * (1.8 + 1.5 * tw), dDew)) * 0.35 * tw;
                    col = lerp(col, _DewColor.rgb * (0.9 + 0.4 * tw), saturate(max(dewA, haloA)));
                    a = max(a, max(dewA * _DewColor.a, haloA));
                }
                a *= _Color.a;
                // 2026-11-14 裂け目（2–5 てじゃ「1枚ものを一部を裂くイメージ」「1〜2ライン裂けてる感じ」「斜めもあり」）：
                //  _TearA→_TearB の線のまわり _TearW の中は糸を消す。ふちは糸ごとに少しずつずらしてギザギザに（ちぎれた感じ）
                float tearRimA = 0;
                if (_TearW > 0.001)
                {
                    float2 ta = _TearA.xy, tb = _TearB.xy, ab = tb - ta;
                    float tt = saturate(dot(i.m - ta, ab) / max(dot(ab, ab), 1e-6));
                    float dT = length(i.m - (ta + ab * tt));
                    float jag = 0.82 + 0.18 * sin(tt * 37.0 + _Seed) * sin(tt * 11.0 + _Seed * 1.7);
                    float endTaper = saturate(min(tt, 1 - tt) * 6.0 + 0.35);   // 両はしは少し細く
                    float tw_ = _TearW * jag * endTaper;
                    a *= smoothstep(tw_ - px * 1.5, tw_ + px * 1.5, dT);
                    // 裂け目のふちをうっすら光らせる（どこが裂けてるか分かる目印。穴のふちと同じ色）
                    float hwT = hw * _RimWidth * 0.6;
                    tearRimA = (1 - smoothstep(hwT - px, hwT + px, abs(dT - tw_))) * 0.75 + (1 - smoothstep(0, max(_RimGlow, 1e-4) * 1.5, abs(dT - tw_))) * _RimGlowAlpha * 0.6;
                }
                // 穴のふち：太く明るい糸＋まわりにうっすら光（穴の場所と開き具合が分かる目印）
                float dRim = abs(r - _HoleRadius);
                float hwR = hw * _RimWidth;
                float rimA = 1 - smoothstep(hwR - lw * 0.5, hwR + lw * 0.5, dRim);
                float glowA = (1 - smoothstep(hwR, hwR + max(_RimGlow, 1e-4), dRim)) * _RimGlowAlpha;
                if (_HoleRadius < 0.01) { rimA = 0; glowA = 0; }
                float rimTotal = max(max(rimA * _RimColor.a, glowA), tearRimA * _RimColor.a);
                float outA = max(a, rimTotal);
                col = lerp(col, _RimColor.rgb, saturate(rimTotal / max(outA, 1e-4)));
                // 奥行きの目印：遠い巣ほど空の色に寄って薄くなる（手前の巣がくっきり）
                float far = smoothstep(_DepthNear, max(_DepthFar, _DepthNear + 0.01), camD);
                col = lerp(col, _DepthTint.rgb, far * 0.65);
                outA *= lerp(1, _DepthFade, far);
                // 通り抜ける時に見づらくないよう、カメラのすぐ近くの部分はスゥっと消す／遠くの巣はうすく（camFade は最初に計算済み）
                outA *= camFade;
                clip(outA - 0.03);   // 糸のない所は奥行きも書かない
                { fixed4 fogc__ = fixed4(col, outA); UNITY_APPLY_FOG(i.fogCoord, fogc__); return fogc__; }
            }
            ENDCG
        }
    }
}
