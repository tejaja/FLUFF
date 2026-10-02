# ゴールの胞子花火の音（1本の音に全部の打ち上げ・破裂を入れる）
# 打ち上げ「ひゅ〜」 t=0, 0.35, 0.7 → 破裂「パーン」 t=0.9, 1.25, 1.6 → 小さい花火「ポンポン」 t=2.2〜2.9
import numpy as np, wave, sys
sr = 44100
T = 3.6
out = np.zeros(int(sr * T))
rng = np.random.default_rng(3)

def add(sig, t0, gain=1.0):
    i = int(t0 * sr); n = min(len(sig), len(out) - i)
    out[i:i + n] += sig[:n] * gain

def whistle(dur=0.8, f0=900, f1=1900):
    t = np.arange(int(sr * dur)) / sr
    f = f0 + (f1 - f0) * (t / dur) ** 0.7
    ph = 2 * np.pi * np.cumsum(f) / sr
    env = np.minimum(1, t / 0.05) * np.exp(-2.2 * t) * (1 - t / dur) ** 0.5
    return np.sin(ph) * env * 0.18 + rng.normal(0, 1, len(t)) * env * 0.02

def bang(dur=1.2, big=True):
    t = np.arange(int(sr * dur)) / sr
    noise = rng.normal(0, 1, len(t))
    # low thump + soft crackle (keep it gentle)
    k = np.exp(-t * (6 if big else 10))
    low = np.sin(2 * np.pi * (70 if big else 110) * t * (1 - 0.3 * t)) * k
    sm = np.convolve(noise, np.ones(40) / 40, mode='same')
    crack = np.zeros(len(t))
    for _ in range(40 if big else 12):
        c = int(rng.uniform(0.05, dur * 0.8) * sr); L = int(0.006 * sr)
        if c + L < len(t): crack[c:c + L] += rng.normal(0, 1, L) * np.exp(-np.arange(L) / (L / 3)) * rng.uniform(0.2, 0.6)
    s = low * 0.55 + sm * k * 1.2 + crack * np.exp(-t * 2.5) * 0.35
    return s * (1.0 if big else 0.5)

for t0 in (0.0, 0.35, 0.7):
    add(whistle(), t0, 1.0)
for t0 in (0.9, 1.25, 1.6):
    add(bang(), t0, 1.0)
for t0 in (2.2, 2.4, 2.65, 2.9):
    add(bang(0.7, big=False), t0, 1.0)

out /= max(1e-6, np.max(np.abs(out)))
out *= 0.8
path = sys.argv[1]
with wave.open(path, 'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
    w.writeframes((out * 32767).astype(np.int16).tobytes())
print('saved', path)
