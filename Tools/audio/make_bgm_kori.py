# ステージ2 コース2–3「こおりのみち」のBGM候補（2026-11-05、てじゃ「曲をつくっちゃおう」→ 4方向「ぜんぶいっちゃお」）。
# コース：滑る氷の島・丸い山・シーソーの板・割れ目が開いたり閉じたりする氷の道。
#  A「きらきらスケート」：ワルツ（3拍子）、ビブラフォン＋グロッケン
#  B「ゆきのよる」：ゆっくり、パッド＋鉄琴のしずく、ちょっとふしぎ
#  C「ふゆのおでかけ」：明るい行進、そりの鈴＋ピチカート＋口笛
#  D「こおりのどうくつ」：低音しっかり、氷のきしみっぽい金属、カッコイイ系
# 風っぽいノイズ（ひゅう）は入れない（コースのギミックの音とまぎれる。2026-10-04 てじゃ）
#   python make_bgm_kori.py <出力フォルダ>            → 4つとも
#   python make_bgm_kori.py <出力フォルダ> kori_a     → 1つだけ
import sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_bgm as old
from fluffsynth import *

PENTA = [0, 2, 4, 7, 9]
OUT = sys.argv[1] if len(sys.argv) > 1 else old.OUT


def melody(rng, chords, key_pc, mode, rhythm_bars, lo, hi, start, bpb=4):
    """make_bgm.make_melody と同じ作り方で、1小節の拍数 bpb（ワルツは 3）"""
    notes = []; p = start
    for b, (cname, rhythm) in enumerate(zip(chords, rhythm_bars)):
        _, cps = old.chord(cname)
        sc = old.scale_for(key_pc, mode, cps)
        for (pos, dur) in rhythm:
            strong = abs(pos - round(pos)) < 1e-6 and (int(round(pos)) == 0 or (bpb == 4 and int(round(pos)) == 2))
            if strong:
                p = old.nearest(p + rng.choice([-3, -2, 0, 2, 3, 4]), cps, lo, hi)
            else:
                d = rng.choice([-1, 1, 1, -1, 2, -2]); q = p
                for _ in range(abs(d)): q = old.step(q, sc, 1 if d > 0 else -1)
                if lo <= q <= hi: p = q
            notes.append((b * bpb + pos, dur, p))
    return notes


def phrase(seed, prog, key_pc, mode, rhythms, lo, hi, start, bpb=4):
    """4小節ずつ作ってつなぐ（4小節ごとに乱数を変える）。最後はその調の主音へ"""
    out = []; p = start
    for i in range(0, len(prog), 4):
        n = melody(np.random.default_rng(seed + i), prog[i:i + 4], key_pc, mode, [rhythms[(i + j) % len(rhythms)] for j in range(4)], lo, hi, p, bpb)
        out += [(pos + i * bpb, d, m) for pos, d, m in n]; p = n[-1][2]
    return old.end_on_root(out, key_pc, lo, hi)


def jingle(vel=0.5, dur=0.35, seed=0):
    """そりの鈴：高い金属の小さい鈴を何個かずらして鳴らす（シャラン）"""
    r = np.random.default_rng(seed); n = int(SR * dur); t = np.arange(n) / SR; y = np.zeros(n)
    for k in range(5):
        f0 = r.uniform(3600, 5600); off = int(r.uniform(0, 0.025) * SR)
        part = sum(a * np.sin(2 * np.pi * f0 * h * t) * np.exp(-t / (0.09 / h ** 0.4)) for h, a in ((1, 1.0), (1.47, 0.5), (2.09, 0.3)))
        y[off:] += part[:n - off] * 0.3
    y += hp(r.standard_normal(n), 6000) * np.exp(-t / 0.03) * 0.08
    return y * vel * np.clip(t / 0.002, 0, 1)


def pizz(f, dur, vel=0.6):
    """ピチカート：短くはじく弦"""
    return ks_string(f, min(dur, 0.6), vel, bright=0.55, damp_time=0.28, body=True)


def creak(f, dur, vel=0.4):
    """氷のきしみっぽい金属：グロッケンを少しずらした2本で、うなりと短いすべり"""
    t = np.arange(int(SR * (dur + 1.2))) / SR
    bend = 1 - 0.012 * np.exp(-t / 0.08)
    ph = 2 * np.pi * np.cumsum(f * bend) / SR
    y = sum(a * np.sin(ph * h) * np.exp(-t / d) for h, a, d in ((1, 1.0, 0.9), (2.76, 0.35, 0.3), (5.4, 0.12, 0.1)))
    y2 = np.sin(ph * 1.006) * np.exp(-t / 0.7) * 0.6
    return (y + y2) * vel * np.clip(t / 0.003, 0, 1)


