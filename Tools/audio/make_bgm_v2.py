# 新しい音源（fluffsynth）で、今の曲（make_bgm.py の作曲）を作り直す。まずはロビー（lobby_b）から。
import sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
import make_bgm as old
from fluffsynth import *

OUT = old.OUT

def lobby_felt(path=None, tone=0.45, sustain=1.0, mech=1.0, rt60=1.6, rev_level=0.32, rev_send=0.22):
    """lobby_b と同じ作曲。メロディとゆりかごを本物寄りのフェルトピアノ（felt_piano2）に、残響は近め"""
    m = Mix(60, 16, tail=5.0, seed=11)
    prog = ['Gmaj7', 'Em7', 'Cmaj7', 'D', 'Gmaj7', 'Em7', 'Cmaj7', 'Dsus4',
            'Cmaj7', 'Gmaj7', 'Am7', 'D', 'Cmaj7', 'Gmaj7', 'Am7', 'Gmaj7']
    kp = old.NOTE['G']; MAJOR = old.MAJOR
    rA = [[(0, 3), (3, 1)], [(0, 4)], [(0, 2), (2, 2)], [(0, 4)]]
    rB = [[(1, 3)], [(0, 3), (3, 1)], [(0, 4)], [(0, 4)]]
    a = old.make_melody(np.random.default_rng(71), prog[:4], kp, MAJOR, rA, 67, 79, 74)
    a2 = old.make_melody(np.random.default_rng(72), prog[4:8], kp, MAJOR, rA, 67, 79, a[-1][2])
    b = old.make_melody(np.random.default_rng(73), prog[8:12], kp, MAJOR, rB, 67, 79, 76)
    c = old.make_melody(np.random.default_rng(74), prog[12:16], kp, MAJOR, rA, 67, 79, 74)
    mel = a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12)
    mel = old.end_on_root(mel, kp, 67, 79)
    for pos, d, mm in mel:
        v = m.vel(0.62 if (pos % 4) == 0 else 0.5)
        m.add(felt_piano2(midi_hz(mm), d * m.beat, v, tone=tone, sustain=sustain, mech=mech), pos, pan=0.08, rev=rev_send, humanize=0.012)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        voic = sorted(old.nearest(55 + (p - 55) % 12, [p], 52, 64) for p in cps)
        for mm in voic:
            m.add(warm_pad(midi_hz(mm), 4 * m.beat + 0.3, 0.42, bright=0.25), bar * 4, rev=0.3, humanize=0)
        root = old.nearest(43, [r], 36, 47)
        m.add(sub_bass(midi_hz(root), 4 * m.beat, m.vel(0.15, 0.04)), bar * 4, pan=0, rev=0.06, humanize=0.004)
        arp = sorted(old.nearest(60 + (p - 60) % 12, [p], 55, 67) for p in cps)
        seq = [arp[0], arp[-1], arp[1], arp[-1]]
        for k in range(4):
            v = m.vel(0.38 if k == 0 else 0.29)
            m.add(felt_piano2(midi_hz(seq[k]), 1.0 * m.beat, v, tone=tone * 0.8, sustain=sustain, mech=mech * 0.8), bar * 4 + k,
                  pan=(-0.3 if k % 2 else 0.2), rev=rev_send, humanize=0.014)
    st = master(m, rt60=rt60, rev_level=rev_level, damp=0.6, lowcut=32, warmth_db=1.0, final_lp=7000, target_rms_db=-12.5)
    save(st, path or os.path.join(OUT, "bgm_lobby_d.wav"))
    return st

def lobby_wind(path=None, kind=0, vib=1.0, breath=1.0, oct=0, taper=0.0, release=0.09, soft=0.0):
    """lobby_b と同じ作曲。メロディを笛（kind 0=オカリナ 1=ソフトフルート 2=リコーダー）に。伴奏はフェルトピアノA"""
    kinds = ['ocarina', 'flute', 'recorder']; kname = kinds[int(kind)]
    m = Mix(60, 16, tail=5.0, seed=11)
    prog = ['Gmaj7', 'Em7', 'Cmaj7', 'D', 'Gmaj7', 'Em7', 'Cmaj7', 'Dsus4',
            'Cmaj7', 'Gmaj7', 'Am7', 'D', 'Cmaj7', 'Gmaj7', 'Am7', 'Gmaj7']
    kp = old.NOTE['G']; MAJOR = old.MAJOR
    rA = [[(0, 3), (3, 1)], [(0, 4)], [(0, 2), (2, 2)], [(0, 4)]]
    rB = [[(1, 3)], [(0, 3), (3, 1)], [(0, 4)], [(0, 4)]]
    a = old.make_melody(np.random.default_rng(71), prog[:4], kp, MAJOR, rA, 67, 79, 74)
    a2 = old.make_melody(np.random.default_rng(72), prog[4:8], kp, MAJOR, rA, 67, 79, a[-1][2])
    b = old.make_melody(np.random.default_rng(73), prog[8:12], kp, MAJOR, rB, 67, 79, 76)
    c = old.make_melody(np.random.default_rng(74), prog[12:16], kp, MAJOR, rA, 67, 79, 74)
    mel = a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12)
    mel = old.end_on_root(mel, kp, 67, 79)
    for pos, d, mm in mel:
        v = m.vel(0.7 if (pos % 4) == 0 else 0.6, 0.05)
        # 笛は音と音の間に少し息つぎ（長さを少し短く）
        m.add(wind(midi_hz(mm + 12 * int(oct)), d * m.beat * 0.93, v, kind=kname, vib=vib, breath=breath, taper=taper, release=release, soft=soft), pos, pan=0.06, rev=0.3, humanize=0.01)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        voic = sorted(old.nearest(55 + (p - 55) % 12, [p], 52, 64) for p in cps)
        for mm in voic:
            m.add(warm_pad(midi_hz(mm), 4 * m.beat + 0.3, 0.4, bright=0.25), bar * 4, rev=0.3, humanize=0)
        root = old.nearest(43, [r], 36, 47)
        m.add(sub_bass(midi_hz(root), 4 * m.beat, m.vel(0.15, 0.04)), bar * 4, pan=0, rev=0.06, humanize=0.004)
        arp = sorted(old.nearest(60 + (p - 60) % 12, [p], 55, 67) for p in cps)
        seq = [arp[0], arp[-1], arp[1], arp[-1]]
        for k in range(4):
            v = m.vel(0.34 if k == 0 else 0.26)
            m.add(felt_piano2(midi_hz(seq[k]), 1.0 * m.beat, v, tone=0.3, sustain=0.9, mech=0.6), bar * 4 + k,
                  pan=(-0.3 if k % 2 else 0.2), rev=0.22, humanize=0.014)
    st = master(m, rt60=1.8, rev_level=0.35, damp=0.6, lowcut=32, warmth_db=1.0, final_lp=8000, target_rms_db=-12.5)
    save(st, path or os.path.join(OUT, "bgm_lobby_e.wav"))
    return st

def lobby_e(path=None):
    """採用版（2026-10-07）：メロディをオカリナ、音の終わりをふわっと弱める、かなりやわらか（soft 1.0）"""
    return lobby_wind(path or os.path.join(OUT, "bgm_lobby_e.wav"), kind=0, taper=0.45, release=0.3, soft=1.0, breath=0.6, vib=0.6)

