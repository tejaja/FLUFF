using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// モーション録画（FuwaMotionRecorder）のログを取り込んで、確認用の棒人間アニメにする（2026-10-07）。
// 1) ログから取り込む：VRChat の output_log（または Editor.log）から [FMR] の行を拾い、テイクごとに Motion/Raw/*.txt に保存
// 2) 保存したテイクから、関節の玉＋骨の棒＋頭の向き＋銃＋ふわふわを動かすアニメ（ワールド座標そのまま）を作ってシーンに置く
// 人形（マネキン）への移し替えは次の段階。ここでは録れたデータが正しいかを見る。
public static class FuwaMotionImport
{
    const string Dir = "Assets/FuwaCourse/Motion";
    const string RawDir = Dir + "/Raw";
    const string ClipDir = Dir + "/Clips";
    const string Tag = "[FMR] ";

    // 録画側の並び（FuwaMotionRecorder._bones と同じ順）。値は HumanBodyBones の番号
    public class Take
    {
        public string name;
        public float rate;
        public float eye;
        public bool vr;
        public int[] bones;
        public List<Frame> frames = new List<Frame>();
    }

    public class Frame
    {
        public float t;
        public Vector3[] bone;      // 無い骨は null 扱い（missing）
        public bool[] has;
        public Vector3 headPos, lPos, rPos;
        public Quaternion headRot, lRot, rRot;
        public bool held;
        public int hand;
        public Vector3 gunPos;
        public Quaternion gunRot;
        public float gunScale;
        public bool charging;
        public float chargeStart, lastShot;
        public Vector3 ballPos;
        public Quaternion ballRot;
    }

