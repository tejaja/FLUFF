// 足元の丸い影（ブロブシャドウ）をデカールで描く。見えない箱（Cube）の中にある「実際に見えている地面の表面」だけに影を塗る。
// 板を置く方式と違って、崖のふちからはみ出した所（箱の中に地面が無い所）は描かれないので、空中に影が浮かない。坂や段差にも沿う。
// 箱のローカル座標：x・z が影の丸（-0.5〜0.5）、y が地面の向き（-0.5〜0.5 の範囲にある表面だけ塗る）。
// 地面の位置はカメラの深度（_CameraDepthTexture）から求める。VRChat ではリアルタイムの影を落とす平行光源があると深度が作られる。
Shader "FuwaCourse/BlobDecal"
{
    Properties
    {
        _Color ("Color", Color) = (0.12, 0.08, 0.2, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.6
        _FalloffStart ("Falloff Start (0=中心からすぐ薄く, 1=ふちまで濃い)", Range(0, 0.95)) = 0.35
        _Power ("Falloff Power", Range(0.5, 3)) = 1
        _EdgeY ("上下のふちで薄くし始める位置", Range(0.05, 0.5)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always   // 箱の面ではなく、深度から求めた地面の位置で判定する（箱の中にカメラが入っても描けるように裏面を描く）
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
            float _Alpha, _FalloffStart, _Power, _EdgeY;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
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
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float raw = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                float sceneDepth = LinearEyeDepth(raw);
                // カメラから箱の面の点へのまっすぐな線を、見えている表面の深さまで伸ばす
                float3 ray = i.worldPos - _WorldSpaceCameraPos;
                float3 surf = _WorldSpaceCameraPos + ray * (sceneDepth / max(i.eyeDepth, 1e-4));
                // 表面の向き（深度から求めた位置の変化から）。discard より前で計算する
                float3 sn = normalize(cross(ddy(surf), ddx(surf)));
                float3 up = normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0)));
                float3 lp = mul(unity_WorldToObject, float4(surf, 1)).xyz;
                if (abs(lp.x) > 0.5 || abs(lp.z) > 0.5 || abs(lp.y) > 0.5) discard;
                float r = length(lp.xz) * 2.0;                 // 0=中心 1=ふち
                if (r > 1.0) discard;
                float a = 1.0 - smoothstep(_FalloffStart, 1.0, r);
                a = pow(a, _Power);
                a *= 1.0 - smoothstep(_EdgeY, 0.5, abs(lp.y));  // 箱の上下のふちでなめらかに消す
                a *= smoothstep(0.55, 0.85, abs(dot(sn, up)));   // 崖の横の面など、地面と違う向きの面には塗らない
                fixed4 c = fixed4(_Color.rgb, a * _Alpha);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(_Color.rgb, 0));
                return c;
            }
            ENDCG
        }
    }
}
