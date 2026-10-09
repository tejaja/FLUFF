# 芝の地面の模様テクスチャ（FuwaCourse/ToonLit の Ground Detail 用）。継ぎ目なしでくり返せる 512x512。
# R = 細かい草の筋と点（0.5 が元の明るさ）、G = 大きいムラ（低い周波数）、B = 中くらいのかたまり
# 使い方：python Tools/ground_detail.py [出力パス]
import sys, math
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else r'D:\UnityProject\FLUFF\Assets\FuwaCourse\Textures\GroundDetail.png'
N = 512
rng = np.random.default_rng(20261009)

def tile_noise(freqs, count):
    # 整数の周波数の波を足すので、端と端がつながる
    y, x = np.mgrid[0:N, 0:N] / N
    acc = np.zeros((N, N))
    for _ in range(count):
        fx, fy = rng.integers(-freqs, freqs + 1, size=2)
        if fx == 0 and fy == 0: continue
        amp = 1.0 / math.sqrt(fx * fx + fy * fy)
        acc += amp * np.cos(2 * math.pi * (fx * x + fy * y) + rng.uniform(0, 2 * math.pi))
    acc -= acc.min(); acc /= acc.max()
    return acc

G = tile_noise(4, 40)                      # 大きいムラ
G = np.clip((G - 0.5) * 1.3 + 0.5, 0, 1)
B = tile_noise(14, 120)                    # 中くらい
B = np.clip((B - 0.5) * 1.4 + 0.5, 0, 1)

# 細かい草：2倍の大きさで短い筋を描いて縮める（なめらかに）。端をまたぐ筋は反対側にも描く
S = N * 2
R = np.full((S, S), 0.5)
def stroke(x0, y0, ang, length, val, width):
    steps = int(length * 2) + 1
    for k in range(steps):
        t = k / max(1, steps - 1)
        px = x0 + math.cos(ang) * length * t; py = y0 + math.sin(ang) * length * t
        w = width * (1.0 - 0.6 * t)          # 先ほど細い
        r = int(math.ceil(w))
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                d = math.hypot(dx, dy)
                if d > w: continue
                a = (1 - d / max(w, 1e-3)) * 0.9
                ix = int(px + dx) % S; iy = int(py + dy) % S
                R[iy, ix] = R[iy, ix] * (1 - a) + val * a
for _ in range(9000):
    x0, y0 = rng.uniform(0, S, size=2)
    ang = rng.uniform(0, 2 * math.pi)
    length = rng.uniform(5, 16)
    val = 0.5 + rng.choice([-1, 1], p=[0.55, 0.45]) * rng.uniform(0.12, 0.32)
    stroke(x0, y0, ang, length, val, rng.uniform(1.0, 1.8))
for _ in range(1600):   # 小さい点（クローバーや小石っぽい）
    x0, y0 = rng.uniform(0, S, size=2)
    stroke(x0, y0, 0, 0.1, 0.5 + rng.uniform(0.15, 0.3) * rng.choice([-1, 1]), rng.uniform(1.5, 2.6))
R = R.reshape(N, 2, N, 2).mean(axis=(1, 3))

img = np.stack([R, G, B], axis=-1)
Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), 'RGB').save(OUT)
print('saved', OUT, 'R mean %.3f' % R.mean())
