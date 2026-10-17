# 今の曲（make_bgm_v2.py の採用版）を長くする（2026-10-17 てじゃ「今の曲は好きだけど、ループすると短い」）。
# 形：A（今の16小節そのまま）→ B（新しい16小節：コード・メロディを変えて、伴奏を少し入れ替え）→ A'（今のメロディにハモりなど）→ 頭に戻る
# 使い方：python make_bgm_long.py <曲> [出力]   例）python make_bgm_long.py lobby
# 出力の初期値は BGM フォルダの bgm_<曲>_long.wav（今の曲は上書きしない）
import sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
import make_bgm as old
from fluffsynth import *

OUT = old.OUT

def third_below(mm, cname, key_pc, mode):
    """メロディの3度下（その小節のコードに合う音階で2段下）"""
    _, cps = old.chord(cname)
    sc = old.scale_for(key_pc, mode, cps)
    q = mm
    for _ in range(2): q = old.step(q, sc, -1)
    return q

def chord_at(prog, pos):
    return prog[min(len(prog) - 1, int(pos // 4))]

# ---------------- ロビー（bgm_lobby_e：G長調60BPM、オカリナ＋フェルトピアノのゆりかご＋パッド）----------------
def lobby(path=None):
    """A＝今の lobby_e そのまま（16小節）、B＝Em から始まる少し切ない16小節（メロディは低め・長い音、ゆりかごは半分に減らして息つぎ）、
    A'＝今のメロディ＋やわらかいビブラフォンの3度下のハモり（後半だけ）。全部で48小節＝3分12秒"""
    kind, taper, release, soft, breath, vib = 'ocarina', 0.45, 0.3, 1.0, 0.6, 0.6
    m = Mix(60, 48, tail=5.0, seed=11)
    kp = old.NOTE['G']; MAJOR = old.MAJOR
    progA = ['Gmaj7', 'Em7', 'Cmaj7', 'D', 'Gmaj7', 'Em7', 'Cmaj7', 'Dsus4',
             'Cmaj7', 'Gmaj7', 'Am7', 'D', 'Cmaj7', 'Gmaj7', 'Am7', 'Gmaj7']
    progB = ['Em7', 'Cmaj7', 'Gmaj7', 'D', 'Em7', 'Am7', 'Cmaj7', 'Dsus4',
             'Am7', 'Bm7', 'Cmaj7', 'Gmaj7', 'Am7', 'Bm7', 'Cmaj7', 'D']
    rA = [[(0, 3), (3, 1)], [(0, 4)], [(0, 2), (2, 2)], [(0, 4)]]
    rB = [[(1, 3)], [(0, 3), (3, 1)], [(0, 4)], [(0, 4)]]
    # A のメロディ（lobby_e と同じ乱数・同じ作り方）
    a = old.make_melody(np.random.default_rng(71), progA[:4], kp, MAJOR, rA, 67, 79, 74)
    a2 = old.make_melody(np.random.default_rng(72), progA[4:8], kp, MAJOR, rA, 67, 79, a[-1][2])
    b = old.make_melody(np.random.default_rng(73), progA[8:12], kp, MAJOR, rB, 67, 79, 76)
    c = old.make_melody(np.random.default_rng(74), progA[12:16], kp, MAJOR, rA, 67, 79, 74)
    melA = a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12)
    melA = old.end_on_root(melA, kp, 67, 79)
    # B のメロディ：少し低め（62〜74）、長い音が多い。最後は D の上で止まって A' へつなぐ
    rB1 = [[(0, 4)], [(1, 3)], [(0, 2), (2, 2)], [(0, 4)]]
    rB2 = [[(0, 3), (3, 1)], [(0, 4)], [(1, 1), (2, 2)], [(0, 4)]]
    b1 = old.make_melody(np.random.default_rng(81), progB[:4], kp, MAJOR, rB1, 62, 74, 71)
    b2 = old.make_melody(np.random.default_rng(82), progB[4:8], kp, MAJOR, rB2, 62, 74, b1[-1][2])
    b3 = old.make_melody(np.random.default_rng(83), progB[8:12], kp, MAJOR, rB1, 64, 76, 72)
    b4 = old.make_melody(np.random.default_rng(84), progB[12:16], kp, MAJOR, rB2[:3] + [[(0, 2)]], 64, 76, b3[-1][2])
    melB = b1 + old.transpose_rest(b2, 4) + old.transpose_rest(b3, 8) + old.transpose_rest(b4, 12)
    pB, dB, mB = melB[-1]; melB[-1] = (pB, dB, old.nearest(mB, [old.NOTE['A'], old.NOTE['D']], 64, 76))   # D の和音の上（ラかレ）で止める

    def ocarina(mel, offset_bars, gain=1.0, oct=0):
        for pos, d, mm in mel:
            v = m.vel(0.7 if (pos % 4) == 0 else 0.6, 0.05)
            m.add(wind(midi_hz(mm + 12 * oct), d * m.beat * 0.93, v, kind=kind, vib=vib, breath=breath, taper=taper, release=release, soft=soft),
                  offset_bars * 4 + pos, pan=0.06, rev=0.3, gain=gain, humanize=0.01)

    def accomp(prog, offset_bars, arp_every=1, arp_gain=1.0):
        for bar, cn in enumerate(prog):
            r, cps = old.chord(cn)
            at = (offset_bars + bar) * 4
            voic = sorted(old.nearest(55 + (p - 55) % 12, [p], 52, 64) for p in cps)
            for mm in voic:
                m.add(warm_pad(midi_hz(mm), 4 * m.beat + 0.3, 0.4, bright=0.25), at, rev=0.3, humanize=0)
            root = old.nearest(43, [r], 36, 47)
            m.add(sub_bass(midi_hz(root), 4 * m.beat, m.vel(0.15, 0.04)), at, pan=0, rev=0.06, humanize=0.004)
            arp = sorted(old.nearest(60 + (p - 60) % 12, [p], 55, 67) for p in cps)
            seq = [arp[0], arp[-1], arp[1], arp[-1]]
            for k in range(0, 4, arp_every):
                v = m.vel(0.34 if k == 0 else 0.26) * arp_gain
                m.add(felt_piano2(midi_hz(seq[k]), arp_every * m.beat, v, tone=0.3, sustain=0.9, mech=0.6), at + k,
                      pan=(-0.3 if k % 2 else 0.2), rev=0.22, humanize=0.014)

    # A（今の曲そのまま）
    ocarina(melA, 0); accomp(progA, 0)
    # B：ゆりかごは2拍ごと（息つぎ）、メロディは同じオカリナで低め。後半だけ遠くでビブラフォンが1小節に1つ
    ocarina(melB, 16); accomp(progB, 16, arp_every=2, arp_gain=1.1)
    for bar in range(8, 16):
        _, cps = old.chord(progB[bar])
        top = old.nearest(79, cps, 74, 84)
        m.add(vibes(midi_hz(top), 3 * m.beat, m.vel(0.22), hard=0.25, rel=1.6), (16 + bar) * 4 + 2, pan=-0.35, rev=0.45, humanize=0.01)
    # A'：同じメロディ、後半8小節だけビブラフォンで3度下のハモり（小さく）
    ocarina(melA, 32); accomp(progA, 32)
    for pos, d, mm in melA:
        if pos < 8 * 4: continue
        h = third_below(mm, chord_at(progA, pos), kp, MAJOR)
        m.add(vibes(midi_hz(h), d * m.beat, m.vel(0.3), hard=0.25, rel=1.2), 32 * 4 + pos, pan=-0.25, rev=0.38, humanize=0.01)
    st = master(m, rt60=1.8, rev_level=0.35, damp=0.6, lowcut=32, warmth_db=1.0, final_lp=8000, target_rms_db=-12.5)
    save(st, path or os.path.join(OUT, "bgm_lobby_long.wav"))
    return st

# ---------------- れんしゅう（bgm_basics2：C長調128BPM、ビブラフォン＋指弾きベース＋ギターのジャカッ＋ドラム）----------------
def basics(path=None):
    """A＝今の basics2 そのまま、B＝Am から始まる16小節（前半はドラムを軽くしてメロディは木のマリンバ、後半は手拍子でだんだん盛り上げて戻る）、
    A'＝今のメロディ＋1オクターブ上に小さく鉄琴のきらきら。48小節＝1分30秒"""
    m = Mix(128, 48, tail=3.0, seed=21)
    kp = old.NOTE['C']; MAJOR = old.MAJOR
    progA = ['C', 'G', 'Am', 'F'] * 2 + ['F', 'G', 'Em', 'Am', 'F', 'G', 'C', 'C']
    progB = ['Am', 'F', 'C', 'G', 'Am', 'F', 'G', 'G', 'F', 'G', 'Em', 'Am', 'F', 'Em', 'Dm', 'G']
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2.5, 0.5), (3, 1)], [(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)]]
    rB = [[(0, 1), (1, 1), (2, 0.5), (2.5, 1.5)], [(0, 0.5), (0.5, 1), (1.5, 0.5), (2, 2)],
          [(0, 1), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 2), (2.5, 0.5), (3, 1)]]
    a = old.make_melody(np.random.default_rng(21), progA[:4], kp, MAJOR, rA, 72, 88, 76)
    a2 = [(p, d, mm) for p, d, mm in a]
    b = old.make_melody(np.random.default_rng(22), progA[8:12], kp, MAJOR, rB, 72, 88, 81)
    c = old.make_melody(np.random.default_rng(23), progA[12:16], kp, MAJOR, rA[:2] + rB[2:3] + [[(0, 3)]], 72, 88, 79)
    melA = old.end_on_root(a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12), kp, 72, 88)
    # B のメロディ：少し低め、跳ねる短い音と伸ばす音を交互に
    rC = [[(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 0.5), (0.5, 0.5), (1, 2), (3, 1)],
          [(0, 1), (1, 1), (2, 1.5), (3.5, 0.5)], [(0, 3)]]
    b1 = old.make_melody(np.random.default_rng(31), progB[:4], kp, MAJOR, rC, 67, 81, 72)
    b2 = old.make_melody(np.random.default_rng(32), progB[4:8], kp, MAJOR, rC, 67, 81, b1[-1][2])
    b3 = old.make_melody(np.random.default_rng(33), progB[8:12], kp, MAJOR, rB, 69, 84, 76)
    b4 = old.make_melody(np.random.default_rng(34), progB[12:16], kp, MAJOR, rB[:3] + [[(0, 2), (2.5, 0.5), (3, 1)]], 69, 84, b3[-1][2])
    melB = b1 + old.transpose_rest(b2, 4) + old.transpose_rest(b3, 8) + old.transpose_rest(b4, 12)
    pB, dB, mB = melB[-1]; melB[-1] = (pB, dB, old.nearest(mB, [old.NOTE['G'], old.NOTE['B'], old.NOTE['D']], 69, 84))

    def lead(mel, off, fn='vibes', gain=1.0, oct=0, vel_mul=1.0):
        for pos, d, mm in mel:
            acc = (0.8 if abs(pos - round(pos)) < 1e-6 else 0.65) * vel_mul
            at = off * 4 + pos
            if fn == 'vibes': m.add(vibes(midi_hz(mm + oct), d * m.beat, m.vel(acc), rel=1.4), at, pan=0.12, rev=0.42, gain=gain, humanize=0.006)
            elif fn == 'marimba': m.add(marimba3(midi_hz(mm + oct), d * m.beat, m.vel(acc), hard=0.35), at, pan=0.12, rev=0.2, gain=gain, humanize=0.006)
            elif fn == 'glock': m.add(glock(midi_hz(mm + oct), d * m.beat, m.vel(acc), hard=0.4), at, pan=0.45, rev=0.3, gain=gain, humanize=0.006)

    def band(prog, off, drums='full', strum_g=1.0, bass_mode='eighth'):
        for bar, cn in enumerate(prog):
            r, cps = old.chord(cn); at = (off + bar) * 4
            root = old.nearest(40, [r], 36, 47)
            if bass_mode == 'eighth':
                for k in range(8):
                    mm = root + (12 if k % 2 else 0)
                    m.add(finger_bass(midi_hz(mm), 0.42 * m.beat, m.vel(0.68 if k % 2 == 0 else 0.48, 0.05), tone=0.35), at + k * 0.5, rev=0.03, humanize=0.004)
            else:   # 2拍ずつ：ゆったり
                for k, vv in ((0, 0.7), (2, 0.55), (3.5, 0.45)):
                    m.add(finger_bass(midi_hz(root + (12 if k == 3.5 else 0)), (1.6 if k < 3 else 0.42) * m.beat, m.vel(vv, 0.05), tone=0.35), at + k, rev=0.03, humanize=0.004)
            voic = sorted(old.nearest(62 + (p - 62) % 12, [p], 60, 72) for p in cps)
            for j, o in enumerate((0.5, 1.5, 2.5, 3.5)):
                m.add(strum([midi_hz(x) for x in voic], 0.28 * m.beat, m.vel(0.5 * strum_g), bright=0.55, spread=0.009, down=(j % 2 == 0)), at + o, pan=-0.55, rev=0.15, humanize=0.005)
            if drums == 'full':
                for beat in (0, 2, 2.75 if bar % 2 else 2):
                    m.add(kick2(m.vel(0.72, 0.04), punch=0.5, low=0.4), at + beat, rev=0.02, humanize=0.002)
                for beat in (1, 3):
                    m.add(snare2(m.vel(0.75, 0.05), tone=0.5, snap=0.5), at + beat, pan=0.05, rev=0.15, humanize=0.003)
                for k in range(8):
                    m.add(hat2(m.vel(0.8 if k % 2 else 0.55, 0.08), open_=(k == 7 and bar % 4 == 3)), at + k * 0.5, pan=0.42, rev=0.03, humanize=0.004)
            elif drums == 'light':   # キックは1拍目だけ、スネアなし、ハットは裏だけ小さく、シェイカー
                m.add(kick2(m.vel(0.6, 0.04), punch=0.4, low=0.4), at, rev=0.02, humanize=0.002)
                for k in range(8):
                    m.add(shaker2(m.vel(0.55 if k % 2 else 0.35)), at + k * 0.5, pan=0.42, rev=0.06, humanize=0.004)
            elif drums == 'build':   # 手拍子で戻る準備：キック2つ＋手拍子、最後の小節はスネアの連打
                for beat in (0, 2):
                    m.add(kick2(m.vel(0.68, 0.04), punch=0.5, low=0.4), at + beat, rev=0.02, humanize=0.002)
                for beat in (1, 3):
                    m.add(clap2(m.vel(0.6, 0.05)), at + beat, pan=-0.1, rev=0.2, humanize=0.004)
                for k in range(8):
                    m.add(hat2(m.vel(0.6 if k % 2 else 0.4, 0.08)), at + k * 0.5, pan=0.42, rev=0.03, humanize=0.004)
                if bar == len(prog) - 1:
                    for k in range(8):
                        m.add(snare2(m.vel(0.35 + 0.07 * k, 0.03), tone=0.5, snap=0.5), at + 2 + k * 0.25, pan=0.05, rev=0.15, humanize=0.002)

    # A
    lead(melA, 0); band(progA, 0)
    # B：前半はドラム軽め・ベースゆったり・メロディは木のマリンバ、後半は手拍子で盛り上げてビブラフォンに戻る
    lead(melB[:sum(1 for p, _, _ in melB if p < 32)], 16, fn='marimba', gain=1.1)
    lead([(p, d, mm) for p, d, mm in melB if p >= 32], 16, fn='vibes')
    band(progB[:8], 16, drums='light', strum_g=0.8, bass_mode='slow')
    band(progB[8:], 24, drums='build', strum_g=0.9)
    # A'：同じ。1オクターブ上に小さく鉄琴
    lead(melA, 32); band(progA, 32)
    lead(melA, 32, fn='glock', gain=0.35, oct=12, vel_mul=0.8)
    st = master(m, rt60=1.3, rev_level=0.35, damp=0.5, lowcut=30, warmth_db=0.5, air_db=1.0, comp_thresh=-16.0, comp_ratio=2.0, target_rms_db=-13.0)
    save(st, path or os.path.join(OUT, "bgm_basics_long.wav"))
    return st

