# まもののすのトゲ床の見た目：イバラのしげみ（葉っぱなし。緑のつるがからまり合って、大きなトゲがびっしり）。
# 1.5m×1.5m の中に収まる（トゲ床の当たり判定と同じ大きさ）。高さ 0.5m くらい。原点は地面の中心。
# 頂点カラー1枚（sRGB。Unity では FuwaCourse/InkCap）。Unity 座標 (x, y, z) で書き出し、表向きは法線ヒントでそろえる
# 使い方：Blenderで exec(open(r'...\Tools\bramble.py', encoding='utf-8').read())
import bpy, json, math, random
from mathutils import Vector
OUT = globals().get('OUT', r'D:\UnityProject\FLUFF\Tools\bramble.json')

STEM = (0.3, 0.47, 0.25); STEM2 = (0.37, 0.55, 0.29); STEM3 = (0.24, 0.39, 0.22); THORN = (0.96, 0.91, 0.8); THORN_DARK = (0.78, 0.66, 0.5)
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
    nst = 18
    vines = []
    for s in range(nst):
        ang = 2 * math.pi * (s + rnd.uniform(-0.4, 0.4)) / nst
        d = Vector((math.cos(ang), 0, math.sin(ang)))
        start = d * rnd.uniform(0.0, 0.18)
        reach = rnd.uniform(0.32, 0.68); hgt = rnd.uniform(0.2, 0.52)
        side = Vector((-d.z, 0, d.x)) * rnd.uniform(-0.9, 0.9)   # 横へ大きくそれて、ほかのつるとからまる
        n = 14; pts = []
        for k in range(n):
            t = k / (n - 1)
            p = start + (d + side * t) * reach * t + Vector((0, hgt * math.sin(t * math.pi * 0.85) + 0.02, 0))
            if 0 < k < n - 1:   # カクカクと折れ曲がる
                p += Vector((rnd.uniform(-1, 1), rnd.uniform(-0.5, 0.5), rnd.uniform(-1, 1))) * 0.03
            pts.append(p)
        vines.append(pts)
        col = rnd.choice((STEM, STEM2, STEM3))
        tube(m, pts, rnd.uniform(0.026, 0.036), 0.009, col, sides=5)
        # トゲ：ほぼ全部の節から、外向き・上向きに大きく鋭く（1節に1〜2本）
        for k in range(1, n - 1):
            for _ in range(1 if rnd.random() < 0.5 else 2):
                p = pts[k]; t = (pts[k + 1] - pts[k - 1]).normalized(); a, b = frame_of(t)
                ph = rnd.uniform(0, 6.28); out = (a * math.cos(ph) + b * math.sin(ph))
                if out.y < -0.2: out = -out
                dd = (out * 0.9 + t * 0.3).normalized()
                ln = rnd.uniform(0.09, 0.15)
                cone(m, p + out * 0.018, dd, ln, rnd.uniform(0.018, 0.024), THORN if rnd.random() < 0.75 else THORN_DARK)
        # 細い枝分かれ（からまり感）
        if rnd.random() < 0.6:
            k0 = rnd.randint(4, 8); p0 = pts[k0]; t0 = (pts[k0 + 1] - pts[k0 - 1]).normalized()
            br = Vector((rnd.uniform(-1, 1), rnd.uniform(0.2, 0.8), rnd.uniform(-1, 1))).normalized()
            bpts = [p0 + (br * 0.6 + t0 * 0.4).normalized() * (0.05 * i) + Vector((0, -0.004 * i * i, 0)) for i in range(6)]
            tube(m, bpts, 0.016, 0.006, col, sides=5)
            for i in range(1, 5):
                q = bpts[i]; tt = (bpts[i + 1] - bpts[i - 1]).normalized(); a, b = frame_of(tt)
                ph = rnd.uniform(0, 6.28); out = (a * math.cos(ph) + b * math.sin(ph))
                if out.y < -0.2: out = -out
                cone(m, q + out * 0.01, (out * 0.9 + tt * 0.3).normalized(), rnd.uniform(0.06, 0.1), 0.014, THORN)
    # 真ん中から突き出す大トゲ（近づくと危ない感）
    for i in range(7):
        ang = rnd.uniform(0, 6.28); rr = rnd.uniform(0.05, 0.3)
        p = Vector((math.cos(ang) * rr, rnd.uniform(0.18, 0.32), math.sin(ang) * rr))
        dd = Vector((math.cos(ang) * rnd.uniform(0.3, 0.8), 1, math.sin(ang) * rnd.uniform(0.3, 0.8))).normalized()
        cone(m, p, dd, rnd.uniform(0.2, 0.28), 0.035, THORN)
    # 根もと：太いつるのかたまり
    for i in range(10):
        ang = rnd.uniform(0, 6.28); d = Vector((math.cos(ang), 0, math.sin(ang)))
        pts = [d * (0.04 * k) + Vector((0, 0.04 + 0.05 * math.sin(k * 0.6) , 0)) + Vector((-d.z, 0, d.x)) * (0.03 * math.sin(k)) for k in range(6)]
        tube(m, pts, 0.04, 0.02, STEM3, sides=5)
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
