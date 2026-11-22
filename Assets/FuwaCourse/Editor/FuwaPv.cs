using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 紹介PV（2026-11-20）。ショットの一覧は Assets/FuwaCourse/PV/pv_shots.json（カメラのキー：t 秒・p 位置・l 見る点・fov）。
// ・絵コンテ：Play しなくても、ショットの頭・真ん中・終わりを静止画で撮る（エリアの出し入れをして、撮ったら元に戻す）
// ・録画：Play（ClientSim）中に FuwaPvRecorder を置いて、1コマずつ撮って mp4 に。出力は D:/Claude_Workspace/fluff_video/pv2/
// エリアは FuwaAreaVisibility のリストをそのまま使う（ステージ1を撮る時はステージ2・入口・あそびばを隠す。てじゃ「ステージ２がうつらないように」）
public static class FuwaPv
{
    public const string ShotsPath = "Assets/FuwaCourse/PV/pv_shots.json";
    public const string OutDir = "D:/Claude_Workspace/fluff_video/pv2";
    public static string LogPath => System.IO.Path.GetFullPath("Library/pv_state.txt");

    public static FuwaPvList Load() => JsonUtility.FromJson<FuwaPvList>(System.IO.File.ReadAllText(ShotsPath));

    static IEnumerable<FuwaPvShot> Pick(FuwaPvList list, string names)
    {
        if (string.IsNullOrEmpty(names)) return list.shots.Where(s => !s.skip);
        var want = names.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        return list.shots.Where(s => want.Any(w => s.name == w || (w.EndsWith("*") && s.name.StartsWith(w.TrimEnd('*')))));
    }

