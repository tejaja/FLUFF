# FLUFF のBGM用・新しい音源（2026-10-07）。make_bgm.py の作曲（メロディ・コード）はそのまま使い、音だけ良くする。
# - 楽器：弦のうなり・ハンマー音・倍音ごとの減衰を持つフェルトピアノ、コーラスのかかった厚いパッド など
# - 演奏のゆらぎ：タイミングと強弱を少しずらす
# - 残響：初期反射＋高い音ほど早く消える余韻（帯域ごとに減衰時間の違うノイズIRをたたみこむ）
# - 仕上げ：低域のにごりカット、バスコンプ、先読みリミッター
# - ループ：曲の長さ＋余韻を描いて、余韻を頭に重ねて切る（つなぎ目なし）
import numpy as np, wave, os
from scipy import signal

SR = 44100
rng_global = np.random.default_rng(1234)

def midi_hz(m): return 440.0 * 2 ** ((m - 69) / 12)

# ---------------- フィルタ ----------------
def lp(x, fc, order=2):
    sos = signal.butter(order, min(fc, SR * 0.45), 'low', fs=SR, output='sos')
    return signal.sosfilt(sos, x, axis=0)

def hp(x, fc, order=2):
    sos = signal.butter(order, fc, 'high', fs=SR, output='sos')
    return signal.sosfilt(sos, x, axis=0)

def bp(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, min(hi, SR * 0.45)], 'band', fs=SR, output='sos')
    return signal.sosfilt(sos, x, axis=0)

def shelf(x, fc, gain_db, kind='low'):
    # 1次のシェルフ（ローパス成分を足し引き）
    g = 10 ** (gain_db / 20)
    lowpart = lp(x, fc, 1)
    if kind == 'low': return x + (g - 1) * lowpart
    return lowpart + g * (x - lowpart)

# ---------------- 楽器 ----------------
def felt_piano(f, dur, vel=0.8, soft=1.0):
    """フェルトピアノ：弦3本（わずかにずらしてうなり）、倍音は少しずつ高くずれる（弦の固さ）、
    高い倍音ほど早く消える2段階の減衰、フェルトのハンマーの「コトッ」、鍵盤を離すとダンパーで止まる"""
    tail = 2.2
    n = int((dur + tail) * SR); t = np.arange(n) / SR
    B = 0.00012 * (f / 261.6) ** 1.5           # 弦の固さ（高い音ほど倍音がずれる）
    bright = 0.35 + 0.65 * vel                  # 強く弾くほど明るい
    y = np.zeros(n)
    nparts = 14
    for k in range(1, nparts + 1):
        fk = f * k * np.sqrt(1 + B * k * k)
        if fk > SR * 0.42: break
        # フェルトなので倍音は弱め、強く弾くと少し出る
        amp = (1.0 / k ** (2.2 - 0.9 * bright)) * (1.0 if k == 1 else 0.9)
        if k == 2: amp *= 0.55
        fast = 0.25 / (1 + 0.5 * k)              # 最初の速い減衰
        slow = (3.2 * (261.6 / f) ** 0.45) / (1 + 0.35 * k)   # その後の長い余韻（低い音ほど長い）
        env = 0.55 * np.exp(-t / fast) + 0.45 * np.exp(-t / slow)
        for det in (-0.9, 0.0, 1.1):            # 弦3本、セント単位でずらす
            ph0 = rng_global.uniform(0, 2 * np.pi)
            y += (amp / 3) * env * np.sin(2 * np.pi * fk * (1 + det / 1731.0) * t + ph0)
    # ハンマーのやわらかい当たり（こもったノイズの短い「ト」）
    hn = int(0.04 * SR)
    hammer = lp(rng_global.standard_normal(hn), 900 + 1500 * vel) * np.exp(-np.arange(hn) / SR / 0.008)
    y[:hn] += hammer * 0.06 * vel
    # アタックを丸く（フェルト）
    y *= np.clip(t / (0.006 + 0.02 * soft), 0, 1)
    # 離鍵：ダンパーでやさしく止まる（完全には切らない）
    y *= np.where(t > dur, np.exp(-(t - dur) / 0.35), 1.0)
    return lp(y, 1800 + 3500 * bright) * vel * 0.5

