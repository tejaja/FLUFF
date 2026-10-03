# コース4「まもののす」のBGM候補。楽器・ミキサーは make_bgm.py（＋ tower の道具）を使う。
# 舞台：空に浮かぶ小枝の巣、けだまのまもの（巣から飛び出す・突進）、イバラ、奥で寝ているママけだま
#   python make_bgm_den.py            → 3つとも
#   python make_bgm_den.py den_a      → 1つだけ
import sys
import numpy as np
from make_bgm import *
from make_bgm_tower import voicing, woodblock, swing, timpani

DORIAN = [0, 2, 3, 5, 7, 9, 10]

def bongo(hi=True, vel=1.0):
    return tom(420 if hi else 300, vel * 0.7)

def den_a():
    # A「けだまのすみか」：A短調（ドリア）104BPM。しのび足のスタッカート（ピチカート＋低いブラスを短く）、
    # ボンゴとウッドブロックのジャングルっぽいリズム、ときどきブラスの「ぶぉっ」（けだまが飛び出す感じ）
    s = Song(104, 16)
    prog = ['Am', 'Am', 'D', 'Am', 'Am', 'Am', 'Em', 'Am', 'F', 'G', 'Am', 'Am', 'F', 'G', 'E', 'E']
    kp = NOTE['A']
    rA = [[(0, 0.5), (1, 0.5), (1.5, 0.5), (2.5, 0.5), (3, 0.5)], [(0.5, 0.5), (1, 0.5), (2, 0.5), (3, 1)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (2, 0.5), (2.5, 0.5), (3, 0.5)], [(0, 1), (2, 0.5), (3, 0.5)]]
    a = make_melody(np.random.default_rng(301), prog[:4], kp, DORIAN, rA, 57, 74, 64)
    a2 = make_melody(np.random.default_rng(302), prog[4:8], kp, DORIAN, rA, 57, 74, a[-1][2])
    b = make_melody(np.random.default_rng(303), prog[8:12], kp, MINOR, rA, 59, 76, 67)
    c = make_melody(np.random.default_rng(304), prog[12:16], kp, [0, 2, 3, 5, 7, 8, 11], rA, 57, 76, 64)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    for pos, d, m in mel:
        s.add(pluck(midi_hz(m), 0.35 * s.beat, 0.9, bright=1.1), pos, pan=0.15, rev=0.2)
        s.add(brass(midi_hz(m - 12), 0.3 * s.beat, 0.45), pos, pan=-0.1, rev=0.2)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(33, [r], 28, 40)
        for beat in range(4):   # しのび足のベース（4分、2拍目と4拍目は半音上から寄る）
            m = root if beat % 2 == 0 else root + (7 if beat == 1 else 5)
            s.add(bass(midi_hz(m), 0.35 * s.beat, 0.9 if beat == 0 else 0.6, tone=0.4), bar * 4 + beat, rev=0.05)
        for off, hi in ((0, False), (0.75, True), (1.5, True), (2, False), (2.5, True), (3.25, True), (3.5, False)):
            s.add(bongo(hi, 0.8 if not hi else 0.55), bar * 4 + off, pan=-0.45 if hi else -0.2, rev=0.12)
        for k in range(4): s.add(woodblock(0.4, hi=True), bar * 4 + k + 0.5, pan=0.5, rev=0.1)
        s.add(kick(0.6), bar * 4, rev=0.05)
        if bar % 2 == 1:   # 「ぶぉっ」
            for m in voicing(cps, 50, 45, 57): s.add(brass(midi_hz(m), 0.8 * s.beat, 0.5), bar * 4 + 3, pan=0, rev=0.3)
        for m in voicing(cps, 57, 52, 64): s.add(pad(midi_hz(m), 4 * s.beat, 0.35, bright=2), bar * 4, pan=0.3, rev=0.4)
    save(s.render(1.2), "bgm_den_a.wav")

