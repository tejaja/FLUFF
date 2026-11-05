# 2–2 の採用曲 hono_e3_long のリズム・構成ちがい（2026-11-01、てじゃ「リズムとか構成を変えて」）。音色とリフの音（E 短調のルート・短3度・5度）は同じ。声と高音は足さない
#   python make_bgm_hono_rhythm.py <出力フォルダ> [tresillo shuffle march seven call]
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import make_bgm as old
from make_bgm_hono import anvil
from fluffsynth import *

A16 = ['Em', 'Em', 'C', 'Em', 'Em', 'Em', 'Am', 'B', 'Em', 'Em', 'C', 'Em', 'Am', 'C', 'B', 'B']
B16 = ['C', 'C', 'Am', 'Am', 'C', 'C', 'B', 'B', 'Em', 'Em', 'C', 'C', 'Am', 'Am', 'B', 'B']
Q = 16.0 / 3.0   # 3 連符の幅（拍の 1/3）を拍で書くのに使う


def sec(bars, prog, level, riff=None, kicks=(), snares=(), toms=(), ticks=(), fill=0, brass=0, bell=(), pad=1.0, rumble=1.0, rev=0.0, bass_only=False, inst=None, extra=None, extra_gain=0.7):
    return dict(bars=bars, prog=prog, level=level, riff=riff, kicks=kicks, snares=snares, toms=toms, ticks=ticks, fill=fill, brass=brass, bell=bell, pad=pad, rumble=rumble, rev=rev, bass_only=bass_only, inst=inst, extra=extra, extra_gain=extra_gain)


# 出だし（太鼓だけの所）のリフをベース以外で（2026-11-05 てじゃ「序盤の小さいベースのとこ、ベースじゃないなにかではじめたい」）
INTRO_INST = 'marimba'   # None＝前のベース / 'marimba'（2026-11-05 てじゃ「マリンバいいな」）/ 'anvil' / 'brass' / 'guitar' / 'taiko'
# 盛り上がり（倍の速さ・最後の行進）でも同じ楽器をリフに重ねる（てじゃ「イントロ以外にも盛り上がりに追加したい」）。None でなし
CLIMAX_INST = 'marimba'
def intro_note(m, inst, f, dur, v, beat, g, rev):
    if inst == 'marimba':   # 低い木のマリンバ（ぽこぽこ）
        m.add(marimba3(f * 2, max(dur, 0.3), m.vel(v), hard=0.45), beat, pan=-0.1, rev=0.12 + rev, gain=1.1 * g, humanize=0.003)
    elif inst == 'anvil':   # 金属を打つ音（カーン）
        m.add(anvil(f * 4, m.vel(v * 0.9)), beat, pan=0.15, rev=0.25 + rev, gain=0.9 * g, humanize=0.003)
    elif inst == 'brass':   # 低いブラスの短い「ぶっ」
        m.add(brass2(f * 2, min(dur, 0.35), m.vel(v), bright=0.3, att=0.02, voices=2), beat, pan=0.05, rev=0.15 + rev, gain=0.75 * g, humanize=0.003)
    elif inst == 'guitar':   # ミュートしたひずみギター（ザッザッ）
        m.add(power2(f * 2, min(dur, 0.25), m.vel(v), drive=3.0, mute=0.04), beat, pan=-0.15, rev=0.08 + rev, gain=0.55 * g, humanize=0.003)
    elif inst == 'taiko':   # 音程のある太鼓（ドン、ドン）
        m.add(tom2(f * 1.0 + 30, m.vel(v), skin=0.45), beat, pan=0.0, rev=0.18 + rev, gain=0.75 * g, humanize=0.003)


# E 短調の音に寄せる（2026-11-01 てじゃ「25〜28秒あたり音程ズレてる」）。リフはどのコードでも同じ度数で弾いていて、長三和音（C、B）の上で短3度がぶつかり、E の上の 6（Bb）も音階の外だった
E_MINOR = {4, 6, 7, 9, 11, 0, 2}   # E F# G A B C D（ピッチクラス）
def fit_riff(cn, root, iv):
    if cn in ('C', 'B') and iv == 3: iv = 4
    allowed = E_MINOR | ({3} if cn == 'B' else set())
    while (root + iv) % 12 not in allowed and iv > 0: iv -= 1
    return iv


