# コース3「やみのもり」のBGM候補。楽器・ミキサーは make_bgm.py（＋ make_bgm_tower.py のウッドブロック等）を使う。
# 方向性：暗い紫の森・クモの巣・とろけるヒトヨタケ・重力。こわすぎない「ふしぎ・ちょっと不気味・でもかわいい」
#   python make_bgm_yami.py            → 3つとも
#   python make_bgm_yami.py yami_a     → 1つだけ
import sys
import numpy as np
from make_bgm import *            # SR, Song, save, 楽器, 作曲まわり
from make_bgm_tower import voicing, woodblock, swing

HMINOR = [0, 2, 3, 5, 7, 8, 11]   # 和声的短音階（導音でちょっと不気味）

def drip(f, vel=1.0):
    # 「ぽちょん」：高い音から少し下がるしずく
    n = int(0.35 * SR); t = np.arange(n) / SR
    ff = f * (1 + 0.6 * np.exp(-t / 0.02))
    y = np.sin(2 * np.pi * np.cumsum(ff) / SR) * np.exp(-t / 0.09)
    return y * vel * 0.2

def low_drone(f, dur, vel=1.0):
    # 低い持続音（ゆっくりうなる）
    n = int((dur + 1.0) * SR); t = np.arange(n) / SR
    y = np.sin(2 * np.pi * f * t) + 0.5 * np.sin(2 * np.pi * f * 1.003 * 2 * t) + 0.25 * np.sin(2 * np.pi * f * 3 * t)
    e = env_adsr(n, dur * 0.3, 0.2, 0.9, 1.0, dur)
    return y * e * vel * 0.12

def yami_a():
    # A「夜の森のオルゴール」：E短調（和声的短音階）84BPM、3連のゆれるリズム。チェレスタのメロディ、
    # ピチカートの歩くベース、低いうなり、ときどき「ぽちょん」（ヒトヨタケのしずく）
    s = Song(84, 16)
    prog = ['Em', 'C', 'Am', 'B7', 'Em', 'C', 'Am', 'B7', 'Em', 'G', 'C', 'B7', 'Am', 'Em', 'B7', 'Em']
    kp = NOTE['E']
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5), (3.5, 0.5)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 1), (2, 1), (3, 1)], [(0, 3), (3, 1)]]
    a = make_melody(np.random.default_rng(201), prog[:4], kp, HMINOR, rA, 64, 81, 71)
    a2 = make_melody(np.random.default_rng(202), prog[4:8], kp, HMINOR, rA, 64, 81, a[-1][2])
    b = make_melody(np.random.default_rng(203), prog[8:12], kp, HMINOR, rA, 66, 83, 74)
    c = make_melody(np.random.default_rng(204), prog[12:16], kp, HMINOR, rA[:3] + [[(0, 4)]], 64, 81, 72)
    mel = end_on_root(a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12), kp, 64, 81)
    for pos, d, m in mel:
        p = swing(pos)
        s.add(celesta(midi_hz(m), d * s.beat, 1.0), p, pan=0.15, rev=0.45)
        s.add(felt(midi_hz(m - 12), d * s.beat, 0.3), p, pan=-0.2, rev=0.35)
    rng = np.random.default_rng(205)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(40, [r], 35, 46)
        fifth = root + 7
        nxt = chord(prog[(bar + 1) % len(prog)])[0]
        nroot = nearest(root, [nxt], root - 6, root + 6)
        walk = [root, fifth, root + 12 if root + 12 <= 52 else fifth, nroot + (1 if nroot < root else -1)]
        for beat in range(4):   # 歩くピチカート（最後は次のコードへ半音で近づく）
            s.add(pluck(midi_hz(walk[beat]), 0.6 * s.beat, 0.75 if beat == 0 else 0.55, bright=0.7), bar * 4 + beat, pan=-0.25, rev=0.2)
        for m in voicing(cps, 59, 54, 66):
            s.add(pad(midi_hz(m), 4 * s.beat, 0.45, bright=2), bar * 4, pan=0.25, rev=0.5)
        if bar % 4 == 0: s.add(low_drone(midi_hz(nearest(28, [kp], 24, 35)), 16 * s.beat, 1.0), bar * 4, rev=0.3)
        for beat in range(4):   # 3連でゆれるシェイカー
            s.add(shaker(0.55), bar * 4 + beat, pan=0.35, rev=0.1)
            s.add(shaker(0.35), bar * 4 + beat + 2.0 / 3.0, pan=0.35, rev=0.1)
        s.add(kick(0.45), bar * 4, rev=0.1)
        for k in range(2):   # ぽちょん
            if rng.random() < 0.6:
                pos = bar * 4 + rng.choice([0.667, 1.333, 2.667, 3.333])
                s.add(drip(midi_hz(nearest(88, cps, 84, 96)), 0.9), pos, pan=rng.uniform(-0.7, 0.7), rev=0.5)
    save(s.render(2.0), "bgm_yami_a.wav")