def kori_a(path=None):
    """A「きらきらスケート」：A長調 3拍子 138BPM。ビブラフォンのメロディ（グロッケンを小さく重ねる）、
    ワルツの伴奏（1拍目にベース、2・3拍目にチェレスタの和音）、2・3拍目に小さいシェイカー。B（17〜32小節）は笛がメロディ"""
    bars = 32; m = Mix(138, bars, beats_per_bar=3, tail=3.0, seed=61)
    kp = old.NOTE['A']
    progA = ['A', 'F#m', 'D', 'E', 'A', 'C#m', 'D', 'E', 'F#m', 'D', 'A', 'E', 'D', 'E', 'A', 'A']
    progB = ['D', 'E', 'C#m', 'F#m', 'Bm', 'E', 'A', 'A', 'D', 'E', 'C#m', 'F#m', 'Bm', 'E', 'A', 'E']
    prog = progA + progB
    rA = [[(0, 1), (1, 1), (2, 1)], [(0, 2), (2, 1)], [(0, 1.5), (1.5, 0.5), (2, 1)], [(0, 3)]]
    rB = [[(0, 2), (2, 1)], [(0, 3)], [(0, 1), (1, 2)], [(0, 3)]]
    melA = phrase(601, progA, kp, old.MAJOR, rA, 69, 88, 76, 3)
    melB = [(p + 16 * 3, d, mm) for p, d, mm in phrase(611, progB, kp, old.MAJOR, rB, 66, 84, 73, 3)]
    for pos, d, mm in melA:
        acc = 0.72 if pos % 3 == 0 else 0.58
        m.add(vibes(midi_hz(mm), d * m.beat, m.vel(acc), rel=1.0), pos, pan=0.12, rev=0.45, humanize=0.006)
        m.add(glock(midi_hz(mm + 12), 0.4, m.vel(acc * 0.22), hard=0.4), pos, pan=-0.2, rev=0.55, humanize=0.006)
    for pos, d, mm in melB:
        m.add(wind(midi_hz(mm), d * m.beat * 0.92, m.vel(0.7), kind='flute', vib=0.8, breath=0.6, taper=0.2, soft=0.4), pos, pan=0.1, rev=0.45, gain=1.2, humanize=0.006)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(45, [r], 40, 51)
        m.add(finger_bass(midi_hz(root), 1.0 * m.beat, m.vel(0.6, 0.05), tone=0.35), bar * 3, rev=0.05, humanize=0.003)
        if bar % 2 == 1: m.add(finger_bass(midi_hz(root + 7), 0.8 * m.beat, m.vel(0.4, 0.05), tone=0.35), bar * 3 + 2, rev=0.05, humanize=0.003)
        vo = sorted(old.nearest(64 + (p - 64) % 12, [p], 59, 71) for p in cps)
        for beat in (1, 2):
            for mm in vo: m.add(celesta(midi_hz(mm), 0.45 * m.beat, m.vel(0.3 if beat == 1 else 0.24), hard=0.3), bar * 3 + beat, pan=-0.3, rev=0.35, humanize=0.003)
            m.add(shaker2(m.vel(0.32)), bar * 3 + beat, pan=0.4, rev=0.08, humanize=0.003)
        if bar % 4 == 3:   # 4小節ごとにきらっ
            pc = [(kp + s) % 12 for s in PENTA]
            for j in range(3): m.add(glock(midi_hz(old.nearest(91 - j * 3, pc, 84, 96)), 0.5, m.vel(0.2), hard=0.4), bar * 3 + 2 + j * 0.33, pan=0.5 - j * 0.4, rev=0.6)
    st = master(m, rt60=2.0, rev_level=0.42, damp=0.5, lowcut=32, warmth_db=0.5, air_db=1.2, comp_thresh=-16, comp_ratio=1.6, target_rms_db=-13.0)
    save(st, path or os.path.join(OUT, "bgm_kori_a.wav"))


