# コース4「まもののす」：小枝で編んだ巣と小枝の橋を作る（見た目＋当たり判定のメッシュ）。
# レイアウト（巣の中心・半径・ふちの切れ目、橋の通り道）はこのファイルが元。座標は Unity のワールド (x, y, z)、m。
# 見た目：頂点カラー1枚（sRGBで書いた色。Unity では FuwaCourse/InkCap シェーダー）。当たり判定は別メッシュ（床・橋・ふちの壁）。
# 書き出し：OUT の JSON に {name: {v, c, t}} と {col_name: {v, t}}（Unity 座標、表向きは法線ヒントに合わせて自動でそろえる）
# 使い方：Blenderで exec(open(r'...\Tools\den.py', encoding='utf-8').read())
import bpy, json, math, random
from mathutils import Vector, noise
OUT = globals().get('OUT', r'D:\UnityProject\FLUFF\Tools\den.json')

FLOOR_Y = 5.5
# ---- レイアウト ----
# 2026-10-09：最初の橋を3mのばした（スタート直後のイバラの中で、先を飛ぶふわふわが巣1のけだまに見つからないように）。
# スタートの島を巣1から遠ざかる向きに3m（旧 (31.22, -3.15)）。Unity ではスタートの島は元の場所のまま、
# かわりにコースの残り（FuwaCourse4 ごと）を3m奥へずらした＝ここの座標は Unity のワールドではなく、Den の中の位置
START = dict(c=(28.601, -4.613), r=3.2)          # 既存のスタートの島（作らない、橋の端だけ合わせる）
# 2026-10-07：巣1と分岐の巣の間に島を1つ足して、分岐から先を SHIFT だけ奥へずらした（巣1=1匹 / 巣2=2匹 / 巣3=やや小さめ＋チェックポイント＋分岐）
SHIFT = (9.7, 4.3)   # 分岐から先のずらし量（巣1→巣2 と同じ向き・長さ）
def sh(p): return (round(p[0] + SHIFT[0], 3), round(p[1] + SHIFT[1], 3))
GOAL = dict(c=sh((71.02, 23.37)), r=3.1)           # 既存のゴールの島（Unity 側でも同じだけ動かす）
NESTS = [
    dict(name='Nest1', c=(40.8, 2.2), r=3.0, rim=0.5, seed=1),
    dict(name='Nest2', c=(50.5, 6.5), r=3.2, rim=0.5, seed=2),       # 新しい島（けだま2匹）
    dict(name='Nest3', c=sh((50.5, 6.5)), r=2.6, rim=0.45, seed=6),  # 分岐の巣（やや小さめ＋チェックポイント）
    dict(name='MamaNest', c=sh((64.2, 17.4)), r=4.6, rim=0.7, seed=3),
    dict(name='SideNest', c=sh((58.7, -0.9)), r=3.0, rim=0.5, seed=4),
    dict(name='Islet', c=sh((65.8, 7.0)), r=1.8, rim=0.35, seed=5),   # 大回りの曲がり角の小島（何もない）   # 大回りルートの巣（けだま1匹＋倒しやすいイバラ）
]
PODS = [   # 寝てるけだまの小さい巣（橋のわき、入口なし）
    dict(name='Pod1', c=sh((54.04, 12.45)), r=1.25, rim=0.45, seed=11),
    dict(name='Pod2', c=sh((59.16, 8.45)), r=1.25, rim=0.45, seed=12),
    dict(name='Pod3', c=sh((57.6, 15.1)), r=1.25, rim=0.45, seed=13),
]
BRIDGE_W = 2.4
# 橋：端の島（名前）と、途中の通り道。端は島のふちの少し内側まで入れる
BRIDGES = [
    dict(name='BridgeA', a='Start', b='Nest1', via=[]),
    dict(name='BridgeB', a='Nest1', b='Nest2', via=[]),
    dict(name='BridgeH', a='Nest2', b='Nest3', via=[]),
    dict(name='BridgeC', a='Nest3', b='MamaNest', via=[sh((55.8, 9.9)), sh((59.0, 12.1))]),
    dict(name='BridgeD', a='MamaNest', b='Goal', via=[]),
    # 大回りルート：巣3 → 横の巣 → ママの巣（南側から入る）
    dict(name='BridgeE', a='Nest3', b='SideNest', via=[]),
    dict(name='BridgeF', a='SideNest', b='Islet', via=[]),
    dict(name='BridgeG', a='Islet', b='MamaNest', via=[]),
]

