"""QA contact sheets: every texture at 2-4x nearest neighbour, tiling ones repeated 2x2,
alpha shown over a dark checkerboard. Written OUTSIDE the repo (scratch dir)."""
import os

from PIL import Image, ImageDraw

from . import core


def _checker(w, h, c=8):
    im = Image.new("RGB", (w, h), (70, 20, 70))
    d = ImageDraw.Draw(im)
    for y in range(0, h, c):
        for x in range(0, w, c):
            if (x // c + y // c) % 2:
                d.rectangle([x, y, x + c - 1, y + c - 1], fill=(40, 10, 40))
    return im


def tile_preview(img, spec, max_side=512):
    w, h = img.size
    tiled = spec["tile"]
    if tiled:
        big = Image.new(img.mode, (w * 2, h * 2))
        for ox in (0, w):
            for oy in (0, h):
                big.paste(img, (ox, oy))
        img = big
        w, h = img.size
    s = max(1, min(4, max_side // max(w, h)))
    img = img.resize((w * s, h * s), Image.NEAREST)
    if img.mode == "RGBA":
        bg = _checker(*img.size)
        bg.paste(img, (0, 0), img)
        img = bg
    return img.convert("RGB")


def contact_sheets(specs, out_dir, sheet_w=1570, maxh=1600):
    os.makedirs(out_dir, exist_ok=True)
    groups = {}
    for s in specs:
        groups.setdefault(s["path"].split("/")[0], []).append(s)
    for g, items in groups.items():
        tiles = []
        for s in items:
            p = os.path.join(core.OUT_ROOT, s["path"] + ".png")
            img = Image.open(p)
            tiles.append((s, tile_preview(img, s)))
        # shelf packing
        rows, row, x, rh = [], [], 0, 0
        for s, t in tiles:
            if x + t.width > sheet_w and row:
                rows.append((row, rh))
                row, x, rh = [], 0, 0
            row.append((s, t))
            x += t.width + 6
            rh = max(rh, t.height + 14)
        if row:
            rows.append((row, rh))
        # paginate by whole rows
        pages, cur, ch = [], [], 0
        for row, rh in rows:
            if cur and ch + rh > maxh:
                pages.append(cur)
                cur, ch = [], 0
            cur.append((row, rh))
            ch += rh
        if cur:
            pages.append(cur)
        for part, page in enumerate(pages):
            H = sum(r[1] for r in page) + 4
            sheet = Image.new("RGB", (sheet_w, H), (24, 24, 28))
            d = ImageDraw.Draw(sheet)
            y = 2
            for row, rh in page:
                x = 2
                for s, t in row:
                    d.text((x, y), s["path"].split("/", 1)[1], fill=(255, 220, 0))
                    sheet.paste(t, (x, y + 12))
                    x += t.width + 6
                y += rh
            sheet.save(os.path.join(out_dir, "%s_%d.png" % (g, part)))
