#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 紹介PVの撮影（2026-11-20 てじゃ「前つくったみたいな紹介PVも撮りたい」「Bメイン」）。
// カメラの通り道（PV/pv_shots.json）どおりに動かして、Play 中に1コマずつ撮って ffmpeg に流す。
// Time.captureFramerate で時間を1コマずつ進めるので、撮るのが遅くても動画はなめらか（60fps）。
// エディタ専用（ワールドのビルドには入らない）。メニューは Editor/FuwaPv.cs

[Serializable] public class FuwaPvKey { public float t; public float[] p; public float[] l; public float fov; }
[Serializable]
public class FuwaPvShot
{
    public string name;
    public string area = "stage1";   // hub / stage1 / stage2 / playground：そのエリアの物だけ出す
    public float dur = 5f;
    public float fov = 50f;
    public string ease = "soft";     // soft（はじめと終わりを少しゆるめる）/ inout / linear / in / out
    public float preroll = 2f;       // 撮り始める前に流しておく秒数（粒・仕掛けを動かしておく）
    public bool skip;
    public string[] hide;            // このショットだけ隠す物（シーンのパス）
    public string[] show;            // このショットだけ出す物
    public float[] player;           // プレイヤーを置く場所（ClientSim。省略時はエリアの決まった場所）
    public string note;
    public FuwaPvKey[] keys;
}
[Serializable] public class FuwaPvList { public string[] hide; public FuwaPvShot[] shots; }

public static class FuwaPvPath
{
    static Vector3 V(float[] a) => a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : Vector3.zero;

    public static float Ease(string e, float x)
    {
        x = Mathf.Clamp01(x);
        float s = x * x * (3f - 2f * x);
        switch (e)
        {
            case "linear": return x;
            case "inout": return s;
            case "in": return x * x;
            case "out": return 1f - (1f - x) * (1f - x);
            default: return Mathf.Lerp(x, s, 0.5f);
        }
    }

    static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float f)
    {
        float f2 = f * f, f3 = f2 * f;
        return 0.5f * ((2f * p1) + (-p0 + p2) * f + (2f * p0 - 5f * p1 + 4f * p2 - p3) * f2 + (-p0 + 3f * p1 - 3f * p2 + p3) * f3);
    }

    // time：ショットの頭からの秒
    public static void Eval(FuwaPvShot s, float time, out Vector3 pos, out Vector3 look, out float fov)
    {
        var k = s.keys;
        float fov0 = s.fov > 0 ? s.fov : 50f;
        if (k == null || k.Length == 0) { pos = Vector3.zero; look = Vector3.forward; fov = fov0; return; }
        float t0 = k[0].t, t1 = k[k.Length - 1].t;
        float u = t1 > t0 ? Mathf.Lerp(t0, t1, Ease(s.ease, s.dur > 0 ? time / s.dur : 0f)) : t0;
        int i = 0;
        while (i < k.Length - 2 && u > k[i + 1].t) i++;
        int j = Mathf.Min(i + 1, k.Length - 1);
        float f = k[j].t > k[i].t ? Mathf.Clamp01((u - k[i].t) / (k[j].t - k[i].t)) : 0f;
        var a = k[Mathf.Max(i - 1, 0)]; var b = k[i]; var c = k[j]; var d = k[Mathf.Min(j + 1, k.Length - 1)];
        pos = CR(V(a.p), V(b.p), V(c.p), V(d.p), f);
        look = CR(V(a.l), V(b.l), V(c.l), V(d.l), f);
        float fb = b.fov > 0 ? b.fov : fov0, fc = c.fov > 0 ? c.fov : fov0;
        fov = Mathf.Lerp(fb, fc, f * f * (3f - 2f * f));
    }

    public static void Place(Camera cam, FuwaPvShot s, float time)
    {
        Eval(s, time, out var p, out var l, out var fov);
        cam.transform.position = p;
        var dir = l - p; if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
        cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        cam.fieldOfView = fov;
    }
}

