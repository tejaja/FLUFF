using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// ステージ2の「かってにスクショ」（2026-11-16 てじゃ「ステージ2のかってにスクショのポイントきめるかー」「とりあえず一旦これでやってみよ」）
//  ・アルバム（FuwaPhotoAlbum）にステージ2の5コースを足し、ステージ2のロビーのボード（FuwaStage2/Lobby/PhotoGallery）を2つめのボードとしてつなぐ
//  ・各コース3か所のフォトポイント。チェックポイントのゲートに重ねるか、しかけのあたりを大きめの箱で囲む
//  このメニューはステージ2の PhotoPoints を作り直す（ステージ1のは触らない）
public static class FuwaPhotoPointBuilder2
{
    struct Spot { public string course, ja, en; public string cp; public string cpA, cpB; public Vector3 at; public Vector3 size; }

    static Spot CP(string c, string cp, string ja, string en) => new Spot { course = c, cp = cp, ja = ja, en = en };
    static Spot Mid(string c, string a, string b, string ja, string en, Vector3 size) => new Spot { course = c, cpA = a, cpB = b, ja = ja, en = en, size = size };
    static Spot At(string c, Vector3 at, string ja, string en, Vector3 size) => new Spot { course = c, at = at, ja = ja, en = en, size = size };

    static readonly string[] CourseJa = { "2–1 みずのこみち", "2–2 ほのおのやま", "2–3 こおりのみち", "2–4 はぐるまのしま", "2–5 そらのいせき" };
    static readonly string[] CourseEn = { "2–1 Water Path", "2–2 Flame Mountain", "2–3 Ice Road", "2–4 Gear Isles", "2–5 Sky Ruins" };

