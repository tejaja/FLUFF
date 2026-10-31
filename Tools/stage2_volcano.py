import bpy, bmesh, json, math, os
import numpy as np
# 2–2 の火山（2026-10-31 てじゃ「目玉からいこ」→ Blender で作り直し）。
# Unity の VolcanoRAt を Tools/stage2_volcano.json に書き出した半径の表を土台に、大きさ・位置は今のまま、尾根・段・欠けた火口・溶岩の川を足す。
# 形とテクスチャは同じノイズから作るので、尾根と色・川と光がぴったり合う。UV：u が角度、v が 火口の池のまん中 → 内壁 → ふち → すそ → 下のとがり。
# テクスチャ：RGB が岩の色、A が光（0 なし、0.25〜0.7 割れ目と熱、0.75 以上 流れる溶岩）。流れる模様は Unity のシェーダー（FuwaCourse/Volcano）。
# 実行：Blender（FLUFF_models.blend）で exec(open(r"D:\UnityProject\FLUFF\Tools\stage2_volcano.py", encoding="utf-8").read())
DIR = r"D:\UnityProject\FLUFF\Tools"
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Models\Stage2_Volcano.fbx"
OUT_TEX = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Textures\Stage2_Volcano.png"
OUT_FLOW = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Textures\Stage2_LavaFlow.png"
TW, TH = 2048, 1024
COLS = 144
POND_Y = 14.4
TOP_H = 16.0
BOTTOM_H, TIP_H = -60.0, -74.0
# 道の高さの帯：ここでは土台の半径からあまり出ない（道・まもののエリアに当たらない）
BAND_LO, BAND_HI, BAND_OUT = -6.0, 10.0, 0.55
BAND_SINK = 0.95   # 帯の中は全体を内側へ（出っぱりが頭打ちで平らにならないように）
JETS = [-30.0, -8.0, 14.0]   # 炎の岩の角度（ここは山肌を引っこめない）
# ⑤の道（山ぞい）の角度。炎の岩が山肌から離れないように、ここでは土台より引っこみすぎない
ROAD_TH0, ROAD_TH1, ROAD_IN = -52.0, 30.0, 1.5
# 溶岩の川（度、太さ m、終わる高さ、うねりの大きさ度）。-128 がジグザグ側で一番太い。⑤の道の後ろは細く
STREAMS = [(-128.0, 1.4, -58.0, 13.0), (-70.0, 0.8, -22.0, 9.0), (-14.0, 0.7, -14.0, 7.0), (62.0, 1.1, -40.0, 11.0), (158.0, 0.9, -30.0, 10.0)]
# v の割り当て：池 → 内壁 → ふち → すそ（道より下まで）→ 下
V_POND, V_RIM, V_LOW = 0.07, 0.13, 0.86
LOW_H = -22.0

d = json.load(open(os.path.join(DIR, "stage2_volcano.json")))
RB = np.array(d["rows"], dtype=np.float32); RB_N = int(d["angles"]); RB_H0, RB_DH = float(d["h0"]), float(d["dh"])

def rbase(a, h):
    # 半径の表（角度はループ、高さは端で止める）を線形補間
    fa = (np.asarray(a) / (2 * np.pi) % 1.0) * RB_N
    fh = np.clip((np.asarray(h) - RB_H0) / RB_DH, 0, RB.shape[0] - 1.001)
    ia = np.floor(fa).astype(int); ih = np.floor(fh).astype(int)
    ta = fa - ia; th = fh - ih
    ia0 = ia % RB_N; ia1 = (ia + 1) % RB_N
    r0 = RB[ih, ia0] * (1 - ta) + RB[ih, ia1] * ta
    r1 = RB[ih + 1, ia0] * (1 - ta) + RB[ih + 1, ia1] * ta
    return r0 * (1 - th) + r1 * th

rng = np.random.RandomState(26)
PERM = rng.permutation(1024); PERM = np.concatenate([PERM, PERM])
GRAD = rng.rand(1024).astype(np.float32)

