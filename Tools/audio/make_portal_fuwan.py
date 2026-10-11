# ポータルの音（2026-10-12 てじゃ「ｼｭｲﾝ→ﾌﾜﾝﾌﾜﾝﾌﾜﾝ みたいな」）
#   乗った瞬間：シュイン（すばやく上がる澄んだ音、キラッとした倍音つき。風っぽいノイズは使わない）
#   たまる間  ：フワンフワンフワン（やわらかい音が「ふわん」とふくらんで少し上がる、を3回。回ごとに少し高く・大きく）
#   python make_portal_fuwan.py <出力フォルダ> [案1|案2]
#     案1：ふわんは丸いサイン＋オクターブ上（やさしい）
#     案2：ふわんにコーラスっぽい揺れ（ゆらぎ多め・少しにぎやか）
import numpy as np, wave, sys, os
sr = 44100

def save(path, out):
    out = out / np.max(np.abs(out)) * 0.6
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((out * 32767).astype(np.int16).tobytes())

def shuin():
    T = 0.45; t = np.arange(int(T * sr)) / sr
    # 0.12秒で G4→G6 へ駆け上がって、そのまま上で軽く鳴って消える
    f = 392 * 4 ** np.clip(t / 0.12, 0, 1) ** 0.7
    ph = 2 * np.pi * np.cumsum(f) / sr
    env = np.minimum(1, t / 0.01) * np.where(t < 0.12, 0.6 + 0.4 * t / 0.12, np.exp(-(t - 0.12) * 9))
    s = (np.sin(ph) + 0.3 * np.sin(2 * ph) + 0.15 * np.sin(3.01 * ph)) * env
    # 頂上でキラッ
    k = int(0.11 * sr); tt = np.arange(len(t) - k) / sr
    s[k:] += 0.35 * np.sin(2 * np.pi * 3135.96 * tt) * np.exp(-tt * 14)
    s[-int(0.03 * sr):] *= np.linspace(1, 0, int(0.03 * sr))
    return s

def fuwan(kind):
    T = 1.5; n = int(T * sr); t = np.arange(n) / sr
    out = np.zeros(n)
    base = [293.66, 349.23, 392.0]   # D → F → G（だんだん上がって、最後は主音に帰る）
    for i in range(3):
        st = i * 0.5; L = int(0.62 * sr); tt = np.arange(L) / sr
        f0 = base[i]
        f = f0 * (1 + 0.06 * (1 - np.exp(-tt * 9)))   # ふわん：ちょっと上がる
        if kind == '2': f = f * (1 + 0.012 * np.sin(2 * np.pi * 6 * tt))
        ph = 2 * np.pi * np.cumsum(f) / sr
        env = np.sin(np.pi * np.clip(tt / 0.55, 0, 1)) ** 1.2   # ふくらんで、しぼむ
        s = np.sin(ph) + 0.35 * np.sin(2 * ph) * (0.6 + 0.4 * tt / 0.55)
        if kind == '2':
            ph2 = 2 * np.pi * np.cumsum(f * 1.006) / sr
            s += 0.6 * np.sin(ph2) + 0.25 * np.sin(2 * ph2)
        s *= env * (0.6 + 0.2 * i)
        k = int(st * sr); Lk = min(L, n - k)
        out[k:k + Lk] += s[:Lk]
    out[-int(0.04 * sr):] *= np.linspace(1, 0, int(0.04 * sr))
    return out

if __name__ == '__main__':
    d = sys.argv[1]; kind = sys.argv[2] if len(sys.argv) > 2 else '1'
    os.makedirs(d, exist_ok=True)
    save(os.path.join(d, 'sfx_portal_enter.wav'), shuin())
    save(os.path.join(d, 'sfx_portal_charge_fuwan%s.wav' % kind), fuwan(kind))
    print('ok', kind)
