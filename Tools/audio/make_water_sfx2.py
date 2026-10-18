# ステージ2「みずのたに」の水の音・2回目の候補（2026-10-17 てじゃ「ふわふわが水に入った時の音がしっくりこない、プレイヤーが入った時も鳴ってもいいかも」）
#   python make_water_sfx2.py <出力フォルダ>  → plop_A/B/C.wav（ふわふわ）、splash_A/B.wav（プレイヤー）
#   決まったら  python make_water_sfx2.py use <plop案> <splash案>  → Assets/FuwaStage2/Audio の sfx_water_plop.wav / sfx_water_splash.wav に
import os, sys, wave
import numpy as np

SR = 44100
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Audio"

def save(y, path, peak=0.85):
    y = y / (np.max(np.abs(y)) + 1e-9) * peak
    st = np.stack([y, y], 1)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes())
    print(os.path.basename(path), f"{len(y)/SR:.2f}s")

def lp(x, a):
    y = np.zeros_like(x); s = 0.0
    for i in range(len(x)): s += a * (x[i] - s); y[i] = s
    return y

def bp(x, lo, hi):
    """かんたんなバンドパス（1次ローパス2つの差）"""
    al = 1 - np.exp(-2 * np.pi * hi / SR); ah = 1 - np.exp(-2 * np.pi * lo / SR)
    return lp(x, al) - lp(x, ah)

def env_end(y, sec):
    n = len(y); return y * np.minimum(1, (n - np.arange(n)) / (sec * SR))

def drops(t, r, start, count, gain, f0=(900, 1600), spread=0.06):
    y = np.zeros_like(t)
    for k in range(count):
        st = start + spread * k + r.uniform(0, spread * 0.6)
        tk = np.clip(t - st, 0, None)
        fk = r.uniform(*f0) * (1 + 0.5 * (1 - np.exp(-tk / 0.01)))
        y += np.sin(2 * np.pi * np.cumsum(fk) / SR) * np.exp(-tk / 0.03) * (t > st) * gain * (0.8 ** k)
    return y

def plop_A(seed=11):
    """ぽちゃん：まるくて低めの「ぽ」（250→420Hz、100ms）＋やわらかい水しぶき（こもったノイズ80ms）＋小さいしずく"""
    r = np.random.default_rng(seed); n = int(0.7 * SR); t = np.arange(n) / SR
    y = bp(r.standard_normal(n), 300, 2500) * np.exp(-t / 0.05) * 0.45
    tt = np.clip(t - 0.008, 0, None); f = 250 + 170 * (1 - np.exp(-tt / 0.06))
    y += np.sin(2 * np.pi * np.cumsum(f) / SR) * np.minimum(1, tt / 0.004) * np.exp(-tt / 0.09) * (t > 0.008)
    y += drops(t, r, 0.12, 3, 0.18)
    return env_end(y, 0.06)

def plop_B(seed=12):
    """ちゃぷん：水面をたたく「ちゃ」（明るめのノイズ、ふたこぶ）＋短い「ぷ」＋しずく多め。軽い物が落ちた感じ"""
    r = np.random.default_rng(seed); n = int(0.7 * SR); t = np.arange(n) / SR
    nz = bp(r.standard_normal(n), 800, 6000)
    y = nz * (np.exp(-t / 0.02) + 0.5 * np.exp(-np.clip(t - 0.045, 0, None) / 0.05) * (t > 0.045)) * 0.5
    tt = np.clip(t - 0.03, 0, None); f = 520 + 380 * (1 - np.exp(-tt / 0.03))
    y += np.sin(2 * np.pi * np.cumsum(f) / SR) * np.minimum(1, tt / 0.003) * np.exp(-tt / 0.045) * (t > 0.03) * 0.6
    y += drops(t, r, 0.1, 4, 0.2, f0=(1200, 2200), spread=0.05)
    return env_end(y, 0.06)

def plop_C(seed=13):
    """とぷん：ふわっと沈む感じ。ノイズはごく弱く、低い「とぷ」（180→300Hz）を長め＋泡がぷくぷく2つ"""
    r = np.random.default_rng(seed); n = int(0.8 * SR); t = np.arange(n) / SR
    y = bp(r.standard_normal(n), 200, 1500) * np.exp(-t / 0.04) * 0.25
    tt = np.clip(t - 0.01, 0, None); f = 180 + 120 * (1 - np.exp(-tt / 0.08))
    y += np.sin(2 * np.pi * np.cumsum(f) / SR) * np.minimum(1, tt / 0.006) * np.exp(-tt / 0.13) * (t > 0.01)
    for st, f0 in ((0.2, 520), (0.31, 640)):
        tk = np.clip(t - st, 0, None); fk = f0 * (1 + 0.4 * (1 - np.exp(-tk / 0.02)))
        y += np.sin(2 * np.pi * np.cumsum(fk) / SR) * np.exp(-tk / 0.035) * (t > st) * 0.3
    return env_end(y, 0.08)

