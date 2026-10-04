# コース1「かぜのみち」のBGM候補（れんしゅうと同じ曲だったので専用に）。楽器・ミキサーは make_bgm.py を使う。
# 方向性：空の上の吹きさらしの道・追い風・横風・つむじ風・風車と吹き流し。さわやかで、風に乗って進む感じ
#   python make_bgm_kaze.py            → 3つとも
#   python make_bgm_kaze.py kaze_a     → 1つだけ
import sys
import numpy as np
from make_bgm import *            # SR, Song, save, 楽器, 作曲まわり
from make_bgm_tower import voicing, swing

MIXO = [0, 2, 4, 5, 7, 9, 10]     # ミクソリディア（ちょっと民謡っぽい明るさ）

def flute(f, dur, vel=1.0):
    # 息まじりの笛：サイン＋ちょっとの倍音、ゆっくり立ち上がって、息の音を少し混ぜる
    y = additive(f, dur, [(1, 1.0), (2, 0.12), (3, 0.04)], [None, None, None], tail=0.15, vib=0.006, vib_rate=5.2)
    e = env_adsr(len(y), 0.06, 0.12, 0.85, 0.12, dur)
    nz = rng_noise.standard_normal(len(y)); nz = np.convolve(nz, np.ones(4) / 4, mode='same')
    return (y + 0.09 * nz * (0.4 + 0.6 * np.exp(-np.arange(len(y)) / SR / 0.08))) * e * vel * 0.3

def gust(dur, vel=1.0, seed=0):
    # 「ひゅうぅ」：こもらせたノイズが、ふくらんで音程（こもり具合）が上がって、すっと抜ける
    n = int((dur + 0.5) * SR)
    r = np.random.default_rng(seed)
    nz = r.standard_normal(n)
    t = np.arange(n) / n
    # 時間とともに平均の幅を変えて、こもり→明るい→こもり（風が通り過ぎる）
    out = np.zeros(n)
    widths = (90, 40, 18)
    mixes = [np.clip(1 - 3 * np.abs(t - c), 0, 1) for c in (0.15, 0.5, 0.8)]
    for w, m in zip(widths, mixes):
        out += np.convolve(nz, np.ones(w) / w, mode='same') * m * (w / 40) ** 0.5
    e = np.sin(np.pi * np.clip(t, 0, 1)) ** 1.5
    return out * e * vel * 0.5

def chime(f, vel=1.0):
    # 風鈴：高い、すんだ短い鈴（倍音が非整数）
    y = additive(f, 0.1, [(1, 1.0), (2.76, 0.4), (5.4, 0.2)], [1.2, 0.5, 0.25], tail=1.4)
    t = np.arange(len(y)) / SR
    return y * np.clip(t / 0.003, 0, 1) * vel * 0.16

def harp(f, dur, vel=1.0):
    return pluck(f, dur, vel * 0.8, bright=1.6)

def kaze_a():
    # A「そよかぜのみち」：D長調 96BPM、3連でゆれる（6/8っぽい）。笛のメロディ、ハープの分散和音、
    # シェイカーとやわらかいキック、4小節ごとに「ひゅうぅ」と風が左右に通り抜ける、ときどき風鈴
    s = Song(96, 16)
    prog = ['D', 'A', 'Bm', 'G', 'D', 'A', 'G', 'A', 'Em', 'A', 'D', 'Bm', 'G', 'A', 'D', 'D']
    kp = NOTE['D']
    rA = [[(0, 1), (1, 2.0 / 3), (5.0 / 3, 1.0 / 3), (2, 2)], [(0, 2.0 / 3), (2.0 / 3, 1.0 / 3), (1, 1), (2, 1.5), (3.5, 0.5)],
          [(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 3)]]
    a = make_melody(np.random.default_rng(301), prog[:4], kp, MAJOR, rA, 69, 86, 74)
    a2 = make_melody(np.random.default_rng(302), prog[4:8], kp, MAJOR, rA, 69, 86, a[-1][2])
    b = make_melody(np.random.default_rng(303), prog[8:12], kp, MAJOR, rA, 71, 88, 78)
    c = make_melody(np.random.default_rng(304), prog[12:16], kp, MAJOR, rA, 69, 86, 76)
    mel = end_on_root(a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12), kp, 69, 86)
    for pos, d, m in mel:
        s.add(flute(midi_hz(m), d * s.beat * 0.95, 1.0), pos, pan=0.1, rev=0.35)
    rng = np.random.default_rng(305)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        vo = voicing(cps, 62, 57, 69)
        arp = vo + [vo[1] + 12, vo[2] + 12, vo[1] + 12]
        for k in range(12):   # 3連の分散和音（上がって下りる）
            m = arp[k % 6]
            s.add(harp(midi_hz(m), 0.6 * s.beat, 0.55 if k % 3 == 0 else 0.4), bar * 4 + k / 3.0, pan=-0.25, rev=0.3)
        root = nearest(38, [r], 33, 44)
        s.add(bass(midi_hz(root), 1.8 * s.beat, 0.6, tone=0.3), bar * 4, rev=0.1)
        s.add(bass(midi_hz(root + 7), 1.6 * s.beat, 0.45, tone=0.3), bar * 4 + 2, rev=0.1)
        for k in range(12): s.add(shaker(0.6 if k % 3 == 0 else 0.35), bar * 4 + k / 3.0, pan=0.35, rev=0.1)
        s.add(kick(0.4), bar * 4, rev=0.05); s.add(kick(0.25), bar * 4 + 2, rev=0.05)
        if bar % 4 == 3:
            s.add(gust(3.0 * s.beat, 0.9, seed=bar), bar * 4 + 1, pan=-0.6 if bar % 8 == 3 else 0.6, rev=0.3)
        if rng.random() < 0.5:
            for k in range(rng.integers(2, 4)):
                s.add(chime(midi_hz(nearest(90 + rng.integers(-4, 5), cps, 86, 98)), 0.8), bar * 4 + 2.5 + k * 0.25, pan=rng.uniform(-0.7, 0.7), rev=0.5)
    save(s.render(1.6), "bgm_kaze_a.wav")

