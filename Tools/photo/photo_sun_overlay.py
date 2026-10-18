# 決まった写真に、あとから写真用の太陽を重ねる（2026-10-18 てじゃ「縦看板の右上に、ちょっと見切れてる感じで太陽見せたいな」）。
# 太陽だけの画像は Unity の FuwaPhotoSun.RenderSunOnly で、同じ撮影カメラ・同じ大きさで撮った透明背景の PNG。
# 使い方: python photo_sun_overlay.py 写真.png 太陽.png 出力.png [ぼかしpx]
import sys
from PIL import Image, ImageFilter

src, sun, out = sys.argv[1], sys.argv[2], sys.argv[3]
blur = float(sys.argv[4]) if len(sys.argv) > 4 else 0.8   # 遠くの物なので、まわりの景色と同じくらいほんの少しぼかす
a = Image.open(src).convert("RGBA")
s = Image.open(sun).convert("RGBA")
if s.size != a.size: raise SystemExit("大きさがちがう %s %s" % (s.size, a.size))
if blur > 0: s = s.filter(ImageFilter.GaussianBlur(blur))
a.alpha_composite(s)
a.convert("RGB").save(out)
print("ok", out)