def render(path, bpm, bar_beats, sections, seed=65, hits=(), in_key=False):
    total = sum(s['bars'] for s in sections) * bar_beats
    assert abs(total / 4.0 - round(total / 4.0)) < 1e-6, total
    m = Mix(bpm, int(round(total / 4.0)), tail=3.0, seed=seed)
    bar_dur = bar_beats * m.beat
    pos = 0.0
    for s in sections:
        for b in range(s['bars']):
            cn = s['prog'][b % len(s['prog'])]
            r, cps = old.chord(cn)
            root = old.nearest(40, [r], 35, 46)
            k = b / max(1, s['bars'] - 1)
            g = s['level'](k) if callable(s['level']) else s['level']
            if s['rumble'] > 0:
                rum = root - 12 if root - 12 >= 26 else root
                m.add(sub_bass(midi_hz(rum), bar_dur + 0.05, 0.3), pos, rev=0.02, gain=(1.2 + 0.5 * g) * s['rumble'], humanize=0)
            if s['pad'] > 0:
                for q in sorted(old.nearest(62 + (x - 62) % 12, [x], 57, 69) for x in cps):
                    m.add(warm_pad(midi_hz(q), bar_dur + 0.3, 0.3, bright=0.2), pos, pan=0.12, rev=0.35, gain=(0.35 + 0.45 * g) * s['pad'], humanize=0)
            rf = s['riff'](b) if callable(s['riff']) else s['riff']
            if rf:
                for beat, iv, v, d in rf:
                    if in_key: iv = fit_riff(cn, root, iv)
                    dur = d * m.beat
                    if s['inst']:
                        intro_note(m, s['inst'], midi_hz(root + iv), dur, v, pos + beat, g, s['rev']); continue
                    if s['extra']: intro_note(m, s['extra'], midi_hz(root + iv), dur, v, pos + beat, g * s['extra_gain'], s['rev'])
                    if not s['bass_only']: m.add(power2(midi_hz(root + iv + 12), dur, m.vel(v), drive=3.0), pos + beat, pan=-0.12, rev=0.08 + s['rev'], gain=0.6 * g, humanize=0.003)
                    m.add(finger_bass(midi_hz(root + iv), dur, m.vel(v, 0.04), tone=0.5), pos + beat, rev=0.03, gain=0.5 * g, humanize=0.003)
            kicks = s['kicks'](b) if callable(s['kicks']) else s['kicks']
            for beat, v in kicks:
                m.add(kick2(m.vel(v, 0.04), punch=0.6, low=0.6), pos + beat, rev=0.02 + s['rev'], gain=0.72 * g, humanize=0.002)
            snares = s['snares'](b) if callable(s['snares']) else s['snares']
            for beat in snares:
                m.add(snare2(m.vel(0.55), tone=0.25, snap=0.35), pos + beat, pan=0.05, rev=0.16 + s['rev'], gain=0.72 * g, humanize=0.003)
            toms = s['toms'](b) if callable(s['toms']) else s['toms']
            for beat, f, v, pan in toms:
                m.add(tom2(f, m.vel(v), skin=0.6), pos + beat, pan=pan, rev=0.18 + s['rev'], gain=0.5 * g + 0.05, humanize=0.003)
            for j, beat in enumerate(s['ticks']):
                m.add(tom2(128 if j % 2 else 110, m.vel(0.26 if j % 2 else 0.34), skin=0.85), pos + beat, pan=-0.25 if j % 2 else 0.25, rev=0.1, gain=0.42 * g, humanize=0.003)
            if s['fill'] and b % s['fill'] == s['fill'] - 1 and b != s['bars'] - 1:
                for j, f in enumerate((150, 130, 110, 90)):
                    m.add(tom2(f, m.vel(0.5 + j * 0.05), skin=0.75), pos + bar_beats - 1 + j * 0.25, pan=-0.2 + j * 0.14, rev=0.14, gain=0.5 * g, humanize=0.002)
            for beat in s['bell']:
                m.add(anvil(midi_hz(old.nearest(76, [r], 71, 83)), m.vel(0.17)), pos + beat, pan=0.32, rev=0.25, gain=0.75 * g, humanize=0.003)
            if s['brass'] and b % s['brass'] == 0:
                m.add(brass2(midi_hz(root + 12), (bar_beats - 0.4) * m.beat, m.vel(0.42), bright=0.3, att=0.6), pos, pan=0.2, rev=0.35, gain=g, humanize=0.004)
            pos += bar_beats
    for beat, g in hits:
        m.add(kick2(m.vel(1.0, 0.0), punch=0.7, low=0.8), beat, rev=0.2, gain=g, humanize=0)
        m.add(tom2(60, m.vel(0.95), skin=0.5), beat, rev=0.45, gain=g * 0.8, humanize=0)
    st = master(m, rt60=1.8, rev_level=0.32, damp=0.65, lowcut=24, warmth_db=0.6, air_db=0.0, comp_thresh=-14, comp_ratio=1.4, target_rms_db=-12.0)
    save(st, path)


