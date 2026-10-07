# コース5「きのこのたに」：オレンジの発射台があった所を、木の足場にして途中でバリっと割る。
# 足場A（平ら）→ 割れ目（斜めの突風でふわふわを飛ばす。人は残った片側の桁を渡る）→ 足場B（上りの坂で次の段へ）
# 座標は Unity のワールド (x, y, z)、m。見た目は頂点カラー1枚（Unity では Den_Twig.mat ＝ InkCap シェーダー）
# 書き出し：OUT に {'Deck5': {v,c,t}, 'Deck5_Col': {v,t}}
# 使い方：Blender（または普通の python＋mathutils）で exec(open(r'...\Tools\deck5.py', encoding='utf-8').read())
import json, math, random
from mathutils import Vector
OUT = globals().get('OUT', r'D:\UnityProject\FLUFF\Tools\deck5.json')

Z0, Z1 = -18.25, -13.75          # 足場の幅（今の道と同じ）
XA0, XA1 = 75.5, 78.0            # 足場A（平ら、上面 YA）
XB0, XB1 = 80.5, 85.6            # 足場B（坂、YA → YB）
YA, YB = 15.8, 18.2
PLANK_W, PLANK_T, PLANK_GAP = 0.3, 0.09, 0.035
BEAM_W, BEAM_H = 0.5, 0.26
BEAM_Z = (Z0 + 0.35, Z1 - 0.35)  # 下の桁2本（z）。人が渡るのは +z 側（Z1寄り）の桁
WOOD = [(0.66, 0.47, 0.31), (0.6, 0.42, 0.28), (0.71, 0.52, 0.35), (0.56, 0.39, 0.26)]
WOOD_DARK = (0.38, 0.26, 0.18); WOOD_IN = (0.84, 0.68, 0.48)   # 割れた断面は明るい生木の色

def mul3(c, k): return tuple(min(1, x * k) for x in c)

class Mesh:
    def __init__(self): self.V = []; self.C = []; self.T = []; self.H = []
    def v(self, p, c=(1, 1, 1)): self.V.append(Vector(p)); self.C.append(c); return len(self.V) - 1
    def tri(self, a, b, c, h): self.T.append((a, b, c)); self.H.append(Vector(h))
    def quad(self, a, b, c, d, h): self.tri(a, b, c, h); self.tri(a, c, d, h)
    def out(self, color=True):
        T = []
        for (a, b, c), h in zip(self.T, self.H):
            n = (self.V[b] - self.V[a]).cross(self.V[c] - self.V[a])
            T.append((a, b, c) if n.dot(h) >= 0 else (a, c, b))
        d = {'v': [tuple(p) for p in self.V], 't': T}
        if color: d['c'] = self.C
        return d

def box(m, corners_top, depth_dir, depth, col, side_col=None):
    # 上面の4点（反時計回りでなくてOK）から、depth_dir の向きに厚みをつけた直方体（ゆがんでもOK）
    top = [Vector(p) for p in corners_top]
    bot = [p + depth_dir * depth for p in top]
    sc = side_col or mul3(col, 0.85)
    it = [m.v(p, col) for p in top]; ib = [m.v(p, mul3(col, 0.7)) for p in bot]
    ctr = sum(top + bot, Vector()) / 8
    m.quad(it[0], it[1], it[2], it[3], -depth_dir)
    m.quad(ib[0], ib[1], ib[2], ib[3], depth_dir)
    for i in range(4):
        j = (i + 1) % 4
        a, b = m.v(top[i], sc), m.v(top[j], sc); c, d = m.v(bot[j], mul3(sc, 0.8)), m.v(bot[i], mul3(sc, 0.8))
        mid = (top[i] + top[j] + bot[i] + bot[j]) / 4
        m.quad(a, b, c, d, mid - ctr)

def deck_y(x):
    if x <= XA1 + 1e-6: return YA
    t = (x - XB0) / (XB1 - XB0)
    return YA + (YB - YA) * max(0.0, min(1.0, t))

