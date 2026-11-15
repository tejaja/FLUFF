# 2026-11-15 2–5 上の段：バリア＋クリスタルの衝撃波の音（1回目の案）
#   python make_sfx_shockwave.py <出力フォルダ>
#   sfx_shield_on.wav     バリアがつく（ガラスっぽい和音がふわっと上がって、きらっ）0.7s
#   sfx_shield_off.wav    バリアが消える（和音が下がってほどける）0.55s
#   sfx_crystal_charge.wav クリスタルがためる（低いうなりが上がっていく＋だんだん速くなる結晶のチリチリ）1.3s ＝ chargeTime
#   sfx_shockwave_boom.wav ドン（低いドゥン＋空気が押しのけられる音＋クリスタルの余韻）1.4s
#   sfx_shield_block.wav  バリアが受けとめる（キィン＋波うつ）0.5s
#   sfx_blown.wav         吹き飛ばされた（ぶわっ）0.6s
# 風のギミックとまぎれないように、ノイズは短く・低めに
import numpy as np, wave, sys, os
sr = 44100
rng = np.random.default_rng(11)

def write(path, x, peak=0.85):
    x = x / (np.max(np.abs(x)) + 1e-9) * peak
    fade = int(0.01 * sr); x[-fade:] *= np.linspace(1, 0, fade)
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((x * 32767).astype(np.int16).tobytes())

def T(sec): return np.arange(int(sr * sec)) / sr

def ph(f):  # 周波数の列 → 位相
    return 2 * np.pi * np.cumsum(f) / sr

def lp(x, a):  # 1次のローパス（a は 0〜1、配列も可）
    y = np.zeros_like(x); s = 0.0
    a = np.broadcast_to(a, x.shape)
    for i in range(len(x)):
        s += a[i] * (x[i] - s); y[i] = s
    return y

def bell(f, t, decay, inh=(1, 2.76, 5.4), amps=(1, 0.4, 0.15)):
    out = np.zeros_like(t)
    for h, a in zip(inh, amps): out += a * np.sin(2 * np.pi * f * h * t) * np.exp(-t * decay * h ** 0.5)
    return out

def shield_on():
    t = T(0.7); x = t / 0.7
    out = np.zeros_like(t)
    # E メジャーの和音が少し上がりながらふくらむ（ガラス＝わずかにずらした2本）
    for i, f in enumerate([329.63, 415.30, 493.88, 659.25]):
        start = i * 0.05
        env = np.clip((t - start) / 0.12, 0, 1) ** 1.5 * np.exp(-np.clip(t - start, 0, None) * 3.2)
        bend = 1 + 0.06 * (1 - np.exp(-t * 8))
        out += (np.sin(ph(f * bend * np.ones_like(t))) + 0.6 * np.sin(ph(f * bend * 1.003 * np.ones_like(t)))) * env
    # きらっ（最後の方の高いベル）
    k = int(0.18 * sr); tb = t[:len(t) - k]
    out[k:] += 0.6 * bell(1975.5, tb, 9) + 0.35 * bell(2637.0, tb, 11)
    return out

def shield_off():
    t = T(0.55)
    out = np.zeros_like(t)
    for i, f in enumerate([659.25, 493.88, 415.30, 329.63]):
        start = i * 0.04
        env = np.clip((t - start) / 0.03, 0, 1) * np.exp(-np.clip(t - start, 0, None) * 6)
        bend = 1 - 0.12 * (t / 0.55)
        out += (np.sin(ph(f * bend)) + 0.5 * np.sin(ph(f * bend * 1.004))) * env
    return out

def crystal_charge(sec=1.3):
    t = T(sec); x = t / sec
    # うなり：低い音＋オクターブ上が、だんだん上がって明るく
    f0 = 98 * 2 ** (x ** 1.4 * 1.0)
    hum = np.sin(ph(f0)) + 0.5 * np.sin(ph(f0 * 2.003)) * x + 0.3 * np.sin(ph(f0 * 3.01)) * x ** 2
    trem = 0.75 + 0.25 * np.sin(ph(4 + 18 * x ** 2))
    out = hum * trem * (0.2 + 0.8 * x ** 1.2)
    # チリチリ：結晶の小さい粒が、だんだん速く・高く
    at = 0.03
    while at < sec - 0.02:
        p = at / sec
        f = 1400 + 1800 * p + rng.uniform(-150, 150)
        L = int(0.07 * sr); tt = np.arange(L) / sr
        s = np.sin(2 * np.pi * f * tt) * np.exp(-tt * 70) * (0.25 + 0.5 * p)
        k = int(at * sr); Lk = min(L, len(t) - k); out[k:k + Lk] += s[:Lk]
        at += 0.12 * (1 - p) ** 1.5 + 0.018
    # 最後の0.1秒だけ吸いこむ
    suck = np.clip((x - 0.85) / 0.15, 0, 1)
    out *= 1 - 0.5 * suck ** 2
    out *= np.minimum(1, t / 0.05)
    return out

