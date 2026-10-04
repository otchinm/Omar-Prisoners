"""Textures/Decals - RGBA overlays (blood, graffiti, stains, road paint, scrawled writing, cracks)."""
import numpy as np
from PIL import Image, ImageDraw

from .core import (blur, canvas, clamp01, col, cracks_mask, drips, fft_noise, gradient_map, lerp, mix, radial,
                   smoothstep, solid, splat_mask, texture, to_mask, water_stains, warp, ellipse_mask)
from .mat import grime, photo

# --------------------------------------------------------------------------------------
# helpers
# --------------------------------------------------------------------------------------
STROKES = {
    "A": [[(0, 1), (0.5, 0), (1, 1)], [(0.2, 0.62), (0.8, 0.62)]],
    "E": [[(1, 0), (0, 0), (0, 1), (1, 1)], [(0, 0.5), (0.8, 0.5)]],
    "H": [[(0, 0), (0, 1)], [(1, 0), (1, 1)], [(0, 0.5), (1, 0.5)]],
    "L": [[(0, 0), (0, 1), (1, 1)]],
    "M": [[(0, 1), (0, 0), (0.5, 0.6), (1, 0), (1, 1)]],
    "O": [[(0.5 + 0.5 * np.sin(t), 0.5 - 0.5 * np.cos(t)) for t in np.linspace(0, 2 * np.pi, 14)]],
    "P": [[(0, 1), (0, 0), (0.75, 0), (1, 0.15), (1, 0.35), (0.75, 0.5), (0, 0.5)]],
    "R": [[(0, 1), (0, 0), (0.75, 0), (1, 0.15), (1, 0.35), (0.75, 0.5), (0, 0.5)], [(0.4, 0.5), (1, 1)]],
    "S": [[(1, 0.12), (0.7, 0), (0.3, 0), (0, 0.18), (0.1, 0.42), (0.5, 0.5), (0.9, 0.58), (1, 0.82), (0.7, 1), (0.3, 1), (0, 0.88)]],
    "I": [[(0.5, 0), (0.5, 1)]],
    "T": [[(0, 0), (1, 0)], [(0.5, 0), (0.5, 1)]],
    "U": [[(0, 0), (0, 0.8), (0.2, 1), (0.8, 1), (1, 0.8), (1, 0)]],
    "N": [[(0, 1), (0, 0), (1, 1), (1, 0)]],
    "D": [[(0, 0), (0, 1), (0.6, 1), (1, 0.7), (1, 0.3), (0.6, 0), (0, 0)]],
    "G": [[(1, 0.15), (0.7, 0), (0.3, 0), (0, 0.3), (0, 0.7), (0.3, 1), (0.7, 1), (1, 0.75), (1, 0.55), (0.55, 0.55)]],
    "Y": [[(0, 0), (0.5, 0.5), (1, 0)], [(0.5, 0.5), (0.5, 1)]],
    "K": [[(0, 0), (0, 1)], [(1, 0), (0, 0.55)], [(0.3, 0.4), (1, 1)]],
    "W": [[(0, 0), (0.25, 1), (0.5, 0.4), (0.75, 1), (1, 0)]],
    "C": [[(1, 0.15), (0.7, 0), (0.3, 0), (0, 0.3), (0, 0.7), (0.3, 1), (0.7, 1), (1, 0.85)]],
    "!": [[(0.5, 0), (0.5, 0.7)], [(0.5, 0.9), (0.5, 1)]],
    "B": [[(0, 1), (0, 0), (0.7, 0), (0.95, 0.12), (0.95, 0.35), (0.7, 0.48), (0, 0.48)], [(0.7, 0.48), (1, 0.62), (1, 0.85), (0.7, 1), (0, 1)]],
    "F": [[(1, 0), (0, 0), (0, 1)], [(0, 0.48), (0.75, 0.48)]],
    "V": [[(0, 0), (0.5, 1), (1, 0)]],
    "'": [[(0.5, 0), (0.45, 0.3)]],
    "?": [[(0, 0.2), (0.3, 0), (0.75, 0), (1, 0.2), (1, 0.38), (0.5, 0.55), (0.5, 0.72)], [(0.5, 0.9), (0.5, 1)]],
}


