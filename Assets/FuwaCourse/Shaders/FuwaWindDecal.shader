// 風のエリアの地面の模様（流れる帯・矢じり＞＞＞）を、地面に投影して描く（2026-10-12）。
// 前は道の形に合わせた板（Fuwa/GroundHighlight）に描いていたので、道の外（芝のふち・島）には出なかった（てじゃ「風の地面エフェクトが芝に出てない」）。
// 見えない箱（Cube）の中にある「実際に見えている地面の表面」だけに描く（FuwaCourse/BlobDecal と同じやり方。深度から地面の位置を出す）。
// 箱のローカル座標（-0.5〜0.5）×箱の大きさ＝メートル。_WindDir（箱のローカルの向き）に沿った距離が帯・矢じりの流れる向き。
// プロパティ名は Fuwa/GroundHighlight と同じ（FuwaWindArea が _Color・_Phase・_FlowDir・_Stroke を書き換える）
Shader "Fuwa/WindDecal"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.95, 0.45, 0.3)
        _WindDir ("風の向き（箱のローカル）", Vector) = (0, 0, 1, 0)
        _EdgeFade ("箱の横のふちでぼかす幅(m)", Float) = 0.6
        _FadeFront ("風下のふちでぼかす幅(m、0=ぼかさない)", Float) = 0.6
        _FadeBack ("風上のふちでぼかす幅(m、0=ぼかさない)", Float) = 0.6
        _Wavelength ("帯の間隔(m)", Float) = 2.2
        _Speed ("流れる速さ(m/s)", Float) = 3.5
        _FlowDir ("流れる向き(1/-1)", Float) = 1
        _Base ("帯の間の薄さ(0-1)", Range(0,1)) = 0.35
        _Sharp ("帯のくっきりさ", Range(1,8)) = 2.5
        [Toggle] _Chevron ("矢じり形（＞＞＞）にする", Float) = 0
        _ChevPeriod ("矢じりの横の間隔(m)", Float) = 2.4
        _ChevSlope ("矢じりのとがり具合", Float) = 0.8
        _Stroke ("矢じりの線の太さ(0-0.5)", Range(0.02, 0.5)) = 0.16
        _Phase ("矢じりの流れた距離(m、スクリプトから)", Float) = 0
        _Taper ("真ん中ほど太く（0=同じ太さ）", Range(0, 1.5)) = 0
        _Soft ("矢じりのふちのぼかし(0-1)", Range(0, 1)) = 0
        [Toggle] _ScriptPhase ("流れる速さをスクリプトが決める（風の強さで速く）", Float) = 0
        // つながった風のエリアの境目（世界座標の面 xyz=向き w=距離。dot(位置, xyz) > w の側は塗らない。0=切らない）。
        // カーブに沿って回した箱どうしが内側でくさび形に重なり、二重に塗られて濃い帯になっていた（2026-10-16 てじゃ「グラデのとこに濃い境目」）
        _CutFront ("風下の境目（スクリプトから）", Vector) = (0, 0, 0, 0)
        _CutBack ("風上の境目（スクリプトから）", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float4 _WindDir;
            float _EdgeFade, _FadeFront, _FadeBack, _Wavelength, _Speed, _FlowDir, _Base, _Sharp;
            float _Chevron, _ChevPeriod, _ChevSlope, _Stroke, _Phase, _Taper, _Soft, _ScriptPhase;
            float4 _CutFront, _CutBack;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.eyeDepth = -UnityObjectToViewPos(v.vertex).z;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float raw = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                float sceneDepth = LinearEyeDepth(raw);
                float3 ray = i.worldPos - _WorldSpaceCameraPos;
                float3 surf = _WorldSpaceCameraPos + ray * (sceneDepth / max(i.eyeDepth, 1e-4));
                float3 sn = normalize(cross(ddy(surf), ddx(surf)));
                float3 up = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));
                float3 lp = mul(unity_WorldToObject, float4(surf, 1)).xyz;
                if (abs(lp.x) > 0.5 || abs(lp.z) > 0.5 || abs(lp.y) > 0.5) discard;
                float3 camL = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                if (camL.y < lp.y) discard;   // 下から見た時は塗らない
                if (dot(surf, _CutFront.xyz) > _CutFront.w || dot(surf, _CutBack.xyz) > _CutBack.w) discard;   // 隣の箱の受け持ち

                // メートルでの位置（箱の大きさ＝物体の拡大率）
                float3 sc = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
                float2 wd = normalize(_WindDir.xz);
                // 模様は世界座標で（つながった風のエリアどうしで帯・矢じりがずれないように）
                float3 wdW3 = mul((float3x3)unity_ObjectToWorld, float3(wd.x, 0, wd.y));
                float2 wdW = normalize(wdW3.xz);
                float2 sideW = float2(wdW.y, -wdW.x);
                float ux = dot(surf.xz, wdW);     // 風下へ（前の板の uv.x と同じ向き）
                float uy = dot(surf.xz, sideW);   // 風に直角

                fixed4 c = _Color;
                // 箱のふちでぼかす（前の板の頂点カラーの代わり）。風上・風下のふちは別の幅（つながったエリアの間は0）
                float2 dEdge = (0.5 - abs(lp.xz)) * sc.xz;
                float2 sgn = sign(lp.xz);
                float ex = dot(float2(sgn.x, 0), wd), ez = dot(float2(0, sgn.y), wd);
                float wx = ex > 0.5 ? _FadeFront : (ex < -0.5 ? _FadeBack : _EdgeFade);
                float wz = ez > 0.5 ? _FadeFront : (ez < -0.5 ? _FadeBack : _EdgeFade);
                c.a *= (wx > 0.001 ? smoothstep(0, wx, dEdge.x) : 1) * (wz > 0.001 ? smoothstep(0, wz, dEdge.y) : 1);
                c.a *= 1.0 - smoothstep(0.3, 0.5, abs(lp.y));   // 箱の上下のふちでなめらかに消す
                c.a *= smoothstep(0.55, 0.85, abs(dot(sn, up)));   // 崖の横の面などには塗らない

                if (_Chevron > 0.5)
                {
                    float yy = abs(frac(uy / _ChevPeriod + 0.5) - 0.5) * _ChevPeriod;
                    float xx = ux + _FlowDir * _ChevSlope * yy;
                    float col = floor(uy / _ChevPeriod + 0.5);
                    xx += abs(fmod(col, 2.0)) * _Wavelength * 0.5;
                    float q = frac(xx / _Wavelength - _Phase * _FlowDir / _Wavelength);
                    float d = abs(q - 0.5);
                    float aa = fwidth(xx / _Wavelength) * 1.5;
                    float sw = _Stroke * lerp(1 + _Taper, 1 - _Taper * 0.6, saturate(yy / (_ChevPeriod * 0.5)));
                    float soft = sw * 0.5 * _Soft;
                    float stroke = 1 - smoothstep(sw * 0.5 - soft - aa, sw * 0.5 + aa, d);
                    stroke *= smoothstep(_ChevPeriod * 0.5, _ChevPeriod * 0.47, yy);
                    c.a *= lerp(_Base, 1.0, stroke);
                }
                else
                {
                    float flow = _ScriptPhase > 0.5 ? _Phase : _Time.y * _Speed;
                    float p = frac(ux / _Wavelength - flow * _FlowDir / _Wavelength);
                    float saw = _FlowDir > 0 ? p : 1 - p;
                    float band = pow(saw, _Sharp);
                    band *= smoothstep(1.0, 0.72, saw);
                    c.a *= lerp(_Base, 1.0, band);
                }
                if (c.a <= 0.002) discard;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(c.rgb, 0));
                return c;
            }
            ENDCG
        }
    }
}
