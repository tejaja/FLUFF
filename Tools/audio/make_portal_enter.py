# ポータルに乗った瞬間の「入った！」音（2026-10-12 てじゃ「入った！ってのをわかりやすく、入った瞬間に音ほしい」）
# たまる音（make_portal_charge.py、ト長調ペンタトニックのベル）と同じ音色・同じ調で、上がる短いフレーズ
#   python make_portal_enter.py <出力.wav> <案A|B|C>
#   A：ぽわん（2音 G→D、やわらかベル）  B：ぽろん（3音 G→B→D の速い上がり）  C：ぽんっ（低い丸い音＋高いキラッ）
import numpy as np, wave, sys
sr = 44100

def bell(fq, dur, vel=1.0, bright=0.25, decay=6.0):
    t = np.arange(int(dur * sr)) / sr
    s = np.sin(2 * np.pi * fq * t) + bright * np.sin(2 * np.pi * fq * 2.76 * t) * np.exp(-t * 12) + 0.15 * np.sin(2 * np.pi * fq * 2 * t) * np.exp(-t * 8)
    return s * np.exp(-t * decay) * np.minimum(1, t / 0.004) * vel

def place(out, s, at):
    k = int(at * sr); L = min(len(s), len(out) - k); out[k:k + L] += s[:L]

def make(kind):
    T = 0.7
    out = np.zeros(int(T * sr))
    if kind == 'A':
        place(out, bell(392.00, 0.6, 0.8), 0.0)
        place(out, bell(587.33, 0.6, 1.0), 0.09)
        place(out, bell(1174.66, 0.4, 0.25, bright=0.1, decay=9), 0.09)   # うっすら上のオクターブで明るく
    elif kind == 'B':
        for i, f in enumerate([392.00, 493.88, 587.33]):
            place(out, bell(f, 0.55, 0.7 + 0.15 * i), i * 0.055)
        place(out, bell(1174.66, 0.35, 0.3, bright=0.1, decay=10), 0.11)
    else:
        t = np.arange(int(0.35 * sr)) / sr
        f = 220 * (1 + 0.5 * np.exp(-t * 30))   # ぽんっ（少し下がる丸い音）
        thump = np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t * 14) * np.minimum(1, t / 0.003)
        place(out, thump * 0.9, 0.0)
        place(out, bell(1567.98, 0.45, 0.45, bright=0.15, decay=8), 0.04)
        place(out, bell(2349.32, 0.35, 0.25, bright=0.1, decay=11), 0.08)
    # ほんの少しの響き
    d = int(0.06 * sr); rev = np.zeros_like(out); rev[d:] = out[:-d] * 0.18; out = out + rev
    out = np.convolve(out, np.ones(3) / 3, mode='same')
    out[-int(0.05 * sr):] *= np.linspace(1, 0, int(0.05 * sr))
    out /= np.max(np.abs(out)); out *= 0.6
    return out

if __name__ == '__main__':
    path, kind = sys.argv[1], (sys.argv[2] if len(sys.argv) > 2 else 'A')
    out = make(kind)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((out * 32767).astype(np.int16).tobytes())
    print('ok', kind)
