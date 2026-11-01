# 2–2 の採用曲 hono_e3_long（bgm_hono_long2）のアレンジ候補（2026-11-01、てじゃ「今の曲をベースにして、すこしアレンジして何パターンか」）。
#   python make_bgm_hono_arr.py <出力フォルダ> [half rush riff metal drop]
# 作曲・音色・構成（A 16 小節 → B 16 小節 → A' 16 小節）は元のまま。声は入れない、高音も足さない（どちらも不採用だった）
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import make_bgm as old
from make_bgm_hono import anvil
from fluffsynth import *

RIFF2 = ((0, 0, 0.75), (0.5, 0, 0.5), (1, 3, 0.6), (1.5, 0, 0.5), (2, 5, 0.6), (2.75, 3, 0.5), (3.25, 0, 0.5), (3.5, 7, 0.6))


def arr(path, v):
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

    # drop：大きいブレイク。ここの音は全部止める（ドラムの一撃は下で足す）
    def muted(pos):
        if v != 'drop': return False
        return 14 * 4 + 2 <= pos < 16 * 4 or 30 * 4 + 2 <= pos < 32 * 4 or 40 * 4 + 3 <= pos < 41 * 4

    def add(sound, pos, **kw):
        if not muted(pos): m.add(sound, pos, **kw)

    for bar, cn in enumerate(prog):
        r, cps = old.chord(cn)
        root = old.nearest(40, [r], 35, 46)
        quiet = 16 <= bar < 20
        last = bar >= 32
        g = level(bar)
        half = v == 'half' and 16 <= bar < 32   # half：B をゆったり重いノリに
        strip = v == 'drop' and 28 <= bar < 30  # drop：B の終わりは太鼓だけ
        rumble = root - 12 if root - 12 >= 26 else root
        add(sub_bass(midi_hz(rumble), 4 * m.beat + 0.05, 0.3), bar * 4, rev=0.02, gain=1.2 + 0.5 * g, humanize=0)
        for q in sorted(old.nearest(62 + (x - 62) % 12, [x], 57, 69) for x in cps):
            add(warm_pad(midi_hz(q), 4 * m.beat + 0.3, 0.3, bright=0.2), bar * 4, pan=0.12, rev=0.35, gain=0.35 + 0.45 * g, humanize=0)

        rf = thin if quiet else riff
        if v == 'riff' and (8 <= bar < 16 or 40 <= bar < 48): rf = RIFF2   # riff：後半だけリフを動かす
        if not strip:
            for beat, iv, vv in rf:
                d = (0.6 if quiet else 0.4) * m.beat
                add(power2(midi_hz(root + iv + 12), d, m.vel(vv), drive=3.0), bar * 4 + beat, pan=-0.12, rev=0.08, gain=0.6 * g, humanize=0.003)
                add(finger_bass(midi_hz(root + iv), d, m.vel(vv, 0.04), tone=0.5), bar * 4 + beat, rev=0.03, gain=0.5 * g, humanize=0.003)

        drums = ((0, 66, 0.85), (2, 66, 0.6)) if quiet else ((0, 66, 0.85), (1.5, 66, 0.6), (2, 88, 0.6), (3, 66, 0.7), (3.5, 88, 0.5))
        if half and not quiet: drums = ((0, 66, 0.95), (2.5, 66, 0.75))
        for beat, f, vv in drums:
            add(tom2(f, m.vel(vv), skin=0.6), bar * 4 + beat, pan=(beat - 2) * 0.08, rev=0.18, gain=0.5 * g + 0.05, humanize=0.003)
        if quiet:
            for beat in (1, 3):
                add(tom2(100, m.vel(0.4), skin=0.7), bar * 4 + beat, pan=0.15, rev=0.2, gain=0.42, humanize=0.003)
            for k, (beat, f0) in enumerate(((0.5, 74), (1.5, 88), (2.5, 74), (3.25, 88), (3.5, 74), (3.75, 100))):
                add(tom2(f0, m.vel(0.42), skin=0.6), bar * 4 + beat, pan=-0.4 + k * 0.16, rev=0.55, gain=0.4, humanize=0.004)
        else:
            kicks = ((0, 0.85), (1.5, 0.6), (2, 0.72), (3.5, 0.6))
            snares = (1, 3)
            if half: kicks, snares = ((0, 0.95), (2.5, 0.8)), (2,)
            for beat, vv in kicks:
                add(kick2(m.vel(vv, 0.04), punch=0.6, low=0.6), bar * 4 + beat, rev=0.02, gain=0.72 * g, humanize=0.002)
            for beat in snares:
                add(snare2(m.vel(0.55), tone=0.25, snap=0.35), bar * 4 + beat, pan=0.05, rev=0.3 if half else 0.16, gain=0.72 * g, humanize=0.003)
            for k in range(16):
                if k % 4 == 0: continue
                if half and k % 2: continue
                add(tom2(128 if k % 2 else 110, m.vel(0.26 if k % 2 else 0.34), skin=0.85), bar * 4 + k * 0.25, pan=-0.25 if k % 2 else 0.25, rev=0.1, gain=0.42 * g, humanize=0.003)
            fill = bar % 2 == 1 and bar not in (31, 47)
            if half: fill = bar in (23, 27)
            if v == 'rush' and bar >= 40: fill = bar != 47
            if fill:
                for j, f in enumerate((150, 130, 110, 90)):
                    add(tom2(f, m.vel(0.5 + j * 0.05), skin=0.75), bar * 4 + 3 + j * 0.25, pan=-0.2 + j * 0.14, rev=0.14, gain=0.5 * g, humanize=0.002)

        # rush：B の後半から太鼓の連打でだんだん詰めて、A' の後半は低い太鼓を 8 分で足す
        if v == 'rush':
            if 24 <= bar < 32:
                n = 2 if bar < 28 else 4
                for k in range(n * 2):
                    add(tom2(74, m.vel(0.34 + 0.02 * (bar - 24)), skin=0.6), bar * 4 + k * (2.0 / n), pan=0.3 if k % 2 else -0.3, rev=0.2, gain=0.42 * g, humanize=0.003)
            if bar >= 40:
                for k in range(8):
                    add(tom2(66 if k % 2 == 0 else 80, m.vel(0.5 if k % 2 == 0 else 0.38), skin=0.55), bar * 4 + k * 0.5, pan=0.28 if k % 2 else -0.28, rev=0.16, gain=0.5, humanize=0.003)

        # 金属の刻み（metal は刻みを減らして、低い鐘を大きく鳴らす）
        if v == 'metal':
            if not quiet and bar % 2 == 0:
                add(anvil(midi_hz(old.nearest(64, [r], 59, 71)), m.vel(0.42)), bar * 4, pan=0.2, rev=0.6, gain=1.1 * g, humanize=0.002)
            if bar % 8 == 6 and not quiet:
                for j, iv in enumerate((12, 10, 7)):
                    add(anvil(midi_hz(old.nearest(64, [r], 59, 71) + iv - 12), m.vel(0.34)), bar * 4 + 2 + j * 0.5, pan=-0.2, rev=0.5, gain=0.95 * g, humanize=0.002)
            if not quiet:
                for k in (1, 3):
                    add(anvil(midi_hz(old.nearest(76, [r], 71, 83)), m.vel(0.12)), bar * 4 + k + 0.5, pan=0.32, rev=0.25, gain=0.7 * g, humanize=0.003)
        elif not quiet:
            for k in range(4):
                add(anvil(midi_hz(old.nearest(76, [r], 71, 83)), m.vel(0.2 if k % 2 == 0 else 0.13)), bar * 4 + k + 0.5, pan=0.32, rev=0.25, gain=0.75 * g, humanize=0.003)
        elif bar % 2 == 1:
            add(anvil(midi_hz(old.nearest(76, [r], 71, 83)), m.vel(0.24)), bar * 4 + 3, pan=0.32, rev=0.5, gain=0.7, humanize=0.002)

        if bar % 4 == 0 or (last and bar % 2 == 0) or (half and bar % 2 == 0):
            add(brass2(midi_hz(root + 12), 3.6 * m.beat, m.vel(0.42), bright=0.3, att=0.6), bar * 4, pan=0.2, rev=0.35, gain=g, humanize=0.004)
            if last: add(brass2(midi_hz(root + 19), 3.6 * m.beat, m.vel(0.34), bright=0.3, att=0.6), bar * 4, pan=-0.2, rev=0.35, humanize=0.004)
        if bar in (31, 47) and v != 'drop':
            for j, f in enumerate((130, 130, 110, 110, 90, 90, 74, 74)):
                add(tom2(f, m.vel(0.42 + j * 0.03), skin=0.6), bar * 4 + 2 + j * 0.25, pan=-0.3 + j * 0.08, rev=0.16, gain=0.42 if bar == 47 else 0.34, humanize=0.002)

    # drop：止まったあとに低い一撃を入れる
    if v == 'drop':
        for bar in (16, 32, 41):
            m.add(kick2(m.vel(1.0, 0.0), punch=0.7, low=0.8), bar * 4, rev=0.2, gain=1.0, humanize=0)
            m.add(tom2(60, m.vel(0.95), skin=0.5), bar * 4, rev=0.45, gain=0.8, humanize=0)
        for j, f in enumerate((130, 110, 90, 74)):   # 止まる直前の太鼓
            m.add(tom2(f, m.vel(0.6 + j * 0.05), skin=0.6), 31 * 4 + 1 + j * 0.25, pan=-0.2 + j * 0.13, rev=0.25, gain=0.5, humanize=0.002)

    st = master(m, rt60=1.8, rev_level=0.32, damp=0.65, lowcut=24, warmth_db=0.6, air_db=0.0, comp_thresh=-14, comp_ratio=1.4, target_rms_db=-12.0)
    save(st, path)


if __name__ == '__main__':
    out = sys.argv[1]
    for v in (sys.argv[2:] or ['half', 'rush', 'riff', 'metal', 'drop']):
        arr(os.path.join(out, "bgm_hono_arr_" + v + ".wav"), v)
        print("done", v, flush=True)
