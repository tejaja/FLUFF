import bpy, bmesh, math
from mathutils import noise, Vector as V3
from mathutils.bvhtree import BVHTree
# Stage2Cave：スタートの洞窟の岩山 Stage2_CaveRock を、元の岩 CaveRock から切り抜きの型（Cut_*）でまとめてくり抜いて作り直す（2026-10-27）。
# 前は直したい所に あとから岩を「足して」いて（ふた）、つなぎ目にすき間・線・板が出た（てじゃ「この岩山こういう隙間おおい」）。
# いまは足さずに、型のほうを先に直してから一回でくり抜く：
#   ・脇道（Cut_Side）は天井 CEIL より下だけ（前は岩のてっぺんまで抜けて上が溝に）
#   ・池（Cut_Pool）は入口側の2つの角を半径 POOL_R で丸く
#   ・池の出口まわりの型（Cut_Pool・Cut_PoolExit_Soft・Cut_ExitTop/Front・Cut_UnderLip）から、出口の壁の所（ExitKeep）を引く
#     → 池から滝の幅 EX_W1 へなめらかにすぼまる壁が、落ち口の先 EX_FWD まで残る
# そのあと 洞窟の口（Cut_Mouth・Cut_Arch）と裏の細い滝の溝の斜面（Cut_FallL/R_Soft）に少し歪み、芝/岩の材質は前の岩（Stage2_CaveRock_bak1027）からうつす
# 実行：Blender（FLUFF_models.blend）で exec(open(r"D:\UnityProject\FLUFF\Tools\stage2_cave_rock.py", encoding="utf-8").read())
CEIL = 4.55                      # 脇道の天井（Unity の StartH + SideGapH = 4.6 のツタの上のはしが 5cm 岩にうまる）
POOL_R, POOL_X, POOL_Y = 0.9, 3.48, 1.6   # 池の入口側の角（Blender の +y のはし）。溝のゆるい斜面（|x|<2.5）にかからない半径
EX_Z0, EX_W0, EX_Z1, EX_W1, EX_LIP, EX_FWD, EX_OUT = 5.4, 2.3, 6.2, 1.70, 6.766, 2.5, 5.0   # 出口の壁（Unity の z＝Blender の -y）
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Models\Stage2_CaveRock.fbx"
sc = bpy.data.scenes['Stage2Cave']; bpy.context.window.scene = sc

def apply(o, m):
    with bpy.context.temp_override(object=o, active_object=o): bpy.ops.object.modifier_apply(modifier=m.name)
def boolean(o, other, op):
    m = o.modifiers.new('b', 'BOOLEAN'); m.operation = op; m.solver = 'EXACT'; m.use_self = True; m.object = other; apply(o, m)
def temp(name, me):
    x = bpy.data.objects.get(name)
    if x: bpy.data.objects.remove(x, do_unlink=True)
    ob = bpy.data.objects.new(name, me); sc.collection.objects.link(ob); ob.hide_set(True); return ob
def copy_of(name):
    s = bpy.data.objects[name]; me = s.data.copy(); me.transform(s.matrix_world); return temp('Tmp_' + name, me)
def prism(name, ring, z0, z1):
    bm = bmesh.new()
    lo = [bm.verts.new((x, y, z0)) for x, y in ring]; hi = [bm.verts.new((x, y, z1)) for x, y in ring]
    n = len(ring)
    for i in range(n):
        j = (i + 1) % n; bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    bm.faces.new(lo); bm.faces.new(list(reversed(hi)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); return temp(name, me)
def smooth01(t): t = min(1.0, max(0.0, t)); return t * t * (3 - 2 * t)

# 前の作業の残り（ふた・型）を消す
for n in [o.name for o in sc.objects if o.name.startswith(('Plug_', 'Corner_Cutter', 'Exit_Cutter', 'HalfBox', 'Fill_', 'Cave_Test', 'DbgC'))]:
    bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)

# ---- 型を直す ----
side = copy_of('Cut_Side')
low = prism('Tmp_SideLow', [(-20, -33), (20, -33), (20, 27), (-20, 27)], -10, CEIL)
boolean(side, low, 'INTERSECT')

