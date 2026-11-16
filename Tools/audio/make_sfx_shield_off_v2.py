# 2026-11-16 てじゃ「（バリアのオフ）あっさり系でちょっとかえたい」→ あっさりのまま4案
#   python make_sfx_shield_off_v2.py <出力フォルダ>
#   off_a  短く：つくの和音の上2音だけ、下がらずに軽く「ピロン」と消える 0.35s
#   off_b  抜ける：和音が少しだけ上へ抜けながら薄くなる（解放感）0.5s
#   off_c  泡：和音を短くして、最後に小さい「ぷちっ」と泡がはじける 0.45s
#   off_d  吸いこまれ：つくの音の形を逆向き（ふくらんで → すっと消える）、きらっは先に 0.45s
import sys, os
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_sfx_shockwave import write, T, ph, bell, sr, rng

E = [329.63, 415.30, 493.88, 659.25]


def glass(f, t, env, bend=1.0):
    fb = f * np.broadcast_to(bend, t.shape)
    return (np.sin(ph(fb)) + 0.5 * np.sin(ph(fb * 1.004))) * env


def off_a():
    t = T(0.35); out = np.zeros_like(t)
    for i, f in enumerate([E[3], E[2]]):
        s = i * 0.06; tt = np.clip(t - s, 0, None)
        env = np.clip((t - s) / 0.01, 0, 1) * np.exp(-tt * 11)
        out += glass(f, t, env)
    return out


def off_b():
    t = T(0.5); x = t / 0.5; out = np.zeros_like(t)
    for i, f in enumerate(E):
        s = i * 0.03; tt = np.clip(t - s, 0, None)
        env = np.clip((t - s) / 0.02, 0, 1) * np.exp(-tt * 7)
        out += glass(f, t, env, 1 + 0.08 * x)
    tb = T(0.5 - 0.12); out[int(0.12 * sr):] += 0.3 * bell(2637.0, tb, 14)
    return out


def off_c():
    t = T(0.45); out = np.zeros_like(t)
    for i, f in enumerate(E[::-1]):
        s = i * 0.03; tt = np.clip(t - s, 0, None)
        env = np.clip((t - s) / 0.01, 0, 1) * np.exp(-tt * 10)
        out += glass(f, t, env)
    # ぷちっ：短い上がる正弦（泡）
    k = int(0.2 * sr); tp = T(0.06)
    pop = np.sin(ph(900 + 2600 * (tp / 0.06) ** 0.6)) * np.exp(-tp * 60) * 0.9
    out[k:k + len(pop)] += pop
    return out


def off_d():
    t = T(0.45); x = t / 0.45; out = np.zeros_like(t)
    out[:len(T(0.45))] += 0.45 * bell(1975.5, t, 12)
    for i, f in enumerate(E):
        env = np.clip(x / 0.55, 0, 1) ** 1.5 * np.clip((1 - x) / 0.45, 0, 1) ** 2
        out += glass(f, t, env, 1 - 0.04 * x) * (0.8 + 0.2 * i / 3)
    return out


if __name__ == '__main__':
    od = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(od, exist_ok=True)
    for n, fn in [('off_a', off_a), ('off_b', off_b), ('off_c', off_c), ('off_d', off_d)]:
        write(os.path.join(od, n + '.wav'), fn(), 0.6)
    print('ok')
