using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// かってにスクショを「3か所×2枚」から「6か所×1枚」にする（2026-11-26 てじゃ「６箇所バラバラにするのってなんか負担あったりする？」「ちょっとやってみたいな　一応戻せるように準備も」）
//  ・今ある フォトポイント（てじゃが動かした場所も）はそのまま残して、それぞれ1枚に
//  ・道のりのすき間がいちばん空いている所に新しいポイント（PhotoPointAdd*）を足して、1コース6か所に。道順に 1〜6 枚目（slot）
//  ・撮り方（kind）と角度の種類（order）：撃った瞬間は、今まで撃った瞬間を撮っていたポイント（order 0 と 1 の2枚目）に。
//    残りは道順に 遠く(角度0) → 近く(角度1) → 後ろ上から(遠く・角度2) → 上から(近く・角度2)
//  ・戻す：メニュー「…を3か所×2枚にもどす」（足したポイントを消して slot/kind を -1 に）。git のタグ photo-3x2-before-6spots も
public static class FuwaPhotoSixSpots
{
    const int KindFar = 0, KindClose = 1, KindFire = 2, KindAvatar = 3;   // 2026-11-27 アバター寄り（てじゃ「アバター寄りを多めに、引きは1〜2枚」）
    const string AddPrefix = "PhotoPointAdd";

    [MenuItem("FuwaCourse/済み（押す前に相談）/かってにスクショ：6か所×1枚にする")]
    static void MenuMake() { Debug.Log(Make()); }
    [MenuItem("FuwaCourse/済み（押す前に相談）/かってにスクショ：3か所×2枚にもどす")]
    static void MenuRevert() { Debug.Log(Revert()); }

