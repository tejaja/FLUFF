# FLUFF のBGMを5曲合成する（numpyだけ）。
# 各曲はループ再生前提：曲の長さ＋余韻を描いてから、余韻を頭に重ねて切る（つなぎ目なし）。
import numpy as np, wave, os, sys

SR = 32000
OUT = r"D:\UnityProject\FLUFF\Assets\FuwaCourse\Audio\BGM"
os.makedirs(OUT, exist_ok=True)

def midi_hz(m): return 440.0 * 2 ** ((m - 69) / 12)

# ---------- 楽器 ----------
def env_adsr(n, a, d, s, r, hold):
    """n: 全長サンプル, hold: 鍵盤を押してる長さ(秒)"""
    t = np.arange(n) / SR
    e = np.where(t < a, t / max(a, 1e-4), 1.0)
    dd = np.clip((t - a) / max(d, 1e-4), 0, 1)
    e = np.where(t >= a, 1 - (1 - s) * dd, e)
    rel = np.clip((t - hold) / max(r, 1e-4), 0, 1)
    return e * (1 - rel)

def additive(f, dur, partials, decays, tail=0.0, vib=0.0, vib_rate=5.5):
    n = int((dur + tail) * SR)
    t = np.arange(n) / SR
    ph = 2 * np.pi * f * t
    if vib > 0:
        ph = ph + (vib * f / vib_rate) * np.sin(2 * np.pi * vib_rate * t) * np.clip(t / 0.25, 0, 1)
    y = np.zeros(n)
    for (mul, amp), dec in zip(partials, decays):
        if f * mul > SR * 0.45: continue
        y += amp * np.sin(ph * mul) * (np.exp(-t / dec) if dec else 1.0)
    return y

def music_box(f, dur, vel=1.0):
    y = additive(f, dur, [(1, 1.0), (2.0, 0.35), (3.01, 0.18), (5.2, 0.07)], [1.4, 0.6, 0.35, 0.15], tail=1.2)
    n = len(y); t = np.arange(n) / SR
    y *= np.clip(t / 0.003, 0, 1)
    return y * vel * 0.5

def marimba(f, dur, vel=1.0):
    y = additive(f, dur, [(1, 1.0), (4.0, 0.25), (9.9, 0.06)], [0.45, 0.08, 0.03], tail=0.6)
    t = np.arange(len(y)) / SR
    y *= np.clip(t / 0.002, 0, 1)
    return y * vel * 0.55

def pluck(f, dur, vel=1.0, bright=1.0):
    parts = [(k, 1.0 / k) for k in range(1, 12)]
    decs = [0.5 / (1 + 0.6 * (k - 1) / bright) for k in range(1, 12)]
    y = additive(f, dur, parts, decs, tail=0.4)
    t = np.arange(len(y)) / SR
    y *= np.clip(t / 0.002, 0, 1) * np.where(t > dur, np.exp(-(t - dur) / 0.08), 1)
    return y * vel * 0.3

def pad(f, dur, vel=1.0, bright=6):
    y = np.zeros(int((dur + 1.0) * SR))
    for det in (-0.12, 0.0, 0.12):
        ff = f * 2 ** (det / 12)
        z = additive(ff, dur, [(k, 1.0 / k) for k in range(1, bright + 1)], [None] * bright, tail=1.0)
        y[:len(z)] += z
    y *= env_adsr(len(y), 0.35, 0.3, 0.8, 0.9, dur)
    return y * vel * 0.07

def brass(f, dur, vel=1.0):
    y = additive(f, dur, [(k, 1.0 / k) for k in range(1, 9)], [None] * 8, tail=0.3, vib=0.004)
    e = env_adsr(len(y), 0.06, 0.15, 0.75, 0.2, dur)
    return y * e * vel * 0.22

def bass(f, dur, vel=1.0, tone=0.35):
    y = additive(f, dur, [(1, 1.0), (2, tone), (3, tone * 0.4)], [None, 0.25, 0.15], tail=0.1)
    e = env_adsr(len(y), 0.005, 0.2, 0.7, 0.06, dur)
    return y * e * vel * 0.5

def square_lead(f, dur, vel=1.0):
    y = additive(f, dur, [(k, 1.0 / k) for k in (1, 3, 5, 7, 9, 11)], [None] * 6, tail=0.15, vib=0.006)
    e = env_adsr(len(y), 0.01, 0.1, 0.8, 0.1, dur)
    return y * e * vel * 0.2

def power(f, dur, vel=1.0):
    y = np.zeros(int((dur + 0.2) * SR))
    for mul in (1.0, 1.4983, 2.0):
        z = additive(f * mul, dur, [(k, 1.0 / k) for k in range(1, 8)], [None] * 7, tail=0.2)
        y[:len(z)] += z
    e = env_adsr(len(y), 0.005, 0.25, 0.5, 0.08, dur)
    return y * e * vel * 0.1

rng_noise = np.random.default_rng(7)
def noise(n): return rng_noise.standard_normal(n)

def kick(vel=1.0, heavy=False):
    n = int(0.45 * SR); t = np.arange(n) / SR
    f0, f1 = (110, 40) if heavy else (140, 50)
    f = f1 + (f0 - f1) * np.exp(-t / 0.04)
    ph = 2 * np.pi * np.cumsum(f) / SR
    y = np.sin(ph) * np.exp(-t / (0.22 if heavy else 0.15))
    y += 0.3 * noise(n) * np.exp(-t / 0.004)
    return y * vel * (0.9 if heavy else 0.75)

