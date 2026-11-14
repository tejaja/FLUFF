# 2–5：大きくなる音 その3（2026-11-14 てじゃ「もっとポワポワした音」）
#   python make_sfx_grow_v3.py <出力フォルダ>
#   「ぽわっ・ぽわっ・ぽわ〜ん」：角のないまるい音色、1音ずつ下からふわっとしゃくり上がる。3段は 0.00・0.18・0.36s、全体 0.62s
#   サイン波だけの上昇ピロリンにはしない（こもらせた矩形・三角、母音の共鳴、息、ゆらぎで）
import numpy as np, wave, sys, os
from scipy.signal import butter, sosfilt, lfilter
sr = 44100
rng = np.random.default_rng(3114)
PUMPS = [0.0, 0.18, 0.36]
DUR = 0.62


def lp(x, f, order=2): return sosfilt(butter(order, min(f, sr * 0.45), btype='low', fs=sr, output='sos'), x)
def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], btype='band', fs=sr, output='sos'), x)
def place(out, s, at):
    k = int(at * sr)
    if k >= len(out): return
    L = min(len(s), len(out) - k); out[k:k + L] += s[:L]
def norm(x, peak=0.8):
    m = np.max(np.abs(x)); return x / m * peak if m > 0 else x
def phase(freq): return 2 * np.pi * np.cumsum(freq) / sr
def saw(ph): return 2 * ((ph / (2 * np.pi)) % 1.0) - 1
def tri(ph): return 2 * np.abs(saw(ph)) - 1
def pulse(ph, duty=0.5): return np.where((ph / (2 * np.pi)) % 1.0 < duty, 1.0, -1.0)
def mtof(m): return 440.0 * 2 ** ((m - 69) / 12)
def env(n, a, d):
    t = np.arange(n) / sr; return np.minimum(1, t / max(a, 1e-4)) * np.exp(-np.maximum(0, t - a) * d)
def formant_sweep(x, f0, f1, q=6):
    out = np.zeros_like(x); B = 128; zi = np.zeros(2)
    for i in range(0, len(x), B):
        u = i / max(1, len(x)); fr = f0 * (f1 / f0) ** min(1, u * 3); w = 2 * np.pi * fr / sr; r = np.exp(-w / (2 * q))
        y, zi = lfilter([1 - r], [1, -2 * r * np.cos(w), r * r], x[i:i + B], zi=zi); out[i:i + B] = y
    return out
def lp_sweep(x, c0, c1, tc=0.04):
    out = np.zeros_like(x); B = 128
    for i in range(0, len(x), B):
        t = i / sr; c = c1 + (c0 - c1) * np.exp(-t / tc); out[i:i + B] = lp(x[i:i + B], c, 1)
    return out

STEPS = [67, 72, 76]   # ソ・ド・ミ（最後は少し長め）
def plen(k): return 0.17 if k < 2 else 0.26
def glide(n, m, amt=5.0, speed=30.0, vib=0.0):
    t = np.arange(n) / sr
    return mtof(m - amt * np.exp(-t * speed)) * (1 + vib * np.sin(2 * np.pi * 6 * t) * np.minimum(1, t / 0.08))


def build(voice):
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        n = int(plen(k) * sr); place(out, voice(k, n), at)
    return out


# 01 こもった矩形波のぽわっ（しゃくり大きめ）
def v01():
    return build(lambda k, n: lp(pulse(phase(glide(n, STEPS[k], 7, 28)), 0.5), 900 + 200 * k) * env(n, 0.012, 9 if k < 2 else 7))
# 02 三角波＋ゆらぎ（ぽわわん、最後にビブラート）
def v02():
    return build(lambda k, n: tri(phase(glide(n, STEPS[k], 5, 25, 0.02 if k == 2 else 0.006))) * env(n, 0.015, 9 if k < 2 else 6))
# 03 「ぷわっ」母音：こもった のこぎり波を「う→あ」の共鳴に通す
def v03():
    return build(lambda k, n: formant_sweep(saw(phase(glide(n, STEPS[k] - 12, 4, 30))), 350, 900 + 150 * k, 5) * env(n, 0.01, 10 if k < 2 else 7))
