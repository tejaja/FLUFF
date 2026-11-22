# 紹介PVの編集：pv_timeline.json の並びどおりに、曲の小節にぴったり合わせてつなぐ
# python -I pv_edit.py pv_timeline.json [--fast]
#  b: Unity で撮ったショット（shots/<名前>.mp4、頭の 0.5 秒はとばす。from で開始位置）
#  a: てじゃのプレイ録画（playdir/<名前>.mp4 があればそれ、from で開始位置）。なければ仮の静止画（old＝前のPVの秒、bstill＝ショットの2秒目）＋文字
#  title: ロゴを重ねる、outro: サムネの絵＋フェードアウト
import json, os, sys, subprocess, tempfile, shutil
from PIL import Image, ImageDraw, ImageFont, ImageFilter

here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(here, '..', 'photo'))
from photo_logo import make_logo

cfg = json.load(open(sys.argv[1], encoding='utf-8'))
fast = '--fast' in sys.argv
W, H, FPS = 1920, 1080, 60
BAR = 240.0 / cfg['bpm']
tmp = tempfile.mkdtemp(prefix='pvedit_')
FONT = "C:/Windows/Fonts/meiryob.ttc"
# 途中のカットは無劣化（-qp 0）で作って、最後につなぐ時に1回だけ圧縮する（2026-11-20 てじゃ：2回圧縮をやめる）
enc = ['-c:v', 'libx264', '-preset', 'ultrafast' if fast else 'veryfast', '-qp', '0', '-pix_fmt', 'yuv420p', '-r', str(FPS)]
final_enc = ['-c:v', 'libx264', '-preset', 'veryfast' if fast else 'slower', '-crf', '18' if fast else '12', '-pix_fmt', 'yuv420p', '-r', str(FPS)]

def run(args):
    r = subprocess.run(['ffmpeg', '-v', 'error', '-y'] + args, capture_output=True, text=True)
    if r.returncode != 0: raise SystemExit(r.stderr)

def frame_at(path, t, out):
    run(['-ss', str(t), '-i', path, '-frames:v', '1', '-vf', f'scale={W}:{H}', out])

