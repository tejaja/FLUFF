using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// モーション録画（FuwaMotionRecorder）のテイクを Nita の人型アニメ（Humanoid のクリップ）に変換する（2026-10-07）。
// 録ったのは骨の「位置」なので、Nita の骨を親から順に「次の骨の方を向く＋横向きの基準をそろえる」ように回してポーズを作り、
// HumanPoseHandler で筋肉の値に直してクリップにする。ひじ・ひざは曲がる軸（筋肉を動かして調べる）を、録った腕の曲がる面に合わせる。
// 頭は頭のトラッキングの向き、手は指の付け根の位置から向きを決める。指の曲げは録っていないので、銃を持っている手は握りの形にする。
// 銃とふわふわは同じクリップに（Nita の子として）入れる。お手本は録った場所（ワールド座標）でそのまま再生される。
public static class FuwaMotionNita
{
    const string ModelPath = "Assets/TejaShop/Nita/FBX/Nita.fbx";
    const string ShowModelPath = "Assets/TejaShop/Nita/FBX/Nita_Mobile.fbx";
    const string ShowPrefabPath = "Assets/TejaShop/Nita/Prefab/Nita_Mobile.prefab";
    const string ClipDir = "Assets/FuwaCourse/Motion/Clips";

    // ポーズを作る係（Nita を1体、見えない所に出して使う）
    public class Solver : IDisposable
    {
        public GameObject go;
        public Transform root;
        public Animator anim;
        public HumanPoseHandler hph;
        readonly Dictionary<HumanBodyBones, Transform> _b = new Dictionary<HumanBodyBones, Transform>();
        Transform[] _all;
        Vector3[] _restPos;
        Quaternion[] _restRot;
        Quaternion _headView;          // 頭の骨から見た「見ている向き」
        Vector3 _hingeArmL, _hingeArmR, _hingeLegL, _hingeLegR;   // ひじ・ひざの曲がる軸（上の骨のローカル）
        readonly Vector3?[] _lastN = new Vector3?[4];

        public Solver(string modelPath)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            go = UnityEngine.Object.Instantiate(src);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.name = "_FMR_Solver";
            root = go.transform;
            anim = go.GetComponent<Animator>();
            foreach (HumanBodyBones hb in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (hb == HumanBodyBones.LastBone) continue;
                var t = anim.GetBoneTransform(hb);
                if (t != null) _b[hb] = t;
            }
            _all = go.GetComponentsInChildren<Transform>(true);
            _restPos = _all.Select(t => t.localPosition).ToArray();
            _restRot = _all.Select(t => t.localRotation).ToArray();
            hph = new HumanPoseHandler(anim.avatar, root);

