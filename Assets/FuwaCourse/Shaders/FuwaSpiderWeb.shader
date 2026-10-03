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
        _RimColor ("Hole Rim Color", Color) = (0.95, 0.95, 1.0, 1.0)
        _RimWidth ("Hole Rim Width (x Line Width)", Float) = 2.6
        _RimGlow ("Hole Rim Glow Width (m)", Float) = 0.05
        _RimGlowAlpha ("Hole Rim Glow Alpha", Range(0, 1)) = 0.3
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
            #include "UnityCG.cginc"

            fixed4 _Color;
            float4 _Size, _HoleCenter;
            float _HoleRadius, _HoleRadiusMax, _LineWidth, _Spokes, _RingStart, _RingGrow, _Seed, _OuterMin, _OuterMax, _Round;
            float _FadeNear, _FadeFar;
            float _FrameN;
            float4 _F01, _F23, _F45, _F67;
            fixed4 _RimColor;
            float _RimWidth, _RimGlow, _RimGlowAlpha;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 m : TEXCOORD0; float3 wpos : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.m = (v.uv - 0.5) * _Size.xy;   // meters from board center
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
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
                k = k % 8;
                float4 v = k < 2 ? _F01 : (k < 4 ? _F23 : (k < 6 ? _F45 : _F67));
                return (k % 2 == 0) ? v.xy : v.zw;
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
                        float2 A = frameCorner(k), B = frameCorner((k + 1) % n);
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
            float ringRadiusAt(float k, float i, float holeR)
            {
                float a = _RingStart, b = _RingGrow;
                float s = (a / b) * (pow(1 + b, k) - 1);
                return (holeR + s) * lerp(0.95, 1.05, hash(float2(spokeIndex(i), k)));
            }
            float ringRadius(float k, float i) { return ringRadiusAt(k, i, _HoleRadius); }

            float segDist(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-6));
                return length(p - a - ab * t);
            }

            float cross2(float2 a, float2 b) { return a.x * b.y - a.y * b.x; }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);   // VRで左右の目それぞれのカメラ位置を使う
                float2 p = i.m - _HoleCenter.xy;
                float r = length(p);
                float px = max(fwidth(r), 1e-5);
                float hw = _LineWidth * 0.5;
                float dIn = 1e3;    // lines that only exist inside the outline
                float dEdge = 1e3;  // the outline thread itself
                float ringA = 0;    // rings (drawn separately so they can fade near the outline)

                // rim around the hole
                dIn = min(dIn, abs(r - _HoleRadius));

                float ang = atan2(p.y, p.x);
                if (ang < 0) ang += 6.2831853;
                float n = _Spokes;
                float fi = floor(ang / 6.2831853 * n);

                // which sector are we in (between spoke s0 and s1)?
                float s0 = fi - 1, s1 = fi;
                [unroll] for (int q = 0; q < 3; q++)
                {
                    float aa = spokeAngle(fi + q - 1), ab = spokeAngle(fi + q);
                    float2 da = float2(cos(aa), sin(aa)), db = float2(cos(ab), sin(ab));
                    if (cross2(da, p) >= 0 && cross2(p, db) >= 0) { s0 = fi + q - 1; s1 = fi + q; }
                }
                float a0 = spokeAngle(s0), a1 = spokeAngle(s1);
                float2 d0 = float2(cos(a0), sin(a0)), d1 = float2(cos(a1), sin(a1));
                float R0 = outerRadius(s0), R1 = outerRadius(s1);
                float2 P0 = d0 * R0, P1 = d1 * R1;
                // inside the outline = same side of edge P0-P1 as the center
                float side = cross2(P1 - P0, p - P0);
                float sideC = cross2(P1 - P0, -P0);
                bool inside = side * sideC >= 0;
                dEdge = segDist(p, P0, P1);
                if (_FrameN > 2.5)
                {
                    // 枠の多角形：直線の枠糸そのものを描き、内側判定も多角形で
                    int fn = (int)_FrameN;
                    dEdge = 1e3; bool allIn = true;
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        if (k < fn)
                        {
                            float2 A = frameCorner(k), B = frameCorner((k + 1) % fn);
                            dEdge = min(dEdge, segDist(p, A, B));
                            if (cross2(B - A, p - A) * cross2(B - A, -A) < 0) allIn = false;
                        }
                    }
                    inside = allIn;
                }

                if (r > _HoleRadius)
                {
                    // spokes near this angle, each only up to its own tip
                    [unroll] for (int o = -1; o <= 2; o++)
                    {
                        float a = spokeAngle(fi + o);
                        float2 dir = float2(cos(a), sin(a));
                        float along = dot(p, dir);
                        if (along > _HoleRadius * 0.9 && along < outerRadius(fi + o))
                            dIn = min(dIn, abs(cross2(p, dir)));
                    }
                    // rings between the two spokes of this sector
                    float sd = r - _HoleRadius;
                    float kf = log(1 + sd * _RingGrow / _RingStart) / log(1 + _RingGrow);
                    float k0 = floor(kf);
                    [unroll] for (int kk = -1; kk <= 2; kk++)   // 輪は糸ごとに±5%ずれるので、1つ内側の輪も見る
                    {
                        float k = max(1, k0 + kk);
                        float ra = ringRadius(k, s0), rb = ringRadius(k, s1);
                        // 外周の糸のすぐそばの輪は描かない（外周との間に細かい切れ端ができるので）。
                        // 判定は「穴がいちばん大きい時の輪の位置」で決めるので、穴が呼吸しても輪が出たり消えたりしない。
                        float hm = max(_HoleRadiusMax, _HoleRadius);
                        float gap = _RingStart * pow(1 + _RingGrow, k);
                        float room = min(R0 - ringRadiusAt(k, s0, hm), R1 - ringRadiusAt(k, s1, hm));
                        float fade = room > gap * 0.6 ? 1 : 0;
                        float dr = segDist(p, d0 * ra, d1 * rb);
                        ringA = max(ringA, (1 - smoothstep(hw - px * 0.6, hw + px * 0.6, dr)) * fade);
                    }
                }

                float d = inside ? min(dIn, dEdge) : dEdge;
                float lw = px * 1.2;
                float a = 1 - smoothstep(hw - lw * 0.5, hw + lw * 0.5, d);
                if (inside) a = max(a, ringA);
                if (r < _HoleRadius - hw - lw) a = 0;
                // 穴のふち：太く明るい糸＋まわりにうっすら光（穴の場所と開き具合が分かる目印）
                float dRim = abs(r - _HoleRadius);
                float hwR = hw * _RimWidth;
                float rimA = 1 - smoothstep(hwR - lw * 0.5, hwR + lw * 0.5, dRim);
                float glowA = (1 - smoothstep(hwR, hwR + max(_RimGlow, 1e-4), dRim)) * _RimGlowAlpha;
                float baseA = _Color.a * a;
                float rimTotal = max(rimA * _RimColor.a, glowA);
                float outA = max(baseA, rimTotal);
                fixed3 col = lerp(_Color.rgb, _RimColor.rgb, saturate(rimTotal / max(outA, 1e-4)));
                // 通り抜ける時に見づらくないよう、カメラのすぐ近くの部分はスゥっと消す
                float camD = distance(i.wpos, _WorldSpaceCameraPos);
                outA *= smoothstep(_FadeNear, max(_FadeFar, _FadeNear + 0.01), camD);
                return fixed4(col, outA);
            }
            ENDCG
        }
    }
}