public class FuwaPvRecorder : MonoBehaviour
{
    public List<FuwaPvShot> shots = new List<FuwaPvShot>();
    public int width = 1920, height = 1080, fps = 60, supersample = 1, crf = 14;
    public string outDir;
    public string logPath;
    public Action<FuwaPvShot> applyArea;   // エリアの出し入れ（毎コマ呼ぶ。Udon が戻しても上書き）
    public Action<FuwaPvShot> onShotStart; // プレイヤーを置く等
    public Action onFinished;

    Camera _cam;
    // PlayerLocal(10)・MirrorReflection(18)・モニターにだけ写す毛玉(22) は撮らない
    public static int CullingMask => ~((1 << 10) | (1 << 18) | (1 << 22));

    void Log(string s)
    {
        Debug.Log("[PV] " + s);
        if (!string.IsNullOrEmpty(logPath)) try { System.IO.File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss ") + s + "\n"); } catch { }
    }

    IEnumerator Start()
    {
        var go = new GameObject("PvCamera"); go.transform.SetParent(transform, false);
        _cam = go.AddComponent<Camera>();
        _cam.enabled = false; _cam.nearClipPlane = 0.05f; _cam.farClipPlane = 4000f; _cam.allowMSAA = true; _cam.allowHDR = false;
        var main = Camera.main;
        if (main != null) { _cam.clearFlags = main.clearFlags; _cam.backgroundColor = main.backgroundColor; }
        else _cam.clearFlags = CameraClearFlags.Skybox;
        _cam.cullingMask = CullingMask;

        int prevCap = Time.captureFramerate;
        Time.captureFramerate = fps;
        int W = width * supersample, H = height * supersample;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = supersample > 1 ? 4 : 8 };
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        System.IO.Directory.CreateDirectory(outDir);
        Log($"start {shots.Count} shots {W}x{H}@{fps}");
        foreach (var s in shots)
        {
            onShotStart?.Invoke(s);
            int pre = Mathf.RoundToInt(s.preroll * fps);
            for (int f = 0; f < pre; f++) { applyArea?.Invoke(s); FuwaPvPath.Place(_cam, s, 0f); yield return null; }
            string path = outDir + "/" + s.name + ".mp4";
            string vf = "vflip" + (supersample > 1 ? $",scale={width}:{height}:flags=lanczos" : "");
            var psi = new System.Diagnostics.ProcessStartInfo("ffmpeg",
                $"-y -v error -f rawvideo -pix_fmt rgb24 -s {W}x{H} -r {fps} -i - -vf {vf} -c:v libx264 -preset slow -crf {crf} -pix_fmt yuv420p \"{path}\"")
            { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true };
            var ff = System.Diagnostics.Process.Start(psi);
            var errTask = ff.StandardError.ReadToEndAsync();
            var stdin = ff.StandardInput.BaseStream;
            int n = Mathf.Max(1, Mathf.RoundToInt(s.dur * fps));
            var t0 = DateTime.Now;
            bool failed = false;
            for (int f = 0; f < n && !failed; f++)
            {
                applyArea?.Invoke(s);
                FuwaPvPath.Place(_cam, s, f / (float)fps);
                yield return new WaitForEndOfFrame();
                applyArea?.Invoke(s);
                _cam.targetTexture = rt; _cam.Render(); _cam.targetTexture = null;
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0, false);
                RenderTexture.active = prev;
                var bytes = tex.GetRawTextureData<byte>();
                try { stdin.Write(bytes.ToArray(), 0, bytes.Length); }
                catch (Exception e) { Log("write error " + e.Message); failed = true; }
            }
            try { stdin.Close(); } catch { }
            string shotName = s.name; double sec = (DateTime.Now - t0).TotalSeconds;
            // エンコードの終わりは別スレッドで待つ（次のショットを止めない）
            var th = new System.Threading.Thread(() =>
            {
                ff.WaitForExit();
                string err = errTask.Result;
                Log($"done {shotName} {n}f {sec:0}s exit={ff.ExitCode}" + (string.IsNullOrWhiteSpace(err) ? "" : " err=" + err.Trim()));
            });
            th.Start();
        }
        Time.captureFramerate = prevCap;
        rt.Release(); Destroy(rt); Destroy(tex);
        yield return new WaitForSecondsRealtime(3f);
        onFinished?.Invoke();
        Log("all finished");
        Destroy(gameObject);
    }
}
#endif