            ResetRest(1f);
            _headView = Quaternion.Inverse(_b[HumanBodyBones.Head].rotation);   // 休みの姿勢は +Z を向いている
            _hingeArmL = ProbeHinge("Left Forearm Stretch", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            _hingeArmR = ProbeHinge("Right Forearm Stretch", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            _hingeLegL = ProbeHinge("Left Lower Leg Stretch", HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            _hingeLegR = ProbeHinge("Right Lower Leg Stretch", HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
            ResetRest(1f);
        }

        public void Dispose()
        {
            hph?.Dispose();
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        public bool Has(HumanBodyBones hb) => _b.ContainsKey(hb);
        public Transform Bone(HumanBodyBones hb) => _b.TryGetValue(hb, out var t) ? t : null;
        public Quaternion HeadView => _b[HumanBodyBones.Head].rotation * _headView;

        public void ResetRest(float scale)
        {
            for (int i = 0; i < _all.Length; i++) { _all[i].localPosition = _restPos[i]; _all[i].localRotation = _restRot[i]; }
            root.position = Vector3.zero;
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one * scale;
        }

        // 筋肉でひじ（ひざ）を曲げて、曲がる軸を上の骨のローカルで調べる
        Vector3 ProbeHinge(string muscle, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end)
        {
            int mi = Array.IndexOf(HumanTrait.MuscleName, muscle);
            var pose = new HumanPose();
            hph.GetHumanPose(ref pose);
            pose.muscles[mi] = -0.6f;
            hph.SetHumanPose(ref pose);
            Transform u = _b[upper], l = _b[lower], e = _b[end];
            Vector3 a = u.InverseTransformDirection(l.position - u.position);
            Vector3 f = u.InverseTransformDirection(e.position - l.position);
            var n = Vector3.Cross(a, f).normalized;
            ResetRest(1f);
            return n;
        }

        Vector3 Pos(HumanBodyBones hb) => _b[hb].position;

        static Quaternion Frame(Vector3 primary, Vector3 secondary)
        {
            return Quaternion.LookRotation(primary, Vector3.ProjectOnPlane(secondary, primary));
        }

        static void Rotate(Transform bone, Vector3 curP, Vector3 curS, Vector3 tgtP, Vector3 tgtS)
        {
            if (curP.sqrMagnitude < 1e-10f || tgtP.sqrMagnitude < 1e-10f) return;
            if (Vector3.Cross(curP, curS).sqrMagnitude < 1e-10f || Vector3.Cross(tgtP, tgtS).sqrMagnitude < 1e-10f) { Swing(bone, curP, tgtP); return; }
            bone.rotation = Frame(tgtP, tgtS) * Quaternion.Inverse(Frame(curP, curS)) * bone.rotation;
        }

        static void Swing(Transform bone, Vector3 cur, Vector3 tgt)
        {
            if (cur.sqrMagnitude < 1e-10f || tgt.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(cur, tgt) * bone.rotation;
        }

        // 録った骨の位置（P、無ければ null）と頭の向きから Nita のポーズを作り、HumanPose を返す
        public HumanPose Solve(Func<HumanBodyBones, Vector3?> P, Quaternion? head, float scale)
        {
            ResetRest(scale);
            bool H(HumanBodyBones hb) => Has(hb) && P(hb).HasValue;
            Vector3 T(HumanBodyBones hb) => P(hb).Value;

            var hips = _b[HumanBodyBones.Hips];
            if (H(HumanBodyBones.Hips)) hips.position = T(HumanBodyBones.Hips);
            // 腰：上（背骨の方）と横（右もも − 左もも）
            if (H(HumanBodyBones.Spine) && H(HumanBodyBones.LeftUpperLeg) && H(HumanBodyBones.RightUpperLeg))
                Rotate(hips,
                    Pos(HumanBodyBones.Spine) - Pos(HumanBodyBones.Hips), Pos(HumanBodyBones.RightUpperLeg) - Pos(HumanBodyBones.LeftUpperLeg),
                    T(HumanBodyBones.Spine) - T(HumanBodyBones.Hips), T(HumanBodyBones.RightUpperLeg) - T(HumanBodyBones.LeftUpperLeg));

            // 背骨〜首：次の骨の方＋肩の横向き
            bool armsLat = H(HumanBodyBones.LeftUpperArm) && H(HumanBodyBones.RightUpperArm);
            Vector3 CurLat() => Pos(HumanBodyBones.RightUpperArm) - Pos(HumanBodyBones.LeftUpperArm);
            Vector3 TgtLat() => T(HumanBodyBones.RightUpperArm) - T(HumanBodyBones.LeftUpperArm);
            var chain = new List<HumanBodyBones> { HumanBodyBones.Spine, HumanBodyBones.Chest };
            if (H(HumanBodyBones.UpperChest)) chain.Add(HumanBodyBones.UpperChest);
            chain.Add(HumanBodyBones.Neck);
            chain.Add(HumanBodyBones.Head);
            for (int i = 0; i + 1 < chain.Count; i++)
            {
                var a = chain[i]; var b = chain[i + 1];
                if (!H(a) || !H(b)) continue;
                var bone = _b[a];
                if (armsLat) Rotate(bone, Pos(b) - Pos(a), CurLat(), T(b) - T(a), TgtLat());
                else Swing(bone, Pos(b) - Pos(a), T(b) - T(a));
            }
            // 頭：トラッキングの向き
            if (head.HasValue) _b[HumanBodyBones.Head].rotation = head.Value * Quaternion.Inverse(_headView);

            Limb(0, P, HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, _hingeArmL);
            Limb(1, P, HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, _hingeArmR);
            Hand(P, HumanBodyBones.LeftHand, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftLittleProximal);
            Hand(P, HumanBodyBones.RightHand, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightLittleProximal);
            Limb(2, P, null, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, _hingeLegL);
            Limb(3, P, null, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, _hingeLegR);
            Foot(P, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, HumanBodyBones.LeftLowerLeg);
            Foot(P, HumanBodyBones.RightFoot, HumanBodyBones.RightToes, HumanBodyBones.RightLowerLeg);

            var pose = new HumanPose();
            hph.GetHumanPose(ref pose);
            return pose;
        }

        void Limb(int side, Func<HumanBodyBones, Vector3?> P, HumanBodyBones? shoulder, HumanBodyBones up, HumanBodyBones lo, HumanBodyBones end, Vector3 hingeLocal)
        {
            bool H(HumanBodyBones hb) => Has(hb) && P(hb).HasValue;
            if (shoulder.HasValue && H(shoulder.Value) && H(up))
                Swing(_b[shoulder.Value], Pos(up) - Pos(shoulder.Value), P(up).Value - P(shoulder.Value).Value);
            if (!H(up) || !H(lo)) return;
            Vector3 tA = P(lo).Value - P(up).Value;
            Vector3? tF = H(end) ? P(end).Value - P(lo).Value : (Vector3?)null;
            Vector3? n = null;
            if (tF.HasValue)
            {
                var c = Vector3.Cross(tA, tF.Value);
                // ほぼまっすぐの時は曲がる面が決まらないので、前のフレームの面を使う
                if (c.magnitude > 0.08f * tA.magnitude * tF.Value.magnitude) { n = c.normalized; _lastN[side] = n; }
                else n = _lastN[side];
            }
            var ub = _b[up];
            if (n.HasValue) Rotate(ub, Pos(lo) - Pos(up), ub.TransformDirection(hingeLocal), tA, n.Value);
            else Swing(ub, Pos(lo) - Pos(up), tA);
            if (tF.HasValue) Swing(_b[lo], Pos(end) - Pos(lo), tF.Value);
        }

        void Hand(Func<HumanBodyBones, Vector3?> P, HumanBodyBones hand, HumanBodyBones index, HumanBodyBones middle, HumanBodyBones little)
        {
            bool H(HumanBodyBones hb) => Has(hb) && P(hb).HasValue;
            if (!H(hand) || !H(middle)) return;
            if (H(index) && H(little))
                Rotate(_b[hand], Pos(middle) - Pos(hand), Pos(index) - Pos(little), P(middle).Value - P(hand).Value, P(index).Value - P(little).Value);
            else Swing(_b[hand], Pos(middle) - Pos(hand), P(middle).Value - P(hand).Value);
        }

        void Foot(Func<HumanBodyBones, Vector3?> P, HumanBodyBones foot, HumanBodyBones toes, HumanBodyBones shin)
        {
            bool H(HumanBodyBones hb) => Has(hb) && P(hb).HasValue;
            if (!H(foot) || !H(toes) || !H(shin)) return;
            Rotate(_b[foot], Pos(toes) - Pos(foot), Pos(shin) - Pos(foot), P(toes).Value - P(foot).Value, P(shin).Value - P(foot).Value);
        }

        // 足の長さ（もも＋すね）。録った人との大きさの比に使う
        public float LegLength()
        {
            return (Pos(HumanBodyBones.LeftLowerLeg) - Pos(HumanBodyBones.LeftUpperLeg)).magnitude
                 + (Pos(HumanBodyBones.LeftFoot) - Pos(HumanBodyBones.LeftLowerLeg)).magnitude;
        }
    }

    // ---- クリップの書き出し ----

    // HumanTrait の筋肉名 → クリップのプロパティ名（指だけ書き方が違う）
    public static string MuscleProp(string muscle)
    {
        var m = Regex.Match(muscle, @"^(Left|Right) (Thumb|Index|Middle|Ring|Little) (\d) Stretched$");
        if (m.Success) return $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.{m.Groups[3].Value} Stretched";
        m = Regex.Match(muscle, @"^(Left|Right) (Thumb|Index|Middle|Ring|Little) Spread$");
        if (m.Success) return $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.Spread";
        return muscle;
    }

    static float Scale(FuwaMotionImport.Take tk, Solver s)
    {
        var idx = Index(tk);
        if (!idx.TryGetValue((int)HumanBodyBones.LeftUpperLeg, out int a) || !idx.TryGetValue((int)HumanBodyBones.LeftLowerLeg, out int b) || !idx.TryGetValue((int)HumanBodyBones.LeftFoot, out int c)) return 1f;
        var lens = tk.frames.Where(f => f.has[a] && f.has[b] && f.has[c]).Select(f => (f.bone[b] - f.bone[a]).magnitude + (f.bone[c] - f.bone[b]).magnitude).OrderBy(x => x).ToList();
        if (lens.Count == 0) return 1f;
        s.ResetRest(1f);
        return lens[lens.Count / 2] / s.LegLength();
    }

    static Dictionary<int, int> Index(FuwaMotionImport.Take tk)
    {
        var d = new Dictionary<int, int>();
        for (int i = 0; i < tk.bones.Length; i++) d[tk.bones[i]] = i;
        return d;
    }

    // テイクを解いて、各フレームの HumanPose を返す
    public static List<HumanPose> SolveTake(FuwaMotionImport.Take tk, Solver s, out float scale)
    {
        var idx = Index(tk);
        scale = Scale(tk, s);
        var poses = new List<HumanPose>();
        foreach (var f in tk.frames)
        {
            var fr = f;
            Vector3? P(HumanBodyBones hb) => idx.TryGetValue((int)hb, out int i) && fr.has[i] ? fr.bone[i] : (Vector3?)null;
            bool headOk = fr.headRot != Quaternion.identity || fr.headPos != Vector3.zero;
            poses.Add(s.Solve(P, headOk ? fr.headRot : (Quaternion?)null, scale));
        }
        return poses;
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/モーション録画/選んだテイクからNitaのお手本を作る")]
    static void BuildMenu()
    {
        foreach (var o in Selection.objects)
            if (o is TextAsset ta) BuildDemo(AssetDatabase.GetAssetPath(ta));
    }

    // 握りの形（銃を持っている手）。指の Stretched は −1 で曲げ、+1 で伸ばし
    static readonly float GripCurl = -0.55f;

    public static GameObject BuildDemo(string rawPath)
    {
        var tk = FuwaMotionImport.Parse(rawPath);
        if (tk.frames.Count < 2) return null;
        List<HumanPose> poses;
        float scale;
        using (var s = new Solver(ModelPath)) poses = SolveTake(tk, s, out scale);

        var clip = new AnimationClip { name = tk.name + "_nita", frameRate = 30f };
        var curves = new Dictionary<string, AnimationCurve>();
        void Key(string path, Type type, string prop, float t, float v)
        {
            string k = path + "|" + type.Name + "|" + prop;
            if (!curves.TryGetValue(k, out var c)) curves[k] = c = new AnimationCurve();
            c.AddKey(new Keyframe(t, v));
        }
        float t0 = tk.frames[0].t;
        Quaternion lastQ = Quaternion.identity;
        var lastRot = new Dictionary<string, Quaternion>();
        for (int i = 0; i < poses.Count; i++)
        {
            var p = poses[i];
            var f = tk.frames[i];
            float t = f.t - t0;
            Key("", typeof(Animator), "RootT.x", t, p.bodyPosition.x);
            Key("", typeof(Animator), "RootT.y", t, p.bodyPosition.y);
            Key("", typeof(Animator), "RootT.z", t, p.bodyPosition.z);
            var q = p.bodyRotation;
            if (i > 0 && Quaternion.Dot(lastQ, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            lastQ = q;
            Key("", typeof(Animator), "RootQ.x", t, q.x);
            Key("", typeof(Animator), "RootQ.y", t, q.y);
            Key("", typeof(Animator), "RootQ.z", t, q.z);
            Key("", typeof(Animator), "RootQ.w", t, q.w);
            for (int m = 0; m < p.muscles.Length; m++)
            {
                string name = HumanTrait.MuscleName[m];
                float v = p.muscles[m];
                bool finger = name.Contains("Thumb") || name.Contains("Index") || name.Contains("Middle") || name.Contains("Ring") || name.Contains("Little");
                if (finger)
                {
                    // 指は録っていない：銃を持っている手は握り、それ以外は軽く曲げた自然な形
                    bool right = name.StartsWith("Right");
                    bool gripping = f.held && ((right && f.hand != 1) || (!right && f.hand == 1));
                    if (name.EndsWith("Spread")) v = 0f;
                    else v = gripping ? (name.Contains("Thumb") ? -0.1f : GripCurl) : 0.35f;
                }
                Key("", typeof(Animator), MuscleProp(name), t, v);
            }
            // 銃とふわふわ（Nita の子。Nita は原点・大きさ scale に置くので、ローカル＝ワールド÷scale）
            void KeyT(string path, Vector3 pos, Quaternion rot, Vector3 sc)
            {
                Key(path, typeof(Transform), "m_LocalPosition.x", t, pos.x / scale);
                Key(path, typeof(Transform), "m_LocalPosition.y", t, pos.y / scale);
                Key(path, typeof(Transform), "m_LocalPosition.z", t, pos.z / scale);
                if (lastRot.TryGetValue(path, out var pr) && Quaternion.Dot(pr, rot) < 0f) rot = new Quaternion(-rot.x, -rot.y, -rot.z, -rot.w);
                lastRot[path] = rot;
                Key(path, typeof(Transform), "m_LocalRotation.x", t, rot.x);
                Key(path, typeof(Transform), "m_LocalRotation.y", t, rot.y);
                Key(path, typeof(Transform), "m_LocalRotation.z", t, rot.z);
                Key(path, typeof(Transform), "m_LocalRotation.w", t, rot.w);
                Key(path, typeof(Transform), "m_LocalScale.x", t, sc.x / scale);
                Key(path, typeof(Transform), "m_LocalScale.y", t, sc.y / scale);
                Key(path, typeof(Transform), "m_LocalScale.z", t, sc.z / scale);
            }
            KeyT("Gun", f.gunPos, f.gunRot, Vector3.one * (f.held ? Mathf.Max(0.01f, f.gunScale) : 0.0001f));
            KeyT("Ball", f.ballPos, f.ballRot, Vector3.one * 0.5f);
        }
        foreach (var kv in curves)
        {
            var parts = kv.Key.Split('|');
            var c = kv.Value;
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            var type = parts[1] == "Animator" ? typeof(Animator) : typeof(Transform);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(parts[0], type, parts[2]), c);
        }
        var st = AnimationUtility.GetAnimationClipSettings(clip);
        st.loopTime = true;
        // ワールドの録った場所でそのまま再生：根の動きはポーズに焼き込み、元の位置・向きを使う
        st.loopBlendOrientation = true; st.loopBlendPositionY = true; st.loopBlendPositionXZ = true;
        st.keepOriginalOrientation = true; st.keepOriginalPositionY = true; st.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, st);

        Directory.CreateDirectory(ClipDir);
        string clipPath = ClipDir + "/" + clip.name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; }
        else AssetDatabase.CreateAsset(clip, clipPath);
        string ctrlPath = ClipDir + "/" + clip.name + ".controller";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) ?? AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, clip);
        ctrl.layers[0].stateMachine.defaultState.motion = clip;
        AssetDatabase.SaveAssets();

        // シーンに置く
        string objName = "MotionDemo_" + tk.name;
        var old = GameObject.Find(objName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var demo = MakeShowNita(objName);
        demo.transform.localScale = Vector3.one * scale;
        var an = demo.GetComponent<Animator>();
        an.runtimeAnimatorController = ctrl;
        an.applyRootMotion = false;
        an.cullingMode = AnimatorCullingMode.CullCompletely;
        FuwaMotionImport.MakeGunVisual(demo.transform);
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
        ball.transform.SetParent(demo.transform, false);
        ball.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Motion/MotionPreviewBall.mat");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(demo.scene);
        Debug.Log($"[FMR] {tk.name}: Nita のお手本 {poses.Count} フレーム, 大きさ ×{scale:F2}");
        return demo;
    }

    static GameObject MakeShowNita(string name)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(ShowModelPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        Undo.RegisterCreatedObjectUndo(go, "motion demo");
        // 見た目のマテリアルは Mobile のプレハブから
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(ShowPrefabPath);
        if (pf != null)
        {
            var mats = pf.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r => r.name, r => r.sharedMaterials);
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (mats.TryGetValue(r.name, out var m)) r.sharedMaterials = m;
        }
        foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = false;
        return go;
    }

    // ---- 確かめ：Nita の既存アニメから骨の位置を作り、変換し直して元と比べる ----
    public static string RoundTripTest(AnimationClip clip, float step = 1f / 15f)
    {
        var bones = new[] { 0, 7, 8, 54, 9, 10, 11, 13, 15, 17, 12, 14, 16, 18, 1, 3, 5, 19, 2, 4, 6, 20, 27, 30, 36, 42, 45, 51 };
        var tk = new FuwaMotionImport.Take { name = "test", rate = 1f / step, bones = bones };
        var bindings = AnimationUtility.GetCurveBindings(clip);
        var muscleIndex = new Dictionary<string, int>();
        for (int i = 0; i < HumanTrait.MuscleCount; i++) muscleIndex[MuscleProp(HumanTrait.MuscleName[i])] = i;
        var log = new System.Text.StringBuilder();
        using (var src = new Solver(ModelPath))
        using (var dst = new Solver(ModelPath))
        {
            var truth = new List<Vector3[]>();
            float len = Mathf.Max(clip.length, 0.0001f);
            for (float t = 0; t <= len + 1e-4f; t += step)
            {
                src.ResetRest(1f);
                var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount], bodyRotation = Quaternion.identity, bodyPosition = new Vector3(0, 1, 0) };
                src.hph.GetHumanPose(ref pose);
                Vector4 rq = new Vector4(pose.bodyRotation.x, pose.bodyRotation.y, pose.bodyRotation.z, pose.bodyRotation.w);
                foreach (var b in bindings)
                {
                    float v = AnimationUtility.GetEditorCurve(clip, b).Evaluate(t);
                    if (muscleIndex.TryGetValue(b.propertyName, out int mi)) pose.muscles[mi] = v;
                    else if (b.propertyName == "RootT.x") pose.bodyPosition.x = v;
                    else if (b.propertyName == "RootT.y") pose.bodyPosition.y = v;
                    else if (b.propertyName == "RootT.z") pose.bodyPosition.z = v;
                    else if (b.propertyName == "RootQ.x") rq.x = v;
                    else if (b.propertyName == "RootQ.y") rq.y = v;
                    else if (b.propertyName == "RootQ.z") rq.z = v;
                    else if (b.propertyName == "RootQ.w") rq.w = v;
                }
                pose.bodyRotation = new Quaternion(rq.x, rq.y, rq.z, rq.w).normalized;
                src.hph.SetHumanPose(ref pose);
                var f = new FuwaMotionImport.Frame { t = t, bone = new Vector3[bones.Length], has = new bool[bones.Length] };
                for (int i = 0; i < bones.Length; i++)
                {
                    var tr = src.Bone((HumanBodyBones)bones[i]);
                    f.has[i] = tr != null;
                    if (tr != null) f.bone[i] = tr.position;
                }
                f.headRot = src.HeadView; f.headPos = src.Bone(HumanBodyBones.Head).position;
                tk.frames.Add(f);
                truth.Add(f.bone.ToArray());
            }
            var poses = SolveTake(tk, dst, out float scale);
            float sum = 0; int n = 0; float max = 0; string maxName = "";
            var perBone = new float[bones.Length];
            for (int k = 0; k < poses.Count; k++)
            {
                dst.ResetRest(scale);
                var p = poses[k];
                dst.hph.SetHumanPose(ref p);
                for (int i = 0; i < bones.Length; i++)
                {
                    var tr = dst.Bone((HumanBodyBones)bones[i]);
                    if (tr == null || !tk.frames[k].has[i]) continue;
                    float e = (tr.position - truth[k][i]).magnitude;
                    sum += e; n++;
                    perBone[i] = Mathf.Max(perBone[i], e);
                    if (e > max) { max = e; maxName = ((HumanBodyBones)bones[i]) + "@" + tk.frames[k].t.ToString("F2"); }
                }
            }
            log.AppendLine($"{clip.name}: frames {poses.Count}, scale {scale:F3}, mean {sum / Mathf.Max(1, n) * 1000:F1}mm, max {max * 1000:F1}mm ({maxName})");
            var worst = Enumerable.Range(0, bones.Length).OrderByDescending(i => perBone[i]).Take(5).Select(i => $"{(HumanBodyBones)bones[i]} {perBone[i] * 1000:F0}mm");
            log.AppendLine("  worst: " + string.Join(", ", worst));
        }
        return log.ToString();
    }
}
