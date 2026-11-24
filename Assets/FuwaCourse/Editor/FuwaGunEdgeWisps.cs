using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 銃の「当たる範囲のふち」のすじ（2026-11-24 てじゃ「ショットが見た目より広いのが分かりづらい」「歪ませるのはやだ、横側にさりげないのを」）
//  銃口（Nozzle）の下に EdgeL / EdgeR（細いすじの粒）。向き・速さは撃つたびに FuwaBlower.PlayEdgeWisps が溜めに合わせて決める。何回押しても同じ
public static class FuwaGunEdgeWisps
{
    public static Color Col = new Color(1f, 1f, 1f, 0.4f);   // 2026-11-24 てじゃ「ちょと露骨」→ 0.8→0.4、本数 4→2、太さ 0.04→0.026
    public static float Size = 0.026f, Stretch = 5f;

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
            var m = ps.main; m.playOnAwake = false; m.loop = false; m.duration = 0.3f; m.startLifetime = 0.32f; m.startSpeed = 8f;
            m.startSize = new ParticleSystem.MinMaxCurve(Size * 0.7f, Size); m.startColor = Col; m.maxParticles = 24; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.gravityModifier = 0f;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 1.5f; sh.radius = 0.01f;
            // はじめは見えにくく、まん中で濃く、射程の所で消える
            var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient();
            gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(0.7f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.4f)));
            var r = g.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = Stretch; r.velocityScale = 0f;
            r.sharedMaterial = puffR.sharedMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }
        var l = Make("EdgeL"); var rr = Make("EdgeR");
        gun.edgeLeft = l; gun.edgeRight = rr;
        UdonSharpEditorUtility.CopyProxyToUdon(gun);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gun.gameObject.scene);
        return "[銃] 当たる範囲のふちのすじ EdgeL/EdgeR";
    }
}
