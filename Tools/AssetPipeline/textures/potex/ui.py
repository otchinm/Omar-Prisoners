"""Textures/UI - bitmap fonts (+ JSON), title logos, cursor / arrows / panel, item icons, note paper,
vignettes, the fake VHS cover and the ending stills."""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from .core import (FONT_BOLD, FONT_DIR, FONT_PIXEL, OUT_ROOT, blur, inner_rim, outer_rim, canvas, clamp01, col, degrade, drips,
                   ellipse_mask, fft_noise, font, gradient_map, lum, mix, radial, resize, rng_for, saturate,
                   smoothstep, solid, splat_mask, text_mask, texture, to_image_rgba, to_mask, warp, water_stains)
from .mat import grime, photo

FONT_ROAD = "Overpass-ExtraBold.woff"
SPECIALS = "▶◀■●▲▼"  # ▶ ◀ ■ ● ▲ ▼


# ======================================================================================
# bitmap fonts
# ======================================================================================
def _pot(n):
    p = 1
    while p < n:
        p *= 2
    return p


def font_layout(size, cols, pad=1, thr=110):
    """measure VT323 at `size` px and compute the atlas layout (deterministic, cheap)"""
    f = ImageFont.truetype(os.path.join(FONT_DIR, FONT_PIXEL), size)
    adv = int(round(f.getlength("M")))
    tops, bots, rights = [], [], []
    for c in range(32, 127):
        g = Image.new("L", (size * 3, size * 3), 0)
        ImageDraw.Draw(g).text((size, size), chr(c), font=f, fill=255)
        a = np.asarray(g) > thr
        if a.any():
            ys, xs = np.nonzero(a)
            tops.append(ys.min() - size)
            bots.append(ys.max() - size)
            rights.append(xs.max() - size)
    top, bot = min(tops), max(bots)
    gw = max(adv, max(rights) + 1)
    gh = bot - top + 1
    asc, desc = f.getmetrics()
    cw, chh = gw + 2 * pad, gh + 2 * pad
    n = 95 + len(SPECIALS)
    rows = (n + cols - 1) // cols
    return dict(size=size, font=f, adv=adv, top=top, gw=gw, gh=gh, cw=cw, ch=chh, pad=pad, cols=cols, rows=rows,
                atlasW=_pot(cols * cw), atlasH=_pot(rows * chh), baseline=pad + (asc - top), thr=thr,
                cap_top=None)


def special_glyph(ch, gw, gh, cap0, cap1, ss=8):
    """hand-drawn ▶ ◀ ■ ● ▲ ▼ in a gw x gh box, vertically centred on the cap band [cap0, cap1]"""
    W, H = gw * ss, gh * ss
    im = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(im)
    cy = (cap0 + cap1 + 1) / 2 * ss
    hh = (cap1 - cap0 + 1) * ss
    s = min(gw * ss * 1.0, hh * 0.95)
    cx = W / 2
    if ch == "▶":
        d.polygon([(cx - s * 0.4, cy - s / 2), (cx + s * 0.45, cy), (cx - s * 0.4, cy + s / 2)], fill=255)
    elif ch == "◀":
        d.polygon([(cx + s * 0.4, cy - s / 2), (cx - s * 0.45, cy), (cx + s * 0.4, cy + s / 2)], fill=255)
    elif ch == "■":
        q = s * 0.36
        d.rectangle([cx - q, cy - q, cx + q, cy + q], fill=255)
    elif ch == "●":
        q = s * 0.42
        d.ellipse([cx - q, cy - q, cx + q, cy + q], fill=255)
    elif ch == "▲":
        d.polygon([(cx, cy - s * 0.4), (cx + s / 2, cy + s * 0.4), (cx - s / 2, cy + s * 0.4)], fill=255)
    elif ch == "▼":
        d.polygon([(cx, cy + s * 0.4), (cx + s / 2, cy - s * 0.4), (cx - s / 2, cy - s * 0.4)], fill=255)
    a = np.asarray(im, np.float32).reshape(gh, ss, gw, ss).mean(axis=(1, 3)) / 255.0
    return (a >= 0.5).astype(np.uint8) * 255


