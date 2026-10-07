# コース2「いわのみち」ゴール前の岩場（2026-10-07）：元の四角い台地（s79〜87）の代わりに、いびつな形の岩場を奥（s≈100）まで伸ばす。
# 上の面は人が歩けるほぼ平ら（ゆるい起伏）、噴気孔の所は平らに。横は岩の崖、下は浮島のように細くなる。
# 座標は Unity のコースローカル（FuwaCourse2）。Blender には (x,y,z)->(x,z,y) で置き、Unity へは JSON で書き出して ImportRocks で取り込む。
import bpy, json, math, bmesh
from mathutils import Vector, noise
from mathutils.geometry import delaunay_2d_cdt

SCR = r'C:\Users\tejas\AppData\Local\Temp\claude\D--Claude-Workspace-ClaudeDiscord\68e7a8cc-2e09-42e1-87b9-4949b84aabe2\scratchpad'
F = json.load(open(SCR + r'\plaza_frame.json'))
O = Vector(F['o']); D = Vector(F['d']); R = Vector(F['r'])
TOP = O.y   # 元の台地の上面（ローカル y=3）

S0 = 78.3            # 岩場の手前のはし（坂の下）
GOAL_S = 102.4       # ゴールの中心（s）。ゴールの丸い地面の半径 3
GOAL_R = 3.0
# 噴気孔 (s, 横)。坂の降り口(s<82, 横<-1.5)と、追い風でふわふわが来る所(s79〜81, 横0付近)はあける
VENTS = []   # build() で岩場いっぱいにばらまく（2026-10-07 てじゃ「目一杯ふやそ」）
VENT_GAP = 2.6       # 噴気孔どうしの最小間隔（口のふちの盛り上がりが半径1.4m）
VENT_EDGE = 1.5      # 岩場のふちからの最小距離

def W(s, lat, y):
    p = O + D * (s - 79.0) + R * lat
    return Vector((p.x, y, p.z))

def n1(x, seed):
    return noise.noise(Vector((x, seed, seed * 0.37)))

def center(s):
    return -0.35 + 0.85 * math.sin((s - 78.0) * 0.27) + 0.25 * n1(s * 0.2, 3.1)

def half(s):
    w = 4.3 + 0.55 * math.sin((s - 78.0) * 0.43 + 0.8) + 0.35 * n1(s * 0.35, 7.7)
    # ゴールに近づくと細くして、ゴールの丸い地面に吸い込まれるように
    k = (s - (GOAL_S - 7.0)) / 5.0
    if k > 0: w = w * (1 - min(k, 1) * 0.42)
    return w

