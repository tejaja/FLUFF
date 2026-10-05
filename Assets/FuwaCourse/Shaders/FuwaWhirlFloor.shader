// つむじかぜの真下の床に描く、うずまき模様（風に影はできないので「ここから出てる」の目印）。平らな Quad（uv 0〜1）に貼る。
// 中心へ巻き込むうずの腕がゆっくり回り、ふちはぼかす。中心はうっすら、ふちに薄い輪。
Shader "FuwaCourse/WhirlFloor"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.55
        _Arms ("Arms", Float) = 4
        _Curl ("Curl (turns from center to edge)", Float) = 0.9
        _Spin ("Spin Speed (turns/s)", Float) = 0.35
        _Sharp ("Arm Sharpness", Range(0.5, 8)) = 2
        _Inner ("Inner Fade Radius (uv)", Range(0, 0.5)) = 0.06
        _Edge ("Edge Ring Width (uv)", Range(0.005, 0.1)) = 0.03
        _SpinDir ("Spin Direction", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Alpha, _Arms, _Curl, _Spin, _Sharp, _Inner, _Edge, _SpinDir;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float r = length(p) * 2;                 // 0（中心）〜1（ふち）
                float a = atan2(p.y, p.x) / 6.2832;      // -0.5〜0.5
                // うずの腕：角度に、中心からの距離ぶんのねじれと時間の回転を足す
                float s = a * _Arms * _SpinDir + r * _Curl * _Arms - _Time.y * _Spin * _Arms;
                half arm = pow(0.5 + 0.5 * sin(s * 6.2832), _Sharp);
                half fadeIn = smoothstep(_Inner, _Inner + 0.2, r);
                half fadeOut = 1 - smoothstep(0.75, 1.0, r);
                half ring = saturate(1 - abs(r - (1 - _Edge * 2)) / _Edge) * 0.6;
                half alpha = saturate(arm * fadeIn * fadeOut + ring + 0.12 * fadeOut) * _Alpha;
                fixed4 c = fixed4(_Color.rgb, alpha * step(r, 1));
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