def R(*hits, d=0.4):
    return tuple((b, iv, v, d) for b, iv, v in hits)


def tresillo(path):
    # 3-3-3-3-2-2：全部のパートが同じ「ダッ ダッ ダッ ダッ ダ ダ」で刻む。A → 太鼓と低音だけの B → A'
    riff = R((0, 0, .8), (.75, 0, .55), (1.5, 3, .65), (2.25, 0, .55), (3, 7, .65), (3.5, 6, .6))
    kicks = ((0, .9), (.75, .6), (1.5, .75), (2.25, .6), (3, .8), (3.5, .7))
    ticks = (.25, .5, 1, 1.25, 1.75, 2, 2.5, 2.75, 3.25, 3.75)
    toms = ((0, 66, .8, -.1), (1.5, 88, .6, .1), (3, 66, .7, -.1), (3.5, 88, .6, .1))
    render(path, 100, 4, [
        sec(16, A16, .72, riff, kicks, (3,), toms, ticks, fill=4, brass=4, bell=(.5, 2.5)),
        sec(8, B16, .5, None, ((0, .9), (1.5, .75), (3, .8)), (), ((0, 74, .6, -.3), (.75, 74, .45, .3), (1.5, 88, .55, -.3), (2.25, 88, .45, .3), (3, 100, .55, -.3), (3.5, 100, .5, .3)), (), pad=1.4),
        sec(8, B16[8:], lambda k: .55 + .4 * k, riff, kicks, (3,), toms, ticks, fill=2, brass=4),
        sec(16, A16, 1.0, riff, kicks, (1, 3), toms, ticks, fill=2, brass=2, bell=(.5, 1.5, 2.5, 3.5)),
    ])


def shuffle(path):
    # 3 連符の跳ねるノリ（重いシャッフル）。B は 3 連の太鼓が転がる
    t = 1.0 / 3.0
    riff = R((0, 0, .8), (2 * t, 0, .5), (1, 0, .65), (1 + 2 * t, 3, .55), (2, 0, .65), (2 + 2 * t, 7, .6), (3, 6, .6), (3 + 2 * t, 3, .5), d=.33)
    kicks = ((0, .9), (1 + 2 * t, .6), (2, .75), (3 + 2 * t, .55))
    ticks = tuple(i + 2 * t for i in range(4)) + (1 + t, 3 + t)
    roll = tuple((i * t, 66 + (i % 3) * 22, .34 + (i % 3 == 0) * .22, -.3 + (i % 2) * .6) for i in range(12))
    render(path, 92, 4, [
        sec(14, A16, .72, riff, kicks, (1, 3), ((0, 66, .8, 0), (2, 66, .6, 0)), ticks, fill=4, brass=4, bell=(2 * t, 2 + 2 * t)),
        sec(8, B16, .5, None, ((0, .85), (2, .7)), (), roll, (), pad=1.4),
        sec(8, B16[8:], lambda k: .55 + .4 * k, riff, kicks, (1, 3), roll[::2], ticks, fill=2, brass=4),
        sec(14, A16, 1.0, riff, kicks, (1, 3), roll[::3], ticks, fill=2, brass=2, bell=(2 * t, 1 + 2 * t, 2 + 2 * t, 3 + 2 * t)),
    ])


