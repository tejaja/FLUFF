using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

// サムネ・看板用の撮影セット（2026-10-18 てじゃ「ブースをコースに置いて写真とったら映えるんじゃないか」。台はなしで、ニタ・銃・ふわふわ）。
// Vket ブースの見た目をそのまま持ってくる（「角度とか全部ひっくるめてそのまま」）：
// PhotoSet/BoothScene.txt ＝ Vket 側の _NitaPoseRig（てじゃが指まで直した最終ポーズ・顔）の骨・顔・銃と、ふわふわ・撃った瞬間のエフェクトの
// ブースでの位置（左右反転を外した状態、ブースの原点から）。ここではニタを本当の大きさ（ブースは 2.5 倍）に縮めて、ブースと同じ向きに左右反転して置く。
// ルートは EditorOnly（ワールドのビルドには入らない）。何度実行しても作り直し（場所・向きは前のを引きつぐ）
public static class FuwaPhotoSet
{
    const string RootName = "_PhotoSet";
    const string Dir = "Assets/FuwaCourse/PhotoSet";
    public static bool Mirror = true;          // ブースで見えている向き（ブースは左右反転して置いてある）
    public static float BoothNitaScale = 2.5f;  // ブースのニタの大きさ（本当の大きさに戻す）

    [MenuItem("FuwaCourse/撮影/撮影セットを作る")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var old = GameObject.Find(RootName);
        Vector3 pos = old ? old.transform.position : Vector3.zero; Quaternion rot = old ? old.transform.rotation : Quaternion.identity;
        // 撮影カメラは動かした位置を引きつぐ
        var oldCam = old ? old.transform.Find(CamName) : null;
        Vector3? camLp = oldCam ? oldCam.localPosition : (Vector3?)null; Quaternion camLr = oldCam ? oldCam.localRotation : Quaternion.identity; float camFov = oldCam ? oldCam.GetComponent<Camera>().fieldOfView : 40f;
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject(RootName); root.tag = "EditorOnly";
        root.transform.SetPositionAndRotation(pos, rot);
        Undo.RegisterCreatedObjectUndo(root, "photo set");
        // ブースの原点（ここから下はブースと同じ並び）。
        // BoothScene.txt のニタ・銃は「ブースで見えている向き」（_NitaPoseRig は反転後の見た目のまま保存されていた）、
        // ふわふわ・エフェクトは反転を外した向きで書いてあるので、ふわふわ・エフェクトの方を鏡（FlatToSeen）の中に置いてそろえる
        var booth = new GameObject("Booth").transform; booth.SetParent(root.transform, false);
        float k = 1f / BoothNitaScale; booth.localScale = new Vector3(Mirror ? k : -k, k, k);
        var flat = new GameObject("FlatToSeen").transform; flat.SetParent(booth, false); flat.localScale = new Vector3(-1, 1, 1);

        var data = AssetDatabase.LoadAssetAtPath<TextAsset>(Dir + "/BoothScene.txt").text.Replace("\r", "").Split('\n').Where(l => l.Length > 0).Select(l => l.Split('\t')).ToList();
        Matrix4x4 Mat(string[] r) { var m = Matrix4x4.identity; for (int i = 0; i < 3; i++) m.SetRow(i, V4(r[1 + i])); return m; }
        string[] Line(string key) => data.First(r => r[0] == key);

        // ニタ：プレハブを解いて、アバター用の部品（VRChat の SDK・揺れ物・消えたスクリプト）を外す
        var nita = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TejaShop/Nita/Prefab/Nita.prefab"), booth);
        PrefabUtility.UnpackPrefabInstance(nita, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        nita.name = "Nita";
        foreach (var t in nita.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        foreach (var c in nita.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().Namespace != null && c.GetType().Namespace.StartsWith("VRC")).ToList()) Object.DestroyImmediate(c);
        nita.GetComponent<Animator>().enabled = false;   // 骨の形をそのまま
        SetLocal(nita.transform, Mat(Line("ROOT")));
        int bones = 0, miss = 0;
        foreach (var r in data.Where(r => r[0] == "T"))
        {
            var t = nita.transform.Find(r[1]); if (t == null) { miss++; continue; }
            t.localPosition = V3(r[2]); var q = V4(r[3]); t.localRotation = new Quaternion(q.x, q.y, q.z, q.w); t.localScale = V3(r[4]); bones++;
        }
        foreach (var r in data.Where(r => r[0] == "B"))
        {
            var smr = nita.transform.Find(r[1])?.GetComponent<SkinnedMeshRenderer>(); if (smr == null) continue;
            int i = smr.sharedMesh.GetBlendShapeIndex(r[2]); if (i >= 0) smr.SetBlendShapeWeight(i, float.Parse(r[3], CultureInfo.InvariantCulture));
        }

        // 銃：ブースの握り・角度のまま、手の子に
        var gun = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FuwaKit_Kaze/Prefabs/AirGun.prefab"));
        gun.name = "AirGun";
        SetWorld(gun.transform, booth.localToWorldMatrix * Mat(Line("GUN")));
        gun.transform.SetParent(nita.transform.Find(Line("GUNP")[1]), true);
        AddGauges(gun.transform);

        // 撃った瞬間のエフェクト・ふわふわ
        var fx = new GameObject("ShotFX", typeof(MeshFilter), typeof(MeshRenderer));
        fx.transform.SetParent(flat, false);
        fx.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Dir + "/Booth_ShotFX.asset");
        fx.GetComponent<MeshRenderer>().sharedMaterials = new[] { "ShotFX_Star", "ShotFX_Ring", "ShotFX_Streak", "ShotFX_Puff" }.Select(n => AssetDatabase.LoadAssetAtPath<Material>(Dir + "/" + n + ".mat")).ToArray();
        SetLocal(fx.transform, Mat(Line("FX")));
        var fuwa = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FuwaKit_Kaze/Prefabs/Fuwafuwa.prefab"), flat);
        fuwa.name = "Fuwafuwa";
        SetLocal(fuwa.transform, Mat(Line("FUWA")));
        PhotoFur(fuwa);

        // 撮影カメラ（ブースを正面から見た向き。Game ビューで見ながら動かせる。撮影はメニュー「撮影セットで撮る」）
        var camGo = new GameObject(CamName, typeof(Camera)); camGo.transform.SetParent(root.transform, false);
        var cam = camGo.GetComponent<Camera>(); cam.nearClipPlane = 0.03f; cam.farClipPlane = 3000f; cam.fieldOfView = camFov; cam.allowMSAA = true;
        cam.enabled = false;   // Play（ClientSim）の画面を乗っ取らないように。選ぶと Scene ビューの右下に見え方が出る。撮る時は手で描く
        if (camLp.HasValue) { camGo.transform.localPosition = camLp.Value; camGo.transform.localRotation = camLr; }
        else
        {
            // ブースの正面（見る人はブースの +z 側にいる）から、ニタとふわふわの間を見る
            var mid = (nita.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head).position + fuwa.transform.position) * 0.5f;
            var front = booth.TransformDirection(Vector3.forward).normalized;
            camGo.transform.position = mid + front * 2.6f - Vector3.up * 0.25f; camGo.transform.LookAt(mid);
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        HitLook(root.transform);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        return $"[撮影セット] 骨 {bones} 本（見つからない {miss}）" + (Mirror ? "・ブースで見えている向き" : "・反転前の向き");
    }

