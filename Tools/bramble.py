# まもののすのトゲ床の見た目：イバラのしげみ（低いアーチ状のつるに白っぽいトゲ、葉っぱ少し、赤い実）。
# 1.5m×1.5m の中に収まる（トゲ床の当たり判定と同じ大きさ）。高さ 0.5m くらい。原点は地面の中心。
# 頂点カラー1枚（sRGB。Unity では FuwaCourse/InkCap）。Unity 座標 (x, y, z) で書き出し、表向きは法線ヒントでそろえる
# 使い方：Blenderで exec(open(r'...\Tools\bramble.py', encoding='utf-8').read())
import bpy, json, math, random
from mathutils import Vector
OUT = globals().get('OUT', r'D:\UnityProject\FLUFF\Tools\bramble.json')

STEM = (0.4, 0.27, 0.38); STEM2 = (0.5, 0.33, 0.42); THORN = (0.95, 0.9, 0.82)
LEAF = (0.32, 0.47, 0.32); LEAF2 = (0.4, 0.56, 0.36); BERRY = (0.82, 0.2, 0.36); BERRY_HI = (1, 0.75, 0.8)

class Mesh:
    def __init__(self): self.V = []; self.C = []; self.T = []; self.H = []
    def v(self, p, c): self.V.append(Vector(p)); self.C.append(c); return len(self.V) - 1
    def tri(self, a, b, c, h): self.T.append((a, b, c)); self.H.append(Vector(h))
    def quad(self, a, b, c, d, h): self.tri(a, b, c, h); self.tri(a, c, d, h)
    def out(self):
        T = []
        for (a, b, c), h in zip(self.T, self.H):
            n = (self.V[b] - self.V[a]).cross(self.V[c] - self.V[a])
            T.append((a, b, c) if n.dot(h) >= 0 else (a, c, b))
        return {'v': [tuple(p) for p in self.V], 'c': self.C, 't': T}

def frame_of(t):
    a = t.cross(Vector((0, 1, 0)))
    if a.length < 1e-3: a = t.cross(Vector((1, 0, 0)))
    a.normalize(); return a, t.cross(a)

def tube(m, pts, r0, r1, col, sides=6):
    n = len(pts); base = len(m.V); P = [Vector(p) for p in pts]
    for k in range(n):
        t = (P[min(k + 1, n - 1)] - P[max(k - 1, 0)]).normalized(); a, b = frame_of(t)
        rr = r0 + (r1 - r0) * k / (n - 1)
        for i in range(sides):
            ph = 2 * math.pi * i / sides
            m.v(P[k] + (a * math.cos(ph) + b * math.sin(ph)) * rr, col)
    for k in range(n - 1):
        for i in range(sides):
            i1 = (i + 1) % sides; a0 = base + k * sides + i
            m.quad(a0, base + k * sides + i1, base + (k + 1) * sides + i1, a0 + sides, (m.V[a0] + m.V[a0 + sides]) * 0.5 - (P[k] + P[k + 1]) * 0.5)
    tip = m.v(P[-1] + (P[-1] - P[-2]).normalized() * r1 * 2, col)
    for i in range(sides): m.tri(base + (n - 1) * sides + i, base + (n - 1) * sides + (i + 1) % sides, tip, P[-1] - P[-2])

def cone(m, base_p, d, length, r, col, sides=5):
    a, b = frame_of(d); ring = []
    for i in range(sides):
        ph = 2 * math.pi * i / sides; ring.append(m.v(base_p + (a * math.cos(ph) + b * math.sin(ph)) * r, col))
    tip = m.v(base_p + d * length, col)
    for i in range(sides): m.tri(ring[i], ring[(i + 1) % sides], tip, (m.V[ring[i]] - base_p))

def sphere(m, c, r, col_fn, seg=8, rings=6):
    base = len(m.V)
    for j in range(rings + 1):
        th = math.pi * j / rings
        for i in range(seg + 1):
            ph = 2 * math.pi * i / seg
            u = Vector((math.sin(th) * math.cos(ph), math.cos(th), math.sin(th) * math.sin(ph)))
            m.v(c + u * r, col_fn(u))
    for j in range(rings):
        for i in range(seg):
            a = base + j * (seg + 1) + i; b = a + seg + 1
            m.quad(a, b, b + 1, a + 1, (m.V[a] + m.V[b + 1]) * 0.5 - c)

