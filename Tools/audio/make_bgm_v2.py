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
