# コース5「キノコのとう」のBGM候補（緊迫系）を3つ作る。楽器・ミキサーは make_bgm.py のものを使う。
#   python make_bgm_tower.py            → 3つとも
#   python make_bgm_tower.py tower_a    → 1つだけ
import sys
import numpy as np
from make_bgm import *   # SR, Song, save, 楽器, 作曲まわり

def voicing(cps, center, lo, hi):
    return sorted(nearest(center + (p - center) % 12, [p], lo, hi) for p in cps)

def tower_a():
    # A：駆け上がり。B短調 136BPM。16分で刻むプラックのオスティナート＋8分のベース、
    # ブラスのメロディ、4小節ごとにタムで駆け上がる。後半はメロディを1オクターブ上に重ねて高まる
    s = Song(136, 24)
    prog = ['Bm', 'G', 'A', 'F#'] * 2 + ['Em', 'F#', 'G', 'A', 'Bm', 'G', 'A', 'F#'] + ['Bm', 'G', 'A', 'F#'] * 2
    kp = NOTE['B']
    rA = [[(0, 1), (1, 0.5), (1.5, 1), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)],
          [(0, 0.5), (0.5, 0.5), (1, 1), (2, 0.5), (2.5, 1.5)], [(0, 3), (3, 0.5), (3.5, 0.5)]]
    rB = [[(0, 2), (2, 1), (3, 1)], [(0, 1), (1, 1), (2, 2)], [(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 4)]]
    a = make_melody(np.random.default_rng(61), prog[:4], kp, MINOR, rA, 62, 78, 66)
    b = make_melody(np.random.default_rng(62), prog[8:12], kp, MINOR, rB, 64, 79, 67)
    b2 = make_melody(np.random.default_rng(63), prog[12:16], kp, MINOR, rB, 64, 81, 71)
    mel = a + transpose_rest(a, 4) + transpose_rest(b, 8) + transpose_rest(b2, 12) + transpose_rest(a, 16) + transpose_rest(a, 20)
    for pos, d, m in mel:
        s.add(brass(midi_hz(m), d * s.beat * 0.92, 1.0), pos, pan=0.1, rev=0.3)
        s.add(marimba(midi_hz(m + 12), d * s.beat, 0.35), pos, pan=-0.25, rev=0.25)
        if pos >= 16:
            s.add(brass(midi_hz(m + 12), d * s.beat * 0.92, 0.45), pos, pan=-0.15, rev=0.35)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(40, [r], 35, 46)
        for k in range(8):
            s.add(bass(midi_hz(root), 0.4 * s.beat, 1.0 if k % 2 == 0 else 0.75, tone=0.55), bar * 4 + k * 0.5, rev=0.05)
        arp = voicing(cps, 66, 62, 74)
        seq = [arp[0], arp[2], arp[1], arp[2], arp[0] + 12, arp[2], arp[1], arp[2]]
        for k in range(16):
            s.add(pluck(midi_hz(seq[k % 8]), 0.2 * s.beat, 0.32, bright=1.3), bar * 4 + k * 0.25, pan=0.45 if k % 2 else -0.45, rev=0.2)
        for m in voicing(cps, 57, 52, 64):
            s.add(pad(midi_hz(m), 4 * s.beat, 0.7, bright=4), bar * 4, pan=0, rev=0.4)
        for beat in range(4): s.add(kick(0.9), bar * 4 + beat, rev=0.05)
        for beat in (1, 3): s.add(snare(0.85), bar * 4 + beat, rev=0.25)
        for k in range(8): s.add(hat(0.8 if k % 2 else 0.45), bar * 4 + k * 0.5, pan=0.3, rev=0.05)
        if bar % 4 == 3:   # 駆け上がるタム
            for k, f in enumerate((80, 95, 110, 130, 150, 175)):
                s.add(tom(f, 0.75), bar * 4 + 2.5 + k * 0.25, pan=-0.5 + 0.2 * k, rev=0.25)
        if bar % 8 == 0: s.add(crash(0.8), bar * 4, pan=0.2, rev=0.3)
    save(s.render(1.3), "bgm_tower_a.wav")

def woodblock(vel=1.0, hi=True):
    n = int(0.12 * SR); t = np.arange(n) / SR
    f = 1250 if hi else 950
    y = np.sin(2 * np.pi * f * t) * np.exp(-t / 0.025) + 0.3 * np.sin(2 * np.pi * f * 2.7 * t) * np.exp(-t / 0.012)
    return y * vel * 0.18