TWIG = [(0.55, 0.41, 0.29), (0.47, 0.34, 0.24), (0.63, 0.49, 0.35), (0.41, 0.3, 0.22)]
TWIG_DARK = (0.3, 0.22, 0.17)
STRAW = (0.78, 0.66, 0.47); STRAW2 = (0.7, 0.58, 0.4)
LASH = (0.66, 0.72, 0.42)   # 橋をしばるツル（黄緑）

def lerp3(a, b, t): t = max(0.0, min(1.0, t)); return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))
def mul3(a, k): return tuple(min(1.0, x * k) for x in a)

class Mesh:
    def __init__(self): self.V = []; self.C = []; self.T = []; self.H = []   # H: 三角形ごとの「表の向き」のヒント
    def v(self, p, c=(1, 1, 1)): self.V.append(Vector(p)); self.C.append(c); return len(self.V) - 1
    def tri(self, a, b, c, hint): self.T.append((a, b, c)); self.H.append(Vector(hint))
    def quad(self, a, b, c, d, hint): self.tri(a, b, c, hint); self.tri(a, c, d, hint)
    def out(self, with_color=True):
        T = []
        for (a, b, c), h in zip(self.T, self.H):
            pa, pb, pc = self.V[a], self.V[b], self.V[c]
            n = (pb - pa).cross(pc - pa)   # Unity の向き（RecalculateNormals と同じ）
            T.append((a, b, c) if n.dot(h) >= 0 else (a, c, b))
        d = {'v': [tuple(p) for p in self.V], 't': T}
        if with_color: d['c'] = self.C
        return d

def tube(m, pts, radius, col_fn, sides=5, rnd=None):
    # 点列に沿った筒（小枝）。法線ヒントは外向き。両端は少しすぼめる
    n = len(pts); base = len(m.V); centers = [Vector(p) for p in pts]
    for k in range(n):
        p = centers[k]
        t = (centers[min(k + 1, n - 1)] - centers[max(k - 1, 0)]).normalized()
        a = t.cross(Vector((0, 1, 0)))
        if a.length < 1e-3: a = t.cross(Vector((1, 0, 0)))
        a.normalize(); b = t.cross(a)
        rr = radius * (0.55 if k in (0, n - 1) else 1.0)
        for i in range(sides):
            ph = 2 * math.pi * i / sides
            m.v(p + (a * math.cos(ph) + b * math.sin(ph)) * rr, col_fn(k / max(1, n - 1)))
    for k in range(n - 1):
        for i in range(sides):
            i1 = (i + 1) % sides
            a0 = base + k * sides + i; a1 = base + k * sides + i1; b0 = a0 + sides; b1 = a1 + sides
            mid = (m.V[a0] + m.V[b1]) * 0.5; ctr = (centers[k] + centers[k + 1]) * 0.5
            m.quad(a0, a1, b1, b0, mid - ctr)
    # 端のふた
    for k in (0, n - 1):
        ring = [base + k * sides + i for i in range(sides)]
        c = m.v(centers[k], col_fn(k / max(1, n - 1)))
        out = (centers[0] - centers[1]) if k == 0 else (centers[-1] - centers[-2])
        for i in range(sides): m.tri(c, ring[i], ring[(i + 1) % sides], out)

def twig_color(rnd):
    c = TWIG[rnd.randrange(len(TWIG))]; k = rnd.uniform(0.9, 1.08)
    return lambda t, c=c, k=k: mul3(lerp3(c, TWIG_DARK, 0.25 * abs(t - 0.5) * 2), k)

def ang_in(a, gaps):
    for (g0, g1) in gaps:
        d = (a - g0) % 360
        if d <= (g1 - g0) % 360: return True
    return False

def gap_for(c, toward, r, width):
    # 巣の中心 c から点 toward の方向に、幅 width の橋が通る切れ目（度）
    ang = math.degrees(math.atan2(toward[1] - c[1], toward[0] - c[0]))
    half = math.degrees(math.asin(min(0.99, (width * 0.5 + 0.25) / r)))
    return ((ang - half) % 360, (ang + half) % 360)

def island(name):
    for n in NESTS:
        if n['name'] == name: return n
    return START if name == 'Start' else GOAL

