# 2–4 はぐるまのこうば：動くしかけの音の候補（2026-11-10 てじゃ「しかけの音」→ 聞き比べ用に A/B/C）
#   python make_sfx_gimmicks_2_4.py <出力フォルダ>
#   ありがちな「サイン波のピコピコ」は使わない：金属の鳴り（倍音が整数倍でない部分音）＋ノイズの成形で機械っぽく
#   部品（Unity で使う）：click_{A,B,C}.wav（歯1枚ぶんのカチッ）、land_{A,B,C}.wav（跳ね橋が下りきった）、
#     rumble_{A,B,C}.wav（伸びる橋が動いている間のループ、2秒）、meet_{A,B,C}.wav（伸びる橋がまん中でつながった）
#   聞き比べ用：demo_drawbridge.wav / demo_rackbridge.wav / demo_turntable.wav（A → B → C の順、間に1秒）
import numpy as np, wave, sys, os
from scipy.signal import butter, sosfilt
sr = 44100
rng = np.random.default_rng(24)


def T(d): return np.arange(int(d * sr)) / sr


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], btype='band', fs=sr, output='sos'), x)


def lp(x, f, order=2):
    return sosfilt(butter(order, f, btype='low', fs=sr, output='sos'), x)


def hp(x, f, order=2):
    return sosfilt(butter(order, f, btype='high', fs=sr, output='sos'), x)


def ring(freqs, amps, decays, dur):
    t = T(dur); s = np.zeros_like(t)
    for f, a, d in zip(freqs, amps, decays):
        s += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t * d)
    return s * np.minimum(1, t / 0.0008)


def place(out, s, at):
    k = int(at * sr)
    if k >= len(out): return
    L = min(len(s), len(out) - k); out[k:k + L] += s[:L]


def norm(x, peak):
    m = np.max(np.abs(x)); return x / m * peak if m > 0 else x


def fade(x, a=0.002, b=0.02):
    x = x.copy(); na, nb = int(a * sr), int(b * sr)
    if na: x[:na] *= np.linspace(0, 1, na)
    if nb: x[-nb:] *= np.linspace(1, 0, nb)
    return x


# ---- 歯1枚のカチッ ----
def click(kind):
    if kind == 'A':   # ばね爪のラチェット：明るく短い「チッ」
        n = rng.standard_normal(int(0.03 * sr)); t = T(0.03)
        s = bp(n, 2500, 7000) * np.exp(-t * 260) * 0.9
        s2 = np.zeros(int(0.09 * sr)); place(s2, s, 0); place(s2, ring([3150, 4870, 6620], [0.5, 0.35, 0.2], [70, 95, 130], 0.09), 0)
        return fade(norm(s2, 0.8))
    if kind == 'B':   # 重い歯車の「ガチッ」：低めの金属＋小さなドン
        t = T(0.14); out = np.zeros(len(t))
        n = rng.standard_normal(int(0.04 * sr)); tn = T(0.04)
        place(out, bp(n, 700, 3200) * np.exp(-tn * 120), 0)
        place(out, ring([930, 1490, 2310, 3170], [0.6, 0.45, 0.3, 0.15], [32, 40, 55, 70], 0.14), 0.001)
        f = 150 * (1 + 0.5 * np.exp(-t * 60)); place(out, np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t * 45) * 0.7, 0)
        return fade(norm(out, 0.85))
    # C：2回鳴る「チキッ」（爪が落ちて跳ねる）
    a = click('A') * 0.75; out = np.zeros(int(0.12 * sr)); place(out, a, 0); place(out, a * 0.45, 0.022)
    return fade(norm(out, 0.8))