def snare(vel=1.0):
    n = int(0.3 * SR); t = np.arange(n) / SR
    nz = noise(n); nz = np.diff(nz, prepend=0)
    y = 0.5 * nz * np.exp(-t / 0.07) + 0.6 * np.sin(2 * np.pi * 190 * t) * np.exp(-t / 0.05)
    return y * vel * 0.35

def hat(vel=1.0, open_=False):
    n = int((0.25 if open_ else 0.06) * SR); t = np.arange(n) / SR
    nz = noise(n); nz = np.diff(np.diff(nz, prepend=0), prepend=0)
    return nz * np.exp(-t / (0.08 if open_ else 0.015)) * vel * 0.07

def shaker(vel=1.0):
    n = int(0.09 * SR); t = np.arange(n) / SR
    nz = np.diff(noise(n), prepend=0)
    e = np.sin(np.pi * np.clip(t / 0.09, 0, 1)) ** 2
    return nz * e * vel * 0.05

def tom(f, vel=1.0):
    n = int(0.4 * SR); t = np.arange(n) / SR
    ff = f * (1 + 0.5 * np.exp(-t / 0.03))
    return np.sin(2 * np.pi * np.cumsum(ff) / SR) * np.exp(-t / 0.18) * vel * 0.5

# ---------- ミキサー ----------
class Song:
    def __init__(self, bpm, bars, beats_per_bar=4):
        self.bpm = bpm; self.bars = bars; self.bpb = beats_per_bar
        self.beat = 60.0 / bpm
        self.length = bars * beats_per_bar * self.beat
        self.tail = 3.0
        n = int((self.length + self.tail) * SR) + SR
        self.L = np.zeros(n); self.R = np.zeros(n)
        self.revL = np.zeros(n); self.revR = np.zeros(n)

    def add(self, y, beat_pos, pan=0.0, rev=0.2, gain=1.0):
        i = int(beat_pos * self.beat * SR)
        y = y * gain
        m = min(len(y), len(self.L) - i)
        if m <= 0: return
        gl = np.sqrt(0.5 * (1 - pan)); gr = np.sqrt(0.5 * (1 + pan))
        self.L[i:i + m] += y[:m] * gl; self.R[i:i + m] += y[:m] * gr
        self.revL[i:i + m] += y[:m] * gl * rev; self.revR[i:i + m] += y[:m] * gr * rev

    def render(self, rev_time=1.4):
        def conv(x, seed):
            r = np.random.default_rng(seed)
            n = int(rev_time * SR); t = np.arange(n) / SR
            ir = r.standard_normal(n) * np.exp(-t / (rev_time / 5))
            ir[:int(0.012 * SR)] = 0
            ir = np.convolve(ir, np.ones(8) / 8, mode='same')  # こもらせる
            ir /= np.sqrt(np.sum(ir ** 2))
            N = 1 << int(np.ceil(np.log2(len(x) + n)))
            return np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(ir, N), N)[:len(x)]
        L = self.L + 0.5 * conv(self.revL, 1)
        R = self.R + 0.5 * conv(self.revR, 2)
        n = int(self.length * SR)
        # 余韻を頭に重ねてループのつなぎ目をなくす
        for ch in (L, R):
            ch[:len(ch) - n] += ch[n:]
        L = L[:n]; R = R[:n]
        st = np.stack([L, R], 1)
        # やわらかいリミッター
        peak = np.max(np.abs(st))
        st = np.tanh(st / peak * 1.2) / np.tanh(1.2) * 0.85
        return st

def save(st, name):
    path = os.path.join(OUT, name)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((st * 32767).astype(np.int16).tobytes())
    print(name, f"{len(st)/SR:.1f}s", f"{os.path.getsize(path)/1e6:.1f}MB")

# ---------- 作曲まわり ----------
NOTE = {n: i for i, n in enumerate(['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'])}
NOTE.update({'Db': 1, 'Eb': 3, 'Gb': 6, 'Ab': 8, 'Bb': 10})
MAJOR = [0, 2, 4, 5, 7, 9, 11]; MINOR = [0, 2, 3, 5, 7, 8, 10]

def chord(name):
    """'F', 'Dm', 'E', 'Bdim' → (root_pc, [pcs])"""
    root = name[:2] if len(name) > 1 and name[1] in '#b' else name[:1]
    q = name[len(root):]
    r = NOTE[root]
    iv = {'': [0, 4, 7], 'm': [0, 3, 7], '7': [0, 4, 7, 10], 'm7': [0, 3, 7, 10], 'maj7': [0, 4, 7, 11], 'sus4': [0, 5, 7]}[q]
    return r, [(r + i) % 12 for i in iv]

def scale_for(key_pc, mode, ch_pcs):
    pcs = [(key_pc + s) % 12 for s in mode]
    out = []
    for p in pcs:
        rep = p
        for c in ch_pcs:
            if (c - p) % 12 in (1, 11): rep = c
        out.append(rep)
    return sorted(set(out))

def nearest(pitch, pcs, lo, hi):
    best = None
    for m in range(lo, hi + 1):
        if m % 12 in pcs and (best is None or abs(m - pitch) < abs(best - pitch)): best = m
    return best

def step(pitch, pcs, direction):
    m = pitch + direction
    while m % 12 not in pcs: m += direction
    return m

