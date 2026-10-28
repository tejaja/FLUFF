import bpy, bmesh, json, os
# 3連滝の柱（2026-10-28 てじゃ「全体的にあかん」→ Blender で作る）。
# Unity の 2–1 を作り直すと Tools/stage2_pillar{0,1,2}.json に型が出る（外の輪・池のふち・切り欠き・落ち口の下の面。柱の Set{i} のローカル座標 x,z）。
# ここで：外の輪を上下にのばした柱 → ボクセルで角を丸めてなめらかに → 池（池のふちの形で bowl の深さ）・切り欠き（ふちを lipRim まで）・落ち口の下の面 をブーリアンでくりぬく。
# 実行：Blender（FLUFF_models.blend）で exec(open(r"D:\UnityProject\FLUFF\Tools\stage2_pillars.py", encoding="utf-8").read())
DIR = r"D:\UnityProject\FLUFF\Tools"
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Models\Stage2_FallsPillars.fbx"
VOXEL, SMOOTH_IT, QF_FACES = 0.08, 6, 1800
sc = bpy.data.scenes.get("Stage2Pillars") or bpy.data.scenes.new("Stage2Pillars")
bpy.context.window.scene = sc

def B(x, y, z): return (-x, -z, y)   # Unity (x,y,z) → Blender (-x,-z,y)

def untangle(P):
    # 輪が自分と交差している所（なめらかにした池のふちの角で小さな結び目ができた）は、小さい方の輪を切り取る。交差した型だとブーリアンが失敗して柱が消えた
    def cr(o, p, q): return (p[0]-o[0])*(q[1]-o[1]) - (p[1]-o[1])*(q[0]-o[0])
    def hit(a, b, c, e): return cr(c, e, a) * cr(c, e, b) < 0 and cr(a, b, c) * cr(a, b, e) < 0
    P = list(P)
    for _ in range(50):
        n = len(P); found = None
        for k in range(n):
            for j in range(k + 2, n):
                if k == 0 and j == n - 1: continue
                if hit(P[k], P[(k+1) % n], P[j], P[(j+1) % n]): found = (k, j); break
            if found: break
        if not found: return P
        k, j = found
        if j - k <= n // 2: P = P[:k+1] + P[j+1:]
        else: P = P[k+1:j+1]
    return P

def prism(name, pts, y0, y1):
    pts = untangle(pts)
    bm = bmesh.new()
    lo = [bm.verts.new(B(p[0], y0, p[1])) for p in pts]
    hi = [bm.verts.new(B(p[0], y1, p[1])) for p in pts]
    n = len(pts)
    for k in range(n):
        k1 = (k + 1) % n
        bm.faces.new([lo[k], lo[k1], hi[k1], hi[k]])
    bm.faces.new(lo); bm.faces.new(hi[::-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    return o

def apply(o, m):
    bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)

def build(i):
    d = json.load(open(os.path.join(DIR, "stage2_pillar%d.json" % i)))
    name = "Stage2_FallsPillar%d" % i
    for n in (name, name + "_Pool", name + "_Notch", name + "_Face"):
        o = sc.objects.get(n)
        if o: bpy.data.objects.remove(o, do_unlink=True)
    yTop, yBot, lipRim, bowl, surf = d["yTop"], d["yBot"], d["lipRim"], d["bowl"], d["surf"]
    o = prism(name, d["outer"], yBot, yTop)
    # 角を丸めて岩らしく（上の面は平らなまま：ボクセル→軽くならす→上を平らに切り戻す）
    m = o.modifiers.new("R", 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = VOXEL; apply(o, m)
    m = o.modifiers.new("S", 'SMOOTH'); m.factor = 0.6; m.iterations = SMOOTH_IT; apply(o, m)
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.quadriflow_remesh(target_faces=QF_FACES, use_preserve_sharp=False, use_preserve_boundary=False, preserve_attributes=False, smooth_normals=False, seed=3)
    o.select_set(False)
    # 上の面を yTop で平らに（ならした角の丸みは残す）
    bm = bmesh.new(); bm.from_mesh(o.data)
    for v in bm.verts:
        if v.co.z > yTop - 0.06: v.co.z = yTop
    bm.to_mesh(o.data); bm.free()
    cuts = [
        prism(name + "_Pool", d["inner"], yTop - bowl, yTop + 1.0),
        prism(name + "_Notch", d["notch"], lipRim, yTop + 1.0),
        prism(name + "_Face", d["face"], surf - 1.5, lipRim + 0.001),
    ]
    for c in cuts:
        m = o.modifiers.new("B_" + c.name, 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.solver = 'EXACT'; m.object = c; apply(o, m)
        c.hide_set(True)
    m = o.modifiers.new("Tri", 'TRIANGULATE'); apply(o, m)
    for p in o.data.polygons: p.use_smooth = False
    mat = bpy.data.materials.get("Stage2_PillarRock") or bpy.data.materials.new("Stage2_PillarRock")
    o.data.materials.clear(); o.data.materials.append(mat)
    o.data.name = name
    return o

objs = [build(i) for i in range(3)]
for x in sc.objects: x.select_set(False)
for o in objs: o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE')
bpy.ops.wm.save_mainfile()
print("built", [len(o.data.polygons) for o in objs])