def vnoise3(x, y, z):
    # 値ノイズ（0〜1）。位置で取るので角度のつなぎ目が出ない
    xi = np.floor(x).astype(int); yi = np.floor(y).astype(int); zi = np.floor(z).astype(int)
    fx = x - xi; fy = y - yi; fz = z - zi
    ux = fx * fx * (3 - 2 * fx); uy = fy * fy * (3 - 2 * fy); uz = fz * fz * (3 - 2 * fz)
    def h(i, j, k): return GRAD[PERM[(PERM[(PERM[(xi + i) & 1023] + yi + j) & 1023] + zi + k) & 1023]]
    a = h(0,0,0) * (1-ux) + h(1,0,0) * ux; b = h(0,1,0) * (1-ux) + h(1,1,0) * ux
    c = h(0,0,1) * (1-ux) + h(1,0,1) * ux; e = h(0,1,1) * (1-ux) + h(1,1,1) * ux
    return (a * (1-uy) + b * uy) * (1-uz) + (c * (1-uy) + e * uy) * uz

def fbm(x, y, z, oct=4):
    s = np.zeros_like(x, dtype=np.float32); amp = 0.5; tot = 0.0; f = 1.0
    for k in range(oct):
        s += amp * vnoise3(x * f + k * 13.7, y * f + k * 7.1, z * f + k * 3.3); tot += amp; amp *= 0.5; f *= 2.03
    return s / tot

def sstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

def wrap_pi(a): return (a + np.pi) % (2 * np.pi) - np.pi

def rim_height(a):
    # ふちの高さ：ゆるく上下して、川の始まりは池の少し上まで欠ける
    y = TOP_H + 0.75 * (2 * fbm(np.sin(a) * 1.7 + 4.0, np.cos(a) * 1.7 + 9.0, np.zeros_like(a), 3) - 1)
    y = y + 0.35 * np.sin(3 * a + 0.7)
    for th, w, _, _ in STREAMS:
        notch = np.exp(-(wrap_pi(a - math.radians(th)) * 4.6 / (w / 1.4 * 0.45 + 0.55)) ** 2)
        y = y * (1 - notch) + (POND_Y + 0.14) * notch
    return y

def flank_h(a, t, yr):
    # t：0 ふち → 1 道より下の LOW_H
    return yr + (LOW_H - yr) * t

def nominal(a, v):
    # 形の下書き（出っぱりなし）：位置 x,y,z と 高さ h。v の区間ごとに火口・すそをつなぐ
    a = np.asarray(a, dtype=np.float64); v = np.asarray(v, dtype=np.float64)
    yr = rim_height(a); rtop = rbase(a, np.full_like(a, TOP_H))
    q = rtop - 1.25
    r = np.zeros_like(a); y = np.zeros_like(a)
    m = v < V_POND
    t = v / V_POND
    r = np.where(m, q * t, r); y = np.where(m, POND_Y, y)
    m = (v >= V_POND) & (v < V_RIM)
    t = np.clip((v - V_POND) / (V_RIM - V_POND), 0, 1)
    tt = t * t * (3 - 2 * t)
    r = np.where(m, q + (rtop - 0.05 - q) * tt, r); y = np.where(m, POND_Y + (yr - POND_Y) * t ** 0.8, y)
    m = (v >= V_RIM) & (v < V_LOW)
    t = np.clip((v - V_RIM) / (V_LOW - V_RIM), 0, 1)
    hf = flank_h(a, t, yr)
    r = np.where(m, rbase(a, hf), r); y = np.where(m, hf, y)
    m = v >= V_LOW
    t = np.clip((v - V_LOW) / (1 - V_LOW), 0, 1)
    h1 = LOW_H + (BOTTOM_H - LOW_H) * np.minimum(t / 0.9, 1)
    rl = rbase(a, h1)
    tip = np.clip((t - 0.9) / 0.1, 0, 1)
    r = np.where(m, rl * (1 - tip), r); y = np.where(m, h1 + (TIP_H - BOTTOM_H) * tip, y)
    return r, y

def stream_field(a, y, r):
    # 川：いちばん近い川の中心までの距離（m）と、太さ、終わりのフェード
    best = np.full_like(a, 1e3); width = np.ones_like(a); life = np.zeros_like(a)
    for i, (th, w, hend, amp) in enumerate(STREAMS):
        depth = TOP_H - y
        c = math.radians(th) + math.radians(amp) * (np.sin(depth * 0.19 + i * 1.3) * np.clip(depth / 8, 0, 1) + 0.55 * np.sin(depth * 0.47 + i * 2.1) * np.clip(depth / 14, 0, 1))
        w1 = w * (0.75 + 0.45 * np.clip(depth / 30, 0, 1)) * (0.85 + 0.15 * np.sin(depth * 0.9 + i))
        dist = np.abs(wrap_pi(a - c)) * np.maximum(r, 0.5)
        nd = dist / w1
        l = sstep(hend, hend + 9.0, y)
        better = nd < best / np.maximum(width, 1e-3)
        best = np.where(better, dist, best); width = np.where(better, w1, width); life = np.where(better, l, life)
    return best, width, life

