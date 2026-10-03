# やみのもりの重力エリア用：ひょろ長いヒトヨタケ（溶けかけ）。軸が重力の球を貫き、傘のふちが黒くとろけてインクのしずくが垂れる。
# 頂点カラー1枚マテリアル。原点は軸の根元、単位はm。書き出し：Unity (x,y,z)=(x,z,y)
# 使い方：Blenderで exec(open(r'...\Tools\inkcap.py', encoding='utf-8').read())。JSON は OUT に出る
import bpy, json, math, random
from mathutils import Vector, noise
OUT = globals().get('OUT', r'D:\UnityProject\FLUFF\Tools\inkcap.json')
col = bpy.data.collections.get('InkCap') or bpy.data.collections.new('InkCap')
if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)

CREAM = (0.95, 0.93, 0.88); TIPBROWN = (0.70, 0.58, 0.47); SCALE = (0.80, 0.74, 0.66)
GRAY = (0.55, 0.52, 0.58); INK = (0.10, 0.07, 0.14); INKHI = (0.22, 0.16, 0.30)
GILL = (0.16, 0.13, 0.19); STEM = (0.93, 0.92, 0.89); STEMB = (0.80, 0.78, 0.76)

def lerp3(a, b, t): t = max(0.0, min(1.0, t)); return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))
def ss(a, b, x): t = max(0.0, min(1.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)

def inkcap(name, seed, stem_h, cap_h, cap_r, stem_r=0.16, ndrips=7):
    random.seed(seed); off = Vector((seed * 4.3, seed * 2.1, 0))
    V = []; C = []; F = []
    def add(p, c): V.append(tuple(p)); C.append(c); return len(V) - 1
    SEG = 40

    # ---- 軸：ゆるいS字、根元が少しふくらむ ----
    def stem_center(z):
        u = z / stem_h
        return Vector((0.18 * math.sin(u * 2.6 + seed) * u, 0.12 * math.sin(u * 1.9 + seed * 2) * u, z))
    SS = 10; NZ = 22
    rings = []
    for k in range(NZ + 1):
        z = stem_h * k / NZ; u = k / NZ
        r = stem_r * (1.0 + 0.55 * (1 - ss(0.0, 0.12, u)) - 0.15 * u)
        c0 = stem_center(z)
        ring = []
        for s in range(SS):
            a = s / SS * math.tau
            rr = r * (1 + 0.06 * noise.noise(off + Vector((math.cos(a), math.sin(a), z * 0.8))))
            ring.append(add(c0 + Vector((rr * math.cos(a), rr * math.sin(a), 0)), lerp3(STEMB, STEM, ss(0, 0.25, u))))
        rings.append(ring)
    for k in range(NZ):
        A, B = rings[k], rings[k + 1]
        for s in range(SS): F.append((A[s], A[(s + 1) % SS], B[(s + 1) % SS], B[s]))
    bot = add(stem_center(0) - Vector((0, 0, 0.05)), STEMB)
    for s in range(SS): F.append((rings[0][(s + 1) % SS], rings[0][s], bot))
    top = stem_center(stem_h)

    # ---- 傘：とんがった釣鐘。外側→ふち（めくれて溶ける）→内側のヒダ ----
    z0 = stem_h - 0.15   # ふちの基準高さ（軸の先は傘の中に隠れる）
    def droop(a):        # ふちの垂れ下がり（溶けてる所ほど下がる）
        return 0.32 * max(0.0, noise.noise(off + Vector((math.cos(a) * 2.4, math.sin(a) * 2.4, 7)))) + 0.06 * max(0.0, math.sin(a * 7 + seed))
    def rimr(a):
        return 1.0 + 0.07 * noise.noise(off + Vector((math.cos(a) * 2.0, math.sin(a) * 2.0, 3)))
    NT = 16
    TR = 0.22   # てっぺんの丸み（この範囲だけ放物線でなだらかに）
    def tipf(t): return 1 - t if t >= TR else 1 - TR / 2 - t * t / (2 * TR)
    outer = []
    for k in range(NT + 1):
        t = (k / NT) ** 1.25   # てっぺん付近を細かく（丸みを出すため）
        ring = []
        for s in range(SEG if k > 0 else 1):
            a = s / SEG * math.tau
            r = cap_r * (t ** 0.8) * rimr(a)
            # ささくれ（ヒトヨタケの鱗片）：上のほうに軽い段々
            scale = 0.035 * cap_r * max(0.0, noise.noise(off + Vector((math.cos(a) * 4, math.sin(a) * 4, t * 9)))) * ss(0.15, 0.4, t) * (1 - ss(0.7, 0.9, t))
            r += scale
            z = z0 + cap_h * tipf(t) - 0.06 * cap_h * math.sin(t * math.pi) - droop(a) * ss(0.75, 1.0, t)
            if k == 0: r = 0; z = z0 + cap_h * tipf(0)
            # 色：てっぺん茶色→クリーム→下から灰色→ふちは真っ黒なインク
            streak = 0.22 * max(0.0, noise.noise(off + Vector((math.cos(a) * 9, math.sin(a) * 9, t * 1.5))))   # 上へ伸びる黒いすじ
            ink = ss(0.62, 0.93, t + 0.12 * noise.noise(off + Vector((math.cos(a) * 3, math.sin(a) * 3, 11))) + droop(a) + streak)
            c = lerp3(TIPBROWN, CREAM, ss(0.0, 0.22, t))
            if scale > 0.012 * cap_r: c = lerp3(c, SCALE, 0.6)
            c = lerp3(c, GRAY, ss(0.45, 0.7, t)); c = lerp3(c, INK, ink)
            ring.append(add((top.x * (1 - t) + r * math.cos(a), top.y * (1 - t) + r * math.sin(a), z), c))
        outer.append(ring)
    for s in range(SEG): F.append((outer[0][0], outer[1][s], outer[1][(s + 1) % SEG]))
    for k in range(1, NT):
        A, B = outer[k], outer[k + 1]
        for s in range(SEG): F.append((A[s], B[s], B[(s + 1) % SEG], A[(s + 1) % SEG]))
    # ふち：外へめくれて上へ少しカール（絵の「くるっ」）→ 内側へ折り返す
    lips = []
    for (dr, dz, cc) in [(0.10, 0.05, INK), (0.13, 0.16, INKHI), (0.07, 0.10, INK)]:
        ring = []
        for s in range(SEG):
            a = s / SEG * math.tau
            curl = 0.6 + 0.8 * max(0.0, noise.noise(off + Vector((math.cos(a) * 2.5, math.sin(a) * 2.5, 13))))
            p = Vector(V[outer[-1][s]])
            ring.append(add(p + Vector((math.cos(a) * dr * curl * cap_r, math.sin(a) * dr * curl * cap_r, dz * curl)), cc))
        lips.append(ring)
    prev = outer[-1]
    for ring in lips:
        for s in range(SEG): F.append((prev[s], ring[s], ring[(s + 1) % SEG], prev[(s + 1) % SEG]))
        prev = ring
    # 内側（ヒダ）：ふちの少し内側から、軸の先に向かって上へすぼむ。放射状のヒダは凸凹で表現
    inner = []
    NI = 6
    for k in range(NI + 1):
        t = k / NI      # 0=ふち 1=軸
        ring = []
        for s in range(SEG):
            a = s / SEG * math.tau
            p_r = Vector(V[outer[-1][s]])
            rr = (cap_r * 0.92 * (1 - t) + stem_r * 1.2 * t) * (1 + (0.025 if s % 2 else -0.025) * (1 - t))
            zz = (p_r.z - 0.02) * (1 - t) + (z0 + cap_h * 0.55) * t
            ring.append(add((top.x + rr * math.cos(a), top.y + rr * math.sin(a), zz), lerp3(INK, GILL, ss(0, 0.4, t))))
        inner.append(ring)
    for s in range(SEG): F.append((prev[s], inner[0][s], inner[0][(s + 1) % SEG], prev[(s + 1) % SEG]))
    for k in range(NI):
        A, B = inner[k], inner[k + 1]
        for s in range(SEG): F.append((A[s], B[s], B[(s + 1) % SEG], A[(s + 1) % SEG]))
    cap_in = add((top.x, top.y, z0 + cap_h * 0.58), GILL)
    for s in range(SEG): F.append((inner[-1][s], cap_in, inner[-1][(s + 1) % SEG]))

    # ---- しずく：ふちから垂れるインク（付け根は傘の裏にぴったり沿わせる→ぷっくり玉） ----
    from mathutils.bvhtree import BVHTree
    shell = BVHTree.FromPolygons([Vector(p) for p in V], F)   # ここまでの傘（＋軸）
    def hug(x, y, zref):
        """(x,y) の真上にある傘の裏面のすぐ内側の高さ。外側の面との間（膜の厚みの中）に収める"""
        under = shell.ray_cast(Vector((x, y, zref - 1.0)), Vector((0, 0, 1)))[0]
        if under is None: return None
        over = shell.ray_cast(Vector((x, y, under.z + 0.0001)), Vector((0, 0, 1)))[0]
        if over is None or over.z - under.z < 0.004: return under.z - 0.003   # 上に面がない（ふちの一番外）：裏面のすぐ下にくっつける
        return min(under.z + 0.006, (under.z + over.z) * 0.5)
    drips = []
    ph = random.uniform(0, math.tau)
    angs = [ph + (i + random.uniform(-0.3, 0.3)) / ndrips * math.tau for i in range(ndrips)]   # 重ならないように散らす
    for a in angs:
        a = a % math.tau
        s = int(round(a / math.tau * SEG)) % SEG
        po = Vector(V[outer[-1][s]])
        base = po - Vector((math.cos(a), math.sin(a), 0)) * 0.04
        L = random.uniform(0.25, 0.8)
        rad = random.uniform(0.07, 0.12)
        DS = 16; prof = []
        NP = 16
        for i in range(NP + 1):
            u = i / NP
            r = rad * (1.3 * (1 - u) ** 2.5 + 0.3 + ss(0.5, 0.8, u) * math.sin(min(1, max(0, u - 0.5) / 0.5) * math.pi) * 0.9)   # 付け根はなだらかに細く→先がぷっくり
            r = max(r, rad * 0.22 * (1 - ss(0.95, 1.0, u)))
            prof.append((r, -L * u, u))
        # 付け根の輪：傘の裏面に沿った高さ（見つからない所はふちの高さ）
        r0 = prof[0][0]
        top_z = []
        for j in range(DS):
            b = j / DS * math.tau
            z = hug(base.x + r0 * math.cos(b), base.y + r0 * math.sin(b), po.z)
            top_z.append(z if z is not None else po.z - 0.01)
        zc = hug(base.x, base.y, po.z); zc = zc if zc is not None else po.z - 0.01
        z_start = min(top_z)   # 本体はここから下へ
        rr = []
        for (r, dz, u) in prof:
            ring = []
            w = 1 - ss(0.0, 0.2, u)   # 付け根付近だけ裏面の形に寄せて、だんだん丸い輪に戻す
            for j in range(DS):
                b = j / DS * math.tau
                zz = z_start + dz + (top_z[j] - z_start) * w
                ring.append(add((base.x + r * math.cos(b), base.y + r * math.sin(b), zz), INKHI if dz < -L * 0.7 and j in (2, 3, 4) else INK))
            rr.append(ring)
        cap_top = add((base.x, base.y, zc), INK)   # 上のふた（膜の中に隠れる）
        for j in range(DS): F.append((rr[0][(j + 1) % DS], rr[0][j], cap_top))
        for k in range(len(rr) - 1):
            A, B = rr[k], rr[k + 1]
            for j in range(DS): F.append((A[j], A[(j + 1) % DS], B[(j + 1) % DS], B[j]))
        tip = add((base.x, base.y, z_start - L * 1.03), INK)
        for j in range(DS): F.append((rr[-1][j], rr[-1][(j + 1) % DS], tip))
        drips.append((a, (base.x, z_start, base.y), L))

    me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
    me.clear_geometry(); me.from_pydata(V, [], F); me.update()
    ca = me.color_attributes.get('Col') or me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
    for i, c in enumerate(C): ca.data[i].color = (c[0], c[1], c[2], 1)
    for p in me.polygons: p.use_smooth = True
    o = bpy.data.objects.get(name)
    if o is None: o = bpy.data.objects.new(name, me); col.objects.link(o)
    return o, C, drips

# (名前, seed, 軸の高さ, 傘の高さ, 傘の半径)
specs = [('InkCap_A', 1, 4.6, 3.0, 2.2), ('InkCap_B', 2, 4.4, 2.7, 2.0), ('InkCap_Tall', 3, 7.6, 3.4, 2.5)]
out = {}
for i, sp in enumerate(specs):
    o, C, drips = inkcap(*sp)
    o.location = (i * 7.0, 30, 0)
    me = o.data; me.calc_loop_triangles()
    out[sp[0]] = {'v': [(v.co.x, v.co.z, v.co.y) for v in me.vertices],
                  'n': [(v.normal.x, v.normal.z, v.normal.y) for v in me.vertices],
                  'c': [tuple(c) for c in C],
                  't': [(t.vertices[0], t.vertices[2], t.vertices[1]) for t in me.loop_triangles],
                  'drips': drips}
json.dump(out, open(OUT, 'w'))
print({k: (len(v['v']), len(v['t'])) for k, v in out.items()})