def leaf(m, p, d, size, col):
    a, b = frame_of(d); up = Vector((0, 1, 0))
    side = d.cross(up).normalized() if d.cross(up).length > 1e-3 else a
    tip = p + d * size; mid = p + d * size * 0.5 + Vector((0, size * 0.12, 0))
    i0 = m.v(p, col); i1 = m.v(mid + side * size * 0.28, col); i2 = m.v(tip, col); i3 = m.v(mid - side * size * 0.28, col)
    m.quad(i0, i1, i2, i3, (0, 1, 0))

def bramble(seed):
    rnd = random.Random(seed); m = Mesh()
    nst = 15
    for s in range(nst):
        ang = 2 * math.pi * (s + rnd.uniform(-0.3, 0.3)) / nst
        d = Vector((math.cos(ang), 0, math.sin(ang)))
        start = d * rnd.uniform(0.0, 0.2)
        reach = rnd.uniform(0.3, 0.68); hgt = rnd.uniform(0.18, 0.5)
        side = Vector((-d.z, 0, d.x)) * rnd.uniform(-0.6, 0.6)
        pts = []
        for k in range(11):
            t = k / 10
            p = start + (d + side * t) * reach * t + Vector((0, hgt * math.sin(t * math.pi * 0.85) + 0.02, 0))
            pts.append(p)
        col = STEM if rnd.random() < 0.6 else STEM2
        tube(m, pts, rnd.uniform(0.032, 0.045), 0.012, col)
        # トゲ：つるに沿って外向き・上向きに
        for k in range(1, 10):
            if rnd.random() < 0.35: continue
            p = pts[k]; t = (pts[k + 1] - pts[k - 1]).normalized(); a, b = frame_of(t)
            ph = rnd.uniform(0, 6.28); out = (a * math.cos(ph) + b * math.sin(ph))
            if out.y < -0.3: out = -out
            dd = (out * 0.85 + t * 0.35).normalized()
            cone(m, p + out * 0.025, dd, rnd.uniform(0.06, 0.09), 0.016, THORN)
        # 葉っぱ・実
        for k in (1, 2, 3, 4, 5, 6, 7, 8):
            if rnd.random() < 0.85:
                p = pts[k]; t = (pts[k + 1] - pts[k - 1]).normalized()
                ld = (Vector((-t.z, 0, t.x)) * rnd.choice((-1, 1)) + Vector((0, 0.3, 0))).normalized()
                leaf(m, p, ld, rnd.uniform(0.11, 0.16), LEAF if rnd.random() < 0.5 else LEAF2)
        if rnd.random() < 0.3:
            p = pts[rnd.randint(4, 8)] + Vector((0, 0.035, 0))
            sphere(m, p, 0.035, lambda u: BERRY_HI if (u.y > 0.6 and u.x > 0.2) else BERRY)
    # 根もとの葉っぱのこんもり（クモの足っぽく見えないように、植物のかたまり感を出す）
    for i in range(46):
        ang = rnd.uniform(0, 6.28); rr = rnd.uniform(0.0, 0.45)
        p = Vector((math.cos(ang) * rr, rnd.uniform(0.04, 0.26) * (1 - rr * 1.2) + 0.03, math.sin(ang) * rr))
        ld = (Vector((math.cos(ang), rnd.uniform(0.3, 0.9), math.sin(ang)))).normalized()
        leaf(m, p, ld, rnd.uniform(0.13, 0.2), LEAF if rnd.random() < 0.5 else LEAF2)
    return m

out = {'Bramble_A': bramble(5).out(), 'Bramble_B': bramble(9).out()}
json.dump(out, open(OUT, 'w'))
coll = bpy.data.collections.get('Bramble') or bpy.data.collections.new('Bramble')
if coll.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(coll)
for o in list(coll.objects): bpy.data.objects.remove(o)
x = 0
for name, d in out.items():
    me = bpy.data.meshes.new(name); me.from_pydata([(p[0], p[2], p[1]) for p in d['v']], [], [tuple(t) for t in d['t']]); me.update()
    ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
    for i, c in enumerate(d['c']): ca.data[i].color = (c[0] ** 2.2, c[1] ** 2.2, c[2] ** 2.2, 1)
    o = bpy.data.objects.new(name, me); coll.objects.link(o); o.location = (300 + x, 320, 0); x += 2
print({k: len(v['t']) for k, v in out.items()})