def felt_piano2(f, dur, vel=0.7, tone=0.5, sustain=1.0, mech=1.0):
    """本物寄りのフェルトピアノ（2026-10-07）。
    - 打鍵の瞬間は少し明るく、すぐこもっていく（明るさのエンベロープ＝倍音ごとに速く減衰）
    - フェルトが弦に当たる低い「ポフッ」（短い低域のノイズ）＋鍵盤の機械音「コトッ」
    - 余韻は短め（ダンパーが軽く触れている感じ）、高い音ほど短い
    tone: 0=こもる〜1=明るい、sustain: 余韻の長さの倍率、mech: 当たり音の量"""
    tail = 1.6 * sustain
    n = int((dur + tail) * SR); t = np.arange(n) / SR
    B = 0.00015 * (f / 261.6) ** 1.6
    y = np.zeros(n)
    for k in range(1, 18):
        fk = f * k * np.sqrt(1 + B * k * k)
        if fk > SR * 0.42: break
        # 打った瞬間の明るさ：フェルトでも2〜5倍音くらいは一瞬出る
        a0 = (1.0 / k ** 1.25) * (0.5 + 0.9 * vel) if k > 1 else 1.0
        if k == 2: a0 *= 0.8
        # 倍音ほど早く消える（＝すぐこもる）
        fast = 0.06 + 0.5 / (k ** 1.3) * (0.6 + tone)
        slow = (1.5 * sustain * (261.6 / f) ** 0.5) / (1 + 0.6 * (k - 1))
        env = 0.6 * np.exp(-t / fast) + 0.4 * (1.0 / k ** 0.6) * np.exp(-t / slow)
        for det in (-0.7, 0.0, 0.9):
            y += (a0 / 3) * env * np.sin(2 * np.pi * fk * (1 + det / 1731.0) * t + rng_global.uniform(0, 6.28))
    # 立ち上がり：フェルトなので 4〜8ms
    y *= np.clip(t / (0.004 + 0.004 * (1 - vel)), 0, 1)
    # フェルトが弦に当たる「ポフッ」：低めのこもったノイズ、20msくらい
    pn = int(0.05 * SR)
    puff = lp(rng_global.standard_normal(pn), 450 + 300 * vel, 2) * np.exp(-np.arange(pn) / SR / 0.012)
    y[:pn] += puff * 0.55 * mech * vel
    # 鍵盤・アクションの機械音「コトッ」：少し高めの短いクリック（音程と関係ない）
    cn = int(0.02 * SR)
    click = bp(rng_global.standard_normal(cn), 1200, 3500, 2) * np.exp(-np.arange(cn) / SR / 0.003)
    y[:cn] += click * 0.08 * mech * (0.5 + vel)
    # 離鍵：ダンパーで止まる（少しだけ残る）
    y *= np.where(t > dur, np.exp(-(t - dur) / (0.12 + 0.12 * sustain)), 1.0)
    # 全体の明るさ（フェルトのこもり）
    y = lp(y, 1400 + 3600 * tone, 2)
    return y * vel * 0.55

def wind(f, dur, vel=0.7, kind='flute', vib=1.0, breath=1.0, taper=0.0, release=0.09, soft=0.0):
    """笛系（2026-10-07）。息のノイズ（音程の近くに帯域を絞ったもの＋広い「ふー」）、
    吹き始めの「チフ」（短い息の破裂）、遅れてかかるビブラート、音程のほんの少しのゆらぎ。
    kind: 'ocarina'（ほぼ正弦波・まるい）/'flute'（倍音少し・息多め）/'recorder'（2,3倍音が少し強め）"""
    tail = 0.35 + release
    n = int((dur + tail) * SR); t = np.arange(n) / SR
    # 音程：吹き始めに少しだけ下から入る＋遅れビブラート＋ゆらぎ
    scoop = -12 * np.exp(-t / 0.035)                          # セント
    vdepth = {'ocarina': 9, 'flute': 14, 'recorder': 7}[kind] * vib
    vfade = np.clip((t - 0.25) / 0.45, 0, 1)
    vibr = vdepth * vfade * np.sin(2 * np.pi * 5.0 * t + rng_global.uniform(0, 6.28))
    wobble = 3 * np.sin(2 * np.pi * 0.7 * t + rng_global.uniform(0, 6.28))
    inst = f * 2 ** ((scoop + vibr + wobble) / 1200)
    ph = 2 * np.pi * np.cumsum(inst) / SR
    harm = {'ocarina': [(1, 1.0), (2, 0.04), (3, 0.06)],
            'flute': [(1, 1.0), (2, 0.22), (3, 0.08), (4, 0.03)],
            'recorder': [(1, 1.0), (2, 0.35), (3, 0.22), (4, 0.06), (5, 0.04)]}[kind]
    y = np.zeros(n)
    for k, a in harm:
        if f * k < SR * 0.42: y += a * np.sin(k * ph)
    # 息：音程付近に絞ったノイズ（音にまとわりつく「ふー」）と、広い息
    nz = rng_global.standard_normal(n)
    tonal_breath = bp(nz, f * 0.8, f * 1.3, 2)
    wide_breath = bp(nz, 1500, 7000, 2)
    bamt = {'ocarina': 0.6, 'flute': 1.0, 'recorder': 0.5}[kind] * breath
    y += bamt * (0.35 * tonal_breath / (np.std(tonal_breath) + 1e-9) * 0.25 + 0.022 * wide_breath / (np.std(wide_breath) + 1e-9))
    # 音量：ふわっと入って（息の量で少しふくらむ）、離すとすっと消える
    att = {'ocarina': 0.06, 'flute': 0.07, 'recorder': 0.035}[kind] * (1 + 1.5 * soft)   # soft: やわらかく入る
    env = np.clip(t / att, 0, 1) ** 1.2
    if taper > 0:
        # 音の終わりをふわっと弱めていく：前半で少しふくらんで、後半はなだらかに下がる（息がぬけていく感じ）
        u = np.clip(t / max(dur, 0.1), 0, 1)
        swell = (1 + 0.12 * np.sin(np.pi * np.clip(u / 0.5, 0, 1) * 0.5)) * (1 - taper * np.clip((u - 0.3) / 0.7, 0, 1) ** 1.3)
    else:
        swell = 1 + 0.08 * np.clip(t / max(dur, 0.1), 0, 1)
    env *= swell * np.where(t > dur, np.exp(-(t - dur) / release), 1.0)
    # チフ：吹き始めの短い息の破裂
    cn = int(0.04 * SR)
    chiff = bp(rng_global.standard_normal(cn), f * 1.5, min(f * 6, 9000), 2) * np.exp(-np.arange(cn) / SR / 0.012)
    y[:cn] += chiff * {'ocarina': 0.10, 'flute': 0.16, 'recorder': 0.22}[kind] * breath * (1 - 0.7 * soft)
    y *= env
    if kind == 'ocarina': y = lp(y, 3500 - 2000 * soft, 2)   # soft: 高い成分をさらに丸める
    return y * vel * 0.17

