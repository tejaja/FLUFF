"""チェックポイントの音 第3弾：6番（フェルトピアノ）の方向でバリエーション。"""
import os

here = os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(here, "make_checkpoint_sfx.py"), encoding="utf-8").read().split("# 01 カリンバ")[0])


def felt2(f, sec=1.3, bright=1.0, thump=0.5, decay=2.5):
    tt = t(sec)
    s = sum(a * np.sin(2 * np.pi * f * h * tt + 0.02 * h * h) * np.exp(-tt * (decay + h * 1.6 / bright)) for h, a in [(1, 1), (2, 0.35 * bright), (3, 0.12 * bright), (4, 0.05 * bright), (5, 0.02 * bright)])
    n = lowpass(rng.normal(0, 1, len(tt)), 700 + 500 * bright) * np.exp(-tt * 70) * thump
    return (s + n) * (1 - np.exp(-tt * 220))


def chord(fs, sec=1.4, spread=0.018, **kw):
    return mix(sec + spread * len(fs), *[(i * spread, felt2(f, sec, **kw) * (1 - i * 0.08)) for i, f in enumerate(fs)])


# 23 6番を完全5度に（素直で明るい）
save("23_felt_5do", mix(1.4, (0, felt2(392)), (0.13, felt2(587))))
# 24 下がる 4度（落ち着く）
save("24_felt_sagaru", mix(1.4, (0, felt2(587)), (0.15, felt2(440))))
# 25 低めであたたかい 2音
save("25_felt_hikui", mix(1.5, (0, felt2(220, 1.5, 0.7)), (0.16, felt2(330, 1.4, 0.7))))
# 26 高めでころん
save("26_felt_takai", mix(1.1, (0, felt2(659, 1.0, 1.2)), (0.1, felt2(988, 0.95, 1.2))))
# 27 和音を1回ポロン（ずらして弾く）
save("27_felt_chord", chord([262, 392, 523, 659]))
# 28 sus の和音（解決しない、ふわっと）
save("28_felt_sus", chord([294, 392, 440, 587]))
# 29 3音 ド→ソ→ミ（順番を崩す）
save("29_felt_3on", mix(1.5, (0, felt2(523)), (0.11, felt2(784, 1.2)), (0.22, felt2(659, 1.2))))
# 30 同じ音を2回、2回目オクターブ（ぽん・ぽーん）
save("30_felt_oct", mix(1.4, (0, felt2(392, 0.6)), (0.14, felt2(784, 1.2))))
# 31 2音＋鍵盤の音を強く（木の感じ）
save("31_felt_kenban", mix(1.3, (0, felt2(349, 1.2, 0.9, 1.1)), (0.14, felt2(523, 1.1, 0.9, 1.1))))
# 32 2音＋遠くのベル（少しだけキラッ）
save("32_felt_bell", mix(1.4, (0, felt2(349)), (0.14, felt2(523)), (0.14, fm(2093, 0.8, 3.0, 0.6, 7) * 0.12)))
