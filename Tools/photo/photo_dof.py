# 撮影セットのぼかし（被写界深度）合成。FuwaPhotoSet.ShootDof が書き出した
#   <base>_color.png / _depth.raw（float32, m, 下の行から）/ _fuwa.png / _fx.png / _meta.json
# から、ピント（銃口の奥行き）より奥と手前を距離に合わせてぼかす。
# ・奥：ピントの合った物（ニタ・銃）がぼけた背景へにじまないよう、ぼけている画素だけで集める（重みつき）
# ・空気の輪（エフェクト）はピントの面にあるとみなす（半透明で奥行きを書かないので、後ろの空と同じにぼけてしまうため）
# ・手前のふわふわは別の層にして、ぼかしたものを上に重ねる（ふちが背景の上へふんわり広がる）
import json, sys
import numpy as np
from PIL import Image
from scipy.signal import fftconvolve

base, out = sys.argv[1], sys.argv[2]
meta = json.load(open(base + "_meta.json", encoding="utf-8"))
W, H = meta["w"], meta["h"]
zf, back, front = meta["focus"], meta["back"], meta["front"]

img = np.asarray(Image.open(base + "_color.png").convert("RGB"), dtype=np.float32) / 255.0
lin = img ** 2.2   # ぼかしは光の量で混ぜる（明るいふちが痩せないように）
z = np.flipud(np.frombuffer(open(base + "_depth.raw", "rb").read(), dtype=np.float32).reshape(H, W)).copy()
def mask(path, gain):
    m = np.asarray(Image.open(path).convert("L"), dtype=np.float32) / 255.0
    return np.clip(m * gain, 0, 1)
fuwa = mask(base + "_fuwa.png", 8.0)
fx = mask(base + "_fx.png", 4.0)

def disk(r):
    r = max(r, 0.5); n = int(np.ceil(r)) + 1
    y, x = np.mgrid[-n:n + 1, -n:n + 1]
    k = np.clip(r + 0.5 - np.sqrt(x * x + y * y), 0, 1)
    return k / k.sum()

def conv(a, k):
    if a.ndim == 3: return np.stack([fftconvolve(a[..., c], k, mode="same") for c in range(a.shape[2])], axis=-1)
    return fftconvolve(a, k, mode="same")

# ぼけの大きさ（px）：ピントはニタの奥行きの幅（near〜far）。奥は (z-far)/z、手前（地面など）は (near-z)/z。エフェクトの所はピント
zn, zx = meta.get("near", zf), meta.get("far", zf)
coc_back = np.clip((z - zx) / np.maximum(z, 1e-3) * 1.3, 0, 1) * back
coc_near = np.clip((zn - z) / np.maximum(z, 1e-3) * 1.0, 0, 1) * front
r = np.where(z >= zx, coc_back, np.where(z <= zn, coc_near, 0.0))
r = r * (1 - fx)
r[z < 0.01] = 0

# 下の層（ふわふわ以外）：ぼけの段ごとに重みつきでぼかし、画素ごとに自分のぼけの大きさで段の間をまぜる
levels = [0.0, 1.5, 3.0, 5.0, 7.5, 10.5, 14.0, 18.0, 23.0]
levels = [l for l in levels if l <= max(back, front) + 0.01] + ([max(back, front)] if max(back, front) not in levels else [])
levels = sorted(set(levels))
keep = 1 - fuwa
idx = np.interp(r, levels, np.arange(len(levels)))   # 画素ごとの「何段目のぼけか」（小数）
res = lin * np.clip(1 - idx, 0, 1)[..., None]          # 0 段目＝そのまま
for i, L in enumerate(levels[1:], start=1):
    wt = np.clip(1 - np.abs(idx - i), 0, 1)             # この段を使う割合（となりの段と線形にまぜる）
    if wt.max() <= 0: continue
    w = keep * np.clip(r / L, 0, 1) ** 2                # 自分もそれくらいぼけている画素だけで集める
    k = disk(L)
    num = conv(lin * w[..., None], k); den = conv(w, k)[..., None]
    b = np.where(den > 1e-4, num / np.maximum(den, 1e-4), lin)
    res += b * wt[..., None]

# ふわふわの後ろ（見えていない所）は、まわりの背景を大きくぼかして埋める
kfill = disk(40)
fill = conv(res * keep[..., None], kfill) / np.maximum(conv(keep, kfill)[..., None], 1e-4)
res = res * keep[..., None] + fill * fuwa[..., None]

# 手前のふわふわ：ぼかして上に重ねる
rf = front * np.clip((zn - meta["fuwa"]) / max(meta["fuwa"], 1e-3) * 2.0, 0.0, 1.0)
if rf < 0.6: rf = 0.0   # ふわふわがピントの中（ニタを隠してふわふわを撮る時など）はぼかさない
kf = disk(rf) if rf > 0 else np.ones((1, 1), np.float32)
fa = conv(fuwa, kf) if rf > 0 else fuwa; fc = conv(lin * fuwa[..., None], kf) if rf > 0 else lin * fuwa[..., None]
res = res * (1 - fa[..., None]) + fc

outimg = np.clip(res, 0, 1) ** (1 / 2.2)
Image.fromarray((outimg * 255 + 0.5).astype(np.uint8)).save(out)
print("ok focus=%.2fm fuwa=%.2fm" % (zf, meta["fuwa"]))
