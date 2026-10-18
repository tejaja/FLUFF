# ステージ2「みずのたに」の水の効果音（2026-10-17）
#   python make_water_sfx.py            → sfx_water_plop.wav（ふわふわが水に落ちた「ちゃぽん」）
# 出力は Assets/FuwaStage2/Audio
import os, sys, wave
import numpy as np

SR = 44100
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaStage2\Audio"

def save(y, name):
    os.makedirs(OUT, exist_ok=True)
    y = y / (np.max(np.abs(y)) + 1e-9) * 0.85
    st = np.stack([y, y], 1)
    p = os.path.join(OUT, name)
    with wave.open(p, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(st, -1, 1) * 32767).astype(np.int16).tobytes())
    print(name, f"{len(y)/SR:.2f}s")

def lp(x, a):
    y = np.zeros_like(x); s = 0.0
    for i in range(len(x)): s += a * (x[i] - s); y[i] = s
    return y

def plop(seed=3):
    """ちゃぽん：水面を割る小さな「ぱしゃ」（こもったノイズ、短い）＋泡がはじける「ぽん」（上がっていく正弦波）＋小さいしずく2〜3粒"""
    r = np.random.default_rng(seed)
    n = int(0.6 * SR); t = np.arange(n) / SR
    y = np.zeros(n)
    # ぱしゃ：やわらかいノイズを 40ms で
    nz = lp(r.standard_normal(n), 0.25) - lp(r.standard_normal(n), 0.02)
    y += nz * np.exp(-t / 0.035) * 0.5
    # ぽん：泡の共鳴。450Hz → 900Hz へ上がりながら 70ms で消える（少し遅れて）
    d = 0.012; tt = np.clip(t - d, 0, None)
    f = 450 + 520 * (1 - np.exp(-tt / 0.05))
    ph = 2 * np.pi * np.cumsum(f) / SR
    y += np.sin(ph) * np.exp(-tt / 0.06) * (t > d) * 0.9
    # しずく：高い「ぴちょ」を小さく
    for k in range(3):
        st = 0.09 + 0.07 * k + r.uniform(0, 0.03)
        tk = np.clip(t - st, 0, None)
        fk = r.uniform(1300, 2100) * (1 + 0.6 * (1 - np.exp(-tk / 0.012)))
        phk = 2 * np.pi * np.cumsum(fk) / SR
        y += np.sin(phk) * np.exp(-tk / 0.025) * (t > st) * (0.22 - 0.05 * k)
    # 終わりをなめらかに
    y *= np.minimum(1, (n - np.arange(n)) / (0.05 * SR))
    return y

def waterfall_loop(seconds=6.0, seed=5):
    """滝の音（ループ）：低い「ごぉー」と、高めの「しゃー」。つなぎ目がないように最後と頭を重ねる"""
    r = np.random.default_rng(seed)
    n = int(seconds * SR); fade = int(0.5 * SR)
    nz = r.standard_normal(n + fade)
    low = lp(nz, 0.02); low = low / (np.max(np.abs(low)) + 1e-9)
    hi = nz - lp(nz, 0.35); hi = lp(hi, 0.6); hi = hi / (np.max(np.abs(hi)) + 1e-9)
    t = np.arange(n + fade) / SR
    y = low * 0.8 + hi * 0.35 * (0.85 + 0.15 * np.sin(2 * np.pi * 0.7 * t))
    # ループ：最後の fade ぶんを頭に重ねる
    head = y[:fade] * np.linspace(0, 1, fade) + y[n:n + fade] * np.linspace(1, 0, fade)
    out = np.concatenate([head, y[fade:n]])
    return out

def bubble_pop(seed=8):
    """泡が割れる「ぱちん」：ごく短い明るいクリック＋高い「ぴゅ」の下がる音"""
    r = np.random.default_rng(seed)
    n = int(0.25 * SR); t = np.arange(n) / SR
    click = (r.standard_normal(n) - lp(r.standard_normal(n), 0.3)) * np.exp(-t / 0.004)
    f = 1800 * np.exp(-t / 0.05) + 700
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.04) * 0.6
    y = click * 0.8 + tone
    y *= np.minimum(1, (n - np.arange(n)) / (0.03 * SR))
    return y

def bubble_wrap(seed=9):
    """泡に包まれた「ぽわん」：やわらかく上がっていく音"""
    n = int(0.45 * SR); t = np.arange(n) / SR
    f = 380 + 420 * (1 - np.exp(-t / 0.12))
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.minimum(1, t / 0.03) * np.exp(-t / 0.18)
    y += 0.4 * np.sin(2 * np.pi * np.cumsum(f * 2.01) / SR) * np.minimum(1, t / 0.03) * np.exp(-t / 0.1)
    y *= np.minimum(1, (n - np.arange(n)) / (0.05 * SR))
    return y

if __name__ == '__main__':
    save(plop(), "sfx_water_plop.wav")
    save(waterfall_loop(), "sfx_waterfall_loop.wav")
    save(bubble_pop(), "sfx_bubble_pop.wav")
    save(bubble_wrap(), "sfx_bubble_wrap.wav")