def march(path):
    # 構成を変える：太鼓だけで始まる → ゆっくり重い行進 → 倍テンで走る → 全部止まって → 重い行進＋倍の太鼓で終わる
    slow = R((0, 0, .85), (1.5, 0, .55), (2, 3, .65), (3, 7, .6), (3.5, 6, .55), d=.7)
    fast = R((0, 0, .75), (.5, 0, .5), (1, 3, .6), (1.5, 0, .5), (2, 7, .6), (2.5, 0, .5), (3, 6, .6), (3.5, 0, .5), d=.3)
    # ベースのリズムを場所ごと・小節ごとに変える（2026-11-01 てじゃ「ベースのリズムにもうちょいパターン増やしたい」）。4 小節目は動きの多いつなぎ
    s2 = R((0, 0, .85), (1.5, 0, .55), (1.75, 0, .45), (2, 3, .65), (3, 7, .6), (3.5, 6, .55), (3.75, 3, .5), d=.6)
    s3 = R((0, 0, .85), (.75, 0, .5), (1.5, 0, .6), (2, 3, .65), (2.75, 3, .5), (3.5, 7, .6), d=.55)
    s4 = R((0, 0, .85), (1, 0, .55), (1.5, 3, .6), (2, 0, .6), (2.5, 5, .6), (3, 7, .65), (3.25, 6, .55), (3.5, 5, .55), (3.75, 3, .55), d=.35)
    f2 = R((0, 0, .75), (.5, 0, .5), (.75, 0, .45), (1, 3, .6), (1.5, 0, .5), (1.75, 0, .45), (2, 7, .6), (2.5, 0, .5), (2.75, 0, .45), (3, 6, .6), (3.5, 3, .55), d=.22)
    f3 = R((0, 0, .75), (.75, 0, .55), (1.25, 3, .55), (2, 0, .7), (2.75, 7, .55), (3.25, 6, .55), (3.75, 3, .5), d=.28)
    f4 = R((0, 0, .75), (.5, 0, .5), (1, 0, .55), (1.5, 3, .55), (2, 3, .6), (2.25, 5, .55), (2.5, 5, .55), (2.75, 7, .6), (3, 7, .6), (3.25, 6, .55), (3.5, 5, .55), (3.75, 3, .55), d=.2)
    # 4 種類を回すと「やりすぎ」だったので、基本の刻みを続けて、4 小節目だけ少し動かす。8 小節に 1 回だけ別の刻み
    s4 = R((0, 0, .85), (1.5, 0, .6), (2, 3, .65), (3, 7, .6), (3.25, 6, .5), (3.5, 5, .5), (3.75, 3, .5), d=.45)
    f4 = R((0, 0, .75), (.5, 0, .5), (1, 3, .6), (1.5, 0, .5), (2, 7, .6), (2.5, 0, .5), (3, 7, .6), (3.25, 6, .55), (3.5, 5, .55), (3.75, 3, .55), d=.25)
    intro_r = lambda b: s4 if b == 7 else slow
    march_r = lambda b: s4 if b % 4 == 3 else (s3 if b % 8 == 5 else slow)
    fast_r = lambda b: f4 if b % 4 == 3 else (f2 if b % 8 == 5 else fast)
    last_r = march_r
    end_prog = ['Em', 'Em', 'C', 'Em', 'Am', 'C', 'B', 'B']   # 最後はちゃんと B で終わらせて E へ解決（2026-11-01 てじゃ「まだ続きそうな感じで最初に戻っちゃう」）
    render(path, 100, 4, [
        sec(8, ['Em'], lambda k: .58 + .2 * k, intro_r, ((0, .95), (1.5, .6), (2.5, .8), (3.5, .55)), (), ((0, 66, .9, 0), (1, 88, .55, .3), (2, 66, .8, 0), (3, 88, .6, -.3), (3.5, 74, .6, .3)), (.5, 1.5, 2.5, 3.5), pad=.7, rumble=1., bass_only=True, inst=INTRO_INST),   # 2026-11-01 てじゃ「最初の部分がちょっと静かすぎ」：キックと刻みとパッドを足す
        sec(16, A16, .78, march_r, ((0, .95), (2.5, .8)), (2,), ((0, 66, .9, 0), (2.5, 66, .7, 0)), (1, 3), fill=8, brass=2, rev=.1),
        sec(12, B16, .85, fast_r, ((0, .85), (.75, .55), (1.5, .7), (2, .8), (2.75, .55), (3.5, .7)), (1, 3), (), (.25, .5, .75, 1.25, 1.5, 1.75, 2.25, 2.5, 2.75, 3.25, 3.5, 3.75), fill=2, brass=4, bell=(.5, 1.5, 2.5, 3.5), extra=CLIMAX_INST),
        sec(2, ['B'], lambda k: .7 + .25 * k, f4, ((0, .9), (2, .8)), (), tuple((i * .25, (66, 74, 88, 100)[i % 4], .42 + i * .02, -.3 + (i % 2) * .6) for i in range(16)), (), pad=1.3),   # 止めずに太鼓の連打でつなぐ（2026-11-01 てじゃ「止まるところ気になる」）。リフとベースも切らない（実機で「ベースがスッと消える」）
        sec(8, end_prog, 1.0, last_r, ((0, 1.0), (1.5, .7), (2.5, .85)), (2,), ((.5, 74, .6, -.3), (1, 88, .6, .3), (1.5, 74, .55, -.3), (3, 88, .65, .3), (3.5, 74, .6, -.3)), (.25, .75, 1.25, 1.75, 2.25, 2.75, 3.25, 3.75), fill=2, brass=2, bell=(1, 3), extra=CLIMAX_INST),
        # 終わり：全員で E を長く鳴らす → 余韻の上で太鼓が下りて、頭の太鼓へ
        sec(1, ['Em'], 1.0, R((0, 0, .95), d=3.8), ((0, 1.0),), (), (), (), brass=1, rev=.15),
        sec(1, ['Em'], .55, None, ((0, .7),), (), ((2, 110, .55, -.25), (2.5, 100, .55, .25), (3, 88, .6, -.25), (3.5, 74, .65, .25)), (), pad=1.1),
    ], hits=((38 * 4, 1.0), (46 * 4, 1.0)), in_key=True)   # 連打のあとと、終わりの一撃


