import bpy, bmesh, json
# Stage2Gate: Unity が書き出した型（stage2_gate_shapes.json）から、ひとつの岩 Stage2_GateRock を作って FBX に書き出す。
# 型の書き出し：Unity で FuwaCourse2_1Builder.GateExportPath にこの JSON のパスを入れて 2–1 を作り直す（ラストの滝の門の形を変えた時）
# 実行：Blender（FLUFF_models.blend）で exec(open(r"D:\UnityProject\FLUFF\Tools\stage2_gate.py", encoding="utf-8").read())
P = r"D:\UnityProject\FLUFF\Tools\stage2_gate_shapes.json"
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Models\Stage2_GateRock.fbx"
R2_VOXEL, DEC_ANGLE = 0.22, 0.09   # 0.03 だと 79k 頂点（スマホに重い）
sc = bpy.data.scenes.get("Stage2Gate") or bpy.data.scenes.new("Stage2Gate")
bpy.context.window.scene = sc

def loops(edges):
    es = set(edges); out = []
    while es:
        e = es.pop(); grp = [e]; stack = [e]
        while stack:
            x = stack.pop()
            for v in x.verts:
                for y in v.link_edges:
                    if y in es: es.remove(y); grp.append(y); stack.append(y)
        out.append(grp)
    return out

def load():
    for ob in json.load(open(P))["objects"]:
        n = ob["name"]; v = ob["v"]; t = ob["t"]
        verts = [(-v[i], -v[i+2], v[i+1]) for i in range(0, len(v), 3)]   # Unity (x,y,z) → Blender (-x,-z,y)
        nv = len(verts)
        bm = bmesh.new(); bv = [bm.verts.new(x) for x in verts]
        for i in range(0, len(t), 3):
            try: bm.faces.new([bv[t[i]], bv[t[i+1]], bv[t[i+2]]])
            except ValueError: pass
        if not n.startswith("Src_Gate"):   # 断面をつないだ型：ふたを作り直す（中心からの扇はへこんだ断面で重なる）
            bmesh.ops.delete(bm, geom=[bv[nv-2], bv[nv-1]], context='VERTS')
            for grp in loops([e for e in bm.edges if e.is_boundary]):
                bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=grp)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        o = sc.objects.get(n)
        if o is None:
            o = bpy.data.objects.new(n, bpy.data.meshes.new(n)); sc.collection.objects.link(o)
        bm.to_mesh(o.data); bm.free(); o.data.update(); o.hide_set(True)

def vol(o):
    bm = bmesh.new(); bm.from_mesh(o.data); v = bm.calc_volume(signed=True); bm.free(); return v

def apply(o, m): bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)

def build():
    for n in ("Gate_Work", "Stage2_GateRock"):
        o = sc.objects.get(n)
        if o: bpy.data.objects.remove(o, do_unlink=True)
    for o in sc.objects: o.select_set(False)
    cps = []
    for n in ("Src_GateRockL", "Src_GateRockR", "Src_Bridge"):
        s = sc.objects[n]; c = s.copy(); c.data = s.data.copy(); sc.collection.objects.link(c); c.hide_set(False); c.select_set(True); cps.append(c)
    bpy.context.view_layer.objects.active = cps[0]; bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = "Stage2_GateRock"; o.data.name = "Stage2_GateRock"
    m = o.modifiers.new("R", 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = 0.15; apply(o, m)
    m = o.modifiers.new("S", 'SMOOTH'); m.factor = 0.8; m.iterations = 12; apply(o, m)
    t1 = bpy.data.textures.get("GateNoise") or bpy.data.textures.new("GateNoise", 'CLOUDS'); t1.noise_scale = 1.6; t1.noise_depth = 1
    m = o.modifiers.new("D1", 'DISPLACE'); m.texture = t1; m.strength = 0.55; m.mid_level = 0.0; m.texture_coords = 'GLOBAL'; apply(o, m)   # 外へだけ
    t2 = bpy.data.textures.get("GateNoise2") or bpy.data.textures.new("GateNoise2", 'CLOUDS'); t2.noise_scale = 0.55; t2.noise_depth = 0
    m = o.modifiers.new("D2", 'DISPLACE'); m.texture = t2; m.strength = 0.25; m.mid_level = 0.0; m.texture_coords = 'GLOBAL'; apply(o, m)
    # 2026-10-28 てじゃ「岩もなんかカクカク」：前は 0.5 のボクセル＋平らな面をまとめる（ローポリのカクカク、スタートの岩山にそろえた）。
    # 細かくして、面のまとめは弱く（なめらかな物を COLLAPSE で減らすとトゲになるので DISSOLVE のまま角度だけ小さく）
    m = o.modifiers.new("R2", 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = R2_VOXEL; apply(o, m)
    m = o.modifiers.new("S2", 'SMOOTH'); m.factor = 0.6; m.iterations = 4; apply(o, m)
    m = o.modifiers.new("Dec", 'DECIMATE'); m.decimate_type = 'DISSOLVE'; m.angle_limit = DEC_ANGLE; apply(o, m)
    m = o.modifiers.new("Tri", 'TRIANGULATE'); apply(o, m)
    for n in ("Cut_Gap", "Cut_Walk", "Cut_BasinL", "Cut_Slab"):
        m = o.modifiers.new("B_" + n, 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.solver = 'EXACT'; m.object = sc.objects[n]; apply(o, m)
    # 2026-10-28 くぼみのくりぬきが1回目で効かないことがあった（右の池がまるごと埋まった）。くぼみのまん中を上から測って、底より上に岩があれば1回だけ くりぬき直す
    #（体積を見て何回もくりぬき直すと、こわれた面からトゲが何本も出た）
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    for n in ("Cut_BasinL",):
        c = sc.objects[n]; vs = [v.co for v in c.data.vertices]
        zlo = min(v.z for v in vs); cen = sum(vs, Vector()) / len(vs)
        tr = BVHTree.FromObject(o, bpy.context.evaluated_depsgraph_get())
        h = tr.ray_cast(Vector((cen.x, cen.y, zlo + 10)), Vector((0, 0, -1)), 30)
        filled = h[0] is not None and h[0].z > zlo + 0.1
        print("basin", n, "filled" if filled else "ok")
        if filled:
            m = o.modifiers.new("B2_" + n, 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.solver = 'EXACT'; m.object = c; apply(o, m)
    for p in o.data.polygons: p.use_smooth = False
    mat = bpy.data.materials.get("Stage2_GateRock") or bpy.data.materials.new("Stage2_GateRock")
    o.data.materials.clear(); o.data.materials.append(mat)
    for x in sc.objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE')
    bpy.ops.wm.save_mainfile()
    print("built", len(o.data.vertices), len(o.data.polygons))

load(); build()
