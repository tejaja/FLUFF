# 2–2「ほのおのやま」の効果音の候補（2026-11-01 てじゃ「SEをふやそう」「全部作って順番にみていこう」）。
#   python make_fire_sfx.py <出力フォルダ>   → <音>_<番号>.wav と、候補を順番に並べた menu_<音>.wav
# 音は聴けないので、てじゃに番号で選んでもらう。ループの音は頭としっぽがつながるように作る
import os, sys, wave
import numpy as np

SR = 44100


def save(y, path, peak=0.85):
    y = y / (np.max(np.abs(y)) + 1e-9) * peak
    st = np.stack([y, y], 1)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes())


def tt(sec): return np.arange(int(sec * SR)) / SR


def band(n, lo, hi, seed, slope=0.0):
    """帯域をしぼったノイズ（FFT で切るので頭としっぽがつながる）。slope は高い音ほど弱くする量"""
    r = np.random.default_rng(seed)
    F = np.fft.rfft(r.standard_normal(n)); f = np.fft.rfftfreq(n, 1 / SR)
    m = np.clip((f - lo) / max(lo * 0.3, 1), 0, 1) * np.clip((hi - f) / max(hi * 0.3, 1), 0, 1)
    if slope > 0: m = m / (1 + (f / max(lo, 1)) ** slope)
    y = np.fft.irfft(F * m, n)
    return y / (np.max(np.abs(y)) + 1e-9)


def sweep_noise(n, f0, f1, seed, q=0.35):
    """中心の高さが動くノイズ（時間で変わる簡単なフィルタ）。f0, f1 は長さ n の配列でも数でも"""
    r = np.random.default_rng(seed); x = r.standard_normal(n)
    fc = np.linspace(f0, f1, n) if np.isscalar(f0) else f0
    y = np.zeros(n); lo = hi = 0.0
    a = 2 * np.sin(np.pi * np.clip(fc, 20, SR / 6) / SR)
    for i in range(n):
        hp = x[i] - lo - q * hi
        hi += a[i] * hp
        lo += a[i] * hi
        y[i] = hi
    return y / (np.max(np.abs(y)) + 1e-9)


def tone(f, t):
    f = np.broadcast_to(f, t.shape)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def att(t, a): return np.minimum(1, t / max(a, 1e-4))
def dec(t, d, at=0.0): return np.exp(-np.clip(t - at, 0, None) / d) * (t >= at)
def tail(y, sec):
    n = len(y); return y * np.minimum(1, (n - np.arange(n)) / (sec * SR))


def thump(t, f0, f1, d, at=0.0):
    tk = np.clip(t - at, 0, None)
    f = f1 + (f0 - f1) * np.exp(-tk / 0.04)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * dec(t, d, at) * att(tk, 0.002)


def pops(t, r, count, f=(1500, 5000), d=(0.002, 0.008), gain=(0.3, 1.0), period=None):
    """パチパチ。period を入れると、はみ出した分は頭に回す（ループ用）"""
    n = len(t); y = np.zeros(n)
    for _ in range(count):
        st = int(r.uniform(0, n)); L = int(0.03 * SR); fk = r.uniform(*f); dk = r.uniform(*d); g = r.uniform(*gain)
        tk = np.arange(L) / SR
        s = np.sin(2 * np.pi * fk * tk) * np.exp(-tk / dk) * g
        s += r.standard_normal(L) * np.exp(-tk / (dk * 0.7)) * g * 0.6
        idx = (st + np.arange(L)) % n if period else np.arange(st, min(n, st + L))
        y[idx] += s[:len(idx)]
    return y


# ---- 1. 火炎放射（1.2〜1.6 秒吹く）----
def roar_1():
    t = tt(1.5); e = att(t, 0.07) * np.minimum(1, (1.5 - t) / 0.35)
    return (band(len(t), 70, 900, 1, 1.2) * 0.9 + tone(58, t) * 0.25) * e
def roar_2():
    t = tt(1.5); e = att(t, 0.05) * np.minimum(1, (1.5 - t) / 0.3) * (0.75 + 0.25 * np.sin(2 * np.pi * 28 * t))
    return band(len(t), 250, 3200, 2, 0.8) * e
def roar_3():
    t = tt(1.5); e = np.minimum(1, (1.5 - t) / 0.4)
    return (thump(t, 170, 55, 0.22) * 1.0 + band(len(t), 900, 6000, 3, 0.6) * (0.5 * att(t, 0.04)) * dec(t, 0.8)) * e