# ---- 跳ね橋が下りきった ----
def land(kind):
    if kind == 'A':   # 鉄の「ドスン」＋鳴り
        t = T(1.2); out = np.zeros(len(t))
        f = 45 + 70 * np.exp(-t * 14); place(out, np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t * 6) * 1.0, 0)
        n = rng.standard_normal(len(t)); place(out, lp(n, 900) * np.exp(-t * 22) * 0.8, 0)
        place(out, ring([412, 687, 1129, 1733], [0.35, 0.28, 0.18, 0.1], [4.5, 6, 8, 11], 1.2), 0.005)
        return fade(norm(out, 0.9), 0.001, 0.15)
    if kind == 'B':   # やわらかい「ゴトン」（2段）
        t = T(0.9); out = np.zeros(len(t))
        for at, g in ((0, 1.0), (0.09, 0.55)):
            tt = T(0.5); f = 70 + 60 * np.exp(-tt * 18)
            place(out, np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-tt * 10) * g, at)
            place(out, lp(rng.standard_normal(len(tt)), 600) * np.exp(-tt * 30) * 0.6 * g, at)
        place(out, ring([520, 830], [0.15, 0.1], [9, 12], 0.9), 0.0)
        return fade(norm(out, 0.85), 0.001, 0.1)
    # C：「ガシャン」明るい鳴りが長め
    t = T(1.4); out = np.zeros(len(t))
    f = 60 + 90 * np.exp(-t * 20); place(out, np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t * 9) * 0.8, 0)
    n = rng.standard_normal(len(t)); place(out, bp(n, 1500, 8000) * np.exp(-t * 14) * 0.5, 0)
    place(out, ring([640, 1013, 1580, 2290, 3170], [0.3, 0.3, 0.25, 0.18, 0.1], [3, 4, 5.5, 7, 9], 1.4), 0.003)
    return fade(norm(out, 0.85), 0.001, 0.2)


# ---- 伸びる橋が動いている間（ループ 2 秒、つなぎ目なし） ----
def rumble(kind, dur=2.0):
    N = int(dur * sr); t = np.arange(N) / sr; out = np.zeros(N)
    if kind == 'A':   # 歯車のゴロゴロ：歯の当たり 22回/秒＋低いうなり
        rate = 22; k = int(sr / rate); n_imp = N // k
        for i in range(n_imp):
            tn = T(0.03); g = 0.6 + 0.4 * rng.random()
            place(out, bp(rng.standard_normal(len(tn)), 300, 1800) * np.exp(-tn * 90) * g, i * k / sr)
        out += np.sin(2 * np.pi * 66 * t) * 0.25 * (0.8 + 0.2 * np.sin(2 * np.pi * rate * t))
        out += lp(rng.standard_normal(N), 250) * 0.6
    elif kind == 'B':   # なめらかなゴーッ（低い音だけ）
        out += lp(rng.standard_normal(N), 180, 3) * 1.4
        out += np.sin(2 * np.pi * 52 * t) * 0.3 + np.sin(2 * np.pi * 104 * t) * 0.1
        out *= 0.85 + 0.15 * np.sin(2 * np.pi * 3 * t)
    else:   # C：カタカタ（ラックの歯がはっきり 14回/秒）
        rate = 14; k = int(sr / rate)
        for i in range(N // k): place(out, click('B') * (0.5 + 0.2 * rng.random()), i * k / sr)
        out += lp(rng.standard_normal(N), 220) * 0.4
    # ループのつなぎ目：はしを重ねて混ぜる
    x = int(0.08 * sr); w = np.linspace(0, 1, x)
    head = out[:x].copy(); out[:x] = head * w + out[-x:] * (1 - w); out = out[:-x]
    return norm(out, 0.6)


# ---- 伸びる橋がつながった ----
def meet(kind):
    if kind == 'A':   # 「ガチャン」止め金がかかる
        out = np.zeros(int(0.8 * sr)); place(out, click('B'), 0); place(out, click('B') * 0.8, 0.07)
        place(out, ring([780, 1240, 1990], [0.3, 0.2, 0.12], [7, 9, 12], 0.7), 0.07)
        return fade(norm(out, 0.85), 0.001, 0.1)
    if kind == 'B':   # 低い「ゴン」
        t = T(0.8); out = np.zeros(len(t)); f = 80 + 40 * np.exp(-t * 20)
        place(out, np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t * 8), 0)
        place(out, ring([330, 560, 910], [0.3, 0.2, 0.1], [6, 8, 11], 0.8), 0)
        return fade(norm(out, 0.85), 0.001, 0.1)
    out = np.zeros(int(1.0 * sr)); place(out, land('C') * 0.7, 0); place(out, click('A') * 0.6, 0.12)   # C：明るい「カシャーン」
    return fade(norm(out, 0.85), 0.001, 0.15)


def write(path, x):
    x = np.clip(x, -1, 1); d = (x * 32767).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes(d.tobytes())


