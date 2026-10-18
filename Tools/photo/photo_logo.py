# タイトルロゴ「FLUFF」をサムネ・看板の写真に入れる。ゲーム内のロビーのタイトル（FuwaHub/TitleBoard/Title）と同じ見た目：
# Sniglet・白い字・紺の太いふち・下にうっすら青い影、1文字ずつ大きさ・上下・傾きに緩急（2026-10-18 てじゃと決めた形。ロビーも同じ並びにした）。
# ふちの紺はロビーのタイトルの材質と同じ（ロビーは霧で明るく見えるので、材質の方を濃くしてそろえた）。
# サムネで試してやめたもの：ふわっとデリバリーの副題、雲・帯・光の背景、全体の右上がり（ゲームで急すぎた）
# VRChat のワールドのカードは 4:3 の上下を切るので、サムネではロゴを真ん中の帯に（ポータルの丸・右上の PC/Quest の印も避ける）。
# 使い方: python photo_logo.py 入力.png 出力名（拡張子なし） cx cy [大きさ]   ※cx, cy は 0〜1（画像全体に対して）
# ほかのスクリプトからは make_logo(u) で透明な画像としてもらえる（photo_poster.py）
import os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

FONTS = "D:/UnityProject/FLUFF/Assets/FuwaCourse/Fonts/"
FACE = (255, 255, 255, 255)
EDGE = (51, 82, 148, 255)
SHADOW = (61, 99, 160, 115)       # 下の影（ロビーの underlay の色・濃さ）

# 並べ方：jump＝緩急（決まった形）、straight＝まっすぐ
STYLES = {
    "jump":     dict(offs=[-6, 12, -12, 6, -14], rots=[-12, 8, -6, 10, -12], sizes=[1.2, 0.9, 1.0, 0.95, 1.12]),
    "straight": dict(offs=[0] * 5, rots=[0] * 5, sizes=[1.0] * 5),
}

def make_logo(u, style="jump", tilt=0.0):
    """u＝1200 幅の画像のときを 1 とした大きさ。透明な RGBA を返す"""
    st = STYLES[style]; text = "FLUFF"; n = len(text)
    spacing, sw, pad = int(14 * u), int(13 * u), int(40 * u)
    d = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    base = ImageFont.truetype(FONTS + "Sniglet-ExtraBold.ttf", int(150 * u))
    _, t, _, b = d.textbbox((0, 0), text, font=base, stroke_width=sw)
    fonts = [ImageFont.truetype(FONTS + "Sniglet-ExtraBold.ttf", int(150 * u * st["sizes"][i])) for i in range(n)]
    widths = [d.textlength(ch, font=fonts[i]) for i, ch in enumerate(text)]
    lay = Image.new("RGBA", (int(sum(widths) + spacing * (n - 1)) + sw * 2 + pad * 2 + int(60 * u), b - t + pad * 2 + int(90 * u)), (0, 0, 0, 0))
    x = pad + sw
    for i, (ch, w) in enumerate(zip(text, widths)):
        f = fonts[i]
        # 1文字ずつ：影＋字を作って回して置く
        cw = int(w + sw * 2 + 30 * u); chh = int(b - t + sw * 2 + 40 * u)
        one = Image.new("RGBA", (cw, chh), (0, 0, 0, 0))
        sh = Image.new("RGBA", one.size, (0, 0, 0, 0))
        ImageDraw.Draw(sh).text((sw + 15 * u, -t + sw + 20 * u + 9 * u), ch, font=f, fill=SHADOW, stroke_width=sw, stroke_fill=SHADOW)
        one = Image.alpha_composite(one, sh.filter(ImageFilter.GaussianBlur(7 * u)))
        ImageDraw.Draw(one).text((sw + 15 * u, -t + sw + 20 * u), ch, font=f, fill=FACE, stroke_width=sw, stroke_fill=EDGE)
        one = one.rotate(st["rots"][i], resample=Image.BICUBIC, expand=True)
        px = int(x + w / 2 - one.width / 2)
        py = int(pad + 45 * u + st["offs"][i] * u + (chh - one.height) / 2 - sw - 20 * u - (st["sizes"][i] - 1) * 150 * u * 0.85)
        lay.alpha_composite(one, (max(0, px), max(0, py)))
        x += w + spacing
    if tilt: lay = lay.rotate(tilt, resample=Image.BICUBIC, expand=True)
    return lay.crop(lay.getbbox())

if __name__ == "__main__":
    src, out = sys.argv[1], sys.argv[2]
    cx, cy = float(sys.argv[3]), float(sys.argv[4])
    scale = float(sys.argv[5]) if len(sys.argv) > 5 else 1.0
    img = Image.open(src).convert("RGBA")
    W, H = img.size
    logo = make_logo(W / 1200.0 * scale, os.environ.get("LOGO_STYLE", "jump"), float(os.environ.get("LOGO_TILT", "0")))
    img.alpha_composite(logo, (int(cx * W - logo.width / 2), int(cy * H - logo.height / 2)))
    full = img.convert("RGB")
    full.save(out + ".png")
    full.resize((1200, int(1200 * H / W)), Image.LANCZOS).save(out + "_1200.png")
    print("ok", out)
