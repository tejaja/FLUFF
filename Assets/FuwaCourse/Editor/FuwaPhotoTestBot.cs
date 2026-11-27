// 2026-11-27 かってにスクショの構図テスト用（てじゃ「1回テストで全コース撮ってみせて」）。Play（ClientSim）中にメニューから。
//  コースごとにフォトポイントへ自分とふわふわを置いて、本物の FuwaPhotoAlbum に撮らせ、ボードの6枚を1枚の画像にして保存する。
//  アバターの代わりに Nita を自分の位置に立てる。テストが終わったら消してよいファイル
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharpEditor;

[InitializeOnLoad]
public static class FuwaPhotoTestBot
{
    static string[] AllCourses = { "FuwaCourse0", "FuwaCourse", "FuwaCourse2", "FuwaCourse3", "FuwaCourse4", "FuwaCourse5", "FuwaCourse2_1", "FuwaCourse2_2", "FuwaCourse2_3", "FuwaCourse2_4", "FuwaCourse2_5" };
    public static string OutDir = "";
    // 指定するとプレイヤーとふわふわをここに置いて撮る（実際の遊び方に近い位置で1か所だけ試す時。2026-11-28）
    public static Vector3? ForcePlayer, ForceBall;
    static string[] Courses;
    static bool _run;
    static int _c, _p, _phase;
    static float _t0;
    static List<FuwaPhotoPoint> _pts;
    static UdonBehaviour _album, _ballU;
    static FuwaBall _ball;
    static Rigidbody _rb;
    static GameObject _stand;
    static Vector3 _ppos, _fwd, _bpos;
    static bool _fired; static int _rt;
    public static string Log = "";

    static FuwaPhotoTestBot() { EditorApplication.update += Tick; }

