# きのこのたにのゴールの大きなキノコ（成菌：傘が平たく開いたベニテングタケ風）。スタートのキノコと同じ作り方。
# 書き出し（Unity 座標、メートル、実寸）：
#   'GiantCap'  … 傘（submesh0 = 赤い傘の表、submesh1 = 白いヒダ）。原点＝傘の付け根（軸のてっぺん）
#   'GiantStem' … 軸（つば・根元のふくらみつき）。原点＝地面
# 使い方：Blenderで exec(open(r'...\Tools\giant_mushroom.py', encoding='utf-8').read())
import json, math
OUT = globals().get('OUT', r'D:\UnityProject\FLUFF\Tools\giant_mushroom.json')

SEG = 64
CAP_R = 4.25          # 傘の半径
CAP_TOP = 1.12        # 付け根から傘のてっぺんまでの高さ
STEM_TOP = 19.8       # 軸のてっぺん（＝傘の付け根の高さ）
STEM_R = 1.05

class M:
    def __init__(self, nsub=1): self.V = []; self.T = [[] for _ in range(nsub)]
    def v(self, p): self.V.append(tuple(p)); return len(self.V) - 1

def lathe(m, sub, prof, seg=SEG, wob=None, flip=False):
    rings = []
    for (r, y) in prof:
        ring = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            rr = r * (1 + (wob(a, y) if wob else 0))
            ring.append(m.v((rr * math.cos(a), y, rr * math.sin(a))))
        rings.append(ring)
    for k in range(len(rings) - 1):
        A, B = rings[k], rings[k + 1]
        for i in range(seg):
            j = (i + 1) % seg
            q = (A[i], A[j], B[j], B[i])
            if not flip: m.T[sub] += [q[0], q[1], q[2], q[0], q[2], q[3]]
            else: m.T[sub] += [q[0], q[2], q[1], q[0], q[3], q[2]]
    return rings

def cap_fan(m, sub, ring, center, flip=False):
    c = m.v(center); n = len(ring)
    for i in range(n):
        j = (i + 1) % n
        if not flip: m.T[sub] += [c, ring[i], ring[j]]
        else: m.T[sub] += [c, ring[j], ring[i]]

# ---- 傘：まん中はゆるく盛り上がり、外へ向かって平たく、ふちは少し下に垂れて薄くなる（成菌）----
cap = M(2)
prof = []
N = 26
for k in range(N + 1):
    t = k / N                                   # 0=てっぺん 1=ふち
    r = CAP_R * t
    # 平たいドーム：まん中の小さなふくらみ＋全体はゆるいカーブ、外の3割で下に垂れる
    y = CAP_TOP - 0.55 * t ** 2.2 - 0.45 * max(0.0, (t - 0.7) / 0.3) ** 1.8 + 0.08 * (1 - t) ** 3
    prof.append((r, y))
# ふち：薄く、ほんの少し内側へ巻きこむ
prof[0] = (0.001, prof[0][1])
prof += [(CAP_R * 0.995, 0.08), (CAP_R * 0.975, 0.04)]
wob = lambda a, y: 0.012 * math.sin(a * 5 + 0.7) + 0.008 * math.sin(a * 9 + 2.1) + 0.005 * math.sin(a * 17 + 1.0)
rings = lathe(cap, 0, prof[1:], wob=wob)
cap_fan(cap, 0, rings[0], (0, prof[0][1], 0), flip=True)
# ヒダ：ふちの内側から軸へ、ほぼ平らに少しだけ上がる下面。放射状のすじ
gprof = [(CAP_R * 0.975, 0.04), (CAP_R * 0.8, 0.07), (CAP_R * 0.55, 0.12), (CAP_R * 0.32, 0.17), (STEM_R * 1.05, 0.2), (STEM_R * 0.9, 0.21)]
gw = lambda a, y: wob(a, y) + (0.02 * abs(math.sin(a * 40)) if 0.05 < y < 0.19 else 0.0)
grings = lathe(cap, 1, gprof, wob=gw)
cap_fan(cap, 1, grings[-1], (0, 0.22, 0))

# ---- 軸：根元のふくらみ → 上へ少し細く → つば → てっぺん ----
stem = M(1)
H = STEM_TOP
sprof = [(STEM_R * 0.92, H + 0.25), (STEM_R * 0.95, H), (STEM_R, H * 0.84), (STEM_R * 1.04, H * 0.6), (STEM_R * 1.1, H * 0.32),
         (STEM_R * 1.2, H * 0.14), (STEM_R * 1.45, H * 0.06), (STEM_R * 1.62, H * 0.025), (STEM_R * 1.5, 0.12), (STEM_R * 1.3, 0.0)]
srings = lathe(stem, 0, sprof, wob=lambda a, y: (0.025 * math.sin(a * 3 + 1.2) + 0.012 * math.sin(a * 7 + y * 0.4)) * (1 if y < H * 0.1 else 0.5))
cap_fan(stem, 0, srings[0], (0, H + 0.25, 0), flip=True)
# つば：軸の上のほうからふわっと広がって下がるスカート（両面）
ry = H * 0.86
skirt = [(STEM_R * 1.01, ry), (STEM_R * 1.25, ry - 0.12), (STEM_R * 1.6, ry - 0.45), (STEM_R * 1.8, ry - 0.85)]
sw = lambda a, y: 0.06 * math.sin(a * 7 + 0.3) * (y < ry - 0.3)
lathe(stem, 0, skirt, wob=sw)
lathe(stem, 0, [(r - 0.05, y - 0.03) for r, y in skirt], wob=sw, flip=True)

def scaled(m, sx, sy):
    return {'v': [(x * sx, y * sy, z * sx) for x, y, z in m.V], 't': m.T}
# ジャンプ用の小さいキノコ（坂の赤いキノコ）：同じ形を縮める。傘の半径1.2・てっぺんは付け根から0.3、軸は長さ2.98・半径0.3
JUMP_R, JUMP_TOP, JUMP_H, JUMP_SR = 1.2, 0.3, 2.98, 0.3
out = {'GiantCap': {'v': cap.V, 't': cap.T}, 'GiantStem': {'v': stem.V, 't': stem.T},
       'JumpCap': scaled(cap, JUMP_R / CAP_R, JUMP_TOP / (CAP_TOP + 0.08)),
       'JumpStem': scaled(stem, JUMP_SR / STEM_R, JUMP_H / STEM_TOP)}
json.dump(out, open(OUT, 'w'))

try:
    import bpy
    coll = bpy.data.collections.get('GiantMush') or bpy.data.collections.new('GiantMush')
    if coll.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(coll)
    for o in list(coll.objects): bpy.data.objects.remove(o)
    for name, d, dy in (('GiantCap', out['GiantCap'], STEM_TOP), ('GiantStem', out['GiantStem'], 0), ('JumpCap', out['JumpCap'], JUMP_H + 6), ('JumpStem', out['JumpStem'], 6)):
        faces = []
        for sub in d['t']:
            for i in range(0, len(sub), 3): faces.append((sub[i], sub[i + 2], sub[i + 1]))
        me = bpy.data.meshes.new(name); me.from_pydata([(p[0], p[2], p[1] + dy) for p in d['v']], [], faces); me.update()
        o = bpy.data.objects.new(name, me); coll.objects.link(o); o.location = (420, 400, 0)
except ImportError:
    pass
print({k: (len(v['v']), [len(s) // 3 for s in v['t']]) for k, v in out.items()})