def surface(a, v):
    # 出っぱりを足した形と、色に使う値を返す（メッシュとテクスチャで同じ計算）
    r0, y0 = nominal(a, v)
    px, pz = np.sin(a) * r0, np.cos(a) * r0
    flank = (v >= V_RIM) * sstep(V_RIM, V_RIM + 0.03, v)
    # 尾根：角度方向に細かく、縦に長いノイズの谷を反転（縦に走るスジ）。下ほど太く大きく
    k = 0.42
    ang = np.stack([np.sin(a), np.cos(a)])
    warp = 2.2 * (fbm(px * 0.07 + 11.0, y0 * 0.05, pz * 0.07 + 3.0, 2) - 0.5)
    rx, rz = np.sin(a + warp * 0.09) * 13.0, np.cos(a + warp * 0.09) * 13.0
    ridge1 = 1 - 2 * np.abs(fbm(rx * k, y0 * 0.045, rz * k, 3) - 0.5) * 2
    ridge2 = 1 - 2 * np.abs(fbm(rx * k * 2.3 + 5.0, y0 * 0.09 + 3.0, rz * k * 2.3, 3) - 0.5) * 2
    ridge = ridge1 * 0.68 + ridge2 * 0.32
    # 段：溶岩が固まった層の棚（高さを少し波打たせて）
    yw = y0 + 2.6 * (fbm(px * 0.13, np.zeros_like(px), pz * 0.13, 2) - 0.5) * 2
    step_t = (yw / 3.4) % 1.0
    ledge = sstep(0.0, 0.18, step_t) - sstep(0.18, 1.0, step_t) * 0.9
    # ごつごつ（小さいかたまり）
    lump = fbm(px * 0.5 + 2.0, y0 * 0.5, pz * 0.5 + 8.0, 3) - 0.5
    grow = 0.65 + 1.15 * sstep(BAND_HI, -35.0, y0)
    dsp = (ridge * 1.75 + ledge * 0.42 + lump * 1.3) * grow * flank
    # 川：まん中をへこませて、両わきに土手
    sd, sw, sl = stream_field(a, y0, r0)
    nd = sd / sw
    channel = (1 - sstep(0.55, 1.25, nd)) * sl * flank
    levee = np.exp(-((nd - 1.65) / 0.55) ** 2) * sl * flank
    dsp = dsp * (1 - 0.75 * channel) - channel * 0.30 + levee * 0.22
    band0 = sstep(BAND_LO - 3, BAND_LO, y0) * (1 - sstep(BAND_HI, BAND_HI + 3, y0))
    # ⑤の道の後ろは土台の半径より外へ出さない（道の板の下のほうは山肌の近くを通っている。+0.55 だと道に 0.66m 食い込んだ）
    thd0 = np.degrees(wrap_pi(a))
    road0 = sstep(ROAD_TH0 - 10, ROAD_TH0 - 5, thd0) * (1 - sstep(ROAD_TH1 + 5, ROAD_TH1 + 10, thd0))
    r = r0 + dsp - (BAND_SINK + 0.5 * road0) * band0 * flank
    # 道の高さの帯で、土台からはみ出さない
    band = sstep(BAND_LO - 3, BAND_LO, y0) * (1 - sstep(BAND_HI, BAND_HI + 3, y0))
    rb = rbase(a, y0)
    r = np.where(flank > 0, np.minimum(r, rb - 0.14 + BAND_OUT * (1 - road0) + (1 - band) * 40.0), r)
    thd = np.degrees(wrap_pi(a))
    road = sstep(ROAD_TH0 - 4, ROAD_TH0, thd) * (1 - sstep(ROAD_TH1, ROAD_TH1 + 4, thd)) * band
    near_jet = np.zeros_like(thd)
    for j in JETS: near_jet = np.maximum(near_jet, 1 - sstep(5.0, 10.0, np.abs(thd - j)))
    r = np.where(road > 0.01, np.maximum(r, rb - (ROAD_IN * (1 - near_jet) + 0.35 * near_jet) * road - 40.0 * (1 - road)), r)
    return dict(r=r, y=y0, r0=r0, ridge=ridge, ridge2=ridge2, lump=lump, ledge=ledge, nd=nd, sl=sl, sw=sw, flank=flank, px=px, pz=pz, channel=channel)

