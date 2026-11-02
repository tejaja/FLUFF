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
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Geometry+20" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -2, -2
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
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
                fixed4 c = fixed4(_Color.rgb, a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
