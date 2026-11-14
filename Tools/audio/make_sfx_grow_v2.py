# 2–5：大きくなる音 その2（2026-11-14 てじゃ「しっくりこない」→ 方向C「ゲームっぽく、はっきりパワーアップした感、音色は自前で」）
#   python make_sfx_grow_v2.py <出力フォルダ>
#   アニメの3段（0.00・0.18・0.36s）に合わせて「タッ・タッ・ターン！」。サイン波のピロリンは使わない
import numpy as np, wave, sys, os
from scipy.signal import butter, sosfilt
sr = 44100
rng = np.random.default_rng(2114)
PUMPS = [0.0, 0.18, 0.36]
DUR = 1.2


def T(d): return np.arange(int(d * sr)) / sr
def lp(x, f, order=2): return sosfilt(butter(order, min(f, sr * 0.45), btype='low', fs=sr, output='sos'), x)
def hp(x, f, order=2): return sosfilt(butter(order, f, btype='high', fs=sr, output='sos'), x)
def bp(x, lo, hi, order=2): return sosfilt(butter(order, [lo, hi], btype='band', fs=sr, output='sos'), x)
def place(out, s, at):
    k = int(at * sr)
    if k >= len(out): return
    L = min(len(s), len(out) - k); out[k:k + L] += s[:L]
def norm(x, peak=0.8):
    m = np.max(np.abs(x)); return x / m * peak if m > 0 else x
def fade(x, a=0.002, b=0.05):
    x = x.copy(); na, nb = int(a * sr), int(b * sr)
    if na: x[:na] *= np.linspace(0, 1, na)
    if nb: x[-nb:] *= np.linspace(1, 0, nb)
    return x
def phase(freq): return 2 * np.pi * np.cumsum(freq) / sr
def F(f, n): return np.full(n, float(f))
def saw(ph): return 2 * ((ph / (2 * np.pi)) % 1.0) - 1
def tri(ph): return 2 * np.abs(saw(ph)) - 1
def pulse(ph, duty=0.25): return np.where((ph / (2 * np.pi)) % 1.0 < duty, 1.0, -1.0)
def env(n, a, d, s=0.0, rel=None):
    t = np.arange(n) / sr; e = np.minimum(1, t / max(a, 1e-4)) * (s + (1 - s) * np.exp(-np.maximum(0, t - a) * d))
    if rel: e *= np.minimum(1, np.maximum(0, (n / sr - t)) / rel)
    return e
def mtof(m): return 440.0 * 2 ** ((m - 69) / 12)
def plen(k): return 0.16 if k < 2 else 0.8

# 3段の音（MIDI）：ドの上のソ → 1オクターブ上 … みたいな「上がる」、最後は和音
STEPS = [67, 72, 79]          # G4 C5 G5
CHORD = [72, 76, 79, 84]      # C5 E5 G5 C6


# 01 ファミコン系：矩形波（25%）の3段＋最後は 12.5% と 50% の重ね、少しビブラート
def v01():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        n = int(plen(k) * sr); t = np.arange(n) / sr
        f = mtof(STEPS[k]) * (1 + (0.012 * np.sin(2 * np.pi * 7 * t) * np.minimum(1, t / 0.15) if k == 2 else 0))
        s = pulse(phase(f), 0.25) * 0.5
        if k == 2: s += pulse(phase(f * 0.5), 0.5) * 0.3 + pulse(phase(f * 1.5), 0.125) * 0.18
        place(out, s * env(n, 0.003, 6 if k < 2 else 1.6, 0, 0.03), at)
    return out


# 02 ファミコン系その2：音程が下から「ぎゅいっ」と上がる矩形波（各段でしゃくり）＋三角波のベース
def v02():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        n = int(plen(k) * sr); t = np.arange(n) / sr
        m = STEPS[k] - 5 * np.exp(-t * 40)
        s = pulse(phase(mtof(m)), 0.5) * 0.4 + tri(phase(F(mtof(STEPS[k] - 24), n))) * 0.5
        place(out, s * env(n, 0.002, 7 if k < 2 else 1.8, 0, 0.03), at)
    return out


