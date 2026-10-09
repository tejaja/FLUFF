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
STYLE = sys.argv[2] if len(sys.argv) > 2 else 'tri'   # 2026-10-09 てじゃが△に決定（長方形も候補だった）
# 'rect' = 短い長方形を向きバラバラに（デフォルメ）、'tri' = 小さい三角を向きバラバラに、
# 'tuft' = 3本のはらいの束（向きそろい）、'strokes' = 細かい筋（リアル寄り）
if STYLE in ('rect', 'tri'):
    from PIL import ImageDraw
    canvas = Image.fromarray((R * 255).astype(np.uint8), 'L'); dr = ImageDraw.Draw(canvas)
    placed = []; tries = 0
    while len(placed) < (300 if STYLE == 'rect' else 280) and tries < 30000:
        tries += 1
        x0, y0 = rng.uniform(0, S, size=2)
        size = rng.uniform(18, 30) if STYLE == 'rect' else rng.uniform(14, 22)
        if any(min(abs(x0 - qx), S - abs(x0 - qx)) ** 2 + min(abs(y0 - qy), S - abs(y0 - qy)) ** 2 < (size * 0.6 + qs * 0.6) ** 2 for qx, qy, qs in placed): continue
        placed.append((x0, y0, size))
        light = rng.random() < 0.7
        val = 0.5 + (rng.uniform(0.3, 0.42) if light else -rng.uniform(0.22, 0.32))
        ang = rng.uniform(0, math.pi * 2)
        if STYLE == 'rect':
            hw, hh = size * 0.5, size * rng.uniform(0.13, 0.2)
            local = [(-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)]
        else:
            local = [(size * 0.6 * math.cos(a), size * 0.6 * math.sin(a)) for a in (0, 2.094, 4.189)]
        ca, sa = math.cos(ang), math.sin(ang)
        pts = [(x0 + px * ca - py * sa, y0 + px * sa + py * ca) for px, py in local]
        for ox in (-S, 0, S):          # 端をまたいでもつながるように、となりのコマにも描く
            for oy in (-S, 0, S):
                dr.polygon([(px + ox, py + oy) for px, py in pts], fill=int(round(val * 255)))
    R = np.asarray(canvas).astype(np.float64) / 255.0
elif STYLE == 'strokes':
    for _ in range(9000):
        x0, y0 = rng.uniform(0, S, size=2)
        ang = rng.uniform(0, 2 * math.pi)
        length = rng.uniform(5, 16)
        val = 0.5 + rng.choice([-1, 1], p=[0.55, 0.45]) * rng.uniform(0.12, 0.32)
        stroke(x0, y0, ang, length, val, rng.uniform(1.0, 1.8))
    for _ in range(1600):   # 小さい点（クローバーや小石っぽい）
        x0, y0 = rng.uniform(0, S, size=2)
        stroke(x0, y0, 0, 0.1, 0.5 + rng.uniform(0.15, 0.3) * rng.choice([-1, 1]), rng.uniform(1.5, 2.6))
else:
    # デフォルメの草の束：根元から3本（まん中が長い）のとがった葉。向きはだいたいそろえる（絵っぽく）。
    # 明るい束が多め、ときどき濃い束。重ならないように間をあける
    placed = []
    def blade(x0, y0, ang, length, width, val, bend):
        steps = int(length) + 2
        for k in range(steps):
            t = k / (steps - 1)
            a = ang + bend * t * t
            px = x0 + sum(math.cos(ang + bend * (j / steps) ** 2) for j in range(k)) * (length / steps)
            py = y0 + sum(math.sin(ang + bend * (j / steps) ** 2) for j in range(k)) * (length / steps)
            w = width * (1 - t) ** 0.8
            r = int(math.ceil(w)) + 1
            for dy in range(-r, r + 1):
                for dx in range(-r, r + 1):
                    d = math.hypot(dx, dy)
                    if d > w + 0.5: continue
                    a2 = min(1.0, w + 0.5 - d)
                    ix = int(round(px + dx)) % S; iy = int(round(py + dy)) % S
                    R[iy, ix] = R[iy, ix] * (1 - a2) + val * a2
    tries = 0
    while len(placed) < 260 and tries < 20000:
        tries += 1
        x0, y0 = rng.uniform(0, S, size=2)
        size = rng.uniform(26, 40)
        if any(min(abs(x0 - qx), S - abs(x0 - qx)) ** 2 + min(abs(y0 - qy), S - abs(y0 - qy)) ** 2 < (size * 0.55 + qs * 0.55) ** 2 for qx, qy, qs in placed): continue
        placed.append((x0, y0, size))
        light = rng.random() < 0.72
        val = 0.5 + (rng.uniform(0.3, 0.42) if light else -rng.uniform(0.25, 0.35))
        up = -math.pi / 2 + rng.uniform(-0.25, 0.25)        # テクスチャの上向きがだいたいそろう
        for off, lk, bd in ((0.0, 1.0, rng.uniform(-0.15, 0.15)), (-0.62, 0.72, -0.35), (0.6, 0.78, 0.35)):
            blade(x0 + off * size * 0.12, y0, up + off, size * lk, size * 0.085, val, bd)
    for _ in range(90):   # ぽつぽつの小さい花・点
        x0, y0 = rng.uniform(0, S, size=2)
        stroke(x0, y0, 0, 0.1, 0.5 + rng.uniform(0.3, 0.4), rng.uniform(2.4, 3.4))
R = R.reshape(N, 2, N, 2).mean(axis=(1, 3))

img = np.stack([R, G, B], axis=-1)
Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), 'RGB').save(OUT)
print('saved', OUT, 'R mean %.3f' % R.mean())