def scrawl(w, h, text, rng, box, width, slant=0.15, jitter=0.06, passes=3, letter_gap=0.25):
    """hand-painted capital letters from stroke definitions (finger / brush look). Returns mask."""
    im, d = canvas(w, h)
    x0, y0, x1, y1 = box
    n = len(text)
    lw = (x1 - x0) / (n + (n - 1) * letter_gap)
    lh = y1 - y0
    x = x0
    for ch in text:
        if ch == " ":
            x += lw * (1 + letter_gap)
            continue
        sc = rng.uniform(0.85, 1.12)
        rot = rng.normal(0, 0.08)
        dy = rng.normal(0, lh * 0.05)
        for stroke in STROKES.get(ch, []):
            for p in range(passes):
                pts = []
                for (u, v) in stroke:
                    u2 = u + rng.normal(0, jitter)
                    v2 = v + rng.normal(0, jitter)
                    px = x + (u2 - 0.5) * lw * sc * np.cos(rot) - (v2 - 0.5) * lh * sc * np.sin(rot) + lw / 2 + (0.5 - v2) * lh * slant
                    py = y0 + dy + (u2 - 0.5) * lw * sc * np.sin(rot) + (v2 - 0.5) * lh * sc * np.cos(rot) + lh / 2
                    pts.append((px, py))
                wd = int(width * rng.uniform(0.6, 1.0))
                d.line(pts, fill=255, width=max(1, wd), joint="curve")
                for (px, py) in (pts[0], pts[-1]):
                    d.ellipse([px - wd / 2, py - wd / 2, px + wd / 2, py + wd / 2], fill=255)
        x += lw * (1 + letter_gap)
    return to_mask(im)


def drip_from(mask, rng, n, max_len, width, h):
    """paint drips running down from the lowest painted pixels of random columns"""
    H, W = mask.shape
    im, d = canvas(W, H)
    cols = np.where(mask.max(0) > 0.5)[0]
    if len(cols) == 0:
        return mask
    for _ in range(n):
        c = int(rng.choice(cols))
        rows = np.where(mask[:, c] > 0.5)[0]
        y = rows.max()
        L = rng.uniform(0.2, 1.0) * max_len
        wd = rng.uniform(0.5, 1.0) * width
        pts = [(c + rng.normal(0, 0.5), y)]
        for k in range(1, 6):
            pts.append((c + rng.normal(0, wd * 0.15), y + L * k / 5))
        d.line(pts, fill=255, width=max(1, int(wd)))
        ex, ey = pts[-1]
        d.ellipse([ex - wd * 0.7, ey - wd * 0.5, ex + wd * 0.7, ey + wd * 0.9], fill=255)
    return np.maximum(mask, to_mask(im))


def blood_color(ctx, H, W, salt=0, dark=0.0):
    r = ctx.sub(300 + salt)
    t = clamp01(0.5 + 0.18 * fft_noise(r, H, W, beta=2.4) + 0.1 * photo(ctx, "gravel_fine", H, W, salt=salt) - dark)
    return gradient_map(t, [(0, "#2a0202"), (0.4, "#5e0807"), (0.75, "#8a100c"), (1, "#a82018")])


def rough_edges(mask, ctx, amt=2.0, salt=0):
    H, W = mask.shape
    r = ctx.sub(500 + salt)
    dx = fft_noise(r, H, W, beta=1.6) * amt
    dy = fft_noise(r, H, W, beta=1.6) * amt
    return warp(mask, dx, dy)


