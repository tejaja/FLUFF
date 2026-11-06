# 2–4 オルゴールのお試し（2026-11-06）：乗る歯車の先のオルゴール1台が鳴らすメロディ1パート
#  てじゃ「何箇所かで音出してて、あわさってBGMなってたらかわいい」「お試しでね、ちゃんとした曲は本番でつくろう」
#  ループの長さ＝オルゴールのシリンダーが1周する時間（歯車の速さと歯数から決まる。引数で渡す）
#  出力：wav（ループ）と、ピンを並べる用の音符の表（json：秒・MIDI）
#  python make_musicbox_test.py <ループ秒>
import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fluffsynth as fs

T = float(sys.argv[1]) if len(sys.argv) > 1 else 31.03
BARS = 16
bpm = BARS * 4 * 60.0 / T

# F長調（拍, MIDI, 長さ拍）
C6, A5, F6, E6, D6, Bb5, G5, F5, G6, A6 = 84, 81, 89, 88, 86, 82, 79, 77, 91, 93
p1 = [(0, C6, 1), (1, A5, .5), (1.5, C6, .5), (2, F6, 1), (3, E6, 1),
      (4, D6, 1), (5, C6, 1), (6, A5, 2),
      (8, Bb5, 1), (9, D6, .5), (9.5, C6, .5), (10, Bb5, 1), (11, G5, 1),
      (12, A5, 1), (13, G5, .5), (13.5, F5, .5), (14, G5, 2)]
p2 = [(16, C6, 1), (17, A5, .5), (17.5, C6, .5), (18, F6, 1), (19, G6, 1),
      (20, A6, 1), (21, G6, .5), (22, F6, .5), (22.5, E6, 1.5),
      (24, D6, 1), (25, Bb5, 1), (26, C6, .5), (26.5, D6, .5), (27, E6, 1),
      (28, F6, 3)]
p3 = [(b + 32, m, d) for (b, m, d) in p1]
p4 = [(b + 32, m, d) for (b, m, d) in p2[:-1]] + [(60, F6, 1), (61, C6, 1), (62, A5, 1), (63, F5, 1)]
notes = p1 + p2 + p3 + p4

mix = fs.Mix(bpm, BARS, tail=3.0, seed=11)
for (b, m, d) in notes:
    mix.add(fs.music_box2(fs.midi_hz(m), d * mix.beat, vel=mix.vel(0.55)), b, pan=0.0, rev=0.22, humanize=0.0)
st = fs.master(mix, rt60=1.6, rev_level=0.35, comp=False)

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
out_dir = os.path.join(root, 'Assets', 'FuwaStage2', 'Audio')
os.makedirs(out_dir, exist_ok=True)
fs.save(st, os.path.join(out_dir, 'musicbox_test_melody.wav'))
with open(os.path.join(os.path.dirname(__file__), 'musicbox_test_melody.json'), 'w') as f:
    json.dump({'length': T, 'bpm': bpm, 'notes': [{'t': b * 60.0 / bpm, 'midi': m} for (b, m, d) in notes]}, f)
print('bpm', round(bpm, 2), 'notes', len(notes))