def tower_b():
    # B：時計じかけの塔。D短調 118BPM。チクタク（ウッドブロック）と16分で脈打つ低音、
    # オルゴールのメロディ（見た目のかわいさは残しつつ不安げ）、8小節目と16小節目はスネアロールで盛り上げる
    s = Song(118, 16)
    prog = ['Dm', 'Bb', 'Gm', 'A', 'Dm', 'Bb', 'Gm', 'A', 'Bb', 'C', 'Am', 'Dm', 'Gm', 'Bb', 'A', 'A']
    kp = NOTE['D']
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 2), (2, 1), (3, 1)]]
    a = make_melody(np.random.default_rng(71), prog[:4], kp, MINOR, rA, 69, 86, 74)
    a2 = make_melody(np.random.default_rng(72), prog[4:8], kp, MINOR, rA, 69, 86, a[-1][2])
    b = make_melody(np.random.default_rng(73), prog[8:12], kp, MINOR, rA, 69, 88, 77)
    c = make_melody(np.random.default_rng(74), prog[12:16], kp, MINOR, rA[:3] + [[(0, 4)]], 69, 86, 76)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    for pos, d, m in mel:
        s.add(music_box(midi_hz(m), d * s.beat, 1.0), pos, pan=0.15, rev=0.35)
        s.add(pluck(midi_hz(m - 12), d * s.beat, 0.35, bright=1.0), pos, pan=-0.2, rev=0.2)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(38, [r], 33, 44)
        for k in range(16):
            s.add(bass(midi_hz(root + (12 if k % 4 == 3 else 0)), 0.2 * s.beat, 0.9 if k % 4 == 0 else 0.55, tone=0.45), bar * 4 + k * 0.25, rev=0.03)
        for m in voicing(cps, 60, 55, 67):
            s.add(pad(midi_hz(m), 4 * s.beat, 0.8, bright=3), bar * 4, pan=-0.2, rev=0.45)
        for k in range(8):   # チクタク
            s.add(woodblock(0.8 if k % 2 == 0 else 0.6, hi=(k % 2 == 0)), bar * 4 + k * 0.5, pan=-0.5 if k % 2 else 0.5, rev=0.15)
        s.add(kick(0.9, heavy=True), bar * 4, rev=0.1)
        s.add(kick(0.6, heavy=True), bar * 4 + 2.5, rev=0.1)
        s.add(snare(0.8), bar * 4 + 2, rev=0.35)
        if bar % 8 == 7:   # スネアロール
            for k in range(8): s.add(snare(0.25 + 0.08 * k), bar * 4 + 2 + k * 0.25, pan=0.1, rev=0.25)
        if bar % 8 == 0: s.add(crash(0.6), bar * 4, pan=-0.2, rev=0.3)
    save(s.render(1.6), "bgm_tower_b.wav")

def timpani(f, vel=1.0):
    n = int(1.0 * SR); t = np.arange(n) / SR
    ff = f * (1 + 0.15 * np.exp(-t / 0.05))
    y = np.sin(2 * np.pi * np.cumsum(ff) / SR) * np.exp(-t / 0.45) + 0.4 * np.sin(2 * np.pi * np.cumsum(ff * 1.5) / SR) * np.exp(-t / 0.25)
    y += 0.25 * noise(n) * np.exp(-t / 0.01)
    return y * vel * 0.55

