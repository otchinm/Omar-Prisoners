"""Item textures (Resources/Textures/Items). Region layouts MUST match ItemMeshFactory.cs (pixel rects, origin top-left).

Each function paints at 4x, downsamples and degrades. Returns (rgb float array, alpha or None).
"""
import numpy as np
from PIL import ImageDraw

from paint import (rgb, fill, uv_grid, fbm, photo_detail, smoothstep, mix, shade, blur, text_mask, downsample, degrade,
                   fabric, blood, blood_color, grime, draw_mask, photo_color, skin, hair_strands, soft_ellipse)

S = 4


class Canvas:
    def __init__(self, w, h, seed):
        self.w, self.h = w, h
        self.img = np.zeros((h * S, w * S, 3), np.float32)
        self.alpha = None
        self.rng = np.random.RandomState(seed)

    def region(self, x, y, w, h):
        return self.img[y * S:(y + h) * S, x * S:(x + w) * S]

    def put(self, x, y, arr):
        h, w = arr.shape[:2]
        self.img[y * S:y * S + h, x * S:x * S + w] = arr

    def done(self, bits=5, q=68, desat=0.08):
        small = downsample(self.img, S)
        out = degrade(small, self.rng, bits=bits, jpeg_q=q, desat=desat)
        return out, self.alpha