def den_b():
    # B「そらの巣めぐり」：G長調→ちょっとだけ短調、6/8っぽくはねる 120BPM（3連）。口笛＋オルゴールのメロディ、
    # かき鳴らすプラック、手拍子とタンバリン（シェイカー）。巣から巣へぴょんぴょん渡る冒険
    s = Song(120, 16)
    prog = ['G', 'Em', 'C', 'D', 'G', 'Em', 'Am', 'D', 'Em', 'C', 'G', 'D', 'C', 'D', 'G', 'G']
    kp = NOTE['G']
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 3), (3, 1)]]
    a = make_melody(np.random.default_rng(311), prog[:4], kp, MAJOR, rA, 67, 84, 71)
    a2 = make_melody(np.random.default_rng(312), prog[4:8], kp, MAJOR, rA, 67, 84, a[-1][2])
    b = make_melody(np.random.default_rng(313), prog[8:12], kp, MAJOR, rA, 67, 86, 76)
    c = make_melody(np.random.default_rng(314), prog[12:16], kp, MAJOR, rA[:3] + [[(0, 4)]], 67, 84, 74)
    mel = end_on_root(a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12), kp, 67, 84)
    for pos, d, m in mel:
        p = swing(pos)
        s.add(whistle(midi_hz(m), d * s.beat * 0.9, 0.9), p, pan=0.1, rev=0.3)
        s.add(music_box(midi_hz(m), d * s.beat, 0.35), p, pan=-0.3, rev=0.3)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(43, [r], 38, 49)
        vo = voicing(cps, 62, 57, 69)
        for beat in range(4):   # かき鳴らし（3連のはね）
            for j, m in enumerate(vo): s.add(pluck(midi_hz(m), 0.4 * s.beat, 0.4, bright=1.4), bar * 4 + beat + j * 0.012, pan=-0.35, rev=0.2)
            for j, m in enumerate(vo): s.add(pluck(midi_hz(m), 0.25 * s.beat, 0.25, bright=1.4), bar * 4 + beat + 2.0 / 3.0 + j * 0.012, pan=-0.35, rev=0.2)
        for beat in (0, 2): s.add(bass(midi_hz(root), 0.9 * s.beat, 0.85, tone=0.45), bar * 4 + beat, rev=0.05)
        for beat in (1, 3): s.add(bass(midi_hz(root + 7), 0.6 * s.beat, 0.6, tone=0.45), bar * 4 + beat, rev=0.05)
        for beat in (1, 3): s.add(snare(0.4), bar * 4 + beat, pan=0.2, rev=0.3)   # 手拍子がわり
        for beat in range(4):
            s.add(shaker(0.6), bar * 4 + beat, pan=0.4, rev=0.1); s.add(shaker(0.4), bar * 4 + beat + 2.0 / 3.0, pan=0.4, rev=0.1)
        s.add(kick(0.55), bar * 4, rev=0.05); s.add(kick(0.4), bar * 4 + 2, rev=0.05)
    save(s.render(1.3), "bgm_den_b.wav")

