using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// かぜのおかの風の飾り（吹き流し・かざぐるま）を作って並べる。メッシュはここで生成して Meshes/WindDeco に保存。
// 柱は道の横の面にくっつけて立てる（道の上には乗せない）。動きは FuwaWindDeco（近くの FuwaWindArea に連動）。
public static class FuwaWindDecoBuilder
{
    const string MeshDir = "Assets/FuwaCourse/Meshes/WindDeco";
    const string MatDir = "Assets/FuwaCourse/Materials/";

    // ---- メッシュ ----
    static Mesh Save(Mesh m, string name)
    {
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "WindDeco");
        string p = MeshDir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        m.name = name;
        if (old != null)
        {
            // CopySerialized だと描画用のデータが更新されないことがあるので、中身を入れ直す
            old.Clear(); old.indexFormat = m.indexFormat; old.SetVertices(m.vertices); old.SetNormals(m.normals);
            if (m.uv.Length == m.vertexCount) old.SetUVs(0, m.uv);
            old.subMeshCount = m.subMeshCount; for (int i = 0; i < m.subMeshCount; i++) old.SetTriangles(m.GetTriangles(i), i);
            old.RecalculateBounds(); old.name = name; EditorUtility.SetDirty(old); return old;
        }
        AssetDatabase.CreateAsset(m, p); return m;
    }

    // y方向の筒（下端 y0、上端 y1）
    static void Tube(List<Vector3> V, List<int> T, float r0, float r1, float y0, float y1, int seg, bool caps)
    {
        int b = V.Count;
        for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2 / seg; V.Add(new Vector3(Mathf.Cos(a) * r0, y0, Mathf.Sin(a) * r0)); V.Add(new Vector3(Mathf.Cos(a) * r1, y1, Mathf.Sin(a) * r1)); }
        for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; int a0 = b + i * 2, a1 = a0 + 1, b0 = b + j * 2, b1 = b0 + 1; T.AddRange(new[] { a0, a1, b1, a0, b1, b0 }); }
        if (!caps) return;
        int c0 = V.Count; V.Add(new Vector3(0, y0, 0)); int c1 = V.Count; V.Add(new Vector3(0, y1, 0));
        for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; T.AddRange(new[] { c0, b + i * 2, b + j * 2 }); T.AddRange(new[] { c1, b + j * 2 + 1, b + i * 2 + 1 }); }
    }

    static Mesh Finish(List<Vector3> V, List<List<int>> subs, bool flat)
    {
        var m = new Mesh();
        if (flat)
        {
            // 面ごとに頂点を分けてカクカクに
            var nv = new List<Vector3>(); var ns = new List<List<int>>();
            foreach (var s in subs) { var t = new List<int>(); for (int i = 0; i < s.Count; i++) { t.Add(nv.Count); nv.Add(V[s[i]]); } ns.Add(t); }
            V = nv; subs = ns;
        }
        m.SetVertices(V); m.subMeshCount = subs.Count; for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 柱（高さ h）＋てっぺんの玉
    public static Mesh PoleMesh(float h)
    {
        var V = new List<Vector3>(); var T = new List<int>();
        Tube(V, T, 0.035f, 0.03f, 0f, h, 10, true);
        // 玉
        int b = V.Count; int la = 6, lo = 10; float r = 0.06f;
        for (int i = 0; i <= la; i++) for (int j = 0; j < lo; j++) { float th = Mathf.PI * i / la, ph = Mathf.PI * 2 * j / lo; V.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * r, h + 0.04f + Mathf.Cos(th) * r, Mathf.Sin(th) * Mathf.Sin(ph) * r)); }
        for (int i = 0; i < la; i++) for (int j = 0; j < lo; j++) { int a = b + i * lo + j, c = b + i * lo + (j + 1) % lo, d = a + lo, e = c + lo; T.AddRange(new[] { a, c, e, a, e, d }); }
        return Finish(V, new List<List<int>> { T }, false);
    }

    // かざぐるまの棒：先端は丸い頭（玉なし）。羽はこの先端から前へ出した軸につける
    public const float PinPoleH = 1.55f, PinPoleR = 0.03f, PinHeadDown = 0.02f, PinRotorZ = -0.085f, PinAxleR = 0.011f;
    public static Mesh PinwheelPoleMesh()
    {
        var V = new List<Vector3>(); var T = new List<int>(); const int seg = 10;
        Tube(V, T, 0.035f, PinPoleR, 0f, PinPoleH, seg, true);
        int b = V.Count; const int la = 3;
        for (int i = 0; i <= la; i++) for (int j = 0; j < seg; j++) { float th = Mathf.PI * 0.5f * i / la, ph = Mathf.PI * 2 * j / seg; V.Add(new Vector3(Mathf.Cos(th) * Mathf.Cos(ph) * PinPoleR, PinPoleH + Mathf.Sin(th) * PinPoleR, Mathf.Cos(th) * Mathf.Sin(ph) * PinPoleR)); }
        for (int i = 0; i < la; i++) for (int j = 0; j < seg; j++) { int a = b + i * seg + j, c = b + i * seg + (j + 1) % seg, d = a + seg, e = c + seg; T.AddRange(new[] { a, d, e, a, e, c }); }
        return Finish(V, new List<List<int>> { T }, false);
    }

    // かざぐるまの軸：頭の原点（棒の中心）からローカル -Z の羽の中心まで
    public static Mesh PinwheelAxleMesh()
    {
        var V = new List<Vector3>(); var T = new List<int>(); const int seg = 8;
        float z0 = 0f, z1 = PinRotorZ - 0.01f;
        for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2 / seg; var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * PinAxleR; V.Add(o + new Vector3(0, 0, z0)); V.Add(o + new Vector3(0, 0, z1)); }
        for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; int a0 = i * 2, a1 = a0 + 1, b0 = j * 2, b1 = b0 + 1; T.AddRange(new[] { a0, a1, b1, a0, b1, b0 }); }
        return Finish(V, new List<List<int>> { T }, false);
    }

    // 吹き流し：ローカル +Z へ伸びる、口から先へ細くなる筒。縞（赤・白）を2つのサブメッシュに。口の輪は3つ目
    public static Mesh SockMesh()
    {
        const float L = 1.15f, R0 = 0.17f, R1 = 0.07f; const int seg = 14, bands = 5, per = 3;
        var V = new List<Vector3>(); var red = new List<int>(); var white = new List<int>(); var ring = new List<int>();
        int rows = bands * per + 1;
        for (int k = 0; k < rows; k++)
        {
            float t = k / (float)(rows - 1); float r = Mathf.Lerp(R0, R1, t);
            for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2 / seg; V.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, t * L)); }
        }
        for (int k = 0; k < rows - 1; k++)
        {
            var lst = ((k / per) % 2 == 0) ? red : white;
            for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; int a = k * seg + i, b = k * seg + j, c = a + seg, d = b + seg; lst.AddRange(new[] { a, c, d, a, d, b }); }
        }
        // 口の金属の輪
        int rb = V.Count; float rr = 0.012f;
        for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2 / seg; var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            for (int j = 0; j < 6; j++) { float p = j * Mathf.PI * 2 / 6; V.Add(o * (R0 + Mathf.Cos(p) * rr) + new Vector3(0, 0, Mathf.Sin(p) * rr)); } }
        for (int i = 0; i < seg; i++) { int i2 = (i + 1) % seg; for (int j = 0; j < 6; j++) { int j2 = (j + 1) % 6; int a = rb + i * 6 + j, b = rb + i * 6 + j2, c = rb + i2 * 6 + j, d = rb + i2 * 6 + j2; ring.AddRange(new[] { a, d, b, a, c, d }); } }   // 2026-10-21 前は面が全部内向きで、輪の内側の壁だけ見えて二重線になっていた（てじゃ）
        return Finish(V, new List<List<int>> { red, white, ring }, false);
    }

    // 吹き流しの吊りひも：柱の横の金具（原点）から、吹き流しの口の輪（z=L、半径 R0）の4点へ細いひも＋金具の小さい輪
    public const float TetherLen = 0.3f, SockR0 = 0.17f;
    public static Mesh TetherMesh()
    {
        var V = new List<Vector3>(); var T = new List<int>();
        void Rope(Vector3 a, Vector3 b, float r)
        {
            var d = (b - a).normalized; var u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var w = Vector3.Cross(d, u);
            int bs = V.Count; const int seg = 5;
            for (int i = 0; i < seg; i++) { float an = i * Mathf.PI * 2 / seg; var o = (u * Mathf.Cos(an) + w * Mathf.Sin(an)) * r; V.Add(a + o); V.Add(b + o); }
            for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; int a0 = bs + i * 2, a1 = a0 + 1, b0 = bs + j * 2, b1 = b0 + 1; T.AddRange(new[] { a0, b0, b1, a0, b1, a1 }); }
            // 両端のふた（2026-10-21 てじゃ「つなぎの部品の裏面がみえなくて後ろが見える」）。角がとがるよう頂点は分ける
            foreach (var e in new[] { 0, 1 })
            {
                int cb = V.Count; V.Add(e == 0 ? a : b);
                for (int i = 0; i < seg; i++) V.Add(V[bs + i * 2 + e]);
                for (int i = 0; i < seg; i++) { int p = cb + 1 + i, q = cb + 1 + (i + 1) % seg; if (e == 1) T.AddRange(new[] { cb, p, q }); else T.AddRange(new[] { cb, q, p }); }
            }
        }
        var c = Vector3.zero;
        for (int k = 0; k < 4; k++) { float an = k * Mathf.PI / 2 + Mathf.PI / 4; Rope(c, new Vector3(Mathf.Cos(an) * SockR0, Mathf.Sin(an) * SockR0, TetherLen), 0.006f); }
        // 金具（よりもどしの小さい輪）：柱から横へ出る短い腕
        Rope(new Vector3(0, 0, -0.07f), c, 0.014f);
        return Finish(V, new List<List<int>> { T }, false);
    }

    // 吹き流しの柱：同じ太さの1本の棒で、地面から上へ SockPoleH 立ち上がって、角を丸く曲がって +X へ SockArm。先端に玉
    public const float SockPoleH = 2.45f, SockArm = 0.42f, SockPoleR = 0.032f, SockBallR = 0.055f, SockBend = 0.1f;
    public const float BraceDown = 0.26f, BraceOut = 0.26f, BraceR = 0.019f;   // 角の斜めの支え
    public static Mesh SockPoleMesh()
    {
        // 中心線（下→上→曲がり→横）
        var path = new List<Vector3>();
        float yb = SockPoleH - SockBend;
        path.Add(new Vector3(0, 0, 0)); path.Add(new Vector3(0, yb * 0.5f, 0)); path.Add(new Vector3(0, yb, 0));
        for (int k = 1; k < 8; k++) { float ang = k / 8f * Mathf.PI / 2; path.Add(new Vector3(SockBend - SockBend * Mathf.Cos(ang), yb + SockBend * Mathf.Sin(ang), 0)); }
        path.Add(new Vector3(SockBend, SockPoleH, 0)); path.Add(new Vector3(SockArm, SockPoleH, 0));
        var V = new List<Vector3>(); var T = new List<int>(); const int seg = 10;
        Vector3 prevN = Vector3.forward;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 d = i == 0 ? path[1] - path[0] : (i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1]); d.Normalize();
            var u = Vector3.Cross(d, Vector3.forward).normalized; var w = Vector3.Cross(d, u);
            for (int j = 0; j < seg; j++) { float an = j * Mathf.PI * 2 / seg; V.Add(path[i] + (u * Mathf.Cos(an) + w * Mathf.Sin(an)) * SockPoleR); }
        }
        for (int i = 0; i < path.Count - 1; i++) for (int j = 0; j < seg; j++) { int j2 = (j + 1) % seg; int a0 = i * seg + j, a1 = i * seg + j2, b0 = a0 + seg, b1 = a1 + seg; T.AddRange(new[] { a0, b0, b1, a0, b1, a1 }); }
        // 下のふた
        int c0 = V.Count; V.Add(path[0]); for (int j = 0; j < seg; j++) T.AddRange(new[] { c0, j, (j + 1) % seg });
        // 先端の玉
        int bb = V.Count; int la = 6, lo = 10; var bc = path[path.Count - 1];
        for (int i = 0; i <= la; i++) for (int j = 0; j < lo; j++) { float th = Mathf.PI * i / la, ph = Mathf.PI * 2 * j / lo; V.Add(bc + new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * SockBallR); }
        for (int i = 0; i < la; i++) for (int j = 0; j < lo; j++) { int a = bb + i * lo + j, c = bb + i * lo + (j + 1) % lo, d = a + lo, e = c + lo; T.AddRange(new[] { a, c, e, a, e, d }); }
        // 角の斜めの支え（柱の BraceDown 下 → 腕の BraceOut 先）。両端は柱・腕の中に少し埋める
        int braceStart = V.Count;
        var ba = new Vector3(0, SockPoleH - BraceDown, 0); var bt = new Vector3(BraceOut, SockPoleH, 0);
        var bd = (bt - ba).normalized; ba -= bd * 0.02f; bt += bd * 0.01f;
        {
            var u = Vector3.Cross(bd, Vector3.forward).normalized; var w = Vector3.Cross(bd, u); const int bs = 8;
            for (int j = 0; j < bs; j++) { float an = j * Mathf.PI * 2 / bs; var o = (u * Mathf.Cos(an) + w * Mathf.Sin(an)) * BraceR; V.Add(ba + o); V.Add(bt + o); }
            for (int j = 0; j < bs; j++) { int j2 = (j + 1) % bs; int a0 = braceStart + j * 2, a1 = a0 + 1, b0 = braceStart + j2 * 2, b1 = b0 + 1; T.AddRange(new[] { a0, b0, b1, a0, b1, a1 }); }
        }
        var m = new Mesh(); m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        // 面の向きを外向きにそろえる（中心線からの向き／玉の中心からの向き）
        var t = m.triangles; var v = m.vertices;
        for (int k = 0; k < t.Length; k += 3)
        {
            var ce = (v[t[k]] + v[t[k + 1]] + v[t[k + 2]]) / 3f; var nn = Vector3.Cross(v[t[k + 1]] - v[t[k]], v[t[k + 2]] - v[t[k]]);
            Vector3 o;
            if (t[k] >= braceStart) { var ab = bt - ba; float tt = Mathf.Clamp01(Vector3.Dot(ce - ba, ab) / ab.sqrMagnitude); o = ce - (ba + ab * tt); }
            else if (t[k] >= bb) o = ce - bc;
            else if (t[k] >= c0) o = Vector3.down;
            else { float best = 1e9f; o = Vector3.up; for (int i = 0; i < path.Count - 1; i++) { var ab = path[i + 1] - path[i]; float tt = Mathf.Clamp01(Vector3.Dot(ce - path[i], ab) / ab.sqrMagnitude); var q = path[i] + ab * tt; float dd = (ce - q).sqrMagnitude; if (dd < best) { best = dd; o = ce - q; } } }
            if (Vector3.Dot(nn, o) < 0) { int sw = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = sw; }
        }
        m.triangles = t; m.RecalculateNormals();
        return m;
    }

    // かざぐるま：中心から4枚の羽（角が手前へ反る）。羽ごとにサブメッシュ（色違い）＋中心の玉
    public static Mesh PinwheelMesh(float R)
    {
        var V = new List<Vector3>(); var subs = new List<List<int>>();
        for (int k = 0; k < 4; k++)
        {
            var T = new List<int>();
            float a0 = k * Mathf.PI / 2, a1 = a0 + Mathf.PI / 2;
            Vector3 P(float ang, float r, float z) => new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, z);
            // 正方形の紙を4つ折りにした羽：中心→外の角（反る）→次の辺の中ほど
            var c = P(0, 0, 0); var tip = P(a0, R, -0.12f); var mid = P((a0 + a1) * 0.5f, R * 0.72f, 0f); var half = P(a0 + 0.25f, R * 0.45f, -0.03f);
            int b = V.Count; V.Add(c); V.Add(half); V.Add(tip); V.Add(mid);
            T.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            subs.Add(T);
        }
        var hub = new List<int>(); int hb = V.Count; int la = 5, lo = 8; float r = 0.045f;
        for (int i = 0; i <= la; i++) for (int j = 0; j < lo; j++) { float th = Mathf.PI * i / la, ph = Mathf.PI * 2 * j / lo; V.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph) * r, Mathf.Sin(th) * Mathf.Sin(ph) * r, -0.02f + Mathf.Cos(th) * r)); }
        for (int i = 0; i < la; i++) for (int j = 0; j < lo; j++) { int a = hb + i * lo + j, cc = hb + i * lo + (j + 1) % lo, d = a + lo, e = cc + lo; hub.AddRange(new[] { a, e, cc, a, d, e }); }   // 軸の取り方が柱の玉と違うので、外向きになるよう逆回り
        // 軸（棒の中心から羽の中心まで）も真ん中の玉と同じ部分に入れる。軸は回る中心なので、羽といっしょに回っても見た目は同じ
        //（2026-10-18 別の部品だと風車1本ごとに描く回数が1つ増えていた。Vket ブースでは SetPass があふれた）
        int ab = V.Count; const int aseg = 8;
        for (int i = 0; i < aseg; i++) { float a = i * Mathf.PI * 2 / aseg; var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * PinAxleR; V.Add(o + new Vector3(0, 0, -PinRotorZ)); V.Add(o + new Vector3(0, 0, -0.01f)); }
        for (int i = 0; i < aseg; i++) { int j = (i + 1) % aseg; int a0 = ab + i * 2, a1 = a0 + 1, b0 = ab + j * 2, b1 = b0 + 1; hub.AddRange(new[] { a0, a1, b1, a0, b1, b0 }); }
        subs.Add(hub);
        return Finish(V, subs, true);
    }

    static Material Mat(string name, Color c, bool twoSided)
    {
        string p = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        var tpl = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "WebTree_Leaves.mat");
        if (m == null) { m = new Material(tpl); AssetDatabase.CreateAsset(m, p); }
        else m.CopyPropertiesFromMaterial(tpl);
        m.SetColor("_Color", c); m.SetFloat("_Cull", twoSided ? 0f : 2f); m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---- 並べる ----
    struct Spot { public float s; public float side; public bool sock; public string windPath; public Vector3 fixedDir; public int bridge; }   // bridge>0：人用の橋（かたまり3/4）の谷側のふちから、谷の上へ斜めに突き出す

    public static string Build()
    {
        var root = FuwaCourse1Builder.Root();
        var old = root.Find("WindDeco"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("WindDeco").transform; holder.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "winddeco");

        var poleSock = Save(PoleMesh(2.1f), "WindsockPole");
        var polePin = Save(PinwheelPoleMesh(), "PinwheelPole");
        var sock = Save(SockMesh(), "Windsock");
        var tether = Save(TetherMesh(), "WindsockTether");
        var sockPole = Save(SockPoleMesh(), "WindsockPoleL");
        var pin = Save(PinwheelMesh(0.42f), "Pinwheel");
        var mPole = Mat("WindDeco_Pole", new Color(0.93f, 0.91f, 0.88f), false);
        var mRed = Mat("WindDeco_SockRed", new Color(0.95f, 0.35f, 0.38f), true);
        var mWhite = Mat("WindDeco_SockWhite", new Color(0.98f, 0.96f, 0.93f), true);
        var mRing = Mat("WindDeco_Ring", new Color(0.72f, 0.70f, 0.74f), false);
        var mWood = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Course_Wood.mat");
        var mP = new[] { Mat("WindDeco_PinPink", new Color(1f, 0.62f, 0.75f), true), Mat("WindDeco_PinYellow", new Color(1f, 0.88f, 0.42f), true),
                         Mat("WindDeco_PinMint", new Color(0.55f, 0.88f, 0.76f), true), Mat("WindDeco_PinLavender", new Color(0.72f, 0.64f, 0.95f), true) };

        var m = FuwaCourse1Builder.ToCourse(root);
        Vector3 W(int pc, float s, float lat, float dy) => root.TransformPoint(m.MultiplyPoint3x4(FuwaCourse1Builder.Point(pc, s, lat, dy)));
        int Piece(float s) { var P = FuwaCourse1Builder.Pieces; for (int i = 0; i < 3; i++) if (s >= P[i].x && s <= P[i].y) return i; return 0; }

        // 横風のエリアの中心に一番近い s
        float NearestS(Vector3 p) { float best = 0, bd = 1e9f; for (float s = 0; s < FuwaCourse1Builder.PathEnd; s += 0.25f) { var q = W(Piece(s), s, 0, 0); float d = (q - p).sqrMagnitude; if (d < bd) { bd = d; best = s; } } return best; }

        var gk = root.Find("Gimmicks_Kaze");
        var spots = new List<Spot>();
        // 追い風：吹き流しとかざぐるまを左右交互に
        spots.Add(new Spot { s = 3.6f, side = 1, sock = true, windPath = "G1_Tailwind/Zone0" });
        spots.Add(new Spot { s = 6.8f, side = -1, sock = false, windPath = "G1_Tailwind/Zone1" });
        spots.Add(new Spot { s = 10.2f, side = 1, sock = false, windPath = "G1_Tailwind/Zone3" });
        spots.Add(new Spot { s = 12.8f, side = -1, sock = true, windPath = "G1_Tailwind/Zone4" });
        // 横風：風上側に吹き流し、風下側にかざぐるま
        foreach (var (g, z) in new[] { ("G2_CrossLeft", "G2_CrossLeft/CrossWind"), ("G3_CrossRight", "G3_CrossRight/CrossWind") })
        {
            var wa = gk.Find(z); if (wa == null) continue;
            float s = NearestS(wa.position);
            var area = wa.GetComponent<FuwaWindArea>();
            var dir = wa.TransformDirection(area.localDirection);
            // 道の右向き
            var r = (W(0, s, 1, 0) - W(0, s, 0, 0)).normalized;
            float upwind = Vector3.Dot(dir, r) > 0 ? -1 : 1;
            // 吹き流しは風下側（腕が道の外へ出て、吹かれると道から離れる方へたなびく）、かざぐるまは風上側
            spots.Add(new Spot { s = s - 1.6f, side = -upwind, sock = true, windPath = z });
            spots.Add(new Spot { s = s + 1.6f, side = upwind, sock = false, windPath = z });
        }
        // 突風（谷の手前）：かざぐるま
        // 突風（谷）：人用の橋の谷側のふちから、谷の上へ斜めに突き出して、強風でぷるぷる
        spots.Add(new Spot { s = (FuwaCourse1Builder.GapA1 + FuwaCourse1Builder.GapB1) * 0.5f - 0.6f, sock = false, windPath = "G3_Gust1/GustZone", bridge = 3 });
        spots.Add(new Spot { s = (FuwaCourse1Builder.GapA2 + FuwaCourse1Builder.GapB2) * 0.5f - 0.6f, sock = false, windPath = "G4_Gust2/GustZone", bridge = 4 });

        string log = "";
        int n = 0;
        foreach (var sp in spots)
        {
            int pc = sp.bridge > 0 ? sp.bridge : Piece(sp.s);
            float hw = FuwaCourse1Builder.Width(pc, sp.s) * 0.5f;
            Vector3 basePos; Quaternion baseRot = Quaternion.identity; float poleScale = 1f;
            if (sp.bridge > 0)
            {
                // 橋の谷側（右）のふちの下から、谷の中ほどへ斜めに
                basePos = W(pc, sp.s, hw - 0.05f, -0.12f);
                var headPos = W(pc, sp.s + 0.2f, hw + 0.75f, 0.55f);   // 柄は短め
                var d = headPos - basePos;
                baseRot = Quaternion.FromToRotation(Vector3.up, d.normalized);
                poleScale = d.magnitude / (PinPoleH - PinHeadDown);
            }
            else
            {
                // 柱は道の横の面にくっつける（道のふちのすぐ外、下端は道の底）。柱の太さぶんだけ外に＝面にぴったり
                //（前は 5cm 外で 1.5〜2cm すき間が見えていた。2026-10-18 てじゃ「コース外の縁にぴったりくっついてる形に」）
                float poleR = sp.sock ? SockPoleR : 0.035f;
                basePos = W(pc, sp.s, sp.side * (hw + poleR + 0.001f), -FuwaCourse1Builder.Thick);
            }
            var go = new GameObject((sp.sock ? "Windsock" : "Pinwheel") + n++);
            go.transform.SetParent(holder, true); go.transform.position = basePos; go.transform.rotation = baseRot;
            var deco = UdonSharpUndo.AddComponent<FuwaWindDeco>(go);
            var wa = gk.Find(sp.windPath); deco.wind = wa != null ? wa.GetComponent<FuwaWindArea>() : null;
            if (sp.sock)
            {
                // L字の柱（同じ太さ、先端に玉）。腕は風下へ向ける＝吹かれても吹き流しは柱から離れる方へ流れる
                Vector3 wdir = Vector3.forward;
                if (wa != null) { var area = wa.GetComponent<FuwaWindArea>(); wdir = wa.TransformDirection(area.localDirection); }
                wdir.y = 0; wdir = wdir.sqrMagnitude > 1e-4f ? wdir.normalized : Vector3.forward;
                var pole = Child(go.transform, "Pole", sockPole, new[] { mPole }, Vector3.zero);
                pole.rotation = Quaternion.LookRotation(Vector3.Cross(wdir, Vector3.up), Vector3.up);   // メッシュの +X を風下へ
                var pivot = new GameObject("SockPivot").transform; pivot.SetParent(go.transform, false);
                pivot.position = go.transform.position + Vector3.up * SockPoleH + wdir * SockArm;   // 玉の真ん中（2026-10-21 前は玉の少し下で、金具が玉の下から出ていた）
                deco.droopAway = wdir;
                Child(pivot, "Tether", tether, new[] { mRing }, new Vector3(0, 0, 0.07f));
                var so = Child(pivot, "Sock", sock, new[] { mRed, mWhite, mRing }, new Vector3(0, 0, 0.07f + TetherLen));
                deco.sock = pivot;
            }
            else
            {
                var pole = Child(go.transform, "Pole", polePin, new[] { mWood }, Vector3.zero); pole.localScale = new Vector3(1, poleScale, 1);
                var head = new GameObject("Head").transform; head.SetParent(go.transform, false);
                if (sp.bridge > 0) { deco.shake = 1f; deco.shakeAngle = 1.4f; deco.shakeSpeed = 48f; deco.spinMax = 1080f; }
                var rotor = new GameObject("Rotor").transform; rotor.SetParent(head, false);
                Child(rotor, "Blades", pin, new[] { mP[n % 4], mP[(n + 1) % 4], mP[(n + 2) % 4], mP[(n + 3) % 4], mPole }, Vector3.zero);
                deco.head = head; deco.rotor = rotor;
                FitPinwheelHead(deco);
            }
            UdonSharpEditorUtility.CopyProxyToUdon(deco);
            log += go.name + " s=" + sp.s.ToString("F1") + " wind=" + (deco.wind != null ? sp.windPath : "none") + "\n";
        }
        EditorUtility.SetDirty(root.gameObject);
        AssetDatabase.SaveAssets();
        return log;
    }

    // 羽の頭を棒の先端に合わせ、棒から前へ出した軸の先に羽をつける（羽と棒が当たらない距離）。軸は羽のメッシュの中（PinwheelMesh）
    static void FitPinwheelHead(FuwaWindDeco deco)
    {
        var pole = deco.transform.Find("Pole");
        float sy = pole != null ? pole.localScale.y : 1f;
        Undo.RecordObject(deco.head, "pinwheel"); Undo.RecordObject(deco.rotor, "pinwheel");
        deco.head.localPosition = new Vector3(0, (PinPoleH - PinHeadDown) * sy, 0);
        deco.head.localRotation = Quaternion.identity;
        deco.rotor.localPosition = new Vector3(0, 0, PinRotorZ);
        var ax = deco.head.Find("Axle"); if (ax != null) Undo.DestroyObjectImmediate(ax.gameObject);   // 前の版の別部品の軸
    }

    // シーンにあるかざぐるま全部（コース1・コース5・ほかのコピー）の棒と羽の頭を今の作りに合わせ直す（並べ直しはしない）
    [MenuItem("FuwaCourse/済み（押す前に相談）/Course1/Fit Pinwheel Heads")]
    public static string FitAllPinwheels()
    {
        Save(PinwheelPoleMesh(), "PinwheelPole");
        Save(PinwheelMesh(0.42f), "Pinwheel");
        string log = "";
        foreach (var d in Object.FindObjectsOfType<FuwaWindDeco>(true))
        {
            if (d.head == null || d.rotor == null) continue;
            FitPinwheelHead(d);
            EditorUtility.SetDirty(d.gameObject);
            log += d.transform.parent.name + "/" + d.name + "\n";
        }
        AssetDatabase.SaveAssets();
        return log;
    }

    // 吹き流しの L 字の腕を風下へ向ける（柱の根元を軸に、柱と吊り元をいっしょに回す）。風がない時も腕の先から垂れるよう droopAway も腕の向きに
    //（2026-10-18 てじゃ「ふきながしの棒のL、全部風向きと揃えよう」。チュートリアルのコピー2本が回したコピーのままでずれていた）
    public static void AlignSockArm(FuwaWindDeco d)
    {
        if (d == null || d.sock == null) return;
        var pole = d.transform.Find("Pole"); if (pole == null) return;
        Vector3 wind = d.wind != null ? d.wind.transform.TransformDirection(d.wind.localDirection) : d.fixedDirection;
        wind.y = 0; if (wind.sqrMagnitude < 1e-4f) return; wind.Normalize();
        Vector3 arm = pole.TransformDirection(Vector3.right); arm.y = 0; arm.Normalize();
        var q = Quaternion.AngleAxis(Vector3.SignedAngle(arm, wind, Vector3.up), Vector3.up);
        Undo.RecordObject(pole, "sock arm"); Undo.RecordObject(d.sock, "sock arm"); Undo.RecordObject(d, "sock arm");
        var root = d.transform.position;
        pole.position = root + q * (pole.position - root); pole.rotation = q * pole.rotation;
        d.sock.position = root + q * (d.sock.position - root); d.sock.rotation = Quaternion.LookRotation(wind, Vector3.up);
        d.droopAway = wind;
        UdonSharpEditorUtility.CopyProxyToUdon(d);
        EditorUtility.SetDirty(d.gameObject);
    }

    [MenuItem("FuwaCourse/よく使う/吹き流しの腕を風下へそろえる")]
    public static string AlignAllSockArms()
    {
        string log = "";
        foreach (var d in Object.FindObjectsOfType<FuwaWindDeco>(true)) if (d.sock != null) { AlignSockArm(d); log += d.transform.parent.name + "/" + d.name + "\n"; }
        return log;
    }

    static Transform Child(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 lp)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false); go.transform.localPosition = lp;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterials = mats; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/Course1/Build Wind Deco")]
    static void Menu() { Debug.Log(Build()); }
}