def boom():
    t = T(1.4)
    # ドゥン：低い音が下がる
    f = 75 * np.exp(-t * 2.5) + 38
    thump = np.sin(ph(f)) * np.exp(-t * 3.2) * np.minimum(1, t / 0.004)
    # 押しのけられる空気：ローパスを閉じていくノイズ（短め）
    n = rng.standard_normal(len(t))
    a = 0.25 * np.exp(-t * 5) + 0.01
    air = lp(n, a) * np.exp(-t * 4.5) * 2.2
    # クリスタルの余韻（低めのベル）
    ring = 0.35 * bell(523.25, t, 3) + 0.25 * bell(783.99, t, 3.5) + 0.15 * bell(1046.5, t, 4)
    return thump * 1.3 + air + ring * np.minimum(1, t / 0.01)

def block():
    t = T(0.5)
    # キィン（少しずれた2本で波うつ）＋はじめのカチッ
    out = np.sin(2 * np.pi * 1318.5 * t) + np.sin(2 * np.pi * 1326 * t) + 0.5 * np.sin(2 * np.pi * 1975.5 * t) * np.exp(-t * 10)
    out *= np.exp(-t * 7) * np.minimum(1, t / 0.002)
    n = rng.standard_normal(len(t)); click = lp(n, 0.5) * np.exp(-t * 120) * 1.2
    low = np.sin(2 * np.pi * 220 * t * (1 - 0.3 * t)) * np.exp(-t * 14) * 0.6
    return out * 0.6 + click + low

def blown():
    t = T(0.6)
    n = rng.standard_normal(len(t))
    a = 0.05 + 0.3 * np.exp(-t * 6)
    w = lp(n, a) * np.minimum(1, t / 0.02) * np.exp(-t * 4) * 2
    f = 180 * np.exp(-t * 3) + 60
    thud = np.sin(ph(f)) * np.exp(-t * 9) * 0.8
    return w + thud

def shatter():
    # パリン：はじめのカシャッ（明るいノイズ）＋ばらばらの高いガラスの粒がこぼれる
    t = T(0.75)
    n = rng.standard_normal(len(t))
    hp = n - lp(n, 0.35)
    crack = hp * np.exp(-t * 38) * 1.4
    out = crack.copy()
    at = 0.0
    while at < 0.45:
        f = rng.uniform(2200, 5200)
        L = int(0.12 * sr); tt = np.arange(L) / sr
        s2 = (np.sin(2 * np.pi * f * tt) + 0.5 * np.sin(2 * np.pi * f * 1.47 * tt)) * np.exp(-tt * rng.uniform(35, 70))
        k = int(at * sr); Lk = min(L, len(t) - k)
        out[k:k + Lk] += s2[:Lk] * 0.55 * np.exp(-at * 4)
        at += rng.uniform(0.008, 0.045) * (1 + at * 4)
    ring = (np.sin(2 * np.pi * 1760 * t) + np.sin(2 * np.pi * 1771 * t)) * np.exp(-t * 9) * 0.35
    return out + ring

def kakin():
    # カキン：硬いカッ＋金属っぽい高い響き（非整数倍音）が短く
    t = T(0.42)
    n = rng.standard_normal(len(t))
    tick = (n - lp(n, 0.4)) * np.exp(-t * 160) * 0.9
    f = 2093.0
    ring = np.zeros_like(t)
    for h, a, d in [(1, 1.0, 11), (2.41, 0.5, 15), (3.87, 0.3, 20), (5.3, 0.15, 26)]:
        ring += a * np.sin(2 * np.pi * f * h * t + h) * np.exp(-t * d)
    body = np.sin(2 * np.pi * 523.25 * t) * np.exp(-t * 18) * 0.35
    return tick + ring * 0.55 * np.minimum(1, t / 0.0015) + body

if __name__ == '__main__':
    od = sys.argv[1] if len(sys.argv) > 1 else '.'
    write(os.path.join(od, 'sfx_shield_on.wav'), shield_on(), 0.7)
    write(os.path.join(od, 'sfx_shield_off.wav'), shield_off(), 0.6)
    write(os.path.join(od, 'sfx_crystal_charge.wav'), crystal_charge(), 0.75)
    write(os.path.join(od, 'sfx_shockwave_boom.wav'), boom(), 0.9)
    write(os.path.join(od, 'sfx_shield_block.wav'), block(), 0.7)
    write(os.path.join(od, 'sfx_blown.wav'), blown(), 0.8)
    write(os.path.join(od, 'sfx_shield_shatter.wav'), shatter(), 0.75)
    write(os.path.join(od, 'sfx_shield_bounce.wav'), kakin(), 0.7)
    print('ok')