# ---- 巣 ----
def build_nest(nd, gaps, vis, col):
    rnd = random.Random(nd['seed']); cx, cz = nd['c']; r = nd['r']; rim = nd['rim']; y = FLOOR_Y
    # 床（わらっぽい筋の色ムラ）。中心から同心円で
    R_STEPS = max(4, int(r / 0.3)); SEG = max(24, int(r * 14))
    ctr = vis.v((cx, y, cz), STRAW); rings = []
    ang0 = rnd.uniform(0, 6.28)
    for j in range(1, R_STEPS + 1):
        rr = r * j / R_STEPS; ring = []
        for i in range(SEG):
            a = 2 * math.pi * i / SEG; px, pz = cx + rr * math.cos(a), cz + rr * math.sin(a)
            s = noise.noise(Vector((px * 1.7 * math.cos(ang0) + pz * 3.1, pz * 1.7 - px * 3.1 * math.sin(ang0), nd['seed'])))
            ring.append(vis.v((px, y - 0.01 * j / R_STEPS, pz), lerp3(STRAW2, STRAW, 0.5 + s * 0.9)))
        rings.append(ring)
    for i in range(SEG):
        vis.tri(ctr, rings[0][i], rings[0][(i + 1) % SEG], (0, 1, 0))
    for j in range(R_STEPS - 1):
        for i in range(SEG):
            i1 = (i + 1) % SEG
            vis.quad(rings[j][i], rings[j + 1][i], rings[j + 1][i1], rings[j][i1], (0, 1, 0))
    # 外側のお椀（下へすぼまる）。ふちの外 r+0.45 から、下 2.2m の点へ
    bowl = []
    BR = [(r + 0.45, y - 0.05), (r + 0.5, y - 0.35), (r * 0.92, y - 0.9), (r * 0.65, y - 1.45), (r * 0.32, y - 1.95), (0.08, y - 2.2)]
    for (rr, yy) in BR:
        ring = []
        for i in range(SEG):
            a = 2 * math.pi * i / SEG
            px, pz = cx + rr * math.cos(a), cz + rr * math.sin(a)
            s = noise.noise(Vector((a * 3.0, yy * 2.3, nd['seed'] + 5)))
            c = lerp3(TWIG[1], TWIG[2], 0.5 + s); c = lerp3(c, TWIG_DARK, (y - yy) / 2.6)
            ring.append(vis.v((px, yy, pz), c))
        bowl.append(ring)
    # 床のふち〜お椀の上（ふちの壁の下を埋める）
    for i in range(SEG):
        i1 = (i + 1) % SEG; a = 2 * math.pi * (i + 0.5) / SEG
        out = (math.cos(a), 0.3, math.sin(a))
        vis.quad(rings[-1][i], rings[-1][i1], bowl[0][i1], bowl[0][i], (0, 1, 0))
    for j in range(len(bowl) - 1):
        for i in range(SEG):
            i1 = (i + 1) % SEG; a = 2 * math.pi * (i + 0.5) / SEG
            vis.quad(bowl[j][i], bowl[j][i1], bowl[j + 1][i1], bowl[j + 1][i], (math.cos(a), -0.4, math.sin(a)))
    # ふちの壁：まず中身の詰まった丸い土手（小枝の色の筋）、その表面に小枝をたくさん巻きつける。切れ目はあける
    PROF = [(-0.02, -0.03), (0.0, 0.45), (0.08, 0.85), (0.22, 1.0), (0.38, 0.85), (0.48, 0.45), (0.52, -0.25)]   # (外への距離, 高さ/rim)
    step = 4.0; angs = []
    a = 0.0
    while a < 360.0 - 1e-6: angs.append(a); a += step
    runs = []; cur = []
    for a in angs:
        if ang_in(a, gaps): 
            if cur: runs.append(cur); cur = []
        else: cur.append(a)
    closed = not gaps
    if closed: runs = [angs + [360.0]]
    elif cur:
        if runs and runs[0][0] == 0.0: runs[0] = cur + [x + 360 for x in runs[0]]
        else: runs.append(cur)
    for run in runs:
        rows = []
        n = len(run)
        for qi, a in enumerate(run):
            end = min(qi, n - 1 - qi)
            hk = 1.0 if closed else 0.35 + 0.65 * min(1.0, end / 3.0)   # 切れ目の端は低くなって床へなじむ
            aa = math.radians(a); ca, sa = math.cos(aa), math.sin(aa)
            row = []
            for (dr, hy) in PROF:
                wob = noise.noise(Vector((a * 0.07, hy * 2, nd['seed'] * 3.1))) * 0.05
                rr = r + dr + wob; yy = y + hy * rim * hk
                st = noise.noise(Vector((a * 0.35, hy * 9.0 + dr * 6, nd['seed'] + 9)))
                c = lerp3(TWIG[3], TWIG[2], 0.5 + st * 0.9); c = lerp3(c, TWIG_DARK, max(0, -hy) * 0.8)
                row.append(vis.v((cx + rr * ca, yy, cz + rr * sa), c))
            rows.append((row, ca, sa))
        for qi in range(n - 1):
            (r0, ca, sa), (r1, _, _) = rows[qi], rows[qi + 1]
            for k in range(len(PROF) - 1):
                dr = (PROF[k][0] + PROF[k + 1][0]) * 0.5 - 0.22; hy = (PROF[k][1] + PROF[k + 1][1]) * 0.5 - 0.5
                vis.quad(r0[k], r1[k], r1[k + 1], r0[k + 1], (ca * dr * 2, hy + 0.2, sa * dr * 2))
        # 端のふた（切れ目に面した断面）
        for (row, ca, sa), sgn in (() if closed else ((rows[0], -1), (rows[-1], 1))):
            tang = Vector((-sa, 0, ca)) * sgn
            mid = vis.v(sum((vis.V[i] for i in row), Vector()) / len(row), TWIG[1])
            for k in range(len(row) - 1): vis.tri(mid, row[k], row[k + 1], tang)
        # 表面に巻きつける小枝：内側の面・てっぺん・外側の面に、それぞれ並べる
        span = run[-1] - run[0]
        for (dr0, dr1, h0, h1, dens) in ((0.0, 0.06, 0.15, 0.8, 1.0), (0.12, 0.34, 0.92, 1.05, 1.3), (0.4, 0.5, 0.2, 0.85, 1.0)):
            nrow = 2 if h1 - h0 > 0.3 else 1
            for rowi in range(nrow):
                a = run[0] + rnd.uniform(-2, 6)
                while a < run[-1] - 4:
                    arc = min(run[-1] - a, rnd.uniform(28, 55) * (3.2 / max(r, 1.2)) ** 0.5)
                    if arc < 8: break
                    hy = rnd.uniform(h0, h1) if nrow == 1 else h0 + (h1 - h0) * (rowi + rnd.uniform(0.15, 0.85)) / nrow
                    dr = rnd.uniform(dr0, dr1); rad = rnd.uniform(0.04, 0.065)
                    pts = []
                    for k in range(7):
                        t = k / 6; aa = math.radians(a + arc * t)
                        endk = 1.0 if closed else min(1.0, min(a + arc * t - run[0], run[-1] - (a + arc * t)) / 12.0)
                        rk = r + dr + (t - 0.5) * rnd.uniform(-0.06, 0.06) + rad * 0.5
                        yk = y + rim * hy * (0.35 + 0.65 * endk) + math.sin(t * math.pi) * 0.025
                        pts.append((cx + rk * math.cos(aa), yk, cz + rk * math.sin(aa)))
                    tube(vis, pts, rad, twig_color(rnd), sides=5)
                    a += arc * rnd.uniform(0.45, 0.7) / dens
    # はみ出す小枝（外向き・上向きに、ばらばら）
    for k in range(int(r * 7)):
        a = rnd.uniform(0, 360)
        if ang_in(a, gaps) or any(ang_in((a + d) % 360, gaps) for d in (-12, 12)): continue
        aa = math.radians(a); base = Vector((cx + (r + 0.25) * math.cos(aa), y + rnd.uniform(0.1, rim), cz + (r + 0.25) * math.sin(aa)))
        tang = Vector((-math.sin(aa), 0, math.cos(aa))) * rnd.choice((-1, 1))
        d = (Vector((math.cos(aa), rnd.uniform(-0.1, 0.5), math.sin(aa))) * rnd.uniform(0.3, 0.8) + tang * rnd.uniform(0.3, 1.0)).normalized()
        L = rnd.uniform(0.5, 1.0)
        pts = [tuple(base - d * L * 0.4 + d * L * t + Vector((0, -0.05 * math.sin(t * math.pi), 0))) for t in (0, 0.33, 0.66, 1.0)]
        tube(vis, pts, rnd.uniform(0.025, 0.04), twig_color(rnd), sides=4)
    # 当たり判定：床（円盤）＋ふちの壁（切れ目以外、内側 r、高さ rim+0.15）
    SEGC = 32; c0 = col.v((cx, y, cz)); prev = None
    ringc = [col.v((cx + r * 1.03 * math.cos(2 * math.pi * i / SEGC), y, cz + r * 1.03 * math.sin(2 * math.pi * i / SEGC))) for i in range(SEGC)]
    for i in range(SEGC): col.tri(c0, ringc[i], ringc[(i + 1) % SEGC], (0, 1, 0))
    for i in range(SEGC):
        a0 = 360.0 * i / SEGC; a1 = 360.0 * (i + 1) / SEGC
        if ang_in((a0 + a1) * 0.5 % 360, gaps): continue
        p0 = Vector((cx + (r + 0.05) * math.cos(math.radians(a0)), y - 0.2, cz + (r + 0.05) * math.sin(math.radians(a0))))
        p1 = Vector((cx + (r + 0.05) * math.cos(math.radians(a1)), y - 0.2, cz + (r + 0.05) * math.sin(math.radians(a1))))
        up = Vector((0, rim + 0.35, 0)); inward = Vector((cx, 0, cz)) - (p0 + p1) * 0.5; inward.y = 0
        i0 = col.v(p0); i1 = col.v(p1); i2 = col.v(p1 + up); i3 = col.v(p0 + up)
        col.quad(i0, i1, i2, i3, inward)

