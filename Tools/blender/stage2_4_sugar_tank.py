# FLUFF 2-4 砂糖ポンプのタンクとピストンの筒（2026-11-10 てじゃ「タンク周りのモデルのクオリティアップ」「スキマはあるしシリンダーは丸裸」「タンク自体もただのカプセル」）
# Blender で実行（exec）。座標は Unity の座標で書いて U() で Blender へ（Unity (x,y,z) → Blender (-x, -z, y)）
# できる物：SugarTankV2（原点＝タンクのまん中、Unity の SugarTank の local）、PumpBarrel（原点＝ピストンの軸の上、ピンの所 pc+axisY。+x がタンクの方）
# 材質：Paint / Brass / Glass / Dark / Liquid / Face（Unity で同じ名前の材質に差し替え）
import bpy, bmesh, math
from mathutils import Vector, Matrix

SC_NAME = "FuwaSugarTank"
scn = bpy.data.scenes.get(SC_NAME) or bpy.data.scenes.new(SC_NAME)
bpy.context.window.scene = scn
for o in list(scn.collection.objects):
    bpy.data.objects.remove(o, do_unlink=True)

def U(x, y, z):
    return Vector((-x, -z, y))

MATS = ["Paint", "Brass", "Glass", "Dark", "Liquid", "Face"]
COLS = {"Paint": (0.93, 0.94, 0.97, 1), "Brass": (0.86, 0.68, 0.32, 1), "Glass": (0.8, 0.9, 1, 0.3), "Dark": (0.2, 0.21, 0.24, 1), "Liquid": (0.6, 0.8, 1, 1), "Face": (1, 1, 1, 1)}
def mat(n):
    m = bpy.data.materials.get("SugarTank_" + n) or bpy.data.materials.new("SugarTank_" + n)
    m.diffuse_color = COLS[n]
    return m

