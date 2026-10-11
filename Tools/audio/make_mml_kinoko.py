# きのこのたにのBGM（make_bgm_tower.py の tower_i「きのこのたにのおさんぽ」）を、マビノギの楽譜用 MML に書き出す。
# 2026-10-11 てじゃ「きのこのたにを３和音までで描いてほしい、1トラック2400制限」
#   メロディ：口笛のメロディそのまま
#   和音1　：ベース（ブラス＋ベースの「ぼん・ぼん」、根音と5度を4分で交互、短く切る）
#   和音2　：裏拍の和音（元はピチカートの3音和音を裏拍で鳴らす → 1音ずつ 下・上・真ん中・上 と分けて鳴らす）
# マビノギの MML は1パート1音。o4c ＝ 真ん中のド（MIDI 60）として書く
#   python make_mml_kinoko.py  → 画面に MML と各パートの文字数
import numpy as np
from make_bgm import NOTE, MAJOR, chord, nearest, make_melody, transpose_rest, end_on_root

BPM = 116

def voicing(cps, center, lo, hi):
    return sorted(nearest(center + (p - center) % 12, [p], lo, hi) for p in cps)

def compose():
    prog = ['F', 'Dm', 'Bb', 'C', 'F', 'Dm', 'Gm', 'C', 'Bb', 'C', 'Am', 'Dm', 'Bb', 'C', 'F', 'F']
    kp = NOTE['F']
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 2)],
          [(0, 1), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)]]
    a = make_melody(np.random.default_rng(141), prog[:4], kp, MAJOR, rA, 65, 82, 69)
    a2 = make_melody(np.random.default_rng(142), prog[4:8], kp, MAJOR, rA, 65, 82, a[-1][2])
    b = make_melody(np.random.default_rng(143), prog[8:12], kp, MAJOR, rA, 65, 84, 72)
    c = make_melody(np.random.default_rng(144), prog[12:16], kp, MAJOR, rA[:3] + [[(0, 4)]], 65, 82, 74)
    mel = end_on_root(a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12), kp, 65, 82)
    bass = []; harm = []
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(41, [r], 36, 47)
        vo = voicing(cps, 62, 57, 69)
        for beat in range(4):
            bass.append((bar * 4 + beat, 0.5, root + (7 if beat % 2 else 0)))   # 4分の頭で短く（8分＋8分休み）
        for k, off in enumerate((0.5, 1.5, 2.5, 3.5)):
            harm.append((bar * 4 + off, 0.5, vo[(0, 2, 1, 2)[k]]))
    return mel, bass, harm, len(prog) * 4

# 拍の長さ → MML の音長（足りない分はタイでつなぐ）
LEN = [(4, '1'), (3, '2.'), (2, '2'), (1.5, '4.'), (1, '4'), (0.75, '8.'), (0.5, '8'), (0.25, '16')]
NAMES = ['c', 'c+', 'd', 'd+', 'e', 'f', 'f+', 'g', 'g+', 'a', 'a+', 'b']

def lengths(beats):
    out = []
    while beats > 1e-6:
        for b, s in LEN:
            if b <= beats + 1e-6:
                out.append(s); beats -= b; break
        else:
            raise ValueError(beats)
    return out

def to_mml(notes, total, vol, deflen='8'):
    s = 't%dv%dl%s' % (BPM, vol, deflen)
    octave = None
    t = 0.0
    def rest(beats):
        nonlocal s
        for k, L in enumerate(lengths(beats)): s += 'r' + ('' if L == deflen else L)
    for pos, d, m in sorted(notes):
        if pos > t + 1e-6: rest(pos - t)
        o = m // 12 - 1
        if octave is None: s += 'o%d' % o
        elif o > octave: s += '>' * (o - octave)
        elif o < octave: s += '<' * (octave - o)
        octave = o
        Ls = lengths(d)
        name = NAMES[m % 12]
        s += '&'.join(name + ('' if L == deflen else L) for L in Ls)
        t = pos + d
    if total > t + 1e-6: rest(total - t)
    return s

if __name__ == '__main__':
    mel, bass, harm, total = compose()
    # 16小節（約33秒）を2回くり返す（ゲームのBGMと同じくループする曲なので）
    rep = 2
    mel = [(p + total * k, d, m) for k in range(rep) for p, d, m in mel]
    bass = [(p + total * k, d, m) for k in range(rep) for p, d, m in bass]
    harm = [(p + total * k, d, m) for k in range(rep) for p, d, m in harm]
    total *= rep
    parts = [to_mml(mel, total, 13, '8'), to_mml(bass, total, 11, '8'), to_mml(harm, total, 9, '8')]
    for name, p in zip(['melody', 'chord1(bass)', 'chord2(harmony)'], parts): print(name, len(p))
    print('MML@' + ','.join(parts) + ';')