# ---- 橋 ----
def catmull(pts, n_per):
    out = []
    P = [pts[0]] + pts + [pts[-1]]
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(n_per):
            t = k / n_per; t2 = t * t; t3 = t2 * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(pts[-1]); return out

def bridge_points(bd):
    A = island(bd['a']); B = island(bd['b'])
    first = Vector((bd['via'][0][0], 0, bd['via'][0][1])) if bd['via'] else Vector((B['c'][0], 0, B['c'][1]))
    last = Vector((bd['via'][-1][0], 0, bd['via'][-1][1])) if bd['via'] else Vector((A['c'][0], 0, A['c'][1]))
    ca = Vector((A['c'][0], 0, A['c'][1])); cb = Vector((B['c'][0], 0, B['c'][1]))
    pa = ca + (first - ca).normalized() * (A['r'] - 0.6)
    pb = cb + (last - cb).normalized() * (B['r'] - 0.6)
    pts = [pa] + [Vector((x, 0, z)) for (x, z) in bd['via']] + [pb]
    return pts

def build_bridge(bd, vis, col):
    ends = [island(bd['a']), island(bd['b'])]
    def sink_at(p, margin=0.0):
        # 島（巣）の中に入った分だけ、小枝をなだらかに床の下へもぐらせる（床と同じ高さで横に切れて見えないように）
        best = 0.0
        for isl in ends:
            d = math.hypot(p.x - isl['c'][0], p.z - isl['c'][1])
            inside = isl['r'] + margin - d
            if inside > 0: best = max(best, inside * 0.45)
        return best
    rnd = random.Random(sum(ord(ch) for ch in bd['name']))
    pts = bridge_points(bd)
    L = sum((pts[i + 1] - pts[i]).length for i in range(len(pts) - 1))
    path = catmull(pts, max(4, int(L / max(1, len(pts) - 1) / 0.35)))
    # 弧長で等間隔に取り直す
    acc = [0.0]
    for i in range(1, len(path)): acc.append(acc[-1] + (path[i] - path[i - 1]).length)
    total = acc[-1]; N = max(6, int(total / 0.3)); res = []; j = 0
    for k in range(N + 1):
        s = total * k / N
        while j < len(acc) - 2 and acc[j + 1] < s: j += 1
        t = (s - acc[j]) / max(1e-6, acc[j + 1] - acc[j]); res.append(path[j].lerp(path[j + 1], t))
    def frame(k):
        t = (res[min(k + 1, N)] - res[max(k - 1, 0)]); t.y = 0; t.normalize()
        return t, Vector((-t.z, 0, t.x))
    # 床板がわりの長い小枝を横に並べる（上面が床の高さ）
    K = 7; rad = BRIDGE_W / K * 0.55
    for q in range(K):
        off = (q - (K - 1) / 2) * (BRIDGE_W - rad * 2) / (K - 1)
        ph = rnd.uniform(0, 6.28); amp = rnd.uniform(0.01, 0.03)
        trim0 = rnd.uniform(0, 0.25); trim1 = rnd.uniform(0, 0.25)
        tp = []
        for k in range(N + 1):
            s = total * k / N
            if s < trim0 * 0.0 or s > total - trim1 * 0.0: continue
            t, side = frame(k)
            p = res[k] + side * (off + amp * math.sin(s * 1.3 + ph)) + Vector((0, FLOOR_Y - rad * 0.85 + amp * math.sin(s * 2.1 + ph), 0))
            p.y -= sink_at(p)
            tp.append(tuple(p))
        tube(vis, tp, rad * rnd.uniform(0.95, 1.05), twig_color(rnd), sides=6)
    # しばってあるツル（橋を横切る輪）
    nl = max(2, int(total / 1.4))
    for i in range(nl):
        s = total * (i + 0.5) / nl; k = min(N, int(round(s / total * N)))
        if sink_at(res[k]) > 0 or sink_at(res[k], 0.5) > 0: continue   # 巣の床のふちにかかるツルは描かない
        t, side = frame(k); c = res[k] + Vector((0, FLOOR_Y - rad * 0.85, 0))
        loop = []
        for a in range(13):
            th = 2 * math.pi * a / 12
            loop.append(tuple(c + side * math.cos(th) * (BRIDGE_W * 0.5 + 0.02) + Vector((0, math.sin(th) * (rad + 0.035), 0)) + t * 0.0))
        tube(vis, loop, 0.028, lambda u: LASH, sides=4)
    # 当たり判定：上面の帯（ふちは少しだけ広め）
    prevL = prevR = None
    for k in range(N + 1):
        t, side = frame(k); p = res[k] + Vector((0, FLOOR_Y, 0))
        l = col.v(p - side * (BRIDGE_W * 0.5 + 0.05)); r = col.v(p + side * (BRIDGE_W * 0.5 + 0.05))
        if prevL is not None: col.quad(prevL, l, r, prevR, (0, 1, 0))
        prevL, prevR = l, r
    return res

