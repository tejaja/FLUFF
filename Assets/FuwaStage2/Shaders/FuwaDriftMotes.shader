// 空中を漂う粒（2–3 ダイヤモンドダスト・2–2 火の粉）を、パーティクルシステムの代わりにシェーダーだけで動かす。
// 2026-11-24 てじゃ「両方のコースの漂ってるやつ、専用Shader組めないかな？」
//  メッシュ（FuwaDriftMotesBaker で作る）：粒 1 個＝板 1 枚（4 点）。点の位置＝粒の家（ワールドの置き場所）、
//   uv0＝板の角(-1〜1)、uv1＝(乱数, 回る速さ rad/s)、uv2＝速度(m/s、ワールド)、uv3＝(寿命 s, 位相 0〜1, 大きさ m, 最初の向き rad)、color＝最初の色（リニア）。
//  動き：寿命ごとに生まれ直す（位相をずらしてあるので、いつも同じくらいの数が出ている）。生まれ直すたびに家のまわり _Jitter m に飛ぶ。
//   速度で流れる＋重力（_Gravity、パーティクルの gravityModifier と同じ）＋ゆらぎ（_NoiseStr/_NoiseFreq、ノイズの代わりの sin）。
//  色と濃さ・大きさの変化は _Ramp（横＝寿命の割合、下の段＝色と濃さ、上の段の r＝大きさ÷2）。画面の _MaxScreen より大きくならない（パーティクルの Max Particle Size と同じ）。
//  CPU は何もしない・描画 1 回・毎フレームのメッシュの送り直しなし。静的バッチでワールドの位置に焼かれても同じに動く（向き・速度はワールド）
Shader "FuwaCourse/DriftMotes"
{
    Properties
    {
        _MainTex ("粒の絵", 2D) = "white" {}
        _Ramp ("寿命ごとの色・濃さ（下）と大きさ（上）", 2D) = "white" {}
        _Tint ("色をかける", Color) = (1,1,1,1)
        _Jitter ("生まれ直す時に家からずれる幅(m)", Float) = 1.5
        _Gravity ("重力の倍率（パーティクルと同じ。マイナスで上へ）", Float) = 0
        _NoiseStr ("ゆらぎの幅(m)", Float) = 0
        _NoiseFreq ("ゆらぎの速さ(回/秒)", Float) = 0.3
        _MaxScreen ("画面の横はばに対するいちばん大きい割合", Float) = 0.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
        [Toggle] _Additive ("加算（霧で黒へ）", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "DisableBatching"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex; sampler2D _Ramp;
            fixed4 _Tint;
            float _Jitter, _Gravity, _NoiseStr, _NoiseFreq, _MaxScreen, _Additive;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 corner : TEXCOORD0;
                float2 seedRot : TEXCOORD1;
                float3 vel : TEXCOORD2;
                float4 lpsr : TEXCOORD3;   // life, phase, size, rot0
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 col : COLOR; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            float3 hash3(float n) { return frac(sin(float3(n, n + 1.7, n + 3.1) * float3(43758.5453, 22578.1459, 19642.3490))); }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float life = max(v.lpsr.x, 0.05);
                float tl = _Time.y / life + v.lpsr.y;
                float age = frac(tl), cyc = floor(tl);
                float tt = age * life;
                float seed = v.seedRot.x;
                float3 home = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                float3 jit = (hash3(seed * 91.7 + cyc * 17.31) - 0.5) * (2 * _Jitter);
                float w = _NoiseFreq * 6.2831853;
                float3 nz = _NoiseStr * float3(sin(tt * w * 0.83 + seed * 37.0), sin(tt * w * 0.61 + seed * 53.0), sin(tt * w * 0.97 + seed * 71.0));
                float3 wp = home + jit + v.vel * tt + nz + float3(0, -0.5 * _Gravity * 9.81 * tt * tt, 0);

                float4 rc = tex2Dlod(_Ramp, float4(age, 0.25, 0, 0));
                float sz = tex2Dlod(_Ramp, float4(age, 0.75, 0, 0)).r * 2.0;
                float3 vp = mul(UNITY_MATRIX_V, float4(wp, 1)).xyz;
                float dist = max(-vp.z, 0.01);
                float maxS = _MaxScreen * 2.0 * dist / abs(UNITY_MATRIX_P[0][0]);   // 画面の横はばに対する割合（パーティクルの Max Particle Size と同じ）。RT や VR では P が反転していることがあるので abs
                float half_ = min(v.lpsr.z * sz, maxS) * 0.5;
                float ang = v.lpsr.w + v.seedRot.y * tt;
                float s, c; sincos(ang, s, c);
                float2 cr = float2(v.corner.x * c - v.corner.y * s, v.corner.x * s + v.corner.y * c);
                vp.xy += cr * half_;
                o.pos = mul(UNITY_MATRIX_P, float4(vp, 1));
                o.uv = v.corner * 0.5 + 0.5;
                o.col = v.color * rc * _Tint;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.col;
                if (_Additive > 0.5) { c.rgb *= c.a; UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0,0,0,0)); }
                else { UNITY_APPLY_FOG(i.fogCoord, c); }
                return c;
            }
            ENDCG
        }
    }
}
