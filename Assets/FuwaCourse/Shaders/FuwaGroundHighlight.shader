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
                // 風下へ流れるノコギリ波：前側がくっきり、後ろがなだらかに消える帯
                float p = frac(i.uv.x / _Wavelength - _Time.y * _Speed * _FlowDir / _Wavelength);
                float saw = _FlowDir > 0 ? p : 1 - p;
                float band = pow(saw, _Sharp);
                // 帯の先端（急に切れる所）を少しなめらかに
                band *= smoothstep(1.0, 0.9, saw);
                fixed4 c = i.color;
                c.a *= lerp(_Base, 1.0, band);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