def roar_4():
    t = tt(1.5); e = np.minimum(1, (1.5 - t) / 0.25) * (0.35 + 0.65 * np.abs(np.sin(np.pi * 11 * t)) ** 2)
    return band(len(t), 90, 1100, 4, 1.0) * e

# ---- 2. 火の玉（飛び上がる）----
def whoosh_1():
    t = tt(0.8); fc = 500 + 1800 * np.sin(np.pi * np.clip(t / 0.55, 0, 1))
    return sweep_noise(len(t), fc, None, 21) * att(t, 0.04) * dec(t, 0.22, 0.2) ** 0.8
def whoosh_2():
    t = tt(0.9); fc = 160 + 520 * np.sin(np.pi * np.clip(t / 0.7, 0, 1))
    return sweep_noise(len(t), fc, None, 22, 0.5) * att(t, 0.07) * np.minimum(1, (0.9 - t) / 0.4)
def whoosh_3():
    t = tt(0.8); fc = 400 + 1300 * np.clip(t / 0.3, 0, 1)
    return thump(t, 340, 110, 0.07) * 0.9 + sweep_noise(len(t), fc, None, 23) * 0.6 * att(t, 0.02) * dec(t, 0.2)

# ---- 3. ふわふわの炎上 ----
def burn_1():
    t = tt(0.6)
    return thump(t, 200, 80, 0.12) + band(len(t), 400, 2500, 31) * dec(t, 0.14) * 0.55
def burn_2():
    t = tt(0.7)
    return sweep_noise(len(t), 300 + 1400 * np.clip(t / 0.12, 0, 1), None, 32, 0.5) * att(t, 0.01) * dec(t, 0.18)
def burn_3():
    t = tt(0.8); r = np.random.default_rng(33)
    return thump(t, 150, 70, 0.09, 0.06) * 0.8 + pops(t, r, 7, (2000, 6000), (0.002, 0.006), (0.4, 1)) * dec(t, 0.3) * 0.7 + band(len(t), 300, 1800, 33) * att(t, 0.03) * dec(t, 0.3) * 0.4

# ---- 4. 焚き火で温まって浮く ----
def heat_1():
    t = tt(0.8)
    return tone(260 + 300 * (t / 0.8), t) * att(t, 0.1) * dec(t, 0.25, 0.25) * 0.8 + band(len(t), 400, 2400, 41, 0.8) * att(t, 0.15) * dec(t, 0.2, 0.2) * 0.3
def heat_2():
    t = tt(0.6)
    return (tone(420 + 180 * np.exp(-t / 0.06), t) + 0.3 * tone(840, t)) * att(t, 0.01) * dec(t, 0.14)
def heat_3():
    t = tt(0.9)
    return sweep_noise(len(t), 500 + 1800 * np.clip(t / 0.5, 0, 1), None, 43, 0.6) * att(t, 0.2) * np.minimum(1, (0.9 - t) / 0.4)

# ---- 5. 噴気孔 ----
def vent_1():
    t = tt(0.7)
    return thump(t, 130, 60, 0.1) * 0.8 + band(len(t), 1000, 7000, 51, 0.5) * att(t, 0.015) * dec(t, 0.2)
def vent_2():
    t = tt(1.3)
    return band(len(t), 1500, 8000, 52, 0.4) * att(t, 0.05) * np.minimum(1, (1.3 - t) / 0.8)
def vent_3():
    t = tt(0.6)
    return thump(t, 110, 48, 0.17) + band(len(t), 150, 1500, 53, 1) * dec(t, 0.12) * 0.7

# ---- 6. まものが水で消える ----
def steam_1():
    t = tt(0.6)
    return band(len(t), 2500, 9000, 61, 0.3) * att(t, 0.005) * dec(t, 0.14)
def steam_2():
    t = tt(1.4)
    return band(len(t), 1800, 8500, 62, 0.4) * att(t, 0.01) * dec(t, 0.5)
def steam_3():
    t = tt(1.1); r = np.random.default_rng(63)
    return band(len(t), 1500, 7000, 63, 0.5) * att(t, 0.01) * dec(t, 0.3) * 0.7 + pops(t, r, 14, (900, 2500), (0.006, 0.02), (0.3, 0.8)) * dec(t, 0.4) * 0.6

