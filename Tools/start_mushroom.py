# スタートのキノコ（ベニテングタケ風、イボなし）。ふわふわが待っている所で、撃つとポヨンと揺れる。
# 傘はポヨン用に「傘の付け根（軸のてっぺん）」を原点にして作る（スクリプトが傘をつぶして揺らすので、付け根が軸から離れない）。
# 書き出し（Unity 座標、メートル）：
#   'StartCap'  … 傘（submesh0 = 赤い傘の表、submesh1 = 白いヒダ）。原点＝傘の付け根
#   'StartStem' … 軸（つば・根元のふくらみつき）。原点＝地面
# 使い方：Blenderで exec(open(r'...\Tools\start_mushroom.py', encoding='utf-8').read())
import json, math
from mathutils import Vector
OUT = globals().get('OUT', r'D:\UnityProject\FLUFF\Tools\start_mushroom.json')

SEG = 48
CAP_R, CAP_H = 0.40, 0.30       # 傘の半径・高さ
STEM_TOP = 0.50                 # 軸のてっぺん（＝傘の付け根の高さ）

class M:
    def __init__(self, nsub=1): self.V = []; self.T = [[] for _ in range(nsub)]
    def v(self, p): self.V.append(tuple(p)); return len(self.V) - 1

def lathe(m, sub, prof, seg=SEG, wob=None, flip=False):
    # prof：[(半径, 高さ)] 上から下へ。外側を向いた面を作る（flip で内向き）
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
            # Unity の表向き：外から見て時計回り
            if not flip: m.T[sub] += [q[0], q[1], q[2], q[0], q[2], q[3]]
            else: m.T[sub] += [q[0], q[2], q[1], q[0], q[3], q[2]]
    return rings

def cap_fan(m, sub, ring, center, flip=False):
    c = m.v(center)
    n = len(ring)
    for i in range(n):
        j = (i + 1) % n
        if not flip: m.T[sub] += [c, ring[i], ring[j]]
        else: m.T[sub] += [c, ring[j], ring[i]]

# ---- 傘：てっぺんからふちへ、丸いドーム→ふちは少し内側に巻きこむ ----
cap = M(2)
prof = []
N = 18
for k in range(N + 1):
    t = k / N                                  # 0=てっぺん 1=ふち
    ang = t * math.pi * 0.5
    r = CAP_R * math.sin(ang)
    y = 0.06 + CAP_H * math.cos(ang) ** 1.15   # 付け根より少し上から始まる丸み
    prof.append((r, y))
# ふちの巻きこみ：外→内へ少し下がる
prof += [(CAP_R * 0.985, 0.035), (CAP_R * 0.95, 0.02)]
wob = lambda a, y: 0.012 * math.sin(a * 5 + 0.7) + 0.008 * math.sin(a * 9 + 2.1)
rings = lathe(cap, 0, prof, wob=wob)
cap_fan(cap, 0, rings[0], (0, prof[0][1], 0), flip=True)   # てっぺんのふた（上向き）
# ヒダ：ふちの内側から軸へ、少し上がっていく下面。放射状のすじ（凸凹）をつける
gprof = [(CAP_R * 0.95, 0.02), (CAP_R * 0.75, 0.035), (CAP_R * 0.5, 0.05), (0.09, 0.07), (0.05, 0.078)]
gw = lambda a, y: wob(a, y) + (0.025 * abs(math.sin(a * 24)) if 0.025 < y < 0.06 else 0.0)   # いちばん外の輪は傘のふちと同じゆがみ（すき間ができないように）
grings = lathe(cap, 1, gprof, wob=gw, flip=False)
cap_fan(cap, 1, grings[-1], (0, 0.085, 0), flip=False)   # ヒダの真ん中もふさぐ（ポヨンで傘が横に広がっても、軸のまわりにすき間が見えないように）

# ---- 軸：根元のふくらみ → まっすぐ少し細く → つば（スカート）→ てっぺん ----
stem = M(1)
sprof = [(0.065, STEM_TOP + 0.08), (0.068, STEM_TOP), (0.072, 0.42), (0.075, 0.30), (0.082, 0.16),
         (0.11, 0.08), (0.125, 0.045), (0.115, 0.015), (0.09, 0.0)]
srings = lathe(stem, 0, sprof, wob=lambda a, y: 0.03 * math.sin(a * 3 + 1.2) * (1 if y < 0.1 else 0.3))
cap_fan(stem, 0, srings[0], (0, STEM_TOP + 0.08, 0), flip=True)
# つば：軸の上のほう(0.40)からふわっと広がって下がるスカート（外側と内側の両面）
skirt = [(0.074, 0.405), (0.10, 0.395), (0.13, 0.37), (0.145, 0.34)]
sw = lambda a, y: 0.06 * math.sin(a * 7 + 0.3) * (y < 0.38)
lathe(stem, 0, skirt, wob=sw)
skirt_in = [(r - 0.006, y - 0.004) for r, y in skirt]
lathe(stem, 0, skirt_in, wob=sw, flip=True)

def shift(m, dy):
    m.V = [(x, y + dy, z) for x, y, z in m.V]

out = {'StartCap': {'v': cap.V, 't': cap.T}, 'StartStem': {'v': stem.V, 't': stem.T}}
json.dump(out, open(OUT, 'w'))

try:
    import bpy
    coll = bpy.data.collections.get('StartMush') or bpy.data.collections.new('StartMush')
    if coll.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(coll)
    for o in list(coll.objects): bpy.data.objects.remove(o)
    for name, d, dy in (('StartCap', out['StartCap'], STEM_TOP), ('StartStem', out['StartStem'], 0)):
        faces = []
        for sub in d['t']:
            for i in range(0, len(sub), 3): faces.append((sub[i], sub[i + 2], sub[i + 1]))
        me = bpy.data.meshes.new(name); me.from_pydata([(p[0], p[2], p[1] + dy) for p in d['v']], [], faces); me.update()
        o = bpy.data.objects.new(name, me); coll.objects.link(o); o.location = (400, 400, 0)
except ImportError:
    pass
print({k: (len(v['v']), [len(s) // 3 for s in v['t']]) for k, v in out.items()})