# 03 シンセブラス：ずらした のこぎり波×3 をフィルタで「パッ」と開く、最後は和音
def v03():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        n = int(plen(k) * sr); t = np.arange(n) / sr
        notes = [STEPS[k]] if k < 2 else CHORD
        s = np.zeros(n)
        for m in notes:
            for dt in (-0.006, 0, 0.007): s += saw(phase(F(mtof(m) * (1 + dt), n)))
        cut = 600 + 5000 * np.exp(-t * (14 if k < 2 else 4))
        y = np.zeros(n); B = 256
        for i in range(0, n, B): y[i:i + B] = lp(s[i:i + B], cut[i], 1)
        place(out, y * env(n, 0.008, 5 if k < 2 else 1.5, 0, 0.05), at)
    return out


# 04 チャージ→ドン：3段ごとに のこぎり波の音程が上へすべる、最後に低いドン＋明るい和音
def v04():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        n = int((0.17 if k < 2 else 0.25) * sr); t = np.arange(n) / sr
        m = 48 + 8 * k + 10 * t / (n / sr)
        s = saw(phase(mtof(m))) * 0.5 + pulse(phase(mtof(m + 12)), 0.3) * 0.25
        place(out, lp(s, 3000 + 2000 * k) * env(n, 0.004, 4, 0.4, 0.02), at)
    at = PUMPS[2] + 0.22; n = int(0.7 * sr); t = np.arange(n) / sr
    boom = np.sin(phase(mtof(36) * (1 + 1.5 * np.exp(-t * 30)))) * env(n, 0.002, 6)
    ch = sum(pulse(phase(F(mtof(m), n)), 0.25) for m in CHORD) * 0.15 * env(n, 0.003, 2.2, 0, 0.05)
    place(out, boom * 0.9 + lp(ch, 5000), at)
    return out


# 05 オルガン：ドローバー風（基音＋オクターブ＋5度、矩形まじり）の3段＋和音、パーカッション付き
def v05():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        n = int(plen(k) * sr); t = np.arange(n) / sr
        notes = [STEPS[k]] if k < 2 else CHORD[:3]
        s = np.zeros(n)
        for m in notes:
            f = mtof(m)
            s += tri(phase(F(f, n))) + 0.6 * tri(phase(F(f * 2, n))) + 0.35 * tri(phase(F(f * 3, n))) + 0.5 * tri(phase(F(f * 4, n))) * np.exp(-t * 25)
        place(out, s * env(n, 0.004, 3 if k < 2 else 0.6, 0.0 if k < 2 else 0.5, 0.06), at)
    return out


# 06 ブラスのスタブ＋ティンパニ（オーケストラっぽい「ジャン・ジャン・ジャーン！」）
def v06():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        n = int(plen(k) * sr); t = np.arange(n) / sr
        notes = [STEPS[k] - 12, STEPS[k]] if k < 2 else [60, 64, 67, 72]
        s = np.zeros(n)
        for m in notes:
            f = mtof(m) * (1 + 0.004 * np.sin(2 * np.pi * 5 * t) * (k == 2))
            s += saw(phase(f)) + 0.6 * saw(phase(f * 1.004))
        y = lp(s, 2200) * env(n, 0.02, 6 if k < 2 else 1.3, 0, 0.06)
        tn = int(0.4 * sr); tt = np.arange(tn) / sr
        timp = np.sin(phase(F(mtof(36 + 5 * k), tn) * (1 + 0.2 * np.exp(-tt * 40)))) * np.exp(-tt * (8 if k < 2 else 3))
        place(out, y, at); place(out, 0.8 * timp, at)
    return out


