// ステージ2「みずのたに」の水面（2026-10-17）。ステージ1の温泉（FuwaCourse/HotSpring）と同じ、光を当てない明るいトゥーンの水
// ・ふちほど明るい水色、まん中は濃い水色。ふちに白い泡の線（ゆらゆら太さが変わる）
// ・流れのある水は、流れの向きに白い筋が流れていく（向き＝この板のローカル +Y を水平にしたもの。builder が向けて置く）
// ・止まった水はゆっくり揺れるさざなみ。どちらもたまにきらっと光る
// 板（Unity の Quad、X に90度倒して置く）の uv 0〜1 と、板の大きさ（行列から）で、ふちからの距離をメートルで出す
// _Ribbon=1：曲がった川・池の帯メッシュ用（2026-10-19、2–1 みずのたに）。uv1＝(横の位置, 半分の幅, 始まりからの距離, 終わりまでの距離) m、uv2.x＝中心線に沿った距離 m。
//   ふちの泡・流れの筋を帯に沿わせる（板の大きさ・向きは使わない）。開いた端（流れ込み・流れ出し）は距離を大きくして泡を出さない
// 静的バッチングだとオブジェクトの行列が消えるので、水面は Static にしないこと
Shader "FuwaStage2/Water"
{
    Properties
    {
        _Deep ("Center Color", Color) = (0.26, 0.66, 0.86, 1)
        _Shallow ("Edge Color", Color) = (0.58, 0.88, 0.95, 1)
        _Foam ("Foam Color", Color) = (0.96, 1, 1, 1)
        _FoamWidth ("Edge Foam Width (m)", Float) = 0.18
        _DeepWidth ("Edge→Center Width (m)", Float) = 2.0
        _FlowSpeed ("Flow Speed (m/s, 0=still)", Float) = 0
        _StreakScale ("Streak Size", Float) = 1.0
        _StreakStrength ("Streak Strength", Range(0, 1)) = 0.55
        _Sparkle ("Sparkle", Range(0, 1)) = 0.5
        [Toggle] _Ribbon ("Ribbon Mesh (uv1/uv2)", Float) = 0
        // 深い水の終わりで次の浅い水の色へなだらかに（uv2.y＝終わりまでの距離 m。2026-10-27）
        _OpenFade ("Fade To Shallow Length (m, 0=off)", Float) = 0
        _OpenDeep ("Shallow Water Center Color", Color) = (0.26, 0.66, 0.86, 1)
        _OpenShallow ("Shallow Water Edge Color", Color) = (0.58, 0.88, 0.95, 1)
        _OpenDeepWidth ("Shallow Water Edge→Center Width (m)", Float) = 2.0
        // 止まった水の模様を、湧き水（_RadialCenter、ワールドの xz）から外へ広がる輪と筋に（2026-10-27 てじゃ「上の池の模様も、中心から外に広がるかんじに」）
        [Toggle] _Radial ("Radial From Spring", Float) = 0
        _RadialCenter ("Spring Center (world x, z)", Vector) = (0, 0, 0, 0)
        _RadialSpeed ("Radial Speed (m/s)", Float) = 0.45
    }
    SubShader
    {
        // 板の大きさと向きを行列から取るので、まとめ描きはしない（滝の板と同じ。2026-10-23）
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "DisableBatching" = "True" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Deep, _Shallow, _Foam, _OpenDeep, _OpenShallow;
            float _FoamWidth, _DeepWidth, _FlowSpeed, _StreakScale, _StreakStrength, _Sparkle, _Ribbon, _OpenFade, _OpenDeepWidth, _Radial, _RadialSpeed;
            float4 _RadialCenter;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 rib : TEXCOORD1; float2 rib2 : TEXCOORD2; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; float4 frame : TEXCOORD2; float4 rib : TEXCOORD4; float2 rs : TEXCOORD5; UNITY_FOG_COORDS(3) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                // 板の横・縦の大きさ(m)と、流れの向き（ローカル +Y を水平に）
                float3 ax = mul((float3x3)unity_ObjectToWorld, float3(1, 0, 0));
                float3 ay = mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0));
                float2 fd = ay.xz; float fl = length(fd); fd = fl > 1e-4 ? fd / fl : float2(0, 1);
                o.frame = float4(length(ax), length(ay), fd);
                o.rib = v.rib; o.rs = v.rib2;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            // x の向きに per マスでくり返すノイズ（角度でつかう。ぐるっと一周しても継ぎ目が出ない）
            float NoiseWrapX(float2 p, float per)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                float i0 = fmod(i.x + per * 100, per), i1 = fmod(i.x + 1 + per * 100, per);
                return lerp(lerp(Hash(float2(i0, i.y)), Hash(float2(i1, i.y)), u.x), lerp(Hash(float2(i0, i.y + 1)), Hash(float2(i1, i.y + 1)), u.x), u.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 size = i.frame.xy;
                float2 fd = i.frame.zw;
                // ふちからの距離(m)
                float2 d2 = min(i.uv, 1 - i.uv) * size;
                float edge = min(d2.x, d2.y);
                if (_Ribbon > 0.5) edge = min(i.rib.y - abs(i.rib.x), min(i.rib.z, i.rib.w));
                float3 col = lerp(_Shallow.rgb, _Deep.rgb, smoothstep(0, _DeepWidth, edge));
                if (_Ribbon > 0.5 && _OpenFade > 0.001)
                {
                    // 終わりに近いほど、浅い水と同じ色の付け方（横のふちからの距離だけ）へ。少しゆらがせて境目をまっすぐにしない
                    float side = i.rib.y - abs(i.rib.x);
                    float3 sc = lerp(_OpenShallow.rgb, _OpenDeep.rgb, smoothstep(0, _OpenDeepWidth, side));
                    float wv = (Noise(float2(i.rib.x * 0.8, i.rs.x * 0.5 + _Time.y * 0.15)) - 0.5) * _OpenFade * 0.5;
                    col = lerp(sc, col, smoothstep(0, _OpenFade, i.rs.y + wv));
                }
                // 流れの座標：along＝流れの向き、across＝横
                float2 perp = float2(-fd.y, fd.x);
                float along = dot(i.wp.xz, fd), across = dot(i.wp.xz, perp);
                if (_Ribbon > 0.5) { along = i.rs.x; across = i.rib.x; }
                float s = _StreakScale;
                if (_FlowSpeed > 0.001)
                {
                    // 流れの白い筋：横に細かく、流れの向きに長いノイズを流す。2つ重ねて速さを少しずらす
                    float2 p1 = float2(across * 2.2 / s, (along - _Time.y * _FlowSpeed) * 0.35 / s);
                    float2 p2 = float2(across * 3.1 / s + 7.3, (along - _Time.y * _FlowSpeed * 1.35) * 0.5 / s);
                    float n = Noise(p1) * 0.6 + Noise(p2) * 0.4;
                    float streak = smoothstep(0.62, 0.72, n);
                    col = lerp(col, _Foam.rgb, streak * _StreakStrength);
                }
                else if (_Radial > 0.5)
                {
                    // 湧き水から外へ：広がっていく波の輪（ところどころ切れる）と、外へ流れていく放射状の筋
                    float2 dv = i.wp.xz - _RadialCenter.xy;
                    float r = length(dv);
                    float ang = (atan2(dv.y, dv.x) / 6.2831853 + 0.5) * 24.0;   // 0〜24 のマス
                    float t = _Time.y * _RadialSpeed;
                    float wob = (NoiseWrapX(float2(ang * 0.5, r * 0.6), 12.0) - 0.5) * 0.35;
                    float ph = frac((r + wob) * 0.85 - t * 0.85);
                    float ring = smoothstep(0.0, 0.04, ph) * (1 - smoothstep(0.04, 0.11, ph));
                    ring *= smoothstep(0.35, 0.55, NoiseWrapX(float2(ang, r * 0.5 - t * 0.6), 24.0));
                    ring *= smoothstep(0.25, 0.9, r);   // まん中（湧き水のふくらみ）は出さない
                    // 放射状の筋：角度に細かく、外へ長くのびて流れる
                    float n = NoiseWrapX(float2(ang * 1.5, (r - t * 1.6) * 0.45), 36.0) * 0.6 + NoiseWrapX(float2(ang * 2.0 + 5.0, (r - t * 2.1) * 0.6), 48.0) * 0.4;
                    float streak = smoothstep(0.64, 0.74, n) * smoothstep(0.5, 1.5, r);
                    col = lerp(col, _Foam.rgb, saturate(ring * 0.8 + streak * 0.6) * _StreakStrength);
                }
                else
                {
                    // 止まった水：ゆっくり動くさざなみの明るい線
                    float n = Noise(i.wp.xz * 0.9 / s + float2(_Time.y * 0.12, _Time.y * 0.07)) + Noise(i.wp.xz * 1.7 / s - _Time.y * 0.1) * 0.5;
                    // 等高線の山のところだけ細い明るい線（frac が 0/1 に近い所）
                    float rip = smoothstep(0.0, 0.025, abs(frac(n * 1.6) - 0.5) - 0.455);
                    col = lerp(col, _Foam.rgb, rip * _StreakStrength * 0.7);
                }
                // きらめき
                float2 cell = floor(i.wp.xz * 5.0);
                float h = Hash(cell);
                float tw = saturate(sin(_Time.y * (1.5 + h * 2.0) + h * 40.0) * 8.0 - 7.0);
                float2 f = frac(i.wp.xz * 5.0) - 0.5;
                col = lerp(col, _Foam.rgb, tw * (1 - smoothstep(0.05, 0.2, length(f))) * _Sparkle * step(0.65, h));
                // ふちの泡（場所と時間で太さがゆらぐ）
                float fw = _FoamWidth * (0.7 + 0.3 * sin((along + across) * 3.0 + _Time.y * 1.2));
                col = lerp(col, _Foam.rgb, 1 - smoothstep(fw * 0.5, fw, edge));
                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
