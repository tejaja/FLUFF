# ステージ2 コース2–1「みずのこみち」のBGM候補（2026-10-25、てじゃ「音楽先につくっちゃうか、モチベのために」）。
# 方向性：川・滝・蓮の池・貝の泡・三連滝。流れる水の感じ。金属系（ビブラフォン）が好み、水のノイズ（ざー）は入れない（ギミックの音とまぎれる）
#   python make_bgm_mizu.py [出力フォルダ]            → 3つとも
#   python make_bgm_mizu.py [出力フォルダ] mizu_a     → 1つだけ
import sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
import make_bgm as old
from fluffsynth import *

OUT = old.OUT
PENTA = [0, 2, 4, 7, 9]

def harp(f, dur, vel=0.6):
    # ハープ：明るい撥弦、余韻長め
    return ks_string(f, dur + 0.6, vel, bright=0.75, damp_time=0.9, body=False)

def drops(m, bar, key_pc, rng, base=84, n=(2, 4), vel=0.28, pan=None):
    # しずく：高いカリンバがぽつぽつ（ペンタトニック、16分でばらける）
    k = int(rng.integers(n[0], n[1] + 1)); start = rng.choice([1.5, 2.0, 2.5, 3.0])
    for j in range(k):
        pc = [(key_pc + s) % 12 for s in PENTA]
        mm = old.nearest(base + int(rng.integers(-5, 6)), pc, base - 7, base + 9)
        m.add(kalimba(midi_hz(mm), 0.6, m.vel(vel)), bar * 4 + start + j * 0.25, pan=float(rng.uniform(-0.7, 0.7)), rev=0.55, humanize=0.004)

def mizu_a(path=None):
    """A「みずのこみち」：F長調 104BPM。ビブラフォンのメロディ、ハープの3連の分散和音（流れる水）、指弾きベース、
    シェイカーとやわらかいキック。4小節ごとにしずく（高いカリンバ）"""
    m = Mix(104, 16, tail=3.0, seed=51)
    prog = ['F', 'C', 'Dm', 'Bb', 'F', 'C', 'Bb', 'C', 'Dm', 'Bb', 'F', 'C', 'Bb', 'C', 'F', 'F']
    kp = old.NOTE['F']
    rA = [[(0, 1.5), (1.5, 0.5), (2, 2)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 1.5), (3.5, 0.5)],
          [(0, 1), (1, 1), (2, 1), (3, 1)], [(0, 3), (3, 1)]]
    a = old.make_melody(np.random.default_rng(501), prog[:4], kp, old.MAJOR, rA, 69, 86, 77)
    a2 = old.make_melody(np.random.default_rng(502), prog[4:8], kp, old.MAJOR, rA, 69, 86, a[-1][2])
    b = old.make_melody(np.random.default_rng(503), prog[8:12], kp, old.MAJOR, rA, 71, 88, 81)
    c = old.make_melody(np.random.default_rng(504), prog[12:16], kp, old.MAJOR, rA, 69, 86, 79)
    mel = old.end_on_root(a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12), kp, 69, 86)
    for pos, d, mm in mel:
        m.add(vibes(midi_hz(mm), d * m.beat, m.vel(0.7 if pos % 1 == 0 else 0.6), rel=1.2), pos, pan=0.12, rev=0.42, humanize=0.006)
    rng = np.random.default_rng(505)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        vo = sorted(old.nearest(60 + (p - 60) % 12, [p], 55, 67) for p in cps)
        arp = vo + [vo[1] + 12, vo[2] + 12, vo[1] + 12]
        for k in range(12):   # 3連の分散和音（上がって下りる）＝流れ
            m.add(harp(midi_hz(arp[k % 6]), 0.5 * m.beat, m.vel(0.5 if k % 3 == 0 else 0.36)), bar * 4 + k / 3.0, pan=-0.3, rev=0.3, gain=1.6, humanize=0.004)
        root = old.nearest(41, [r], 36, 47)
        m.add(finger_bass(midi_hz(root), 1.6 * m.beat, m.vel(0.62, 0.05), tone=0.3), bar * 4, rev=0.04, humanize=0.003)
        m.add(finger_bass(midi_hz(root + 7), 1.2 * m.beat, m.vel(0.48, 0.05), tone=0.3), bar * 4 + 2, rev=0.04, humanize=0.003)
        m.add(finger_bass(midi_hz(root + 12), 0.5 * m.beat, m.vel(0.4, 0.05), tone=0.3), bar * 4 + 3.5, rev=0.04, humanize=0.003)
        for k in range(12): m.add(shaker2(m.vel(0.5 if k % 3 == 0 else 0.3)), bar * 4 + k / 3.0, pan=0.35, rev=0.08, humanize=0.003)
        m.add(kick2(m.vel(0.5, 0.04), punch=0.3, low=0.5), bar * 4, rev=0.02, humanize=0.002)
        m.add(kick2(m.vel(0.38, 0.04), punch=0.3, low=0.5), bar * 4 + 2, rev=0.02, humanize=0.002)
        if bar % 4 == 3: drops(m, bar, kp, rng)
    st = master(m, rt60=1.8, rev_level=0.4, damp=0.5, lowcut=32, warmth_db=0.5, air_db=1.0, comp_thresh=-16, comp_ratio=1.6, target_rms_db=-13.0)
    save(st, path or os.path.join(OUT, "bgm_mizu_a.wav"))

