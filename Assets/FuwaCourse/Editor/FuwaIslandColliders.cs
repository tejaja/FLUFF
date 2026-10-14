using System.Linq;
using UnityEditor;
using UnityEngine;

// コースの外に浮いている飾りの浮島にも乗れるように、床の当たり（MeshCollider）をつける（2026-10-14 てじゃ「コース外の浮島にも乗れるように。乗ってる物は考えなくていい、島の床だけ」）。
// ・対象：上の円盤（IslandDisc_*）と、遠くの浮島（FarIsland_*）のうち、まだ当たりがない物。下の岩（IslandUnder*）や上の飾りはつけない
// ・当たりは地面のレイヤー（11）にする：ふわふわが触れたらコースの床と同じくアウト（地面のレイヤーでないと、コースの外の島に乗って止まってしまう）。
//   見た目の物が別のレイヤーなら、子に当たりだけの物（IslandFloorCollider）を作る
// ・何度実行してもよい（ついている物は飛ばす）。コースを作り直して当たりが消えたら、もう一度このメニュー
public static class FuwaIslandColliders
{
    const int Ground = 11;
    const string ChildName = "IslandFloorCollider";

    [MenuItem("FuwaCourse/コース外の浮島に床の当たりをつける")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        int added = 0, skipped = 0, moved = 0;
        foreach (var mf in Object.FindObjectsOfType<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null) continue;
            if (!m.name.StartsWith("IslandDisc_") && !m.name.StartsWith("FarIsland_")) continue;
            var own = mf.GetComponent<MeshCollider>();
            // 前の版で地面でないレイヤーの物に直接つけた当たりは、子へ移す
            if (own != null && mf.gameObject.layer != Ground && own.sharedMesh == m) { Object.DestroyImmediate(own); own = null; moved++; }
            if (mf.GetComponent<Collider>() != null || mf.transform.Find(ChildName) != null) { skipped++; continue; }
            if (mf.gameObject.layer == Ground)
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = m;
            }
            else
            {
                var c = new GameObject(ChildName); c.layer = Ground;
                c.transform.SetParent(mf.transform, false);
                GameObjectUtility.SetStaticEditorFlags(c, GameObjectUtility.GetStaticEditorFlags(mf.gameObject));
                var mc = c.AddComponent<MeshCollider>(); mc.sharedMesh = m;
            }
            EditorUtility.SetDirty(mf.gameObject);
            added++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "island colliders added " + added + " (moved to child " + moved + ", already had " + skipped + ")";
    }
}
