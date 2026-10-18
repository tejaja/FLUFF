# きのこのたにの新バージョン（make_bgm_long.py の tower：A→B→A' の48小節）をマビノギの楽譜用 MML に（2026-10-18 てじゃ「新バージョンもできないかな」）
# 旧版（make_mml_kinoko.py）と同じ3パート：メロディ＝口笛（B前半はビブラフォンのメロディ）／和音1＝ベース「ぼん・ぼん」／和音2＝裏拍の和音を1音ずつ
# 新しい曲のハモり（B後半・A'後半の2本目の口笛）は3パートに入りきらないので、harm=1 の時だけ和音2をその区間ハモりに切り替える
#   python make_mml_kinoko_long.py [harm=1]
import sys
import numpy as np
import make_bgm_tower as T
from make_bgm_long import third_below, chord_at
from make_mml_kinoko import to_mml, voicing

def compose(harm_mode=False):
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

    mel, bass, harm = [], [], []
    for off, m in ((0, melA), (16, melB), (32, melA)):
        mel += [(off * 4 + p, d, mm) for p, d, mm in m]
    prog = progA + progB + progA
    hsec = {}   # ハモりの区間（小節 → その曲のコード進行）
    for bar in list(range(24, 32)): hsec[bar] = progB
    for bar in list(range(40, 48)): hsec[bar] = progA
    for bar, cn in enumerate(prog):
        r, cps = T.chord(cn)
        root = T.nearest(41, [r], 36, 47)
        for beat in range(4): bass.append((bar * 4 + beat, 0.5, root + (7 if beat % 2 else 0)))
        if harm_mode and bar in hsec: continue
        vo = voicing(cps, 62, 57, 69)
        for k, o in enumerate((0.5, 1.5, 2.5, 3.5)): harm.append((bar * 4 + o, 0.5, vo[(0, 2, 1, 2)[k]]))
    if harm_mode:
        for pos, d, mm in mel:
            bar = int(pos // 4)
            if bar in hsec:
                local = pos - (bar - bar % 16) * 4
                harm.append((pos, d, third_below(mm, chord_at(hsec[bar], local), kp, MAJOR)))
    return mel, bass, harm, len(prog) * 4

if __name__ == '__main__':
    hm = any(x == 'harm=1' for x in sys.argv[1:])
    mel, bass, harm, total = compose(hm)
    parts = [to_mml(mel, total, 13, '8'), to_mml(bass, total, 11, '8'), to_mml(harm, total, 9, '8')]
    for name, p in zip(['melody', 'chord1(bass)', 'chord2(harmony)'], parts): print(name, len(p))
    print('MML@' + ','.join(parts) + ';')
