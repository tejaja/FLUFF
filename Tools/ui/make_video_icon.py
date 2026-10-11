# CAMボタン（モニターのアングル切り替え）のビデオカメラのピクトグラム（白、まわり透明）。2026-10-11 てじゃ「📷より📹のほうがいいかな」
#   python make_video_icon.py → Assets/FuwaCourse/Textures/Icon_Video.png
from PIL import Image, ImageDraw
import os
S = 4
W, H = 512, 400
im = Image.new('L', (W * S, H * S), 0)
d = ImageDraw.Draw(im)
def rr(box, r, fill): d.rounded_rectangle([v * S for v in box], radius=r * S, fill=fill)
# 本体（横長の角丸）
rr((20, 90, 350, 330), 60, 255)
# 右のレンズ（台形を角丸っぽく：多角形＋丸）
d.polygon([(v[0] * S, v[1] * S) for v in [(330, 175), (470, 100), (470, 320), (330, 245)]], fill=255)
rr((440, 100, 492, 320), 26, 255)
a = im.resize((W, H), Image.LANCZOS)
out = Image.new('RGBA', (W, H), (255, 255, 255, 0))
out.putalpha(a)
p = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'FuwaCourse', 'Textures', 'Icon_Video.png')
out.save(os.path.abspath(p))
print('saved', os.path.abspath(p))