def tower_c():
    # C：冒険サスペンス。G短調 104BPM。低い弦のギャロップ（タッ・タタ）で前へ前へ、
    # ティンパニ、ブラスの勇ましいメロディ。テンポは落ち着いてるけど止まらない緊張感
    s = Song(104, 16)
    prog = ['Gm', 'Eb', 'F', 'D', 'Gm', 'Eb', 'Cm', 'D', 'Eb', 'F', 'Gm', 'Gm', 'Cm', 'Eb', 'D', 'D']
    kp = NOTE['G']
    rA = [[(0, 1.5), (1.5, 0.5), (2, 1.5), (3.5, 0.5)], [(0, 1), (1, 1), (2, 2)],
          [(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 3), (3, 1)]]
    a = make_melody(np.random.default_rng(81), prog[:4], kp, MINOR, rA, 62, 77, 67)
    a2 = make_melody(np.random.default_rng(82), prog[4:8], kp, MINOR, rA, 62, 77, a[-1][2])
    b = make_melody(np.random.default_rng(83), prog[8:12], kp, MINOR, rA, 64, 79, 70)
    c = make_melody(np.random.default_rng(84), prog[12:16], kp, MINOR, rA[:3] + [[(0, 4)]], 62, 79, 72)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    for pos, d, m in mel:
        s.add(brass(midi_hz(m), d * s.beat * 0.95, 1.0), pos, pan=0.1, rev=0.35)
        s.add(brass(midi_hz(m - 12), d * s.beat * 0.95, 0.45), pos, pan=-0.1, rev=0.35)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(43, [r], 38, 49)
        for beat in range(4):   # ギャロップ：8分＋16分2つ
            for off, v in ((0, 1.0), (0.5, 0.7), (0.75, 0.75)):
                s.add(bass(midi_hz(root), 0.22 * s.beat, v, tone=0.6), bar * 4 + beat + off, rev=0.08)
                s.add(pluck(midi_hz(root + 12), 0.2 * s.beat, 0.3 * v, bright=0.8), bar * 4 + beat + off, pan=-0.35, rev=0.15)
        for m in voicing(cps, 62, 57, 69):
            s.add(pad(midi_hz(m), 4 * s.beat, 0.75, bright=5), bar * 4, pan=0.3, rev=0.45)
        s.add(timpani(midi_hz(nearest(38, [r], 33, 44)), 1.0), bar * 4, rev=0.3)
        if bar % 2 == 1: s.add(timpani(midi_hz(nearest(38, [r], 33, 44)), 0.7), bar * 4 + 2.5, rev=0.3)
        s.add(snare(0.7), bar * 4 + 1, rev=0.3); s.add(snare(0.7), bar * 4 + 3, rev=0.3)
        for k in range(8): s.add(hat(0.6 if k % 2 else 0.35), bar * 4 + k * 0.5, pan=0.3, rev=0.05)
        if bar % 4 == 3:
            for k in range(6): s.add(timpani(midi_hz(nearest(38, [r], 33, 44)), 0.35 + 0.1 * k), bar * 4 + 2.5 + k * 0.25, rev=0.25)
        if bar % 8 == 0: s.add(crash(0.7), bar * 4, rev=0.3)
    save(s.render(1.6), "bgm_tower_c.wav")