# 07 ピコピコじゃない電子音：FM のベル（金属っぽい）3段＋最後はシマーの和音
def v07():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        n = int(plen(k) * sr); t = np.arange(n) / sr
        notes = [STEPS[k]] if k < 2 else CHORD
        s = np.zeros(n)
        for m in notes:
            f = mtof(m); I = 3.0 * np.exp(-t * 8)
            s += np.sin(phase(F(f, n)) + I * np.sin(phase(F(f * 3.5, n))))
        place(out, s * env(n, 0.002, 7 if k < 2 else 1.6, 0, 0.05), at)
    return out


# 08 ベースのスラップ「ドゥッ・ドゥッ・ドゥーン！」＋矩形のリード（ファンク寄り）
def v08():
    out = np.zeros(int(DUR * sr))
    bass = [43, 48, 55]
    for k, at in enumerate(PUMPS):
        n = int(plen(k) * sr); t = np.arange(n) / sr
        b = saw(phase(F(mtof(bass[k]), n))); b = lp(b, 300 + 3000 * np.exp(-t[0] * 0) * 1.0) * env(n, 0.002, 9 if k < 2 else 2.5)
        click = hp(rng.standard_normal(n), 2000) * np.exp(-t * 300) * 0.3
        lead = pulse(phase(F(mtof(STEPS[k] + 12), n)), 0.3) * 0.25 * env(n, 0.003, 8 if k < 2 else 2, 0, 0.03)
        place(out, b + click + lead, at)
    return out


# 09 マリンバ（木）とグロッケン：3段の音を2打ずつトトッと＋最後のロール
def v09():
    out = np.zeros(int(DUR * sr))
    def wood(m, d=0.5):
        n = int(d * sr); t = np.arange(n) / sr; f = mtof(m)
        return (np.sin(phase(F(f, n))) * np.exp(-t * 9) + 0.5 * np.sin(phase(F(f * 3.93, n))) * np.exp(-t * 35) + 0.2 * np.sin(phase(F(f * 9.2, n))) * np.exp(-t * 80)) * env(n, 0.001, 0)
    for k, at in enumerate(PUMPS):
        place(out, wood(STEPS[k] - 12), at); place(out, wood(STEPS[k]) * 0.8, at + 0.05)
    for i in range(10):
        place(out, wood(CHORD[i % 4], 0.6) * (0.6 + 0.04 * i), PUMPS[2] + 0.09 + i * 0.035)
    return out


# 10 チップチューンの「上がってキラーン」：矩形波の速い装飾（16分の刻み）で3段→ノイズのシャーン
def v10():
    out = np.zeros(int(DUR * sr))
    for k, at in enumerate(PUMPS):
        seq = [STEPS[k] - 7, STEPS[k] - 3, STEPS[k]] if k < 2 else [72, 76, 79, 84, 79, 84, 88]
        dt = 0.045
        for i, m in enumerate(seq):
            n = int((dt if i < len(seq) - 1 else (0.1 if k < 2 else 0.5)) * sr)
            s = pulse(phase(F(mtof(m), n)), 0.25 if i % 2 == 0 else 0.125) * 0.4
            place(out, s * env(n, 0.002, 4 if k < 2 else 2, 0.3, 0.01), at + i * dt)
    n = int(0.6 * sr); t = np.arange(n) / sr
    place(out, hp(rng.standard_normal(n), 5000) * np.exp(-t * 6) * 0.12, PUMPS[2] + 0.3)
    return out


TAIL_END = 0.62   # 2026-11-14 てじゃ「後ろがのばしすぎ」→ 最後の音は 0.36s から 0.26s で消す
def save(path, x):
    n = int(TAIL_END * sr); x = x[:n].copy(); t = np.arange(n) / sr
    x *= np.where(t < 0.48, 1.0, np.clip((TAIL_END - t) / (TAIL_END - 0.48), 0, 1) ** 1.5)
    x = fade(norm(lp(x, 12000), 0.8))
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


if __name__ == '__main__':
    outdir = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(outdir, exist_ok=True)
    for i, fn in enumerate([v01, v02, v03, v04, v05, v06, v07, v08, v09, v10], 1):
        save(os.path.join(outdir, 'growC_%02d.wav' % i), fn())
    print('ok')
