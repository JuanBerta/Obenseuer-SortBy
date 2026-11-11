# Builds the 1300x372 Nexus header for one of JuanBerta's Obenseuer skill mods.
# Usage: python make_header.py haggling|stealth|bribery   (art/ holds the game's map and landmark icons)
import os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageEnhance

HERE = os.path.dirname(os.path.abspath(__file__)); ART = os.path.join(HERE, "art")
W, H = 1300, 372
F = lambda name, size: ImageFont.truetype(os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", name), size)
A = lambda n: Image.open(os.path.join(ART, n)).convert("RGBA")

THEMES = {
    "haggling": dict(
        title="HAGGLING", accent=(201, 154, 59), tint=(1.0, 0.92, 0.8), bright=0.45,
        lines=["A Haggling skill: trade with traders,", "level up, and get better prices."],
        chips=["Character tab skill", "Up to 12% off", "Silver Tongue perk"],
        card=("H A G G L I N G", 6, "6240 XP \u00b7 -7% buy, +7% sell"),
        note=("Trade", [("Lamp Oil", "", (226, 220, 208)), ("Price:  ", "", (226, 220, 208))], "price"),
        icons=["landmark-pawn-shop.png", "landmark-general-trader.png", "landmark-money-exhange.png"],
        foot="BepInEx 6 mod  \u00b7  works with Loot Map and Scavenger"),
    "stealth": dict(
        title="STEALTH", accent=(120, 160, 205), tint=(0.75, 0.85, 1.0), bright=0.3,
        lines=["A Stealth skill: get away with it unseen,", "level up, and get harder to notice."],
        chips=["Character tab skill", "Up to 50% less seen", "Shadow perk"],
        card=("S T E A L T H", 5, "5410 XP \u00b7 -40% seen now"),
        note=("Skill increased!", [("Stealth", "(unseen theft, crouching, in the dark)", (150, 200, 240))], "text"),
        icons=["landmark-militia.png", "landmark-security.png", "landmark-toilet.png"],
        foot="BepInEx 6 mod  \u00b7  works with Loot Map and Scavenger"),
    "bribery": dict(
        title="BRIBERY+", accent=(110, 180, 110), tint=(0.85, 1.0, 0.85), bright=0.4,
        lines=["Ask anyone for money, using the game's", "own Bribery skill. Rewards grow with level."],
        chips=["Game's Bribery skill", "OC and RM rewards", "Police pay double"],
        card=None,
        note=("Bribery+", [("Pertti Koskinen sighs and", "", (226, 220, 208)), ("hands over 27 OC.", "", (120, 214, 120))], "text"),
        prompt="[B] Ask Pertti Koskinen for money (48%)",
        icons=["landmark-bank.png", "landmark-loan-office.png", "landmark-money-exhange.png"],
        foot="BepInEx 6 mod  \u00b7  works with Haggling and Stealth"),
    "scam": dict(
        title="SCAM ARTIST", accent=(196, 120, 80), tint=(1.0, 0.85, 0.75), bright=0.38,
        lines=["Scam invoices that play out over days,", "and a Fraud skill to get better at it."],
        chips=["Fraud skill", "Odds on every invoice", "Bribe, settle or lay low"],
        card=("F R A U D", 4, "4280 XP \u00b7 3 open \u00b7 12 paid"),
        note=("Scam ledger: new letter", [("From Chamber of Commerce:", "", (226, 220, 208)), ("Payment processed: 312.40 RM.", "", (120, 214, 120))], "text"),
        icons=["landmark-bank.png", "landmark-one-stop-shop.png", "landmark-loan-office.png"],
        foot="BepInEx 6 mod  \u00b7  works with Bribery+, Stealth and Haggling"),
    "orders": dict(
        title="SPECIAL ORDERS", accent=(214, 168, 72), tint=(1.0, 0.95, 0.85), bright=0.42,
        lines=["Order the furniture and items a shop", "doesn't have in stock. Delivered by mail."],
        chips=["Ask the shopkeeper", "Rarer costs more", "Pay now or invoice"],
        card=None,
        dialogue=[("Let's trade.", False), ("I'd like to place a special order.", True), ("Goodbye.", False)],
        note=("Special order arrived", [("Malter from Ankea is waiting", "", (226, 220, 208)), ("at your mailbox.", "", (120, 214, 120))], "text"),
        icons=["landmark-commercial-furniture-shop.png", "landmark-general-trader.png", "landmark-furniture.png"],
        foot="BepInEx 6 mod  \u00b7  works with Haggling and Loot Map"),
    "sortby": dict(
        title="SORT BY", accent=(190, 150, 70), tint=(1.0, 0.93, 0.82), bright=0.4,
        lines=["Sort your inventory, the bank or any", "storage by value, name, amount and more."],
        chips=["Sort by menu", "U / Y hotkeys", "Fast on big banks"],
        card=None,
        dialogue=[("Value (stack)", True), ("Name", False), ("Amount", False)],
        note=("Sorted!", [("Bank Inventory  \u00b7  page 1/4", "", (226, 220, 208)), ("Most valuable stacks first.", "", (120, 214, 120))], "text"),
        icons=["landmark-general-trader.png", "landmark-pawn-shop.png", "landmark-one-stop-shop.png"],
        foot="BepInEx 6 mod  \u00b7  works with Bank Inventory"),
}

name = (sys.argv[1] if len(sys.argv) > 1 else "haggling").lower()
T = THEMES[name]
ACC, CREAM = T["accent"], (240, 228, 205)

# --- background: the game's map, darkened and tinted, with a dark fade on the left
m = A("tenement.jpg")
s = max(W / m.width, H / m.height) * 1.3
m = m.resize((int(m.width * s), int(m.height * s)), Image.LANCZOS)
bg = m.crop(((m.width - W) // 2, (m.height - H) // 2, (m.width - W) // 2 + W, (m.height - H) // 2 + H))
bg = bg.filter(ImageFilter.GaussianBlur(1.5))
bg = ImageEnhance.Brightness(bg).enhance(T["bright"])
r, g, b, a = bg.split()
tr, tg, tb = T["tint"]
bg = Image.merge("RGBA", (r.point(lambda v: int(v * tr)), g.point(lambda v: int(v * tg)), b.point(lambda v: int(v * tb)), a))
fade = Image.new("L", (W, H)); fd = ImageDraw.Draw(fade)
for x in range(W):
    fd.line([(x, 0), (x, H)], fill=max(0, min(245, int(245 - max(0, x - 380) * 0.55))))
img = Image.composite(Image.new("RGBA", (W, H), (16, 16, 16, 255)), bg, fade)
vig = Image.new("L", (W, H), 0); vd = ImageDraw.Draw(vig)
for i in range(60):
    vd.rectangle((i, i, W - 1 - i, H - 1 - i), outline=int(150 * (1 - i / 60)))
img = Image.composite(Image.new("RGBA", (W, H), (0, 0, 0, 255)), img, vig)
d = ImageDraw.Draw(img, "RGBA")

# --- title block
d.text((44, 50), "O B E N S E U E R", font=F("courbd.ttf", 22), fill=ACC)
ts = 82
while ts > 40 and d.textlength(T["title"], font=F("courbd.ttf", ts)) > 470: ts -= 2
d.text((38, 80 + (82 - ts) // 2), T["title"], font=F("courbd.ttf", ts), fill=CREAM)
d.line((46, 178, 452, 178), fill=ACC, width=3)
for i, line in enumerate(T["lines"]):
    d.text((46, 192 + 28 * i), line, font=F("segoeuib.ttf", 21), fill=(236, 228, 212))
x, y, cf = 46, 274, F("segoeuib.ttf", 14)
chip_fill = tuple(int(c * 0.72) for c in ACC)
for c in T["chips"]:
    w = d.textlength(c, font=cf) + 22
    d.rounded_rectangle((x, y, x + w, y + 30), radius=4, fill=chip_fill, outline=ACC)
    d.text((x + 11, y + 6), c, font=cf, fill=(255, 248, 232)); x += w + 8
d.text((46, 322), T["foot"], font=F("segoeui.ttf", 14), fill=(163, 152, 131))

# --- the game's skill card (Character tab look), or the crosshair prompt
cx, cy, cw, ch = 560, 58, 380, 96
if T["card"]:
    title, lvl, xp = T["card"]
    d.rectangle((cx + 5, cy + 6, cx + cw + 5, cy + ch + 6), fill=(0, 0, 0, 140))
    d.rectangle((cx, cy, cx + cw, cy + ch), fill=(28, 27, 25, 250), outline=(48, 46, 42), width=2)
    t = F("courbd.ttf", 19)
    d.text((cx + 16, cy + 12), title, font=t, fill=(236, 231, 222))
    lv = "LVL %d" % lvl
    d.text((cx + cw - 16 - d.textlength(lv, font=t), cy + 12), lv, font=t, fill=(236, 231, 222))
    xf = F("courbd.ttf", 12)
    d.text((cx + cw - 16 - d.textlength(xp, font=xf), cy + 36), xp, font=xf, fill=(200, 194, 184))
    seg_w, gap, sx, sy = 28, 7, cx + 16, cy + 58
    for i in range(10):
        x0 = sx + i * (seg_w + gap)
        d.rectangle((x0, sy, x0 + seg_w, sy + 22), fill=(40, 39, 36), outline=(22, 21, 19))
        fill = 1.0 if i < lvl else (0.4 if i == lvl else 0)
        if fill > 0:
            d.rectangle((x0 + 2, sy + 2, x0 + 2 + (seg_w - 4) * fill, sy + 20), fill=(214, 140, 40))
            d.line((x0 + 2, sy + 11, x0 + 2 + (seg_w - 4) * fill, sy + 11), fill=(160, 98, 22), width=2)
elif T.get("dialogue"):
    # a conversation's answer options, the special order highlighted
    d.rectangle((cx + 5, cy + 6, cx + cw + 5, cy + ch + 6), fill=(0, 0, 0, 140))
    d.rectangle((cx, cy, cx + cw, cy + ch), fill=(20, 19, 17, 235), outline=(48, 46, 42), width=2)
    of = F("segoeuib.ttf", 16)
    for i, (txt, on) in enumerate(T["dialogue"]):
        y0 = cy + 12 + i * 26
        if on: d.rectangle((cx + 8, y0 - 3, cx + cw - 8, y0 + 22), fill=ACC + (70,))
        d.text((cx + 16, y0), (("\u25B8 " if on else "   ") + txt) if name == "sortby" else (str(i + 1) + ". " + txt), font=of, fill=(255, 240, 200) if on else (200, 194, 184))
else:
    # crosshair and the hint under it
    px, py = cx + cw // 2, cy + 22
    d.ellipse((px - 4, py - 4, px + 4, py + 4), outline=(240, 240, 240), width=2)
    pf = F("segoeuib.ttf", 18)
    text = T["prompt"]
    tw = d.textlength(text, font=pf)
    d.text((px - tw / 2 + 1, py + 30 + 1), text, font=pf, fill=(0, 0, 0, 220))
    d.text((px - tw / 2, py + 30), text, font=pf, fill=(255, 255, 255))

# --- notification
nx, ny, nw, nh = 560, 196, 380, 98
d.rectangle((nx + 5, ny + 6, nx + nw + 5, ny + nh + 6), fill=(0, 0, 0, 140))
d.rectangle((nx, ny, nx + nw, ny + nh), fill=(20, 19, 17, 245), outline=ACC, width=2)
head, rows, kind = T["note"]
d.text((nx + 16, ny + 12), head, font=F("courbd.ttf", 20), fill=tuple(min(255, int(c * 1.2)) for c in ACC))
if kind == "price":
    f1, f2 = F("segoeui.ttf", 16), F("segoeuib.ttf", 18)
    d.text((nx + 16, ny + 44), "Lamp Oil  \u00b7  Trader's price", font=f1, fill=(226, 220, 208))
    old = "120 OC"; d.text((nx + 16, ny + 66), old, font=f2, fill=(150, 140, 125))
    ow = d.textlength(old, font=f2)
    d.line((nx + 14, ny + 78, nx + 18 + ow, ny + 78), fill=(220, 90, 70), width=2)
    d.text((nx + 30 + ow, ny + 66), "\u2192  111 OC", font=f2, fill=(120, 214, 120))
else:
    y = ny + 44
    for a, b, col in rows:
        f = F("segoeuib.ttf", 15)
        d.text((nx + 16, y), a, font=f, fill=col)
        if b:
            d.text((nx + 16 + d.textlength(a + " ", font=f), y), b, font=F("segoeui.ttf", 15), fill=(226, 220, 208))
        y += 22

# --- the map's landmark icons
for icon, (px, py), size, rot in zip(T["icons"], [(1000, 40), (1150, 120), (1010, 200)], [130, 120, 125], [-8, 10, -4]):
    it = A(icon).resize((size, size), Image.LANCZOS).rotate(rot, expand=True, resample=Image.BICUBIC)
    glow = Image.new("RGBA", it.size, (0, 0, 0, 0)); gl = ImageDraw.Draw(glow)
    gl.ellipse((6, 6, it.size[0] - 6, it.size[1] - 6), fill=ACC + (40,))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(12)), (px, py))
    img.alpha_composite(it, (px, py))

d = ImageDraw.Draw(img, "RGBA")
d.rectangle((0, 0, W - 1, H - 1), outline=(12, 11, 10), width=6)
d.rectangle((6, 6, W - 7, H - 7), outline=(61, 56, 47), width=1)

rgb = img.convert("RGB")
rgb.save(os.path.join(HERE, "header.jpg"), quality=92, optimize=True)
rgb.save(os.path.join(HERE, "header.png"), optimize=True)
print("header", name, rgb.size, os.path.getsize(os.path.join(HERE, "header.jpg")) // 1024, "KB")
