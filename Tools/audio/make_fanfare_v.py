# ゴールのファンファーレ：元の fanfare()（make_bgm.py）と同じ音・雰囲気のまま、メロディ違いを作る（2026-10-07）
# 採用版：python make_fanfare_v.py orig0.88 → jingle_goal.wav（元の曲で、出だしのタタタだけギリ切れるように）
#   python make_fanfare_v.py v1 [出力パス]
import sys
import numpy as np
from make_bgm import *

def brass_tongued(f, dur, vel=1.0, att=0.015, rel=0.025):
    """元の brass と同じ音色（倍音8本）で、吹き始めを速く・切れを短くした版（タンギングで1音ずつ切る）"""
    y = additive(f, dur, [(k, 1.0 / k) for k in range(1, 9)], [None] * 8, tail=rel + 0.02, vib=0.004)
    e = env_adsr(len(y), att, 0.08, 0.75, rel, dur)
    # 頭にほんの少しアクセント（舌で突く「タッ」）
    t = np.arange(len(y)) / SR
    return y * e * (1 + 0.35 * np.exp(-t / 0.03)) * vel * 0.22

def render(mel, chords, drums, path, bpm=132, total=5.0, short=None):
    beat = 60 / bpm
    n = int(total * SR)
    L = np.zeros(n); R = np.zeros(n); rv = np.zeros(n)
    def add(y, t, pan=0.0, rev=0.25, g=1.0):
        i = int(t * SR); m = min(len(y), n - i)
        if m <= 0: return
        gl = np.sqrt(0.5 * (1 - pan)); gr = np.sqrt(0.5 * (1 + pan))
        L[i:i+m] += y[:m] * gl * g; R[i:i+m] += y[:m] * gr * g; rv[i:i+m] += y[:m] * rev * g
    for b0, d, m in mel:
        if short is not None and d < 0.5:   # 短い音（タタタ）は切って1音ずつ聞こえるように
            add(brass_tongued(midi_hz(m), d * beat * short, 1.3), b0 * beat, pan=0.1)
            add(brass_tongued(midi_hz(m + 12), d * beat * short, 0.5), b0 * beat, pan=-0.1)
        else:
            add(brass(midi_hz(m), d * beat * 0.95, 1.3), b0 * beat, pan=0.1)
            add(brass(midi_hz(m + 12), d * beat * 0.95, 0.5), b0 * beat, pan=-0.1)
        add(music_box(midi_hz(m + 12), d * beat, 0.5), b0 * beat, pan=0.3, rev=0.4)
    for b0, d, ch in chords:
        for m in ch: add(brass(midi_hz(m - 12), d * beat * 0.95, 0.55), b0 * beat, pan=-0.3)
        add(bass(midi_hz(ch[0] - 24), d * beat, 1.0, tone=0.6), b0 * beat)
    drums(add, beat)
    def conv(x, seed, rt=1.3):
        r = np.random.default_rng(seed); m = int(rt * SR); t = np.arange(m) / SR
        ir = r.standard_normal(m) * np.exp(-t / (rt / 5)); ir[:int(0.012 * SR)] = 0
        ir = np.convolve(ir, np.ones(8) / 8, mode='same'); ir /= np.sqrt(np.sum(ir ** 2))
        N = 1 << int(np.ceil(np.log2(len(x) + m)))
        return np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(ir, N), N)[:len(x)]
    L = L + 0.4 * conv(rv, 3); R = R + 0.4 * conv(rv, 4)
    st = np.stack([L, R], 1)
    fade = np.ones(n); f = int(0.4 * SR); fade[-f:] = np.linspace(1, 0, f)
    st *= fade[:, None]
    st = np.tanh(st / np.max(np.abs(st)) * 1.2) / np.tanh(1.2) * 0.85
    save(st, path)

def std_drums(hit1, hit2, fill_at):
    def d(add, beat):
        for k in range(6): add(snare(0.5 + 0.1 * k), k * beat / 6, pan=0.1, rev=0.2)
        add(kick(1.0), hit1 * beat); add(crash(1.2), hit1 * beat, pan=-0.2)
        add(kick(0.8), (hit1 + 1) * beat)
        add(kick(1.0), hit2 * beat); add(crash(1.4), hit2 * beat, pan=0.2)
        for k, f in enumerate((150, 125, 100)): add(tom(f, 0.8), fill_at * beat + k * beat / 6, pan=-0.3 + 0.3 * k)
    return d

