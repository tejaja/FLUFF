import bpy, bmesh, math
import numpy as np
# 2–2 ⑤の炎の岩（2026-10-31 てじゃ「おけ～」、前はただの箱）。Unity 側は今の箱のまま見た目のメッシュだけ入れ替えるので、形は 1 辺 1 の箱の座標（中心 0、前 +z、上 +y）で書き出す。
# 穴は炎の高さ（道から 0.55 / 1.75 / 2.95m）。箱の大きさと高さは FuwaCourse2_2Builder.VolcanoJet と同じ。
# 実行：Blender（FLUFF_models.blend）で exec(open(r"D:\UnityProject\FLUFF\Tools\stage2_jetrock.py", encoding="utf-8").read())
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Models\Stage2_JetRock.fbx"
W, H, D = 2.2, 6.45, 2.5       # 箱の大きさ（m）
CENTER_Y = 1.125               # 箱の中心の高さ（道から）
ROWS = [0.55, 1.75, 2.95]      # 炎の高さ（道から）
HOLE_R, HOLE_DEPTH = 0.5, 0.5
LON, LAT = 44, 36

def B(x, y, z): return (-x, -z, y)   # Unity (x,y,z) → Blender (-x,-z,y)

def noise3(p, seed):
    # なめらかなサインの重ね（小さい岩なのでこれで足りる。座標で取るのでつなぎ目なし）
    r = np.random.RandomState(seed); s = np.zeros(p.shape[0]); amp = 1.0; tot = 0.0
    for k in range(4):
        f = 0.8 * 2.0 ** k
        for j in range(5):
            d = r.normal(size=3); d /= np.linalg.norm(d)
            s += amp * np.sin((p @ d) * f + r.rand() * 6.283); tot += amp
        amp *= 0.55
    return s / tot * 1.8

def rock_points(seed):
    lon = np.arange(LON) / LON * 2 * np.pi
    lat = (np.arange(LAT) + 1) / (LAT + 1) * np.pi - np.pi / 2
    LO, LA = np.meshgrid(lon, lat)
    def sp(v, e): return np.sign(v) * np.abs(v) ** e
    e = 0.42   # 角の丸い箱
    x = sp(np.cos(LA), e) * sp(np.sin(LO), e); y = sp(np.sin(LA), e); z = sp(np.cos(LA), e) * sp(np.cos(LO), e)
    def place(x, y, z):
        yn = (y + 1) * 0.5
        wid = 1.2 + (0.72 - 1.2) * yn ** 1.2   # 上ほど細く
        return np.stack([x * W * 0.5 * wid, y * H * 0.5, z * D * 0.5], -1)
    P = place(x, y, z)
    q = P.reshape(-1, 3)
    n = noise3(q * np.array([1.2, 0.4, 1.2]), seed).reshape(x.shape)       # 縦に長いかたまり
    n = (0.45 - np.abs(n)) * 2.0                                           # 谷と尾根をはっきり（なめらかだとカプセルに見えた）
    n2 = noise3(q * 2.4, seed + 1).reshape(x.shape)
    n3 = noise3(np.stack([x.ravel() * 2.2, np.zeros(x.size), z.ravel() * 2.2], -1), seed + 2).reshape(x.shape)
    # 外向きへ押す（前の面はおだやかに：穴を開ける面）
    rad = np.stack([x, y * 0.35, z], -1); rad /= np.maximum(np.linalg.norm(rad, axis=-1, keepdims=True), 1e-4)
    front = np.clip((z - 0.35) / 0.5, 0, 1)
    amt = (0.42 * n + 0.17 * n2) * (1 - 0.55 * front)
    P = P + rad * amt[..., None]
    # てっぺんをでこぼこに、片側へ傾ける
    up = np.clip((y - 0.45) / 0.45, 0, 1)
    P[..., 1] += up * (0.65 * n3 + 0.3 * np.sin(LO + seed) - 0.25)
    P[..., 0] += up * up * 0.22 * math.sin(seed)
    # 前は穴の高さで箱の前の面より出ない（炎の口が奥に沈まない。少し手前に引いて口のまわりを平らぎみに）
    fz = D * 0.5
    P[..., 2] = np.minimum(P[..., 2], fz + 0.02 * n2)
    return P