def mizu_b(path=None):
    """B「せせらぎのおさんぽ」：G長調 116BPM、はずむ感じ。カリンバのメロディ＋ビブラフォンの重ね、木のマリンバのはずむ低音、
    ボンゴとウッドブロック。Bメロは笛"""
    m = Mix(116, 16, tail=3.0, seed=52)
    prog = ['G', 'Em', 'C', 'D', 'G', 'Em', 'Am', 'D', 'C', 'D', 'Bm', 'Em', 'C', 'D', 'G', 'G']
    kp = old.NOTE['G']
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 2), (2, 1), (3, 1)]]
    a = old.make_melody(np.random.default_rng(511), prog[:4], kp, old.MAJOR, rA, 71, 88, 79)
    a2 = old.make_melody(np.random.default_rng(512), prog[4:8], kp, old.MAJOR, rA, 71, 88, a[-1][2])
    b = old.make_melody(np.random.default_rng(513), prog[8:12], kp, old.MAJOR, rA, 71, 88, 83)
    c = old.make_melody(np.random.default_rng(514), prog[12:16], kp, old.MAJOR, rA[:3] + [[(0, 4)]], 71, 88, 81)
    mel = old.end_on_root(a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12), kp, 71, 88)
    for pos, d, mm in mel:
        acc = 0.75 if pos % 1 == 0 else 0.62
        if 32 <= pos < 48:   # Bメロ：笛
            m.add(wind(midi_hz(mm), d * m.beat * 0.9, m.vel(acc + 0.05), kind='flute', vib=0.7, breath=0.6, taper=0.2), pos, pan=0.1, rev=0.3, gain=1.7, humanize=0.006)
        else:
            m.add(kalimba(midi_hz(mm), d * m.beat, m.vel(acc)), pos, pan=0.12, rev=0.3, gain=1.3, humanize=0.006)
            m.add(vibes(midi_hz(mm), d * m.beat, m.vel(acc * 0.45), rel=0.8), pos, pan=-0.1, rev=0.35, humanize=0.006)
    rng = np.random.default_rng(515)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(43, [r], 38, 49)
        for beat, iv, v in ((0, 0, 0.7), (1, 7, 0.5), (1.5, 12, 0.4), (2, 0, 0.6), (3, 7, 0.5), (3.5, 4 if 4 in [(p - r) % 12 for p in cps] else 3, 0.4)):
            m.add(marimba3(midi_hz(root + iv), 0.45 * m.beat, m.vel(v), hard=0.3), bar * 4 + beat, pan=-0.15, rev=0.12, gain=1.4, humanize=0.004)
        vo = sorted(old.nearest(64 + (p - 64) % 12, [p], 59, 71) for p in cps)
        for off in (0.5, 1.5, 2.5, 3.5):
            for mm in vo: m.add(warm_pad(midi_hz(mm), 0.35 * m.beat, 0.25, bright=0.4), bar * 4 + off, pan=-0.35, rev=0.15, humanize=0)
        for beat, hi in ((0, False), (1.5, True), (2, False), (2.75, True), (3.5, True)):
            m.add(bongo2(hi, m.vel(0.6)), bar * 4 + beat, pan=0.3, rev=0.12, humanize=0.004)
        for k in range(8): m.add(shaker2(m.vel(0.5 if k % 2 else 0.3)), bar * 4 + k * 0.5, pan=0.4, rev=0.06, humanize=0.003)
        if bar % 2 == 1: m.add(woodblock2(0.5, hi=True), bar * 4 + 3.5, pan=-0.4, rev=0.15)
        if bar % 4 == 3: drops(m, bar, kp, rng, base=86)
    st = master(m, rt60=1.4, rev_level=0.35, damp=0.5, lowcut=32, warmth_db=0.5, air_db=1.0, comp_thresh=-16, comp_ratio=1.8, target_rms_db=-13.0)
    save(st, path or os.path.join(OUT, "bgm_mizu_b.wav"))