pool = copy_of('Cut_Pool')
for sx in (1, -1):
    cx, cy = sx * (POOL_X - POOL_R), POOL_Y - POOL_R
    ring = [(sx * (POOL_X + 0.05), cy - 0.05)]
    for k in range(13):
        a = math.radians(90 * k / 12); ring.append((cx + sx * POOL_R * math.cos(a), cy + POOL_R * math.sin(a)))
    ring += [(cx - sx * 0.05, POOL_Y + 0.05), (sx * (POOL_X + 0.05), POOL_Y + 0.05)]
    if sx < 0: ring = ring[::-1]
    boolean(pool, prism('Tmp_Corner' + str(sx), ring, 4.0, 10.0), 'DIFFERENCE')

keeps = []
for sx in (1, -1):
    ring = []
    for k in range(21):
        z = EX_Z0 + (EX_LIP + EX_FWD - EX_Z0) * k / 20
        ring.append((sx * (EX_W0 + (EX_W1 - EX_W0) * smooth01((z - EX_Z0) / (EX_Z1 - EX_Z0))), -z))
    ring += [(sx * EX_OUT, -(EX_LIP + EX_FWD)), (sx * EX_OUT, -EX_Z0)]
    if sx < 0: ring = ring[::-1]
    keeps.append(prism("Tmp_ExitKeep" + str(sx), ring, 4.3, 10.0))   # 下は落ち口の床の少し下まで。その下は Cut_UnderLip で滝の横の岩をけずったまま（残すと滝の横に岩の棚ができて、ふわふわが止まった。2026-10-27 てじゃ）
