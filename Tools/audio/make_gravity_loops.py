# 重力エリアのループ音の試作（3案）。どれも4秒でつなぎ目なくループする
import numpy as np, wave, os, sys
sr = 44100
T = 4.0
n = int(sr * T)
t = np.arange(n) / sr
out_dir = sys.argv[1]
rng = np.random.default_rng(7)

def save(name, x):
    x = x / max(1e-6, np.max(np.abs(x))) * 0.7
    with wave.open(os.path.join(out_dir, name), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((x * 32767).astype(np.int16).tobytes())

def loop_noise(cut_hz):
    # 周期的なノイズ（FFTで作るので4秒で必ずつながる）
    spec = np.fft.rfft(rng.normal(0, 1, n))
    f = np.fft.rfftfreq(n, 1 / sr)
    spec *= 1 / (1 + (f / cut_hz) ** 4)
    return np.fft.irfft(spec, n)

# A: 低いうなり（ブォーン…）。ゆっくり息をするように強弱
a = (np.sin(2*np.pi*55*t) + 0.6*np.sin(2*np.pi*82.5*t) + 0.25*np.sin(2*np.pi*110*t))
a *= 0.75 + 0.25*np.sin(2*np.pi*0.5*t)          # 0.5Hz（4秒で2回）
a += 0.15 * loop_noise(120)
save('gravity_A_hum.wav', a)

# B: 引っぱられる感じ（ふぉぉん↓ が繰り返す）。下がっていく音が2秒ごとに重なる
b = np.zeros(n)
for k in range(2):
    ph = (t - k*2.0) % T
    env = np.exp(-ph*1.1) * np.minimum(1, ph/0.25)
    freq = 330*np.exp(-ph*0.55)                     # だんだん下がる
    phase = 2*np.pi*np.cumsum(np.roll(freq, 0))/sr
    b += env * (np.sin(phase) + 0.3*np.sin(2*phase))
b += 0.5*np.sin(2*np.pi*55*t) * (0.8 + 0.2*np.sin(2*np.pi*0.25*t))
save('gravity_B_pull.wav', b)

# C: うねる低音（ウォン…ウォン…）。重たい空間がゆらぐ感じ
c = np.zeros(n)
lfo = 0.5 + 0.5*np.sin(2*np.pi*1.5*t)             # 1.5Hz（4秒で6回）
for h, amp in [(1, 1.0), (2, 0.5), (3, 0.3), (4, 0.15)]:
    c += amp * np.sin(2*np.pi*65*h*t) * (lfo ** (0.5*h))
c += 0.1 * loop_noise(200) * lfo
save('gravity_C_wobble.wav', c)
print('ok')