# ---------------- かぜのみち（bgm_kaze2：Aミクソリディア108BPM、フルート＋ギターのストローク＋タム＋ドローン）----------------
def kaze(path=None):
    """A＝今の kaze2 そのまま、B＝D から始まる16小節（前半はギターを指弾きのつま弾きに、タムなし、フルートは低めで長く。
    後半はタムが戻って、フルートが元の高さで2本目のフルートが3度下でハモる）、A'＝今のメロディ＋後半8小節だけハモり。48小節＝1分47秒。風の音は入れない"""
    from make_bgm_tower import voicing
    MIXO = [0, 2, 4, 5, 7, 9, 10]
    fvib, fbreath, ftaper, fsoft, fgain, tgain, bgain = 0.8, 0.8, 0.2, 0.3, 1.9, 0.6, 0.75
    m = Mix(108, 48, tail=3.0, seed=31)
    progA = ['A', 'G', 'A', 'G', 'D', 'A', 'G', 'A', 'D', 'G', 'A', 'Em', 'D', 'G', 'A', 'A']
    progB = ['D', 'A', 'G', 'D', 'Em', 'G', 'A', 'A', 'D', 'A', 'G', 'D', 'Em', 'G', 'D', 'E']
    kp = old.NOTE['A']
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5), (3.5, 0.5)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 2)],
          [(0, 1.5), (1.5, 0.25), (1.75, 0.25), (2, 1), (3, 1)], [(0, 3), (3, 1)]]
    a = old.make_melody(np.random.default_rng(321), progA[:4], kp, MIXO, rA, 69, 86, 76)
    a2 = old.make_melody(np.random.default_rng(322), progA[4:8], kp, MIXO, rA, 69, 86, a[-1][2])
    b = old.make_melody(np.random.default_rng(323), progA[8:12], kp, MIXO, rA, 71, 88, 81)
    c = old.make_melody(np.random.default_rng(324), progA[12:16], kp, MIXO, rA, 69, 86, 79)
    melA = old.end_on_root(a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12), kp, 69, 86)
    rS = [[(0, 2), (2, 2)], [(0, 3), (3, 1)], [(0, 1), (1, 1), (2, 2)], [(0, 4)]]   # ゆったり
    b1 = old.make_melody(np.random.default_rng(331), progB[:4], kp, MIXO, rS, 64, 79, 69)
    b2 = old.make_melody(np.random.default_rng(332), progB[4:8], kp, MIXO, rS, 64, 79, b1[-1][2])
    b3 = old.make_melody(np.random.default_rng(333), progB[8:12], kp, MIXO, rA, 69, 86, 78)
    b4 = old.make_melody(np.random.default_rng(334), progB[12:16], kp, MIXO, rA[:3] + [[(0, 2), (2, 1), (3, 1)]], 69, 86, b3[-1][2])
    melB = b1 + old.transpose_rest(b2, 4) + old.transpose_rest(b3, 8) + old.transpose_rest(b4, 12)
    pB, dB, mB = melB[-1]; melB[-1] = (pB, dB, old.nearest(mB, [old.NOTE['E'], old.NOTE['G#'], old.NOTE['B']], 69, 86))   # E の上で止めて A へ

    def flute(mel, off, gain=1.0, grace=True, transpose=None, prog=None):
        for i, (pos, d, mm) in enumerate(mel):
            if transpose: mm = transpose(mm, pos)
            v = m.vel(0.72 if abs(pos - round(pos)) < 1e-6 else 0.62, 0.05)
            at = off * 4 + pos
            m.add(wind(midi_hz(mm), d * m.beat * 0.92, v, kind='flute', vib=fvib, breath=fbreath, taper=ftaper, release=0.12, soft=fsoft), at, pan=0.1 if gain >= 1 else -0.25, rev=0.32, gain=fgain * gain, humanize=0.008)
            if grace and d >= 1 and i % 3 == 0 and pos >= 0.12:
                m.add(wind(midi_hz(mm + 2), 0.07, v * 0.6, kind='flute', vib=0, breath=fbreath * 0.6, release=0.03, soft=fsoft), at - 0.12, pan=0.1, rev=0.28, gain=fgain * gain, humanize=0)

    def band(prog, off, guitar='strum', toms=True, bell=True):
        for bar, cn in enumerate(prog):
            r, cps = old.chord(cn); at = (off + bar) * 4
            vo = voicing(cps, 57, 52, 64)
            vo = vo + [next(x for x in voicing(cps, 64, 59, 71) + [y + 12 for y in voicing(cps, 64, 59, 71)] if x > vo[-1])]
            if guitar == 'strum':
                for j, (o, vel) in enumerate(((0, 0.5), (1, 0.36), (1.5, 0.3), (2.5, 0.36), (3, 0.4))):
                    m.add(strum([midi_hz(x) for x in vo], 0.4 * m.beat, m.vel(vel), bright=0.5, spread=0.012, down=(o == int(o))), at + o, pan=-0.45, rev=0.2, humanize=0.005)
            else:   # 指弾きのつま弾き（1音ずつ8分で）
                seq = [vo[0], vo[2], vo[1], vo[3], vo[2], vo[1], vo[3], vo[2]]
                for k, x in enumerate(seq):
                    m.add(ks_string(midi_hz(x), 1.2 * m.beat, m.vel(0.42 if k % 4 == 0 else 0.32), bright=0.45), at + k * 0.5, pan=-0.4, rev=0.25, gain=2.2, humanize=0.006)
            if bar % 4 == 0:
                m.add(warm_pad(midi_hz(old.nearest(45, [kp], 40, 50)), 16 * m.beat, 0.35, bright=0.3), at, rev=0.35, humanize=0)
                m.add(warm_pad(midi_hz(old.nearest(52, [(kp + 7) % 12], 48, 57)), 16 * m.beat, 0.22, bright=0.3), at, rev=0.35, humanize=0)
            root = old.nearest(45, [r], 40, 51)
            m.add(finger_bass(midi_hz(root), 1.5 * m.beat, m.vel(0.62, 0.04), tone=0.3), at, rev=0.04, gain=bgain, humanize=0.004)
            m.add(finger_bass(midi_hz(root), 1.0 * m.beat, m.vel(0.48, 0.04), tone=0.3), at + 2.5, rev=0.04, gain=bgain, humanize=0.004)
            if toms:
                m.add(tom2(85, m.vel(0.8, 0.04)), at, pan=-0.12, rev=0.22, gain=tgain, humanize=0.003)
                m.add(tom2(85, m.vel(0.5, 0.04)), at + 2, pan=-0.12, rev=0.22, gain=tgain, humanize=0.003)
                m.add(tom2(100, m.vel(0.45, 0.04)), at + 2.5, pan=0.15, rev=0.22, gain=tgain, humanize=0.003)
            for k in range(8): m.add(shaker2(m.vel(0.6 if k % 2 else 0.38) * (1.0 if toms else 0.7)), at + k * 0.5, pan=0.45, rev=0.08, humanize=0.004)
            if bell and bar % 4 == 3:
                m.add(celesta(midi_hz(old.nearest(84, cps, 79, 88)), 1.5 * m.beat, 0.3), at + 2.5, pan=0.4, rev=0.6, humanize=0)

    def harm(prog, off_from):
        return lambda mm, pos: third_below(mm, chord_at(prog, pos), kp, MIXO)

    # A
    flute(melA, 0); band(progA, 0)
    # B：前半つま弾き・タムなし、後半タムが戻ってハモり
    flute([x for x in melB if x[0] < 32], 16)
    flute([x for x in melB if x[0] >= 32], 16)
    flute([x for x in melB if x[0] >= 32], 16, gain=0.45, grace=False, transpose=harm(progB, 0))
    band(progB[:8], 16, guitar='pick', toms=False, bell=False)
    band(progB[8:], 24)
    # A'：後半8小節だけハモり
    flute(melA, 32); band(progA, 32)
    flute([x for x in melA if x[0] >= 32], 32, gain=0.45, grace=False, transpose=harm(progA, 0))
    st = master(m, rt60=1.8, rev_level=0.38, damp=0.5, lowcut=30, warmth_db=0.5, air_db=1.5, comp_thresh=-16, comp_ratio=1.8, target_rms_db=-13.0)
    save(st, path or os.path.join(OUT, "bgm_kaze_long.wav"))
    return st