exit_cuts = []
# 出口の横・落ち口の下をけずる昔の型（Cut_ExitTop/ExitFront/UnderLip ×6）はやめた。重なって薄いヒレ・すき間ができ、滝の横に棚が残ってふわふわが止まった（2026-10-27 てじゃ「隙間とかおおい」「滝手前の岩、まだひっかかる　斜めにしちゃおう」）。
# かわりに落ち口の下を「下へ行くほど奥へ引っこむ斜めの面」1つでけずる（Tmp_UnderCut、下の「くり抜く」で）
for n in ['Cut_PoolExit_Soft']:
    c = copy_of(n)
    bm = bmesh.new(); bm.from_mesh(c.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(c.data); bm.free()
    for k in keeps: boolean(c, k, 'DIFFERENCE')
    exit_cuts.append(c)
for k in keeps: boolean(pool, k, 'DIFFERENCE')

# 裏の細い滝の溝のゆるい斜面の型：上のふちの丸み（z 6.3〜6.4 の点）が岩の上の面（≒6.25）とすれすれで交わって、幅2cmの細い面取りの帯ができ、芝の上の光るスジに見えた（2026-10-27）。
# 丸みの点を上へ持ち上げて、斜面が上の面とはっきりした角度で交わるように
soft = []
for n in ('Cut_FallL_Soft', 'Cut_FallR_Soft'):
    c = copy_of(n)
    for v in c.data.vertices:
        if 6.28 < v.co.z < 6.6: v.co.z = 6.6
    soft.append(c)
# 落ち口の下の斜めのけずり：落ち口のふち（Blender y=-LIP_Y, 床の少し下 UC_TOP）から、下へ行くほど奥（+y）へ UC_SLOPE の割合で引っこみ、UC_BACK まで。
# 幅も下へ行くほど少し広がる（四角く切った感じにしない。あとで面に歪みも）
UC_TOP, UC_SLOPE, UC_BACK, UC_W0, UC_WS = 4.95, 0.5, 0.5, 2.4, 0.05   # 奥へは 0.5m まで（1.3 だと洞窟の口の横の壁を突きぬけて、中から大きな穴が見えた）
LIP_Y = 6.766
bm = bmesh.new(); rings = []
for k in range(16):
    z = UC_TOP - k * 0.75
    rec = min((UC_TOP - z) * UC_SLOPE, UC_BACK); w = UC_W0 + (UC_TOP - z) * UC_WS
    yb = -LIP_Y + rec
    rings.append([bm.verts.new(p) for p in ((-w, yb, z), (w, yb, z), (w, -14.0, z), (-w, -14.0, z))])
for a, c in zip(rings, rings[1:]):
    for i in range(4):
        j = (i + 1) % 4; bm.faces.new([a[i], a[j], c[j], c[i]])
bm.faces.new(rings[0]); bm.faces.new(list(reversed(rings[-1])))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me_uc = bpy.data.meshes.new('Tmp_UnderCut'); bm.to_mesh(me_uc); bm.free()
undercut = temp('Tmp_UnderCut', me_uc)
# ---- くり抜く ----
# 入口のトンネルを少しせまく（半分の幅 2.6→ARCH_HW）。岩の後ろの面がトンネルの横で内へ入りこんでいて、壁の厚みが数cmしかなく、すき間が見えた（2026-10-27 てじゃ「入口の右側隙間だらけ」）。
# Unity の入口の当たり判定（DoorHW）も同じ幅に
ARCH_HW = 2.25
arch = copy_of('Cut_Arch')
for v in arch.data.vertices: v.co.x *= ARCH_HW / 2.6
cuts = [copy_of('Cut_Inside'), copy_of('Cut_Mouth'), arch, side, pool,
        copy_of('Cut_FallL'), copy_of('Cut_FallR')] + soft + exit_cuts + [undercut]
base = bpy.data.objects['CaveRock']
work = temp('Tmp_Rock', base.data.copy()); work.matrix_world = base.matrix_world
for c in cuts:
    n0 = len(work.data.vertices); boolean(work, c, 'DIFFERENCE')
    print(c.name, n0, '->', len(work.data.vertices))
# くり抜きで穴になったへり（面が1枚だけの辺）があればふさぐ
bm = bmesh.new(); bm.from_mesh(work.data)
holes = [e for e in bm.edges if e.is_boundary]
if holes: bmesh.ops.holes_fill(bm, edges=holes, sides=0)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
print('holes filled', len(holes), 'open edges left', sum(1 for e in bm.edges if e.is_boundary))
bm.to_mesh(work.data); bm.free()

# ---- 洞窟の口・溝の斜面に歪み（岩の中の向きへだけ。通り道はせまくならない）----
bm = bmesh.new(); bm.from_mesh(work.data)
mtrees = []
for n, a in (('Tmp_Cut_Mouth', 0.24), ('Tmp_Cut_Arch', 0.18), ('Tmp_Cut_Side', 0.2), ('Tmp_UnderCut', 0.14)):   # 溝の斜面は歪ませない（上の芝とのさかいに段の線が出た）
    # 脇道（ツタ）の口も（2026-10-27 てじゃ「ツタがわの穴の壁のフチもパキッっとしすぎ」）。ただし天井（下向きの面）は動かさない（ツタの上のはしとすき間ができる）
    mb = bmesh.new(); mb.from_mesh(bpy.data.objects[n].data); mtrees.append((BVHTree.FromBMesh(mb), a, n == 'Tmp_Cut_Side')); mb.free()
def amp_at(p, nz=0.0):
    best = 0.0
    for t, a, keep_ceil in mtrees:
        if keep_ceil and nz < -0.7: continue
        d = t.find_nearest(p)[3]
        if d is not None and d < 2e-3: best = max(best, a)
    return best
for _ in range(3):
    edges = list({e for f in bm.faces if amp_at(f.calc_center_median()) > 0 for e in f.edges if e.calc_length() > 0.25})
    if not edges: break
    bmesh.ops.subdivide_edges(bm, edges=edges, cuts=1, use_grid_fill=False)
bm.normal_update()
mv = {}; ma = {}
for f in bm.faces:
    a = amp_at(f.calc_center_median(), f.normal.z)
    if a > 0:
        for v in f.verts: mv.setdefault(v, V3()); mv[v] -= f.normal; ma[v] = max(ma.get(v, 0.0), a)
for v, d in mv.items():
    if d.length > 1e-6: v.co += d.normalized() * ma[v] * (noise.noise(v.co * 1.8) * 0.5 + 0.5)
bm.to_mesh(work.data); bm.free()

# ---- 材質（芝/岩）：前の岩の同じ面の上ならそれ、ちがえば上向きで高い所＝芝 ----
bak = bpy.data.objects['Stage2_CaveRock_bak1027']
o = bpy.data.objects['Stage2_CaveRock']
mats = list(o.data.materials) if len(o.data.materials) == 2 else list(bak.data.materials)
bb = bmesh.new(); bb.from_mesh(bak.data); bb.faces.ensure_lookup_table(); tree = BVHTree.FromBMesh(bb)
me = work.data; me.materials.clear()
for m in mats: me.materials.append(m)
for p in me.polygons:
    loc, nrm, idx, d = tree.find_nearest(p.center)
    if p.normal.z > 0.7 and p.center.z > 6.05: p.material_index = 1   # 上の面は必ず芝（前の岩で溝だった所が岩の色のままハゲていた）
    elif d is not None and d < 0.05 and nrm.dot(p.normal) > 0.95: p.material_index = bb.faces[idx].material_index
    else: p.material_index = 1 if (p.normal.z > 0.5 and p.center.z > 6.0) else 0
for p in me.polygons: p.use_smooth = False
bb.free()
# 芝の上の面の「スジ」をなくす（2026-10-27 てじゃ「まだスジがみえる」）：くり抜きでできた細い面がほんの少し傾いて、平らな芝の上に折れ目の線が見えた。
# 芝の上向きの面を、となりと向きが FLAT_DEG 以内ならひとかたまりにして、かたまりごとに1枚の平面へ頂点を上下にだけ動かしてのせる（横の壁が少しのびちぢみするだけ）
FLAT_DEG = 12.0
# くり抜きのはしでできた、ほとんど同じ所にある頂点（2cm以内）をくっつけて、細い面・つぶれた面を消す（ちがう向きで光って細い線に見えた）。
# くっつけて穴（ふさげないへり）ができたら、くっつける距離を小さくしてやり直す
for dist in (0.02, 0.015, 0.01, 0.006, 0.0):
    bm = bmesh.new(); bm.from_mesh(me)
    nv0 = len(bm.verts)
    if dist > 0:
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist)
        bmesh.ops.dissolve_degenerate(bm, dist=0.005, edges=bm.edges)
        holes = [e for e in bm.edges if e.is_boundary]
        if holes: bmesh.ops.holes_fill(bm, edges=holes, sides=0)
    left = sum(1 for e in bm.edges if e.is_boundary)
    print('merge near verts', dist, nv0, '->', len(bm.verts), 'open', left)
    if left == 0: break
    bm.free()