def splash_A(seed=21):
    """ざぶっ：人が水に入る。低い「どぼ」＋広い水しぶき（200ms）＋あとから落ちるしずくのざわざわ"""
    r = np.random.default_rng(seed); n = int(1.0 * SR); t = np.arange(n) / SR
    y = bp(r.standard_normal(n), 250, 5000) * (np.minimum(1, t / 0.006) * np.exp(-t / 0.12)) * 0.8
    y += bp(r.standard_normal(n), 1500, 7000) * np.exp(-np.clip(t - 0.08, 0, None) / 0.25) * (t > 0.08) * 0.25 * (0.6 + 0.4 * r.random(n))
    tt = t; f = 110 + 60 * (1 - np.exp(-tt / 0.05))
    y += np.sin(2 * np.pi * np.cumsum(f) / SR) * np.minimum(1, tt / 0.005) * np.exp(-tt / 0.1) * 0.7
    y += drops(t, r, 0.18, 5, 0.12, f0=(900, 1800), spread=0.07)
    return env_end(y, 0.15)

def splash_B(seed=22):
    """じゃぶ：浅瀬に足を入れる軽め。こもった「じゃ」（150ms）＋短い「ぶ」＋しずく少し"""
    r = np.random.default_rng(seed); n = int(0.7 * SR); t = np.arange(n) / SR
    y = bp(r.standard_normal(n), 300, 3500) * np.minimum(1, t / 0.01) * np.exp(-t / 0.08) * 0.7
    tt = np.clip(t - 0.02, 0, None); f = 160 + 140 * (1 - np.exp(-tt / 0.04))
    y += np.sin(2 * np.pi * np.cumsum(f) / SR) * np.minimum(1, tt / 0.004) * np.exp(-tt / 0.07) * (t > 0.02) * 0.6
    y += drops(t, r, 0.12, 3, 0.12)
    return env_end(y, 0.1)

def chirp_bubble(t, start, f0, rise, tau, gain):
    """泡の共鳴（ミナールト振動）：水に入った空気の泡が、つぶれながら高さが上がる短い「ぷ」"""
    tk = np.clip(t - start, 0, None)
    f = f0 * (1 + rise * (1 - np.exp(-tk / (tau * 0.8))))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.minimum(1, tk / 0.0015) * np.exp(-tk / tau) * (t > start) * gain

def room(y, r, length=0.18, mix=0.12):
    """ほんのり響き（短い減衰ノイズのインパルス応答をたたみこむ）"""
    n = int(length * SR); ir = r.standard_normal(n) * np.exp(-np.arange(n) / (0.045 * SR))
    ir = lp(ir, 0.35); ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    wet = np.convolve(y, ir)[:len(y)]
    return y + wet * mix

def nat(seed, f0, splash_len, n_small, bright):
    """自然な「ちゃぷん」：ごく短い水面の「ちゃ」（帯域ノイズ、立ち上がり1ms）→ 主の泡「ぷ」（f0 から速く上がる、25〜45ms）
    → 小さい泡が数粒（高め・短い）→ ほんのり響き"""
    r = np.random.default_rng(seed); n = int(0.6 * SR); t = np.arange(n) / SR
    y = bp(r.standard_normal(n), 500, bright) * np.minimum(1, t / 0.001) * np.exp(-t / splash_len) * 0.55
    y += chirp_bubble(t, 0.006, f0, 0.9, 0.035, 1.0)
    for k in range(n_small):
        st = 0.03 + r.uniform(0, 0.12)
        y += chirp_bubble(t, st, f0 * r.uniform(1.4, 2.6), r.uniform(0.5, 1.0), r.uniform(0.008, 0.018), r.uniform(0.12, 0.3))
    y = room(y, r)
    return env_end(y, 0.08)

def nat_1(): return nat(31, 620, 0.018, 3, 4500)    # まん中の高さ、泡少し
def nat_2(): return nat(32, 480, 0.025, 4, 3500)    # 少し低くてやわらかい
def nat_3(): return nat(33, 780, 0.014, 2, 6000)    # 小さく軽い

