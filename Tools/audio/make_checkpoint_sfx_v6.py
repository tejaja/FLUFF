"""チェックポイントの音 第6弾：第5弾の 10番「泡のぽこっ」（音程が少し上がる泡、シ→ミ）が好評（2026-11-05 てじゃ「10おもろいな」）→ そのバリエーション。
01 は第5弾10番そのもの。
使い方：python make_checkpoint_sfx_v6.py <出力フォルダ>
"""
import os
import sys
import wave

import numpy as np

here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, here)
import fluffsynth as fs   # noqa: E402

SR = fs.SR
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
B5, E6 = 987.77, 1318.51


def hz(m): return 440.0 * 2 ** ((m - 69) / 12)


def bubble(f, rise=0.2, rise_t=0.03, dec=0.09, sec=0.5, harm=0.0, wob=0.0):
    """泡：音程が (1-rise) 倍から f へ上がる。harm で2倍音、wob で泡のゆらぎ"""
    t = np.arange(int(SR * sec)) / SR
    ff = f * (1 - rise * np.exp(-t / rise_t)) * (1 + wob * np.sin(2 * np.pi * 28 * t) * np.exp(-t / 0.05))
    ph = 2 * np.pi * np.cumsum(ff) / SR
    y = np.sin(ph) + harm * np.sin(2 * ph)
    return y * np.exp(-t / dec) * np.clip(t / 0.002, 0, 1)


def seq(parts, length):
    out = np.zeros(int(SR * length))
    for st, sig, g in parts:
        i = int(st * SR); n = min(len(sig), len(out) - i)
        out[i:i + n] += sig[:n] * g
    return out


def sparkle(length):
    """余韻に小さいキラキラ（ビブラフォンの高い音をうすく）"""
    return fs.vibes(hz(88), 0.3, 0.5, 0.3)[:int(SR * length)]


cands = [
    ("元の10番（シ→ミの泡）", seq([(0, bubble(B5), 0.8), (0.12, bubble(E6), 1.0)], 0.6)),
    ("泡を3つ ソ→シ→ミ", seq([(0, bubble(hz(79)), 0.7), (0.09, bubble(B5), 0.8), (0.18, bubble(E6), 1.0)], 0.7)),
    ("ぐっと上がる（音程の上がり幅を大きく）", seq([(0, bubble(B5, rise=0.4, rise_t=0.04), 0.8), (0.12, bubble(E6, rise=0.4, rise_t=0.05), 1.0)], 0.6)),
    ("低めの泡（1オクターブ下、ぽこぽこ）", seq([(0, bubble(B5 / 2, dec=0.12), 0.8), (0.13, bubble(E6 / 2, dec=0.14), 1.0)], 0.65)),
    ("水っぽい（泡のゆらぎを足す）", seq([(0, bubble(B5, wob=0.04), 0.8), (0.12, bubble(E6, wob=0.04), 1.0)], 0.6)),
    ("丸くて太い（2倍音を少し）", seq([(0, bubble(B5, harm=0.25), 0.8), (0.12, bubble(E6, harm=0.25), 1.0)], 0.6)),
    ("最後にキラッ（泡＋うすい鉄琴）", seq([(0, bubble(B5), 0.8), (0.12, bubble(E6), 1.0), (0.14, sparkle(0.6), 0.35)], 0.8)),
    ("ぽこぽこぽこっ（小さい泡が4つ上がっていく）", seq([(i * 0.06, bubble(hz(m), dec=0.06, sec=0.3), 0.6 + 0.13 * i) for i, m in enumerate([76, 79, 83, 88])], 0.6)),
    ("ゆっくり2つ（間隔0.18秒、余韻長め）", seq([(0, bubble(B5, dec=0.12), 0.8), (0.18, bubble(E6, dec=0.16, sec=0.6), 1.0)], 0.8)),
    ("ふわっと（上がり方をゆっくり、やわらかい）", seq([(0, bubble(B5, rise=0.25, rise_t=0.06, dec=0.11), 0.8), (0.13, bubble(E6, rise=0.25, rise_t=0.07, dec=0.14), 1.0)], 0.7)),
]


def save(idx, x):
    x = np.array(x, dtype=float)
    n = int(0.05 * SR); x[-n:] *= np.linspace(1, 0, n)
    x -= np.mean(x)
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.7
    with wave.open(os.path.join(OUT, f"menu_{idx:02d}.wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


menu = []
for i, (name, x) in enumerate(cands, 1):
    save(i, x); menu.append(f"{i:02d} {name}")
with open(os.path.join(OUT, "menu.txt"), "w", encoding="utf-8") as f:
    f.write("\n".join(menu))
print("ok", len(menu))