def kaze_b():
    # B「おいかぜピクニック」：G長調 126BPM、軽快。口笛と笛のユニゾンのメロディ、アコーディオン風の
    # 刻み（パッドを短く）、はずむベース、手拍子。8小節ごとに風が通り抜けて、風鈴がしゃらん
    s = Song(126, 16)
    prog = ['G', 'C', 'D', 'G', 'Em', 'C', 'D', 'D', 'C', 'D', 'Bm', 'Em', 'C', 'D', 'G', 'G']
    kp = NOTE['G']
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2, 0.5), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 2), (2, 1), (3, 1)]]
    a = make_melody(np.random.default_rng(311), prog[:4], kp, MAJOR, rA, 71, 88, 79)
    a2 = make_melody(np.random.default_rng(312), prog[4:8], kp, MAJOR, rA, 71, 88, a[-1][2])
    b = make_melody(np.random.default_rng(313), prog[8:12], kp, MAJOR, rA, 71, 88, 83)
    c = make_melody(np.random.default_rng(314), prog[12:16], kp, MAJOR, rA[:3] + [[(0, 4)]], 71, 88, 81)
    mel = end_on_root(a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12), kp, 71, 88)
    for pos, d, m in mel:
        p = swing(pos)
        s.add(whistle(midi_hz(m), d * s.beat * 0.85, 0.9), p, pan=0.15, rev=0.25)
        s.add(flute(midi_hz(m - 12), d * s.beat * 0.85, 0.45), p, pan=-0.1, rev=0.25)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        vo = voicing(cps, 64, 59, 71)
        for off in (0.5, 1.5, 2.5, 3.5):   # 裏で「ぷっ」と刻む（アコーディオンっぽく）
            for m in vo: s.add(pad(midi_hz(m), 0.35 * s.beat, 0.32, bright=4), bar * 4 + swing(off), pan=-0.3, rev=0.15)
        root = nearest(43, [r], 38, 49)
        for beat, iv in ((0, 0), (1, 7), (2, 12), (3, 7)):
            s.add(bass(midi_hz(root + iv if iv < 12 else root + 12), 0.5 * s.beat, 0.6 if beat % 2 == 0 else 0.45, tone=0.35), bar * 4 + beat, rev=0.05)
        for beat in (1, 3): s.add(snare(0.4), bar * 4 + beat, pan=0.2, rev=0.25)
        for k in range(8): s.add(shaker(0.8 if k % 2 else 0.5), bar * 4 + swing(k * 0.5), pan=0.35, rev=0.1)
        s.add(kick(0.55), bar * 4, rev=0.05); s.add(kick(0.4), bar * 4 + 2, rev=0.05)
        if bar % 8 == 7:
            s.add(gust(4.0 * s.beat, 1.0, seed=100 + bar), bar * 4, pan=0.0, rev=0.3)
            for k in range(5): s.add(chime(midi_hz(nearest(88 + k * 2, cps, 84, 100)), 0.7), bar * 4 + 3 + k * 0.12, pan=-0.5 + 0.25 * k, rev=0.5)
    save(s.render(1.3), "bgm_kaze_b.wav")

