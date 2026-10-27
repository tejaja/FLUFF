import bpy, bmesh
from mathutils.bvhtree import BVHTree
# Stage2Cave：脇道（ツタのすき間）の切り抜き Cut_Side が岩山のてっぺんまで抜けて、池の水がもれて上が溝になっていた（2026-10-27 てじゃ）。
# 切り抜きの天井より上（CEIL〜）を元の岩（CaveRock − ほかの切り抜き）で埋めるふた Plug を作り、今の岩（Stage2_CaveRock_bak1027）にくっつけて書き出す。
# ブーリアンで足すと同じ面どうしが重なって穴があくので、くっつけるだけ（join）。つなぎ目の線を消すため：
# ふたは左右に少し太らせて（GROW）上の面を前の岩の上の面に少しかぶせ、ふたの横の面（切り抜きの壁の上）と、前の岩の切り抜きの壁の天井より上は消す
# 実行：Blender（FLUFF_models.blend）で exec(open(r"D:\UnityProject\FLUFF\Tools\stage2_cave_sideplug.py", encoding="utf-8").read())
CEIL = 4.55          # 脇道の天井（Unity の StartH + SideGapH = 4.6 のツタの上のはしが 5cm 岩にうまる）
GROW = 0.012
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Models\Stage2_CaveRock.fbx"
OTHER_CUTS = ['Cut_Inside', 'Cut_Mouth', 'Cut_Arch', 'Cut_Pool', 'Cut_FallL', 'Cut_FallR', 'Cut_FallL_Soft', 'Cut_FallR_Soft', 'Cut_PoolExit_Soft',
              'Cut_ExitTop_L', 'Cut_ExitTop_R', 'Cut_ExitFront_L', 'Cut_ExitFront_R', 'Cut_UnderLip_L']   # Cut_UnderLip_R は裏返っていて足し算になる（ふたの高さより下なので不要）
sc = bpy.data.scenes['Stage2Cave']; bpy.context.window.scene = sc

def apply(o, m):
    with bpy.context.temp_override(object=o, active_object=o): bpy.ops.object.modifier_apply(modifier=m.name)

for n in ['Plug_Side', 'Plug_Cutter', 'HalfBox']:
    x = bpy.data.objects.get(n)
    if x: bpy.data.objects.remove(x, do_unlink=True)
# 脇道の切り抜きを左右に少し太らせて、天井より上だけにする（断面は6点：0〜2 が左、3〜5 が右）
cs = bpy.data.objects['Cut_Side']
pc = cs.copy(); pc.data = cs.data.copy(); pc.name = 'Plug_Cutter'; sc.collection.objects.link(pc)
for i, v in enumerate(pc.data.vertices): v.co.x += -GROW if i % 6 < 3 else GROW
bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
for v in bm.verts: v.co.x *= 20; v.co.y = v.co.y * 30 - 3; v.co.z = (CEIL + 10) / 2 + v.co.z * (10 - CEIL)
hb = bpy.data.meshes.new('HalfBox'); bm.to_mesh(hb); bm.free()
half = bpy.data.objects.new('HalfBox', hb); sc.collection.objects.link(half)
m = pc.modifiers.new('i', 'BOOLEAN'); m.operation = 'INTERSECT'; m.solver = 'EXACT'; m.object = half; apply(pc, m)
bpy.data.objects.remove(half, do_unlink=True)
# ふた＝元の岩 ∩ 上の切り抜き − ほかの切り抜き
base = bpy.data.objects['CaveRock']
pl = base.copy(); pl.data = base.data.copy(); pl.name = 'Plug_Side'; sc.collection.objects.link(pl); pl.hide_set(False)
m = pl.modifiers.new('i', 'BOOLEAN'); m.operation = 'INTERSECT'; m.solver = 'EXACT'; m.object = pc; apply(pl, m)
for n in OTHER_CUTS:
    m = pl.modifiers.new('d', 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.solver = 'EXACT'; m.object = bpy.data.objects[n]; apply(pl, m)
pc.hide_set(True)
bm = bmesh.new(); bm.from_mesh(pl.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < CEIL - 0.05 or v.co.z > 6.5], context='VERTS')
# ふたの横の面（太らせた切り抜きの壁の上）を消す。天井（CEIL の下向きの面）は残す
cb = bmesh.new(); cb.from_mesh(pc.data); ctree = BVHTree.FromBMesh(cb); cb.free()
side = [f for f in bm.faces if ctree.find_nearest(f.calc_center_median())[3] < 1e-3 and not (abs(f.calc_center_median().z - CEIL) < 1e-3 and f.normal.z < -0.9)]
bmesh.ops.delete(bm, geom=side, context='FACES')
bm.to_mesh(pl.data); bm.free(); pl.data.update()
# 材質：前の岩の同じ面の上なら同じ材質（芝/岩）、それ以外は上向きで高い所＝芝
bak = bpy.data.objects['Stage2_CaveRock_bak1027']
bb = bmesh.new(); bb.from_mesh(bak.data); bb.faces.ensure_lookup_table(); tree = BVHTree.FromBMesh(bb)
o = bpy.data.objects['Stage2_CaveRock']
o.data = bak.data.copy(); o.data.name = 'Stage2_CaveRock'
# 前の岩の切り抜きの壁（Cut_Side の面の上）を CEIL で切って、上の部分を消す（ふたの中にうまる）
sb = bmesh.new(); sb.from_mesh(cs.data); stree = BVHTree.FromBMesh(sb); sb.free()
bm = bmesh.new(); bm.from_mesh(o.data)
walls = [f for f in bm.faces if stree.find_nearest(f.calc_center_median())[3] < 1e-3]
geom = list({e for f in walls for e in f.edges}) + walls + list({v for f in walls for v in f.verts})
bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, CEIL), plane_no=(0, 0, 1))
up = [f for f in bm.faces if f.calc_center_median().z > CEIL + 1e-3 and stree.find_nearest(f.calc_center_median())[3] < 1e-3]
bmesh.ops.delete(bm, geom=up, context='FACES')
print('slot walls removed', len(up))
bm.to_mesh(o.data); bm.free(); o.data.update()
pl.data.materials.clear(); pl.data.materials.append(o.data.materials[0]); pl.data.materials.append(o.data.materials[1])
for p in pl.data.polygons:
    loc, nrm, idx, d = tree.find_nearest(p.center)
    if d is not None and d < 0.05 and nrm.dot(p.normal) > 0.95: p.material_index = bb.faces[idx].material_index
    else: p.material_index = 1 if (p.normal.z > 0.5 and p.center.z > 6.0) else 0
bb.free()
for x in sc.objects: x.select_set(False)
pl.select_set(True); o.hide_set(False); o.select_set(True); bpy.context.view_layer.objects.active = o
bpy.ops.object.join()
# 面を材質の順に（FBX のサブメッシュの順番がずれないように）
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.sort_elements(type='MATERIAL', elements={'FACE'})
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.ed.undo_push(message='cave side plug')
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE')
bpy.ops.wm.save_mainfile()
print('built', len(o.data.vertices))