class MB:
    def __init__(self):
        self.bm = bmesh.new()
    def v(self, p):
        return self.bm.verts.new(p)
    def f(self, vs, mi):
        try:
            fa = self.bm.faces.new(vs); fa.material_index = mi; return fa
        except ValueError:
            return None
    # 回転体（Unity の y が軸）：prof = [(r, y)]、下から上。cx,cz はずらし
    def lathe(self, prof, segs, mi, cx=0.0, cz=0.0, a0=0.0, closed_ends=True):
        rings = []
        for (r, y) in prof:
            ring = []
            for j in range(segs):
                a = a0 + 2 * math.pi * j / segs
                ring.append(self.v(U(cx + r * math.cos(a), y, cz + r * math.sin(a))) if r > 1e-6 else None)
            if r <= 1e-6:
                ring = [self.v(U(cx, y, cz))]
            rings.append(ring)
        for i in range(len(rings) - 1):
            A, B = rings[i], rings[i + 1]
            for j in range(segs):
                k = (j + 1) % segs
                if len(A) == 1:
                    self.f([A[0], B[k], B[j]], mi)
                elif len(B) == 1:
                    self.f([A[j], A[k], B[0]], mi)
                else:
                    self.f([A[j], A[k], B[k], B[j]], mi)
        if closed_ends:
            if len(rings[0]) > 1: self.f(list(reversed(rings[0])), mi)
            if len(rings[-1]) > 1: self.f(rings[-1], mi)
    # p0→p1 の筒（Unity 座標）
    def tube(self, p0, p1, r, segs, mi, r_in=0.0):
        p0, p1 = Vector(p0), Vector(p1)
        d = (p1 - p0).normalized()
        up = Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
        e1 = d.cross(up).normalized(); e2 = d.cross(e1).normalized()
        def ring(c, rr):
            return [self.v(U(*(c + (e1 * math.cos(2 * math.pi * j / segs) + e2 * math.sin(2 * math.pi * j / segs)) * rr))) for j in range(segs)]
        A, B = ring(p0, r), ring(p1, r)
        for j in range(segs):
            k = (j + 1) % segs
            self.f([A[j], A[k], B[k], B[j]], mi)
        if r_in > 0:
            Ai, Bi = ring(p0, r_in), ring(p1, r_in)
            for j in range(segs):
                k = (j + 1) % segs
                self.f([Ai[k], Ai[j], Bi[j], Bi[k]], mi)
                self.f([A[k], A[j], Ai[j], Ai[k]], mi)
                self.f([B[j], B[k], Bi[k], Bi[j]], mi)
        else:
            self.f(list(reversed(A)), mi); self.f(B, mi)
    def box(self, c, size, mi, rot_y=0.0):
        c = Vector(c); hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        R = Matrix.Rotation(-rot_y, 3, 'Y')   # Unity の y まわり（Unity の Euler(0,deg,0) と同じ向き）
        pts = [c + R @ Vector((sx * hx, sy * hy, sz * hz)) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
        vs = [self.v(U(*p)) for p in pts]
        idx = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        for q in idx: self.f([vs[i] for i in q], mi)
    def bolt(self, c, n, mi, r=0.016, h=0.02):
        c, n = Vector(c), Vector(n).normalized()
        self.tube(c - n * 0.01, c + n * h, r, 6, mi)
    def obj(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(name)
        for n in MATS: me.materials.append(mat(n))
        self.bm.to_mesh(me); self.bm.free()
        ob = bpy.data.objects.new(name, me); scn.collection.objects.link(ob)
        # 材質の順に面を並べる（Unity のサブメッシュの順＝材質の順）
        bpy.context.view_layer.objects.active = ob
        for o in scn.objects: o.select_set(o == ob)
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.sort_elements(type='MATERIAL', elements={'FACE'})
        bpy.ops.object.mode_set(mode='OBJECT')
        for p in me.polygons: p.use_smooth = False
        return ob

PAINT, BRASS, GLASS, DARK, LIQ, FACE = range(6)
R = 0.55
Y_TOP, Y_BOT = 0.45, -0.70        # 胴のつなぎ目（上・下）
H_TOP, H_BOT = 0.27, 0.18         # 上下の鏡板（だ円）の高さ
NECK_R, NECK_Y = 0.16, 0.84
FEET_Y = -1.0

# ===== タンク =====
t = MB()
prof = [(0.0, Y_BOT - H_BOT)]
for i in range(1, 9):
    a = math.radians(90 * i / 8)
    prof.append((R * math.sin(a), Y_BOT - H_BOT * math.cos(a)))
prof.append((R, Y_TOP))
neck_t = math.acos(NECK_R / R)
for i in range(1, 9):
    a = neck_t * i / 8
    prof.append((R * math.cos(a), Y_TOP + H_TOP * math.sin(a)))
prof.append((NECK_R, NECK_Y)); prof.append((0.0, NECK_Y))
t.lathe(prof, 48, PAINT)
# 首のフランジとボルト
t.lathe([(0, NECK_Y - 0.005), (0.23, NECK_Y - 0.005), (0.23, NECK_Y + 0.06), (0, NECK_Y + 0.06)], 32, BRASS)
for j in range(6):
    a = 2 * math.pi * (j + 0.5) / 6
    t.bolt((0.19 * math.cos(a), NECK_Y + 0.06, 0.19 * math.sin(a)), (0, 1, 0), BRASS, 0.018, 0.02)
# 帯（上下のつなぎ目）とボルト
for yc in (Y_TOP, Y_BOT):
    t.lathe([(R - 0.01, yc - 0.045), (R + 0.03, yc - 0.045), (R + 0.03, yc + 0.045), (R - 0.01, yc + 0.045)], 48, BRASS, closed_ends=False)
    t.lathe([(R - 0.01, yc - 0.045), (R + 0.03, yc - 0.045)], 48, BRASS, closed_ends=False)
    for j in range(16):
        a = 2 * math.pi * (j + 0.5) / 16
        t.bolt(((R + 0.03) * math.cos(a), yc, (R + 0.03) * math.sin(a)), (math.cos(a), 0, math.sin(a)), BRASS)
# 脚4本（ななめ）と足の板・ボルト
for j in range(4):
    a = math.radians(45 + 90 * j); d = Vector((math.cos(a), 0, math.sin(a)))
    top = d * (R * 0.82) + Vector((0, Y_BOT - 0.02, 0)); foot = d * (R * 0.98) + Vector((0, FEET_Y + 0.03, 0))
    t.tube(top, foot, 0.04, 8, DARK)
    fc = d * (R * 0.98) + Vector((0, FEET_Y + 0.015, 0))
    t.box(fc, (0.17, 0.03, 0.17), DARK, rot_y=math.degrees(-a))
    for s in (-1, 1):
        side = Vector((-d.z, 0, d.x)) * 0.055 * s
        t.bolt(fc + side + Vector((0, 0.015, 0)), (0, 1, 0), BRASS, 0.014, 0.015)
# 入口（-x、ピストンから）：首とフランジ（筒のフランジと x=-0.75 で合わさる）
t.tube((-0.45, 0, 0), (-0.71, 0, 0), 0.15, 24, PAINT)
t.tube((-0.71, 0, 0), (-0.75, 0, 0), 0.24, 24, BRASS)
for j in range(6):
    a = 2 * math.pi * (j + 0.5) / 6
    t.bolt((-0.71, 0.19 * math.cos(a), 0.19 * math.sin(a)), (1, 0, 0), BRASS, 0.016, 0.018)
# 出口（+x、下、白い管へ）：管の始まり x=0.6
OUT_Y = -0.45
t.tube((0.45, OUT_Y, 0), (0.58, OUT_Y, 0), 0.17, 24, PAINT)
t.tube((0.56, OUT_Y, 0), (0.6, OUT_Y, 0), 0.3, 24, BRASS)
for j in range(8):
    a = 2 * math.pi * (j + 0.5) / 8
    t.bolt((0.56, OUT_Y + 0.26 * math.cos(a), 0.26 * math.sin(a)), (-1, 0, 0), BRASS, 0.016, 0.018)
# 液面計（+z）：ガラスの管・中の液・金の継手
GZ = R + 0.09; g0, g1 = -0.55, 0.32
for gy in (g0, g1):
    t.tube((0, gy, R - 0.06), (0, gy, GZ), 0.026, 10, BRASS)
    t.lathe([(0, gy - 0.05), (0.05, gy - 0.05), (0.05, gy + 0.05), (0, gy + 0.05)], 12, BRASS, cz=GZ)
t.lathe([(0.034, g0 + 0.05), (0.034, g1 - 0.05)], 16, GLASS, cz=GZ, closed_ends=False)
t.lathe([(0, g0 + 0.05), (0.024, g0 + 0.05), (0.024, -0.05), (0, -0.05)], 12, LIQ, cz=GZ)
# 圧力計（肩、-x と +z の間）：足・ケース・文字盤。針は Unity で（GAUGE_* を出力）
ga = math.radians(120)
gt = math.radians(42)
gd = Vector((math.cos(ga), 0, math.sin(ga)))
gp = gd * (R * math.cos(gt)) + Vector((0, Y_TOP + H_TOP * math.sin(gt), 0))
gn = (gd * (math.cos(gt) / R) + Vector((0, math.sin(gt) / H_TOP, 0))).normalized()
nrm = (gd * 0.35 + Vector((0, 1, 0)) * 0.0).normalized()
face_n = (gd + Vector((0, 0.35, 0))).normalized()   # 文字盤は外向き少し上
stem_end = gp + gn * 0.06
t.tube(gp - gn * 0.02, stem_end, 0.022, 10, BRASS)
case_c = stem_end + face_n * 0.0 + Vector((0, 0.07, 0))
t.tube(stem_end, case_c - face_n * 0.01, 0.022, 10, BRASS)
t.tube(case_c - face_n * 0.03, case_c + face_n * 0.03, 0.1, 24, BRASS)
t.tube(case_c + face_n * 0.03, case_c + face_n * 0.034, 0.085, 24, FACE)
t.tube(case_c + face_n * 0.03, case_c + face_n * 0.04, 0.1, 24, BRASS, r_in=0.086)
tank = t.obj("SugarTankV2")
print("GAUGE_C", tuple(round(x, 4) for x in (case_c + face_n * 0.034)), "GAUGE_N", tuple(round(x, 4) for x in face_n))

# ===== ピストンの筒（クランク側は開いている。のぞき窓が ±z に1つずつ）=====
b = MB()
RO, RI = 0.40, 0.36
X0, X1 = 0.62, 1.9
SLOT_X0, SLOT_X1 = 0.88, 1.62
SLOT_HALF = math.radians(28)
segs = 48
xs = [X0, SLOT_X0, SLOT_X1, X1]
def in_slot(a, x0, x1):
    if not (x0 >= SLOT_X0 - 1e-6 and x1 <= SLOT_X1 + 1e-6): return False
    for c in (math.pi / 2, -math.pi / 2):
        d = (a - c + math.pi) % (2 * math.pi) - math.pi
        if abs(d) < SLOT_HALF: return True
    return False
# 角度の並び：窓のふちで切れるように
angs = sorted(set([2 * math.pi * j / segs for j in range(segs)] + [((c + s * SLOT_HALF) % (2 * math.pi)) for c in (math.pi / 2, 3 * math.pi / 2) for s in (-1, 1)]))
def P(r, x, a):
    return U(x, r * math.cos(a), r * math.sin(a))   # 軸 = x、y と z で回る
vo = {}; vi = {}
for x in xs:
    for a in angs:
        vo[(x, a)] = b.v(P(RO, x, a)); vi[(x, a)] = b.v(P(RI, x, a))
n = len(angs)
for xi in range(len(xs) - 1):
    x0, x1 = xs[xi], xs[xi + 1]
    for j in range(n):
        a0, a1 = angs[j], angs[(j + 1) % n]
        am = a0 + (((a1 - a0) % (2 * math.pi)) / 2)
        if in_slot(am, x0, x1):
            continue
        b.f([vo[(x0, a0)], vo[(x0, a1)], vo[(x1, a1)], vo[(x1, a0)]], PAINT)
        b.f([vi[(x0, a1)], vi[(x0, a0)], vi[(x1, a0)], vi[(x1, a1)]], DARK)
# 窓のふち（切り口）
for c in (math.pi / 2, 3 * math.pi / 2):
    lo, hi = (c - SLOT_HALF) % (2 * math.pi), (c + SLOT_HALF) % (2 * math.pi)
    for a in (lo, hi):
        b.f([vo[(SLOT_X0, a)], vi[(SLOT_X0, a)], vi[(SLOT_X1, a)], vo[(SLOT_X1, a)]], BRASS)
    seg = [a for a in angs if ((a - lo) % (2 * math.pi)) <= ((hi - lo) % (2 * math.pi)) + 1e-6]
    seg.sort(key=lambda a: (a - lo) % (2 * math.pi))
    for k in range(len(seg) - 1):
        for x in (SLOT_X0, SLOT_X1):
            b.f([vo[(x, seg[k])], vo[(x, seg[k + 1])], vi[(x, seg[k + 1])], vi[(x, seg[k])]], BRASS)
# クランク側のはし（輪）
for j in range(n):
    a0, a1 = angs[j], angs[(j + 1) % n]
    b.f([vo[(X0, a1)], vo[(X0, a0)], vi[(X0, a0)], vi[(X0, a1)]], PAINT)
# 両はしのフランジ・ボルト
b.tube((X0 - 0.02, 0, 0), (X0 + 0.06, 0, 0), 0.47, 32, BRASS, r_in=RI)
b.tube((X1, 0, 0), (X1 + 0.1, 0, 0), 0.47, 32, BRASS)
for j in range(8):
    a = 2 * math.pi * (j + 0.5) / 8
    b.bolt((X0 + 0.06, 0.43 * math.cos(a), 0.43 * math.sin(a)), (1, 0, 0), BRASS)
    b.bolt((X1, 0.43 * math.cos(a), 0.43 * math.sin(a)), (-1, 0, 0), BRASS)
# 筒の奥（タンク側）の内がわのふた
b.tube((X1 - 0.005, 0, 0), (X1 + 0.001, 0, 0), RI + 0.001, 32, DARK)
# 帯（窓をよけて）
for xr in (0.74, 1.76):
    b.tube((xr - 0.03, 0, 0), (xr + 0.03, 0, 0), RO + 0.025, 48, BRASS, r_in=RO - 0.005)
# 台（サドル2つ）：台の上 y=-1.01（軸から）
for xs_ in (0.78, 1.74):
    b.box((xs_, -0.695, 0), (0.08, 0.63, 0.4), DARK)
    b.box((xs_, -0.995, 0), (0.2, 0.03, 0.62), DARK)
barrel = b.obj("PumpBarrel")
print("ok", len(tank.data.vertices), len(barrel.data.vertices))
