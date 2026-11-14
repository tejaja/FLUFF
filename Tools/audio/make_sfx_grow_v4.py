# 2–5：大きくなる音 その4（2026-11-14 てじゃ「8が近いけどもうちょい」→ v3 の 08「低いぽむっ＋上にぽわ」のバリエーション）
#   python make_sfx_grow_v4.py <出力フォルダ>
import importlib.util, os, sys
import numpy as np
spec = importlib.util.spec_from_file_location('v3', os.path.join(os.path.dirname(os.path.abspath(__file__)), 'make_sfx_grow_v3.py'))
v3 = importlib.util.module_from_spec(spec); spec.loader.exec_module(v3)
sr = v3.sr; lp, tri, pulse, phase, mtof, env, place, glide, bp = v3.lp, v3.tri, v3.pulse, v3.phase, v3.mtof, v3.env, v3.place, v3.glide, v3.bp
rng = np.random.default_rng(4114)


def pom(n, m, drop=0.5, dec=18, sp=40):
    t = np.arange(n) / sr
    return tri(phase(mtof(m) * (1 + drop * np.exp(-t * sp)))) * env(n, 0.003, dec)


def pwa(n, m, amt=5, cut=1100, dec=9, duty=0.5, vib=0.0):
    return lp(pulse(phase(glide(n, m, amt, 30, vib)), duty), cut) * env(n, 0.02, dec)


def make(steps, lowOff=-24, hiGain=0.6, pomKw=None, pwaKw=None, lens=(0.17, 0.17, 0.26), extra=None):
    pomKw = pomKw or {}; pwaKw = pwaKw or {}
    out = np.zeros(int(v3.DUR * sr))
    for k, at in enumerate(v3.PUMPS):
        n = int(lens[k] * sr); last = k == 2
        lo = pom(n, steps[k] + lowOff, **pomKw)
        kw = dict(pwaKw); kw.setdefault('dec', 9 if not last else 6)
        hi = pwa(n, steps[k], **kw) * hiGain
        place(out, lo + hi, at)
    if extra: extra(out)
    return out


S = [67, 72, 76]
VARS = [
    # 01 8 のまま「ぽむ」を太く（ぽむの音程の落ち幅・長さを増やす）
    lambda: make(S, pomKw=dict(drop=0.8, dec=11)),
    # 02 「ぽわ」をもっとまるく（矩形のこもりを強く）、ぽむは同じ
    lambda: make(S, pwaKw=dict(cut=700)),
    # 03 「ぽわ」のしゃくりを大きく（下からぐいっと）
    lambda: make(S, pwaKw=dict(amt=9)),
    # 04 全体を4度下げて、ゆったり重めに
    lambda: make([s - 5 for s in S], hiGain=0.7),
    # 05 全体を3度上げて、かわいく軽く
    lambda: make([s + 4 for s in S], hiGain=0.5, pomKw=dict(dec=24)),
    # 06 音を ド→ミ→ソ に（明るい3つ上がり）
    lambda: make([60, 64, 67], lowOff=-12, hiGain=0.65),
    # 07 最後の「ぽわ〜ん」だけゆらゆら（ビブラート）＋少し長く
    lambda: make(S, pwaKw=dict(vib=0.0), lens=(0.17, 0.17, 0.3), extra=lambda o: place(o, pwa(int(0.22 * sr), S[2], 2, 1000, 6, 0.5, 0.025) * 0.35, v3.PUMPS[2] + 0.06)),
    # 08 「ぽわ」の矩形を細め（25%）にして少し明るく、でもこもらせる
    lambda: make(S, pwaKw=dict(duty=0.25, cut=1300)),
    # 09 ぽむを1オクターブ上に（ぽむ＋ぽわが近い高さで、ころっとまとまる）
    lambda: make(S, lowOff=-12, hiGain=0.5),
    # 10 ぽむのあとに小さい泡のぽこっを添える
    lambda: make(S, extra=lambda o: [place(o, tri(phase(mtof(S[k] + 7) * (1 + 1.0 * (1 - np.exp(-np.arange(int(0.07 * sr)) / sr * 40))))) * env(int(0.07 * sr), 0.003, 30) * 0.3, v3.PUMPS[k] + 0.06) for k in range(3)]),
]

if __name__ == '__main__':
    outdir = sys.argv[1] if len(sys.argv) > 1 else '.'
    os.makedirs(outdir, exist_ok=True)
    for i, fn in enumerate(VARS, 1):
        v3.save(os.path.join(outdir, 'growP8_%02d.wav' % i), fn())
    print('ok')


# ---- 2026-11-14 てじゃ「音程かえたい」：8 の音色のまま、3段の音の並びちがい ----
MELODIES = [
    ('do_do_so', [60, 60, 67]),      # 01 ド・ド・ソ
    ('so_do_so', [55, 60, 67]),      # 02 ソ・ド・ソ
    ('do_so_DO', [60, 67, 72]),      # 03 ド・ソ・高いド
    ('mi_so_DO', [64, 67, 72]),      # 04 ミ・ソ・高いド
    ('do_re_mi', [60, 62, 64]),      # 05 ド・レ・ミ
    ('octaves', [48, 60, 72]),       # 06 低いド・ド・高いド
    ('chromatic', [60, 61, 62]),     # 07 半音ずつ ド・ド#・レ
    ('la_do_mi', [57, 60, 64]),      # 08 ラ・ド・ミ（ちょっとふしぎ）
    ('so_mi_DO', [67, 64, 72]),      # 09 ソ・ミ・高いド（いったん下がって跳ねる）
    ('fa_la_DO', [65, 69, 72]),      # 10 ファ・ラ・高いド
]


def melodies(outdir):
    os.makedirs(outdir, exist_ok=True)
    for i, (nm, st) in enumerate(MELODIES, 1):
        v3.save(os.path.join(outdir, 'mel_%02d_%s.wav' % (i, nm)), make(st))


# ---- 2026-11-14 てじゃ「2かな」→ 大きくなる＝ソ・ド・ソ（mel_02）。小さくなる・戻るの候補 ----
GROW = [55, 60, 67]
SHRINK_SET = [
    ('shrinkA_rev', lambda: make([67, 60, 55])),                                  # 大きくなるの逆（ソ・ド・低いソ）
    ('shrinkB_rev_down', lambda: make([67, 60, 55], pwaKw=dict(amt=-5))),          # 逆＋上から「しゅぽっ」と下がる
    ('shrinkC_high', lambda: make([79, 72, 67], hiGain=0.5, pomKw=dict(dec=26))),  # 1オクターブ高い所で下がる（小さくてかるい）
]
NORMAL_SET = [
    ('normalA_do_so_do', lambda: make([60, 55, 60])),    # ド・ソ・ド（まん中へ戻る）
    ('normalB_so_so_do', lambda: make([55, 55, 60])),    # ソ・ソ・ド（すとんと落ちつく）
    ('normalC_mi_re_do', lambda: make([64, 62, 60])),    # ミ・レ・ド
]


def final_set(outdir):
    os.makedirs(outdir, exist_ok=True)
    v3.save(os.path.join(outdir, 'grow.wav'), make(GROW))
    for nm, fn in SHRINK_SET + NORMAL_SET: v3.save(os.path.join(outdir, nm + '.wav'), fn())