def den_c():
    # C「ママのねむる巣」：E短調 96BPM。マリンバの8分のオスティナート（ゆりかごっぽいけど不穏）、
    # 低いブラスの重いテーマ、ティンパニとタムのどっしりしたリズム。だんだん楽器が増えて盛り上がる（ボス前の緊張感）
    s = Song(96, 16)
    prog = ['Em', 'C', 'Am', 'B', 'Em', 'C', 'Am', 'B', 'C', 'D', 'Em', 'Em', 'C', 'D', 'B', 'B']
    kp = NOTE['E']
    rA = [[(0, 1.5), (1.5, 0.5), (2, 2)], [(0, 1), (1, 1), (2, 1.5), (3.5, 0.5)], [(0, 2), (2, 1), (3, 1)], [(0, 4)]]
    a = make_melody(np.random.default_rng(321), prog[:4], kp, [0, 2, 3, 5, 7, 8, 11], rA, 52, 67, 59)
    a2 = make_melody(np.random.default_rng(322), prog[4:8], kp, [0, 2, 3, 5, 7, 8, 11], rA, 52, 67, a[-1][2])
    b = make_melody(np.random.default_rng(323), prog[8:12], kp, MINOR, rA, 55, 71, 62)
    c = make_melody(np.random.default_rng(324), prog[12:16], kp, [0, 2, 3, 5, 7, 8, 11], rA, 55, 71, 64)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    for pos, d, m in mel:
        s.add(brass(midi_hz(m), d * s.beat * 0.95, 0.9), pos, pan=0.05, rev=0.35)
        if pos >= 32: s.add(brass(midi_hz(m + 12), d * s.beat * 0.95, 0.4), pos, pan=-0.2, rev=0.35)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        arp = voicing(cps, 64, 59, 71)
        seq = [arp[0], arp[1], arp[2], arp[1], arp[0] + 12, arp[2], arp[1], arp[2]]
        for k in range(8): s.add(marimba(midi_hz(seq[k]), 0.45 * s.beat, 0.55 if k % 2 == 0 else 0.4), bar * 4 + k * 0.5, pan=0.3, rev=0.25)
        root = nearest(40, [r], 35, 46)
        s.add(bass(midi_hz(root), 1.9 * s.beat, 0.8, tone=0.35), bar * 4, rev=0.1); s.add(bass(midi_hz(root), 1.9 * s.beat, 0.6, tone=0.35), bar * 4 + 2, rev=0.1)
        for m in voicing(cps, 55, 50, 62): s.add(pad(midi_hz(m), 4 * s.beat, 0.55, bright=3), bar * 4, pan=-0.2, rev=0.45)
        s.add(timpani(midi_hz(nearest(40, [r], 35, 46)), 0.9), bar * 4, rev=0.3)
        if bar >= 4:
            s.add(tom(110, 0.6), bar * 4 + 1.5, pan=-0.3, rev=0.2); s.add(tom(90, 0.7), bar * 4 + 2.5, pan=0.3, rev=0.2)
        if bar >= 8:
            s.add(kick(0.7), bar * 4 + 2, rev=0.05); s.add(snare(0.6), bar * 4 + 3, rev=0.3)
        if bar % 4 == 3:
            for k in range(4): s.add(timpani(midi_hz(nearest(40, [r], 35, 46)), 0.4 + 0.15 * k), bar * 4 + 2 + k * 0.5, rev=0.3)
        if bar % 8 == 0 and bar > 0: s.add(crash(0.6), bar * 4, rev=0.3)
    save(s.render(1.6), "bgm_den_c.wav")

# ---------- 野生っぽい楽器 ----------
PENTA_MIN = [0, 3, 5, 7, 10]

def djembe(kind, vel=1.0):
    # ジャンベ：'b'=低いドン、't'=トン、's'=パシッ（スラップ）
    n = int(0.35 * SR); t = np.arange(n) / SR
    if kind == 'b':
        ff = 75 * (1 + 0.6 * np.exp(-t / 0.02)); y = np.sin(2 * np.pi * np.cumsum(ff) / SR) * np.exp(-t / 0.2)
    elif kind == 't':
        ff = 210 * (1 + 0.3 * np.exp(-t / 0.015)); y = np.sin(2 * np.pi * np.cumsum(ff) / SR) * np.exp(-t / 0.09) + 0.2 * noise(n) * np.exp(-t / 0.01)
    else:
        y = 0.7 * noise(n) * np.exp(-t / 0.025) + 0.4 * np.sin(2 * np.pi * 380 * t) * np.exp(-t / 0.04)
    return y * vel * 0.5

def log_drum(f, vel=1.0):
    # 木をくりぬいた太鼓（低いマリンバ＋木のコツッ）
    n = int(0.6 * SR); t = np.arange(n) / SR
    y = np.sin(2 * np.pi * f * t) * np.exp(-t / 0.22) + 0.35 * np.sin(2 * np.pi * f * 3.9 * t) * np.exp(-t / 0.04)
    y += 0.15 * noise(n) * np.exp(-t / 0.006)
    return y * vel * 0.5

def voice_hoo(f, dur, vel=1.0, vowel='o'):
    # 「ほー」「うー」「あー」っぽい声（倍音をフォルマントで重みづけ）
    F = {'o': [(450, 1.0), (800, 0.5), (2830, 0.08)], 'u': [(325, 1.0), (700, 0.3), (2530, 0.05)], 'a': [(730, 1.0), (1090, 0.6), (2440, 0.12)]}[vowel]
    parts = []
    k = 1
    while f * k < 4000:
        w = sum(a * np.exp(-((f * k - fc) / 130) ** 2) for fc, a in F) / k ** 0.3
        parts.append((k, w)); k += 1
    y = additive(f, dur, parts, [None] * len(parts), tail=0.2, vib=0.012, vib_rate=5.2)
    e = env_adsr(len(y), 0.06, 0.1, 0.85, 0.2, dur)
    return y * e * vel * 0.18

