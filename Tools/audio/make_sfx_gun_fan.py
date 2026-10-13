# 銃のスポーン音と、扇風機の ON/OFF 音（2026-10-14 てじゃ「銃のスポーン時（台クリックと召喚）、扇風機のオンオフに効果音」）
#   python make_sfx_gun_fan.py <出力フォルダ>
# 銃のスポーン：やわらかい「ぽんっ」（少し下がる丸い音）＋キラッ（ポータルの音と同じト長調のベル）＋ふわっとした空気
# 扇風機 ON：カチッ（スイッチ）＋モーターが回り始める「ふぃーん↑」、OFF：カチッ＋止まっていく「ふぃーん↓」
import numpy as np, wave, sys, os
sr = 44100
rng = np.random.default_rng(7)

def bell(fq, dur, vel=1.0, bright=0.25, decay=6.0):
    t = np.arange(int(dur * sr)) / sr
    s = np.sin(2 * np.pi * fq * t) + bright * np.sin(2 * np.pi * fq * 2.76 * t) * np.exp(-t * 12) + 0.15 * np.sin(2 * np.pi * fq * 2 * t) * np.exp(-t * 8)
    return s * np.exp(-t * decay) * np.minimum(1, t / 0.004) * vel

def place(out, s, at):
    k = int(at * sr); L = min(len(s), len(out) - k); out[k:k + L] += s[:L]

def lowpass(x, a):   # 1次のローパス（a 小さいほどこもる）
    y = np.zeros_like(x); acc = 0.0
    for i in range(len(x)): acc += a * (x[i] - acc); y[i] = acc
    return y

def finish(out, peak):
    d = int(0.05 * sr); rev = np.zeros_like(out); rev[d:] = out[:-d] * 0.15; out = out + rev
    f = int(0.04 * sr); out[-f:] *= np.linspace(1, 0, f)
    out /= np.max(np.abs(out)); return out * peak

def gun_spawn():
    out = np.zeros(int(0.75 * sr))
    t = np.arange(int(0.3 * sr)) / sr
    f = 260 * (1 + 0.6 * np.exp(-t * 28))
    thump = np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t * 16) * np.minimum(1, t / 0.003)
    place(out, thump * 0.9, 0.0)
    # ふわっ（空気）：ノイズを少しこもらせて、短く
    n = rng.standard_normal(int(0.22 * sr)); tn = np.arange(len(n)) / sr
    air = lowpass(n, 0.18) * np.exp(-tn * 18) * np.minimum(1, tn / 0.01)
    place(out, air * 0.35, 0.0)
    place(out, bell(1567.98, 0.5, 0.5, bright=0.15, decay=8), 0.05)    # G6
    place(out, bell(2349.32, 0.4, 0.3, bright=0.1, decay=11), 0.10)    # D7
    return finish(out, 0.6)

def click(vel=1.0, tone=2600):
    t = np.arange(int(0.03 * sr)) / sr
    n = rng.standard_normal(len(t))
    c = (np.sin(2 * np.pi * tone * t) * 0.6 + lowpass(n, 0.5) * 0.6) * np.exp(-t * 260)
    return c * vel

def motor(f0, f1, dur, rising):
    t = np.arange(int(dur * sr)) / sr
    k = t / dur
    curve = (1 - np.exp(-k * 3.5)) / (1 - np.exp(-3.5)) if rising else np.exp(-k * 3.0)
    f = f0 + (f1 - f0) * (curve if rising else (1 - curve))
    if not rising: f = f1 + (f0 - f1) * curve   # f0 → f1 へ下がる
    ph = 2 * np.pi * np.cumsum(f) / sr
    s = np.sin(ph) * 0.6 + np.sin(ph * 2) * 0.25 + np.sin(ph * 3) * 0.1
    # 羽の風切り（ノイズを羽の回転でゆらす）
    n = lowpass(rng.standard_normal(len(t)), 0.25) * (0.5 + 0.5 * np.sin(ph * 0.5)) * 0.35
    amp = np.minimum(1, t / 0.03) * (np.minimum(1, (dur - t) / 0.12) if rising else np.exp(-k * 2.2))
    return (s + n) * amp

# 2026-10-14 てじゃ「扇風機、カチッだけのほうがいいかも」→ モーター音なし。ON は少し高い・OFF は少し低いカチッ
def fan_on():
    out = np.zeros(int(0.15 * sr))
    place(out, click(1.0, 2800), 0.0)
    return finish(out, 0.45)

def fan_off():
    out = np.zeros(int(0.15 * sr))
    place(out, click(1.0, 2000), 0.0)
    return finish(out, 0.45)

def save(path, x):
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())

if __name__ == '__main__':
    d = sys.argv[1]
    save(os.path.join(d, 'sfx_gun_spawn.wav'), gun_spawn())
    save(os.path.join(d, 'sfx_fan_on.wav'), fan_on())
    save(os.path.join(d, 'sfx_fan_off.wav'), fan_off())
    print('ok')
