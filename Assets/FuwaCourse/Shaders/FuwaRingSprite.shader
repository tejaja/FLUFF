// パーティクル用の光る輪（uv の中心から _Radius の所に輪）。色と濃さはパーティクルの色（頂点カラー）で決まる。
// キノコに乗っている間、傘のふちから上へ広がって消える輪などに使う。加算っぽく明るく見えるように少し足し合わせる
Shader "FuwaCourse/RingSprite"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.85, 0.45, 1)
        _Radius ("Ring Radius (uv)", Range(0.1, 0.5)) = 0.44
        _Width ("Ring Width (uv)", Range(0.005, 0.2)) = 0.045
        _Glow ("内側のほんのりした光", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Radius, _Width, _Glow;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5);
                half band = saturate(1 - abs(d - _Radius) / _Width);
                band = band * band * (3 - 2 * band);
                // 輪の内側にうっすら光（輪に向かって明るく）
                half inner = saturate(d / _Radius) * step(d, _Radius);
                half a = max(band, inner * inner * _Glow);
                fixed4 c = fixed4(i.color.rgb, a * i.color.a);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0, 0, 0, 0));
                return c;
            }
            ENDCG
        }
    }
}
