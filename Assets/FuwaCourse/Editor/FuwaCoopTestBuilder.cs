using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 「みんなで」協力の試作場（2026-10-12）：巨大ふわふわをみんなで押してゴールへ（FuwaCoopGiant）。
// みんなでロビーの +Z 側 65m（色ゴールの会場 Arena の反対側）に島を浮かべる。島・キノコはみんなでロビーのものをコピー、
// 巨大ふわふわはロビーの共有ふわふわ（FuwaSharedBall）をコピーして大きくする。ロビーとの行き来はワープ床（ToSolo のコピー）。
// 何度実行しても同じ結果
public static class FuwaCoopTestBuilder
{
    const string LobbyName = "FuwaVersusLobby";
    static readonly Vector3 Offset = new Vector3(0f, 0f, 65f);
    const float GiantScale = 1.4f;          // ロビーの共有ふわふわは 0.3
    const float MushScale = 3f;             // スタートのキノコも大きく
    const float MushZ = 5f;                 // キノコの位置（島の真ん中から +Z、コースの入口寄り）
    const float GoalIslandR = 4.5f;
    const float IslandEdge = 14.05f;        // 島の床の半径は14。コースはふちのすぐ外から（重ねない）
    const float PathW = 5f, PathT = 0.5f, Stripe = 0.6f, RailH = 0.35f, RailT = 0.14f;
    const string MeshDir = "Assets/FuwaCourse/Generated/Coop";
    const string ProgramPath = "Assets/FuwaCourse/Scripts/FuwaCoopGiant.asset";