def build(i, sc):
    name = "Stage2_JetRock%d" % i
    for n in (name, name + "_Cut"):
        o = sc.objects.get(n)
        if o: bpy.data.objects.remove(o, do_unlink=True)
    P = rock_points(11 + i * 7)
    bm = bmesh.new()
    vs = [[bm.verts.new(B(*P[j, k])) for k in range(LON)] for j in range(LAT)]
    bot = bm.verts.new(B(float(P[0, :, 0].mean()), float(P[0, :, 1].mean()) - 0.08, float(P[0, :, 2].mean())))
    top = bm.verts.new(B(float(P[-1, :, 0].mean()), float(P[-1, :, 1].mean()) + 0.08, float(P[-1, :, 2].mean())))
    for k in range(LON):
        k1 = (k + 1) % LON
        bm.faces.new([bot, vs[0][k], vs[0][k1]])
        for j in range(LAT - 1): bm.faces.new([vs[j][k], vs[j + 1][k], vs[j + 1][k1], vs[j][k1]])
        bm.faces.new([top, vs[LAT - 1][k1], vs[LAT - 1][k]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    # 穴：炎の高さに、前から円柱でくりぬく
    cut_bm = bmesh.new()
    for ry in ROWS:
        y = ry - CENTER_Y
        ret = bmesh.ops.create_cone(cut_bm, cap_ends=True, segments=20, radius1=HOLE_R, radius2=HOLE_R, depth=3.0)
        # 円柱の軸（Blender Z）を Unity の z（Blender -Y）へ。奥の端が前の面から HOLE_DEPTH
        for v in ret["verts"]:
            bx, by, bz = v.co.x, v.co.y, v.co.z
            uz = D * 0.5 - HOLE_DEPTH + (bz + 1.5)
            v.co = (bx, -uz, y + by)
    bmesh.ops.recalc_face_normals(cut_bm, faces=cut_bm.faces)
    cm = bpy.data.meshes.new(name + "_Cut"); cut_bm.to_mesh(cm); cut_bm.free()
    cut = bpy.data.objects.new(name + "_Cut", cm); sc.collection.objects.link(cut)
    m = o.modifiers.new("Holes", 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.solver = 'EXACT'; m.object = cut
    bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(cut, do_unlink=True); bpy.data.meshes.remove(cm)
    # 材質：穴の中は別の材質。岩は小さい番号が先になるよう面を並べ替え（FBX のサブメッシュの順番がそろう）
    me.materials.clear()
    for mn in ("Stage2_JetRock", "Stage2_JetHole"):
        me.materials.append(bpy.data.materials.get(mn) or bpy.data.materials.new(mn))
    bm = bmesh.new(); bm.from_mesh(me)
    hole = 0
    for f in bm.faces:
        # 穴の面：3 頂点とも円柱の壁か底の上（面の中心だけで見ると前の面の大きい三角形まで穴の色になった）
        inside = False
        for ry in ROWS:
            ok = True
            for v in f.verts:
                ux, uz, uy = -v.co.x, -v.co.y, v.co.z
                d2 = ux * ux + (uy - (ry - CENTER_Y)) ** 2
                wall = abs(math.sqrt(d2) - HOLE_R) < 0.012
                floor = abs(uz - (D * 0.5 - HOLE_DEPTH)) < 0.012 and d2 < (HOLE_R + 0.012) ** 2
                if not (wall or floor): ok = False; break
            if ok: inside = True; break
        f.material_index = 1 if inside else 0
        f.smooth = not inside
        hole += 1 if inside else 0
    res = bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.faces.ensure_lookup_table(); bm.faces.sort(key=lambda f: f.material_index); bm.faces.index_update()
    # 1 辺 1 の箱の座標へ（Unity の箱の大きさで元の大きさに戻る。法線はこの形のものを書き出す）
    for v in bm.verts: v.co = (v.co.x / W, v.co.y / D, v.co.z / H)
    bm.to_mesh(me); vol = bm.calc_volume(signed=True); nm = sum(1 for e in bm.edges if not e.is_manifold); bm.free()
    return o, len(me.polygons), hole, round(vol, 3), nm

prev = bpy.context.window.scene
sc = bpy.data.scenes.get("Stage2JetRock") or bpy.data.scenes.new("Stage2JetRock")
bpy.context.window.scene = sc
res = [build(i, sc) for i in range(3)]
for x in sc.objects: x.select_set(False)
for r in res: r[0].select_set(True)
bpy.context.view_layer.objects.active = res[0][0]
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
bpy.ops.ed.undo_push(message="stage2 jet rock")
bpy.ops.wm.save_mainfile()
print([r[1:] for r in res])