def outline():
    """(s, lat) の外周（反時計回り：左のふちを奥へ → 右のふちを手前へ）"""
    s_end = GOAL_S - 1.2
    pts_l, pts_r = [], []
    s = S0 + 0.9
    while s <= s_end:
        c, w = center(s), half(s)
        jl = 0.55 * n1(s * 0.55, 11.3) + 0.25 * n1(s * 1.4, 2.9)
        jr = 0.55 * n1(s * 0.55, 19.1) + 0.25 * n1(s * 1.4, 5.3)
        l = c - w - jl
        if s < 82.5: l = min(l, -5.0)        # 坂の降り口を必ず含める
        pts_l.append((s, l)); pts_r.append((s, c + w + jr))
        s += 0.35
    # 手前のはし：丸くふくらむ
    cap = []
    c0, w0 = center(S0 + 0.9), half(S0 + 0.9)
    l0, r0 = min(c0 - w0, -5.0), c0 + w0
    mid = (l0 + r0) * 0.5; hw = (r0 - l0) * 0.5
    for i in range(1, 12):
        a = math.pi * i / 12
        cap.append((S0 + 0.9 - math.sin(a) * 0.9 * (1 + 0.15 * n1(i * 0.7, 4.4)), mid + math.cos(a) * hw))   # 右→左
    ring = list(reversed(pts_r)) + cap + pts_l   # 右(奥→手前) → 手前のはし → 左(手前→奥)
    # 奥のはしはゴールの円の中まで伸ばして、円の内側に入った点は円周へ押し出す（ゴールの地面と接する）
    gc = Vector((GOAL_S, 0.0))
    # 円の中に入った点は捨てて、円に入る所で切る（円周上の点を足す）
    def cut(a, b):
        # a(外)→b(中) の線分と円の交点
        lo, hi = 0.0, 1.0
        for _ in range(30):
            m = (lo + hi) / 2
            if (Vector(a).lerp(Vector(b), m) - gc).length > GOAL_R: lo = m
            else: hi = m
        return Vector(a).lerp(Vector(b), lo).to_tuple()
    out = []
    for i, (s, l) in enumerate(ring):
        if (Vector((s, l)) - gc).length >= GOAL_R:
            if out == [] and i > 0 and (Vector(ring[i - 1]) - gc).length < GOAL_R:
                out.append(cut((s, l), ring[i - 1]))
            out.append((s, l))
        else:
            if i > 0 and (Vector(ring[i - 1]) - gc).length >= GOAL_R:
                out.append(cut(ring[i - 1], (s, l)))

    # 円周に沿う所：左の最後 → 右の最初 を円弧でつなぐ
    a0 = math.atan2(out[-1][1], out[-1][0] - GOAL_S)
    a1 = math.atan2(out[0][1], out[0][0] - GOAL_S)
    if a1 > a0: a1 -= 2 * math.pi
    arc = []
    n = max(2, int(abs(a1 - a0) / 0.12))
    for i in range(1, n):
        a = a0 + (a1 - a0) * i / n
        arc.append((GOAL_S + math.cos(a) * GOAL_R, math.sin(a) * GOAL_R))
    out = out + arc
    # 近すぎる点を間引く
    clean = []
    for p in out:
        if not clean or (Vector(p) - Vector(clean[-1])).length > 0.12: clean.append(p)
    return clean

def inside(pt, poly):
    x, y = pt; c = False
    for i in range(len(poly)):
        x1, y1 = poly[i]; x2, y2 = poly[i - 1]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1: c = not c
    return c