def make_melody(rng, chords_per_bar, key_pc, mode, rhythm_bars, lo, hi, start):
    """rhythm_bars: 小節ごとのリズム [(拍, 長さ)...]。返り値 [(拍位置, 長さ, midi)]"""
    notes = []; p = start
    for b, (cname, rhythm) in enumerate(zip(chords_per_bar, rhythm_bars)):
        _, cps = chord(cname)
        sc = scale_for(key_pc, mode, cps)
        for (pos, dur) in rhythm:
            strong = abs(pos - round(pos)) < 1e-6 and int(round(pos)) % 2 == 0
            if strong:
                target = p + rng.choice([-3, -2, 0, 2, 3, 4])
                p = nearest(target, cps, lo, hi)
            else:
                d = rng.choice([-1, 1, 1, -1, 2, -2])
                q = p
                for _ in range(abs(d)): q = step(q, sc, 1 if d > 0 else -1)
                if lo <= q <= hi: p = q
            notes.append((b * 4 + pos, dur, p))
    return notes

def transpose_rest(notes, bar_offset):
    return [(pos + bar_offset * 4, d, m) for pos, d, m in notes]

def end_on_root(notes, key_pc, lo, hi):
    pos, d, m = notes[-1]
    notes[-1] = (pos, d, nearest(m, [key_pc], lo, hi))
    return notes

# ---------- 曲 ----------
def lobby():
    # のんびり：F長調 84BPM、オルゴール＋パッド＋マリンバのベース、シェイカーだけ
    s = Song(84, 16)
    rng = np.random.default_rng(11)
    prog = ['F', 'Dm', 'Bb', 'C', 'F', 'Am', 'Bb', 'C', 'Dm', 'Bb', 'F', 'C', 'Bb', 'C', 'F', 'F']
    rA = [[(0, 1.5), (1.5, 0.5), (2, 2)], [(0, 1), (1, 1), (2, 1.5), (3.5, 0.5)], [(0, 2), (2, 1), (3, 1)], [(0, 3)]]
    rB = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 2)], [(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 1), (1, 1), (2, 2)], [(0, 2), (2, 2)]]
    kp = NOTE['F']
    a = make_melody(rng, prog[:4], kp, MAJOR, rA, 72, 86, 77)
    a2 = make_melody(np.random.default_rng(12), prog[4:8], kp, MAJOR, rA, 72, 86, a[-1][2])
    b = make_melody(np.random.default_rng(13), prog[8:12], kp, MAJOR, rB, 72, 86, 81)
    c = make_melody(np.random.default_rng(14), prog[12:16], kp, MAJOR, rA[:3] + [[(0, 4)]], 72, 86, 79)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    mel = end_on_root(mel, kp, 72, 86)
    for pos, d, m in mel:
        s.add(music_box(midi_hz(m), d * s.beat, 0.9), pos, pan=0.15, rev=0.45)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        voic = sorted(nearest(60 + (p - 60) % 12, [p], 55, 67) for p in cps)
        for m in voic: s.add(pad(midi_hz(m), 4 * s.beat, 0.8), bar * 4, pan=-0.2, rev=0.5)
        root = nearest(41, [r], 36, 47)
        s.add(marimba(midi_hz(root), 1.5 * s.beat, 0.9), bar * 4, pan=0, rev=0.25)
        s.add(marimba(midi_hz(root + 7), 1 * s.beat, 0.6), bar * 4 + 2, pan=0, rev=0.25)
        # 柔らかいアルペジオ（マリンバ、控えめ）
        arp = sorted(nearest(64 + (p - 64) % 12, [p], 60, 72) for p in cps)
        for k, off in enumerate([1, 1.5, 3, 3.5]):
            s.add(marimba(midi_hz(arp[k % len(arp)]), 0.5 * s.beat, 0.35), bar * 4 + off, pan=0.4, rev=0.35)
        for k in range(8): s.add(shaker(0.8 if k % 2 else 0.5), bar * 4 + k * 0.5, pan=0.3, rev=0.2)
    save(s.render(1.8), "bgm_lobby.wav")

def basics():
    # わくわく：C長調 128BPM、マリンバのメロディ、はずむベース、軽いドラム
    s = Song(128, 16)
    prog = ['C', 'G', 'Am', 'F'] * 2 + ['F', 'G', 'Em', 'Am', 'F', 'G', 'C', 'C']
    kp = NOTE['C']
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2.5, 0.5), (3, 1)], [(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)]]
    rB = [[(0, 1), (1, 1), (2, 0.5), (2.5, 1.5)], [(0, 0.5), (0.5, 1), (1.5, 0.5), (2, 2)],
          [(0, 1), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 2), (2.5, 0.5), (3, 1)]]
    a = make_melody(np.random.default_rng(21), prog[:4], kp, MAJOR, rA, 72, 88, 76)
    a2 = [(p, d, m) for p, d, m in a]  # 繰り返し
    b = make_melody(np.random.default_rng(22), prog[8:12], kp, MAJOR, rB, 72, 88, 81)
    c = make_melody(np.random.default_rng(23), prog[12:16], kp, MAJOR, rA[:2] + rB[2:3] + [[(0, 3)]], 72, 88, 79)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    mel = end_on_root(mel, kp, 72, 88)
    for pos, d, m in mel:
        s.add(marimba(midi_hz(m), d * s.beat, 1.0), pos, pan=0.1, rev=0.2)
        s.add(music_box(midi_hz(m + 12), d * s.beat, 0.18), pos, pan=0.3, rev=0.3)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(40, [r], 36, 47)
        for k in range(8):
            m = root + (12 if k % 2 else 0)
            s.add(bass(midi_hz(m), 0.4 * s.beat, 0.9 if k % 2 == 0 else 0.6), bar * 4 + k * 0.5, rev=0.05)
        voic = sorted(nearest(62 + (p - 62) % 12, [p], 60, 72) for p in cps)
        for off in (0.5, 1.5, 2.5, 3.5):
            for m in voic: s.add(pluck(midi_hz(m), 0.25 * s.beat, 0.35), bar * 4 + off, pan=-0.35, rev=0.2)
        for beat in (0, 2, 2.75 if bar % 2 else 2): s.add(kick(0.9), bar * 4 + beat, rev=0.05)
        for beat in (1, 3): s.add(snare(0.8), bar * 4 + beat, pan=0.05, rev=0.2)
        for k in range(8): s.add(hat(0.9 if k % 2 else 0.6), bar * 4 + k * 0.5, pan=0.25, rev=0.05)
    save(s.render(1.2), "bgm_basics.wav")