def demo_drawbridge(k):
    # 風車を撃って回す：カチカチが速くなって一定 → 下りきってドスン
    out = np.zeros(int(4.6 * sr)); tt = 0.0; c = click(k)
    while tt < 3.0:
        rate = 3 + 11 * min(1, tt / 1.0); place(out, c * 0.7, tt); tt += 1 / rate
    place(out, land(k), 3.05)
    return out


def demo_rack(k):
    out = np.zeros(int(4.2 * sr)); r = rumble(k); seg = np.concatenate([r, r])[:int(3.0 * sr)]
    env = np.minimum(1, np.arange(len(seg)) / (0.3 * sr)) * np.minimum(1, (len(seg) - np.arange(len(seg))) / (0.05 * sr))
    place(out, seg * env, 0); place(out, meet(k), 3.0)
    return out


def demo_turn(k):
    # 回転床：ゆっくり重いカチ…カチ（ピッチ低め）、止まる
    out = np.zeros(int(3.2 * sr)); c = click(k); tt = 0.0; i = 0
    while tt < 2.8:
        rate = 6 - 4 * max(0, (tt - 1.6) / 1.2); place(out, c * 0.75, tt); tt += 1 / max(1.5, rate); i += 1
    return out


# ===== 2026-11-10 てじゃ「ちょい耳障りかもなぁ」→ やわらかい版 D/E/F：高い音を落として（ローパス）、立ち上がりもやわらかく、こもった「コト」「ゴトン」系 =====
def soft(x, f=1800):
    return lp(x, f, 3)


def click_soft(kind):
    if kind == 'D':   # 木づちのような「コト」
        t = T(0.12); out = np.zeros(len(t))
        place(out, ring([420, 690, 1050], [0.7, 0.35, 0.15], [45, 60, 80], 0.12), 0)
        n = rng.standard_normal(int(0.02 * sr)); place(out, lp(n, 1200) * np.exp(-T(0.02) * 150) * 0.5, 0)
        out *= np.minimum(1, t / 0.003)
        return fade(norm(soft(out), 0.6))
    if kind == 'E':   # 低い「トッ」（ほとんど胴の音だけ）
        t = T(0.1); f = 180 + 80 * np.exp(-t * 50)
        out = np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t * 40) * np.minimum(1, t / 0.004)
        out += lp(rng.standard_normal(len(t)), 700) * np.exp(-t * 90) * 0.3
        return fade(norm(soft(out, 1200), 0.55))
    # F：やわらかいラチェット「ころっ」（2つの小さいコト）
    a = click_soft('D') * 0.8; out = np.zeros(int(0.14 * sr)); place(out, a, 0); place(out, a * 0.4, 0.03)
    return fade(norm(out, 0.55))


def land_soft(kind):
    t = T(1.0); out = np.zeros(len(t))
    if kind == 'D':   # 低い「ドゥン」
        f = 48 + 40 * np.exp(-t * 10); out += np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t * 5)
        out += lp(rng.standard_normal(len(t)), 300) * np.exp(-t * 18) * 0.5
    elif kind == 'E':   # 「ゴトン」2段、低め
        for at, g in ((0, 1.0), (0.1, 0.45)):
            tt = T(0.6); f = 60 + 40 * np.exp(-tt * 16)
            place(out, np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-tt * 9) * g, at)
            place(out, lp(rng.standard_normal(len(tt)), 400) * np.exp(-tt * 25) * 0.4 * g, at)
    else:   # F：「ドン」＋ほんの少しだけ鳴り（低い）
        f = 55 + 50 * np.exp(-t * 14); out += np.sin(2 * np.pi * np.cumsum(f) / sr) * np.exp(-t * 7)
        out += ring([260, 410], [0.12, 0.06], [5, 7], 1.0)
    out *= np.minimum(1, t / 0.004)
    return fade(norm(soft(out, 1000), 0.7), 0.001, 0.15)


