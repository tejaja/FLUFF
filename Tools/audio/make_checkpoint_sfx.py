"""チェックポイントの音の候補。よくある「サイン波の上昇アルペジオ」を避けて、音色の違うものを作る。"""
import os
import sys
import wave

import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)


def t(sec):
    return np.arange(int(SR * sec)) / SR


def mix(length, *parts):
    out = np.zeros(int(SR * length))
    for start, sig in parts:
        i = int(start * SR)
        n = min(len(sig), len(out) - i)
        if n > 0:
            out[i:i + n] += sig[:n]
    return out


def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.zeros_like(x)
    prev = 0.0
    for i, v in enumerate(x):
        prev = (1 - a) * v + a * prev
        y[i] = prev
    return y


def bandpass(x, center, q=4.0):
    w = 2 * np.pi * center / SR
    alpha = np.sin(w) / (2 * q)
    b0, b2 = alpha, -alpha
    a0, a1, a2 = 1 + alpha, -2 * np.cos(w), 1 - alpha
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        r = (b0 * v + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1 = x1, v
        y2, y1 = y1, r
        y[i] = r
    return y


def save(name, x, fade=0.02):
    x = np.array(x, dtype=float)
    n = int(fade * SR)
    if n:
        x[-n:] *= np.linspace(1, 0, n)
    x -= np.mean(x)
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.85
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print(name, round(len(x) / SR, 2))


def kalimba(f, sec=0.9):
    tt = t(sec)
    s = np.sin(2 * np.pi * f * tt) * np.exp(-tt * 5)
    s += 0.35 * np.sin(2 * np.pi * f * 5.4 * tt) * np.exp(-tt * 28)
    s += 0.18 * np.sin(2 * np.pi * f * 12.1 * tt) * np.exp(-tt * 70)
    return s * (1 - np.exp(-tt * 900))


def marimba(f, sec=0.6):
    tt = t(sec)
    s = np.sin(2 * np.pi * f * tt) * np.exp(-tt * 9)
    s += 0.5 * np.sin(2 * np.pi * f * 4 * tt) * np.exp(-tt * 26)
    s += 0.2 * np.sin(2 * np.pi * f * 9.2 * tt) * np.exp(-tt * 60)
    return s * (1 - np.exp(-tt * 1500))


def pluck(f, sec=1.0, damp=0.996):
    n = int(SR * sec)
    p = max(2, int(SR / f))
    buf = rng.uniform(-1, 1, p)
    buf = lowpass(buf, 5000)
    out = np.zeros(n)
    for i in range(n):
        out[i] = buf[i % p]
        buf[i % p] = damp * 0.5 * (buf[i % p] + buf[(i + 1) % p])
    return out


def felt(f, sec=1.1):
    tt = t(sec)
    s = sum(a * np.sin(2 * np.pi * f * h * tt) * np.exp(-tt * (2.5 + h * 1.6)) for h, a in [(1, 1), (2, 0.35), (3, 0.12), (4, 0.05)])
    thump = lowpass(rng.normal(0, 1, len(tt)), 900) * np.exp(-tt * 70) * 0.5
    return (s + thump) * (1 - np.exp(-tt * 220))


def fm(f, sec=1.0, ratio=3.5, index=1.6, decay=4.0):
    tt = t(sec)
    env = np.exp(-tt * decay)
    return np.sin(2 * np.pi * f * tt + index * np.exp(-tt * 9) * np.sin(2 * np.pi * f * ratio * tt)) * env * (1 - np.exp(-tt * 1200))


def bubble(f0, f1, sec=0.16):
    tt = t(sec)
    ph = 2 * np.pi * np.cumsum(np.geomspace(f0, f1, len(tt))) / SR
    return np.sin(ph) * np.sin(np.pi * tt / sec) ** 1.5


def steel(f, sec=0.8):
    tt = t(sec)
    s = sum(a * np.sin(2 * np.pi * f * h * tt) * np.exp(-tt * d) for h, a, d in [(1, 1, 4), (2, 0.6, 6), (3.02, 0.25, 10), (4.05, 0.12, 15)])
    return s * (1 - np.exp(-tt * 500))


def breath(sec, c0, c1, q=3.0):
    tt = t(sec)
    noise = rng.normal(0, 1, len(tt))
    parts = []
    step = int(0.02 * SR)
    out = np.zeros(len(tt))
    for i in range(0, len(tt), step):
        k = i / len(tt)
        seg = bandpass(noise[i:i + step * 2], c0 + (c1 - c0) * k, q)
        out[i:i + len(seg)] += seg * np.hanning(len(seg))
    return out * np.sin(np.pi * tt / sec) ** 1.5


def vowel(f, sec=0.75):
    tt = t(sec)
    vib = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * tt)
    src = sum(np.sin(2 * np.pi * f * h * tt * vib) / h for h in range(1, 18))
    y = bandpass(src, 450, 6) + 0.6 * bandpass(src, 900, 7) + 0.25 * bandpass(src, 2500, 8)
    return y * (1 - np.exp(-tt * 14)) * np.exp(-tt * 3.2)


# 01 カリンバ 2音（下がって上がる）
save("01_kalimba", mix(1.1, (0, kalimba(784)), (0.11, kalimba(587, 0.95))))
# 02 木琴 ぽんぽん（同じ高さの連打＋オクターブ）
save("02_mokkin", mix(0.8, (0, marimba(523)), (0.09, marimba(523)), (0.18, marimba(1047, 0.5) * 0.8)))
# 03 ふわっ＋ぽふっ（毛玉らしい柔らかい音、音程ほぼなし）
save("03_fuwa_pofu", mix(0.7, (0, breath(0.34, 500, 1900) * 0.9), (0.2, bubble(190, 120, 0.2) * 1.0)))
# 04 ギターの爪弾き 1本弦の和音
save("04_tsumabiki", mix(1.3, (0, pluck(392)), (0.05, pluck(494) * 0.8), (0.1, pluck(587) * 0.75), (0.15, pluck(784) * 0.6)))
# 05 グラスの音（低めの FM、1音だけ）
save("05_glass", mix(1.2, (0, fm(1175, 1.2, 3.5, 1.3, 3.5))))
# 06 フェルトピアノ 2音
save("06_felt_piano", mix(1.3, (0, felt(349)), (0.14, felt(523))))
# 07 泡 ぽこぽこ（音程が上がる短い泡を不規則に）
save("07_awa", mix(0.5, (0, bubble(350, 800)), (0.09, bubble(500, 1200)), (0.16, bubble(420, 1000)), (0.26, bubble(700, 1800, 0.13) * 0.9)))
# 08 風がひゅっ＋小さな鈴
save("08_kaze_suzu", mix(1.0, (0, breath(0.45, 700, 3400, 4) * 0.9), (0.34, fm(2093, 0.65, 2.76, 0.9, 6.5) * 0.45)))
# 09 声の「ふぉー」（口笛まじりの短いコーラス）
save("09_koe", mix(0.9, (0, vowel(392)), (0.08, vowel(587) * 0.6)))
# 10 スチールパン 3音（順番を崩す）
save("10_steelpan", mix(1.1, (0, steel(659)), (0.1, steel(880)), (0.21, steel(740, 0.8))))
