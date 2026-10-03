# やみのもりのデカいクモの巣の飾り：めちゃくちゃデフォルメしたかわいいクモ（動かない・当たり判定なし）。
# 巣の面（Blender の y=0 の面）に足先をつけて止まっていて、体は手前（-y）へふくらむ。頭が上(+z)。
# 頂点カラー1枚マテリアル（sRGBで書いた色。Unity では FuwaCourse/InkCap シェーダー：暗い色の所＝目にツヤが出る）。
# 原点は巣の面の上の体の中心、単位はm。書き出し：Unity (x,y,z)=(x,z,y)、巻き順反転
# 使い方：Blenderで exec(open(r'...\Tools\spider.py', encoding='utf-8').read())。JSON は OUT に出る
import bpy, bmesh, json, math
from mathutils import Vector, Matrix
OUT = globals().get('OUT', r'D:\UnityProject\FLUFF\Tools\spider.json')

BODY = (0.36, 0.27, 0.52); BODYDK = (0.27, 0.2, 0.4); PATTERN = (0.78, 0.66, 0.95)
LEG = (0.32, 0.24, 0.46); LEGTIP = (0.62, 0.5, 0.8)
EYE = (0.05, 0.035, 0.09); HI = (1.0, 1.0, 1.0); CHEEK = (1.0, 0.58, 0.74); FANG = (0.96, 0.92, 0.86)

V = []; N = []; C = []; F = []

def add_sphere(center, radius, color_fn, scale=(1, 1, 1), rot=None, seg=24, rings=14):
    # UV球（色は表面の位置ごとに color_fn(局所の単位ベクトル) で決める）
    base = len(V)
    R = rot or Matrix.Identity(3)
    S = Vector(scale)
    for j in range(rings + 1):
        th = math.pi * j / rings
        for i in range(seg + 1):
            ph = 2 * math.pi * i / seg
            u = Vector((math.sin(th) * math.cos(ph), math.sin(th) * math.sin(ph), math.cos(th)))
            p = Vector((u.x * S.x, u.y * S.y, u.z * S.z)) * radius
            n = Vector((u.x / S.x, u.y / S.y, u.z / S.z)).normalized()
            V.append(tuple(Vector(center) + R @ p)); N.append(tuple(R @ n)); C.append(color_fn(u))
    for j in range(rings):
        for i in range(seg):
            a = base + j * (seg + 1) + i; b = a + seg + 1
            F.append((a, b, b + 1, a + 1))

def add_tube(pts, radii, color_fn, seg=12):
    # 点列に沿った筒（両端はふさがない。端は球でふさぐ）
    base = len(V); n = len(pts)
    for k in range(n):
        p = Vector(pts[k])
        t = (Vector(pts[min(k + 1, n - 1)]) - Vector(pts[max(k - 1, 0)])).normalized()
        a = t.cross(Vector((0, 0, 1)))
        if a.length < 1e-3: a = t.cross(Vector((1, 0, 0)))
        a.normalize(); b = t.cross(a)
        for i in range(seg + 1):
            ph = 2 * math.pi * i / seg
            d = a * math.cos(ph) + b * math.sin(ph)
            V.append(tuple(p + d * radii[k])); N.append(tuple(d)); C.append(color_fn(k / (n - 1)))
    for k in range(n - 1):
        for i in range(seg):
            a0 = base + k * (seg + 1) + i; b0 = a0 + seg + 1
            F.append((a0, a0 + 1, b0 + 1, b0))

def lerp3(a, b, t): t = max(0.0, min(1.0, t)); return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))
def ss(a, b, x): t = max(0.0, min(1.0, (x - a) / (b - a))); return t * t * (3 - 2 * t)

BY = -0.15   # 体の中心の奥行き（巣の面から手前へ）
# ---- おしり（大きい丸）。手前側にハート形の明るい模様 ----
AB_C = (0, BY, -0.075); AB_R = 0.15
def ab_col(u):
    c = lerp3(BODYDK, BODY, ss(-0.6, 0.4, -u.y))
    return c
add_sphere(AB_C, AB_R, ab_col, scale=(1.0, 0.92, 1.02), seg=32, rings=18)

# ---- おしりのハート模様：輪郭から扇形に面を張って、おしりの表面に沿わせる（頂点カラーだとギザギザになるので形で作る） ----
def on_abdomen(x, z, out):
    cx, cy, cz = AB_C; sx, sy, sz = 1.0, 0.92, 1.02
    lx, lz = (x - cx) / (AB_R * sx), (z - cz) / (AB_R * sz)
    ly = -math.sqrt(max(0.0, 1 - lx * lx - lz * lz))
    n = Vector((lx / sx, ly / sy, lz / sz)).normalized()
    return Vector((x, cy + ly * AB_R * sy, z)) + n * out, n
HS = 0.0042; HZ = -0.085; NR = 3; NT = 48
base = len(V)
pc, nc = on_abdomen(0, HZ, 0.0025)
V.append(tuple(pc)); N.append(tuple(nc)); C.append(PATTERN)
for r in range(1, NR + 1):
    for k in range(NT):
        t = 2 * math.pi * k / NT
        hx = 16 * math.sin(t) ** 3
        hz = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        f = r / NR
        p, n = on_abdomen(hx * HS * f, HZ + (hz + 2.5) * HS * f, 0.0025)
        V.append(tuple(p)); N.append(tuple(n)); C.append(PATTERN)
for k in range(NT):
    k1 = (k + 1) % NT
    F.append((base, base + 1 + k1, base + 1 + k))
    for r in range(1, NR):
        a = base + 1 + (r - 1) * NT; b = base + 1 + r * NT
        F.append((a + k, a + k1, b + k1, b + k))

