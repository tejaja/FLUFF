# コース2「いわのみち」ゴール前の崩れた広場（2026-10-07）：噴気孔の岩柱3本・坂のふもとの足場・浮き岩2つ・崩れた名残。
# 座標は Unity のコースローカル（FuwaCourse2 の子 Rocks2 = 単位変換）で計算し、Blender には (x,y,z)->(x,z,y) で置く。
# Unity へは JSON（頂点は Unity 座標、三角形は Unity の巻き順）で書き出して FuwaCourse2Builder.ImportRocks で取り込む。
import bpy, json, math, bmesh
from mathutils import Vector, noise, Matrix

SCR = r'C:\Users\tejas\AppData\Local\Temp\claude\D--Claude-Workspace-ClaudeDiscord\68e7a8cc-2e09-42e1-87b9-4949b84aabe2\scratchpad'
F = json.load(open(SCR + r'\plaza_frame.json'))
O = Vector(F['o']); D = Vector(F['d']); R = Vector(F['r'])
UP = Vector((0, 1, 0))
TOP = O.y   # 元の台地の上面（ローカル y=3）

def W(s, lat, y):
    p = O + D * (s - 79.0) + R * lat
    return Vector((p.x, y, p.z))

def nz(p, scale, amp, seed=0.0):
    return noise.noise(Vector((p[0] * scale + seed, p[1] * scale, p[2] * scale))) * amp

class MB:
    def __init__(self): self.V = []; self.T = []
    def v(self, p): self.V.append(tuple(p)); return len(self.V) - 1
    def tri(self, a, b, c): self.T.append((a, b, c))
    def quad(self, a, b, c, d): self.tri(a, b, c); self.tri(a, c, d)

def loft(mb, c, rows, m, seed, ax=R, az=D, cap_top=True, tip=None, top_noise_down=True, jog=0.0):
    """rows: [(y, rx, rz, amp)] 上から下へ。c=中心(xz)。上はふたを閉じる。tip=(y)なら下を一点に絞る。
    jog=段ごとに中心を横へずらす量（ゴツゴツした積み重なり感）"""
    rings = []
    for k, (y, rx, rz, amp) in enumerate(rows):
        ring = []
        off = (R * nz(Vector((k * 1.7, seed, 0.3)), 1.0, jog, seed) + D * nz(Vector((seed, k * 1.7, 0.9)), 1.0, jog, seed + 9)) if k > 1 else Vector()
        for i in range(m):
            a = 2 * math.pi * i / m + seed * 0.37
            dirv = ax * math.cos(a) * rx + az * math.sin(a) * rz
            p = Vector((c.x, y, c.z)) + dirv + off
            n = dirv.normalized() if dirv.length > 0 else Vector((1, 0, 0))
            p = p + n * nz(p, 1.6, amp, seed)
            if k > 0: p.y += nz(p + Vector((5, 0, 0)), 1.3, amp * 0.6, seed + 2)
            ring.append(mb.v(p))
        rings.append(ring)
    for k in range(len(rings) - 1):
        for i in range(m):
            j = (i + 1) % m
            mb.quad(rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i])
    if cap_top:
        y0 = rows[0][0]
        ctr = mb.v(Vector((c.x, y0, c.z)))
        for i in range(m): mb.tri(rings[0][i], ctr, rings[0][(i + 1) % m])
    if tip is not None:
        t = mb.v(Vector((c.x, tip, c.z)) + R * nz(Vector((c.x, tip, c.z)), 1.0, 0.3, seed))
        for i in range(m): mb.tri(rings[-1][(i + 1) % m], t, rings[-1][i])
    return rings