    // 銃の左右の空気ゲージ（偽物：量を固定）。ゲームの銃（DemoGun の Visual）と同じ位置・形で、量と奥行きの判定はブースと同じ
    //（2026-10-18 てじゃ「銃に偽ゲージもつけたい」。ブースは量 0.62、銃に貼るので ZTest LessEqual）
    public static float GaugeFill = 0.62f;
    static void AddGauges(Transform gun)
    {
        var src = Object.FindObjectsOfType<Renderer>(true).FirstOrDefault(r => r.name == "GunGaugeR" && r.transform.parent.Find("GunBody") != null)?.transform.parent;
        var body = gun.Find("GunBody"); if (src == null || body == null) return;
        string mp = Dir + "/GunGauge_Photo.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        var srcMat = src.Find("GunGaugeR").GetComponent<Renderer>().sharedMaterial;
        if (mat == null) { mat = new Material(srcMat); AssetDatabase.CreateAsset(mat, mp); } else mat.CopyPropertiesFromMaterial(srcMat);
        mat.SetFloat("_Fill", GaugeFill); mat.SetFloat("_ZTest", 4f); EditorUtility.SetDirty(mat);
        foreach (var n in new[] { "GunGaugeR", "GunGaugeL" })
        {
            var s = src.Find(n);
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = s.GetComponent<MeshFilter>().sharedMesh;
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(gun, false);
            SetWorld(go.transform, body.localToWorldMatrix * (src.Find("GunBody").worldToLocalMatrix * s.localToWorldMatrix));
        }
    }

