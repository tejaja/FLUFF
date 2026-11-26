// 2026-11-26 クモの巣の「本物の糸」版（てじゃ「PICO4でクモの巣を至近距離で見るとFPSが落ちる」→ メッシュ化）
// FuwaCourse/SpiderWeb（板1枚に1ピクセルごと計算で描く）と同じ見た目を、糸1本ずつの細い帯のメッシュで描く。
// 糸のあるピクセルしか描かないので、近づいて画面いっぱいになっても軽い。
// メッシュは SpiderWebLinesMesh.cs が作る（マテリアルの値から。プロパティ名は SpiderWeb と同じなので、シェーダーを差しかえるだけで値はそのまま）。
// 穴の半径 _HoleRadius は頂点で計算するので、FuwaWebHole で穴が呼吸しても、輪・縦糸・夜露がいっしょに動く。
//
// 頂点の中身：uv0 = (種類, 横 -1/+1, 糸の上の位置 t, 段)、uv1・uv2 = 種類ごとの値（下の webGeom を参照）
//  種類 0 縦糸 / 1 横糸（輪）/ 2 外枠の糸 / 3 穴のふち / 4 夜露のつぶ
// 描き方：1回目の描画で、メッシュに並べた順に 0 外枠のまわりのにじみ → 1 糸の縁取り（暗い色） → 2 糸の中心（明るい色） → 3 穴のふちの光・夜露（uv0.w = 段）。2回目は奥行きだけ書く
Shader "FuwaCourse/SpiderWebLines"
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
        _SplitChance ("途中から生える放射の糸の割合", Range(0, 0.6)) = 0
        _SplitStart ("途中から生える糸の始まり（外周までの何割）", Range(0, 0.9)) = 0.4
        _RimColor ("Hole Rim Color", Color) = (0.95, 0.95, 1.0, 1.0)
        _RimWidth ("Hole Rim Width (x Line Width)", Float) = 2.6
        _RimGlow ("Hole Rim Glow Width (m)", Float) = 0.05
        _RimGlowAlpha ("Hole Rim Glow Alpha", Range(0, 1)) = 0.3
        [NoScaleOffset] _SpokeTex ("縦糸の表（板版で使う。こちらでは使わない）", 2D) = "black" {}
        _SpokeTexOn ("縦糸の表を使う（板版用）", Float) = 0
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    fixed4 _Color;
    float4 _Size, _HoleCenter;
    float4 _TearA, _TearB; float _TearW;
    float _HoleRadius, _LineWidth;
    float _FadeNear, _FadeFar, _FarFadeStart, _FarFadeEnd, _FarFadeMin;
    fixed4 _RimColor;
    float _RimWidth, _RimGlow, _RimGlowAlpha;
    fixed4 _CoreColor, _OutlineColor, _DewColor;
    fixed4 _EdgeGlow;
    float _EdgeHalo, _EdgePulseSpeed, _PulseNear, _PulseFar;
    fixed4 _DepthTint;
    float _DepthNear, _DepthFar, _DepthFade;
    float _OutlineWidth, _EdgeWidth, _DewSize, _TwinkleSpeed;
    float _Sag, _Seed;

    struct appdata
    {
        float4 vertex : POSITION;
        float4 uv0 : TEXCOORD0;
        float4 uv1 : TEXCOORD1;
        float4 uv2 : TEXCOORD2;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct v2f
    {
        float4 pos : SV_POSITION;
        float4 e : TEXCOORD0;      // x 横の距離(m) y 糸に沿った位置(m) z 糸の長さ(m, 端を丸める時) w 種類
        float4 f : TEXCOORD1;      // xy 夜露の中心からの位置(m) z 夜露のキラッの種 w 外枠なら1
        float3 wpos : TEXCOORD2;
        float2 m : TEXCOORD3;      // 板の中心からの位置(m)：裂け目用
        float4 j : TEXCOORD5;      // 外枠：角を二等分する線の向き（始点側 xy・終点側 zw、糸に沿った・横の向きで）
        UNITY_FOG_COORDS(4)
        UNITY_VERTEX_OUTPUT_STEREO
    };

    float2 perp2(float2 a) { return float2(-a.y, a.x); }

    // 種類ごとの太さ（中心から片側, m）。PASS で切りかえ
    //  pass 0 にじみ / 1 縁取り / 2 中心 / 3 穴のふち・夜露
    float halfWidthFor(float kind, int ps)
    {
        float hw = _LineWidth * 0.5;
        float hwE = hw * _EdgeWidth;
        float owE = hwE * _OutlineWidth * 0.8;
        if (ps == 0) return hwE + owE + _EdgeHalo;
        if (ps == 1) return kind > 1.5 && kind < 2.5 ? hwE + owE : hw + hw * _OutlineWidth;
        if (ps == 2) return kind > 1.5 && kind < 2.5 ? hwE : hw;
        // pass 3：穴のふち（太い線＋光）、夜露（玉＋キラッのにじみ）
        if (kind > 3.5) { float dewR = max(_DewSize, hw * 1.7); return dewR * 3.3; }
        return hw * _RimWidth + _RimGlow;
    }

    bool kindInPass(float kind, int ps)
    {
        if (ps == 0) return kind > 1.5 && kind < 2.5;
        if (ps == 3) return kind > 2.5;
        return kind < 3.5;   // 縁取り・中心：縦糸・輪・外枠・穴のふち（ふちも普通の糸として描く）
    }

    // 1つのメッシュの中に、描く順（0 にじみ → 1 縁取り → 2 中心 → 3 光・夜露）に三角形を並べてある（uv0.w = その段）。
    //  同じ描画の中では三角形は並んだ順に重なるので、1回の描画で「縁取りの上に中心」になる（ドローコールを減らすため）
    v2f webVert(appdata v, bool depthOnly)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        float kind = v.uv0.x, side = v.uv0.y, t = v.uv0.z;
        float hr = _HoleRadius;
        float2 c = 0, T = float2(1, 0);
        float along = 0, len = 0;
        bool corner = false;
        float2 cutDir = 0;
        float4 joinN = 0;
        int ps = (int)(v.uv0.w + 0.5);
        bool hide = !kindInPass(kind, ps) || (depthOnly && ps != 2);

        if (kind < 0.5)
        {
            // 縦糸：uv1.xy = 向き、uv2.x = 先端までの長さ、uv2.y = 途中から生える時の割合（<0 なら穴から）
            float2 d = v.uv1.xy;
            float Ro = v.uv2.x;
            float st = v.uv2.y < 0 ? hr : max(hr, lerp(hr, Ro, v.uv2.y));
            if (st >= Ro) hide = true;
            c = d * lerp(st, Ro, t);
            T = d;
            // 先端は外枠の糸に沿って斜めに切る（まっすぐ切ると、斜めに当たる所で角が枠の外へはみ出すので）
            if (t > 0.5 && dot(v.uv2.zw, v.uv2.zw) > 0.5) cutDir = v.uv2.zw;
        }
        else if (kind < 1.5)
        {
            // 横糸（輪の一部）：uv1 = (縦糸 s0 の向き, 縦糸 s1 の向き)、uv2 = (sA, wA, sB, wB)。両端 = (穴の半径 + s) * w
            float2 d0 = v.uv1.xy, d1 = v.uv1.zw;
            float2 A = d0 * (hr + v.uv2.x) * v.uv2.y, B = d1 * (hr + v.uv2.z) * v.uv2.w;
            float2 AB = B - A;
            float L = length(AB);
            float2 outN = normalize(A + B);
            float h = _Sag * L * 4 * t * (1 - t);
            float dh = _Sag * L * 4 * (1 - 2 * t);
            c = A + AB * t - outN * h;
            float2 tg = AB - outN * dh;
            T = tg / max(length(tg), 1e-6);
        }
        else if (kind < 2.5)
        {
            // 外枠の糸：uv1 = (端A, 端B)（穴の中心基準 m）。両端は丸くするので少しはみ出して作る
            float2 A = v.uv1.xy, B = v.uv1.zw;
            len = length(B - A);
            T = (B - A) / max(len, 1e-6);
            c = t < 0.5 ? A : B;
            corner = true;
            // 角で前後の糸と重ならないよう、角を二等分する線で持ち分を分ける（重なると縁取りが2回重なって濃くなるので）
            float2 nA = v.uv2.xy + T, nB = T + v.uv2.zw;
            float2 Nn = perp2(T);
            joinN = float4(dot(nA, T), dot(nA, Nn), dot(nB, T), dot(nB, Nn));
        }
        else if (kind < 3.5)
        {
            // 穴のふち：uv1.xy = 円の上の向き
            if (hr < 0.01) hide = true;
            c = v.uv1.xy * hr;
            T = perp2(v.uv1.xy);
        }
        else
        {
            // 夜露：uv1.xy = 縦糸の向き、uv2 = (s, w, キラッの種)。四角の角 = (side, t*2-1)
            c = v.uv1.xy * (hr + v.uv2.x) * v.uv2.y;
            if (hr + v.uv2.x <= hr - 1e-4) hide = true;
        }

        // 1ピクセルの大きさ(m)：細い糸でも最低1ピクセルぶん＋なめらかにする分は帯を太らせる
        float3 objC = float3((c + _HoleCenter.xy) / _Size.xy, 0);
        float4 cc = UnityObjectToClipPos(objC);
        float pxm = max(cc.w, 1e-3) * 2.0 / (abs(UNITY_MATRIX_P[1][1]) * _ScreenParams.y);
        float W = halfWidthFor(kind, ps);
        float ext = W + 2.0 * pxm;

        float2 q = c;
        float across = 0;
        float2 dewLocal = 0;
        if (kind > 3.5)
        {
            float2 cr = float2(side, t * 2 - 1);
            dewLocal = cr * ext;
            q = c + dewLocal;
        }
        else
        {
            float2 N = perp2(T);
            across = side * ext;
            q = c + N * across;
            if (dot(cutDir, cutDir) > 0.5)
            {
                float cs = dot(cutDir, N);
                if (abs(cs) > 0.25) q = c + cutDir * (across / cs);
            }
            if (corner)
            {
                // 外枠：端を ext ぶん外へのばす（丸い角にする）
                float sgn = t < 0.5 ? -1 : 1;
                q += T * sgn * ext;
                along = t < 0.5 ? -ext : len + ext;
            }
        }

        float3 obj = float3((q + _HoleCenter.xy) / _Size.xy, 0);
        o.pos = UnityObjectToClipPos(obj);
        if (hide) o.pos = float4(2, 2, 2, 1);   // この線は描かない（三角形をつぶす）
        o.e = float4(across, along, len, kind + ps * 10);
        o.f = float4(dewLocal, v.uv2.z, corner ? 1 : 0);
        o.wpos = mul(unity_ObjectToWorld, float4(obj, 1)).xyz;
        o.m = q + _HoleCenter.xy;
        o.j = joinN;
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
    }

    // 線の中心からの距離(m)。外枠は端を丸く
    float lineDist(v2f i)
    {
        float du = 0;
        if (i.f.w > 0.5)
        {
            du = max(0, max(-i.e.y, i.e.y - i.e.z));
            // 角の持ち分の外（となりの糸が描く所）は描かない
            if (dot(float2(i.e.y, i.e.x), i.j.xy) < 0 || dot(float2(i.e.y - i.e.z, i.e.x), i.j.zw) > 0) return 1e3;
        }
        return length(float2(i.e.x, du));
    }

    float camFadeAt(float camD)
    {
        float f = smoothstep(_FadeNear, max(_FadeFar, _FadeNear + 0.01), camD);
        if (_FarFadeEnd > _FarFadeStart) f *= lerp(1, _FarFadeMin, smoothstep(_FarFadeStart, _FarFadeEnd, camD));
        return f;
    }

    float tearMask(float2 m, float px)
    {
        if (_TearW <= 0.001) return 1;
        float2 ta = _TearA.xy, tb = _TearB.xy, ab = tb - ta;
        float tt = saturate(dot(m - ta, ab) / max(dot(ab, ab), 1e-6));
        float dT = length(m - (ta + ab * tt));
        float jag = 0.82 + 0.18 * sin(tt * 37.0 + _Seed) * sin(tt * 11.0 + _Seed * 1.7);
        float endTaper = saturate(min(tt, 1 - tt) * 6.0 + 0.35);
        float tw_ = _TearW * jag * endTaper;
        return smoothstep(tw_ - px * 1.5, tw_ + px * 1.5, dT);
    }

    float pulseAt(float camD)
    {
        return (0.5 + 0.5 * sin(_Time.y * _EdgePulseSpeed)) * (1 - smoothstep(_PulseNear, max(_PulseFar, _PulseNear + 0.01), camD));
    }

    // 最後の仕上げ：遠い巣の色・薄さ、カメラの近く・遠くで消す、霧
    fixed4 finish(v2f i, float3 col, float a, float camD)
    {
        float far = smoothstep(_DepthNear, max(_DepthFar, _DepthNear + 0.01), camD);
        col = lerp(col, _DepthTint.rgb, far * 0.65);
        a *= lerp(1, _DepthFade, far) * camFadeAt(camD);
        clip(a - 0.03);
        fixed4 c = fixed4(col, a);
        UNITY_APPLY_FOG(i.fogCoord, c);
        return c;
    }
    // 線の種類（e.w = 種類 + 段*10）
    float kindOf(v2f i) { return i.e.w - floor(i.e.w * 0.1 + 0.05) * 10; }

    fixed4 fragHalo(v2f i, float camD, float px)
    {
        float pulse = pulseAt(camD);
        if (pulse * _EdgeGlow.a < 0.01) discard;
        float d = lineDist(i);
        float hw = _LineWidth * 0.5, hwE = hw * _EdgeWidth, owE = hwE * _OutlineWidth * 0.8;
        float halo = (1 - smoothstep(hwE + owE, hwE + owE + max(_EdgeHalo, px), d)) * pulse * _EdgeGlow.a * 0.35;
        halo *= _Color.a * tearMask(i.m, px);
        return finish(i, _EdgeGlow.rgb, halo, camD);
    }

    fixed4 fragOutline(v2f i, float camD, float px)
    {
        float d = lineDist(i);
        float lw = px * 1.2;
        float W = halfWidthFor(kindOf(i), 1);
        float lineA = 1 - smoothstep(W - lw * 0.5, W + lw * 0.5, d);
        float a = lineA * _OutlineColor.a * _Color.a * tearMask(i.m, px);
        return finish(i, _OutlineColor.rgb, a, camD);
    }

    // 近づいた巣はうっすら明滅（外枠は少し強め）
    fixed4 fragCore(v2f i, float camD, float px)
    {
        float kind = kindOf(i);
        float d = lineDist(i);
        float lw = px * 1.2;
        float W = halfWidthFor(kind, 2);
        float coreA = 1 - smoothstep(W - lw * 0.5, W + lw * 0.5, d);
        bool edge = kind > 1.5 && kind < 2.5;
        float pulse = pulseAt(camD);
        fixed3 core = lerp(_CoreColor.rgb, _EdgeGlow.rgb, pulse * _EdgeGlow.a * (edge ? lerp(0.75, 1.0, coreA) : 0.75));
        float a = coreA * _CoreColor.a * _Color.a * tearMask(i.m, px);
        return finish(i, core, a, camD);
    }

    // 穴のふち（太く明るい糸＋まわりにうっすら光）と、夜露のつぶ（ときどきキラッ）
    fixed4 fragOverlay(v2f i, float camD, float px, float pxDew)
    {
        float hw = _LineWidth * 0.5;
        if (kindOf(i) > 3.5)
        {
            float dDew = length(i.f.xy);
            float lw = pxDew * 1.2;
            float dewR = max(_DewSize, hw * 1.7);
            float tw = pow(saturate(sin(_Time.y * _TwinkleSpeed + i.f.z * 97.0)), 16);
            float dewA = 1 - smoothstep(dewR - lw * 0.5, dewR + lw * 0.5, dDew);
            float haloA = (1 - smoothstep(dewR, dewR * (1.8 + 1.5 * tw), dDew)) * 0.35 * tw;
            float a = max(dewA * _DewColor.a, haloA) * _Color.a * tearMask(i.m, pxDew);
            return finish(i, _DewColor.rgb * (0.9 + 0.4 * tw), a, camD);
        }
        float dRim = abs(i.e.x);
        float lw = px * 1.2;
        float hwR = hw * _RimWidth;
        float rimA = 1 - smoothstep(hwR - lw * 0.5, hwR + lw * 0.5, dRim);
        float glowA = (1 - smoothstep(hwR, hwR + max(_RimGlow, 1e-4), dRim)) * _RimGlowAlpha;
        return finish(i, _RimColor.rgb, max(rimA * _RimColor.a, glowA), camD);
    }
    ENDCG

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Cull Off

        // 色：にじみ・縁取り・中心・光／夜露を、メッシュに並べた順に1回で描く。
        //  奥行きはここでは書かない（重なった糸どうしが同じ面で奥行きを取り合って欠けるので）→ 次のパスで書く
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            v2f vert (appdata v) { return webVert(v, false); }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float px = max(fwidth(i.e.x), 1e-5);
                float pxDew = max(fwidth(i.f.x), 1e-5);
                float camD = distance(i.wpos, _WorldSpaceCameraPos);
                float layer = floor(i.e.w * 0.1 + 0.05);
                [branch] if (layer < 0.5) return fragHalo(i, camD, px);
                [branch] if (layer < 1.5) return fragOutline(i, camD, px);
                [branch] if (layer < 2.5) return fragCore(i, camD, px);
                return fragOverlay(i, camD, px, pxDew);
            }
            ENDCG
        }

        // 糸の所だけ奥行きを書く（色は書かない）。あとから描く半透明のエフェクト＝重力エリアなどが、手前の糸の上に出ないように
        Pass
        {
            ZWrite On
            ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            v2f vert (appdata v) { return webVert(v, true); }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float px = max(fwidth(i.e.x), 1e-5);
                float camD = distance(i.wpos, _WorldSpaceCameraPos);
                float d = lineDist(i);
                float W = halfWidthFor(kindOf(i), 2);
                float coreA = 1 - smoothstep(W - px * 0.6, W + px * 0.6, d);
                float far = smoothstep(_DepthNear, max(_DepthFar, _DepthNear + 0.01), camD);
                clip(coreA * _CoreColor.a * _Color.a * lerp(1, _DepthFade, far) * camFadeAt(camD) - 0.03);
                return 0;
            }
            ENDCG
        }
    }
    Fallback Off
}