# ---- 噴気孔の岩柱（てっぺんがクレーター）。下は雲の中へ細く消える ----
VENTS = [(76.6, 0.3), (80.9, 1.4), (85.0, 1.9)]
CREST = 2.3
def pillar(mb, mbd, s, lat, k):
    c = W(s, lat, 0)
    m = 10
    # 外側：上から下（てっぺんのふち→肩→段々の胴→細く）。段（出っぱり→へこみ）をくり返して岩の層っぽく
    rows = [
        (CREST - 0.12, 0.47, 0.47, 0.015),   # 内側のふち（穴の口）
        (CREST, 0.68, 0.68, 0.04),           # ふちの頂
        (CREST - 0.2, 0.95, 0.9, 0.1),       # ふちの外
        (CREST - 0.65, 1.12, 1.05, 0.2),     # 肩
        (0.9, 1.02, 0.98, 0.22),
        (0.65, 1.3, 1.22, 0.25),             # 段の出っぱり
        (-0.6, 1.2, 1.12, 0.3),
        (-0.9, 1.0, 0.95, 0.25),             # へこみ
        (-1.2, 1.28, 1.2, 0.3),              # 段
        (-2.9, 1.1, 1.05, 0.32),
        (-4.4, 0.85, 0.8, 0.28),
        (-5.8, 0.55, 0.5, 0.2),
        (-6.9, 0.28, 0.26, 0.1),
    ]
    rings = loft(mb, c, rows, m, k * 3.7 + 1.0, cap_top=False, tip=-7.8, jog=0.25)
    # 穴の中：口から下へ（内壁）、底はふた
    inner = [(CREST - 0.12, 0.47, 0.47, 0.015), (CREST - 0.45, 0.43, 0.43, 0.02), (CREST - 0.62, 0.40, 0.40, 0.0)]
    mi = MB()
    ir = []
    for (y, rx, rz, amp) in inner:
        ring = []
        for i in range(m):
            a = 2 * math.pi * i / m + (k * 3.7 + 1.0) * 0.37
            dirv = R * math.cos(a) * rx + D * math.sin(a) * rz
            p = Vector((c.x, y, c.z)) + dirv
            p = p + dirv.normalized() * nz(p, 1.6, amp, k * 3.7 + 1.0)
            ring.append(p)
        ir.append(ring)
    # 口のリングは外側の一段目と同じ頂点を使う
    idx = [rings[0]] + [[mb.v(p) for p in ring] for ring in ir[1:]]
    for q in range(len(idx) - 1):
        for i in range(m):
            j = (i + 1) % m
            mb.quad(idx[q][j], idx[q][i], idx[q + 1][i], idx[q + 1][j])
    bot = mb.v(Vector((c.x, CREST - 0.62, c.z)))
    for i in range(m): mb.tri(idx[-1][(i + 1) % m], idx[-1][i], bot)
    # 穴の底の暗い円盤（少し上に浮かせて底をおおう）
    yd = CREST - 0.55
    ctr = mbd.v(Vector((c.x, yd, c.z)))
    ring = [mbd.v(Vector((c.x, yd, c.z)) + R * math.cos(2 * math.pi * i / m) * 0.39 + D * math.sin(2 * math.pi * i / m) * 0.39) for i in range(m)]
    for i in range(m): mbd.tri(ring[i], ctr, ring[(i + 1) % m])
    # 裏面も（下からのぞいても抜けないように薄い板に）
    ctr2 = mbd.v(Vector((c.x, yd - 0.02, c.z)))
    ring2 = [mbd.v(Vector(mbd.V[r]) + Vector((0, -0.02, 0))) for r in ring]
    for i in range(m): mbd.tri(ring2[(i + 1) % m], ctr2, ring2[i])
    for i in range(m):
        j = (i + 1) % m
        mbd.quad(ring[j], ring[i], ring2[i], ring2[j])
    return [c.x, CREST, c.z]

# ---- 浮いた岩（上が平ら＝乗れる、下はとがる） ----
def float_rock(mb, s, lat, top, rx, rz, seed, depth=1.7):
    c = W(s, lat, 0)
    rows = [
        (top, rx, rz, 0.07),
        (top - 0.16, rx * 1.06, rz * 1.06, 0.12),
        (top - 0.45, rx * 0.95, rz * 0.95, 0.2),
        (top - depth * 0.55, rx * 0.66, rz * 0.66, 0.2),
        (top - depth * 0.85, rx * 0.32, rz * 0.32, 0.12),
    ]
    loft(mb, c, rows, 9, seed, tip=top - depth, jog=0.12)

def tilted_slab(mb, s, lat, y, hx, hy, hz, rot_deg, axis, seed):
    """崩れ落ちた床の板（岩の厚い板）を傾けて置く"""
    c = W(s, lat, y)
    rot = Matrix.Rotation(math.radians(rot_deg), 3, axis)
    n = 3
    idx = {}
    def P(i, j, k):
        key = (i, j, k)
        if key in idx: return idx[key]
        u = Vector((i / n * 2 - 1, j / n * 2 - 1, k / n * 2 - 1))
        p = R * (u.x * hx) + UP * (u.y * hy) + D * (u.z * hz)
        dirv = (R * u.x + UP * u.y + D * u.z)
        p = p + dirv.normalized() * nz(c + p, 1.7, 0.07, seed)
        p = c + rot @ p
        idx[key] = mb.v(p); return idx[key]
    for a in range(n):
        for b in range(n):
            mb.quad(P(n, a, b), P(n, a + 1, b), P(n, a + 1, b + 1), P(n, a, b + 1))
            mb.quad(P(0, a, b), P(0, a, b + 1), P(0, a + 1, b + 1), P(0, a + 1, b))
            mb.quad(P(a, n, b), P(a, n, b + 1), P(a + 1, n, b + 1), P(a + 1, n, b))
            mb.quad(P(a, 0, b), P(a + 1, 0, b), P(a + 1, 0, b + 1), P(a, 0, b + 1))
            mb.quad(P(a, b, n), P(a + 1, b, n), P(a + 1, b + 1, n), P(a, b + 1, n))
            mb.quad(P(a, b, 0), P(a, b + 1, 0), P(a + 1, b + 1, 0), P(a + 1, b, 0))