# --------------------------------------------------------------------------------------
# blood
# --------------------------------------------------------------------------------------
@texture("Decals/blood_pool", (128, 128), alpha="hard", q=45, desat=0.0, dark=0.97)
def blood_pool(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    m = splat_mask(r, W, H, W * 0.5, H * 0.52, W * 0.3, drops=10, irregular=0.25)
    m2 = splat_mask(ctx.sub(2), W, H, W * 0.68, H * 0.4, W * 0.16, drops=4, irregular=0.3)
    m = np.maximum(m, m2)
    m = rough_edges(blur(m, 3), ctx, 4) > 0.5
    m = m.astype(np.float32)
    from .core import edge_distance
    e = edge_distance(m)
    depth = smoothstep(0, W * 0.08, e)
    img = blood_color(ctx, H, W)
    img = img * (0.75 + 0.35 * (1 - depth))[..., None]  # thin rim slightly brighter, pool darker
    img = mix(img, "#140000", depth * 0.55)
    # glossy reflection streak
    gl = ellipse_mask(W, H, W * 0.42, H * 0.42, W * 0.1, H * 0.03, soft=0.8) * m
    img = mix(img, "#8a2a24", gl * 0.4)
    return img, m


def splatter(ctx, salt, cx, cy, rad, drops, direction=None):
    W, H = ctx.W, ctx.H
    r = ctx.sub(10 + salt)
    m = splat_mask(r, W, H, cx * W, cy * H, rad * W, drops=drops, irregular=0.45)
    if direction is not None:
        im, d = canvas(W, H)
        for _ in range(drops * 2):
            a = direction + r.normal(0, 0.35)
            dist = r.uniform(0.15, 0.48) * W
            px, py = cx * W + np.cos(a) * dist, cy * H + np.sin(a) * dist
            rr = r.uniform(1.0, 4.0) * W / 512 * 4
            # elongated drop with tail pointing back
            d.ellipse([px - rr, py - rr, px + rr, py + rr], fill=255)
            d.line([(px, py), (px - np.cos(a) * rr * 4, py - np.sin(a) * rr * 4)], fill=255, width=max(1, int(rr)))
        m = np.maximum(m, to_mask(im))
    m = (rough_edges(blur(m, 1.5), ctx, 2.5, salt) > 0.5).astype(np.float32)
    img = blood_color(ctx, H, W, salt)
    from .core import edge_distance
    e = edge_distance(m)
    img = mix(img, "#160000", smoothstep(0, W * 0.05, e) * 0.5)
    return img, m


@texture("Decals/blood_splatter_1", (128, 128), alpha="hard", q=45, desat=0.0, dark=0.97)
def blood_splatter_1(ctx):
    return splatter(ctx, 1, 0.5, 0.5, 0.17, 26)


@texture("Decals/blood_splatter_2", (128, 128), alpha="hard", q=45, desat=0.0, dark=0.97)
def blood_splatter_2(ctx):
    return splatter(ctx, 2, 0.32, 0.55, 0.12, 14, direction=-0.2)


@texture("Decals/blood_splatter_3", (128, 128), alpha="hard", q=45, desat=0.0, dark=0.97)
def blood_splatter_3(ctx):
    W, H = ctx.W, ctx.H
    img, m = splatter(ctx, 3, 0.5, 0.25, 0.1, 10)
    # vertical drips running down (wall splatter)
    r = ctx.sub(33)
    m = drip_from(m, r, 9, H * 0.6, W * 0.025, H)
    m = (blur(m, 1.0) > 0.5).astype(np.float32)
    img = blood_color(ctx, H, W, 3)
    return img, m


@texture("Decals/blood_smear", (128, 64), alpha="hard", q=45, desat=0.0, dark=0.97)
def blood_smear(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # drag: band that thins out to the right, with finger streaks
    t = xx / W
    centre = H * (0.5 + 0.08 * np.sin(t * 3.0))
    half = H * (0.32 - 0.22 * t)
    band = (np.abs(yy - centre) < half).astype(np.float32)
    streaks = fft_noise(r, H, W, beta=1.4, ax=10, ay=1)
    cover = smoothstep(-0.2 - 1.3 * (1 - t), 0.3 - 1.3 * (1 - t), -streaks)
    m = band * (1 - cover * smoothstep(0.15, 0.6, t))
    m = (rough_edges(blur(m, 2), ctx, 3) > 0.5).astype(np.float32)
    # start blob on the left
    m = np.maximum(m, (rough_edges(ellipse_mask(W, H, W * 0.08, H * 0.5, W * 0.08, H * 0.36), ctx, 3, 1) > 0.5).astype(np.float32))
    img = blood_color(ctx, H, W, 4)
    thin = smoothstep(0.3, 1.0, t)
    img = mix(img, "#8a2a20", (thin * 0.35 * (streaks > 0)))
    return img, m


@texture("Decals/blood_handprint", (64, 64), alpha="hard", q=50, desat=0.0, dark=0.97)
def blood_handprint(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    im, d = canvas(W, H)
    # palm
    d.polygon([(170 * s, 260 * s), (350 * s, 250 * s), (370 * s, 420 * s), (300 * s, 480 * s), (200 * s, 470 * s), (150 * s, 380 * s)], fill=255)
    fingers = [((180, 255), (150, 110), 34), ((235, 245), (225, 60), 36), ((290, 245), (300, 65), 35), ((340, 260), (380, 120), 30)]
    for (a, b, wd) in fingers:
        d.line([(a[0] * s, a[1] * s), (b[0] * s, b[1] * s)], fill=255, width=int(wd * 2 * s))
        d.ellipse([b[0] * s - wd * s, b[1] * s - wd * s, b[0] * s + wd * s, b[1] * s + wd * s], fill=255)
    d.line([(165 * s, 380 * s), (80 * s, 300 * s)], fill=255, width=int(66 * s))
    d.ellipse([50 * s, 270 * s, 115 * s, 335 * s], fill=255)
    m = to_mask(im)
    # partial print: pressure gaps
    gaps = smoothstep(1.0, 1.5, fft_noise(r, H, W, beta=1.8))
    m = m * (1 - gaps)
    m = drip_from((m > 0.5).astype(np.float32), r, 4, H * 0.15, 10 * s, H)
    m = (rough_edges(blur(m, 3 * s), ctx, 6 * s) > 0.5).astype(np.float32)
    img = blood_color(ctx, H, W, 5)
    return img, m


# --------------------------------------------------------------------------------------
# graffiti / writing
# --------------------------------------------------------------------------------------
def marker_color(ctx, H, W, salt=0, base="#0e0c0c"):
    r = ctx.sub(600 + salt)
    t = clamp01(0.5 + 0.25 * fft_noise(r, H, W, beta=2.0))
    return gradient_map(t, [(0, "#050404"), (0.6, base), (1, "#2a2422")])


@texture("Decals/graffiti_scrawl_1", (128, 128), alpha="hard", q=45)
def graffiti_scrawl_1(ctx):
    """crude pentagram / star + circle (like the clock bedroom walls)"""
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    im, d = canvas(W, H)
    cx, cy, R = W * 0.5, H * 0.52, W * 0.4
    pts = []
    for i in range(5):
        a = -np.pi / 2 + i * 4 * np.pi / 5
        pts.append((cx + np.cos(a) * R + r.normal(0, 8 * s), cy + np.sin(a) * R + r.normal(0, 8 * s)))
    pts.append((pts[0][0] + r.normal(0, 6 * s), pts[0][1] + 15 * s))
    for p in range(2):
        d.line([(x + r.normal(0, 3 * s), y + r.normal(0, 3 * s)) for x, y in pts], fill=255, width=int(14 * s), joint="curve")
    circ = [(cx + np.cos(t) * R * 1.08 + r.normal(0, 5 * s), cy + np.sin(t) * R * 1.05 + r.normal(0, 5 * s)) for t in np.linspace(0.3, 2 * np.pi + 0.1, 30)]
    d.line(circ, fill=255, width=int(10 * s), joint="curve")
    m = to_mask(im)
    m = drip_from((m > 0.5).astype(np.float32), r, 6, H * 0.12, 7 * s, H)
    m = (rough_edges(blur(m, 1.5 * s), ctx, 3 * s) > 0.5).astype(np.float32)
    return marker_color(ctx, H, W), m


@texture("Decals/graffiti_scrawl_2", (128, 128), alpha="hard", q=45)
def graffiti_scrawl_2(ctx):
    """tally marks (days counted), an eye and runes"""
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    im, d = canvas(W, H)
    # tally groups
    for gy, gx, cnt in ((60, 40, 5), (60, 200, 5), (60, 360, 3), (170, 40, 5), (170, 200, 2)):
        for i in range(min(cnt, 4)):
            x = (gx + i * 26) * s + r.normal(0, 3 * s)
            d.line([(x, gy * s + r.normal(0, 4 * s)), (x + r.normal(0, 4 * s), (gy + 80) * s)], fill=255, width=int(10 * s))
        if cnt == 5:
            d.line([((gx - 15) * s, (gy + 65) * s), ((gx + 100) * s, (gy + 15) * s)], fill=255, width=int(10 * s))
    # eye
    ex, ey = 256 * s, 370 * s
    up = [(ex + (t - 0.5) * 240 * s, ey - np.sin(t * np.pi) * 60 * s) for t in np.linspace(0, 1, 12)]
    dn = [(ex + (t - 0.5) * 240 * s, ey + np.sin(t * np.pi) * 55 * s) for t in np.linspace(0, 1, 12)]
    d.line(up, fill=255, width=int(11 * s), joint="curve")
    d.line(dn, fill=255, width=int(11 * s), joint="curve")
    d.ellipse([ex - 30 * s, ey - 30 * s, ex + 30 * s, ey + 30 * s], fill=255)
    for k in range(5):  # lashes / rays
        a = np.pi + 0.3 + k * 0.6
        d.line([(ex + np.cos(a) * 80 * s, ey + np.sin(a) * 70 * s), (ex + np.cos(a) * 130 * s, ey + np.sin(a) * 115 * s)], fill=255, width=int(8 * s))
    # runes along the bottom
    for i in range(5):
        x = (60 + i * 90) * s
        y = 470 * s
        pts = [(x + r.uniform(-20, 20) * s, y + r.uniform(-30, 30) * s) for _ in range(3)]
        d.line(pts, fill=255, width=int(8 * s))
    m = to_mask(im)
    m = (rough_edges(blur(m, 1.5 * s), ctx, 3 * s) > 0.5).astype(np.float32)
    return marker_color(ctx, H, W, 1), m


def blood_writing(ctx, text, box_frac, width, drips_n):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    box = (box_frac[0] * W, box_frac[1] * H, box_frac[2] * W, box_frac[3] * H)
    m = scrawl(W, H, text, r, box, width, slant=0.12, jitter=0.05, passes=3)
    m = drip_from((m > 0.5).astype(np.float32), r, drips_n, H * 0.3, width * 0.45, H)
    m = (rough_edges(blur(m, W / 512), ctx, W / 256) > 0.5).astype(np.float32)
    img = blood_color(ctx, H, W, 9)
    return img, m


@texture("Decals/writing_help", (128, 64), alpha="hard", q=45, desat=0.0, dark=0.97)
def writing_help(ctx):
    return blood_writing(ctx, "HELP", (0.06, 0.1, 0.94, 0.66), ctx.W * 0.045, 12)


@texture("Decals/writing_omar", (128, 64), alpha="hard", q=45, desat=0.0, dark=0.97)
def writing_omar(ctx):
    return blood_writing(ctx, "OMAR SEES", (0.03, 0.12, 0.97, 0.62), ctx.W * 0.026, 14)


# ---- iteration 2: varied writings (placed sparingly and where they make sense)
@texture("Decals/writing_let_me_out", (128, 64), alpha="hard", q=45, desat=0.0, dark=0.97)
def writing_let_me_out(ctx):
    return blood_writing(ctx, "LET ME OUT", (0.03, 0.14, 0.97, 0.6), ctx.W * 0.026, 10)


@texture("Decals/writing_he_sees_you", (128, 64), alpha="hard", q=45, desat=0.0, dark=0.97)
def writing_he_sees_you(ctx):
    return blood_writing(ctx, "HE SEES YOU", (0.03, 0.16, 0.97, 0.58), ctx.W * 0.024, 9)


@texture("Decals/writing_dont_look", (128, 64), alpha="hard", q=45)
def writing_dont_look(ctx):
    """charcoal / marker: DON'T LOOK BACK"""
    W, H = ctx.W, ctx.H
    r = ctx.sub(3)
    m = scrawl(W, H, "DON'T LOOK", r, (0.04 * W, 0.18 * H, 0.96 * W, 0.6 * H), W * 0.02, slant=0.1, jitter=0.07, passes=2)
    m = (rough_edges(blur(m, W / 512), ctx, W / 256) > 0.5).astype(np.float32)
    return marker_color(ctx, H, W, 4), m


@texture("Decals/writing_names", (128, 128), alpha="hard", q=45)
def writing_names(ctx):
    """names scratched into the plaster and crossed out, one after another"""
    W, H = ctx.W, ctx.H
    r = ctx.sub(5)
    words = ["ANNA", "TOM", "SARAH", "MIKE", "LEE", "KATE"]
    m = np.zeros((H, W), np.float32)
    im, d = canvas(W, H)
    for i, wd in enumerate(words):
        y0 = (0.05 + i * 0.155) * H
        x0 = (0.06 + 0.12 * r.uniform()) * W
        x1 = min(W * 0.96, x0 + len(wd) * 0.13 * W)
        m = np.maximum(m, scrawl(W, H, wd, r, (x0, y0, x1, y0 + 0.1 * H), W * 0.012, slant=0.05, jitter=0.08, passes=2))
        if i < len(words) - 1:  # crossed out - all but the last one
            d.line([(x0 - 4, y0 + 0.06 * H + r.normal(0, 2)), (x1 + 4, y0 + 0.04 * H + r.normal(0, 2))], fill=255, width=max(2, int(W * 0.012)))
    m = np.maximum(m, to_mask(im))
    m = (rough_edges(blur(m, W / 512), ctx, W / 256) > 0.5).astype(np.float32)
    return marker_color(ctx, H, W, 5, "#1a1612"), m


def _cross_mask(W, H, r, crosses, width):
    im, d = canvas(W, H)
    for (cx, cy, size, rot, inv) in crosses:
        a = np.radians(rot)
        def pt(x, y):
            return (cx + (x * np.cos(a) - y * np.sin(a)) * size + r.normal(0, size * 0.02),
                    cy + (x * np.sin(a) + y * np.cos(a)) * size + r.normal(0, size * 0.02))
        bar = 0.25 if inv else -0.25
        for _ in range(2):
            d.line([pt(0, -0.6), pt(0, 0.6)], fill=255, width=int(width))
            d.line([pt(-0.35, bar), pt(0.35, bar)], fill=255, width=int(width))
    return to_mask(im)


@texture("Decals/crosses_1", (128, 128), alpha="hard", q=45)
def crosses_1(ctx):
    """a cluster of crosses daubed on the wall (the grandmother's room)"""
    W, H = ctx.W, ctx.H
    r = ctx.sub(7)
    cr = [(W * 0.5, H * 0.42, W * 0.38, r.normal(0, 4), False), (W * 0.18, H * 0.72, W * 0.16, r.normal(0, 8), False),
          (W * 0.82, H * 0.7, W * 0.14, r.normal(0, 8), False), (W * 0.32, H * 0.14, W * 0.1, r.normal(0, 10), False)]
    m = _cross_mask(W, H, r, cr, W * 0.035)
    m = drip_from((m > 0.5).astype(np.float32), r, 5, H * 0.1, W * 0.012, H)
    m = (rough_edges(blur(m, W / 512), ctx, W / 256) > 0.5).astype(np.float32)
    return marker_color(ctx, H, W, 7, "#141010"), m


@texture("Decals/crosses_2", (128, 128), alpha="hard", q=45, desat=0.0, dark=0.97)
def crosses_2(ctx):
    """upside-down crosses in dried blood"""
    W, H = ctx.W, ctx.H
    r = ctx.sub(8)
    cr = [(W * 0.3, H * 0.45, W * 0.3, r.normal(0, 5), True), (W * 0.72, H * 0.55, W * 0.24, r.normal(0, 6), True)]
    m = _cross_mask(W, H, r, cr, W * 0.03)
    m = drip_from((m > 0.5).astype(np.float32), r, 8, H * 0.22, W * 0.014, H)
    m = (rough_edges(blur(m, W / 512), ctx, W / 256) > 0.5).astype(np.float32)
    return blood_color(ctx, H, W, 11), m


@texture("Decals/nail_scratches", (128, 128), alpha="hard", q=45)
def nail_scratches(ctx):
    """desperate finger nail scratches (inside the cages / on doors)"""
    W, H = ctx.W, ctx.H
    r = ctx.sub(9)
    im, d = canvas(W, H)
    for g in range(4):
        x0, y0 = r.uniform(0.1, 0.8) * W, r.uniform(0.05, 0.4) * H
        ln = r.uniform(0.3, 0.55) * H
        slope = r.uniform(-0.15, 0.15)
        for k in range(4):
            xs = x0 + k * W * 0.035
            d.line([(xs, y0 + k * 3), (xs + slope * ln, y0 + ln - k * 6)], fill=255, width=max(1, int(W * 0.008)))
    m = to_mask(im)
    m = (rough_edges(blur(m, W / 768), ctx, W / 400) > 0.5).astype(np.float32)
    img = gradient_map(clamp01(0.5 + 0.2 * fft_noise(r, H, W, beta=2.0)), [(0, "#2a1a14"), (1, "#5a3a2a")])
    return img, m


# --------------------------------------------------------------------------------------
# stains / paint / cracks
# --------------------------------------------------------------------------------------
@texture("Decals/grime", (128, 128), alpha="soft", q=50, bits=5)
def grime_decal(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    rad = radial(W, H)
    n = fft_noise(r, H, W, beta=2.4) * 0.35 + photo(ctx, "gravel", salt=1) * 0.15
    a = clamp01(1.0 - smoothstep(0.35, 0.95, rad + n * 0.5))
    a = a * clamp01(0.55 + 0.45 * smoothstep(-0.8, 0.6, fft_noise(ctx.sub(2), H, W, beta=2.0)))
    img = gradient_map(clamp01(0.5 + 0.3 * n), [(0, "#0e0c08"), (1, "#3a3224")])
    return img, a * 0.92


@texture("Decals/water_stain", (128, 128), alpha="soft", q=50)
def water_stain(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    rad = radial(W, H, r=W * 0.42)
    n = fft_noise(r, H, W, beta=2.2)
    d = rad + n * 0.12
    fill = 1 - smoothstep(0.92, 1.0, d)
    ring = np.exp(-((d - 0.96) / 0.05) ** 2) + 0.6 * np.exp(-((d - 0.68) / 0.04) ** 2) + 0.35 * np.exp(-((d - 0.42) / 0.04) ** 2)
    a = clamp01(fill * 0.28 * clamp01(0.4 + 0.6 * d) + ring * 0.75)
    img = gradient_map(clamp01(0.5 + 0.3 * n), [(0, "#3a2408"), (1, "#7a5a2a")])
    return img, a


@texture("Decals/parking_line", (64, 16), alpha="hard", k=8, q=50, desat=0.0)
def parking_line(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    worn = smoothstep(0.9, 1.4, fft_noise(r, H, W, beta=1.8) + 0.6 * photo(ctx, "gravel_fine", H, W, salt=1))
    yy = np.arange(H)[:, None]
    stripe = ((yy > H * 0.18) & (yy < H * 0.82)).astype(np.float32) * np.ones((1, W))
    m = (stripe * (1 - worn) > 0.5).astype(np.float32)
    img = gradient_map(clamp01(0.5 + 0.2 * fft_noise(ctx.sub(2), H, W, beta=2.0)), [(0, "#7a5a10"), (1, "#b08a22")])
    return img, m


@texture("Decals/road_dashes", (64, 16), alpha="hard", k=8, q=50, desat=0.0)
def road_dashes(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    worn = smoothstep(0.9, 1.4, fft_noise(r, H, W, beta=1.8) + 0.6 * photo(ctx, "gravel_fine", H, W, salt=2))
    yy, xx = np.mgrid[0:H, 0:W]
    dash = ((yy > H * 0.15) & (yy < H * 0.85) & (xx > W * 0.04) & (xx < W * 0.96)).astype(np.float32)
    m = (dash * (1 - worn) > 0.5).astype(np.float32)
    img = gradient_map(clamp01(0.5 + 0.2 * fft_noise(ctx.sub(2), H, W, beta=2.0)), [(0, "#8a8a84"), (1, "#bcbcb4")])
    return img, m


@texture("Decals/cracks", (128, 128), alpha="hard", q=45)
def cracks(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    im, d = canvas(W, H)
    cx, cy = W * 0.5, H * 0.5

    def crack(x, y, a, depth, wd, n):
        pts = [(x, y)]
        for _ in range(n):
            a += r.normal(0, 0.35)
            L = r.uniform(18, 40) * s
            x, y = x + np.cos(a) * L, y + np.sin(a) * L
            pts.append((x, y))
            if depth < 3 and r.random() < 0.3:
                crack(x, y, a + r.choice([-1, 1]) * r.uniform(0.5, 1.1), depth + 1, max(2, wd * 0.6), max(2, n // 2))
        d.line(pts, fill=255, width=int(max(2, wd)), joint="curve")

    for k in range(6):
        crack(cx, cy, k * np.pi / 3 + r.normal(0, 0.3), 0, 12 * s, 6)
    d.ellipse([cx - 22 * s, cy - 22 * s, cx + 22 * s, cy + 22 * s], fill=255)
    m = to_mask(im)
    m = m * (radial(W, H) < 0.98)
    img = gradient_map(clamp01(0.5 + 0.3 * fft_noise(ctx.sub(2), H, W, beta=2.0)), [(0, "#060606"), (1, "#1e1c1a")])
    return img, (m > 0.5).astype(np.float32)
