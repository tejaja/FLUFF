# 2–5 そらのいせき：ふわふわが大きくなる時の音の候補（2026-11-14 てじゃ「大きくなるときにSEつけたい」）
#   python make_sfx_grow.py <出力フォルダ>
#   アニメに合わせて3段：0.00s「ぐっ」→ 0.18s「ぐっ」→ 0.36s「ぐーん」（最後が長い）。全体 1 秒くらい
#   ありがちな「サイン波の上昇ピロリン」は使わない。音色ちがいで 01〜10
import numpy as np, wave, sys, os
from scipy.signal import butter, sosfilt, lfilter
sr = 44100
rng = np.random.default_rng(1114)
PUMPS = [0.0, 0.18, 0.36]
DUR = 1.15


def T(d): return np.arange(int(d * sr)) / sr
def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], btype='band', fs=sr, output='sos'), x)
def lp(x, f, order=2): return sosfilt(butter(order, f, btype='low', fs=sr, output='sos'), x)
def hp(x, f, order=2): return sosfilt(butter(order, f, btype='high', fs=sr, output='sos'), x)


def place(out, s, at):
    k = int(at * sr)
    if k >= len(out): return
    L = min(len(s), len(out) - k); out[k:k + L] += s[:L]


def norm(x, peak=0.8):
    m = np.max(np.abs(x)); return x / m * peak if m > 0 else x


def fade(x, a=0.002, b=0.04):
    x = x.copy(); na, nb = int(a * sr), int(b * sr)
    if na: x[:na] *= np.linspace(0, 1, na)
    if nb: x[-nb:] *= np.linspace(1, 0, nb)
    return x


def phase(freq):   # 周波数の列 → 位相
    return 2 * np.pi * np.cumsum(freq) / sr


def saw(ph): return 2 * ((ph / (2 * np.pi)) % 1.0) - 1
def tri(ph): return 2 * np.abs(saw(ph)) - 1


def env_ad(n, a, d):
    t = np.arange(n) / sr
    return np.minimum(1, t / max(a, 1e-4)) * np.exp(-np.maximum(0, t - a) * d)


def formant(x, f, q=8):   # 共鳴（2次の帯域）
    w = 2 * np.pi * f / sr; r = np.exp(-w / (2 * q))
    return lfilter([1 - r], [1, -2 * r * np.cos(w), r * r], x)


def formant_sweep(x, f0, f1, q=6):   # 共鳴の周波数を動かす（ブロックごと）
    out = np.zeros_like(x); B = 256; zi = np.zeros(2)
    for i in range(0, len(x), B):
        fr = f0 + (f1 - f0) * i / max(1, len(x)); w = 2 * np.pi * fr / sr; r = np.exp(-w / (2 * q))
        a = [1, -2 * r * np.cos(w), r * r]
        y, zi = lfilter([1 - r], a, x[i:i + B], zi=zi); out[i:i + B] = y
    return out


def pump_len(k): return 0.17 if k < 2 else 0.75


# 01 ゴム風船のキュッ キュッ キューン（のこぎり波＋声みたいな共鳴、音程がぐにっと上がる）
def v01():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        d = pump_len(k); t = T(d); u = t / d
        f = (300 + 60 * k) * (1 + 0.35 * u ** 0.6) * (1 + 0.02 * np.sin(2 * np.pi * 9 * t))
        s = saw(phase(f)); s = formant_sweep(s, 700 + 200 * k, 1300 + 300 * k, 5) + 0.4 * formant(s, 2600, 10)
        place(out, lp(s, 4000) * env_ad(len(t), 0.01, 3 if k == 2 else 12), at)
    return out


# 02 空気入れのシュッ シュッ ＋ ぼわん（ノイズの息＋低いやわらかい太鼓）
def v02():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        d = 0.14 if k < 2 else 0.22; t = T(d)
        n = bp(rng.standard_normal(len(t)), 900 + 300 * k, 3500 + 500 * k) * np.sin(np.pi * t / d) ** 1.5
        place(out, n * 0.6, at)
    t = T(0.8); f = 95 * (1 + 0.6 * np.exp(-t * 25))
    boom = np.sin(phase(f)) + 0.3 * np.sin(phase(f * 2.7)) * np.exp(-t * 18)
    place(out, lp(boom, 600) * env_ad(len(t), 0.006, 5) * 1.1, PUMPS[2] + 0.12)
    return out


# 03 チューバの ぶぉ ぶぉ ぶぉーん（のこぎり波、こもりから開く）
def v03():
    out = np.zeros(int(DUR * sr))
    notes = [58.27, 65.41, 87.31]   # A#1, C2, F2
    for k, at in enumerate(PUMPS):
        d = pump_len(k); t = T(d)
        f = notes[k] * 2 * (1 + 0.01 * np.sin(2 * np.pi * 5.5 * t * (k == 2)))
        s = saw(phase(f)) + 0.5 * saw(phase(f * 1.003))
        cut = 300 + 1500 * np.minimum(1, t / 0.05) * (np.exp(-t * (8 if k < 2 else 2.5)))
        y = np.zeros_like(s); B = 256
        for i in range(0, len(s), B): y[i:i + B] = lp(s[i:i + B], max(150, cut[i]), 1)
        place(out, lp(y, 1200) * env_ad(len(t), 0.02, 9 if k < 2 else 2.2), at)
    return out