def bird(vel=1.0, kind=0):
    # 鳥の鳴き声：上がって下がるピッという声のくり返し
    rng = np.random.default_rng(kind + 9)
    out = np.zeros(int(0.9 * SR)); pos = 0
    for i in range(rng.integers(2, 5)):
        n = int(rng.uniform(0.05, 0.11) * SR); t = np.arange(n) / SR
        f0 = rng.uniform(2200, 3400); sweep = rng.uniform(-1400, 1600)
        ff = f0 + sweep * np.sin(np.pi * t / t[-1])
        y = np.sin(2 * np.pi * np.cumsum(ff) / SR) * np.sin(np.pi * t / t[-1]) ** 2
        if pos + n > len(out): break
        out[pos:pos + n] += y; pos += n + int(rng.uniform(0.02, 0.06) * SR)
    return out * vel * 0.12

def monkey(vel=1.0):
    # 「ウッ、ウッ、アーッ」っぽい鳴き声（声のピッチを大きくすべらせる）
    pieces = []
    for f0, f1, d in ((300, 420, 0.12), (320, 450, 0.12), (500, 900, 0.35)):
        n = int(d * SR); t = np.arange(n) / SR
        ff = f0 + (f1 - f0) * (t / d)
        ph = 2 * np.pi * np.cumsum(ff) / SR
        y = sum((1 / k) * np.sin(ph * k) for k in range(1, 9))
        pieces.append(y * np.sin(np.pi * t / d) ** 0.5); pieces.append(np.zeros(int(0.05 * SR)))
    return np.concatenate(pieces) * vel * 0.07

def didgeridoo(f, dur, vel=1.0):
    # ぶおーんとうなる低い管（低音＋ゆっくり動く「わうわう」）
    n = int((dur + 0.4) * SR); t = np.arange(n) / SR
    ph = 2 * np.pi * f * t
    wah = 0.5 + 0.5 * np.sin(2 * np.pi * 1.6 * t)
    y = sum((1 / k) * np.sin(ph * k) * (1 + wah * (1 if k in (3, 4, 5) else 0)) for k in range(1, 12))
    e = env_adsr(n, 0.1, 0.1, 0.9, 0.3, dur)
    return y * e * vel * 0.08

def horn_call(f, dur, vel=1.0):
    # 狩りの角笛っぽい（ブラスを少しかすれさせる）
    y = brass(f, dur, vel)
    nz = noise(len(y)); nz = np.convolve(nz, np.ones(20) / 20, mode='same')
    return y + 0.05 * nz * np.abs(y).max()

def kalimba(f, dur, vel=1.0):
    y = additive(f, dur, [(1, 1.0), (5.4, 0.15), (2.0, 0.08)], [0.9, 0.08, 0.3], tail=0.8)
    t = np.arange(len(y)) / SR
    return y * np.clip(t / 0.004, 0, 1) * vel * 0.4