JAG = globals().get('JAG', False)   # 2026-10-07：割れ目のギザギザは無しに（てじゃ「なくてもいいかも」）
def jag_end(m, x_edge, z0, z1, y, sgn, rnd, col):
    # 割れた板の先：ギザギザのトゲ（生木の色）を何本か
    n = max(2, int((z1 - z0) / 0.12))
    for k in range(n):
        za = z0 + (z1 - z0) * k / n; zb = z0 + (z1 - z0) * (k + 1) / n
        L = rnd.uniform(0.05, 0.28)
        tip = Vector((x_edge + sgn * L, y - PLANK_T * rnd.uniform(0.2, 0.8), (za + zb) / 2 + rnd.uniform(-0.03, 0.03)))
        a = m.v((x_edge, y, za), WOOD_IN); b = m.v((x_edge, y, zb), WOOD_IN)
        c = m.v((x_edge, y - PLANK_T, zb), mul3(WOOD_IN, 0.8)); d = m.v((x_edge, y - PLANK_T, za), mul3(WOOD_IN, 0.8))
        t = m.v(tip, WOOD_IN)
        if not JAG: continue   # 乱数の並びは変えない（板の形がそのまま）
        m.tri(a, b, t, (0, 1, 0)); m.tri(d, t, c, (0, -1, 0)); m.tri(a, t, d, (0, 0, -1)); m.tri(b, c, t, (0, 0, 1))

rnd = random.Random(5)
vis = Mesh(); col = Mesh()

# 2026-10-07：板をガタつかせる（きのこのたに入口の木の床と同じくらい）。見た目だけ（判定は平らのまま）
JIT = globals().get('JIT', 1.0)
jrnd = random.Random(11)
def jitter(corners, k=1.0, sink=0.0):
    # 板の上面4点を、板の中心まわりに少し傾け（3軸）、少し浮かせ/沈め、横に少しずらす
    # k：ガタつきの強さ（道に差しこむ一番はしの板は0＝平らに。乱数の並びは変えない）、sink：沈める量
    from mathutils import Euler
    P = [Vector(p) for p in corners]
    ctr = sum(P, Vector()) / 4
    e = Euler((math.radians(jrnd.uniform(-1.6, 1.6) * JIT * k), math.radians(jrnd.uniform(-1.8, 1.8) * JIT * k), math.radians(jrnd.uniform(-2.4, 2.4) * JIT * k)), 'XYZ')
    off = Vector((jrnd.uniform(-0.015, 0.015) * JIT * k, jrnd.uniform(-0.012, 0.028) * JIT * k - sink, 0))
    out = []
    for p in P:
        q = p - ctr; q.rotate(e); out.append(tuple(ctr + q + off))
    return out
def chip(za, zb):
    # 板の両はしを不ぞろいに欠けさせる（35%の板で 5〜26cm）
    if jrnd.random() < 0.35: za += jrnd.uniform(0.05, 0.26)
    if jrnd.random() < 0.35: zb -= jrnd.uniform(0.05, 0.26)
    return za, zb

# ---- 板（横向き）を並べる ----
def lay_planks(x0, x1, broken_end=None):
    x = x0
    while x < x1 - 0.05:
        w = min(PLANK_W, x1 - x)
        xa, xb = x, x + w
        c = mul3(WOOD[rnd.randrange(len(WOOD))], rnd.uniform(0.93, 1.06))
        za, zb = Z0 + rnd.uniform(-0.04, 0.04), Z1 + rnd.uniform(-0.04, 0.04)
        za, zb = chip(za, zb)
        # 割れ目のそばの板：長さがバラバラに欠けている（人が渡る +z 側の桁の上は残す）
        cut_z = None
        if broken_end is not None:
            edge, sgn = broken_end
            dist = abs((xa + xb) / 2 - edge)
            if dist < 1.2:
                cut_z = Z1 - rnd.uniform(0.55, 1.2) - (1.2 - dist) * rnd.uniform(0.5, 2.0)
                cut_z = max(Z0 + 0.4, cut_z)
        ya, yb = deck_y(xa), deck_y(xb)
        # 2026-10-07：坂Bの終わり（上の道の下にもぐる板）はガタつかせない。傾くと道の上面から板のかどが突き出ていた
        under_path = xb > XB1 - 0.35
        jk, js = (0.0, 0.015) if under_path else (1.0, 0.0)
        if cut_z is None:
            box(vis, jitter([(xa, ya, za), (xb, yb, za), (xb, yb, zb), (xa, ya, zb)], jk, js), Vector((0, -1, 0)), PLANK_T, c)
        else:
            # 欠けた板：-z 側だけ残る（割れ目に向かって短くなる）＋ 先はギザギザ
            box(vis, jitter([(xa, ya, za), (xb, yb, za), (xb, yb, cut_z), (xa, ya, cut_z)]), Vector((0, -1, 0)), PLANK_T, c)
            if Z1 - cut_z > 0.6:
                # +z 側の桁の上だけ、短い板の切れ端が残る
                zz = Z1 - rnd.uniform(0.45, 0.6)
                box(vis, jitter([(xa, ya, zz), (xb, yb, zz), (xb, yb, zb), (xa, ya, zb)]), Vector((0, -1, 0)), PLANK_T, c)
        x = xb + PLANK_GAP