# ---- 頭（小さい丸、おしりの上に少しめりこむ） ----
HD_C = (0, BY - 0.04, 0.125); HD_R = 0.135
add_sphere(HD_C, HD_R, lambda u: lerp3(BODYDK, BODY, ss(-0.5, 0.5, -u.y)), scale=(1.1, 0.95, 0.92), seg=28, rings=16)

def on_head(x, z, out=0.0):
    # 頭の手前の表面の点（x,z 指定）
    cx, cy, cz = HD_C; sx, sy, sz = 1.1, 0.95, 0.92
    lx, lz = (x - cx) / (HD_R * sx), (z - cz) / (HD_R * sz)
    ly = -math.sqrt(max(0.0, 1 - lx * lx - lz * lz))
    p = Vector((x, cy + ly * HD_R * sy, z))
    n = Vector((lx / sx, ly / sy, lz / sz)).normalized()
    return p + n * out, n

def facing(n):
    # 局所 z を n に向ける回転
    z = Vector((0, 0, 1))
    return z.rotation_difference(n).to_matrix()

# ---- 目：でっかい黒目（平たい楕円）＋白いハイライト2つ ----
for sx in (-1, 1):
    p, n = on_head(sx * 0.056, 0.14, -0.014)
    R = facing(n)
    add_sphere(tuple(p), 0.05, lambda u: EYE, scale=(0.95, 1.15, 0.45), rot=R @ Matrix.Rotation(math.radians(90), 3, 'Z'), seg=20, rings=12)
    for (hx, hz, hr) in ((-0.014, 0.018, 0.013), (0.012, -0.012, 0.006)):
        q, nq = on_head(sx * 0.056 + hx * 1.15, 0.14 + hz * 1.15, 0.007)
        add_sphere(tuple(q), hr, lambda u: HI, scale=(1, 1, 0.5), rot=facing(nq), seg=12, rings=8)
    # ほっぺ
    q, nq = on_head(sx * 0.098, 0.085, -0.004)
    add_sphere(tuple(q), 0.022, lambda u: CHEEK, scale=(1.3, 1.0, 0.35), rot=facing(nq), seg=14, rings=8)
# ---- 小さいキバ（クリーム色のぽっちり2つ）と、にこっとした口 ----
for sx in (-1, 1):
    q, nq = on_head(sx * 0.02, 0.06, 0.0)
    add_sphere(tuple(q + Vector((0, 0, -0.008))), 0.012, lambda u: FANG, scale=(0.9, 0.9, 1.4), seg=10, rings=8)
mouth = []
for k in range(7):
    t = k / 6; x = (t - 0.5) * 0.04
    q, nq = on_head(x, 0.085 - 0.012 * math.sin(t * math.pi), 0.002)
    mouth.append(tuple(q))
add_tube(mouth, [0.0055] * len(mouth), lambda t: EYE, seg=8)
for end in (mouth[0], mouth[-1]): add_sphere(end, 0.0055, lambda u: EYE, seg=8, rings=6)

# ---- 足8本：体の横から出て、ひざが手前上に持ち上がり、先は巣の面（y=0）につく。ぽてっと太め ----
LEGS = [(50, 0.22, 0.1), (20, 0.25, 0.03), (-12, 0.25, -0.05), (-42, 0.22, -0.11)]   # 角度(度, 水平から), 長さ, 付け根の高さ
for sx in (-1, 1):
    for li, (ang, L, z0) in enumerate(LEGS):
        a = math.radians(ang)
        root = Vector((sx * 0.1, BY + 0.02, z0))
        dirv = Vector((sx * math.cos(a), 0, math.sin(a)))
        knee = root + dirv * (L * 0.5) + Vector((0, -0.05, 0.06))
        tip = root + dirv * L + Vector((0, 0, -0.02))
        tip.y = -0.012
        pts = []
        for k in range(10):
            t = k / 9
            # 2次ベジェ（付け根→ひざ→先）
            p = root * (1 - t) ** 2 + knee * 2 * (1 - t) * t + tip * t * t
            pts.append(tuple(p))
        radii = [0.03 - 0.006 * (k / 9) for k in range(10)]
        add_tube(pts, radii, lambda t: lerp3(LEG, LEGTIP, ss(0.75, 1.0, t)), seg=12)
        add_sphere(pts[-1], radii[-1] * 1.35, lambda u: LEGTIP, seg=12, rings=8)

# ---- Blender に置いて見る＋書き出し ----
col = bpy.data.collections.get('Spider') or bpy.data.collections.new('Spider')
if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
o = bpy.data.objects.get('CuteSpider')
if o: bpy.data.objects.remove(o)
me = bpy.data.meshes.new('CuteSpider'); me.from_pydata(V, [], F); me.update()
ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
for i, c in enumerate(C): ca.data[i].color = (c[0] ** 2.2, c[1] ** 2.2, c[2] ** 2.2, 1)
for p in me.polygons: p.use_smooth = True
o = bpy.data.objects.new('CuteSpider', me); col.objects.link(o); o.location = (0, -10, 1)
tris = []
for f in F:
    tris.append((f[0], f[2], f[1]))   # Unity 用に巻き順反転
    if len(f) == 4: tris.append((f[0], f[3], f[2]))
out = {'v': [(v[0], v[2], v[1]) for v in V], 'n': [(n[0], n[2], n[1]) for n in N], 'c': C, 't': tris}
json.dump(out, open(OUT, 'w'))
print('verts', len(V), 'tris', len(tris))