def pitfalls():
    # どっしり：D短調 88BPM、低いブラス、重いキック、ハーフタイム
    s = Song(88, 16)
    prog = ['Dm', 'Bb', 'Gm', 'A'] * 2 + ['Dm', 'C', 'Bb', 'A', 'Gm', 'Bb', 'A', 'A']
    kp = NOTE['D']
    rA = [[(0, 1.5), (1.5, 0.5), (2, 2)], [(0, 1), (1, 1), (2, 2)], [(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 3), (3, 1)]]
    a = make_melody(np.random.default_rng(31), prog[:4], kp, MINOR, rA, 57, 69, 62)
    b = make_melody(np.random.default_rng(32), prog[8:12], kp, MINOR, rA, 57, 71, 65)
    c = make_melody(np.random.default_rng(33), prog[12:16], kp, MINOR, rA[:3] + [[(0, 4)]], 57, 69, 67)
    mel = a + transpose_rest(a, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    for pos, d, m in mel:
        s.add(brass(midi_hz(m), d * s.beat * 0.95, 1.0), pos, pan=0.1, rev=0.3)
        s.add(brass(midi_hz(m - 12), d * s.beat * 0.95, 0.5), pos, pan=-0.1, rev=0.3)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(38, [r], 33, 44)
        s.add(bass(midi_hz(root), 1.8 * s.beat, 1.0, tone=0.6), bar * 4, rev=0.1)
        s.add(bass(midi_hz(root), 0.9 * s.beat, 0.8, tone=0.6), bar * 4 + 2, rev=0.1)
        s.add(bass(midi_hz(root + 12), 0.4 * s.beat, 0.6, tone=0.6), bar * 4 + 3.5, rev=0.1)
        voic = sorted(nearest(55 + (p - 55) % 12, [p], 50, 62) for p in cps)
        for m in voic: s.add(pad(midi_hz(m), 4 * s.beat, 0.9, bright=4), bar * 4, pan=-0.25, rev=0.4)
        s.add(kick(1.0, heavy=True), bar * 4, rev=0.15)
        s.add(kick(0.7, heavy=True), bar * 4 + 1.5, rev=0.15)
        s.add(snare(1.0), bar * 4 + 2, rev=0.45)
        if bar % 4 == 3:
            for k, f in enumerate((110, 90, 70)): s.add(tom(f, 0.8), bar * 4 + 3 + k / 3, pan=-0.3 + 0.3 * k, rev=0.3)
        for k in range(4): s.add(hat(0.5), bar * 4 + k + 0.5, pan=0.3, rev=0.1)
    save(s.render(2.0), "bgm_pitfalls.wav")

def mix():
    # ドキドキ：A短調 140BPM、16分のアルペジオ、刻むベース、4つ打ち
    s = Song(140, 24)
    prog = (['Am', 'F', 'G', 'E'] * 2 + ['Dm', 'Am', 'E', 'Am', 'Dm', 'F', 'E', 'E'] + ['Am', 'F', 'G', 'E'] * 2)
    kp = NOTE['A']
    rA = [[(0, 1), (1, 0.5), (1.5, 1), (2.5, 0.5), (3, 1)], [(0, 0.5), (0.5, 0.5), (1, 1), (2, 1.5), (3.5, 0.5)],
          [(0, 1), (1, 0.5), (1.5, 0.5), (2, 0.5), (2.5, 1.5)], [(0, 1.5), (1.5, 0.5), (2, 2)]]
    a = make_melody(np.random.default_rng(41), prog[:4], kp, MINOR, rA, 69, 84, 76)
    b = make_melody(np.random.default_rng(42), prog[8:12], kp, MINOR, rA, 69, 86, 74)
    b2 = make_melody(np.random.default_rng(43), prog[12:16], kp, MINOR, rA[:3] + [[(0, 4)]], 69, 86, 77)
    mel = a + transpose_rest(a, 4) + transpose_rest(b, 8) + transpose_rest(b2, 12) + transpose_rest(a, 16) + transpose_rest(a, 20)
    for pos, d, m in mel:
        if 16 <= pos < 24:  # 後半の頭はメロディをオクターブ上に重ねて盛り上げる
            s.add(pluck(midi_hz(m + 12), d * s.beat, 0.4, bright=2), pos, pan=-0.3, rev=0.25)
        s.add(marimba(midi_hz(m), d * s.beat, 1.0), pos, pan=0.15, rev=0.2)
        s.add(square_lead(midi_hz(m), d * s.beat * 0.9, 0.35), pos, pan=0.05, rev=0.2)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(45, [r], 40, 51)
        for k in range(8): s.add(bass(midi_hz(root), 0.35 * s.beat, 0.9 if k % 2 == 0 else 0.7, tone=0.5), bar * 4 + k * 0.5, rev=0.05)
        arp = sorted(nearest(64 + (p - 64) % 12, [p], 60, 72) for p in cps)
        seq = [arp[0], arp[1], arp[2], arp[1] + 0, arp[0] + 12, arp[2], arp[1], arp[2]]
        for k in range(16):
            s.add(pluck(midi_hz(seq[k % 8]), 0.22 * s.beat, 0.3, bright=1.5), bar * 4 + k * 0.25, pan=-0.4 if k % 2 else 0.4, rev=0.2)
        for beat in range(4): s.add(kick(0.85), bar * 4 + beat, rev=0.05)
        for beat in (1, 3): s.add(snare(0.8), bar * 4 + beat, rev=0.2)
        for k in range(16): s.add(hat(0.8 if k % 4 == 2 else 0.45), bar * 4 + k * 0.25, pan=0.3, rev=0.05)
    save(s.render(1.2), "bgm_mix.wav")

def monster():
    # かっこよく：E短調 150BPM、矩形波リード、パワーコード、ロックビート
    s = Song(150, 24)
    prog = ['Em', 'C', 'D', 'B'] * 2 + ['C', 'D', 'Em', 'Em', 'C', 'D', 'B', 'B'] + ['Em', 'C', 'D', 'B'] * 2
    kp = NOTE['E']
    rA = [[(0, 0.75), (0.75, 0.75), (1.5, 0.5), (2, 1), (3, 0.5), (3.5, 0.5)], [(0, 1.5), (1.5, 0.5), (2, 1), (3, 1)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 1), (2.5, 0.5), (3, 1)], [(0, 2.5), (3, 0.5), (3.5, 0.5)]]
    rB = [[(0, 2), (2, 1), (3, 1)], [(0, 1.5), (1.5, 1.5), (3, 1)], [(0, 1), (1, 1), (2, 1), (3, 1)], [(0, 4)]]
    a = make_melody(np.random.default_rng(51), prog[:4], kp, MINOR, rA, 64, 81, 71)
    b = make_melody(np.random.default_rng(52), prog[8:12], kp, MINOR, rB, 64, 83, 72)
    b2 = make_melody(np.random.default_rng(53), prog[12:16], kp, MINOR, rB, 64, 83, 74)
    mel = a + transpose_rest(a, 4) + transpose_rest(b, 8) + transpose_rest(b2, 12) + transpose_rest(a, 16) + transpose_rest(a, 20)
    for pos, d, m in mel:
        s.add(square_lead(midi_hz(m), d * s.beat * 0.92, 1.0), pos, pan=0.1, rev=0.25)
        s.add(square_lead(midi_hz(m + 12), d * s.beat * 0.92, 0.25), pos, pan=-0.2, rev=0.3)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(40, [r], 35, 46)
        for k in range(8):
            m = root + (12 if k in (3, 7) else 0)
            s.add(bass(midi_hz(m), 0.4 * s.beat, 1.0 if k % 2 == 0 else 0.75, tone=0.7), bar * 4 + k * 0.5, rev=0.05)
        pr = nearest(52, [r], 47, 58)
        for off, d in ((0, 1.25), (1.5, 0.5), (2.5, 1.25)):
            s.add(power(midi_hz(pr), d * s.beat, 0.9), bar * 4 + off, pan=-0.35, rev=0.15)
            s.add(power(midi_hz(pr), d * s.beat, 0.9), bar * 4 + off + 0.02, pan=0.35, rev=0.15)
        for beat in (0, 1.5, 2.5 if bar % 2 else 2): s.add(kick(1.0), bar * 4 + beat, rev=0.05)
        for beat in (1, 3): s.add(snare(1.0), bar * 4 + beat, rev=0.25)
        for k in range(8): s.add(hat(0.8 if k % 2 else 0.5, open_=(k == 7 and bar % 4 == 3)), bar * 4 + k * 0.5, pan=0.3, rev=0.05)
        if bar % 8 == 7:
            for k, f in enumerate((140, 115, 95, 75)): s.add(tom(f, 0.9), bar * 4 + 2 + k * 0.5, pan=-0.4 + 0.25 * k, rev=0.25)
    save(s.render(1.0), "bgm_monster.wav")