def kori_b(path=None):
    """B「ゆきのよる」：E短調 72BPM。長い音のビブラフォン＋チェレスタのメロディ、ゆっくり動くパッド、
    鉄琴のしずく（ペンタトニック）、低いベースだけ。打楽器なし（ときどき小さい鈴）"""
    bars = 24; m = Mix(72, bars, tail=5.0, seed=62)
    kp = old.NOTE['E']
    prog = ['Em', 'Cmaj7', 'G', 'D', 'Em', 'Am7', 'Cmaj7', 'D', 'Cmaj7', 'G', 'Am7', 'Em', 'Cmaj7', 'D', 'Em', 'Em',
            'Am7', 'D', 'G', 'Cmaj7', 'Am7', 'Bm7', 'Em', 'Em']
    rB = [[(0, 2), (2, 2)], [(0, 3), (3, 1)], [(1, 3)], [(0, 4)]]
    mel = phrase(621, prog, kp, old.MINOR, rB, 64, 81, 71)
    for pos, d, mm in mel:
        m.add(vibes(midi_hz(mm), d * m.beat, m.vel(0.6), rel=1.6, depth=0.2), pos, pan=0.1, rev=0.6, humanize=0.008)
        m.add(celesta(midi_hz(mm + 12), max(0.5, d * m.beat * 0.5), m.vel(0.22), hard=0.25), pos, pan=-0.15, rev=0.65, humanize=0.008)
    rng = np.random.default_rng(625)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        for p in sorted(old.nearest(55 + (q - 55) % 12, [q], 52, 64) for q in cps):
            m.add(warm_pad(midi_hz(p), 4 * m.beat + 0.5, 0.34, bright=0.25), bar * 4, rev=0.5, humanize=0)
        root = old.nearest(40, [r], 35, 46)
        m.add(sub_bass(midi_hz(root), 3.5 * m.beat, m.vel(0.2, 0.04)), bar * 4, rev=0.05, humanize=0.003)
        pc = [(kp + s) % 12 for s in [0, 3, 5, 7, 10]]
        for j in range(int(rng.integers(1, 4))):   # しずく
            mm = old.nearest(86 + int(rng.integers(-6, 7)), pc, 79, 96)
            m.add(glock(midi_hz(mm), 0.6, m.vel(0.18), hard=0.3, ring=1.3), bar * 4 + float(rng.choice([0.5, 1.5, 2.25, 3.0, 3.5])) + j * 0.17, pan=float(rng.uniform(-0.7, 0.7)), rev=0.7, humanize=0.004)
        if bar % 4 == 3: m.add(jingle(0.18, seed=bar), bar * 4 + 3.5, pan=0.5, rev=0.5)
    st = master(m, rt60=3.0, rev_level=0.5, damp=0.6, lowcut=30, warmth_db=0.8, air_db=0.8, comp_thresh=-17, comp_ratio=1.5, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_kori_b.wav"))


def kori_c(path=None):
    """C「ふゆのおでかけ」：F長調 120BPM の行進。口笛（オカリナ寄りの笛）のメロディ、ピチカートの伴奏（裏拍）、
    1・3拍目にベース、そりの鈴（8分）、やわらかいキックとスネア（2・4拍目、小さく）。B（9〜16小節）はグロッケンがメロディ"""
    bars = 24; m = Mix(120, bars, tail=3.0, seed=63)
    kp = old.NOTE['F']
    prog = ['F', 'C', 'Dm', 'Bb', 'F', 'C', 'Bb', 'C', 'Dm', 'Bb', 'F', 'C', 'Gm7', 'C', 'F', 'F',
            'Bb', 'C', 'Am', 'Dm', 'Gm7', 'C', 'F', 'C']
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2, 1), (3, 1)], [(0, 1), (1, 0.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 2), (2, 1), (3, 1)]]
    mel = phrase(631, prog, kp, old.MAJOR, rA, 69, 88, 77)
    for pos, d, mm in mel:
        acc = 0.75 if pos % 1 == 0 else 0.6
        if 32 <= pos < 64:
            m.add(glock(midi_hz(mm), d * m.beat, m.vel(acc * 0.8), hard=0.45), pos, pan=0.1, rev=0.35, humanize=0.005)
            m.add(marimba3(midi_hz(mm - 12), 0.4 * m.beat, m.vel(acc * 0.5), hard=0.3), pos, pan=-0.1, rev=0.2, humanize=0.005)
        else:
            m.add(wind(midi_hz(mm), d * m.beat * 0.85, m.vel(acc), kind='ocarina', vib=0.9, breath=0.5, taper=0.15), pos, pan=0.1, rev=0.3, gain=1.4, humanize=0.006)
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(41, [r], 36, 47)
        m.add(finger_bass(midi_hz(root), 0.9 * m.beat, m.vel(0.62, 0.05), tone=0.35), bar * 4, rev=0.04, humanize=0.003)
        m.add(finger_bass(midi_hz(root + 7), 0.9 * m.beat, m.vel(0.5, 0.05), tone=0.35), bar * 4 + 2, rev=0.04, humanize=0.003)
        vo = sorted(old.nearest(60 + (p - 60) % 12, [p], 55, 67) for p in cps)
        for off in (1, 3):
            for mm in vo: m.add(pizz(midi_hz(mm), 0.4 * m.beat, m.vel(0.42)), bar * 4 + off, pan=-0.3, rev=0.2, gain=1.3, humanize=0.004)
        for k in range(8): m.add(jingle(m.vel(0.42 if k % 2 == 0 else 0.28), dur=0.3, seed=k + bar * 8), bar * 4 + k * 0.5, pan=0.4, rev=0.15, humanize=0.003)
        m.add(kick2(m.vel(0.5, 0.04), punch=0.3, low=0.5), bar * 4, rev=0.02, humanize=0.002)
        m.add(kick2(m.vel(0.42, 0.04), punch=0.3, low=0.5), bar * 4 + 2, rev=0.02, humanize=0.002)
        for beat in (1, 3): m.add(snare2(m.vel(0.3), tone=0.4, snap=0.3), bar * 4 + beat, pan=0.1, rev=0.15, humanize=0.003)
    st = master(m, rt60=1.5, rev_level=0.35, damp=0.5, lowcut=32, warmth_db=0.5, air_db=1.0, comp_thresh=-16, comp_ratio=1.8, target_rms_db=-13.0)
    save(st, path or os.path.join(OUT, "bgm_kori_c.wav"))