def basics2(path=None, swing=0.0, woody=0, mbox=0.0, lead='vibes', goct=0, ggain=1.0, ghard=0.5, moct=12, trms=-13.0, cthr=-16.0, vrel=1.4, vrev=0.42):
    """れんしゅう（basics）と同じ作曲：C長調128BPM。採用版（2026-10-07）はメロディがビブラフォン（引数なしで再現）。lead で他の楽器も試せる、指弾きベース、ギターのジャカッ、ドラム"""
    m = Mix(128, 16, tail=3.0, seed=21)
    prog = ['C', 'G', 'Am', 'F'] * 2 + ['F', 'G', 'Em', 'Am', 'F', 'G', 'C', 'C']
    kp = old.NOTE['C']; MAJOR = old.MAJOR
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2.5, 0.5), (3, 1)], [(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)]]
    rB = [[(0, 1), (1, 1), (2, 0.5), (2.5, 1.5)], [(0, 0.5), (0.5, 1), (1.5, 0.5), (2, 2)],
          [(0, 1), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 2), (2.5, 0.5), (3, 1)]]
    a = old.make_melody(np.random.default_rng(21), prog[:4], kp, MAJOR, rA, 72, 88, 76)
    a2 = [(p, d, mm) for p, d, mm in a]
    b = old.make_melody(np.random.default_rng(22), prog[8:12], kp, MAJOR, rB, 72, 88, 81)
    c = old.make_melody(np.random.default_rng(23), prog[12:16], kp, MAJOR, rA[:2] + rB[2:3] + [[(0, 3)]], 72, 88, 79)
    mel = a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12)
    mel = old.end_on_root(mel, kp, 72, 88)
    sw = lambda pos: pos + (swing if abs((pos % 1) - 0.5) < 1e-6 else 0.0)
    for pos, d, mm in mel:
        acc = 0.8 if abs(pos - round(pos)) < 1e-6 else 0.65
        if lead == 'none': pass
        elif lead in ('vibes', 'celesta', 'kalimba'):
            fn = {'vibes': vibes, 'celesta': celesta, 'kalimba': kalimba}[lead]
            kw = {'rel': vrel} if lead == 'vibes' else {}
            m.add(fn(midi_hz(mm + int(goct)), d * m.beat, m.vel(acc), **kw), sw(pos), pan=0.12, rev=(vrev if lead == 'vibes' else 0.25), gain=ggain, humanize=0.006)
        elif lead == 'glock': m.add(glock(midi_hz(mm + int(goct)), d * m.beat, m.vel(acc), hard=ghard), sw(pos), pan=0.12, rev=0.25, gain=ggain, humanize=0.006)
        elif lead == 'recorder': m.add(wind(midi_hz(mm), d * m.beat * 0.9, m.vel(acc + 0.1), kind='recorder', vib=0.4, breath=0.5, taper=0.2, release=0.08), sw(pos), pan=0.12, rev=0.2, gain=1.6, humanize=0.006)
        elif lead == 'pluck': m.add(ks_string(midi_hz(mm), d * m.beat, m.vel(acc), bright=0.6, damp_time=0.12), sw(pos), pan=0.12, rev=0.2, gain=3.5, humanize=0.006)
        elif lead == 'synth': m.add(soft_lead(midi_hz(mm), d * m.beat * 0.85, m.vel(acc)), sw(pos), pan=0.12, rev=0.2, gain=2.0, humanize=0.006)
        elif woody: m.add(marimba3(midi_hz(mm), d * m.beat, m.vel(acc), hard=0.35), sw(pos), pan=0.12, rev=0.18, humanize=0.006)
        else: m.add(marimba2(midi_hz(mm), d * m.beat, m.vel(acc), hard=0.55), sw(pos), pan=0.12, rev=0.18, humanize=0.006)
        if mbox > 0: m.add(music_box2(midi_hz(mm + int(moct)), d * m.beat, m.vel(0.16 * mbox)), sw(pos), pan=(0.12 if lead == 'none' else 0.5), rev=0.3, humanize=0.006)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(40, [r], 36, 47)
        for k in range(8):
            mm = root + (12 if k % 2 else 0)
            m.add(finger_bass(midi_hz(mm), 0.42 * m.beat, m.vel(0.68 if k % 2 == 0 else 0.48, 0.05), tone=0.35), sw(bar * 4 + k * 0.5), rev=0.03, humanize=0.004)
        voic = sorted(old.nearest(62 + (p - 62) % 12, [p], 60, 72) for p in cps)
        for j, off in enumerate((0.5, 1.5, 2.5, 3.5)):
            m.add(strum([midi_hz(x) for x in voic], 0.28 * m.beat, m.vel(0.5), bright=0.55, spread=0.009, down=(j % 2 == 0)), sw(bar * 4 + off), pan=-0.55, rev=0.15, humanize=0.005)
        for beat in (0, 2, 2.75 if bar % 2 else 2):
            m.add(kick2(m.vel(0.72, 0.04), punch=0.5, low=0.4), bar * 4 + beat, rev=0.02, humanize=0.002)
        for beat in (1, 3):
            m.add(snare2(m.vel(0.75, 0.05), tone=0.5, snap=0.5), bar * 4 + beat, pan=0.05, rev=0.15, humanize=0.003)
        for k in range(8):
            m.add(hat2(m.vel(0.8 if k % 2 else 0.55, 0.08), open_=(k == 7 and bar % 4 == 3)), sw(bar * 4 + k * 0.5), pan=0.42, rev=0.03, humanize=0.004)
    st = master(m, rt60=1.3, rev_level=0.35, damp=0.5, lowcut=30, warmth_db=0.5, air_db=1.0, comp_thresh=cthr, comp_ratio=2.0, target_rms_db=trms)
    save(st, path or os.path.join(OUT, "bgm_basics2.wav"))
    return st

def kaze2(path=None, fvib=0.8, fbreath=0.8, ftaper=0.2, fsoft=0.3, fgain=1.9, tgain=0.6, bgain=0.75):
    """かぜのみち（kaze_e と同じ作曲）：Aミクソリディア108BPM、民謡っぽい。フルートのメロディ（装飾音つき）、
    ギターのジャカジャカ、低いタムの「どん・どどん」、ドローン、4小節ごとに遠くでやわらかいベル1音。風の音は入れない"""
    from make_bgm_tower import voicing
    MIXO = [0, 2, 4, 5, 7, 9, 10]
    m = Mix(108, 16, tail=3.0, seed=31)
    prog = ['A', 'G', 'A', 'G', 'D', 'A', 'G', 'A', 'D', 'G', 'A', 'Em', 'D', 'G', 'A', 'A']
    kp = old.NOTE['A']
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5), (3.5, 0.5)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 2)],
          [(0, 1.5), (1.5, 0.25), (1.75, 0.25), (2, 1), (3, 1)], [(0, 3), (3, 1)]]
    a = old.make_melody(np.random.default_rng(321), prog[:4], kp, MIXO, rA, 69, 86, 76)
    a2 = old.make_melody(np.random.default_rng(322), prog[4:8], kp, MIXO, rA, 69, 86, a[-1][2])
    b = old.make_melody(np.random.default_rng(323), prog[8:12], kp, MIXO, rA, 71, 88, 81)
    c = old.make_melody(np.random.default_rng(324), prog[12:16], kp, MIXO, rA, 69, 86, 79)
    mel = old.end_on_root(a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12), kp, 69, 86)
    for i, (pos, d, mm) in enumerate(mel):
        v = m.vel(0.72 if abs(pos - round(pos)) < 1e-6 else 0.62, 0.05)
        m.add(wind(midi_hz(mm), d * m.beat * 0.92, v, kind='flute', vib=fvib, breath=fbreath, taper=ftaper, release=0.12, soft=fsoft), pos, pan=0.1, rev=0.32, gain=fgain, humanize=0.008)
        if d >= 1 and i % 3 == 0 and pos >= 0.12:   # 長い音の前に短い装飾音
            m.add(wind(midi_hz(mm + 2), 0.07, v * 0.6, kind='flute', vib=0, breath=fbreath * 0.6, release=0.03, soft=fsoft), pos - 0.12, pan=0.1, rev=0.28, gain=fgain, humanize=0)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        # 4本目は、下の3音より上にある一番低い和音の音（前は下の3音と同じ音が重なって、同じ高さの弦2本でうなっていた）
        vo = voicing(cps, 57, 52, 64)
        vo = vo + [next(x for x in voicing(cps, 64, 59, 71) + [y + 12 for y in voicing(cps, 64, 59, 71)] if x > vo[-1])]
        for j, (off, vel) in enumerate(((0, 0.5), (1, 0.36), (1.5, 0.3), (2.5, 0.36), (3, 0.4))):   # ストローク
            m.add(strum([midi_hz(x) for x in vo], 0.4 * m.beat, m.vel(vel), bright=0.5, spread=0.012, down=(off == int(off))), bar * 4 + off, pan=-0.45, rev=0.2, humanize=0.005)
        if bar % 4 == 0:   # ドローン
            m.add(warm_pad(midi_hz(old.nearest(45, [kp], 40, 50)), 16 * m.beat, 0.35, bright=0.3), bar * 4, rev=0.35, humanize=0)
            m.add(warm_pad(midi_hz(old.nearest(52, [(kp + 7) % 12], 48, 57)), 16 * m.beat, 0.22, bright=0.3), bar * 4, rev=0.35, humanize=0)
        root = old.nearest(45, [r], 40, 51)
        m.add(finger_bass(midi_hz(root), 1.5 * m.beat, m.vel(0.62, 0.04), tone=0.3), bar * 4, rev=0.04, gain=bgain, humanize=0.004)
        m.add(finger_bass(midi_hz(root), 1.0 * m.beat, m.vel(0.48, 0.04), tone=0.3), bar * 4 + 2.5, rev=0.04, gain=bgain, humanize=0.004)
        m.add(tom2(85, m.vel(0.8, 0.04)), bar * 4, pan=-0.12, rev=0.22, gain=tgain, humanize=0.003)
        m.add(tom2(85, m.vel(0.5, 0.04)), bar * 4 + 2, pan=-0.12, rev=0.22, gain=tgain, humanize=0.003)
        m.add(tom2(100, m.vel(0.45, 0.04)), bar * 4 + 2.5, pan=0.15, rev=0.22, gain=tgain, humanize=0.003)
        for k in range(8): m.add(shaker2(m.vel(0.6 if k % 2 else 0.38)), bar * 4 + k * 0.5, pan=0.45, rev=0.08, humanize=0.004)
        if bar % 4 == 3:   # 遠くでやわらかいベルを1音「ぽーん」
            m.add(celesta(midi_hz(old.nearest(84, cps, 79, 88)), 1.5 * m.beat, 0.3), bar * 4 + 2.5, pan=0.4, rev=0.6, humanize=0)
    st = master(m, rt60=1.8, rev_level=0.38, damp=0.5, lowcut=30, warmth_db=0.5, air_db=1.5, comp_thresh=-16, comp_ratio=1.8, target_rms_db=-13.0)
    save(st, path or os.path.join(OUT, "bgm_kaze2.wav"))
    return st

