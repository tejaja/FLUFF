# ポータルに立っている間の「たまる音」作り直し候補（2026-10-12 てじゃ「ピロピロのほうもこの際かえるかぁ」）。1.5秒
#   python make_portal_charge2.py <出力.wav> <案X|Y|Z>
#   X：ふわーっ（やわらかい和音のパッドが、だんだん明るく・高くふくらむ。粒なし）
#   Y：ぽこぽこ（あわがはじけるような丸い粒が、だんだん速く・高く）
#   Z：しゅわわ〜ん（上がっていく音＋最後に向けてベルが1つずつ重なる。前のピロピロを落ち着かせた版）
# 風っぽいノイズは使わない（コースの風ギミックの音とまぎれるので）
import numpy as np, wave, sys
sr = 44100
T = 1.5
n = int(sr * T)
t = np.arange(n) / sr
x = t / T

def tri(ph): return 2 / np.pi * np.arcsin(np.sin(ph))

def make(kind):
    rng = np.random.default_rng(5)
    if kind == 'X':
        out = np.zeros(n)
        # G の和音（G B D G）を、全体で4度くらい持ち上げながら、上の声部をだんだん足す
        bend = 2 ** (5 / 12 * x ** 1.6)
        for i, (f, start) in enumerate([(196.0, 0.0), (246.94, 0.0), (293.66, 0.25), (392.0, 0.5), (587.33, 0.75)]):
            env = np.clip((x - start) / 0.25, 0, 1) ** 1.5
            ph = 2 * np.pi * np.cumsum(f * bend) / sr
            det = 2 * np.pi * np.cumsum(f * bend * 1.004) / sr
            out += (np.sin(ph) + 0.5 * tri(det) * x) * env * (0.5 + 0.5 * x) / (1 + i * 0.2)
        out *= np.minimum(1, t / 0.15)
    elif kind == 'Y':
        out = np.zeros(n)
        at = 0.02
        scale = [392.0, 440.0, 493.88, 587.33, 659.25, 783.99, 880.0, 987.77, 1174.66]
        while at < T - 0.05:
            prog = at / T
            f = scale[min(len(scale) - 1, int(prog * (len(scale) - 2)) + rng.integers(0, 2))]
            L = int(0.18 * sr); tt = np.arange(L) / sr
            # あわ：音程がきゅっと上がる丸い音
            fq = f * (1 + 0.35 * (1 - np.exp(-tt * 40)))
            s = np.sin(2 * np.pi * np.cumsum(fq) / sr) * np.exp(-tt * 22) * np.minimum(1, tt / 0.003)
            k = int(at * sr); Lk = min(L, n - k)
            out[k:k + Lk] += s[:Lk] * (0.45 + 0.4 * prog)
            at += 0.15 - 0.10 * prog + rng.uniform(-0.01, 0.01)
        # 下にうっすら上がる土台
        f = 196 * 2 ** (0.6 * x)
        out += np.sin(2 * np.pi * np.cumsum(f) / sr) * np.minimum(1, t / 0.3) * 0.12
    else:
        out = np.zeros(n)
        f = 196 * 2 ** (1.0 * x ** 1.3)   # 1オクターブ上がる
        sweep = (np.sin(2 * np.pi * np.cumsum(f) / sr) + 0.35 * np.sin(2 * np.pi * np.cumsum(f * 2) / sr)) * np.minimum(1, t / 0.2) * (0.35 + 0.3 * x)
        out += sweep
        for i, (fb, at) in enumerate([(392.0, 0.15), (493.88, 0.55), (587.33, 0.85), (783.99, 1.1), (987.77, 1.3)]):
            L = min(int(0.6 * sr), n - int(at * sr)); tt = np.arange(L) / sr
            s = (np.sin(2 * np.pi * fb * tt) + 0.2 * np.sin(2 * np.pi * fb * 2.76 * tt) * np.exp(-tt * 12)) * np.exp(-tt * 5) * np.minimum(1, tt / 0.004)
            k = int(at * sr); out[k:k + L] += s * (0.35 + 0.1 * i)
    out = np.convolve(out, np.ones(3) / 3, mode='same')
    out[-int(0.04 * sr):] *= np.linspace(1, 0, int(0.04 * sr))
    out /= np.max(np.abs(out)); out *= 0.6
    return out

if __name__ == '__main__':
    path, kind = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else 'X')
    out = make(kind)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((out * 32767).astype(np.int16).tobytes())
    print('ok', kind)
