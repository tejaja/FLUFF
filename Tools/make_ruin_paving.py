# 2026-11-15 2–5 の道の石だたみ（FuwaToonLit の _GroundTex 用、継ぎ目なしの 4m 四方）
#   R＝細かい模様（石だたみ：目地が暗い、石ごとに明るさがちがう、目地のそばは少し暗く＝面取り）
#   G＝大きいムラ、B＝中くらいのムラ（どちらも継ぎ目なしのノイズ）
#   python make_ruin_paving.py <出力.png>
import numpy as np, sys
from PIL import Image
N = 512
M = 4.0                     # 1枚 = 4m
px = N / M
rng = np.random.default_rng(23)
R = np.zeros((N, N)); D = np.full((N, N), 99.0)   # 石の明るさ、目地までの距離(px)
rows = 8
rh = N // rows
yy, xx = np.mgrid[0:N, 0:N]
for r in range(rows):
    y0 = r * rh
    # 行ごとに石の長さ（0.45〜1.0m）を足して 4m ちょうどに
    ls = []
    while True:
        L = rng.uniform(0.45, 1.0); rest = M - sum(ls)
        if rest - L < 0.4: ls.append(rest); break   # のこりが少ないなら最後の石でちょうどに（細いかけらを作らない）
        ls.append(L)
    off = rng.uniform(0, M)
    x = off
    for L in ls:
        x0 = x * px; x1 = (x + L) * px
        tone = rng.uniform(0.38, 0.62)
        # この石の範囲（横は巻き戻し）
        for k in (-1, 0, 1):
            a = x0 + k * N; b = x1 + k * N
            ia, ib = int(np.floor(max(a, 0))), int(np.ceil(min(b, N)))
            if ib <= ia: continue
            sub = (slice(y0, y0 + rh), slice(ia, ib))
            R[sub] = tone
            dx = np.minimum(xx[sub] - a, b - xx[sub])
            dy = np.minimum(yy[sub] - y0, y0 + rh - yy[sub])
            D[sub] = np.minimum(dx, dy)
        x += L
g = 4.5 / 2   # 目地の半分のはば(px)
grout = D < g
bevel = np.clip((D - g) / 6.0, 0, 1)
# 石の中の細かいざらつき（継ぎ目なし：周期ノイズ）
def tile_noise(scale, seed):
    r2 = np.random.default_rng(seed)
    acc = np.zeros((N, N))
    for _ in range(6):
        fx, fy = r2.integers(1, scale + 1, 2)
        ph = r2.uniform(0, 2 * np.pi)
        acc += np.sin(2 * np.pi * (fx * xx + fy * yy) / N + ph)
    return (acc - acc.min()) / (acc.max() - acc.min())
grain = tile_noise(24, 3)
out_r = R * (0.75 + 0.25 * bevel) + (grain - 0.5) * 0.06
out_r[grout] = 0.12
out_r = np.clip(out_r, 0, 1)
G = tile_noise(3, 7); B = tile_noise(8, 11)
img = np.stack([out_r, G, B], -1)
Image.fromarray((img * 255).astype(np.uint8), 'RGB').save(sys.argv[1])
print('ok', out_r.mean())