def pitfalls2(path=None, bgain=3.5, ggain=0.6, drive=4.0, mute=0.06, lead='brass', lgain=1.0, loct=0, btone=0.75, bassg=1.0, fill='none', fgain=1.0):
    """いわのみち（pitfalls_b と同じ作曲）：D短調100BPM、どっしりパワフル。ブラスのメロディ（1オクターブ下を重ねる）、
    ズンズン刻むパワーコード（左右に2本）、ピック弾きのベース、重いドラムと大きいスネア、4小節ごとにクラッシュとタムの駆け下り。
    最後の8小節はメロディの1オクターブ上にピコピコを重ねて盛り上げる"""
    m = Mix(100, 24, tail=3.0, seed=41)
    prog = ['Dm', 'Bb', 'C', 'A'] * 2 + ['Gm', 'Bb', 'Dm', 'A', 'Gm', 'Bb', 'C', 'A'] + ['Dm', 'Bb', 'C', 'A'] * 2
    kp = old.NOTE['D']; MINOR = old.MINOR
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5), (3.5, 0.5)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 1), (2, 1), (3, 1)], [(0, 3), (3, 0.5), (3.5, 0.5)]]
    rB = [[(0, 2), (2, 1), (3, 1)], [(0, 1.5), (1.5, 1.5), (3, 1)], [(0, 1), (1, 1), (2, 1), (3, 1)], [(0, 4)]]
    a = old.make_melody(np.random.default_rng(81), prog[:4], kp, MINOR, rA, 62, 76, 69)
    b = old.make_melody(np.random.default_rng(82), prog[8:12], kp, MINOR, rB, 62, 77, 67)
    b2 = old.make_melody(np.random.default_rng(83), prog[12:16], kp, MINOR, rB, 62, 77, 70)
    mel = a + old.transpose_rest(a, 4) + old.transpose_rest(b, 8) + old.transpose_rest(b2, 12) + old.transpose_rest(a, 16) + old.transpose_rest(a, 20)
    mel = old.end_on_root(mel, kp, 62, 76)
    for pos, d, mm0 in mel:
        mm = mm0 + 12 * int(loct)
        v = m.vel(0.85 if abs(pos - round(pos)) < 1e-6 else 0.72, 0.05)
        if lead == 'brass':
            m.add(brass2(midi_hz(mm), d * m.beat * 0.92, v, bright=0.55), pos, pan=0.15, rev=0.22, gain=bgain, humanize=0.006)
            m.add(brass2(midi_hz(mm - 12), d * m.beat * 0.92, v * 0.7, bright=0.4), pos, pan=-0.15, rev=0.22, gain=bgain, humanize=0.006)
        elif lead == 'horn':   # まるいホルン隊：ゆっくり吹き始め、明るさ控えめ、揺れ少なめ
            m.add(brass2(midi_hz(mm), d * m.beat * 0.95, v, bright=0.12, voices=2, att=0.07, det_c=4), pos, pan=0.15, rev=0.3, gain=bgain * lgain, humanize=0.008)
            m.add(brass2(midi_hz(mm - 12), d * m.beat * 0.95, v * 0.6, bright=0.05, voices=2, att=0.07, det_c=4), pos, pan=-0.15, rev=0.3, gain=bgain * lgain, humanize=0.008)
        elif lead == 'guitar':   # 歪んだリードギター（ロックっぽく）
            m.add(lead_guitar(midi_hz(mm), d * m.beat * 0.95, v), pos, pan=0.1, rev=0.25, gain=lgain, humanize=0.006)
            m.add(lead_guitar(midi_hz(mm), d * m.beat * 0.95, v * 0.7, bend=0.6), pos + 0.006, pan=-0.25, rev=0.25, gain=lgain * 0.8, humanize=0.006)
        elif lead == 'lowbrass':   # トロンボーン＋チューバ隊：重く、吹き始めに「ブァッ」
            m.add(brass2(midi_hz(mm), d * m.beat * 0.94, v, bright=0.45, voices=3, att=0.04, det_c=7), pos, pan=0.15, rev=0.2, gain=bgain * lgain, humanize=0.006)
            m.add(brass2(midi_hz(mm - 12), d * m.beat * 0.94, v * 0.75, bright=0.3, voices=2, att=0.05, det_c=5), pos, pan=-0.15, rev=0.2, gain=bgain * lgain, humanize=0.006)
        elif lead == 'heavyguitar':   # 低く太い歪みギター（オクターブ重ね）
            m.add(lead_guitar(midi_hz(mm), d * m.beat * 0.95, v, drive=8.0, bend=0.5), pos, pan=-0.3, rev=0.18, gain=lgain, humanize=0.005)
            m.add(lead_guitar(midi_hz(mm), d * m.beat * 0.95, v, drive=8.0, bend=0.5), pos + 0.008, pan=0.3, rev=0.18, gain=lgain, humanize=0.005)
            m.add(lead_guitar(midi_hz(mm - 12), d * m.beat * 0.95, v * 0.6, drive=8.0, bend=0.3), pos, pan=0.0, rev=0.15, gain=lgain, humanize=0.005)
        elif lead == 'old':   # 前の曲と同じシンプルなブラス（倍音8本、ゆっくり立ち上がる）を、今の仕上げで
            m.add(old.brass(midi_hz(mm), d * m.beat * 0.92, 1.2 * v), pos, pan=0.15, rev=0.25, gain=lgain, humanize=0.006)
            m.add(old.brass(midi_hz(mm - 12), d * m.beat * 0.92, 0.8 * v), pos, pan=-0.15, rev=0.25, gain=lgain, humanize=0.006)
        if pos >= 64: m.add(soft_lead(midi_hz(mm0 + 12), d * m.beat * 0.85, v * 0.5), pos, pan=0.3, rev=0.28, gain=2.0, humanize=0.006)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(38, [r], 33, 44)
        for k in range(8):
            m.add(finger_bass(midi_hz(root + (12 if k == 7 else 0)), 0.42 * m.beat, m.vel(0.8 if k % 2 == 0 else 0.62, 0.04), tone=btone), bar * 4 + k * 0.5, rev=0.03, gain=bassg, humanize=0.003)
        pr = old.nearest(50, [r], 45, 56) - 12
        if fill in ('ring', 'both'):   # 伸ばすパワーコード「ギャーン」：小節の頭と3拍目裏だけ、刻まずに鳴らしっぱなし
            for off, ln, vv in ((0, 2.5, 0.9), (2.5, 1.5, 0.7)):
                m.add(power2(midi_hz(pr), ln * m.beat, m.vel(vv, 0.03), drive=drive, mute=0.25), bar * 4 + off, pan=-0.55, rev=0.18, gain=0.45 * fgain, humanize=0.003)
                m.add(power2(midi_hz(pr), ln * m.beat, m.vel(vv, 0.03), drive=drive, mute=0.25), bar * 4 + off + 0.012, pan=0.55, rev=0.18, gain=0.45 * fgain, humanize=0.003)
        if fill in ('pad', 'both'):   # 低めの暗いパッドで和音をうめる
            vo = sorted(old.nearest(52 + (p - 52) % 12, [p], 48, 60) for p in cps)
            for x in vo: m.add(warm_pad(midi_hz(x), 4 * m.beat + 0.2, 0.42, bright=0.2), bar * 4, rev=0.3, gain=fgain, humanize=0)
        for k in range(8):
            if k in (3, 7) and bar % 2 == 1: continue
            vel = 1.0 if k in (0, 3, 6) else 0.6
            mt = mute * (2.5 if k in (0, 6) else 1.0)
            m.add(power2(midi_hz(pr), 0.38 * m.beat, m.vel(vel, 0.04), drive=drive, mute=mt), bar * 4 + k * 0.5, pan=-0.6, rev=0.1, gain=ggain, humanize=0.004)
            m.add(power2(midi_hz(pr), 0.38 * m.beat, m.vel(vel, 0.04), drive=drive, mute=mt), bar * 4 + k * 0.5 + 0.012, pan=0.6, rev=0.1, gain=ggain, humanize=0.004)
        for beat in (0, 1, 2, 3): m.add(kick2(m.vel(0.95 if beat % 2 == 0 else 0.8, 0.03), punch=0.7, low=0.7), bar * 4 + beat, rev=0.03, humanize=0.002)
        m.add(kick2(m.vel(0.7, 0.03), punch=0.7, low=0.7), bar * 4 + 2.5, rev=0.03, humanize=0.002)
        for beat in (1, 3): m.add(snare2(m.vel(1.0, 0.03), tone=0.7, snap=0.6), bar * 4 + beat, pan=0.05, rev=0.4, humanize=0.003)
        for k in range(8): m.add(hat2(m.vel(0.85 if k % 2 else 0.6, 0.06), open_=(k == 7)), bar * 4 + k * 0.5, pan=0.35, rev=0.03, humanize=0.003)
        if bar % 4 == 0: m.add(crash2(m.vel(0.85, 0.03)), bar * 4, pan=-0.25, rev=0.25, humanize=0)
        if bar % 4 == 3:
            for k, f in enumerate((130, 110, 90, 72)): m.add(tom2(f, m.vel(0.85, 0.04), skin=0.7), bar * 4 + 2 + k * 0.5, pan=-0.4 + 0.27 * k, rev=0.25, humanize=0.003)
    st = master(m, rt60=1.4, rev_level=0.32, damp=0.5, lowcut=32, warmth_db=0.5, air_db=2.5, comp_thresh=-16, comp_ratio=2.2, target_rms_db=-13.0)
    save(st, path or os.path.join(OUT, "bgm_pitfalls2.wav"))
    return st