    public static string Revert()
    {
        int del = 0, reset = 0;
        foreach (var p in Object.FindObjectsOfType<FuwaPhotoPoint>(true).ToList())
        {
            if (p.name.StartsWith(AddPrefix)) { Undo.DestroyObjectImmediate(p.gameObject); del++; continue; }
            if (p.slot < 0 && p.kind < 0) continue;
            Undo.RecordObject(p, "photo 3x2");
            p.slot = -1; p.kind = -1;
            if (p.name.EndsWith("_o")) { }   // 名前はそのまま
            UdonSharpEditorUtility.CopyProxyToUdon(p); EditorUtility.SetDirty(p); reset++;
        }
        // 角度の種類（order）を元にもどす（Make で変えた分）
        foreach (var p in Object.FindObjectsOfType<FuwaPhotoPoint>(true))
        {
            int o = OrigOrder(p); if (o < 0 || o == p.order) continue;
            Undo.RecordObject(p, "photo 3x2"); p.order = o; UdonSharpEditorUtility.CopyProxyToUdon(p); EditorUtility.SetDirty(p);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "[かってにスクショ] 3か所×2枚にもどした（足したポイント " + del + " 個を消した、" + reset + " 個を元の撮り方に）";
    }

    // 元の order は名前の数字（PhotoPoint1/1b → 0、2 → 1、3 → 2）
    static int OrigOrder(FuwaPhotoPoint p)
    {
        if (!p.name.StartsWith("PhotoPoint") || p.name.StartsWith(AddPrefix)) return -1;
        string n = p.name.Substring("PhotoPoint".Length);
        if (n.StartsWith("1")) return 0; if (n.StartsWith("2")) return 1; if (n.StartsWith("3")) return 2;
        return -1;
    }

    public static string Make()
    {
        Revert();
        var album = Object.FindObjectsOfType<FuwaPhotoAlbum>(true).FirstOrDefault();
        if (album == null) return "FuwaPhotoAlbum がない";
        var log = new List<string>();
        var byCourse = Object.FindObjectsOfType<FuwaPhotoPoint>(true).GroupBy(p => p.transform.root).OrderBy(g => g.Key.name);
        foreach (var g in byCourse)
        {
            var root = g.Key;
            bool wasActive = root.gameObject.activeSelf;
            root.gameObject.SetActive(true); Physics.SyncTransforms();
            try { log.Add(root.name + ": " + MakeCourse(root, g.ToList(), album)); }
            finally { root.gameObject.SetActive(wasActive); }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "[かってにスクショ] 6か所×1枚にした\n" + string.Join("\n", log);
    }

    class Node { public Vector3 pos; public FuwaPhotoPoint pt; public bool isEnd; }

    static string MakeCourse(Transform root, List<FuwaPhotoPoint> pts, FuwaPhotoAlbum album)
    {
        var start = root.Find("Start/BallStart") ?? root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BallStart");
        var goal = root.GetComponentsInChildren<FuwaGoal>(true).FirstOrDefault();
        if (start == null || goal == null) return "スタートかゴールがない";
        // 道順：order、同じ order なら1枚目を撮る方が先（1 → 1b）
        var ordered = pts.OrderBy(p => OrigOrder(p) < 0 ? p.order : OrigOrder(p)).ThenBy(p => p.takeFirst ? 0 : 1).ThenBy(p => p.name).ToList();
        var nodes = new List<Node> { new Node { pos = start.position, isEnd = true } };
        nodes.AddRange(ordered.Select(p => new Node { pos = p.transform.position, pt = p }));
        nodes.Add(new Node { pos = goal.transform.position, isEnd = true });
        var cps = root.GetComponentsInChildren<FuwaCheckpoint>(true).Select(c => c.GetComponent<BoxCollider>()).Where(b => b != null).Select(b => b.bounds.center).ToList();
        var holder = ordered[0].transform.parent;
        _root = root;
        int layer = ordered[0].gameObject.layer;

        // すき間がいちばん長い所に足す（ゴールの手前のすき間は半分に数える：ゴール前のポイントの後ろはゴールの島）
        int add = 0;
        while (nodes.Count(n => !n.isEnd) < 6 && add < 6)
        {
            int best = -1; float bestLen = 0f;
            for (int i = 0; i + 1 < nodes.Count; i++)
            {
                float len = Vector3.Distance(nodes[i].pos, nodes[i + 1].pos);
                if (i + 1 == nodes.Count - 1) len *= 0.5f;
                if (i == 0) len *= 0.8f;   // スタート直後も少し控えめに
                if (len > bestLen) { bestLen = len; best = i; }
            }
            if (best < 0 || bestLen < 6f) break;
            Vector3 a = nodes[best].pos, b = nodes[best + 1].pos;
            var spot = FindSpot(a, b, cps);
            add++;
            nodes.Insert(best + 1, new Node { pos = spot });
        }

        // 撮り方を配る：今あるポイントは今までの1枚目の撮り方（場所と構図を決めた時の主役の1枚）。2枚目しか撮らないポイント（1b）は今までの2枚目
        //  足したポイントには、残りの撮り方（6枚の組み合わせ：遠く0・撃った瞬間0・近く1・撃った瞬間1・後ろ上から2・上から2）を道順に
        var spots = nodes.Where(n => !n.isEnd).ToList();
        var all = new List<(int kind, int order)> { (KindFar, 0), (KindFire, 0), (KindClose, 1), (KindFire, 1), (KindAvatar, 2), (KindAvatar, 1) };   // 2026-11-27 後ろ上から・上から → アバター寄り2枚
        var assign = new Dictionary<Node, (int kind, int order)>();
        foreach (var n in spots)
        {
            if (n.pt == null) continue;
            int o = OrigOrder(n.pt); if (o < 0) o = Mathf.Clamp(n.pt.order, 0, 2);
            (int, int) k = n.pt.takeFirst ? (o == 0 ? (KindFar, 0) : o == 1 ? (KindClose, 1) : (KindAvatar, 2))
                                          : (o <= 1 ? (KindFire, o) : (KindAvatar, 1));
            if (!all.Contains(k)) k = all[0];
            all.Remove(k); assign[n] = k;
        }
        var rest = new Queue<(int kind, int order)>(all);
        var made = new List<string>();
        for (int i = 0; i < spots.Count; i++)
        {
            var n = spots[i];
            var (kind, order) = assign.TryGetValue(n, out var kk) ? kk : (rest.Count > 0 ? rest.Dequeue() : (KindClose, 1));
            if (n.pt == null)
            {
                Vector3 prev = spots.Take(i).Select(s => s.pos).DefaultIfEmpty(start.position).Last();
                Vector3 next = i + 1 < spots.Count ? spots[i + 1].pos : goal.transform.position;
                Vector3 dir = next - prev; dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
                string nm = AddPrefix + (i + 1);
                FuwaPhotoPointBuilder.Make(holder, nm, order, n.pos, dir, 8f, layer, album, "", "");
                n.pt = holder.Find(nm).GetComponent<FuwaPhotoPoint>();
                n.pt.gameObject.layer = layer;
                Undo.RegisterCreatedObjectUndo(n.pt.gameObject, "photo 6");
            }
            var p = n.pt;
            Undo.RecordObject(p, "photo 6");
            p.slot = i; p.kind = kind; p.order = order;
            UdonSharpEditorUtility.CopyProxyToUdon(p); EditorUtility.SetDirty(p);
            made.Add((i + 1) + ":" + p.name + "(" + (kind == KindFire ? "撃" : kind == KindFar ? "遠" : kind == KindAvatar ? "人" : "近") + order + ")");
        }
        return string.Join(" ", made);
    }

    // a と b の間で、ふわふわが通る道（レイヤー11＝道、このコースの物）の上の点。道が曲がっていても拾えるよう、a・b のまわりを 1.5m 間隔で調べて
    //  「a までと b までの距離の合計が小さく（遠回りでない）、両方への距離が同じくらい」の点を選ぶ。
    //  上が空いていない所（トンネル・岩の中）と、チェックポイントのそば（7m以内、光るキノコではさまれる）はやめる
    static Transform _root;
    static Vector3 FindSpot(Vector3 a, Vector3 b, List<Vector3> cps)
    {
        const int PathMask = 1 << 11; const int AllMask = (1 << 0) | (1 << 11);
        Vector3 mn = Vector3.Min(a, b) - new Vector3(15f, 0f, 15f), mx = Vector3.Max(a, b) + new Vector3(15f, 0f, 15f);
        float top = Mathf.Max(a.y, b.y) + 4f, depth = top - (Mathf.Min(a.y, b.y) - 12f);
        float L = Vector3.Distance(a, b);
        Vector3 best = a + (b - a) * 0.5f; float bestScore = 1e9f;
        for (float x = mn.x; x <= mx.x; x += 1.5f)
            for (float z = mn.z; z <= mx.z; z += 1.5f)
            {
                var hits = Physics.RaycastAll(new Vector3(x, top, z), Vector3.down, depth, PathMask, QueryTriggerInteraction.Ignore);
                foreach (var h in hits)
                {
                    if (h.normal.y < 0.6f || h.collider.transform.root != _root) continue;
                    Vector3 g = h.point;
                    Vector3 c = g + Vector3.up * 3.5f;
                    float da = Vector3.Distance(c, a), db = Vector3.Distance(c, b);
                    if (da < L * 0.25f || db < L * 0.25f) continue;
                    if (da + db > L * 1.6f) continue;
                    if (cps.Any(cp => Vector3.Distance(new Vector3(cp.x, g.y, cp.z), g) < 7f && Mathf.Abs(cp.y - g.y) < 6f)) continue;
                    if (Physics.Raycast(g + Vector3.up * 0.5f, Vector3.up, 10f, AllMask, QueryTriggerInteraction.Ignore)) continue;   // 上に屋根・岩
                    float score = (da + db) + Mathf.Abs(da - db) * 0.7f;
                    if (score < bestScore) { bestScore = score; best = c; }
                }
            }
        return best;
    }
}