def rumble_soft(kind, dur=2.0):
    N = int(dur * sr); t = np.arange(N) / sr; out = np.zeros(N)
    if kind == 'D':   # 低いゴーッ（こもった）
        out += lp(rng.standard_normal(N), 140, 3) * 1.4 + np.sin(2 * np.pi * 48 * t) * 0.25
    elif kind == 'E':   # ゆっくりのゴロゴロ（歯の当たりは 10回/秒、低い）
        rate = 10; k = int(sr / rate)
        for i in range(N // k): tn = T(0.05); place(out, lp(rng.standard_normal(len(tn)), 500) * np.exp(-tn * 60) * (0.6 + 0.3 * rng.random()), i * k / sr)
        out += lp(rng.standard_normal(N), 160, 3) * 0.8
    else:   # F：やわらかいコトコト（click_soft D を 9回/秒）
        rate = 9; k = int(sr / rate)
        for i in range(N // k): place(out, click_soft('D') * (0.5 + 0.2 * rng.random()), i * k / sr)
        out += lp(rng.standard_normal(N), 150, 3) * 0.5
    x = int(0.08 * sr); w = np.linspace(0, 1, x)
    head = out[:x].copy(); out[:x] = head * w + out[-x:] * (1 - w); out = out[:-x]
    return norm(soft(out, 900), 0.45)


def meet_soft(kind):
    if kind == 'D':   # 「コトン」
        out = np.zeros(int(0.7 * sr)); place(out, click_soft('D'), 0); place(out, land_soft('E') * 0.6, 0.05); return fade(norm(out, 0.65), 0.001, 0.1)
    if kind == 'E':   # 低い「ゴン」（やわらかめ）
        return land_soft('D') * 0.9
    out = np.zeros(int(0.8 * sr)); place(out, click_soft('E'), 0); place(out, click_soft('E') * 0.7, 0.08); return fade(norm(out, 0.6), 0.001, 0.1)


def demo_drawbridge_soft(k):
    out = np.zeros(int(4.6 * sr)); tt = 0.0; c = click_soft(k)
    while tt < 3.0:
        rate = 3 + 9 * min(1, tt / 1.0); place(out, c * 0.7, tt); tt += 1 / rate
    place(out, land_soft(k), 3.05); return out


def demo_rack_soft(k):
    out = np.zeros(int(4.2 * sr)); r = rumble_soft(k); seg = np.concatenate([r, r])[:int(3.0 * sr)]
    env = np.minimum(1, np.arange(len(seg)) / (0.3 * sr)) * np.minimum(1, (len(seg) - np.arange(len(seg))) / (0.05 * sr))
    place(out, seg * env, 0); place(out, meet_soft(k), 3.0); return out


def demo_turn_soft(k):
    out = np.zeros(int(3.2 * sr)); c = click_soft(k); tt = 0.0
    while tt < 2.8:
        rate = 5 - 3 * max(0, (tt - 1.6) / 1.2); place(out, c * 0.75, tt); tt += 1 / max(1.5, rate)
    return out


def main_soft(od):
    for k in 'DEF':
        write(os.path.join(od, 'click_%s.wav' % k), click_soft(k)); write(os.path.join(od, 'land_%s.wav' % k), land_soft(k))
        write(os.path.join(od, 'rumble_%s.wav' % k), rumble_soft(k)); write(os.path.join(od, 'meet_%s.wav' % k), meet_soft(k))
    gap = np.zeros(int(1.0 * sr))
    for name, fn in (('drawbridge', demo_drawbridge_soft), ('rackbridge', demo_rack_soft), ('turntable', demo_turn_soft)):
        seq = np.concatenate([np.concatenate([fn(k), gap]) for k in 'DEF'])
        write(os.path.join(od, 'demo_soft_%s.wav' % name), norm(seq, 0.6))


if __name__ == '__main__':
    od = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(od, exist_ok=True)
    for k in 'ABC':
        write(os.path.join(od, 'click_%s.wav' % k), click(k))
        write(os.path.join(od, 'land_%s.wav' % k), land(k))
        write(os.path.join(od, 'rumble_%s.wav' % k), rumble(k))
        write(os.path.join(od, 'meet_%s.wav' % k), meet(k))
    gap = np.zeros(int(1.0 * sr))
    for name, fn in (('drawbridge', demo_drawbridge), ('rackbridge', demo_rack), ('turntable', demo_turn)):
        seq = np.concatenate([np.concatenate([fn(k), gap]) for k in 'ABC'])
        write(os.path.join(od, 'demo_%s.wav' % name), norm(seq, 0.85))
    main_soft(od)
    print('ok', od)