def yami2(path=None, lead='vibes', lgain=1.0, cgain=1.0, dgain=0.28, drev=0.85):
    """よるのもり（yami_b と同じ作曲）：D短調70BPM、ゆったり・ふしぎ。メロディ（ビブラフォン or フェルトピアノ）に
    1オクターブ上のチェレスタを小さく重ねる、暗いパッド、息の「ふわー」、音階のしずく「ぽちょん」、心臓みたいな低いキック「どっ・くん」"""
    from make_bgm_tower import voicing
    from make_bgm_yami import drip, HMINOR
    m = Mix(70, 16, tail=4.0, seed=51)
    prog = ['Dm', 'Bb', 'Gm', 'A', 'Dm', 'Bb', 'Gm', 'A', 'Bb', 'C', 'Am', 'Dm', 'Gm', 'Bb', 'A', 'A']
    kp = old.NOTE['D']; MINOR = old.MINOR
    rA = [[(0, 2), (2, 1), (3, 1)], [(0, 3), (3, 1)], [(0, 1), (1, 1), (2, 2)], [(0, 4)]]
    a = old.make_melody(np.random.default_rng(211), prog[:4], kp, HMINOR, rA, 62, 79, 69)
    a2 = old.make_melody(np.random.default_rng(212), prog[4:8], kp, HMINOR, rA, 62, 79, a[-1][2])
    b = old.make_melody(np.random.default_rng(213), prog[8:12], kp, MINOR, rA, 64, 81, 72)
    c = old.make_melody(np.random.default_rng(214), prog[12:16], kp, HMINOR, rA, 62, 79, 70)
    mel = a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12)
    for pos, d, mm in mel:
        v = m.vel(0.7 if (pos % 4) == 0 else 0.6, 0.05)
        if lead == 'vibes': m.add(vibes(midi_hz(mm), d * m.beat, v, hard=0.3, trem=3.5, depth=0.35, rel=1.2), pos, pan=0.1, rev=0.5, gain=lgain, humanize=0.01)
        else: m.add(felt_piano2(midi_hz(mm), d * m.beat, v, tone=0.3, sustain=1.0, mech=0.5), pos, pan=0.1, rev=0.5, gain=lgain, humanize=0.01)
        m.add(celesta(midi_hz(mm + 12), d * m.beat, v * 0.3), pos + 0.02, pan=-0.35, rev=0.55, gain=cgain, humanize=0.006)
    rng = np.random.default_rng(215)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        for x in voicing(cps, 57, 52, 64):
            m.add(warm_pad(midi_hz(x), 4 * m.beat + 0.3, 0.42, bright=0.18), bar * 4, rev=0.45, humanize=0)
        m.add(finger_bass(midi_hz(old.nearest(38, [r], 33, 44)), 3.5 * m.beat, m.vel(0.5, 0.04), tone=0.15), bar * 4, rev=0.1, humanize=0.004)
        if bar % 2 == 0:   # 息の「ふわー」：こもったノイズがゆっくりふくらんで消える
            n = int(6 * m.beat * SR); t = np.arange(n) / SR
            nz = bp(rng.standard_normal(n), 300, 1500, 2) * np.sin(np.pi * t / t[-1]) ** 2
            m.add(nz * 0.05, bar * 4, pan=float(rng.uniform(-0.5, 0.5)), rev=0.5, humanize=0)
        m.add(kick2(m.vel(0.38, 0.03), punch=0.1, low=0.9), bar * 4, rev=0.12, humanize=0.002)   # どっ・くん
        m.add(kick2(m.vel(0.24, 0.03), punch=0.1, low=0.9), bar * 4 + 0.5, rev=0.12, humanize=0.002)
        sc = old.scale_for(kp, HMINOR, cps)
        for k in range(4):   # しずくの音階
            if rng.random() < 0.55:
                x = old.nearest(int(rng.integers(84, 96)), sc, 82, 98)
                m.add(lp(drip(midi_hz(x), 0.8), 3500, 1), bar * 4 + k + float(rng.choice([0, 0.5])), pan=float(rng.uniform(-0.8, 0.8)), rev=drev, gain=dgain, humanize=0.004)
    st = master(m, rt60=2.6, rev_level=0.45, damp=0.55, lowcut=30, warmth_db=0.5, air_db=0.5, comp_thresh=-18, comp_ratio=1.6, target_rms_db=-12.0)
    save(st, path or os.path.join(OUT, "bgm_yami2.wav"))
    return st

def den2(path=None, pgain=1.6, bgain=3.0, stabg=3.0, lead='marimba', lgain=1.0):
    """まもののす（den_a と同じ作曲）：A短調（ドリア）104BPM。しのび足のスタッカート（ピチカート＋1オクターブ下の短いブラス）、
    半音で寄るベース、ボンゴとウッドブロックのジャングルっぽいリズム、2小節ごとにブラスの「ぶぉっ」"""
    from make_bgm_tower import voicing
    DORIAN = [0, 2, 3, 5, 7, 9, 10]
    m = Mix(104, 16, tail=3.0, seed=61)
    prog = ['Am', 'Am', 'D', 'Am', 'Am', 'Am', 'Em', 'Am', 'F', 'G', 'Am', 'Am', 'F', 'G', 'E', 'E']
    kp = old.NOTE['A']; MINOR = old.MINOR
    rA = [[(0, 0.5), (1, 0.5), (1.5, 0.5), (2.5, 0.5), (3, 0.5)], [(0.5, 0.5), (1, 0.5), (2, 0.5), (3, 1)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (2, 0.5), (2.5, 0.5), (3, 0.5)], [(0, 1), (2, 0.5), (3, 0.5)]]
    a = old.make_melody(np.random.default_rng(301), prog[:4], kp, DORIAN, rA, 57, 74, 64)
    a2 = old.make_melody(np.random.default_rng(302), prog[4:8], kp, DORIAN, rA, 57, 74, a[-1][2])
    b = old.make_melody(np.random.default_rng(303), prog[8:12], kp, MINOR, rA, 59, 76, 67)
    c = old.make_melody(np.random.default_rng(304), prog[12:16], kp, [0, 2, 3, 5, 7, 8, 11], rA, 57, 76, 64)
    mel = a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12)
    for pos, d, mm in mel:
        v = m.vel(0.85 if abs(pos - round(pos)) < 1e-6 else 0.7, 0.05)
        # ピチカート：弦をはじいてすぐ止める
        if lead == 'reeds':   # クラリネット＋1オクターブ下のファゴット（しのび足の定番）
            m.add(reed(midi_hz(mm), 0.32 * m.beat, v, kind='clarinet'), pos, pan=0.2, rev=0.2, gain=lgain, humanize=0.006)
            m.add(reed(midi_hz(mm - 12), 0.32 * m.beat, v * 0.8, kind='bassoon'), pos, pan=-0.2, rev=0.2, gain=lgain, humanize=0.006)
            continue
        if lead == 'marimba':
            m.add(marimba3(midi_hz(mm), 0.5 * m.beat, v, hard=0.45), pos, pan=0.15, rev=0.2, gain=lgain, humanize=0.006)
            m.add(marimba3(midi_hz(mm - 12), 0.5 * m.beat, v * 0.6, hard=0.3), pos, pan=-0.2, rev=0.2, gain=lgain, humanize=0.006)
            continue
        if lead == 'kalimba':
            m.add(kalimba(midi_hz(mm), 0.5 * m.beat, v, hard=0.5), pos, pan=0.15, rev=0.22, gain=lgain, humanize=0.006)
            m.add(brass2(midi_hz(mm - 12), 0.28 * m.beat, v * 0.6, bright=0.25, voices=2, att=0.03, det_c=5), pos, pan=-0.1, rev=0.2, gain=bgain * 0.6, humanize=0.006)
            continue
        m.add(lp(ks_string(midi_hz(mm), 0.3 * m.beat, v, bright=0.35, damp_time=0.06), 2800, 2), pos, pan=0.15, rev=0.2, gain=pgain * 2.5, humanize=0.006)
        m.add(brass2(midi_hz(mm - 12), 0.28 * m.beat, v * 0.6, bright=0.25, voices=2, att=0.03, det_c=5), pos, pan=-0.1, rev=0.2, gain=bgain, humanize=0.006)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(33, [r], 28, 40)
        for beat in range(4):   # しのび足のベース
            x = root if beat % 2 == 0 else root + (7 if beat == 1 else 5)
            m.add(finger_bass(midi_hz(x), 0.35 * m.beat, m.vel(0.85 if beat == 0 else 0.6, 0.04), tone=0.4), bar * 4 + beat, rev=0.04, humanize=0.004)
        for off, hi in ((0, False), (0.75, True), (1.5, True), (2, False), (2.5, True), (3.25, True), (3.5, False)):
            m.add(bongo2(hi, m.vel(0.8 if not hi else 0.6, 0.06)), bar * 4 + off, pan=-0.45 if hi else -0.2, rev=0.12, humanize=0.004)
        for k in range(4): m.add(woodblock2(m.vel(0.45, 0.06), hi=True), bar * 4 + k + 0.5, pan=0.5, rev=0.1, humanize=0.003)
        m.add(kick2(m.vel(0.6, 0.03), punch=0.4, low=0.6), bar * 4, rev=0.04, humanize=0.002)
        if bar % 2 == 1:   # 「ぶぉっ」
            for x in voicing(cps, 50, 45, 57): m.add(brass2(midi_hz(x), 0.8 * m.beat, m.vel(0.6, 0.04), bright=0.45, voices=3, att=0.04), bar * 4 + 3, rev=0.3, gain=stabg, humanize=0.004)
        for x in voicing(cps, 57, 52, 64): m.add(warm_pad(midi_hz(x), 4 * m.beat + 0.2, 0.3, bright=0.25), bar * 4, pan=0.3, rev=0.4, humanize=0)
    st = master(m, rt60=1.5, rev_level=0.33, damp=0.5, lowcut=30, warmth_db=0.5, air_db=1.0, comp_thresh=-16, comp_ratio=1.8, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_den2.wav"))
    return st

def tower2(path=None, wgain=2.0, ggain=1.0, fgain=1.0, pzg=1.0):
    """きのこのたに（tower_i と同じ作曲）：F長調116BPM、ほがらかなおさんぽ。口笛のメロディ＋1オクターブ上の小さいグロッケン、
    はずむファゴット（低い音を4分で「ぽっ・ぽっ」）、裏拍のピチカートの和音、ボンゴの「ぽこぽこ」、軽いドラムとシェイカー"""
    from make_bgm_tower import voicing
    m = Mix(116, 16, tail=3.0, seed=71)
    prog = ['F', 'Dm', 'Bb', 'C', 'F', 'Dm', 'Gm', 'C', 'Bb', 'C', 'Am', 'Dm', 'Bb', 'C', 'F', 'F']
    kp = old.NOTE['F']; MAJOR = old.MAJOR
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 2)],
          [(0, 1), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)]]
    a = old.make_melody(np.random.default_rng(141), prog[:4], kp, MAJOR, rA, 65, 82, 69)
    a2 = old.make_melody(np.random.default_rng(142), prog[4:8], kp, MAJOR, rA, 65, 82, a[-1][2])
    b = old.make_melody(np.random.default_rng(143), prog[8:12], kp, MAJOR, rA, 65, 84, 72)
    c = old.make_melody(np.random.default_rng(144), prog[12:16], kp, MAJOR, rA[:3] + [[(0, 4)]], 65, 82, 74)
    mel = old.end_on_root(a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12), kp, 65, 82)
    for pos, d, mm in mel:
        v = m.vel(0.72 if abs(pos - round(pos)) < 1e-6 else 0.62, 0.05)
        # 口笛：ほぼ純音、息少し、速めの細かいビブラート。1オクターブ上で吹く
        m.add(wind(midi_hz(mm + 12), d * m.beat * 0.9, v, kind='ocarina', vib=1.4, breath=0.35, taper=0.25, release=0.08, soft=0.3), pos, pan=0.1, rev=0.25, gain=wgain, humanize=0.008)
        m.add(glock(midi_hz(mm + 24), d * m.beat, v * 0.22, hard=0.3, ring=0.7), pos, pan=-0.3, rev=0.3, gain=ggain, humanize=0.004)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(41, [r], 36, 47)
        vo = voicing(cps, 62, 57, 69)
        for beat in range(4):
            x = root + (7 if beat % 2 else 0)
            m.add(reed(midi_hz(x + 12), 0.3 * m.beat, m.vel(0.7 if beat % 2 == 0 else 0.6, 0.04), kind='bassoon', breath=0.4), bar * 4 + beat, pan=-0.2, rev=0.15, gain=fgain * 1.4, humanize=0.004)
            m.add(finger_bass(midi_hz(x), 0.35 * m.beat, m.vel(0.6, 0.04), tone=0.35), bar * 4 + beat, rev=0.04, humanize=0.004)
        for off in (0.5, 1.5, 2.5, 3.5):
            for k, x in enumerate(vo): m.add(lp(ks_string(midi_hz(x), 0.22 * m.beat, m.vel(0.4, 0.06), bright=0.4, damp_time=0.05), 3000, 2), bar * 4 + off + k * 0.006, pan=0.35, rev=0.2, gain=pzg * 1.6, humanize=0.003)
        for off, hi in ((0.5, False), (1.25, True), (2.5, False), (3.0, True), (3.5, True)):
            m.add(bongo2(hi, m.vel(0.5, 0.06)), bar * 4 + off, pan=-0.4, rev=0.12, humanize=0.004)
        m.add(kick2(m.vel(0.55, 0.03), punch=0.4, low=0.5), bar * 4, rev=0.03, humanize=0.002)
        m.add(kick2(m.vel(0.42, 0.03), punch=0.4, low=0.5), bar * 4 + 2, rev=0.03, humanize=0.002)
        for beat in (1, 3): m.add(snare2(m.vel(0.42, 0.04), tone=0.5, snap=0.4), bar * 4 + beat, pan=0.2, rev=0.3, humanize=0.003)
        for k in range(8): m.add(shaker2(m.vel(0.5 if k % 2 else 0.32, 0.06)), bar * 4 + k * 0.5, pan=0.45, rev=0.05, humanize=0.004)
    st = master(m, rt60=1.4, rev_level=0.3, damp=0.5, lowcut=30, warmth_db=0.5, air_db=1.5, comp_thresh=-16, comp_ratio=1.8, target_rms_db=-12.5)
    save(st, path or os.path.join(OUT, "bgm_tower2.wav"))
    return st

