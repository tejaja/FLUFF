"""チェックポイントの音 第4弾：最初の音（2026-10-03〜11-02 に使っていた sfx_checkpoint）の音色のまま、音の並びを変える。
元の音（測った値）：B5(988Hz) → 0.12秒後に E6(1319Hz)。倍音は 2倍 -9dB・3倍 -18dB だけ。
1音目は 約80dB/秒、2音目は 約55dB/秒で減衰、2音目が 3dB 大きい。全体 0.62秒。
使い方：python make_checkpoint_sfx_v4.py <出力フォルダ>  → menu_01.wav … と一覧 menu.txt
"""
import os
import sys
import wave

import numpy as np

SR = 44100
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)

NOTE = {n: i for i, n in enumerate(["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"])}


def hz(name):
    n, o = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((NOTE[n] + 12 * (o + 1) - 69) / 12)


def tone(f, sec, decay_db=55.0, gain_db=0.0):
    tt = np.arange(int(SR * sec)) / SR
    s = np.sin(2 * np.pi * f * tt) + 10 ** (-9 / 20) * np.sin(2 * np.pi * 2 * f * tt) + 10 ** (-18 / 20) * np.sin(2 * np.pi * 3 * f * tt)
    env = 10 ** (-decay_db * tt / 20) * (1 - np.exp(-tt * 900))   # ほぼ即アタック
    return s * env * 10 ** (gain_db / 20)


def phrase(notes, length=None):
    """notes: [(開始秒, 音名, 長さ秒, 減衰dB/秒, 音量dB)]"""
    end = max(st + ln for st, _, ln, _, _ in notes)
    out = np.zeros(int(SR * (length or end)))
    for st, nm, ln, dec, g in notes:
        sig = tone(hz(nm), ln, dec, g)
        i = int(st * SR)
        n = min(len(sig), len(out) - i)
        out[i:i + n] += sig[:n]
    return out


def save(idx, name, x):
    x = np.array(x, dtype=float)
    n = int(0.02 * SR)
    x[-n:] *= np.linspace(1, 0, n)
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.7   # 元の音のピーク 0.70 にそろえる
    path = os.path.join(OUT, f"menu_{idx:02d}.wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    return f"{idx:02d} {name}"


A, B = 80.0, 55.0   # 短い音・最後の音の減衰
menu = []
D = 0.12            # 元の音の間隔
menu.append(save(1, "元の音（シ→ミ）", phrase([(0, "B5", 0.12, A, -3), (D, "E6", 0.5, B, 0)])))
menu.append(save(2, "下がる（ミ→シ）", phrase([(0, "E6", 0.12, A, -3), (D, "B5", 0.5, B, 0)])))
menu.append(save(3, "3音 ソ→シ→ミ（元の前に1音）", phrase([(0, "G5", 0.1, A, -5), (0.09, "B5", 0.1, A, -3), (0.18, "E6", 0.5, B, 0)])))
menu.append(save(4, "シ→ミ→シ（戻る）", phrase([(0, "B5", 0.12, A, -3), (D, "E6", 0.12, A, 0), (2 * D, "B5", 0.5, B, -1)])))
menu.append(save(5, "ド→ソ（5度、少し低め）", phrase([(0, "C6", 0.12, A, -3), (D, "G6", 0.5, B, 0)])))
menu.append(save(6, "ラ→ド#（長3度、やわらかめ）", phrase([(0, "A5", 0.12, A, -3), (D, "C#6", 0.5, B, 0)])))
menu.append(save(7, "シ→ミ→ミ（最後をこだま）", phrase([(0, "B5", 0.12, A, -3), (D, "E6", 0.2, A, 0), (0.3, "E6", 0.45, B, -8)])))
menu.append(save(8, "シ→ミを同時に鳴らす（和音1回）", phrase([(0, "B5", 0.5, B, -2), (0.015, "E6", 0.5, B, 0)])))
menu.append(save(9, "ミ→ラ→シ（上がって止まる）", phrase([(0, "E6", 0.09, A, -4), (0.09, "A6", 0.09, A, -2), (0.18, "B6", 0.45, B, 0)], 0.65)))
menu.append(save(10, "シ→ミ ゆっくり（間隔0.2秒）", phrase([(0, "B5", 0.2, A, -3), (0.2, "E6", 0.5, B, 0)])))

with open(os.path.join(OUT, "menu.txt"), "w", encoding="utf-8") as f:
    f.write("\n".join(menu))
print("\n".join(menu))
