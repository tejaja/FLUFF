// ワープ床（ポータル）。円盤の上で、色のグラデーションがうずを巻きながら中心へ吸い込まれていく。
// Unityの Cylinder（半径0.5、高さ-1〜1）に貼る前提。オブジェクト空間のxzで中心からの距離と角度を出す。
Shader "Fuwa/WarpPortal"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.55, 1.0, 0.88, 1)
        _ColorB ("Color B", Color) = (0.55, 0.78, 1.0, 1)
        _ColorC ("Color C", Color) = (0.86, 0.66, 1.0, 1)
        _Speed ("Speed", Float) = 0.5
        _Swirl ("Swirl (spiral twist)", Float) = 2.5
        _Arms ("Spiral Arms", Float) = 3
        _Rings ("Rings", Float) = 3
        _Alpha ("Alpha", Range(0, 1)) = 0.9
    }
    SubShader
    {
        // DisableBatching：同じマテリアルの床がまとめて描かれる（動的バッチ）と、頂点がワールド座標になって
        // 「中心からの距離」が狂い、全部透明になって消えてしまうので止める
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _ColorA, _ColorB, _ColorC;
            float _Speed, _Swirl, _Arms, _Rings, _Alpha;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 obj : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.obj = v.vertex.xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // 3色をぐるっと循環させる（0〜1）
            fixed3 Palette(float h)
            {
                h = frac(h) * 3.0;
                fixed3 c = lerp(_ColorA.rgb, _ColorB.rgb, saturate(h));
                c = lerp(c, _ColorC.rgb, saturate(h - 1.0));
                c = lerp(c, _ColorA.rgb, saturate(h - 2.0));
                return c;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = saturate(length(i.obj.xz) * 2.0);          // 0=中心, 1=ふち
                float ang = atan2(i.obj.z, i.obj.x) / 6.2831853;     // -0.5〜0.5
                float t = _Time.y * _Speed;

                // うず：腕の数ぶんのらせんの帯が、中心へ向かって回りながら流れる
                float spiral = 0.5 + 0.5 * sin((ang * _Arms + r * _Swirl + t) * 6.2831853);
                // 中心へ吸い込まれていく輪
                float rings = 0.5 + 0.5 * sin((r * _Rings + t * 1.6) * 6.2831853);

                // 角度はちょうど1周で色も1周させる（半端だと atan2 の切れ目に線が出る）
                fixed3 col = Palette(r * 0.7 + ang - t * 0.35);
                col *= lerp(0.8, 1.2, spiral * 0.6 + rings * 0.4);
                // 中心はふわっと白く光る
                col = lerp(col, fixed3(1, 1, 1), pow(1.0 - r, 3.0) * 0.6);

                // ふちはやわらかく消える（床に溶け込むように）
                float a = _Alpha * (1.0 - smoothstep(0.8, 1.0, r)) * lerp(0.75, 1.0, rings);
                fixed4 c = fixed4(col, a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