# ---- 組み立て ----
out = {}
gaps = {n['name']: [] for n in NESTS}
for bd in BRIDGES:
    pts = bridge_points(bd)
    for end, nb in ((pts[0], bd['a']), (pts[-1], bd['b'])):
        if nb in gaps:
            n = island(nb); nxt = pts[1] if end is pts[0] else pts[-2]
            gaps[nb].append(gap_for(n['c'], (nxt.x, nxt.z), n['r'], BRIDGE_W))
# 近すぎる切れ目どうしはまとめる（間に細い壁の切れ端が残らないように）
def merge_gaps(gs, min_wall=18.0):
    iv = sorted([(g0, g0 + (g1 - g0) % 360) for g0, g1 in gs])
    out = []
    for a, b in iv:
        if out and a - out[-1][1] < min_wall: out[-1] = (out[-1][0], max(out[-1][1], b))
        else: out.append((a, b))
    if len(out) > 1 and out[0][0] + 360 - out[-1][1] < min_wall:
        out[0] = (out[-1][0], out[0][1] + 360); out.pop()
    return [(a % 360, b % 360) for a, b in out]
gaps = {k: merge_gaps(v) for k, v in gaps.items()}
for nd in NESTS + PODS:
    vis = Mesh(); col = Mesh()
    build_nest(nd, gaps.get(nd['name'], []), vis, col)
    out[nd['name']] = vis.out(); out[nd['name'] + '_Col'] = col.out(False)
for bd in BRIDGES:
    vis = Mesh(); col = Mesh()
    build_bridge(bd, vis, col)
    out[bd['name']] = vis.out(); out[bd['name'] + '_Col'] = col.out(False)
json.dump(out, open(OUT, 'w'))

# Blender で確認用に置く（Blender の座標 = (x, z, y)）
coll = bpy.data.collections.get('Den') or bpy.data.collections.new('Den')
if coll.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(coll)
for o in list(coll.objects): bpy.data.objects.remove(o)
for name, d in out.items():
    if name.endswith('_Col'): continue
    me = bpy.data.meshes.new('Den_' + name)
    me.from_pydata([(p[0], p[2], p[1]) for p in d['v']], [], [tuple(t) for t in d['t']]); me.update()
    ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'POINT')
    for i, c in enumerate(d['c']): ca.data[i].color = (c[0] ** 2.2, c[1] ** 2.2, c[2] ** 2.2, 1)
    o = bpy.data.objects.new('Den_' + name, me); coll.objects.link(o); o.location = (0, 300, 0)
print({k: len(v['t']) for k, v in out.items()})
