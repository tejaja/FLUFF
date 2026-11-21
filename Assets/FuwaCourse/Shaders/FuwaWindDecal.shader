// 風のエリアの地面の模様（流れる帯・矢じり＞＞＞）を、地面に投影して描く（2026-10-12）。
// 前は道の形に合わせた板（Fuwa/GroundHighlight）に描いていたので、道の外（芝のふち・島）には出なかった（てじゃ「風の地面エフェクトが芝に出てない」）。
// 見えない箱（Cube）の中にある「実際に見えている地面の表面」だけに描く（FuwaCourse/BlobDecal と同じやり方。深度から地面の位置を出す）。
// 箱のローカル座標（-0.5〜0.5）×箱の大きさ＝メートル。_WindDir（箱のローカルの向き）に沿った距離が帯・矢じりの流れる向き。
// プロパティ名は Fuwa/GroundHighlight と同じ（FuwaWindArea が _Color・_Phase・_FlowDir・_Stroke を書き換える）
//
// スマホ・Quest（GLES・Vulkan）は深度なしの SubShader（2026-11-21。深度を作るだけで全部の物を2回描いていたのでやめた）：
//  メッシュは「箱（uv2.y=0）＋地面に沿った網（uv2.y=1）」をひとつにした物（FuwaWindDecalHeight が高さマップから作る）。
//  PC は網をつぶして箱で深度版、スマホは箱をつぶして網をふつうの深度テストで描く（uv2.x＝地面ありの濃さ。ふちで薄く）
Shader "Fuwa/WindDecal"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.95, 0.45, 0.3)
        _WindDir ("風の向き（箱のローカル）", Vector) = (0, 0, 1, 0)
        _EdgeFade ("箱の横のふちでぼかす幅(m)", Float) = 0.6
        _FadeFront ("風下のふちでぼかす幅(m、0=ぼかさない)", Float) = 0.6
        _FadeBack ("風上のふちでぼかす幅(m、0=ぼかさない)", Float) = 0.6
        _Wavelength ("帯の間隔(m)", Float) = 2.2
        _Speed ("流れる速さ(m/s)", Float) = 3.5
        _FlowDir ("流れる向き(1/-1)", Float) = 1
        _Base ("帯の間の薄さ(0-1)", Range(0,1)) = 0.35
        _Sharp ("帯のくっきりさ", Range(1,8)) = 2.5
        [Toggle] _Chevron ("矢じり形（＞＞＞）にする", Float) = 0
        _ChevPeriod ("矢じりの横の間隔(m)", Float) = 2.4
        _ChevSlope ("矢じりのとがり具合", Float) = 0.8
        _Stroke ("矢じりの線の太さ(0-0.5)", Range(0.02, 0.5)) = 0.16
        _Phase ("矢じりの流れた距離(m、スクリプトから)", Float) = 0
        _Taper ("真ん中ほど太く（0=同じ太さ）", Range(0, 1.5)) = 0
        _Soft ("矢じりのふちのぼかし(0-1)", Range(0, 1)) = 0
        [Toggle] _ScriptPhase ("流れる速さをスクリプトが決める（風の強さで速く）", Float) = 0
        // つながった風のエリアの境目（世界座標の面 xyz=向き w=距離。dot(位置, xyz) > w の側は塗らない。0=切らない）。
        // カーブに沿って回した箱どうしが内側でくさび形に重なり、二重に塗られて濃い帯になっていた（2026-10-16 てじゃ「グラデのとこに濃い境目」）
        _CutFront ("風下の境目（スクリプトから）", Vector) = (0, 0, 0, 0)
        _CutBack ("風上の境目（スクリプトから）", Vector) = (0, 0, 0, 0)
        // 2026-11-15 てじゃ「各風の地面エフェクトのグラデーションが、アバターにもうつってる」
        //  箱（高さ1.5m、まん中が地面の0.15m上）の中の面なら何にでも塗っていたので、箱に入ったアバターの足にも模様が出ていた
        //  → 箱のまん中から上は、この高さ（m、まん中から）で消す。地面すれすれだけに塗る
        _TopStart ("上で消し始める高さ(m、箱のまん中から)", Float) = -0.12
        _TopEnd ("上で消え切る高さ(m、箱のまん中から)", Float) = -0.05
        // 2026-11-15 てじゃ「もっとギリギリに」→ A案：地面の高さマップ（FuwaWindDecalHeight が見た目のメッシュへのレイで焼く）
        //  箱のローカル xz（-0.5〜0.5）→ uv。R*256+G（16bit）＝地面の高さ（箱の高さの 0〜1）、A＝地面あり。Point で読む
        //  その場所の地面＋_HeightTol より上（アバター・草の葉など）は塗らない。_UseHeight=1 の時は _TopStart/_TopEnd は使わない
        [NoScaleOffset] _HeightMap ("地面の高さマップ（スクリプトが焼く）", 2D) = "black" {}
        _UseHeight ("高さマップを使う（スクリプト）", Float) = 0
        _HeightTol ("地面からこの高さ(m)まで塗る", Float) = 0.05
        _NetLift ("深度なし（スマホ）：網を地面から浮かせる量(m)", Float) = 0.02
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    fixed4 _Color;
    float4 _WindDir;
    float _EdgeFade, _FadeFront, _FadeBack, _Wavelength, _Speed, _FlowDir, _Base, _Sharp;
    float _Chevron, _ChevPeriod, _ChevSlope, _Stroke, _Phase, _Taper, _Soft, _ScriptPhase;
    float4 _CutFront, _CutBack;
    float _TopStart, _TopEnd;
    sampler2D _HeightMap; float _UseHeight, _HeightTol, _NetLift;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

    struct appdata { float4 vertex : POSITION; float2 uv2 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct v2f
    {
        float4 pos : SV_POSITION;
        float4 screenPos : TEXCOORD0;
        float3 worldPos : TEXCOORD1;
        float eyeDepth : TEXCOORD2;
        UNITY_FOG_COORDS(3)
        float4 lpos : TEXCOORD4;   // xyz=箱のローカル、w=地面ありの濃さ（網）
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };

    // 帯・矢じりの模様と、箱のふち・境目のぼかし（lp＝箱のローカル、surf＝世界の位置）
    fixed4 WindLook(float3 lp, float3 surf)
    {
        if (dot(surf, _CutFront.xyz) > _CutFront.w || dot(surf, _CutBack.xyz) > _CutBack.w) discard;   // 隣の箱の受け持ち
        // メートルでの位置（箱の大きさ＝物体の拡大率）
        float3 sc = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
        float2 wd = normalize(_WindDir.xz);
        // 模様は世界座標で（つながった風のエリアどうしで帯・矢じりがずれないように）
        float3 wdW3 = mul((float3x3)unity_ObjectToWorld, float3(wd.x, 0, wd.y));
        float2 wdW = normalize(wdW3.xz);
        float2 sideW = float2(wdW.y, -wdW.x);
        float ux = dot(surf.xz, wdW);     // 風下へ（前の板の uv.x と同じ向き）
        float uy = dot(surf.xz, sideW);   // 風に直角

        fixed4 c = _Color;
        // 箱のふちでぼかす（前の板の頂点カラーの代わり）。風上・風下のふちは別の幅（つながったエリアの間は0）
        float2 dEdge = (0.5 - abs(lp.xz)) * sc.xz;
        float2 sgn = sign(lp.xz);
        float ex = dot(float2(sgn.x, 0), wd), ez = dot(float2(0, sgn.y), wd);
        float wx = ex > 0.5 ? _FadeFront : (ex < -0.5 ? _FadeBack : _EdgeFade);
        float wz = ez > 0.5 ? _FadeFront : (ez < -0.5 ? _FadeBack : _EdgeFade);
        c.a *= (wx > 0.001 ? smoothstep(0, wx, dEdge.x) : 1) * (wz > 0.001 ? smoothstep(0, wz, dEdge.y) : 1);

        if (_Chevron > 0.5)
        {
            float yy = abs(frac(uy / _ChevPeriod + 0.5) - 0.5) * _ChevPeriod;
            float xx = ux + _FlowDir * _ChevSlope * yy;
            float col = floor(uy / _ChevPeriod + 0.5);
            xx += abs(fmod(col, 2.0)) * _Wavelength * 0.5;
            float q = frac(xx / _Wavelength - _Phase * _FlowDir / _Wavelength);
            float d = abs(q - 0.5);
            float aa = fwidth(xx / _Wavelength) * 1.5;
            float sw = _Stroke * lerp(1 + _Taper, 1 - _Taper * 0.6, saturate(yy / (_ChevPeriod * 0.5)));
            float soft = sw * 0.5 * _Soft;
            float stroke = 1 - smoothstep(sw * 0.5 - soft - aa, sw * 0.5 + aa, d);
            stroke *= smoothstep(_ChevPeriod * 0.5, _ChevPeriod * 0.47, yy);
            c.a *= lerp(_Base, 1.0, stroke);
        }
        else
        {
            float flow = _ScriptPhase > 0.5 ? _Phase : _Time.y * _Speed;
            float p = frac(ux / _Wavelength - flow * _FlowDir / _Wavelength);
            float saw = _FlowDir > 0 ? p : 1 - p;
            float band = pow(saw, _Sharp);
            band *= smoothstep(1.0, 0.72, saw);
            c.a *= lerp(_Base, 1.0, band);
        }
        return c;
    }

    // ===== 深度版（PC）：箱で描く。網（uv2.y=1）はつぶす =====
    v2f vert (appdata v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_TRANSFER_INSTANCE_ID(v, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        float4 ov = v.uv2.y > 0.5 ? float4(0, 0, 0, 1) : v.vertex;
        o.pos = UnityObjectToClipPos(ov);
        o.screenPos = ComputeScreenPos(o.pos);
        o.worldPos = mul(unity_ObjectToWorld, ov).xyz;
        o.eyeDepth = -UnityObjectToViewPos(ov).z;
        o.lpos = float4(ov.xyz, 1);
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
        if (camL.y < lp.y) discard;   // 下から見た時は塗らない
        float3 sc = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
        fixed4 c = WindLook(lp, surf);
        c.a *= 1.0 - smoothstep(0.3, 0.5, abs(lp.y));   // 箱の上下のふちでなめらかに消す
        if (_UseHeight > 0.5)
        {
            float4 hm = tex2Dlod(_HeightMap, float4(lp.xz + 0.5, 0, 0));
            if (hm.a < 0.5) discard;   // 地面のない所
            float gy = (hm.r * 65280.0 + hm.g * 255.0) / 65535.0 - 0.5;   // 地面の高さ（箱のローカル）
            float above = (lp.y - gy) * sc.y;
            c.a *= 1.0 - smoothstep(_HeightTol * 0.6, _HeightTol, above);   // 地面より上（アバターの足など）には塗らない
        }
        else c.a *= 1.0 - smoothstep(_TopStart, _TopEnd, lp.y * sc.y);
        c.a *= smoothstep(0.55, 0.85, abs(dot(sn, up)));   // 崖の横の面などには塗らない
        if (c.a <= 0.002) discard;
        UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(c.rgb, 0));
        return c;
    }

    // ===== 深度なし版（スマホ・Quest）：網で描く。箱（uv2.y=0）はつぶす =====
    v2f vertNet (appdata v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_TRANSFER_INSTANCE_ID(v, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        float4 ov = v.uv2.y > 0.5 ? v.vertex : float4(0, 0, 0, 1);
        float3 wp = mul(unity_ObjectToWorld, ov).xyz;
        wp += normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 1, 0))) * _NetLift;
        o.pos = UnityWorldToClipPos(wp);
        o.worldPos = wp;
        o.screenPos = 0; o.eyeDepth = 0;
        o.lpos = float4(ov.xyz, v.uv2.x);
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
    }

    fixed4 fragNet (v2f i) : SV_Target
    {
        UNITY_SETUP_INSTANCE_ID(i);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        if (i.lpos.w < 0.02) discard;
        fixed4 c = WindLook(i.lpos.xyz, i.worldPos);
        c.a *= saturate(i.lpos.w);
        if (c.a <= 0.002) discard;
        UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(c.rgb, 0));
        return c;
    }
    ENDCG

    // スマホ・Quest：深度なし（GLES・Vulkan だけ）
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Back
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma only_renderers gles gles3 vulkan
            #pragma vertex vertNet
            #pragma fragment fragNet
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDCG
        }
    }

    // PC：深度版
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Front
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            ENDCG
        }
    }
}