# ---------------- いわのみち（bgm_pitfalls_b：D短調100BPM、古い音源 make_bgm.py のまま。てじゃ「古いほうがいい」）----------------
def pitfalls(path=None):
    """A＝今の pitfalls_b（24小節）そのままの作曲、B＝新しい16小節：前半8小節は重い半分のテンポ感（キックは1拍目、スネアは3拍目、
    パワーコードは小節の頭でズーンと伸ばす、ブラスは低く長い音）、後半8小節は刻みとタムが戻って盛り上げ、最後はスネアの連打でAへ。
    40小節＝1分36秒。音源は古い make_bgm.py のまま（ドラムの細かいザラつきは乱数の都合で今のと少し違う）"""
    S_ = old
    s = S_.Song(100, 40)
    progA = ['Dm', 'Bb', 'C', 'A'] * 2 + ['Gm', 'Bb', 'Dm', 'A', 'Gm', 'Bb', 'C', 'A'] + ['Dm', 'Bb', 'C', 'A'] * 2
    progB = ['Gm', 'Dm', 'Bb', 'F', 'Gm', 'Dm', 'Bb', 'A', 'Bb', 'C', 'Dm', 'Dm', 'Bb', 'C', 'A', 'A']
    kp = S_.NOTE['D']; MINOR = S_.MINOR
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5), (3.5, 0.5)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 1), (2, 1), (3, 1)], [(0, 3), (3, 0.5), (3.5, 0.5)]]
    rB = [[(0, 2), (2, 1), (3, 1)], [(0, 1.5), (1.5, 1.5), (3, 1)], [(0, 1), (1, 1), (2, 1), (3, 1)], [(0, 4)]]
    a = S_.make_melody(np.random.default_rng(81), progA[:4], kp, MINOR, rA, 62, 76, 69)
    b = S_.make_melody(np.random.default_rng(82), progA[8:12], kp, MINOR, rB, 62, 77, 67)
    b2 = S_.make_melody(np.random.default_rng(83), progA[12:16], kp, MINOR, rB, 62, 77, 70)
    melA = a + S_.transpose_rest(a, 4) + S_.transpose_rest(b, 8) + S_.transpose_rest(b2, 12) + S_.transpose_rest(a, 16) + S_.transpose_rest(a, 20)
    melA = S_.end_on_root(melA, kp, 62, 76)
    rL = [[(0, 4)], [(0, 2), (2, 2)], [(0, 3), (3, 1)], [(0, 4)]]   # 重く長い
    c1 = S_.make_melody(np.random.default_rng(91), progB[:4], kp, MINOR, rL, 55, 69, 62)
    c2 = S_.make_melody(np.random.default_rng(92), progB[4:8], kp, MINOR, rL, 55, 69, c1[-1][2])
    c3 = S_.make_melody(np.random.default_rng(93), progB[8:12], kp, MINOR, rB, 62, 77, 69)
    c4 = S_.make_melody(np.random.default_rng(94), progB[12:16], kp, MINOR, rB[:2] + [[(0, 1), (1, 1), (2, 2)], [(0, 2)]], 62, 77, c3[-1][2])
    melB = c1 + S_.transpose_rest(c2, 4) + S_.transpose_rest(c3, 8) + S_.transpose_rest(c4, 12)
    pB, dB, mB = melB[-1]; melB[-1] = (pB, dB, S_.nearest(mB, [S_.NOTE['A'], S_.NOTE['C#'], S_.NOTE['E']], 62, 77))
    O = 24 * 4   # B の始まり（拍）

    # A（今の曲と同じ作り方・同じ順番）
    for pos, d, mm in melA:
        s.add(S_.brass(midi_hz(mm), d * s.beat * 0.92, 1.2), pos, pan=0.15, rev=0.25)
        s.add(S_.brass(midi_hz(mm - 12), d * s.beat * 0.92, 0.8), pos, pan=-0.15, rev=0.25)
        if pos >= 64: s.add(S_.square_lead(midi_hz(mm + 12), d * s.beat * 0.9, 0.35), pos, pan=0.3, rev=0.3)
    def full_bar(bar_abs, cn, last=False):
        r, cps = S_.chord(cn); at = bar_abs * 4
        root = S_.nearest(38, [r], 33, 44)
        for k in range(8):
            s.add(S_.bass(midi_hz(root + (12 if k == 7 else 0)), 0.42 * s.beat, 1.0 if k % 2 == 0 else 0.8, tone=0.8), at + k * 0.5, rev=0.05)
        pr = S_.nearest(50, [r], 45, 56)
        for k in range(8):
            if k in (3, 7) and bar_abs % 2 == 1: continue
            vel = 1.0 if k in (0, 3, 6) else 0.6
            s.add(S_.power(midi_hz(pr), 0.38 * s.beat, vel), at + k * 0.5, pan=-0.4, rev=0.12)
            s.add(S_.power(midi_hz(pr), 0.38 * s.beat, vel), at + k * 0.5 + 0.01, pan=0.4, rev=0.12)
        for beat in (0, 1, 2, 3): s.add(S_.kick(1.0 if beat % 2 == 0 else 0.8, heavy=True), at + beat, rev=0.1)
        s.add(S_.kick(0.7, heavy=True), at + 2.5, rev=0.1)
        for beat in (1, 3): s.add(S_.snare(1.3), at + beat, rev=0.45)
        for k in range(8): s.add(S_.hat(0.9 if k % 2 else 0.6, open_=(k == 7)), at + k * 0.5, pan=0.3, rev=0.05)
        if bar_abs % 4 == 0: s.add(S_.crash(1.0), at, pan=-0.2, rev=0.3)
        if last:   # スネアの連打で A へ
            for k in range(8): s.add(S_.snare(0.5 + 0.1 * k), at + 2 + k * 0.25, rev=0.3)
        elif bar_abs % 4 == 3:
            for k, f in enumerate((130, 110, 90, 70)): s.add(S_.tom(f, 1.0), at + 2 + k * 0.5, pan=-0.4 + 0.27 * k, rev=0.3)
    for bar, cn in enumerate(progA): full_bar(bar, cn)

    # B 前半：重い半分のテンポ感
    for pos, d, mm in melB:
        if pos >= 32: continue
        s.add(S_.brass(midi_hz(mm), d * s.beat * 0.95, 1.1), O + pos, pan=0.1, rev=0.35)
        s.add(S_.brass(midi_hz(mm - 12), d * s.beat * 0.95, 0.9), O + pos, pan=-0.1, rev=0.35)
    for bar in range(8):
        cn = progB[bar]; r, cps = S_.chord(cn); at = O + bar * 4
        root = S_.nearest(38, [r], 33, 44)
        s.add(S_.bass(midi_hz(root), 1.8 * s.beat, 1.0, tone=0.8), at, rev=0.08)
        s.add(S_.bass(midi_hz(root), 1.4 * s.beat, 0.8, tone=0.8), at + 2.5, rev=0.08)
        pr = S_.nearest(50, [r], 45, 56)
        s.add(S_.power(midi_hz(pr), 2.6 * s.beat, 1.0), at, pan=-0.4, rev=0.25)
        s.add(S_.power(midi_hz(pr), 2.6 * s.beat, 1.0), at + 0.01, pan=0.4, rev=0.25)
        s.add(S_.power(midi_hz(pr), 0.38 * s.beat, 0.7), at + 3.5, pan=-0.4, rev=0.12)
        s.add(S_.kick(1.0, heavy=True), at, rev=0.12); s.add(S_.kick(0.75, heavy=True), at + 1.5, rev=0.12)
        s.add(S_.snare(1.4), at + 2, rev=0.6)
        for k in range(4): s.add(S_.hat(0.7 if k % 2 else 0.5), at + k, pan=0.3, rev=0.05)
        if bar % 4 == 0: s.add(S_.crash(0.8), at, pan=-0.2, rev=0.4)
    # B 後半：刻みが戻って盛り上げ、メロディの上にピコピコ
    for pos, d, mm in melB:
        if pos < 32: continue
        s.add(S_.brass(midi_hz(mm), d * s.beat * 0.92, 1.2), O + pos, pan=0.15, rev=0.25)
        s.add(S_.brass(midi_hz(mm - 12), d * s.beat * 0.92, 0.8), O + pos, pan=-0.15, rev=0.25)
        s.add(S_.square_lead(midi_hz(mm + 12), d * s.beat * 0.9, 0.3), O + pos, pan=0.3, rev=0.3)
    for bar in range(8, 16): full_bar(24 + bar, progB[bar], last=(bar == 15))
    st = s.render(1.4) * 10 ** (0.7 / 20)   # B のぶん頭が下がるので、今の曲と同じ聞こえる大きさ（A の RMS -13.5dB）に戻す
    out = path or os.path.join(OUT, "bgm_pitfalls_long.wav")
    import wave as _w
    with _w.open(out, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(S_.SR)
        w.writeframes((st * 32767).astype(np.int16).tobytes())
    print(os.path.basename(out), f"{len(st)/S_.SR:.1f}s")
    return st

# ---------------- やみのもり（bgm_yami2：D短調70BPM、ビブラフォン＋チェレスタ、暗いパッド、息、しずく、心臓の「どっ・くん」）----------------
def yami(path=None):
    """A＝今の yami2 そのまま、B＝Bb から始まる16小節：前半8小節は「森のひらけた所」＝心臓の音が止まって、遠くの合唱「うー」が
    ゆっくりメロディを歌う（しずくは少し多め）、後半8小節は心臓が戻ってビブラフォンのメロディ、最後は A の和音で止めて A へ。
    A'＝今のメロディ＋後半8小節だけ合唱「うー」が和音を小さく支える。48小節＝2分45秒"""
    from make_bgm_tower import voicing
    from make_bgm_yami import drip, HMINOR
    m = Mix(70, 48, tail=4.0, seed=51)
    progA = ['Dm', 'Bb', 'Gm', 'A', 'Dm', 'Bb', 'Gm', 'A', 'Bb', 'C', 'Am', 'Dm', 'Gm', 'Bb', 'A', 'A']
    progB = ['Bb', 'F', 'Gm', 'Dm', 'Bb', 'F', 'C', 'A', 'Gm', 'Dm', 'Bb', 'F', 'Gm', 'Bb', 'A', 'A']
    kp = old.NOTE['D']; MINOR = old.MINOR
    rA = [[(0, 2), (2, 1), (3, 1)], [(0, 3), (3, 1)], [(0, 1), (1, 1), (2, 2)], [(0, 4)]]
    a = old.make_melody(np.random.default_rng(211), progA[:4], kp, HMINOR, rA, 62, 79, 69)
    a2 = old.make_melody(np.random.default_rng(212), progA[4:8], kp, HMINOR, rA, 62, 79, a[-1][2])
    b = old.make_melody(np.random.default_rng(213), progA[8:12], kp, MINOR, rA, 64, 81, 72)
    c = old.make_melody(np.random.default_rng(214), progA[12:16], kp, HMINOR, rA, 62, 79, 70)
    melA = a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12)
    rS = [[(0, 4)], [(0, 2), (2, 2)], [(0, 4)], [(0, 3), (3, 1)]]
    b1 = old.make_melody(np.random.default_rng(221), progB[:4], kp, MINOR, rS, 60, 74, 65)
    b2 = old.make_melody(np.random.default_rng(222), progB[4:8], kp, MINOR, rS, 60, 74, b1[-1][2])
    b3 = old.make_melody(np.random.default_rng(223), progB[8:12], kp, MINOR, rA, 64, 81, 70)
    b4 = old.make_melody(np.random.default_rng(224), progB[12:16], kp, HMINOR, rA[:3] + [[(0, 2)]], 62, 79, b3[-1][2])
    melB = b1 + old.transpose_rest(b2, 4) + old.transpose_rest(b3, 8) + old.transpose_rest(b4, 12)
    pB, dB, mB = melB[-1]; melB[-1] = (pB, dB, old.nearest(mB, [old.NOTE['A'], old.NOTE['C#'], old.NOTE['E']], 62, 79))

    def vib_lead(mel, off):
        for pos, d, mm in mel:
            v = m.vel(0.7 if (pos % 4) == 0 else 0.6, 0.05)
            m.add(vibes(midi_hz(mm), d * m.beat, v, hard=0.3, trem=3.5, depth=0.35, rel=1.2), off * 4 + pos, pan=0.1, rev=0.5, humanize=0.01)
            m.add(celesta(midi_hz(mm + 12), d * m.beat, v * 0.3), off * 4 + pos + 0.02, pan=-0.35, rev=0.55, humanize=0.006)

    def bed(prog, off, rng, heart=True, drip_p=0.55):
        for bar, cn in enumerate(prog):
            r, cps = old.chord(cn); at = (off + bar) * 4
            for x in voicing(cps, 57, 52, 64):
                m.add(warm_pad(midi_hz(x), 4 * m.beat + 0.3, 0.42, bright=0.18), at, rev=0.45, humanize=0)
            m.add(finger_bass(midi_hz(old.nearest(38, [r], 33, 44)), 3.5 * m.beat, m.vel(0.5, 0.04), tone=0.15), at, rev=0.1, humanize=0.004)
            if bar % 2 == 0:
                n = int(6 * m.beat * SR); t = np.arange(n) / SR
                nz = bp(rng.standard_normal(n), 300, 1500, 2) * np.sin(np.pi * t / t[-1]) ** 2
                m.add(nz * 0.05, at, pan=float(rng.uniform(-0.5, 0.5)), rev=0.5, humanize=0)
            if heart:
                m.add(kick2(m.vel(0.38, 0.03), punch=0.1, low=0.9), at, rev=0.12, humanize=0.002)
                m.add(kick2(m.vel(0.24, 0.03), punch=0.1, low=0.9), at + 0.5, rev=0.12, humanize=0.002)
            sc = old.scale_for(kp, HMINOR, cps)
            for k in range(4):
                if rng.random() < drip_p:
                    x = old.nearest(int(rng.integers(84, 96)), sc, 82, 98)
                    m.add(lp(drip(midi_hz(x), 0.8), 3500, 1), at + k + float(rng.choice([0, 0.5])), pan=float(rng.uniform(-0.8, 0.8)), rev=0.85, gain=0.28, humanize=0.004)

    # A（今の曲と同じ順番）
    vib_lead(melA, 0); bed(progA, 0, np.random.default_rng(215))
    # B 前半：合唱「うー」のメロディ、心臓なし、しずく多め
    for pos, d, mm in melB:
        if pos >= 32: continue
        m.add(choir(midi_hz(mm), d * m.beat, m.vel(0.6, 0.04), vowel='u', voices=4), 16 * 4 + pos, pan=0.05, rev=0.6, gain=0.85, humanize=0.01)
    bed(progB[:8], 16, np.random.default_rng(225), heart=False, drip_p=0.7)
    # B 後半：心臓とビブラフォンが戻る
    vib_lead([x for x in melB if x[0] >= 32], 16)
    bed(progB[8:], 24, np.random.default_rng(226))
    # A'：同じ。後半8小節は合唱「うー」が和音を小さく支える
    vib_lead(melA, 32); bed(progA, 32, np.random.default_rng(227))
    for bar in range(8, 16):
        _, cps = old.chord(progA[bar])
        for x in voicing(cps, 62, 57, 69):
            m.add(choir(midi_hz(x), 4 * m.beat, 0.3, vowel='u', voices=3), (32 + bar) * 4, pan=-0.2, rev=0.6, gain=0.55, humanize=0)
    st = master(m, rt60=2.6, rev_level=0.45, damp=0.55, lowcut=30, warmth_db=0.5, air_db=0.5, comp_thresh=-18, comp_ratio=1.6, target_rms_db=-12.0)
    save(st, path or os.path.join(OUT, "bgm_yami_long.wav"))
    return st

