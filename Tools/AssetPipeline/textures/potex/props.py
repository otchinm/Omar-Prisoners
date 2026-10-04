"""Textures/Props - prop faces (furniture, signs, barrels, cars, doors, windows ...)."""
import numpy as np
from PIL import Image, ImageDraw

from .core import (FONT_BOLD, FONT_PIXEL, blur, inner_rim, outer_rim, canvas, clamp01, col, cracks_mask, drips, edge_distance,
                   ellipse_mask, fft_noise, font, gradient_map, lerp, lum, make_tileable, mix, radial, resize,
                   saturate, scratches_mask, smoothstep, solid, splat_mask, text_mask, texture, to_mask,
                   uneven_light, warp, water_stains, worley, wrap_offsets)
from .mat import (boards, chips, corrugation, grid, grime, photo, plank_surface, rust_color, rust_mask, wood_grain)

FONT_ROAD = "Overpass-ExtraBold.woff"
FONT_STENCIL = "AllertaStencil-Regular.woff"


# --------------------------------------------------------------------------------------
# helpers
# --------------------------------------------------------------------------------------
def finish(ctx, img, light=0.1, grain=0.03, salt=0):
    H, W = ctx.H, ctx.W
    img = img * uneven_light(ctx.sub(90 + salt), H, W, light)[..., None]
    if grain:
        img = img * (1.0 + grain * photo(ctx, "gravel", salt=77 + salt))[..., None]
    return clamp01(img)


def wood(ctx, H, W, along="y", base="#5a3a24", dark="#1e120a", light="#7a5236", rings=6, salt=0, amt=1.6):
    g = wood_grain(ctx, H, W, along, rings=rings, salt=salt)
    return gradient_map(clamp01((g - 0.5) * amt + 0.5), [(0, dark), (0.55, base), (1, light)])


def rect(H, W, x0, y0, x1, y1):
    """mask of a rectangle given in fractions of the texture"""
    m = np.zeros((H, W), np.float32)
    m[int(y0 * H):int(y1 * H), int(x0 * W):int(x1 * W)] = 1.0
    return m


def bevel(H, W, x0, y0, x1, y1, width, raised=True):
    """shading for a raised (or sunken) rectangular panel, fractions of texture. Returns (mask, shade)"""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    X0, X1, Y0, Y1 = x0 * W, x1 * W, y0 * H, y1 * H
    inside = ((xx >= X0) & (xx < X1) & (yy >= Y0) & (yy < Y1)).astype(np.float32)
    dl, dr, dt, db = xx - X0, X1 - xx, yy - Y0, Y1 - yy
    dmin = np.minimum(np.minimum(dl, dr), np.minimum(dt, db))
    edge = (dmin < width) & (inside > 0)
    shade = np.zeros((H, W), np.float32)
    # top/left lit, bottom/right shadowed
    lit = (np.minimum(dl, dt) <= np.minimum(dr, db))
    s = np.where(lit, 1.0, -1.0) * (1 if raised else -1)
    shade[edge] = s[edge]
    return inside, shade


def apply_bevel(img, shade, amt=0.25):
    return clamp01(img * (1.0 + amt * shade)[..., None])


def put_text(img, lines, fnt_name, size_px, box, color, align="center", stretch=False, spacing=0, rough=None, ctx=None):
    """draw text (fitted into box given as pixel coords) onto img with colour; returns new img and mask"""
    H, W = img.shape[:2]
    fnt = font(fnt_name, size_px)
    m = text_mask(W, H, lines, fnt, align=align, spacing=spacing, box=tuple(int(v) for v in box), stretch=True if stretch else None)
    if rough is not None and ctx is not None:
        r = ctx.sub(950)
        m = m * (1.0 - smoothstep(1.3 - rough, 1.7 - rough, fft_noise(r, H, W, beta=1.6)))
    return mix(img, color, m), m


def dirt_pass(ctx, img, cover=0.3, amt=0.35, color="#1a140e", salt=0):
    H, W = img.shape[:2]
    return mix(img, color, grime(ctx.sub(800 + salt), H, W, cover=cover, beta=2.6, sharp=0.6) * amt)


def frame_border(H, W, t):
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.minimum(np.minimum(xx, W - 1 - xx), np.minimum(yy, H - 1 - yy))
    return (d < t).astype(np.float32), d


def knob(img, cx, cy, r, colr="#a08040", dark="#3a2a10"):
    H, W = img.shape[:2]
    m = ellipse_mask(W, H, cx, cy, r, r)
    hl = ellipse_mask(W, H, cx - r * 0.3, cy - r * 0.3, r * 0.45, r * 0.45, soft=0.8)
    sh = ellipse_mask(W, H, cx + r * 0.25, cy + r * 0.35, r * 1.1, r * 1.1, soft=0.5)
    img = mix(img, "#0c0806", sh * 0.5 * (1 - m))
    img = mix(img, colr, m)
    img = mix(img, dark, m * clamp01((np.mgrid[0:H, 0:W][0] - cy) / r) * 0.6)
    img = mix(img, "#e0d0a0", hl * m * 0.7)
    return img


# --------------------------------------------------------------------------------------
# furniture
# --------------------------------------------------------------------------------------
@texture("Props/wardrobe_front", (128, 256))
def wardrobe_front(ctx):
    W, H = ctx.W, ctx.H
    img = wood(ctx, H, W, "y", base="#5e3e28", dark="#24140a", light="#7a5638", rings=7)
    shade = np.zeros((H, W), np.float32)
    # crown + plinth
    for (y0, y1) in ((0.0, 0.05), (0.93, 1.0)):
        m, sh = bevel(H, W, 0.0, y0, 1.0, y1, W * 0.02)
        shade += sh
        img = mix(img, "#3a2416", m * 0.5)
    for dx in (0.0, 0.5):
        x0, x1 = dx + 0.04, dx + 0.46
        m, sh = bevel(H, W, x0, 0.07, x1, 0.91, W * 0.015, raised=True)
        shade += sh
        for (y0, y1) in ((0.12, 0.52), (0.58, 0.86)):
            pm, psh = bevel(H, W, x0 + 0.06, y0, x1 - 0.06, y1, W * 0.03, raised=True)
            gm, gsh = bevel(H, W, x0 + 0.05, y0 - 0.01, x1 - 0.05, y1 + 0.01, W * 0.012, raised=False)
            shade += psh + gsh * (1 - pm)
            img = mix(img, "#1a0e06", (gm - pm).clip(0, 1) * 0.6)
    # centre split
    img = mix(img, "#0c0604", rect(H, W, 0.495, 0.07, 0.505, 0.91))
    img = apply_bevel(img, shade, 0.3)
    for kx in (0.44, 0.56):
        img = knob(img, kx * W, 0.5 * H, W * 0.022, "#8a6a30")
    img = mix(img, "#0a0604", ellipse_mask(W, H, 0.44 * W, 0.54 * H, W * 0.006, W * 0.012))
    img = dirt_pass(ctx, img, 0.35, 0.4)
    sc = scratches_mask(ctx.sub(3), H, W, 40, length=(10, 60), width=2, tile=False)
    img = mix(img, "#8a6a4a", sc * 0.3)
    return finish(ctx, img)


@texture("Props/dresser_front", (128, 128))
def dresser_front(ctx):
    W, H = ctx.W, ctx.H
    img = wood(ctx, H, W, "x", base="#4e3424", dark="#1c100a", light="#6e4c34", rings=5)
    shade = np.zeros((H, W), np.float32)
    m, sh = bevel(H, W, 0.0, 0.0, 1.0, 0.07, W * 0.015)
    shade += sh
    rows = [(0.1, 0.3), (0.32, 0.52), (0.54, 0.74), (0.76, 0.96)]
    for i, (y0, y1) in enumerate(rows):
        gm, _ = bevel(H, W, 0.05, y0 - 0.008, 0.95, y1 + 0.008, 2)
        img = mix(img, "#0e0804", gm * 0.85)
        dm, dsh = bevel(H, W, 0.06, y0, 0.94, y1, W * 0.02)
        dw = wood(ctx, H, W, "x", base="#56382a", dark="#22140c", light="#76523a", rings=4, salt=10 + i)
        img = mix(img, dw, dm)
        shade += dsh
        for hx in (0.25, 0.75):
            cy = (y0 + y1) / 2 * H
            hm = rect(H, W, hx - 0.07, (y0 + y1) / 2 - 0.018, hx + 0.07, (y0 + y1) / 2 + 0.018)
            img = mix(img, "#8a6a2a", hm)
            img = mix(img, "#d0b070", rect(H, W, hx - 0.07, (y0 + y1) / 2 - 0.018, hx + 0.07, (y0 + y1) / 2 - 0.008) * 0.6)
            img = knob(img, (hx - 0.07) * W, cy, W * 0.014, "#7a5a24")
            img = knob(img, (hx + 0.07) * W, cy, W * 0.014, "#7a5a24")
        img = mix(img, "#080402", ellipse_mask(W, H, 0.5 * W, (y0 + y1) / 2 * H, W * 0.008, W * 0.016))
    img = apply_bevel(img, shade, 0.3)
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img)


@texture("Props/clock_face", (64, 64), k=8)
def clock_face(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    img = wood(ctx, H, W, "y", base="#3e2416", dark="#140a04", light="#5a3a26")
    rad = radial(W, H)
    bezel = (rad < 0.98).astype(np.float32)
    dial = (rad < 0.86).astype(np.float32)
    img = mix(img, gradient_map(clamp01(0.5 + 0.4 * (0.9 - rad)), [(0, "#4a3a1a"), (1, "#a08a4a")]), bezel)
    t = clamp01(0.55 + 0.1 * fft_noise(r, H, W, beta=2.4) + 0.05 * photo(ctx, "moon"))
    face = gradient_map(t, [(0, "#8a7a54"), (0.6, "#c4b48a"), (1, "#d2c49a")])
    face = face * (1.0 - 0.3 * smoothstep(0.5, 0.86, rad))[..., None]
    img = mix(img, face, dial)
    im, d = canvas(W, H)
    cx, cy = W / 2, H / 2
    for i in range(60):
        a = i / 60 * 2 * np.pi
        r0, r1 = (0.72, 0.82) if i % 5 == 0 else (0.77, 0.82)
        d.line([(cx + np.sin(a) * r0 * cx, cy - np.cos(a) * r0 * cy), (cx + np.sin(a) * r1 * cx, cy - np.cos(a) * r1 * cy)],
               fill=255, width=int((9 if i % 5 == 0 else 4) * s))
    numerals = ["XII", "I", "II", "III", "IIII", "V", "VI", "VII", "VIII", "IX", "X", "XI"]
    f = font(FONT_ROAD, int(46 * s))
    for i, nm in enumerate(numerals):
        a = i / 12 * 2 * np.pi
        x, y = cx + np.sin(a) * 0.56 * cx, cy - np.cos(a) * 0.56 * cy
        bb = d.textbbox((0, 0), nm, font=f)
        d.text((x - (bb[2] - bb[0]) / 2 - bb[0], y - (bb[3] - bb[1]) / 2 - bb[1]), nm, font=f, fill=255)
    # hands: 11:55 (five to midnight)
    for ang, L, wd in ((-np.pi / 12 * 0.9 * 2, 0.42, 16), (-np.pi / 30 * 5 * 2 / 2, 0.66, 10)):
        d.line([(cx, cy), (cx + np.sin(ang) * L * cx, cy - np.cos(ang) * L * cy)], fill=255, width=int(wd * s))
    d.ellipse([cx - 14 * s, cy - 14 * s, cx + 14 * s, cy + 14 * s], fill=255)
    m = to_mask(im)
    img = mix(img, "#140e08", m * dial)
    img = mix(img, "#3a2a10", grime(ctx.sub(2), H, W, cover=0.3, beta=2.6, sharp=0.5) * dial * 0.35)
    # glass glare
    img = mix(img, "#e8e0c8", ellipse_mask(W, H, 0.36 * W, 0.3 * H, 0.18 * W, 0.08 * H, soft=0.9) * dial * 0.25)
    return finish(ctx, img, light=0.05)


@texture("Props/clock_body", (64, 256), k=8)
def clock_body(ctx):
    W, H = ctx.W, ctx.H
    img = wood(ctx, H, W, "y", base="#4e2e1e", dark="#1a0c06", light="#6a4430", rings=4)
    shade = np.zeros((H, W), np.float32)
    # hood (top 25%): arch opening darker where the face sits
    hm, hsh = bevel(H, W, 0.02, 0.0, 0.98, 0.26, W * 0.04)
    shade += hsh
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    arch = ((xx - W / 2) ** 2 + (yy - 0.14 * H) ** 2 < (0.36 * W) ** 2).astype(np.float32)
    img = mix(img, "#1a0e06", arch * 0.6)
    # trunk with glass window
    tm, tsh = bevel(H, W, 0.12, 0.28, 0.88, 0.80, W * 0.03)
    shade += tsh
    gm, gsh = bevel(H, W, 0.24, 0.33, 0.76, 0.75, W * 0.02, raised=False)
    shade += gsh
    r = ctx.sub(1)
    glass = gradient_map(clamp01(0.3 + 0.2 * fft_noise(r, H, W, beta=2.4)), [(0, "#0a0a08"), (1, "#3a3a32")])
    # pendulum rod + bob seen through glass
    rod = rect(H, W, 0.485, 0.33, 0.515, 0.66)
    bob = ellipse_mask(W, H, 0.5 * W, 0.67 * H, 0.13 * W, 0.13 * W)
    glass = mix(glass, "#7a6a3a", rod * 0.8)
    glass = mix(glass, "#9a8240", bob)
    glass = mix(glass, "#d8c890", ellipse_mask(W, H, 0.47 * W, 0.655 * H, 0.05 * W, 0.04 * W, soft=0.8) * 0.6)
    refl = smoothstep(0.45, 0.55, ((xx / W) - (yy / H) * 0.3) % 0.5 / 0.5) * 0.12
    glass = clamp01(glass + refl[..., None])
    img = mix(img, glass, gm)
    # base
    bm, bsh = bevel(H, W, 0.0, 0.82, 1.0, 1.0, W * 0.05)
    shade += bsh
    pm, psh = bevel(H, W, 0.15, 0.85, 0.85, 0.96, W * 0.03)
    shade += psh
    img = apply_bevel(img, shade, 0.3)
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img, light=0.06)