def aftergoal2(path=None, lead='whistle', lgain=1.0):
    """ゴールしたあと（after_goal と同じ作曲）：F長調112BPM、軽快。ウクレレのカッティング、口笛 or ビブラフォンのメロディ、手拍子とシェイカー"""
    m = Mix(112, 16, tail=3.0, seed=81)
    prog = ['F', 'Dm', 'Bb', 'C'] * 2 + ['Bb', 'C', 'Am', 'Dm', 'Bb', 'C', 'F', 'F']
    kp = old.NOTE['F']; MAJOR = old.MAJOR
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 3)]]
    a = old.make_melody(np.random.default_rng(91), prog[:4], kp, MAJOR, rA, 72, 86, 77)
    b = old.make_melody(np.random.default_rng(92), prog[8:12], kp, MAJOR, rA, 72, 86, 81)
    c = old.make_melody(np.random.default_rng(93), prog[12:16], kp, MAJOR, rA, 72, 86, 79)
    mel = old.end_on_root(a + old.transpose_rest(a, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12), kp, 72, 86)
    for pos, d, mm in mel:
        v = m.vel(0.72 if abs(pos - round(pos)) < 1e-6 else 0.62, 0.05)
        if lead == 'whistle':
            m.add(wind(midi_hz(mm), d * m.beat * 0.9, v, kind='ocarina', vib=1.4, breath=0.35, taper=0.25, release=0.08, soft=0.3), pos, pan=0.1, rev=0.25, gain=1.1 * lgain, humanize=0.008)
        else:
            m.add(vibes(midi_hz(mm), d * m.beat, v, hard=0.45, rel=0.6), pos, pan=0.1, rev=0.3, gain=1.3 * lgain, humanize=0.006)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        voic = sorted(old.nearest(62 + (p - 62) % 12, [p], 57, 69) for p in cps)
        # ウクレレ：ジャッ・ジャ・ジャッ・ジャ（ダウン強め、アップ弱め）
        for off, vel in ((0, 0.5), (0.75, 0.28), (1.5, 0.45), (2, 0.5), (2.75, 0.28), (3.5, 0.45)):
            m.add(strum([midi_hz(x) for x in voic], 0.3 * m.beat, m.vel(vel), bright=0.6, spread=0.01, down=(vel > 0.3)), bar * 4 + off, pan=-0.35, rev=0.15, humanize=0.004)
        root = old.nearest(41, [r], 36, 47)
        m.add(finger_bass(midi_hz(root), 0.8 * m.beat, m.vel(0.6, 0.04), tone=0.3), bar * 4, rev=0.04, humanize=0.004)
        m.add(finger_bass(midi_hz(root + 7), 0.8 * m.beat, m.vel(0.48, 0.04), tone=0.3), bar * 4 + 2, rev=0.04, humanize=0.004)
        for beat in (1, 3): m.add(clap2(m.vel(0.6, 0.05)), bar * 4 + beat, pan=0.2, rev=0.25, humanize=0.004)
        for k in range(8): m.add(shaker2(m.vel(0.6 if k % 2 else 0.4, 0.06)), bar * 4 + k * 0.5, pan=0.4, rev=0.06, humanize=0.004)
        m.add(kick2(m.vel(0.45, 0.03), punch=0.3, low=0.5), bar * 4, rev=0.03, humanize=0.002)
    st = master(m, rt60=1.3, rev_level=0.3, damp=0.5, lowcut=30, warmth_db=0.5, air_db=1.5, comp_thresh=-16, comp_ratio=1.8, target_rms_db=-13.0)
    save(st, path or os.path.join(OUT, "bgm_aftergoal2.wav"))
    return st

def fanfare2(path=None, bgain=3.5, top='glock', total=5.0):
    """ゴールのファンファーレ（fanfare と同じ作曲、ループしない5秒）：タタタ・ターン！→ラ・シ・ド〜〜。
    ブラス（メロディ＋1オクターブ上）、低いブラスの和音、ベース、スネアのロール→キック＋クラッシュ、タムの駆け下り"""
    bpm = 132; beat = 60 / bpm
    n = int(total * SR)
    dry = np.zeros((n, 2)); send = np.zeros((n, 2))
    def add(y, t, pan=0.0, rev=0.25, g=1.0):
        i = int(t * SR); mlen = min(len(y), n - i)
        if mlen <= 0: return
        gl = np.sqrt(0.5 * (1 - pan)); gr = np.sqrt(0.5 * (1 + pan))
        z = np.stack([y[:mlen] * gl, y[:mlen] * gr], 1) * g
        dry[i:i + mlen] += z; send[i:i + mlen] += z * rev
    mel = [(0, 0.33, 67), (0.33, 0.33, 67), (0.66, 0.33, 67), (1.0, 1.0, 72), (2.0, 0.5, 69), (2.5, 0.5, 71), (3.0, 3.0, 72)]
    for b0, d, mm in mel:
        v = 0.9 if d >= 1 else 0.78
        add(brass2(midi_hz(mm), d * beat * 0.92, v, bright=0.7, voices=3, att=0.025), b0 * beat, pan=0.1, g=bgain)
        add(brass2(midi_hz(mm + 12), d * beat * 0.92, v * 0.45, bright=0.55, voices=2, att=0.025), b0 * beat, pan=-0.1, g=bgain)
        if top == 'glock': add(glock(midi_hz(mm + 12), d * beat, 0.35, hard=0.5, ring=0.8), b0 * beat, pan=0.3, rev=0.35)
    for b0, d, ch in [(0, 1.0, [60, 64, 67]), (1.0, 1.0, [60, 64, 67]), (2.0, 0.5, [60, 65, 69]), (2.5, 0.5, [62, 67, 71]), (3.0, 3.0, [60, 64, 67, 72])]:
        for x in ch: add(brass2(midi_hz(x - 12), d * beat * 0.95, 0.5, bright=0.4, voices=2, att=0.03), b0 * beat, pan=-0.3, g=bgain)
        add(finger_bass(midi_hz(ch[0] - 24), d * beat, 0.85, tone=0.5), b0 * beat, rev=0.05, g=2.2)
    for k in range(6): add(snare2(0.45 + 0.1 * k, tone=0.6, snap=0.6), k * beat / 6, pan=0.1, rev=0.2)
    add(kick2(0.95, punch=0.7, low=0.6), 1.0 * beat, rev=0.05, g=1.6); add(crash2(0.9), 1.0 * beat, pan=-0.2, rev=0.3, g=1.8)
    add(kick2(0.75, punch=0.7, low=0.6), 2.0 * beat, rev=0.05, g=1.6)
    add(kick2(0.95, punch=0.7, low=0.6), 3.0 * beat, rev=0.05, g=1.6); add(crash2(1.0, dur=3.0), 3.0 * beat, pan=0.2, rev=0.3, g=1.8)
    for k, f in enumerate((150, 125, 100)): add(tom2(f, 0.75, skin=0.7), 2.5 * beat + k * beat / 6, pan=-0.3 + 0.3 * k, rev=0.25)
    ir = reverb_ir(1.6, damp=0.5)
    wet = np.stack([fftconv(send[:, 0], ir[:, 0])[:n], fftconv(send[:, 1], ir[:, 1])[:n]], 1)
    st = dry + 0.4 * wet
    st = hp(st, 30, 2)
    st = shelf(st, 6000, 1.5, 'high')
    st /= np.max(np.abs(st)) + 1e-9
    st = compressor(st, thresh_db=-14, ratio=1.6, makeup_db=0.0)
    st = st / (np.max(np.abs(st)) + 1e-9) * 1.4
    st = limiter(st, ceiling=0.85)
    f = int(0.4 * SR); st[-f:] *= np.linspace(1, 0, f)[:, None]
    st[:int(0.002 * SR)] *= np.linspace(0, 1, int(0.002 * SR))[:, None]
    save(st, path or os.path.join(OUT, "jingle_goal2.wav"))
    return st