def placeholder(seg, out):
    src = os.path.join(tmp, seg['a'] + '_src.png')
    if 'old' in seg: frame_at(cfg['old_pv'], seg['old'], src)
    else: frame_at(os.path.join(cfg['shots'], seg['bstill'] + '.mp4'), 2.0, src)
    im = Image.open(src).convert('RGB').filter(ImageFilter.GaussianBlur(1.5))
    im = Image.blend(im, Image.new('RGB', im.size, (20, 20, 30)), 0.35)
    d = ImageDraw.Draw(im)
    f1 = ImageFont.truetype(FONT, 34); f2 = ImageFont.truetype(FONT, 64)
    t1 = '仮：てじゃのプレイ録画（' + seg['a'] + '）'; t2 = seg['label']
    for t, f, y in ((t1, f1, H // 2 - 70), (t2, f2, H // 2 - 10)):
        w = d.textlength(t, font=f)
        d.text(((W - w) / 2, y), t, font=f, fill=(255, 255, 255), stroke_width=3, stroke_fill=(40, 40, 60))
    im.save(out)

def still_clip(png, n, out, zoom=0.00025):
    # ゆっくり寄る（静止画のままだと止まって見えるので）
    run(['-loop', '1', '-i', png, '-vf',
         f"scale={W*2}:{H*2},zoompan=z='1+{zoom}*on':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={n}:s={W}x{H}:fps={FPS}",
         '-frames:v', str(n)] + enc + [out])

def outro_png(out):
    th = Image.open(cfg['thumb']).convert('RGB')
    bg = th.resize((W, int(W * th.height / th.width)), Image.LANCZOS)
    bg = bg.crop((0, (bg.height - H) // 2, W, (bg.height - H) // 2 + H)).filter(ImageFilter.GaussianBlur(28))
    bg = Image.blend(bg, Image.new('RGB', bg.size, (255, 255, 255)), 0.25)
    fg = th.resize((int(H * 0.86 * th.width / th.height), int(H * 0.86)), Image.LANCZOS)
    sh = Image.new('RGBA', (fg.width + 60, fg.height + 60), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle((30, 30, 30 + fg.width, 30 + fg.height), 18, fill=(0, 0, 0, 90))
    sh = sh.filter(ImageFilter.GaussianBlur(14))
    bg = bg.convert('RGBA'); x, y = (W - fg.width) // 2, (H - fg.height) // 2
    bg.alpha_composite(sh, (x - 30, y - 22))
    m = Image.new('L', fg.size, 0); ImageDraw.Draw(m).rounded_rectangle((0, 0, fg.width, fg.height), 18, fill=255)
    bg.paste(fg, (x, y), m)
    bg.convert('RGB').save(out)

parts = []
segs = cfg['segments']
# 全カットのクロスフェード（xfade_all 秒、カットに xfade があればその秒）。重なりの真ん中が小節の頭に来るように、
# 各カットの前後に半分ずつ「のりしろ」を足して作る（2026-11-20 てじゃ「各カット切り替え全部フェード」）
XA = float(cfg.get('xfade_all', 0))
half = [0] * (len(segs) + 1)   # half[k]：カット k-1 と k の境目ののりしろ（コマ）
for k in range(1, len(segs)):
    d = float(segs[k].get('xfade', XA)); half[k] = round(d * FPS / 2)
bar0 = 0
lens = []
for i, seg in enumerate(segs):
    # 小節の頭のコマ番号で切る（ずれがたまらない）
    f0 = round(bar0 * BAR * FPS); bar0 += seg['bars']; f1 = round(bar0 * BAR * FPS)
    n = f1 - f0
    pre, post = half[i], half[i + 1]
    L = pre + n + post
    out = os.path.join(tmp, f'{i:03d}.mp4')
    def src_args(path, frm):
        st = frm - pre / FPS
        pad = 0
        if st < 0: pad = round(-st * FPS); st = 0
        return ['-ss', str(st), '-i', path], (f',tpad=start={pad}:start_mode=clone' if pad else '') + f',tpad=stop={post + 2}:stop_mode=clone'
    if 'b' in seg:
        src = os.path.join(cfg['shots'], seg['b'] + '.mp4')
        ia, tp = src_args(src, seg.get('from', 0.5))
        vf = f'scale={W}:{H},fps={FPS}' + tp
        if seg.get('title'):
            logo = make_logo(W / 1200.0 * 1.25); lp = os.path.join(tmp, 'logo.png'); logo.save(lp)
            t_in = BAR * 1.5 + pre / FPS
            run(ia + ['-loop', '1', '-i', lp, '-filter_complex',
                 f'[0:v]{vf}[v];[1:v]format=rgba,fade=in:st={t_in}:d=1.2:alpha=1[l];[v][l]overlay=(W-w)/2:(H-h)/2-230:shortest=1,fade=in:st=0:d=1.5',
                 '-frames:v', str(L)] + enc + [out])
        elif seg.get('fadein'):
            run(ia + ['-vf', vf + ',fade=in:st=0:d=1.5', '-frames:v', str(L)] + enc + [out])
        else:
            run(ia + ['-vf', vf, '-frames:v', str(L)] + enc + [out])
    elif 'a' in seg:
        clip = os.path.join(cfg['playdir'], seg['a'] + '.mp4')
        if os.path.exists(clip):
            ia, tp = src_args(clip, seg.get('from', 0))
            run(ia + ['-vf', f'scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},fps={FPS}' + tp, '-frames:v', str(L)] + enc + [out])
        else:
            png = os.path.join(tmp, seg['a'] + '.png'); placeholder(seg, png); still_clip(png, L, out)
    elif seg.get('outro'):
        png = os.path.join(tmp, 'outro.png'); outro_png(png)
        fo = (pre + n) / FPS - BAR * 1.2
        run(['-loop', '1', '-i', png, '-vf',
             f"scale={W*2}:{H*2},zoompan=z='1+0.00012*on':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={L}:s={W}x{H}:fps={FPS},fade=in:st=0:d=0.6:color=white,fade=out:st={fo}:d={BAR*1.2}",
             '-frames:v', str(L)] + enc + [out])
    if seg.get('logo'):
        # ロゴを重ねる（logo＝何小節目から出すか。ラストのカットに使う）
        logo = make_logo(W / 1200.0 * 1.25); lp = os.path.join(tmp, 'logo.png'); logo.save(lp)
        t_in = BAR * float(seg['logo']) + pre / FPS; lo = os.path.join(tmp, f'{i:03d}_logo.mp4')
        run(['-i', out, '-loop', '1', '-i', lp, '-filter_complex',
             f'[1:v]format=rgba,fade=in:st={t_in}:d=1.2:alpha=1[l];[0:v][l]overlay=(W-w)/2:(H-h)/2{int(seg.get("logoy", -230)):+d}:shortest=1',   # logoy：まん中からの上下（px、1080p）
             '-frames:v', str(L)] + enc + [lo]); out = lo
    if seg.get('fadeout'):
        fo = os.path.join(tmp, f'{i:03d}_fo.mp4'); d = BAR * seg.get('fadebars', 2)
        run(['-i', out, '-vf', f'fade=out:st={(pre + n) / FPS - d}:d={d}', '-frames:v', str(L)] + enc + [fo]); out = fo
    parts.append(out); lens.append(L)
    print(f'{i:2d} bar {bar0 - seg["bars"] + 1:2d}-{bar0:2d} {seg.get("b") or seg.get("a") or "outro"} {n}f (+{pre}/{post})', flush=True)

total = round(bar0 * BAR * FPS) / FPS
music = cfg['music']
out = cfg['out'] + '_1080p.mp4'
aud = ['-af', f'afade=out:st={total - 3}:d=3', '-c:a', 'aac', '-b:a', '256k', '-t', str(total), out]
if not any(half):
    lst = os.path.join(tmp, 'list.txt')
    open(lst, 'w', encoding='utf-8').write(''.join(f"file '{p}'\n" for p in parts))
    run(['-f', 'concat', '-safe', '0', '-i', lst, '-i', music, '-map', '0:v', '-map', '1:a'] + final_enc + aud)
else:
    # つなぎ：のりしろ同士を xfade で重ねる
    ins = []
    for p in parts: ins += ['-i', p]
    # プレイ録画は yuvj420p（フルレンジ）で、そのままだと出力が pc 扱いになって白っぽく見えた（2026-11-20 てじゃ「やたら明るい」）→ 全部 TV レンジにそろえる
    fc = ''.join(f'[{k}:v]scale=out_range=tv,format=yuv420p,settb=AVTB,setpts=PTS-STARTPTS[v{k}];' for k in range(len(parts)))
    cur = 'v0'; acc = lens[0]
    for k in range(1, len(parts)):
        D = half[k] * 2
        if D == 0:
            fc += f'[{cur}][v{k}]concat=n=2:v=1:a=0[x{k}];'
        else:
            fc += f'[{cur}][v{k}]xfade=transition=fade:duration={D / FPS}:offset={(acc - D) / FPS}[x{k}];'
        acc += lens[k] - D; cur = f'x{k}'
    fc += f'[{cur}]format=yuv420p,setparams=range=tv[vout]'
    run(ins + ['-i', music, '-filter_complex', fc, '-map', '[vout]', '-map', f'{len(parts)}:a'] + final_enc + aud)
print(out)
# Discord 用（10MB 以下）
small = cfg['out'] + '_720p_discord.mp4'
kbps = int((9.3 * 8 * 1024) / total) - 130
run(['-i', out, '-vf', 'scale=1280:720', '-c:v', 'libx264', '-b:v', f'{kbps}k', '-pass', '1', '-an', '-f', 'mp4', '-passlogfile', os.path.join(tmp, 'p'), 'NUL'])
run(['-i', out, '-vf', 'scale=1280:720', '-c:v', 'libx264', '-b:v', f'{kbps}k', '-pass', '2', '-passlogfile', os.path.join(tmp, 'p'), '-c:a', 'aac', '-b:a', '128k', small])
print(small, os.path.getsize(small) // 1024, 'KB')
shutil.rmtree(tmp, ignore_errors=True)