# ---- 7. 消えたまものが目を覚ます ----
def wake_1():
    t = tt(0.4)
    return tone(380 * 2 ** (t / 0.16), t) * att(t, 0.01) * dec(t, 0.08, 0.12)
def wake_2():
    t = tt(0.6); e = 0.4 + 0.6 * np.abs(np.sin(2 * np.pi * 16 * t))
    return (tone(190, t) + 0.5 * tone(380, t)) * e * att(t, 0.02) * np.minimum(1, (0.6 - t) / 0.18)
def wake_3():
    t = tt(0.5)
    return (tone(240 + 240 * np.clip(t / 0.25, 0, 1) ** 2, t) + 0.25 * tone(960, t)) * att(t, 0.04) * np.minimum(1, (0.5 - t) / 0.15)

# ---- 8. 火山の岩が飛び出す ----
def rocklaunch_1():
    t = tt(1.2)
    return thump(t, 110, 42, 0.32) + band(len(t), 70, 800, 81, 1) * att(t, 0.01) * dec(t, 0.4) * 0.6
def rocklaunch_2():
    t = tt(1.4)
    return thump(t, 90, 34, 0.45) * 0.9 + band(len(t), 40, 350, 82, 0.6) * att(t, 0.03) * dec(t, 0.55) * 0.5
def rocklaunch_3():
    t = tt(1.0)
    return thump(t, 180, 55, 0.18) + band(len(t), 300, 3000, 83, 0.8) * dec(t, 0.1) * 0.6 + band(len(t), 100, 600, 83, 1) * dec(t, 0.3) * 0.4

# ---- 9. 火山の岩がはねる ----
def rockbounce_1():
    t = tt(0.5)
    return thump(t, 150, 60, 0.09) + band(len(t), 300, 2200, 91, 0.6) * dec(t, 0.03) * 0.6
def rockbounce_2():
    t = tt(0.6)
    return thump(t, 95, 45, 0.15) + band(len(t), 90, 700, 92, 1) * dec(t, 0.07) * 0.5
def rockbounce_3():
    t = tt(0.5)
    return thump(t, 220, 90, 0.05) + (tone(620, t) + 0.6 * tone(1130, t)) * dec(t, 0.03) * 0.3 + band(len(t), 800, 4000, 93, 0.5) * dec(t, 0.02) * 0.5

# ---- 10〜12. 近づくと鳴るループ（4 秒。つながる）----
def wob(t, per, amp):
    return 1 + amp * sum(np.sin(2 * np.pi * k * t / 4.0 + per * k) / k for k in (2, 3, 5)) / 1.8
def looplava_1():
    t = tt(4); r = np.random.default_rng(101)
    return band(len(t), 40, 260, 101, 1) * wob(t, 0.6, 0.3) * 0.7 + pops(t, r, 16, (120, 320), (0.02, 0.05), (0.4, 1), period=True) * 0.9
def looplava_2():
    t = tt(4)
    return band(len(t), 30, 180, 102, 0.7) * wob(t, 1.3, 0.45)
def loopfire_1():
    t = tt(4); r = np.random.default_rng(111)
    return band(len(t), 200, 1400, 111, 1) * wob(t, 0.3, 0.3) * 0.25 + pops(t, r, 40, (1500, 6000), (0.002, 0.007), (0.2, 1), period=True) * 0.9
def loopfire_2():
    t = tt(4); r = np.random.default_rng(112)
    return band(len(t), 90, 800, 112, 1) * wob(t, 2.1, 0.4) * 0.7 + pops(t, r, 18, (1200, 4000), (0.003, 0.009), (0.2, 0.7), period=True) * 0.5
def loopwall_1():
    t = tt(4)
    return band(len(t), 60, 900, 121, 1.1) * wob(t, 0.9, 0.5)
def loopwall_2():
    t = tt(4)
    return band(len(t), 150, 2200, 122, 0.9) * wob(t, 1.7, 0.6)


# ---- ゲームで聞こえなかった物の直し（低音だけだとスピーカーと BGM に負ける。2026-11-01 てじゃ「岩、溶岩 きこえない」）----
def rocklaunch_2b():
    t = tt(1.4)
    y = thump(t, 90, 34, 0.45) * 0.5 + band(len(t), 40, 350, 82, 0.6) * att(t, 0.03) * dec(t, 0.55) * 0.35
    y += thump(t, 230, 120, 0.25) * 0.9 + band(len(t), 220, 1800, 84, 0.5) * att(t, 0.01) * dec(t, 0.4) * 1.1
    return np.tanh(y * 1.8)
