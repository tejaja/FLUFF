# ステージ2 コース2–2「ほのおのやま」のBGM候補（2026-11-01、てじゃ「2-2も音楽つくろか」）。
# 方向性：火山の重さと緊張感。金属系（ビブラフォン）が好み、重さが大事（いわのみち）。炎や風のノイズは入れない（ギミックの音とまぎれる）
#   python make_bgm_hono.py [出力フォルダ]            → 3つとも
#   python make_bgm_hono.py [出力フォルダ] hono_a     → 1つだけ
import sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
import make_bgm as old
from fluffsynth import *

OUT = old.OUT
MINOR = old.MINOR

def melody(prog, kp, rhythms, lo, hi, start, seed):
    parts = []; pitch = start
    for i in range(0, len(prog), 4):
        p = old.make_melody(np.random.default_rng(seed + i), prog[i:i + 4], kp, MINOR, rhythms, lo, hi, pitch)
        pitch = p[-1][2]
        parts += old.transpose_rest(p, i)
    return old.end_on_root(parts, kp, lo, hi)

def hono_a(path=None):
    """A「ほのおのやま」：C短調 126BPM、走る感じ。低いブラスのメロディ、8分で刻むベース、タムの連打、裏で鳴るビブラフォン"""
    m = Mix(126, 16, tail=3.0, seed=61)
    prog = ['Cm', 'Ab', 'Bb', 'Cm', 'Cm', 'Ab', 'Fm', 'G', 'Ab', 'Bb', 'Cm', 'Ab', 'Fm', 'G', 'Cm', 'Cm']
    kp = old.NOTE['C']
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 2)], [(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 2)], [(0, 3), (3, 1)]]
    for pos, d, mm in melody(prog, kp, rA, 60, 75, 67, 601):
        m.add(brass2(midi_hz(mm), d * m.beat * 0.92, m.vel(0.72), bright=0.55), pos, pan=0.08, rev=0.3, gain=1.0, humanize=0.005)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(36, [r], 31, 42)
        for k in range(8):
            m.add(finger_bass(midi_hz(root + (12 if k == 7 else 0)), 0.42 * m.beat, m.vel(0.66 if k % 4 == 0 else 0.5, 0.05), tone=0.45), bar * 4 + k * 0.5, rev=0.03, gain=0.62, humanize=0.002)
        vo = sorted(old.nearest(67 + (p - 67) % 12, [p], 62, 74) for p in cps)
        for off in (0.5, 1.5, 2.5, 3.5):
            for mm in vo: m.add(vibes(midi_hz(mm), 0.3 * m.beat, m.vel(0.2), rel=0.4), bar * 4 + off, pan=-0.28, rev=0.3, humanize=0.003)
        m.add(kick2(m.vel(0.72, 0.04), punch=0.5, low=0.45), bar * 4, rev=0.02, gain=0.75, humanize=0.002)
        m.add(kick2(m.vel(0.6, 0.04), punch=0.5, low=0.45), bar * 4 + 2.5, rev=0.02, gain=0.75, humanize=0.002)
        m.add(snare2(m.vel(0.42), tone=0.4, snap=0.4), bar * 4 + 1, pan=0.05, rev=0.14, humanize=0.003)
        m.add(snare2(m.vel(0.46), tone=0.4, snap=0.4), bar * 4 + 3, pan=0.05, rev=0.14, humanize=0.003)
        for k in range(8): m.add(hat2(m.vel(0.3 if k % 2 else 0.2)), bar * 4 + k * 0.5, pan=0.3, rev=0.05, humanize=0.002)
        if bar % 4 == 3:
            for j, f in enumerate((130, 110, 90, 74)):
                m.add(tom2(f, m.vel(0.62), skin=0.5), bar * 4 + 2 + j * 0.5, pan=-0.3 + j * 0.2, rev=0.16, humanize=0.003)
    st = master(m, rt60=1.5, rev_level=0.32, damp=0.55, lowcut=30, warmth_db=1.0, air_db=0.6, comp_thresh=-17, comp_ratio=2.0, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_hono_a.wav"))

def hono_b(path=None):
    """B「マグマのたいこ」：D短調 92BPM、のしのし重い。太鼓（低いタム）とブラスの和音の「どーん」、低い声のパッド、上でビブラフォンのメロディ"""
    m = Mix(92, 16, tail=4.0, seed=62)
    prog = ['Dm', 'Dm', 'Bb', 'A', 'Dm', 'Dm', 'Gm', 'A', 'Bb', 'C', 'Dm', 'Bb', 'Gm', 'A', 'Dm', 'Dm']
    kp = old.NOTE['D']
    rA = [[(0, 2), (2, 1), (3, 1)], [(1, 1), (2, 2)], [(0, 1.5), (1.5, 0.5), (2, 2)], [(0, 4)]]
    for pos, d, mm in melody(prog, kp, rA, 69, 86, 74, 611):
        m.add(vibes(midi_hz(mm), d * m.beat, m.vel(0.72), rel=1.1), pos, pan=0.14, rev=0.42, gain=1.5, humanize=0.006)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(38, [r], 33, 44)
        m.add(sub_bass(midi_hz(root), 2.2 * m.beat, m.vel(0.2, 0.04)), bar * 4, rev=0.04, humanize=0.003)
        m.add(sub_bass(midi_hz(root), 1.0 * m.beat, m.vel(0.15, 0.04)), bar * 4 + 2.5, rev=0.04, humanize=0.003)
        for p in sorted(old.nearest(53 + (q - 53) % 12, [q], 50, 62) for q in cps):
            m.add(choir(midi_hz(p), 4 * m.beat + 0.3, 0.22, vowel='o'), bar * 4, pan=-0.1, rev=0.4, humanize=0)
            if bar % 2 == 0: m.add(brass2(midi_hz(p), 1.2 * m.beat, m.vel(0.5), bright=0.4), bar * 4, pan=-0.2, rev=0.3, humanize=0.004)
        for beat, f, v in ((0, 62, 0.85), (1.5, 74, 0.5), (2, 62, 0.72), (3, 90, 0.5), (3.5, 74, 0.56)):
            m.add(tom2(f, m.vel(v), skin=0.55), bar * 4 + beat, pan=(beat - 2) * 0.08, rev=0.22, gain=0.55, humanize=0.003)
        if bar % 2 == 1: m.add(woodblock2(0.36, hi=False), bar * 4 + 3.5, pan=0.35, rev=0.16)
        if bar % 8 == 7: m.add(crash2(m.vel(0.34), dur=2.4), bar * 4 + 4, pan=0.1, rev=0.3)
    st = master(m, rt60=2.2, rev_level=0.42, damp=0.6, lowcut=28, warmth_db=1.2, air_db=0.4, comp_thresh=-17, comp_ratio=2.0, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_hono_b.wav"))

def hono_c(path=None):
    """C「ひのこのおどり」：A短調 118BPM、ちょっとかわいくて、あせる感じ。マリンバの速い分散和音、ピッコロ風の笛とビブラフォンのメロディ、はずむ低ブラス、ボンゴ"""
    m = Mix(118, 16, tail=3.0, seed=63)
    prog = ['Am', 'F', 'G', 'Am', 'Am', 'F', 'Dm', 'E', 'F', 'G', 'Am', 'F', 'Dm', 'E', 'Am', 'Am']
    kp = old.NOTE['A']
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)], [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 2), (2, 1), (3, 1)]]
    for pos, d, mm in melody(prog, kp, rA, 72, 89, 76, 621):
        acc = 0.72 if pos % 1 == 0 else 0.6
        m.add(wind(midi_hz(mm), d * m.beat * 0.85, m.vel(acc), kind='flute', vib=0.5, breath=0.5, taper=0.2, soft=0.3), pos, pan=0.1, rev=0.3, gain=1.3, humanize=0.005)
        m.add(vibes(midi_hz(mm), d * m.beat, m.vel(acc * 0.42), rel=0.7), pos, pan=-0.1, rev=0.35, humanize=0.005)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        vo = sorted(old.nearest(60 + (p - 60) % 12, [p], 55, 67) for p in cps)
        seq = vo + [vo[1] + 12, vo[2] + 12, vo[1] + 12]
        for k in range(16):
            m.add(marimba3(midi_hz(seq[k % len(seq)]), 0.22 * m.beat, m.vel(0.42 if k % 4 == 0 else 0.3), hard=0.3), bar * 4 + k * 0.25, pan=-0.3, rev=0.14, gain=1.1, humanize=0.003)
        root = old.nearest(40, [r], 35, 46)
        for beat, iv, v in ((0, 0, 0.62), (1.5, 0, 0.46), (2, 7, 0.54), (3, 0, 0.46), (3.5, 12, 0.42)):
            m.add(brass2(midi_hz(root + iv), 0.38 * m.beat, m.vel(v), bright=0.3, voices=2), bar * 4 + beat, pan=0.05, rev=0.1, humanize=0.003)
        for beat, hi in ((0, False), (1, True), (1.5, True), (2.5, False), (3, True), (3.75, True)):
            m.add(bongo2(hi, m.vel(0.55)), bar * 4 + beat, pan=0.3, rev=0.1, humanize=0.004)
        m.add(kick2(m.vel(0.5, 0.04), punch=0.35, low=0.55), bar * 4, rev=0.02, humanize=0.002)
        if bar % 4 == 3:
            for j in range(4): m.add(glock(midi_hz(old.nearest(88 - j * 2, [(kp + s) % 12 for s in MINOR], 80, 92)), 0.4, m.vel(0.25), hard=0.4), bar * 4 + 2 + j * 0.5, pan=-0.5 + j * 0.3, rev=0.5, humanize=0.003)
    st = master(m, rt60=1.5, rev_level=0.34, damp=0.5, lowcut=32, warmth_db=0.6, air_db=0.9, comp_thresh=-16, comp_ratio=1.8, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_hono_c.wav"))

def anvil(f, vel=0.5):
    # 金属をたたく「かーん」：低めのグロッケンとビブラフォンを重ねる
    a = glock(f, 0.6, vel, hard=0.75); b = vibes(f * 0.5, 0.5, vel * 0.8, hard=0.7, rel=0.5)
    n = max(len(a), len(b)); y = np.zeros(n); y[:len(a)] += a; y[:len(b)] += b
    return y

# メロディなしの重い候補（てじゃ「メロディと言えるのがないような、重い感じ」）
def hono_d(path=None):
    """D「ちのそこ」：D短調 76BPM。太鼓、低いブラスのふくらみ、低い声のドローンだけ。ときどき金属の音"""
    m = Mix(76, 16, tail=4.0, seed=64)
    prog = ['Dm', 'Dm', 'Bb', 'Dm', 'Dm', 'Dm', 'Gm', 'A', 'Dm', 'Dm', 'Bb', 'Dm', 'Gm', 'Bb', 'A', 'A']
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(38, [r], 33, 44)
        m.add(sub_bass(midi_hz(root), 4 * m.beat, m.vel(0.1, 0.03)), bar * 4, rev=0.05, humanize=0)
        for q in sorted(old.nearest(50 + (x - 50) % 12, [x], 47, 59) for x in cps):
            m.add(choir(midi_hz(q), 4 * m.beat + 0.4, 0.26, vowel='o'), bar * 4, pan=-0.08, rev=0.45, humanize=0)
        if bar % 2 == 0:
            m.add(brass2(midi_hz(root + 12), 3.2 * m.beat, m.vel(0.5), bright=0.3, att=0.5), bar * 4, pan=0.12, rev=0.35, gain=0.9, humanize=0.004)
            m.add(brass2(midi_hz(root + 19), 3.2 * m.beat, m.vel(0.42), bright=0.3, att=0.5), bar * 4, pan=-0.12, rev=0.35, gain=0.9, humanize=0.004)
        for beat, f, v in ((0, 58, 0.9), (2, 58, 0.62), (2.5, 70, 0.5), (3.5, 70, 0.46)):
            m.add(tom2(f, m.vel(v), skin=0.6), bar * 4 + beat, pan=(beat - 1.5) * 0.08, rev=0.3, gain=0.48, humanize=0.003)
        if bar % 4 == 3: m.add(anvil(midi_hz(old.nearest(74, [r], 69, 81)), m.vel(0.34)), bar * 4 + 3, pan=0.3, rev=0.55, humanize=0.002)
    st = master(m, rt60=2.8, rev_level=0.45, damp=0.62, lowcut=28, warmth_db=1.0, air_db=0.2, comp_thresh=-17, comp_ratio=2.0, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_hono_d.wav"))

def hono_e(path=None):
    """E「ようがんのこどう」：E短調 100BPM。同じ低音のリフをくり返す。ひずんだ和音、太鼓、裏で金属の刻み"""
    m = Mix(100, 16, tail=3.0, seed=65)
    prog = ['Em', 'Em', 'C', 'Em', 'Em', 'Em', 'Am', 'B', 'Em', 'Em', 'C', 'Em', 'Am', 'C', 'B', 'B']
    riff = ((0, 0, 0.75), (0.75, 0, 0.5), (1.5, 0, 0.6), (2, 3, 0.55), (2.5, 0, 0.5), (3, 7, 0.55), (3.5, 6, 0.5))
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(40, [r], 35, 46)
        for beat, iv, v in riff:
            d = 0.4 * m.beat
            m.add(power2(midi_hz(root + iv + 12), d, m.vel(v), drive=3.0), bar * 4 + beat, pan=-0.12, rev=0.08, gain=0.6, humanize=0.003)
            m.add(finger_bass(midi_hz(root + iv), d, m.vel(v, 0.04), tone=0.5), bar * 4 + beat, rev=0.03, gain=0.3, humanize=0.003)
        for beat, f, v in ((0, 66, 0.85), (1.5, 66, 0.6), (2, 88, 0.6), (3, 66, 0.7), (3.5, 88, 0.5)):
            m.add(tom2(f, m.vel(v), skin=0.6), bar * 4 + beat, pan=(beat - 2) * 0.08, rev=0.18, gain=0.42, humanize=0.003)
        for k in range(4): m.add(anvil(midi_hz(old.nearest(76, [r], 71, 83)), m.vel(0.2 if k % 2 == 0 else 0.13)), bar * 4 + k + 0.5, pan=0.32, rev=0.25, gain=0.75, humanize=0.003)
        if bar % 4 == 0:
            m.add(brass2(midi_hz(root + 12), 3.6 * m.beat, m.vel(0.42), bright=0.35, att=0.6), bar * 4, pan=0.2, rev=0.35, humanize=0.004)
    st = master(m, rt60=1.5, rev_level=0.3, damp=0.6, lowcut=30, warmth_db=0.8, air_db=0.3, comp_thresh=-17, comp_ratio=2.2, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_hono_e.wav"))

def hono_e2(path=None):
    """E2：E と同じリフのまま、小さいスピーカーでも重く聞こえるように。ひずみとブラスの中低音を太く、一番低い所を減らす"""
    m = Mix(100, 16, tail=3.0, seed=65)
    prog = ['Em', 'Em', 'C', 'Em', 'Em', 'Em', 'Am', 'B', 'Em', 'Em', 'C', 'Em', 'Am', 'C', 'B', 'B']
    riff = ((0, 0, 0.75), (0.75, 0, 0.5), (1.5, 0, 0.6), (2, 3, 0.55), (2.5, 0, 0.5), (3, 7, 0.55), (3.5, 6, 0.5))
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(40, [r], 35, 46)
        for beat, iv, v in riff:
            d = 0.4 * m.beat
            m.add(power2(midi_hz(root + iv + 12), d, m.vel(v), drive=4.0), bar * 4 + beat, pan=-0.14, rev=0.08, gain=0.95, humanize=0.003)
            m.add(power2(midi_hz(root + iv + 12), d, m.vel(v), drive=4.0), bar * 4 + beat, pan=0.14, rev=0.08, gain=0.7, humanize=0.006)
            m.add(brass2(midi_hz(root + iv + 12), d, m.vel(v * 0.8), bright=0.55, voices=2, att=0.012), bar * 4 + beat, rev=0.08, gain=0.75, humanize=0.003)
            m.add(finger_bass(midi_hz(root + iv), d, m.vel(v, 0.04), tone=0.8), bar * 4 + beat, rev=0.03, gain=0.2, humanize=0.003)
        for beat, f, v in ((0, 82, 0.85), (1.5, 82, 0.6), (2, 110, 0.6), (3, 82, 0.7), (3.5, 110, 0.5)):
            m.add(tom2(f, m.vel(v), skin=1.0), bar * 4 + beat, pan=(beat - 2) * 0.08, rev=0.18, gain=0.32, humanize=0.003)
        for k in range(4): m.add(anvil(midi_hz(old.nearest(76, [r], 71, 83)), m.vel(0.2 if k % 2 == 0 else 0.13)), bar * 4 + k + 0.5, pan=0.32, rev=0.25, gain=0.75, humanize=0.003)
        if bar % 4 == 0:
            m.add(brass2(midi_hz(root + 12), 3.6 * m.beat, m.vel(0.42), bright=0.35, att=0.6), bar * 4, pan=0.2, rev=0.35, humanize=0.004)
    st = master(m, rt60=1.5, rev_level=0.3, damp=0.6, lowcut=55, warmth_db=0.3, air_db=0.6, comp_thresh=-17, comp_ratio=2.2, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_hono_e2.wav"))

def hono_f(path=None):
    """F「かざんのねむり」：C短調 64BPM、一番静か。低い声と長いブラス、心臓みたいな太鼓の「どっくん」、遠くの金属音。打楽器は少なめ"""
    m = Mix(64, 12, tail=5.0, seed=66)
    prog = ['Cm', 'Cm', 'Ab', 'Cm', 'Fm', 'Cm', 'Ab', 'G', 'Cm', 'Cm', 'Fm', 'G']
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(36, [r], 31, 42)
        m.add(sub_bass(midi_hz(root), 4 * m.beat, m.vel(0.14, 0.03)), bar * 4, rev=0.05, humanize=0)
        for q in sorted(old.nearest(55 + (x - 55) % 12, [x], 50, 62) for x in cps):
            m.add(choir(midi_hz(q), 4 * m.beat + 0.6, 0.3, vowel='o'), bar * 4, rev=0.55, humanize=0)
            m.add(warm_pad(midi_hz(q), 4 * m.beat + 0.4, 0.2, bright=0.25), bar * 4, pan=0.15, rev=0.45, humanize=0)
        m.add(brass2(midi_hz(root + 12), 3.6 * m.beat, m.vel(0.42), bright=0.25, att=0.9), bar * 4 + 0.25, pan=-0.15, rev=0.5, humanize=0.004)
        for beat, v in ((0, 0.85), (0.5, 0.55), (2, 0.72), (2.5, 0.46)):
            m.add(tom2(55, m.vel(v), skin=0.5), bar * 4 + beat, rev=0.25, gain=0.55, humanize=0.002)
        if bar % 2 == 1: m.add(anvil(midi_hz(old.nearest(72, [r], 67, 79)), m.vel(0.24)), bar * 4 + 3, pan=-0.4 if bar % 4 == 1 else 0.4, rev=0.7, humanize=0.002)
    st = master(m, rt60=3.2, rev_level=0.5, damp=0.65, lowcut=26, warmth_db=1.0, air_db=0.1, comp_thresh=-18, comp_ratio=1.8, target_rms_db=-15.0)
    save(st, path or os.path.join(OUT, "bgm_hono_f.wav"))

def hono_e_long(path=None):
    """2–2 の採用版（2026-11-01 てじゃ E が好き。E2 の中音足しより「前のほうがいい」）。A（E そのまま16小節）→ B（リフを間引いて声とブラスが広がる8小節＋戻っていく8小節）
    → A'（E＋長いブラスの重ね、最後にタムの連打）→ 頭に戻る。48小節＝約1分55秒"""
    m = Mix(100, 48, tail=3.0, seed=65)
    progA = ['Em', 'Em', 'C', 'Em', 'Em', 'Em', 'Am', 'B', 'Em', 'Em', 'C', 'Em', 'Am', 'C', 'B', 'B']
    progB = ['C', 'C', 'Am', 'Am', 'C', 'C', 'B', 'B', 'Em', 'Em', 'C', 'C', 'Am', 'Am', 'B', 'B']
    prog = progA + progB + progA
    riff = ((0, 0, 0.75), (0.75, 0, 0.5), (1.5, 0, 0.6), (2, 3, 0.55), (2.5, 0, 0.5), (3, 7, 0.55), (3.5, 6, 0.5))
    thin = ((0, 0, 0.75), (2, 0, 0.55), (3.5, 6, 0.42))
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(40, [r], 35, 46)
        quiet = 16 <= bar < 24; back = 24 <= bar < 32; last = bar >= 32
        for beat, iv, v in (thin if quiet else riff):
            d = (1.2 if quiet else 0.4) * m.beat; k = 0.72 if back else 1.0
            m.add(power2(midi_hz(root + iv + 12), d, m.vel(v * k), drive=3.0), bar * 4 + beat, pan=-0.12, rev=0.08, gain=0.6, humanize=0.003)
            m.add(finger_bass(midi_hz(root + iv), d, m.vel(v * k, 0.04), tone=0.5), bar * 4 + beat, rev=0.03, gain=0.3, humanize=0.003)
        drums = ((0, 66, 0.85), (2, 66, 0.6)) if quiet else ((0, 66, 0.85), (1.5, 66, 0.6), (2, 88, 0.6), (3, 66, 0.7), (3.5, 88, 0.5))
        for beat, f, v in drums:
            m.add(tom2(f, m.vel(v), skin=0.6), bar * 4 + beat, pan=(beat - 2) * 0.08, rev=0.18, gain=0.42, humanize=0.003)
        if not quiet:
            for k in range(4): m.add(anvil(midi_hz(old.nearest(76, [r], 71, 83)), m.vel(0.2 if k % 2 == 0 else 0.13)), bar * 4 + k + 0.5, pan=0.32, rev=0.25, gain=0.75, humanize=0.003)
        elif bar % 2 == 1:
            m.add(anvil(midi_hz(old.nearest(76, [r], 71, 83)), m.vel(0.24)), bar * 4 + 3, pan=0.32, rev=0.5, humanize=0.002)
        if quiet or back:   # B：低い声が広がる
            for q in sorted(old.nearest(52 + (x - 52) % 12, [x], 47, 59) for x in cps):
                m.add(choir(midi_hz(q), 4 * m.beat + 0.3, 0.24 if quiet else 0.16, vowel='o'), bar * 4, pan=-0.1, rev=0.42, humanize=0)
        if bar % 4 == 0 or (last and bar % 2 == 0):
            m.add(brass2(midi_hz(root + 12), 3.6 * m.beat, m.vel(0.42), bright=0.35, att=0.6), bar * 4, pan=0.2, rev=0.35, humanize=0.004)
            if last: m.add(brass2(midi_hz(root + 19), 3.6 * m.beat, m.vel(0.34), bright=0.35, att=0.6), bar * 4, pan=-0.2, rev=0.35, humanize=0.004)
        if bar in (31, 47):   # 次へ戻るタムの連打
            for j, f in enumerate((130, 130, 110, 110, 90, 90, 74, 74)):
                m.add(tom2(f, m.vel(0.42 + j * 0.03), skin=0.6), bar * 4 + 2 + j * 0.25, pan=-0.3 + j * 0.08, rev=0.16, gain=0.42, humanize=0.002)
    st = master(m, rt60=1.5, rev_level=0.3, damp=0.6, lowcut=30, warmth_db=0.8, air_db=0.3, comp_thresh=-17, comp_ratio=2.2, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_hono_long.wav"))

def hono_e3_long(path=None):
    """E の長い版に、てじゃの参考曲の帯域と強弱を寄せた版（2026-11-01）。リフは同じ。地鳴りの低音（40Hz あたり）と、声とやわらかいパッドの中音の芯（500〜1000Hz）を足す。
    高音は足さない（E2 は高音まで増えて不採用）。静かな所と大きい所の差をつける：A 少しおさえる → B 前半ぐっと静か → B 後半だんだん大きく → A' 一番大きい"""
    m = Mix(100, 48, tail=3.0, seed=65)
    progA = ['Em', 'Em', 'C', 'Em', 'Em', 'Em', 'Am', 'B', 'Em', 'Em', 'C', 'Em', 'Am', 'C', 'B', 'B']
    progB = ['C', 'C', 'Am', 'Am', 'C', 'C', 'B', 'B', 'Em', 'Em', 'C', 'C', 'Am', 'Am', 'B', 'B']
    prog = progA + progB + progA
    riff = ((0, 0, 0.75), (0.75, 0, 0.5), (1.5, 0, 0.6), (2, 3, 0.55), (2.5, 0, 0.5), (3, 7, 0.55), (3.5, 6, 0.5))
    thin = ((0, 0, 0.75), (0.75, 0, 0.45), (1.5, 0, 0.5), (2, 3, 0.5), (3, 7, 0.5), (3.5, 6, 0.42))
    def level(bar):
        if bar < 16: return 0.72
        if bar < 20: return 0.45
        if bar < 32: return 0.5 + 0.48 * (bar - 20) / 11.0
        return 1.0
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(40, [r], 35, 46)
        quiet = 16 <= bar < 20; last = bar >= 32;   # 静かな所は短く、リフも減らしすぎない（2026-11-01 てじゃ「静かなパートが淋しい」）
        g = level(bar)
        # 地鳴り：リフの 1 オクターブ下を長く
        rumble = root - 12 if root - 12 >= 26 else root
        m.add(sub_bass(midi_hz(rumble), 4 * m.beat + 0.05, 0.3), bar * 4, rev=0.02, gain=1.2 + 0.5 * g, humanize=0)
        # 中音の芯：声とやわらかいパッド（速い動きはしない）
        for q in sorted(old.nearest(62 + (x - 62) % 12, [x], 57, 69) for x in cps):   # 声はやめて、低めのパッドだけ（てじゃ「やっぱ声つよい」。高い所のパッドも声っぽく聞こえるかもしれないので下げた）
            m.add(warm_pad(midi_hz(q), 4 * m.beat + 0.3, 0.3, bright=0.2), bar * 4, pan=0.12, rev=0.35, gain=0.35 + 0.45 * g, humanize=0)
        for beat, iv, v in (thin if quiet else riff):
            d = (0.6 if quiet else 0.4) * m.beat
            m.add(power2(midi_hz(root + iv + 12), d, m.vel(v), drive=3.0), bar * 4 + beat, pan=-0.12, rev=0.08, gain=0.6 * g, humanize=0.003)
            m.add(finger_bass(midi_hz(root + iv), d, m.vel(v, 0.04), tone=0.5), bar * 4 + beat, rev=0.03, gain=0.5 * g, humanize=0.003)
        drums = ((0, 66, 0.85), (2, 66, 0.6)) if quiet else ((0, 66, 0.85), (1.5, 66, 0.6), (2, 88, 0.6), (3, 66, 0.7), (3.5, 88, 0.5))
        for beat, f, v in drums:
            m.add(tom2(f, m.vel(v), skin=0.6), bar * 4 + beat, pan=(beat - 2) * 0.08, rev=0.18, gain=0.5 * g + 0.05, humanize=0.003)
        # 打楽器を足す（2026-11-01 てじゃ「打楽器をもっとふやそ」）：キック、低いスネア、タムの細かい刻み、2小節ごとのフィル。静かな所は小さいタムだけ
        if quiet:
            for beat in (1, 3):
                m.add(tom2(100, m.vel(0.4), skin=0.7), bar * 4 + beat, pan=0.15, rev=0.2, gain=0.42, humanize=0.003)
            for k, (beat, f0) in enumerate(((0.5, 74), (1.5, 88), (2.5, 74), (3.25, 88), (3.5, 74), (3.75, 100))):   # 遠くで鳴る太鼓：響きを多めに
                m.add(tom2(f0, m.vel(0.42), skin=0.6), bar * 4 + beat, pan=-0.4 + k * 0.16, rev=0.55, gain=0.4, humanize=0.004)
        else:
            for beat, v in ((0, 0.85), (1.5, 0.6), (2, 0.72), (3.5, 0.6)):
                m.add(kick2(m.vel(v, 0.04), punch=0.6, low=0.6), bar * 4 + beat, rev=0.02, gain=0.72 * g, humanize=0.002)
            for beat in (1, 3):
                m.add(snare2(m.vel(0.55), tone=0.25, snap=0.35), bar * 4 + beat, pan=0.05, rev=0.16, gain=0.72 * g, humanize=0.003)
            for k in range(16):
                if k % 4 == 0: continue
                m.add(tom2(128 if k % 2 else 110, m.vel(0.26 if k % 2 else 0.34), skin=0.85), bar * 4 + k * 0.25, pan=-0.25 if k % 2 else 0.25, rev=0.1, gain=0.42 * g, humanize=0.003)
            if bar % 2 == 1 and bar not in (31, 47):
                for j, f in enumerate((150, 130, 110, 90)):
                    m.add(tom2(f, m.vel(0.5 + j * 0.05), skin=0.75), bar * 4 + 3 + j * 0.25, pan=-0.2 + j * 0.14, rev=0.14, gain=0.5 * g, humanize=0.002)
        if not quiet:
            for k in range(4): m.add(anvil(midi_hz(old.nearest(76, [r], 71, 83)), m.vel(0.2 if k % 2 == 0 else 0.13)), bar * 4 + k + 0.5, pan=0.32, rev=0.25, gain=0.75 * g, humanize=0.003)
        elif bar % 2 == 1:
            m.add(anvil(midi_hz(old.nearest(76, [r], 71, 83)), m.vel(0.24)), bar * 4 + 3, pan=0.32, rev=0.5, gain=0.7, humanize=0.002)
        if bar % 4 == 0 or (last and bar % 2 == 0):
            m.add(brass2(midi_hz(root + 12), 3.6 * m.beat, m.vel(0.42), bright=0.3, att=0.6), bar * 4, pan=0.2, rev=0.35, gain=g, humanize=0.004)
            if last: m.add(brass2(midi_hz(root + 19), 3.6 * m.beat, m.vel(0.34), bright=0.3, att=0.6), bar * 4, pan=-0.2, rev=0.35, humanize=0.004)
        if bar in (31, 47):
            for j, f in enumerate((130, 130, 110, 110, 90, 90, 74, 74)):
                m.add(tom2(f, m.vel(0.42 + j * 0.03), skin=0.6), bar * 4 + 2 + j * 0.25, pan=-0.3 + j * 0.08, rev=0.16, gain=0.42 if bar == 47 else 0.34, humanize=0.002)
    st = master(m, rt60=1.8, rev_level=0.32, damp=0.65, lowcut=24, warmth_db=0.6, air_db=0.0, comp_thresh=-14, comp_ratio=1.4, target_rms_db=-12.0)
    save(st, path or os.path.join(OUT, "bgm_hono_long2.wav"))

if __name__ == '__main__':
    args = sys.argv[1:]
    out = None
    if args and (os.path.isdir(args[0]) or args[0].endswith(os.sep) or '\\' in args[0] or '/' in args[0]):
        out = args.pop(0)
    kw = {a.split('=')[0]: float(a.split('=')[1]) for a in args if '=' in a}
    names = [a for a in args if '=' not in a]
    for w in (names or ['hono_d', 'hono_e', 'hono_f']):
        globals()[w](os.path.join(out, "bgm_" + w + ".wav") if out else None, **kw)