# 04 ガムふうせん ぷく ぷく ぷくーっ（くちびるの「ぷ」＋中の空気の共鳴が上がる）
def v04():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        d = 0.16 if k < 2 else 0.6; t = T(d)
        n = rng.standard_normal(len(t)) * env_ad(len(t), 0.003, 30 if k < 2 else 6)
        s = formant_sweep(n, 350 + 120 * k, 600 + 250 * k + (900 if k == 2 else 0), 14)
        pop = np.sin(phase(np.full(len(t), 180 + 50 * k))) * env_ad(len(t), 0.001, 60) * 0.5
        place(out, s * 3 + pop, at)
    return out


# 05 口琴のびよん びよん びよーん（低いブーンに母音みたいな共鳴が動く）
def v05():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        d = pump_len(k) + 0.05; t = T(d)
        f0 = 110.0
        s = saw(phase(np.full(len(t), f0))) * 0.6 + np.sign(np.sin(phase(np.full(len(t), f0)))) * 0.3
        hi = 500 + 900 * k
        y = formant_sweep(s, 350, hi + (1200 if k == 2 else 0), 12)
        place(out, y * env_ad(len(t), 0.004, 7 if k < 2 else 2.5), at)
    return out


# 06 ばねのぼよん ぼよん ぼよーん（三角波＋FM、音程がうねって下がって上がる）
def v06():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        d = pump_len(k); t = T(d)
        f = (160 + 40 * k) * (1 + 0.5 * np.exp(-t * 30) * np.cos(2 * np.pi * 22 * t)) * (1 + (0.4 * t / d if k == 2 else 0))
        ph = phase(f); s = tri(ph + 1.2 * np.sin(ph * 2.01) * np.exp(-t * 10))
        place(out, lp(s, 3000) * env_ad(len(t), 0.003, 10 if k < 2 else 3), at)
    return out


# 07 カリンバの和音をぐにっと上へ（鉄の舌の部分音、ベンド）
def v07():
    out = np.zeros(int(DUR * sr))
    base = [196.0, 246.9, 293.7]
    for k, at in enumerate(PUMPS):
        d = 0.3 if k < 2 else 0.8; t = T(d)
        bend = 2 ** ((2 + 2 * k) / 12 * np.minimum(1, t / 0.08))
        s = np.zeros_like(t)
        for i, f in enumerate(base):
            ff = f * bend * (1 + 0.5 * k / 2)
            s += np.sin(phase(ff)) * np.exp(-t * 6) + 0.3 * np.sin(phase(ff * 5.4)) * np.exp(-t * 30)
        place(out, s * env_ad(len(t), 0.002, 4 if k < 2 else 2), at)
    return out


# 08 こもった太鼓 ドゥン ドゥン ドドーン（力がたまる感じ、高い音なし）
def v08():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        d = 0.35 if k < 2 else 0.8; t = T(d)
        f = (70 + 12 * k) * (1 + 0.8 * np.exp(-t * 35))
        s = np.sin(phase(f)) + 0.25 * np.sin(phase(f * 1.6)) * np.exp(-t * 20)
        hit = lp(rng.standard_normal(len(t)), 400) * np.exp(-t * 60) * 0.6
        place(out, (s + hit) * env_ad(len(t), 0.002, 10 if k < 2 else 3.5) * (0.7 + 0.15 * k), at)
    t = T(0.5); place(out, lp(rng.standard_normal(len(t)), 900) * np.sin(np.pi * t / 0.5) * 0.25, PUMPS[2] - 0.05)
    return out


# 09 びんを吹く ほっ ほっ ほぉーっ（息のノイズ＋びんの共鳴、だんだん高く）
def v09():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        d = 0.16 if k < 2 else 0.7; t = T(d)
        f = [220, 262, 330][k]; f1 = f * (1.06 if k == 2 else 1.0)
        breath = rng.standard_normal(len(t))
        tone = formant_sweep(breath, f, f1, 60) * 25 + formant_sweep(breath, f * 2, f1 * 2, 40) * 6 + bp(breath, 1500, 5000) * 0.15
        vib = 1 + 0.08 * np.sin(2 * np.pi * 5 * t) * (k == 2)
        place(out, tone * vib * env_ad(len(t), 0.03, 10 if k < 2 else 2.5), at)
    return out


# 10 ゴムのふくらむ音 ぽわわわーん（三角波＋息、ゆれながら上がって、最後に低いふわっ）
def v10():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        d = pump_len(k); t = T(d)
        f = (180 * 1.25 ** k) * (1 + 0.04 * np.sin(2 * np.pi * (7 + 3 * k) * t)) * (1 + (0.15 * t / d if k == 2 else 0.05 * t / d))
        s = tri(phase(f)) + 0.15 * bp(rng.standard_normal(len(t)), 800, 3000)
        place(out, lp(s, 2500) * env_ad(len(t), 0.02, 8 if k < 2 else 2.2), at)
    t = T(0.6); place(out, lp(np.sin(phase(np.full(len(t), 90.0))), 300) * env_ad(len(t), 0.01, 5) * 0.5, PUMPS[2] + 0.05)
    return out


def save(path, x):
    x = fade(norm(lp(x, 9000), 0.8))
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


if __name__ == '__main__':
    outdir = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(outdir, exist_ok=True)
    for i, fn in enumerate([v01, v02, v03, v04, v05, v06, v07, v08, v09, v10], 1):
        save(os.path.join(outdir, 'grow_%02d.wav' % i), fn())
    print('ok')
