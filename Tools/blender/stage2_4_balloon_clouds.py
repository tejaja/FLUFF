# FLUFF 2-4 気球から生まれる雲（2026-11-10 てじゃ「この形だとおもったより変」→ A「近くで見る用の雲をBlenderで」）
# メタボールで丸いもこもこを3〜5個＋小さいのをくっつけて、底を平らにした「イラストっぽい雲」。形ちがい3つ（BalloonCloudA/B/C）
# 原点＝底のまん中。幅 3m くらい（Unity で 1.3〜2.2 倍に）。Blender の z が上（Unity の y）
import bpy, bmesh, math, random
from mathutils import Vector

SC_NAME = "FuwaBalloonClouds"
scn = bpy.data.scenes.get(SC_NAME) or bpy.data.scenes.new(SC_NAME)
bpy.context.window.scene = scn
for o in list(scn.collection.objects):
    bpy.data.objects.remove(o, do_unlink=True)

LAYOUTS = {
    # (x, y, z, 半径)。2026-11-10 てじゃ「もっと丸っこくて良い」→ 横に広げず、大きい玉どうしを近くに重ねた丸いかたまり
    "A": [(0, 0, 1.0, 1.0), (-0.6, 0.15, 0.75, 0.78), (0.62, -0.1, 0.78, 0.8), (0.05, 0.55, 0.8, 0.72), (0, -0.55, 0.78, 0.72), (0.2, 0.05, 1.55, 0.7), (-0.35, -0.1, 1.4, 0.6)],
    "B": [(0, 0, 0.95, 0.98), (-0.55, -0.1, 0.7, 0.72), (0.55, 0.15, 0.72, 0.74), (0.0, 0.5, 0.72, 0.68), (0.05, -0.5, 0.7, 0.66), (-0.1, 0.05, 1.5, 0.66)],
    "C": [(0, 0, 1.05, 1.05), (-0.68, 0.05, 0.75, 0.76), (0.7, 0, 0.75, 0.76), (0.3, -0.2, 1.65, 0.62), (-0.3, 0.2, 1.6, 0.62), (0, -0.6, 0.78, 0.7), (0, 0.6, 0.78, 0.7)],
}
FLAT_Z = -99.0   # 底を平らにする高さ（2026-11-10 てじゃ「雲の下平らじゃなくてもいい」→ つぶさない。前は 0.35）

made = []
for key, balls in LAYOUTS.items():
    mb = bpy.data.metaballs.new("CloudMB_" + key)
    mb.resolution = 0.16; mb.render_resolution = 0.16;   # 2026-11-10 てじゃ「ポリ数おおくなってたりする？」→ 0.09（2〜2.4千頂点）から荒く
    mb.threshold = 0.6
    for (x, y, z, r) in balls:
        e = mb.elements.new(); e.co = (x, y, z); e.radius = r * 1.32; e.stiffness = 2.0   # 「もうちょいまるよりで」→ 玉をすこし大きく（1.22 から）
    mo = bpy.data.objects.new("CloudMBObj_" + key, mb); scn.collection.objects.link(mo)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(mo.evaluated_get(dg))
    bpy.data.objects.remove(mo, do_unlink=True); bpy.data.metaballs.remove(mb)
    bm = bmesh.new(); bm.from_mesh(me)
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        if v.co.z < FLAT_Z:
            t = (FLAT_Z - v.co.z) / max(1e-4, FLAT_Z - zmin)
            v.co.z = FLAT_Z - (FLAT_Z - zmin) * 0.12 * t   # 底はほぼ平ら（少しだけ丸み）
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= zmin
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.004)
    bm.to_mesh(me); bm.free()
    me.name = "BalloonCloud" + key
    m = bpy.data.materials.get("BalloonCloud") or bpy.data.materials.new("BalloonCloud")
    me.materials.clear(); me.materials.append(m)
    ob = bpy.data.objects.new("BalloonCloud" + key, me); scn.collection.objects.link(ob)
    for p in me.polygons: p.use_smooth = True
    ob.location = (0, 0, 0)
    made.append(ob)
    print(key, len(me.vertices), [round(x, 2) for x in ob.dimensions])