def soft(seed, f0, rise, splash_gain, bright, lpa, main_gain=1.0, small_gain=1.0):
    """やわらかい「ちゃぷん」（てじゃ「ちょっとよくなった、もうちょいやわらかく」）：N2 をもとに、
    立ち上がりをゆるく（4ms）、しぶきを暗く小さく、泡の上がり幅を小さく、全体を少しこもらせる"""
    r = np.random.default_rng(seed); n = int(0.6 * SR); t = np.arange(n) / SR
    y = bp(r.standard_normal(n), 300, bright) * np.minimum(1, t / 0.004) * np.exp(-t / 0.03) * splash_gain
    y += chirp_bubble(t, 0.008, f0, rise, 0.045, main_gain) * np.minimum(1, np.clip(t - 0.008, 0, None) / 0.004)
    for k in range(3):
        st = 0.04 + r.uniform(0, 0.12)
        y += chirp_bubble(t, st, f0 * r.uniform(1.3, 2.0), r.uniform(0.4, 0.7), r.uniform(0.012, 0.022), r.uniform(0.08, 0.18) * small_gain)
    y = lp(y, lpa)
    y = room(y, r, mix=0.15)
    return env_end(y, 0.1)

def soft_1(): return soft(41, 440, 0.6, 0.35, 2500, 0.35)
def soft_2(): return soft(42, 380, 0.5, 0.25, 2000, 0.25)
# ささやか（てじゃ「ここから強い音を抜こう、ほんとささやかでいい」）：主の泡「ぷ」を弱く、ピークも 0.85→0.45 に（save の peak）
def tiny_1(): return soft(41, 440, 0.45, 0.35, 2500, 0.35, main_gain=0.3, small_gain=0.8)
def tiny_2(): return soft(42, 380, 0.4, 0.28, 2000, 0.25, main_gain=0.25, small_gain=0.7)
def tiny_3(): return soft(42, 380, 0.4, 0.28, 2000, 0.25, main_gain=0.06, small_gain=0.7)   # T2 の最初の「ぷ」をほぼなしに（てじゃ）
def tiny_4(): return soft(42, 380, 0.4, 0.28, 2000, 0.25, main_gain=0.0, small_gain=0.7)   # 最初の「ぷ」を完全になし（てじゃ）
def lap(seed, attack, bright, gain, small_gain, n_small=3):
    """「バシッ」もなし（てじゃ）：水面をたたく立ち上がりをゆるく（attack 秒かけてふくらむ）・暗いノイズで「ちゃぷ…」と寄せる水だけ＋小さい泡"""
    r = np.random.default_rng(seed); n = int(0.6 * SR); t = np.arange(n) / SR
    sw = np.minimum(1, t / attack) * np.exp(-np.clip(t - attack, 0, None) / 0.07)
    y = bp(r.standard_normal(n), 150, bright) * sw * gain
    for k in range(n_small):
        st = attack * 0.6 + r.uniform(0, 0.14)
        y += chirp_bubble(t, st, 380 * r.uniform(1.3, 2.0), r.uniform(0.4, 0.7), r.uniform(0.012, 0.022), r.uniform(0.08, 0.18) * small_gain)
    y = lp(y, 0.25)
    y = room(y, r, mix=0.15)
    return env_end(y, 0.1)

def tiny_5(): return lap(42, 0.025, 1400, 0.3, 0.7)          # ゆるく寄せる水＋泡
def tiny_6(): return lap(43, 0.04, 900, 0.18, 0.8, 4)        # さらにこもって、ほぼ泡だけ
def tiny_7(): return lap(43, 0.04, 900, 0.0, 0.8, 4)         # T6 の「ザブ」（水のノイズ）もなし＝小さい泡だけ（てじゃ）
QUIET = {'T1': 0.45, 'T2': 0.45, 'T3': 0.45, 'T4': 0.45, 'T5': 0.4, 'T6': 0.4, 'T7': 0.35}

PLOPS = {'T1': tiny_1, 'T2': tiny_2, 'T3': tiny_3, 'T4': tiny_4, 'T5': tiny_5, 'T6': tiny_6, 'T7': tiny_7, 'A': plop_A, 'B': plop_B, 'C': plop_C, 'N1': nat_1, 'N2': nat_2, 'N3': nat_3, 'S1': soft_1, 'S2': soft_2}
def dopon(seed, f0, rise, tau, noise_gain, tail):
    """ドポン…（てじゃ）：人が水に沈む、まるくて低い「どぽ」。大きい泡の共鳴（低い f0 から少し上がる、長め）が主役、
    水面のノイズはこもらせて弱く、あとから小さい泡がぽこぽこ上がってくる＋響き長め"""
    r = np.random.default_rng(seed); n = int(1.2 * SR); t = np.arange(n) / SR
    y = bp(r.standard_normal(n), 120, 1200) * np.minimum(1, t / 0.008) * np.exp(-t / 0.06) * noise_gain
    y += chirp_bubble(t, 0.01, f0, rise, tau, 1.0) * np.minimum(1, np.clip(t - 0.01, 0, None) / 0.006)
    y += chirp_bubble(t, 0.05, f0 * 1.6, rise * 0.8, tau * 0.6, 0.35)
    for k in range(tail):
        st = 0.18 + 0.09 * k + r.uniform(0, 0.05)
        y += chirp_bubble(t, st, r.uniform(420, 750), r.uniform(0.4, 0.8), r.uniform(0.015, 0.03), 0.18 * (0.85 ** k))
    y = lp(y, 0.3)
    y = room(y, r, length=0.35, mix=0.2)
    return env_end(y, 0.15)