bm.faces.ensure_lookup_table(); bm.normal_update()
top = {f for f in bm.faces if f.material_index == 1 and f.normal.z > 0.8}
seen = set(); regions = []
for f0 in top:
    if f0 in seen: continue
    st = [f0]; seen.add(f0); reg = []
    while st:
        f = st.pop(); reg.append(f)
        for e in f.edges:
            for g in e.link_faces:
                if g in top and g not in seen and g.normal.dot(f.normal) > math.cos(math.radians(FLAT_DEG)): seen.add(g); st.append(g)
    regions.append(reg)
moved = 0; maxd = 0.0
reg_set = {id(r): set(r) for r in regions}
for reg in regions:
    if len(reg) < 2: continue
    n = V3(); c = V3(); A = 0.0
    for f in reg: a = f.calc_area(); n += f.normal * a; c += f.calc_center_median() * a; A += a
    n.normalize(); c /= A
    for v in {v for f in reg for v in f.verts if all((g in reg_set[id(reg)]) or g.normal.z < 0.8 or g.material_index != 1 for g in v.link_faces)}:
        # 平面上の高さ：n・(p - c) = 0 → z = c.z - (n.x (x-c.x) + n.y (y-c.y)) / n.z
        z = c.z - (n.x * (v.co.x - c.x) + n.y * (v.co.y - c.y)) / n.z
        d = z - v.co.z
        if abs(d) < 0.15: v.co.z = z; moved += 1; maxd = max(maxd, abs(d))
print('flatten regions', sum(1 for r in regions if len(r) > 1), 'verts moved', moved, 'max move', round(maxd, 3), 'open edges', sum(1 for e in bm.edges if e.is_boundary))
bm.to_mesh(me); bm.free()
# 芝の面だけなめらかに（スムーズ）：細い三角形が少しちがう向きで光ってスジに見えるのを、となりと同じ光り方にして消す。岩の面はカクカクのまま
for p in me.polygons: p.use_smooth = (p.material_index == 1)
old = o.data; o.data = me; me.name = 'Stage2_CaveRock'
bpy.data.objects.remove(work, do_unlink=True)
for n in [x.name for x in sc.objects if x.name.startswith('Tmp_')]: bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)
for x in sc.objects: x.select_set(False)
o.hide_set(False); o.select_set(True); bpy.context.view_layer.objects.active = o
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.sort_elements(type='MATERIAL', elements={'FACE'})   # FBX のサブメッシュの順番がずれないように
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.ed.undo_push(message='cave rock rebuild')
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE')
bpy.ops.wm.save_mainfile()
print('built', len(o.data.vertices), 'grass faces', sum(1 for p in o.data.polygons if p.material_index == 1))