    [MenuItem("FuwaCourse/みんなで/協力の試作場（巨大ふわふわ）を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        // 2026-10-12 てじゃ「みんなでワープ→小さい待機ロビーここ、って構造に。デカいロビーやたいせんコースは不要」：
        // この島をみんなでの待機ロビー兼スタートにする。入れ物は新しいルート FuwaCoop（エリアの表示切り替えの versus はこれだけ）。
        // 古いみんなでロビー（FuwaVersusLobby：色ゴールの会場・チーム分けの台つき）は非表示にして残す（島・キノコ・ふわふわのコピー元）
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        var lobby = roots.First(g => g.name == LobbyName).transform;
        Vector3 lc = lobby.position, ac = lc + Offset;
        var coopGo = roots.FirstOrDefault(g => g.name == "FuwaCoop");
        if (coopGo == null) { coopGo = new GameObject("FuwaCoop"); }
        var coop = coopGo.transform; coop.position = ac; coop.rotation = Quaternion.identity; coop.localScale = Vector3.one;
        foreach (var o in new[] { lobby.Find("CoopTest"), coop.Find("CoopTest"), lobby.Find("ToCoopTest") }) if (o != null) Object.DestroyImmediate(o.gameObject);
        var root = new GameObject("CoopTest").transform; root.SetParent(coop, false); root.position = ac;

        // 島
        foreach (var n in new[] { "VersusFloor", "VersusRing", "IslandUnder" })   // 落ちた時の戻しはコース用に別に作る（ロビーの FallReturn はロビーへ戻す）
        {
            var src = lobby.Find(n);
            var g = Object.Instantiate(src.gameObject, root, true); g.name = n;
            g.transform.position = src.position + Offset;
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
        float floorY = root.Find("VersusFloor").GetComponent<Renderer>().bounds.max.y;

        // スタートのキノコ（島の手前寄り）と巨大ふわふわ
        var mushSrc = lobby.Find("CenterMushroom");
        var mush = Object.Instantiate(mushSrc.gameObject, root, true); mush.name = "StartMushroom";
        mush.transform.position = mushSrc.position + Offset + new Vector3(0f, 0f, MushZ);
        mush.transform.localScale = mushSrc.localScale * MushScale;   // 巨大ふわふわが隠れられる大きさに（足もとを基準に大きく）
        var fluffSrc = lobby.Find("SharedFluff");
        var giantGo = Object.Instantiate(fluffSrc.gameObject, root, true); giantGo.name = "GiantFluff";
        giantGo.transform.localScale = Vector3.one * GiantScale;
        // キノコの中の待つ位置：ロビーのふわふわのキノコの根元からの高さを、キノコを大きくしたぶん上げる
        float r1 = GiantScale * 0.5f;
        giantGo.transform.position = mush.transform.position + Vector3.up * (fluffSrc.position.y - mushSrc.position.y) * MushScale;
        var giant = giantGo.GetComponent<FuwaSharedBall>();
        var so = new SerializedObject(giant);
        so.FindProperty("popLayers").intValue = 0;                 // 床ではじけない
        so.FindProperty("hitPadding").floatValue = r1 - 0.1f;       // ふちに当たった風も効くように
        so.FindProperty("shotScale").floatValue = 0.3f;             // FuwaCoopGiant が人数で書き換える
        so.FindProperty("releasePush").floatValue = 1.2f;           // キノコから出る時も重め
        so.FindProperty("releaseUp").floatValue = 1.6f;
        so.FindProperty("fallLimit").floatValue = 2.5f;   // 道から落ちたらすぐキノコへ（ゆっくり落ちるので 8m だと待たされた。てじゃ「落ちてもアウトにならん」）
        so.FindProperty("arenaCenter").objectReferenceValue = null;
        so.FindProperty("arenaRadius").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo(); Sync(giant);
        foreach (var sm in mush.GetComponentsInChildren<FuwaStartMushroom>(true)) { SetU(sm, "sharedBall", giant); Sync(sm); }
        // エアガンの「みんなで吹くふわふわ」に足す
        foreach (var bl in Object.FindObjectsOfType<FuwaBlower>(true))
        {
            var bso = new SerializedObject(bl); var p = bso.FindProperty("sharedBalls");
            var list = new List<Object>();
            for (int i = 0; i < p.arraySize; i++) { var o = p.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null) list.Add(o); }
            if (!list.Contains(giant)) list.Add(giant);
            p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            bso.ApplyModifiedPropertiesWithoutUndo(); Sync(bl);
        }

        // ゴールの島（コースの最後のパーツの出口に FuwaCoopCourse が動かす）：丸い島＋光る円＋花火
        var goalRoot = new GameObject("GoalIsland").transform; goalRoot.SetParent(root, false); goalRoot.position = new Vector3(ac.x, floorY, ac.z + 40f);
        var gisl = GameObject.CreatePrimitive(PrimitiveType.Cylinder); gisl.name = "Ground";
        gisl.transform.SetParent(goalRoot, false); gisl.transform.localPosition = new Vector3(0f, -0.3f, GoalIslandR + 0.05f); gisl.transform.localScale = new Vector3(GoalIslandR * 2f, 0.3f, GoalIslandR * 2f);
        Object.DestroyImmediate(gisl.GetComponent<Collider>()); var gmc = gisl.AddComponent<MeshCollider>(); gmc.sharedMesh = gisl.GetComponent<MeshFilter>().sharedMesh;
        gisl.layer = root.Find("VersusFloor").gameObject.layer;
        gisl.GetComponent<MeshRenderer>().sharedMaterial = root.Find("VersusFloor").GetComponent<Renderer>().sharedMaterial;
        var goal = new GameObject("Goal").transform; goal.SetParent(goalRoot, false); goal.localPosition = new Vector3(0f, 0f, GoalIslandR + 0.05f);
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disc.name = "Circle";
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(goal, false); disc.transform.localPosition = Vector3.up * 0.02f; disc.transform.localScale = new Vector3(4.4f, 0.01f, 4.4f);
        disc.GetComponent<MeshRenderer>().sharedMaterial = GoalMat();
        var flag = RootGo("FuwaCourse2").transform.Find("Goal");   // 旗の代わりにゴールの花火を借りる
        ParticleSystem fx = null;
        var fxSrc = flag != null ? flag.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GoalFx") : null;
        if (fxSrc != null) { var f = Object.Instantiate(fxSrc.gameObject, goal, false); f.name = "GoalFx"; f.transform.localPosition = Vector3.up * 0.5f; fx = f.GetComponentInChildren<ParticleSystem>(true); }

        // 看板（人数・ゴール回数）：ロビーの遊び方の看板の文字をコピー、島の奥に
        var txtSrc = lobby.Find("HowToPlayVersus/Text").GetComponent<TMPro.TextMeshPro>();
        var bRoot = new GameObject("Board").transform; bRoot.SetParent(root, false); bRoot.position = new Vector3(ac.x - 7f, floorY + 4.5f, ac.z + 2f);
        bRoot.rotation = Quaternion.LookRotation(new Vector3(-0.6f, 0f, 1f));
        var board = Object.Instantiate(txtSrc.gameObject, bRoot, false).GetComponent<TMPro.TextMeshPro>(); board.name = "Text";
        board.transform.localPosition = Vector3.zero; board.transform.localRotation = Quaternion.identity;
        board.text = "みんなで運ぼう";

        // スポーン（入口の島のみんなでワープの行き先。作り直しても消えないよう FuwaCoop の直下）
        var spawn = coop.Find("CoopSpawn"); if (spawn == null) { spawn = new GameObject("CoopSpawn").transform; spawn.SetParent(coop, false); }
        spawn.position = new Vector3(ac.x, floorY + 0.05f, ac.z - 10f); spawn.rotation = Quaternion.LookRotation(Vector3.forward);
        // 古いみんなでロビーから、エアガンの台・入口へ戻るワープ・ひとりでへのワープを島へ引っ越し（参照・言語の登録はそのまま）
        Transform Move(string n, Vector3 local, float yaw)
        {
            var t = coop.Find(n) ?? lobby.Find(n); if (t == null) return null;
            t.SetParent(coop, true);
            t.position = spawn.position + Quaternion.Euler(0f, spawn.eulerAngles.y, 0f) * local; t.rotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }
        var stand = Move("GunStand_Versus", new Vector3(2.4f, 0.4f, 1.6f), 0f);
        var marker = coop.Find("GunStand_Versus_Marker") ?? lobby.Find("GunStand_Versus_Marker");
        if (marker != null && stand != null) { marker.SetParent(coop, true); marker.position = stand.position + Vector3.up * 0.3f; }
        var toHub = Move("ReturnToHub", new Vector3(-4.5f, 0.07f, -1.2f), 90f);
        var toSolo = Move("ToSolo", new Vector3(-4.5f, 0.07f, 2.2f), 90f);
        var gunSpot = stand != null ? stand.Find("GunSpot") : spawn;
        // 入口の島の「みんなで」ワープと、ひとりでのロビーの「みんなでへ」ワープの行き先をこの島に
        foreach (var w in Object.FindObjectsOfType<FuwaCourseWarp>(true))
            if (w.name == "VersusGate_Trigger" || w.name == "ToVersus_Trigger") { SetU(w, "playerSpot", spawn); SetU(w, "gunSpot", gunSpot); Sync(w); }
        // エリアの表示切り替え：みんなで＝この島だけ。古いロビーと試合の係は止める
        var av = Object.FindObjectsOfType<FuwaAreaVisibility>(true).FirstOrDefault();
        if (av != null) { var aso = new SerializedObject(av); var vp = aso.FindProperty("versusObjects"); vp.arraySize = 1; vp.GetArrayElementAtIndex(0).objectReferenceValue = coopGo; aso.ApplyModifiedPropertiesWithoutUndo(); Sync(av); }
        lobby.gameObject.SetActive(false);
        var match = Object.FindObjectsOfType<FuwaMatch>(true).FirstOrDefault(); if (match != null) match.gameObject.SetActive(false);
        var back = toHub;

        // 動かす係
        var holder = new GameObject("CoopGiant"); holder.transform.SetParent(root, false);
        var prog = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath);
        if (prog == null)
        {
            prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/FuwaCoopGiant.cs");
            AssetDatabase.CreateAsset(prog, ProgramPath); AssetDatabase.SaveAssets();
        }
        var cg = UdonSharpUndo.AddComponent<FuwaCoopGiant>(holder);
        var countC = new GameObject("CountCenter").transform; countC.SetParent(root, false); countC.position = ac + new Vector3(0f, 0f, 30f);
        cg.giant = giant; cg.arenaCenter = countC; cg.arenaRadius = 60f;   // 人数を数える範囲は島とコース全体
        cg.goal = goal; cg.goalFx = fx; cg.board = board;
        UdonSharpEditorUtility.CopyProxyToUdon(cg);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cg);
        if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(ub); }   // ゴールの合図（ネットワークイベント）を受けるため

        log.Add(BuildCourse(root, ac, floorY, cg, goalRoot, spawn, gunSpot, back));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        log.Add("coop at " + ac + " giant " + giantGo.transform.position.ToString("F2"));
        return string.Join("\n", log);
    }


    // ---------- ランダムコース ----------
    // パーツ：入口が自分の原点（+Z へ進む、道の上面が y=0）、出口が子の Exit。同じ種類を2つずつ作って FuwaCoopCourse が並べる
    static string BuildCourse(Transform root, Vector3 ac, float floorY, FuwaCoopGiant cg, Transform goalRoot, Transform spawn, Transform gunSpot, Transform backWarp)
    {
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Coop");
        var pm = RootGo("FuwaCourse0").transform.Find("PathMesh").GetComponent<MeshRenderer>().sharedMaterials;
        Material pathA = pm[0], pathB = pm.Length > 1 ? pm[1] : pm[0];
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course_Wood.mat");
        int envLayer = root.Find("VersusFloor").gameObject.layer;

        var holder = new GameObject("CoursePieces").transform; holder.SetParent(root, false);
        var startExit = new GameObject("StartExit").transform; startExit.SetParent(root, false);
        startExit.position = new Vector3(ac.x, floorY, ac.z + IslandEdge); startExit.rotation = Quaternion.LookRotation(Vector3.forward);

        // 種類：0=まっすぐ 1=左カーブ 2=右カーブ 3=細い橋 4=向かい風
        var defs = new (string name, int type, float turn)[] { ("Straight", 0, 0f), ("CurveL", 1, 90f), ("CurveR", 2, -90f), ("Bridge", 3, 0f), ("Headwind", 4, 0f) };
        var pieces = new List<Transform>(); var types = new List<int>(); var turns = new List<float>();
        foreach (var d in defs)
        {
            var mesh = PieceMeshes(d.type, out var rail, out var exitPos, out var exitYaw);
            for (int copy = 0; copy < 2; copy++)
            {
                var p = new GameObject(d.name + "_" + copy).transform; p.SetParent(holder, false);
                p.SetPositionAndRotation(startExit.position + new Vector3(copy * 12f - 6f, 0f, types.Count * 3f), startExit.rotation);   // エディタでの仮置き（実行時に並べ替える）
                var path = new GameObject("Path", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)); path.transform.SetParent(p, false);
                path.GetComponent<MeshFilter>().sharedMesh = mesh; path.GetComponent<MeshRenderer>().sharedMaterials = new[] { pathA, pathB, pathA };
                path.GetComponent<MeshCollider>().sharedMesh = mesh; path.layer = envLayer;
                if (rail != null)
                {
                    var r = new GameObject("Rails", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)); r.transform.SetParent(p, false);
                    r.GetComponent<MeshFilter>().sharedMesh = rail; r.GetComponent<MeshRenderer>().sharedMaterial = wood; r.GetComponent<MeshCollider>().sharedMesh = rail;
                    r.layer = envLayer;
                }
                var ex = new GameObject("Exit").transform; ex.SetParent(p, false); ex.localPosition = exitPos; ex.localRotation = Quaternion.Euler(0f, exitYaw, 0f);
                if (d.type == 4) Headwind(p, exitPos.z);
                pieces.Add(p); types.Add(d.type); turns.Add(d.turn);
            }
        }

        // 並べる係（同期するので、エリアの表示切り替えで隠れない FuwaSystem の下に）
        var sys = RootGo("FuwaSystem").transform;
        var oldC = sys.Find("CoopCourse"); if (oldC != null) Object.DestroyImmediate(oldC.gameObject);
        var cgo = new GameObject("CoopCourse"); cgo.transform.SetParent(sys, false);
        EnsureProgram("FuwaCoopCourse");
        var course = UdonSharpUndo.AddComponent<FuwaCoopCourse>(cgo);
        course.pieces = pieces.ToArray(); course.pieceType = types.ToArray(); course.pieceTurn = turns.ToArray();
        course.startExit = startExit; course.goalRoot = goalRoot; course.count = 5; course.giant = cg;
        UdonSharpEditorUtility.CopyProxyToUdon(course);
        var cub = UdonSharpEditorUtility.GetBackingUdonBehaviour(course);
        if (cub != null) { cub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(cub); }

        // 「コースを変える」ボタン：スポーンの横の押せる板
        var btn = GameObject.CreatePrimitive(PrimitiveType.Cube); btn.name = "RerollButton"; btn.transform.SetParent(root, false);
        btn.transform.position = spawn.position + new Vector3(-2.2f, 1.1f, 1.5f); btn.transform.rotation = Quaternion.LookRotation(new Vector3(0.5f, 0f, -1f));
        btn.transform.localScale = new Vector3(1.4f, 0.5f, 0.12f);
        btn.GetComponent<MeshRenderer>().sharedMaterial = GoalMat();
        EnsureProgram("FuwaCoopButton");
        var b = UdonSharpUndo.AddComponent<FuwaCoopButton>(btn); b.course = course; UdonSharpEditorUtility.CopyProxyToUdon(b);
        var bub = UdonSharpEditorUtility.GetBackingUdonBehaviour(b); if (bub != null) { bub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; bub.interactText = "コースを変える"; EditorUtility.SetDirty(bub); }
        var lblSrc = RootGo("FuwaVersusLobby").transform.Find("HowToPlayVersus/Text").gameObject;
        var lbl = Object.Instantiate(lblSrc, btn.transform, false).GetComponent<TMPro.TextMeshPro>();
        lbl.name = "Label"; lbl.transform.localPosition = new Vector3(0f, 0f, -0.6f); lbl.transform.localRotation = Quaternion.identity;
        lbl.transform.localScale = new Vector3(lblSrc.transform.localScale.x / 1.4f, lblSrc.transform.localScale.y / 0.5f, lblSrc.transform.localScale.z / 0.12f) * 0.35f;
        lbl.text = "コースを変える";

        // 落ちた人の戻し：コースの下の大きな板（FuwaLobbyFall）→ 島のスポーンへ（見えないワープ）
        var fallWarp = Object.Instantiate(backWarp.gameObject, root, true); fallWarp.name = "CoopFallWarp";
        foreach (var rr in fallWarp.GetComponentsInChildren<Renderer>(true)) rr.enabled = false;
        foreach (var cc in fallWarp.GetComponentsInChildren<Collider>(true)) cc.enabled = false;
        foreach (var ps in fallWarp.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
        var fw = fallWarp.GetComponentInChildren<FuwaCourseWarp>(true); SetU(fw, "playerSpot", spawn); SetU(fw, "gunSpot", gunSpot); Sync(fw);
        var lobbyFall = RootGo("FuwaVersusLobby").transform.Find("FallReturn").GetComponent<FuwaLobbyFall>();
        var fall = new GameObject("CoopFall"); fall.transform.SetParent(root, false); fall.transform.position = ac + new Vector3(0f, -12f, 30f);
        var fbx = fall.AddComponent<BoxCollider>(); fbx.isTrigger = true; fbx.size = new Vector3(150f, 4f, 150f);
        EnsureProgram("FuwaLobbyFall");
        var nf = UdonSharpUndo.AddComponent<FuwaLobbyFall>(fall);
        nf.returnWarp = fw; nf.fade = lobbyFall.fade; nf.fallSound = lobbyFall.fallSound; UdonSharpEditorUtility.CopyProxyToUdon(nf);
        var nub = UdonSharpEditorUtility.GetBackingUdonBehaviour(nf); if (nub != null) { nub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(nub); }
        return "course pieces " + pieces.Count;
    }

    static void EnsureProgram(string name)
    {
        string path = "Assets/FuwaCourse/Scripts/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) return;
        var prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/" + name + ".cs");
        AssetDatabase.CreateAsset(prog, path); AssetDatabase.SaveAssets();
    }

    // 向かい風：道の上の箱（巨大ふわふわを入口の方へ押し返す）＋風の筋
    static void Headwind(Transform piece, float len)
    {
        var z = new GameObject("Wind"); z.transform.SetParent(piece, false);
        z.transform.localPosition = new Vector3(0f, 2.5f, len * 0.5f); z.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var bc = z.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(PathW, 5f, len - 1f);
        z.layer = 2;   // Ignore Raycast
        EnsureProgram("FuwaCoopWind");
        var w = UdonSharpUndo.AddComponent<FuwaCoopWind>(z);
        w.strength = 1.2f; w.onSeconds = 2f; w.offSeconds = 2.5f;   // 2.5 だと1人の風では止まった時しか進めず、押し戻される一方だった
        var src = RootGo("FuwaCourse2").transform.Find("Gimmick_Tailwind/TailwindParticles");
        if (src != null)
        {
            var ps = Object.Instantiate(src.gameObject, z.transform, false); ps.name = "Streaks";
            ps.transform.localPosition = new Vector3(0f, -1.5f, 0f); ps.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);   // 粒の +x が風の向き
            var p = ps.GetComponent<ParticleSystem>(); var sh = p.shape; sh.position = Vector3.zero; sh.scale = new Vector3(len - 1f, 2.5f, PathW * 0.9f);
            w.particles = p;
        }
        UdonSharpEditorUtility.CopyProxyToUdon(w);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(w); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
    }

    // パーツの形：中心線（位置・右向き）を並べて、道の板（上面はしましま2色）と手すり
    static Mesh PieceMeshes(int type, out Mesh rail, out Vector3 exitPos, out float exitYaw)
    {
        var frames = new List<(Vector3 p, Vector3 r)>();
        float width = PathW; bool rails = true;
        if (type == 0 || type == 3 || type == 4)
        {
            float len = type == 0 ? 8f : 10f;
            if (type == 3) { width = 2.2f; rails = false; }
            for (float z = 0f; z < len - 1e-3f; z += Stripe) frames.Add((new Vector3(0f, 0f, z), Vector3.right));
            frames.Add((new Vector3(0f, 0f, len), Vector3.right));
            exitPos = new Vector3(0f, 0f, len); exitYaw = 0f;
        }
        else
        {
            const float Rc = 7f; float sgn = type == 1 ? -1f : 1f;   // 左カーブは中心が -x 側
            Vector3 c = new Vector3(sgn * Rc, 0f, 0f);
            int n = Mathf.CeilToInt(Rc * Mathf.PI * 0.5f / Stripe);
            for (int i = 0; i <= n; i++)
            {
                float th = (float)i / n * Mathf.PI * 0.5f;
                var p = c + new Vector3(-sgn * Rc * Mathf.Cos(th), 0f, Rc * Mathf.Sin(th));
                var fwd = new Vector3(sgn * Mathf.Sin(th), 0f, Mathf.Cos(th));
                frames.Add((p, new Vector3(fwd.z, 0f, -fwd.x)));
            }
            exitPos = frames[frames.Count - 1].p; exitYaw = sgn * 90f;
        }
        string key = "CoopPiece" + type;
        var m = Slab(frames, width, key);
        rail = rails ? Rails(frames, width, key + "_Rails") : null;
        return m;
    }

    class MB { public List<Vector3> V = new List<Vector3>(); }
    // 四角（a,b,c,d の順）を、want の向きを表にして足す
    static void Quad(MB mb, List<int> T, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
    {
        int i = mb.V.Count; mb.V.Add(a); mb.V.Add(b); mb.V.Add(c); mb.V.Add(d);
        var n = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(n, want) >= 0) T.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        else T.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
    }

    static Mesh Slab(List<(Vector3 p, Vector3 r)> f, float w, string name)
    {
        var mb = new MB(); var A = new List<int>(); var B = new List<int>(); var S = new List<int>();
        float hw = w * 0.5f; Vector3 dn = Vector3.down * PathT;
        for (int i = 0; i + 1 < f.Count; i++)
        {
            var (p0, r0) = f[i]; var (p1, r1) = f[i + 1];
            Vector3 l0 = p0 - r0 * hw, R0 = p0 + r0 * hw, l1 = p1 - r1 * hw, R1 = p1 + r1 * hw;
            Quad(mb, i % 2 == 0 ? A : B, l0, l1, R1, R0, Vector3.up);
            Quad(mb, S, R0, R1, R1 + dn, R0 + dn, r0);
            Quad(mb, S, l1, l0, l0 + dn, l1 + dn, -r0);
            Quad(mb, S, l0 + dn, R0 + dn, R1 + dn, l1 + dn, Vector3.down);
        }
        { var (p, r) = f[0]; var back = Vector3.Cross(r, Vector3.up); Quad(mb, S, p + r * hw, p + r * hw + dn, p - r * hw + dn, p - r * hw, back); }
        { var (p, r) = f[f.Count - 1]; var fwd = -Vector3.Cross(r, Vector3.up); Quad(mb, S, p - r * hw, p - r * hw + dn, p + r * hw + dn, p + r * hw, fwd); }
        var m = new Mesh { name = name }; m.SetVertices(mb.V); m.subMeshCount = 3; m.SetTriangles(A, 0); m.SetTriangles(B, 1); m.SetTriangles(S, 2);
        m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m, MeshDir + "/" + name + ".asset");
    }

    static Mesh Rails(List<(Vector3 p, Vector3 r)> f, float w, string name)
    {
        var mb = new MB(); var T = new List<int>();
        float hw = w * 0.5f - RailT * 0.5f; Vector3 up = Vector3.up * RailH;
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i + 1 < f.Count; i++)
            {
                var (p0, r0) = f[i]; var (p1, r1) = f[i + 1];
                Vector3 a0 = p0 + r0 * hw * side, a1 = p1 + r1 * hw * side, s0 = r0 * RailT * 0.5f, s1 = r1 * RailT * 0.5f;
                Vector3 o0 = a0 + s0, i0 = a0 - s0, o1 = a1 + s1, i1 = a1 - s1;
                Quad(mb, T, i0 + up, i1 + up, o1 + up, o0 + up, Vector3.up);
                Quad(mb, T, o0, o1, o1 + up, o0 + up, r0);
                Quad(mb, T, i1, i0, i0 + up, i1 + up, -r0);
                if (i == 0) { var bk = Vector3.Cross(r0, Vector3.up); Quad(mb, T, i0, o0, o0 + up, i0 + up, bk); }
                if (i + 2 == f.Count) { var fw = -Vector3.Cross(r1, Vector3.up); Quad(mb, T, o1, i1, i1 + up, o1 + up, fw); }
            }
        var m = new Mesh { name = name }; m.SetVertices(mb.V); m.SetTriangles(T, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        return SaveMesh(m, MeshDir + "/" + name + ".asset");
    }

    static Mesh SaveMesh(Mesh m, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null)
        {
            old.Clear(); old.SetVertices(m.vertices); old.subMeshCount = m.subMeshCount;
            for (int s = 0; s < m.subMeshCount; s++) old.SetTriangles(m.GetTriangles(s), s);
            old.RecalculateNormals(); old.RecalculateBounds(); EditorUtility.SetDirty(old); Object.DestroyImmediate(m); return old;
        }
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static Transform MakeWarp(Transform src, Transform parent, string name, Vector3 pos, Transform playerSpot, Transform gunSpot, string ja, string en)
    {
        var old = parent.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject);
        var g = Object.Instantiate(src.gameObject, parent, true); g.name = name; g.transform.position = pos;
        var w = g.GetComponentInChildren<FuwaCourseWarp>(true);
        SetU(w, "playerSpot", playerSpot); SetU(w, "gunSpot", gunSpot);
        SetU(w, "courseStart", null); SetU(w, "ball", null);
        var wso = new SerializedObject(w); wso.FindProperty("homeAction").intValue = 0; wso.FindProperty("area").intValue = -1; wso.ApplyModifiedPropertiesWithoutUndo();
        Sync(w);
        var label = g.GetComponentsInChildren<TMPro.TextMeshPro>(true).FirstOrDefault();
        if (label != null) label.text = ja;
        return g.transform;
    }

    static Material GoalMat()
    {
        const string p = "Assets/FuwaCourse/Materials/CoopGoal.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Goal_Ground.mat")) { name = "CoopGoal" }; AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_Color", new Color(1f, 0.8f, 0.35f, 1f));
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(0.5f, 0.35f, 0.05f, 1f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject RootGo(string n) { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n); }
    static void SetU(Object o, string field, Object v) { var s = new SerializedObject(o); var p = s.FindProperty(field); if (p != null) { p.objectReferenceValue = v; s.ApplyModifiedPropertiesWithoutUndo(); } }
    static void Sync(Object o) { if (o is UdonSharpBehaviour u) UdonSharpEditorUtility.CopyProxyToUdon(u); EditorUtility.SetDirty(o); }
}