    [MenuItem("FuwaCourse/ステージ2/かってにスクショのポイントを置き直す（ステージ2）")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var album = Object.FindObjectsOfType<FuwaPhotoAlbum>(true).First();
        var log = new List<string>();
        WireAlbum(album, log);

        var spots = new List<Spot>
        {
            CP("FuwaCourse2_1", "CheckpointRiver", "川の魚", "River Fish"),
            Mid("FuwaCourse2_1", "Checkpoint1", "Checkpoint2", "泡の貝の道", "Bubble Clams", new Vector3(16f, 10f, 4f)),
            CP("FuwaCourse2_1", "Checkpoint3", "滝の道", "Waterfall Road"),

            Mid("FuwaCourse2_2", "Checkpoint1", "Checkpoint2", "溶岩の穴", "Lava Holes", new Vector3(12f, 10f, 4f)),
            CP("FuwaCourse2_2", "CheckpointZig", "火のジグザグ", "Fire Zigzag"),
            CP("FuwaCourse2_2", "Checkpoint4", "ゴール前", "Final Stretch"),

            CP("FuwaCourse2_3", "CheckpointHill", "氷の坂", "Ice Slope"),
            Mid("FuwaCourse2_3", "Checkpoint1", "Checkpoint2", "まるいドーム", "Round Dome", new Vector3(20f, 10f, 4f)),
            CP("FuwaCourse2_3", "CheckpointSpinExit", "まわる床", "Spinning Floor"),

            CP("FuwaCourse2_4", "Checkpoint1", "歯車にのって", "Gear Ride"),
            CP("FuwaCourse2_4", "CheckpointRack", "ラックの橋", "Rack Bridge"),
            CP("FuwaCourse2_4", "CheckpointTwin", "ふたつの台", "Twin Tables"),

            CP("FuwaCourse2_5", "Checkpoint1", "クモの巣の道", "Web Walk"),
            At("FuwaCourse2_5", Vector3.zero, "衝撃波の広場", "Crystal Plaza", new Vector3(22f, 10f, 22f)),
            At("FuwaCourse2_5", Vector3.zero, "さいごのポータル", "Last Portal", new Vector3(9f, 9f, 5f)),
        };

        foreach (var cname in spots.Select(s => s.course).Distinct())
        {
            var course = GameObject.Find(cname).transform;
            bool wasActive = course.gameObject.activeSelf; course.gameObject.SetActive(true); Physics.SyncTransforms();
            try
            {
                var holder = course.Find("PhotoPoints"); if (holder) Object.DestroyImmediate(holder.gameObject);
                holder = new GameObject("PhotoPoints").transform; holder.SetParent(course, false);
                var cps = course.GetComponentsInChildren<FuwaCheckpoint>(true).ToDictionary(c => c.transform.parent.name, c => c.GetComponent<BoxCollider>());
                var start = course.Find("Start/BallStart").position;
                var goal = course.GetComponentsInChildren<FuwaGoal>(true).First().transform.position;
                int layer = cps.Values.First().gameObject.layer;
                int order = 0; var made = new List<string>();
                foreach (var s in spots.Where(x => x.course == cname))
                {
                    Vector3 center, dir, size; Quaternion rot;
                    if (s.cp != null)
                    {
                        var b = cps[s.cp]; center = b.bounds.center;
                        var bt = b.transform; var sz = Vector3.Scale(b.size, bt.lossyScale);
                        // ゲートの薄い方が通る向き。スタートから遠ざかる方を前に
                        dir = Mathf.Abs(sz.x) < Mathf.Abs(sz.z) ? bt.right : bt.forward; dir.y = 0f; dir.Normalize();
                        if (Vector3.Dot(dir, goal - center) < 0f && Vector3.Dot(dir, center - start) < 0f) dir = -dir;
                        size = new Vector3(Mathf.Max(Mathf.Abs(sz.x), Mathf.Abs(sz.z)) + 2f, 9f, Mathf.Min(Mathf.Abs(sz.x), Mathf.Abs(sz.z)) + 2f);
                    }
                    else if (s.cpA != null)
                    {
                        Vector3 a = cps[s.cpA].bounds.center, c = cps[s.cpB].bounds.center;
                        center = (a + c) * 0.5f; dir = c - a; dir.y = 0f; dir.Normalize(); size = s.size;
                        // 地面から少し上（下に道があれば、そこから）
                        if (Physics.Raycast(center + Vector3.up * 6f, Vector3.down, out var hit, 20f, (1 << 0) | (1 << 11), QueryTriggerInteraction.Ignore)) center.y = hit.point.y + 3f;
                    }
                    else if (s.en == "Crystal Plaza")
                    {
                        var sw = course.GetComponentsInChildren<FuwaShockwave>(true).First().transform.position;
                        center = sw + Vector3.up * 3f; dir = (sw - cps["Checkpoint2"].bounds.center); dir.y = 0f; dir.Normalize(); size = s.size;
                    }
                    else
                    {
                        // ゴールは洞窟の中（ポータルWarpBでワープして入る）なので、その入口のポータルの手前で
                        var wb = course.GetComponentsInChildren<Transform>(true).First(t => t.name == "WarpB").position;
                        var sw = course.GetComponentsInChildren<FuwaShockwave>(true).First().transform.position;
                        dir = wb - sw; dir.y = 0f; dir.Normalize(); center = wb - dir * 4f + Vector3.up * 2f; size = s.size;
                    }
                    string r = FuwaPhotoPointBuilder.Make(holder, "PhotoPoint" + (order + 1), order, center, dir, size.x, layer, album, s.ja, s.en);
                    var col = holder.Find("PhotoPoint" + (order + 1)).GetComponent<BoxCollider>(); col.size = size;
                    // てじゃ「氷の柱はいやだな」：氷の柱が多い所は、柱で画面がふさがる位置をよける
                    if (s.en == "Round Dome") { var pp = col.GetComponent<FuwaPhotoPoint>(); pp.clearRadius = 0.9f; UdonSharpEditorUtility.CopyProxyToUdon(pp); EditorUtility.SetDirty(pp); }
                    made.Add(r);
                    order++;
                }
                log.Add(cname + ": " + string.Join(", ", made));
            }
            finally { course.gameObject.SetActive(wasActive); }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(album.gameObject.scene);
        return string.Join("\n", log);
    }

    static void WireAlbum(FuwaPhotoAlbum album, List<string> log)
    {
        // コース（スタート）と名前：ステージ1の6つのあとにステージ2の5つ
        var starts = album.courseStarts.Take(6).ToList(); var ja = album.courseNamesJa.Take(6).ToList(); var en = album.courseNamesEn.Take(6).ToList();
        for (int i = 0; i < 5; i++)
        {
            starts.Add(GameObject.Find("FuwaCourse2_" + (i + 1)).transform.Find("Start/BallStart"));
            ja.Add(CourseJa[i]); en.Add(CourseEn[i]);
        }
        album.courseStarts = starts.ToArray(); album.courseNamesJa = ja.ToArray(); album.courseNamesEn = en.ToArray();

        var g2 = GameObject.Find("FuwaStage2/Lobby/PhotoGallery").transform;
        int n = album.frames.Length;
        album.frames2 = new Renderer[n]; album.emptyMarks2 = new GameObject[n];
        for (int i = 0; i < n; i++)
        {
            var ph = g2.Find("Photo" + i); if (ph == null) continue;
            album.frames2[i] = ph.Find("Image").GetComponent<Renderer>();
            album.emptyMarks2[i] = ph.Find("Empty").gameObject;
            // ステージ1のボードの写真と同じ材質（Udon の .material で個別のコピーになる）
            if (album.frames[i] != null) album.frames2[i].sharedMaterial = album.frames[i].sharedMaterial;
        }
        album.titleText2 = g2.Find("Title").GetComponent<TMPro.TextMeshPro>();
        UdonSharpEditorUtility.CopyProxyToUdon(album);
        EditorUtility.SetDirty(album);
        log.Add("album: courses " + album.courseStarts.Length + ", board2 " + album.frames2.Count(f => f != null));
    }
}