def tower_d():
    # D：ヒーローロック（明るい＋かっこいい）。E長調 152BPM。I-bVII-IV-V のロック進行、
    # パワーコードの刻み、矩形波リード＋ブラスの重ね、8分のベース、ロックのドラム
    s = Song(152, 24)
    prog = ['E', 'D', 'A', 'B'] * 2 + ['C#m', 'A', 'E', 'B', 'C#m', 'A', 'B', 'B'] + ['E', 'D', 'A', 'B'] * 2
    kp = NOTE['E']
    rA = [[(0, 0.75), (0.75, 0.75), (1.5, 0.5), (2, 1), (3, 0.5), (3.5, 0.5)], [(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 1), (2.5, 0.5), (3, 1)], [(0, 2.5), (3, 0.5), (3.5, 0.5)]]
    rB = [[(0, 2), (2, 1), (3, 1)], [(0, 1.5), (1.5, 1.5), (3, 1)], [(0, 1), (1, 1), (2, 1), (3, 1)], [(0, 4)]]
    mixo = [0, 2, 4, 5, 7, 9, 10]   # ミクソリディアン（Dのコードに合うように）
    a = make_melody(np.random.default_rng(91), prog[:4], kp, mixo, rA, 66, 83, 71)
    b = make_melody(np.random.default_rng(92), prog[8:12], kp, MAJOR, rB, 66, 85, 73)
    b2 = make_melody(np.random.default_rng(93), prog[12:16], kp, MAJOR, rB, 66, 85, 76)
    mel = a + transpose_rest(a, 4) + transpose_rest(b, 8) + transpose_rest(b2, 12) + transpose_rest(a, 16) + transpose_rest(a, 20)
    mel = end_on_root(mel, kp, 66, 83)
    for pos, d, m in mel:
        s.add(square_lead(midi_hz(m), d * s.beat * 0.92, 1.0), pos, pan=0.1, rev=0.25)
        s.add(brass(midi_hz(m), d * s.beat * 0.92, 0.55), pos, pan=-0.15, rev=0.3)
        if pos >= 16: s.add(square_lead(midi_hz(m + 12), d * s.beat * 0.92, 0.3), pos, pan=-0.3, rev=0.3)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(40, [r], 35, 46)
        for k in range(8):
            s.add(bass(midi_hz(root + (12 if k in (3, 7) else 0)), 0.4 * s.beat, 1.0 if k % 2 == 0 else 0.75, tone=0.65), bar * 4 + k * 0.5, rev=0.05)
        pr = nearest(52, [r], 47, 58)
        for off, d in ((0, 0.75), (0.75, 0.75), (1.5, 0.5), (2, 0.75), (2.75, 0.75), (3.5, 0.5)):
            s.add(power(midi_hz(pr), d * s.beat, 0.8), bar * 4 + off, pan=-0.4, rev=0.12)
            s.add(power(midi_hz(pr), d * s.beat, 0.8), bar * 4 + off + 0.02, pan=0.4, rev=0.12)
        for beat in (0, 1.5, 2, 2.75 if bar % 2 else 2): s.add(kick(1.0), bar * 4 + beat, rev=0.05)
        for beat in (1, 3): s.add(snare(1.0), bar * 4 + beat, rev=0.25)
        for k in range(8): s.add(hat(0.8 if k % 2 else 0.5, open_=(k == 7 and bar % 2 == 1)), bar * 4 + k * 0.5, pan=0.3, rev=0.05)
        if bar % 8 == 7:
            for k, f in enumerate((175, 150, 130, 110, 95, 80)): s.add(tom(f, 0.85), bar * 4 + 2.5 + k * 0.25, pan=0.5 - 0.2 * k, rev=0.25)
        if bar % 8 == 0: s.add(crash(0.9), bar * 4, rev=0.3)
    save(s.render(1.0), "bgm_tower_d.wav")

def tower_e():
    # E：スカイラン（明るい＋疾走）。A長調 144BPM。vi-IV-I-V のエモい進行、16分のきらきらアルペジオ、
    # 裏拍のシンセブラス、4つ打ち＋裏のオープンハット、マリンバ＋リードのメロディ
    s = Song(144, 24)
    prog = ['F#m', 'D', 'A', 'E'] * 2 + ['D', 'E', 'C#m', 'F#m', 'D', 'E', 'A', 'A'] + ['F#m', 'D', 'A', 'E'] * 2
    kp = NOTE['A']
    rA = [[(0, 1), (1, 0.5), (1.5, 1), (2.5, 0.5), (3, 1)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 1.5), (3.5, 0.5)],
          [(0, 1), (1, 0.5), (1.5, 0.5), (2, 0.5), (2.5, 1.5)], [(0, 1.5), (1.5, 0.5), (2, 2)]]
    a = make_melody(np.random.default_rng(101), prog[:4], kp, MAJOR, rA, 69, 86, 76)
    b = make_melody(np.random.default_rng(102), prog[8:12], kp, MAJOR, rA, 69, 88, 78)
    b2 = make_melody(np.random.default_rng(103), prog[12:16], kp, MAJOR, rA[:3] + [[(0, 4)]], 69, 88, 81)
    mel = a + transpose_rest(a, 4) + transpose_rest(b, 8) + transpose_rest(b2, 12) + transpose_rest(a, 16) + transpose_rest(a, 20)
    for pos, d, m in mel:
        s.add(marimba(midi_hz(m), d * s.beat, 0.9), pos, pan=0.15, rev=0.2)
        s.add(square_lead(midi_hz(m), d * s.beat * 0.9, 0.45), pos, pan=0.0, rev=0.2)
        if pos >= 16: s.add(pluck(midi_hz(m + 12), d * s.beat, 0.4, bright=2), pos, pan=-0.3, rev=0.25)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(45, [r], 40, 51)
        for k in range(8): s.add(bass(midi_hz(root + (12 if k % 2 else 0)), 0.38 * s.beat, 0.95 if k % 2 == 0 else 0.7, tone=0.5), bar * 4 + k * 0.5, rev=0.05)
        arp = voicing(cps, 69, 64, 76)
        seq = [arp[0], arp[1], arp[2], arp[0] + 12, arp[2], arp[1], arp[2], arp[0] + 12]
        for k in range(16):
            s.add(pluck(midi_hz(seq[k % 8]), 0.2 * s.beat, 0.28, bright=2.2), bar * 4 + k * 0.25, pan=0.45 if k % 2 else -0.45, rev=0.22)
        for off in (0.5, 1.5, 2.5, 3.5):   # 裏拍のシンセブラス
            for m in voicing(cps, 62, 57, 69): s.add(brass(midi_hz(m), 0.3 * s.beat, 0.45), bar * 4 + off, pan=-0.2, rev=0.25)
        for beat in range(4): s.add(kick(0.9), bar * 4 + beat, rev=0.05)
        for beat in (1, 3): s.add(snare(0.85), bar * 4 + beat, rev=0.25)
        for k in range(4): s.add(hat(0.7, open_=True), bar * 4 + k + 0.5, pan=0.3, rev=0.08)
        if bar % 8 == 0: s.add(crash(0.8), bar * 4, rev=0.3)
        if bar % 4 == 3:
            for k in range(8): s.add(snare(0.2 + 0.08 * k), bar * 4 + 2 + k * 0.25, rev=0.2)
    save(s.render(1.2), "bgm_tower_e.wav")