def rockbounce_2b():
    t = tt(0.6)
    y = thump(t, 95, 45, 0.15) * 0.8 + band(len(t), 90, 700, 92, 1) * dec(t, 0.07) * 0.5
    y += thump(t, 240, 130, 0.06) * 0.7 + band(len(t), 400, 2400, 94, 0.6) * dec(t, 0.035) * 0.6
    return np.tanh(y * 1.8)
def looplava_2b():
    t = tt(4)
    y = band(len(t), 30, 180, 102, 0.7) * wob(t, 1.3, 0.45) * 0.8 + band(len(t), 140, 650, 103, 0.8) * wob(t, 2.4, 0.5) * 0.55
    return np.tanh(y * 1.5)


# ---- 2 回目：火の玉・炎上・浮く音・焚き火のループ（1 回目はピンと来なかったので、ゲームらしい音を増やす）----
def whoosh2_1():   # ボウッ：火が丸ごと飛ぶ太い音
    t = tt(0.8); fc = 180 + 700 * np.sin(np.pi * np.clip(t / 0.6, 0, 1))
    return np.tanh((sweep_noise(len(t), fc, None, 201, 0.35) * 1.2 + tone(90 + 90 * np.sin(np.pi * np.clip(t / 0.6, 0, 1)), t) * 0.5) * att(t, 0.03) * np.minimum(1, (0.8 - t) / 0.35) * 1.5)
def whoosh2_2():   # ぴゅーん：上がる音程（ゲームっぽい）
    t = tt(0.7); f = 260 * 2.6 ** np.clip(t / 0.45, 0, 1)
    return (tone(f, t) * 0.7 + 0.25 * tone(f * 2, t)) * att(t, 0.01) * np.minimum(1, (0.7 - t) / 0.3) + sweep_noise(len(t), f * 1.5, None, 202) * 0.3 * dec(t, 0.3)
def whoosh2_3():   # ぼよん：飛び出してはずむ（かわいい）
    t = tt(0.5); f = 170 + 330 * (1 - np.exp(-t / 0.05)) - 180 * np.clip((t - 0.12) / 0.3, 0, 1)
    return (tone(f, t) + 0.4 * tone(f * 2, t)) * att(t, 0.005) * dec(t, 0.14)
def whoosh2_4():   # ふぉわっ：炎が上がる（やわらかい風）
    t = tt(0.7); fc = 350 + 1100 * np.clip(t / 0.25, 0, 1) - 800 * np.clip((t - 0.25) / 0.45, 0, 1)
    return sweep_noise(len(t), fc, None, 204, 0.5) * att(t, 0.06) * np.minimum(1, (0.7 - t) / 0.4)

def burn2_1():     # ボワッ：一気に燃え上がる（太い）
    t = tt(0.8)
    return np.tanh((thump(t, 260, 100, 0.1) * 0.8 + band(len(t), 300, 3000, 211, 0.6) * att(t, 0.01) * dec(t, 0.28)) * 1.6)
def burn2_2():     # ぽっ：ちいさく火がつく（かわいい）
    t = tt(0.4); f = 500 + 500 * np.exp(-t / 0.02)
    return tone(f, t) * att(t, 0.002) * dec(t, 0.06) + band(len(t), 800, 5000, 212, 0.5) * dec(t, 0.03) * 0.4
def burn2_3():     # メラッ：上がる音程＋炎
    t = tt(0.7); f = 200 * 2.2 ** np.clip(t / 0.2, 0, 1)
    return tone(f, t) * att(t, 0.01) * dec(t, 0.15) * 0.8 + band(len(t), 500, 3500, 213, 0.5) * att(t, 0.04) * dec(t, 0.22) * 0.7
def burn2_4():     # 警告っぽい「ピピッ」＋ボッ（燃えたのに気づく）
    t = tt(0.6); e = dec(t, 0.04) + dec(t, 0.04, 0.09)
    return tone(1150, t) * e * 0.5 + thump(t, 220, 90, 0.1, 0.16) + band(len(t), 400, 2500, 214, 0.6) * dec(t, 0.12, 0.16) * 0.5

def heat2_1():     # ほわぁん：あったかい和音がふくらむ
    t = tt(1.0); e = att(t, 0.12) * np.minimum(1, (1.0 - t) / 0.55)
    return (tone(330, t) + 0.8 * tone(495, t) + 0.5 * tone(660, t)) * e
