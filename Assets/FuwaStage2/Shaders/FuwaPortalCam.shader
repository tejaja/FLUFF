// 2026-11-14 カメラ式ポータルの穴（てじゃ「ワープ前にカーテンとかクモの巣とかみえない」「明るさ」→ カメラ式に）
//  出口側のカメラ（左目・右目、デスクトップは左だけ）の映像を、穴の面のワールド座標をそのカメラで投影した位置から読む。
//  _VPL / _VPR：入口側のワールド座標 → カメラのクリップ座標（＝カメラの投影 × カメラの view × 入口→出口の変換）。FuwaPortalCams が毎フレーム入れる
//  _Active = 0 の間（カメラが別のポータルを映している・遠い）は、暗いうず（_Idle）だけ
Shader "Fuwa/PortalCam"
{
    Properties
    {
        _TexL ("Left", 2D) = "black" {}
        _TexR ("Right", 2D) = "black" {}
        _Idle ("Idle Color", Color) = (0.18, 0.22, 0.32, 1)
        _IdleGlow ("Idle Glow", Color) = (0.45, 0.8, 1, 1)
        _Active ("Active", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _TexL, _TexR;
            float4x4 _VPL, _VPR;
            float4 _Idle, _IdleGlow;
            float _Active;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; float3 lpos : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.lpos = v.vertex.xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // 待機：中心が明るい暗いうず
                float r = length(i.lpos.xy * float2(1.0, 0.6));
                float a = atan2(i.lpos.y, i.lpos.x);
                float sw = 0.5 + 0.5 * sin(a * 3.0 + r * 6.0 - _Time.y * 2.0);
                float4 idle = lerp(_IdleGlow, _Idle, saturate(r * 0.9)) * (0.85 + 0.15 * sw);
                if (_Active < 0.5) return idle;
                float4x4 vp = unity_StereoEyeIndex == 0 ? _VPL : _VPR;
                float4 c = mul(vp, float4(i.wpos, 1.0));
                if (c.w <= 0.0001) return idle;
                float2 uv = c.xy / c.w * 0.5 + 0.5;
                float4 col = unity_StereoEyeIndex == 0 ? tex2D(_TexL, uv) : tex2D(_TexR, uv);
                return float4(col.rgb, 1.0);
            }
            ENDCG
        }
    }
}