def kori_d(path=None):
    """D「こおりのどうくつ」：D短調 96BPM、カッコイイ系。低い 8 分のビブラフォンの刻み（リフ）、サブベース＋太いキック、タム、
    ハイハット、氷のきしみ（ずれた金属）がときどき。メロディは低めのブラス（2・4小節目の頭に「ぶぉっ」）、Bはチェレスタの高い分散和音"""
    bars = 24; m = Mix(96, bars, tail=3.0, seed=64)
    kp = old.NOTE['D']
    prog = ['Dm', 'Dm', 'Bb', 'C', 'Dm', 'Dm', 'Gm', 'A', 'Bb', 'C', 'Dm', 'Dm', 'Gm', 'Bb', 'A', 'A',
            'Dm', 'Dm', 'Bb', 'C', 'Gm', 'Bb', 'A', 'A']
    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(38, [r], 33, 44)
        inB = 8 <= bar < 16
        # 低いリフ（8分）：根音・5度・オクターブ
        riff = [0, 0, 7, 0, 12, 7, 0, 10 if 10 in [(p - r) % 12 for p in cps] or cn in ('Dm', 'Gm') else 7]
        for k, iv in enumerate(riff):
            m.add(vibes(midi_hz(root + 12 + iv), 0.45 * m.beat, m.vel(0.5 if k % 2 == 0 else 0.38), hard=0.5, depth=0.1, rel=0.3), bar * 4 + k * 0.5, pan=-0.2, rev=0.2, humanize=0.003)
        m.add(sub_bass(midi_hz(root), 1.8 * m.beat, m.vel(0.5, 0.04)), bar * 4, rev=0.03, humanize=0.002)
        m.add(sub_bass(midi_hz(root), 1.4 * m.beat, m.vel(0.42, 0.04)), bar * 4 + 2.5, rev=0.03, humanize=0.002)
        m.add(kick2(m.vel(0.85, 0.04), punch=0.6, low=0.8), bar * 4, rev=0.03, humanize=0.002)
        m.add(kick2(m.vel(0.7, 0.04), punch=0.6, low=0.8), bar * 4 + 2.5, rev=0.03, humanize=0.002)
        m.add(snare2(m.vel(0.55), tone=0.5, snap=0.5), bar * 4 + 1, pan=0.05, rev=0.2, humanize=0.003)
        m.add(snare2(m.vel(0.6), tone=0.5, snap=0.5), bar * 4 + 3, pan=0.05, rev=0.2, humanize=0.003)
        for k in range(8): m.add(hat2(m.vel(0.32 if k % 2 else 0.22)), bar * 4 + k * 0.5, pan=0.35, rev=0.05, humanize=0.002)
        if bar % 4 == 3:
            for j, tf in enumerate((110, 90, 72)): m.add(tom2(tf, m.vel(0.6), skin=0.5), bar * 4 + 3 + j * 0.25, pan=-0.3 + j * 0.3, rev=0.15)
        if bar % 2 == 1 and not inB:   # ぶぉっ（低いブラスの和音）
            for mm in sorted(old.nearest(50 + (p - 50) % 12, [p], 45, 57) for p in cps):
                m.add(brass2(midi_hz(mm), 1.2 * m.beat, m.vel(0.5), bright=0.4), bar * 4, pan=0.15, rev=0.25, gain=0.9, humanize=0.004)
        if inB:   # B：チェレスタの高い分散和音（16分）
            vo = sorted(old.nearest(74 + (p - 74) % 12, [p], 69, 81) for p in cps); seq = vo + [vo[1] + 12, vo[2] + 12, vo[1] + 12]
            for k in range(16): m.add(celesta(midi_hz(seq[k % len(seq)]), 0.25 * m.beat, m.vel(0.3 if k % 4 == 0 else 0.22), hard=0.4), bar * 4 + k * 0.25, pan=0.35 if k % 2 else -0.1, rev=0.4, humanize=0.002)
        if bar % 4 == 1:   # 氷のきしみ
            m.add(creak(midi_hz(old.nearest(88, cps, 84, 96)), 1.0, 0.25), bar * 4 + 1.5, pan=0.6, rev=0.6)
    # メロディ（A の 1〜8・17〜24 小節）：低めのブラス＋オカリナで重ねる
    rM = [[(0, 1.5), (1.5, 0.5), (2, 2)], [(0, 1), (1, 1), (2, 1.5), (3.5, 0.5)], [(0, 3), (3, 1)], [(0, 4)]]
    for start_bar, seed in ((0, 641), (16, 642)):
        mel = phrase(seed, prog[start_bar:start_bar + 8], kp, old.MINOR, rM, 62, 77, 69)
        for pos, d, mm in mel:
            pp = pos + start_bar * 4
            m.add(brass2(midi_hz(mm), d * m.beat * 0.9, m.vel(0.6), bright=0.45, voices=2), pp, pan=0.05, rev=0.3, gain=0.8, humanize=0.005)
            m.add(wind(midi_hz(mm + 12), d * m.beat * 0.9, m.vel(0.4), kind='ocarina', vib=0.6, breath=0.3), pp, pan=0.2, rev=0.35, gain=0.7, humanize=0.005)
    st = master(m, rt60=1.8, rev_level=0.38, damp=0.55, lowcut=30, warmth_db=1.0, air_db=0.6, comp_thresh=-15, comp_ratio=2.0, target_rms_db=-12.5)
    save(st, path or os.path.join(OUT, "bgm_kori_d.wav"))