# ---- テクスチャ ----
def build_texture():
    u = (np.arange(TW) + 0.5) / TW; v = (np.arange(TH) + 0.5) / TH
    A, V = np.meshgrid(u * 2 * np.pi, v)
    s = surface(A, V)
    y, ridge, lump, ledge, nd, sl, flank, px, pz = s["y"], s["ridge"], s["lump"], s["ledge"], s["nd"], s["sl"], s["flank"], s["px"], s["pz"]
    # 高さで色：下の黒っぽい岩 → 上の赤茶
    ht = sstep(-34.0, 15.0, y)
    low = np.array([0.135, 0.11, 0.155]); high = np.array([0.335, 0.165, 0.145])
    col = low[None, None, :] * (1 - ht[..., None]) + high[None, None, :] * ht[..., None]
    # トゥーンっぽいまだら：尾根が明るく谷が暗い。段にしてくっきり
    tone = ridge * 1.1 + lump * 0.4 + ledge * 0.24 + (fbm(px * 0.11 + 30.0, y * 0.11, pz * 0.11, 3) - 0.5) * 0.45
    q = np.clip(tone + 0.5, 0, 1) * 4.0
    stepped = (np.floor(q) + sstep(0.42, 0.58, q % 1.0)) / 4.0
    tone2 = stepped * 0.72 + np.clip(tone + 0.5, 0, 1) * 0.28
    col = col * (0.50 + 1.05 * tone2)[..., None]
    # 細かいザラつき
    grain = fbm(px * 2.3, y * 2.3, pz * 2.3, 2) - 0.5
    col = col * (1 + grain * 0.28)[..., None]
    # 灰：ふち近くの上向きの所に少し
    ash = sstep(11.0, 15.8, y) * sstep(0.45, 0.8, ridge) * flank
    col = col * (1 - 0.5 * ash[..., None]) + np.array([0.52, 0.43, 0.44])[None, None, :] * 0.5 * ash[..., None]
    # 内壁は焼けて暗く、下の方は赤く
    inner = (V >= V_POND) & (V < V_RIM)
    tw = np.clip((V - V_POND) / (V_RIM - V_POND), 0, 1)
    wall = np.array([0.27, 0.11, 0.10])
    wc = wall[None, None, :] * (0.7 + 0.8 * lump[..., None] + 0.4 * tw[..., None])
    lip = sstep(0.75, 1.0, tw)   # ふちは外側の色へなじませる
    col = np.where(inner[..., None], wc * (1 - lip[..., None]) + col * lip[..., None], col)
    # 光 A
    glow = np.zeros_like(y, dtype=np.float32)
    # 割れ目：ノイズの 0.5 の線（細い筋）。上ほど多く、川の近くでも増える
    # 縦に長くのばしたノイズの 0.5 の線を、短く区切って使う（のばさないと閉じた輪になって落書きに見えた）。傾きで割って太さをそろえる
    cn = fbm(px * 0.45 + 50.0, y * 0.03 + 20.0, pz * 0.45, 1)
    gy, gx = np.gradient(cn)
    gx = np.where(np.abs(gx) > 0.2, 0.0, gx)   # 角度のつなぎ目の列の大きな差は無視
    line = np.abs(cn - 0.5) / np.maximum(np.sqrt(gx * gx + gy * gy), 1e-5)   # 線までの距離（ピクセル）
    wid = (0.5 + 0.5 * sstep(-12.0, 12.0, y)) * (0.5 + 0.9 * fbm(px * 0.2 + 15.0, y * 0.45, pz * 0.2, 2))
    keep = sstep(0.54, 0.62, fbm(px * 0.22 + 90.0, y * 0.2, pz * 0.22, 2) + 0.05 * sstep(-20.0, 14.0, y))
    near_hot = np.maximum(np.exp(-(nd / 4.5) ** 2) * sl, sstep(8.0, 14.0, y))   # 川のそばとふちの近くだけ（山肌のまん中に出すと落書きに見えた）
    crack = (1 - sstep(wid * 0.7, wid * 1.5, line)) * keep * flank * sstep(-8.0, 0.0, y) * near_hot
    # 割れ目の色：岩をくらくして光
    col = col * (1 - 0.85 * crack[..., None])
    glow = np.maximum(glow, crack * (0.34 + 0.28 * sstep(-20.0, 13.0, y)))
    # 川の両わき：岩が焼けて熱い
    lavazone = (V >= V_POND) * 1.0
    hot = np.exp(-(np.maximum(nd - 0.9, 0) / 0.5) ** 2) * sl * lavazone
    col = col * (1 - 0.6 * hot[..., None]) + np.array([0.20, 0.05, 0.04])[None, None, :] * 0.6 * hot[..., None]
    glow = np.maximum(glow, hot * 0.40 * sstep(0.1, 1.0, hot))
    # ふちの近く：池の熱で下から赤く
    rimhot = (1 - sstep(V_POND, V_RIM + 0.008, V)) * 0.62
    glow = np.maximum(glow, rimhot * (V >= V_POND))
    # 溶岩（川と池）：A 0.75〜1
    lava = (1 - sstep(0.72, 0.88, nd)) * sl * lavazone
    pond = (V < V_POND)
    lavaPool = sstep(V_POND * 1.04, V_POND * 0.98, V)
    lava = np.maximum(lava, lavaPool)
    # 川が途中で固まって切れ切れになる所：途中で黒いかさぶた
    crust = sstep(0.60, 0.72, fbm(px * 0.42 + 120.0, y * 0.16, pz * 0.42, 2)) * (1 - sstep(0.55, 0.85, s["sl"])) * flank
    lava = lava * (1 - 0.9 * crust)
    glow = np.maximum(glow, lava)   # ふちをなめらかに（急に変えると圧縮のブロックでギザギザになる）
    col = col * (1 - lava[..., None]) + np.array([0.75, 0.2, 0.04])[None, None, :] * lava[..., None]
    img = np.clip(col, 0, 1).astype(np.float32)
    # スペースは Non-Color（配色の数字をそのまま PNG へ。Unity では sRGB として読む）
    srgb = np.clip(img, 0, 1)
    rgba = np.dstack([srgb, np.clip(glow, 0, 1)])
    name = "Stage2_Volcano_Tex"
    im = bpy.data.images.get(name)
    if im: bpy.data.images.remove(im)
    im = bpy.data.images.new(name, TW, TH, alpha=True, float_buffer=False)
    im.colorspace_settings.name = 'Non-Color'
    im.alpha_mode = 'CHANNEL_PACKED'
    im.pixels.foreach_set(rgba[::-1].ravel().astype(np.float32))  # 画像の上が v=1。下から詰める Blender に合わせて、v は 0 が上
    im.filepath_raw = OUT_TEX; im.file_format = 'PNG'; im.save()
    return float(glow.mean()), float((glow > 0.74).mean())