def den_d():
    # D「ジャングルのたいこ」（野生）：A短調ペンタ 112BPM。ジャンベ2台のずれたリズム、木の太鼓のメロディ、
    # 「ほー」の声の掛け合い、鳥とサルの鳴き声、ディジュリドゥのうなり
    s = Song(112, 16)
    kp = NOTE['A']
    prog = ['Am', 'Am', 'G', 'Am', 'Am', 'Am', 'G', 'Em', 'C', 'G', 'Am', 'Am', 'C', 'G', 'Em', 'Am']
    rng = np.random.default_rng(401)
    rA = [[(0, 0.5), (0.75, 0.25), (1, 0.5), (2, 0.5), (2.75, 0.25), (3, 0.5)], [(0, 0.5), (0.5, 0.5), (1.5, 0.5), (2, 1), (3, 0.5), (3.5, 0.5)],
          [(0, 0.75), (0.75, 0.25), (1, 0.5), (1.5, 0.5), (2, 0.5), (3, 1)], [(0, 1), (1.5, 0.5), (2, 1), (3, 0.5)]]
    a = make_melody(np.random.default_rng(402), prog[:4], kp, PENTA_MIN, rA, 57, 72, 64)
    b = make_melody(np.random.default_rng(403), prog[8:12], kp, PENTA_MIN, rA, 57, 74, 67)
    mel = a + transpose_rest(a, 4) + transpose_rest(b, 8) + transpose_rest(a, 12)
    for pos, d, m in mel:
        s.add(log_drum(midi_hz(m), 0.9), pos, pan=0.2, rev=0.2)
        s.add(log_drum(midi_hz(m - 12), 0.35), pos, pan=-0.2, rev=0.15)
    calls = [(57, 'o'), (60, 'o'), (64, 'a'), (62, 'o')]
    for bar, cn in enumerate(prog):
        b0 = bar * 4
        for off, k, v in ((0, 'b', 1.0), (0.75, 't', 0.6), (1, 's', 0.7), (1.5, 't', 0.6), (2, 'b', 0.9), (2.5, 's', 0.6), (2.75, 't', 0.5), (3, 's', 0.8), (3.5, 't', 0.6), (3.75, 't', 0.5)):
            s.add(djembe(k, v), b0 + off, pan=-0.3 if k != 'b' else 0, rev=0.12)
        for off in (0.5, 1.75, 3.25): s.add(djembe('t', 0.45), b0 + off, pan=0.45, rev=0.12)
        for k in range(8): s.add(shaker(0.55 if k % 2 else 0.35), b0 + k * 0.5, pan=0.5, rev=0.08)
        s.add(tom(70, 0.9), b0, rev=0.15)
        if bar % 4 == 0: s.add(didgeridoo(midi_hz(nearest(33, [kp], 28, 40)), 16 * s.beat, 1.0), b0, rev=0.2)
        if bar % 2 == 0 and bar >= 4:
            m, v = calls[(bar // 2) % 4]
            s.add(voice_hoo(midi_hz(m), 1.2 * s.beat, 0.9, v), b0 + 2, pan=-0.4, rev=0.4)
            s.add(voice_hoo(midi_hz(m + 5), 0.8 * s.beat, 0.7, 'o'), b0 + 3.5, pan=0.4, rev=0.4)
        if rng.random() < 0.5: s.add(bird(0.8, bar), b0 + rng.choice([0.5, 1.5, 2.5]), pan=rng.uniform(-0.9, 0.9), rev=0.5)
        if bar in (6, 14): s.add(monkey(0.9), b0 + 3, pan=0.6, rev=0.4)
    save(s.render(1.2), "bgm_den_d.wav")

def den_e():
    # E「けもののおどり」（野生＋かっこよく）：E短調ペンタ 128BPM。前に進む大太鼓、たたみかけるジャンベ、
    # 角笛の呼び声、ログドラムのリフ、「ハッ！」っぽい掛け声
    s = Song(128, 16)
    kp = NOTE['E']
    prog = ['Em', 'Em', 'D', 'Em', 'Em', 'Em', 'G', 'D', 'C', 'D', 'Em', 'Em', 'C', 'D', 'B', 'B']
    riff = [0, 0, 3, 0, 5, 0, 3, 7]
    rng = np.random.default_rng(411)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        b0 = bar * 4
        base = nearest(52, [r], 47, 58)
        for k in range(8): s.add(log_drum(midi_hz(base + riff[k]), 0.7 if k % 2 == 0 else 0.5), b0 + k * 0.5, pan=0.25, rev=0.15)
        for beat in range(4): s.add(tom(60, 1.0), b0 + beat, rev=0.1)
        for k in range(16):
            kind = 's' if k in (4, 12) else ('b' if k in (0, 8) else 't')
            s.add(djembe(kind, 0.55 if kind == 't' else 0.8), b0 + k * 0.25, pan=-0.35, rev=0.08)
        for k in range(8): s.add(shaker(0.5), b0 + k * 0.5 + 0.25, pan=0.5, rev=0.05)
        s.add(bass(midi_hz(nearest(40, [r], 35, 46)), 3.6 * s.beat, 0.7, tone=0.5), b0, rev=0.05)
        if bar % 4 == 0:
            root = nearest(64, [kp], 59, 70)
            for off, m, d in ((0, root, 0.75), (1, root + 7, 0.75), (2, root + 12, 1.8)):
                s.add(horn_call(midi_hz(m), d * s.beat, 0.8), b0 + off, pan=0.1, rev=0.45)
        if bar % 4 == 2:
            for off in (1, 3): s.add(voice_hoo(midi_hz(nearest(57, [kp], 52, 64)), 0.2 * s.beat, 1.0, 'a'), b0 + off, pan=rng.uniform(-0.5, 0.5), rev=0.3)
        if bar % 8 == 7:
            for k, f in enumerate((180, 150, 120, 100, 80, 65)): s.add(tom(f, 0.9), b0 + 2.5 + k * 0.25, pan=0.5 - 0.2 * k, rev=0.2)
        if rng.random() < 0.35: s.add(bird(0.7, bar + 30), b0 + 1.5, pan=rng.uniform(-0.9, 0.9), rev=0.5)
    save(s.render(1.0), "bgm_den_e.wav")

def den_f():
    # F「よるの密林」（野生＋あやしい）：D短調ペンタ 96BPM。カリンバのぽろぽろメロディ、ディジュリドゥ、
    # 虫やカエルのような「ころころ」、まばらで深い太鼓
    s = Song(96, 16)
    kp = NOTE['D']
    prog = ['Dm', 'Dm', 'C', 'Dm', 'Dm', 'Dm', 'Am', 'Dm', 'Bb', 'C', 'Dm', 'Dm', 'Bb', 'C', 'Am', 'Am']
    rng = np.random.default_rng(421)
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 0.5), (2.5, 1.5)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (2, 1), (3, 1)], [(0, 2), (2.5, 1.5)]]
    a = make_melody(np.random.default_rng(422), prog[:4], kp, PENTA_MIN, rA, 62, 79, 69)
    b = make_melody(np.random.default_rng(423), prog[8:12], kp, PENTA_MIN, rA, 64, 81, 72)
    mel = a + transpose_rest(a, 4) + transpose_rest(b, 8) + transpose_rest(a, 12)
    for pos, d, m in mel:
        s.add(kalimba(midi_hz(m), d * s.beat, 0.9), pos, pan=0.2, rev=0.35)
        if rng.random() < 0.3: s.add(kalimba(midi_hz(m + 12), 0.3 * s.beat, 0.3), pos + 0.25, pan=-0.4, rev=0.4)
    for bar, cn in enumerate(prog):
        b0 = bar * 4
        if bar % 4 == 0: s.add(didgeridoo(midi_hz(nearest(38, [kp], 33, 44)), 16 * s.beat, 1.1), b0, rev=0.25)
        s.add(tom(65, 0.9), b0, rev=0.25); s.add(tom(65, 0.6), b0 + 2.5, rev=0.25)
        for off in (1, 3): s.add(djembe('t', 0.4), b0 + off, pan=-0.4, rev=0.2)
        for off in (1.75, 3.75): s.add(djembe('s', 0.3), b0 + off, pan=0.4, rev=0.2)
        for k in range(rng.integers(3, 7)):
            f = rng.uniform(900, 1600); st = rng.uniform(0, 3.4)
            for j in range(rng.integers(3, 6)):
                n = int(0.03 * SR); t = np.arange(n) / SR
                y = np.sin(2 * np.pi * f * t) * np.sin(np.pi * t / t[-1]) * 0.05
                s.add(y, b0 + st + j * 0.12, pan=rng.uniform(-0.9, 0.9), rev=0.4)
        if rng.random() < 0.3: s.add(bird(0.6, bar + 60), b0 + 2, pan=rng.uniform(-0.9, 0.9), rev=0.6)
        if bar in (7, 15): s.add(voice_hoo(midi_hz(nearest(57, [kp], 52, 64)), 2 * s.beat, 0.6, 'u'), b0 + 1, pan=-0.5, rev=0.6)
    save(s.render(1.8), "bgm_den_f.wav")

if __name__ == '__main__':
    for w in (sys.argv[1:] or ['den_a', 'den_b', 'den_c']): globals()[w]()   # 野生系：den_d den_e den_f