def _jingle_render(build, total, path, rt=1.6, peak_drive=1.05):
    """ジングル（ループしない短い曲）の仕上げ共通。build(add) で音を置く。add(y, 秒, pan, rev, g)"""
    n = int(total * SR)
    dry = np.zeros((n, 2)); send = np.zeros((n, 2))
    def add(y, t, pan=0.0, rev=0.25, g=1.0):
        i = int(round(t * SR)); mlen = min(len(y), n - i)
        if mlen <= 0: return
        if y.ndim == 2: z = y[:mlen] * g
        else:
            gl = np.sqrt(0.5 * (1 - pan)); gr = np.sqrt(0.5 * (1 + pan))
            z = np.stack([y[:mlen] * gl, y[:mlen] * gr], 1) * g
        dry[i:i + mlen] += z; send[i:i + mlen] += z * rev
    build(add)
    ir = reverb_ir(rt, damp=0.5)
    wet = np.stack([fftconv(send[:, 0], ir[:, 0])[:n], fftconv(send[:, 1], ir[:, 1])[:n]], 1)
    st = hp(dry + 0.4 * wet, 30, 2)
    st = shelf(st, 6000, 1.5, 'high')
    st /= np.max(np.abs(st)) + 1e-9
    st = compressor(st, thresh_db=-14, ratio=1.6)
    st = limiter(st / (np.max(np.abs(st)) + 1e-9) * peak_drive, ceiling=0.85)
    f = int(0.5 * SR); st[-f:] *= np.linspace(1, 0, f)[:, None] ** 2
    st[:int(0.002 * SR)] *= np.linspace(0, 1, int(0.002 * SR))[:, None]
    save(st, path)
    return st

def fanfare_a(path=None, bgain=3.5):
    """A「やったー！」：王道の勝利ファンファーレ。C長調 140BPM。
    3連符で駆け上がる「ソ・ド・ミ」→「ソー・ミ・ソ」→ Ab → Bb → C の和音で「ジャン・ジャン・ジャーン！」（♭6→♭7→1の勝利の進行）"""
    beat = 60 / 140; T = lambda b: b * beat
    mel = [(0, 1/3, 67), (1/3, 1/3, 72), (2/3, 1/3, 76), (1, 1, 79), (2, 0.5, 76), (2.5, 0.5, 79),
           (3, 0.9, 80), (4, 0.9, 82), (5, 3, 84)]
    chords = [(1, 2, [60, 64, 67], 36), (3, 0.9, [56, 60, 63], 32), (4, 0.9, [58, 62, 65], 34), (5, 3, [60, 64, 67, 72], 36)]
    def build(add):
        for b0, d, mm in mel:
            v = 0.9 if d >= 0.9 else 0.75
            add(brass2(midi_hz(mm - 12), T(d) * 0.9, v, bright=0.7, voices=3, att=0.02), T(b0), pan=0.1, g=bgain)
            add(brass2(midi_hz(mm), T(d) * 0.9, v * 0.4, bright=0.55, voices=2, att=0.02), T(b0), pan=-0.1, g=bgain)
        for b0, d, ch, bs in chords:
            for x in ch: add(brass2(midi_hz(x - 12), T(d) * 0.92, 0.5, bright=0.45, voices=2, att=0.025), T(b0), pan=-0.3, g=bgain)
            add(finger_bass(midi_hz(bs), T(d), 0.85, tone=0.5), T(b0), rev=0.05, g=2.2)
            add(kick2(0.95, punch=0.7, low=0.6), T(b0), rev=0.05, g=1.6)
        for k in range(3): add(snare2(0.55 + 0.15 * k, tone=0.6, snap=0.6), T(k / 3), pan=0.1, rev=0.2)
        add(crash2(0.9), T(1), pan=-0.2, rev=0.3, g=1.6)
        add(snare2(0.9, tone=0.6, snap=0.6), T(3), rev=0.25); add(snare2(0.9, tone=0.6, snap=0.6), T(4), rev=0.25)
        add(crash2(1.0, dur=3.0), T(5), pan=0.2, rev=0.3, g=1.9)
        for k in range(4): add(glock(midi_hz([84, 88, 91, 96][k]), 0.3, 0.3, hard=0.5, ring=0.8), T(5 + k * 0.25), pan=0.4, rev=0.35)
    return _jingle_render(build, T(5) + 2.4, path or os.path.join(OUT, "jingle_goal_a.wav"))

def fanfare_b(path=None):
    """B「ふわっとおめでとう」：かわいい系。F長調 128BPM。ビブラフォン＋グロッケンで「ドレミファソ〜」と駆け上がって、
    跳ねる「ソ・ラ・ド〜」、最後はウクレレのジャカッ＋鈴のキラキラ、ベースの「ぽーん」。ブラスなし、ふわふわの世界に合わせて"""
    beat = 60 / 128; T = lambda b: b * beat
    run = [65, 67, 69, 70, 72, 74, 76, 77]
    mel = [(k * 0.25, 0.25, x) for k, x in enumerate(run)] + [(2, 0.5, 81), (2.5, 0.5, 79), (3, 0.5, 81), (3.5, 0.5, 84), (4, 3, 89)]
    def build(add):
        for b0, d, mm in mel:
            v = 0.75 if d < 0.5 else 0.85
            add(vibes(midi_hz(mm - 12), T(d), v, hard=0.5, rel=0.8), T(b0), pan=0.1, rev=0.3, g=1.1)
            add(glock(midi_hz(mm), T(d), v * 0.25, hard=0.4, ring=0.8), T(b0), pan=-0.25, rev=0.35)
        for b0, ch, bs in [(0, [65, 69, 72], 41), (2, [62, 65, 70], 46), (3, [64, 67, 72], 48), (4, [65, 69, 72, 77], 41)]:
            add(strum([midi_hz(x) for x in ch], T(0.8 if b0 < 4 else 3), 0.55, bright=0.6, spread=0.012), T(b0), pan=-0.35, rev=0.2, g=1.4)
            add(finger_bass(midi_hz(bs), T(0.9 if b0 < 4 else 2.5), 0.75, tone=0.3), T(b0), rev=0.05, g=1.8)
        add(kick2(0.6, punch=0.3, low=0.5), T(2), g=1.2); add(kick2(0.6, punch=0.3, low=0.5), T(3), g=1.2); add(kick2(0.8, punch=0.3, low=0.5), T(4), g=1.2)
        add(clap2(0.6), T(3), pan=0.2, rev=0.25); add(clap2(0.75), T(4), pan=0.2, rev=0.3)
        for k in range(10): add(shaker2(0.4 + 0.04 * k), T(4 + k * 0.12), pan=0.5 - 0.1 * k, rev=0.3)
        for k, x in enumerate([101, 98, 96, 93, 89]): add(glock(midi_hz(x), 0.2, 0.18, hard=0.6, ring=0.6), T(4.25 + k * 0.12), pan=0.5 - 0.2 * k, rev=0.45)
    return _jingle_render(build, T(4) + 2.6, path or os.path.join(OUT, "jingle_goal_b.wav"), rt=1.9)

