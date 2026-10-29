import bpy, bmesh, math
# 2–1 貝の道の貝（2026-10-29 てじゃ「貝のモデル忘れてた」→ ホタテ型の下書き →「遠目だとシャコガイのほうがめだつかも」でシャコガイ型）。
# 下の殻（Stage2_ClamBottom）・ふた（Stage2_ClamTop）・真珠（Stage2_ClamPearl）を、貝のローカル座標（Unity：+Z＝泡を吹く向き、原点＝台の上）のまま書き出す。
# ちょうつがいは (0, HINGE_Y, HINGE_Z)。Unity 側はふた（LidHinge）の下に ShellTop を -ちょうつがい の位置で置く（FuwaCourse2_1Builder の ClamHingeY/Z と同じ数字）。
# 殻：上から見て横長のだ円（ちょうつがいはだ円の後ろのはし）。ちょうつがいから放射状に太いうね RIBS 本＝ふちが大きなジグザグ。ふちは両方の殻で同じ波＝閉じるとかみ合う。
# 内側（身）は別の材質（2番目）。
# 実行：Blender（FLUFF_models.blend）で exec(open(r"D:\UnityProject\FLUFF\Tools\stage2_clam.py", encoding="utf-8").read())
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Models\Stage2_Clam.fbx"
HINGE_Y, HINGE_Z = 0.22, -0.42
ELL_A, ELL_B, ELL_IN = 0.68, 0.48, 0.05   # だ円の横・前後の半分、ちょうつがいがだ円の後ろのはしから内に入る量
TH_MAX = math.radians(100)
RIBS, RIB_AMP, FINE_AMP, FINE_N = 6, 0.17, 0.008, 4   # 太いうね（ジグザグ）と、うねの上の細いすじ
H_TOP, H_BOT = 0.20, 0.22
THICK = 0.035
N_TH, N_R, N_RI = 72, 9, 6
EAR = []   # 耳（ホタテ用。シャコガイはなし）
PEARL_R, PEARL_AT = 0.15, 0.42   # てじゃ「玉大きくしよう」0.085→0.15
sc = bpy.data.scenes.get("Stage2Clam") or bpy.data.scenes.new("Stage2Clam")
bpy.context.window.scene = sc

def B(x, y, z): return (-x, -z, y)   # Unity (x,y,z) → Blender

def R(th):
    # ちょうつがいから角度 th の向きに、だ円のふちまでの距離
    s, c = math.sin(th), math.cos(th); C = ELL_B - ELL_IN
    A = s * s / ELL_A ** 2 + c * c / ELL_B ** 2; Bq = c * C / ELL_B ** 2; Cq = C * C / ELL_B ** 2 - 1.0
    return (Bq + math.sqrt(max(Bq * Bq - A * Cq, 0.0))) / A
def rib(th):
    t = (th + TH_MAX) / (2 * TH_MAX)
    z = 1.0 - abs(2.0 * ((RIBS * t + 0.5) % 1.0) - 1.0)
    z = 0.8 * z + 0.2 * z * z * (3 - 2 * z)                  # ジグザグ（山と谷をほんの少し丸く）
    return z   # 0〜1
def fine(th):
    t = (th + TH_MAX) / (2 * TH_MAX)
    return math.cos(2 * math.pi * RIBS * FINE_N * t)
def dome(u, th, H):
    return H * math.sin(math.pi * min(max(u, 0.0), 1.0)) ** 0.75 * max(math.cos(th / TH_MAX * math.pi / 2), 0.0) ** 0.45
def pt(u, th, sgn, H, inner=False):
    r = u * R(th)
    x, z = r * math.sin(th), HINGE_Z + r * math.cos(th)
    w = (RIB_AMP * (rib(th) - 0.5) + FINE_AMP * fine(th)) * u ** 1.3
    d = dome(u, th, H)
    if inner: d = max(d - THICK, 0.0) * 0.97   # うねは外と同じ（小さくすると山の所で内の面が外へつき抜けた）
    return (x, HINGE_Y + w + sgn * d, z)

