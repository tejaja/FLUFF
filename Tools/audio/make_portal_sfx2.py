# ポータルの音・決定版の素（2026-10-12 てじゃがメニューから「乗った瞬間5・たまる音4」）
#   乗った瞬間：しゃらん（ベルのすばやい上がりアルペジオ、ト長調ペンタトニック）
#   たまる音  ：ぽこぽこ（泡がはじける丸い音が、だんだん速く・高く。最後はワープ音へつながる）
#   python make_portal_sfx2.py <出力フォルダ>  → sfx_portal_enter.wav / sfx_portal_charge.wav（1.5秒）
import numpy as np, wave, sys, os
sr = 44100
rng = np.random.default_rng(21)

def tt(T): return np.arange(int(T * sr)) / sr

def bell(f, T, decay=8.0, bright=0.2):
    t = tt(T)
    s = np.sin(2 * np.pi * f * t) + bright * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t * 14) + 0.12 * np.sin(2 * np.pi * f * 2 * t) * np.exp(-t * 10)
    return s * np.exp(-t * decay) * np.minimum(1, t / 0.003)

def add(out, s, at, g=1.0):
    k = int(at * sr); L = min(len(s), len(out) - k)
    if L > 0: out[k:k + L] += s[:L] * g

def enter():
    out = np.zeros(int(0.55 * sr))
    notes = [587.33, 783.99, 987.77, 1174.66, 1567.98]   # D5 G5 B5 D6 G6
    for i, f in enumerate(notes):
        add(out, bell(f, 0.5, decay=10 + i), i * 0.032, 0.55 + 0.1 * i)   # 余韻は短め（泡が始まる 0.42 秒までに消える）
    # 下に丸いひと鳴り（入ったよ、の芯）
    add(out, bell(392.0, 0.45, decay=9, bright=0.1), 0.0, 0.6)
    d = int(0.05 * sr); rev = np.zeros_like(out); rev[d:] = out[:-d] * 0.2; out += rev
    out[-int(0.05 * sr):] *= np.linspace(1, 0, int(0.05 * sr))
    return out

def charge():
    T = 1.5; n = int(T * sr); out = np.zeros(n)
    scale = [392.0, 440.0, 493.88, 587.33, 659.25, 783.99, 880.0, 987.77]   # G4〜B5
    # しゃらん（乗った瞬間の音、0.4秒くらい）と重ならないよう、泡は 0.42 秒から（てじゃ「重なってるのがきになる」）
    t0 = 0.42
    at = t0
    while at < T - 0.06:
        p = (at - t0) / (T - t0)
        f = scale[min(len(scale) - 1, int(p * (len(scale) - 2)) + rng.integers(0, 2))]
        t = tt(0.16)
        fq = f * (1 + 0.45 * (1 - np.exp(-t * 45)))      # ぽこっ：音程がきゅっと上がる
        s = np.sin(2 * np.pi * np.cumsum(fq) / sr) * np.exp(-t * 24) * np.minimum(1, t / 0.002)
        add(out, s, at, 0.45 + 0.45 * p)
        at += 0.15 - 0.10 * p + rng.uniform(-0.01, 0.01)   # だんだん速く
    # うっすら上がる土台（泡が湧いてくる水の感じ。ノイズは使わない）
    t = tt(T); f = 196 * 2 ** (0.7 * t / T)
    out += np.sin(2 * np.pi * np.cumsum(f) / sr) * np.clip((t - t0) / 0.3, 0, 1) * (0.08 + 0.08 * t / T)
    out[-int(0.04 * sr):] *= np.linspace(1, 0, int(0.04 * sr))
    return out

def save(p, x):
    x = x / np.max(np.abs(x)) * 0.6
    with wave.open(p, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes((x * 32767).astype(np.int16).tobytes())

if __name__ == '__main__':
    d = sys.argv[1]; os.makedirs(d, exist_ok=True)
    save(os.path.join(d, 'sfx_portal_enter.wav'), enter())
    save(os.path.join(d, 'sfx_portal_charge.wav'), charge())
    print('ok')
