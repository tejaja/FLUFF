# ポータルの音の「メニュー」：性格のちがう候補を1つのファイルに順番に並べて、何番目が近いか聞く用
#   python make_portal_menu.py <出力フォルダ>
#   enter_menu.wav：乗った瞬間 1〜5（1秒おき）／ charge_menu.wav：たまる音 1〜5（2秒おき）
import numpy as np, wave, sys, os
sr = 44100
rng = np.random.default_rng(3)

def norm(x): return x / (np.max(np.abs(x)) + 1e-9) * 0.6
def tt(T): return np.arange(int(T * sr)) / sr
def sweep(f, amp, harm=(1, 0.3)):
    ph = 2 * np.pi * np.cumsum(f) / sr
    return amp * sum(a * np.sin((k + 1) * ph) for k, a in enumerate(harm))
def fade(x, a=0.005, r=0.03):
    x = x.copy(); x[:int(a * sr)] *= np.linspace(0, 1, int(a * sr)); x[-int(r * sr):] *= np.linspace(1, 0, int(r * sr)); return x

# ---- 乗った瞬間 ----
def e1():   # 澄んだシュイン（短く上がる）
    t = tt(0.35); f = 400 * 3 ** np.clip(t / 0.1, 0, 1); env = np.where(t < 0.1, 1, np.exp(-(t - 0.1) * 12))
    return fade(sweep(f, env))
def e2():   # 電子音っぽいシュイン（矩形波っぽい、ピュイーン）
    t = tt(0.35); f = 300 * 4 ** np.clip(t / 0.15, 0, 1); env = np.where(t < 0.15, 1, np.exp(-(t - 0.15) * 10))
    return fade(sweep(f, env, harm=(1, 0, 0.33, 0, 0.2)))
def e3():   # かわいい「ぴょいん」（上がって少し戻る）
    t = tt(0.35); f = 600 * (1 + 1.0 * np.sin(np.pi * np.clip(t / 0.18, 0, 1))); env = np.exp(-t * 8)
    return fade(sweep(f, env, harm=(1, 0.2)))
def e4():   # 低めの「ふぉん」（丸く、どっしり）
    t = tt(0.4); f = 180 * 2 ** np.clip(t / 0.12, 0, 1); env = np.minimum(1, t / 0.02) * np.exp(-t * 6)
    return fade(sweep(f, env, harm=(1, 0.5, 0.2)))
def e5():   # キラキラ「シャラン」（ベルの速いアルペジオ）
    out = np.zeros(int(0.5 * sr))
    for i, f in enumerate([783.99, 987.77, 1174.66, 1567.98]):
        t = tt(0.4); s = np.sin(2 * np.pi * f * t) * np.exp(-t * 9); k = int(i * 0.03 * sr); out[k:k + len(s)] += s[:len(out) - k]
    return fade(out)

# ---- たまる音（1.5秒） ----
def c1():   # ブォン…ブォン（低い脈）
    t = tt(1.5); f = 110 * 2 ** (0.5 * t / 1.5); puls = 0.5 + 0.5 * np.sin(2 * np.pi * (2 + 2 * t / 1.5) * t - np.pi / 2)
    return fade(sweep(f, puls * (0.5 + 0.5 * t / 1.5), harm=(1, 0.6, 0.3)), 0.05, 0.05)
def c2():   # ふわ〜ん（ずっとゆれながら上がる、途切れない）
    t = tt(1.5); f = 262 * 2 ** (1.0 * t / 1.5) * (1 + 0.03 * np.sin(2 * np.pi * 5 * t))
    return fade(sweep(f, np.minimum(1, t / 0.3) * (0.4 + 0.6 * t / 1.5), harm=(1, 0.35)), 0.05, 0.05)
def c3():   # ういーん（機械っぽく上がる、ワープ装置）
    t = tt(1.5); f = 150 * 4 ** (t / 1.5) ** 1.5
    return fade(sweep(f, np.minimum(1, t / 0.2) * (0.4 + 0.6 * t / 1.5), harm=(1, 0, 0.33, 0, 0.2)), 0.05, 0.05)
def c4():   # ぽこぽこ（泡がだんだん速く）
    out = np.zeros(int(1.5 * sr)); at = 0.0
    while at < 1.45:
        p = at / 1.5; t = tt(0.12); f = (300 + 500 * p) * (1 + 0.4 * (1 - np.exp(-t * 40)))
        s = sweep(f, np.exp(-t * 25), harm=(1,)); k = int(at * sr); out[k:k + len(s)] += s[:len(out) - k] * (0.5 + 0.5 * p)
        at += 0.14 - 0.09 * p
    return fade(out, 0.002, 0.04)
def c5():   # しゃらら〜（細かいベルが鳴り続けて上へ）
    out = np.zeros(int(1.5 * sr)); at = 0.0
    sc = [392, 440, 493.88, 587.33, 659.25, 783.99, 880, 987.77, 1174.66, 1318.5]
    while at < 1.45:
        p = at / 1.5; f = sc[min(9, int(p * 9) + rng.integers(0, 2))]; t = tt(0.3)
        s = np.sin(2 * np.pi * f * t) * np.exp(-t * 14); k = int(at * sr); out[k:k + len(s)] += s[:len(out) - k] * 0.6
        at += 0.06
    return fade(out, 0.002, 0.04)

def menu(items, gap):
    parts = []
    for it in items:
        x = norm(it()); pad = np.zeros(int(gap * sr) - len(x)) if len(x) < int(gap * sr) else np.zeros(int(0.3 * sr))
        parts += [x, pad]
    return np.concatenate(parts)

def save(p, x):
    with wave.open(p, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes((x * 32767).astype(np.int16).tobytes())

if __name__ == '__main__':
    d = sys.argv[1]; os.makedirs(d, exist_ok=True)
    save(os.path.join(d, 'enter_menu.wav'), menu([e1, e2, e3, e4, e5], 1.0))
    save(os.path.join(d, 'charge_menu.wav'), menu([c1, c2, c3, c4, c5], 2.2))
    print('ok')