def dopon_1(): return dopon(51, 140, 0.7, 0.11, 0.35, 5)   # ドポン…（標準）
def dopon_2(): return dopon(52, 110, 0.6, 0.14, 0.25, 6)   # もっと低く深く

def zabun(seed, attack, decay, bright, body, n_small, second):
    """ザブン（てじゃ「plop_T5 の方向性で」）：T5 と同じく立ち上がりのゆるい寄せる水だけど、量が多くて長い。
    低い水のかたまり（body）＋こもったざわざわ、少し遅れてもうひと寄せ（second）、あとから小さい泡"""
    r = np.random.default_rng(seed); n = int(1.0 * SR); t = np.arange(n) / SR
    sw = np.minimum(1, t / attack) ** 1.5 * np.exp(-np.clip(t - attack, 0, None) / decay)
    y = bp(r.standard_normal(n), 150, bright) * sw * 0.6
    y += bp(r.standard_normal(n), 60, 400) * sw * body
    if second > 0:
        t2 = np.clip(t - 0.12, 0, None)
        sw2 = np.minimum(1, t2 / (attack * 1.5)) * np.exp(-np.clip(t2 - attack * 1.5, 0, None) / (decay * 1.3)) * (t > 0.12)
        y += bp(r.standard_normal(n), 150, bright * 0.7) * sw2 * second
    for k in range(n_small):
        st = attack + r.uniform(0, 0.35)
        y += chirp_bubble(t, st, r.uniform(380, 800), r.uniform(0.4, 0.8), r.uniform(0.012, 0.025), r.uniform(0.06, 0.14))
    y = lp(y, 0.3)
    y = room(y, r, length=0.3, mix=0.18)
    return env_end(y, 0.15)

def zabun_1(): return zabun(61, 0.04, 0.12, 1800, 0.5, 5, 0.35)    # ザブン（標準）
def zabun_2(): return zabun(62, 0.05, 0.16, 1400, 0.7, 6, 0.45)    # 重め・低め

SPLASHES = {'A': splash_A, 'B': splash_B, 'D1': dopon_1, 'D2': dopon_2, 'Z1': zabun_1, 'Z2': zabun_2}
def topun_1(): return dopon(71, 230, 0.6, 0.07, 0.2, 3)    # トプン（てじゃ「ドプンを軽くして」）：高めで短く、泡少し
def topun_2(): return dopon(72, 190, 0.6, 0.085, 0.15, 2)  # 少し低め・さらに控えめ
SPLASHES['TP1'] = topun_1; SPLASHES['TP2'] = topun_2
SPLASH_PEAK = {'Z1': 0.4, 'TP1': 0.5, 'TP2': 0.5}   # てじゃ「プレイヤはちょっとやかましかった」→ 半分くらいに

if __name__ == '__main__':
    if len(sys.argv) >= 4 and sys.argv[1] == 'use':
        save(PLOPS[sys.argv[2]](), os.path.join(OUT, "sfx_water_plop.wav"), QUIET.get(sys.argv[2], 0.85))
        save(SPLASHES[sys.argv[3]](), os.path.join(OUT, "sfx_water_splash.wav"), SPLASH_PEAK.get(sys.argv[3], 0.85))
        # 歩いて入る・軽く触れた時の小さい「ちゃぷ」（FuwaShallowWater の splashSoft。2026-10-17 てじゃ「軽くふれてもザブンなるのが違和感」）
        save(tiny_5(), os.path.join(OUT, "sfx_water_splash_soft.wav"), 0.35)
    else:
        d = sys.argv[1] if len(sys.argv) > 1 else "."
        os.makedirs(d, exist_ok=True)
        for k, fn in PLOPS.items(): save(fn(), os.path.join(d, f"plop_{k}.wav"))
        for k, fn in SPLASHES.items(): save(fn(), os.path.join(d, f"splash_{k}.wav"))
