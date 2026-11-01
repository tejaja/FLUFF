// 炎の粒（2026-11-01 てじゃ「フワフワの炎上とまものの炎上Particle、もうちょいいい感じに」）。
// 焚き火の FuwaCourse/FlameMesh の「ちぎれて飛ぶかたまり」と同じ見た目：ふちのくっきりした丸（色は粒の頂点カラー）。不透明なので重なっても重くない
Shader "FuwaCourse/FlameBlob"
{
    Properties
    {
        _Wobble ("ふちのでこぼこ", Range(0, 0.5)) = 0.16
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "IgnoreProjector"="True" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float _Wobble;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // 角度で少しでこぼこさせた丸。粒ごとの透明度を大きさの代わりに使う（消える時に小さくなる）
                float a = atan2(i.uv.y, i.uv.x);
                float r = length(i.uv) * (1.0 + _Wobble * (0.5 * sin(a * 3.0 + i.color.a * 7.0) + 0.3 * sin(a * 5.0 - i.color.a * 11.0)));
                clip(1.0 - r);
                fixed4 c = fixed4(i.color.rgb, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
