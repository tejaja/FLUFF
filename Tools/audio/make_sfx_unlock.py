# 2026-11-19 てじゃ「あとは音かな～」：あそびばのおもちゃが出る時の音の候補（自前合成）
#  P1〜P5：ドームがパカッと開く音 ／ Y1〜Y5：おもちゃがポヨンと出る音
#  使い方：python make_sfx_unlock.py <出力フォルダ>
import sys, os
import numpy as np
from scipy.signal import butter, sosfilt
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(7)

def t_of(sec): return np.arange(int(SR * sec)) / SR

def env_exp(t, tau): return np.exp(-t / tau)

def bp(x, lo, hi, order=2):
    sos = butter(order, [lo, hi], btype="band", fs=SR, output="sos"); return sosfilt(sos, x)

def lp(x, f, order=2):
    sos = butter(order, f, btype="low", fs=SR, output="sos"); return sosfilt(sos, x)

def hp(x, f, order=2):
    sos = butter(order, f, btype="high", fs=SR, output="sos"); return sosfilt(sos, x)

def sweep(t, f0, f1, tau):
    """f0 から f1 へ指数的に近づく音程の位相"""
    f = f1 + (f0 - f1) * np.exp(-t / tau)
    return 2 * np.pi * np.cumsum(f) / SR

def place(total, sig, at):
    out = np.zeros(int(SR * total)); i = int(SR * at); n = min(len(sig), len(out) - i); out[i:i + n] += sig[:n]; return out

def fade(x, ms_in=3, ms_out=30):
    a = int(SR * ms_in / 1000); b = int(SR * ms_out / 1000)
    x = x.copy(); x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b); return x

def norm_mean(x, target_db=-21.0):
    """有音部分の平均音量（RMS）を target_db に、ピークは -1dB まで"""
    a = np.abs(x); th = a.max() * 0.05; act = x[a > th]
    rms = np.sqrt(np.mean(act ** 2)) if len(act) else 1e-6
    g = 10 ** (target_db / 20) / rms
    y = x * g; pk = np.max(np.abs(y))
    if pk > 0.89: y *= 0.89 / pk
    return y

def write(name, x, outdir):
    y = norm_mean(fade(x)); wavfile.write(os.path.join(outdir, name + ".wav"), SR, (y * 32767).astype(np.int16))

def knock(t, f, tau, noise=0.3, bw=(600, 3000)):
    """木をたたいたような短い音：減衰する正弦＋少しの雑音"""
    s = np.sin(2 * np.pi * f * t) * env_exp(t, tau) + 0.5 * np.sin(2 * np.pi * f * 2.76 * t) * env_exp(t, tau * 0.4)
    n = bp(rng.standard_normal(len(t)), *bw) * env_exp(t, 0.004) * noise
    return s + n

# ---------------- パカッ（ドームが開く） ----------------
def P1():   # 木の「カ・ポッ」：高い木の音 → 低い木の音
    t = t_of(0.35)
    a = knock(t, 1250, 0.025, 0.4); b = knock(t, 760, 0.05, 0.25)
    return place(0.45, a, 0.0) + place(0.45, b * 0.9, 0.07)

def P2():   # 中が空洞のプラの「ポン」：音程が少し上がる
    t = t_of(0.4)
    ph = sweep(t, 260, 520, 0.03)
    s = np.sin(ph) * env_exp(t, 0.07) + 0.3 * np.sin(2 * ph) * env_exp(t, 0.03)
    n = bp(rng.standard_normal(len(t)), 800, 4000) * env_exp(t, 0.006) * 0.5
    return lp(s + n, 5000)

def P3():   # コルクの「ポッ」＋空気の抜ける音
    t = t_of(0.45)
    ph = sweep(t, 1400, 420, 0.012)
    s = np.sin(ph) * env_exp(t, 0.03)
    air = bp(rng.standard_normal(len(t)), 1500, 6000) * (np.clip(t / 0.02, 0, 1) * env_exp(t, 0.09)) * 0.25
    return lp(s + air, 6000)

def P4():   # ふたが開く「カポ」＋ゆっくり振れる空気（シュワ）
    t = t_of(0.7)
    k = knock(t, 900, 0.03, 0.35)
    wh = rng.standard_normal(len(t))
    # 帯域を上げていくシュワ
    sw = np.zeros_like(wh); seg = len(t) // 8
    for i in range(8):
        lo = 500 + i * 250; c = bp(wh, lo, lo * 2.2)[i * seg:(i + 1) * seg]; sw[i * seg:(i + 1) * seg] = c
    sw *= np.sin(np.pi * np.clip(t / 0.55, 0, 1)) * 0.18
    return k + lp(sw, 4500)

def P5():   # マリンバの低い2音「パ・カッ」（木の鍵盤）
    def mar(f, d):
        t = t_of(d); return (np.sin(2 * np.pi * f * t) + 0.25 * np.sin(2 * np.pi * f * 4 * t) * env_exp(t, 0.02)) * env_exp(t, 0.12)
    return place(0.6, mar(392, 0.4), 0.0) + place(0.6, mar(523.25, 0.45) * 0.9, 0.085)