def build_font(L, name):
    f = L["font"]
    glyphs = "".join(chr(c) for c in range(32, 127)) + SPECIALS
    atlas = np.zeros((L["atlasH"], L["atlasW"]), np.uint8)
    # cap band for special glyphs (from 'H')
    g = Image.new("L", (L["size"] * 3, L["size"] * 3), 0)
    ImageDraw.Draw(g).text((L["size"], L["size"]), "H", font=f, fill=255)
    ys = np.nonzero(np.asarray(g) > L["thr"])[0]
    cap0, cap1 = ys.min() - L["size"] - L["top"], ys.max() - L["size"] - L["top"]
    for i, ch in enumerate(glyphs):
        cx, cy = (i % L["cols"]) * L["cw"], (i // L["cols"]) * L["ch"]
        if ch in SPECIALS:
            a = special_glyph(ch, L["gw"], L["gh"], cap0, cap1)
        else:
            gim = Image.new("L", (L["gw"] + L["size"], L["gh"] + L["size"]), 0)
            ImageDraw.Draw(gim).text((0, -L["top"]), ch, font=f, fill=255)
            a = (np.asarray(gim)[:L["gh"], :L["gw"]] > L["thr"]).astype(np.uint8) * 255
        atlas[cy + L["pad"]:cy + L["pad"] + L["gh"], cx + L["pad"]:cx + L["pad"] + L["gw"]] = a
    return write_font(atlas, L, glyphs, name, "VT323 (SIL OFL 1.1) %dpx, thresholded" % L["size"])


def write_font(atlas, L, glyphs, name, source):
    """white RGBA atlas from a 0/255 coverage array + the JSON metrics file next to it"""
    rgba = np.zeros((L["atlasH"], L["atlasW"], 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = atlas
    meta = {
        "cellW": L["cw"], "cellH": L["ch"], "cols": L["cols"], "rows": L["rows"], "first": 32,
        "glyphs": glyphs, "advance": L["adv"], "lineHeight": L.get("line", L["gh"] + 2),
        "atlasW": L["atlasW"], "atlasH": L["atlasH"],
        "padding": L["pad"], "glyphW": L["gw"], "glyphH": L["gh"], "baseline": L["baseline"],
        "source": source,
        "note": "glyph i is the cellW x cellH rect at (i % cols * cellW, i / cols * cellH) from the TOP-LEFT of the atlas; "
                "its ink starts at (padding, padding) inside the cell. Draw the cell at (penX - padding, penY - padding) "
                "and advance penX by 'advance'; new line = lineHeight. Unknown chars -> '?'.",
    }
    meta = {k: (int(v) if isinstance(v, (np.integer,)) else v) for k, v in meta.items()}
    out_json = os.path.join(OUT_ROOT, "UI", name + ".json")
    os.makedirs(os.path.dirname(out_json), exist_ok=True)
    with open(out_json, "w", encoding="utf-8") as fh:
        json.dump(meta, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    return Image.fromarray(rgba, "RGBA")


FONT_SMALL = font_layout(20, 16)
FONT_BIG = font_layout(40, 28)


@texture("UI/font_vhs", (FONT_SMALL["atlasW"], FONT_SMALL["atlasH"]), k=1, alpha="hard")
def font_vhs(ctx):
    return build_font(FONT_SMALL, "font_vhs")


@texture("UI/font_vhs_big", (FONT_BIG["atlasW"], FONT_BIG["atlasH"]), k=1, alpha="hard")
def font_vhs_big(ctx):
    return build_font(FONT_BIG, "font_vhs_big")


def compact_layout(cols=16, pad=1):
    from . import pixelfont as pf
    cw, chh = pf.GW + 2 * pad, pf.GH + 2 * pad
    n = 95 + len(SPECIALS)
    rows = (n + cols - 1) // cols
    return dict(gw=pf.GW, gh=pf.GH, cw=cw, ch=chh, pad=pad, cols=cols, rows=rows, adv=pf.GW + 1,
                line=pf.GH + 2, atlasW=_pot(cols * cw), atlasH=_pot(rows * chh), baseline=pad + pf.CAP)


FONT_COMPACT = compact_layout()


@texture("UI/font_vhs_small", (FONT_COMPACT["atlasW"], FONT_COMPACT["atlasH"]), k=1, alpha="hard")
def font_vhs_small(ctx):
    """compact hand-made 5x7 VCR/OSD pixel font (5x9 with descenders) for dense screens"""
    from . import pixelfont as pf
    L = FONT_COMPACT
    glyphs = "".join(chr(c) for c in range(32, 127)) + SPECIALS
    atlas = np.zeros((L["atlasH"], L["atlasW"]), np.uint8)
    for i, ch in enumerate(glyphs):
        cx, cy = (i % L["cols"]) * L["cw"] + L["pad"], (i // L["cols"]) * L["ch"] + L["pad"]
        atlas[cy:cy + L["gh"], cx:cx + L["gw"]] = pf.glyph_bitmap(ch)
    return write_font(atlas, L, glyphs, "font_vhs_small", "hand-made 5x7 VCR/OSD pixel font (potex/pixelfont.py)")


# ======================================================================================
# small UI pieces
# ======================================================================================
def pixel_sprite(rows, palette):
    """rows of chars -> RGBA image; '.' transparent"""
    h, w = len(rows), len(rows[0])
    out = np.zeros((h, w, 4), np.uint8)
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c != ".":
                rgb = (col(palette[c]) * 255).astype(np.uint8)
                out[y, x, :3] = rgb
                out[y, x, 3] = 255
    return Image.fromarray(out, "RGBA")


@texture("UI/cursor", (16, 16), k=1, alpha="hard")
def cursor(ctx):
    rows = [
        "K...............",
        "KK..............",
        "KWK.............",
        "KWWK............",
        "KWWWK...........",
        "KWWWWK..........",
        "KWWWWWK.........",
        "KWWWWWWK........",
        "KWWWWWWWK.......",
        "KWWWWWWWWK......",
        "KWWWWWKKKKK.....",
        "KWWKWWK.........",
        "KWK.KWWK........",
        "KK..KWWK........",
        "K....KWWK.......",
        ".....KKK........",
    ]
    return pixel_sprite(rows, {"K": "#000000", "W": "#e8e8e8"})


def tri(direction):
    W = H = 16 * 8
    im = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(im)
    if direction == "right":
        d.polygon([(3 * 8, 1 * 8), (13 * 8, 8 * 8), (3 * 8, 15 * 8)], fill=255)
    else:
        d.polygon([(13 * 8, 1 * 8), (3 * 8, 8 * 8), (13 * 8, 15 * 8)], fill=255)
    a = (np.asarray(im, np.float32).reshape(16, 8, 16, 8).mean(axis=(1, 3)) >= 128).astype(np.uint8) * 255
    out = np.zeros((16, 16, 4), np.uint8)
    out[..., :3] = 255
    out[..., 3] = a
    return Image.fromarray(out, "RGBA")


@texture("UI/arrow_left", (16, 16), k=1, alpha="hard")
def arrow_left(ctx):
    return tri("left")


@texture("UI/arrow_right", (16, 16), k=1, alpha="hard")
def arrow_right(ctx):
    return tri("right")


@texture("UI/panel", (32, 32), k=1, alpha="soft")
def panel(ctx):
    """9-slice dark translucent panel: 4 px border (use 4,4,4,4 slice margins)"""
    r = ctx.sub(1)
    n = 32
    a = np.full((n, n), 0.78, np.float32)
    rgb = np.zeros((n, n, 3), np.float32) + col("#060606")
    yy, xx = np.mgrid[0:n, 0:n]
    d = np.minimum(np.minimum(xx, n - 1 - xx), np.minimum(yy, n - 1 - yy))
    rgb[d == 1] = col("#8a8a86")       # light frame line
    a[d == 1] = 1.0
    rgb[d == 0] = col("#000000")       # dark outer line
    a[d == 0] = 0.9
    rgb[d == 2] = col("#1a1a1a")
    a[d == 2] = 0.9
    # cut corners
    for (cx, cy) in ((0, 0), (n - 1, 0), (0, n - 1), (n - 1, n - 1)):
        a[cy, cx] = 0.0
    # faint scanlines in the fill
    fill = d >= 3
    rgb[fill] *= 1.0
    a[fill & (yy % 2 == 0)] = 0.72
    return to_image_rgba(rgb, a)


# ======================================================================================
# item icons (32x32)
# ======================================================================================
ICON_DEG = dict(jpeg=False, bits=5, dither=0.0, sharpen=0.3, desat=0.0, dark=1.0, maxv=1.0)


def icon_finish(img, a, outline=True):
    """returns (rgb, alpha) at 8x with a dark outline ring for readability"""
    if outline:
        H, W = a.shape
        grow = (blur(a, 7.0) > 0.08).astype(np.float32)
        ring = clamp01(grow - a)
        img = img * a[..., None] + col("#0a0806")[None, None, :] * ring[..., None]
        a = clamp01(a + ring)
    return img, a


def shade_lr(H, W, x0, x1, amt=0.35):
    """cylinder-ish horizontal shading between x0..x1 (px)"""
    xx = np.arange(W, dtype=np.float32)
    t = np.clip((xx - x0) / max(1.0, x1 - x0), 0, 1)
    return (1.0 + amt * np.cos(t * np.pi * 0.9 + 0.3))[None, :] * np.ones((H, 1), np.float32)


def poly(W, H, pts, rot=0.0, cx=None, cy=None):
    cx = W / 2 if cx is None else cx
    cy = H / 2 if cy is None else cy
    c, s = np.cos(rot), np.sin(rot)
    q = [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in pts]
    im, d = canvas(W, H)
    d.polygon(q, fill=255)
    return to_mask(im)


def rrect(W, H, x0, y0, x1, y1, rad, rot=0.0):
    im, d = canvas(W, H)
    d.rounded_rectangle([x0, y0, x1, y1], radius=rad, fill=255)
    m = im
    if rot:
        m = m.rotate(np.degrees(-rot), resample=Image.BILINEAR, center=(W / 2, H / 2))
    return (to_mask(m) > 0.5).astype(np.float32)


def grad_v(H, W, c0, c1):
    t = np.linspace(0, 1, H, dtype=np.float32)[:, None] * np.ones((1, W), np.float32)
    return gradient_map(t, [(0, c0), (1, c1)])


def tex_noise(ctx, H, W, amt=0.06, salt=0):
    return 1.0 + amt * fft_noise(ctx.sub(900 + salt), H, W, beta=1.6)


def icon(name):
    def deco(fn):
        @texture("UI/icon_" + name, (32, 32), k=8, alpha="hard", **ICON_DEG)
        def _f(ctx):
            img, a = fn(ctx)
            return icon_finish(img, a)
        _f.__name__ = "icon_" + name
        return _f
    return deco


@icon("lighter")
def _lighter(ctx):
    """Slim upright Zippo like the first-person view: olive case, pale brass perforated chimney, blue flint wheel,
    open lid, the pixel flame tongue."""
    W = H = 256
    body = rrect(W, H, 84, 104, 160, 244, 12)
    img = solid(H, W, "#5c662e") * shade_lr(H, W, 84, 160, 0.45)[..., None] * tex_noise(ctx, H, W, 0.1)[..., None]
    rim = rrect(W, H, 87, 96, 157, 108, 3)
    img = mix(img, "#c8bc84", rim)
    chim = rrect(W, H, 90, 50, 134, 98, 4)
    img = mix(img, "#d8cf9a", chim)
    for hx in (99, 113):
        for hy in (60, 74, 88):
            img = mix(img, "#1a120a", ellipse_mask(W, H, hx + (7 if hy == 74 else 0), hy, 5, 4) * chim)
    wheel = ellipse_mask(W, H, 146, 78, 10, 10)
    img = mix(img, "#1c2a6a", wheel)
    # open lid hanging on the right
    lid = rrect(W, H, 162, 50, 192, 104, 8, rot=0.35)
    img = mix(img, "#4e5828", lid)
    flame = ellipse_mask(W, H, 112, 26, 11, 30)
    img = mix(img, "#ffa030", flame)
    img = mix(img, "#fff4c8", ellipse_mask(W, H, 112, 32, 6, 20))
    a = clamp01(body + rim + chim + wheel + lid + flame)
    return img, a


@icon("revolver")
def _revolver(ctx):
    W = H = 256
    rot = -0.1
    barrel = rrect(W, H, 96, 92, 236, 112, 4, rot)
    frame = rrect(W, H, 60, 84, 132, 132, 8, rot)
    cyl = rrect(W, H, 92, 80, 140, 138, 10, rot)
    grip = poly(W, H, [(58, 116), (96, 120), (82, 214), (34, 204)], rot)
    hammer = poly(W, H, [(52, 82), (68, 70), (76, 88)], rot)
    guard = rrect(W, H, 92, 128, 120, 156, 10, rot) * (1 - rrect(W, H, 98, 132, 114, 150, 6, rot))
    img = solid(H, W, "#34373c") * tex_noise(ctx, H, W, 0.1)[..., None]
    img = mix(img, "#565a60", cyl)
    for k in range(3):
        img = mix(img, "#1e2024", rrect(W, H, 100 + k * 14, 86, 104 + k * 14, 132, 2, rot) * cyl)
    img = mix(img, "#5a3420", grip * tex_noise(ctx, H, W, 0.2, 3))
    img = mix(img, "#8a8e94", ((np.arange(H) > 93) & (np.arange(H) < 97))[:, None] * np.ones((1, W)) * barrel)
    a = clamp01(barrel + frame + cyl + grip + hammer + guard)
    return img, a


@icon("lighterfuel")
def _lighterfuel(ctx):
    W = H = 256
    can = rrect(W, H, 70, 60, 186, 236, 10)
    img = solid(H, W, "#1e3a8a") * shade_lr(H, W, 70, 186, 0.4)[..., None]
    band = can * ((np.arange(H) > 100) & (np.arange(H) < 168))[:, None]
    img = mix(img, "#e0c020", band)
    f = font(FONT_BOLD, 40)
    t1 = text_mask(W, H, ["LIGHTER", "FUEL"], f, box=(80, 106, 176, 162), spacing=2)
    img = mix(img, "#1e3a8a", t1 * band)
    top = rrect(W, H, 80, 44, 176, 66, 6)
    img = mix(img, "#a8a8a0", top)
    spout = poly(W, H, [(150, 46), (162, 46), (160, 8), (154, 8)])
    img = mix(img, "#c01818", spout)
    a = clamp01(can + top + spout)
    return img * tex_noise(ctx, H, W)[..., None], a


@icon("bandages")
def _bandages(ctx):
    W = H = 256
    box = rrect(W, H, 50, 70, 206, 210, 8, rot=-0.15)
    img = solid(H, W, "#e0dcd0") * tex_noise(ctx, H, W)[..., None]
    side = rrect(W, H, 50, 196, 206, 222, 4, rot=-0.15)
    img = mix(img, "#a8a49a", side)
    cross = clamp01(rrect(W, H, 112, 92, 144, 188, 3, rot=-0.15) + rrect(W, H, 80, 124, 176, 156, 3, rot=-0.15))
    img = mix(img, "#c01814", cross)
    strip = rrect(W, H, 60, 76, 196, 90, 3, rot=-0.15)
    img = mix(img, "#1e4aa0", strip)
    return img, clamp01(box + side)


@icon("flashlight")
def _flashlight(ctx):
    W = H = 256
    rot = -0.75
    body = rrect(W, H, 40, 104, 170, 152, 10, rot)
    head = rrect(W, H, 160, 90, 216, 166, 8, rot)
    img = solid(H, W, "#2a2a2c") * tex_noise(ctx, H, W, 0.1)[..., None]
    img = mix(img, "#4a4a4e", head)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # highlight stripe along the body
    c, s = np.cos(rot), np.sin(rot)
    v = -(xx - W / 2) * s + (yy - H / 2) * c
    img = mix(img, "#8a8a90", ((v > -20) & (v < -12)).astype(np.float32) * clamp01(body + head) * 0.7)
    lens_c = (W / 2 + (188 - W / 2) * c - (128 - H / 2) * s, H / 2 + (188 - W / 2) * s + (128 - H / 2) * c)
    lens = ellipse_mask(W, H, lens_c[0] + 22, lens_c[1] - 22, 22, 34) * 0
    sw = rrect(W, H, 100, 96, 122, 108, 3, rot)
    img = mix(img, "#c02020", sw)
    a = clamp01(body + head)
    # lens face
    face = poly(W, H, [(214, 92), (222, 100), (222, 158), (214, 166)], rot)
    img = mix(img, "#f0e8a0", face)
    return img, clamp01(a + face)


@icon("batteries")
def _batteries(ctx):
    W = H = 256
    img = np.zeros((H, W, 3), np.float32)
    a = np.zeros((H, W), np.float32)
    for i, (ox, rot) in enumerate(((-34, -0.5), (34, -0.5))):
        body = rrect(W, H, 104 + ox, 50, 152 + ox, 200, 8, rot)
        cap = rrect(W, H, 104 + ox, 50, 152 + ox, 84, 6, rot)
        nub = rrect(W, H, 120 + ox, 36, 136 + ox, 54, 3, rot)
        img = mix(img, "#1a1a1a", body)
        img = mix(img, "#b0702a", cap)
        img = mix(img, "#c8c8c0", nub)
        yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
        img = img * (1.0 + 0.0 * xx)[..., None]
        a = clamp01(a + body + nub)
    img = img * tex_noise(ctx, H, W)[..., None]
    img = mix(img, "#d0d0d0", np.zeros((H, W), np.float32))
    return img, a


@icon("soundmeter")
def _soundmeter(ctx):
    W = H = 256
    body = rrect(W, H, 64, 24, 192, 236, 12)
    img = solid(H, W, "#3a3c3e") * tex_noise(ctx, H, W, 0.08)[..., None] * shade_lr(H, W, 64, 192, 0.25)[..., None]
    face = rrect(W, H, 80, 40, 176, 120, 6)
    img = mix(img, "#d8d4c4", face)
    im, d = canvas(W, H)
    for k in range(7):
        ang = -1.0 + k * 0.33
        d.line([(128 + np.sin(ang) * 36, 112 - np.cos(ang) * 36), (128 + np.sin(ang) * 46, 112 - np.cos(ang) * 46)], fill=255, width=4)
    d.line([(128, 112), (150, 64)], fill=255, width=6)
    img = mix(img, "#1a1a1a", to_mask(im) * face)
    img = mix(img, "#c02020", rrect(W, H, 140, 104, 170, 114, 2) * face)
    for ky in (150, 192):
        img = mix(img, "#101010", ellipse_mask(W, H, 104, ky, 14, 14))
        img = mix(img, "#7a7a7a", ellipse_mask(W, H, 100, ky - 4, 5, 5))
    img = mix(img, "#40c040", ellipse_mask(W, H, 160, 150, 8, 8))
    for gy in range(176, 220, 10):
        img = mix(img, "#151515", rrect(W, H, 140, gy, 178, gy + 4, 2))
    return img, body


@icon("boltcutters")
def _boltcutters(ctx):
    W = H = 256
    im, d = canvas(W, H)
    cim, cd = canvas(W, H)
    # two handles from bottom-left to the pivot, jaws to the top-right
    d.line([(30, 200), (140, 116)], fill=255, width=22)
    d.line([(56, 226), (150, 128)], fill=255, width=22)
    cd.line([(30, 200), (122, 130)], fill=255, width=22)
    cd.line([(56, 226), (134, 144)], fill=255, width=22)
    d.polygon([(132, 108), (200, 40), (214, 56), (156, 132)], fill=255)
    d.polygon([(140, 130), (216, 70), (226, 86), (160, 146)], fill=255)
    d.ellipse([132, 108, 166, 142], fill=255)
    a = to_mask(im)
    grips = to_mask(cim)
    img = solid(H, W, "#9a9c9e") * tex_noise(ctx, H, W, 0.1)[..., None]
    img = mix(img, "#b01818", grips)
    img = mix(img, "#3a3a3a", ellipse_mask(W, H, 149, 125, 7, 7))
    return img, a


@icon("carkeys")
def _carkeys(ctx):
    W = H = 256
    ring = clamp01(ellipse_mask(W, H, 70, 70, 44, 44) - ellipse_mask(W, H, 70, 70, 32, 32))
    head = rrect(W, H, 96, 92, 156, 150, 16, -0.78)
    blade = poly(W, H, [(140, 132), (226, 218), (214, 230), (200, 216), (190, 226), (128, 152)])
    fob = rrect(W, H, 20, 110, 70, 196, 12, 0.3)
    img = solid(H, W, "#b8b8b0") * tex_noise(ctx, H, W, 0.08)[..., None]
    img = mix(img, "#181818", head)
    img = mix(img, "#b09040", blade)
    img = mix(img, "#7a1a14", fob)
    img = mix(img, "#d0d0c8", ellipse_mask(W, H, 112, 108, 6, 6) * head)
    return img, clamp01(ring + head + blade + fob)


@icon("gascan")
def _gascan(ctx):
    W = H = 256
    can = poly(W, H, [(48, 80), (92, 50), (208, 50), (208, 232), (48, 232)])
    img = solid(H, W, "#b01a12") * tex_noise(ctx, H, W, 0.1)[..., None] * shade_lr(H, W, 48, 208, 0.35)[..., None]
    handle = clamp01(rrect(W, H, 120, 22, 200, 56, 10) - rrect(W, H, 134, 32, 186, 46, 4))
    img = mix(img, "#8a120c", handle)
    spout = poly(W, H, [(52, 82), (24, 30), (36, 22), (72, 66)])
    img = mix(img, "#c8a020", spout)
    emb = clamp01(poly(W, H, [(80, 110), (180, 200), (172, 210), (72, 120)]) + poly(W, H, [(180, 110), (80, 200), (72, 190), (172, 100)]))
    img = mix(img, "#7a0e0a", emb * 0.7)
    return img, clamp01(can + handle + spout)


@icon("carbattery")
def _carbattery(ctx):
    W = H = 256
    box = rrect(W, H, 28, 84, 228, 226, 8)
    lid = rrect(W, H, 28, 72, 228, 104, 6)
    img = solid(H, W, "#202022") * tex_noise(ctx, H, W, 0.1)[..., None]
    img = mix(img, "#2e2e30", lid)
    pos = rrect(W, H, 52, 48, 84, 76, 4)
    neg = rrect(W, H, 172, 48, 204, 76, 4)
    img = mix(img, "#c02020", pos)
    img = mix(img, "#3a3a3a", neg)
    lab = rrect(W, H, 52, 128, 204, 196, 4)
    img = mix(img, "#d0c890", lab)
    f = font(FONT_BOLD, 40)
    img = mix(img, "#1a1a1a", text_mask(W, H, ["12V"], f, box=(70, 136, 186, 188)) * lab)
    img = mix(img, "#ffffff", text_mask(W, H, ["+"], f, box=(56, 52, 80, 72)) * pos)
    return img, clamp01(box + lid + pos + neg)


@icon("fuse")
def _fuse(ctx):
    W = H = 256
    rot = -0.78
    glass = rrect(W, H, 72, 100, 184, 156, 10, rot)
    cap1 = rrect(W, H, 32, 96, 84, 160, 8, rot)
    cap2 = rrect(W, H, 172, 96, 224, 160, 8, rot)
    img = solid(H, W, "#7a8a90")
    img = mix(img, "#c8d4d8", glass * 0.6)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    wire = (np.abs((xx - W / 2) + (yy - H / 2)) < 5).astype(np.float32) * glass
    img = mix(img, "#40403a", wire)
    for cm in (cap1, cap2):
        img = mix(img, "#b0b0a8", cm)
    img = img * tex_noise(ctx, H, W, 0.06)[..., None]
    return img, clamp01(glass + cap1 + cap2)


@icon("cagekey")
def _cagekey(ctx):
    W = H = 256
    bow = clamp01(ellipse_mask(W, H, 64, 64, 46, 46) - ellipse_mask(W, H, 64, 64, 24, 24))
    shaft = poly(W, H, [(88, 80), (100, 68), (216, 184), (204, 196)])
    bit = poly(W, H, [(176, 168), (196, 148), (230, 182), (214, 196), (200, 186), (190, 196)])
    img = rust_icon(ctx, H, W)
    return img, clamp01(bow + shaft + bit)


def rust_icon(ctx, H, W):
    t = clamp01(0.5 + 0.3 * fft_noise(ctx.sub(5), H, W, beta=1.8))
    return gradient_map(t, [(0, "#3a2010"), (0.5, "#7a4a24"), (1, "#a8784a")])


@icon("lockpick")
def _lockpick(ctx):
    W = H = 256
    im, d = canvas(W, H)
    d.line([(40, 210), (200, 50)], fill=255, width=10)
    d.line([(200, 50), (214, 50), (222, 38)], fill=255, width=10)
    d.line([(70, 226), (220, 86)], fill=255, width=9)
    d.line([(220, 86), (228, 70)], fill=255, width=9)
    a = to_mask(im)
    grip = clamp01(rrect(W, H, 20, 176, 90, 206, 8, -0.78) + rrect(W, H, 50, 196, 116, 222, 8, -0.75))
    img = solid(H, W, "#c0c0c4")
    img = mix(img, "#202020", grip)
    return img, clamp01(a + grip)


@icon("screwdriver")
def _screwdriver(ctx):
    W = H = 256
    # diagonal: fat fluted amber handle bottom-left, steel shaft up to a flat tip top-right
    handle = rrect(W, H, 14, 104, 152, 152, 20, -0.785)
    im, d = canvas(W, H)
    d.line([(136, 120), (224, 32)], fill=255, width=15)
    d.polygon([(214, 30), (234, 20), (240, 38), (226, 44)], fill=255)
    shaft = clamp01(to_mask(im) - handle)
    img = solid(H, W, "#b0501c") * tex_noise(ctx, H, W, 0.1)[..., None]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    across = (xx + yy)                        # flutes run along the handle (constant along x - y)
    along = (xx - yy)
    flutes = (np.sin(along * 0.2) > 0.4).astype(np.float32) * handle
    img = mix(img, "#4a1a08", flutes * 0.75)
    img = mix(img, "#e09a58", (np.sin(along * 0.2 + 1.5) > 0.92).astype(np.float32) * handle * 0.5)
    img = mix(img, "#a8aaae", shaft)
    img = mix(img, "#e6e6e8", (np.abs(along + 0.0 * across - 8) < 3).astype(np.float32) * shaft * 0.7)  # glint
    return img, clamp01(handle + shaft)


@icon("crowbar")
def _crowbar(ctx):
    W = H = 256
    im, d = canvas(W, H)
    d.line([(52, 220), (196, 60)], fill=255, width=22)
    d.arc([168, 20, 236, 88], start=200, end=360, fill=255, width=22)
    d.polygon([(40, 214), (66, 236), (34, 244)], fill=255)
    a = to_mask(im)
    img = solid(H, W, "#a01414") * tex_noise(ctx, H, W, 0.12)[..., None]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    img = mix(img, "#1a1a1a", ((xx + yy) > 360).astype(np.float32) * 0.0)
    img = mix(img, "#e06a5a", (np.abs((xx - 52) * 160 / 144 + (yy - 220)) < 4).astype(np.float32) * a * 0.6)
    return img, a


@icon("bottle")
def _bottle(ctx):
    W = H = 256
    body = rrect(W, H, 82, 100, 174, 238, 20)
    neck = rrect(W, H, 112, 26, 144, 110, 8)
    img = solid(H, W, "#2a4a1e") * shade_lr(H, W, 82, 174, 0.5)[..., None]
    img = mix(img, "#8ab070", ((np.arange(W) > 98) & (np.arange(W) < 108))[None, :] * clamp01(body + neck) * 0.7)
    lab = rrect(W, H, 84, 140, 172, 196, 2)
    img = mix(img, "#c8b880", lab)
    img = mix(img, "#7a1a14", rrect(W, H, 96, 154, 160, 182, 2) * 0.8)
    cap = rrect(W, H, 108, 18, 148, 36, 4)
    img = mix(img, "#a89a50", cap)
    return img, clamp01(body + neck + cap)


@icon("pills")
def _pills(ctx):
    W = H = 256
    bot = rrect(W, H, 60, 76, 168, 228, 12)
    cap = rrect(W, H, 52, 40, 176, 84, 8)
    img = solid(H, W, "#c86a14") * shade_lr(H, W, 60, 168, 0.4)[..., None]
    img = mix(img, "#e8e4d8", cap)
    lab = rrect(W, H, 62, 120, 166, 190, 2)
    img = mix(img, "#f0ece0", lab)
    for ly in (134, 150, 166):
        img = mix(img, "#3a3a3a", rrect(W, H, 74, ly, 152, ly + 6, 2) * 0.8)
    p1 = ellipse_mask(W, H, 196, 206, 22, 13)
    p2 = ellipse_mask(W, H, 212, 172, 13, 13)
    img = mix(img, "#f0f0e8", clamp01(p1 + p2))
    img = mix(img, "#b0b0a8", (np.abs(np.mgrid[0:H, 0:W][1] - 196) < 2).astype(np.float32) * p1)
    return img, clamp01(bot + cap + p1 + p2)


@icon("backpack")
def _backpack(ctx):
    """(iteration 3) Olive canvas rucksack seen from the front: top flap with two leather straps and buckles,
    big front pocket, side pockets, a grab loop."""
    W = H = 256
    body = rrect(W, H, 58, 56, 198, 236, 26)
    sides = clamp01(rrect(W, H, 36, 128, 70, 226, 12) + rrect(W, H, 186, 128, 220, 226, 12))
    flap = rrect(W, H, 62, 44, 194, 124, 22)
    pocket = rrect(W, H, 82, 150, 174, 226, 10)
    loop = rrect(W, H, 108, 18, 148, 52, 14) * (1 - rrect(W, H, 118, 28, 138, 52, 8))
    img = solid(H, W, "#5c5c34") * tex_noise(ctx, H, W, 0.14)[..., None]
    img = mix(img, "#4a4a2a", sides)
    img = mix(img, "#40411f", flap)
    img = mix(img, "#56562f", pocket)
    img = mix(img, "#2c2c18", rrect(W, H, 82, 150, 174, 166, 4) * 0.8)          # pocket flap seam
    for x in (92, 150):
        img = mix(img, "#5a3a1e", rrect(W, H, x, 92, x + 16, 162, 3))           # leather straps
        img = mix(img, "#a8a490", rrect(W, H, x - 3, 140, x + 19, 154, 3))      # buckles
    img = mix(img, "#30301c", loop)
    a = clamp01(body + sides + flap + pocket + loop)
    return img, a


@icon("smallkey")
def _smallkey(ctx):
    """(iteration 3) Small brass cabinet key on a string with a cardboard tag."""
    W = H = 256
    bow = clamp01(ellipse_mask(W, H, 150, 92, 34, 34) - ellipse_mask(W, H, 150, 92, 15, 15))
    shaft = poly(W, H, [(164, 116), (178, 106), (232, 196), (218, 206)])
    bit = poly(W, H, [(206, 178), (222, 166), (240, 192), (228, 200), (220, 192), (214, 202)])
    tag = poly(W, H, [(20, 40), (90, 22), (112, 96), (42, 116)])
    im, d = canvas(W, H)
    d.line([(100, 60), (126, 78)], fill=255, width=5)
    string = to_mask(im)
    img = solid(H, W, "#b8963c") * tex_noise(ctx, H, W, 0.12)[..., None]
    img = mix(img, "#c8b48a", tag)
    img = mix(img, "#2a2018", ellipse_mask(W, H, 66, 70, 9, 16) * (1 - ellipse_mask(W, H, 66, 70, 4, 10)) * tag)
    img = mix(img, "#d8d0b8", string)
    return img, clamp01(bow + shaft + bit + tag + string)


@icon("vhstape")
def _vhstape(ctx):
    """(iteration 3) A black VHS cassette with a hand-written paper label and the two reels in the window."""
    W = H = 256
    body = rrect(W, H, 20, 62, 236, 194, 10)
    img = solid(H, W, "#1c1c20") * tex_noise(ctx, H, W, 0.1)[..., None]
    lab = rrect(W, H, 40, 76, 216, 120, 4)
    img = mix(img, "#e0dccb", lab)
    for x0 in (60, 88, 116, 148, 176):
        img = mix(img, "#202050", rrect(W, H, x0, 90, x0 + 20, 106, 3) * 0.85)
    win = rrect(W, H, 84, 134, 172, 178, 6)
    img = mix(img, "#0a0a0e", win)
    for cx in (106, 150):
        img = mix(img, "#4a3a2e", ellipse_mask(W, H, cx, 156, 15, 15))
        img = mix(img, "#d8d4c8", ellipse_mask(W, H, cx, 156, 5, 5))
    return img, body


def ragged(m, ctx, amt, salt=0, erode=0.22):
    """distressed edges: displacement + noisy threshold (keeps it hard-ish)"""
    H, W = m.shape
    r = ctx.sub(700 + salt)
    dx = fft_noise(r, H, W, beta=1.2) * amt
    dy = fft_noise(r, H, W, beta=1.2) * amt
    w = warp(m, dx, dy)
    n = fft_noise(r, H, W, beta=0.9)
    return smoothstep(0.42, 0.58, blur(w, amt * 0.4) + erode * n * 0.5)


def logo_mask(ctx, W, H, lines_boxes, salt=0, rough=2.2):
    m = np.zeros((H, W), np.float32)
    for (txt, box) in lines_boxes:
        f = font(FONT_BOLD, 400)
        m = np.maximum(m, text_mask(W, H, [txt], f, box=tuple(int(v) for v in box), stretch=True))
    return ragged(m, ctx, W / 512.0 * rough, salt)


def red_logo(ctx, m, salt=0):
    """VHS red title: core red with lighter top, offset/blurred red bleed. Returns rgb, alpha"""
    H, W = m.shape
    r = ctx.sub(720 + salt)
    yy = (np.arange(H) + 0.5) / H
    # per-line vertical gradient approximation: use blurred mask's vertical position via local row
    t = clamp01(0.6 + 0.16 * fft_noise(r, H, W, beta=2.8) + 0.04 * photo(ctx, "gravel_fine", H, W, salt=salt))
    core = gradient_map(t, [(0, "#7a0806"), (0.5, "#c81c10"), (1, "#ee4430")])
    # scanline texture inside the letters
    scan = 1.0 - 0.12 * ((np.arange(H) // max(1, H // 128)) % 2)[:, None]
    core = core * scan[..., None]
    # chroma bleed: smear to the right and slightly down, plus faint left ghost
    s = W / 512.0
    bleed = blur(np.roll(m, (int(1 * s), int(5 * s)), (0, 1)), s * 2.5)
    ghost = blur(np.roll(m, (0, -int(4 * s)), (0, 1)), s * 1.5)
    a = clamp01(np.maximum(m, np.maximum(bleed * 0.55, ghost * 0.3)))
    rgb = mix(np.zeros((H, W, 3), np.float32) + col("#9a0c08")[None, None, :], core, m)
    # thin bright inner highlight on the top edges of letters
    top_edge = clamp01(m - np.roll(m, int(4 * s), 0))
    rgb = mix(rgb, "#ff8a6a", top_edge * 0.35)
    return rgb, a


LOGO_DEG = dict(q=70, bits=5, dither=0.3, sharpen=0.2, desat=0.0, dark=1.0, maxv=1.0)


@texture("UI/title_logo", (512, 256), k=4, alpha="soft", **LOGO_DEG)
def title_logo(ctx):
    W, H = ctx.W, ctx.H
    m = logo_mask(ctx, W, H, [("THE PRISONERS", (0.03 * W, 0.05 * H, 0.97 * W, 0.47 * H)),
                              ("OF OMAR", (0.17 * W, 0.53 * H, 0.83 * W, 0.96 * H))])
    return red_logo(ctx, m)


@texture("UI/title_logo_small", (256, 64), k=8, alpha="soft", **LOGO_DEG)
def title_logo_small(ctx):
    W, H = ctx.W, ctx.H
    m = logo_mask(ctx, W, H, [("THE PRISONERS OF OMAR", (0.03 * W, 0.12 * H, 0.97 * W, 0.88 * H))], salt=1, rough=1.0)
    return red_logo(ctx, m, 1)


# ======================================================================================
# note paper
# ======================================================================================


@texture("UI/note_paper", (256, 256), k=4, alpha="hard", q=55, desat=0.05, dark=0.95)
def note_paper(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.62 + 0.08 * fft_noise(r, H, W, beta=2.6) + 0.04 * photo(ctx, "moon"))
    img = gradient_map(t, [(0, "#8a7a54"), (0.6, "#c8b88e"), (1, "#d8caa4")])
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # faint ruled lines + margin
    rule = (np.abs(((yy / H) * 18) % 1.0 - 0.5) > 0.47) & (yy > H * 0.12)
    img = mix(img, "#6a7a9a", rule.astype(np.float32) * 0.35)
    img = mix(img, "#a04040", (np.abs(xx - W * 0.13) < W * 0.003).astype(np.float32) * 0.45)
    # fold creases
    for fy in (0.34, 0.67):
        crease = np.exp(-((yy / H - fy) / 0.004) ** 2)
        img = img * (1 - 0.25 * crease)[..., None] * (1 + 0.06 * np.exp(-((yy / H - fy - 0.01) / 0.01) ** 2))[..., None]
    fill, ring = water_stains(ctx.sub(2), H, W, n=2, rmin=0.08, rmax=0.18, tile=False)
    img = mix(img, "#8a6a34", fill * 0.2 + ring * 0.35)
    blood = splat_mask(ctx.sub(3), W, H, W * 0.86, H * 0.88, W * 0.04, drops=8)
    img = mix(img, "#5a0e08", blood * 0.85)
    dirt = grime(ctx.sub(4), H, W, cover=0.25, beta=2.8, sharp=0.6)
    img = mix(img, "#6a5a3a", dirt * 0.15)
    # torn outline: ragged top edge (torn from a pad), slightly irregular others
    n1 = fft_noise(ctx.sub(5), 1, W, beta=1.2)[0]
    n2 = fft_noise(ctx.sub(6), H, 1, beta=1.6)[:, 0]
    top = H * (0.035 + 0.012 * n1)
    left = W * (0.02 + 0.004 * n2)
    right = W * (0.98 + 0.004 * np.roll(n2, 50))
    bottom = H * (0.975 + 0.004 * np.roll(n1, 90))
    a = ((yy > top[None, :]) & (yy < bottom[None, :]) & (xx > left[:, None]) & (xx < right[:, None])).astype(np.float32)
    corner = ((xx > W * 0.88) & (yy < H * 0.12) & ((xx - W * 0.88) > (yy) * 1.0)).astype(np.float32)  # dog-ear torn corner
    a = a * (1 - corner)
    edge = clamp01(blur(1 - a, W * 0.01) * 1.6) * a
    img = mix(img, "#6a5634", edge * 0.6)
    return img, a


# ======================================================================================
# vignettes
# ======================================================================================


@texture("UI/vignette_mask", (512, 256), k=2, alpha="soft", jpeg=False, bits=6, dither=0.4, sharpen=0.0, desat=0.0, dark=1.0, maxv=1.0)
def vignette_mask(ctx):
    """black overlay with two ragged eye holes: looking out through Omar's sack mask"""
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    holes = np.full((H, W), -10.0, np.float32)
    for (cx, cy, rx, ry, tilt) in ((0.3, 0.5, 0.17, 0.38, 0.12), (0.7, 0.49, 0.165, 0.37, -0.1)):
        u = (xx / W - cx)
        v = (yy / H - cy)
        uu = u * np.cos(tilt) + v * 0.5 * np.sin(tilt)
        vv = v - u * np.sin(tilt) * 0.6
        dd = np.sqrt((uu / rx) ** 2 + (vv / ry) ** 2)
        holes = np.maximum(holes, 1 - dd)
    rag = fft_noise(r, H, W, beta=1.6) * 0.09 + fft_noise(ctx.sub(2), H, W, beta=2.6) * 0.07
    open_ = smoothstep(-0.02, 0.16, holes + rag)
    # loose burlap fibres hanging across the holes
    im, d = canvas(W, H)
    for k in range(26):
        side = k % 2
        cx = (0.3 if side == 0 else 0.7) * W
        a = r.uniform(0, 2 * np.pi)
        x0 = cx + np.cos(a) * 0.17 * W
        y0 = H * 0.5 + np.sin(a) * 0.37 * H
        L = r.uniform(0.03, 0.09) * W
        b = a + np.pi + r.normal(0, 0.6)
        pts = [(x0, y0)]
        for q in range(4):
            x0 += np.cos(b) * L / 4 + r.normal(0, 2)
            y0 += np.sin(b) * L / 4 + r.normal(0, 2) + 2
            pts.append((x0, y0))
        d.line(pts, fill=int(r.uniform(150, 255)), width=int(r.uniform(1, 3)))
    fib = to_mask(im)
    a = clamp01((1 - open_) + fib * 0.9)
    a = blur(a, W / 512.0 * 1.2)
    # burlap weave visible just around the holes (faint light leaking in)
    near = clamp01(blur(open_, W * 0.03) * 1.8) * (1 - open_)
    xs = np.sin(xx / W * 2 * np.pi * 140) * np.sin(yy / H * 2 * np.pi * 70)
    weave = clamp01(0.5 + 0.3 * xs + 0.2 * photo(ctx, "grass", H, W, salt=3))
    burlap = gradient_map(weave, [(0, "#000000"), (1, "#3a2c18")])
    rgb = burlap * near[..., None] * 0.9 + gradient_map(fib, [(0, "#000000"), (1, "#4a3a22")]) * fib[..., None]
    return rgb, a


@texture("UI/vignette_blood", (512, 256), k=2, alpha="soft", jpeg=False, bits=6, dither=0.4, sharpen=0.0, desat=0.0, dark=1.0, maxv=1.0)
def vignette_blood(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = (xx / W - 0.5) * 2, (yy / H - 0.5) * 2
    rr = np.sqrt((u * 0.92) ** 2 + (v * 0.85) ** 2)
    n = fft_noise(r, H, W, beta=2.0)
    a = smoothstep(0.62, 1.05, rr + 0.12 * n)
    # splats at the edges
    sp = np.zeros((H, W), np.float32)
    for k in range(10):
        ang = r.uniform(0, 2 * np.pi)
        cx = W * (0.5 + 0.52 * np.cos(ang))
        cy = H * (0.5 + 0.55 * np.sin(ang))
        sp = np.maximum(sp, splat_mask(ctx.sub(10 + k), W, H, cx, cy, W * r.uniform(0.04, 0.08), drops=12))
    # drips running down from the top edge
    dr = np.zeros((H, W), np.float32)
    im, d = canvas(W, H)
    for k in range(16):
        x = r.uniform(0, W)
        L = r.uniform(0.08, 0.35) * H
        wd = r.uniform(3, 9)
        d.line([(x, 0), (x + r.normal(0, 3), L)], fill=255, width=int(wd))
        d.ellipse([x - wd * 0.8, L - wd * 0.6, x + wd * 0.8, L + wd], fill=255)
    a = clamp01(np.maximum(np.maximum(a, sp * 0.95), to_mask(im) * 0.95))
    t = clamp01(0.5 + 0.3 * fft_noise(ctx.sub(3), H, W, beta=2.2) - 0.3 * clamp01(rr - 0.8))
    rgb = gradient_map(t, [(0, "#100000"), (0.5, "#4a0404"), (1, "#8a0e0a")])
    return rgb, a


@texture("UI/vignette_dark", (512, 256), k=2, alpha="soft", jpeg=False, bits=6, dither=0.6, sharpen=0.0, desat=0.0, dark=1.0, maxv=1.0)
def vignette_dark(ctx):
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = (xx / W - 0.5) * 2, (yy / H - 0.5) * 2
    rr = np.sqrt((u * 0.9) ** 2 + (v * 0.95) ** 2)
    a = smoothstep(0.45, 1.25, rr) ** 1.3
    return np.zeros((H, W, 3), np.float32), clamp01(a)


def load_tex(path, mode="RGB"):
    """an already generated texture (built earlier in the same run) as float array"""
    im = Image.open(os.path.join(OUT_ROOT, path + ".png")).convert(mode)
    return np.asarray(im, np.float32) / 255.0


def paste(dst, src, x0, y0, alpha=None):
    """paste float src (h,w,3) into dst at pixel (x0,y0) with optional alpha (h,w)"""
    h, w = src.shape[:2]
    H, W = dst.shape[:2]
    xa, ya, xb, yb = max(0, x0), max(0, y0), min(W, x0 + w), min(H, y0 + h)
    if xa >= xb or ya >= yb:
        return dst
    s = src[ya - y0:yb - y0, xa - x0:xb - x0]
    if alpha is None:
        dst[ya:yb, xa:xb] = s
    else:
        a = alpha[ya - y0:yb - y0, xa - x0:xb - x0][..., None]
        dst[ya:yb, xa:xb] = dst[ya:yb, xa:xb] * (1 - a) + s * a
    return dst


def text_rgb(img, lines, fnt_name, box, color, spacing=0, align="center", stretch=False, size=200, shear=0.0):
    H, W = img.shape[:2]
    f = font(fnt_name, size)
    m = text_mask(W, H, lines, f, box=tuple(int(v) for v in box), spacing=spacing, align=align, stretch=True if stretch else None)
    if shear:
        im = Image.fromarray((m * 255).astype(np.uint8))
        cy = (box[1] + box[3]) / 2
        im = im.transform((W, H), Image.AFFINE, (1, shear, -shear * cy, 0, 1, 0), resample=Image.BILINEAR)
        m = to_mask(im)
    return mix(img, color, m), m


def sack_head(ctx, img, cx, cy, w, h, light_dir=-1.0, rim="#4a70c0"):
    """pale burlap sack mask with two dark eye holes; (cx,cy,w,h) in pixels"""
    H, W = img.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = (xx - cx) / (w / 2), (yy - cy) / (h / 2)
    body = ((np.abs(u) ** 2.2 + np.abs(v) ** 2.2) < 1.0) | ((np.abs(u) < 0.95 - 0.3 * clamp01(v)) & (v > -0.55) & (v < 0.9))
    # pointed top corners (the sack is tied off) and gathered neck
    ear_l = ((u + 0.8) ** 2 / 0.018 + (v + 0.92) ** 2 / 0.035) < 1
    ear_r = ((u - 0.8) ** 2 / 0.018 + (v + 0.92) ** 2 / 0.035) < 1
    neck = (np.abs(u) < 0.55 - 0.25 * clamp01(v - 0.8)) & (v > 0.8) & (v < 1.15)
    m = (body | ear_l | ear_r | neck).astype(np.float32)
    # rounded volume: lit from the upper left, falling off to the right and toward the chin
    vol = clamp01(1.0 - (u + 0.35) ** 2 * 0.6 - (v + 0.3) ** 2 * 0.35)
    t = clamp01(0.25 + 0.55 * vol + 0.12 * photo(ctx, "grass", H, W, salt=31) + 0.1 * fft_noise(ctx.sub(31), H, W, beta=2.4))
    sack = gradient_map(t, [(0, "#3a3826"), (0.5, "#8c8862"), (1, "#c0bc90")])
    rimm = clamp01(1 - (u * light_dir * -1 + 1.0) * 1.5) * m
    sack = mix(sack, rim, rimm * 0.5)
    img = mix(img, sack, m)
    img = mix(img, "#4a3a20", (neck & (np.abs(v - 0.92) < 0.06)).astype(np.float32) * 0.9)  # rope
    for ex in (-0.33, 0.33):
        eye = ellipse_mask(W, H, cx + ex * w / 2, cy - 0.05 * h / 2, w * 0.13, h * 0.1, soft=0.25)
        img = mix(img, "#030202", eye)
        img = mix(img, "#2a2418", clamp01(ellipse_mask(W, H, cx + ex * w / 2, cy - 0.05 * h / 2, w * 0.19, h * 0.15, soft=0.6) - eye) * 0.5)
    return img, m


def plaid(ctx, H, W, scale, base="#5a0e0a"):
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    pu = np.sin(xx / scale * 2 * np.pi) > 0.25
    pv = np.sin(yy / scale * 2 * np.pi) > 0.25
    thin = (np.abs(np.sin(xx / scale * 2 * np.pi + 1.3)) < 0.1) | (np.abs(np.sin(yy / scale * 2 * np.pi + 1.3)) < 0.1)
    c = solid(H, W, base)
    c = mix(c, "#9a1a12", (pu ^ pv).astype(np.float32) * 0.6)
    c = mix(c, "#120404", (pu & pv).astype(np.float32) * 0.7)
    c = mix(c, "#0a0a0a", thin.astype(np.float32) * 0.6)
    return c


def farmhouse(ctx, img, x0, y0, w, h, lit_windows=True):
    """white clapboard farmhouse silhouette; (x0,y0) = top-left of its bounding box"""
    H, W = img.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = (xx - x0) / w, (yy - y0) / h
    main = ((u > 0.3) & (u < 0.92) & (v > 0.32) & (v < 1.0)).astype(np.float32)
    roof = ((v > 0.05 + np.abs(u - 0.61) * 0.85) & (v < 0.34) & (u > 0.26) & (u < 0.96)).astype(np.float32)
    wing = ((u > 0.02) & (u < 0.32) & (v > 0.58) & (v < 1.0)).astype(np.float32)
    wroof = ((v > 0.45 + np.abs(u - 0.17) * 0.5) & (v < 0.6) & (u > 0.0) & (u < 0.34)).astype(np.float32)
    porch = ((u > 0.3) & (u < 0.92) & (v > 0.66) & (v < 0.71)).astype(np.float32)
    walls = clamp01(main + wing)
    clap = 0.85 + 0.15 * ((v * 40) % 1.0)
    wall_c = solid(H, W, "#8a9098") * clap[..., None]
    wall_c = wall_c * (0.55 + 0.4 * clamp01(1 - u) - 0.25 * clamp01((v - 0.6) * 2.5))[..., None]  # moonlight from the left
    wall_c = wall_c * (1.0 + 0.12 * fft_noise(ctx.sub(14), H, W, beta=2.0))[..., None]
    img = mix(img, wall_c, walls)
    img = mix(img, "#1a1e26", clamp01(roof + wroof))
    img = mix(img, "#2a2e36", porch)
    wins = [(0.4, 0.4), (0.55, 0.4), (0.7, 0.4), (0.83, 0.4), (0.4, 0.76), (0.62, 0.76), (0.83, 0.76), (0.1, 0.7), (0.22, 0.7)]
    for i, (wx, wy) in enumerate(wins):
        wm = ((u > wx - 0.035) & (u < wx + 0.035) & (v > wy - 0.0) & (v < wy + 0.15)).astype(np.float32)
        c = "#a01810" if (lit_windows and i in (1, 4, 7)) else "#0e1014"
        img = mix(img, "#e8e8e0", outer_rim(wm, w * 0.006) * 0.6)
        img = mix(img, c, wm)
    for px in (0.34, 0.5, 0.66, 0.82):
        img = mix(img, "#c8c8c0", ((np.abs(u - px) < 0.008) & (v > 0.71) & (v < 1.0)).astype(np.float32))
    return img


def lightning(img, rng, x0, y0, x1, y1, width):
    H, W = img.shape[:2]
    im, d = canvas(W, H)
    pts = [(x0, y0)]
    n = 9
    for i in range(1, n + 1):
        t = i / n
        pts.append((x0 + (x1 - x0) * t + rng.normal(0, W * 0.012), y0 + (y1 - y0) * t))
    d.line(pts, fill=255, width=int(width))
    k = int(n * 0.5)
    bx, by = pts[k]
    d.line([(bx, by), (bx + W * 0.03, by + H * 0.06), (bx + W * 0.025, by + H * 0.1)], fill=200, width=max(1, int(width * 0.6)))
    m = to_mask(im)
    glow = blur(m, width * 4)
    img = mix(img, "#6080d0", clamp01(glow * 2.5) * 0.6)
    img = mix(img, "#f0f4ff", m)
    return img


@texture("UI/vhs_cover", (512, 400), k=2, q=48, bits=5, dither=0.3, sharpen=0.3, desat=0.05, dark=0.97, maxv=0.97)
def vhs_cover(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    img = solid(H, W, "#070707") * (1 + 0.05 * fft_noise(r, H, W, beta=1.6))[..., None]
    BX, SX = int(0.44 * W), int(0.555 * W)  # back | spine | front
    # ---------------- back panel ----------------
    img, _ = text_rgb(img, ["THE", "PRISONERS", "OF OMAR"], FONT_BOLD, (0.02 * W, 0.03 * H, 0.2 * W, 0.24 * H), "#c81e12", spacing=6)
    quote = ['"...the smell of rust...', 'a sack over his face...', 'he drags you down into', 'the basement and cages', 'you like an animal! You', 'have three nights until', 'the slaughter..."']
    img, _ = text_rgb(img, quote, FONT_ROAD, (0.215 * W, 0.035 * H, 0.425 * W, 0.25 * H), "#d8481e", spacing=6, shear=-0.18)
    # oval "screenshot" built from the game's own textures
    ow, oh = int(0.33 * W), int(0.15 * H)
    wall = np.tile(load_tex("Env/wall_wallpaper_green"), (2, 4, 1))
    wall = resize(wall, ow, oh, Image.NEAREST)
    door = resize(load_tex("Props/door_wood_dirty"), int(oh * 0.45), int(oh * 0.9), Image.NEAREST)
    wall = paste(wall, door, int(ow * 0.62), int(oh * 0.08))
    spl = load_tex("Decals/blood_splatter_1", "RGBA")
    for (sx, sy, sz) in ((0.15, 0.1, 0.6), (0.4, 0.3, 0.8), (0.75, 0.05, 0.5)):
        s = resize(spl, int(oh * sz), int(oh * sz), Image.NEAREST)
        wall = paste(wall, s[..., :3], int(ow * sx), int(oh * sy), s[..., 3])
    wall = wall * 0.85 + col("#202a10")[None, None, :] * 0.15
    oval = ellipse_mask(ow, oh, ow / 2, oh / 2, ow / 2 - 2, oh / 2 - 2)
    rim = clamp01(ellipse_mask(ow, oh, ow / 2, oh / 2, ow / 2, oh / 2) - oval)
    img = paste(img, wall, int(0.055 * W), int(0.28 * H), oval)
    img = paste(img, np.ones((oh, ow, 3), np.float32) * col("#d8d0c0"), int(0.055 * W), int(0.28 * H), rim)
    para = ["Welcome to the farm of OMAR. Welcome", "to YOUR new home. Why are you here?", "Why has this mad man chosen you, and",
            "what are his plans for you? How will", "you escape? Can you even escape?", "All of these questions have answers,",
            "but can you seek them out? If only", "you'd heeded the warning to STAY", "AWAY FROM THE FARM."]
    img, _ = text_rgb(img, para, FONT_ROAD, (0.025 * W, 0.47 * H, 0.29 * W, 0.66 * H), "#d8d8d0", spacing=5, align="left")
    # framed photo of the mask
    fx0, fy0, fw, fh = int(0.305 * W), int(0.47 * H), int(0.115 * W), int(0.2 * H)
    ph = solid(fh, fw, "#1a2414") * (1 + 0.15 * fft_noise(ctx.sub(4), fh, fw, beta=2.2))[..., None]
    sub = type("C", (), {})()
    ph, _ = sack_head(ctx, ph, fw * 0.5, fh * 0.5, fw * 0.62, fh * 0.6, rim="#a0b070")
    ph = mix(ph, "#6a8a40", np.full((fh, fw), 0.18, np.float32))
    img = paste(img, ph, fx0, fy0)
    for (x, y, w_, h_) in ((fx0 - 2, fy0 - 2, fw + 4, 2), (fx0 - 2, fy0 + fh, fw + 4, 2), (fx0 - 2, fy0, 2, fh), (fx0 + fw, fy0, 2, fh)):
        img[max(0, y):y + h_, max(0, x):x + w_] = col("#b01810")
    # mannequins (dim silhouettes, bottom left)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    for (mx, sc) in ((0.07, 1.0), (0.165, 0.72)):
        hx, hy = mx * W, H * (0.69 + 0.08 * (1 - sc))
        hw, hh = W * 0.016 * sc, H * 0.03 * sc
        sy = hy + hh * 1.6  # shoulder line
        parts = [ellipse_mask(W, H, hx, hy, hw, hh),
                 poly(W, H, [(hx - hw * 0.5, hy + hh * 0.8), (hx + hw * 0.5, hy + hh * 0.8), (hx + hw * 0.5, sy), (hx - hw * 0.5, sy)]),
                 poly(W, H, [(hx - hw * 2.6, sy), (hx + hw * 2.6, sy), (hx + hw * 1.6, sy + hh * 5.5), (hx + hw * 1.9, H * 0.93),
                             (hx - hw * 1.9, H * 0.93), (hx - hw * 1.6, sy + hh * 5.5)]),
                 poly(W, H, [(hx - hw * 2.6, sy), (hx - hw * 3.2, sy + hh * 5), (hx - hw * 2.5, sy + hh * 5.2), (hx - hw * 1.9, sy + hh * 1)]),
                 poly(W, H, [(hx + hw * 2.6, sy), (hx + hw * 3.2, sy + hh * 5), (hx + hw * 2.5, sy + hh * 5.2), (hx + hw * 1.9, sy + hh * 1)])]
        man = clamp01(sum(parts))
        img = mix(img, gradient_map(clamp01((xx - hx) / (W * 0.06) + 0.5), [(0, "#1e1e14"), (1, "#7a7652")]), man * 0.9)
    img, _ = text_rgb(img, ["SECOND CLASS VIDEO"], FONT_ROAD, (0.29 * W, 0.73 * H, 0.43 * W, 0.77 * H), "#d040b0")
    img, _ = text_rgb(img, ["Second Class Video", "Copyright 1987", "All Rights Reserved"], FONT_ROAD, (0.28 * W, 0.79 * H, 0.42 * W, 0.9 * H), "#d0d0c8", spacing=4)
    img[int(0.935 * H):, :BX] = col("#c81e14")
    img, _ = text_rgb(img, ["WARNING: For domestic use only. Any unauthorized copying, hiring,", "lending or public performance of this videocassette is illegal."],
                      FONT_ROAD, (0.02 * W, 0.945 * H, 0.42 * W, 0.99 * H), "#140404", spacing=3)
    # ---------------- spine ----------------
    img[:, BX:BX + 3] = col("#202020")
    img[:, SX - 3:SX] = col("#202020")
    sw = SX - BX
    tmp = np.zeros((sw, int(H * 0.55), 3), np.float32)  # rotated canvas (w <-> h)
    th, tw = tmp.shape[:2]
    tmp, tm = text_rgb(tmp, ["THE PRISONERS OF OMAR"], FONT_BOLD, (0.02 * tw, 0.12 * th, 0.98 * tw, 0.88 * th), "#c81e12", stretch=True)
    rot = np.rot90(tmp, -1)
    rm = np.rot90(tm, -1)
    img = paste(img, rot, BX + (sw - rot.shape[1]) // 2, int(0.14 * H), rm)
    vb = (BX + int(sw * 0.15), int(0.72 * H), int(sw * 0.7), int(0.07 * H))
    img[vb[1]:vb[1] + vb[3], vb[0]:vb[0] + vb[2]] = col("#e0e0d8")
    img[vb[1] + 3:vb[1] + vb[3] - 3, vb[0] + 3:vb[0] + vb[2] - 3] = col("#070707")
    img, _ = text_rgb(img, ["VHS"], FONT_BOLD, (vb[0] + 6, vb[1] + 5, vb[0] + vb[2] - 6, vb[1] + vb[3] - 5), "#e0e0d8")
    img, _ = text_rgb(img, ["hi-fi"], FONT_ROAD, (BX + sw * 0.2, 0.81 * H, SX - sw * 0.2, 0.85 * H), "#e0e0d8")
    img, _ = text_rgb(img, ["MONO", "SC1987"], FONT_ROAD, (BX + sw * 0.15, 0.86 * H, SX - sw * 0.15, 0.92 * H), "#e0e0d8", spacing=4)
    img, _ = text_rgb(img, ["OMAR"], FONT_ROAD, (BX + sw * 0.2, 0.03 * H, SX - sw * 0.2, 0.07 * H), "#d040b0")
    img[int(0.935 * H):, BX + 3:SX - 3] = col("#c81e14")
    # ---------------- front ----------------
    FW = W - SX
    yy, xx = np.mgrid[0:H, 0:FW].astype(np.float32)
    u, v = xx / FW, yy / H
    bg = gradient_map(clamp01(0.55 - 0.6 * np.hypot(u - 0.35, v - 0.3) + 0.12 * fft_noise(ctx.sub(5), H, FW, beta=2.6)),
                      [(0, "#03060e"), (0.5, "#0c1a3a"), (1, "#2a4a86")])
    front = bg
    front = lightning(front, ctx.sub(6), FW * 0.33, 0.0, FW * 0.42, H * 0.36, FW * 0.008)
    # body / plaid shirt
    shirt = plaid(ctx, H, FW, FW * 0.11)
    folds = fft_noise(ctx.sub(13), H, FW, beta=2.6, ax=1.0, ay=1.6)
    shirt = shirt * (0.25 + 0.55 * clamp01(1.0 - u * 1.1) - 0.25 * clamp01((v - 0.45) * 3) + 0.12 * folds)[..., None]
    shirt = clamp01(shirt)
    torso = poly(FW, H, [(0.0 * FW, 0.66 * H), (0.12 * FW, 0.42 * H), (0.36 * FW, 0.34 * H), (0.62 * FW, 0.34 * H),
                         (0.86 * FW, 0.42 * H), (1.0 * FW, 0.6 * H), (1.0 * FW, 0.75 * H), (0.0, 0.75 * H)])
    arm = poly(FW, H, [(0.06 * FW, 0.46 * H), (0.02 * FW, 0.2 * H), (0.08 * FW, 0.1 * H), (0.2 * FW, 0.1 * H), (0.24 * FW, 0.22 * H), (0.26 * FW, 0.4 * H)])
    front = mix(front, shirt, clamp01(torso + arm))
    # rim light along the arm / shoulder (blue)
    edge = inner_rim(torso + arm, FW * 0.01)
    front = mix(front, "#5a80d0", edge * 0.5)
    # fist + cleaver
    fist = ellipse_mask(FW, H, 0.14 * FW, 0.1 * H, FW * 0.065, H * 0.055)
    front = mix(front, "#8a6a52", fist)
    handle = poly(FW, H, [(0.12 * FW, 0.1 * H), (0.17 * FW, 0.12 * H), (0.27 * FW, 0.01 * H), (0.22 * FW, -0.01 * H)])
    front = mix(front, "#3a2414", handle)
    blade = poly(FW, H, [(0.2 * FW, 0.035 * H), (0.6 * FW, -0.02 * H), (0.62 * FW, 0.12 * H), (0.27 * FW, 0.15 * H)])
    steel = gradient_map(clamp01(0.6 - 0.3 * v / 0.15 + 0.1 * fft_noise(ctx.sub(7), H, FW, beta=2.0)), [(0, "#4a5058"), (1, "#c0c8d0")])
    front = mix(front, steel, blade)
    bl = grime(ctx.sub(8), H, FW, cover=0.22, beta=1.8, sharp=0.2) * blade
    front = mix(front, "#8a0806", bl)
    dr = drips(ctx.sub(9), H, FW, 6, length=(0.02, 0.08), width=(2, 4), tile=False) * clamp01(blur(blade, 4) * 3)
    front = mix(front, "#8a0806", dr)
    # head
    front, _ = sack_head(ctx, front, 0.49 * FW, 0.22 * H, 0.3 * FW, 0.3 * H)
    # house + grass
    front = farmhouse(ctx, front, int(0.18 * FW), int(0.43 * H), 0.8 * FW, 0.26 * H)
    gr = (v > 0.66 + 0.02 * fft_noise(ctx.sub(10), H, FW, beta=0.8)).astype(np.float32)
    front = mix(front, "#04060a", gr)
    # title, rating, tagline
    front, _ = text_rgb(front, ["THE PRISONERS"], FONT_BOLD, (0.08 * FW, 0.71 * H, 0.92 * FW, 0.8 * H), "#d0261a", stretch=True)
    front, _ = text_rgb(front, ["OF OMAR"], FONT_BOLD, (0.2 * FW, 0.81 * H, 0.8 * FW, 0.92 * H), "#d0261a", stretch=True)
    rc = ellipse_mask(FW, H, 0.9 * FW, 0.62 * H, FW * 0.06, FW * 0.06)
    front = mix(front, "#e0281c", rc)
    front, _ = text_rgb(front, ["R"], FONT_BOLD, (0.865 * FW, 0.595 * H, 0.935 * FW, 0.645 * H), "#100404")
    front, _ = text_rgb(front, ["Nowhere", "to run...", "", "Nowhere", "to hide..."], FONT_ROAD, (0.7 * FW, 0.05 * H, 0.98 * FW, 0.3 * H), "#e8e8e0", spacing=2, align="right")
    front[int(0.935 * H):] = col("#c81e14")
    front, _ = text_rgb(front, ["VHS        hi-fi"], FONT_BOLD, (0.25 * FW, 0.945 * H, 0.75 * FW, 0.99 * H), "#160404")
    img[:, SX:] = front
    # worn box: scuffs, fold lines, uneven scan light
    scuff = (grime(ctx.sub(11), H, W, cover=0.05, beta=1.6, sharp=0.2) * smoothstep(0.35, 0.5, radial(W, H, r=W * 0.7)))
    img = mix(img, "#8a8a80", scuff * 0.5)
    for fxl in (BX, SX):
        img = mix(img, "#3a3a38", (np.abs(np.arange(W) - fxl) < 2)[None, :] * np.ones((H, 1), np.float32) * 0.6)
    img = img * (1.0 + 0.04 * fft_noise(ctx.sub(12), H, W, beta=3.0))[..., None]
    img = img * (1.0 + 0.05 * photo(ctx, "gravel_fine", H, W, salt=12))[..., None]
    return clamp01(img)


# ======================================================================================
# ending stills (256x144 degraded photos)
# ======================================================================================
END_DEG = dict(q=34, bits=5, dither=0.4, sharpen=0.4, desat=0.15, dark=0.95, maxv=0.92)


def photo_finish(ctx, img, vig=0.55, grain=0.08):
    H, W = img.shape[:2]
    img = img * (1.0 - vig * smoothstep(0.35, 1.2, radial(W, H, r=np.hypot(W, H) / 2)))[..., None]
    img = img * (1.0 + grain * ctx.sub(99).standard_normal((H, W)).astype(np.float32))[..., None]
    img = img + 0.03 * photo(ctx, "gravel_fine", H, W, salt=99)[..., None] * 0.3
    return clamp01(img)


def treeline_silhouette(ctx, H, W, y0, amp, salt=0):
    n = fft_noise(ctx.sub(400 + salt), 1, W, beta=1.4)[0] * 0.5 + fft_noise(ctx.sub(401 + salt), 1, W, beta=2.6)[0]
    top = y0 - amp * clamp01(0.5 + 0.35 * n)
    yy = np.arange(H, dtype=np.float32)[:, None]
    return (yy > top[None, :]).astype(np.float32)


@texture("UI/ending_road", (256, 144), k=4, **END_DEG)
def ending_road(ctx):
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = xx / W, yy / H
    hz = 0.47
    img = gradient_map(clamp01(v / hz), [(0, "#05070a"), (1, "#252a2e")])
    img = mix(img, "#3a3a36", np.exp(-((v - hz) / 0.05) ** 2) * 0.6)  # fog at the horizon
    trees = treeline_silhouette(ctx, H, W, hz * H, 0.12 * H) * (v < 0.8)
    img = mix(img, "#07090a", trees)
    # ground + road in perspective
    ground = (v > hz).astype(np.float32)
    depth = clamp01((v - hz) / (1 - hz))
    gcol = gradient_map(clamp01(0.4 + 0.25 * fft_noise(ctx.sub(1), H, W, beta=1.8)), [(0, "#0e0c08"), (1, "#3a3424")])
    img = mix(img, gcol * (0.4 + 0.8 * depth)[..., None], ground)
    half = 0.02 + 0.42 * depth
    road = ((np.abs(u - 0.5) < half) & (v > hz)).astype(np.float32)
    rcol = gradient_map(clamp01(0.45 + 0.2 * fft_noise(ctx.sub(2), H, W, beta=1.2)), [(0, "#141416"), (1, "#3a3a3c")])
    img = mix(img, rcol * (0.35 + 0.9 * depth)[..., None], road)
    # dashed centre line (perspective spacing) + edge lines
    z = 1.0 / np.maximum(depth, 1e-3)
    dash = ((np.abs(u - 0.5) < 0.004 + 0.012 * depth) & ((z * 0.8) % 1.0 < 0.5) & (v > hz + 0.01)).astype(np.float32)
    img = mix(img, "#b8a868", dash * 0.8)
    for sgn in (-1, 1):
        el = (np.abs(u - (0.5 + sgn * half * 0.96)) < 0.002 + 0.006 * depth) & (v > hz)
        img = mix(img, "#8a8a84", el.astype(np.float32) * 0.6)
    # headlight pool on the road
    img = img * (1.0 + 1.1 * np.exp(-(((u - 0.5) / 0.28) ** 2 + ((v - 0.95) / 0.3) ** 2)) * road)[..., None]
    # roadside sign
    sx0, sx1, sy0, sy1 = 0.74, 0.88, 0.33, 0.43
    for px in (0.765, 0.855):
        img = mix(img, "#6a6a66", ((np.abs(u - px) < 0.003) & (v > sy1) & (v < 0.62)).astype(np.float32))
    sm = ((u > sx0) & (u < sx1) & (v > sy0) & (v < sy1)).astype(np.float32)
    img = mix(img, "#a8a8a0", sm)
    img, _ = text_rgb(img, ["DEAD END", "NO EXIT AHEAD"], FONT_ROAD, (sx0 * W + 6, sy0 * H + 5, sx1 * W - 6, sy1 * H - 5), "#141414", spacing=4)
    return photo_finish(ctx, img)


@texture("UI/ending_car", (256, 144), k=4, **END_DEG)
def ending_car(ctx):
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = xx / W, yy / H
    fog = clamp01(0.5 + 0.3 * fft_noise(ctx.sub(1), H, W, beta=2.8, ax=1.5, ay=0.6))
    img = gradient_map(fog * 0.6, [(0, "#020304"), (1, "#2a3034")])
    img = mix(img, "#0a0a0a", treeline_silhouette(ctx, H, W, 0.5 * H, 0.15 * H) * 0.8)
    # car silhouette
    car = (((u > 0.3) & (u < 0.7) & (v > 0.47) & (v < 0.66)) | ((u > 0.36) & (u < 0.64) & (v > 0.38) & (v < 0.5))).astype(np.float32)
    img = mix(img, "#070707", car)
    light = np.zeros((H, W), np.float32)
    for cx in (0.39, 0.61):
        d = np.hypot((u - cx) * W / H, v - 0.56)
        light += np.exp(-(d / 0.025) ** 2) * 1.6 + np.exp(-(d / 0.12) ** 2) * 0.5 + np.exp(-(d / 0.35) ** 2) * 0.25 * fog
        light += np.exp(-((v - 0.56) / 0.006) ** 2) * np.exp(-((u - cx) / 0.25) ** 2) * 0.5  # lens streak
    refl = np.exp(-((u - 0.5) / 0.2) ** 2) * smoothstep(0.66, 1.0, v) * 0.25 * (0.6 + 0.4 * fog)
    img = img + gradient_map(clamp01(light + refl), [(0, "#000000"), (0.5, "#a89a70"), (1, "#fff6e0")])
    return photo_finish(ctx, clamp01(img), grain=0.1)


@texture("UI/ending_tunnel", (256, 144), k=4, **END_DEG)
def ending_tunnel(ctx):
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = (xx / W - 0.5) * 2, (yy / H - 0.47) * 2
    # perspective tunnel: depth from max-norm distance to the centre
    dd = np.maximum(np.abs(u) * 0.95, np.abs(v) * 1.1)
    depth = 1.0 / np.maximum(dd, 0.06)
    exit_ = (dd < 0.12).astype(np.float32)
    rings = ((depth * 1.2) % 1.0 < 0.08).astype(np.float32)
    conc = gradient_map(clamp01(0.4 + 0.2 * fft_noise(ctx.sub(1), H, W, beta=1.6)), [(0, "#0c0c0c"), (1, "#4a4844")])
    lightfall = clamp01(0.22 + 0.9 * (0.12 / np.maximum(dd, 0.12)) ** 0.9)
    img = conc * lightfall[..., None]
    img = img * (1 - 0.4 * rings)[..., None]
    floor = (v > np.abs(u) * 1.1 / 0.95).astype(np.float32) * (dd >= 0.12)
    img = mix(img, "#0a0a0a", floor * 0.4)
    img = img + (np.exp(-((u / 0.15) ** 2)) * floor * lightfall * 0.25)[..., None]  # wet reflection
    glow = np.exp(-(np.maximum(dd - 0.12, 0) / 0.08) ** 2) * 0.8
    img = mix(img, "#e8ecf0", clamp01(exit_ + glow * 0.6))
    return photo_finish(ctx, clamp01(img), grain=0.08)


@texture("UI/ending_signal", (256, 144), k=4, **END_DEG)
def ending_signal(ctx):
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = xx / W, yy / H
    clouds = clamp01(0.4 + 0.3 * fft_noise(ctx.sub(1), H, W, beta=2.8, ax=1.6, ay=0.6))
    img = gradient_map(clouds * (1 - v * 0.5), [(0, "#020306"), (1, "#1a2028")])
    hz = 0.72
    img = mix(img, "#050605", (v > hz).astype(np.float32))
    img = mix(img, "#06080a", treeline_silhouette(ctx, H, W, hz * H, 0.08 * H, 3))
    # helicopter
    hx, hy = 0.7, 0.18
    body = ellipse_mask(W, H, hx * W, hy * H, 0.05 * W, 0.035 * H)
    tail = ((u > hx + 0.03) & (u < hx + 0.15) & (np.abs(v - (hy - 0.01) + (u - hx) * 0.05) < 0.008)).astype(np.float32)
    rotor = ((np.abs(v - (hy - 0.05)) < 0.004) & (np.abs(u - hx) < 0.13)).astype(np.float32)
    skid = ((np.abs(v - (hy + 0.045)) < 0.004) & (np.abs(u - hx) < 0.05)).astype(np.float32)
    # searchlight cone to the ground spot
    sx, sy = 0.45, 0.86
    t = clamp01((v - hy) / (sy - hy))
    cx = hx + (sx - hx) * t
    width = 0.01 + 0.09 * t
    cone = (np.abs(u - cx) < width).astype(np.float32) * (v > hy) * (v < sy + 0.04)
    cone = blur(cone, W * 0.01) * (0.35 + 0.25 * clouds)
    spot = ellipse_mask(W, H, sx * W, sy * H, 0.13 * W, 0.045 * H, soft=0.7)
    grass = gradient_map(clamp01(0.5 + 0.3 * photo(ctx, "grass", H, W, salt=5)), [(0, "#202018"), (1, "#a8a888")])
    img = img + cone[..., None] * col("#c8d0d8")[None, None, :]
    img = mix(img, grass, spot * 0.9)
    figure = ((np.abs(u - sx) < 0.008) & (v > sy - 0.07) & (v < sy)).astype(np.float32)
    figure = np.maximum(figure, ellipse_mask(W, H, sx * W, (sy - 0.08) * H, 0.008 * W, 0.014 * H))
    img = mix(img, "#050505", figure)
    img = mix(img, "#0a0a0c", clamp01(body + tail + rotor + skid))
    img = mix(img, "#ff3020", ellipse_mask(W, H, (hx + 0.15) * W, (hy - 0.02) * H, 0.006 * W, 0.006 * W))
    img = mix(img, "#ffffff", ellipse_mask(W, H, (hx - 0.03) * W, (hy + 0.03) * H, 0.012 * W, 0.012 * W))
    return photo_finish(ctx, clamp01(img), grain=0.1)


@texture("UI/ending_ashes", (256, 144), k=4, **END_DEG)
def ending_ashes(ctx):
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = xx / W, yy / H
    n = fft_noise(ctx.sub(1), H, W, beta=2.4, ax=1.0, ay=2.0)
    heat = clamp01(1.3 - np.hypot((u - 0.5) * 1.2, (v - 0.85) * 1.6) * 1.6 + 0.35 * n)
    fire = (v > 0.4) * smoothstep(0.35, 0.8, heat + 0.2 * fft_noise(ctx.sub(2), H, W, beta=1.6, ay=2.5))
    img = gradient_map(clamp01(heat * 0.6), [(0, "#030100"), (0.5, "#3a0c02"), (1, "#a03a0a")])
    img = img + gradient_map(clamp01(fire), [(0, "#000000"), (0.4, "#8a1a04"), (0.7, "#ff7a18"), (1, "#ffe6a0")])
    smoke = clamp01(0.5 + 0.4 * fft_noise(ctx.sub(3), H, W, beta=2.6)) * (1 - smoothstep(0.1, 0.55, v))
    img = mix(img, "#0a0806", smoke * 0.7)
    # chain-link fence silhouette in front
    n_d = 14
    a1 = np.abs(((u * n_d * W / H + v * n_d) % 1.0) - 0.5) > 0.45
    a2 = np.abs(((u * n_d * W / H - v * n_d) % 1.0) - 0.5) > 0.45
    mesh = ((a1 | a2) & (v > 0.18)).astype(np.float32)
    posts = ((np.abs(((u * 3) % 1.0) - 0.5) > 0.485) & (v > 0.12)).astype(np.float32)
    rail = (np.abs(v - 0.19) < 0.008).astype(np.float32)
    img = mix(img, "#050302", clamp01(mesh * 0.85 + posts + rail))
    return photo_finish(ctx, clamp01(img), grain=0.08)


@texture("UI/ending_caught", (256, 144), k=4, **END_DEG)
def ending_caught(ctx):
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = xx / W, yy / H
    back = clamp01(0.3 + 0.25 * fft_noise(ctx.sub(1), H, W, beta=2.6) + 0.35 * np.exp(-((u - 0.62) ** 2 / 0.04 + (v - 0.35) ** 2 / 0.06)))
    img = gradient_map(back, [(0, "#020101"), (0.6, "#2a0806"), (1, "#6a1a0e")])
    # figure slumped behind the bars
    fig = ellipse_mask(W, H, 0.42 * W, 0.55 * H, 0.06 * W, 0.08 * H) + ((np.abs(u - 0.42) < 0.08 - (v - 0.6) * 0.0) & (v > 0.6)).astype(np.float32)
    img = mix(img, "#050202", clamp01(fig) * 0.85)
    # bars: cylinders lit from the right (dim red light)
    nb = 9
    p = (u * nb) % 1.0
    bar = (np.abs(p - 0.5) < 0.09).astype(np.float32)
    shade = np.clip((p - 0.41) / 0.18, 0, 1)
    barc = gradient_map(shade * (0.5 + 0.5 * back), [(0, "#020202"), (0.7, "#2a1410"), (1, "#8a3a28")])
    img = mix(img, barc, bar)
    cross = (np.abs(v - 0.2) < 0.015) | (np.abs(v - 0.82) < 0.015)
    img = mix(img, "#140a08", cross.astype(np.float32))
    rust = grime(ctx.sub(2), H, W, cover=0.3, beta=1.8, sharp=0.3) * bar
    img = mix(img, "#4a1e0e", rust * 0.5)
    return photo_finish(ctx, clamp01(img), vig=0.75, grain=0.1)


@texture("UI/ending_dawn", (256, 144), k=4, **END_DEG)
def ending_dawn(ctx):
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = xx / W, yy / H
    hz = 0.55
    clouds = clamp01(0.5 + 0.35 * fft_noise(ctx.sub(1), H, W, beta=2.8, ax=1.8, ay=0.6))
    sky = gradient_map(clamp01(v / hz), [(0, "#3a4048"), (0.75, "#7a7c7c"), (1, "#a8a090")])
    sky = sky * (0.85 + 0.25 * clouds)[..., None]
    sky = sky + (np.exp(-(((u - 0.68) / 0.18) ** 2 + ((v - hz) / 0.08) ** 2)) * 0.25)[..., None] * col("#d8c8a8")[None, None, :]
    img = sky
    img = mix(img, "#3a3e40", treeline_silhouette(ctx, H, W, hz * H, 0.05 * H, 9) * 0.8)
    field = (v > hz).astype(np.float32)
    depth = clamp01((v - hz) / (1 - hz))
    g = photo(ctx, "grass", H, W, salt=8)
    fcol = gradient_map(clamp01(0.5 + 0.25 * g + 0.1 * fft_noise(ctx.sub(2), H, W, beta=2.2)), [(0, "#2a2618"), (0.5, "#6a6044"), (1, "#a09068")])
    fcol = fcol * (0.55 + 0.5 * depth)[..., None]
    img = mix(img, fcol, field)
    mist = np.exp(-((v - hz - 0.02) / 0.06) ** 2) * (0.6 + 0.4 * clouds)
    img = mix(img, "#9a9a94", mist * 0.6)
    # lone dead tree (re-use the foliage cutout)
    tree = load_tex("Foliage/tree_dead_1", "RGBA")
    th = int(H * 0.5)
    tr = resize(tree, th, th, Image.NEAREST)
    img = paste(img, tr[..., :3] * 0.5, int(W * 0.18), int(H * hz - th * 0.9), (tr[..., 3] > 0.5).astype(np.float32))
    # fence posts
    for i, px in enumerate(np.linspace(0.55, 0.98, 6)):
        ph = 0.06 + 0.1 * (px - 0.5)
        img = mix(img, "#1e1c18", ((np.abs(u - px) < 0.004 + 0.004 * (px - 0.5)) & (v > hz + 0.06 - ph) & (v < hz + 0.06 + ph * 0.6)).astype(np.float32))
    return photo_finish(ctx, clamp01(img), vig=0.45, grain=0.07)