def mizu_c(path=None, mgain=1.8, msoft=0.3, mrev=0.4):
    """C「すいめんのひかり」：D長調 88BPM、ゆったりきらきら。笛のメロディ、ビブラフォンの16分の分散和音（水面のきらめき）、
    あたたかいパッド、低いベース、シェイカーだけ。グロッケンのしずく"""
    m = Mix(88, 16, tail=4.0, seed=53)
    prog = ['Dmaj7', 'Bm7', 'Gmaj7', 'A', 'Dmaj7', 'Bm7', 'Em7', 'A', 'Gmaj7', 'A', 'Dmaj7', 'Bm7', 'Gmaj7', 'A', 'Dmaj7', 'Dmaj7']
    kp = old.NOTE['D']
    rA = [[(0, 2), (2, 1), (3, 1)], [(0, 3), (3, 1)], [(0, 1), (1, 1), (2, 2)], [(0, 4)]]
    a = old.make_melody(np.random.default_rng(521), prog[:4], kp, old.MAJOR, rA, 69, 86, 78)
    a2 = old.make_melody(np.random.default_rng(522), prog[4:8], kp, old.MAJOR, rA, 69, 86, a[-1][2])
    b = old.make_melody(np.random.default_rng(523), prog[8:12], kp, old.MAJOR, rA, 71, 88, 81)
    c = old.make_melody(np.random.default_rng(524), prog[12:16], kp, old.MAJOR, rA, 69, 86, 78)
    mel = old.end_on_root(a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12), kp, 69, 86)
    for pos, d, mm in mel:
        m.add(wind(midi_hz(mm), d * m.beat * 0.95, m.vel(0.72), kind='flute', vib=0.8, breath=0.7, taper=0.25, soft=msoft), pos, pan=0.1, rev=mrev, gain=mgain, humanize=0.008)
    rng = np.random.default_rng(525)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        vo = sorted(old.nearest(67 + (p - 67) % 12, [p], 62, 74) for p in cps)
        seq = vo + vo[::-1][1:-1]
        for k in range(16):   # 16分のきらめき（小さく）
            m.add(vibes(midi_hz(seq[k % len(seq)]), 0.3 * m.beat, m.vel(0.34 if k % 4 == 0 else 0.24), rel=0.9), bar * 4 + k * 0.25, pan=(-0.35 if k % 2 else 0.3), rev=0.45, humanize=0.003)
        for p in sorted(old.nearest(55 + (q - 55) % 12, [q], 52, 64) for q in cps):
            m.add(warm_pad(midi_hz(p), 4 * m.beat + 0.3, 0.32, bright=0.3), bar * 4, rev=0.35, humanize=0)
        root = old.nearest(38, [r], 33, 44)
        m.add(sub_bass(midi_hz(root), 2.5 * m.beat, m.vel(0.2, 0.04)), bar * 4, rev=0.05, humanize=0.003)
        m.add(sub_bass(midi_hz(root + 7), 1.2 * m.beat, m.vel(0.14, 0.04)), bar * 4 + 2.5, rev=0.05, humanize=0.003)
        for k in range(8): m.add(shaker2(m.vel(0.4 if k % 2 else 0.25)), bar * 4 + k * 0.5, pan=0.4, rev=0.1, humanize=0.003)
        if bar % 2 == 1:
            for j in range(int(rng.integers(2, 4))):
                pc = [(kp + s) % 12 for s in PENTA]
                m.add(glock(midi_hz(old.nearest(88 + int(rng.integers(-4, 5)), pc, 84, 96)), 0.5, m.vel(0.22), hard=0.4), bar * 4 + 2.5 + j * 0.33, pan=float(rng.uniform(-0.6, 0.6)), rev=0.6, humanize=0.004)
    st = master(m, rt60=2.2, rev_level=0.45, damp=0.55, lowcut=30, warmth_db=0.8, air_db=0.8, comp_thresh=-16, comp_ratio=1.5, target_rms_db=-13.5)
    save(st, path or os.path.join(OUT, "bgm_mizu_c.wav"))