    public static void Start(string outDir, string[] only = null)
    {
        Courses = only ?? AllCourses;
        OutDir = outDir; System.IO.Directory.CreateDirectory(outDir);
        var albumP = Resources.FindObjectsOfTypeAll<FuwaPhotoAlbum>().First(x => x.gameObject.scene.IsValid());
        _album = UdonSharpEditorUtility.GetBackingUdonBehaviour(albumP);
        _ball = Resources.FindObjectsOfTypeAll<FuwaBall>().First(x => x.gameObject.scene.IsValid());
        _ballU = UdonSharpEditorUtility.GetBackingUdonBehaviour(_ball);
        _rb = _ball.GetComponent<Rigidbody>();
        if (_stand == null)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TejaShop/Nita/FBX/Nita.fbx");
            _stand = Object.Instantiate(fbx); _stand.name = "_PhotoTestStand";
        }
        // 落下判定・ロビーに戻す床など、自分を別の所へ動かす物を止める
        foreach (var c in Resources.FindObjectsOfTypeAll<FuwaPlayerCatcher>()) if (c.gameObject.scene.IsValid()) { var u = UdonSharpEditorUtility.GetBackingUdonBehaviour(c); if (u != null) u.enabled = false; }
        foreach (var u in Resources.FindObjectsOfTypeAll<UdonBehaviour>()) if (u.gameObject.scene.IsValid() && (u.gameObject.name.Contains("Fall") || u.gameObject.name.Contains("Catcher") || u.gameObject.name.Contains("PlayerOut"))) u.enabled = false;
        _c = 0; _p = -1; _phase = 0; _run = true; Log = "";
        System.IO.File.WriteAllText(System.IO.Path.Combine(OutDir, "log.txt"), "start\n");
    }

    public static void Stop() { _run = false; if (_stand != null) Object.DestroyImmediate(_stand); }

    static void L(string s) { System.IO.File.AppendAllText(System.IO.Path.Combine(OutDir, "log.txt"), s + "\n"); }

    static Transform Root(string n)
    {
        return Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == n && t.parent == null && t.gameObject.scene.IsValid());
    }

    static void Tick()
    {
        if (!_run) return;
        if (!EditorApplication.isPlaying) { _run = false; return; }
        try { Step(); } catch (System.Exception e) { L("EXC " + e); _run = false; }
    }

    static void Step()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        if (_c >= Courses.Length) { L("DONE"); Stop(); return; }
        Transform root = Root(Courses[_c]);
        if (root == null) { L("no root " + Courses[_c]); _c++; return; }
        // エリアを出しっぱなしに
        for (Transform t = root; t != null; t = t.parent) if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);

        if (_p < 0)
        {
            _pts = root.GetComponentsInChildren<FuwaPhotoPoint>(true).OrderBy(x => x.slot).ToList();
            var albumP = Resources.FindObjectsOfTypeAll<FuwaPhotoAlbum>().First(x => x.gameObject.scene.IsValid());
            Transform start = albumP.courseStarts.FirstOrDefault(s => s != null && s.IsChildOf(root));
            if (start != null) _ballU.SetProgramVariable("startPoint", start);
            _ballU.SetProgramVariable("_startTime", 1000f + _c * 7f);
            L("course " + Courses[_c] + " points=" + _pts.Count + " start=" + (start != null ? start.name : "null"));
            _p = 0; _phase = 0;
            return;
        }
        if (_p >= _pts.Count)
        {
            if (_phase == 0) { _t0 = Time.realtimeSinceStartup; _phase = 1; return; }
            if (Time.realtimeSinceStartup - _t0 < 0.3f) return;
            SaveSheet(Courses[_c]);
            _c++; _p = -1; return;
        }

        FuwaPhotoPoint pt = _pts[_p];
        if (_phase == 0)
        {
            _fwd = pt.transform.forward; _fwd.y = 0f; _fwd = _fwd.sqrMagnitude > 0.001f ? _fwd.normalized : Vector3.forward;
            bool found = false;
            foreach (float back in new[] { 2.2f, 1.6f, 1.0f, 0.4f, 2.8f, -0.4f, -1.0f })
            {
                Vector3 c = pt.transform.position - _fwd * back;
                // 道（上を向いた床）だけ。水の底などは避けたいので、上から最初に当たった物の上
                if (Physics.Raycast(c + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f) { _ppos = hit.point; found = true; break; }
            }
            for (int ring = 1; !found && ring <= 4; ring++)
                for (int a = 0; a < 12 && !found; a++)
                {
                    Vector3 c = pt.transform.position + Quaternion.Euler(0f, a * 30f, 0f) * Vector3.forward * ring;
                    if (Physics.Raycast(c + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f) { _ppos = hit.point; found = true; }
                }
            if (!found) { _ppos = pt.transform.position - Vector3.up * 3.5f; L("    no ground " + pt.name); }
            _bpos = pt.transform.position + _fwd * 0.8f + Vector3.up * 0.9f;
            if (ForcePlayer.HasValue && ForceBall.HasValue)
            {
                _ppos = ForcePlayer.Value; _bpos = ForceBall.Value;
                Vector3 d = _bpos - _ppos; d.y = 0f; if (d.sqrMagnitude > 0.01f) _fwd = d.normalized;
            }
            lp.TeleportTo(_ppos, Quaternion.LookRotation(_fwd));
            _t0 = Time.realtimeSinceStartup; _phase = 1; _fired = false; _rt = 0;
            return;
        }
        // 毎フレーム固定（落下判定などで別の所へ戻されたら置き直す）
        if ((lp.GetPosition() - _ppos).sqrMagnitude > 1f && _rt++ < 30) { lp.TeleportTo(_ppos, Quaternion.LookRotation(_fwd)); if (_phase >= 1) { if (_rt <= 2 || _rt == 30) L("    re-teleport " + pt.name + " was at " + lp.GetPosition().ToString("F1") + " want " + _ppos.ToString("F1")); _t0 = Time.realtimeSinceStartup; if (_phase == 2) _phase = 1; } return; }
        if (_stand != null) { _stand.transform.position = lp.GetPosition(); _stand.transform.rotation = Quaternion.LookRotation(_fwd); }
        _rb.isKinematic = false; _rb.position = _bpos; _ball.transform.position = _bpos; _rb.velocity = _fwd * 2.5f + Vector3.up * 0.3f;
        float el = Time.realtimeSinceStartup - _t0;
        if (_phase == 1 && el > 0.5f)
        {
            for (int q = 0; q < 2; q++)
            {
                if (_album.publicVariables.TryGetVariableType("__" + q + "_point__param", out _) || true) { _album.SetProgramVariable("__" + q + "_point__param", UdonSharpEditorUtility.GetBackingUdonBehaviour(pt)); _album.SetProgramVariable("__" + q + "_ball__param", _ballU); }
            }
            _album.SendCustomEvent("__0_OnPhotoPoint");
            _phase = 2; _t0 = Time.realtimeSinceStartup;
            return;
        }
        if (_phase == 2)
        {
            if (!_fired && el > 0.15f && pt.kind == 2)
            {
                Vector3 ch = lp.GetPosition() + Vector3.up * 1.0f;
                Vector3 right = Vector3.Cross(Vector3.up, _fwd);
                _album.SetProgramVariable("__0_origin__param", ch + _fwd * 0.45f + right * 0.15f);
                _album.SetProgramVariable("__0_aim__param", (_fwd + Vector3.up * 0.15f).normalized);
                _album.SendCustomEvent("__0_OnFire");
                _fired = true;
            }
            bool[] has = (bool[])_album.GetProgramVariable("_has");
            int slot = pt.slot;
            if ((has != null && slot >= 0 && slot < has.Length && has[slot] && el > 0.4f) || el > 6f)
            {
                var cam = ((Camera)_album.GetProgramVariable("shotCam")).transform;
                L("  " + pt.name + " slot=" + slot + " kind=" + pt.kind + " ok=" + (has != null && has[slot]) + " cam=" + cam.position.ToString("F2") + " fwd=" + cam.forward.ToString("F2") + " player=" + lp.GetPosition().ToString("F2") + " ball=" + _bpos.ToString("F2"));
                _p++; _phase = 0;
            }
        }
    }

    static void SaveSheet(string name)
    {
        var photos = (RenderTexture[])_album.GetProgramVariable("_photos");
        int w = 640, h = 360, gap = 8;
        var sheet = new Texture2D(w * 3 + gap * 2, h * 2 + gap, TextureFormat.RGB24, false);
        var fill = new Color32[sheet.width * sheet.height]; for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(240, 240, 240, 255); sheet.SetPixels32(fill);
        var tmp = RenderTexture.GetTemporary(w, h, 0);
        for (int s = 0; s < 6; s++)
        {
            if (photos == null || s >= photos.Length || photos[s] == null) continue;
            Graphics.Blit(photos[s], tmp);
            RenderTexture.active = tmp;
            int col = s % 3, row = s / 3;
            sheet.ReadPixels(new Rect(0, 0, w, h), col * (w + gap), (1 - row) * (h + gap));
        }
        RenderTexture.active = null; RenderTexture.ReleaseTemporary(tmp);
        sheet.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(OutDir, name + ".jpg"), sheet.EncodeToJPG(88));
        L("saved " + name);
    }
}
