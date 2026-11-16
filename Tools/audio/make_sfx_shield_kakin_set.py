# 2026-11-16 てじゃ「いまおもうとつく時もカキン系がいいかもな」→ 床で跳ねるカキン（make_sfx_shockwave.kakin）と同じ音色で、つく・消えるを作る
#   python make_sfx_shield_kakin_set.py <出力フォルダ>
#   on_k1   カキン2つで上がる（ド→ソ）＋うすいきらきら 0.6s
#   on_k2   上がるガラスのシュイン → 最後にカキン 0.6s
#   off_k1  やわらかいカキン2つで下がる（ソ→ド）、カッはなし 0.45s
#   off_k2  こもったカキン1つ、響きが早く消える 0.4s
import sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_sfx_shockwave import write, T, ph, lp, bell, sr, rng


def kakin_f(f=2093.0, sec=0.42, tick=0.9, ringDecay=1.0, body=0.35):
    """kakin() と同じ作り。f＝高い響きの音程、tick＝カッの強さ、ringDecay＞1 で響きが早く消える"""
    t = T(sec)
    n = rng.standard_normal(len(t))
    tk = (n - lp(n, 0.4)) * np.exp(-t * 160) * tick
    ring = np.zeros_like(t)
    for h, a, d in [(1, 1.0, 11), (2.41, 0.5, 15), (3.87, 0.3, 20), (5.3, 0.15, 26)]:
        ring += a * np.sin(2 * np.pi * f * h * t + h) * np.exp(-t * d * ringDecay)
    bd = np.sin(2 * np.pi * (f / 4) * t) * np.exp(-t * 18) * body
    return tk + ring * 0.55 * np.minimum(1, t / 0.0015) + bd


def place(out, x, at):
    i = int(at * sr); n = min(len(x), len(out) - i); out[i:i + n] += x[:n]


def on_k1():
    out = np.zeros(int(0.6 * sr))
    place(out, kakin_f(2093.0, 0.42, 0.7), 0.0)
    place(out, kakin_f(3136.0, 0.45, 0.6), 0.09)
    tb = T(0.45); place(out, 0.25 * bell(4186.0, tb, 10) + 0.15 * bell(5274.0, tb, 12), 0.15)
    return out


def on_k2():
    out = np.zeros(int(0.6 * sr))
    t = T(0.22); x = t / 0.22
    sweep = (np.sin(ph(1200 + 1900 * x ** 1.5)) + 0.4 * np.sin(ph((1200 + 1900 * x ** 1.5) * 2.01))) * x ** 2 * 0.5
    place(out, sweep, 0.0)
    place(out, kakin_f(3136.0, 0.42, 0.8), 0.2)
    return out


def off_k1():
    out = np.zeros(int(0.45 * sr))
    place(out, kakin_f(3136.0, 0.35, 0.0, 1.3, 0.2) * 0.8, 0.0)
    place(out, kakin_f(2093.0, 0.38, 0.0, 1.5, 0.25) * 0.7, 0.08)
    return out


def off_k2():
    x = kakin_f(2093.0, 0.4, 0.15, 1.8, 0.4)
    return lp(x, 0.35)


if __name__ == '__main__':
    od = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(od, exist_ok=True)
    for n, fn in [('on_k1', on_k1), ('on_k2', on_k2), ('off_k1', off_k1), ('off_k2', off_k2)]:
        write(os.path.join(od, n + '.wav'), fn(), 0.65)
    print('ok')