def fanfare_c(path=None, bgain=3.0):
    """C「ゴールテープ！」：元気でちょっとおどけた感じ。G長調 150BPM。ブラスの「タッタ・タッタ・タ〜ン」と合いの手、
    ためて「・・・ジャン！」（一瞬止まってから決める）"""
    beat = 60 / 150; T = lambda b: b * beat
    mel = [(0, 0.5, 74), (0.75, 0.25, 74), (1, 0.5, 79), (1.75, 0.25, 79), (2, 1.5, 83), (3.5, 0.5, 81), (4, 0.5, 79), (4.5, 0.5, 81),
           (5, 0.25, 83), (5.25, 0.25, 84), (5.5, 0.25, 85), (6.5, 2.5, 86)]
    def build(add):
        for b0, d, mm in mel:
            v = 0.9 if d >= 1 else 0.78
            add(brass2(midi_hz(mm - 12), T(d) * 0.88, v, bright=0.75, voices=3, att=0.018), T(b0), pan=0.1, g=bgain)
            add(brass2(midi_hz(mm - 24), T(d) * 0.88, v * 0.5, bright=0.4, voices=2, att=0.02), T(b0), pan=-0.15, g=bgain)
        for b0, d, ch, bs in [(0, 1, [55, 59, 62], 43), (1, 1, [55, 59, 62], 43), (2, 1.5, [60, 64, 67], 48), (3.5, 1.5, [62, 66, 69], 50), (6.5, 2.5, [55, 59, 62, 67], 43)]:
            for x in ch: add(brass2(midi_hz(x - 12), T(d) * 0.9, 0.45, bright=0.45, voices=2, att=0.02), T(b0), pan=-0.3, g=bgain)
            add(finger_bass(midi_hz(bs - 12), T(d), 0.85, tone=0.5), T(b0), rev=0.05, g=2.2)
        for bb in (0, 1, 2, 3.5, 6.5): add(kick2(0.9, punch=0.7, low=0.6), T(bb), g=1.6)
        for bb in (0.75, 1.75, 2.5, 3, 4, 4.5): add(snare2(0.7, tone=0.6, snap=0.6), T(bb), pan=0.1, rev=0.2)
        for k in range(4): add(tom2([160, 140, 120, 100][k], 0.75, skin=0.7), T(5 + k * 0.25), pan=-0.3 + 0.2 * k, rev=0.25)
        add(crash2(1.0, dur=3.0), T(6.5), pan=0.2, rev=0.3, g=1.9)
        add(glock(midi_hz(98), 1.0, 0.3, hard=0.5), T(6.5), pan=0.4, rev=0.4)
    return _jingle_render(build, T(6.5) + 2.4, path or os.path.join(OUT, "jingle_goal_c.wav"))

def fanfare_d(path=None, hgain=3.0):
    """D「ほっこり達成」：やさしいオーケストラ風。D長調 96BPM。ハープのグリッサンドで駆け上がって、
    ホルンの「ミ〜ファ#ソ〜ラ〜」、あったかい弦（パッド）、最後は鐘（ビブラフォン＋グロッケン）がふわっと鳴る"""
    beat = 60 / 96; T = lambda b: b * beat
    def build(add):
        sc = [62, 64, 66, 69, 71, 74, 76, 78, 81, 83, 86]   # D ペンタトニック
        for k, x in enumerate(sc): add(ks_string(midi_hz(x), 0.6, 0.45 + 0.03 * k, bright=0.55), T(k * 0.09), pan=-0.4 + 0.08 * k, rev=0.45, g=2.0)
        mel = [(1, 1, 76), (2, 0.5, 78), (2.5, 0.5, 79), (3, 1, 81), (4, 3.5, 86)]
        for b0, d, mm in mel:
            add(brass2(midi_hz(mm - 12), T(d) * 0.95, 0.8, bright=0.18, voices=2, att=0.06, det_c=4), T(b0), pan=0.1, rev=0.4, g=hgain)
            add(wind(midi_hz(mm), T(d) * 0.95, 0.6, kind='flute', vib=0.6, breath=0.4, taper=0.3, release=0.2, soft=0.5), T(b0), pan=-0.2, rev=0.4, g=1.2)
        for b0, d, ch, bs in [(1, 2, [62, 66, 69], 38), (3, 1, [67, 71, 74], 43), (4, 3.5, [62, 66, 69, 74], 38)]:
            for x in ch: add(warm_pad(midi_hz(x), T(d) + 0.3, 0.5, bright=0.35), T(b0), rev=0.4, g=1.0)
            add(finger_bass(midi_hz(bs), T(d), 0.7, tone=0.25), T(b0), rev=0.1, g=1.8)
        add(timp := tom2(73, 0.8, skin=0.3), T(4), rev=0.35, g=1.4)
        for k, x in enumerate([86, 90, 93, 98]): add(vibes(midi_hz(x), T(2.5), 0.55, hard=0.4, rel=1.2), T(4 + k * 0.12), pan=0.3 - 0.15 * k, rev=0.5, g=0.9)
        add(glock(midi_hz(98), 1.5, 0.25, hard=0.4), T(4.5), pan=0.4, rev=0.5)
    return _jingle_render(build, T(4) + 3.0, path or os.path.join(OUT, "jingle_goal_d.wav"), rt=2.2)

def fanfare_e(path=None):
    """E「ピロリロリン♪」：ゲームのクリア音っぽい、やわらかピコピコ。C長調 160BPM。
    16分の「ドミソド ミソドミ」で駆け上がって、「ソ・ソ・ソ・ド〜♪」。下でピコピコのベースとハイハット、最後に和音"""
    beat = 60 / 160; T = lambda b: b * beat
    def build(add):
        run = [60, 64, 67, 72, 64, 67, 72, 76, 67, 72, 76, 79]
        for k, x in enumerate(run): add(soft_lead(midi_hz(x + 12), T(0.25) * 0.8, 0.6 + 0.02 * k, pulse=0.25, cutoff=3.0), T(k * 0.25), pan=-0.2 + 0.04 * k, rev=0.15, g=2.0)
        mel = [(3.5, 0.33, 79), (3.83, 0.33, 79), (4.17, 0.33, 79), (4.5, 3, 84)]
        for b0, d, mm in mel:
            add(soft_lead(midi_hz(mm + 12), T(d) * 0.88, 0.8, pulse=0.35, cutoff=2.5), T(b0), pan=0.1, rev=0.2, g=2.2)
            add(soft_lead(midi_hz(mm), T(d) * 0.88, 0.5, pulse=0.5, cutoff=2.0), T(b0), pan=-0.1, rev=0.2, g=2.0)
        for k in range(14): add(soft_lead(midi_hz([36, 48][k % 2]), T(0.2), 0.7, pulse=0.5, cutoff=1.5), T(k * 0.25), rev=0.05, g=2.2)
        for x in [60, 64, 67, 72]: add(soft_lead(midi_hz(x), T(3), 0.4, pulse=0.4, cutoff=1.8), T(4.5), pan=-0.3, rev=0.25, g=1.6)
        add(soft_lead(midi_hz(36), T(2.5), 0.8, pulse=0.5, cutoff=1.5), T(4.5), rev=0.05, g=2.2)
        for k in range(14): add(hat2(0.5 if k % 2 else 0.35), T(k * 0.25), pan=0.3, rev=0.05)
        add(kick2(0.8, punch=0.6, low=0.5), T(4.5), g=1.4); add(crash2(0.6, dur=2.5), T(4.5), pan=0.2, rev=0.3, g=1.2)
    return _jingle_render(build, T(4.5) + 2.4, path or os.path.join(OUT, "jingle_goal_e.wav"), rt=1.2, peak_drive=2.2)

def fanfare_f(path=None, bgain=3.2):
    """F「パレード」：マーチ風。B♭長調 120BPM。スネアの「タタタ・タタタ」、ブラスバンドの「タ・ラ・ラ・ター！」、
    グロッケンがメロディをなぞる、シンバルの「ジャーン」で決め、最後にチューバの「ぶっ・ぶん」でおどけて終わる"""
    beat = 60 / 120; T = lambda b: b * beat
    mel = [(0, 0.75, 65), (0.75, 0.25, 70), (1, 0.75, 74), (1.75, 0.25, 77), (2, 1, 82), (3, 0.5, 81), (3.5, 0.5, 79), (4, 2, 82)]
    def build(add):
        for b0, d, mm in mel:
            v = 0.88 if d >= 1 else 0.75
            add(brass2(midi_hz(mm - 12), T(d) * 0.88, v, bright=0.65, voices=3, att=0.02), T(b0), pan=0.1, g=bgain)
            add(glock(midi_hz(mm + 12), T(d), 0.3, hard=0.5, ring=0.7), T(b0), pan=0.35, rev=0.3)
        for b0, d, ch, bs in [(0, 2, [58, 62, 65], 34), (2, 1, [63, 67, 70], 39), (3, 1, [60, 65, 69], 41), (4, 2, [58, 62, 65, 70], 34)]:
            for x in ch: add(brass2(midi_hz(x - 12), T(d) * 0.9, 0.45, bright=0.4, voices=2, att=0.02), T(b0), pan=-0.3, g=bgain)
            for k in range(int(d)): add(reed(midi_hz(bs + (7 if k % 2 else 0)), T(0.4), 0.8, kind='bassoon', breath=0.3), T(b0 + k), pan=-0.1, rev=0.15, g=1.8)
        for bb in range(4):
            for k in range(3): add(snare2(0.45 + 0.12 * k, tone=0.6, snap=0.7), T(bb + k / 6), pan=0.1, rev=0.2)
            add(kick2(0.75, punch=0.5, low=0.5), T(bb), g=1.4)
        add(kick2(0.95, punch=0.6, low=0.6), T(4), g=1.6); add(crash2(1.0, dur=2.5), T(4), pan=0.2, rev=0.3, g=1.8)
        add(reed(midi_hz(41), T(0.3), 0.9, kind='bassoon'), T(5.5), rev=0.15, g=2.2); add(reed(midi_hz(34), T(0.5), 1.0, kind='bassoon'), T(6), rev=0.15, g=2.2)
        add(kick2(0.6, punch=0.4, low=0.6), T(6), g=1.2)
    return _jingle_render(build, T(6) + 1.6, path or os.path.join(OUT, "jingle_goal_f.wav"))

