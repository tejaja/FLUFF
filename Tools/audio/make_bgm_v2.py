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

def basics2(path=None, swing=0.0, woody=0, mbox=0.0, lead='vibes', goct=0, ggain=1.0, ghard=0.5, moct=12, trms=-13.0, cthr=-16.0, vrel=1.4, vrev=0.6):
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
