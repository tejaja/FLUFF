# 新しい音源（fluffsynth）で、今の曲（make_bgm.py の作曲）を作り直す。まずはロビー（lobby_b）から。
import sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
import make_bgm as old
from fluffsynth import *

OUT = old.OUT

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
    globals()[which](out)