def airy(dur, vel=1.0):
    # 息のような「ふわー」：こもらせたノイズをゆっくりふくらませる
    n = int((dur + 1.5) * SR)
    nz = rng_noise.standard_normal(n)
    k = 60
    nz = np.convolve(nz, np.ones(k) / k, mode='same')
    e = env_adsr(n, dur * 0.45, 0.2, 0.9, 1.5, dur)
    return nz * e * vel * 0.25

def celesta(f, dur, vel=1.0):
    # やわらかいベル（オルゴールより丸く、余韻長め）
    y = additive(f, dur, [(1, 1.0), (2.0, 0.18), (4.0, 0.05)], [1.4, 0.5, 0.2], tail=1.6)
    t = np.arange(len(y)) / SR
    y *= np.clip(t / 0.012, 0, 1)
    return y * vel * 0.42

def felt(f, dur, vel=1.0):
    # フェルトピアノっぽい、こもったやわらかい音（アタックを丸く、倍音少なめ）
    y = additive(f, dur, [(1, 1.0), (2.0, 0.12), (3.0, 0.03)], [1.8, 0.6, 0.3], tail=1.5)
    t = np.arange(len(y)) / SR
    y *= np.clip(t / 0.035, 0, 1) * np.where(t > dur, np.exp(-(t - dur) / 0.5), 1)
    return y * vel * 0.45

