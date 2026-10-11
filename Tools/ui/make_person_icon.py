# 写真館のモニターの「映す人」ボタンに置く人のピクトグラム（白、まわり透明）。カメラの絵（make_camera_icon.py）と同じ作り
#   python make_person_icon.py → Assets/FuwaCourse/Textures/Icon_Person.png
from PIL import Image, ImageDraw
import os
S = 4
W, H = 512, 400
im = Image.new('L', (W * S, H * S), 0)
d = ImageDraw.Draw(im)
def el(box, fill): d.ellipse([v * S for v in box], fill=fill)
def rr(box, r, fill): d.rounded_rectangle([v * S for v in box], radius=r * S, fill=fill)
# 頭
el((186, 20, 326, 160), 255)
# 体（上が丸い台形っぽい半円）
d.pieslice([v * S for v in (96, 180, 416, 500)], 180, 360, fill=255)
rr((96, 330, 416, 380), 20, 255)
d.rectangle([v * S for v in (96, 336, 416, 356)], fill=255)   # 半円と下の角丸の間のくびれを埋める
a = im.resize((W, H), Image.LANCZOS)
out = Image.new('RGBA', (W, H), (255, 255, 255, 0))
out.putalpha(a)
p = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'FuwaCourse', 'Textures', 'Icon_Person.png')
out.save(os.path.abspath(p))
print('saved', os.path.abspath(p))