@texture("Props/radiator", (128, 64))
def radiator(ctx):
    W, H = ctx.W, ctx.H
    n = 14
    xs = (np.arange(W) + 0.5) / W
    p = (xs * n) % 1.0
    fin = (np.abs(p - 0.5) < 0.36).astype(np.float32)
    cyl = np.cos((p - 0.5) / 0.36 * np.pi / 2).clip(0, 1) ** 0.6
    shade = np.broadcast_to((0.35 + 0.75 * cyl * fin)[None, :], (H, W)).copy()
    yy = (np.arange(H) + 0.5) / H
    hub = ((yy < 0.12) | (yy > 0.86)).astype(np.float32)
    shade = np.where(hub[:, None] > 0, 0.55 + 0.45 * np.cos((yy[:, None] - 0.06) * 8) * fin[None, :] + 0.2, shade)
    gap = (1 - fin)[None, :] * (1 - hub[:, None])
    r = ctx.sub(1)
    base = gradient_map(clamp01(0.5 + 0.12 * fft_noise(r, H, W, beta=2.6)), [(0, "#6a5a44"), (1, "#a0907a")])
    rm = rust_mask(ctx, H, W, cover=0.3, streaks=10)
    base = mix(base, rust_color(ctx, H, W), rm * 0.8)
    img = base * shade[..., None]
    img = mix(img, "#080604", gap * 0.9)
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img)


def crt_frame(ctx, inner, salt=0):
    """compose screen content `inner` (H,W,3) into a curved CRT screen with dark bezel"""
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = (xx + 0.5) / W * 2 - 1, (yy + 0.5) / H * 2 - 1
    rr = (np.abs(u) ** 4 + np.abs(v) ** 4) ** 0.25
    screen = (rr < 0.9).astype(np.float32)
    img = inner * (1.0 - 0.6 * smoothstep(0.5, 0.9, rr))[..., None]
    scan = 1.0 - 0.18 * (np.floor(yy / (H / 64)) % 2)
    img = img * scan[..., None]
    img = mix(img, "#0c0c0c", 1 - screen)
    img = mix(img, "#2a2a2a", ((rr > 0.9) & (rr < 0.93)).astype(np.float32) * 0.6)
    return img