def lowpass(st, cutoff):
    # 1次のローパスを2回（高い音を丸める）
    a = np.exp(-2 * np.pi * cutoff / SR)
    out = st.copy()
    for _ in range(2):
        y = np.zeros_like(out); prev = np.zeros(out.shape[1])
        # ベクトル化できないので、チャンクごとに状態を引き継ぐ
        for i in range(len(out)):
            prev = (1 - a) * out[i] + a * prev
            y[i] = prev
        out = y
    return out

def lobby_b():
    # 寝ちゃいそうなくらいやわらか：G長調 60BPM、フェルトピアノのゆっくりなメロディ、あたたかいパッド、
    # ゆりかごみたいな分散和音、打楽器なし、高い音はこもらせる
    s = Song(60, 16)
    prog = ['Gmaj7', 'Em7', 'Cmaj7', 'D', 'Gmaj7', 'Em7', 'Cmaj7', 'Dsus4',
            'Cmaj7', 'Gmaj7', 'Am7', 'D', 'Cmaj7', 'Gmaj7', 'Am7', 'Gmaj7']
    kp = NOTE['G']
    rA = [[(0, 3), (3, 1)], [(0, 4)], [(0, 2), (2, 2)], [(0, 4)]]
    rB = [[(1, 3)], [(0, 3), (3, 1)], [(0, 4)], [(0, 4)]]
    a = make_melody(np.random.default_rng(71), prog[:4], kp, MAJOR, rA, 67, 79, 74)
    a2 = make_melody(np.random.default_rng(72), prog[4:8], kp, MAJOR, rA, 67, 79, a[-1][2])
    b = make_melody(np.random.default_rng(73), prog[8:12], kp, MAJOR, rB, 67, 79, 76)
    c = make_melody(np.random.default_rng(74), prog[12:16], kp, MAJOR, rA, 67, 79, 74)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    mel = end_on_root(mel, kp, 67, 79)
    for pos, d, m in mel:
        s.add(felt(midi_hz(m), d * s.beat, 0.8), pos, pan=0.1, rev=0.3)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        voic = sorted(nearest(55 + (p - 55) % 12, [p], 52, 64) for p in cps)
        for m in voic:
            s.add(pad(midi_hz(m), 4 * s.beat, 0.7, bright=2), bar * 4, pan=-0.25, rev=0.3)
        root = nearest(43, [r], 36, 47)
        s.add(pad(midi_hz(root), 4 * s.beat, 1.1, bright=2), bar * 4, pan=0, rev=0.2)
        # ゆりかご：1拍ずつ、根音→上の音→真ん中→上の音
        arp = sorted(nearest(60 + (p - 60) % 12, [p], 55, 67) for p in cps)
        seq = [arp[0], arp[-1], arp[1], arp[-1]]
        for k in range(4):
            s.add(felt(midi_hz(seq[k]), 1.0 * s.beat, 0.28 if k == 0 else 0.2), bar * 4 + k, pan=(-0.35 if k % 2 else 0.35), rev=0.3)
    st = s.render(1.6)
    st = lowpass(st, 2600)
    st = st / np.max(np.abs(st)) * 0.8
    save(st, "bgm_lobby_b.wav")

def crash(vel=1.0):
    n = int(1.6 * SR); t = np.arange(n) / SR
    nz = np.diff(np.diff(noise(n), prepend=0), prepend=0)
    return nz * np.exp(-t / 0.45) * vel * 0.09

def pitfalls_b():
    # どっしり＋パワフル：D短調 100BPM、刻むパワーコード、太いブラス＋オクターブ、重いドラムと大きいスネア
    s = Song(100, 24)
    prog = ['Dm', 'Bb', 'C', 'A'] * 2 + ['Gm', 'Bb', 'Dm', 'A', 'Gm', 'Bb', 'C', 'A'] + ['Dm', 'Bb', 'C', 'A'] * 2
    kp = NOTE['D']
    rA = [[(0, 1), (1, 0.5), (1.5, 0.5), (2, 1.5), (3.5, 0.5)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 1), (2, 1), (3, 1)], [(0, 3), (3, 0.5), (3.5, 0.5)]]
    rB = [[(0, 2), (2, 1), (3, 1)], [(0, 1.5), (1.5, 1.5), (3, 1)], [(0, 1), (1, 1), (2, 1), (3, 1)], [(0, 4)]]
    a = make_melody(np.random.default_rng(81), prog[:4], kp, MINOR, rA, 62, 76, 69)
    b = make_melody(np.random.default_rng(82), prog[8:12], kp, MINOR, rB, 62, 77, 67)
    b2 = make_melody(np.random.default_rng(83), prog[12:16], kp, MINOR, rB, 62, 77, 70)
    mel = a + transpose_rest(a, 4) + transpose_rest(b, 8) + transpose_rest(b2, 12) + transpose_rest(a, 16) + transpose_rest(a, 20)
    mel = end_on_root(mel, kp, 62, 76)
    for pos, d, m in mel:
        s.add(brass(midi_hz(m), d * s.beat * 0.92, 1.2), pos, pan=0.15, rev=0.25)
        s.add(brass(midi_hz(m - 12), d * s.beat * 0.92, 0.8), pos, pan=-0.15, rev=0.25)
        if pos >= 64: s.add(square_lead(midi_hz(m + 12), d * s.beat * 0.9, 0.35), pos, pan=0.3, rev=0.3)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        root = nearest(38, [r], 33, 44)
        for k in range(8):
            s.add(bass(midi_hz(root + (12 if k == 7 else 0)), 0.42 * s.beat, 1.0 if k % 2 == 0 else 0.8, tone=0.8), bar * 4 + k * 0.5, rev=0.05)
        pr = nearest(50, [r], 45, 56)
        for k in range(8):
            if k in (3, 7) and bar % 2 == 1: continue
            vel = 1.0 if k in (0, 3, 6) else 0.6
            s.add(power(midi_hz(pr), 0.38 * s.beat, vel), bar * 4 + k * 0.5, pan=-0.4, rev=0.12)
            s.add(power(midi_hz(pr), 0.38 * s.beat, vel), bar * 4 + k * 0.5 + 0.01, pan=0.4, rev=0.12)
        for beat in (0, 1, 2, 3): s.add(kick(1.0 if beat % 2 == 0 else 0.8, heavy=True), bar * 4 + beat, rev=0.1)
        s.add(kick(0.7, heavy=True), bar * 4 + 2.5, rev=0.1)
        for beat in (1, 3): s.add(snare(1.3), bar * 4 + beat, rev=0.45)
        for k in range(8): s.add(hat(0.9 if k % 2 else 0.6, open_=(k == 7)), bar * 4 + k * 0.5, pan=0.3, rev=0.05)
        if bar % 4 == 0: s.add(crash(1.0), bar * 4, pan=-0.2, rev=0.3)
        if bar % 4 == 3:
            for k, f in enumerate((130, 110, 90, 70)): s.add(tom(f, 1.0), bar * 4 + 2 + k * 0.5, pan=-0.4 + 0.27 * k, rev=0.3)
    save(s.render(1.4), "bgm_pitfalls_b.wav")

