# Vket ブースの縦看板（ワールド紹介）を作る（2026-10-18 てじゃ「縦いっぱいに写真＋ロゴ、下にゲームワールド公開中。日英両方」）。
# 撮影セットの縦の写真（1280×1624 など、640:812 の比）の下の方に、ゲーム内タイトルと同じ「FLUFF」ロゴ（緩急つき）と
# 「ゲームワールド公開中！」「Game world — now open!」を重ねる。看板の枠で外周が少し隠れるので、文字は内側に。
# 使い方: python photo_poster.py 入力.png 出力名（拡張子なし）
import os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONTS = "D:/UnityProject/FLUFF/Assets/FuwaCourse/Fonts/"
FACE = (255, 255, 255, 255)
EDGE = (51, 82, 148, 255)        # ロゴ・ゲーム内タイトルと同じ紺
SHADOW = (61, 99, 160, 120)
JA = os.environ.get("POSTER_JA", "ゲームワールド公開中！")
EN = os.environ.get("POSTER_EN", "Game world — now open!")
src, out = sys.argv[1], sys.argv[2]

img = Image.open(src).convert("RGBA")
# 看板の比（640:812）に、上下（横長なら左右）を同じだけ切ってそろえる（「たて長・看板用」の 3:4 で撮った写真も使えるように）
W, H = img.size; want = 640 / 812
if abs(W / H - want) > 0.005:
    if W / H < want: nh = int(W / want); img = img.crop((0, (H - nh) // 2, W, (H - nh) // 2 + nh))
    else: nw = int(H * want); img = img.crop(((W - nw) // 2, 0, (W - nw) // 2 + nw, H))
W, H = img.size
u = W / 640.0
POS = os.environ.get("POSTER_POS", "bottom")   # 文字を置く所：bottom＝下、top＝上、split＝ロゴは上の空・2行の文は下（真ん中に見せたい物がある写真）
LOGO_W = float(os.environ.get("POSTER_LOGO_W", "0.82"))   # ロゴの幅（看板の幅に対して）

# 文字を置く側を少しだけ暗く（白い字が草や空に溶けないように）
grad = Image.new("L", (1, H))
for y in range(H):
    yy = y / H if POS in ("bottom", "split") else 1 - y / H
    t = max(0.0, (yy - 0.62) / 0.38)
    grad.putpixel((0, y), int(110 * t ** 1.6))
shade = Image.new("RGBA", (W, H), (30, 45, 90, 0)); shade.putalpha(grad.resize((W, H)))
img = Image.alpha_composite(img, shade)

def text_line(text, fontfile, size, sw):
    f = ImageFont.truetype(fontfile, int(size * u))
    d = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    l, t, r, b = d.textbbox((0, 0), text, font=f, stroke_width=int(sw * u))
    pad = int(16 * u)
    lay = Image.new("RGBA", (r - l + pad * 2, b - t + pad * 2), (0, 0, 0, 0))
    sh = Image.new("RGBA", lay.size, (0, 0, 0, 0))
    ImageDraw.Draw(sh).text((pad - l, pad - t + 4 * u), text, font=f, fill=SHADOW, stroke_width=int(sw * u), stroke_fill=SHADOW)
    lay = Image.alpha_composite(lay, sh.filter(ImageFilter.GaussianBlur(4 * u)))
    ImageDraw.Draw(lay).text((pad - l, pad - t), text, font=f, fill=FACE, stroke_width=int(sw * u), stroke_fill=EDGE)
    return lay

# ロゴ：photo_logo.py と同じ（緩急つき・傾きなし）。幅は看板の 82%
sys.path.insert(0, HERE)
from photo_logo import make_logo
logo = make_logo(1.0)
lw = int(W * LOGO_W); logo = logo.resize((lw, int(logo.height * lw / logo.width)), Image.LANCZOS)

ja = text_line(JA, FONTS + "YuseiMagic-Regular.ttf", 48, 6)   # 日本語は Yusei Magic（てじゃ。Cherry Bomb One・Darumadrop には漢字がない）
en = text_line(EN, FONTS + "Sniglet-ExtraBold.ttf", 32, 5)
if POS == "bottom":
    bottom = H - int(44 * u)   # 看板の枠のぶん内側
    y_en = bottom - en.height
    y_ja = y_en - ja.height + int(10 * u)
    y_logo = y_ja - logo.height + int(14 * u)
elif POS == "split":
    y_logo = int(26 * u)
    bottom = H - int(44 * u)
    y_en = bottom - en.height
    y_ja = y_en - ja.height + int(10 * u)
else:
    y_logo = int(34 * u)
    y_ja = y_logo + logo.height - int(14 * u)
    y_en = y_ja + ja.height - int(10 * u)
img.alpha_composite(logo, ((W - logo.width) // 2, y_logo))
img.alpha_composite(ja, ((W - ja.width) // 2, y_ja))
img.alpha_composite(en, ((W - en.width) // 2, y_en))
full = img.convert("RGB")
full.save(out + ".png")
full.resize((640, 812), Image.LANCZOS).save(out + "_640x812.png")
print("ok", out)
