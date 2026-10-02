# 島の下の▼（土＋岩のとんがり）。上面＝z=0、半径1、深さ1（Unityで半径・深さを別々に拡大）。上はふた付き（上の床の中に隠れる）
import bpy, json, math, random
from mathutils import Vector, noise
SCR = r'C:\Users\tejas\AppData\Local\Temp\claude\D--Claude-Workspace-ClaudeDiscord\e6d24fd8-b7a4-4eeb-90a0-47b449e1d858\scratchpad'
col = bpy.data.collections.get('FarIslands')
SOIL = (0.56, 0.42, 0.32); SOIL2 = (0.48, 0.36, 0.30); ROCK = (0.66, 0.58, 0.70); ROCK2 = (0.46, 0.40, 0.54)
def lerp3(a, b, t): return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))
out = {}
for (vi, seed, SEG, n, suffix) in [(0, 11, 56, 10, ''), (1, 12, 56, 10, ''), (2, 13, 56, 10, ''), (0, 11, 22, 5, 'Low'), (1, 12, 22, 5, 'Low'), (2, 13, 22, 5, 'Low')]:
    random.seed(seed); off = Vector((seed * 2.3, seed * 0.7, 0))
    V = []; C = []; F = []; rings = []
    def rim(a): return 1.0 + 0.04 * noise.noise(off + Vector((math.cos(a) * 1.5, math.sin(a) * 1.5, 0)))
    # 上は床の真下（ふちは床より少し内側＝はみ出さない）、土の帯→岩→とがった先
    prof = [(0.97, 0.0, SOIL), (0.97, -0.05, SOIL), (0.93, -0.12, SOIL2), (0.86, -0.18, ROCK)]
    for i in range(1, n + 1):
        t = i / n
        prof.append((0.86 * (1 - t) ** 0.8, -0.18 - 0.82 * t, lerp3(ROCK, ROCK2, t)))
    for (rr, zz, cc) in prof:
        ring = []
        for s in range(SEG):
            a = s / SEG * math.tau
            bump = 1 + (0.16 * noise.noise(off + Vector((math.cos(a) * 2.4, math.sin(a) * 2.4, zz * 2.2))) if zz < -0.1 else 0)
            r = max(0.0, rr * rim(a) * bump)
            ring.append(len(V)); V.append((r * math.cos(a), r * math.sin(a), zz)); C.append(cc)
        rings.append(ring)
    tip = len(V); V.append((0.04, -0.02, -1.05)); C.append(ROCK2)
    top = len(V); V.append((0, 0, 0)); C.append(SOIL)
    for s in range(SEG): F.append((top, rings[0][(s + 1) % SEG], rings[0][s]))
    for k in range(len(rings) - 1):
        A, B = rings[k], rings[k + 1]
        for s in range(SEG): F.append((A[s], B[s], B[(s + 1) % SEG], A[(s + 1) % SEG]))
    last = rings[-1]
    for s in range(SEG): F.append((last[(s + 1) % SEG], last[s], tip))
    name = 'IslandUnder%s_%d' % (suffix, vi)
    me = bpy.data.meshes.get(name) or bpy.data.meshes.new(name)
    me.clear_geometry(); me.from_pydata(V, [], F); me.update()
    for p in me.polygons: p.use_smooth = True
    o = bpy.data.objects.get(name) or bpy.data.objects.new(name, me)
    if o.name not in col.objects: col.objects.link(o)
    o.location = (vi * 3, -16 - (3 if suffix else 0), 2)
    me.calc_loop_triangles()
    out[name] = {'v': [(v.co.x, v.co.z, v.co.y) for v in me.vertices], 'n': [(v.normal.x, v.normal.z, v.normal.y) for v in me.vertices],
                 'c': [(c[0], c[1], c[2], 1.0) for c in C], 'u': [(-1, -1)] * len(V),
                 't': [(t.vertices[0], t.vertices[2], t.vertices[1]) for t in me.loop_triangles]}
json.dump(out, open(SCR + r'\island_under.json', 'w'))
print({k: (len(v['v']), len(v['t'])) for k, v in out.items()})