def build_flow():
    # 流れる溶岩の模様（継ぎ目なしの 256）：R ゆっくり大きな明暗、G 細い筋、B かさぶた
    n = 256
    x = np.arange(n) / n; X, Y = np.meshgrid(x, x)
    def tile(f, seed):
        # 円柱ふうに 4D の代わりに、周期ノイズをサインで作る
        s = np.zeros_like(X); amp = 1.0; r = np.random.RandomState(seed); tot = 0.0
        for k in range(5):
            for j in range(6):
                fx = r.randint(-(f << k), (f << k) + 1); fy = r.randint(-(f << k), (f << k) + 1); ph = r.rand() * 6.283
                s += amp * np.sin(2 * np.pi * (fx * X + fy * Y) + ph); tot += amp
            amp *= 0.6
        return s / tot * 1.6 * 0.5 + 0.5
    R = np.clip(tile(2, 1), 0, 1)
    G = 1 - np.clip(np.abs(tile(3, 2) - 0.5) * 8.0, 0, 1)
    B = sstep(0.46, 0.60, tile(3, 3))
    rgba = np.dstack([R, G, B, np.ones_like(R)]).astype(np.float32)
    name = "Stage2_LavaFlow"
    im = bpy.data.images.get(name)
    if im: bpy.data.images.remove(im)
    im = bpy.data.images.new(name, n, n, alpha=True, float_buffer=False)
    im.colorspace_settings.name = 'Non-Color'
    im.pixels.foreach_set(rgba.ravel())
    im.filepath_raw = OUT_FLOW; im.file_format = 'PNG'; im.save()

