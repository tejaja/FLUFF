# CAMボタンの「CAM」の文字のかわりに置くカメラのピクトグラム（白、まわり透明）。2026-10-11 てじゃ「📷みたいなピクトグラムにできないかな」
#   python make_camera_icon.py → Assets/FuwaCourse/Textures/Icon_Camera.png
from PIL import Image, ImageDraw
import os
S = 4                      # 4倍で描いて縮める（ふちをなめらかに）
W, H = 512, 400
im = Image.new('L', (W * S, H * S), 0)
d = ImageDraw.Draw(im)
def rr(box, r, fill): d.rounded_rectangle([v * S for v in box], radius=r * S, fill=fill)
def el(box, fill): d.ellipse([v * S for v in box], fill=fill)
# 上のでっぱり（シャッターの台）
rr((150, 40, 300, 120), 26, 255)
# 本体
rr((24, 90, 488, 376), 70, 255)
# レンズ：外の輪をくり抜いて、まん中にレンズ
cx, cy = 256, 238
el((cx - 112, cy - 112, cx + 112, cy + 112), 0)
el((cx - 80, cy - 80, cx + 80, cy + 80), 255)
# 右上の小さい窓（フラッシュ）
el((392, 128, 432, 168), 0)
a = im.resize((W, H), Image.LANCZOS)
out = Image.new('RGBA', (W, H), (255, 255, 255, 0))
out.putalpha(a)
p = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'FuwaCourse', 'Textures', 'Icon_Camera.png')
out.save(os.path.abspath(p))
print('saved', os.path.abspath(p))