def dist_poly(pt, poly):
    best = 1e9; p = Vector(pt)
    for i in range(len(poly)):
        a = Vector(poly[i - 1]); b = Vector(poly[i]); ab = b - a
        t = max(0, min(1, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
        best = min(best, (a + ab * t - p).length)
    return best

def top_y(s, lat, poly):
    # ゆるい起伏。噴気孔・坂の降り口・ゴールの近く・ふちは平ら
    h = 0.06 * noise.noise(Vector((s * 0.28, lat * 0.28, 1.7))) + 0.03 * noise.noise(Vector((s * 0.9, lat * 0.9, 5.2)))
    f = 1.0
    lip = 0.0
    for (vs, vl) in VENTS:
        d = math.hypot(s - vs, lat - vl)
        f = min(f, max(0.0, (d - 1.45) / 0.8))
        if d < 1.45:
            if d < 0.58: k = 0.0
            elif d < 0.86: k = (d - 0.58) / 0.28; k = k * k * (3 - 2 * k)
            else: k = 1.0 - (d - 0.86) / 0.59; k = k * k * (3 - 2 * k)
            a = math.atan2(lat - vl, s - vs)
            lip = max(lip, k * (0.22 + 0.06 * noise.noise(Vector((math.cos(a) * 1.3 + vs, math.sin(a) * 1.3 + vl, 8.1)))))
    if s < 83.0 and lat < -1.0: f = min(f, max(0.0, (s - 82.0) / 1.0))
    dg = math.hypot(s - GOAL_S, lat)
    f = min(f, max(0.0, (dg - GOAL_R - 0.3) / 1.2))
    de = dist_poly((s, lat), poly)
    f = min(f, max(0.0, de / 0.6))
    return TOP + h * f + lip

class MB:
    def __init__(self): self.V = []; self.T = []
    def v(self, p): self.V.append(tuple(p)); return len(self.V) - 1
    def tri(self, a, b, c): self.T.append((a, b, c))
    def quad(self, a, b, c, d): self.tri(a, b, c); self.tri(a, c, d)

def place_vents(poly):
    import random
    rnd = random.Random(5)
    s_min = min(p[0] for p in poly); s_max = max(p[0] for p in poly)
    l_min = min(p[1] for p in poly); l_max = max(p[1] for p in poly)
    out = []
    for _ in range(8000):
        s = rnd.uniform(s_min, s_max); l = rnd.uniform(l_min, l_max)
        if s < 81.3: continue                                   # 追い風でふわふわが入ってくる所
        if s < 83.2 and l < -1.0: continue                      # 坂の降り口
        if math.hypot(s - GOAL_S, l) < GOAL_R + 1.9: continue   # ゴールの手前はあける
        if not inside((s, l), poly) or dist_poly((s, l), poly) < VENT_EDGE: continue
        if any(math.hypot(s - a, l - b) < VENT_GAP for (a, b) in out): continue
        out.append((round(s, 2), round(l, 2)))
    out.sort()
    return out

def build():
    poly = outline()
    VENTS[:] = place_vents(poly)
    n = len(poly)
    mb = MB()
    # ---- 上の面：外周＋中の格子点で三角形分割（外周の辺は固定）
    pts = [Vector(p) for p in poly]
    s_min = min(p[0] for p in poly); s_max = max(p[0] for p in poly)
    l_min = min(p[1] for p in poly); l_max = max(p[1] for p in poly)
    g = 0.7
    s = s_min + g * 0.5
    while s < s_max:
        l = l_min + g * 0.5 + (0.35 if int((s - s_min) / g) % 2 else 0)
        while l < l_max:
            if inside((s, l), poly) and dist_poly((s, l), poly) > 0.3 and all(math.hypot(s - a, l - b) > 1.7 for (a, b) in VENTS):
                pts.append(Vector((s, l)))
            l += g
        s += g
    # 噴気孔の口のまわりは細かめの輪（ふちの岩が乗るので平らに）
    for (vs, vl) in VENTS:
        for r, m in ((0.3, 8), (0.58, 16), (0.72, 16), (0.86, 18), (1.05, 18), (1.25, 18), (1.45, 18)):
            for k in range(m):
                a = 2 * math.pi * (k + (0.5 if m == 18 and r > 1.0 else 0)) / m
                pts.append(Vector((vs + math.cos(a) * r, vl + math.sin(a) * r)))
    edges = [(i, (i + 1) % n) for i in range(n)]
    res = delaunay_2d_cdt(pts, edges, [], 2, 1e-5)   # 2 = 外周の内側だけ
    cverts, cedges, cfaces = res[0], res[1], res[2]
    top_idx = []
    for cv in cverts:
        s, l = cv.x, cv.y
        top_idx.append(mb.v(W(s, l, top_y(s, l, poly))))
    for f in cfaces:
        if len(f) == 3: mb.tri(top_idx[f[0]], top_idx[f[2]], top_idx[f[1]])
    # 外周の点が上の面のどの頂点か（CDT は外周の点を先頭にそのまま残す）
    orig = res[3]
    edge_top = [None] * n
    for j, ov in enumerate(orig):
        for o in ov:
            if o < n: edge_top[o] = top_idx[j]
    # 外周の辺が CDT で分割されていないか（されていると横の面とすき間ができる）
    split = sum(1 for oe in res[4] if len(oe) > 1)
    print('cdt outline missing', sum(1 for e in edge_top if e is None), 'verts', len(cverts), 'edges split?', split)
    # ---- 横と下：外周を外へ・下へずらした輪を重ねる
    def normal2(i):
        a = Vector(poly[i - 1]); b = Vector(poly[(i + 1) % n])
        t = (b - a).normalized()
        return Vector((t.y, -t.x))   # 反時計回りの外向き… 向きは下で確かめて合わせる
    # 外向きの確認（外周の少し外が多角形の外なら OK）
    test = Vector(poly[0]) + normal2(0) * 0.05
    sign = -1.0 if inside(test.to_tuple(), poly) else 1.0
    # (外への張り出し m, 下がる量 m, ノイズ, 真ん中へ寄せる割合)
    rows = [(0.06, 0.10, 0.02, 0.0), (0.16, 0.38, 0.08, 0.0), (0.10, 0.85, 0.16, 0.0), (-0.12, 1.5, 0.24, 0.0),
            (0.0, 2.3, 0.26, 0.16), (0.0, 3.1, 0.26, 0.38), (0.0, 3.9, 0.22, 0.62), (0.0, 4.6, 0.15, 0.84)]
    rings = [edge_top]
    for k, (outv, down, amp, pull) in enumerate(rows):
        ring = []
        for i in range(n):
            s, l = poly[i]
            nm = normal2(i) * sign
            q = Vector((s, l)) + nm * outv
            if pull > 0:
                c = center(min(max(s, S0 + 1.2), GOAL_S - 2.0))
                q = Vector((min(max(q.x, S0 + 0.8 + 3.0 * pull), GOAL_S - 1.0 - 4.0 * pull), q.y + (c - q.y) * pull))
            p = W(q.x, q.y, TOP - down)
            # 岩らしいでこぼこ（外向き＋上下）
            nw = (D * nm.x + R * nm.y)
            p = p + nw * (noise.noise(Vector((p.x * 0.9, p.y * 0.9, p.z * 0.9))) * amp)
            p.y += noise.noise(Vector((p.x * 0.7 + 3, p.y * 0.7, p.z * 0.7))) * amp * 0.6
            ring.append(mb.v(p))
        rings.append(ring)
    for k in range(len(rings) - 1):
        a, b = rings[k], rings[k + 1]
        for i in range(n):
            j = (i + 1) % n
            mb.quad(a[i], b[i], b[j], a[j])
    # 底：最後の輪を真ん中の点へ
    last = rings[-1]
    cp = Vector((0, 0, 0))
    for r in last: cp += Vector(mb.V[r])
    cp /= len(last); cp.y -= 0.5
    c = mb.v(cp)
    for i in range(n): mb.tri(last[i], c, last[(i + 1) % n])
    return mb, poly

def to_blender(name, mb, col):
    me = bpy.data.meshes.new(name)
    verts = [(p[0], p[2], p[1]) for p in mb.V]
    faces = [(a, c, b) for (a, b, c) in mb.T]
    me.from_pydata(verts, [], faces); me.update()
    ob = bpy.data.objects.get(name)
    if ob: bpy.data.objects.remove(ob)
    ob = bpy.data.objects.new(name, me); col.objects.link(ob)
    return ob

mb, poly = build()
col = bpy.data.collections.get('C2Field') or bpy.data.collections.new('C2Field')
if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
ob = to_blender('C2F_field', mb, col)
me = ob.data
bm = bmesh.new(); bm.from_mesh(me)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
bm.to_mesh(me); bm.free(); me.update()
# 上の面が上を向いているか（Blender は z が上）
up = sum(1 for p in me.polygons if p.normal.z > 0.9)
V = [(v.co.x, v.co.z, v.co.y) for v in me.vertices]
T = []
for p in me.polygons:
    vs = list(p.vertices)
    for i in range(1, len(vs) - 1): T.append((vs[0], vs[i + 1], vs[i]))
vents = [list(W(s, l, TOP)) for (s, l) in VENTS]
info = {'vents': vents, 'goal': list(W(GOAL_S, 0, TOP)), 'outline_n': len(poly)}
json.dump({'field': {'v': V, 't': T}, 'info': info}, open(SCR + r'\course2_field.json', 'w'))
json.dump(info, open(r'D:\UnityProject\FLUFF\BlenderSource\course2_field_vents.json', 'w'))
print('vents', len(VENTS), VENTS)
# 非多様体の辺（穴）チェック
bm = bmesh.new(); bm.from_mesh(me)
nm = sum(1 for e in bm.edges if not e.is_manifold); bm.free()
print('field tris', len(T), 'up-facing', up, 'non-manifold', nm, 'outline', len(poly))