def kori_b_long(path=None):
    """2–3 の採用版（2026-11-05 てじゃ「Bいいな」）を、ほかの曲と同じく長く。
    A（kori_b の 24 小節そのまま）→ B（16 小節：G長調寄り、笛の低めの長いメロディ、チェレスタの8分の分散和音、しずくは少なめ）
    → A'（A のメロディ＋後半12小節にビブラフォンの3度下のハモり）→ 頭に戻る。64 小節＝約3分33秒"""
    kp = old.NOTE['E']; MINOR = old.MINOR
    progA = ['Em', 'Cmaj7', 'G', 'D', 'Em', 'Am7', 'Cmaj7', 'D', 'Cmaj7', 'G', 'Am7', 'Em', 'Cmaj7', 'D', 'Em', 'Em',
             'Am7', 'D', 'G', 'Cmaj7', 'Am7', 'Bm7', 'Em', 'Em']
    progB = ['G', 'D', 'Em', 'Cmaj7', 'G', 'D', 'Am7', 'Bm7', 'Cmaj7', 'G', 'Am7', 'Em', 'Cmaj7', 'Am7', 'Bm7', 'Bm7']
    prog = progA + progB + progA
    m = Mix(72, len(prog), tail=5.0, seed=62)
    rB = [[(0, 2), (2, 2)], [(0, 3), (3, 1)], [(1, 3)], [(0, 4)]]
    melA = phrase(621, progA, kp, MINOR, rB, 64, 81, 71)
    rL = [[(0, 4)], [(0, 2), (2, 2)], [(0, 3), (3, 1)], [(0, 4)]]
    melB = phrase(651, progB, kp, MINOR, rL, 62, 77, 67); melB[-1] = (melB[-1][0], melB[-1][1], old.nearest(melB[-1][2], [old.NOTE['B']], 62, 77))   # B の終わりは属音（A へ戻る）
    oB, oA2 = 24 * 4, 40 * 4
    def third_below(x, cname):
        _, cps = old.chord(cname); sc = old.scale_for(kp, MINOR, cps); q = x
        for _ in range(2): q = old.step(q, sc, -1)
        return q
    for off, mel in ((0, melA), (oA2, melA)):
        for pos, d, mm in mel:
            pp = pos + off
            m.add(vibes(midi_hz(mm), d * m.beat, m.vel(0.6), rel=1.6, depth=0.2), pp, pan=0.1, rev=0.6, humanize=0.008)
            m.add(celesta(midi_hz(mm + 12), max(0.5, d * m.beat * 0.5), m.vel(0.22), hard=0.25), pp, pan=-0.15, rev=0.65, humanize=0.008)
            if off == oA2 and pos >= 12 * 4:
                m.add(vibes(midi_hz(third_below(mm, prog[min(len(prog) - 1, int(pp // 4))])), d * m.beat, m.vel(0.3), rel=1.4, depth=0.2), pp, pan=-0.3, rev=0.6, humanize=0.008)
    for pos, d, mm in melB:
        m.add(wind(midi_hz(mm), d * m.beat * 0.95, m.vel(0.62), kind='flute', vib=0.7, breath=0.6, taper=0.3, soft=0.6), pos + oB, pan=0.1, rev=0.6, gain=0.9, humanize=0.008)
    rng = np.random.default_rng(625)
    for bar, cn in enumerate(prog):
        inB = 24 <= bar < 40
        r, cps = old.chord(cn)
        for p in sorted(old.nearest(55 + (q - 55) % 12, [q], 52, 64) for q in cps):
            m.add(warm_pad(midi_hz(p), 4 * m.beat + 0.5, 0.3 if inB else 0.34, bright=0.25), bar * 4, rev=0.5, humanize=0)
        root = old.nearest(40, [r], 35, 46)
        m.add(sub_bass(midi_hz(root), 3.5 * m.beat, m.vel(0.2, 0.04)), bar * 4, rev=0.05, humanize=0.003)
        if inB:   # チェレスタの8分の分散和音（ゆっくり）
            vo = sorted(old.nearest(67 + (p - 67) % 12, [p], 62, 74) for p in cps); seq = vo + [vo[1] + 12, vo[2] + 12, vo[1] + 12]
            for k in range(8): m.add(celesta(midi_hz(seq[k % len(seq)]), 0.5 * m.beat, m.vel(0.24 if k % 4 == 0 else 0.17), hard=0.25), bar * 4 + k * 0.5, pan=-0.3 if k % 2 else 0.25, rev=0.55, humanize=0.004)
        pc = [(kp + s) % 12 for s in [0, 3, 5, 7, 10]]
        nd = int(rng.integers(0, 2)) if inB else int(rng.integers(1, 4))
        for j in range(nd):   # しずく
            mm = old.nearest(86 + int(rng.integers(-6, 7)), pc, 79, 96)
            m.add(glock(midi_hz(mm), 0.6, m.vel(0.18), hard=0.3, ring=1.3), bar * 4 + float(rng.choice([0.5, 1.5, 2.25, 3.0, 3.5])) + j * 0.17, pan=float(rng.uniform(-0.7, 0.7)), rev=0.7, humanize=0.004)
        if bar % 4 == 3: m.add(jingle(0.18, seed=bar), bar * 4 + 3.5, pan=0.5, rev=0.5)
    st = master(m, rt60=3.0, rev_level=0.5, damp=0.6, lowcut=30, warmth_db=0.8, air_db=0.8, comp_thresh=-17, comp_ratio=1.5, target_rms_db=-14.0)
    save(st, path or os.path.join(OUT, "bgm_kori_long.wav"))


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    fns = {'kori_a': kori_a, 'kori_b': kori_b, 'kori_c': kori_c, 'kori_d': kori_d, 'kori_b_long': kori_b_long}
    for name in (sys.argv[2:] or fns.keys()): fns[name]()