def mizu_c_long(path=None, mgain=0.85, msoft=0.6, mrev=0.6):
    """2–1 の採用版（2026-10-25 てじゃ「C3かな」＝C の笛をひかえめ：音量 0.85・soft 0.6・響き 0.6）を、ほかの曲と同じく約3倍に。
    A（C3 そのまま16小節）→ B（ロ短調寄りの16小節：低めの長い音のメロディ、伴奏はハープの8分の分散和音、シェイカーは裏だけ）
    → A'（A のメロディ＋ビブラフォンの3度下のハモり（後半8小節）） → 頭に戻る。48小節＝約2分11秒"""
    m = Mix(88, 48, tail=4.0, seed=53)
    kp = old.NOTE['D']; MAJOR = old.MAJOR
    progA = ['Dmaj7', 'Bm7', 'Gmaj7', 'A', 'Dmaj7', 'Bm7', 'Em7', 'A', 'Gmaj7', 'A', 'Dmaj7', 'Bm7', 'Gmaj7', 'A', 'Dmaj7', 'Dmaj7']
    progB = ['Bm7', 'Gmaj7', 'Dmaj7', 'A', 'Bm7', 'Em7', 'Gmaj7', 'A', 'Em7', 'Gmaj7', 'Dmaj7', 'Bm7', 'Em7', 'Gmaj7', 'A', 'A']
    prog = progA + progB + progA
    rA = [[(0, 2), (2, 1), (3, 1)], [(0, 3), (3, 1)], [(0, 1), (1, 1), (2, 2)], [(0, 4)]]
    rB = [[(0, 4)], [(0, 2), (2, 2)], [(1, 3)], [(0, 3), (3, 1)]]
    # A のメロディ（mizu_c と同じ乱数・同じ作り方）
    a = old.make_melody(np.random.default_rng(521), progA[:4], kp, MAJOR, rA, 69, 86, 78)
    a2 = old.make_melody(np.random.default_rng(522), progA[4:8], kp, MAJOR, rA, 69, 86, a[-1][2])
    b = old.make_melody(np.random.default_rng(523), progA[8:12], kp, MAJOR, rA, 71, 88, 81)
    c = old.make_melody(np.random.default_rng(524), progA[12:16], kp, MAJOR, rA, 69, 86, 78)
    melA = old.end_on_root(a + old.transpose_rest(a2, 4) + old.transpose_rest(b, 8) + old.transpose_rest(c, 12), kp, 69, 86)
    # B のメロディ：低め・長い音
    b1 = old.make_melody(np.random.default_rng(531), progB[:4], kp, MAJOR, rB, 66, 81, 74)
    b2 = old.make_melody(np.random.default_rng(532), progB[4:8], kp, MAJOR, rB, 66, 81, b1[-1][2])
    b3 = old.make_melody(np.random.default_rng(533), progB[8:12], kp, MAJOR, rB, 67, 83, 76)
    b4 = old.make_melody(np.random.default_rng(534), progB[12:16], kp, MAJOR, rB, 66, 81, 73)
    melB = b1 + old.transpose_rest(b2, 4) + old.transpose_rest(b3, 8) + old.transpose_rest(b4, 12)
    mel = melA + [(p + 64, d, mm) for p, d, mm in melB] + [(p + 128, d, mm) for p, d, mm in melA]
    for pos, d, mm in mel:
        m.add(wind(midi_hz(mm), d * m.beat * 0.95, m.vel(0.72), kind='flute', vib=0.8, breath=0.7, taper=0.25, soft=msoft), pos, pan=0.1, rev=mrev, gain=mgain, humanize=0.008)
        # A' の後半：ビブラフォンの3度下のハモり（小さく）
        if pos >= 128 + 32:
            def third_below(x, cname):
                _, cps = old.chord(cname); sc = old.scale_for(kp, MAJOR, cps); q = x
                for _ in range(2): q = old.step(q, sc, -1)
                return q
            h = third_below(mm, prog[min(len(prog) - 1, int(pos // 4))])
            m.add(vibes(midi_hz(h), d * m.beat, m.vel(0.32), rel=1.0), pos, pan=-0.25, rev=0.5, humanize=0.006)
    rng = np.random.default_rng(525)
    for bar, cn in enumerate(prog):
        inB = 16 <= bar < 32
        r, cps = old.chord(cn)
        if inB:
            # B：ハープの8分の分散和音（ゆっくり流れる）
            vo = sorted(old.nearest(62 + (p - 62) % 12, [p], 57, 69) for p in cps)
            seq = vo + [vo[1] + 12, vo[2] + 12, vo[1] + 12]
            for k in range(8):
                m.add(harp(midi_hz(seq[k % len(seq)]), 0.5 * m.beat, m.vel(0.42 if k % 4 == 0 else 0.3)), bar * 4 + k * 0.5, pan=-0.3, rev=0.4, gain=1.5, humanize=0.004)
        else:
            vo = sorted(old.nearest(67 + (p - 67) % 12, [p], 62, 74) for p in cps)
            seq = vo + vo[::-1][1:-1]
            for k in range(16):   # 16分のきらめき（小さく）
                m.add(vibes(midi_hz(seq[k % len(seq)]), 0.3 * m.beat, m.vel(0.34 if k % 4 == 0 else 0.24), rel=0.9), bar * 4 + k * 0.25, pan=(-0.35 if k % 2 else 0.3), rev=0.45, humanize=0.003)
        for p in sorted(old.nearest(55 + (q - 55) % 12, [q], 52, 64) for q in cps):
            m.add(warm_pad(midi_hz(p), 4 * m.beat + 0.3, 0.32, bright=0.3), bar * 4, rev=0.35, humanize=0)
        root = old.nearest(38, [r], 33, 44)
        m.add(sub_bass(midi_hz(root), 2.5 * m.beat, m.vel(0.2, 0.04)), bar * 4, rev=0.05, humanize=0.003)
        m.add(sub_bass(midi_hz(root + 7), 1.2 * m.beat, m.vel(0.14, 0.04)), bar * 4 + 2.5, rev=0.05, humanize=0.003)
        for k in range(8):
            if inB and k % 2 == 0: continue   # B はシェイカーを裏だけ
            m.add(shaker2(m.vel(0.4 if k % 2 else 0.25)), bar * 4 + k * 0.5, pan=0.4, rev=0.1, humanize=0.003)
        if bar % 2 == 1:
            for j in range(int(rng.integers(2, 4))):
                pc = [(kp + s) % 12 for s in PENTA]
                m.add(glock(midi_hz(old.nearest(88 + int(rng.integers(-4, 5)), pc, 84, 96)), 0.5, m.vel(0.22), hard=0.4), bar * 4 + 2.5 + j * 0.33, pan=float(rng.uniform(-0.6, 0.6)), rev=0.6, humanize=0.004)
    st = master(m, rt60=2.2, rev_level=0.45, damp=0.55, lowcut=30, warmth_db=0.8, air_db=0.8, comp_thresh=-16, comp_ratio=1.5, target_rms_db=-13.5)
    save(st, path or os.path.join(OUT, "bgm_mizu_long.wav"))

if __name__ == '__main__':
    args = sys.argv[1:]
    out = None
    if args and (os.path.isdir(args[0]) or args[0].endswith(os.sep) or '\\' in args[0] or '/' in args[0]):
        out = args.pop(0)
    # 引数 k=v で調整（例：mizu_c mgain=1.2）
    kw = {a.split('=')[0]: float(a.split('=')[1]) for a in args if '=' in a}
    names = [a for a in args if '=' not in a]
    for w in (names or ['mizu_a', 'mizu_b', 'mizu_c']):
        globals()[w](os.path.join(out, "bgm_" + w + ".wav") if out else None, **kw)
