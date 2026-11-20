# 紹介PVの絵コンテ（FuwaPv.Board の書き出し）を、ショットごとに横1列で並べた一覧画像にする
# python -I pv_sheet.py <boardフォルダ> <出力.png> [ショット名,...]
import sys, os, re
from PIL import Image, ImageDraw, ImageFont

src, out = sys.argv[1], sys.argv[2]
want = sys.argv[3].split(',') if len(sys.argv) > 3 and sys.argv[3] else None
files = {}
for f in os.listdir(src):
    m = re.match(r'(.+)_(\d+)\.png$', f)
    if m: files.setdefault(m.group(1), []).append((int(m.group(2)), f))
names = [n for n in (want or sorted(files)) if n in files]
tw, th = 480, 270
try: font = ImageFont.truetype("C:/Windows/Fonts/meiryob.ttc", 22)
except Exception: font = ImageFont.load_default()
cols = max(len(files[n]) for n in names)
sheet = Image.new('RGB', (cols * (tw + 6) + 6, len(names) * (th + 34) + 6), (30, 30, 36))
d = ImageDraw.Draw(sheet)
for r, n in enumerate(names):
    y = 6 + r * (th + 34)
    d.text((8, y), n, fill=(255, 230, 120), font=font)
    for i, f in sorted(files[n]):
        im = Image.open(os.path.join(src, f)).convert('RGB').resize((tw, th), Image.LANCZOS)
        sheet.paste(im, (6 + i * (tw + 6), y + 28))
sheet.save(out)
print(out, sheet.size)
