import bpy, bmesh, math
import numpy as np
# 2–2 の火山から飛んでくる岩（2026-10-31 てじゃ「いいね」）。球の方向ごとに、ランダムな平面で切った半径を取って、角ばった岩にする（なめらかなノイズより岩らしい）。
# Unity の Sphere と同じ大きさ（半径およそ 0.5）で、Visual の大きさの設定はそのまま使う。色と割れ目は FuwaCourse/LavaRock。
# 実行：Blender（FLUFF_models.blend）で exec(open(r"D:\UnityProject\FLUFF\Tools\stage2_lavarock.py", encoding="utf-8").read())
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Models\Stage2_LavaRock.fbx"
PLANES, SUBDIV = 34, 4

def build(i, sc):
    name = "Stage2_LavaRock%d" % i
    o = sc.objects.get(name)
    if o: bpy.data.objects.remove(o, do_unlink=True)
    r = np.random.RandomState(70 + i)
    ns = r.normal(size=(PLANES, 3)); ns /= np.linalg.norm(ns, axis=1, keepdims=True)
    ds = r.uniform(0.40, 0.52, PLANES)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=SUBDIV, radius=1.0)
    for v in bm.verts:
        d = np.array(v.co); d /= np.linalg.norm(d)
        c = ns @ d
        t = np.where(c > 1e-3, ds / np.maximum(c, 1e-3), 9.0).min()
        v.co = tuple(d * min(t, 0.56))
    for f in bm.faces: f.smooth = False
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    vol = bm.calc_volume(signed=True); nm = sum(1 for e in bm.edges if not e.is_manifold)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    mat = bpy.data.materials.get("Stage2_LavaRock") or bpy.data.materials.new("Stage2_LavaRock")
    me.materials.clear(); me.materials.append(mat)
    return o, len(me.polygons), round(vol, 3), nm

sc = bpy.data.scenes.get("Stage2LavaRock") or bpy.data.scenes.new("Stage2LavaRock")
bpy.context.window.scene = sc
res = [build(i, sc) for i in range(2)]
for x in sc.objects: x.select_set(False)
for r in res: r[0].select_set(True)
bpy.context.view_layer.objects.active = res[0][0]
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
bpy.ops.ed.undo_push(message="stage2 lava rock")
bpy.ops.wm.save_mainfile()
print([r[1:] for r in res])