def yami_b():
    # B「ふしぎなキノコ」：D短調 70BPM、ゆったり。フェルトピアノのぽつぽつしたメロディ、ふわーっとしたパッドと息、
    # しずくの音が音階でぽちょぽちょ、心臓みたいな低いキック。打楽器はほぼなし（ぼんやり、ふしぎ）
    s = Song(70, 16)
    prog = ['Dm', 'Bb', 'Gm', 'A', 'Dm', 'Bb', 'Gm', 'A', 'Bb', 'C', 'Am', 'Dm', 'Gm', 'Bb', 'A', 'A']
    kp = NOTE['D']
    rA = [[(0, 2), (2, 1), (3, 1)], [(0, 3), (3, 1)], [(0, 1), (1, 1), (2, 2)], [(0, 4)]]
    a = make_melody(np.random.default_rng(211), prog[:4], kp, HMINOR, rA, 62, 79, 69)
    a2 = make_melody(np.random.default_rng(212), prog[4:8], kp, HMINOR, rA, 62, 79, a[-1][2])
    b = make_melody(np.random.default_rng(213), prog[8:12], kp, MINOR, rA, 64, 81, 72)
    c = make_melody(np.random.default_rng(214), prog[12:16], kp, HMINOR, rA, 62, 79, 70)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    for pos, d, m in mel:
        s.add(felt(midi_hz(m), d * s.beat, 1.0), pos, pan=0.1, rev=0.5)
        s.add(celesta(midi_hz(m + 12), d * s.beat, 0.25), pos + 0.02, pan=-0.3, rev=0.55)
    rng = np.random.default_rng(215)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        for m in voicing(cps, 57, 52, 64):
            s.add(pad(midi_hz(m), 4 * s.beat, 0.6, bright=2), bar * 4, pan=-0.15, rev=0.55)
        s.add(bass(midi_hz(nearest(38, [r], 33, 44)), 3.5 * s.beat, 0.6, tone=0.3), bar * 4, rev=0.15)
        if bar % 2 == 0: s.add(airy(6 * s.beat, 0.7), bar * 4, pan=rng.uniform(-0.5, 0.5), rev=0.4)
        s.add(kick(0.4), bar * 4, rev=0.15); s.add(kick(0.25), bar * 4 + 0.5, rev=0.15)   # どっ・くん
        sc = scale_for(kp, HMINOR, cps)
        for k in range(4):   # しずくの音階
            if rng.random() < 0.55:
                m = nearest(int(rng.integers(84, 96)), sc, 82, 98)
                s.add(drip(midi_hz(m), 0.8), bar * 4 + k + rng.choice([0, 0.5]), pan=rng.uniform(-0.8, 0.8), rev=0.6)
    save(s.render(2.4), "bgm_yami_b.wav")

def tuba(f, dur, vel=1.0):
    # ぼわっとした低いチューバ（ブラスを低く丸く）
    x = brass(f, dur, vel) * 0.9; y = bass(f, dur, vel * 0.5, tone=0.25)
    n = max(len(x), len(y)); out = np.zeros(n); out[:len(x)] += x; out[:len(y)] += y
    return out

def yami_c():
    # C「おばけのマーチ」：G短調 108BPM。ずんちゃっのチューバ＋ピチカートの和音、ファゴットっぽい低いブラスのメロディ、
    # ウッドブロックのカタカタ、オルゴールの合いの手。不気味だけどコミカル（ちょっとハロウィン）
    s = Song(108, 16)
    prog = ['Gm', 'D', 'Gm', 'D', 'Cm', 'Gm', 'D', 'Gm', 'Eb', 'D', 'Cm', 'D', 'Gm', 'Cm', 'D', 'Gm']
    kp = NOTE['G']
    rA = [[(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 1), (1, 0.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 2), (2, 2)]]
    a = make_melody(np.random.default_rng(221), prog[:4], kp, HMINOR, rA, 55, 72, 62)
    a2 = make_melody(np.random.default_rng(222), prog[4:8], kp, HMINOR, rA, 55, 72, a[-1][2])
    b = make_melody(np.random.default_rng(223), prog[8:12], kp, HMINOR, rA, 57, 74, 65)
    c = make_melody(np.random.default_rng(224), prog[12:16], kp, HMINOR, rA, 55, 72, 62)
    mel = end_on_root(a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12), kp, 55, 72)
    for pos, d, m in mel:
        s.add(brass(midi_hz(m), d * s.beat * 0.6, 0.9), pos, pan=0.1, rev=0.25)   # 短く切る＝ファゴットっぽく
        s.add(music_box(midi_hz(m + 24), d * s.beat, 0.22), pos, pan=-0.35, rev=0.35)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(31, [r], 26, 37)
        vo = voicing(cps, 62, 57, 69)
        for beat in (0, 2): s.add(tuba(midi_hz(root + (7 if beat == 2 and bar % 2 else 0)), 0.6 * s.beat, 0.9), bar * 4 + beat, pan=-0.1, rev=0.15)
        for beat in (1, 3):
            for m in vo: s.add(pluck(midi_hz(m), 0.3 * s.beat, 0.32, bright=1.0), bar * 4 + beat, pan=0.3, rev=0.2)
        for k in range(8): s.add(woodblock(0.55 if k % 2 == 0 else 0.35, hi=(k % 4 != 2)), bar * 4 + k * 0.5, pan=-0.4 if k % 2 else 0.4, rev=0.15)
        s.add(kick(0.6), bar * 4, rev=0.05); s.add(snare(0.45), bar * 4 + 2, rev=0.3)
        if bar % 4 == 3:
            for k in range(4): s.add(snare(0.2 + 0.08 * k), bar * 4 + 3 + k * 0.25, rev=0.25)
    save(s.render(1.4), "bgm_yami_c.wav")

if __name__ == '__main__':
    for w in (sys.argv[1:] or ['yami_a', 'yami_b', 'yami_c']): globals()[w]()
