using System.Linq;
using UnityEditor;
using UnityEngine;

// 2-1 の魚の水しぶき（2026-11-22 てじゃ「2-1の魚の水しぶきがささやかすぎ」）。
// 水の効果（WaterFx）はプレイヤーが落ちた時の大きさなので、魚（2倍の大きさで 2.4〜3m 跳ぶ）には小さすぎた。
// → WaterFx の写し「FishWaterFx」を作って、しずくを大きく・速く・多く、波紋も大きくする。川の魚・見張りの魚はこれを使う。
// 2-1 のビルダーからも呼ぶ（作り直しても同じになる）。
public static class FuwaFishSplash
{
    public const string Name = "FishWaterFx";
    public static float DropSize0 = 0.08f, DropSize1 = 0.17f, DropSpeed0 = 2.6f, DropSpeed1 = 4.6f, DropRadius = 0.35f, DropAngle = 34f, DropLife = 1.0f;
    public static float RippleSize = 1.9f, RippleBigSize = 3.2f; public static int MaxDrops = 40;

    public static FuwaWaterFx Make(Transform root, FuwaWaterFx src)
    {
        var old = root.Find(Name); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var go = Object.Instantiate(src.gameObject, src.transform.parent); go.name = Name;
        go.transform.localPosition = src.transform.localPosition; go.transform.localRotation = src.transform.localRotation; go.transform.localScale = src.transform.localScale;
        Undo.RegisterCreatedObjectUndo(go, "fish fx");
        var fx = go.GetComponent<FuwaWaterFx>();
        var d = go.transform.Find("Drops").GetComponent<ParticleSystem>(); var m = d.main;
        m.startSize = new ParticleSystem.MinMaxCurve(DropSize0, DropSize1); m.startSpeed = new ParticleSystem.MinMaxCurve(DropSpeed0, DropSpeed1);
        m.startLifetime = DropLife; m.maxParticles = 160;
        var sh = d.shape; sh.radius = DropRadius; sh.angle = DropAngle;
        var rp = go.transform.Find("Ripple").GetComponent<ParticleSystem>().main; rp.startSize = RippleSize;
        var rb = go.transform.Find("RippleBig").GetComponent<ParticleSystem>().main; rb.startSize = RippleBigSize;
        fx.drops = d; fx.ripple = go.transform.Find("Ripple").GetComponent<ParticleSystem>(); fx.rippleBig = go.transform.Find("RippleBig").GetComponent<ParticleSystem>();
        fx.sound = go.GetComponent<AudioSource>(); fx.maxDrops = MaxDrops;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fx);
        return fx;
    }

    [MenuItem("FuwaCourse/ステージ2/2-1 魚の水しぶきを大きく")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var root = GameObject.Find("FuwaCourse2_1").transform;
        var src = root.Find("WaterFx").GetComponent<FuwaWaterFx>();
        var fx = Make(root, src);
        int n = 0;
        foreach (var f in root.GetComponentsInChildren<FuwaRiverFish>(true)) { Undo.RecordObject(f, "fish fx"); f.fx = fx; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(f); n++; }
        foreach (var f in root.GetComponentsInChildren<FuwaGuardFish>(true)) { Undo.RecordObject(f, "fish fx"); f.fx = fx; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(f); n++; }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "[魚の水しぶき] " + n + " 匹を " + Name + " に";
    }
}
