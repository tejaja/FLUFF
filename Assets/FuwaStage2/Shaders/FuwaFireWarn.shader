// 火炎放射が当たる所を地面に見せる赤いグラデーション（2026-11-02 てじゃ）。uv.x が横（まん中が濃い）、uv.y が炎の根元→先。
// 道と重なってチラつかないよう、少し持ち上げた上で Offset をかけて、深さは書かない
Shader "FuwaCourse/FireWarn"
{
    Properties
    {
        _Color ("色", Color) = (1, 0.1, 0.05, 1)
        _Root ("根元の濃さ", Range(0,1)) = 1
        _Tip ("先の濃さ", Range(0,1)) = 0.8
        _Length ("エリアの長さ(m)", Float) = 6.5
        _Wavelength ("帯の間隔(m)", Float) = 1.8
        _Speed ("流れる速さ(m/s)。風の地面は 3.5", Float) = 10
        _Base ("帯の間の薄さ(0-1)", Range(0,1)) = 0.3
        _Sharp ("帯のくっきりさ", Range(1,8)) = 2.5
        _Alpha ("出ている量（スクリプトが動かす）", Range(0,1)) = 1
        _Flow ("流れる向き(1 か -1。スクリプトが動かす)", Float) = 1
        _RootFade ("根元のふちをぼかす幅(uv.y、0 でぼかさない)", Float) = 0
        // 2026-11-04 てじゃ「影より下にうつしたい」：1 で上塗りでなく、下の道の色に _Tint を掛ける（2倍の掛け算。影の所は暗いまま色だけ変わる）。_SrcBlend/_DstBlend は DstColor/SrcColor に
        [Toggle] _Mode ("下の色に掛ける(影の下に見える)", Float) = 0
        _Tint ("掛ける色(0-2、1 で変わらない)", Color) = (1.15, 1.7, 0.4, 1)
        _EdgeTint ("ふちの掛ける色(_Mode 1 の時)", Color) = (1.05, 1.0, 0.2, 1)
        _EdgeW ("ふちの幅(横の0-1)", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("SrcBlend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("DstBlend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Geometry+20" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Offset -2, -2
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color; half4 _Tint, _EdgeTint; half _Mode, _EdgeW;
            half _Root, _Tip, _Alpha;
            float _Length, _Wavelength, _Speed, _Base, _Sharp, _Flow, _RootFade;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half side = 1 - abs(i.uv.x * 2 - 1);
                // 風の地面（Fuwa/GroundHighlight）と同じ帯が、炎の向きへ流れる。前側がくっきり、後ろがなだらか
                float saw = frac(i.uv.y * _Length / _Wavelength - _Flow * _Time.y * _Speed / _Wavelength);
                if (_Flow < 0) saw = 1 - saw;
                half band = pow(saw, _Sharp) * smoothstep(1.0, 0.72, saw);
                half rootEdge = _RootFade > 0.001 ? saturate(i.uv.y / _RootFade) : 1;
                half a = smoothstep(0, 0.55, side) * lerp(_Root, _Tip, i.uv.y) * smoothstep(1, 0.88, i.uv.y) * rootEdge * lerp(_Base, 1, band) * _Alpha;
                if (_Mode > 0.5)
                {
                    // 出力 s で 2*dst*s。0.5 で変わらず、_Tint*0.5 で _Tint 倍
                    // ふち（2026-11-04 てじゃ「予告線のふちは色をちょっと濃く」）：両はしの _EdgeW の所は薄くせず、濃い色に
                    half rim = _EdgeW > 0.001 ? 1 - smoothstep(_EdgeW * 0.5, _EdgeW, side) : 0;
                    half3 tint = lerp(_Tint.rgb, _EdgeTint.rgb, rim);
                    half am = _EdgeW > 0.001 ? max(a, rim * smoothstep(0, _EdgeW * 0.35, side) * lerp(_Root, _Tip, i.uv.y) * smoothstep(1, 0.88, i.uv.y) * rootEdge * _Alpha) : a;
                    half4 m = half4(lerp(half3(1, 1, 1), tint, am) * 0.5, 1);
                    UNITY_APPLY_FOG_COLOR(i.fogCoord, m, half4(0.5, 0.5, 0.5, 1));
                    return m;
                }
                fixed4 c = fixed4(_Color.rgb, a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