def tv_noise(ctx, salt, ghost=False):
    W, H = ctx.W, ctx.H
    r = ctx.sub(100 + salt)
    # static at 64-ish native resolution, horizontally smeared
    n = r.random((H // 4, W // 2)).astype(np.float32)
    n = resize(n, W, H, Image.NEAREST)
    lines = r.random(H // 4).astype(np.float32)
    lines = np.repeat(lines, 4)[:, None]
    v = clamp01(0.15 + 0.7 * n * (0.6 + 0.6 * lines))
    # rolling bright band
    yy = (np.arange(H) + 0.5) / H
    band = np.exp(-((yy - r.uniform(0.1, 0.9)) / 0.08) ** 2)[:, None]
    v = clamp01(v + band * 0.25)
    if ghost:
        # faint sack-mask face
        xx = np.arange(W)[None, :]
        face = ellipse_mask(W, H, W * 0.5, H * 0.5, W * 0.24, H * 0.32, soft=0.6)
        eye_l = ellipse_mask(W, H, W * 0.41, H * 0.45, W * 0.06, H * 0.075, soft=0.5)
        eye_r = ellipse_mask(W, H, W * 0.59, H * 0.45, W * 0.06, H * 0.075, soft=0.5)
        mouth = ellipse_mask(W, H, W * 0.5, H * 0.66, W * 0.07, H * 0.02, soft=0.6)
        v = v * (1.0 - 0.45 * face)
        ghostv = face * 0.5 - (eye_l + eye_r) * 0.75 - mouth * 0.3
        v = clamp01(v * 0.8 + ghostv)
    img = np.stack([v, v, v * 1.05], -1)
    return crt_frame(ctx, clamp01(img), salt)


@texture("Props/tv_static_0", (64, 64), k=4, q=60, bits=5, desat=0.0, dark=1.0)
def tv_static_0(ctx):
    return tv_noise(ctx, 0)


@texture("Props/tv_static_1", (64, 64), k=4, q=60, bits=5, desat=0.0, dark=1.0)
def tv_static_1(ctx):
    return tv_noise(ctx, 1)


@texture("Props/tv_static_2", (64, 64), k=4, q=60, bits=5, desat=0.0, dark=1.0)
def tv_static_2(ctx):
    return tv_noise(ctx, 2, ghost=True)


@texture("Props/tv_static_3", (64, 64), k=4, q=60, bits=5, desat=0.0, dark=1.0)
def tv_static_3(ctx):
    return tv_noise(ctx, 3)


@texture("Props/tv_body", (64, 64), k=8)
def tv_body(ctx):
    W, H = ctx.W, ctx.H
    img = wood(ctx, H, W, "x", base="#5a3a22", dark="#22140a", light="#7a5232", rings=4)
    shade = np.zeros((H, W), np.float32)
    m, sh = bevel(H, W, 0.0, 0.0, 1.0, 1.0, W * 0.04)
    shade += sh
    # screen opening left, control panel right
    sm, ssh = bevel(H, W, 0.07, 0.1, 0.7, 0.9, W * 0.03, raised=False)
    shade += ssh
    r = ctx.sub(1)
    glass = gradient_map(clamp01(0.3 + 0.15 * fft_noise(r, H, W, beta=2.4)), [(0, "#0e1010"), (1, "#2e3432")])
    glass = mix(glass, "#5a625e", ellipse_mask(W, H, 0.3 * W, 0.3 * H, 0.15 * W, 0.08 * H, soft=0.9) * 0.4)
    img = mix(img, glass, sm)
    pm, psh = bevel(H, W, 0.75, 0.1, 0.94, 0.9, W * 0.015, raised=False)
    img = mix(img, "#2a2620", pm)
    shade += psh
    for ky in (0.22, 0.42):
        img = knob(img, 0.845 * W, ky * H, W * 0.055, "#3a3632", "#0a0a0a")
        img = mix(img, "#c0b8a0", rect(H, W, 0.84, ky - 0.05, 0.85, ky - 0.02))
    yy = (np.arange(H) + 0.5) / H
    grille = ((yy * 40) % 1.0 < 0.5)[:, None] * rect(H, W, 0.78, 0.58, 0.91, 0.86)
    img = mix(img, "#0a0806", grille * 0.8)
    img = apply_bevel(img, shade, 0.3)
    img = dirt_pass(ctx, img, 0.3, 0.35)
    return finish(ctx, img, light=0.06)


@texture("Props/fridge_front", (64, 128), k=8)
def fridge_front(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.6 + 0.08 * fft_noise(r, H, W, beta=2.6) + 0.04 * photo(ctx, "moon"))
    img = gradient_map(t, [(0, "#7a7466"), (0.6, "#b4ae9a"), (1, "#c4bea8")])
    shade = np.zeros((H, W), np.float32)
    m1, s1 = bevel(H, W, 0.03, 0.02, 0.97, 0.33, W * 0.03)
    m2, s2 = bevel(H, W, 0.03, 0.35, 0.97, 0.93, W * 0.03)
    shade += s1 + s2
    img = mix(img, "#2a2620", rect(H, W, 0.0, 0.332, 1.0, 0.348))
    img = mix(img, "#1a1814", rect(H, W, 0.0, 0.93, 1.0, 1.0))
    # chrome handles
    for (y0, y1) in ((0.18, 0.3), (0.38, 0.6)):
        img = mix(img, "#1a1814", rect(H, W, 0.80, y0 - 0.01, 0.9, y1 + 0.01) * 0.7)
        hm = rect(H, W, 0.81, y0, 0.88, y1)
        img = mix(img, "#6a6c68", hm)
        img = mix(img, "#d0d0c8", rect(H, W, 0.82, y0, 0.84, y1) * 0.8)
    # rust and grime at the bottom + around handles
    rm = rust_mask(ctx, H, W, cover=0.12, streaks=8) * smoothstep(0.55, 0.95, (np.arange(H) / H))[:, None]
    img = mix(img, rust_color(ctx, H, W), clamp01(rm * 1.4))
    hand = ellipse_mask(W, H, 0.82 * W, 0.45 * H, 0.18 * W, 0.12 * H, soft=0.8)
    img = mix(img, "#4a4232", hand * 0.4)
    # magnets
    for (mx, my, c) in ((0.25, 0.12, "#8a1a14"), (0.45, 0.2, "#1a4a8a"), (0.3, 0.45, "#c0a020"), (0.6, 0.5, "#2a6a2a")):
        img = mix(img, c, ellipse_mask(W, H, mx * W, my * H, 0.045 * W, 0.045 * W))
    # a note held by a magnet
    img = mix(img, "#c8b880", rect(H, W, 0.22, 0.46, 0.46, 0.62) * 0.95)
    img = mix(img, "#4a4030", (scratches_mask(r, H, W, 8, length=(20, 50), angle=0.0, width=4, tile=False) * rect(H, W, 0.25, 0.49, 0.43, 0.6)) * 0.6)
    img = apply_bevel(img, shade, 0.25)
    img = dirt_pass(ctx, img, 0.35, 0.35, "#3a3222")
    return finish(ctx, img, light=0.06)


# --------------------------------------------------------------------------------------
# paintings
# --------------------------------------------------------------------------------------
def gilded_frame(ctx, img, t_frac):
    H, W = img.shape[:2]
    t = t_frac * min(W, H)
    fm, d = frame_border(H, W, t)
    r = ctx.sub(400)
    prof = np.cos(np.clip(d / t, 0, 1) * np.pi) * 0.5 + 0.5
    orn = clamp01(0.5 + 0.35 * fft_noise(r, H, W, beta=1.2) + 0.2 * photo(ctx, "gravel_fine", H, W, salt=40))
    gold = gradient_map(clamp01(0.3 + 0.5 * prof * orn), [(0, "#1e1406"), (0.5, "#6a5022"), (0.85, "#a08440"), (1, "#c8ac60")])
    img = mix(img, gold, fm)
    inner = ((d >= t) & (d < t + max(2, t * 0.12))).astype(np.float32)
    img = mix(img, "#0a0604", inner * 0.8)
    return img


def oil_paint(ctx, img, strength=1.0):
    """brush texture + craquelure + varnish yellowing on a painting"""
    H, W = img.shape[:2]
    r = ctx.sub(410)
    strokes = fft_noise(r, H, W, beta=1.4, ax=3, ay=1) * 0.06 * strength
    img = clamp01(img * (1 + strokes)[..., None])
    F1, F2, _ = worley(r, H, W, 160, tile=False)
    crack = 1 - smoothstep(0.0, 1.5, F2 - F1)
    img = img * (1 - 0.35 * crack)[..., None]
    img = mix(img, "#6a5420", np.full((H, W), 0.18, np.float32))
    return img


@texture("Props/portrait_omar", (64, 128), k=8, q=40)
def portrait_omar(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    bgt = clamp01(0.4 + 0.25 * fft_noise(r, H, W, beta=3.0))
    img = gradient_map(bgt, [(0, "#0a0806"), (0.5, "#2a2214"), (1, "#4a3a20")])
    img = img * (1.0 - 0.5 * smoothstep(0.3, 1.0, radial(W, H, W * 0.5, H * 0.4, W * 0.7)))[..., None]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # shoulders / plaid shirt
    body = ((yy > 0.6 * H) & (np.abs(xx - W / 2) < (0.2 * W + (yy - 0.6 * H) * 0.9))).astype(np.float32)
    plaid_u = np.sin(xx / W * 2 * np.pi * 7) > 0.2
    plaid_v = np.sin(yy / H * 2 * np.pi * 14) > 0.2
    thin_u = np.abs(np.sin(xx / W * 2 * np.pi * 7 + 1.2)) < 0.12
    thin_v = np.abs(np.sin(yy / H * 2 * np.pi * 14 + 1.2)) < 0.12
    plaid = np.stack([0.30 + 0.25 * plaid_u + 0.25 * plaid_v, 0.04 + 0.04 * plaid_u * plaid_v, 0.04 + 0.03 * plaid_u * plaid_v], -1)
    plaid = mix(plaid.astype(np.float32), "#101010", (thin_u | thin_v).astype(np.float32) * 0.7)
    folds = fft_noise(r, H, W, beta=2.2, ax=1, ay=2)
    plaid = plaid * (0.6 + 0.25 * folds)[..., None]
    img = mix(img, plaid, body)
    # apron straps (bloody)
    for sx in (-1, 1):
        strap = (np.abs((xx - W / 2) - sx * (0.12 * W + (yy - 0.62 * H) * 0.15)) < 0.035 * W) & (yy > 0.62 * H)
        img = mix(img, "#3a1410", strap.astype(np.float32) * 0.9)
    # neck rope
    img = mix(img, "#6a5232", ellipse_mask(W, H, W / 2, 0.6 * H, 0.16 * W, 0.025 * H) * 0.9)
    # sack head
    head = ellipse_mask(W, H, W / 2, 0.42 * H, 0.21 * W, 0.17 * H)
    top = ((np.abs(xx - W / 2) < 0.19 * W) & (yy > 0.24 * H) & (yy < 0.42 * H)).astype(np.float32)
    headm = np.maximum(head, top)
    sk = clamp01(0.55 + 0.12 * photo(ctx, "grass", salt=3) + 0.15 * fft_noise(r, H, W, beta=2.4))
    sack = gradient_map(sk, [(0, "#5a5a40"), (0.5, "#a4a27a"), (1, "#c8c49a")])
    sack = sack * (1.0 - 0.45 * smoothstep(0.0, 0.2 * W, np.abs(xx - W * 0.45)) * 0)[..., None]
    sack = sack * (0.75 + 0.35 * clamp01(1 - (xx - W * 0.35) / (W * 0.4)))[..., None]
    img = mix(img, sack, headm)
    for ex in (0.41, 0.59):
        eye = ellipse_mask(W, H, ex * W, 0.41 * H, 0.055 * W, 0.04 * H, soft=0.35)
        img = mix(img, "#050302", eye)
    img = mix(img, "#3a3826", ellipse_mask(W, H, 0.5 * W, 0.5 * H, 0.06 * W, 0.012 * H, soft=0.6) * 0.6)
    img = oil_paint(ctx, img)
    img = gilded_frame(ctx, img, 0.12)
    return finish(ctx, img, light=0.05, grain=0.02)


@texture("Props/painting_landscape", (128, 64), q=40)
def painting_landscape(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    pano = ctx.src.hdri_ldr("sunrise", exposure=0.6)  # real photo: field, trees, sky
    crop = pano[96:160, 210:338]  # field + treeline + sky band
    scene = resize(crop, W, H, Image.BICUBIC)
    scene = saturate(scene, 0.55)
    l = lum(scene)[..., None]
    scene = clamp01((scene * 0.8 + l * 0.2) * 1.1)
    scene = mix(scene, "#3a3a24", np.full((H, W), 0.25, np.float32))
    yy = (np.arange(H) + 0.5) / H
    sky = (1 - smoothstep(0.35, 0.6, yy))[:, None] * np.ones((1, W))
    clouds = clamp01(0.5 + 0.35 * fft_noise(r, H, W, beta=2.6, ax=1.5, ay=0.5))
    scene = mix(scene, gradient_map(clouds, [(0, "#0e1012"), (0.6, "#3a4040"), (1, "#6a6a5a")]), sky * 0.75)
    # farmhouse silhouette on the horizon
    im, d = canvas(W, H)
    hx, hy = 0.62 * W, 0.56 * H
    d.polygon([(hx, hy), (hx + 0.1 * W, hy), (hx + 0.1 * W, hy - 0.1 * H), (hx + 0.05 * W, hy - 0.17 * H), (hx, hy - 0.1 * H)], fill=255)
    d.rectangle([hx + 0.11 * W, hy - 0.22 * H, hx + 0.125 * W, hy], fill=255)  # silo
    scene = mix(scene, "#0a0a08", to_mask(im) * 0.9)
    scene = mix(scene, "#a03018", ellipse_mask(W, H, hx + 0.05 * W, hy - 0.05 * H, 0.008 * W, 0.016 * H) * 0.8)
    scene = scene * (1 - 0.45 * smoothstep(0.5, 1.2, radial(W, H, r=W * 0.55)))[..., None]
    img = oil_paint(ctx, scene)
    img = gilded_frame(ctx, img, 0.1)
    return finish(ctx, img, light=0.05, grain=0.02)


# --------------------------------------------------------------------------------------
# signs
# --------------------------------------------------------------------------------------
def sign_wear(ctx, img, W, H, salt=0, rust_amt=0.6, holes=0):
    r = ctx.sub(200 + salt)
    img = dirt_pass(ctx, img, 0.35, 0.3, "#3a3020", salt)
    dr = drips(r, H, W, 8, length=(0.1, 0.5), width=(W / 300, W / 120), tile=False)
    img = mix(img, "#4a2a12", dr * 0.4 * rust_amt)
    ch = chips(ctx, H, W, cover=0.05, sharp=0.04, beta=1.8, salt=salt)
    img = mix(img, rust_color(ctx, H, W, salt=salt), ch * rust_amt)
    for _ in range(holes):
        x, y = r.uniform(0.15, 0.85) * W, r.uniform(0.15, 0.85) * H
        rr = W * 0.012
        img = mix(img, "#4a3a2a", ellipse_mask(W, H, x, y, rr * 2.2, rr * 2.2, soft=0.7) * 0.6)
        img = mix(img, "#050404", ellipse_mask(W, H, x, y, rr, rr))
    return img


def bolts(img, pts, r):
    H, W = img.shape[:2]
    for (x, y) in pts:
        img = mix(img, "#5a5048", ellipse_mask(W, H, x * W, y * H, r, r))
        img = mix(img, "#2a2018", ellipse_mask(W, H, x * W + r * 0.3, y * H + r * 0.3, r * 0.6, r * 0.6) * 0.6)
    return img


@texture("Props/sign_fallout", (64, 64), k=8, q=45, desat=0.05)
def sign_fallout(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.55 + 0.1 * fft_noise(r, H, W, beta=2.4))
    img = gradient_map(t, [(0, "#a06a10"), (1, "#e0a020")])
    fb, _ = frame_border(H, W, W * 0.03)
    img = mix(img, "#1a1206", fb)
    cx, cy, R = W * 0.5, H * 0.4, W * 0.32
    circ = ellipse_mask(W, H, cx, cy, R, R)
    img = mix(img, "#120c06", circ)
    inner = ellipse_mask(W, H, cx, cy, R * 0.9, R * 0.9)
    img = mix(img, "#d8981c", inner)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    ang = np.arctan2(yy - cy, xx - cx)
    rad = np.hypot(xx - cx, yy - cy)
    blade = ((np.mod(ang + np.pi / 2 + np.pi / 6, 2 * np.pi / 3) < np.pi / 3) & (rad > R * 0.22) & (rad < R * 0.82)).astype(np.float32)
    img = mix(img, "#120c06", blade)
    img = mix(img, "#120c06", ellipse_mask(W, H, cx, cy, R * 0.15, R * 0.15))
    img = mix(img, "#120c06", rect(H, W, 0.06, 0.8, 0.94, 0.95))
    img, _ = put_text(img, ["FALLOUT SHELTER"], FONT_ROAD, 120, (0.1 * W, 0.82 * H, 0.9 * W, 0.93 * H), "#d8981c", stretch=True)
    img = sign_wear(ctx, img, W, H, 1, rust_amt=0.4)
    return finish(ctx, img, light=0.06)


@texture("Props/sign_restricted", (128, 64), k=8, q=45, desat=0.05)
def sign_restricted(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.6 + 0.08 * fft_noise(r, H, W, beta=2.4))
    img = gradient_map(t, [(0, "#8a8678"), (1, "#c8c4b2")])
    fb, d = frame_border(H, W, W * 0.018)
    img = mix(img, "#141210", fb)
    img = mix(img, "#8a1410", rect(H, W, 0.04, 0.33, 0.96, 0.66))
    img, _ = put_text(img, ["PRIVATE PROPERTY"], FONT_ROAD, 160, (0.08 * W, 0.08 * H, 0.92 * W, 0.28 * H), "#121010", stretch=True)
    img, _ = put_text(img, ["NO TRESPASSING"], FONT_ROAD, 200, (0.07 * W, 0.37 * H, 0.93 * W, 0.62 * H), "#d8d4c4", stretch=True)
    img, _ = put_text(img, ["VIOLATORS WILL BE SHOT"], FONT_ROAD, 160, (0.08 * W, 0.72 * H, 0.92 * W, 0.91 * H), "#121010", stretch=True)
    img = bolts(img, [(0.05, 0.5), (0.95, 0.5)], W * 0.008)
    img = sign_wear(ctx, img, W, H, 2, rust_amt=0.7, holes=5)
    return finish(ctx, img, light=0.06)


@texture("Props/sign_road", (128, 64), k=8, q=45, desat=0.05)
def sign_road(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.62 + 0.07 * fft_noise(r, H, W, beta=2.4))
    img = gradient_map(t, [(0, "#9a988e"), (1, "#d2d0c6")])
    # rounded black border inset
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    x0, y0, x1, y1, rr = W * 0.035, H * 0.07, W * 0.965, H * 0.93, W * 0.03
    dx = np.maximum(np.maximum(x0 + rr - xx, xx - (x1 - rr)), 0)
    dy = np.maximum(np.maximum(y0 + rr - yy, yy - (y1 - rr)), 0)
    dist = np.hypot(dx, dy)
    border = ((dist <= rr) & (dist >= rr - W * 0.012)).astype(np.float32)
    inside_edge = ((xx > x0) & (xx < x1) & (yy > y0) & (yy < y1)).astype(np.float32)
    border = np.maximum(border, inside_edge * (((xx - x0 < W * 0.012) | (x1 - xx < W * 0.012) | (yy - y0 < W * 0.012) | (y1 - yy < W * 0.012)) & (dist == 0)))
    img = mix(img, "#141414", border)
    img, _ = put_text(img, ["DEAD END"], FONT_ROAD, 200, (0.1 * W, 0.13 * H, 0.9 * W, 0.37 * H), "#141414", stretch=True)
    img, _ = put_text(img, ["NO EXIT AHEAD"], FONT_ROAD, 200, (0.1 * W, 0.42 * H, 0.9 * W, 0.63 * H), "#141414", stretch=True)
    img, _ = put_text(img, ["$1000 FINE FOR TRESPASSING"], FONT_ROAD, 120, (0.1 * W, 0.7 * H, 0.9 * W, 0.84 * H), "#141414", stretch=True)
    img = sign_wear(ctx, img, W, H, 3, rust_amt=0.5, holes=3)
    return finish(ctx, img, light=0.05)


# --------------------------------------------------------------------------------------
# containers
# --------------------------------------------------------------------------------------
def drum_shading(H, W, ribs=(0.18, 0.82)):
    yy = (np.arange(H) + 0.5) / H
    sh = np.ones(H, np.float32)
    for ry in ribs:
        sh += 0.22 * np.exp(-((yy - ry + 0.012) / 0.012) ** 2) - 0.3 * np.exp(-((yy - ry - 0.012) / 0.012) ** 2)
    sh -= 0.3 * (np.exp(-((yy - 0.0) / 0.04) ** 2) + np.exp(-((yy - 1.0) / 0.04) ** 2))
    return np.broadcast_to(sh[:, None], (H, W)).astype(np.float32)


@texture("Props/barrel_water", (64, 64), k=8, tile=True, q=45)
def barrel_water(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.5 + 0.12 * fft_noise(r, H, W, beta=2.4) + 0.05 * photo(ctx, "gravel_fine"))
    img = gradient_map(t, [(0, "#0c0c0c"), (1, "#2c2c2a")])
    img = img * drum_shading(H, W)[..., None]
    lab = (0.22, 0.26, 0.78, 0.78)
    small = ["IN CASE OF CIVIL", "EMERGENCY USE ONLY", "STORE IN COOL PLACE"]
    img, _ = put_text(img, small[:2], FONT_ROAD, 60, (lab[0] * W, 0.27 * H, lab[2] * W, 0.4 * H), "#a8a8a0", stretch=True, spacing=8)
    img, _ = put_text(img, ["DRINKING WATER"], FONT_BOLD, 200, (0.18 * W, 0.44 * H, 0.82 * W, 0.6 * H), "#d0d0c4", stretch=True)
    img, _ = put_text(img, ["50 GAL.  CIVIL DEFENSE"], FONT_ROAD, 60, (0.26 * W, 0.64 * H, 0.74 * W, 0.72 * H), "#a8a8a0", stretch=True)
    # CD triangle-in-circle symbol on the right of the label
    cx, cy, R = 0.86 * W, 0.36 * H, 0.06 * W
    img = mix(img, "#c8c8bc", ellipse_mask(W, H, cx, cy, R, R) - ellipse_mask(W, H, cx, cy, R * 0.8, R * 0.8))
    im, d = canvas(W, H)
    d.polygon([(cx, cy - R * 0.7), (cx + R * 0.62, cy + R * 0.42), (cx - R * 0.62, cy + R * 0.42)], fill=255)
    img = mix(img, "#c8c8bc", to_mask(im))
    img = dirt_pass(ctx, img, 0.3, 0.3, "#3a3426")
    sc = scratches_mask(ctx.sub(3), H, W, 30, length=(10, 50), width=3)
    img = mix(img, "#4a4a46", sc * 0.4)
    return finish(ctx, img, light=0.05)


@texture("Props/barrel_fuel", (64, 64), k=8, tile=True, q=45)
def barrel_fuel(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.5 + 0.12 * fft_noise(r, H, W, beta=2.4))
    img = gradient_map(t, [(0, "#3a0a06"), (1, "#8a2216")])
    rm = rust_mask(ctx, H, W, cover=0.35, streaks=14)
    img = mix(img, rust_color(ctx, H, W), rm)
    img = img * drum_shading(H, W)[..., None]
    img, m = put_text(img, ["FLAMMABLE"], FONT_STENCIL, 200, (0.2 * W, 0.4 * H, 0.8 * W, 0.58 * H), "#d0c8b0", stretch=True, rough=0.3, ctx=ctx)
    # flame diamond
    cx, cy, R = 0.5 * W, 0.28 * H, 0.08 * W
    im, d = canvas(W, H)
    d.polygon([(cx, cy - R), (cx + R, cy), (cx, cy + R), (cx - R, cy)], fill=255)
    img = mix(img, "#d0c8b0", to_mask(im))
    im, d = canvas(W, H)
    d.polygon([(cx, cy - R * 0.6), (cx + R * 0.3, cy + R * 0.1), (cx + R * 0.15, cy + R * 0.45), (cx - R * 0.2, cy + R * 0.45), (cx - R * 0.3, cy)], fill=255)
    img = mix(img, "#6a140c", to_mask(im))
    img, _ = put_text(img, ["NO SMOKING  DIESEL"], FONT_ROAD, 60, (0.3 * W, 0.62 * H, 0.7 * W, 0.68 * H), "#c0b8a0", stretch=True)
    img = dirt_pass(ctx, img, 0.3, 0.35)
    return finish(ctx, img, light=0.05)


@texture("Props/bucket_food", (64, 64), k=8, tile=True, q=45)
def bucket_food(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.6 + 0.08 * fft_noise(r, H, W, beta=2.4) + 0.03 * photo(ctx, "moon"))
    img = gradient_map(t, [(0, "#8a8a84"), (1, "#cacac2")])
    yy = (np.arange(H) + 0.5) / H
    rim = (np.exp(-((yy - 0.06) / 0.03) ** 2) + np.exp(-((yy - 0.14) / 0.02) ** 2))[:, None]
    img = img * (1 - 0.25 * rim)[..., None]
    lab = rect(H, W, 0.18, 0.26, 0.82, 0.88)
    green = gradient_map(clamp01(0.5 + 0.1 * fft_noise(ctx.sub(2), H, W, beta=2.4)), [(0, "#14301a"), (1, "#2a5a32")])
    img = mix(img, green, lab)
    img = mix(img, "#c8c8b4", rect(H, W, 0.2, 0.3, 0.8, 0.31) + rect(H, W, 0.2, 0.83, 0.8, 0.84))
    img, _ = put_text(img, ["FOOD SUPPLY"], FONT_BOLD, 200, (0.21 * W, 0.36 * H, 0.79 * W, 0.56 * H), "#e0e0d0", stretch=True)
    img, _ = put_text(img, ["EMERGENCY RATIONS", "1200 SERVINGS"], FONT_ROAD, 60, (0.27 * W, 0.6 * H, 0.73 * W, 0.78 * H), "#c0c8b8", stretch=True, spacing=10)
    img = dirt_pass(ctx, img, 0.35, 0.3, "#4a4232")
    sm = drips(ctx.sub(3), H, W, 6, length=(0.1, 0.3), width=(3, 8))
    img = mix(img, "#5a4a30", sm * 0.3)
    return finish(ctx, img, light=0.05)


@texture("Props/box_cardboard", (64, 64), k=8)
def box_cardboard(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.55 + 0.12 * fft_noise(r, H, W, beta=2.6) + 0.06 * photo(ctx, "gravel_fine"))
    img = gradient_map(t, [(0, "#4a3418"), (0.5, "#8a6a3e"), (1, "#a8865a")])
    xs = (np.arange(W) + 0.5) / W
    flute = (0.97 + 0.03 * np.sin(xs * 2 * np.pi * 60))[None, :]
    img = img * flute[..., None]
    img = mix(img, "#5a4020", rect(H, W, 0.0, 0.0, 1.0, 0.035) + rect(H, W, 0.0, 0.965, 1.0, 1.0))
    img = mix(img, "#a89060", rect(H, W, 0.42, 0.0, 0.58, 0.3) * 0.8)  # tape
    img = mix(img, "#3a2a14", rect(H, W, 0.0, 0.0, 0.02, 1.0) + rect(H, W, 0.98, 0.0, 1.0, 1.0))
    img, _ = put_text(img, ["THIS SIDE UP"], FONT_ROAD, 80, (0.2 * W, 0.62 * H, 0.8 * W, 0.7 * H), "#2a1e10", stretch=True)
    im, d = canvas(W, H)
    for ax in (0.33, 0.67):
        d.polygon([(ax * W, 0.4 * H), (ax * W + 0.06 * W, 0.5 * H), (ax * W - 0.06 * W, 0.5 * H)], fill=255)
        d.rectangle([ax * W - 0.02 * W, 0.5 * H, ax * W + 0.02 * W, 0.58 * H], fill=255)
    img = mix(img, "#2a1e10", to_mask(im))
    fill, ring = water_stains(ctx.sub(2), H, W, n=2, rmin=0.15, rmax=0.3, tile=False)
    img = mix(img, "#4a3014", fill * 0.35 + ring * 0.4)
    img = dirt_pass(ctx, img, 0.3, 0.35)
    return finish(ctx, img, light=0.06)


@texture("Props/crate_wood", (64, 64), k=8)
def crate_wood(ctx):
    W, H = ctx.W, ctx.H
    img, b = plank_surface(ctx, H, W, 4, "x", base="#6e5234", dark="#2a1a0a", light="#8a6a48", joints=(0, 0), gap_px=W * 0.012, grain_amt=1.3)
    shade = np.zeros((H, W), np.float32)
    fr = wood(ctx, H, W, "y", base="#a08058", dark="#4e361c", light="#bea070", salt=5)
    fm = np.maximum(rect(H, W, 0, 0, 1, 0.12) + rect(H, W, 0, 0.88, 1, 1), rect(H, W, 0, 0, 0.12, 1) + rect(H, W, 0.88, 0, 1, 1))
    fm = clamp01(fm)
    # diagonal brace
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    diag = (np.abs((xx / W) - (yy / H)) < 0.07).astype(np.float32) * (1 - fm)
    img = mix(img, fr, clamp01(fm + diag))
    edge = outer_rim(fm + diag, W * 0.006)
    img = mix(img, "#0e0802", edge * 0.9)
    sh = np.roll(clamp01(fm + diag), (int(W * 0.015), int(W * 0.015)), (0, 1)) * (1 - clamp01(fm + diag))
    img = mix(img, "#0a0602", sh * 0.6)
    for (x, y) in ((0.06, 0.06), (0.94, 0.06), (0.06, 0.94), (0.94, 0.94), (0.5, 0.06), (0.5, 0.94)):
        img = mix(img, "#1a1410", ellipse_mask(W, H, x * W, y * H, W * 0.012, W * 0.012))
    img, _ = put_text(img, ["FRAGILE"], FONT_STENCIL, 120, (0.2 * W, 0.18 * H, 0.62 * W, 0.3 * H), "#1a0e04", stretch=True, rough=0.3, ctx=ctx)
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img, light=0.06)


def car_paint(ctx, H, W, base="#4a181c", salt=0, rust=0.12, peel=0.2):
    r = ctx.sub(600 + salt)
    t = clamp01(0.55 + 0.1 * fft_noise(r, H, W, beta=2.6) + 0.03 * photo(ctx, "gravel_fine", H, W, salt=salt))
    img = gradient_map(t, [(0, "#1a0808"), (0.55, base), (1, "#7a3a3a")])
    # oxidised / faded clearcoat
    ox = grime(ctx.sub(601 + salt), H, W, cover=peel, beta=2.2, sharp=0.3)
    img = mix(img, "#8a6a66", ox * 0.4)
    rm = rust_mask(ctx, H, W, cover=rust, salt=salt)
    img = mix(img, rust_color(ctx, H, W, salt=salt), rm)
    return img


@texture("Props/car_body", (128, 128), tile=True)
def car_body(ctx):
    W, H = ctx.W, ctx.H
    img = car_paint(ctx, H, W)
    img = dirt_pass(ctx, img, 0.35, 0.35, "#3a3020")
    dr = drips(ctx.sub(1), H, W, 12, length=(0.1, 0.4), width=(2, 6))
    img = mix(img, "#4a2a14", dr * 0.4)
    return finish(ctx, img)


@texture("Props/car_body_wreck", (128, 128), tile=True)
def car_body_wreck(ctx):
    W, H = ctx.W, ctx.H
    img = car_paint(ctx, H, W, base="#3a1a18", salt=1, rust=0.6, peel=0.4)
    holes = (fft_noise(ctx.sub(2), H, W, beta=1.8) > 2.0).astype(np.float32)
    rim = outer_rim(holes, 4, True)
    img = mix(img, "#2a1408", rim * 0.8)
    img = mix(img, "#030202", holes)
    img = dirt_pass(ctx, img, 0.4, 0.45, "#2a2014")
    return finish(ctx, img)


def headlight(img, cx, cy, rw, rh, lens="#8a8a7a", rim="#4a4a46"):
    H, W = img.shape[:2]
    img = mix(img, rim, ellipse_mask(W, H, cx, cy, rw * 1.15, rh * 1.15))
    img = mix(img, lens, ellipse_mask(W, H, cx, cy, rw, rh))
    img = mix(img, "#e0e0d0", ellipse_mask(W, H, cx - rw * 0.3, cy - rh * 0.3, rw * 0.35, rh * 0.35, soft=0.8) * 0.6)
    return img


def chrome(H, W, ctx, salt=0):
    r = ctx.sub(650 + salt)
    yy = (np.arange(H) + 0.5) / H
    band = 0.5 + 0.4 * np.sin(yy * 2 * np.pi * 3)[:, None] + 0.1 * fft_noise(r, H, W, beta=2.0)
    return gradient_map(clamp01(band), [(0, "#2a2a2a"), (0.6, "#8a8a86"), (1, "#c8c8c0")])


def plate(img, x0, y0, x1, y1, text, ctx):
    H, W = img.shape[:2]
    pm = rect(H, W, x0, y0, x1, y1)
    img = mix(img, "#b8b4a0", pm)
    img = mix(img, "#8a1a14", rect(H, W, x0, y0, x1, y0 + (y1 - y0) * 0.22))
    img, _ = put_text(img, [text], FONT_ROAD, 120, (x0 * W + 4, (y0 + (y1 - y0) * 0.3) * H, x1 * W - 4, (y1 - (y1 - y0) * 0.1) * H), "#141414", stretch=True)
    return img


@texture("Props/car_front", (128, 64))
def car_front(ctx):
    W, H = ctx.W, ctx.H
    img = car_paint(ctx, H, W, salt=2)
    # grille
    gm = rect(H, W, 0.28, 0.3, 0.72, 0.62)
    yy = (np.arange(H) + 0.5) / H
    bars = (np.sin(yy * 2 * np.pi * 18) > 0)[:, None].astype(np.float32)
    gr = mix(chrome(H, W, ctx), "#060606", bars * 0.85)
    img = mix(img, gr, gm)
    img = mix(img, "#a0a09a", outer_rim(gm, 3) * 0.7)
    for cx in (0.14, 0.86):
        img = mix(img, "#1a1a18", rect(H, W, cx - 0.12, 0.28, cx + 0.12, 0.64))
        img = headlight(img, cx * W, 0.46 * H, 0.09 * W, 0.15 * H)
    # bumper
    img = mix(img, chrome(H, W, ctx, 1), rect(H, W, 0.0, 0.72, 1.0, 0.86))
    img = mix(img, "#0a0a0a", rect(H, W, 0.0, 0.86, 1.0, 1.0))
    img = plate(img, 0.4, 0.68, 0.6, 0.86, "OMR 666", ctx)
    img = dirt_pass(ctx, img, 0.35, 0.4, "#2a2418")
    return finish(ctx, img, light=0.06)


@texture("Props/car_rear", (128, 64))
def car_rear(ctx):
    W, H = ctx.W, ctx.H
    img = car_paint(ctx, H, W, salt=3)
    img = mix(img, "#140606", rect(H, W, 0.05, 0.08, 0.95, 0.1))  # trunk lid seam
    img = mix(img, "#140606", rect(H, W, 0.05, 0.08, 0.06, 0.62) + rect(H, W, 0.94, 0.08, 0.95, 0.62))
    for cx in (0.13, 0.87):
        img = mix(img, "#1a0a08", rect(H, W, cx - 0.11, 0.3, cx + 0.11, 0.6))
        img = mix(img, "#7a0e0a", rect(H, W, cx - 0.1, 0.32, cx + 0.1, 0.5))
        img = mix(img, "#a89a80", rect(H, W, cx - 0.1, 0.5, cx + 0.1, 0.58))
        img = mix(img, "#d06050", rect(H, W, cx - 0.08, 0.34, cx - 0.02, 0.38) * 0.5)
    img = mix(img, chrome(H, W, ctx, 2), rect(H, W, 0.0, 0.7, 1.0, 0.84))
    img = mix(img, "#0a0a0a", rect(H, W, 0.0, 0.84, 1.0, 1.0))
    img = plate(img, 0.39, 0.38, 0.61, 0.62, "OMR 666", ctx)
    img = mix(img, "#a8a8a0", ellipse_mask(W, H, 0.5 * W, 0.24 * H, 0.02 * W, 0.04 * H) * 0.6)  # lock
    img = dirt_pass(ctx, img, 0.35, 0.4, "#2a2418")
    return finish(ctx, img, light=0.06)


@texture("Props/car_side", (256, 64))
def car_side(ctx):
    W, H = ctx.W, ctx.H
    img = car_paint(ctx, H, W, salt=4)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = xx / W, yy / H
    # greenhouse (windows) top band between pillars
    win = ((v > 0.06) & (v < 0.4) & (u > 0.24) & (u < 0.8)).astype(np.float32)
    slope_f = (v > 0.06 + (0.3 - u) * 2.0)  # A pillar slope
    slope_r = (v > 0.06 + (u - 0.74) * 2.0)
    win = win * slope_f * slope_r
    pillars = ((np.abs(u - 0.52) < 0.012)).astype(np.float32)
    r = ctx.sub(1)
    glass = gradient_map(clamp01(0.3 + 0.2 * fft_noise(r, H, W, beta=2.4) + 0.3 * smoothstep(0.3, 0.06, v)), [(0, "#06080a"), (1, "#3a4448")])
    refl = ((((u * 3 - v * 0.8) % 1.0) > 0.85) & ((((u * 3 - v * 0.8) % 1.0) < 0.9))).astype(np.float32)
    glass = mix(glass, "#6a7478", refl * 0.4)
    img = mix(img, "#101010", clamp01(blur(win, 3) * 1.6) * 0.8)
    img = mix(img, glass, win * (1 - pillars))
    img = mix(img, "#0e0e0e", pillars * win)
    # roof edge shading above windows, lower body
    img = img * (1.0 - 0.35 * smoothstep(0.06, 0.0, v))[..., None]
    # door seams and handles
    for sx in (0.27, 0.52, 0.77):
        img = mix(img, "#120606", ((np.abs(u - sx) < 0.003) & (v > 0.06) & (v < 0.85)).astype(np.float32))
    img = mix(img, "#120606", ((np.abs(v - 0.42) < 0.008) & (u > 0.2) & (u < 0.82)).astype(np.float32) * 0.6)
    for hx in (0.47, 0.72):
        img = mix(img, "#9a9a92", rect(H, W, hx - 0.025, 0.47, hx + 0.005, 0.52))
    # chrome trim line
    img = mix(img, chrome(H, W, ctx, 3), rect(H, W, 0.04, 0.6, 0.96, 0.64))
    # wheel arches (dark)
    for wx in (0.16, 0.84):
        arch = ((xx - wx * W) ** 2 / (0.1 * W) ** 2 + (yy - 1.0 * H) ** 2 / (0.48 * H) ** 2 < 1).astype(np.float32)
        img = mix(img, "#050505", arch)
    img = mix(img, "#080808", rect(H, W, 0.0, 0.92, 1.0, 1.0))
    img = dirt_pass(ctx, img, 0.35, 0.4, "#2a2418")
    mud = smoothstep(0.6, 1.0, v) * grime(ctx.sub(2), H, W, cover=0.5, beta=2.4, sharp=0.5)
    img = mix(img, "#2a2216", mud * 0.6)
    return finish(ctx, img, light=0.06)


@texture("Props/car_tire", (64, 64), k=8)
def car_tire(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    rad = radial(W, H)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    ang = np.arctan2(yy - H / 2, xx - W / 2)
    t = clamp01(0.4 + 0.1 * fft_noise(r, H, W, beta=2.0) + 0.04 * photo(ctx, "gravel_fine"))
    rubber = gradient_map(t, [(0, "#0a0a0a"), (1, "#2e2e2c")])
    tread = (np.sin(ang * 36) > 0.3).astype(np.float32) * (rad > 0.88) * (rad < 1.0)
    img = mix(rubber, "#030303", tread * 0.8)
    img = img * (1.0 + 0.25 * np.cos((rad - 0.75) * 9) * (rad < 0.9))[..., None]
    hub = (rad < 0.5).astype(np.float32)
    hubc = gradient_map(clamp01(0.55 + 0.25 * np.cos(ang * 5) * (rad > 0.2) + 0.1 * fft_noise(r, H, W, beta=2.0)), [(0, "#3a3834"), (1, "#8a8680")])
    hubc = mix(hubc, rust_color(ctx, H, W), rust_mask(ctx, H, W, cover=0.4) * 0.8)
    img = mix(img, hubc, hub)
    img = mix(img, "#141412", ((rad > 0.48) & (rad < 0.52)).astype(np.float32))
    for i in range(5):
        a = i / 5 * 2 * np.pi
        img = mix(img, "#1a1816", ellipse_mask(W, H, W / 2 + np.cos(a) * 0.3 * W / 2, H / 2 + np.sin(a) * 0.3 * H / 2, W * 0.03, W * 0.03))
    img = mix(img, "#0a0a0a", (rad > 1.0).astype(np.float32))
    img = dirt_pass(ctx, img, 0.35, 0.4, "#3a3022")
    return finish(ctx, img, light=0.04)


# --------------------------------------------------------------------------------------
# technical
# --------------------------------------------------------------------------------------


def brushed_metal(ctx, H, W, base="#7a7c7a", dark="#3a3c3c", light="#a0a29e", salt=0):
    r = ctx.sub(700 + salt)
    t = clamp01(0.5 + 0.1 * fft_noise(r, H, W, beta=2.6) + 0.05 * fft_noise(r, H, W, beta=1.0, ax=8, ay=1))
    return gradient_map(t, [(0, dark), (0.5, base), (1, light)])


@texture("Props/keypad", (32, 64), k=8, q=55)
def keypad(ctx):
    W, H = ctx.W, ctx.H
    img = brushed_metal(ctx, H, W, "#6a6c6a", "#2e302e", "#8a8c8a")
    m, sh = bevel(H, W, 0.0, 0.0, 1.0, 1.0, W * 0.05)
    img = apply_bevel(img, sh, 0.3)
    # LED + small display
    img = mix(img, "#0a0c0a", rect(H, W, 0.12, 0.05, 0.88, 0.17))
    img = mix(img, "#3a0806", rect(H, W, 0.14, 0.07, 0.86, 0.15))
    img = mix(img, "#ff2a1a", ellipse_mask(W, H, 0.78 * W, 0.11 * H, W * 0.05, W * 0.05))
    labels = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "0", "#"]
    f = font(FONT_ROAD, int(W * 0.22))
    im, d = canvas(W, H)
    shade = np.zeros((H, W), np.float32)
    for i, lb in enumerate(labels):
        cx = (0.2 + (i % 3) * 0.3)
        cy = (0.28 + (i // 3) * 0.18)
        bm, bsh = bevel(H, W, cx - 0.13, cy - 0.075, cx + 0.13, cy + 0.075, W * 0.025)
        img = mix(img, "#1a1a1a", rect(H, W, cx - 0.14, cy - 0.082, cx + 0.14, cy + 0.082))
        img = mix(img, "#4a4c4a", bm)
        shade += bsh
        bb = d.textbbox((0, 0), lb, font=f)
        d.text((cx * W - (bb[2] + bb[0]) / 2, cy * H - (bb[3] + bb[1]) / 2), lb, font=f, fill=255)
    img = apply_bevel(img, shade, 0.35)
    img = mix(img, "#d8d8d0", to_mask(im))
    img = dirt_pass(ctx, img, 0.3, 0.35)
    return finish(ctx, img, light=0.04, grain=0.02)


@texture("Props/fusebox", (64, 64), k=8, q=45)
def fusebox(ctx):
    W, H = ctx.W, ctx.H
    img = brushed_metal(ctx, H, W, "#6e706c", "#30322e", "#8e908a")
    m, sh = bevel(H, W, 0.0, 0.0, 1.0, 1.0, W * 0.04)
    img = apply_bevel(img, sh, 0.3)
    img = mix(img, "#1c1c1a", rect(H, W, 0.08, 0.1, 0.72, 0.9))
    img = mix(img, "#4a4c48", rect(H, W, 0.1, 0.12, 0.7, 0.88))
    slots = [(0.24, 0.3), (0.56, 0.3), (0.24, 0.56), (0.56, 0.56), (0.24, 0.8), (0.56, 0.8)]
    for i, (cx, cy) in enumerate(slots):
        img = mix(img, "#0c0c0a", ellipse_mask(W, H, cx * W, cy * H, W * 0.11, W * 0.11))
        if i != 3:  # empty slot
            img = mix(img, "#8a8a84", ellipse_mask(W, H, cx * W, cy * H, W * 0.085, W * 0.085))
            img = mix(img, "#c8a040", ellipse_mask(W, H, cx * W, cy * H, W * 0.04, W * 0.04))
            img = mix(img, "#d8d8d0", ellipse_mask(W, H, cx * W - W * 0.03, cy * H - W * 0.03, W * 0.02, W * 0.02, soft=0.8) * 0.6)
        else:
            img = mix(img, "#a01810", rect(H, W, cx - 0.12, cy - 0.17, cx + 0.12, cy - 0.12))
        img = mix(img, "#c8c4b0", rect(H, W, cx - 0.1, cy - 0.19 if i != 3 else cy + 0.12, cx + 0.1, cy - 0.15 if i != 3 else cy + 0.15) * 0.8)
    # main lever
    img = mix(img, "#1a1a18", rect(H, W, 0.78, 0.25, 0.9, 0.75))
    img = mix(img, "#8a1a12", rect(H, W, 0.8, 0.28, 0.88, 0.45))
    img = mix(img, "#c8c0a0", rect(H, W, 0.74, 0.8, 0.94, 0.94))
    img, _ = put_text(img, ["MAIN"], FONT_ROAD, 80, (0.755 * W, 0.82 * H, 0.925 * W, 0.92 * H), "#1a1410", stretch=True)
    img = dirt_pass(ctx, img, 0.35, 0.35)
    img = mix(img, rust_color(ctx, H, W), rust_mask(ctx, H, W, cover=0.08) * 0.8)
    return finish(ctx, img, light=0.04, grain=0.02)


@texture("Props/radio_set", (128, 64), q=45)
def radio_set(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.5 + 0.1 * fft_noise(r, H, W, beta=2.6))
    img = gradient_map(t, [(0, "#22281a"), (0.5, "#3e4a2e"), (1, "#56603e")])
    m, sh = bevel(H, W, 0.0, 0.0, 1.0, 1.0, W * 0.02)
    img = apply_bevel(img, sh, 0.3)
    # two analog meters
    for mx in (0.1, 0.3):
        img = mix(img, "#101010", rect(H, W, mx - 0.01, 0.1, mx + 0.17, 0.5))
        face = rect(H, W, mx, 0.13, mx + 0.16, 0.47)
        img = mix(img, "#c8bc94", face)
        im, d = canvas(W, H)
        cx, cy = (mx + 0.08) * W, 0.44 * H
        for k in range(9):
            a = -0.9 + k * 0.225
            d.line([(cx + np.sin(a) * 0.1 * W, cy - np.cos(a) * 0.1 * W), (cx + np.sin(a) * 0.12 * W, cy - np.cos(a) * 0.12 * W)], fill=255, width=4)
        a = r.uniform(-0.6, 0.6)
        d.line([(cx, cy), (cx + np.sin(a) * 0.13 * W, cy - np.cos(a) * 0.13 * W)], fill=200, width=5)
        img = mix(img, "#1a1410", to_mask(im) * face)
    # frequency display
    img = mix(img, "#080808", rect(H, W, 0.52, 0.12, 0.92, 0.36))
    img = mix(img, "#1a0e02", rect(H, W, 0.535, 0.15, 0.905, 0.33))
    img, _ = put_text(img, ["121.50 MHz"], FONT_PIXEL, 160, (0.55 * W, 0.16 * H, 0.89 * W, 0.32 * H), "#ffa020", stretch=True)
    img, _ = put_text(img, ["FREQ"], FONT_ROAD, 60, (0.52 * W, 0.38 * H, 0.62 * W, 0.45 * H), "#c8c8b0", stretch=True)
    # knobs row
    for kx in (0.12, 0.27, 0.42, 0.62, 0.8):
        img = knob(img, kx * W, 0.72 * H, W * 0.045, "#1e1e1c", "#050505")
        img = mix(img, "#c8c8b0", rect(H, W, kx - 0.003, 0.58, kx + 0.003, 0.62))
    # toggles + jack
    for tx in (0.9, 0.95):
        img = mix(img, "#b0b0a8", rect(H, W, tx - 0.008, 0.52, tx + 0.008, 0.62))
    img = mix(img, "#050505", ellipse_mask(W, H, 0.92 * W, 0.8 * H, W * 0.02, W * 0.02))
    for (x, y) in ((0.02, 0.05), (0.98, 0.05), (0.02, 0.95), (0.98, 0.95)):
        img = mix(img, "#7a7a70", ellipse_mask(W, H, x * W, y * H, W * 0.008, W * 0.008))
    img = dirt_pass(ctx, img, 0.3, 0.3)
    sc = scratches_mask(ctx.sub(2), H, W, 30, length=(10, 40), width=2, tile=False)
    img = mix(img, "#8a8a70", sc * 0.3)
    return finish(ctx, img, light=0.04, grain=0.02)


@texture("Props/generator", (64, 64), k=8, q=45)
def generator(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.5 + 0.12 * fft_noise(r, H, W, beta=2.6))
    img = gradient_map(t, [(0, "#1e2a1a"), (0.5, "#3a4e30"), (1, "#566a44")])
    m, sh = bevel(H, W, 0.0, 0.0, 1.0, 1.0, W * 0.04)
    img = apply_bevel(img, sh, 0.3)
    # louvre vents
    for i in range(7):
        y = 0.12 + i * 0.07
        lm, lsh = bevel(H, W, 0.1, y, 0.6, y + 0.045, W * 0.012)
        img = mix(img, "#0a0e08", rect(H, W, 0.1, y + 0.03, 0.6, y + 0.045))
        img = apply_bevel(img, lsh, 0.4)
    # data plate
    img = mix(img, "#8a8470", rect(H, W, 0.66, 0.12, 0.92, 0.3))
    img, _ = put_text(img, ["5 KW", "120V"], FONT_ROAD, 60, (0.68 * W, 0.14 * H, 0.9 * W, 0.28 * H), "#1a1a14", stretch=True, spacing=6)
    # fuel cap, exhaust
    img = knob(img, 0.79 * W, 0.5 * H, W * 0.07, "#2a2a28", "#050505")
    img = mix(img, "#151515", ellipse_mask(W, H, 0.3 * W, 0.78 * H, W * 0.12, W * 0.07))
    img = mix(img, "#050505", ellipse_mask(W, H, 0.3 * W, 0.78 * H, W * 0.07, W * 0.035))
    ch = chips(ctx, H, W, cover=0.1, sharp=0.03)
    img = mix(img, rust_color(ctx, H, W), ch)
    oil = drips(ctx.sub(2), H, W, 6, length=(0.2, 0.5), width=(4, 10), tile=False)
    img = mix(img, "#0a0a06", oil * 0.6)
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img, light=0.04)


@texture("Props/cage_bars", (64, 64), k=8, tile=True, alpha="hard")
def cage_bars(ctx):
    W, H = ctx.W, ctx.H
    n = 4
    xs = (np.arange(W) + 0.5) / W * n
    ys = (np.arange(H) + 0.5) / H * n
    dx = np.abs(xs - np.round(xs))[None, :]
    dy = np.abs(ys - np.round(ys))[:, None]
    bw = 0.075
    vbar = (dx < bw).astype(np.float32) * np.ones((H, 1))
    hbar = (dy < bw * 0.8).astype(np.float32) * np.ones((1, W))
    a = np.maximum(vbar, hbar)
    r = ctx.sub(1)
    cyl_v = np.cos(np.clip(dx / bw, 0, 1) * np.pi / 2)
    cyl_h = np.cos(np.clip(dy / (bw * 0.8), 0, 1) * np.pi / 2)
    shade = np.where(vbar > 0, 0.5 + 0.6 * cyl_v, 0.5 + 0.6 * cyl_h)
    weld = ((dx < bw * 1.4) & (dy < bw * 1.4)).astype(np.float32)
    base = gradient_map(clamp01(0.5 + 0.15 * fft_noise(r, H, W, beta=2.0)), [(0, "#2a2a28"), (1, "#5a5a56")])
    img = mix(base, rust_color(ctx, H, W), clamp01(rust_mask(ctx, H, W, cover=0.55) + weld * 0.6))
    img = img * shade[..., None]
    return img, np.maximum(a, weld)


@texture("Props/mannequin_burnt", (128, 128), tile=True)
def mannequin_burnt(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    F1, F2, _ = worley(r, H, W, 120)
    blister = smoothstep(0.0, 14.0, F1)
    t = clamp01(0.35 + 0.25 * fft_noise(r, H, W, beta=2.4) + 0.12 * photo(ctx, "gravel") + 0.15 * (1 - blister))
    img = gradient_map(t, [(0, "#060402"), (0.35, "#1e140c"), (0.6, "#4a3422"), (0.85, "#7a5a3e"), (1, "#a08060")])
    plastic = grime(ctx.sub(2), H, W, cover=0.42, beta=2.8, sharp=0.3)
    pl = gradient_map(clamp01(0.6 + 0.1 * photo(ctx, "moon")), [(0, "#8a7a62"), (1, "#c8b494")])
    img = mix(img, pl, plastic * 0.85)
    ash = grime(ctx.sub(3), H, W, cover=0.25, beta=1.8, sharp=0.3)
    img = mix(img, "#8a8680", ash * 0.45)
    soot = grime(ctx.sub(4), H, W, cover=0.35, beta=2.6, sharp=0.4)
    img = mix(img, "#050302", soot * 0.6)
    return finish(ctx, img, light=0.1)


@texture("Props/flamingo_pink", (32, 32), k=16, tile=True, desat=0.2)
def flamingo_pink(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.55 + 0.15 * fft_noise(r, H, W, beta=2.6) + 0.05 * photo(ctx, "gravel_fine"))
    img = gradient_map(t, [(0, "#7a3a4a"), (0.6, "#c06a80"), (1, "#d88a9a")])
    fade = grime(ctx.sub(2), H, W, cover=0.3, beta=2.4, sharp=0.5)
    img = mix(img, "#c0a0a0", fade * 0.3)
    img = dirt_pass(ctx, img, 0.3, 0.35, "#3a2a20")
    return finish(ctx, img, light=0.05)


@texture("Props/water_tower_tank", (128, 128), tile=True)
def water_tower_tank(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.5 + 0.12 * fft_noise(r, H, W, beta=2.6) + 0.04 * photo(ctx, "gravel_fine"))
    img = gradient_map(t, [(0, "#4a120a"), (0.5, "#8a2a18"), (1, "#a8442a")])
    g = grid(ctx, H, W, 2, 2, offset=0.5)
    seam = 1 - smoothstep(0, 4, g["edge"])
    xs = (np.arange(W) + 0.5)
    ys = (np.arange(H) + 0.5)
    # rivet rows along seams
    riv = np.zeros((H, W), np.float32)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    near = (g["edge"] > 6) & (g["edge"] < 14)
    dots = ((np.sin(xx / W * 2 * np.pi * 32) > 0.75) & (np.sin(yy / H * 2 * np.pi * 32) > 0.75))
    riv = (near & dots).astype(np.float32)
    img = img * (1 - 0.4 * seam)[..., None]
    img = mix(img, "#c06a4a", riv * 0.6)
    img = mix(img, "#2a0a04", np.roll(riv, (2, 2), (0, 1)) * (1 - riv) * 0.5)
    rm = rust_mask(ctx, H, W, cover=0.35, streaks=20)
    img = mix(img, rust_color(ctx, H, W), rm * 0.85)
    fade = grime(ctx.sub(2), H, W, cover=0.3, beta=2.8, sharp=0.6)
    img = mix(img, "#8a6a60", fade * 0.2)
    return finish(ctx, img, light=0.1)


@texture("Props/silo_metal", (128, 128), tile=True)
def silo_metal(ctx):
    W, H = ctx.W, ctx.H
    shade, _ = corrugation(W, H, 8, "x")
    r = ctx.sub(1)
    base = gradient_map(clamp01(0.5 + 0.15 * fft_noise(r, H, W, beta=2.6) + 0.05 * photo(ctx, "gravel_fine")),
                        [(0, "#4e5254"), (0.5, "#7e8284"), (1, "#9a9e9e")])
    yy = (np.arange(H) + 0.5) / H
    seam = (np.abs(((yy * 2) % 1.0) - 0.5) > 0.485).astype(np.float32)[:, None]
    base = base * (1 - 0.35 * seam)[..., None]
    rm = rust_mask(ctx, H, W, cover=0.12, streaks=28)
    img = mix(base, rust_color(ctx, H, W), rm * 0.9)
    img = img * shade[..., None]
    img = dirt_pass(ctx, img, 0.3, 0.35, "#2a2a22")
    return finish(ctx, img, light=0.1)


@texture("Props/utility_pole", (32, 128), k=8, tile=True)
def utility_pole(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    g = wood_grain(ctx, H, W, "y", rings=3)
    img = gradient_map(clamp01((g - 0.5) * 1.6 + 0.45), [(0, "#0e0a06"), (0.5, "#2e2216"), (1, "#4a3a28")])
    cr = clamp01(1 - np.abs(fft_noise(r, H, W, beta=2.2, ax=1, ay=10)) / 0.08)
    img = mix(img, "#050302", cr * 0.8)
    xs = (np.arange(W) + 0.5) / W
    img = img * (0.75 + 0.35 * np.sin(xs * np.pi))[None, :, None]
    img = dirt_pass(ctx, img, 0.3, 0.3, "#0a0806")
    return finish(ctx, img, light=0.05)


# --------------------------------------------------------------------------------------
# windows
# --------------------------------------------------------------------------------------


def window_frame(ctx, inner, frame_col="#b8b4a8", muntins=True, salt=0):
    W, H = ctx.W, ctx.H
    r = ctx.sub(820 + salt)
    fr = gradient_map(clamp01(0.55 + 0.12 * fft_noise(r, H, W, beta=2.4) + 0.05 * photo(ctx, "gravel_fine", H, W, salt=salt)),
                      [(0, "#4a463e"), (0.6, frame_col), (1, "#d0ccc0")])
    ch = chips(ctx, H, W, cover=0.12, sharp=0.04, salt=salt)
    fr = mix(fr, "#5a5248", ch)
    fm, d = frame_border(H, W, W * 0.12)
    if muntins:
        fm = np.maximum(fm, rect(H, W, 0.47, 0, 0.53, 1))
        fm = np.maximum(fm, rect(H, W, 0, 0.47, 1, 0.53))
    img = mix(inner, fr, fm)
    edge = outer_rim(fm, W * 0.01)
    img = mix(img, "#0a0806", edge * 0.6)
    img = dirt_pass(ctx, img, 0.35, 0.35, "#2a2418", salt)
    return img, fm


@texture("Props/window_red_glow", (64, 64), k=8, desat=0.0, dark=1.0, q=60)
def window_red_glow(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    xs = (np.arange(W) + 0.5) / W
    folds = 0.75 + 0.25 * np.sin(xs * 2 * np.pi * 7 + fft_noise(r, H, W, beta=3.0, ax=1, ay=4) * 1.5)
    glow = 1.0 - 0.35 * radial(W, H, r=W * 0.7)
    v = clamp01(folds * glow)
    inner = gradient_map(v, [(0, "#3a0404"), (0.5, "#a01208"), (1, "#e0301a")])
    img, fm = window_frame(ctx, inner, "#b0aca0")
    return finish(ctx, img, light=0.03, grain=0.02)


@texture("Props/window_dim_glow", (64, 64), k=8, desat=0.0, dark=1.0, q=60)
def window_dim_glow(ctx):
    """Drawn, dusty curtains lit dimly from behind: grey and bleak (replaces the red windows)."""
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    xs = (np.arange(W) + 0.5) / W
    folds = 0.75 + 0.25 * np.sin(xs * 2 * np.pi * 7 + fft_noise(r, H, W, beta=3.0, ax=1, ay=4) * 1.5)
    glow = 1.0 - 0.35 * radial(W, H, r=W * 0.7)
    v = clamp01(folds * glow)
    inner = gradient_map(v, [(0, "#16161a"), (0.5, "#5a5850"), (1, "#9c988a")])
    dirt = grime(ctx.sub(2), H, W, cover=0.35, beta=2.4, sharp=0.5)
    inner = mix(inner, "#3a362c", dirt * 0.3)
    img, fm = window_frame(ctx, inner, "#b0aca0")
    return finish(ctx, img, light=0.03, grain=0.02)


@texture("Props/window_dark", (64, 64), k=8)
def window_dark(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.25 + 0.15 * fft_noise(r, H, W, beta=2.4))
    inner = gradient_map(t, [(0, "#040506"), (1, "#28302e")])
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    refl = ((((xx / W) * 1.5 - (yy / H)) % 1.0) < 0.12).astype(np.float32)
    inner = mix(inner, "#5a6462", blur(refl, W * 0.02) * 0.25)
    dirt = grime(ctx.sub(2), H, W, cover=0.4, beta=2.4, sharp=0.5)
    inner = mix(inner, "#4a463a", dirt * 0.35)
    img, fm = window_frame(ctx, inner, "#bcb8ac", salt=1)
    return finish(ctx, img, light=0.04)


@texture("Props/window_boarded", (64, 64), k=8)
def window_boarded(ctx):
    W, H = ctx.W, ctx.H
    inner = solid(H, W, "#030303")
    img, fm = window_frame(ctx, inner, "#aaa69a", muntins=False, salt=2)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    r = ctx.sub(1)
    planks = [(0.18, 0.06, 0.18), (0.42, -0.04, 0.17), (0.68, 0.05, 0.18), (0.5, 0.55, 0.14)]
    for i, (cy, slope, th) in enumerate(planks):
        if i == 3:
            pm = (np.abs((yy / H - 0.5) - (xx / W - 0.5) * 0.9) < th / 2).astype(np.float32)
        else:
            pm = (np.abs(yy / H - (cy + (xx / W - 0.5) * slope)) < th / 2).astype(np.float32)
        wd = wood(ctx, H, W, "x", base="#6a5a46", dark="#2a2016", light="#8a7860", salt=20 + i)
        sh_m = np.roll(pm, (int(W * 0.03), int(W * 0.02)), (0, 1)) * (1 - pm)
        img = mix(img, "#050403", sh_m * 0.7)
        img = mix(img, wd, pm)
        for nx in (0.1, 0.9):
            ny = cy + (nx - 0.5) * slope if i != 3 else 0.5 + (nx - 0.5) * 0.9
            img = mix(img, "#141210", ellipse_mask(W, H, nx * W, ny * H, W * 0.015, W * 0.015) * pm)
    img = dirt_pass(ctx, img, 0.3, 0.35)
    return finish(ctx, img, light=0.05)


# --------------------------------------------------------------------------------------
# doors
# --------------------------------------------------------------------------------------


def panel_door(ctx, base, dark, light, salt=0, panels=((0.08, 0.06, 0.46, 0.4), (0.54, 0.06, 0.92, 0.4), (0.08, 0.48, 0.46, 0.93), (0.54, 0.48, 0.92, 0.93))):
    W, H = ctx.W, ctx.H
    img = wood(ctx, H, W, "y", base=base, dark=dark, light=light, rings=4, salt=salt, amt=1.0)
    shade = np.zeros((H, W), np.float32)
    m, sh = bevel(H, W, 0, 0, 1, 1, W * 0.03)
    shade += sh
    for (x0, y0, x1, y1) in panels:
        gm, gsh = bevel(H, W, x0, y0, x1, y1, W * 0.03, raised=False)
        pm, psh = bevel(H, W, x0 + 0.06, y0 + 0.03, x1 - 0.06, y1 - 0.03, W * 0.04, raised=True)
        shade += gsh * (1 - pm) + psh
        img = mix(img, dark, (gm - pm).clip(0, 1) * 0.45)
        img = mix(img, dark, inner_rim(gm, W * 0.006) * 0.8)
    img = apply_bevel(img, shade, 0.5)
    return img


@texture("Props/door_wood", (64, 128), k=8)
def door_wood(ctx):
    W, H = ctx.W, ctx.H
    img = panel_door(ctx, "#5a3a26", "#1e1008", "#76523a")
    img = mix(img, "#3a2a14", rect(H, W, 0.78, 0.49, 0.9, 0.58) * 0.8)
    img = knob(img, 0.84 * W, 0.53 * H, W * 0.05, "#9a7a3a")
    img = mix(img, "#3a3020", ellipse_mask(W, H, 0.84 * W, 0.53 * H, W * 0.16, H * 0.09, soft=0.9) * 0.3)
    img = dirt_pass(ctx, img, 0.3, 0.35)
    return finish(ctx, img, light=0.06)


@texture("Props/door_wood_dirty", (64, 128), k=8)
def door_wood_dirty(ctx):
    W, H = ctx.W, ctx.H
    img = panel_door(ctx, "#4e3222", "#160a04", "#664632", salt=1)
    img = mix(img, "#3a2a14", rect(H, W, 0.78, 0.49, 0.9, 0.58) * 0.8)
    img = knob(img, 0.84 * W, 0.53 * H, W * 0.05, "#7a5a2a")
    img = dirt_pass(ctx, img, 0.5, 0.55, "#140c06")
    dr = drips(ctx.sub(1), H, W, 10, length=(0.1, 0.4), width=(4, 10), tile=False)
    img = mix(img, "#3a0806", dr * 0.6)
    # bloody hand prints near the knob and lower
    r = ctx.sub(2)
    for (hx, hy, sc) in ((0.66, 0.48, 1.0), (0.3, 0.62, 0.9), (0.72, 0.7, 0.85)):
        m = np.zeros((H, W), np.float32)
        palm = ellipse_mask(W, H, hx * W, hy * H, W * 0.09 * sc, H * 0.045 * sc)
        m = np.maximum(m, palm)
        for k, (fx, fl) in enumerate(((-0.07, 0.07), (-0.025, 0.09), (0.02, 0.09), (0.065, 0.07))):
            fing = ((np.abs(np.mgrid[0:H, 0:W][1] - (hx + fx * sc) * W) < W * 0.018 * sc) &
                    (np.mgrid[0:H, 0:W][0] > (hy - fl * sc) * H) & (np.mgrid[0:H, 0:W][0] < hy * H)).astype(np.float32)
            m = np.maximum(m, fing)
        m = m * (1 - smoothstep(0.8, 1.3, fft_noise(r, H, W, beta=1.8)))
        img = mix(img, "#5a0806", m * 0.9)
    smear = drips(ctx.sub(3), H, W, 5, length=(0.05, 0.2), width=(6, 12), tile=False)
    img = mix(img, "#4a0606", smear * rect(H, W, 0.5, 0.4, 0.95, 0.9) * 0.8)
    return finish(ctx, img, light=0.06)


@texture("Props/door_front", (64, 128), k=8)
def door_front(ctx):
    W, H = ctx.W, ctx.H
    img = panel_door(ctx, "#3a4438", "#121610", "#56604e", salt=2,
                     panels=((0.08, 0.5, 0.46, 0.93), (0.54, 0.5, 0.92, 0.93)))
    # glass window upper part with 4 panes
    gm, gsh = bevel(H, W, 0.14, 0.06, 0.86, 0.42, W * 0.03, raised=False)
    r = ctx.sub(1)
    glass = gradient_map(clamp01(0.25 + 0.2 * fft_noise(r, H, W, beta=2.4)), [(0, "#050606"), (1, "#3a4240")])
    glass = mix(glass, "#6a7270", ellipse_mask(W, H, 0.35 * W, 0.15 * H, 0.15 * W, 0.04 * H, soft=0.9) * 0.3)
    img = mix(img, glass, gm)
    img = mix(img, "#3a4438", (rect(H, W, 0.48, 0.06, 0.52, 0.42) + rect(H, W, 0.14, 0.23, 0.86, 0.25)) * gm)
    img = apply_bevel(img, gsh, 0.3)
    ch = chips(ctx, H, W, cover=0.1, sharp=0.04)
    img = mix(img, wood(ctx, H, W, "y", "#5a4a36", "#2a2016", "#7a6a50", salt=5), ch * (1 - gm))
    img = mix(img, "#8a7a4a", rect(H, W, 0.8, 0.5, 0.88, 0.62) * 0.8)
    img = knob(img, 0.84 * W, 0.55 * H, W * 0.05, "#9a8040")
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img, light=0.06)


def metal_door(ctx, base, dark, light, salt=0):
    W, H = ctx.W, ctx.H
    r = ctx.sub(850 + salt)
    t = clamp01(0.5 + 0.12 * fft_noise(r, H, W, beta=2.6) + 0.05 * photo(ctx, "gravel_fine", H, W, salt=salt))
    img = gradient_map(t, [(0, dark), (0.5, base), (1, light)])
    fm, d = frame_border(H, W, W * 0.06)
    img = mix(img, dark, fm * 0.5)
    m, sh = bevel(H, W, 0.06, 0.03, 0.94, 0.97, W * 0.025)
    img = apply_bevel(img, sh, 0.3)
    return img


@texture("Props/door_metal_shelter", (64, 128), k=8)
def door_metal_shelter(ctx):
    W, H = ctx.W, ctx.H
    img = metal_door(ctx, "#7a7258", "#2e2a1e", "#9a9274")
    # fallout sign near the top
    sx0, sy0, sx1, sy1 = 0.3, 0.07, 0.7, 0.27
    sw = rect(H, W, sx0, sy0, sx1, sy1)
    img = mix(img, "#1a1206", rect(H, W, sx0 - 0.02, sy0 - 0.01, sx1 + 0.02, sy1 + 0.01))
    img = mix(img, "#d89a1c", sw)
    cx, cy, R = 0.5 * W, 0.155 * H, 0.15 * W
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    ang = np.arctan2(yy - cy, xx - cx)
    rad = np.hypot(xx - cx, yy - cy)
    blade = ((np.mod(ang + np.pi / 2 + np.pi / 6, 2 * np.pi / 3) < np.pi / 3) & (rad > R * 0.2) & (rad < R * 0.85)).astype(np.float32)
    img = mix(img, "#120c06", blade + ellipse_mask(W, H, cx, cy, R * 0.15, R * 0.15))
    img = mix(img, "#120c06", rect(H, W, sx0 + 0.02, 0.24, sx1 - 0.02, 0.265))
    # lever handle
    img = mix(img, "#1a1814", rect(H, W, 0.74, 0.5, 0.86, 0.56))
    img = mix(img, "#5a5a54", rect(H, W, 0.76, 0.51, 0.92, 0.545))
    img = knob(img, 0.8 * W, 0.53 * H, W * 0.04, "#6a6a62", "#1a1a18")
    # rivets along the edges
    for y in np.linspace(0.06, 0.94, 9):
        for x in (0.1, 0.9):
            img = mix(img, "#4a4434", ellipse_mask(W, H, x * W, y * H, W * 0.018, W * 0.018))
    rm = rust_mask(ctx, H, W, cover=0.25, streaks=14)
    img = mix(img, rust_color(ctx, H, W), rm * 0.8)
    # dark brown stain around the handle (like the reference basement door)
    img = mix(img, "#3a1e10", ellipse_mask(W, H, 0.5 * W, 0.62 * H, 0.3 * W, 0.16 * H, soft=0.9) * grime(ctx.sub(3), H, W, cover=0.5, beta=2.0, sharp=0.5) * 0.7)
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img, light=0.06)


@texture("Props/door_metal", (64, 128), k=8)
def door_metal(ctx):
    W, H = ctx.W, ctx.H
    img = metal_door(ctx, "#6a6e70", "#26282a", "#8a8e90", salt=1)
    img = mix(img, "#1a1a1a", rect(H, W, 0.74, 0.5, 0.86, 0.56))
    img = knob(img, 0.8 * W, 0.53 * H, W * 0.045, "#8a8a84", "#2a2a28")
    img = mix(img, "#2a2a2a", rect(H, W, 0.1, 0.85, 0.9, 0.95) * 0.4)  # kick plate
    dents = fft_noise(ctx.sub(2), H, W, beta=3.0)
    img = img * (1 + 0.06 * dents)[..., None]
    rm = rust_mask(ctx, H, W, cover=0.1, streaks=8, salt=1)
    img = mix(img, rust_color(ctx, H, W, 1), rm * 0.8)
    img = dirt_pass(ctx, img, 0.35, 0.35)
    return finish(ctx, img, light=0.06)


@texture("Props/door_planks", (64, 128), k=8)
def door_planks(ctx):
    W, H = ctx.W, ctx.H
    img, b = plank_surface(ctx, H, W, 5, "y", base="#7a7068", dark="#2a2420", light="#9a908a", joints=(0, 0), gap_px=W * 0.01, grain_amt=1.8)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = xx / W, yy / H
    bat = clamp01(rect(H, W, 0.0, 0.1, 1.0, 0.18) + rect(H, W, 0.0, 0.82, 1.0, 0.9))
    diag = ((np.abs((v - 0.18) - (0.82 - 0.18) * (1 - u)) < 0.045) & (v > 0.15) & (v < 0.85)).astype(np.float32)
    brace = clamp01(bat + diag)
    bw = wood(ctx, H, W, "x", base="#6a6058", dark="#221c18", light="#8a8078", salt=7)
    sh = np.roll(brace, (int(W * 0.02), int(W * 0.02)), (0, 1)) * (1 - brace)
    img = mix(img, "#0a0806", sh * 0.6)
    img = mix(img, bw, brace)
    for y in (0.14, 0.86):
        for x in np.linspace(0.1, 0.9, 5):
            img = mix(img, "#141210", ellipse_mask(W, H, x * W, y * H, W * 0.014, W * 0.014))
    img = mix(img, "#2a2826", rect(H, W, 0.82, 0.46, 0.95, 0.52))  # latch
    img = mix(img, rust_color(ctx, H, W), rect(H, W, 0.83, 0.47, 0.94, 0.51) * 0.8)
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img, light=0.06)


# --------------------------------------------------------------------------------------
# misc furniture / surfaces
# --------------------------------------------------------------------------------------


@texture("Props/stairs_wood", (64, 64), k=8, tile=True)
def stairs_wood(ctx):
    W, H = ctx.W, ctx.H
    img, b = plank_surface(ctx, H, W, 2, "x", base="#6a5440", dark="#24180e", light="#8a7058", joints=(0, 0), gap_px=W * 0.008, grain_amt=1.5)
    yy = (np.arange(H) + 0.5) / H
    worn = np.exp(-((np.arange(W) / W - 0.5) / 0.22) ** 2)[None, :] * np.ones((H, 1))
    img = mix(img, "#9a8a74", worn * 0.18)
    img = mix(img, "#120a04", ((yy > 0.94)[:, None] * np.ones((1, W))).astype(np.float32) * 0.7)
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img, light=0.06)


@texture("Props/bed_frame_metal", (64, 64), k=8, tile=True)
def bed_frame_metal(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.55 + 0.1 * fft_noise(r, H, W, beta=2.6))
    img = gradient_map(t, [(0, "#6a665a"), (1, "#b0aa98")])
    ch = chips(ctx, H, W, cover=0.3, sharp=0.04, beta=2.0)
    img = mix(img, rust_color(ctx, H, W), ch)
    dr = drips(ctx.sub(2), H, W, 8, length=(0.2, 0.6), width=(3, 8))
    img = mix(img, "#4a2410", dr * 0.5)
    img = dirt_pass(ctx, img, 0.3, 0.35)
    return finish(ctx, img, light=0.06)


@texture("Props/table_wood", (128, 128), tile=True)
def table_wood(ctx):
    W, H = ctx.W, ctx.H
    img, b = plank_surface(ctx, H, W, 3, "x", base="#5a4030", dark="#1e120a", light="#7a5a42", joints=(0, 0), gap_px=3, grain_amt=1.6)
    r = ctx.sub(1)
    sc = scratches_mask(r, H, W, 80, length=(8, 60), width=2)
    img = mix(img, "#a08060", sc * 0.4)
    cuts = scratches_mask(ctx.sub(2), H, W, 25, length=(10, 30), width=3)
    img = mix(img, "#140a04", cuts * 0.6)
    fill, ring = water_stains(ctx.sub(3), H, W, n=3, rmin=0.06, rmax=0.12)
    img = mix(img, "#1a1008", ring * 0.6)
    blood = grime(ctx.sub(4), H, W, cover=0.08, beta=2.2, sharp=0.15)
    img = mix(img, "#3a0806", blood * 0.75)
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img, light=0.08)


@texture("Props/shelf_metal", (64, 64), k=8, tile=True)
def shelf_metal(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.5 + 0.1 * fft_noise(r, H, W, beta=2.6))
    img = gradient_map(t, [(0, "#0e0e0e"), (1, "#2e2e2c")])
    g = grid(ctx, H, W, 4, 8, offset=0.5)
    hole = ((np.abs(g["fx"] - 0.5) < 0.18) & (np.abs(g["fy"] - 0.5) < 0.22)).astype(np.float32)
    img = mix(img, "#020202", hole)
    img = mix(img, "#3a3a38", np.roll(hole, (2, 2), (0, 1)) * (1 - hole) * 0.6)
    sc = scratches_mask(ctx.sub(2), H, W, 40, length=(10, 60), width=3)
    img = mix(img, "#5a5a56", sc * 0.5)
    rm = rust_mask(ctx, H, W, cover=0.1)
    img = mix(img, rust_color(ctx, H, W), rm * 0.6)
    img = dirt_pass(ctx, img, 0.3, 0.3, "#3a3428")
    return finish(ctx, img, light=0.06)


@texture("Props/paper_note", (64, 64), k=8)
def paper_note(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    t = clamp01(0.6 + 0.08 * fft_noise(r, H, W, beta=2.6) + 0.04 * photo(ctx, "moon"))
    img = gradient_map(t, [(0, "#8a7a54"), (0.6, "#c4b48a"), (1, "#d4c6a0")])
    fm, d = frame_border(H, W, W * 0.2)
    torn = smoothstep(0.0, W * 0.2, d + fft_noise(r, H, W, beta=1.6) * W * 0.05)
    img = img * (0.55 + 0.45 * torn)[..., None]
    img = mix(img, "#6a5430", (1 - torn) * 0.4)
    fold = np.exp(-((np.arange(H) / H - 0.48) / 0.012) ** 2)[:, None]
    img = img * (1 - 0.18 * fold)[..., None]
    fill, ring = water_stains(ctx.sub(2), H, W, n=2, rmin=0.1, rmax=0.22, tile=False)
    img = mix(img, "#8a6a3a", fill * 0.3 + ring * 0.4)
    blood = grime(ctx.sub(3), H, W, cover=0.03, beta=2.0, sharp=0.15)
    img = mix(img, "#5a1008", blood * 0.6)
    return finish(ctx, img, light=0.05)


@texture("Props/hay_bale", (64, 64), k=8, tile=True)
def hay_bale(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    st = photo(ctx, "grass", salt=5, stretch=(1.0, 4.0))
    t = clamp01(0.5 + 0.2 * st + 0.1 * fft_noise(r, H, W, beta=2.4))
    img = gradient_map(t, [(0, "#2a200c"), (0.4, "#6a5428"), (0.75, "#9a804a"), (1, "#b49a62")])
    im, d = canvas(W, H)
    for _ in range(700):
        x, y = r.uniform(0, W), r.uniform(0, H)
        a = np.pi / 2 + r.normal(0, 0.35)
        L = r.uniform(30, 90)
        v = int(255 * r.uniform(0.3, 1.0))
        for ox, oy in wrap_offsets(W, H, True):
            d.line([(x + ox, y + oy), (x + ox + np.cos(a) * L, y + oy + np.sin(a) * L)], fill=v, width=int(r.uniform(2, 5)))
    m = to_mask(im)
    straw = gradient_map(m, [(0, "#3a2c12"), (0.5, "#8a7040"), (1, "#c2a86e")])
    img = mix(img, straw, (m > 0).astype(np.float32) * 0.75)
    # twine
    xs = (np.arange(W) + 0.5) / W
    for tx in (0.3, 0.7):
        tw = (np.abs(xs - tx) < 0.02)[None, :] * np.ones((H, 1))
        img = mix(img, "#2a2418", tw.astype(np.float32) * 0.85)
    img = dirt_pass(ctx, img, 0.3, 0.35, "#1e180c")
    return finish(ctx, img, light=0.06)


@texture("Props/meat_slab", (64, 64), k=8)
def meat_slab(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    table = wood(ctx, H, W, "x", base="#4a3424", dark="#1a0e06", light="#6a4c34", salt=3)
    pool = splat_mask(r, W, H, W * 0.5, H * 0.55, W * 0.38, drops=12, irregular=0.3)
    table = mix(table, "#3a0604", pool * 0.85)
    meatm = splat_mask(ctx.sub(2), W, H, W * 0.5, H * 0.5, W * 0.28, drops=0, irregular=0.25)
    t = clamp01(0.5 + 0.2 * fft_noise(r, H, W, beta=2.0, ax=1, ay=2) + 0.1 * photo(ctx, "gravel_fine"))
    meat = gradient_map(t, [(0, "#2a0404"), (0.5, "#7a1612"), (1, "#a8342c")])
    fat = smoothstep(0.7, 1.3, fft_noise(ctx.sub(3), H, W, beta=2.2, ax=1, ay=3))
    meat = mix(meat, "#d0b0a0", fat * 0.75)
    e = edge_distance(meatm)
    meat = meat * (0.6 + 0.4 * smoothstep(0, W * 0.06, e))[..., None]
    img = mix(table, meat, meatm)
    bone = ellipse_mask(W, H, W * 0.62, H * 0.42, W * 0.06, W * 0.05)
    img = mix(img, "#d8ccb0", bone * meatm)
    img = mix(img, "#8a2a20", ellipse_mask(W, H, W * 0.62, H * 0.42, W * 0.03, W * 0.025) * meatm)
    return finish(ctx, img, light=0.05)


@texture("Props/book_spines", (128, 64))
def book_spines(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    img = solid(H, W, "#0a0806")
    cols = ["#4a1a14", "#1e3420", "#3a2a1a", "#1a2236", "#5a4a2a", "#2a1a1a", "#3e3a30", "#1a1a1a", "#5a2a1a"]
    x = 0.0
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    while x < W:
        bw = r.uniform(0.04, 0.1) * W
        top = r.uniform(0.08, 0.3) * H
        c = cols[int(r.integers(0, len(cols)))]
        m = ((xx >= x) & (xx < x + bw - 1) & (yy > top) & (yy < H * 0.97)).astype(np.float32)
        u = clamp01((xx - x) / bw)
        tone = clamp01(0.5 + 0.12 * fft_noise(r, H, W, beta=2.2, ax=1, ay=3))
        spine = gradient_map(tone, [(0, "#000000"), (0.5, c), (1, "#a09070")]) * (0.6 + 0.5 * np.sin(u * np.pi))[..., None]
        img = mix(img, spine, m)
        for by in (top + H * 0.08, H * 0.82):
            band = ((yy > by) & (yy < by + H * 0.025)).astype(np.float32) * m
            img = mix(img, "#a08a40", band * 0.7)
        lbl = ((yy > top + H * 0.2) & (yy < top + H * 0.34) & (xx > x + bw * 0.2) & (xx < x + bw * 0.75)).astype(np.float32)
        if r.random() < 0.6:
            img = mix(img, "#8a7a50", lbl * 0.6)
        x += bw
    img = mix(img, "#2a1e12", ((yy > H * 0.97)).astype(np.float32))  # shelf board
    img = dirt_pass(ctx, img, 0.3, 0.3, "#3a3228")
    dust = grime(ctx.sub(2), H, W, cover=0.3, beta=2.4, sharp=0.5)
    img = mix(img, "#6a645a", dust * 0.15)
    return finish(ctx, img, light=0.08)


@texture("Props/boards_nailed", (64, 128), k=8)
def boards_nailed(ctx):
    W, H = ctx.W, ctx.H
    img = solid(H, W, "#020202")
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    r = ctx.sub(1)
    planks = [(0.08, 0.04), (0.24, -0.06), (0.41, 0.03), (0.57, -0.04), (0.74, 0.06), (0.9, -0.02)]
    for i, (cy, slope) in enumerate(planks):
        th = r.uniform(0.09, 0.12)
        pm = (np.abs(yy / H - (cy + (xx / W - 0.5) * slope)) < th / 2).astype(np.float32)
        wd = wood(ctx, H, W, "x", base="#6e5e4a", dark="#2a2016", light="#8e7c62", salt=30 + i)
        wd = wd * (1 + (r.random() - 0.5) * 0.25)
        sh = np.roll(pm, (int(H * 0.012), int(W * 0.02)), (0, 1)) * (1 - pm)
        img = mix(img, "#000000", sh * 0.6)
        img = mix(img, wd, pm)
        img = mix(img, "#0e0a06", inner_rim(pm, W * 0.008) * 0.7)
        for nx in (0.08, 0.92):
            ny = cy + (nx - 0.5) * slope
            img = mix(img, "#181410", ellipse_mask(W, H, nx * W, ny * H, W * 0.02, W * 0.02) * pm)
    img = dirt_pass(ctx, img, 0.35, 0.4)
    return finish(ctx, img, light=0.06)


@texture("Props/porch_screen", (64, 64), k=8, tile=True, alpha="soft", q=50)
def porch_screen(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    n = 16
    xs = (np.arange(W) + 0.5) / W * n
    ys = (np.arange(H) + 0.5) / H * n
    wire = np.maximum((np.abs(xs - np.round(xs)) < 0.14)[None, :], (np.abs(ys - np.round(ys)) < 0.14)[:, None]).astype(np.float32)
    # the mesh itself is too fine for 64 px: average to a soft translucent veil + visible grid
    a = 0.35 + 0.35 * wire
    tears = (fft_noise(ctx.sub(2), H, W, beta=2.2) > 1.6).astype(np.float32)
    a = a * (1 - tears)
    a = a * (0.75 + 0.25 * grime(ctx.sub(3), H, W, cover=0.5, beta=2.4, sharp=0.6))
    base = gradient_map(clamp01(0.5 + 0.2 * fft_noise(r, H, W, beta=2.0)), [(0, "#1a1a18"), (1, "#4a4a46")])
    img = mix(base, rust_color(ctx, H, W), rust_mask(ctx, H, W, cover=0.5) * 0.8)
    return img, a