# 04 ワウ：矩形波のフィルタが「わっ」と開いてすぐ閉じる
def v04():
    def v(k, n):
        s = pulse(phase(glide(n, STEPS[k] - 12, 3, 30)), 0.35)
        out = np.zeros(n); B = 128
        for i in range(0, n, B):
            t = i / sr; c = 400 + 1800 * np.sin(np.pi * min(1, t / 0.12)) ** 2; out[i:i + B] = lp(s[i:i + B], c, 2)
        return out * env(n, 0.01, 8 if k < 2 else 6)
    return build(v)
# 05 オカリナ：息まじりのまるい音（三角＋息のノイズ）
def v05():
    def v(k, n):
        f = glide(n, STEPS[k], 3, 35, 0.008)
        s = tri(phase(f)) + 0.12 * bp(rng.standard_normal(n), 800, 2500)
        return lp(s, 2200) * env(n, 0.02, 8 if k < 2 else 6)
    return build(v)
# 06 あわ：ぽこっと音程が上がる泡の音を3つ（大きくなるほど低く深く）
def v06():
    def v(k, n):
        t = np.arange(n) / sr; f = mtof(STEPS[k] - 12) * (1 + 1.2 * (1 - np.exp(-t * 25)))
        return tri(phase(f)) * env(n, 0.004, 14 if k < 2 else 9)
    return build(v)
# 07 やわらかいシンセ（ずらした三角2つ、ぽわ〜っと広がる）
def v07():
    def v(k, n):
        f = glide(n, STEPS[k], 4, 25)
        return (tri(phase(f)) + tri(phase(f * 1.008)) + 0.4 * tri(phase(f * 2.003))) * env(n, 0.02, 8 if k < 2 else 5)
    return build(v)
# 08 ゴムまり：低めの「ぽむっ」＋上に小さい「ぽわ」
def v08():
    def v(k, n):
        t = np.arange(n) / sr
        low = tri(phase(mtof(STEPS[k] - 24) * (1 + 0.5 * np.exp(-t * 40)))) * env(n, 0.003, 18)
        hi = lp(pulse(phase(glide(n, STEPS[k], 5, 30)), 0.5), 1100) * env(n, 0.02, 9 if k < 2 else 6) * 0.6
        return low + hi
    return build(v)
# 09 FM でまるく（変調はごく浅く、ぽよっと揺れる）
def v09():
    def v(k, n):
        t = np.arange(n) / sr; f = glide(n, STEPS[k], 5, 28)
        ph = phase(f); return np.sin(ph + 0.9 * np.exp(-t * 10) * tri(ph * 2)) * env(n, 0.012, 9 if k < 2 else 6)
    return build(v)
# 10 口笛まじりのぽわっ（三角＋高い息、最後だけ2音でぽわわっ）
def v10():
    out = build(lambda k, n: (lp(tri(phase(glide(n, STEPS[k], 6, 26))), 2500) + 0.08 * bp(rng.standard_normal(n), 2500, 6000)) * env(n, 0.015, 10 if k < 2 else 7))
    n = int(0.16 * sr); place(out, tri(phase(glide(n, 79, 4, 30))) * env(n, 0.01, 12) * 0.5, PUMPS[2] + 0.1)
    return out


TAIL_END = DUR
def save(path, x):
    n = int(TAIL_END * sr); x = x[:n].copy(); t = np.arange(n) / sr
    x *= np.where(t < 0.5, 1.0, np.clip((TAIL_END - t) / (TAIL_END - 0.5), 0, 1) ** 1.5)
    na = int(0.002 * sr); x[:na] *= np.linspace(0, 1, na)
    x = norm(lp(x, 8000), 0.8)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


if __name__ == '__main__':
    outdir = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(outdir, exist_ok=True)
    for i, fn in enumerate([v01, v02, v03, v04, v05, v06, v07, v08, v09, v10], 1):
        save(os.path.join(outdir, 'growP_%02d.wav' % i), fn())
    print('ok')