def build():
    out = {}
    mbp = MB(); mbd = MB(); info = []
    for k, (s, lat) in enumerate(VENTS): info.append(pillar(mbp, mbd, s, lat, k))
    out['plaza_pillars'] = mbp; out['plaza_ventdark'] = mbd
    # 坂のふもとの足場（元の台地の左手前の名残）。上面は元の台地と同じ高さ
    mbl = MB()
    c = W(80.35, -2.65, 0)
    rows = [(TOP, 1.5, 1.0, 0.06), (TOP - 0.2, 1.58, 1.07, 0.12), (TOP - 0.7, 1.45, 0.98, 0.22),
            (TOP - 1.0, 1.25, 0.86, 0.2), (TOP - 1.3, 1.4, 0.95, 0.24),
            (TOP - 2.2, 1.1, 0.76, 0.24), (TOP - 3.1, 0.75, 0.55, 0.18), (TOP - 3.9, 0.38, 0.3, 0.1)]
    loft(mbl, c, rows, 11, 7.3, tip=TOP - 4.6, jog=0.2)
    out['plaza_ledge'] = mbl
    # 浮き岩（上下にゆっくり動く。メッシュは静止位置で作る）
    for name, (s, lat, top, rx, rz, seed) in {'plaza_float1': (83.2, -3.0, TOP, 0.82, 0.74, 11.1), 'plaza_float2': (85.0, -1.0, TOP + 0.15, 0.78, 0.8, 13.7)}.items():
        mb = MB(); float_rock(mb, s, lat, top, rx, rz, seed); out[name] = mb
    # 崩れた名残：右奥と右手前のふちのかけら、穴の中に引っかかった床の板、小石
    mbr = MB()
    float_rock(mbr, 86.9, 3.4, TOP - 0.25, 0.85, 0.6, 21.3, depth=2.2)
    float_rock(mbr, 79.6, 3.5, TOP - 0.45, 0.7, 0.55, 23.9, depth=1.9)
    tilted_slab(mbr, 78.8, 1.9, 0.2, 1.1, 0.16, 0.7, 28, D, 31.0)
    tilted_slab(mbr, 83.0, 1.1, -0.6, 0.9, 0.15, 0.8, -22, R, 33.0)
    tilted_slab(mbr, 82.6, 3.7, 1.2, 0.6, 0.13, 0.5, 35, D, 35.0)
    for (s, lat, y, r, sd) in [(86.6, 3.2, TOP - 0.18, 0.18, 41), (87.2, 3.7, TOP - 0.2, 0.13, 43), (79.4, 3.2, TOP - 0.38, 0.15, 45)]:
        float_rock(mbr, s, lat, y, r, r * 0.85, sd, depth=r * 1.4)
    out['plaza_remnants'] = mbr
    return out, info

def to_blender(name, mb, col):
    me = bpy.data.meshes.new(name)
    verts = [(p[0], p[2], p[1]) for p in mb.V]
    faces = [(a, c, b) for (a, b, c) in mb.T]
    me.from_pydata(verts, [], faces); me.update()
    ob = bpy.data.objects.get(name)
    if ob: bpy.data.objects.remove(ob)
    ob = bpy.data.objects.new(name, me); col.objects.link(ob)
    return ob

res, info = build()
col = bpy.data.collections.get('C2Plaza') or bpy.data.collections.new('C2Plaza')
if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
names = []
for name, mb in res.items():
    to_blender('C2P_' + name, mb, col); names.append('C2P_' + name)

_out = {}
for _n in names:
    _me = bpy.data.objects[_n].data
    _bm = bmesh.new(); _bm.from_mesh(_me)
    bmesh.ops.remove_doubles(_bm, verts=_bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(_bm, faces=_bm.faces)
    bmesh.ops.triangulate(_bm, faces=_bm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    _bm.to_mesh(_me); _bm.free(); _me.update()
    _V = [(v.co.x, v.co.z, v.co.y) for v in _me.vertices]
    _T = []
    for _p in _me.polygons:
        _vs = list(_p.vertices)
        for _i in range(1, len(_vs) - 1): _T.append((_vs[0], _vs[_i + 1], _vs[_i]))
    _out[_n.replace('C2P_', '')] = {'v': _V, 't': _T}
_out['info'] = {'vents': info}
json.dump(_out, open(SCR + r'\course2_plaza.json', 'w'))
print('exported', {k: len(v['t']) for k, v in _out.items() if k != 'info'}, info)