def heat2_2():     # ぷわん：下から持ち上がる
    t = tt(0.6); f = 240 + 360 * np.clip(t / 0.18, 0, 1) ** 0.6
    return (tone(f, t) + 0.3 * tone(f * 2, t)) * att(t, 0.02) * dec(t, 0.18, 0.1)
def heat2_3():     # しゅわん：泡がのぼる
    t = tt(0.8); r = np.random.default_rng(223); y = np.zeros(len(t))
    for k in range(7):
        st = 0.02 + k * 0.08 + r.uniform(0, 0.03); f = 420 + k * 110 + r.uniform(-30, 30)
        tk = np.clip(t - st, 0, None); y += np.sin(2 * np.pi * (f * tk + 0.5 * 500 * tk * tk)) * np.exp(-tk / 0.05) * (t > st) * 0.6
    return y
def heat2_4():     # ふぉーん：息を吹き込むような音（音程なし）
    t = tt(0.9)
    return sweep_noise(len(t), 300 + 500 * np.clip(t / 0.5, 0, 1), None, 224, 0.25) * att(t, 0.15) * np.minimum(1, (0.9 - t) / 0.45)

def loopfire2_1(): # ぼーっ：中くらいの炎（パチパチなし）
    t = tt(4)
    return band(len(t), 120, 1100, 231, 1.0) * wob(t, 0.7, 0.45)
def loopfire2_2(): # ごぉー：低めで静か
    t = tt(4)
    return band(len(t), 80, 650, 232, 1.0) * wob(t, 1.9, 0.35)
def loopfire2_3(): # ふぁさふぁさ：炎がゆれる（ゆらぎ大きめ）
    t = tt(4); e = 0.55 + 0.45 * (np.sin(2 * np.pi * 3 * t / 4.0 + 0.7) * 0.6 + np.sin(2 * np.pi * 5 * t / 4.0 + 2.1) * 0.4)
    return band(len(t), 250, 2000, 233, 0.9) * e


# 3 回目：火の玉は「ボフッ」（てじゃ）。噴気孔の vent_3 と同じ系統で、ゲームで聞こえるよう中音を足す
def whoosh3_1():   # ボフッ：標準
    t = tt(0.5)
    return np.tanh((thump(t, 170, 75, 0.1) * 0.9 + band(len(t), 200, 1500, 241, 0.9) * att(t, 0.004) * dec(t, 0.09) * 0.9) * 1.6)
def whoosh3_2():   # ボフッ：低く重い
    t = tt(0.6)
    return np.tanh((thump(t, 125, 55, 0.15) * 1.0 + band(len(t), 130, 900, 242, 1.0) * att(t, 0.006) * dec(t, 0.13) * 0.8) * 1.7)
def whoosh3_3():   # ボフッ：軽くて短い
    t = tt(0.35)
    return np.tanh((thump(t, 240, 120, 0.05) * 0.8 + band(len(t), 300, 2200, 243, 0.8) * att(t, 0.003) * dec(t, 0.05) * 1.0) * 1.5)
def whoosh3_4():   # ボフッ＋ふぁさ：空気が抜ける余韻つき
    t = tt(0.7)
    return np.tanh((thump(t, 170, 70, 0.1) * 0.9 + band(len(t), 180, 1200, 244, 0.9) * dec(t, 0.08) * 0.8 + band(len(t), 500, 3000, 245, 0.6) * att(t, 0.04) * dec(t, 0.22) * 0.35) * 1.6)


# 炎上は「ボウッ」（てじゃ）：頭は少しやわらかく、炎が広がって消える。低音に中音を重ねる
def burn3_1():     # ボウッ：標準
    t = tt(0.8)
    return np.tanh((band(len(t), 120, 1200, 251, 1.0) * att(t, 0.025) * dec(t, 0.22) * 1.2 + thump(t, 130, 75, 0.14) * 0.5) * 1.6)
def burn3_2():     # ボウッ：太くて長め
    t = tt(1.1)
    return np.tanh((band(len(t), 80, 800, 252, 1.0) * att(t, 0.04) * dec(t, 0.36) * 1.3 + thump(t, 100, 55, 0.22) * 0.6) * 1.7)