# ---------------- ポヨン（おもちゃが出る） ----------------
def Y1():   # ゼリーの「ぽよん」：低めの音がゆれながら上がる
    t = t_of(0.7)
    f = 170 + 120 * (1 - np.exp(-t / 0.08)) + 22 * np.sin(2 * np.pi * 9 * t) * env_exp(t, 0.18)
    ph = 2 * np.pi * np.cumsum(f) / SR
    s = (np.sin(ph) + 0.35 * np.sin(2 * ph)) * np.clip(t / 0.01, 0, 1) * env_exp(t, 0.22)
    return lp(s, 2500)

def Y2():   # ゴムの「ぽよよん」：音量がぷるぷるゆれる三角波
    t = t_of(0.8)
    ph = sweep(t, 220, 330, 0.06)
    tri = 2 / np.pi * np.arcsin(np.sin(ph))
    am = 0.65 + 0.35 * np.cos(2 * np.pi * 11 * t) * env_exp(t, 0.25)
    return lp(tri * am * env_exp(t, 0.28) * np.clip(t / 0.008, 0, 1), 1800)

def Y3():   # 泡の「ぽむっ」：低いポン＋小さい泡2つ
    t = t_of(0.3)
    def pop(f0, f1, tau, d):
        tt = t_of(d); return np.sin(sweep(tt, f0, f1, 0.015)) * env_exp(tt, tau)
    return place(0.6, pop(180, 360, 0.08, 0.3), 0.0) + place(0.6, pop(500, 900, 0.03, 0.15) * 0.45, 0.14) + place(0.6, pop(650, 1100, 0.025, 0.12) * 0.35, 0.22)

def Y4():   # スライムの「ぷよん」：口の形が「お→う」に変わる感じ
    t = t_of(0.6)
    f0 = 150 + 70 * (1 - np.exp(-t / 0.05))
    ph = 2 * np.pi * np.cumsum(f0) / SR
    buzz = sum(np.sin(k * ph) / k for k in range(1, 18))
    form1 = 500 - 200 * np.clip(t / 0.3, 0, 1)
    # 2つの帯域で声っぽく（区間ごとに帯域を動かす）
    out = np.zeros_like(buzz); n = 12; seg = len(t) // n
    for i in range(n):
        f1 = form1[min(len(t) - 1, i * seg)]
        c = bp(buzz, f1 * 0.7, f1 * 1.3) + 0.4 * bp(buzz, 900, 1300)
        out[i * seg:(i + 1) * seg] = c[i * seg:(i + 1) * seg]
    return out * np.clip(t / 0.01, 0, 1) * env_exp(t, 0.18)

def Y5():   # 木のばねの「ぼよーん」：はじいた弦の音程が下がって戻る
    t = t_of(0.9)
    f = 110 * (1 + 0.25 * np.sin(2 * np.pi * 6 * t) * env_exp(t, 0.3))
    ph = 2 * np.pi * np.cumsum(f) / SR
    s = sum(np.sin(k * ph) * env_exp(t, 0.35 / k) / k for k in range(1, 8))
    return lp(s * np.clip(t / 0.004, 0, 1), 2200)

# 2026-11-19 てじゃ「もっとボワワンってかんじがいいな」
def W1():   # 低いブザーをワウ（帯域がゆれる）に通す「ぼわわわん」：ゆれがだんだんゆっくりに
    t = t_of(1.0)
    f0 = 120 + 40 * (1 - np.exp(-t / 0.1))
    ph = 2 * np.pi * np.cumsum(f0) / SR
    buzz = sum(np.sin(k * ph) / k for k in range(1, 20))
    lfo_ph = 2 * np.pi * np.cumsum(9 - 5 * np.clip(t / 0.8, 0, 1)) / SR
    center = 450 + 300 * (0.5 + 0.5 * np.sin(lfo_ph)) * env_exp(t, 0.6)
    out = np.zeros_like(buzz); n = 60; seg = len(t) // n
    for i in range(n):
        c = center[min(len(t) - 1, i * seg + seg // 2)]
        y = bp(buzz, c * 0.6, c * 1.5)
        out[i * seg:(i + 1) * seg] = y[i * seg:(i + 1) * seg]
    return lp(out, 2500) * np.clip(t / 0.015, 0, 1) * env_exp(t, 0.35)

def W2():   # まるい音の「ぼわ〜ん」：音程と大きさがいっしょにゆれる
    t = t_of(1.0)
    wob = np.sin(2 * np.pi * np.cumsum(8 - 4 * np.clip(t / 0.9, 0, 1)) / SR) * env_exp(t, 0.4)
    f = 150 + 60 * (1 - np.exp(-t / 0.12)) + 18 * wob
    ph = 2 * np.pi * np.cumsum(f) / SR
    s = (np.sin(ph) + 0.4 * np.sin(2 * ph) + 0.15 * np.sin(3 * ph)) * (0.75 + 0.25 * wob)
    return lp(s * np.clip(t / 0.02, 0, 1) * env_exp(t, 0.38), 2000)

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    for name, fn in [("P1_wood_kapo", P1), ("P2_hollow_pon", P2), ("P3_cork_pop", P3), ("P4_lid_whoosh", P4), ("P5_marimba_paka", P5),
                     ("Y1_jelly_poyon", Y1), ("Y2_rubber_poyoyon", Y2), ("Y3_bubble_pomu", Y3), ("Y4_slime_puyon", Y4), ("Y5_spring_boyon", Y5), ("W1_wah_bowawan", W1), ("W2_round_bowan", W2)]:
        write(name, fn(), out); print(name)