lay_planks(XA0, XA1, broken_end=(XA1, 1))
lay_planks(XB0, XB1, broken_end=(XB0, -1))
# 割れ目に面した板の断面（ギザギザ）
jag_end(vis, XA1, Z0 + 0.05, Z1 - 0.7, YA, 1, rnd, WOOD_IN)
jag_end(vis, XB0, Z0 + 0.05, Z1 - 0.7, YA, -1, rnd, WOOD_IN)

# ---- 下の桁2本：-z 側は割れ目で折れてぶら下がる、+z 側（人が渡る）は割れ目の上もつながっている ----
yb_top = lambda x: deck_y(x) - PLANK_T
for bi, bz in enumerate(BEAM_Z):
    z0, z1 = bz - BEAM_W / 2, bz + BEAM_W / 2
    segs = [(XA0, XA1 + (0.0 if bi == 0 else 0)), (XB0, XB1)] if bi == 0 else [(XA0, XB1)]
    for (s0, s1) in segs:
        n = max(1, int((s1 - s0) / 0.5))
        for k in range(n):
            xa = s0 + (s1 - s0) * k / n; xb = s0 + (s1 - s0) * (k + 1) / n
            ya, yb2 = yb_top(xa), yb_top(xb)
            box(vis, [(xa, ya, z0), (xb, yb2, z0), (xb, yb2, z1), (xa, ya, z1)], Vector((0, -1, 0)), BEAM_H, WOOD_DARK, mul3(WOOD_DARK, 1.1))
    if bi == 0:
        # 折れた桁の先が割れ目へ下向きにぶら下がる
        for x_edge, sgn in ((XA1, 1), (XB0, -1)):
            L = 0.9
            a = Vector((x_edge, yb_top(x_edge), z0)); b = Vector((x_edge, yb_top(x_edge), z1))
            d = Vector((sgn * L * 0.55, -L * 0.83, 0))
            box(vis, [a, a + d, b + d, b], Vector((sgn * 0.83, 0.55, 0)).normalized() * -1, BEAM_H * 0.8, WOOD_DARK)
            jag_end(vis, x_edge + sgn * L * 0.55, z0, z1, yb_top(x_edge) - L * 0.83, sgn, rnd, WOOD_IN)

# 割れ目に落ちかけた板が1枚、+z の桁に引っかかって斜めにぶら下がる
pz0, pz1 = BEAM_Z[1] - 1.6, BEAM_Z[1] + 0.1
px = (XA1 + XB0) / 2 - 0.4
box(vis, [(px, YA - PLANK_T - 0.05, pz1), (px + PLANK_W, YA - PLANK_T - 0.05, pz1), (px + PLANK_W + 0.1, YA - 1.0, pz0), (px + 0.1, YA - 1.0, pz0)],
    Vector((0, -0.85, -0.5)).normalized(), PLANK_T, mul3(WOOD[2], 0.95))

# ---- 当たり判定：足場Aの上面、足場Bの坂、+z の桁の上（割れ目を渡る細い道）----
def col_quad(p0, p1, p2, p3):
    i = [col.v(p) for p in (p0, p1, p2, p3)]
    col.quad(i[0], i[1], i[2], i[3], (0, 1, 0))
col_quad((XA0, YA, Z0), (XA1, YA, Z0), (XA1, YA, Z1), (XA0, YA, Z1))
N = 8
for k in range(N):
    xa = XB0 + (XB1 - XB0) * k / N; xb = XB0 + (XB1 - XB0) * (k + 1) / N
    col_quad((xa, deck_y(xa), Z0), (xb, deck_y(xb), Z0), (xb, deck_y(xb), Z1), (xa, deck_y(xa), Z1))
bz = BEAM_Z[1]; yt = YA - PLANK_T
col_quad((XA1, yt, bz - BEAM_W / 2 - 0.05), (XB0, yt, bz - BEAM_W / 2 - 0.05), (XB0, yt, bz + BEAM_W / 2 + 0.05), (XA1, yt, bz + BEAM_W / 2 + 0.05))

json.dump({'Deck5': vis.out(), 'Deck5_Col': col.out(False)}, open(OUT, 'w'))
print('deck verts', len(vis.V), 'tris', len(vis.T))
