# 道のふちの飾り（草のふさ・小さな花・苔の小石・垂れるツタ）。Blender では原点＝置く点（道のふちの地面）、+Z 上。
# ツタだけは原点＝道のふちの上の角、-Z へ垂れる、+Y が道の外側。
# 書き出し：Unity (x,y,z)=(x,z,y)、三角形ごとに材質番号つき。材質名は MATS の順。
import bpy, bmesh, json, math, random
from mathutils import Vector, Matrix, noise

SCR = r'C:\Users\tejas\AppData\Local\Temp\claude\D--Claude-Workspace-ClaudeDiscord\e6d24fd8-b7a4-4eeb-90a0-47b449e1d858\scratchpad'
MATS = ['Grass', 'GrassDark', 'Stem', 'PetalWhite', 'PetalYellow', 'PetalPink', 'FlowerCenter', 'Pebble', 'Moss', 'Vine', 'Leaf']
random.seed(12)

class Builder:
    def __init__(self): self.V = []; self.F = []; self.M = []
    def v(self, p): self.V.append(Vector(p)); return len(self.V) - 1
    def f(self, idx, mat): self.F.append(tuple(idx)); self.M.append(MATS.index(mat))

def blade(b, base, h, w, lean_dir, lean, mat, segs=4, twist=0.0):
    # 先細りの曲がった草の葉（両面で見えるよう、Unity側は両面描画の材質にする）
    side = Vector((-lean_dir.y, lean_dir.x, 0)).normalized()
    rows = []
    for i in range(segs + 1):
        t = i / segs
        c = base + Vector((0, 0, h * t)) + lean_dir * lean * h * t * t
        ww = w * (1 - t) ** 0.9
        sd = (Matrix.Rotation(twist * t, 3, 'Z') @ side)
        rows.append((b.v(c - sd * ww * 0.5), b.v(c + sd * ww * 0.5)))
    for i in range(segs):
        a0, a1 = rows[i]; b0, b1 = rows[i + 1]
        if i == segs - 1:
            tip = b.v((b.V[b0] + b.V[b1]) * 0.5)
            b.f((a0, a1, tip), mat)
        else:
            b.f((a0, a1, b1, b0), mat)

def grass(n, hmin, hmax, spread):
    b = Builder()
    for i in range(n):
        ang = random.uniform(0, math.tau); r = random.uniform(0, spread)
        base = Vector((math.cos(ang) * r, math.sin(ang) * r, 0))
        d = (base.normalized() if r > 0.01 else Vector((1, 0, 0)))
        d = (Matrix.Rotation(random.uniform(-0.6, 0.6), 3, 'Z') @ d).normalized()
        blade(b, base, random.uniform(hmin, hmax), random.uniform(0.025, 0.04), d, random.uniform(0.25, 0.6),
              'Grass' if random.random() < 0.65 else 'GrassDark', twist=random.uniform(-0.5, 0.5))
    return b

def tube(b, pts, r0, r1, mat, seg=5):
    rings = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        a = t.orthogonal().normalized(); c = t.cross(a).normalized()
        r = r0 + (r1 - r0) * i / (len(pts) - 1)
        rings.append([b.v(p + (a * math.cos(k * math.tau / seg) + c * math.sin(k * math.tau / seg)) * r) for k in range(seg)])
    for i in range(len(rings) - 1):
        for k in range(seg):
            k2 = (k + 1) % seg
            b.f((rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]), mat)
    # 先端を閉じる
    tip = b.v(pts[-1] + (pts[-1] - pts[-2]).normalized() * r1)
    for k in range(seg): b.f((rings[-1][k], rings[-1][(k + 1) % seg], tip), mat)

def flower_head(b, c, up, size, petal_mat):
    a = up.orthogonal().normalized(); bb = up.cross(a).normalized()
    n = 5
    for k in range(n):
        ang = k * math.tau / n
        d = a * math.cos(ang) + bb * math.sin(ang)
        side = up.cross(d).normalized()
        # 花びら：少し反った菱形（両面）
        p0 = c
        p1 = c + d * size * 0.55 + side * size * 0.28 + up * size * 0.08
        p2 = c + d * size * 1.0 + up * size * 0.18
        p3 = c + d * size * 0.55 - side * size * 0.28 + up * size * 0.08
        b.f((b.v(p0), b.v(p1), b.v(p2), b.v(p3)), petal_mat)
    # 真ん中：小さな半球
    seg = 6; rr = size * 0.22
    top = b.v(c + up * rr * 0.9)
    ring = [b.v(c + up * rr * 0.2 + (a * math.cos(k * math.tau / seg) + bb * math.sin(k * math.tau / seg)) * rr) for k in range(seg)]
    for k in range(seg): b.f((ring[k], ring[(k + 1) % seg], top), 'FlowerCenter')

def flowers(petal_mat, n=3):
    b = Builder()
    for i in range(n):
        ang = random.uniform(0, math.tau); r = random.uniform(0.02, 0.09)
        base = Vector((math.cos(ang) * r, math.sin(ang) * r, 0))
        h = random.uniform(0.16, 0.3)
        lean = Vector((math.cos(ang), math.sin(ang), 0)) * random.uniform(0.02, 0.07)
        pts = [base + Vector((0, 0, h * t)) + lean * t * t for t in (0, 0.33, 0.66, 1.0)]
        tube(b, pts, 0.008, 0.006, 'Stem', seg=4)
        up = (pts[-1] - pts[-2]).normalized().lerp(Vector((0, 0, 1)), 0.4).normalized()
        flower_head(b, pts[-1], up, random.uniform(0.06, 0.085), petal_mat)
    # 足元に短い草を少し
    g = grass(5, 0.06, 0.11, 0.07)
    off = len(b.V); b.V += g.V; b.F += [tuple(i + off for i in f) for f in g.F]; b.M += g.M
    return b