def shell(name, sgn, H, lift):
    bm = bmesh.new()
    ths = [-TH_MAX + 2 * TH_MAX * k / N_TH for k in range(N_TH + 1)]
    U_IN = 0.965   # 内側の面のはし（ふちの厚み）
    outer = [[bm.verts.new(B(*pt(j / N_R, th, sgn, H))) for th in ths] for j in range(1, N_R + 1)]
    inner = [[bm.verts.new(B(*pt(U_IN * j / N_RI, th, sgn, H, True))) for th in ths] for j in range(1, N_RI + 1)]
    ho = bm.verts.new(B(0, HINGE_Y, HINGE_Z)); hi = bm.verts.new(B(0, HINGE_Y + sgn * 0.002, HINGE_Z + 0.01))
    faces_in = []
    def quad(a, b, c, d, mi):
        f = bm.faces.new((a, b, c, d)); f.material_index = mi; return f
    for k in range(N_TH):
        f = bm.faces.new((ho, outer[0][k], outer[0][k + 1])); f.material_index = 0
        f = bm.faces.new((hi, inner[0][k + 1], inner[0][k])); f.material_index = 1
        for j in range(N_R - 1): quad(outer[j][k], outer[j + 1][k], outer[j + 1][k + 1], outer[j][k + 1], 0)
        for j in range(N_RI - 1): quad(inner[j][k + 1], inner[j + 1][k + 1], inner[j + 1][k], inner[j][k], 1)
        quad(outer[-1][k], inner[-1][k], inner[-1][k + 1], outer[-1][k + 1], 0)   # ふちの厚み
    # 耳：ちょうつがいの左右、殻の面の上（ふた）/下（下の殻）に厚み THICK の板
    ear_faces = []
    for sx in ((-1, 1) if EAR else ()):
        y0, y1 = (HINGE_Y, HINGE_Y + sgn * THICK)
        lo = [bm.verts.new(B(sx * x, y0, HINGE_Z + z)) for x, z in EAR]
        up = [bm.verts.new(B(sx * x, y1, HINGE_Z + z)) for x, z in EAR]
        n = len(EAR)
        ear_faces += [bm.faces.new(lo), bm.faces.new(up[::-1])]
        for k in range(n):
            k1 = (k + 1) % n; ear_faces.append(bm.faces.new((lo[k], lo[k1], up[k1], up[k])))
    for f in ear_faces: f.material_index = 0
    bmesh.ops.recalc_face_normals(bm, faces=ear_faces)
    # 殻の面の向き：殻の外の面は sgn の向き（ふた＝上、下の殻＝下）。逆なら全部裏返す
    tot = 0.0
    for f in bm.faces:
        if f.material_index == 0 and len(f.verts) == 4 and f not in ear_faces: tot += f.normal.z * sgn * f.calc_area()
    if tot < 0: bmesh.ops.reverse_faces(bm, faces=[f for f in bm.faces if f not in ear_faces])
    bmesh.ops.triangulate(bm, faces=bm.faces)
    for v in bm.verts: v.co.z += lift
    me = bpy.data.meshes.new(name)
    for m in ("Stage2_ClamShell", "Stage2_ClamInner"): me.materials.append(mat(m))   # 先に材質の枠を作る（枠がないと to_mesh で材質番号が全部 0 になった）
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    return me

def mat(n):
    return bpy.data.materials.get(n) or bpy.data.materials.new(n)

def put(name, me, mats):
    o = sc.objects.get(name)
    if o: bpy.data.objects.remove(o, do_unlink=True)
    if [m.name for m in me.materials] != mats:
        me.materials.clear()
        for m in mats: me.materials.append(mat(m))
    # FBX のサブメッシュ順＝面の並び順（材質0→1 に並べ替え）
    bm = bmesh.new(); bm.from_mesh(me)
    order = sorted(bm.faces, key=lambda f: f.material_index)
    for i, f in enumerate(order): f.index = i
    bm.faces.sort(key=lambda f: f.index); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    return o

bot = put("Stage2_ClamBottom", shell("Stage2_ClamBottom", -1, H_BOT, 0.0), ["Stage2_ClamShell", "Stage2_ClamInner"])
top = put("Stage2_ClamTop", shell("Stage2_ClamTop", 1, H_TOP, 0.004), ["Stage2_ClamShell", "Stage2_ClamInner"])
# 真珠：下の殻の内側の面にのせる
u = PEARL_AT; py = pt(u, 0.0, -1, H_BOT, True)[1] + PEARL_R * 0.85; pz = HINGE_Z + u * R(0.0)
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=PEARL_R)
for v in bm.verts: v.co += __import__("mathutils").Vector(B(0, py, pz))
me = bpy.data.meshes.new("Stage2_ClamPearl"); bm.to_mesh(me); bm.free()
for p in me.polygons: p.use_smooth = True
pearl = put("Stage2_ClamPearl", me, ["Stage2_ClamPearl"])

for x in sc.objects: x.select_set(False)
for o in (bot, top, pearl): o.select_set(True)
bpy.context.view_layer.objects.active = bot
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE')
bpy.ops.wm.save_mainfile()
print("clam tris", len(bot.data.polygons), len(top.data.polygons), len(pearl.data.polygons), "pearl y", round(py, 3))
