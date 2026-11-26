"""고양이찾기 게임 아이콘을 그립니다.

결과:
  Assets/Images/AppIcon.png  (1024x1024, Unity 플레이어 아이콘)
  Tools/AppIcon.ico          (16~256 여러 크기, 바로가기용)

실행: python Tools/make_icon.py
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
S = 2048  # 크게 그린 뒤 줄여서 가장자리를 부드럽게


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(len(a)))


def rounded_mask(size, radius):
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, size - 1, size - 1), radius, fill=255)
    return m


def draw_icon():
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    # 배경: 하늘색 → 연보라 대각선 그라데이션, 둥근 사각형
    bg = Image.new("RGBA", (S, S))
    top, bottom = (126, 206, 244), (186, 160, 240)
    px = bg.load()
    for y in range(S):
        for x in range(0, S, 4):
            c = lerp(top, bottom, (x * 0.35 + y * 0.65) / S) + (255,)
            for k in range(4):
                px[x + k, y] = c
    img.paste(bg, (0, 0), rounded_mask(S, int(S * 0.22)))

    d = ImageDraw.Draw(img)

    # 배경 반짝이 점
    for (cx, cy, r) in [(300, 330, 34), (1760, 420, 26), (1650, 1700, 22), (380, 1650, 18), (1000, 220, 16)]:
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(255, 255, 255, 150))

    cx, cy = S // 2, int(S * 0.56)
    fur = (255, 178, 92)
    fur_dark = (232, 138, 58)
    outline = (120, 72, 40)
    ow = 28

    # 그림자
    sh = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(sh).ellipse((cx - 700, cy + 560, cx + 700, cy + 720), fill=(60, 40, 90, 90))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(40)))
    d = ImageDraw.Draw(img)

    # 귀
    def ear(sign):
        tip = (cx + sign * 520, cy - 820)
        base_out = (cx + sign * 760, cy - 180)
        base_in = (cx + sign * 160, cy - 470)
        d.polygon([tip, base_out, base_in], fill=fur, outline=outline, width=ow)
        inner = [
            (cx + sign * 515, cy - 660),
            (cx + sign * 655, cy - 290),
            (cx + sign * 290, cy - 450),
        ]
        d.polygon(inner, fill=(255, 160, 175))

    ear(-1)
    ear(1)

    # 머리
    hw, hh = 760, 600
    d.ellipse((cx - hw, cy - hh, cx + hw, cy + hh), fill=fur, outline=outline, width=ow)

    # 이마 줄무늬
    for dx, h in [(-170, 230), (0, 290), (170, 230)]:
        d.rounded_rectangle((cx + dx - 42, cy - hh + 30, cx + dx + 42, cy - hh + 30 + h), 42, fill=fur_dark)

    # 볼 줄무늬
    for sign in (-1, 1):
        for i, yy in enumerate((cy - 40, cy + 70)):
            x0 = cx + sign * (hw - 20)
            x1 = cx + sign * (hw - 170 + i * 20)
            d.line((x0, yy, x1, yy + 20), fill=fur_dark, width=46)

    # 주둥이(흰색)
    d.ellipse((cx - 330, cy + 60, cx + 10, cy + 380), fill=(255, 244, 228))
    d.ellipse((cx - 10, cy + 60, cx + 330, cy + 380), fill=(255, 244, 228))

    # 눈
    for sign in (-1, 1):
        ex, ey = cx + sign * 330, cy - 60
        d.ellipse((ex - 150, ey - 175, ex + 150, ey + 175), fill=(45, 32, 40))
        d.ellipse((ex - 105, ey - 120, ex + 105, ey + 145), fill=(70, 150, 95))
        d.ellipse((ex - 62, ey - 80, ex + 62, ey + 120), fill=(30, 22, 28))
        d.ellipse((ex - 95 + sign * 10, ey - 135, ex + 5 + sign * 10, ey - 35), fill=(255, 255, 255))
        d.ellipse((ex + 40, ey + 50, ex + 85, ey + 95), fill=(255, 255, 255, 230))

    # 코
    d.polygon([(cx - 70, cy + 110), (cx + 70, cy + 110), (cx, cy + 185)], fill=(240, 110, 130))
    d.ellipse((cx - 72, cy + 85, cx + 72, cy + 150), fill=(240, 110, 130))

    # 입 (w 모양)
    d.line((cx, cy + 180, cx, cy + 240), fill=outline, width=22)
    d.arc((cx - 140, cy + 150, cx, cy + 300), 20, 160, fill=outline, width=22)
    d.arc((cx, cy + 150, cx + 140, cy + 300), 20, 160, fill=outline, width=22)

    # 볼터치
    blush = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    bd = ImageDraw.Draw(blush)
    for sign in (-1, 1):
        bx, by = cx + sign * 470, cy + 170
        bd.ellipse((bx - 110, by - 60, bx + 110, by + 60), fill=(255, 120, 150, 140))
    img.alpha_composite(blush.filter(ImageFilter.GaussianBlur(18)))
    d = ImageDraw.Draw(img)

    # 수염
    for sign in (-1, 1):
        for k, ang in enumerate((-10, 3, 16)):
            x0 = cx + sign * 300
            y0 = cy + 200 + k * 25
            a = math.radians(ang)
            x1 = x0 + sign * 420 * math.cos(a)
            y1 = y0 + 420 * math.sin(a)
            d.line((x0, y0, x1, y1), fill=(255, 255, 255), width=16)

    # 돋보기 (오른쪽 아래)
    mx, my, mr = int(S * 0.74), int(S * 0.74), 260
    a = math.radians(45)
    hx0, hy0 = mx + (mr + 10) * math.cos(a), my + (mr + 10) * math.sin(a)
    hx1, hy1 = mx + (mr + 330) * math.cos(a), my + (mr + 330) * math.sin(a)
    d.line((hx0, hy0, hx1, hy1), fill=outline, width=150)
    d.line((hx0 + 15, hy0 + 15, hx1 - 15, hy1 - 15), fill=(150, 90, 50), width=105)
    d.ellipse((mx - mr - 40, my - mr - 40, mx + mr + 40, my + mr + 40), fill=outline)
    d.ellipse((mx - mr - 10, my - mr - 10, mx + mr + 10, my + mr + 10), fill=(255, 210, 90))
    d.ellipse((mx - mr + 40, my - mr + 40, mx + mr - 40, my + mr - 40), fill=(215, 240, 255))
    d.arc((mx - mr + 90, my - mr + 90, mx + mr - 90, my + mr - 90), 200, 260, fill=(255, 255, 255), width=40)

    # 돋보기 안의 발바닥
    pc = (240, 110, 130)
    d.ellipse((mx - 85, my - 5, mx + 85, my + 135), fill=pc)
    for (tx, ty) in [(-115, -70), (-40, -125), (40, -125), (115, -70)]:
        d.ellipse((mx + tx - 40, my + ty - 48, mx + tx + 40, my + ty + 48), fill=pc)

    return img


def main():
    big = draw_icon()
    icon = big.resize((1024, 1024), Image.LANCZOS)

    png_path = os.path.join(ROOT, "Assets", "Images", "AppIcon.png")
    icon.save(png_path)

    ico_path = os.path.join(ROOT, "Tools", "AppIcon.ico")
    sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    icon.resize((256, 256), Image.LANCZOS).save(ico_path, sizes=sizes)
    print("saved", png_path, ico_path)


if __name__ == "__main__":
    main()