    // ===== エリアの出し入れ =====
    static GameObject FindPath(string path)
    {
        var parts = path.Split('/');
        foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (r.name != parts[0]) continue;
            if (parts.Length == 1) return r;
            var t = r.transform.Find(string.Join("/", parts.Skip(1))); if (t) return t.gameObject;
        }
        return null;
    }

    // restore != null なら、さわる前の状態を記録（Play していない時に元へ戻す用）
    public static void ApplyArea(FuwaPvShot s, FuwaPvList list, Dictionary<GameObject, bool> restore)
    {
        var av = Object.FindObjectsOfType<FuwaAreaVisibility>(true).FirstOrDefault();
        var on = new List<GameObject>(); var off = new List<GameObject>();
        void Add(GameObject[] a, bool show) { if (a == null) return; foreach (var g in a) if (g) (show ? on : off).Add(g); }
        if (av)
        {
            string ar = s.area ?? "stage1";
            Add(av.hubObjects, ar == "hub"); Add(av.soloObjects, ar == "stage1"); Add(av.stage2Objects, ar == "stage2");
            Add(av.playgroundObjects, ar == "playground"); Add(av.debugObjects, false); Add(av.bonusObjects, false);
            Add(av.bonusLites, false); Add(av.bonusFarObjects, ar == "stage1");
        }
        void Set(GameObject g, bool v)
        {
            if (!g || g.activeSelf == v) return;
            if (restore != null && !restore.ContainsKey(g)) restore[g] = g.activeSelf;
            g.SetActive(v);
        }
        foreach (var g in off) if (!on.Contains(g)) Set(g, false);
        foreach (var g in on) Set(g, true);
        foreach (var p in (list?.hide ?? new string[0]).Concat(s.hide ?? new string[0])) Set(FindPath(p), false);
        foreach (var p in s.show ?? new string[0]) Set(FindPath(p), true);
    }

    static void Restore(Dictionary<GameObject, bool> r) { foreach (var kv in r) if (kv.Key) kv.Key.SetActive(kv.Value); }

    // ===== 絵コンテ =====
    [MenuItem("FuwaCourse/PV/絵コンテを撮る（全部）")]
    static void BoardMenu() { Debug.Log(Board(null)); }

    // names：カンマ区切り（末尾 * で前方一致）、空なら skip 以外ぜんぶ。times：ショットの中の割合
    public static string Board(string names, string times = "0,0.5,1", int w = 960, int h = 540)
    {
        var list = Load();
        var ts = times.Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        string dir = OutDir + "/board"; System.IO.Directory.CreateDirectory(dir);
        var camGo = new GameObject("_PvBoardCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = camGo.AddComponent<Camera>(); cam.enabled = false; cam.nearClipPlane = 0.05f; cam.farClipPlane = 4000f; cam.allowHDR = false;
        cam.cullingMask = FuwaPvRecorder.CullingMask;
        var main = Camera.main; if (main) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var restore = new Dictionary<GameObject, bool>();
        var made = new List<string>();
        try
        {
            foreach (var s in Pick(list, names))
            {
                ApplyArea(s, list, restore);
                for (int i = 0; i < ts.Length; i++)
                {
                    FuwaPvPath.Place(cam, s, ts[i] * s.dur);
                    cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = prev;
                    string p = $"{dir}/{s.name}_{i}.png";
                    System.IO.File.WriteAllBytes(p, tex.EncodeToPNG()); made.Add(p);
                }
                // 次のショットの前に一度戻す（エリアがちがうショットが続いても正しく）
                Restore(restore); restore.Clear();
            }
        }
        finally
        {
            Restore(restore);
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(camGo);
        }
        return $"[PV] 絵コンテ {made.Count} 枚 → {dir}";
    }

    // ===== 録画（Play 中）=====
    [MenuItem("FuwaCourse/PV/録画する（全部・Play中）")]
    static void RecordMenu() { Debug.Log(Record(null)); }

    static readonly Dictionary<string, Vector3> Stand = new Dictionary<string, Vector3>
    {
        { "hub", new Vector3(0f, 158.1f, -48.2f) },
        { "stage1", new Vector3(13f, 8.2f, -16f) },
        { "stage2", new Vector3(13f, -161.8f, -16f) },
        { "playground", new Vector3(-72f, 88.2f, 80f) },
    };

    // PV 用に軽量化を全部切る（2026-11-20 てじゃ「動くものフル稼働、軽量化処理無し」「軽量化モデルにかわるやつのこと」）。Play 中だけ。
    // ・FuwaCourseLod（他のコースを床だけの軽い版に・ロビーで仕掛けを止める）を止めて、軽い版を消し・仕掛けと粒を全部動かす
    // ・FuwaVisibleGate（映っていない間は止める）を止めて全部動かす
    // ・FuwaFarShadows（遠い物は影なし）を止めて影を全部出す
    // ・LOD は一番きれいなまま（lodBias を大きく。撮り終わったら戻す）
    static float _lodBiasBack = -1f;
    public static string FullMode()
    {
        int n = 0;
        void Stop(UdonSharp.UdonSharpBehaviour p) { var ub = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(p); if (ub) ub.enabled = false; n++; }
        // 影の軽量化は止めてもまた動き出した（2026-11-20 2-5 の石像の影がチカチカ、影つきが 441→100 に減っていった）→ Play 中だけ UdonBehaviour ごと消す
        void Kill(UdonSharp.UdonSharpBehaviour p) { var ub = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(p); if (ub) Object.Destroy(ub); n++; }
        foreach (var l in Object.FindObjectsOfType<FuwaCourseLod>(true))
        {
            Stop(l);
            if (l.liteRoots != null) foreach (var g in l.liteRoots) if (g) g.SetActive(false);
            if (l.lobbyStopBehaviours != null) foreach (var b in l.lobbyStopBehaviours) if (b) b.enabled = true;
            if (l.lobbyHideObjects != null) foreach (var g in l.lobbyHideObjects) if (g) g.SetActive(true);
            // そのコースにいる時だけ動く物（風の地面の模様など）：止めた時にいなかったコースの分は止まったままになっていた（2026-11-22 1-1 の風の模様が止まって撮れた）→ 全部動かす
            if (l.courseOnlyBehaviours != null) foreach (var b in l.courseOnlyBehaviours) if (b) b.enabled = true;
        }
        // 番人は止めても「映らなくなった」の知らせ（OnBecameInvisible）は届いて仕掛けを止めてしまう（1-2 の岩が途中で止まった）。
        // target を外しても止まったので、Play 中だけ番人の UdonBehaviour ごと消す（Play を抜ければ元どおり）
        foreach (var v in Object.FindObjectsOfType<FuwaVisibleGate>(true))
        {
            var t = v.target;
            var ub = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(v); if (ub) { Object.Destroy(ub); n++; }
            if (t) t.enabled = true;
        }
        int sh = 0;
        foreach (var f in Object.FindObjectsOfType<FuwaFarShadows>(true))
        {
            Kill(f);
            if (f.targets != null) foreach (var r in f.targets) if (r) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; sh++; }
        }
        // 2–5 の段ごとの影（自分の段より下・遠い物は影なし）も軽量化 → プレイヤーをカメラに運ぶと影がチカチカした
        foreach (var f in Object.FindObjectsOfType<FuwaTierShadows>(true))
        {
            Kill(f);
            if (f.targets != null) foreach (var r in f.targets) if (r) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; sh++; }
        }
        if (_lodBiasBack < 0) _lodBiasBack = QualitySettings.lodBias;
        QualitySettings.lodBias = 100f;
        return $"[PV] 軽量化オフ（止めた {n}、影を戻した {sh}、lodBias {_lodBiasBack}→100）";
    }
    static void OnPlayMode(PlayModeStateChange s) { if (s == PlayModeStateChange.ExitingPlayMode) RestoreQuality(); }
    public static void RestoreQuality() { if (_lodBiasBack >= 0) { QualitySettings.lodBias = _lodBiasBack; _lodBiasBack = -1f; } }

    public static string Record(string names, int supersample = 1, int fps = 60, int crf = 14, string sub = "shots")
    {
        if (!EditorApplication.isPlaying) return "[PV] Play 中に呼んでね";
        string full = FullMode();
        // ClientSim の最初の「Accept」を閉じる（頭についてくるので、プレイヤーをカメラに運ぶショットで写った）
        foreach (var b in Object.FindObjectsOfType<UnityEngine.UI.Button>()) if (b.name == "AcceptButton" && b.transform.root.name == "__ClientSimSystem") b.onClick.Invoke();
        var list = Load();
        var shots = Pick(list, names).ToList();
        if (shots.Count == 0) return "[PV] ショットがない";
        System.IO.File.WriteAllText(LogPath, "");
        var go = new GameObject("_PvRecorder");
        var rec = go.AddComponent<FuwaPvRecorder>();
        rec.shots = shots; rec.supersample = supersample; rec.fps = fps; rec.crf = crf;
        rec.outDir = OutDir + "/" + sub; rec.logPath = LogPath;
        rec.applyArea = s => ApplyArea(s, list, null);
        rec.onFinished = RestoreQuality;
        rec.onFrame = (s, cam) =>
        {
            if (!s.followPlayer) return;
            var lp = VRC.SDKBase.Networking.LocalPlayer; if (lp == null) return;
            var eye = lp.GetTrackingData(VRC.SDKBase.VRCPlayerApi.TrackingDataType.Head).position - lp.GetPosition();
            var fwd = cam.transform.forward; fwd.y = 0;
            lp.TeleportTo(cam.transform.position - eye, Quaternion.LookRotation(fwd.sqrMagnitude > 1e-4f ? fwd : Vector3.forward));
        };
        // send：決まった秒に Udon のイベントを送る（"秒|パス|イベント|回数"）。1コマ前の秒をまたいだら送る
        float lastT = float.NegativeInfinity; FuwaPvShot lastS = null;
        rec.onTime = (s, t) =>
        {
            if (s != lastS) { lastS = s; lastT = float.NegativeInfinity; }
            if (s.send != null)
                foreach (var e in s.send)
                {
                    var a = e.Split('|'); if (a.Length < 3 || !float.TryParse(a[0], out var at)) continue;
                    if (!(lastT < at && t >= at)) continue;
                    var obj = FindPath(a[1]); var ub = obj ? obj.GetComponent<VRC.Udon.UdonBehaviour>() : null;
                    // "秒|パス|set:変数名|値"：Udon の変数を書きかえる（2026-11-20 1-4 のボスを決まった秒で起こす）
                    if (a[2].StartsWith("set:"))
                    {
                        if (!ub || a.Length < 4) { Debug.LogWarning("[PV] set の相手か値がない: " + e); continue; }
                        // 型は今入っている値から取る（public の変数は GetProgramVariableType が Object を返して、文字列のまま入れると Udon が止まった）
                        string vn = a[2].Substring(4); var ty = ub.GetProgramVariable(vn)?.GetType() ?? ub.GetProgramVariableType(vn);
                        object val = ty == typeof(bool) ? (object)bool.Parse(a[3]) : ty == typeof(int) ? (object)int.Parse(a[3]) : ty == typeof(float) ? (object)float.Parse(a[3]) : a[3];
                        ub.SetProgramVariable(vn, val);
                        continue;
                    }
                    int n = a.Length >= 4 && int.TryParse(a[3], out var c) ? c : 1;
                    if (ub) for (int k = 0; k < n; k++) ub.SendCustomEvent(a[2]);
                    else Debug.LogWarning("[PV] send の相手が見つからない: " + a[1]);
                }
            lastT = t;
        };
        EditorApplication.playModeStateChanged -= OnPlayMode; EditorApplication.playModeStateChanged += OnPlayMode;
        rec.onShotStart = s =>
        {
            var p = s.player != null && s.player.Length >= 3 ? new Vector3(s.player[0], s.player[1], s.player[2]) : (Stand.TryGetValue(s.area ?? "stage1", out var v) ? v : Vector3.zero);
            var lp = VRC.SDKBase.Networking.LocalPlayer;
            if (lp != null) lp.TeleportTo(p, Quaternion.identity);
            if (s.boulderAt >= 0)
            {
                // FuwaBoulder は Time.time + phaseOffset で回っている。並びの差はそのままで全体をずらす
                var bs = Object.FindObjectsOfType<FuwaBoulder>(true).Select(b => UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(b)).Where(b => b).ToList();
                if (bs.Count > 0)
                {
                    float period = (float)bs[0].GetProgramVariable("period"), emerge = (float)bs[0].GetProgramVariable("emergeTime");
                    float T = Time.time + s.preroll + s.boulderAt;
                    float shift = emerge - Mathf.Repeat(T + (float)bs[0].GetProgramVariable("phaseOffset"), period);
                    foreach (var b in bs) b.SetProgramVariable("phaseOffset", (float)b.GetProgramVariable("phaseOffset") + shift);
                }
            }
        };
        return full + $"\n[PV] 録画開始 {shots.Count} ショット（{string.Join(",", shots.Select(s => s.name))}）→ {rec.outDir}　ログ {LogPath}";
    }

    // コースの道の中心線（スタート→ゴール、だいたい step m おき）。道レイヤー（11）のメッシュの頂点を 2m のマスにまとめて、
    // となりのマス（高さの差 1.5m まで）をつないだ最短の道をたどる。カメラを「道にそって」動かすショット用
    public static List<Vector3> CoursePath(string root, float step = 4f, float cell = 2f)
    {
        var r = FindPath(root); if (!r) return null;
        var pts = new Dictionary<Vector3Int, (Vector3 sum, int n)>();
        foreach (var mf in r.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.gameObject.layer != 11 || !mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
            var m = mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = m.MultiplyPoint3x4(v); var k = new Vector3Int(Mathf.FloorToInt(w.x / cell), Mathf.FloorToInt(w.y / 1.5f), Mathf.FloorToInt(w.z / cell));
                pts.TryGetValue(k, out var a); pts[k] = (a.sum + w, a.n + 1);
            }
        }
        if (pts.Count == 0) return null;
        var keys = pts.Keys.ToList(); var pos = keys.ToDictionary(k => k, k => pts[k].sum / pts[k].n);
        var st = r.transform.Find("Start"); var go = r.transform.Find("Goal");
        Vector3Int Near(Vector3 p) => keys.OrderBy(k => (pos[k] - p).sqrMagnitude).First();
        var s = Near(st ? st.position : r.transform.position); var g = Near(go ? go.position : r.transform.position);
        var dist = new Dictionary<Vector3Int, float> { [s] = 0 }; var prev = new Dictionary<Vector3Int, Vector3Int>();
        var open = new SortedSet<(float, int, int, int)> { (0, s.x, s.y, s.z) };
        while (open.Count > 0)
        {
            var c = open.Min; open.Remove(c); var k = new Vector3Int(c.Item2, c.Item3, c.Item4);
            if (k == g) break;
            // すき間（飛び越える所・橋の切れ目）も渡れるように 2 マス先まで（遠いほど損）
            for (int dx = -3; dx <= 3; dx++) for (int dy = -2; dy <= 2; dy++) for (int dz = -3; dz <= 3; dz++)
            {
                var nk = new Vector3Int(k.x + dx, k.y + dy, k.z + dz); if (nk == k || !pos.ContainsKey(nk)) continue;
                float e = (pos[nk] - pos[k]).magnitude; if (e > cell * 1.8f) e *= 3f;
                float d = dist[k] + e;
                if (dist.TryGetValue(nk, out var od) && od <= d) continue;
                if (dist.ContainsKey(nk)) open.Remove((od, nk.x, nk.y, nk.z));
                dist[nk] = d; prev[nk] = k; open.Add((d, nk.x, nk.y, nk.z));
            }
        }
        if (!prev.ContainsKey(g)) return null;
        var line = new List<Vector3>(); for (var k = g; ; k = prev[k]) { line.Add(pos[k]); if (k == s) break; }
        line.Reverse();
        // なめらかにして step おきに間引く
        var sm = line.Select((p, i) => { Vector3 a = Vector3.zero; int n = 0; for (int j = Mathf.Max(0, i - 2); j <= Mathf.Min(line.Count - 1, i + 2); j++) { a += line[j]; n++; } return a / n; }).ToList();
        var outp = new List<Vector3> { sm[0] }; float acc = 0;
        for (int i = 1; i < sm.Count; i++) { acc += (sm[i] - sm[i - 1]).magnitude; if (acc >= step) { outp.Add(sm[i]); acc = 0; } }
        if (outp[outp.Count - 1] != sm[sm.Count - 1]) outp.Add(sm[sm.Count - 1]);
        return outp;
    }

    // Scene ビューの今の見え方を、キー1個分の JSON にしてログへ（ショットを手で作る時用）
    [MenuItem("FuwaCourse/PV/Scene ビューの位置をキーにしてログへ")]
    static void SceneKeyMenu()
    {
        var sv = SceneView.lastActiveSceneView; if (sv == null) return;
        var c = sv.camera.transform; var ci = CultureInfo.InvariantCulture;
        var l = c.position + c.forward * Mathf.Max(5f, sv.cameraDistance);
        string F(Vector3 v) => $"[{v.x.ToString("0.##", ci)},{v.y.ToString("0.##", ci)},{v.z.ToString("0.##", ci)}]";
        Debug.Log($"{{\"t\":0,\"p\":{F(c.position)},\"l\":{F(l)},\"fov\":{sv.cameraSettings.fieldOfView.ToString("0", ci)}}}");
    }
}