def fanfare():
    # ゴールのファンファーレ（ループしない、約4秒）：タタタ・ターン！→ 和音で締め
    bpm = 132; beat = 60 / bpm
    total = 5.0
    n = int(total * SR)
    L = np.zeros(n); R = np.zeros(n); rv = np.zeros(n)
    def add(y, t, pan=0.0, rev=0.25, g=1.0):
        i = int(t * SR); m = min(len(y), n - i)
        if m <= 0: return
        gl = np.sqrt(0.5 * (1 - pan)); gr = np.sqrt(0.5 * (1 + pan))
        L[i:i+m] += y[:m] * gl * g; R[i:i+m] += y[:m] * gr * g; rv[i:i+m] += y[:m] * rev * g
    # メロディ（C長調）：ソソソ ド〜 | ラ シ ド〜〜
    mel = [(0, 0.33, 67), (0.33, 0.33, 67), (0.66, 0.33, 67), (1.0, 1.0, 72),
           (2.0, 0.5, 69), (2.5, 0.5, 71), (3.0, 3.0, 72)]
    for b0, d, m in mel:
        add(brass(midi_hz(m), d * beat * 0.95, 1.3), b0 * beat, pan=0.1)
        add(brass(midi_hz(m + 12), d * beat * 0.95, 0.5), b0 * beat, pan=-0.1)
        add(music_box(midi_hz(m + 12), d * beat, 0.5), b0 * beat, pan=0.3, rev=0.4)
    # 和音：C → F → G → C
    for b0, d, ch in [(0, 1.0, [60, 64, 67]), (1.0, 1.0, [60, 64, 67]), (2.0, 1.0, [60, 65, 69]), (2.5, 0.5, [62, 67, 71]), (3.0, 3.0, [60, 64, 67, 72])]:
        for m in ch: add(brass(midi_hz(m - 12), d * beat * 0.95, 0.55), b0 * beat, pan=-0.3)
        add(bass(midi_hz(ch[0] - 24), d * beat, 1.0, tone=0.6), b0 * beat)
    # ドラム：スネアのロール → シンバル
    for k in range(6): add(snare(0.5 + 0.1 * k), k * beat / 6, pan=0.1, rev=0.2)
    add(kick(1.0), 1.0 * beat); add(crash(1.2), 1.0 * beat, pan=-0.2)
    add(kick(0.8), 2.0 * beat); add(kick(1.0), 3.0 * beat); add(crash(1.4), 3.0 * beat, pan=0.2)
    for k, f in enumerate((150, 125, 100)): add(tom(f, 0.8), 2.5 * beat + k * beat / 6, pan=-0.3 + 0.3 * k)
    # 残響
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
    save(st, "jingle_goal.wav")

def whistle(f, dur, vel=1.0):
    # 口笛っぽいやわらかいリード（サイン＋少しのビブラート、ゆっくり立ち上がる）
    y = additive(f, dur, [(1, 1.0), (2, 0.06)], [None, None], tail=0.12, vib=0.008, vib_rate=5.0)
    e = env_adsr(len(y), 0.04, 0.1, 0.85, 0.1, dur)
    n = rng_noise.standard_normal(len(y)); n = np.convolve(n, np.ones(6) / 6, mode='same')
    return (y + 0.04 * n) * e * vel * 0.3

