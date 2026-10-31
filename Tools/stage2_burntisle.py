import bpy, bmesh, math
import numpy as np
# 2–2 のコースの外に浮かべる、黒焦げの木と小さな浮島（2026-10-31 てじゃ「いいね」。岩系が多いので島は土台だけ小さく）。
# 木の根元・島の上の面が y=0。Unity 座標で作って B() で Blender へ。色と光る割れ目は Unity の FuwaCourse/LavaRock。
# 実行：Blender（FLUFF_models.blend）で exec(open(r"D:\UnityProject\FLUFF\Tools\stage2_burntisle.py", encoding="utf-8").read())
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Models\Stage2_BurntIsle.fbx"

def B(p): return (-p[0], -p[2], p[1])   # Unity (x,y,z) → Blender (-x,-z,y)

def add_tube(bm, pts, radii, sides):
    # 点の列にそって筒を作る。先はとじる。根元は親の枝の中に埋まるので開いたままにしない（ふたをする）
    rings = []
    up = np.array([0.0, 0.0, 1.0])
    for k, p in enumerate(pts):
        d = pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]; d = d / max(np.linalg.norm(d), 1e-5)
        a = np.cross(d, up)
        if np.linalg.norm(a) < 1e-3: a = np.array([1.0, 0.0, 0.0])
        a /= np.linalg.norm(a); b = np.cross(d, a)
        rings.append([bm.verts.new(B(p + (a * math.cos(i * 2 * math.pi / sides) + b * math.sin(i * 2 * math.pi / sides)) * radii[k])) for i in range(sides)])
    for k in range(len(rings) - 1):
        for i in range(sides):
            i1 = (i + 1) % sides
            bm.faces.new([rings[k][i], rings[k][i1], rings[k + 1][i1], rings[k + 1][i]])
    bm.faces.new(rings[0][::-1])
    tip = bm.verts.new(B(pts[-1] + (pts[-1] - pts[-2]) * 0.5))
    for i in range(sides):
        bm.faces.new([rings[-1][i], rings[-1][(i + 1) % sides], tip])

def branch(bm, r, p, d, length, rad, depth):
    segs = 5 if depth == 0 else 4
    pts = [p.copy()]; radii = [rad]
    cur = d.copy()
    for k in range(segs):
        cur = cur + r.normal(0, 0.16, 3) + np.array([0, 0.03 if depth > 0 else 0.0, 0]); cur /= np.linalg.norm(cur)
        pts.append(pts[-1] + cur * length / segs)
        radii.append(rad * (1 - (k + 1) / segs * 0.82))
    add_tube(bm, pts, radii, 7 if depth == 0 else 5)
    if depth >= 3: return
    n = 3 if depth == 0 else 2
    for j in range(n):
        at = r.randint(2, segs + 1) if depth == 0 else segs - r.randint(0, 2)
        at = min(at, segs)
        side = np.cross(cur, r.normal(size=3)); side /= max(np.linalg.norm(side), 1e-4)
        nd = cur * 0.62 + side * 0.78; nd /= np.linalg.norm(nd)
        branch(bm, r, pts[at] - cur * 0.03, nd, length * r.uniform(0.5, 0.68), max(radii[at] * 0.72, 0.035), depth + 1)

def tree(i, sc):
    r = np.random.RandomState(120 + i)
    bm = bmesh.new()
    # 根の張り出し（幹の根元にとがった根）
    for k in range(5):
        a = k * 2 * math.pi / 5 + r.uniform(-0.3, 0.3)
        out = np.array([math.sin(a), 0, math.cos(a)])
        pts = [np.array([0, 0.75, 0]) + out * 0.05, out * 0.42 + np.array([0, 0.18, 0]), out * 0.95 + np.array([0, -0.04, 0]), out * 1.25 + np.array([0, -0.25, 0])]
        add_tube(bm, pts, [0.2, 0.16, 0.09, 0.03], 5)
    branch(bm, r, np.array([0.0, -0.3, 0.0]), np.array([0.0, 1.0, 0.0]), 4.2 + i * 0.9, 0.42, 0)
    return finish(bm, "Stage2_BurntTree%d" % i, sc)

def isle(i, sc):
    r = np.random.RandomState(170 + i)
    N = 36; R0 = 3.1 + i * 0.5; D = R0 * 1.25
    ph = r.uniform(0, 6.28, 4)
    def edge(a): return R0 * (0.86 + 0.14 * math.sin(a * 2 + ph[0]) + 0.07 * math.sin(a * 5 + ph[1]) + 0.04 * math.sin(a * 9 + ph[2]))
    bm = bmesh.new()
    prof = [(0.0, 0.0)]  # (半径の割合, y) は下で使う
    rows = [(0.55, 0.0), (0.97, 0.0), (1.0, -0.2), (0.93, -0.55), (0.72, -0.28 * D), (0.46, -0.55 * D), (0.2, -0.82 * D)]
    grid = []
    for rf, y in rows:
        ring = []
        for k in range(N):
            a = k * 2 * math.pi / N
            n = (r.uniform(-0.1, 0.1) if y < -0.3 else 0.0)
            ra = edge(a) * rf * (1 + n)
            yy = y + (r.uniform(-0.18, 0.18) if y < -0.3 else 0.0)
            ring.append(bm.verts.new(B(np.array([math.sin(a) * ra, yy, math.cos(a) * ra]))))
        grid.append(ring)
    top = bm.verts.new(B(np.array([0.0, 0.0, 0.0]))); tip = bm.verts.new(B(np.array([r.uniform(-0.3, 0.3), -D, r.uniform(-0.3, 0.3)])))
    for k in range(N):
        k1 = (k + 1) % N
        bm.faces.new([top, grid[0][k], grid[0][k1]])
        for j in range(len(grid) - 1): bm.faces.new([grid[j][k], grid[j + 1][k], grid[j + 1][k1], grid[j][k1]])
        bm.faces.new([grid[-1][k], tip, grid[-1][k1]])
    return finish(bm, "Stage2_BurntIsle%d" % i, sc)

def finish(bm, name, sc):
    o = sc.objects.get(name)
    if o: bpy.data.objects.remove(o, do_unlink=True)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    res = bmesh.ops.triangulate(bm, faces=bm.faces)
    for f in bm.faces: f.smooth = False
    vol = bm.calc_volume(signed=True); nm = sum(1 for e in bm.edges if not e.is_manifold)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    mat = bpy.data.materials.get("Stage2_BurntIsle") or bpy.data.materials.new("Stage2_BurntIsle")
    me.materials.clear(); me.materials.append(mat)
    return o, len(me.polygons), round(vol, 1), nm

sc = bpy.data.scenes.get("Stage2BurntIsle") or bpy.data.scenes.new("Stage2BurntIsle")
bpy.context.window.scene = sc
res = [tree(0, sc), tree(1, sc), isle(0, sc), isle(1, sc)]
for x in sc.objects: x.select_set(False)
for r in res: r[0].select_set(True)
bpy.context.view_layer.objects.active = res[0][0]
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False)
bpy.ops.ed.undo_push(message="stage2 burnt isle")
bpy.ops.wm.save_mainfile()
print([(r[0].name,) + r[1:] for r in res])