# ---- メッシュ ----
def v_rows():
    # v の行：池 4、内壁 7、すそ 58、下 10、とがり
    r = [V_POND * (i + 1) / 4 for i in range(4)]
    r += [V_POND + (V_RIM - V_POND) * (i + 1) / 7 for i in range(7)]
    # 上ほど細かく（t^1.25）
    r += [V_RIM + (V_LOW - V_RIM) * ((i + 1) / 58) ** 1.2 for i in range(58)]
    r += [V_LOW + (1 - V_LOW) * (i + 1) / 10 for i in range(9)]
    return r

def B(x, y, z): return (-x, -z, y)   # Unity (x,y,z) → Blender (-x,-z,y)

def build_mesh(sc):
    rows = v_rows(); nr = len(rows); N = COLS
    aa = np.arange(N) / N * 2 * np.pi
    AA, VV = np.meshgrid(aa, np.array(rows))
    s = surface(AA, VV)
    r, y = s["r"], s["y"]
    x = np.sin(AA) * r; z = np.cos(AA) * r
    name = "Stage2_Volcano"
    old = sc.objects.get(name)
    if old: bpy.data.objects.remove(old, do_unlink=True)
    bm = bmesh.new()
    vs = [[bm.verts.new(B(float(x[j, i]), float(y[j, i]), float(z[j, i]))) for i in range(N)] for j in range(nr)]
    c = bm.verts.new(B(0.0, POND_Y, 0.0))            # 池のまん中
    tip = bm.verts.new(B(0.0, TIP_H, 0.0))           # 下のとがり
    uvl = bm.loops.layers.uv.new("UVMap")
    def face(vv, uvs):
        f = bm.faces.new(vv)
        for l, q in zip(f.loops, uvs): l[uvl].uv = (q[0], 1.0 - q[1])
        f.smooth = True
    for i in range(N):
        i1 = (i + 1) % N; u0 = i / N; u1 = (i + 1) / N
        face([c, vs[0][i], vs[0][i1]], [((u0 + u1) / 2, 0.0), (u0, rows[0]), (u1, rows[0])])
        for j in range(nr - 1):
            face([vs[j][i], vs[j + 1][i], vs[j + 1][i1], vs[j][i1]], [(u0, rows[j]), (u0, rows[j + 1]), (u1, rows[j + 1]), (u1, rows[j])])
        face([vs[nr - 1][i], tip, vs[nr - 1][i1]], [(u0, rows[nr - 1]), ((u0 + u1) / 2, 1.0), (u1, rows[nr - 1])])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)   # Blender で外向きにそろえる（書き出しで Unity でも外向きになる）
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    mat = bpy.data.materials.get("Stage2_Volcano") or bpy.data.materials.new("Stage2_Volcano")
    # Blender で見る用：色のテクスチャ＋光（Unity では FuwaCourse/Volcano を使う）
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tn = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None) or nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.get("Stage2_Volcano_Tex")
    nt.links.new(tn.outputs[0], bsdf.inputs[0])
    me.materials.clear(); me.materials.append(mat)
    # 符号つき体積（正なら外向き）
    bm2 = bmesh.new(); bm2.from_mesh(me); vol = bm2.calc_volume(signed=True); open_e = sum(1 for e in bm2.edges if not e.is_manifold); bm2.free()
    return o, vol, open_e, r, y, AA

prev_scene = bpy.context.window.scene.name
sc = bpy.data.scenes.get("Stage2Volcano") or bpy.data.scenes.new("Stage2Volcano")
bpy.context.window.scene = sc
gl = build_texture(); build_flow()
o, vol, open_e, r, y, AA = build_mesh(sc)
m = o.modifiers.new("Tri", 'TRIANGULATE'); m.keep_custom_normals = False
bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
for x in sc.objects: x.select_set(False)
o.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False, bake_anim=False)
bpy.ops.ed.undo_push(message="stage2 volcano")
bpy.ops.wm.save_mainfile()
print("tris", len(o.data.polygons), "verts", len(o.data.vertices), "vol", round(vol, 1), "open", open_e, "rim y", round(float(y[10].min()), 2), round(float(y[10].max()), 2), "glow", gl, "maxR", round(float(r.max()), 1))