def seven(path):
    # 7/8 拍子（1 小節 3.5 拍）。つんのめって落ち着かない感じ
    riff = R((0, 0, .85), (.75, 0, .55), (1.5, 3, .65), (2, 0, .55), (2.5, 7, .65), (3, 6, .6))
    kicks = ((0, .9), (.75, .55), (1.5, .75), (2.5, .75))
    ticks = (.25, .5, 1, 1.25, 1.75, 2.25, 2.75, 3.25)
    toms = ((0, 66, .85, 0), (1.5, 88, .6, .2), (2.5, 66, .7, -.2), (3, 88, .55, .2))
    render(path, 116, 3.5, [
        sec(32, A16, .72, riff, kicks, (1.5,), toms, ticks, fill=4, brass=4, bell=(1, 3)),
        sec(16, B16, .5, None, ((0, .85), (1.5, .7), (2.5, .7)), (), ((0, 74, .6, -.3), (.75, 88, .42, .3), (1.5, 74, .55, -.3), (2.5, 100, .5, .3), (3, 88, .45, -.3)), (), pad=1.4),
        sec(16, B16, lambda k: .55 + .4 * k, riff, kicks, (1.5,), toms, ticks, fill=2, brass=4),
        sec(32, A16, 1.0, riff, kicks, (1.5, 3), toms, ticks, fill=2, brass=2, bell=(.5, 1, 2, 3)),
    ])


def call(path):
    # 掛け合い：2 小節リフ → 2 小節太鼓だけ、が交互。だんだん間が詰まって、最後は 1 小節ごと、全部いっしょ
    riff = R((0, 0, .8), (.75, 0, .5), (1.5, 0, .6), (2, 3, .55), (2.5, 0, .5), (3, 7, .55), (3.5, 6, .5))
    kicks = ((0, .85), (1.5, .6), (2, .72), (3.5, .6))
    ticks = (.25, .5, .75, 1.25, 1.5, 1.75, 2.25, 2.5, 2.75, 3.25, 3.5, 3.75)
    answer = ((0, 66, .9, -.3), (.5, 74, .5, .3), (1, 88, .6, -.3), (1.5, 74, .5, .3), (2, 100, .65, -.3), (2.5, 88, .55, .3), (3, 110, .65, -.3), (3.25, 100, .55, .3), (3.5, 88, .6, -.3), (3.75, 74, .65, .3))
    s = []
    for n, g in ((2, .72), (2, .72), (2, .72), (2, .72), (2, .6), (2, .6), (1, .75), (1, .75), (1, .75), (1, .75), (1, .9), (1, .9), (1, .9), (1, .9)):
        s.append(sec(n, A16[::n], g, riff, kicks, (1, 3), (), ticks, brass=n * 2, bell=(.5, 2.5)))
        s.append(sec(n, A16[::n], g, None, ((0, .9), (2, .7)), (), answer, (), pad=1.2, rumble=.9))
    s.append(sec(12, A16, 1.0, riff, kicks, (1, 3), answer[::2], ticks, fill=2, brass=2, bell=(.5, 1.5, 2.5, 3.5)))
    render(path, 100, 4, s)


if __name__ == '__main__':
    out = sys.argv[1]
    if len(sys.argv) > 2 and sys.argv[2] == 'intro':   # 出だしの楽器ちがい：python make_bgm_hono_rhythm.py <出力> intro marimba anvil ...
        for inst in sys.argv[3:]:
            INTRO_INST = inst
            march(os.path.join(out, "bgm_hono_march4_" + inst + ".wav")); print("done", inst, flush=True)
        sys.exit(0)
    if len(sys.argv) > 2 and sys.argv[2] == 'march5':   # 2026-11-05 の採用版：出だしマリンバ＋盛り上がりにもマリンバ
        march(os.path.join(out, "bgm_hono_march5.wav")); sys.exit(0)
    for name in (sys.argv[2:] or ['tresillo', 'shuffle', 'march', 'seven', 'call']):
        globals()[name](os.path.join(out, "bgm_hono_r_" + name + ".wav"))
        print("done", name, flush=True)