def burn3_3():     # ボウッ：明るい炎（少し高め）
    t = tt(0.7)
    return np.tanh((band(len(t), 220, 2400, 253, 0.8) * att(t, 0.02) * dec(t, 0.18) * 1.3 + thump(t, 180, 100, 0.09) * 0.4) * 1.5)
def burn3_4():     # ボッ→ウゥ：頭に一発、そのあと炎がふくらむ
    t = tt(0.9); fc = 260 + 650 * np.clip((t - 0.03) / 0.18, 0, 1) - 450 * np.clip((t - 0.2) / 0.7, 0, 1)
    return np.tanh((thump(t, 170, 85, 0.07) * 0.8 + sweep_noise(len(t), fc, None, 254, 0.45) * att(t, 0.03) * dec(t, 0.26) * 1.0) * 1.6)


# burn3_1 は近いが「硬い音がきこえる」→ 頭の一発（thump）と強い歪み（tanh）が原因とみて外す／やわらげる
def burn4_1():     # 一発なし・歪みなし、頭 40ms で立ち上がる
    t = tt(0.8)
    return band(len(t), 120, 1200, 251, 1.0) * att(t, 0.04) * dec(t, 0.22)
def burn4_2():     # 上の高さを切ってさらにやわらかく（900Hz まで）
    t = tt(0.8)
    return band(len(t), 110, 900, 251, 1.0) * att(t, 0.05) * dec(t, 0.24)
def burn4_3():     # 低音は残す：頭のやわらかい低音を足す
    t = tt(0.8)
    return band(len(t), 120, 1100, 251, 1.0) * att(t, 0.04) * dec(t, 0.22) + tone(95, t) * att(t, 0.05) * dec(t, 0.16) * 0.5


# 火の玉 whoosh3_2 は「聞こえたり聞こえなかったり」→ 中音が 9% しかなく BGM の低音に負ける時があるとみて、ボフッのまま中音を足す
def whoosh3_2b():
    t = tt(0.6)
    return np.tanh((thump(t, 125, 55, 0.15) * 0.65 + band(len(t), 130, 900, 242, 1.0) * att(t, 0.006) * dec(t, 0.13) * 0.8 + band(len(t), 280, 1300, 246, 0.8) * att(t, 0.006) * dec(t, 0.1) * 1.0) * 1.7)


GROUPS = [
    ('roar', [roar_1, roar_2, roar_3, roar_4]),
    ('whoosh', [whoosh_1, whoosh_2, whoosh_3]),
    ('burn', [burn_1, burn_2, burn_3]),
    ('heat', [heat_1, heat_2, heat_3]),
    ('vent', [vent_1, vent_2, vent_3]),
    ('steam', [steam_1, steam_2, steam_3]),
    ('wake', [wake_1, wake_2, wake_3]),
    ('rocklaunch', [rocklaunch_1, rocklaunch_2, rocklaunch_3]),
    ('rockbounce', [rockbounce_1, rockbounce_2, rockbounce_3]),
    ('looplava', [looplava_1, looplava_2]),
    ('loopfire', [loopfire_1, loopfire_2]),
    ('loopwall', [loopwall_1, loopwall_2]),
    ('fix', [rocklaunch_2b, rockbounce_2b, looplava_2b]),
    ('whoosh2', [whoosh2_1, whoosh2_2, whoosh2_3, whoosh2_4]),
    ('whoosh3', [whoosh3_1, whoosh3_2, whoosh3_3, whoosh3_4]),
    ('whoosh3b', [whoosh3_2b]),
    ('burn2', [burn2_1, burn2_2, burn2_3, burn2_4]),
    ('burn3', [burn3_1, burn3_2, burn3_3, burn3_4]),
    ('burn4', [burn4_1, burn4_2, burn4_3]),
    ('heat2', [heat2_1, heat2_2, heat2_3, heat2_4]),
    ('loopfire2', [loopfire2_1, loopfire2_2, loopfire2_3]),
]

if __name__ == '__main__':
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    for name, fns in GROUPS:
        parts = []
        for k, fn in enumerate(fns):
            y = tail(fn(), 0.02) if not (name.startswith('loop') or fn is looplava_2b) else fn()
            y = y / (np.max(np.abs(y)) + 1e-9) * 0.85
            save(y, os.path.join(out, f"{name}_{k + 1}.wav"))
            parts += [y, np.zeros(int(0.9 * SR))]
        save(np.concatenate(parts), os.path.join(out, f"menu_{name}.wav"))
        print(name, 'done', flush=True)
