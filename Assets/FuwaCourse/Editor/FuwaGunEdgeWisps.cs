using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 銃の「当たる範囲のふち」のすじ（2026-11-24 てじゃ「ショットが見た目より広いのが分かりづらい」「歪ませるのはやだ、横側にさりげないのを」）
//  銃口（Nozzle）の下に EdgeL / EdgeR（細いすじの粒）。向き・速さは撃つたびに FuwaBlower.PlayEdgeWisps が溜めに合わせて決める。何回押しても同じ
public static class FuwaGunEdgeWisps
{
    // 2026-11-24 てじゃ「ちょと露骨」「リングじゃなくて薄めの煙みたいなのが、ふわっとリングの横に出てる感じ」「幅はちょい内寄り」→ 細いすじ → うすいもや
    public static Color Col = new Color(1f, 1f, 1f, 0.28f);
    public static float Size = 0.16f, Stretch = 5f;

    [MenuItem("FuwaCourse/銃/当たる範囲のふちのすじを作り直す")]
    public static void Menu() { Debug.Log(Run()); }
    public static string Run()
    {
        var gun = Object.FindObjectsOfType<FuwaBlower>(true).First(b => b.transform.parent == null && b.name == "FuwaBlower");
        var nz = gun.transform.Find("Nozzle");
        var puffR = gun.puffEffect.GetComponent<ParticleSystemRenderer>();
        ParticleSystem Make(string name)
        {
            var old = nz.Find(name); if (old) Object.DestroyImmediate(old.gameObject);
            var g = new GameObject(name); g.transform.SetParent(nz, false); g.layer = gun.puffEffect.gameObject.layer;
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.playOnAwake = false; m.loop = false; m.duration = 0.3f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.6f); m.startSpeed = 2f;
            m.startSize = new ParticleSystem.MinMaxCurve(Size * 0.75f, Size); m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f); m.startColor = Col; m.maxParticles = 24; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0f;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = 0.04f;
            // ふわっと出て（すぐ濃く）、広がりながら消える
            var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 0.3f; lv.dampen = 0.06f;   // ふわっと止まる
            var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.8f)));
            var r = g.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = puffR.sharedMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }
        var l = Make("EdgeL"); var rr = Make("EdgeR");
        gun.edgeLeft = l; gun.edgeRight = rr;
        UdonSharpEditorUtility.CopyProxyToUdon(gun);
        // 見本台の銃にも（2026-11-24 てじゃ「見本のショットエフェクトが前のまま」）。見本台の銃口へ同じもやを入れる
        int nd = 0;
        foreach (var ds in Object.FindObjectsOfType<FuwaDemoStand>(true))
        {
            if (ds.nozzle == null) continue;
            foreach (var n in new[] { "EdgeL", "EdgeR" }) { var o = ds.nozzle.Find(n); if (o) Object.DestroyImmediate(o.gameObject); }
            var cl = Object.Instantiate(l.gameObject, ds.nozzle, false); cl.name = "EdgeL";
            var cr = Object.Instantiate(rr.gameObject, ds.nozzle, false); cr.name = "EdgeR";
            ds.edgeLeft = cl.GetComponent<ParticleSystem>(); ds.edgeRight = cr.GetComponent<ParticleSystem>();
            UdonSharpEditorUtility.CopyProxyToUdon(ds); nd++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gun.gameObject.scene);
        return "[銃] 当たる範囲のふちのもや EdgeL/EdgeR・見本台 " + nd;
    }
}
