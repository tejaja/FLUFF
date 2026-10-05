// 地面に重ねる半透明のハイライト（横風のエリア表示など）。
// 頂点カラーのアルファで端をぼかし、_Colorのアルファでフェードイン／アウトする。
// UVのx＝風の向きに沿った距離(m)。濃い帯が風の向きに流れていく（_FlowDirで向きを反転）。
Shader "Fuwa/GroundHighlight"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.95, 0.45, 0.3)
        _Wavelength ("帯の間隔(m)", Float) = 2.2
        _Speed ("流れる速さ(m/s)", Float) = 3.5
        _FlowDir ("流れる向き(1/-1)", Float) = 1
        _Base ("帯の間の薄さ(0-1)", Range(0,1)) = 0.35
        _Sharp ("帯のくっきりさ", Range(1,8)) = 2.5
        [Header(Chevron)]
        [Toggle] _Chevron ("矢じり形（＞＞＞）にする", Float) = 0
        _ChevPeriod ("矢じりの横の間隔(m)", Float) = 2.4
        _ChevSlope ("矢じりのとがり具合", Float) = 0.8
        _Stroke ("矢じりの線の太さ(0-0.5)", Range(0.02, 0.5)) = 0.16
        _Phase ("矢じりの流れた距離(m、スクリプトから)", Float) = 0
        _Taper ("真ん中ほど太く（0=同じ太さ）", Range(0, 1.5)) = 0
        [Toggle] _DebugEdges ("確認用：矢じりの両はしに線", Float) = 0
        _Soft ("矢じりのふちのぼかし(0-1)", Range(0, 1)) = 0
        [Toggle] _ScriptPhase ("流れる速さをスクリプトが決める（風の強さで速く）", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Offset -1, -2
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Wavelength, _Speed, _FlowDir, _Base, _Sharp;
            float _Chevron, _ChevPeriod, _ChevSlope, _Stroke, _Phase, _Taper, _DebugEdges, _Soft, _ScriptPhase;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = i.color;
                if (_Chevron > 0.5)
                {
                    // 矢じり（＞＞＞）：先が風下を向いたV字の線が風下へ流れる。uv.y＝風に直角の距離
                    float yy = abs(frac(i.uv.y / _ChevPeriod + 0.5) - 0.5) * _ChevPeriod;
                    float xx = i.uv.x + _FlowDir * _ChevSlope * yy;
                    // となりの列は半周期ずらす（Vのはしどうしがつながってジグザグにならない＝列の間を切らなくていい）
                    float col = floor(i.uv.y / _ChevPeriod + 0.5);
                    xx += abs(fmod(col, 2.0)) * _Wavelength * 0.5;
                    // 流れる距離はスクリプトが風の強さに合わせて進める（_Phase）
                    float q = frac(xx / _Wavelength - _Phase * _FlowDir / _Wavelength);
                    float d = abs(q - 0.5);
                    float aa = fwidth(xx / _Wavelength) * 1.5;
                    // 線の太さ：Vの先（真ん中）ほど太く、はしほど細く
                    float sw = _Stroke * lerp(1 + _Taper, 1 - _Taper * 0.6, saturate(yy / (_ChevPeriod * 0.5)));
                    // ふちを少しぼかす（_Soft＝線の太さに対するぼかし幅）
                    float soft = sw * 0.5 * _Soft;
                    float stroke = 1 - smoothstep(sw * 0.5 - soft - aa, sw * 0.5 + aa, d);
                    // となりの矢じりとつながってジグザグに見えないように、Vの両はしを切って1つずつ離す
                    stroke *= smoothstep(_ChevPeriod * 0.5, _ChevPeriod * 0.47, yy);
                    // 確認用：矢じりの両はし（列の境目）に風向きの直線を引く
                    if (_DebugEdges > 0.5)
                    {
                        float ed = _ChevPeriod * 0.5 - yy;
                        float aw = fwidth(i.uv.y) * 1.5;
                        stroke = max(stroke, 1 - smoothstep(0.15 - aw, 0.15 + aw, ed));
                    }
                    c.a *= lerp(_Base, 1.0, stroke);
                    UNITY_APPLY_FOG(i.fogCoord, c);
                    return c;
                }
                // 風下へ流れるノコギリ波：前側がくっきり、後ろがなだらかに消える帯
                // 流れた距離：スクリプトが風の強さに合わせて進める時は _Phase、そうでなければ一定の速さ
                float flow = _ScriptPhase > 0.5 ? _Phase : _Time.y * _Speed;
                float p = frac(i.uv.x / _Wavelength - flow * _FlowDir / _Wavelength);
                float saw = _FlowDir > 0 ? p : 1 - p;
                float band = pow(saw, _Sharp);
                // 帯の先端（急に切れる所）を少しなめらかに
                band *= smoothstep(1.0, 0.72, saw);
                c.a *= lerp(_Base, 1.0, band);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