def tower_f():
    # F：大冒険のはじまり（ワクワク冒険）。D長調 126BPM。はずむピチカート（8分）＋ブラスのメロディ＋鉄琴の重ね、
    # ティンパニと行進のスネア、区切りでは bVI-bVII-I（Bb-C-D）の「勇者の進行」で盛り上げる
    s = Song(126, 16)
    prog = ['D', 'A', 'Bm', 'G', 'D', 'A', 'G', 'A', 'Bm', 'G', 'D', 'A', 'G', 'A', 'Bb', 'C']
    kp = NOTE['D']
    rA = [[(0, 1.5), (1.5, 0.5), (2, 1), (3, 0.5), (3.5, 0.5)], [(0, 1), (1, 1), (2, 1.5), (3.5, 0.5)],
          [(0, 0.5), (0.5, 0.5), (1, 1), (2, 1), (3, 1)], [(0, 3), (3, 0.5), (3.5, 0.5)]]
    a = make_melody(np.random.default_rng(111), prog[:4], kp, MAJOR, rA, 66, 83, 69)
    a2 = make_melody(np.random.default_rng(112), prog[4:8], kp, MAJOR, rA, 66, 83, a[-1][2])
    b = make_melody(np.random.default_rng(113), prog[8:12], kp, MAJOR, rA, 66, 85, 74)
    c = make_melody(np.random.default_rng(114), prog[12:16], kp, MAJOR, rA[:2] + [[(0, 2), (2, 2)], [(0, 2), (2, 2)]], 66, 85, 76)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    for pos, d, m in mel:
        s.add(brass(midi_hz(m), d * s.beat * 0.92, 1.0), pos, pan=0.1, rev=0.3)
        s.add(music_box(midi_hz(m + 12), d * s.beat, 0.35), pos, pan=-0.3, rev=0.3)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(38, [r], 33, 45)
        vo = voicing(cps, 62, 57, 69)
        for k in range(8):   # はずむピチカート：低音と和音を交互に
            if k % 2 == 0: s.add(pluck(midi_hz(root + (7 if k == 4 else 0)), 0.3 * s.beat, 0.7, bright=0.8), bar * 4 + k * 0.5, pan=-0.2, rev=0.15)
            else:
                for m in vo: s.add(pluck(midi_hz(m), 0.25 * s.beat, 0.28, bright=1.2), bar * 4 + k * 0.5, pan=0.3, rev=0.2)
        s.add(bass(midi_hz(root), 1.8 * s.beat, 0.7, tone=0.4), bar * 4, rev=0.1)
        s.add(bass(midi_hz(root + 7), 1.8 * s.beat, 0.55, tone=0.4), bar * 4 + 2, rev=0.1)
        for m in vo: s.add(pad(midi_hz(m), 4 * s.beat, 0.55, bright=5), bar * 4, pan=0, rev=0.45)
        s.add(timpani(midi_hz(root), 0.9), bar * 4, rev=0.3)
        s.add(kick(0.6), bar * 4 + 2, rev=0.1)
        for beat, v in ((1, 0.8), (3, 0.8), (3.5, 0.4), (3.75, 0.5)): s.add(snare(v), bar * 4 + beat, rev=0.25)
        for k in range(8): s.add(shaker(0.8 if k % 2 else 0.5), bar * 4 + k * 0.5, pan=0.35, rev=0.1)
        if bar >= 14:   # 勇者の進行：ブラスの和音を大きく
            for m in vo: s.add(brass(midi_hz(m), 3.8 * s.beat, 0.5), bar * 4, pan=-0.1, rev=0.35)
            for k in range(4): s.add(timpani(midi_hz(root), 0.4 + 0.15 * k), bar * 4 + 2 + k * 0.5, rev=0.3)
        if bar % 8 == 0: s.add(crash(0.7), bar * 4, rev=0.3)
    save(s.render(1.5), "bgm_tower_f.wav")