def metal(h, w, color, rng, brushed=True, scratches=0.3, rust=0.0):
    base = fill(h, w, rgb(color))
    if brushed:
        streak = rng.rand(h, 1).astype(np.float32)
        streak = np.repeat(streak, w, axis=1)
        streak = blur(streak, (0.5)) if False else streak
        base = shade(base, 0.9 + 0.2 * streak)
    base = shade(base, 1 + photo_detail("coins", h, w, rng, zoom=2.5, sigma=2) * 0.06)
    if scratches:
        m = np.zeros((h, w), np.float32)
        for _ in range(int(scratches * 40)):
            x0, y0 = rng.randint(0, w), rng.randint(0, h)
            ln = rng.randint(4, max(5, w // 2))
            ang = rng.uniform(-0.4, 0.4)
            xs = np.clip((x0 + np.arange(ln) * np.cos(ang)).astype(int), 0, w - 1)
            ys = np.clip((y0 + np.arange(ln) * np.sin(ang)).astype(int), 0, h - 1)
            m[ys, xs] = 1
        base = mix(base, np.clip(rgb(color) * 1.35, 0, 1), m * 0.6)
    if rust:
        n = fbm(h, w, 6, rng, octaves=4)
        r = photo_detail("ihc", h, w, rng, zoom=2, sigma=4)
        base = mix(base, rgb("#6a3a1c"), smoothstep(0.55, 0.75, n + r * 0.05) * rust)
        base = mix(base, rgb("#3a2010"), smoothstep(0.7, 0.85, n) * rust * 0.7)
    return base


def plastic(h, w, color, rng, gloss=0.1, dirt=0.2):
    base = fill(h, w, rgb(color))
    n = fbm(h, w, 3, rng, octaves=3)
    base = shade(base, 0.92 + 0.16 * n)
    base = shade(base, 1 + photo_detail("gravel", h, w, rng, zoom=4, sigma=1) * 0.025)
    return grime(base, rng, amount=dirt, color=(0.15, 0.12, 0.1), scale=4)


def paper(h, w, color, rng, dirt=0.25):
    base = fill(h, w, rgb(color))
    base = shade(base, 1 + photo_detail("page", h, w, rng, zoom=1.5, sigma=3) * 0.03)
    return grime(base, rng, amount=dirt, color=(0.45, 0.4, 0.3), scale=4)


def text_lines(h, w, rng, x0, y0, x1, y1, rows, color, img, alpha=0.8, thick=1):
    m = np.zeros((h, w), np.float32)
    ys = np.linspace(y0, y1, rows)
    for y in ys:
        x = x0
        while x < x1:
            ln = rng.randint(2, 8) * S
            m[int(y):int(y) + thick * S, int(x):int(min(x + ln, x1))] = 1
            x += ln + S * 1.5
    return mix(img, rgb(color), m * alpha)


# --------------------------------------------------------------------------------------------- items
def lighter():
    """Zippo like the first-person reference: olive case, pale brass perforated chimney, dark blue flint wheel."""
    c = Canvas(64, 64, 11)
    r = c.rng
    olive = "#59632e"

    def case(h, w, light=1.0):
        base = metal(h, w, olive, r, brushed=True, scratches=0.35)
        base = shade(base, light)
        # crackle-paint speckle + worn edges showing the brass underneath
        n = fbm(h, w, 14, r, octaves=3)
        base = mix(base, rgb("#3c4420"), smoothstep(0.62, 0.7, n) * 0.6)
        u, v = uv_grid(h, w)
        edge = np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v))
        wear = smoothstep(0.07, 0.0, edge) * smoothstep(0.35, 0.65, fbm(h, w, 6, r, octaves=3))
        base = mix(base, rgb("#b09650"), wear * 0.8)
        return grime(base, r, 0.25, (0.12, 0.12, 0.06))

    front = case(32 * S, 32 * S)
    u, v = uv_grid(32 * S, 32 * S)
    front = shade(front, 1 + 0.18 * np.exp(-((u - 0.28) / 0.07) ** 2))      # rounded corner highlight
    c.put(0, 0, front)
    c.put(32, 0, case(32 * S, 16 * S, 0.8))
    lid = case(16 * S, 32 * S)
    lu, lv = uv_grid(16 * S, 32 * S)
    lid = shade(lid, 1 + 0.18 * np.exp(-((lu - 0.28) / 0.07) ** 2))
    c.put(0, 32, lid)

    # chimney: pale brass sheet with big round holes in a 3 x 3 grid
    chim = metal(16 * S, 16 * S, "#d8cf9a", r, scratches=0.15)
    yy, xx = np.mgrid[0:16 * S, 0:16 * S] / float(S)
    holes = np.zeros((16 * S, 16 * S), np.float32)
    for cy in (3.2, 8.0, 12.8):
        for cx in (2.8, 8.0, 13.2):
            ox = 2.6 if cy == 8.0 else 0.0
            d = np.sqrt((xx - cx - ox) ** 2 + (yy - cy) ** 2)
            holes = np.maximum(holes, smoothstep(1.9, 1.3, d))
    chim = shade(chim, 1 - 0.25 * smoothstep(2.6, 1.6, np.sqrt(0) + 0) * 0)
    chim = mix(chim, rgb("#1a120a"), holes)
    c.put(32, 32, grime(chim, r, 0.2, (0.3, 0.25, 0.1)))
    c.put(48, 0, metal(16 * S, 16 * S, "#c8bc84", r, scratches=0.3))           # case / insert top
    c.put(48, 16, metal(16 * S, 16 * S, "#bdb289", r, scratches=0.2))          # insert
    wheel = metal(16 * S, 16 * S, "#1c2a6a", r, brushed=False, scratches=0.0)  # dark blue flint wheel
    wheel = shade(wheel, 1 - 0.35 * ((np.mgrid[0:16 * S, 0:16 * S][1] // S) % 2 == 0))
    c.put(48, 32, wheel)
    c.put(0, 48, metal(16 * S, 32 * S, "#2c2a22", r, brushed=False, scratches=0.1))  # inside of the lid
    c.put(32, 48, fill(16 * S, 32 * S, rgb("#3a3a36")))
    return c.done()


def lighterfuel():
    c = Canvas(128, 64, 12)
    r = c.rng
    blue = "#1a2a8a"
    h, w = 64 * S, 64 * S
    f = metal(h, w, blue, r, brushed=False, scratches=0.15)
    u, v = uv_grid(h, w)
    band = (v > 0.48) & (v < 0.86)
    f = mix(f, rgb("#e8d020"), band.astype(np.float32))
    f = mix(f, rgb("#0a1a6a"), ((np.abs(v - 0.48) < 0.012) | (np.abs(v - 0.86) < 0.012)).astype(np.float32))
    t = text_mask(h, w, "LIGHTER\nFUEL", w * 0.5, h * 0.33, int(h * 0.15), stroke=2, spacing=2, stretch_x=0.95)
    f = mix(f, rgb("#14207a"), t)
    em = draw_mask(h, w, lambda d: d.ellipse([w * 0.4, h * 0.58, w * 0.6, h * 0.74], fill=255))
    f = mix(f, rgb("#e8d020"), em)
    f = text_lines(h, w, r, w * 0.15, h * 0.8, w * 0.85, h * 0.93, 3, "#c8c8e0", f, 0.6)
    f = grime(f, r, 0.2, (0.1, 0.1, 0.1))
    c.put(0, 0, f)
    b = metal(64 * S, 32 * S, blue, r, brushed=False, scratches=0.15)
    b = text_lines(64 * S, 32 * S, r, 3 * S, 10 * S, 29 * S, 50 * S, 10, "#d0d0e0", b, 0.6)
    c.put(64, 0, b)
    c.put(96, 0, metal(64 * S, 16 * S, blue, r, brushed=False, scratches=0.2))
    c.put(112, 0, metal(16 * S, 16 * S, "#9a9aa0", r))
    c.put(112, 16, plastic(16 * S, 16 * S, "#b01818", r, dirt=0.1))
    c.put(112, 32, plastic(16 * S, 16 * S, "#e0e0d8", r, dirt=0.1))
    c.put(112, 48, fill(16 * S, 16 * S, rgb("#202020")))
    return c.done()


def bandages():
    c = Canvas(64, 64, 13)
    r = c.rng
    h, w = 48 * S, 32 * S
    f = paper(h, w, "#e8e8e4", r)
    u, v = uv_grid(h, w)
    f = mix(f, rgb("#c01818"), (v > 0.86).astype(np.float32))
    t = text_mask(h, w, "BAND-AIDES", w * 0.5, h * 0.07, int(h * 0.075), stroke=1, stretch_x=0.62)
    f = mix(f, rgb("#f0f0f0"), t)
    # bandage strip picture, diagonal
    strip = draw_mask(h, w, lambda d: d.polygon([(w * 0.1, h * 0.62), (w * 0.75, h * 0.3), (w * 0.9, h * 0.42), (w * 0.25, h * 0.74)], fill=255))
    f = mix(f, rgb("#d8a070"), strip)
    pad = draw_mask(h, w, lambda d: d.polygon([(w * 0.38, h * 0.48), (w * 0.55, h * 0.4), (w * 0.62, h * 0.5), (w * 0.45, h * 0.58)], fill=255))
    f = mix(f, rgb("#f0e8e0"), pad)
    f = text_lines(h, w, r, w * 0.12, h * 0.72, w * 0.88, h * 0.82, 2, "#2040a0", f, 0.8)
    c.put(0, 0, grime(f, r, 0.25, (0.5, 0.45, 0.35)))
    bk = paper(h, w, "#e4e4e0", r)
    bk = text_lines(h, w, r, w * 0.1, h * 0.1, w * 0.9, h * 0.9, 12, "#404050", bk, 0.6)
    c.put(32, 0, bk)
    sd = paper(16 * S, 32 * S, "#e4e4e0", r)
    sd[6 * S:9 * S] = sd[6 * S:9 * S] * 0.3 + rgb("#2040a0") * 0.7
    c.put(0, 48, sd)
    tp = paper(16 * S, 32 * S, "#dcdcd8", r)
    tp[7 * S:8 * S] *= 0.6
    c.put(32, 48, tp)
    return c.done()


def flashlight():
    c = Canvas(64, 64, 14)
    r = c.rng
    body = metal(32 * S, 64 * S, "#26262a", r, brushed=False, scratches=0.4)
    yy, xx = np.mgrid[0:32 * S, 0:64 * S]
    knurl = (((xx // 2 + yy // 2) % 3) == 0) & (yy > 10 * S) & (yy < 24 * S)
    body = shade(body, 1 - 0.3 * knurl)
    body = mix(body, rgb("#8a8a8a"), ((np.abs(yy - 6 * S) < S) | (np.abs(yy - 28 * S) < S)).astype(np.float32) * 0.6)
    c.put(0, 0, body)
    c.put(0, 32, metal(16 * S, 32 * S, "#a0a0a4", r, scratches=0.3))
    u, v = uv_grid(16 * S, 16 * S)
    d = np.sqrt((u - 0.5) ** 2 + (v - 0.5) ** 2)
    lens = fill(16 * S, 16 * S, rgb("#d8d0a0"))
    lens = shade(lens, 1.25 - d * 1.2)
    lens = mix(lens, rgb("#fff8d0"), smoothstep(0.18, 0.05, d))
    lens = shade(lens, 1 - 0.3 * (np.abs(d - 0.3) < 0.03))
    lens = mix(lens, rgb("#606060"), (d > 0.45).astype(np.float32))
    c.put(32, 32, lens)
    c.put(48, 32, metal(16 * S, 16 * S, "#1c1c1e", r, scratches=0.2))
    c.put(0, 48, plastic(16 * S, 16 * S, "#8a1414", r, dirt=0.2))
    c.put(16, 48, fill(16 * S, 48 * S, rgb("#202020")))
    return c.done()


def batteries():
    c = Canvas(64, 32, 15)
    r = c.rng
    h, w = 32 * S, 48 * S
    lab = plastic(h, w, "#141414", r, dirt=0.15)
    u, v = uv_grid(h, w)
    lab = mix(lab, rgb("#b0602a"), (v > 0.68).astype(np.float32))      # copper top towards the + end
    lab = mix(lab, rgb("#d8b040"), (np.abs(v - 0.68) < 0.025).astype(np.float32))
    t = text_mask(h, w, "D", w * 0.25, h * 0.45, int(h * 0.3), stroke=1)
    lab = mix(lab, rgb("#e0e0e0"), t)
    t2 = text_mask(h, w, "1.5V", w * 0.7, h * 0.45, int(h * 0.16), stroke=0)
    lab = mix(lab, rgb("#c0c0c0"), t2)
    c.put(0, 0, lab)
    c.put(48, 0, metal(16 * S, 16 * S, "#b0b0b0", r))
    c.put(48, 16, metal(16 * S, 16 * S, "#8a8a8a", r))
    return c.done()


def soundmeter():
    c = Canvas(128, 128, 16)
    r = c.rng
    h, w = 128 * S, 64 * S
    f = metal(h, w, "#2a2c2e", r, brushed=False, scratches=0.25)
    u, v = uv_grid(h, w)
    # dial window (top quarter)
    win = (u > 0.1) & (u < 0.9) & (v > 0.72) & (v < 0.96)
    face = fill(h, w, rgb("#e0d8c0"))
    face = shade(face, 1 + photo_detail("page", h, w, r, zoom=1, sigma=3) * 0.03)
    f = mix(f, face, win.astype(np.float32))
    f = shade(f, 1 - 0.5 * (win & ((np.abs(u - 0.1) < 0.02) | (np.abs(u - 0.9) < 0.02) | (np.abs(v - 0.72) < 0.008) | (np.abs(v - 0.96) < 0.008))))
    # arc scale centred at the needle pivot (u 0.5, v 0.74)
    px, py = 0.5 * w, (1 - 0.74) * h
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    dx, dy = xx - px, py - yy
    rad = np.sqrt(dx * dx + dy * dy)
    ang = np.degrees(np.arctan2(dx, dy))
    arc = (np.abs(rad - 0.17 * h) < 1.2 * S) & (np.abs(ang) < 48)
    f = mix(f, rgb("#202020"), (arc & (ang < 20)).astype(np.float32))
    f = mix(f, rgb("#c01818"), (arc & (ang >= 20)).astype(np.float32))
    ticks = (rad > 0.17 * h) & (rad < 0.19 * h) & (np.abs(((ang + 48) % 12) - 6) > 5) & (np.abs(ang) < 49)
    f = mix(f, rgb("#202020"), ticks.astype(np.float32))
    f = mix(f, rgb("#202020"), text_mask(h, w, "VU", w * 0.5, h * 0.17, int(h * 0.035)))
    # knobs, switch, LED, grille, labels
    for kx in (0.26, 0.74):
        f = mix(f, rgb("#101010"), draw_mask(h, w, lambda d, kx=kx: d.ellipse([w * kx - 6 * S, h * 0.45 - 6 * S, w * kx + 6 * S, h * 0.45 + 6 * S], fill=255)))
        f = mix(f, rgb("#8a8a8a"), draw_mask(h, w, lambda d, kx=kx: d.line([w * kx, h * 0.45, w * kx, h * 0.45 - 5 * S], fill=255, width=S)))
    f = mix(f, rgb("#d02020"), draw_mask(h, w, lambda d: d.ellipse([w * 0.5 - 2 * S, h * 0.36 - 2 * S, w * 0.5 + 2 * S, h * 0.36 + 2 * S], fill=255)))
    f = mix(f, rgb("#8a8a8a"), draw_mask(h, w, lambda d: d.rectangle([w * 0.45, h * 0.55, w * 0.55, h * 0.6], fill=255)))
    for gy in np.arange(0.74, 0.92, 0.025):
        f = mix(f, rgb("#0a0a0a"), draw_mask(h, w, lambda d, gy=gy: d.rectangle([w * 0.2, h * gy, w * 0.8, h * gy + S], fill=255)))
    f = mix(f, rgb("#c0c0c0"), text_mask(h, w, "SOUND LEVEL", w * 0.5, h * 0.66, int(h * 0.028), stretch_x=0.8))
    f = grime(f, r, 0.25, (0.08, 0.08, 0.08))
    c.put(0, 0, f)
    b = metal(128 * S, 32 * S, "#262829", r, brushed=False)
    for sy in (0.08, 0.92):
        for sx in (0.15, 0.85):
            b = mix(b, rgb("#6a6a6a"), draw_mask(128 * S, 32 * S, lambda d, sx=sx, sy=sy: d.ellipse([32 * S * sx - 2 * S, 128 * S * sy - 2 * S, 32 * S * sx + 2 * S, 128 * S * sy + 2 * S], fill=255)))
    c.put(64, 0, b)
    c.put(96, 0, metal(128 * S, 16 * S, "#222324", r, brushed=False))
    c.put(112, 0, metal(32 * S, 16 * S, "#2a2a2a", r, brushed=False))
    nd = fill(32 * S, 16 * S, rgb("#0a0a0a"))
    nd[:8 * S] = rgb("#b01010")
    c.put(112, 32, nd)
    c.put(112, 64, fill(64 * S, 16 * S, rgb("#202020")))
    return c.done()


def boltcutters():
    c = Canvas(64, 64, 17)
    r = c.rng
    c.put(0, 0, metal(64 * S, 32 * S, "#3a3c40", r, scratches=0.5, rust=0.3))
    g = plastic(32 * S, 32 * S, "#a01818", r, dirt=0.35)
    yy, xx = np.mgrid[0:32 * S, 0:32 * S]
    g = shade(g, 1 - 0.18 * (((yy // (2 * S)) % 2) == 0))
    c.put(32, 0, g)
    j = metal(32 * S, 32 * S, "#2a2c30", r, scratches=0.6, rust=0.2)
    j[:, :4 * S] = j[:, :4 * S] * 0.5 + rgb("#b0b0b0") * 0.5
    c.put(32, 32, j)
    return c.done()


def carkeys():
    c = Canvas(64, 64, 18)
    r = c.rng
    k1 = metal(16 * S, 32 * S, "#b8963c", r, scratches=0.3)
    yy, xx = np.mgrid[0:16 * S, 0:32 * S]
    k1 = shade(k1, 1 - 0.3 * (((xx // (3 * S)) % 2) == 0) * (yy > 10 * S))
    c.put(0, 0, k1)
    c.put(0, 16, metal(16 * S, 32 * S, "#b0b0b4", r, scratches=0.3))
    fob = plastic(32 * S, 32 * S, "#141416", r, dirt=0.15)
    for by in (0.3, 0.62):
        fob = mix(fob, rgb("#3a3a3e"), draw_mask(32 * S, 32 * S, lambda d, by=by: d.rounded_rectangle([8 * S, 32 * S * by - 4 * S, 24 * S, 32 * S * by + 4 * S], radius=2 * S, fill=255)))
    fob = mix(fob, rgb("#c8c8c8"), text_mask(32 * S, 32 * S, "<>", 16 * S, 32 * S * 0.3, 5 * S))
    fob = mix(fob, rgb("#c03030"), draw_mask(32 * S, 32 * S, lambda d: d.ellipse([14 * S, 32 * S * 0.62 - 2 * S, 18 * S, 32 * S * 0.62 + 2 * S], fill=255)))
    c.put(32, 0, fob)
    c.put(0, 32, metal(16 * S, 16 * S, "#a8a8a8", r))
    c.put(16, 32, plastic(16 * S, 16 * S, "#18181a", r))
    c.put(32, 32, fill(32 * S, 32 * S, rgb("#202020")))
    return c.done()


def gascan():
    c = Canvas(64, 64, 19)
    r = c.rng
    h, w = 32 * S, 64 * S
    s = plastic(h, w, "#b01a14", r, dirt=0.35)
    u, v = uv_grid(h, w)
    # molded X ribs
    xr = (np.abs((u * 2 % 1) - v) < 0.04) | (np.abs((u * 2 % 1) - (1 - v)) < 0.04)
    s = shade(s, 1 - 0.18 * xr)
    lab = (u > 0.3) & (u < 0.7) & (v > 0.3) & (v < 0.7)
    s = mix(s, rgb("#e0c020"), lab.astype(np.float32))
    s = mix(s, rgb("#101010"), text_mask(h, w, "GASOLINE", w * 0.5, h * 0.45, int(h * 0.11), stroke=1, stretch_x=0.7) * lab)
    s = mix(s, rgb("#101010"), text_mask(h, w, "DANGER", w * 0.5, h * 0.6, int(h * 0.07), stretch_x=0.7) * lab)
    c.put(0, 0, grime(s, r, 0.3, (0.15, 0.08, 0.06)))
    c.put(0, 32, plastic(32 * S, 32 * S, "#a81812", r, dirt=0.4))
    c.put(32, 32, plastic(16 * S, 16 * S, "#a01610", r, dirt=0.3))
    c.put(48, 32, plastic(16 * S, 16 * S, "#d8b818", r, dirt=0.3))
    c.put(32, 48, plastic(16 * S, 16 * S, "#141414", r, dirt=0.2))
    c.put(48, 48, fill(16 * S, 16 * S, rgb("#202020")))
    return c.done()


def carbattery():
    c = Canvas(64, 64, 20)
    r = c.rng
    h, w = 32 * S, 64 * S
    s = plastic(h, w, "#18181a", r, dirt=0.3)
    u, v = uv_grid(h, w)
    lab = (v > 0.35) & (v < 0.75) & (u > 0.08) & (u < 0.92)
    s = mix(s, rgb("#d8d8d0"), lab.astype(np.float32))
    s = mix(s, rgb("#1838a0"), (lab & (v > 0.62)).astype(np.float32))
    s = mix(s, rgb("#101010"), text_mask(h, w, "12V HEAVY DUTY", w * 0.5, h * 0.5, int(h * 0.12), stroke=1, stretch_x=0.7) * lab)
    c.put(0, 0, grime(s, r, 0.3, (0.3, 0.28, 0.25)))
    c.put(0, 32, plastic(24 * S, 32 * S, "#141416", r, dirt=0.3))
    t = plastic(32 * S, 32 * S, "#4a4c4e", r, dirt=0.4)
    for i in range(6):
        cx = (6 + i * 4) * S
        t = mix(t, rgb("#202020"), draw_mask(32 * S, 32 * S, lambda d, cx=cx: d.ellipse([cx - 1.5 * S, 15 * S, cx + 1.5 * S, 18 * S], fill=255)))
    t = mix(t, rgb("#d02020"), text_mask(32 * S, 32 * S, "+", 26 * S, 6 * S, 7 * S, stroke=1))
    t = mix(t, rgb("#101010"), text_mask(32 * S, 32 * S, "-", 6 * S, 6 * S, 7 * S, stroke=1))
    c.put(32, 32, t)
    c.put(0, 56, metal(8 * S, 8 * S, "#c02018", r, brushed=False, scratches=0))
    c.put(8, 56, metal(8 * S, 8 * S, "#1a1a1a", r, brushed=False, scratches=0))
    c.put(16, 56, plastic(8 * S, 16 * S, "#101010", r))
    c.put(32, 56, fill(8 * S, 32 * S, rgb("#202020")))
    return c.done()


def fuse():
    c = Canvas(64, 32, 21)
    r = c.rng
    h, w = 32 * S, 48 * S
    b = paper(h, w, "#d8d0b8", r, dirt=0.4)
    b = shade(b, 1 + photo_detail("coins", h, w, r, zoom=2, sigma=2) * 0.04)
    b = mix(b, rgb("#202020"), text_mask(h, w, "30A", w * 0.3, h * 0.5, int(h * 0.28), stroke=1))
    b = mix(b, rgb("#702020"), text_mask(h, w, "250V", w * 0.75, h * 0.5, int(h * 0.18)))
    c.put(0, 0, grime(b, r, 0.35, (0.3, 0.25, 0.18)))
    c.put(48, 0, metal(16 * S, 16 * S, "#b08a40", r, scratches=0.4))
    c.put(48, 16, metal(16 * S, 16 * S, "#a07a30", r, scratches=0.4))
    return c.done()


def cagekey():
    c = Canvas(32, 32, 22)
    r = c.rng
    c.put(0, 0, metal(32 * S, 32 * S, "#3a3632", r, brushed=False, scratches=0.3, rust=0.6))
    return c.done()


def lockpick():
    c = Canvas(64, 32, 23)
    r = c.rng
    h, w = 32 * S, 48 * S
    l = fabric(h, w, rgb("#6a4026"), r, folds=0.2, grain=0.1, fold_src=("ihc", None))
    u, v = uv_grid(h, w)
    border = (np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v)) < 0.08)
    stitch = border & (np.minimum(np.minimum(u, 1 - u), np.minimum(v, 1 - v)) > 0.05) & ((np.mgrid[0:h, 0:w][1] // (2 * S)) % 2 == 0)
    l = mix(l, rgb("#c8a878"), stitch.astype(np.float32) * 0.8)
    c.put(0, 0, grime(l, r, 0.3, (0.12, 0.08, 0.05)))
    c.put(48, 0, metal(32 * S, 16 * S, "#b0b0b4", r, scratches=0.2))
    return c.done()


def crowbar():
    c = Canvas(32, 64, 24)
    r = c.rng
    p = metal(64 * S, 16 * S, "#9a1810", r, brushed=False, scratches=0.0)
    chips = smoothstep(0.62, 0.66, fbm(64 * S, 16 * S, 8, r, octaves=3))
    p = mix(p, rgb("#4a4a4a"), chips)
    p = mix(p, rgb("#5a3018"), smoothstep(0.7, 0.75, fbm(64 * S, 16 * S, 6, r, octaves=3)) * 0.7)
    c.put(0, 0, grime(p, r, 0.3, (0.1, 0.08, 0.06)))
    c.put(16, 0, metal(64 * S, 16 * S, "#4a4c50", r, scratches=0.5, rust=0.4))
    return c.done()


def bottle():
    c = Canvas(64, 64, 25)
    r = c.rng
    h, w = 48 * S, 48 * S
    g = fill(h, w, rgb("#1e4a22"))
    u, v = uv_grid(h, w)
    g = shade(g, 0.75 + 0.6 * np.exp(-((u - 0.62) / 0.05) ** 2) + 0.3 * np.exp(-((u - 0.12) / 0.04) ** 2))
    g = shade(g, 1 + photo_detail("moon", h, w, r, zoom=2, sigma=3) * 0.05)
    label = (v > 0.3) & (v < 0.72) & (u > 0.05) & (u < 0.62)
    torn = smoothstep(0.45, 0.5, fbm(h, w, 10, r, octaves=3) + 0.2)
    lab = paper(h, w, "#c8b890", r, dirt=0.5)
    lab = mix(lab, rgb("#8a1a14"), text_mask(h, w, "OLD\nCROW", w * 0.33, h * 0.5, int(h * 0.09), stroke=1, spacing=2) * 0.9)
    g = mix(g, lab, label * torn)
    c.put(0, 0, grime(g, r, 0.25, (0.1, 0.1, 0.06)))
    n = fill(48 * S, 16 * S, rgb("#1a4220"))
    u, v = uv_grid(48 * S, 16 * S)
    n = shade(n, 0.8 + 0.5 * np.exp(-((u - 0.6) / 0.1) ** 2))
    c.put(48, 0, n)
    c.put(0, 48, fill(16 * S, 16 * S, rgb("#143018")))
    c.put(16, 48, fill(16 * S, 16 * S, rgb("#2a5a2e")))
    c.put(32, 48, fill(16 * S, 32 * S, rgb("#202020")))
    return c.done()


def pills():
    c = Canvas(64, 32, 26)
    r = c.rng
    h, w = 32 * S, 48 * S
    b = fill(h, w, rgb("#d0701a"))
    u, v = uv_grid(h, w)
    b = shade(b, 0.8 + 0.4 * np.exp(-((u - 0.6) / 0.08) ** 2))
    lab = (v > 0.18) & (v < 0.78) & (u > 0.12) & (u < 0.88)
    b = mix(b, paper(h, w, "#ecece4", r, dirt=0.2), lab.astype(np.float32))
    b = mix(b, rgb("#202020"), text_mask(h, w, "Rx", w * 0.24, h * 0.36, int(h * 0.18), stroke=1) * lab)
    b = text_lines(h, w, r, w * 0.36, h * 0.3, w * 0.84, h * 0.7, 5, "#303030", b, 0.8)
    c.put(0, 0, b)
    cap = plastic(16 * S, 16 * S, "#e8e8e0", r, dirt=0.15)
    u, v = uv_grid(16 * S, 16 * S)
    d = np.sqrt((u - 0.5) ** 2 + (v - 0.5) ** 2)
    cap = shade(cap, 1 - 0.2 * (np.abs(d - 0.3) < 0.03))
    c.put(48, 0, cap)
    cs = plastic(16 * S, 16 * S, "#e0e0d8", r)
    yy, xx = np.mgrid[0:16 * S, 0:16 * S]
    cs = shade(cs, 1 - 0.2 * ((xx // S) % 2 == 0))
    c.put(48, 16, cs)
    return c.done()


def cleaver():
    c = Canvas(64, 64, 27)
    r = c.rng
    h, w = 32 * S, 64 * S
    b = metal(h, w, "#8a8c90", r, scratches=0.7, rust=0.25)
    u, v = uv_grid(h, w)
    # sharpened bevel along the edge (bottom)
    b = mix(b, rgb("#c8c8cc"), (v < 0.1).astype(np.float32) * 0.8)
    # hang hole near the spine at the tip end
    hole = draw_mask(h, w, lambda d: d.ellipse([w * 0.82, h * 0.12, w * 0.9, h * 0.28], fill=255))
    b = mix(b, rgb("#050505"), hole)
    bm = blood(h, w, r, amount=0.5, scale=5, splatter=2.0)
    b = mix(b, blood_color(r, h, w), bm * 0.9)
    smear = smoothstep(0.4, 0.7, fbm(h, w, 3, r, octaves=3)) * (v < 0.6)
    b = mix(b, rgb("#4a0606"), smear * 0.6)
    c.put(0, 0, grime(b, r, 0.25, (0.15, 0.1, 0.08)))
    wood = fabric(16 * S, 32 * S, rgb("#3a2414"), r, folds=0.0, grain=0.0)
    wood = shade(wood, 1 + photo_detail("coffee", 16 * S, 32 * S, r, zoom=3, sigma=2, crop=(0.0, 0.0, 0.4, 0.4)) * 0.12)
    yy, xx = np.mgrid[0:16 * S, 0:32 * S]
    wood = shade(wood, 0.9 + 0.2 * np.sin(yy / (1.5 * S) + np.sin(xx / (6.0 * S)) * 2) * 0.5)
    for rx in (0.2, 0.5, 0.8):
        wood = mix(wood, rgb("#c0a050"), draw_mask(16 * S, 32 * S, lambda d, rx=rx: d.ellipse([32 * S * rx - 2 * S, 6 * S, 32 * S * rx + 2 * S, 10 * S], fill=255)))
    wood = mix(wood, rgb("#3a0606"), blood(16 * S, 32 * S, r, amount=0.3, scale=4, splatter=1) * 0.7)
    c.put(0, 32, wood)
    c.put(32, 32, metal(8 * S, 32 * S, "#b0b0b4", r, scratches=0.4))
    c.put(32, 40, metal(8 * S, 32 * S, "#7a5a28", r, scratches=0.2))
    w2 = fabric(16 * S, 32 * S, rgb("#2e1c10"), r, folds=0.0, grain=0.1)
    c.put(0, 48, w2)
    c.put(32, 48, fill(16 * S, 32 * S, rgb("#202020")))
    return c.done()


def tripwire_stake():
    c = Canvas(32, 32, 28)
    r = c.rng
    c.put(0, 0, metal(24 * S, 32 * S, "#4a4a4c", r, scratches=0.3, rust=0.5))
    c.put(0, 24, metal(8 * S, 32 * S, "#9a9a9c", r, scratches=0.0))
    return c.done()


def beartrap():
    c = Canvas(64, 64, 29)
    r = c.rng
    c.put(0, 0, metal(64 * S, 32 * S, "#33302c", r, brushed=False, scratches=0.4, rust=0.6))
    t = metal(32 * S, 32 * S, "#5a5852", r, scratches=0.5, rust=0.3)
    u, v = uv_grid(32 * S, 32 * S)
    t = mix(t, rgb("#a8a8a0"), (v > 0.7).astype(np.float32) * 0.6)
    t = mix(t, rgb("#4a0606"), blood(32 * S, 32 * S, r, amount=0.25, scale=4, splatter=1) * 0.7)
    c.put(32, 0, t)
    p = metal(32 * S, 32 * S, "#3a3632", r, brushed=False, scratches=0.2, rust=0.7)
    yy, xx = np.mgrid[0:32 * S, 0:32 * S]
    p = shade(p, 1 - 0.15 * (((xx + yy) // (3 * S)) % 2 == 0))
    c.put(32, 32, p)
    return c.done()


def revolver():
    """Old revolver: blued steel worn silver at the edges, dark wooden grip, brass cartridge rims."""
    c = Canvas(64, 64, 41)
    r = c.rng
    blued = metal(32 * S, 32 * S, "#2b2e33", r, scratches=0.45, rust=0.15)
    c.put(0, 0, grime(blued, r, 0.25, (0.08, 0.07, 0.06)))
    worn = metal(32 * S, 32 * S, "#55585c", r, scratches=0.7, rust=0.25)
    c.put(32, 0, grime(worn, r, 0.3, (0.1, 0.08, 0.06)))
    wood = fill(32 * S, 32 * S, rgb("#4a2c18"))
    u, v = uv_grid(32 * S, 32 * S)
    grain = np.sin((u * 18 + fbm(32 * S, 32 * S, 5, r, octaves=3) * 3) * np.pi)
    wood = shade(wood, 0.85 + 0.15 * grain)
    # checkering
    yy, xx = np.mgrid[0:32 * S, 0:32 * S]
    chk = (((xx + yy) // (2 * S)) % 2 == 0) & (((xx - yy) // (2 * S)) % 2 == 0)
    wood = shade(wood, 1 - 0.18 * chk)
    c.put(0, 32, grime(wood, r, 0.35, (0.1, 0.06, 0.03)))
    cyl = metal(16 * S, 16 * S, "#3a3d42", r, scratches=0.5)
    cu, cv = uv_grid(16 * S, 16 * S)
    cyl = shade(cyl, 1 - 0.35 * ((np.floor(cu * 8) % 2) == 0) * smoothstep(0.3, 0.5, cv) * smoothstep(0.9, 0.7, cv))  # flutes
    c.put(32, 32, cyl)
    c.put(48, 32, fill(16 * S, 16 * S, rgb("#0c0c0c")))
    rim = metal(16 * S, 16 * S, "#a08040", r, brushed=False, scratches=0.2)
    c.put(32, 48, rim)
    c.put(0, 0, c.region(0, 0, 32, 32))
    return c.done()


# grandma.png 128x128 (GrandmaRig.cs): dress (0,0,64,64) face (64,0,32,32) skin (96,0,32,32) hair (64,32,32,32)
# socks (96,32,32,16) shoes (96,48,32,16) hair back (64,64,32,32) mouth (96,64,32,32)
# chair metal (0,96,32,32) tyre (32,96,32,32) seat vinyl (64,96,32,32) chrome (96,96,32,32)  [64..96 rows 0..64: dress hem]
def _grandma(bloody):
    c = Canvas(128, 128, 77)
    r = c.rng
    # faded floral house dress: pale yellow, small pink / orange roses with green leaves
    dress = fabric(64 * S, 64 * S, rgb("#cfc08a"), r, folds=0.22, grain=0.05, stains=0.35, stain_color=rgb("#8a7a48"))
    h = w = 64 * S
    for _ in range(46):
        cx, cy = r.randint(0, w), r.randint(0, h)
        rr = r.uniform(2.2, 3.6) * S
        col = rgb(["#c87a6a", "#d49060", "#b86870", "#d8a070"][r.randint(0, 4)])
        dress = mix(dress, col, soft_ellipse(h, w, cx, cy, rr, rr * 0.85, 2.0) * 0.75)
        for k in range(2):
            a = r.uniform(0, 6.28)
            dress = mix(dress, rgb("#7a8a50"), soft_ellipse(h, w, cx + np.cos(a) * rr * 1.5, cy + np.sin(a) * rr * 1.5, rr * 0.7, rr * 0.35, 1.5, a) * 0.6)
    dress = grime(dress, r, 0.3, (0.35, 0.3, 0.15))
    if bloody:
        m = blood(h, w, r, amount=0.55, scale=5, splatter=1.0)
        dress = mix(dress, blood_color(r, h, w), m * 0.95)
    c.put(0, 0, dress)
    c.put(0, 64, dress[: 32 * S])
    c.put(64, 64 + 0, dress[: 32 * S, : 32 * S]) if False else None

    # face: sallow, sunken dark eyes, thin pursed mouth, hair falling over her left eye
    fh = fw = 32 * S
    face = skin(fh, fw, rgb("#c4ae72"), r, mottle=0.12, pores=0.05, redness=0.15)
    face = shade(face, 1 - 0.18 * soft_ellipse(fh, fw, fw * 0.22, fh * 0.66, fw * 0.12, fh * 0.12, 8))   # hollow cheeks
    face = shade(face, 1 - 0.18 * soft_ellipse(fh, fw, fw * 0.78, fh * 0.66, fw * 0.12, fh * 0.12, 8))
    for ex in (0.32, 0.68):
        face = mix(face, rgb("#3a2614"), soft_ellipse(fh, fw, fw * ex, fh * 0.45, fw * 0.12, fh * 0.075, 6) * 0.85)
        face = mix(face, rgb("#0a0604"), soft_ellipse(fh, fw, fw * ex, fh * 0.455, fw * 0.05, fh * 0.03, 2))
        face = mix(face, rgb("#d8d0b0"), soft_ellipse(fh, fw, fw * ex + fw * 0.012, fh * 0.452, fw * 0.012, fh * 0.01, 1) * 0.6)
    face = mix(face, rgb("#6a5030"), soft_ellipse(fh, fw, fw * 0.5, fh * 0.6, fw * 0.035, fh * 0.07, 4) * 0.5)        # nose shadow
    face = mix(face, rgb("#3a1a14"), soft_ellipse(fh, fw, fw * 0.5, fh * 0.78, fw * 0.13, fh * 0.012, 2) * 0.9)       # mouth
    yy, xx = np.mgrid[0:fh, 0:fw].astype(np.float32)
    for k in range(5):  # wrinkles
        y0 = fh * (0.25 + 0.04 * k)
        face = shade(face, 1 - 0.12 * (np.abs(yy - y0 - 3 * np.sin(xx / fw * 6)) < 1.2) * smoothstep(0.15, 0.3, xx / fw) * smoothstep(0.85, 0.7, xx / fw))
    hair_c = rgb("#d8d6cc")
    fringe = hair_strands(fh, fw, hair_c, r, highlight=rgb("#f0eee4"), contrast=0.3)
    fmask = np.clip(smoothstep(fh * 0.16, fh * 0.1, yy) + smoothstep(fw * 0.5, fw * 0.36, xx) * smoothstep(fh * 0.58, fh * 0.5, yy), 0, 1)
    face = mix(face, fringe, fmask)
    if bloody:
        face = mix(face, blood_color(r, fh, fw), blood(fh, fw, r, amount=0.3, scale=4, splatter=0.6) * 0.9)
    c.put(64, 0, face)
    sk = skin(fh, fw, rgb("#bea86c"), r, mottle=0.14, pores=0.05, redness=0.1)
    sk = shade(sk, 1 - 0.1 * smoothstep(0.6, 0.9, fbm(fh, fw, 4, r, octaves=3)))  # liver spots
    if bloody:
        sk = mix(sk, blood_color(r, fh, fw), blood(fh, fw, r, amount=0.35, scale=4, splatter=0.8) * 0.9)
    c.put(96, 0, sk)
    c.put(64, 32, hair_strands(fh, fw, hair_c, r, highlight=rgb("#f2f0e8"), contrast=0.35))
    c.put(64, 64, hair_strands(fh, fw, rgb("#c8c6bc"), r, highlight=rgb("#e8e6dc"), contrast=0.35))
    c.put(96, 32, fabric(16 * S, 32 * S, rgb("#dcd8cc"), r, folds=0.1, stains=0.3, stain_color=rgb("#8a8270")))
    c.put(96, 48, plastic(16 * S, 32 * S, "#cfc8b8", r, dirt=0.4))
    c.put(96, 64, fill(32 * S, 32 * S, rgb("#1a0806")))
    c.put(0, 96, metal(32 * S, 32 * S, "#1c1c1e", r, scratches=0.5, rust=0.2))
    tyre = fill(32 * S, 32 * S, rgb("#141414"))
    tyre = shade(tyre, 1 + 0.25 * ((np.mgrid[0:32 * S, 0:32 * S][0] // S) % 3 == 0))
    c.put(32, 96, tyre)
    c.put(64, 96, plastic(32 * S, 32 * S, "#202020", r, dirt=0.3))
    c.put(96, 96, metal(32 * S, 32 * S, "#6a6a6a", r, scratches=0.5, rust=0.3))
    return c.done()


def screwdriver():
    """screwdriver.png 32x32: handle (0,0,16,32) amber plastic with dark grip flutes, shaft (16,0,16,32) worn steel."""
    c = Canvas(32, 32, 27)
    r = c.rng
    h = plastic(32 * S, 16 * S, "#a8481a", r, dirt=0.45)
    u, v = uv_grid(32 * S, 16 * S)
    flutes = (np.sin(u * np.pi * 2 * 4) > 0.55).astype(np.float32)   # 4 grooves around the handle
    h = mix(h, rgb("#4a1a08"), flutes * 0.7)
    h = mix(h, rgb("#d88a48"), smoothstep(0.85, 1.0, np.sin(u * np.pi * 2 * 4 + 1.2)) * 0.35)  # glossy ridge
    c.put(0, 0, grime(h, r, 0.35, (0.1, 0.07, 0.05)))
    c.put(16, 0, metal(32 * S, 16 * S, "#8e9094", r, scratches=0.4, rust=0.35))
    return c.done()


def backpack():
    """backpack.png 64x64 (iteration 3, old army-surplus rucksack): canvas (0,0,32,32) flap (32,0,32,16) pocket (32,16,32,16)
    strap (0,32,32,8) leather (0,40,32,8) bottom (0,48,32,16) buckle (32,32,16,16) side pocket (32,48,16,16)
    side (48,32,16,32, portrait: the bag's sides are taller than deep)."""
    c = Canvas(64, 64, 31)
    r = c.rng
    olive = rgb("#5a5a34")

    def canvas(h, w, color=olive, stains=0.35):
        t = fabric(h, w, color, r, folds=0.22, grain=0.1, weave=0.05, stains=stains, stain_color=rgb("#2e2a1a"))
        return grime(t, r, 0.35, (0.16, 0.13, 0.08))

    def stitches(img, h, w, inset):
        m = np.zeros((h, w), np.float32)
        y0, y1, x0, x1 = inset, h - inset - 1, inset, w - inset - 1
        for x in range(x0, x1, 3 * S):
            m[y0:y0 + S // 2 + 1, x:x + 2 * S] = 1
            m[y1 - S // 2:y1 + 1, x:x + 2 * S] = 1
        for y in range(y0, y1, 3 * S):
            m[y:y + 2 * S, x0:x0 + S // 2 + 1] = 1
            m[y:y + 2 * S, x1 - S // 2:x1 + 1] = 1
        return mix(img, rgb("#8a8458"), m * 0.7)

    def edged(img, color="#2a2a16", k=0.85):
        """1 px dark seam round the region so the box edges read at 240p."""
        h, w = img.shape[:2]
        m = np.zeros((h, w), np.float32)
        m[:S], m[-S:], m[:, :S], m[:, -S:] = 1, 1, 1, 1
        return mix(img, rgb(color), m * k)

    body = canvas(32 * S, 32 * S)
    u, v = uv_grid(32 * S, 32 * S)
    body = shade(body, 0.9 + 0.2 * v)                                          # light from above, darker lower down
    c.put(0, 0, edged(stitches(body, 32 * S, 32 * S, S)))

    # the flap: dark, sun-faded at the top, a name strip from the prisoner who carried it before
    flap = canvas(16 * S, 32 * S, rgb("#34351c"), 0.25)
    fu, fv = uv_grid(16 * S, 32 * S)
    flap = mix(flap, rgb("#4a4a2a"), smoothstep(0.55, 1.0, fv) * 0.5)   # sun-faded fold
    tag = draw_mask(16 * S, 32 * S, lambda d: d.rectangle([10 * S, 9 * S, 22 * S, 13 * S], fill=255))
    flap = mix(flap, rgb("#d8d0b0"), tag * 0.9)
    pts = [(11.2 + i * 0.75, 11 + (1.2 if i % 3 == 0 else -0.8 if i % 3 == 1 else 0.3)) for i in range(14)]
    scrawl = draw_mask(16 * S, 32 * S, lambda d: d.line([(x * S, y * S) for x, y in pts], fill=255, width=S))
    flap = mix(flap, rgb("#1c1810"), scrawl * 0.85)
    c.put(32, 0, edged(stitches(flap, 16 * S, 32 * S, S), "#1e1e10"))

    pocket = canvas(16 * S, 32 * S, rgb("#6a6a40"), 0.3)
    pu, pv = uv_grid(16 * S, 32 * S)
    pocket = mix(pocket, rgb("#3c3d22"), (pv > 0.7).astype(np.float32) * 0.8)     # the pocket's own little flap
    pocket = shade(pocket, 1 - 0.3 * (np.abs(pv - 0.7) < 0.03))
    pocket = mix(pocket, rgb("#2a2a18"), text_mask(16 * S, 32 * S, "U.S.", 16 * S, 10.5 * S, int(7 * S), stroke=1) * 0.6)
    c.put(32, 16, edged(stitches(pocket, 16 * S, 32 * S, S)))

    webbing = fill(8 * S, 32 * S, rgb("#3a3a24"))
    yy = np.mgrid[0:8 * S, 0:32 * S][0]
    webbing = shade(webbing, 0.85 + 0.3 * ((yy // S) % 2))
    c.put(0, 32, edged(grime(webbing, r, 0.3, (0.12, 0.1, 0.06)), "#1e1e12", 0.6))
    c.put(0, 40, edged(plastic(8 * S, 32 * S, "#5a3a1e", r, dirt=0.5), "#2a1a0c", 0.6))

    bottom = canvas(16 * S, 32 * S, rgb("#3e3c26"), 0.6)
    bottom = grime(bottom, r, 0.6, (0.2, 0.16, 0.1))
    bottom = mix(bottom, rgb("#3a120c"), soft_ellipse(16 * S, 32 * S, 20 * S, 9 * S, 6 * S, 3.5 * S, soft=3) * 0.5)   # old, dark
    c.put(0, 48, edged(bottom))

    buckle = metal(16 * S, 16 * S, "#5e5a4c", r, scratches=0.3, rust=0.7)
    c.put(32, 32, edged(buckle, "#24221c", 0.7))
    sp = canvas(16 * S, 16 * S, rgb("#4a4a2a"), 0.4)
    c.put(32, 48, edged(stitches(sp, 16 * S, 16 * S, S)))
    side = canvas(32 * S, 16 * S, rgb("#525230"), 0.4)
    su, sv = uv_grid(32 * S, 16 * S)
    side = shade(side, 0.88 + 0.18 * sv)
    c.put(48, 32, edged(stitches(side, 32 * S, 16 * S, S)))
    return c.done()


def smallkey():
    """smallkey.png 32x32 (iteration 3): brass (0,0,16,32) with dark edges, cardboard tag face (16,0,16,16) with an eyelet
    and a red padlock mark (never a number: numbers are codes in this game), plain card (16,16,16,8), string (16,24,16,8)."""
    c = Canvas(32, 32, 32)
    r = c.rng
    brass = metal(32 * S, 16 * S, "#a88a3c", r, brushed=False, scratches=0.4, rust=0.15)
    m = np.zeros((32 * S, 16 * S), np.float32)
    m[:, :S], m[:, -S:] = 1, 1
    c.put(0, 0, mix(brass, rgb("#6a5424"), m * 0.8))
    tag = paper(16 * S, 16 * S, "#c8b48a", r, dirt=0.5)
    # brass eyelet at the string end (top of the image = towards the key)
    tag = mix(tag, rgb("#8a7a5a"), soft_ellipse(16 * S, 16 * S, 8 * S, 2.3 * S, 2.1 * S, 2.1 * S))
    tag = mix(tag, rgb("#2a2018"), soft_ellipse(16 * S, 16 * S, 8 * S, 2.3 * S, 1.0 * S, 1.0 * S))
    # a padlock drawn in red marker: shackle arc over a filled body
    red = rgb("#9a1a10")
    mark = draw_mask(16 * S, 16 * S, lambda d: (d.arc([5.4 * S, 5.2 * S, 10.6 * S, 10.4 * S], 180, 360, fill=255, width=int(1.1 * S)),
                                               d.line([5.9 * S, 7.8 * S, 5.9 * S, 9.4 * S], fill=255, width=int(1.1 * S)),
                                               d.line([10.1 * S, 7.8 * S, 10.1 * S, 9.4 * S], fill=255, width=int(1.1 * S)),
                                               d.rectangle([4.4 * S, 9.2 * S, 11.6 * S, 14.2 * S], fill=255)))
    tag = mix(tag, red, mark * 0.85)
    tag = mix(tag, rgb("#c8b48a"), draw_mask(16 * S, 16 * S, lambda d: d.ellipse([7.3 * S, 10.6 * S, 8.7 * S, 12.0 * S], fill=255)) * 0.8)
    c.put(16, 0, tag)
    c.put(16, 16, paper(8 * S, 16 * S, "#b8a47a", r, dirt=0.6))
    twine = fill(8 * S, 16 * S, rgb("#8a7a60"))
    xx = np.mgrid[0:8 * S, 0:16 * S][1]
    c.put(16, 24, shade(twine, 0.8 + 0.3 * ((xx // S) % 2)))
    return c.done()


def padlock():
    """padlock.png 64x32 (iteration 3, on locked drawers): old brass (0,0,16,16), steel (16,0,16,16), keyhole face with a
    bright escutcheon (0,16,16,16), black enamel face of the combination lock with a white mark and a red dot (16,16,16,16),
    rusty hasp strap with a slot and two screw holes (32,0,16,32), number wheel band (48,0,16,8), plain black enamel
    (48,8,16,8), splintered raw wood where a crowbar tore the staple out (48,16,16,16)."""
    import math
    c = Canvas(64, 32, 33)
    r = c.rng

    def edged(img, color, k=0.8):
        h, w = img.shape[:2]
        m = np.zeros((h, w), np.float32)
        m[:S], m[-S:], m[:, :S], m[:, -S:] = 1, 1, 1, 1
        return mix(img, rgb(color), m * k)

    brass = metal(16 * S, 16 * S, "#7a6028", r, brushed=False, scratches=0.4, rust=0.25)
    c.put(0, 0, edged(brass, "#3a2a10"))
    c.put(16, 0, metal(16 * S, 16 * S, "#8a8c90", r, scratches=0.3, rust=0.3))
    face = metal(16 * S, 16 * S, "#7a6028", r, brushed=False, scratches=0.3, rust=0.2)
    face = mix(face, rgb("#c8b060"), soft_ellipse(16 * S, 16 * S, 8 * S, 8.5 * S, 3.6 * S, 4.6 * S))     # escutcheon
    face = mix(face, rgb("#141008"), soft_ellipse(16 * S, 16 * S, 8 * S, 7 * S, 1.5 * S, 1.5 * S))
    face = mix(face, rgb("#141008"), draw_mask(16 * S, 16 * S, lambda d: d.rectangle([7.4 * S, 7 * S, 8.6 * S, 11.5 * S], fill=255)))
    c.put(0, 16, edged(face, "#3a2a10"))
    enamel = plastic(16 * S, 16 * S, "#141416", r, dirt=0.2)
    enamel = mix(enamel, rgb("#e8e4d8"), draw_mask(16 * S, 16 * S, lambda d: d.rectangle([7.4 * S, 1.5 * S, 8.6 * S, 5 * S], fill=255)) * 0.9)
    enamel = mix(enamel, rgb("#c02018"), soft_ellipse(16 * S, 16 * S, 8 * S, 7 * S, 0.9 * S, 0.9 * S))
    c.put(16, 16, edged(enamel, "#050506", 0.6))

    strap = metal(32 * S, 16 * S, "#4a4640", r, brushed=False, scratches=0.3, rust=0.85)
    strap = mix(strap, rgb("#0c0a08"), draw_mask(32 * S, 16 * S, lambda d: d.rectangle([5 * S, 24 * S, 11 * S, 28.5 * S], fill=255)))  # the slot
    for x in (4.5, 11.5):
        strap = mix(strap, rgb("#1a1410"), soft_ellipse(32 * S, 16 * S, x * S, 4 * S, 1.2 * S, 1.2 * S))
    c.put(32, 0, edged(strap, "#1e1a14", 0.7))

    band = fill(8 * S, 16 * S, rgb("#101012"))
    for k in range(10):
        x0 = k * 1.6
        band = mix(band, rgb("#e0dccc"), draw_mask(8 * S, 16 * S, lambda d, x0=x0: d.rectangle([(x0 + 0.5) * S, 2.5 * S, (x0 + 1.1) * S, 5.5 * S], fill=255)) * 0.85)
    c.put(48, 0, band)
    c.put(48, 8, plastic(8 * S, 16 * S, "#141416", r, dirt=0.2))

    wood = fill(16 * S, 16 * S, rgb("#a8845a"))
    n = fbm(16 * S, 16 * S, 5, r, octaves=3)
    yy, xx = np.mgrid[0:16 * S, 0:16 * S] / float(S)
    grain = 0.5 + 0.5 * np.sin(xx * 2.2 + n * 3)
    wood = shade(wood, 0.8 + 0.3 * grain)
    for (x, y) in ((8, 3), (8, 12)):
        wood = mix(wood, rgb("#1a1008"), soft_ellipse(16 * S, 16 * S, x * S, y * S, 1.1 * S, 1.4 * S))
    splinters = smoothstep(0.55, 0.75, fbm(16 * S, 16 * S, 9, r, octaves=2))
    wood = mix(wood, rgb("#5a3a1e"), splinters * 0.6)
    c.put(48, 16, wood)
    return c.done()


def vhstape():
    """vhstape.png 64x32 (iteration 3): label (0,0,48,16) with 'MAMA 10/31' in marker, black shell (0,16,48,16),
    spine label (48,0,16,16), reel window (48,16,16,16)."""
    c = Canvas(64, 32, 34)
    r = c.rng
    shell = plastic(16 * S, 48 * S, "#18181a", r, dirt=0.35)
    c.put(0, 16, shell)
    lab = plastic(16 * S, 48 * S, "#18181a", r, dirt=0.3)
    paper_lab = paper(12 * S, 40 * S, "#e0dccb", r, dirt=0.5)
    paper_lab = mix(paper_lab, rgb("#1a1a40"), text_mask(12 * S, 40 * S, "MAMA 10/31", 20 * S, 6 * S, int(7 * S), stroke=1, stretch_x=0.8) * 0.9)
    lab[2 * S:14 * S, 4 * S:44 * S] = paper_lab
    c.put(0, 0, lab)
    sp = plastic(16 * S, 16 * S, "#18181a", r, dirt=0.3)
    sp[5 * S:11 * S, 1 * S:15 * S] = mix(paper(6 * S, 14 * S, "#e0dccb", r, dirt=0.6), rgb("#802018"),
                                         text_mask(6 * S, 14 * S, "PLAY", 7 * S, 3 * S, int(4 * S), stroke=1) * 0.8)
    c.put(48, 0, sp)
    win = fill(16 * S, 16 * S, rgb("#0c0c10"))
    for cx in (4.5, 11.5):
        win = mix(win, rgb("#3a3028"), soft_ellipse(16 * S, 16 * S, cx * S, 8 * S, 3.2 * S, 3.2 * S))
        win = mix(win, rgb("#d8d4c8"), soft_ellipse(16 * S, 16 * S, cx * S, 8 * S, 1.1 * S, 1.1 * S))
    c.put(48, 16, win)
    return c.done()


def grandma():
    return _grandma(False)


def grandma_dead():
    return _grandma(True)


ITEMS = {
    "lighter": lighter,
    "lighterfuel": lighterfuel,
    "bandages": bandages,
    "flashlight": flashlight,
    "batteries": batteries,
    "soundmeter": soundmeter,
    "boltcutters": boltcutters,
    "carkeys": carkeys,
    "gascan": gascan,
    "carbattery": carbattery,
    "fuse": fuse,
    "cagekey": cagekey,
    "lockpick": lockpick,
    "crowbar": crowbar,
    "bottle": bottle,
    "pills": pills,
    "revolver": revolver,
    "screwdriver": screwdriver,
    "backpack": backpack,
    "smallkey": smallkey,
    "padlock": padlock,
    "vhstape": vhstape,
    "grandma": grandma,
    "grandma_dead": grandma_dead,
    "cleaver": cleaver,
    "tripwire_stake": tripwire_stake,
    "beartrap": beartrap,
}