# ---------- たたく・はじく・打楽器（2026-10-07） ----------
def marimba3(f, dur, vel=0.8, hard=0.35):
    """木っぽいマリンバ（2026-10-07）：金属っぽさの元になる高いモード（9倍以上）をほぼ無くし、3.9倍もすぐ消す。
    やわらかい毛糸のマレットの「ポコ」っとした当たり（低めのノイズ）、共鳴パイプの「ぼわん」を強め、全体を少し丸める"""
    tail = 1.0
    n = int((dur + tail) * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    base_dec = 0.42 * (440 / f) ** 0.4
    for mul, amp, dec in [(1.0, 1.0, base_dec), (3.93, 0.16 * (0.4 + hard), 0.05), (9.2, 0.02 * hard, 0.012)]:
        if f * mul > SR * 0.42: continue
        y += amp * np.exp(-t / dec) * np.sin(2 * np.pi * f * mul * t + rng_global.uniform(0, 6.28))
    # 共鳴パイプ：基音を少し遅れてふくらませる（木琴の「ぼわん」）
    y += 0.4 * (1 - np.exp(-t / 0.015)) * np.exp(-t / (0.75 * (440 / f) ** 0.3)) * np.sin(2 * np.pi * f * t)
    # 毛糸のマレット：低めのこもった「ポコ」
    cn = int(0.02 * SR)
    knock = lp(rng_global.standard_normal(cn), 900 + 900 * hard) * np.exp(-np.arange(cn) / SR / 0.004)
    y[:cn] += knock * 0.22
    y *= np.clip(t / 0.003, 0, 1)
    y = lp(y, 2800 + 2500 * hard, 2)
    return y * vel * 0.48

def glock(f, dur, vel=0.7, hard=0.5, ring=1.0):
    """グロッケン（鉄琴）（2026-10-07）：鉄の棒の振動モード（1, 2.76, 5.40, 8.93倍）。
    基音は長く「キーン」と残り、上の倍音は速く消える。硬いマレットの「チッ」を少し。
    ring: 余韻の長さの倍率。音は離しても止めない（本物と同じく鳴りっぱなし）"""
    base = 1.6 * ring * (880 / f) ** 0.5
    tail = min(base * 3.5, 4.0)
    n = int(tail * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    for mul, amp, dec in [(1.0, 1.0, base), (2.76, 0.28 * (0.4 + hard), base * 0.12), (5.40, 0.12 * hard, base * 0.04), (8.93, 0.05 * hard, base * 0.02)]:
        if f * mul > SR * 0.45: continue
        ph = rng_global.uniform(0, 6.28)
        # ほんの少しの揺らぎ（2本のわずかにずれた成分でうなり）
        y += amp * np.exp(-t / dec) * (0.8 * np.sin(2 * np.pi * f * mul * t + ph) + 0.2 * np.sin(2 * np.pi * f * mul * 1.0007 * t + ph))
    cn = int(0.006 * SR)
    click = hp(rng_global.standard_normal(cn), 3000) * np.exp(-np.arange(cn) / SR / 0.0012)
    y[:cn] += click * 0.08 * (0.3 + hard)
    y *= np.clip(t / 0.0008, 0, 1)
    return y * vel * 0.3

def vibes(f, dur, vel=0.7, hard=0.4, trem=5.5, depth=0.3, rel=0.35):
    """ビブラフォン（2026-10-07）：アルミの棒（1, 4, 10倍）＋共鳴パイプのモーターで「わんわん」揺れる余韻。
    金属だけど丸くてやさしい。音を離したらペダルを離した感じで早めに消える"""
    base = 1.8 * (660 / f) ** 0.35
    n = int((dur + rel + 0.1) * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    for mul, amp, dec in [(1.0, 1.0, base), (4.0, 0.22 * (0.4 + hard), base * 0.08), (10.0, 0.05 * hard, base * 0.02)]:
        if f * mul > SR * 0.45: continue
        y += amp * np.exp(-t / dec) * np.sin(2 * np.pi * f * mul * t + rng_global.uniform(0, 6.28))
    y *= 1 - depth * (0.5 + 0.5 * np.sin(2 * np.pi * trem * t + rng_global.uniform(0, 6.28)))
    cn = int(0.008 * SR)
    y[:cn] += lp(rng_global.standard_normal(cn), 2500) * np.exp(-np.arange(cn) / SR / 0.0015) * 0.05 * (0.3 + hard)
    y *= np.clip(t / 0.001, 0, 1) * np.where(t < dur, 1.0, np.exp(-(t - dur) / (rel / 3)))
    return y * vel * 0.32

def celesta(f, dur, vel=0.7, hard=0.4):
    """チェレスタ（2026-10-07）：鉄の板をフェルトのハンマーで叩く、木の箱の共鳴つき。
    グロッケンより丸くて短め、「ポロン」とした鐘っぽさ（くるみ割り人形の金平糖の音）"""
    base = 0.9 * (880 / f) ** 0.5
    n = int(min(base * 4, 3.0) * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    for mul, amp, dec in [(1.0, 1.0, base), (2.0, 0.25, base * 0.35), (3.0, 0.07, base * 0.15), (5.4, 0.04 * hard, base * 0.05)]:
        if f * mul > SR * 0.45: continue
        y += amp * np.exp(-t / dec) * np.sin(2 * np.pi * f * mul * t + rng_global.uniform(0, 6.28))
    # 箱の共鳴：低めの短いこもり
    y += 0.12 * np.exp(-t / 0.05) * np.sin(2 * np.pi * f * 0.5 * t)
    y *= np.clip(t / 0.002, 0, 1) * np.where(t < dur + 0.1, 1.0, np.exp(-(t - dur - 0.1) / 0.25))
    return lp(y, min(f * 6, 12000), 2) * vel * 0.34

def kalimba(f, dur, vel=0.7, hard=0.4):
    """カリンバ（2026-10-07）：金属の歯をはじく。基音＋かなり上の倍音（約6倍）が「ティン」と短く、木の箱で丸く響く"""
    base = 0.7 * (660 / f) ** 0.4
    n = int(min(base * 4, 2.5) * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    for mul, amp, dec in [(1.0, 1.0, base), (6.2, 0.14 * (0.4 + hard), base * 0.06), (17.0, 0.03 * hard, base * 0.015)]:
        if f * mul > SR * 0.45: continue
        y += amp * np.exp(-t / dec) * np.sin(2 * np.pi * f * mul * t + rng_global.uniform(0, 6.28))
    y += 0.25 * (1 - np.exp(-t / 0.01)) * np.exp(-t / 0.12) * np.sin(2 * np.pi * f * t)
    y *= np.clip(t / 0.0015, 0, 1)
    return y * vel * 0.36

def soft_lead(f, dur, vel=0.7, pulse=0.35, cutoff=2.2):
    """やわらかいピコピコ系（2026-10-07）：細めの矩形波を2本少しずらして重ね、音の頭だけ明るくしてすぐ丸める。
    cutoff は基音の何倍まで残すか（フィルターの開き）"""
    rel = 0.12
    n = int((dur + rel + 0.05) * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    for det in (-4, 4):
        ff = f * 2 ** (det / 1200)
        ph = (ff * t + rng_global.uniform(0, 1)) % 1.0
        y += np.where(ph < pulse, 1.0, -1.0) - (2 * pulse - 1)
    y *= 0.5
    env = np.minimum(t / 0.006, 1.0) * np.where(t < dur, 0.75 + 0.25 * np.exp(-t / 0.08), 0.0)
    rel_part = np.where(t >= dur, np.exp(-(t - dur) / (rel / 3)), 0.0) * (0.75 + 0.25 * np.exp(-dur / 0.08))
    env = env + rel_part * np.minimum(t / 0.006, 1.0)
    # 頭だけ明るく→丸める：明るい版と暗い版を混ぜる
    bright = lp(y, min(f * cutoff * 2.2, 9000), 2); dark = lp(y, min(f * cutoff, 6000), 2)
    k = np.exp(-t / 0.05)
    y = (bright * k + dark * (1 - k)) * env
    return y * vel * 0.16

def marimba2(f, dur, vel=0.8, hard=0.5):
    """マリンバ：木の板のモード（1, 3.9, 9.2 倍…高いほどすぐ消える）、マレットの当たり、下の共鳴パイプで基音が少し長く鳴る"""
    tail = 1.2
    n = int((dur + tail) * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    modes = [(1.0, 1.0, 0.55 * (440 / f) ** 0.35), (3.93, 0.32 * (0.5 + hard), 0.12), (9.2, 0.12 * hard, 0.04), (16.1, 0.05 * hard, 0.015)]
    for mul, amp, dec in modes:
        if f * mul > SR * 0.42: continue
        y += amp * np.exp(-t / dec) * np.sin(2 * np.pi * f * mul * t + rng_global.uniform(0, 6.28))
    # 共鳴パイプ：基音だけ少し長く、ほんの少し遅れてふくらむ
    y += 0.25 * (1 - np.exp(-t / 0.02)) * np.exp(-t / (0.9 * (440 / f) ** 0.3)) * np.sin(2 * np.pi * f * t)
    # マレットの当たり（やわらかいクリック）
    cn = int(0.012 * SR)
    y[:cn] += lp(rng_global.standard_normal(cn), 2500 + 3000 * hard) * np.exp(-np.arange(cn) / SR / 0.002) * 0.15
    y *= np.clip(t / 0.0015, 0, 1)
    return y * vel * 0.45

def music_box2(f, dur, vel=0.5):
    """オルゴール：金属の歯（くし）のモード＋チリッとした当たり、長い余韻"""
    tail = 1.8
    n = int((dur + tail) * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    for mul, amp, dec in [(1.0, 1.0, 1.6), (5.4, 0.18, 0.25), (11.5, 0.06, 0.08), (2.0, 0.08, 0.6)]:
        if f * mul > SR * 0.42: continue
        y += amp * np.exp(-t / dec) * np.sin(2 * np.pi * f * mul * t + rng_global.uniform(0, 6.28))
    cn = int(0.006 * SR)
    y[:cn] += hp(rng_global.standard_normal(cn), 4000) * np.exp(-np.arange(cn) / SR / 0.0012) * 0.12
    y *= np.clip(t / 0.001, 0, 1)
    return y * vel * 0.4

def ks_string(f, dur, vel=0.7, bright=0.5, damp_time=None, body=True):
    """カープラス・ストロング（弦の物理計算）：ノイズの一発を遅延線でぐるぐる回して弦の音にする。
    bright: 弾いた時の明るさ（ピックの硬さ）。damp_time: 離した後に消えるまでの秒数（None=自然に）"""
    tail = 1.4
    n = int((dur + tail) * SR)
    # 音程（2026-10-07修正）：2点平均で0.5サンプル遅れる分を引き、残りの端数は線形補間で足す。
    # 前は端数を捨てていて、音によって最大15セントくらい低かった（ギターが「音程ずれてる」と言われた）
    P = SR / f
    L = max(2, int(np.floor(P - 0.5)))
    frac = P - 0.5 - L
    # 最初の一発：明るさで高域を削ったノイズ
    exc = rng_global.uniform(-1, 1, L + 2)
    exc = lp(exc, 1500 + 7000 * bright, 1)
    exc -= exc.mean()
    y = np.zeros(n)
    y[:L + 2] = exc
    # 遅延線のフィードバック：y[i] += g * (a*y[i-L] + (1-a)*y[i-L-1])
    loss = 0.996 - 0.002 * (f / 1000)
    # 2点平均（ローパス）×線形補間（端数 frac）＝3タップ：遅れ L + 0.5 + frac
    w0, w1, w2 = 0.5 * (1 - frac), 0.5, 0.5 * frac
    i = L + 2
    while i < n:
        j = min(n, i + L)
        y[i:j] += loss * (w0 * y[i - L:j - L] + w1 * y[i - L - 1:j - L - 1] + w2 * y[i - L - 2:j - L - 2])
        i = j
    t = np.arange(n) / SR
    if damp_time is not None:
        y *= np.where(t > dur, np.exp(-(t - dur) / damp_time), 1.0)
    if body:
        # 胴鳴り：低めの共鳴を少し
        y = y + 0.25 * bp(y, 180, 600, 1)
    return y * vel * 0.5

def strum(freqs, dur, vel=0.6, bright=0.5, spread=0.012, down=True):
    """和音をジャカッと：弦ごとに少しずつ遅らせて鳴らす"""
    fs = sorted(freqs, reverse=not down)
    parts = [ks_string(f, dur, vel * (1 - 0.07 * k), bright, damp_time=0.08) for k, f in enumerate(fs)]
    off = [int(k * spread * SR) for k in range(len(parts))]
    n = max(len(p) + o for p, o in zip(parts, off))
    y = np.zeros(n)
    for p, o in zip(parts, off): y[o:o + len(p)] += p
    return y / np.sqrt(len(parts))

def finger_bass(f, dur, vel=0.8, tone=0.4):
    """指弾きベース：弦（KS）の低い版＋サイン波の芯、指が当たる低い「ブン」"""
    y = ks_string(f, dur, vel, bright=0.15 + 0.3 * tone, damp_time=0.05, body=False)
    n = len(y); t = np.arange(n) / SR
    core = np.sin(2 * np.pi * f * t) * np.exp(-t / 1.2) * np.clip(t / 0.004, 0, 1) * np.where(t > dur, np.exp(-(t - dur) / 0.05), 1.0)
    y = lp(y, 900 + 1500 * tone, 2) * 0.8 + core * 0.45 * vel
    return y * 0.8

def kick2(vel=0.9, punch=0.5, low=0.5):
    n = int(0.5 * SR); t = np.arange(n) / SR
    f0, f1 = 150 + 60 * punch, 46 + 10 * (1 - low)
    fr = f1 + (f0 - f1) * np.exp(-t / 0.035)
    body = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-t / (0.16 + 0.12 * low))
    click = lp(rng_global.standard_normal(n), 5000) * np.exp(-t / 0.0025) * 0.35 * (0.5 + punch)
    y = np.tanh((body + click) * 1.4) / np.tanh(1.4)
    return y * vel * 0.8

def tom2(f=90, vel=0.8, skin=0.5):
    """低いタム（2026-10-07）：皮の「どん」（音程が少し下がるサイン＋2つ目のモード1.5倍）＋叩いた瞬間の「ぱすっ」"""
    n = int(0.9 * SR); t = np.arange(n) / SR
    fr = f * (1 + 0.35 * np.exp(-t / 0.04))
    body = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-t / 0.32)
    body += 0.25 * np.sin(2 * np.pi * np.cumsum(fr * 1.5) / SR) * np.exp(-t / 0.12)
    hit = lp(rng_global.standard_normal(n), 1800 + 1500 * skin) * np.exp(-t / 0.006) * 0.3 * (0.4 + skin)
    y = np.tanh((body + hit) * 1.2) / np.tanh(1.2)
    return y * np.clip(t / 0.001, 0, 1) * vel * 0.7

def snare2(vel=0.8, tone=0.5, snap=0.5):
    n = int(0.35 * SR); t = np.arange(n) / SR
    drum = (np.sin(2 * np.pi * 185 * t) * 0.7 + np.sin(2 * np.pi * 330 * t) * 0.35) * np.exp(-t / (0.06 + 0.04 * tone))
    wires = bp(rng_global.standard_normal(n), 1800, 9000, 2) * np.exp(-t / (0.09 + 0.08 * snap))
    attack = hp(rng_global.standard_normal(n), 3000) * np.exp(-t / 0.004) * 0.6
    y = drum * 0.8 + wires * (0.5 + 0.5 * snap) + attack
    return y * vel * 0.32

def hat2(vel=0.6, open_=False):
    """ハイハット：金属っぽい6本の矩形波を混ぜてハイパス（808のやり方）"""
    dur = 0.32 if open_ else 0.07
    n = int(dur * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    for fr in (205.3, 304.4, 369.6, 522.7, 540.0, 800.0):
        y += np.sign(np.sin(2 * np.pi * fr * 1.0 * t + rng_global.uniform(0, 6.28)))
    y = hp(y, 7000, 2) + 0.4 * hp(rng_global.standard_normal(n), 8000, 2)
    y *= np.exp(-t / (0.1 if open_ else 0.018)) * np.clip(t / 0.0008, 0, 1)
    return y * vel * 0.06

def shaker2(vel=0.5):
    n = int(0.12 * SR); t = np.arange(n) / SR
    y = bp(rng_global.standard_normal(n), 3500, 11000, 2)
    e = np.sin(np.pi * np.clip(t / 0.1, 0, 1)) ** 2
    return y * e * vel * 0.09

def warm_pad(f, dur, vel=0.7, bright=0.5):
    """パッド：ゆっくり揺れる7本のノコギリ波（帯域制限）、ローパスがゆっくり開いて閉じる、コーラスで左右に広げる。ステレオで返す"""
    tail = 1.6
    n = int((dur + tail) * SR); t = np.arange(n) / SR
    L = np.zeros(n); R = np.zeros(n)
    nharm = max(2, int(min(14, (SR * 0.2) / f)))
    for v, det in enumerate(np.linspace(-9, 9, 7)):
        rate = 0.15 + 0.07 * v
        drift = 2.5 * np.sin(2 * np.pi * rate * t + v)          # セント単位でゆっくり揺れる
        inst = f * 2 ** ((det + drift) / 1200)
        ph = 2 * np.pi * np.cumsum(inst) / SR + rng_global.uniform(0, 6.28)
        saw = np.zeros(n)
        for k in range(1, nharm + 1):
            saw += np.sin(k * ph) / k
        pan = (v - 3) / 3.0
        L += saw * np.sqrt(0.5 * (1 - pan) + 1e-9); R += saw * np.sqrt(0.5 * (1 + pan) + 1e-9)
    st = np.stack([L, R], 1) / 7
    # 明るさ（ローパス）：やわらかめ
    st = lp(st, f * (2.5 + 5 * bright), 2)
    a = min(0.8, dur * 0.35)
    env = np.clip(t / a, 0, 1) ** 1.5
    env *= np.where(t > dur, np.exp(-(t - dur) / 0.7), 1.0)
    return st * env[:, None] * vel * 0.16

def sub_bass(f, dur, vel=0.8):
    tail = 0.8
    n = int((dur + tail) * SR); t = np.arange(n) / SR
    y = np.sin(2 * np.pi * f * t) + 0.18 * np.sin(4 * np.pi * f * t)
    env = np.clip(t / 0.08, 0, 1) * np.where(t > dur, np.exp(-(t - dur) / 0.3), 1.0)
    return y * env * vel * 0.32

# ---------------- ミキサー ----------------
class Mix:
    def __init__(self, bpm, bars, beats_per_bar=4, tail=4.0, seed=7):
        self.bpm = bpm; self.beat = 60.0 / bpm
        self.length = bars * beats_per_bar * self.beat
        n = int((self.length + tail) * SR) + SR
        self.dry = np.zeros((n, 2)); self.send = np.zeros((n, 2))
        self.rng = np.random.default_rng(seed)

    def add(self, y, beat_pos, pan=0.0, rev=0.25, gain=1.0, humanize=0.008):
        # 演奏のゆらぎ：タイミングを少しずらす
        jitter = self.rng.normal(0, humanize) if humanize > 0 else 0.0
        i = max(0, int((beat_pos * self.beat + jitter) * SR))
        if y.ndim == 1:
            gl = np.sqrt(0.5 * (1 - pan)); gr = np.sqrt(0.5 * (1 + pan))
            y = np.stack([y * gl, y * gr], 1)
        y = y * gain
        m = min(len(y), len(self.dry) - i)
        if m <= 0: return
        self.dry[i:i + m] += y[:m]
        self.send[i:i + m] += y[:m] * rev

    def vel(self, v, spread=0.08):
        # 強弱のゆらぎ
        return float(np.clip(v * (1 + self.rng.normal(0, spread)), 0.05, 1.2))

def reverb_ir(rt60=2.2, predelay=0.018, damp=0.55, seed=3, size=1.0):
    """初期反射（数本の遅れ）＋後期残響（帯域ごとに減衰時間が違う：高い音ほど早く消える）。ステレオで左右無相関"""
    r = np.random.default_rng(seed)
    n = int((rt60 * 1.1 + predelay) * SR)
    t = np.arange(n) / SR
    ir = np.zeros((n, 2))
    # 後期残響：4つの帯域
    bands = [(20, 400, 1.0), (400, 1600, 0.85), (1600, 5000, 0.6 * (1.2 - damp)), (5000, 16000, 0.35 * (1.2 - damp))]
    for ch in range(2):
        nz = r.standard_normal(n)
        late = np.zeros(n)
        for lo, hi, k in bands:
            b = bp(nz, lo, hi, 2)
            tau = (rt60 * k) / 6.91
            late += b * np.exp(-np.maximum(t - predelay, 0) / tau)
        late[:int(predelay * SR)] = 0
        # 立ち上がりをなめらかに
        rise = np.clip((t - predelay) / (0.06 * size), 0, 1)
        ir[:, ch] = late * rise
        # 初期反射
        for j in range(9):
            d = predelay * 0.4 + r.uniform(0.004, 0.045) * size
            a = 0.55 * (0.8 ** j) * r.choice([-1, 1])
            idx = int(d * SR)
            if idx < n: ir[idx, ch] += a
    ir[:, 0] /= np.sqrt(np.sum(ir[:, 0] ** 2)); ir[:, 1] /= np.sqrt(np.sum(ir[:, 1] ** 2))
    return ir

def fftconv(x, h):
    N = 1 << int(np.ceil(np.log2(len(x) + len(h))))
    return np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(h, N), N)[:len(x)]

def compressor(st, thresh_db=-18, ratio=2.0, attack=0.02, release=0.25, makeup_db=0.0):
    # RMSを見てゆっくり音量をそろえる（左右リンク）
    lvl = np.sqrt(lp(np.mean(st ** 2, 1), 1.0 / (2 * np.pi * 0.03), 1) + 1e-12)
    db = 20 * np.log10(lvl + 1e-9)
    over = np.maximum(db - thresh_db, 0)
    gr = -over * (1 - 1 / ratio)
    # アタック・リリースでなめらかに
    # 64サンプルごとに計算（速さ優先）→ 間は1サンプルずつ直線でつなぐ（2026-10-07）。
    # 前は64サンプルごとに階段状にゲインが跳んでいて、オルゴール等のきれいな音にジリジリした「音割れ感」が乗っていた
    idx = np.arange(0, len(gr), 64); gk = np.zeros(len(idx)); g = 0.0
    a_a = np.exp(-1 / (attack * SR)); a_r = np.exp(-1 / (release * SR))
    for j, i in enumerate(idx):
        target = gr[i]
        a = a_a if target < g else a_r
        g = a ** 64 * g + (1 - a ** 64) * target
        gk[j] = g
    out = np.interp(np.arange(len(gr)), idx, gk)
    return st * (10 ** ((out + makeup_db) / 20))[:, None]

def limiter(st, ceiling=0.89, lookahead=0.005, release=0.08, smooth=True):
    """smooth=True（2026-10-07）：ゲインを先読みの長さで「なめらかに」下げる版。
    前の版はピークの直前で一気にガクッと下げていて、金属系の鋭いアタックで音割れっぽく聞こえていた"""
    if smooth: return limiter_smooth(st, ceiling, lookahead * 1.6, release * 1.5)
    peak = np.max(np.abs(st), 1)
    la = int(lookahead * SR)
    # 先読み：少し先までの最大値
    from numpy.lib.stride_tricks import sliding_window_view
    pad = np.concatenate([peak, np.zeros(la)])
    ahead = sliding_window_view(pad, la + 1).max(1)[:len(peak)]
    need = np.minimum(1.0, ceiling / np.maximum(ahead, 1e-9))
    g = np.empty_like(need); cur = 1.0; a_r = np.exp(-1 / (release * SR))
    for i in range(len(need)):
        cur = need[i] if need[i] < cur else a_r * cur + (1 - a_r) * need[i]
        g[i] = cur
    return st * g[:, None]

def limiter_smooth(st, ceiling=0.89, lookahead=0.008, release=0.12):
    from numpy.lib.stride_tricks import sliding_window_view
    peak = np.max(np.abs(st), 1)
    la = max(2, int(lookahead * SR))
    need = np.minimum(1.0, ceiling / np.maximum(peak, 1e-9))
    # 先読み区間の最小ゲイン → 同じ長さで移動平均すると、ピークの la サンプル前からなだらかに下がり、ピークでちょうど need 以下になる
    pad = np.concatenate([need, np.ones(la)])
    gmin = sliding_window_view(pad, la + 1).min(1)[:len(need)]
    # 直前 la+1 サンプルの平均：ピーク位置 p では gmin[p-la..p] がどれも need[p] 以下なので、平均も need[p] 以下になる
    gpad = np.concatenate([np.full(la, gmin[0]), gmin])
    cs = np.concatenate([[0.0], np.cumsum(gpad)])
    gs = (cs[la + 1:] - cs[:-la - 1]) / (la + 1)
    # 戻りはゆっくり（下げるのは即）
    out = np.empty_like(gs); cur = 1.0; a_r = np.exp(-1 / (release * SR))
    for i in range(len(gs)):
        cur = gs[i] if gs[i] < cur else a_r * cur + (1 - a_r) * gs[i]
        out[i] = cur
    y = st * out[:, None]
    return np.clip(y, -ceiling, ceiling)

def master(mix, rt60=2.2, rev_level=0.45, damp=0.55, lowcut=35, air_db=0.0, warmth_db=1.0, out_peak=0.89,
           comp=True, final_lp=None, comp_thresh=-14, comp_ratio=1.4, target_rms_db=None):
    ir = reverb_ir(rt60, damp=damp)
    wetL = fftconv(mix.send[:, 0], ir[:, 0]); wetR = fftconv(mix.send[:, 1], ir[:, 1])
    st = mix.dry + rev_level * np.stack([wetL, wetR], 1)
    n = int(mix.length * SR)
    # 余韻を頭に重ねる（シームレスループ）
    st[:len(st) - n] += st[n:]
    loop = st[:n].copy()
    # フィルタやコンプは「前の続き」の状態を持つので、ループを2周つなげて処理し、2周目だけ使う（つなぎ目でプツッとしない）
    st = np.concatenate([loop, loop])
    st = hp(st, lowcut, 2)
    if warmth_db: st = shelf(st, 220, warmth_db, 'low')
    if air_db: st = shelf(st, 6000, air_db, 'high')
    if final_lp: st = lp(st, final_lp, 2)
    st /= np.max(np.abs(st)) + 1e-9
    if comp: st = compressor(st, thresh_db=comp_thresh, ratio=comp_ratio, makeup_db=1.0)
    st = st / (np.max(np.abs(st)) + 1e-9) * 1.03
    if target_rms_db is not None:
        # 今の曲と同じくらいの聞こえる大きさにそろえる（リミッターで頭を抑えながら2回合わせる）
        for _ in range(2):
            cur = 20 * np.log10(np.sqrt(np.mean(limiter(st, ceiling=out_peak) ** 2)) + 1e-12)
            st = st * 10 ** ((target_rms_db - cur) / 20)
    st = limiter(st, ceiling=out_peak)
    return st[n:2 * n]

def save(st, path):
    with wave.open(path, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes())
    print(os.path.basename(path), f"{len(st)/SR:.1f}s", f"{os.path.getsize(path)/1e6:.1f}MB")
