using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// かってにスクショの試し撮り（エディタで、各フォトポイントで撮れる写真をだいたい再現して PNG に書き出す）。
// ふわふわは白い球、自分は灰色のカプセルで代わりに置く。撮る位置の選び方は FuwaPhotoAlbum.PlaceAround / PlaceFireShot と同じ（2026-11-26）
public static class FuwaPhotoSpotPreview
{
    public static string Run(string dir, int w = 400, int h = 225)
    {
        var a = Object.FindObjectsOfType<FuwaPhotoAlbum>(true).FirstOrDefault();
        if (a == null) return "no album";
        var occ = typeof(FuwaPhotoAlbum).GetMethod("Occlusion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int mask = a.obstacleMask;
        System.IO.Directory.CreateDirectory(dir);
        var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(sph.GetComponent<Collider>()); sph.transform.localScale = Vector3.one * 0.6f;
        var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule); Object.DestroyImmediate(cap.GetComponent<Collider>()); cap.transform.localScale = new Vector3(0.45f, 0.65f, 0.45f);
        var go = new GameObject("tmpcam"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.allowHDR = false;
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 }; cam.targetTexture = rt; cam.aspect = 16f / 9f;
        var log = new List<string>();
        try
        {
            foreach (var p in Object.FindObjectsOfType<FuwaPhotoPoint>(true))
            {
                if (p.slot < 0) continue;
                var root = p.transform.root; bool wa = root.gameObject.activeSelf; root.gameObject.SetActive(true); Physics.SyncTransforms();
                Vector3 c = p.transform.position; Vector3 ball;
                if (Physics.Raycast(c - Vector3.up * 1.5f, Vector3.down, out var gh, 7f, mask, QueryTriggerInteraction.Ignore)) ball = gh.point + Vector3.up * 0.6f; else ball = c - Vector3.up * 3f;
                Vector3 fwd = p.transform.forward; fwd.y = 0; fwd.Normalize(); Vector3 side = Vector3.Cross(Vector3.up, fwd);
                float pref = Vector3.Dot(p.camPose.position - c, side) >= 0 ? 1 : -1;
                Vector3 feet = ball - fwd * 2.5f;
                if (Physics.Raycast(feet + Vector3.up * 1f, Vector3.down, out var fh, 4f, mask, QueryTriggerInteraction.Ignore)) feet = fh.point; else feet.y = ball.y - 0.6f;
                Vector3 chest = feet + Vector3.up * 0.85f, head = feet + Vector3.up * 1.3f;
                cap.transform.position = feet + Vector3.up * 0.65f; sph.transform.position = ball;
                float nb = 1e9f; Vector3 np = c + Vector3.up * 3, nd = (ball - np).normalized; float F = 45f;
                if (p.kind == 2)
                {
                    Vector3 m0 = chest + fwd * 0.45f + side * 0.15f; Vector3 mid = Vector3.Lerp(chest, m0, 0.5f);
                    for (int k = 0; k < 2; k++)
                    {
                        float mm = k == 0 ? 1 : -1; Vector3 pos, look; float fov;
                        if (p.order == 1) { pos = mid + (side * (2.2f * mm) + fwd * 0.55f + Vector3.up * -0.05f); look = Vector3.Lerp(chest, m0 + fwd * 0.9f, 0.55f); fov = 56; }
                        else { pos = m0 + (fwd * 1.3f + side * (0.85f * mm) + Vector3.up * 0.2f); look = Vector3.Lerp(head, m0, 0.6f); fov = 44; }
                        if (Physics.CheckSphere(pos, 0.2f, mask, QueryTriggerInteraction.Ignore)) continue;
                        var d = (look - pos).normalized; float b = (float)occ.Invoke(a, new object[] { pos, d, fov, chest, m0 });
                        if (b < 0.2f) { nb = b; np = pos; nd = d; F = fov; break; }
                        if (b < nb) { nb = b; np = pos; nd = d; F = fov; }
                    }
                }
                else
                {
                    Arr(a, p.kind, p.order, out var AX, out var SX, out var UX, out var D, out var FF); F = FF;
                    Vector3 basePos = (p.kind == 0 && p.order == 2) ? chest : ball;
                    for (int i = 0; i < AX.Length; i++)
                    {
                        var pos = basePos + (fwd * AX[i] + side * SX[i] * pref + Vector3.up * UX[i]) * D;
                        if (Physics.CheckSphere(pos, 0.3f, mask, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.SphereCast(pos, 0.1f, (ball - pos).normalized, out var hh, (ball - pos).magnitude - 0.4f, mask, QueryTriggerInteraction.Ignore)) continue;
                        if (p.clearRadius > 0 && Physics.SphereCast(pos, p.clearRadius, (ball - pos).normalized, out hh, (ball - pos).magnitude * 0.7f, mask, QueryTriggerInteraction.Ignore)) continue;
                        Vector3 dB = (ball + fwd * p.lookAhead - pos).normalized, d = dB; float fov = FF;
                        Vector3 dC = (chest - pos).normalized; float ang = Vector3.Angle(dB, dC);
                        if (ang < 70f) { float wB = p.kind == 0 ? 0.5f : 0.6f; d = (dB * wB + dC * (1 - wB)).normalized; fov = Mathf.Clamp(ang * 1.1f + (p.kind == 0 ? 20f : 14f), 32f, 62f); }
                        float b = (float)occ.Invoke(a, new object[] { pos, d, fov, ball, chest });
                        if (b < 0.2f) { nb = b; np = pos; nd = d; F = fov; break; }
                        if (b < nb) { nb = b; np = pos; nd = d; F = fov; }
                    }
                }
                go.transform.position = np; go.transform.rotation = Quaternion.LookRotation(nd, Vector3.up); cam.fieldOfView = F; cam.Render();
                var d2 = new RenderTexture(w, h, 0); Graphics.Blit(rt, d2); RenderTexture.active = d2;
                var tx = new Texture2D(w, h, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, w, h), 0, 0); tx.Apply(); RenderTexture.active = null; d2.Release();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, root.name + "_" + p.slot + ".png"), tx.EncodeToPNG()); Object.DestroyImmediate(tx);
                log.Add(root.name + "\t" + p.slot + "\t" + (p.kind == 2 ? "fire" : p.kind == 0 ? "far" : "near") + p.order + (p.name.StartsWith("PhotoPointAdd") ? " NEW" : ""));
                root.gameObject.SetActive(wa);
            }
        }
        finally
        {
            Object.DestroyImmediate(sph); Object.DestroyImmediate(cap); cam.targetTexture = null; Object.DestroyImmediate(go); rt.Release();
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "tags.txt"), string.Join("\n", log));
        return log.Count + " shots";
    }

    static void Arr(FuwaPhotoAlbum a, int kind, int order, out float[] AX, out float[] SX, out float[] UX, out float D, out float F)
    {
        if (kind == 0 && order == 2) { AX = new[] { -0.55f, -0.55f, -0.35f, -0.35f, 0.3f, 0.3f }; SX = new[] { 0.25f, -0.25f, 0.5f, -0.5f, 0.9f, -0.9f }; UX = new[] { 0.45f, 0.45f, 0.6f, 0.6f, 0.4f, 0.4f }; D = a.farDistance; F = 50f; return; }
        if (kind == 0) { AX = new[] { 0.3f, 0.3f, 0.65f, 0.65f, -0.2f, -0.2f, 0.9f, 0.9f, -0.8f, -0.8f }; SX = new[] { 0.95f, -0.95f, 0.7f, -0.7f, 0.9f, -0.9f, 0.3f, -0.3f, 0.3f, -0.3f }; UX = new[] { 0.45f, 0.45f, 0.35f, 0.35f, 0.55f, 0.55f, 0.45f, 0.45f, 0.5f, 0.5f }; D = a.farDistance; F = 50f; return; }
        if (order == 2) { AX = new[] { 0.45f, 0.45f, 0.2f, 0.2f, 0.85f, 0.85f }; SX = new[] { 0.25f, -0.25f, 0.5f, -0.5f, 0.55f, -0.55f }; UX = new[] { 1.05f, 1.05f, 0.9f, 0.9f, 0.3f, 0.3f }; D = a.closeDistance; F = 45f; return; }
        AX = new[] { 0.9f, 0.9f, 0.55f, 0.55f, 1.2f, 1.2f, 0.25f, 0.25f }; SX = new[] { 0.5f, -0.5f, 0.85f, -0.85f, 0.35f, -0.35f, 0.15f, -0.15f }; UX = new[] { 0.08f, 0.08f, 0.3f, 0.3f, 0.2f, 0.2f, 0.75f, 0.75f }; D = a.closeDistance; F = 45f;
    }
}