def pebbles():
    b = Builder()
    for i in range(random.randint(2, 3)):
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1.0)
        s = random.uniform(0.06, 0.13); sq = random.uniform(0.5, 0.7)
        c = Vector((random.uniform(-0.12, 0.12), random.uniform(-0.12, 0.12), 0))
        seed = random.uniform(0, 50)
        idx = {}
        for v in bm.verts:
            d = v.co.copy(); d *= 1 + noise.noise(d * 1.3 + Vector((seed, 0, 0))) * 0.18
            p = c + Vector((d.x * s, d.y * s * random.uniform(0.95, 1.05), max(d.z, -0.3) * s * sq + s * sq * 0.25))
            idx[v.index] = b.v(p)
        for f in bm.faces:
            n = f.normal
            b.f([idx[v.index] for v in f.verts], 'Moss' if n.z > 0.55 + noise.noise(f.calc_center_median() * 3) * 0.2 else 'Pebble')
        bm.free()
    return b

def vines():
    # 道のふちから外へ少し出て、下へ垂れるツタ（数本）＋小さな葉
    b = Builder()
    for i in range(random.randint(3, 4)):
        x = random.uniform(-0.25, 0.25)
        L = random.uniform(0.35, 0.85)
        pts = []
        for k in range(8):
            t = k / 7
            pts.append(Vector((x + math.sin(t * 3 + i) * 0.04, 0.03 + 0.06 * math.sin(min(t * 4, 1) * math.pi * 0.5) + 0.02 * t, -L * t)))
        tube(b, pts, 0.009, 0.005, 'Vine', seg=4)
        for k in range(1, 7):
            t = k / 7
            p = pts[k]
            side = 1 if k % 2 == 0 else -1
            d = Vector((side * 0.7, 0.6, -0.2)).normalized()
            sz = 0.065 * (1 - t * 0.35)
            n = Vector((0, 1, 0.3)).normalized()
            q = d.cross(n).normalized()
            b.f((b.v(p), b.v(p + d * sz * 0.5 + q * sz * 0.35), b.v(p + d * sz), b.v(p + d * sz * 0.5 - q * sz * 0.35)), 'Leaf')
    return b

def moss(rad, h, stretch, seed):
    # こんもりした苔のかたまり：ふちはでこぼこ、上はもこもこ。外周は地面より少し下（浮かない）
    b = Builder(); off = Vector((seed, seed * 0.37, 0))
    SEG = 22; NR = 6
    rings = []
    for i in range(1, NR + 1):
        t = i / NR
        ring = []
        for k in range(SEG):
            a = k / SEG * math.tau
            edge = 1 + 0.22 * noise.noise(off + Vector((math.cos(a) * 1.4, math.sin(a) * 1.4, 0)))
            r = rad * t * edge
            x, y = math.cos(a) * r * stretch, math.sin(a) * r
            z = h * (1 - t * t) ** 0.6 * (1 + 0.35 * noise.noise(off + Vector((x * 9, y * 9, 3)))) if i < NR else -0.012
            ring.append(b.v((x, y, z)))
        rings.append(ring)
    c = b.v((0, 0, h * 1.05))
    for k in range(SEG): b.f((c, rings[0][k], rings[0][(k + 1) % SEG]), 'Moss')
    for i in range(NR - 1):
        for k in range(SEG):
            k2 = (k + 1) % SEG
            b.f((rings[i][k], rings[i + 1][k], rings[i + 1][k2], rings[i][k2]), 'Moss')
    return b

PROPS = {
    'GrassSmall': grass(7, 0.12, 0.22, 0.06),
    'GrassLarge': grass(13, 0.22, 0.42, 0.1),
    'FlowersWhite': flowers('PetalWhite'),
    'FlowersYellow': flowers('PetalYellow'),
    'FlowersPink': flowers('PetalPink'),
    'Pebbles': pebbles(),
    'Vines': vines(),
    'MossSmall': moss(0.14, 0.05, 1.0, 3.0),
    'MossLarge': moss(0.26, 0.075, 1.4, 7.0),
}

col = bpy.data.collections.get('Deco') or bpy.data.collections.new('Deco')
if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
out = {'mats': MATS, 'props': {}}
x = 0.0
for name, b in PROPS.items():
    on = 'Deco_' + name
    o = bpy.data.objects.get(on)
    if o: bpy.data.objects.remove(o)
    me = bpy.data.meshes.new(on); me.from_pydata([tuple(v) for v in b.V], [], b.F); me.update()
    for p, mi in zip(me.polygons, b.M): p.material_index = mi
    o = bpy.data.objects.new(on, me); col.objects.link(o); o.location = (x, -6, 0); x += 0.8
    # 三角形にして書き出し（Unity座標、巻き順反転）
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces)
    V = [(v.co.x, v.co.z, v.co.y) for v in bm.verts]
    T = []; S = []
    for f in bm.faces:
        vs = [v.index for v in f.verts]; T.append((vs[0], vs[2], vs[1])); S.append(f.material_index)
    bm.free()
    out['props'][name] = {'v': V, 't': T, 's': S}
json.dump(out, open(SCR + r'\deco.json', 'w'))
print({k: len(v['t']) for k, v in out['props'].items()})