# ---------------- まもののす（bgm_den2：Aドリア104BPM、木のマリンバ＋しのび足のベース＋ボンゴ・ウッドブロック、ブラスの「ぶぉっ」）----------------
def den(path=None, blead='kalimba', leadg=0.65):   # 2026-10-17 てじゃ実機で「ピチカートやっぱ気になる」→ 聴き比べでカリンバ
    """A＝今の den2 そのまま、B＝D から始まる16小節：前半8小節は「そーっと奥へ」＝マリンバをやめて、はじく弦（ピチカート）の
    しのび足メロディ、ボンゴだけ・キックなし、ブラスの「ぶぉっ」は1小節おきに小さく。後半8小節はマリンバが戻ってブラスが毎小節、
    ウッドブロックが細かくなって緊張→E で止めて A へ。A'＝今のメロディ＋後半8小節だけ1オクターブ上にピチカートを小さく重ねる。48小節＝1分51秒"""
    from make_bgm_tower import voicing
    DORIAN = [0, 2, 3, 5, 7, 9, 10]
    pgain, bgain, stabg, lgain = 1.6, 3.0, 3.0, 1.0
    m = Mix(104, 48, tail=3.0, seed=61)
    progA = ['Am', 'Am', 'D', 'Am', 'Am', 'Am', 'Em', 'Am', 'F', 'G', 'Am', 'Am', 'F', 'G', 'E', 'E']
    progB = ['D', 'Am', 'D', 'Am', 'F', 'Em', 'Dm', 'E', 'Am', 'G', 'F', 'E', 'Am', 'G', 'F', 'E']
    kp = old.NOTE['A']; MINOR = old.MINOR
    rA = [[(0, 0.5), (1, 0.5), (1.5, 0.5), (2.5, 0.5), (3, 0.5)], [(0.5, 0.5), (1, 0.5), (2, 0.5), (3, 1)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (2, 0.5), (2.5, 0.5), (3, 0.5)], [(0, 1), (2, 0.5), (3, 0.5)]]
    a = old.make_melody(np.random.default_rng(301), progA[:4], kp, DORIAN, rA, 57, 74, 64)
    a2 = old.make_melody(np.random.default_rng(302), progA[4:8], kp, DORIAN, rA, 57, 74, a[-1][2])
    b = old.make_melody(np.random.default_rng(303), progA[8:12], kp, MINOR, rA, 59, 76, 67)
    c = old.make_melody(np.random.default_rng(304), progA[12:16], kp, [0, 2, 3, 5, 7, 8, 11], rA, 57, 76, 64)
    melA = a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12)
    rP = [[(0, 0.5), (1, 0.5), (2, 0.5), (3, 0.5)], [(0.5, 0.5), (1.5, 0.5), (2.5, 0.5), (3, 1)],
          [(0, 0.5), (1, 0.5), (1.5, 0.5), (2, 0.5), (3, 0.5)], [(0, 1.5), (2, 0.5), (3, 0.5)]]
    b1 = old.make_melody(np.random.default_rng(311), progB[:4], kp, DORIAN, rP, 57, 72, 62)
    b2 = old.make_melody(np.random.default_rng(312), progB[4:8], kp, [0, 2, 3, 5, 7, 8, 11], rP, 57, 72, b1[-1][2])
    b3 = old.make_melody(np.random.default_rng(313), progB[8:12], kp, [0, 2, 3, 5, 7, 8, 11], rA, 59, 76, 69)
    b4 = old.make_melody(np.random.default_rng(314), progB[12:16], kp, [0, 2, 3, 5, 7, 8, 11], rA[:3] + [[(0, 1)]], 59, 76, b3[-1][2])
    melB = b1 + old.transpose_rest(b2, 4) + old.transpose_rest(b3, 8) + old.transpose_rest(b4, 12)
    pB, dB, mB = melB[-1]; melB[-1] = (pB, dB, old.nearest(mB, [old.NOTE['E'], old.NOTE['G#'], old.NOTE['B']], 59, 76))

    def marimba_lead(mel, off):
        for pos, d, mm in mel:
            v = m.vel(0.85 if abs(pos - round(pos)) < 1e-6 else 0.7, 0.05)
            m.add(marimba3(midi_hz(mm), 0.5 * m.beat, v, hard=0.45), off * 4 + pos, pan=0.15, rev=0.2, gain=lgain, humanize=0.006)
            m.add(marimba3(midi_hz(mm - 12), 0.5 * m.beat, v * 0.6, hard=0.3), off * 4 + pos, pan=-0.2, rev=0.2, gain=lgain, humanize=0.006)

    def pizz(mel, off, gain=1.0, oct=0, low_brass=True):
        for pos, d, mm in mel:
            v = m.vel(0.85 if abs(pos - round(pos)) < 1e-6 else 0.7, 0.05)
            m.add(lp(ks_string(midi_hz(mm + oct), 0.3 * m.beat, v, bright=0.35, damp_time=0.06), 2800, 2), off * 4 + pos, pan=0.15 if oct == 0 else 0.45, rev=0.2, gain=pgain * 2.5 * gain, humanize=0.006)
            if low_brass: m.add(brass2(midi_hz(mm - 12), 0.28 * m.beat, v * 0.6, bright=0.25, voices=2, att=0.03, det_c=5), off * 4 + pos, pan=-0.1, rev=0.2, gain=bgain * gain, humanize=0.006)

    def jungle(prog, off, kick=True, stab_every=2, stab_gain=1.0, wood='quarter'):
        for bar, cn in enumerate(prog):
            r, cps = old.chord(cn); at = (off + bar) * 4
            root = old.nearest(33, [r], 28, 40)
            for beat in range(4):
                x = root if beat % 2 == 0 else root + (7 if beat == 1 else 5)
                m.add(finger_bass(midi_hz(x), 0.35 * m.beat, m.vel(0.85 if beat == 0 else 0.6, 0.04), tone=0.4), at + beat, rev=0.04, humanize=0.004)
            for o, hi in ((0, False), (0.75, True), (1.5, True), (2, False), (2.5, True), (3.25, True), (3.5, False)):
                m.add(bongo2(hi, m.vel(0.8 if not hi else 0.6, 0.06)), at + o, pan=-0.45 if hi else -0.2, rev=0.12, humanize=0.004)
            if wood == 'quarter':
                for k in range(4): m.add(woodblock2(m.vel(0.45, 0.06), hi=True), at + k + 0.5, pan=0.5, rev=0.1, humanize=0.003)
            elif wood == 'eighth':
                for k in range(8): m.add(woodblock2(m.vel(0.45 if k % 2 else 0.32, 0.06), hi=(k % 2 == 1)), at + k * 0.5, pan=0.5, rev=0.1, humanize=0.003)
            if kick: m.add(kick2(m.vel(0.6, 0.03), punch=0.4, low=0.6), at, rev=0.04, humanize=0.002)
            if stab_every and bar % stab_every == stab_every - 1:
                for x in voicing(cps, 50, 45, 57): m.add(brass2(midi_hz(x), 0.8 * m.beat, m.vel(0.6, 0.04), bright=0.45, voices=3, att=0.04), at + 3, rev=0.3, gain=stabg * stab_gain, humanize=0.004)
            for x in voicing(cps, 57, 52, 64): m.add(warm_pad(midi_hz(x), 4 * m.beat + 0.2, 0.3, bright=0.25), at, pan=0.3, rev=0.4, humanize=0)

    # A（今の曲と同じ順番）
    marimba_lead(melA, 0); jungle(progA, 0)
    # B 前半：ピチカートのしのび足、キックなし、ぶぉっは小さく1小節おき
    # B 前半のしのび足メロディ（2026-10-17 てじゃ「ピチカートやっぱちょい気になる、音変えてみても」→ 聴き比べ）
    melB1 = [x for x in melB if x[0] < 32]
    if blead == 'pizz': pizz(melB1, 16, gain=0.8 * leadg)
    else:
        for pos, d, mm in melB1:
            v = m.vel(0.85 if abs(pos - round(pos)) < 1e-6 else 0.7, 0.05); at = 16 * 4 + pos
            if blead == 'reeds':   # クラリネット＋1オクターブ下のファゴット（しのび足の定番）
                m.add(reed(midi_hz(mm), 0.32 * m.beat, v, kind='clarinet'), at, pan=0.2, rev=0.2, gain=leadg, humanize=0.006)
                m.add(reed(midi_hz(mm - 12), 0.32 * m.beat, v * 0.8, kind='bassoon'), at, pan=-0.2, rev=0.2, gain=leadg, humanize=0.006)
            elif blead == 'lowmarimba':   # 1オクターブ下の木のマリンバを短く（A のマリンバより低く・こもらせて）
                m.add(lp(marimba3(midi_hz(mm - 12), 0.35 * m.beat, v, hard=0.25), 1800, 2), at, pan=0.15, rev=0.18, gain=1.4 * leadg, humanize=0.006)
            elif blead == 'kalimba':   # カリンバ＋低いブラスを小さく
                m.add(kalimba(midi_hz(mm), 0.5 * m.beat, v, hard=0.4), at, pan=0.15, rev=0.22, gain=leadg, humanize=0.006)
                m.add(brass2(midi_hz(mm - 12), 0.28 * m.beat, v * 0.5, bright=0.2, voices=2, att=0.03, det_c=5), at, pan=-0.1, rev=0.2, gain=leadg * 1.5, humanize=0.006)
    jungle(progB[:8], 16, kick=False, stab_every=2, stab_gain=0.8, wood='none')
    # B 後半：マリンバが戻る、ぶぉっ毎小節、ウッドブロック細かく
    marimba_lead([x for x in melB if x[0] >= 32], 16)
    jungle(progB[8:], 24, stab_every=1, stab_gain=0.8, wood='eighth')
    # A'：後半8小節は1オクターブ上にピチカートを小さく
    marimba_lead(melA, 32); jungle(progA, 32)
    pizz([x for x in melA if x[0] >= 32], 32, gain=0.4, oct=12, low_brass=False)
    st = master(m, rt60=1.5, rev_level=0.33, damp=0.5, lowcut=30, warmth_db=0.5, air_db=1.0, comp_thresh=-16, comp_ratio=1.8, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_den_long.wav"))
    return st

def _old_bell(f, dur, vel=1.0):
    """古い音源のチェレスタ（make_bgm.celesta）は、余韻が鳴っている途中で音がブツッと切れる（端の音量が2割ほど残る）。
    伴奏の小さい音だと気づかないが、メロディにすると音割れのように聞こえた（2026-10-17 てじゃ）→ 最後の0.6秒をなめらかに消す"""
    y = old.celesta(f, dur, vel)
    n = min(len(y), int(0.6 * old.SR))
    y[-n:] *= np.cos(np.linspace(0, np.pi / 2, n)) ** 2
    return y

def _to_old_sr(y):
    """新しい音源（44.1kHz）の音を古い音源の曲（32kHz）に混ぜる用"""
    n = int(len(y) * old.SR / SR)
    return np.interp(np.arange(n) / old.SR, np.arange(len(y)) / SR, y)

def _b_lead(add, f, dur, kind, gain):
    """B 前半のメロディ（2026-10-17 チェレスタがメインだと てじゃ「気になる」→ ビブラフォン／低めの口笛から選ぶ）"""
    if kind == 'vibes': add(_to_old_sr(vibes(f, dur, 0.7, hard=0.3, rel=1.2)) * gain)
    elif kind == 'whistle': add(old.whistle(f, dur * 0.9, 0.85) * gain)

def _save_old(st, path, sr):
    import wave as _w
    with _w.open(path, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes())
    print(os.path.basename(path), f"{len(st)/sr:.1f}s")

def _match_old(st, orig_name, a_seconds, sr):
    """古い音源の曲は最大値でそろえる仕組みなので、のばすと A が小さくなる → 今の曲の A と同じ聞こえる大きさに戻す"""
    import wave as _w
    w = _w.open(os.path.join(OUT, orig_name)); o = np.frombuffer(w.readframes(w.getnframes()), np.int16).reshape(-1, 2) / 32768
    n = int(a_seconds * sr)
    g = np.sqrt((o[:n] ** 2).mean()) / (np.sqrt((st[:n] ** 2).mean()) + 1e-12)
    st = st * g
    pk = np.abs(st).max()
    if pk > 0.95: st = np.tanh(st / 0.95) * 0.95
    return st

# ---------------- きのこのたに（bgm_tower_i：F長調116BPM、古い音源のまま。口笛＋チェレスタ、はずむ低いブラス、プラック、ボンゴ）----------------
def tower(path=None, blead='vibes', bgain=1.6):   # 2026-10-17 てじゃ「ビブラフォンがいい」
    """A＝今の tower_i そのまま、B＝Bb から始まる16小節：前半8小節は口笛をお休みしてチェレスタがメロディ、ドラムはボンゴとシェイカーだけで
    のんびり、後半8小節は口笛が戻って2本目の口笛が3度下でハモり、ドラムも戻る→C で止めて A へ。A'＝今のメロディ＋後半8小節だけハモり。
    48小節＝1分39秒。音源は古い make_bgm.py のまま"""
    import make_bgm_tower as T
    s = T.Song(116, 48)
    progA = ['F', 'Dm', 'Bb', 'C', 'F', 'Dm', 'Gm', 'C', 'Bb', 'C', 'Am', 'Dm', 'Bb', 'C', 'F', 'F']
    progB = ['Bb', 'F', 'Gm', 'C', 'Bb', 'F', 'Dm', 'C', 'Gm', 'Am', 'Bb', 'C', 'Gm', 'Am', 'Bb', 'C']
    kp = T.NOTE['F']; MAJOR = T.MAJOR
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 2)],
          [(0, 1), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)]]
    a = T.make_melody(np.random.default_rng(141), progA[:4], kp, MAJOR, rA, 65, 82, 69)
    a2 = T.make_melody(np.random.default_rng(142), progA[4:8], kp, MAJOR, rA, 65, 82, a[-1][2])
    b = T.make_melody(np.random.default_rng(143), progA[8:12], kp, MAJOR, rA, 65, 84, 72)
    c = T.make_melody(np.random.default_rng(144), progA[12:16], kp, MAJOR, rA[:3] + [[(0, 4)]], 65, 82, 74)
    melA = T.end_on_root(a + T.transpose_rest(a2, 4) + T.transpose_rest(b, 8) + T.transpose_rest(c, 12), kp, 65, 82)
    rC = [[(0, 0.5), (0.5, 0.5), (1, 1), (2.5, 0.5), (3, 1)], [(0, 2), (2, 1), (3, 1)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 2)], [(0, 3)]]
    b1 = T.make_melody(np.random.default_rng(151), progB[:4], kp, MAJOR, rC, 67, 84, 74)
    b2 = T.make_melody(np.random.default_rng(152), progB[4:8], kp, MAJOR, rC, 67, 84, b1[-1][2])
    b3 = T.make_melody(np.random.default_rng(153), progB[8:12], kp, MAJOR, rA, 65, 82, 70)
    b4 = T.make_melody(np.random.default_rng(154), progB[12:16], kp, MAJOR, rA[:3] + [[(0, 2)]], 65, 82, b3[-1][2])
    melB = b1 + T.transpose_rest(b2, 4) + T.transpose_rest(b3, 8) + T.transpose_rest(b4, 12)
    pB, dB, mB = melB[-1]; melB[-1] = (pB, dB, T.nearest(mB, [T.NOTE['C'], T.NOTE['E'], T.NOTE['G']], 65, 82))

    def lead(mel, off, whistle=True, harmony=None):
        for pos, d, mm in mel:
            at = off * 4 + pos
            if whistle:
                s.add(T.whistle(midi_hz(mm), d * s.beat * 0.9, 0.9), at, pan=0.1, rev=0.25)
                s.add(_old_bell(midi_hz(mm + 12), d * s.beat, 0.3), at, pan=-0.3, rev=0.3)
            else:   # チェレスタがメロディ（1オクターブ上は鳴らさない）
                _b_lead(lambda y: s.add(y, at, pan=0.05, rev=0.32), midi_hz(mm + (0 if blead == 'vibes' else -12)), d * s.beat, blead, bgain)
            if harmony:
                s.add(T.whistle(midi_hz(harmony(mm, pos)), d * s.beat * 0.9, 0.42), at, pan=-0.3, rev=0.28)

    def groove(prog, off, drums='full'):
        for bar, cn in enumerate(prog):
            r, cps = T.chord(cn); at = (off + bar) * 4
            root = T.nearest(41, [r], 36, 47)
            vo = T.voicing(cps, 62, 57, 69)
            for beat in range(4):
                s.add(T.brass(midi_hz(root + (7 if beat % 2 else 0)), 0.35 * s.beat, 0.55), at + beat, pan=-0.15, rev=0.15)
                s.add(T.bass(midi_hz(root + (7 if beat % 2 else 0)), 0.35 * s.beat, 0.6, tone=0.4), at + beat, rev=0.05)
            for o in (0.5, 1.5, 2.5, 3.5):
                for mm in vo: s.add(T.pluck(midi_hz(mm), 0.25 * s.beat, 0.25, bright=1.3), at + o, pan=0.3, rev=0.2)
            for o, f in ((0.5, 300), (1.25, 420), (2.5, 300), (3.0, 420), (3.5, 420)): s.add(T.tom(f, 0.35), at + o, pan=-0.4, rev=0.12)
            if drums == 'full':
                s.add(T.kick(0.6), at, rev=0.05); s.add(T.kick(0.45), at + 2, rev=0.05)
                for beat in (1, 3): s.add(T.snare(0.45), at + beat, pan=0.2, rev=0.3)
            for k in range(8): s.add(T.shaker(0.45 if k % 2 else 0.3), at + k * 0.5, pan=0.45, rev=0.05)

    harmA = lambda mm, pos: third_below(mm, chord_at(progA, pos), kp, MAJOR)
    harmB = lambda mm, pos: third_below(mm, chord_at(progB, pos), kp, MAJOR)
    # A（今の曲と同じ順番）
    lead(melA, 0); groove(progA, 0)
    # B
    lead([x for x in melB if x[0] < 32], 16, whistle=False)
    lead([x for x in melB if x[0] >= 32], 16, harmony=harmB)
    groove(progB[:8], 16, drums='light'); groove(progB[8:], 24)
    # A'
    lead([x for x in melA if x[0] < 32], 32); lead([x for x in melA if x[0] >= 32], 32, harmony=harmA)
    groove(progA, 32)
    st = _match_old(s.render(1.2), "bgm_tower_i.wav", 16 * 4 * s.beat, T.SR)
    _save_old(st, path or os.path.join(OUT, "bgm_tower_long.wav"), T.SR)
    return st