C, F, G, Am, Bb, Ab, Em, Dm = [60, 64, 67], [60, 65, 69], [62, 67, 71], [60, 64, 69], [58, 62, 65], [56, 60, 63], [59, 64, 67], [62, 65, 69]
Cfull = [60, 64, 67, 72]

# 元のファンファーレ（make_bgm.py の fanfare と同じ）
ORIG = ([(0, 0.33, 67), (0.33, 0.33, 67), (0.66, 0.33, 67), (1.0, 1.0, 72), (2.0, 0.5, 69), (2.5, 0.5, 71), (3.0, 3.0, 72)],
        [(0, 1.0, [60, 64, 67]), (1.0, 1.0, [60, 64, 67]), (2.0, 1.0, [60, 65, 69]), (2.5, 0.5, [62, 67, 71]), (3.0, 3.0, [60, 64, 67, 72])])

def orig_drums(add, beat):
    for k in range(6): add(snare(0.5 + 0.1 * k), k * beat / 6, pan=0.1, rev=0.2)
    add(kick(1.0), 1.0 * beat); add(crash(1.2), 1.0 * beat, pan=-0.2)
    add(kick(0.8), 2.0 * beat); add(kick(1.0), 3.0 * beat); add(crash(1.4), 3.0 * beat, pan=0.2)
    for k, f in enumerate((150, 125, 100)): add(tom(f, 0.8), 2.5 * beat + k * beat / 6, pan=-0.3 + 0.3 * k)

V = {
    # v1：ソソソ・ド〜 → ミレド・ソ〜〜（最後を5度で明るく開いて終わる）
    'v1': ([(0, .33, 67), (.33, .33, 67), (.66, .33, 67), (1, 1, 72), (2, .33, 76), (2.33, .33, 74), (2.66, .33, 72), (3, 3, 79)],
           [(0, 1, C), (1, 1, C), (2, 1, F), (3, 3, Cfull)], std_drums(1, 3, 2.5)),
    # v2：ドミソ・ド〜 → シ♭〜 → ド〜〜（ゲームのクリアっぽい♭7の寄り道）
    'v2': ([(0, .33, 60), (.33, .33, 64), (.66, .33, 67), (1, 1, 72), (2, .5, 70), (2.5, .5, 74), (3, 3, 72)],
           [(0, 1, C), (1, 1, C), (2, 1, Bb), (3, 3, Cfull)], std_drums(1, 3, 2.5)),
    # v3：ソド・ミ〜ソ〜 ファミ・レ・ミ・ド〜（少し歌うフレーズ、長め）
    'v3': ([(0, .5, 67), (.5, .5, 72), (1, .5, 76), (1.5, 1, 79), (2.5, .25, 77), (2.75, .25, 76), (3, .5, 74), (3.5, .5, 76), (4, 3, 72)],
           [(0, 1.5, C), (1.5, 1, C), (2.5, .5, F), (3, 1, G), (4, 3, Cfull)], std_drums(1.5, 4, 3.5)),
    # v4：ソソソ・ミ〜 → ファファファ・ラ〜 → シ・ド〜〜（元の形を2回くり返して階段状に上がる）
    'v4': ([(0, .33, 67), (.33, .33, 67), (.66, .33, 67), (1, .67, 76), (1.67, .33, 69), (2, .33, 69), (2.33, .33, 69), (2.67, .67, 77), (3.33, .33, 79), (3.67, .33, 83), (4, 3, 84)],
           [(0, 1.67, C), (1.67, 1.33, F), (3, 1, G), (4, 3, Cfull)], std_drums(1, 4, 3.5)),
}

if __name__ == '__main__':
    k = sys.argv[1]
    if k.startswith('orig'):   # orig / orig0.6 など：元の曲で、タタタの長さだけ短く
        sh = float(k[4:]) if len(k) > 4 else None
        render(ORIG[0], ORIG[1], orig_drums, sys.argv[2] if len(sys.argv) > 2 else os.path.join(OUT, "jingle_goal.wav"), short=sh)
        sys.exit()
    mel, ch, dr = V[k]
    total = 5.0 + (1.0 if k in ('v3', 'v4') else 0.0)
    bpm = 132 if k not in ('v3', 'v4') else 144
    render(mel, ch, dr, sys.argv[2] if len(sys.argv) > 2 else os.path.join(OUT, f"jingle_goal_{k}.wav"), bpm=bpm, total=total)
