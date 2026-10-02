# まるっこい雲：丸い玉を重ねて、ボクセルでひとつにつないで、つなぎ目だけ少しなめらかにする（こぶの丸さは残す）
import bpy, bmesh, json, math, random
SCR = r'C:\Users\tejas\AppData\Local\Temp\claude\D--Claude-Workspace-ClaudeDiscord\e6d24fd8-b7a4-4eeb-90a0-47b449e1d858\scratchpad'
col = bpy.data.collections.get('Clouds')

def make_cloud(name, seed, length, width):
    random.seed(seed)
    o = bpy.data.objects.get(name)
    if o: bpy.data.objects.remove(o)
    bm = bmesh.new()
    def ball(x, y, z, r):
        m = bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=r)
        for v in m['verts']: v.co.x += x; v.co.y += y; v.co.z += z
    # 底の段：横に並んだ玉（大きさ・位置をバラバラに）
    nb = max(3, int(length / 2.0))
    for i in range(nb):
        t = i / max(1, nb - 1) + random.uniform(-0.08, 0.08); t = min(1, max(0, t)); h = math.sin(t * math.pi)
        r = (1.0 + 1.1 * h) * random.uniform(0.7, 1.3)
        x = (t - 0.5) * (length - 2 * r)
        ball(x, random.uniform(-width * 0.3, width * 0.3), r * random.uniform(0.4, 0.7), r)
    # 上のこぶ：数も大きさも高さもランダム、中心から片寄らせる
    nt = 2 + int(length / 4) + random.randint(0, 2)
    skew = random.uniform(-0.2, 0.2)
    for i in range(nt):
        t = random.uniform(0.12, 0.88); h = math.sin(t * math.pi)
        r = (0.9 + 1.5 * h) * random.uniform(0.6, 1.3)
        x = (t - 0.5 + skew) * length * 0.6
        ball(x, random.uniform(-width * 0.25, width * 0.25), random.uniform(0.6, 1.0) + 1.5 * h * random.uniform(0.5, 1.1), r)
    # 小さなはみ出しこぶ
    for i in range(random.randint(2, 4)):
        t = random.uniform(0.05, 0.95)
        ball((t - 0.5) * length * 0.85, random.uniform(-width * 0.4, width * 0.4), random.uniform(0.3, 1.2), random.uniform(0.6, 1.1))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); col.objects.link(o)
    bpy.context.view_layer.objects.active = o
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True)
    rm = o.modifiers.new('rm', 'REMESH'); rm.mode = 'VOXEL'; rm.voxel_size = 0.13
    bpy.ops.object.modifier_apply(modifier='rm')
    tex = bpy.data.textures.get('CloudNoise') or bpy.data.textures.new('CloudNoise', 'CLOUDS')
    tex.noise_scale = 1.4; tex.noise_depth = 2
    dp = o.modifiers.new('dp', 'DISPLACE'); dp.texture = tex; dp.strength = 0.55; dp.mid_level = 0.5; dp.texture_coords = 'LOCAL'
    bpy.ops.object.modifier_apply(modifier='dp')
    sm = o.modifiers.new('sm', 'SMOOTH'); sm.factor = 0.6; sm.iterations = 5
    bpy.ops.object.modifier_apply(modifier='sm')
    bm = bmesh.new(); bm.from_mesh(o.data)
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        if v.co.z < zmin + 0.7: v.co.z = zmin + 0.7 - (zmin + 0.7 - v.co.z) * 0.3
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts: v.co.z -= zmin
    # はぐれた小さなかけらを消す（一番大きい塊だけ残す）
    bm.verts.ensure_lookup_table()
    seen = set(); parts = []
    for v in bm.verts:
        if v.index in seen: continue
        stack = [v]; comp = []; seen.add(v.index)
        while stack:
            x = stack.pop(); comp.append(x)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen: seen.add(y.index); stack.append(y)
        parts.append(comp)
    parts.sort(key=len, reverse=True)
    for comp in parts[1:]: bmesh.ops.delete(bm, geom=comp, context='VERTS')
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.to_mesh(o.data); bm.free()
    d = o.modifiers.new('dec', 'DECIMATE'); d.ratio = 0.04
    bpy.ops.object.modifier_apply(modifier='dec')
    for p in o.data.polygons: p.use_smooth = True
    return o

out = {}
for i, (nm, seed, L, W) in enumerate([('CloudR_A', 3, 12, 4), ('CloudR_B', 8, 8, 3.5), ('CloudR_C', 21, 15, 5), ('CloudR_D', 34, 10, 5), ('CloudR_E', 47, 17, 4.5)]):
    o = make_cloud(nm, seed, L, W)
    o.location = (i * 20, 40, 9)
    me = o.data; me.calc_loop_triangles()
    out[nm] = {'v': [(v.co.x, v.co.z, v.co.y) for v in me.vertices],
               'n': [(v.normal.x, v.normal.z, v.normal.y) for v in me.vertices],
               't': [(t.vertices[0], t.vertices[2], t.vertices[1]) for t in me.loop_triangles]}
json.dump(out, open(SCR + r'\clouds_round.json', 'w'))
print({k: (len(v['v']), len(v['t'])) for k, v in out.items()})
for n in out: print(n, [round(d, 2) for d in bpy.data.objects[n].dimensions])