def fanfare_g(path=None, ggain=1.1):
    """G「ギャーン！」：ロック。E長調 150BPM。ドラムのフィル→歪んだギターのパワーコード「E・G・A・E〜」、
    リードギターがチョーキングで「キュイーン」、最後はシンバルとギターをかき鳴らして伸ばす"""
    beat = 60 / 150; T = lambda b: b * beat
    def build(add):
        for k in range(8): add(snare2(0.45 + 0.07 * k, tone=0.6, snap=0.7), T(k * 0.25), pan=0.1, rev=0.2)
        for k, f in enumerate((170, 140, 115, 95)): add(tom2(f, 0.8, skin=0.7), T(1 + k * 0.25), pan=-0.3 + 0.2 * k, rev=0.25)
        hits = [(2, 0.75, 40), (2.75, 0.75, 43), (3.5, 0.5, 45), (4, 3, 40)]
        for b0, d, r in hits:
            for side in (-0.6, 0.6):
                add(power2(midi_hz(r), T(d), 0.9, drive=5.0, mute=(0.12 if d < 1 else 0.8)), T(b0) + (0.012 if side > 0 else 0), pan=side, rev=0.2, g=ggain)
            add(finger_bass(midi_hz(r - 12), T(d), 0.85, tone=0.6), T(b0), rev=0.05, g=1.2)
            add(kick2(0.95, punch=0.8, low=0.6), T(b0), g=1.6)
            add(crash2(0.8 if d < 1 else 1.0, dur=1.0 if d < 1 else 3.0), T(b0), pan=0.2 if b0 % 1 else -0.2, rev=0.3, g=1.5)
        add(lead_guitar(midi_hz(76), T(0.5), 0.8, drive=6.0, bend=0.2), T(2), pan=0.15, rev=0.3, g=0.9)
        add(lead_guitar(midi_hz(79), T(0.5), 0.8, drive=6.0, bend=0.2), T(2.75), pan=0.15, rev=0.3, g=0.9)
        add(lead_guitar(midi_hz(81), T(0.4), 0.8, drive=6.0, bend=0.2), T(3.5), pan=0.15, rev=0.3, g=0.9)
        add(lead_guitar(midi_hz(83), T(3), 0.9, drive=6.0, bend=2.0), T(4), pan=0.15, rev=0.35, g=0.9)
    return _jingle_render(build, T(4) + 2.4, path or os.path.join(OUT, "jingle_goal_g.wav"))

def fanfare_h(path=None):
    """H「ハレルヤ」：合唱の「あー」が下から積み上がって光が差す感じ。C長調 80BPM。
    ド→ミ→ソ→ドと声が重なっていき、上でグロッケンとビブラフォンがキラキラ、低い鐘「ごーん」。おごそか＋ちょっとユーモラス"""
    beat = 60 / 80; T = lambda b: b * beat
    def build(add):
        stack = [(0, 48, 'o'), (0.5, 55, 'o'), (1, 60, 'a'), (1.5, 64, 'a'), (2, 67, 'a'), (2.5, 72, 'a')]
        for b0, x, vw in stack:
            add(choir(midi_hz(x), T(5.5 - b0), 0.8, vowel=vw), T(b0), pan=-0.4 + 0.15 * (x % 7), rev=0.6, g=1.6)
        add(choir(midi_hz(76), T(3), 0.7, vowel='a'), T(3), pan=0.2, rev=0.6, g=1.4)
        add(choir(midi_hz(79), T(2.5), 0.7, vowel='a'), T(3.5), pan=-0.2, rev=0.6, g=1.3)
        for k, x in enumerate([84, 88, 91, 96, 100]): add(glock(midi_hz(x), 0.4, 0.25, hard=0.4, ring=1.2), T(3 + k * 0.15), pan=0.5 - 0.25 * k, rev=0.55)
        for k, x in enumerate([72, 76, 79, 84]): add(vibes(midi_hz(x), T(2), 0.5, hard=0.4, rel=1.5), T(3 + k * 0.1), pan=-0.3 + 0.2 * k, rev=0.55, g=0.8)
        add(celesta(midi_hz(36), T(3), 0.6), T(3), rev=0.6, g=1.5)
        add(tom2(65, 0.7, skin=0.2), T(3), rev=0.5, g=1.2)
    return _jingle_render(build, T(3) + 3.2, path or os.path.join(OUT, "jingle_goal_h.wav"), rt=2.8)

def fanfare_i(path=None, bgain=3.2):
    """I「タ・ダー！」：ビッグバンドのブラスで短く一発。F長調 170BPM、はねる。
    「ダッ・ダ・ダッ・ダー・ダ・ダー！」最後はおしゃれな6/9の和音を伸ばして、トランペットがふるえる＋ドラムのフィル"""
    beat = 60 / 170; T = lambda b: b * beat
    mel = [(0, 0.5, 72), (0.67, 0.33, 72), (1, 0.5, 74), (1.67, 0.33, 77), (2, 1, 79), (3, 0.33, 77), (3.33, 2.7, 81)]
    def build(add):
        for b0, d, mm in mel:
            v = 0.9 if d >= 1 else 0.78
            add(brass2(midi_hz(mm), T(d) * 0.85, v, bright=0.8, voices=3, att=0.015), T(b0), pan=0.1, g=bgain)
            add(brass2(midi_hz(mm - 5), T(d) * 0.85, v * 0.6, bright=0.6, voices=2, att=0.015), T(b0), pan=-0.15, g=bgain)
            add(brass2(midi_hz(mm - 12), T(d) * 0.85, v * 0.55, bright=0.45, voices=2, att=0.015), T(b0), pan=-0.35, g=bgain)
        for x in [53, 57, 60, 62, 67]: add(brass2(midi_hz(x), T(2.7), 0.45, bright=0.45, voices=2, att=0.02), T(3.33), pan=-0.3 + 0.15 * (x % 5), g=bgain)
        add(finger_bass(midi_hz(29), T(2.7), 0.9, tone=0.5), T(3.33), rev=0.05, g=2.0)
        for b0 in (0, 1, 2): add(finger_bass(midi_hz(29 + [0, 2, 5][int(b0)]), T(0.5), 0.8, tone=0.5), T(b0), rev=0.05, g=2.0)
        for b0 in (0, 1, 2, 3.33): add(kick2(0.8, punch=0.6, low=0.5), T(b0), g=1.4)
        for b0 in (0.67, 1.67, 3): add(snare2(0.75, tone=0.6, snap=0.6), T(b0), pan=0.1, rev=0.2)
        add(crash2(1.0, dur=2.5), T(3.33), pan=0.2, rev=0.3, g=1.8)
        for k in range(6): add(hat2(0.5, open_=(k == 5)), T(k * 0.67), pan=0.35, rev=0.05)
    return _jingle_render(build, T(3.33) + 2.2, path or os.path.join(OUT, "jingle_goal_i.wav"))

def lobby_c(path=None):
    """lobby_b と同じメロディ・コード・ゆりかごの分散和音。音源と残響・仕上げを新しくした版"""
    m = Mix(60, 16, tail=5.0, seed=11)
    prog = ['Gmaj7', 'Em7', 'Cmaj7', 'D', 'Gmaj7', 'Em7', 'Cmaj7', 'Dsus4',
            'Cmaj7', 'Gmaj7', 'Am7', 'D', 'Cmaj7', 'Gmaj7', 'Am7', 'Gmaj7']
    kp = old.NOTE['G']; MAJOR = old.MAJOR
    rA = [[(0, 3), (3, 1)], [(0, 4)], [(0, 2), (2, 2)], [(0, 4)]]
    rB = [[(1, 3)], [(0, 3), (3, 1)], [(0, 4)], [(0, 4)]]
    a = old.make_melody(np.random.default_rng(71), prog[:4], kp, MAJOR, rA, 67, 79, 74)
    a2 = old.make_melody(np.random.default_rng(72), prog[4:8], kp, MAJOR, rA, 67, 79, a[-1][2])
    b = old.make_melody(np.random.default_rng(73), prog[8:12], kp, MAJOR, rB, 67, 79, 76)
    c = old.make_melody(np.random.default_rng(74), prog[12:16], kp, MAJOR, rA, 67, 79, 74)
    mel = a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12)
    mel = old.end_on_root(mel, kp, 67, 79)
    # メロディ：フェルトピアノ。フレーズの頭は少し強く、伸ばす音は少し弱く
    for pos, d, mm in mel:
        v = m.vel(0.62 if (pos % 4) == 0 else 0.52)
        m.add(felt_piano(midi_hz(mm), d * m.beat, v, soft=1.0), pos, pan=0.08, rev=0.32, humanize=0.012)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        voic = sorted(old.nearest(55 + (p - 55) % 12, [p], 52, 64) for p in cps)
        for mm in voic:
            m.add(warm_pad(midi_hz(mm), 4 * m.beat + 0.3, 0.5, bright=0.3), bar * 4, rev=0.35, humanize=0)
        root = old.nearest(43, [r], 36, 47)
        m.add(sub_bass(midi_hz(root), 4 * m.beat, m.vel(0.17, 0.04)), bar * 4, pan=0, rev=0.08, humanize=0.004)
        # ゆりかご：1拍ずつ 根音→上→真ん中→上（ピアノの低め、やさしく）
        arp = sorted(old.nearest(60 + (p - 60) % 12, [p], 55, 67) for p in cps)
        seq = [arp[0], arp[-1], arp[1], arp[-1]]
        for k in range(4):
            v = m.vel(0.36 if k == 0 else 0.27)
            m.add(felt_piano(midi_hz(seq[k]), 1.0 * m.beat, v, soft=1.3), bar * 4 + k,
                  pan=(-0.32 if k % 2 else 0.22), rev=0.34, humanize=0.014)
    st = master(m, rt60=2.6, rev_level=0.5, damp=0.7, lowcut=32, warmth_db=1.5, final_lp=5200, target_rms_db=-12.5)
    save(st, path or os.path.join(OUT, "bgm_lobby_c.wav"))
    return st

if __name__ == "__main__":
    which = sys.argv[1] if len(sys.argv) > 1 else "lobby_c"
    out = sys.argv[2] if len(sys.argv) > 2 else None
    kw = dict(kv.split('=') for kv in sys.argv[3:])
    def _num(v):
        try: return float(v)
        except ValueError: return v
    globals()[which](out, **{k: _num(v) for k, v in kw.items()})
