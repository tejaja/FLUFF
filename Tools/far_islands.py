# 遠景の浮島：上は草（ゆるい起伏）、ふちは少し張り出し、下は岩のとんがり（でこぼこ）。木・滝つき。頂点カラー1枚マテリアル。
# 大きさは半径1基準（Unityで拡大）。書き出し：Unity (x,y,z)=(x,z,y)
import bpy, bmesh, json, math, random
from mathutils import Vector, noise
SCR = r'C:\Users\tejas\AppData\Local\Temp\claude\D--Claude-Workspace-ClaudeDiscord\e6d24fd8-b7a4-4eeb-90a0-47b449e1d858\scratchpad'
col = bpy.data.collections.get('FarIslands') or bpy.data.collections.new('FarIslands')
if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)

GRASS = (0.56, 0.82, 0.46); GRASS2 = (0.47, 0.74, 0.40); ROCK = (0.66, 0.58, 0.70); ROCK2 = (0.46, 0.40, 0.54)
TRUNK = (0.50, 0.36, 0.26); LEAF = (0.38, 0.68, 0.36); LEAF2 = (0.46, 0.76, 0.40); WATER = (0.80, 0.93, 1.0)

def lerp3(a, b, t): return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))

def island(name, seed, depth, ntrees, falls, squash=1.0):
    random.seed(seed); off = Vector((seed * 3.1, seed * 1.7, 0))
    V = []; C = []; F = []
    SEG = 48
    # 半径方向の形（外周のでこぼこ）
    def rim(a): return 1.0 + 0.10 * noise.noise(off + Vector((math.cos(a) * 1.3, math.sin(a) * 1.3, 0))) + 0.05 * math.sin(a * 3 + seed)
    rings = []
    # 上面：中心→ふち
    tops = [0.0, 0.2, 0.4, 0.6, 0.78, 0.92, 1.0]
    for k, t in enumerate(tops):
        ring = []
        for s in range(SEG if t > 0 else 1):
            a = s / SEG * math.tau
            r = t * rim(a)
            x, y = r * math.cos(a) * squash, r * math.sin(a)
            z = 0.06 * noise.noise(off + Vector((x * 1.5, y * 1.5, 2))) + 0.05 * (1 - t * t)
            if t == 1.0: z -= 0.02
            ring.append(len(V)); V.append((x, y, z)); C.append(lerp3(GRASS, GRASS2, 0.5 + 0.5 * noise.noise(off + Vector((x * 3, y * 3, 5)))))
        rings.append(ring)
    # ふちの張り出し（草の厚み）→岩
    under = [(1.02, -0.07, GRASS2), (0.97, -0.13, ROCK)]
    n = 9
    for i in range(1, n + 1):
        t = i / n
        under.append(((1 - t) ** 0.75 * 0.93, -0.13 - depth * t, lerp3(ROCK, ROCK2, t)))
    for (rr, zz, cc) in under:
        ring = []
        for s in range(SEG):
            a = s / SEG * math.tau
            ridge = 1 + 0.18 * noise.noise(off + Vector((math.cos(a) * 2.2, math.sin(a) * 2.2, zz * 1.8))) if zz < -0.1 else 1
            r = max(0.0, rr * rim(a) * ridge)
            x, y = r * math.cos(a) * squash, r * math.sin(a)
            ring.append(len(V)); V.append((x, y, zz + 0.05 * noise.noise(off + Vector((x * 2, y * 2, 9))))); C.append(cc)
        rings.append(ring)
    tip = len(V); V.append((0.05, -0.03, -0.13 - depth * 1.08)); C.append(ROCK2)
    # 面
    for s in range(SEG): F.append((rings[0][0], rings[1][s], rings[1][(s + 1) % SEG]))
    for k in range(1, len(rings) - 1):
        A, B = rings[k], rings[k + 1]
        for s in range(SEG): F.append((A[s], B[s], B[(s + 1) % SEG], A[(s + 1) % SEG]))
    last = rings[-1]
    for s in range(SEG): F.append((last[(s + 1) % SEG], last[s], tip))
    parts = [(V, C, F, [(-1, -1)] * len(V), 1.0)]

    def top_z(x, y):
        return 0.06 * noise.noise(off + Vector((x * 1.5, y * 1.5, 2))) + 0.05 * (1 - (x * x + y * y))

    # 木：丸いもこもこの葉＋短い幹
    for i in range(ntrees):
        for _ in range(30):
            a = random.uniform(0, math.tau); r = random.uniform(0.1, 0.55)
            x, y = r * math.cos(a) * squash, r * math.sin(a)
            break
        h = random.uniform(0.10, 0.16); cr = random.uniform(0.2, 0.3)
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.05, radius2=0.04, depth=h)
        for v in bm.verts: v.co.z += h / 2
        tv = [tuple(v.co + Vector((x, y, top_z(x, y) - 0.02))) for v in bm.verts]
        tf = [tuple(v.index for v in f.verts) for f in bm.faces]; bm.free()
        parts.append((tv, [TRUNK] * len(tv), tf, [(-1, -1)] * len(tv), 1.0))
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=2, radius=cr)
        lc = lerp3(LEAF, LEAF2, random.random())
        lv = []
        for v in bm.verts:
            p = v.co.copy(); p *= 1 + 0.18 * noise.noise(p * 9 + off); p.z *= 0.85
            lv.append(tuple(p + Vector((x, y, top_z(x, y) + h + cr * 0.55))))
        lf = [tuple(v.index for v in f.verts) for f in bm.faces]; bm.free()
        parts.append((lv, [lc] * len(lv), lf, [(-1, -1)] * len(lv), 1.0))

    # 滝：上の川→ふちを丸く越えて→下へ落ちる、を1本のつながった帯で作る（切れ目なし）
    # uv.y：川の部分はマイナス、ふち=0、落ちきった所=1（シェーダーで流す・下でちぎれる）
    for k in range(falls):
        a = random.uniform(0, math.tau)
        r0 = rim(a)
        dirv = Vector((math.cos(a) * squash, math.sin(a), 0)).normalized()
        side = Vector((-dirv.y, dirv.x, 0))
        edge = Vector((math.cos(a) * r0 * squash, math.sin(a) * r0, 0))
        pts = []   # (位置, 幅の半分, uv.y)
        L = depth * 1.5
        # 川（中心寄り→ふち）
        rs = 0.35
        for i in range(8):
            t = i / 7; rr = rs + t * (r0 - rs)
            p = Vector((math.cos(a) * rr * squash, math.sin(a) * rr, 0))
            p.z = top_z(p.x, p.y) + 0.012
            pts.append((p, 0.07 * min(1.0, 0.1 + t * 2.5), -(r0 - rr)))
        # ふちを丸く越える（草の張り出しより外へ）
        for i, (ro, zo) in enumerate([(0.03, -0.01), (0.06, -0.05), (0.075, -0.10)]):
            p = edge + dirv * ro + Vector((0, 0, zo + top_z(edge.x, edge.y) * 0.5))
            pts.append((p, 0.075 + 0.005 * i, (i + 1) * 0.025))
        base = pts[-1][0].copy(); u0 = pts[-1][2]
        # 落ちる（少しずつ外へふくらむ・下ほど少し細く）
        N = 14
        for i in range(1, N + 1):
            t = i / N
            p = base + dirv * (0.08 * math.sqrt(t)) + Vector((0, 0, -L * t))
            pts.append((p, 0.09 * (1 - 0.45 * t), u0 + t * (1 - u0)))
        wv = []; wf = []; wu = []
        for i, (p, w, uy) in enumerate(pts):
            wv += [tuple(p - side * w), tuple(p + side * w)]; wu += [(0, uy), (1, uy)]
            if i: j = len(wv) - 4; wf.append((j, j + 2, j + 3, j + 1))
        parts.append((wv, [WATER] * len(wv), wf, wu, 0.0))

    # ひとつのメッシュに
    me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
    allV = []; allC = []; allF = []; allU = []
    for (pv, pc, pf, pu, kind) in parts:
        b = len(allV); allV += pv; allC += [(c[0], c[1], c[2], kind) for c in pc]; allU += pu; allF += [tuple(i + b for i in f) for f in pf]
    me.clear_geometry(); me.from_pydata(allV, [], allF); me.update()
    ca = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
    for i, c in enumerate(allC): ca.data[i].color = c
    for p in me.polygons: p.use_smooth = True
    o = bpy.data.objects.get(name)
    if o is None: o = bpy.data.objects.new(name, me); col.objects.link(o)
    return o, allV, allC, allU

specs = [('FarIsland_A', 1, 1.3, 3, 1, 1.0), ('FarIsland_B', 2, 1.0, 1, 0, 1.25), ('FarIsland_C', 3, 1.6, 5, 1, 0.9),
         ('FarIsland_D', 4, 0.8, 0, 0, 1.1), ('FarIsland_E', 5, 1.2, 2, 2, 1.0)]
out = {}
for i, sp in enumerate(specs):
    o, V, C, U = island(*sp)
    o.location = (i * 3.5, -10, 2)
    me = o.data; me.calc_loop_triangles()
    out[sp[0]] = {'v': [(v.co.x, v.co.z, v.co.y) for v in me.vertices],
                  'n': [(v.normal.x, v.normal.z, v.normal.y) for v in me.vertices],
                  'c': [tuple(c) for c in C], 'u': [tuple(u) for u in U],
                  't': [(t.vertices[0], t.vertices[2], t.vertices[1]) for t in me.loop_triangles]}
json.dump(out, open(SCR + r'\far_islands.json', 'w'))
print({k: (len(v['v']), len(v['t'])) for k, v in out.items()})