# ---------------- ゴール後（bgm_aftergoal：F長調112BPM、古い音源のまま。ウクレレ風カッティング＋口笛＋手拍子）----------------
def aftergoal(path=None, blead='vibes', bgain=2.0):   # 2026-10-17 てじゃ「ビブラフォンがいい」
    """A＝今の aftergoal そのまま、B＝Dm から始まる16小節：前半8小節は口笛お休み・チェレスタがメロディ、ウクレレは1音ずつのつま弾き、
    手拍子なし。後半8小節は口笛とカッティングが戻って3度下のハモり→C で止めて A へ。A'＝今のメロディ＋後半8小節だけハモり。48小節＝1分43秒"""
    O = old
    s = O.Song(112, 48)
    progA = ['F', 'Dm', 'Bb', 'C'] * 2 + ['Bb', 'C', 'Am', 'Dm', 'Bb', 'C', 'F', 'F']
    progB = ['Dm', 'Bb', 'F', 'C', 'Dm', 'Bb', 'Gm', 'C', 'Bb', 'F', 'Gm', 'C', 'Bb', 'F', 'Gm', 'C']
    kp = O.NOTE['F']; MAJOR = O.MAJOR
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 3)]]
    a = O.make_melody(np.random.default_rng(91), progA[:4], kp, MAJOR, rA, 72, 86, 77)
    b = O.make_melody(np.random.default_rng(92), progA[8:12], kp, MAJOR, rA, 72, 86, 81)
    c = O.make_melody(np.random.default_rng(93), progA[12:16], kp, MAJOR, rA, 72, 86, 79)
    melA = O.end_on_root(a + O.transpose_rest(a, 4) + O.transpose_rest(b, 8) + O.transpose_rest(c, 12), kp, 72, 86)
    rC = [[(0, 1), (1, 1), (2, 2)], [(0, 0.5), (0.5, 0.5), (1, 1.5), (3, 1)], [(0, 2), (2, 1), (3, 1)], [(0, 3)]]
    b1 = O.make_melody(np.random.default_rng(101), progB[:4], kp, MAJOR, rC, 69, 84, 74)
    b2 = O.make_melody(np.random.default_rng(102), progB[4:8], kp, MAJOR, rC, 69, 84, b1[-1][2])
    b3 = O.make_melody(np.random.default_rng(103), progB[8:12], kp, MAJOR, rA, 72, 86, 77)
    b4 = O.make_melody(np.random.default_rng(104), progB[12:16], kp, MAJOR, rA[:3] + [[(0, 2)]], 72, 86, b3[-1][2])
    melB = b1 + O.transpose_rest(b2, 4) + O.transpose_rest(b3, 8) + O.transpose_rest(b4, 12)
    pB, dB, mB = melB[-1]; melB[-1] = (pB, dB, O.nearest(mB, [O.NOTE['C'], O.NOTE['E'], O.NOTE['G']], 72, 86))

    def lead(mel, off, whistle=True, harmony=None):
        for pos, d, mm in mel:
            at = off * 4 + pos
            if whistle: s.add(O.whistle(midi_hz(mm), d * s.beat * 0.9, 1.0), at, pan=0.1, rev=0.25)
            else: _b_lead(lambda y: s.add(y, at, pan=0.1, rev=0.3), midi_hz(mm + (0 if blead == 'vibes' else -12)), d * s.beat, blead, bgain)
            if harmony: s.add(O.whistle(midi_hz(harmony(mm, pos)), d * s.beat * 0.9, 0.45), at, pan=-0.3, rev=0.28)

    def band(prog, off, style='strum'):
        for bar, cn in enumerate(prog):
            r, cps = O.chord(cn); at = (off + bar) * 4
            voic = sorted(O.nearest(62 + (p - 62) % 12, [p], 57, 69) for p in cps)
            if style == 'strum':
                for o, vel in ((0, 0.5), (0.75, 0.25), (1.5, 0.45), (2, 0.5), (2.75, 0.25), (3.5, 0.45)):
                    for k, mm in enumerate(voic):
                        s.add(O.pluck(midi_hz(mm), 0.3 * s.beat, vel, bright=1.2), at + o + k * 0.012, pan=-0.3, rev=0.15)
            else:   # 1音ずつつま弾き
                seq = [voic[0], voic[2], voic[1], voic[2]] * 2
                for k, mm in enumerate(seq):
                    s.add(O.pluck(midi_hz(mm), 0.45 * s.beat, 0.42 if k % 4 == 0 else 0.32, bright=1.0), at + k * 0.5, pan=-0.3, rev=0.2)
            root = O.nearest(41, [r], 36, 47)
            s.add(O.bass(midi_hz(root), 0.8 * s.beat, 0.7, tone=0.3), at, rev=0.05)
            s.add(O.bass(midi_hz(root + 7), 0.8 * s.beat, 0.55, tone=0.3), at + 2, rev=0.05)
            if style == 'strum':
                for beat in (1, 3): s.add(O.snare(0.45), at + beat, pan=0.2, rev=0.25)
            for k in range(8): s.add(O.shaker((0.9 if k % 2 else 0.6) * (1.0 if style == 'strum' else 0.7)), at + k * 0.5, pan=0.35, rev=0.1)
            s.add(O.kick(0.45), at, rev=0.05)

    harmA = lambda mm, pos: third_below(mm, chord_at(progA, pos), kp, MAJOR)
    harmB = lambda mm, pos: third_below(mm, chord_at(progB, pos), kp, MAJOR)
    lead(melA, 0); band(progA, 0)
    lead([x for x in melB if x[0] < 32], 16, whistle=False); band(progB[:8], 16, style='pick')
    lead([x for x in melB if x[0] >= 32], 16, harmony=harmB); band(progB[8:], 24)
    lead([x for x in melA if x[0] < 32], 32); lead([x for x in melA if x[0] >= 32], 32, harmony=harmA); band(progA, 32)
    st = _match_old(s.render(1.2), "bgm_aftergoal.wav", 16 * 4 * s.beat, O.SR)
    _save_old(st, path or os.path.join(OUT, "bgm_aftergoal_long.wav"), O.SR)
    return st

if __name__ == '__main__':
    fn = globals()[sys.argv[1]]
    kw = {}
    for x in sys.argv[3:]:
        k, v = x.split('=', 1)
        try: v = float(v)
        except ValueError: pass
        kw[k] = v
    fn(sys.argv[2] if len(sys.argv) > 2 and sys.argv[2] != '-' else None, **kw)
