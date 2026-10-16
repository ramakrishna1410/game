"""Draws the story's in-world signs (Tamil text shaped with HarfBuzz via Pillow/raqm).

python3 tools/story_art.py <NotoSansTamil-Regular.ttf> <NotoSansTamil-Bold.ttf> <out dir>
"""
import math, random, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

REG, BOLD, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
LATIN_BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

def font(path, size):
    return ImageFont.truetype(path, size, layout_engine=ImageFont.Layout.RAQM)

def centered(d, y, text, f, fill, w, **kw):
    box = d.textbbox((0, 0), text, font=f, **kw)
    d.text(((w - (box[2] - box[0])) / 2 - box[0], y), text, font=f, fill=fill, **kw)

def billboard():
    w, h = 1024, 512
    img = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):  # purple to orange flex-banner gradient
        t = y / h
        d.line([(0, y), (w, y)], fill=(int(90 + 150 * t), int(20 + 70 * t), int(120 - 80 * t)))
    for i in range(0, w, 64):  # party-flag stripes along the top
        d.rectangle([i, 0, i + 32, 26], fill=(250, 200, 40))
        d.rectangle([i + 32, 0, i + 64, 26], fill=(30, 120, 70))
    # Kumaran: white shirt, hands folded (simple portrait)
    cx, cy = 230, 300
    d.ellipse([cx - 95, cy - 20, cx + 95, cy + 260], fill=(245, 245, 240))
    d.ellipse([cx - 62, cy - 150, cx + 62, cy - 10], fill=(120, 75, 50))
    d.pieslice([cx - 66, cy - 160, cx + 66, cy - 60], 180, 360, fill=(25, 20, 20))
    d.rectangle([cx - 30, cy - 52, cx + 30, cy - 44], fill=(25, 20, 20))     # moustache
    d.polygon([(cx - 40, cy + 120), (cx, cy + 40), (cx + 40, cy + 120)], fill=(120, 75, 50))  # folded hands
    d.ellipse([cx - 110, cy - 175, cx + 110, cy + 45], outline=(250, 200, 40), width=8)
    d.text((420, 60), "புதிய காசிமேடு", font=font(BOLD, 62), fill=(255, 255, 255), language="ta")
    d.text((420, 160), "புதிய வாழ்வு", font=font(BOLD, 62), fill=(255, 230, 90), language="ta")
    d.text((420, 290), "குமரன் அண்ணன்", font=font(BOLD, 52), fill=(255, 255, 255), language="ta")
    d.text((420, 380), "உங்கள் வேட்பாளர் - கடலோர மக்கள் கட்சி", font=font(REG, 24), fill=(255, 240, 220), language="ta")
    img.save(f"{OUT}/billboard_kumaran.png")

def wall_slogan():
    w, h = 1024, 384
    img = Image.new("RGBA", (w, h), (196, 186, 164, 255))   # weathered plaster
    d = ImageDraw.Draw(img)
    random.seed(9)
    for _ in range(900):  # grime speckles
        x = random.randint(0, w - 1); y = random.randint(0, h - 1); g = random.randint(140, 190)
        d.point((x, y), fill=(g, g - 8, g - 20, 255))
    f = font(BOLD, 96)
    red = (190, 25, 25, 255)
    d.text((40, 60), "எங்க கரை", font=f, fill=red, language="ta")
    d.text((40, 200), "எங்களுக்கு!", font=f, fill=red, language="ta")
    random.seed(4)
    for _ in range(40):  # paint drips
        x = random.randint(40, 900); y = random.randint(150, 330)
        d.line([(x, y), (x, y + random.randint(10, 45))], fill=red, width=random.randint(3, 6))
    # half whitewashed over, with a rough brush edge
    wash = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    wd = ImageDraw.Draw(wash)
    for y in range(0, h, 6):
        x0 = 520 + int(30 * math.sin(y * 0.09)) + random.randint(-12, 12)
        wd.rectangle([x0, y, w, y + 6], fill=(232, 228, 215, 235))
    img = Image.alpha_composite(img, wash)
    img.save(f"{OUT}/wall_slogan.png")

def kadai_sign():
    w, h = 1024, 200
    img = Image.new("RGB", (w, h), (250, 205, 40))
    d = ImageDraw.Draw(img)
    d.rectangle([6, 6, w - 7, h - 7], outline=(150, 30, 30), width=8)
    centered(d, 28, "செல்வி இட்லி கடை", font(BOLD, 84), (150, 30, 30), w, language="ta")
    img.save(f"{OUT}/kadai_sign.png")

def velu_photo():
    w, h = 256, 320
    img = Image.new("RGB", (w, h), (70, 45, 25))
    d = ImageDraw.Draw(img)
    d.rectangle([18, 18, w - 19, h - 19], fill=(196, 170, 130))     # sepia photo
    d.ellipse([88, 60, 168, 150], fill=(110, 80, 55))
    d.rectangle([70, 150, 186, 300], fill=(225, 220, 205))
    d.rectangle([108, 112, 148, 118], fill=(40, 30, 25))
    d.rectangle([18, 280, w - 19, h - 19], fill=(150, 125, 95))
    for i in range(23):  # marigold garland hanging in a U
        t = i / 22
        x = 22 + t * (w - 44)
        y = 30 + math.sin(t * math.pi) * 150
        d.ellipse([x - 13, y - 13, x + 13, y + 13], fill=(245, 140 if i % 2 else 175, 20))
    img.save(f"{OUT}/velu_photo.png")

def neon():
    w, h = 1024, 128
    base = Image.new("RGB", (w, h), (0, 0, 0))
    glow = Image.new("RGB", (w, h), (0, 0, 0))
    f = ImageFont.truetype(LATIN_BOLD, 50)
    text = "ROYAPURAM BOXING CLUB"
    x = 95
    for i, ch in enumerate(text):
        dead = (i == text.index("X"))   # one letter burnt out
        col = (60, 20, 20) if dead else (255, 60, 60)
        ImageDraw.Draw(base).text((x, 32), ch, font=f, fill=col)
        if not dead:
            ImageDraw.Draw(glow).text((x, 32), ch, font=f, fill=(255, 40, 40))
        x += ImageDraw.Draw(base).textlength(ch, font=f) + 2
    glow = glow.filter(ImageFilter.GaussianBlur(10))
    out = Image.blend(base, glow, 0.5)
    out = Image.composite(base, out, base.convert("L").point(lambda v: 255 if v > 80 else 0))
    out.save(f"{OUT}/neon_boxing.png")

def auto_back():
    w, h = 512, 256
    img = Image.new("RGB", (w, h), (20, 20, 20))
    d = ImageDraw.Draw(img)
    centered(d, 34, "அம்மன் துணை", font(BOLD, 50), (250, 205, 40), w, language="ta")
    centered(d, 140, "KUTTY", ImageFont.truetype(LATIN_BOLD, 70), (255, 255, 255), w)
    img.save(f"{OUT}/auto_back.png")

for fn in (billboard, wall_slogan, kadai_sign, velu_photo, neon, auto_back):
    fn()
print("ok")