    [MenuItem("FuwaCourse/モーション録画/ログから取り込む")]
    static void ImportMenu()
    {
        string vrcDir = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? "", "AppData", "LocalLow", "VRChat", "VRChat");
        string path = EditorUtility.OpenFilePanel("VRChat のログ（output_log_*.txt）", Directory.Exists(vrcDir) ? vrcDir : "", "txt,log");
        if (string.IsNullOrEmpty(path)) return;
        var names = ImportLog(path);
        EditorUtility.DisplayDialog("モーション録画", names.Count == 0 ? "テイクが見つからなかった" : "取り込んだ：\n" + string.Join("\n", names), "OK");
    }

    [MenuItem("FuwaCourse/モーション録画/選んだテイクから確認用アニメを作る")]
    static void BuildMenu()
    {
        foreach (var o in Selection.objects)
        {
            var ta = o as TextAsset;
            if (ta == null) continue;
            BuildPreview(AssetDatabase.GetAssetPath(ta));
        }
    }

    // ログを読んでテイクごとに Raw に保存。保存したアセットのパスを返す
    public static List<string> ImportLog(string logPath)
    {
        var result = new List<string>();
        string stamp = Path.GetFileNameWithoutExtension(logPath).Replace("output_log_", "");
        var cur = new List<string>();
        string take = null;
        // VRChat はログを書き込み中なので共有読みで開く
        using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var sr = new StreamReader(fs))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                int i = line.IndexOf(Tag, StringComparison.Ordinal);
                if (i < 0) continue;
                string body = line.Substring(i + Tag.Length).Trim();
                if (body.StartsWith("B "))
                {
                    take = Field(body, "take") ?? "0";
                    cur.Clear();
                    cur.Add(body);
                }
                else if (take != null)
                {
                    cur.Add(body);
                    if (body.StartsWith("E "))
                    {
                        result.Add(SaveRaw(stamp, take, cur));
                        take = null;
                    }
                }
            }
        }
        // 終わりの行が無いテイク（落ちた・ログ途中）も保存
        if (take != null && cur.Count > 2) result.Add(SaveRaw(stamp, take, cur));
        AssetDatabase.Refresh();
        foreach (var p in result) BuildPreview(p);
        return result;
    }

    static string SaveRaw(string stamp, string take, List<string> lines)
    {
        Directory.CreateDirectory(RawDir);
        string safe = new string(stamp.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
        string p = RawDir + "/" + safe + "_take" + take + ".txt";
        File.WriteAllLines(p, lines);
        return p;
    }

    static string Field(string body, string key)
    {
        foreach (var tok in body.Split(' '))
            if (tok.StartsWith(key + "=")) return tok.Substring(key.Length + 1);
        return null;
    }

    public static Take Parse(string rawPath)
    {
        var tk = new Take { name = Path.GetFileNameWithoutExtension(rawPath) };
        foreach (var line in File.ReadAllLines(rawPath))
        {
            if (line.StartsWith("B "))
            {
                tk.rate = float.Parse(Field(line, "rate") ?? "30", CultureInfo.InvariantCulture);
                tk.eye = int.Parse(Field(line, "eye") ?? "1600") / 1000f;
                tk.vr = Field(line, "vr") == "1";
                tk.bones = (Field(line, "bones") ?? "").Split(',').Select(int.Parse).ToArray();
                continue;
            }
            if (!line.StartsWith("F ") || tk.bones == null) continue;
            var v = line.Split(' ');
            int n = tk.bones.Length;
            if (v.Length < 2 + n * 3 + 21 + 2 + 7 + 4 + 7) continue;   // 途中で切れた行
            int k = 1;
            var f = new Frame { bone = new Vector3[n], has = new bool[n] };
            f.t = I(v[k++]) / 1000f;
            for (int b = 0; b < n; b++)
            {
                int x = I(v[k]), y = I(v[k + 1]), z = I(v[k + 2]);
                f.has[b] = !(x == 0 && y == 0 && z == 0);
                f.bone[b] = new Vector3(x, y, z) / 1000f;
                k += 3;
            }
            f.headPos = P(v, ref k); f.headRot = Q(v, ref k);
            f.lPos = P(v, ref k); f.lRot = Q(v, ref k);
            f.rPos = P(v, ref k); f.rRot = Q(v, ref k);
            f.held = v[k++] == "1";
            f.hand = I(v[k++]);
            f.gunPos = P(v, ref k); f.gunRot = Q(v, ref k);
            f.gunScale = I(v[k++]) / 1000f;
            f.charging = v[k++] == "1";
            f.chargeStart = I(v[k++]) / 1000f;
            f.lastShot = I(v[k++]) / 1000f;
            f.ballPos = P(v, ref k); f.ballRot = Q(v, ref k);
            tk.frames.Add(f);
        }
        return tk;
    }

    static int I(string s) { return int.Parse(s, CultureInfo.InvariantCulture); }
    static Vector3 P(string[] v, ref int k) { var p = new Vector3(I(v[k]), I(v[k + 1]), I(v[k + 2])) / 1000f; k += 3; return p; }
    static Quaternion Q(string[] v, ref int k)
    {
        var q = new Quaternion(I(v[k]), I(v[k + 1]), I(v[k + 2]), I(v[k + 3]));
        k += 4;
        float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        return m < 1e-6f ? Quaternion.identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
    }

    // 棒人間の骨（HumanBodyBones の番号の組）。UpperChest が無いアバターは Chest→Neck/Shoulder につなぎ替える
    static readonly int[][] Links =
    {
        new[] { 0, 7 }, new[] { 7, 8 }, new[] { 8, 54 }, new[] { 54, 9 }, new[] { 9, 10 },
        new[] { 54, 11 }, new[] { 11, 13 }, new[] { 13, 15 }, new[] { 15, 17 }, new[] { 17, 30 },
        new[] { 54, 12 }, new[] { 12, 14 }, new[] { 14, 16 }, new[] { 16, 18 }, new[] { 18, 45 },
        new[] { 0, 1 }, new[] { 1, 3 }, new[] { 3, 5 }, new[] { 5, 19 },
        new[] { 0, 2 }, new[] { 2, 4 }, new[] { 4, 6 }, new[] { 6, 20 },
    };

    public static GameObject BuildPreview(string rawPath)
    {
        var tk = Parse(rawPath);
        if (tk.frames.Count < 2) { Debug.LogWarning("[FMR] フレームが足りない: " + rawPath); return null; }
        int n = tk.bones.Length;
        var idx = new Dictionary<int, int>();
        for (int i = 0; i < n; i++) idx[tk.bones[i]] = i;
        bool Has(int hb) => idx.ContainsKey(hb) && tk.frames.Any(f => f.has[idx[hb]]);
        // UpperChest が無い時は Chest で代用
        int Map(int hb) => (hb == 54 && !Has(54)) ? 8 : hb;

        Directory.CreateDirectory(ClipDir);
        string objName = "MotionPreview_" + tk.name;
        var old = GameObject.Find(objName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject(objName);
        Undo.RegisterCreatedObjectUndo(root, "motion preview");

        var jointMat = PreviewMat("MotionPreviewJoint", new Color(0.35f, 0.8f, 1f));
        var boneMat = PreviewMat("MotionPreviewBone", new Color(0.9f, 0.95f, 1f));
        var headMat = PreviewMat("MotionPreviewHead", new Color(1f, 0.75f, 0.4f));
        var ballMat = PreviewMat("MotionPreviewBall", new Color(1f, 0.85f, 0.95f));

        var clip = new AnimationClip { name = tk.name, frameRate = Mathf.Max(30f, tk.rate) };
        var curves = new Dictionary<string, AnimationCurve>();
        void Key(string path, Type type, string prop, float t, float val)
        {
            string key = path + "|" + type.Name + "|" + prop;
            if (!curves.TryGetValue(key, out var c)) curves[key] = c = new AnimationCurve();
            c.AddKey(new Keyframe(t, val));
        }
        void KeyPos(string path, float t, Vector3 p)
        {
            Key(path, typeof(Transform), "m_LocalPosition.x", t, p.x);
            Key(path, typeof(Transform), "m_LocalPosition.y", t, p.y);
            Key(path, typeof(Transform), "m_LocalPosition.z", t, p.z);
        }
        var lastRot = new Dictionary<string, Quaternion>();
        void KeyRot(string path, float t, Quaternion q)
        {
            // 符号が反転すると補間で一回転するので、前のキーと同じ側にそろえる
            if (lastRot.TryGetValue(path, out var pq) && Quaternion.Dot(pq, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            lastRot[path] = q;
            Key(path, typeof(Transform), "m_LocalRotation.x", t, q.x);
            Key(path, typeof(Transform), "m_LocalRotation.y", t, q.y);
            Key(path, typeof(Transform), "m_LocalRotation.z", t, q.z);
            Key(path, typeof(Transform), "m_LocalRotation.w", t, q.w);
        }
        void KeyScale(string path, float t, Vector3 s)
        {
            Key(path, typeof(Transform), "m_LocalScale.x", t, s.x);
            Key(path, typeof(Transform), "m_LocalScale.y", t, s.y);
            Key(path, typeof(Transform), "m_LocalScale.z", t, s.z);
        }

        // 関節の玉
        var jointPaths = new Dictionary<int, string>();
        foreach (var hb in tk.bones)
        {
            if (!Has(hb)) continue;
            string p = "J_" + ((HumanBodyBones)hb);
            Prim(PrimitiveType.Sphere, p, root.transform, jointMat, Vector3.one * 0.05f);
            jointPaths[hb] = p;
        }
        // 骨の棒
        var linkPaths = new List<(string path, int a, int b)>();
        foreach (var l in Links)
        {
            int a = Map(l[0]), b = Map(l[1]);
            if (a == b || !Has(a) || !Has(b)) continue;
            string p = "L_" + ((HumanBodyBones)a) + "_" + ((HumanBodyBones)b);
            if (linkPaths.Any(x => x.path == p)) continue;
            Prim(PrimitiveType.Cylinder, p, root.transform, boneMat, Vector3.one * 0.03f);
            linkPaths.Add((p, a, b));
        }
        // 頭の向き（トラッキング）：鼻の付いた箱
        var head = Prim(PrimitiveType.Cube, "HeadView", root.transform, headMat, new Vector3(0.16f, 0.12f, 0.12f));
        Prim(PrimitiveType.Cube, "Nose", head.transform, headMat, new Vector3(0.25f, 0.25f, 0.6f)).transform.localPosition = new Vector3(0, 0, 0.7f);
        // 銃：本物の見た目をコピー
        var gun = MakeGunVisual(root.transform);
        // ふわふわ
        Prim(PrimitiveType.Sphere, "Ball", root.transform, ballMat, Vector3.one * 0.5f);

        float t0 = tk.frames[0].t;
        foreach (var f in tk.frames)
        {
            float t = f.t - t0;
            foreach (var kv in jointPaths)
            {
                int i = idx[kv.Key];
                if (f.has[i]) KeyPos(kv.Value, t, f.bone[i]);
            }
            foreach (var (path, a, b) in linkPaths)
            {
                int ia = idx[a], ib = idx[b];
                if (!f.has[ia] || !f.has[ib]) continue;
                Vector3 pa = f.bone[ia], pb = f.bone[ib];
                Vector3 d = pb - pa;
                float len = d.magnitude;
                KeyPos(path, t, (pa + pb) * 0.5f);
                KeyRot(path, t, len > 1e-5f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity);
                KeyScale(path, t, new Vector3(0.03f, len * 0.5f, 0.03f));
            }
            KeyPos("HeadView", t, f.headPos);
            KeyRot("HeadView", t, f.headRot);
            if (gun != null)
            {
                KeyPos(gun.name, t, f.gunPos);
                KeyRot(gun.name, t, f.gunRot);
                float s = f.held ? Mathf.Max(0.01f, f.gunScale) : 0.0001f;
                KeyScale(gun.name, t, Vector3.one * s);
            }
            KeyPos("Ball", t, f.ballPos);
        }

        foreach (var kv in curves)
        {
            var parts = kv.Key.Split('|');
            var c = kv.Value;
            for (int i = 0; i < c.length; i++) AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            for (int i = 0; i < c.length; i++) AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            clip.SetCurve(parts[0], typeof(Transform), parts[2], c);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string clipPath = ClipDir + "/" + tk.name + "_preview.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; }
        else AssetDatabase.CreateAsset(clip, clipPath);
        string ctrlPath = ClipDir + "/" + tk.name + "_preview.controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) ?? AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, clip);
        ctrl.layers[0].stateMachine.defaultState.motion = clip;
        root.AddComponent<Animator>().runtimeAnimatorController = ctrl;
        AssetDatabase.SaveAssets();

        // 見やすいように最初のフレームの形にしておく
        clip.SampleAnimation(root, 0f);
        var fr = tk.frames;
        Debug.Log($"[FMR] {tk.name}: {fr.Count} フレーム, {fr[fr.Count - 1].t - fr[0].t:F1}秒, 骨 {jointPaths.Count}/{n}, vr={tk.vr}, 目の高さ {tk.eye:F2}m");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        return root;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Material mat, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // シーンの銃（FuwaBlower のひな形）から見た目だけコピー
    static GameObject MakeGunVisual(Transform parent)
    {
        var src = UnityEngine.Object.FindObjectsOfType<FuwaBlower>(true).Select(b => b.gameObject).FirstOrDefault();
        var holder = new GameObject("Gun");
        holder.transform.SetParent(parent, false);
        if (src == null) return holder;
        foreach (var mr in src.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!mr.enabled || !mr.gameObject.activeInHierarchy && !mr.gameObject.activeSelf) continue;
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var go = new GameObject(mr.name);
            go.transform.SetParent(holder.transform, false);
            var m = src.transform.worldToLocalMatrix * mr.transform.localToWorldMatrix;
            go.transform.localPosition = m.GetColumn(3);
            go.transform.localRotation = m.rotation;
            go.transform.localScale = m.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mr.sharedMaterials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return holder;
    }

    static Material PreviewMat(string name, Color c)
    {
        Directory.CreateDirectory(Dir);
        string p = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m != null) return m;
        m = new Material(Shader.Find("Unlit/Color")) { color = c };
        AssetDatabase.CreateAsset(m, p);
        return m;
    }
}