def kaze_c(gusts=True, name="bgm_kaze_c.wav", accent="chime"):
    # C「かざぐるまの丘」：Aミクソリディア 108BPM、民謡っぽい。笛のメロディ（細かい装飾）、
    # ギター風のストローク、低いタムの「どん・どどん」、ドローン。風の音が多め（2小節ごと）で、空の上の吹きさらし感
    s = Song(108, 16)
    prog = ['A', 'G', 'A', 'G', 'D', 'A', 'G', 'A', 'D', 'G', 'A', 'Em', 'D', 'G', 'A', 'A']
    kp = NOTE['A']
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5), (3.5, 0.5)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 2)],
          [(0, 1.5), (1.5, 0.25), (1.75, 0.25), (2, 1), (3, 1)], [(0, 3), (3, 1)]]
    a = make_melody(np.random.default_rng(321), prog[:4], kp, MIXO, rA, 69, 86, 76)
    a2 = make_melody(np.random.default_rng(322), prog[4:8], kp, MIXO, rA, 69, 86, a[-1][2])
    b = make_melody(np.random.default_rng(323), prog[8:12], kp, MIXO, rA, 71, 88, 81)
    c = make_melody(np.random.default_rng(324), prog[12:16], kp, MIXO, rA, 69, 86, 79)
    mel = end_on_root(a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12), kp, 69, 86)
    for i, (pos, d, m) in enumerate(mel):
        s.add(flute(midi_hz(m), d * s.beat * 0.92, 1.0), pos, pan=0.1, rev=0.35)
        if d >= 1 and i % 3 == 0 and pos >= 0.12:   # 長い音の前に短い装飾音
            s.add(flute(midi_hz(m + 2), 0.08, 0.5), pos - 0.12, pan=0.1, rev=0.3)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        vo = voicing(cps, 57, 52, 64) + [voicing(cps, 64, 59, 71)[0]]
        for off, vel in ((0, 0.5), (1, 0.35), (1.5, 0.3), (2.5, 0.35), (3, 0.4)):   # ストローク
            for k, m in enumerate(vo): s.add(pluck(midi_hz(m), 0.4 * s.beat, vel, bright=1.0), bar * 4 + off + k * 0.015, pan=-0.3, rev=0.2)
        if bar % 4 == 0: s.add(pad(midi_hz(nearest(45, [kp], 40, 50)), 16 * s.beat, 0.3, bright=2), bar * 4, rev=0.4)   # ドローン
        root = nearest(45, [r], 40, 51)
        s.add(bass(midi_hz(root), 1.5 * s.beat, 0.6, tone=0.3), bar * 4, rev=0.1)
        s.add(bass(midi_hz(root), 1.0 * s.beat, 0.45, tone=0.3), bar * 4 + 2.5, rev=0.1)
        s.add(tom(85, 0.8), bar * 4, pan=-0.1, rev=0.25)
        s.add(tom(85, 0.5), bar * 4 + 2, pan=-0.1, rev=0.25); s.add(tom(100, 0.45), bar * 4 + 2.5, pan=0.1, rev=0.25)
        for k in range(8): s.add(shaker(0.55 if k % 2 else 0.35), bar * 4 + k * 0.5, pan=0.4, rev=0.1)
        if gusts and bar % 2 == 1:
            s.add(gust(2.5 * s.beat, 0.8, seed=200 + bar), bar * 4 + 1.5, pan=(-0.7 if bar % 4 == 1 else 0.7), rev=0.35)
        if not gusts and bar % 4 == 3:
            if accent == "chime":
                # 風の音のかわりに、風鈴をちょっとだけ（ギミックの風の音とまぎれないように、音程のある音だけ）
                for k in range(3): s.add(chime(midi_hz(nearest(88 + k * 3, cps, 84, 98)), 0.55), bar * 4 + 2.5 + k * 0.2, pan=-0.4 + 0.4 * k, rev=0.5)
            elif accent == "soft":
                # ひかえめ：やわらかいベルを1音だけ、小さく遠くで「ぽーん」
                s.add(celesta(midi_hz(nearest(84, cps, 79, 88)), 1.5 * s.beat, 0.3), bar * 4 + 2.5, pan=0.4, rev=0.6)
    save(s.render(1.6), name)

def kaze_e():
    # E＝Dの風鈴（ちりりん3つ）を、やわらかいベル1音だけ・小さめに
    kaze_c(gusts=False, name="bgm_kaze_e.wav", accent="soft")

def kaze_d():
    # D＝Cから風の音（ノイズの「ひゅうぅ」）を抜いた版。コースの風のギミックの音とまぎらわしくならないように
    kaze_c(gusts=False, name="bgm_kaze_d.wav")

if __name__ == '__main__':
    for w in (sys.argv[1:] or ['kaze_a', 'kaze_b', 'kaze_c']): globals()[w]()