    // 撮影用のふわふわ（2026-10-18 てじゃ「盛ろう」）：毛の層 64 枚のシェーダー（FuwaCourse/FurPhoto）と、なめらかな球。ゲームのふわふわは変えない
    public static float HitSquash = 0.1f, HitDrag = 0.05f;   // 撃たれた向きにつぶす量、毛先が押される量（0.2・0.12 は「ちょいやりすぎ」）
    static void PhotoFur(GameObject fuwa)
    {
        var mr = fuwa.GetComponent<MeshRenderer>(); var mf = fuwa.GetComponent<MeshFilter>();
        string mp = Dir + "/Fuwa_BallFur_Photo.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        var src = mr.sharedMaterial;
        if (mat == null) { mat = new Material(src); AssetDatabase.CreateAsset(mat, mp); } else mat.CopyPropertiesFromMaterial(src);
        mat.shader = Shader.Find("FuwaCourse/FurPhoto"); EditorUtility.SetDirty(mat);
        mr.sharedMaterial = mat;
        // 球：元の球（LowSphere）と同じ大きさで、たて 48・よこ 96 分割
        float r = mf.sharedMesh.bounds.extents.x;
        string sp = Dir + "/PhotoSphere.asset";
        var sphere = AssetDatabase.LoadAssetAtPath<Mesh>(sp);
        if (sphere == null)
        {
            sphere = new Mesh { name = "PhotoSphere" };
            var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<int>(); const int la = 48, lo = 96;
            for (int i = 0; i <= la; i++) for (int j = 0; j <= lo; j++) { float th = Mathf.PI * i / la, ph = Mathf.PI * 2 * j / lo; var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)); V.Add(n * r); N.Add(n); }
            for (int i = 0; i < la; i++) for (int j = 0; j < lo; j++) { int a = i * (lo + 1) + j, b = a + 1, c = a + lo + 1, d = c + 1; T.AddRange(new[] { a, b, c, b, d, c }); }
            sphere.SetVertices(V); sphere.SetNormals(N); sphere.SetTriangles(T, 0); sphere.RecalculateBounds();
            AssetDatabase.CreateAsset(sphere, sp);
        }
        mf.sharedMesh = sphere;
    }

    // 当たった瞬間：銃口→ふわふわの向きにつぶして、毛先をその向きへ押す（向きはワールドなので、撮る前に毎回入れ直す）
    static void HitLook(Transform root)
    {
        var fuwa = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Fuwafuwa");
        var muzzle = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GunMuzzleHole");
        var mat = fuwa ? fuwa.GetComponent<MeshRenderer>().sharedMaterial : null; if (!mat || !muzzle) return;
        var d = (fuwa.position - muzzle.position).normalized;   // （ニタを隠すと Renderer の範囲が取れないので、位置で）
        mat.SetVector("_Squash", new Vector4(d.x, d.y, d.z, HitSquash));
        mat.SetVector("_Drag", new Vector4(d.x, d.y, d.z, 0) * HitDrag);
        EditorUtility.SetDirty(mat);
    }

    const string CamName = "PhotoCamera";
    public static int ShotWidth = 2400, ShotHeight = 1800;   // ワールドのサムネ（1200×900）の2倍。看板用はカメラの縦横を変えて
    public const string OutDir = "D:/Claude_Workspace/FLUFF_Photos";

    // 撮影カメラの見え方で高解像度の PNG を書き出す（近くの風の粒も流してから撮る）
    [MenuItem("FuwaCourse/撮影/撮影セットで撮る")]
    public static void ShootMenu() { Debug.Log(Shoot(ShotWidth, ShotHeight)); }
    // Scene ビューの見え方（位置・向き・画角）を撮影カメラへ写す（2026-10-18 てじゃ「シーンビューって画角かえれないの」）
    [MenuItem("FuwaCourse/撮影/撮影カメラを Scene ビューに合わせる")]
    public static void MatchSceneViewMenu()
    {
        var sv = SceneView.lastActiveSceneView; var root = GameObject.Find(RootName);
        var cam = root ? root.transform.Find(CamName)?.GetComponent<Camera>() : null;
        if (sv == null || cam == null) { Debug.LogWarning("Scene ビューか撮影セットがない"); return; }
        Undo.RecordObject(cam.transform, "match view"); Undo.RecordObject(cam, "match view");
        cam.transform.SetPositionAndRotation(sv.camera.transform.position, sv.camera.transform.rotation);
        cam.fieldOfView = sv.cameraSettings.fieldOfView;
        Debug.Log("[撮影] カメラを Scene ビューに合わせた（画角 " + cam.fieldOfView.ToString("0") + "°）");
    }

    [MenuItem("FuwaCourse/撮影/撮影セットで撮る（たて長・看板用）")]
    public static void ShootPortraitMenu() { Debug.Log(Shoot(ShotHeight, ShotWidth)); }

    // ぼかし（被写界深度）つきで撮る（2026-10-18 てじゃ「ちょっとぼかしとかいれたら映えるかな」）。
    // ピントはニタ・銃・空気の輪、奥はふんわり、手前のふわふわも少し。Unity で 色・奥行き・ふわふわの形・エフェクトの形 を書き出し、
    // Tools/photo/photo_dof.py で合成する（距離ごとにぼかす。ピントの合った物が後ろへにじまないように奥と手前を分けて）
    public static float DofBack = 10f, DofFront = 9f;   // 16・14 から「ちょいよわめ」   // 奥のぼけの最大（2400 幅で px）、手前のふわふわのぼけ（px）
    [MenuItem("FuwaCourse/撮影/撮影セットで撮る（ぼかしつき・8枚）")]
    public static void ShootDofManyMenu()
    {
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        for (int i = 0; i < 8; i++) Debug.Log(ShootDof(ShotWidth, ShotHeight, "FLUFF_" + stamp + "_dof_" + (i + 1), 2.5f + i * 0.37f));
    }

    public static string ShootDof(int w, int h, string name, float simTime = 3f)
    {
        var root = GameObject.Find(RootName); if (!root) return "撮影セットがない";
        var cam = root.transform.Find(CamName)?.GetComponent<Camera>(); if (!cam) return "PhotoCamera がない";
        HitLook(root.transform);
        var sims = new List<ParticleSystem>();
        foreach (var ps in Object.FindObjectsOfType<ParticleSystem>(false))
        {
            if ((ps.transform.position - cam.transform.position).sqrMagnitude > 60f * 60f || ps.isPlaying) continue;
            ps.Simulate(0f, true, true); ps.Simulate(simTime, true, false); sims.Add(ps);
        }
        System.IO.Directory.CreateDirectory(OutDir + "/_layers");
        string baseP = OutDir + "/_layers/" + name;
        int oldMask = cam.cullingMask; var oldClear = cam.clearFlags; var oldBg = cam.backgroundColor;
        try
        {
            // 1 色
            Save(RenderColor(cam, w, h), baseP + "_color.png");
            // 2 奥行き（m）。空は遠く
            var drt = new RenderTexture(w, h, 24, RenderTextureFormat.RFloat);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(10000, 0, 0, 0);
            cam.targetTexture = drt; cam.RenderWithShader(AssetDatabase.LoadAssetAtPath<Shader>(Dir + "/FuwaPhotoDepth.shader"), "RenderType"); cam.targetTexture = null;
            var prev = RenderTexture.active; RenderTexture.active = drt;
            var dt = new Texture2D(w, h, TextureFormat.RFloat, false); dt.ReadPixels(new Rect(0, 0, w, h), 0, 0); dt.Apply(); RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(baseP + "_depth.raw", dt.GetRawTextureData());
            Object.DestroyImmediate(dt); drt.Release(); Object.DestroyImmediate(drt);
            // 3 ふわふわだけ・エフェクトだけ（黒の上に描いて形をとる）
            var fuwa = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Fuwafuwa");
            var fx = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "ShotFX");
            Save(RenderOnly(cam, w, h, fuwa.gameObject), baseP + "_fuwa.png");
            Save(RenderOnly(cam, w, h, fx.gameObject), baseP + "_fx.png");
            // ピント：銃口の奥行き。ふわふわ：中心の奥行き
            var muzzle = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "GunMuzzleHole");
            float zf = -cam.worldToCameraMatrix.MultiplyPoint(muzzle.GetComponent<Renderer>().bounds.center).z;
            float zu = -cam.worldToCameraMatrix.MultiplyPoint(fuwa.position).z;
            // ピントの合う奥行きの幅：ニタと銃ぜんぶ（カメラが近いので、銃口の1点だと足や服がぼけた）
            float zn = float.MaxValue, zx = 0f;
            // （箱の角だと大きすぎたので、今のポーズのメッシュの頂点で測る）
            var V = cam.worldToCameraMatrix;
            foreach (var smr in root.transform.Find("Booth/Nita").GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled) continue;   // 体を消して撮る時は数えない（銃だけ残す絵）
                var m = new Mesh(); smr.BakeMesh(m, true); var M = V * smr.transform.localToWorldMatrix;
                foreach (var v in m.vertices) { float d = -M.MultiplyPoint3x4(v).z; zn = Mathf.Min(zn, d); zx = Mathf.Max(zx, d); }
                Object.DestroyImmediate(m);
            }
            foreach (var mr in root.transform.Find("Booth/Nita").GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue; var M = V * mr.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) { float d = -M.MultiplyPoint3x4(v).z; zn = Mathf.Min(zn, d); zx = Mathf.Max(zx, d); }
            }
            bool bodyHidden = !root.transform.Find("Booth/Nita").GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.enabled);
            if (zx <= 0f) { zn = zu * 0.85f; zx = zu * 1.25f; zf = zu; }   // ニタを隠している時は、ふわふわにピント
            else if (bodyHidden) { zn = Mathf.Min(zn, zu * 0.85f); zx = Mathf.Max(zx, zu * 1.25f); }   // 体だけ消して銃を残す時は、銃とふわふわの両方にピント
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            System.IO.File.WriteAllText(baseP + "_meta.json", $"{{\"w\":{w},\"h\":{h},\"focus\":{zf.ToString(ci)},\"near\":{Mathf.Max(0.05f, zn).ToString(ci)},\"far\":{zx.ToString(ci)},\"fuwa\":{zu.ToString(ci)},\"back\":{DofBack.ToString(ci)},\"front\":{DofFront.ToString(ci)}}}");
        }
        finally
        {
            cam.cullingMask = oldMask; cam.clearFlags = oldClear; cam.backgroundColor = oldBg; cam.targetTexture = null;
            foreach (var ps in sims) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        // 合成
        var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{System.IO.Path.GetFullPath("Tools/photo/photo_dof.py")}\" \"{baseP}\" \"{OutDir}/{name}.png\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        using (var p = System.Diagnostics.Process.Start(psi))
        {
            string err = p.StandardError.ReadToEnd(); string outp = p.StandardOutput.ReadToEnd(); p.WaitForExit();
            if (p.ExitCode != 0) return "[撮影] 合成に失敗: " + err;
            return "[撮影] " + OutDir + "/" + name + ".png " + outp.Trim();
        }
    }

    static Texture2D RenderColor(Camera cam, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; rt.Release(); Object.DestroyImmediate(rt);
        return tex;
    }

    // その物（と子）だけを黒の上に描く。使っていないレイヤー 31 に一時的に移して
    static Texture2D RenderOnly(Camera cam, int w, int h, GameObject go)
    {
        var all = go.GetComponentsInChildren<Transform>(true).Select(t => (t.gameObject, t.gameObject.layer)).ToList();
        foreach (var (g, _) in all) g.layer = 31;
        cam.cullingMask = 1 << 31; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        try { return RenderColor(cam, w, h); }
        finally { foreach (var (g, l) in all) g.layer = l; }
    }

    static void Save(Texture2D t, string path) { System.IO.File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t); }

    // 綿毛・風の粒は毎回ちがう形になるので、何枚か撮って選ぶ（2026-10-18 てじゃ「何枚かとろう」）
    [MenuItem("FuwaCourse/撮影/撮影セットで撮る（8枚・粒の形ちがい）")]
    public static void ShootManyMenu()
    {
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        for (int i = 0; i < 8; i++) Debug.Log(Shoot(ShotWidth, ShotHeight, "FLUFF_" + stamp + "_" + (i + 1), 2.5f + i * 0.37f));
    }

    public static string Shoot(int w, int h, string name = null, float simTime = 3f)
    {
        var root = GameObject.Find(RootName); if (!root) return "撮影セットがない";
        var cam = root.transform.Find(CamName)?.GetComponent<Camera>(); if (!cam) return "PhotoCamera がない";
        HitLook(root.transform);   // セットを回していても、今の銃口→ふわふわの向きで
        // 近くの風の粒・葉っぱを流しておく（見えていた物だけ。止めて戻す）
        var sims = new List<ParticleSystem>();
        foreach (var ps in Object.FindObjectsOfType<ParticleSystem>(false))
        {
            if ((ps.transform.position - cam.transform.position).sqrMagnitude > 60f * 60f || ps.isPlaying) continue;
            ps.Simulate(0f, true, true); ps.Simulate(simTime, true, false); sims.Add(ps);
        }
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
        var prevT = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prevT;
        var prevA = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prevA;
        System.IO.Directory.CreateDirectory(OutDir);
        string path = OutDir + "/" + (name ?? ("FLUFF_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"))) + ".png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        foreach (var ps in sims) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return "[撮影] " + path;
    }

    static Vector3 V3(string s) { var a = s.Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray(); return new Vector3(a[0], a[1], a[2]); }
    static Vector4 V4(string s) { var a = s.Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray(); return new Vector4(a[0], a[1], a[2], a[3]); }

    static void SetLocal(Transform t, Matrix4x4 L)
    {
        t.localPosition = L.GetColumn(3);
        t.localRotation = L.rotation;
        t.localScale = new Vector3(L.GetColumn(0).magnitude, L.GetColumn(1).magnitude, L.GetColumn(2).magnitude);
    }

    // ワールドの行列どおりに置く（裏返った親の中でも。スケール x を負にして表す）
    static void SetWorld(Transform t, Matrix4x4 W)
    {
        var L = t.parent ? t.parent.worldToLocalMatrix * W : W;
        bool neg = L.determinant < 0;
        var LS = neg ? L * Matrix4x4.Scale(new Vector3(-1, 1, 1)) : L;
        t.localPosition = L.GetColumn(3);
        t.localRotation = LS.rotation;
        var sc = new Vector3(LS.GetColumn(0).magnitude, LS.GetColumn(1).magnitude, LS.GetColumn(2).magnitude);
        if (neg) sc.x = -sc.x;
        t.localScale = sc;
    }
}
