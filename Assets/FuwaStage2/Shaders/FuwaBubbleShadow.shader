// 貝の泡の地面の影（2026-10-22 てじゃ「地面に影ないのつらい」「泡の影だから表現むずい」「黒い影だと変」）。
// 本物のシャボン玉の影のように、まっ黒ではなく「うすい水色がかった輪」＋「まん中に光が集まった明るい点」。
// 描き方は FuwaCourse/BlobDecal と同じ（見えない箱の中の、深度から求めた地面の表面だけに塗る）。_Alpha は泡の高さで FuwaClamBubbles が変える
Shader "FuwaStage2/BubbleShadow"
{
    Properties
    {
        _Shade ("Shade Color (輪・うす影)", Color) = (0.12, 0.27, 0.48, 1)
        _Glow ("Glow Color (まん中の光)", Color) = (0.92, 0.98, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _RingAlpha ("Ring Alpha", Range(0, 1)) = 0.62
        _FillAlpha ("Inner Shade Alpha", Range(0, 1)) = 0.22
        _GlowAlpha ("Glow Alpha", Range(0, 1)) = 0   // 0.75→0（2026-10-22 てじゃ「真ん中の白い◯いらない」）
        _GlowSize ("Glow Size (半径に対して)", Range(0.05, 0.6)) = 0.3
        _EdgeY ("上下のふちで薄くし始める位置", Range(0.05, 0.5)) = 0.35
        _UpMin ("この向きより横を向いた面には塗らない (dot)", Range(0, 1)) = 0.55
        _UpMax ("この向きより上を向いていれば全部塗る (dot)", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Front
        // ふわふわ（毛玉シェーダーがステンシル64番の印をつける）の上には塗らない（BlobDecal と同じ）
        Stencil { Ref 64 ReadMask 64 Comp NotEqual }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Shade, _Glow;
            float _Alpha, _RingAlpha, _FillAlpha, _GlowAlpha, _GlowSize, _EdgeY, _UpMin, _UpMax;
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
                if (camL.y < lp.y) discard;
                float r = length(lp.xz) * 2.0;                 // 0=中心 1=ふち
                if (r > 1.0) discard;
                // 輪：ふちの少し内側がいちばん濃い。内側はうすい影、まん中は光の点
                float ring = smoothstep(0.55, 0.86, r) * (1.0 - smoothstep(0.88, 1.0, r));
                float shade = max(ring * _RingAlpha, (1.0 - smoothstep(0.7, 0.95, r)) * _FillAlpha);
                float glow = (1.0 - smoothstep(0.0, _GlowSize, r)) * _GlowAlpha;
                float a = max(shade, glow);
                fixed3 col = lerp(_Shade.rgb, _Glow.rgb, saturate(glow / max(a, 1e-4)));
                float k = (1.0 - smoothstep(_EdgeY, 0.5, abs(lp.y))) * smoothstep(_UpMin, _UpMax, abs(dot(sn, up)));
                fixed4 c = fixed4(col, a * k * _Alpha);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(col, 0));
                return c;
            }
            ENDCG
        }
    }
}
