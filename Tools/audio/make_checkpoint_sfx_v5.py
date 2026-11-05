"""チェックポイントの音 第5弾：最初の音（シ→ミ、0.12秒あけて2音目）の並びのまま、音色を変える（2026-11-05 てじゃ「音色かえてみるのはどう」）。
楽器は fluffsynth.py のもの＋ここで作るもの。長さは 1秒以内に切ってフェード。
使い方：python make_checkpoint_sfx_v5.py <出力フォルダ>  → menu_01.wav … と menu.txt
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
F1, F2, GAP = 987.77, 1318.51, 0.12   # B5, E6
rng = np.random.default_rng(5)


def two(make, length=0.9, g1=0.75, g2=1.0, gap=GAP, lo=1.0):
    """1音目・2音目を作って並べる。lo で1オクターブ下げる（0.5）"""
    a, b = make(F1 * lo, gap), make(F2 * lo, length - gap)
    out = np.zeros(int(SR * length))
    for st, sig, g in [(0.0, a, g1), (gap, b, g2)]:
        i = int(st * SR); n = min(len(sig), len(out) - i)
        out[i:i + n] += sig[:n] * g
    return out


def steelpan(f, dur):
    t = np.arange(int(SR * 0.9)) / SR
    y = sum(a * np.exp(-t / d) * np.sin(2 * np.pi * f * m * t) for m, a, d in [(1, 1, 0.35), (2, 0.5, 0.18), (3, 0.18, 0.08), (2.98, 0.1, 0.1)])
    y *= 1 + 0.25 * np.exp(-t / 0.05) * np.sin(2 * np.pi * 6 * t)   # たたいた直後の「わん」
    return y * np.clip(t / 0.002, 0, 1)


def handbell(f, dur):
    t = np.arange(int(SR * 0.9)) / SR
    y = sum(a * np.exp(-t / d) * np.sin(2 * np.pi * f * m * t) for m, a, d in [(1, 1, 0.6), (2.0, 0.35, 0.3), (2.76, 0.2, 0.15), (5.4, 0.08, 0.05)])
    y *= 1 + 0.15 * np.sin(2 * np.pi * 4.5 * t)
    return y * np.clip(t / 0.003, 0, 1)


def toypiano(f, dur):
    t = np.arange(int(SR * 0.9)) / SR
    y = sum(a * np.exp(-t / d) * np.sin(2 * np.pi * f * m * t) for m, a, d in [(1, 1, 0.3), (2.0, 0.3, 0.12), (4.1, 0.25, 0.05), (7.3, 0.1, 0.02)])
    cn = int(0.004 * SR); y[:cn] += fs.hp(rng.standard_normal(cn), 2000) * 0.15
    return y * np.clip(t / 0.001, 0, 1)


def bubble(f, dur):
    """ぽこっ：音程が少し上がる泡（ふわふわっぽい）"""
    t = np.arange(int(SR * 0.5)) / SR
    ff = f * (0.8 + 0.2 * (1 - np.exp(-t / 0.03)))
    ph = 2 * np.pi * np.cumsum(ff) / SR
    return np.sin(ph) * np.exp(-t / 0.09) * np.clip(t / 0.002, 0, 1)


def harp(f, dur):
    """ハープ：高い音だと弦の計算（ks_string）がすぐ消えるので、倍音を足して作る"""
    t = np.arange(int(SR * 0.9)) / SR
    y = sum((1.0 / h) * np.exp(-t / (0.55 / h ** 0.6)) * np.sin(2 * np.pi * f * h * 1.0004 ** h * t) for h in range(1, 7))
    cn = int(0.003 * SR); y[:cn] += fs.bp(rng.standard_normal(cn), 1500, 6000) * 0.2
    return y * np.clip(t / 0.0015, 0, 1)


def softsquare(f, dur):
    return fs.soft_lead(f, min(dur, 0.25), vel=0.7, pulse=0.35, cutoff=1.6)


cands = [
    ("ビブラフォン（やわらかい金属、ゆれる余韻）", lambda: two(lambda f, d: fs.vibes(f, max(d, 0.4), 0.8, 0.5))),
    ("グロッケン（鉄琴、キラッ）", lambda: two(lambda f, d: fs.glock(f, d, 0.8, 0.5, ring=0.6))),
    ("チェレスタ（丸い鐘）", lambda: two(lambda f, d: fs.celesta(f, max(d, 0.3), 0.8, 0.4))),
    ("カリンバ（ティン、木の箱）", lambda: two(lambda f, d: fs.kalimba(f, d, 0.8, 0.5))),
    ("木のマリンバ（1オクターブ下、ポコ）", lambda: two(lambda f, d: fs.marimba3(f, d, 0.9, 0.5), lo=0.5)),
    ("ハープ（弦をはじく）", lambda: two(harp)),
    ("スチールパン（わんっ）", lambda: two(steelpan)),
    ("ハンドベル（鐘、長め）", lambda: two(handbell, length=1.0)),
    ("おもちゃのピアノ（チープでかわいい）", lambda: two(toypiano)),
    ("泡のぽこっ（音程が少し上がる）", lambda: two(bubble, length=0.6, g1=0.8)),
]


def save(idx, x):
    x = np.array(x, dtype=float)
    if x.ndim > 1: x = x.mean(axis=1) if x.shape[1] == 2 else x.mean(axis=0)
    n = int(0.08 * SR); x[-n:] *= np.linspace(1, 0, n)
    x -= np.mean(x)
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.7
    with wave.open(os.path.join(OUT, f"menu_{idx:02d}.wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


menu = []
for i, (name, make) in enumerate(cands, 1):
    save(i, make())
    menu.append(f"{i:02d} {name}")
with open(os.path.join(OUT, "menu.txt"), "w", encoding="utf-8") as f:
    f.write("\n".join(menu))
print("ok", len(menu))