def after_goal():
    # ゴール後の軽快でかるいBGM：F長調 112BPM、ウクレレっぽいカッティング＋口笛＋手拍子
    s = Song(112, 16)
    prog = ['F', 'Dm', 'Bb', 'C'] * 2 + ['Bb', 'C', 'Am', 'Dm', 'Bb', 'C', 'F', 'F']
    kp = NOTE['F']
    rA = [[(0, 0.5), (0.5, 0.5), (1, 1), (2.5, 0.5), (3, 1)], [(0, 1.5), (1.5, 0.5), (2, 2)],
          [(0, 0.5), (0.5, 0.5), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1)], [(0, 3)]]
    a = make_melody(np.random.default_rng(91), prog[:4], kp, MAJOR, rA, 72, 86, 77)
    b = make_melody(np.random.default_rng(92), prog[8:12], kp, MAJOR, rA, 72, 86, 81)
    c = make_melody(np.random.default_rng(93), prog[12:16], kp, MAJOR, rA, 72, 86, 79)
    mel = a + transpose_rest(a, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    mel = end_on_root(mel, kp, 72, 86)
    for pos, d, m in mel:
        s.add(whistle(midi_hz(m), d * s.beat * 0.9, 1.0), pos, pan=0.1, rev=0.25)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        voic = sorted(nearest(62 + (p - 62) % 12, [p], 57, 69) for p in cps)
        # ウクレレ風：ジャッ・ジャ・ジャッ・ジャ（ダウン強め、アップ弱め）
        for off, vel in ((0, 0.5), (0.75, 0.25), (1.5, 0.45), (2, 0.5), (2.75, 0.25), (3.5, 0.45)):
            for k, m in enumerate(voic):
                s.add(pluck(midi_hz(m), 0.3 * s.beat, vel, bright=1.2), bar * 4 + off + k * 0.012, pan=-0.3, rev=0.15)
        root = nearest(41, [r], 36, 47)
        s.add(bass(midi_hz(root), 0.8 * s.beat, 0.7, tone=0.3), bar * 4, rev=0.05)
        s.add(bass(midi_hz(root + 7), 0.8 * s.beat, 0.55, tone=0.3), bar * 4 + 2, rev=0.05)
        # 手拍子（スネアを軽く）とシェイカー
        for beat in (1, 3): s.add(snare(0.45), bar * 4 + beat, pan=0.2, rev=0.25)
        for k in range(8): s.add(shaker(0.9 if k % 2 else 0.6), bar * 4 + k * 0.5, pan=0.35, rev=0.1)
        s.add(kick(0.45), bar * 4, rev=0.05)
    save(s.render(1.2), "bgm_aftergoal.wav")

def lobby_b_old():
    # もっとふわふわ：G長調 72BPM、maj7の浮いた和音、長いパッド＋やわらかいベル、打楽器なし
    s = Song(72, 16)
    prog = ['Gmaj7', 'Em7', 'Cmaj7', 'D', 'Gmaj7', 'Bm7' if False else 'Em7', 'Cmaj7', 'Dsus4',
            'Cmaj7', 'Gmaj7', 'Am7', 'D', 'Cmaj7', 'Gmaj7', 'Am7', 'D']
    kp = NOTE['G']
    rA = [[(0, 3), (3, 1)], [(0, 2), (2, 2)], [(0, 1.5), (1.5, 2.5)], [(0, 4)]]
    rB = [[(1, 2), (3, 1)], [(0, 3), (3, 1)], [(0, 2), (2, 1), (3, 1)], [(0, 4)]]
    a = make_melody(np.random.default_rng(61), prog[:4], kp, MAJOR, rA, 74, 86, 79)
    a2 = make_melody(np.random.default_rng(62), prog[4:8], kp, MAJOR, rA, 74, 86, a[-1][2])
    b = make_melody(np.random.default_rng(63), prog[8:12], kp, MAJOR, rB, 74, 88, 83)
    c = make_melody(np.random.default_rng(64), prog[12:16], kp, MAJOR, rA, 74, 86, 81)
    mel = a + transpose_rest(a2, 4) + transpose_rest(b, 8) + transpose_rest(c, 12)
    mel = end_on_root(mel, kp, 74, 86)
    for pos, d, m in mel:
        s.add(celesta(midi_hz(m), d * s.beat, 0.85), pos, pan=0.2, rev=0.3)
        # 1拍半遅れのうすいこだま（反対側から）
        s.add(celesta(midi_hz(m + 12), d * s.beat, 0.07), pos + 1.5, pan=-0.5, rev=0.35)
    for bar, cn in enumerate(prog):
        r, cps = chord(cn)
        voic = sorted(nearest(57 + (p - 57) % 12, [p], 55, 67) for p in cps)
        for m in voic:
            s.add(pad(midi_hz(m), 4 * s.beat, 0.9, bright=3), bar * 4, pan=-0.3, rev=0.3)
            s.add(pad(midi_hz(m + 12), 4 * s.beat, 0.35, bright=2), bar * 4, pan=0.4, rev=0.35)
        root = nearest(43, [r], 36, 47)
        s.add(pad(midi_hz(root), 4 * s.beat, 1.4, bright=2), bar * 4, pan=0, rev=0.3)
        # きらきら：ゆっくりした分散和音（高い所、うすく）
        arp = sorted(nearest(76 + (p - 76) % 12, [p], 72, 84) for p in cps)
        for k, off in enumerate([0.5, 1.5, 2.5, 3.5]):
            s.add(celesta(midi_hz(arp[k % len(arp)]), 1.0 * s.beat, 0.2), bar * 4 + off, pan=(-0.6 if k % 2 else 0.6), rev=0.35)
        if bar % 2 == 0: s.add(airy(6 * s.beat, 0.8), bar * 4, pan=0, rev=0.2)
    save(s.render(1.5), "bgm_lobby_b.wav")

if __name__ == '__main__':
    which = sys.argv[1:] or ['lobby', 'basics', 'pitfalls', 'mix', 'monster']
    for w in which: globals()[w]()
