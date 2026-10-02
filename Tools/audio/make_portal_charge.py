# ポータルに立っている間の「たまっていく」音（1.5秒）。キラキラ主役版：
# やわらかいベルの粒が、だんだん細かく・高くなりながら降ってくる。下にごく薄い音の土台
import numpy as np, wave, sys
sr = 44100
T = 1.5
n = int(sr * T)
t = np.arange(n) / sr
x = t / T
rng = np.random.default_rng(11)

# ペンタトニック（C D E G A）で、だんだん上へ
scale = [392.00, 440.00, 493.88, 587.33, 659.25]   # ト長調のペンタトニック（前より4度低い）
notes = [s * (2 ** o) for o in range(0, 3) for s in scale]   # 392〜2637Hz

bell = np.zeros(n)
at = 0.03
i = 0
while at < T - 0.05:
    prog = at / T
    idx = min(len(notes) - 1, int(prog * (len(notes) - 4)) + rng.integers(0, 4))
    fq = notes[min(idx, 9)]   # 最後が高くなりすぎないよう、上は1318Hzまで（前半はそのまま）
    L = min(int(0.45 * sr), n - int(at * sr))
    tt = np.arange(L) / sr
    # やわらかいベル：基音＋ちょっとだけ上の倍音、すぐ減衰
    s = (np.sin(2 * np.pi * fq * tt) + 0.25 * np.sin(2 * np.pi * fq * 2.76 * tt) * np.exp(-tt * 12))
    s *= np.exp(-tt * 7) * np.minimum(1, tt / 0.004)
    amp = 0.35 + 0.35 * prog
    k = int(at * sr)
    bell[k:k + L] += s * amp
    # だんだん間隔が詰まる（0.16秒 → 0.05秒）
    at += 0.16 - 0.11 * prog + rng.uniform(-0.015, 0.015)
    i += 1

# ごく薄い土台（ふわっと上がる）
f = 196 * 2 ** (0.5 * x)
pad = np.sin(2 * np.pi * np.cumsum(f) / sr) * np.minimum(1, t / 0.4) * 0.12

out = bell + pad
out = np.convolve(out, np.ones(4) / 4, mode='same')
out[-int(0.05 * sr):] *= np.linspace(1, 0, int(0.05 * sr))
out /= np.max(np.abs(out)); out *= 0.6
with wave.open(sys.argv[1], 'wb') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
    w.writeframes((out * 32767).astype(np.int16).tobytes())
print('ok notes', i)