def swing(pos):
    # 8分の裏を3連の3つめへ（はねるリズム）
    b = np.floor(pos); f = pos - b
    return b + (2.0 / 3.0 if abs(f - 0.5) < 1e-6 else f)

def tower_g():
    # G：はねる冒険（ワクワク＋ちょっとコミカル）。G長調 112BPM、シャッフル。
    # はねるベースとマリンバのメロディ、ブラスの合いの手、タムのジャングルっぽいリズム
    s = Song(112, 16)
    prog = ['G', 'Em', 'C', 'D', 'G', 'Em', 'Am', 'D', 'C', 'D', 'Bm', 'Em', 'C', 'D', 'G', 'G']
    kp = NOTE['G']
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 1), (1, 0.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)]]
    a = make_melody(np.random.default_rng(121), prog[:4], kp, MAJOR, rA, 67, 84, 71)
    a2 = make_melody(np.random.default_rng(122), prog[4:8], kp, MAJOR, rA, 67, 84, a[-1][2])
    b = make_melody(np.random.default_rng(123), prog[8:12], kp, MAJOR, rA, 67, 86, 74)
    c = make_melody(np.random.default_rng(124), prog[12:16], kp, MAJOR, rA[:3] + [[(0, 4)]], 67, 84, 76)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    mel = end_on_root(mel, kp, 67, 84)
    for pos, d, m in mel:
        p = swing(pos)
        s.add(marimba(midi_hz(m), d * s.beat, 1.0), p, pan=0.15, rev=0.2)
        s.add(pluck(midi_hz(m - 12), d * s.beat, 0.3, bright=1.0), p, pan=-0.2, rev=0.2)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(43, [r], 38, 49)
        vo = voicing(cps, 62, 57, 69)
        for beat in range(4):   # はねるベース
            s.add(bass(midi_hz(root + (7 if beat % 2 else 0)), 0.5 * s.beat, 0.95, tone=0.5), bar * 4 + beat, rev=0.05)
            s.add(bass(midi_hz(root + 12), 0.25 * s.beat, 0.55, tone=0.5), bar * 4 + beat + 2.0 / 3.0, rev=0.05)
        for beat in (1, 3):   # 裏の和音（ブラスの合いの手）
            for m in vo: s.add(brass(midi_hz(m), 0.3 * s.beat, 0.45), bar * 4 + beat + 2.0 / 3.0, pan=-0.25, rev=0.25)
        for beat in (0, 2): s.add(kick(0.85), bar * 4 + beat, rev=0.05)
        for beat in (1, 3): s.add(snare(0.75), bar * 4 + beat, rev=0.25)
        for beat in range(4):
            s.add(hat(0.6), bar * 4 + beat, pan=0.3, rev=0.05); s.add(hat(0.4), bar * 4 + beat + 2.0 / 3.0, pan=0.3, rev=0.05)
        for k, (off, f) in enumerate(((0.667, 160), (1.667, 130), (2.667, 160), (3.333, 110), (3.667, 130))):   # ジャングルのタム
            s.add(tom(f, 0.4), bar * 4 + off, pan=-0.4 + 0.2 * k, rev=0.2)
        if bar % 8 == 0: s.add(crash(0.6), bar * 4, rev=0.3)
    save(s.render(1.2), "bgm_tower_g.wav")

if __name__ == '__main__':
    for w in (sys.argv[1:] or ['tower_a', 'tower_b', 'tower_c']): globals()[w]()
