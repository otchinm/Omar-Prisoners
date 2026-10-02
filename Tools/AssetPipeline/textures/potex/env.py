"""Textures/Env - tiling world surfaces (128x128 unless noted)."""
import numpy as np
from PIL import Image, ImageDraw

from .core import (blur, blur_xy, canvas, clamp01, col, cracks_mask, drips, fft_noise, gradient_map, grime_mask,
                   lerp, mix, mul, normz, scratches_mask, smoothstep, solid, texture, to_mask,
                   uneven_light, warp, water_stains, worley, wrap_offsets, saturate, lum, edge_distance)
from .mat import (grime, boards, chips, corrugation, grid, photo, plank_surface, rust_color, rust_mask, wood_grain)

T128 = (128, 128)


def finish(ctx, img, light=0.12, grain=0.035, grain_kind="gravel", salt=0):
    """photographic feel: uneven exposure + real photo grain"""
    H, W = ctx.H, ctx.W
    img = img * uneven_light(ctx.sub(90 + salt), H, W, light)[..., None]
    if grain:
        img = img * (1.0 + grain * photo(ctx, grain_kind, salt=77 + salt))[..., None]
    return clamp01(img)


def motif_canvas(ctx, positions, draw_fn):
    """draw a motif at each position with wrap-around copies; returns float mask (H,W)"""
    W, H = ctx.W, ctx.H
    im, d = canvas(W, H)
    for (x, y) in positions:
        for ox, oy in wrap_offsets(W, H, True):
            draw_fn(d, x + ox, y + oy)
    return to_mask(im)


def lattice(W, H, nx, ny, offset=False):
    pts = []
    sx, sy = W / nx, H / ny
    for j in range(ny):
        for i in range(nx):
            pts.append((sx * (i + 0.5), sy * (j + 0.5)))
            if offset:
                pts.append((sx * i, sy * j))
    return pts


def wallpaper_paper(ctx, base, salt=0):
    """paper ground: subtle fibres, yellowing, light changes"""
    H, W = ctx.H, ctx.W
    r = ctx.sub(10 + salt)
    img = solid(H, W, base)
    yel = clamp01(0.5 + 0.5 * fft_noise(r, H, W, beta=3.0))
    img = mix(img, "#a08a5a", yel * 0.12)
    fib = fft_noise(r, H, W, beta=1.2, ax=1, ay=6)
    img = img * (1.0 + 0.025 * fib)[..., None]
    # paper strip seams (vertical) at the tile edge, slightly lighter / darker pair
    xs = np.arange(W)
    seam = np.exp(-((xs - 2) / 1.5) ** 2) * 0.12 - np.exp(-((xs - 6) / 2.0) ** 2) * 0.05
    img = img * (1.0 - seam[None, :, None])
    return img


def brown_marks(ctx, n, salt=0, length=(20, 80), width=None):
    """short dark brown streak marks like the clock bedroom wallpaper"""
    W = ctx.W
    r = ctx.sub(20 + salt)
    m = scratches_mask(r, ctx.H, W, n, length=length, width=width or max(2, W // 128), tile=True)
    return blur(m, W / 512.0, True)


# ======================================================================================
# wallpapers
# ======================================================================================
@texture("Env/wall_wallpaper_blue", T128, tile=True)
def wall_wallpaper_blue(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img = wallpaper_paper(ctx, "#a9b4dc")

    def big(d, x, y):
        r = 15 * s
        for dx, dy in ((22, 0), (-22, 0), (0, 22), (0, -22)):
            d.rectangle([x + dx * s - r, y + dy * s - r, x + dx * s + r, y + dy * s + r], fill=255)
        rr = 26 * s
        d.polygon([(x, y - rr), (x + rr, y), (x, y + rr), (x - rr, y)], fill=255)

    def inner(d, x, y):
        rr = 13 * s
        d.polygon([(x, y - rr), (x + rr, y), (x, y + rr), (x - rr, y)], fill=255)
        for dx, dy in ((22, 0), (-22, 0), (0, 22), (0, -22)):
            q = 6 * s
            d.rectangle([x + dx * s - q, y + dy * s - q, x + dx * s + q, y + dy * s + q], fill=255)

    def small(d, x, y):
        rr = 9 * s
        d.polygon([(x, y - rr), (x + rr, y), (x, y + rr), (x - rr, y)], fill=255)

    main = lattice(W, H, 4, 4)
    off = [(x - W / 8, y - H / 8) for (x, y) in main]
    m_big = blur(motif_canvas(ctx, main, big), 1.2 * s, True)
    m_in = blur(motif_canvas(ctx, main, inner), 1.0 * s, True)
    m_sm = blur(motif_canvas(ctx, off, small), 1.0 * s, True)
    img = mix(img, "#56679f", m_big * 0.95)
    img = mix(img, "#7d8cc4", m_in * 0.9)
    img = mix(img, "#8593c6", m_sm * 0.8)
    # slight print misregistration: dark offset edge
    img = mix(img, "#3d4a7c", clamp01(np.roll(m_big, (2, 2), (0, 1)) - m_big) * 0.35)
    # dirt, brown marks, stains
    r = ctx.sub(1)
    dirt = grime(r, H, W, cover=0.3, beta=2.8, sharp=0.6)
    img = mix(img, "#5b4a3a", dirt * 0.2)
    marks = brown_marks(ctx, 30, length=(10 * s, 60 * s), width=int(7 * s))
    img = mix(img, "#4e2c1a", marks * 0.8)
    spots = grime(ctx.sub(7), H, W, cover=0.05, beta=1.6, sharp=0.15)
    img = mix(img, "#4a2a18", spots * 0.7)
    fill, ring = water_stains(r, H, W, n=2, rmin=0.12, rmax=0.25)
    img = mix(img, "#8a6a40", fill * 0.18)
    img = mix(img, "#5a3e22", ring * 0.45)
    dr = drips(r, H, W, 8, length=(0.2, 0.6), width=(1.5 * s, 4 * s))
    img = mix(img, "#4a3524", dr * 0.45)
    return finish(ctx, img, light=0.1, grain=0.04)


def rot_ellipse(cx, cy, rx, ry, ang, n=24):
    t = np.linspace(0, 2 * np.pi, n, endpoint=False)
    x, y = np.cos(t) * rx, np.sin(t) * ry
    ca, sa = np.cos(ang), np.sin(ang)
    return [(cx + x[i] * ca - y[i] * sa, cy + x[i] * sa + y[i] * ca) for i in range(n)]


def damask(d, x, y, s):
    """symmetric damask ornament (centre x,y; scale s = px per unit)"""
    def E(cx, cy, rx, ry, a):
        d.polygon(rot_ellipse(x + cx * s, y + cy * s, rx * s, ry * s, a), fill=255)
    E(0, 0, 13, 34, 0)
    E(0, -46, 7, 12, 0)
    E(0, 44, 6, 10, 0)
    for sg in (-1, 1):
        E(sg * 24, -14, 8, 26, sg * 0.6)
        E(sg * 30, 22, 7, 20, -sg * 0.7)
        E(sg * 14, -36, 5, 12, sg * 0.9)
        E(sg * 40, -2, 4, 9, sg * 0.2)
        E(sg * 20, 40, 5, 9, -sg * 1.0)


@texture("Env/wall_wallpaper_green", T128, tile=True)
def wall_wallpaper_green(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img = wallpaper_paper(ctx, "#7c8768", salt=1)
    pts = lattice(W, H, 2, 2, offset=True)
    m = motif_canvas(ctx, pts, lambda d, x, y: damask(d, x, y, s * 1.35))
    m = blur(m, 1.0 * s, True)
    img = mix(img, "#4e5a42", m * 0.9)
    # thin light outline offset (embossed print)
    img = mix(img, "#95a084", clamp01(np.roll(m, (-int(3 * s), -int(3 * s)), (0, 1)) - m) * 0.5)
    # vertical faint stripes between
    r = ctx.sub(1)
    # water damage from the top + tide rings
    fill, ring = water_stains(r, H, W, n=3, rmin=0.15, rmax=0.32)
    img = mix(img, "#6e5a36", fill * 0.25)
    img = mix(img, "#3e2e18", ring * 0.5)
    dr = drips(r, H, W, 12, length=(0.25, 0.8), width=(2 * s, 6 * s))
    img = mix(img, "#4b3b22", dr * 0.45)
    # peeling: plaster shows through, curled light rim + shadow
    peel = chips(ctx, H, W, cover=0.12, sharp=0.02, beta=2.6)
    rim = clamp01(blur(peel, 3 * s, True) - peel) * 2.5
    under = gradient_map(clamp01(0.5 + 0.25 * photo(ctx, "moon")), [(0, "#6a5032"), (0.5, "#9a7a52"), (1, "#b09068")])
    img = mix(img, "#c0bca2", clamp01(rim) * 0.5)
    img = img * (1.0 - 0.35 * clamp01(np.roll(peel, (int(3 * s), int(2 * s)), (0, 1)) - peel))[..., None]
    img = mix(img, under, peel)
    dirt = grime(r, H, W, cover=0.3, beta=2.6, sharp=0.6)
    img = mix(img, "#2f3022", dirt * 0.22)
    return finish(ctx, img, light=0.1, grain=0.04)


@texture("Env/wall_wallpaper_rose", T128, tile=True)
def wall_wallpaper_rose(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img = wallpaper_paper(ctx, "#b49cab", salt=2)
    xs = np.arange(W)
    stripes = (np.sin(2 * np.pi * xs / W * 8) > 0.85).astype(np.float32)
    img = mix(img, "#a28a9c", np.broadcast_to(blur(stripes, 2 * s)[None, :], (H, W)) * 0.4)

    def sprig(d, x, y):
        for a in range(5):
            ang = a / 5 * 2 * np.pi
            cx, cy = x + np.cos(ang) * 7 * s, y + np.sin(ang) * 7 * s
            d.ellipse([cx - 5 * s, cy - 5 * s, cx + 5 * s, cy + 5 * s], fill=255)
        d.line([(x, y + 8 * s), (x + 6 * s, y + 24 * s)], fill=180, width=int(3 * s))
        d.polygon(rot_ellipse(x + 10 * s, y + 18 * s, 7 * s, 3 * s, 0.6, 10), fill=180)

    pts = lattice(W, H, 4, 4, offset=True)
    m = blur(motif_canvas(ctx, pts, sprig), 1.2 * s, True)
    img = mix(img, "#7e6076", m * 0.8)
    # big dark grime blotches (like the desk room reference)
    r = ctx.sub(1)
    big = grime(r, H, W, cover=0.3, beta=3.0, sharp=0.18)
    tex = clamp01(0.5 + 0.3 * photo(ctx, "gravel", salt=4))
    img = mix(img, gradient_map(tex, [(0, "#2c2228"), (1, "#5e4b57")]), big * 0.9)
    edge = clamp01(blur(big, 4 * s, True) - big)
    img = mix(img, "#4e3e48", edge * 0.6)
    small = grime(ctx.sub(2), H, W, cover=0.25, beta=2.0, sharp=0.3)
    img = mix(img, "#5d4a55", small * 0.35)
    return finish(ctx, img, light=0.12, grain=0.04)


@texture("Env/wall_wallpaper_yellow", T128, tile=True)
def wall_wallpaper_yellow(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img = wallpaper_paper(ctx, "#b3a26a", salt=3)
    xs = (np.arange(W) + 0.5) / W
    # stripe pattern: wide + narrow pair, 4 repeats per tile
    p = (xs * 4) % 1.0
    st = np.zeros(W, np.float32)
    st[(p > 0.05) & (p < 0.30)] = 1.0
    st[(p > 0.36) & (p < 0.40)] = 0.7
    st[(p > 0.62) & (p < 0.64)] = 0.5
    st = blur(st, 1.5 * s)
    img = mix(img, "#857240", np.broadcast_to(st[None, :], (H, W)) * 0.95)
    r = ctx.sub(1)
    # nicotine: darker brown wash from the top plus streaks
    yy = (np.arange(H) + 0.5) / H
    wash = clamp01(0.5 + 0.5 * fft_noise(r, H, W, beta=3.0, ax=2, ay=0.5))
    img = mix(img, "#6e5626", wash * 0.2)
    dr = drips(r, H, W, 16, length=(0.2, 0.7), width=(1.5 * s, 6 * s))
    img = mix(img, "#5a4420", dr * 0.5)
    # mould band toward the bottom, periodic so the tile still wraps vertically
    prof = smoothstep(0.62, 0.93, yy) * (1.0 - smoothstep(0.965, 1.0, yy) * 0.6)
    mould_n = fft_noise(ctx.sub(2), H, W, beta=1.8) * 0.8 + 3.0 * (prof[:, None] - 0.62)
    mould = smoothstep(0.2, 0.8, mould_n)
    speck = (fft_noise(ctx.sub(3), H, W, beta=0.6) > 1.3).astype(np.float32) * smoothstep(0.1, 0.6, prof)[:, None]
    img = mix(img, "#2b2e1c", clamp01(mould * 0.8 + speck * 0.5))
    img = mix(img, "#4c4a28", blur(mould, 6 * s, True) * 0.3)
    dirt = grime(r, H, W, cover=0.25, beta=2.6, sharp=0.6)
    img = mix(img, "#3b321e", dirt * 0.2)
    return finish(ctx, img, light=0.12, grain=0.05)


# ======================================================================================
# painted wood / plaster / brick / tiles / blocks / siding
# ======================================================================================
@texture("Env/wall_wainscot", T128, tile=True)
def wall_wainscot(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    b = boards(ctx, H, W, 6, "y", joints=(0, 0), jitter=0.0)
    g = wood_grain(ctx, H, W, "y", rings=8)
    paint = solid(H, W, "#46525f") * (1.0 + 0.10 * (g - 0.5))[..., None]
    paint = paint * (1.0 + (b["rand"] - 0.5)[..., None] * 0.08)
    groove = 1.0 - smoothstep(0.0, 5 * s, b["edge"])
    bevel = smoothstep(4 * s, 9 * s, b["edge"]) * (1 - smoothstep(9 * s, 14 * s, b["edge"]))
    paint = paint * (1.0 - 0.6 * groove)[..., None] * (1.0 + 0.1 * bevel)[..., None]
    wood = gradient_map(clamp01(g), [(0, "#3a2a1c"), (0.6, "#6a5038"), (1, "#80654a")])
    ch = chips(ctx, H, W, cover=0.08, sharp=0.03, bias=-0.6 * smoothstep(0, 20 * s, b["edge"]))
    img = mix(paint, wood, ch)
    img = mix(img, "#7a8590", clamp01(blur(ch, 2 * s, True) - ch) * 0.5)
    r = ctx.sub(1)
    sc = scratches_mask(r, H, W, 30, length=(10 * s, 60 * s), angle=0.0, width=int(2 * s))
    img = mix(img, "#6c7682", sc * 0.4)
    dirt = grime(r, H, W, cover=0.35, beta=2.6, sharp=0.6)
    img = mix(img, "#1c1e20", dirt * 0.35)
    return finish(ctx, img, light=0.1, grain=0.05)


@texture("Env/wall_plaster_dirty", T128, tile=True)
def wall_plaster_dirty(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    rough = photo(ctx, "gravel", salt=1) * 0.6 + photo(ctx, "moon") * 0.4
    tone = clamp01(0.55 + 0.12 * fft_noise(r, H, W, beta=2.6) + 0.07 * rough)
    img = gradient_map(tone, [(0, "#3e3644"), (0.5, "#83788c"), (1, "#a69cab")])
    big = grime(r, H, W, cover=0.45, beta=3.0, sharp=0.5)
    img = mix(img, "#3a323e", big * 0.55)
    mottled = grime(ctx.sub(2), H, W, cover=0.35, beta=1.8, sharp=0.4)
    img = mix(img, "#4a4150", mottled * 0.35)
    dr = drips(r, H, W, 10, length=(0.3, 0.9), width=(W / 300, W / 90))
    img = mix(img, "#2c2630", dr * 0.4)
    cr = blur(cracks_mask(ctx.sub(3), H, W, n=2, seg=(W / 40, W / 14), width=max(1, W // 300), steps=8), 0.6, True)
    img = mix(img, "#2a2428", cr * 0.7)
    return finish(ctx, img, light=0.14, grain=0.06)


@texture("Env/wall_brick_basement", T128, tile=True)
def wall_brick_basement(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g = grid(ctx, H, W, 4, 12, offset=0.5, warp_px=2 * s)
    r = ctx.sub(1)
    ph = photo(ctx, "gravel", salt=2)
    mo = photo(ctx, "moon", salt=3)
    face = clamp01(0.62 + 0.14 * (g["rand"] - 0.5) + 0.08 * ph + 0.05 * mo + 0.07 * fft_noise(r, H, W, beta=2.4))
    wash = gradient_map(face, [(0, "#5a4e52"), (0.5, "#a69898"), (1, "#c2b6b2")])
    brick = gradient_map(clamp01(0.5 + 0.3 * (g["rand"] - 0.5) + 0.12 * ph), [(0, "#4a2e28"), (0.5, "#6e4438"), (1, "#8a5848")])
    flake = grime(ctx.sub(2), H, W, cover=0.16, beta=2.2, sharp=0.15)
    img = mix(wash, brick, flake * 0.85)
    e = g["edge"]
    bevel = smoothstep(1.5 * s, 9 * s, e)
    img = img * (0.78 + 0.22 * bevel)[..., None]
    top = 1 - smoothstep(0.0, 0.2, g["fy"])
    bot = smoothstep(0.8, 1.0, g["fy"])
    img = img * (1 + 0.07 * top - 0.14 * bot)[..., None]
    mortar = 1.0 - smoothstep(2.5 * s, 5.0 * s, e + 1.5 * s * ph)
    mort_col = gradient_map(clamp01(0.5 + 0.25 * ph), [(0, "#2e282a"), (1, "#625a5c")])
    img = mix(img, mort_col, mortar)
    dirt = grime(r, H, W, cover=0.35, beta=2.8, sharp=0.5)
    img = mix(img, "#2a2224", dirt * 0.4)
    dr = drips(ctx.sub(3), H, W, 8, length=(0.2, 0.6), width=(2 * s, 6 * s))
    img = mix(img, "#3a3030", dr * 0.35)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/wall_wood_planks", T128, tile=True)
def wall_wood_planks(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img, b = plank_surface(ctx, H, W, 5, "y", base="#86808c", dark="#2e2a32", light="#aaa4ae",
                           joints=(0, 1), gap_px=6 * s, grain_amt=1.9, var=0.14, rings=9)
    r = ctx.sub(1)
    streak = fft_noise(r, H, W, beta=1.4, ax=1, ay=20)
    img = img * (1.0 + 0.12 * streak)[..., None]
    knots = worley(r, H, W, 5)[0]
    km = smoothstep(9 * s, 3 * s, knots) * (b["edge"] > 10 * s)
    img = mix(img, "#2a2228", km * 0.7)
    dirt = grime(r, H, W, cover=0.35, beta=2.6, sharp=0.6, ay=3)
    img = mix(img, "#221e24", dirt * 0.4)
    # nails
    nail = np.zeros((H, W), np.float32)
    for yv in (0.12, 0.62):
        for cx in np.unique(b["board"]):
            xs = np.where(b["board"][0] == cx)[0]
            xm = xs.mean()
            for dx in (-0.25, 0.25):
                x = xm + dx * len(xs)
                y = yv * H
                yy, xx = np.ogrid[0:H, 0:W]
                nail = np.maximum(nail, (((xx - x) ** 2 + (yy - y) ** 2) < (3.5 * s) ** 2).astype(np.float32))
    img = mix(img, "#1a1414", nail * 0.85)
    return finish(ctx, img, light=0.14, grain=0.04)


@texture("Env/wall_wood_dark", T128, tile=True)
def wall_wood_dark(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img, b = plank_surface(ctx, H, W, 4, "y", base="#5a3826", dark="#24140c", light="#7a5034",
                           joints=(0, 0), gap_px=4 * s, grain_amt=1.6, var=0.08, rings=7)
    # panel V grooves + small bevel highlight
    bev = smoothstep(4 * s, 7 * s, b["edge"]) * (1 - smoothstep(7 * s, 10 * s, b["edge"]))
    img = img * (1.0 + 0.15 * bev)[..., None]
    r = ctx.sub(1)
    sheen = clamp01(0.5 + 0.5 * fft_noise(r, H, W, beta=3.0, ax=1, ay=3))
    img = img * (0.9 + 0.2 * sheen)[..., None]
    sc = scratches_mask(r, H, W, 25, length=(8 * s, 40 * s), width=int(2 * s))
    img = mix(img, "#8a6448", sc * 0.35)
    dirt = grime(r, H, W, cover=0.3, beta=2.6, sharp=0.6)
    img = mix(img, "#120a06", dirt * 0.35)
    return finish(ctx, img, light=0.1, grain=0.04)


def tile_wall(ctx, nx, ny, colors, grout_col, grout_w, warp_px, var=0.2, salt=0):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g = grid(ctx, H, W, nx, ny, warp_px=warp_px * s, salt=salt)
    r = ctx.sub(5 + salt)
    ph = photo(ctx, "gravel_fine", salt=salt + 3)
    t = clamp01(0.5 + var * (g["rand"] - 0.5) * 2 + 0.06 * ph + 0.08 * fft_noise(r, H, W, beta=2.4))
    img = gradient_map(t, colors)
    # glaze: subtle highlight on upper-left area of each tile, darker rim
    rim = 1.0 - smoothstep(grout_w * s, grout_w * s + 6 * s, g["edge"])
    img = img * (1.0 - 0.18 * rim)[..., None]
    hl = smoothstep(0.0, 0.45, g["fx"]) * smoothstep(0.0, 0.45, g["fy"]) * (1 - smoothstep(0.3, 0.6, g["fx"] * g["fy"] * 2))
    img = img * (1.0 + 0.06 * hl)[..., None]
    grout = 1.0 - smoothstep(grout_w * s * 0.6, grout_w * s * 1.2, g["edge"] + s * fft_noise(r, H, W, beta=1.0))
    return g, img, grout


@texture("Env/wall_tile_red", T128, tile=True)
def wall_tile_red(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g, img, grout = tile_wall(ctx, 8, 8, [(0, "#6a2a20"), (0.5, "#9a4637"), (1, "#b5604a")], "#cfc4b6", 4.0, 7.0, var=0.25)
    r = ctx.sub(1)
    gcol = gradient_map(clamp01(0.6 + 0.2 * fft_noise(r, H, W, beta=2.0)), [(0, "#8a8076"), (1, "#d6ccc0")])
    img = mix(img, gcol, grout)
    dirt = grime(r, H, W, cover=0.4, beta=2.8, sharp=0.5)
    img = mix(img, "#2e1612", dirt * 0.35)
    soot = grime(ctx.sub(2), H, W, cover=0.2, beta=2.2, sharp=0.3)
    img = mix(img, "#1a1210", soot * 0.4)
    return finish(ctx, img, light=0.12, grain=0.04)


@texture("Env/wall_tile_white_dirty", T128, tile=True)
def wall_tile_white_dirty(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g, img, grout = tile_wall(ctx, 8, 8, [(0, "#9c9a90"), (0.5, "#c4c2b6"), (1, "#d4d2c6")], "#5a564c", 3.5, 1.5, var=0.12, salt=1)
    r = ctx.sub(1)
    img = mix(img, "#4e4a40", grout * 0.9)
    yel = grime(r, H, W, cover=0.45, beta=2.6, sharp=0.6)
    img = mix(img, "#8a7a52", yel * 0.35)
    cr = cracks_mask(ctx.sub(2), H, W, n=3, seg=(W / 40, W / 16), width=max(1, int(2 * s)), steps=7)
    img = mix(img, "#3a3630", blur(cr, 0.5, True) * 0.8)
    # blood smears + drips
    sm = grime(ctx.sub(3), H, W, cover=0.08, beta=2.2, sharp=0.15, ax=0.4, ay=1.0)
    dr = drips(ctx.sub(4), H, W, 7, length=(0.15, 0.5), width=(2 * s, 5 * s))
    blood = clamp01(sm * 0.9 + dr * 0.85)
    img = mix(img, "#4a0c08", blood * 0.85)
    dirt = grime(ctx.sub(5), H, W, cover=0.3, beta=2.0, sharp=0.5)
    img = mix(img, "#3c3830", dirt * 0.4)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/wall_concrete_block", T128, tile=True)
def wall_concrete_block(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g = grid(ctx, H, W, 2, 4, offset=0.5, warp_px=1.5 * s)
    r = ctx.sub(1)
    ph = photo(ctx, "gravel", salt=1)
    t = clamp01(0.55 + 0.15 * ph + 0.1 * (g["rand"] - 0.5) + 0.1 * fft_noise(r, H, W, beta=2.4))
    img = gradient_map(t, [(0, "#4a4a48"), (0.5, "#878580"), (1, "#a8a6a0")])
    pores = (photo(ctx, "gravel_fine", salt=2) < -1.6).astype(np.float32)
    img = mix(img, "#3a3a38", pores * 0.5)
    mortar = 1.0 - smoothstep(3 * s, 7 * s, g["edge"])
    img = mix(img, "#6a6864", mortar * 0.8)
    img = img * (1.0 - 0.35 * (1.0 - smoothstep(0, 4 * s, g["edge"])))[..., None]
    dirt = grime(r, H, W, cover=0.35, beta=2.8, sharp=0.5)
    img = mix(img, "#2a2a26", dirt * 0.4)
    dr = drips(ctx.sub(2), H, W, 8, length=(0.2, 0.6), width=(2 * s, 6 * s))
    img = mix(img, "#3a3428", dr * 0.35)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/wall_siding_white", T128, tile=True)
def wall_siding_white(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    n = 8
    bh = H / n
    yy = (np.arange(H) + 0.5)
    fy = (yy % bh) / bh  # 0 top of board .. 1 bottom lip
    g = wood_grain(ctx, H, W, "x", rings=10)
    shade = 0.82 + 0.22 * fy  # clapboard gets lighter toward the lip
    lip = smoothstep(0.88, 0.97, fy)
    shadow = 1.0 - smoothstep(0.0, 0.10, fy)  # shadow under the previous board's lip
    paint = solid(H, W, "#c4c3bb") * (shade[:, None] * (1.0 + 0.07 * (g - 0.5)))[..., None]
    paint = paint * (1.0 - 0.55 * shadow[:, None, None]) * (1.0 + 0.08 * lip[:, None, None])
    wood = gradient_map(clamp01(g), [(0, "#3e3a34"), (0.6, "#77706a"), (1, "#8e877e")]) * (shade[:, None, None] * (1 - 0.5 * shadow[:, None, None]))
    r = ctx.sub(1)
    peel = chips(ctx, H, W, cover=0.12, sharp=0.03, beta=2.2,
                 bias=0.5 * fft_noise(r, H, W, beta=1.5, ax=0.4, ay=3))
    img = mix(paint, wood, peel)
    img = img * (1.0 - 0.3 * clamp01(np.roll(peel, int(2 * s), 0) - peel))[..., None]
    dr = drips(r, H, W, 18, length=(0.3, 0.9), width=(2 * s, 6 * s))
    img = mix(img, "#4a463a", dr * 0.45)
    mil = grime(ctx.sub(2), H, W, cover=0.35, beta=2.6, sharp=0.5)
    img = mix(img, "#4b4f3e", mil * 0.35)
    return finish(ctx, img, light=0.1, grain=0.03)


@texture("Env/wall_barn_red", T128, tile=True)
def wall_barn_red(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img, b = plank_surface(ctx, H, W, 6, "y", base="#7e2c22", dark="#3a120c", light="#94402e",
                           joints=(0, 1), gap_px=5 * s, grain_amt=1.2, var=0.12, rings=8)
    g = wood_grain(ctx, H, W, "y", rings=8, salt=3)
    bare = gradient_map(clamp01(g), [(0, "#2e2824"), (0.6, "#6a625a"), (1, "#8a8278")])
    bare = bare * (1.0 - 0.75 * (1.0 - smoothstep(0, 5 * s, b["edge"])))[..., None]
    r = ctx.sub(1)
    worn = chips(ctx, H, W, cover=0.3, sharp=0.2, beta=1.8, bias=0.8 * fft_noise(r, H, W, beta=1.4, ax=1, ay=10))
    img = mix(img, bare, worn * 0.9)
    dirt = grime(r, H, W, cover=0.35, beta=2.6, sharp=0.6)
    img = mix(img, "#1c0e0a", dirt * 0.4)
    return finish(ctx, img, light=0.12, grain=0.04)


@texture("Env/wall_corrugated_metal", T128, tile=True)
def wall_corrugated_metal(ctx):
    W, H = ctx.W, ctx.H
    shade, _ = corrugation(W, H, 10, "y")
    r = ctx.sub(1)
    base = gradient_map(clamp01(0.5 + 0.15 * fft_noise(r, H, W, beta=2.4) + 0.05 * photo(ctx, "gravel_fine")),
                        [(0, "#5a5e60"), (0.5, "#83888a"), (1, "#9ca0a0")])
    rm = rust_mask(ctx, H, W, cover=0.35, streaks=22)
    img = mix(base, rust_color(ctx, H, W), rm)
    img = img * shade[..., None]
    dirt = grime(ctx.sub(2), H, W, cover=0.3, beta=2.6, sharp=0.6)
    img = mix(img, "#2a2018", dirt * 0.35)
    return finish(ctx, img, light=0.1, grain=0.03)


# ======================================================================================
# floors / ceilings
# ======================================================================================
@texture("Env/floor_wood_planks", T128, tile=True)
def floor_wood_planks(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img, b = plank_surface(ctx, H, W, 6, "x", base="#7a6c5c", dark="#2e261e", light="#9a8c78",
                           joints=(1, 2), gap_px=5 * s, grain_amt=1.6, var=0.14, rings=7)
    r = ctx.sub(1)
    worn = clamp01(0.5 + 0.5 * fft_noise(r, H, W, beta=3.0, ax=3, ay=1))
    img = mix(img, "#a09484", worn * 0.18)
    sc = scratches_mask(r, H, W, 40, length=(10 * s, 50 * s), angle=0.0, width=int(2 * s))
    img = mix(img, "#a89a88", sc * 0.25)
    dirt = grime(ctx.sub(2), H, W, cover=0.4, beta=2.6, sharp=0.6, ax=2)
    img = mix(img, "#1e1812", dirt * 0.45)
    stain = grime(ctx.sub(3), H, W, cover=0.08, beta=2.6, sharp=0.15)
    img = mix(img, "#2a1c12", stain * 0.5)
    return finish(ctx, img, light=0.12, grain=0.04)


@texture("Env/floor_wood_dark", T128, tile=True)
def floor_wood_dark(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img, b = plank_surface(ctx, H, W, 5, "x", base="#4a3022", dark="#180e08", light="#6a4630",
                           joints=(1, 2), gap_px=4 * s, grain_amt=1.5, var=0.1, rings=6, salt=1)
    r = ctx.sub(1)
    sheen = clamp01(0.5 + 0.5 * fft_noise(r, H, W, beta=3.2, ax=2, ay=1))
    img = img * (0.88 + 0.24 * sheen)[..., None]
    sc = scratches_mask(r, H, W, 70, length=(6 * s, 40 * s), width=max(1, int(1.5 * s)))
    img = mix(img, "#8a6a50", sc * 0.4)
    dirt = grime(ctx.sub(2), H, W, cover=0.3, beta=2.6, sharp=0.6)
    img = mix(img, "#0c0806", dirt * 0.35)
    return finish(ctx, img, light=0.1, grain=0.03)


@texture("Env/floor_linoleum_dirty", T128, tile=True)
def floor_linoleum_dirty(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g = grid(ctx, H, W, 4, 4, warp_px=1.0 * s)
    chk = ((g["ix"] + g["iy"]) % 2).astype(np.float32)
    r = ctx.sub(1)
    ph = photo(ctx, "gravel_fine", salt=1)
    light = gradient_map(clamp01(0.5 + 0.1 * ph + 0.15 * (g["rand"] - 0.5)), [(0, "#a39a7e"), (1, "#c6bea2")])
    dark = gradient_map(clamp01(0.5 + 0.1 * ph + 0.15 * (g["rand"] - 0.5)), [(0, "#1e2420"), (1, "#3c443c")])
    img = mix(light, dark, chk)
    seam = 1.0 - smoothstep(0, 2.5 * s, g["edge"])
    img = img * (1.0 - 0.4 * seam)[..., None]
    # marbled flecks in the vinyl
    fl = (fft_noise(r, H, W, beta=1.0) > 1.4).astype(np.float32)
    img = mix(img, "#7a7464", fl * 0.25)
    yel = grime(ctx.sub(2), H, W, cover=0.5, beta=2.8, sharp=0.7)
    img = mix(img, "#6a5a32", yel * 0.35)
    st = grime(ctx.sub(3), H, W, cover=0.12, beta=2.6, sharp=0.15)
    img = mix(img, "#2a1e10", st * 0.55)
    fill, ring = water_stains(ctx.sub(4), H, W, n=2, rmin=0.1, rmax=0.2)
    img = mix(img, "#3a2a14", ring * 0.5)
    sc = scratches_mask(ctx.sub(5), H, W, 40, length=(10 * s, 40 * s), width=max(1, int(2 * s)))
    img = mix(img, "#4a4434", sc * 0.4)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/floor_concrete", T128, tile=True)
def floor_concrete(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    ph = photo(ctx, "gravel", salt=5) * 0.5 + photo(ctx, "moon", salt=1) * 0.5
    t = clamp01(0.55 + 0.14 * fft_noise(r, H, W, beta=2.4) + 0.08 * ph)
    img = gradient_map(t, [(0, "#4a4650"), (0.5, "#8e8a92"), (1, "#aaa6ac")])
    st = grime(ctx.sub(2), H, W, cover=0.35, beta=2.8, sharp=0.4)
    img = mix(img, "#3a3438", st * 0.5)
    oil = grime(ctx.sub(3), H, W, cover=0.07, beta=2.4, sharp=0.2)
    img = mix(img, "#1a1618", oil * 0.65)
    cr = cracks_mask(ctx.sub(4), H, W, n=3, seg=(W / 30, W / 12), width=max(1, int(2.5 * s)), steps=8)
    img = mix(img, "#221e22", blur(cr, 0.6, True) * 0.8)
    pits = (photo(ctx, "gravel_fine", salt=6) < -1.9).astype(np.float32)
    img = mix(img, "#3a363c", pits * 0.4)
    return finish(ctx, img, light=0.14, grain=0.03)


@texture("Env/floor_tile_dirty", T128, tile=True)
def floor_tile_dirty(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g, img, grout = tile_wall(ctx, 16, 16, [(0, "#7c7a70"), (0.5, "#a6a498"), (1, "#bab6a8")], "#3a362e", 4.0, 1.0, var=0.2, salt=3)
    r = ctx.sub(1)
    # a few darker / broken tiles
    dark_t = (g["rand"] > 0.93).astype(np.float32)
    img = mix(img, "#4a463c", dark_t * 0.6)
    img = mix(img, "#2c2820", grout * 0.95)
    dirt = grime(r, H, W, cover=0.5, beta=2.6, sharp=0.6)
    img = mix(img, "#3a3224", dirt * 0.5)
    st = grime(ctx.sub(2), H, W, cover=0.12, beta=2.4, sharp=0.2)
    img = mix(img, "#3a1a0e", st * 0.45)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/floor_carpet_stained", T128, tile=True)
def floor_carpet_stained(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    pile = photo(ctx, "grass", salt=2) * 0.6 + photo(ctx, "gravel_fine", salt=3) * 0.4
    t = clamp01(0.5 + 0.12 * pile + 0.12 * fft_noise(r, H, W, beta=2.6))
    img = gradient_map(t, [(0, "#2e140e"), (0.5, "#5e2c20"), (1, "#7a4430")])
    # faded medallion / border pattern
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    pat = np.sin(xx / W * 2 * np.pi * 4) * np.sin(yy / H * 2 * np.pi * 4)
    pm = smoothstep(0.55, 0.75, np.abs(pat)) * 0.6 + smoothstep(0.92, 0.98, np.abs(np.sin(xx / W * 2 * np.pi * 2))) * 0.4
    img = mix(img, "#7a5a36", blur(pm, 2 * s, True) * 0.35)
    worn = grime(ctx.sub(2), H, W, cover=0.3, beta=3.0, sharp=0.6)
    img = mix(img, "#7a6450", worn * 0.25)
    st = grime(ctx.sub(3), H, W, cover=0.18, beta=2.6, sharp=0.2)
    img = mix(img, "#1a0c08", st * 0.6)
    fill, ring = water_stains(ctx.sub(4), H, W, n=2, rmin=0.1, rmax=0.22)
    img = mix(img, "#2a1208", fill * 0.4)
    return finish(ctx, img, light=0.12, grain=0.05, grain_kind="grass")


@texture("Env/ceiling_plaster_stained", T128, tile=True)
def ceiling_plaster_stained(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    t = clamp01(0.62 + 0.05 * fft_noise(r, H, W, beta=2.6) + 0.05 * photo(ctx, "moon") + 0.03 * photo(ctx, "gravel_fine"))
    img = gradient_map(t, [(0, "#86806e"), (0.6, "#b2ac98"), (1, "#c2bca8")])
    fill, ring = water_stains(ctx.sub(2), H, W, n=2, rmin=0.2, rmax=0.36, ring_w=0.07)
    img = mix(img, "#a08454", fill * 0.45)
    img = mix(img, "#6a4a22", ring * 0.8)
    fill2, ring2 = water_stains(ctx.sub(3), H, W, n=3, rmin=0.06, rmax=0.14, ring_w=0.09)
    img = mix(img, "#8a6a3a", fill2 * 0.3)
    img = mix(img, "#5a3c18", ring2 * 0.6)
    cr = cracks_mask(ctx.sub(4), H, W, n=3, seg=(W / 40, W / 14), width=max(1, int(2 * s)), steps=7)
    img = mix(img, "#4a4232", blur(cr, 0.6, True) * 0.7)
    mold = grime(ctx.sub(5), H, W, cover=0.08, beta=1.8, sharp=0.3)
    img = mix(img, "#3a3a2a", mold * 0.45)
    dirt = grime(ctx.sub(6), H, W, cover=0.3, beta=3.0, sharp=0.7)
    img = mix(img, "#5a5444", dirt * 0.2)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/ceiling_wood", T128, tile=True)
def ceiling_wood(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img, b = plank_surface(ctx, H, W, 5, "x", base="#6a5a4a", dark="#241a12", light="#86735e",
                           joints=(0, 1), gap_px=6 * s, grain_amt=1.7, var=0.15, rings=6, salt=2)
    r = ctx.sub(1)
    dirt = grime(r, H, W, cover=0.4, beta=2.6, sharp=0.6)
    img = mix(img, "#1a140e", dirt * 0.45)
    fill, ring = water_stains(ctx.sub(2), H, W, n=2, rmin=0.12, rmax=0.25)
    img = mix(img, "#2a1e12", ring * 0.4)
    cob = grime(ctx.sub(3), H, W, cover=0.1, beta=1.6, sharp=0.4)
    img = mix(img, "#8a8678", cob * 0.15)
    return finish(ctx, img, light=0.14, grain=0.04)


# ======================================================================================
# ground
# ======================================================================================
@texture("Env/ground_dirt", T128, tile=True)
def ground_dirt(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    gr = photo(ctx, "gravel", salt=1)
    t = clamp01(0.5 + 0.18 * fft_noise(r, H, W, beta=2.4) + 0.1 * gr)
    img = gradient_map(t, [(0, "#1e1914"), (0.45, "#4a3e32"), (0.8, "#6a5a48"), (1, "#7e6e5a")])
    stones = smoothstep(0.8, 1.6, photo(ctx, "gravel", salt=2)) * smoothstep(0.0, 0.8, fft_noise(ctx.sub(2), H, W, beta=2.0))
    img = mix(img, "#8a8070", stones * 0.6)
    img = mix(img, "#141210", smoothstep(-0.8, -1.6, photo(ctx, "gravel", salt=2)) * 0.5)
    damp = grime(ctx.sub(3), H, W, cover=0.35, beta=3.0, sharp=0.6)
    img = mix(img, "#1c1712", damp * 0.45)
    grass = smoothstep(1.0, 1.8, photo(ctx, "grass", salt=3)) * grime(ctx.sub(4), H, W, cover=0.2, beta=2.6, sharp=0.4)
    img = mix(img, "#4a4a2a", grass * 0.5)
    return finish(ctx, img, light=0.14, grain=0.03)


@texture("Env/ground_grass_dead", T128, tile=True)
def ground_grass_dead(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    gp = photo(ctx, "grass", sigma=W / 10)
    t = clamp01(0.5 + 0.22 * gp + 0.12 * fft_noise(r, H, W, beta=2.6))
    hue = clamp01(0.5 + 0.5 * fft_noise(ctx.sub(2), H, W, beta=3.0))
    dry = gradient_map(t, [(0, "#1e1a10"), (0.45, "#5e5434"), (0.75, "#8a7c54"), (1, "#a6966c")])
    olive = gradient_map(t, [(0, "#181a0e"), (0.45, "#45482c"), (0.75, "#66663e"), (1, "#807c54")])
    img = mix(dry, olive, hue * 0.5)
    dirt = grime(ctx.sub(3), H, W, cover=0.22, beta=2.8, sharp=0.4)
    dcol = gradient_map(clamp01(0.5 + 0.2 * photo(ctx, "gravel")), [(0, "#2a2218"), (1, "#5e5040")])
    img = mix(img, dcol, dirt * 0.7)
    return finish(ctx, img, light=0.14, grain=0.03, grain_kind="grass")


@texture("Env/ground_gravel", T128, tile=True)
def ground_gravel(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    gp = photo(ctx, "gravel", sigma=W / 8)
    t = clamp01(0.5 + 0.24 * gp + 0.1 * fft_noise(r, H, W, beta=2.6))
    img = gradient_map(t, [(0, "#1e1c1a"), (0.4, "#5a554e"), (0.75, "#8a847a"), (1, "#a8a296")])
    tint = clamp01(0.5 + 0.6 * fft_noise(ctx.sub(2), H, W, beta=1.0))
    img = mix(img, "#7a6a58", tint * 0.25)
    dirt = grime(ctx.sub(3), H, W, cover=0.3, beta=2.8, sharp=0.5)
    img = mix(img, "#3a3026", dirt * 0.4)
    return finish(ctx, img, light=0.12, grain=0.02)


@texture("Env/ground_asphalt", T128, tile=True)
def ground_asphalt(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    agg = photo(ctx, "gravel_fine", sigma=W / 20)
    t = clamp01(0.5 + 0.16 * agg + 0.1 * fft_noise(r, H, W, beta=2.6))
    img = gradient_map(t, [(0, "#1e1e20"), (0.5, "#3e3e42"), (1, "#5e5e62")])
    light_ag = (agg > 1.4).astype(np.float32)
    img = mix(img, "#6a6a6a", light_ag * 0.35)
    oil = grime(ctx.sub(2), H, W, cover=0.12, beta=2.6, sharp=0.2)
    img = mix(img, "#121214", oil * 0.6)
    worn = grime(ctx.sub(3), H, W, cover=0.3, beta=3.0, sharp=0.6)
    img = mix(img, "#5a5a5a", worn * 0.18)
    return finish(ctx, img, light=0.1, grain=0.03)


@texture("Env/ground_asphalt_cracked", T128, tile=True)
def ground_asphalt_cracked(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    agg = photo(ctx, "gravel_fine", sigma=W / 20, salt=1)
    t = clamp01(0.5 + 0.14 * agg + 0.12 * fft_noise(r, H, W, beta=2.6))
    img = gradient_map(t, [(0, "#343436"), (0.5, "#58585a"), (1, "#747474")])
    F1, F2, _ = worley(ctx.sub(2), H, W, 26)
    edge = F2 - F1
    dx = fft_noise(ctx.sub(6), H, W, beta=2.2) * 6 * s
    dy = fft_noise(ctx.sub(7), H, W, beta=2.2) * 6 * s
    edge = warp(edge, dx, dy, tile=True)
    net = 1.0 - smoothstep(1.0 * s, 4.0 * s, edge)
    region = smoothstep(-0.6, 0.2, fft_noise(ctx.sub(3), H, W, beta=2.4))
    big = cracks_mask(ctx.sub(4), H, W, n=6, seg=(W / 24, W / 10), width=max(1, int(4 * s)), steps=10, branch=0.5)
    crack = clamp01(net * region + big)
    img = mix(img, "#141414", crack * 0.9)
    img = mix(img, "#828280", clamp01(blur(crack, 3 * s, True) - crack) * 0.2)
    patch = grime(ctx.sub(5), H, W, cover=0.15, beta=3.0, sharp=0.15)
    img = mix(img, "#2c2c2e", patch * 0.5)
    return finish(ctx, img, light=0.1, grain=0.03)


# ======================================================================================
# roofs / metals
# ======================================================================================
@texture("Env/roof_shingles", T128, tile=True)
def roof_shingles(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g = grid(ctx, H, W, 4, 8, offset=0.5, warp_px=2 * s)
    r = ctx.sub(1)
    gp = photo(ctx, "gravel_fine", sigma=W / 24)
    t = clamp01(0.5 + 0.18 * gp + 0.2 * (g["rand"] - 0.5))
    img = gradient_map(t, [(0, "#1a1c1a"), (0.5, "#3a3e3a"), (1, "#5a5e58")])
    # each tab: shadow at the top (under the row above), slight lighter bottom edge
    sh = 1.0 - smoothstep(0.0, 0.2, g["fy"])
    img = img * (1.0 - 0.55 * sh)[..., None]
    slot = 1.0 - smoothstep(0, 3 * s, np.minimum(g["fx"], 1 - g["fx"]) * W / 4)
    img = img * (1.0 - 0.7 * slot)[..., None]
    moss = grime(ctx.sub(2), H, W, cover=0.3, beta=2.4, sharp=0.4)
    mcol = gradient_map(clamp01(0.5 + 0.3 * photo(ctx, "grass", salt=1)), [(0, "#20240e"), (1, "#5a6a2a")])
    img = mix(img, mcol, moss * 0.65)
    dr = drips(ctx.sub(3), H, W, 10, length=(0.3, 0.8), width=(3 * s, 8 * s))
    img = mix(img, "#101210", dr * 0.35)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/roof_tin_rusty", T128, tile=True)
def roof_tin_rusty(ctx):
    W, H = ctx.W, ctx.H
    shade, _ = corrugation(W, H, 8, "y", sharp=1.2)
    r = ctx.sub(1)
    base = gradient_map(clamp01(0.5 + 0.15 * fft_noise(r, H, W, beta=2.4)), [(0, "#4a4c4c"), (1, "#7a7c7a")])
    rm = clamp01(rust_mask(ctx, H, W, cover=0.6, streaks=26, salt=1) + 0.2)
    img = mix(base, rust_color(ctx, H, W, salt=1), rm)
    holes = (fft_noise(ctx.sub(2), H, W, beta=1.6) > 2.7).astype(np.float32)
    img = mix(img, "#0c0806", holes * 0.9)
    img = img * shade[..., None]
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/metal_rusty", T128, tile=True)
def metal_rusty(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    img = rust_color(ctx, H, W, salt=2)
    steel = gradient_map(clamp01(0.5 + 0.2 * fft_noise(r, H, W, beta=2.0)), [(0, "#3a3a3a"), (1, "#6a6866")])
    sm = 1.0 - rust_mask(ctx, H, W, cover=0.7, salt=2)
    img = mix(img, steel, sm * 0.7)
    pits = (photo(ctx, "gravel_fine", salt=1) < -1.5).astype(np.float32)
    img = mix(img, "#1a0c06", pits * 0.6)
    flake = (photo(ctx, "gravel", salt=4) > 1.5).astype(np.float32)
    img = mix(img, "#b07040", flake * 0.35)
    return finish(ctx, img, light=0.12, grain=0.04)


@texture("Env/metal_painted_green", T128, tile=True)
def metal_painted_green(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    t = clamp01(0.5 + 0.12 * fft_noise(r, H, W, beta=2.6) + 0.04 * photo(ctx, "gravel_fine"))
    img = gradient_map(t, [(0, "#262a1c"), (0.5, "#454d34"), (1, "#5a6244")])
    ch = chips(ctx, H, W, cover=0.12, sharp=0.03, beta=2.0)
    img = mix(img, "#7a7c70", clamp01(blur(ch, 2 * s, True) * 1.5 - ch) * 0.3)
    img = mix(img, rust_color(ctx, H, W, salt=3), ch)
    dr = drips(r, H, W, 10, length=(0.1, 0.4), width=(2 * s, 5 * s))
    img = mix(img, "#4a2410", dr * 0.5)
    sc = scratches_mask(ctx.sub(2), H, W, 30, length=(10 * s, 40 * s), width=max(1, int(2 * s)))
    img = mix(img, "#6a6c60", sc * 0.4)
    dirt = grime(ctx.sub(3), H, W, cover=0.3, beta=2.6, sharp=0.6)
    img = mix(img, "#141a10", dirt * 0.35)
    return finish(ctx, img, light=0.1, grain=0.03)


@texture("Env/metal_dark", T128, tile=True)
def metal_dark(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    brushed = fft_noise(r, H, W, beta=1.2, ax=12, ay=1)
    t = clamp01(0.5 + 0.1 * fft_noise(r, H, W, beta=2.8) + 0.05 * brushed)
    img = gradient_map(t, [(0, "#1a1c1e"), (0.5, "#34373c"), (1, "#4a4e54")])
    sc = scratches_mask(ctx.sub(2), H, W, 50, length=(8 * s, 50 * s), width=max(1, int(1.5 * s)))
    img = mix(img, "#6a6e74", sc * 0.45)
    rm = rust_mask(ctx, H, W, cover=0.08, salt=4)
    img = mix(img, rust_color(ctx, H, W, salt=4) * 0.7, rm * 0.6)
    dirt = grime(ctx.sub(3), H, W, cover=0.3, beta=2.6, sharp=0.6)
    img = mix(img, "#0a0a0a", dirt * 0.35)
    return finish(ctx, img, light=0.1, grain=0.03)


@texture("Env/metal_galvanized", T128, tile=True)
def metal_galvanized(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    F1, F2, ids = worley(r, H, W, 90)
    tab = r.random(91).astype(np.float32)
    spangle = tab[ids] - 0.5
    t = clamp01(0.55 + 0.12 * spangle + 0.12 * fft_noise(r, H, W, beta=2.6))
    img = gradient_map(t, [(0, "#4e5254"), (0.5, "#80858a"), (1, "#9ca0a4")])
    wh = grime(ctx.sub(2), H, W, cover=0.3, beta=2.4, sharp=0.5)
    img = mix(img, "#a8aaa6", wh * 0.25)
    dr = drips(ctx.sub(3), H, W, 12, length=(0.2, 0.7), width=(1.5, 5.0))
    img = mix(img, "#5a3a22", dr * 0.4)
    dirt = grime(ctx.sub(4), H, W, cover=0.3, beta=2.6, sharp=0.6)
    img = mix(img, "#2a2a28", dirt * 0.35)
    return finish(ctx, img, light=0.1, grain=0.03)


# ======================================================================================
# wood
# ======================================================================================
@texture("Env/wood_raw", T128, tile=True)
def wood_raw(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g = wood_grain(ctx, H, W, "y", rings=5, salt=4)
    img = gradient_map(clamp01((g - 0.5) * 1.6 + 0.5), [(0, "#5a3e22"), (0.5, "#a07c50"), (1, "#bc9a6a")])
    r = ctx.sub(1)
    F1, _, _ = worley(r, H, W, 3)
    k = smoothstep(14 * s, 4 * s, F1)
    img = mix(img, "#4a2e16", k * 0.75)
    dirt = grime(ctx.sub(2), H, W, cover=0.35, beta=2.6, sharp=0.6, ay=4)
    img = mix(img, "#3a2a1a", dirt * 0.4)
    return finish(ctx, img, light=0.1, grain=0.03)


@texture("Env/wood_furniture", T128, tile=True)
def wood_furniture(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g = wood_grain(ctx, H, W, "y", rings=6, salt=5)
    img = gradient_map(clamp01((g - 0.5) * 1.8 + 0.5), [(0, "#1c0e06"), (0.5, "#4e2c18"), (1, "#6e4428")])
    r = ctx.sub(1)
    wear = grime(r, H, W, cover=0.18, beta=2.6, sharp=0.4)
    img = mix(img, "#8a6040", wear * 0.3)
    sc = scratches_mask(ctx.sub(2), H, W, 30, length=(8 * s, 40 * s), width=max(1, int(1.5 * s)))
    img = mix(img, "#9a7050", sc * 0.35)
    dirt = grime(ctx.sub(3), H, W, cover=0.3, beta=2.6, sharp=0.6)
    img = mix(img, "#0a0604", dirt * 0.35)
    return finish(ctx, img, light=0.1, grain=0.03)


@texture("Env/wood_painted_white", T128, tile=True)
def wood_painted_white(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    g = wood_grain(ctx, H, W, "y", rings=6, salt=6)
    paint = solid(H, W, "#c2bfb4") * (1.0 + 0.12 * (g - 0.5))[..., None]
    r = ctx.sub(1)
    cr = (fft_noise(r, H, W, beta=1.0, ax=1, ay=8) > 1.8).astype(np.float32)
    paint = mix(paint, "#8a867a", cr * 0.35)
    wood = gradient_map(clamp01(g), [(0, "#3a342c"), (0.6, "#6e665a"), (1, "#8a8070")])
    ch = chips(ctx, H, W, cover=0.09, sharp=0.03, beta=2.0, bias=0.6 * fft_noise(r, H, W, beta=1.5, ax=1, ay=6))
    img = mix(paint, wood, ch)
    img = img * (1.0 - 0.3 * clamp01(blur(ch, 2 * s, True) * 1.4 - ch))[..., None]
    dirt = grime(ctx.sub(2), H, W, cover=0.3, beta=2.6, sharp=0.6)
    img = mix(img, "#5a564a", dirt * 0.3)
    return finish(ctx, img, light=0.1, grain=0.03)


# ======================================================================================
# fabric / misc
# ======================================================================================
def weave(ctx, H, W, n, amt=0.08):
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    wv = np.sin(xx / W * 2 * np.pi * n) * np.sin(yy / H * 2 * np.pi * n)
    return 1.0 + amt * wv


@texture("Env/fabric_mattress_stained", T128, tile=True)
def fabric_mattress_stained(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    xs = (np.arange(W) + 0.5) / W
    p = (xs * 8) % 1.0
    st = ((p > 0.1) & (p < 0.22)).astype(np.float32) + 0.5 * ((p > 0.32) & (p < 0.36))
    st = np.broadcast_to(blur(st, 1.2 * s)[None, :], (H, W))
    img = solid(H, W, "#bbb39e")
    img = mix(img, "#56607a", st * 0.8)
    img = img * weave(ctx, H, W, 96, 0.06)[..., None]
    # quilting dimples
    g = grid(ctx, H, W, 4, 4, offset=0.5)
    dimple = 1.0 - smoothstep(0, 0.12, np.hypot(g["fx"] - 0.5, g["fy"] - 0.5))
    puff = 0.9 + 0.12 * smoothstep(0.0, 0.5, np.hypot(g["fx"] - 0.5, g["fy"] - 0.5))
    img = img * (1.0 - 0.4 * dimple)[..., None] / puff[..., None]
    r = ctx.sub(1)
    fill, ring = water_stains(r, H, W, n=4, rmin=0.12, rmax=0.3)
    img = mix(img, "#9a7a3a", fill * 0.5)
    img = mix(img, "#5a3e18", ring * 0.6)
    blood = grime(ctx.sub(2), H, W, cover=0.07, beta=2.4, sharp=0.15)
    img = mix(img, "#4a0e0a", blood * 0.8)
    dirt = grime(ctx.sub(3), H, W, cover=0.4, beta=2.6, sharp=0.6)
    img = mix(img, "#4a4030", dirt * 0.4)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/fabric_sofa", T128, tile=True)
def fabric_sofa(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    img = solid(H, W, "#5e4030") * (1.0 + 0.06 * photo(ctx, "grass", salt=1))[..., None]

    def flower(d, x, y):
        for a in range(6):
            ang = a / 6 * 2 * np.pi
            d.polygon(rot_ellipse(x + np.cos(ang) * 16 * s, y + np.sin(ang) * 16 * s, 13 * s, 7 * s, ang, 14), fill=255)
        d.ellipse([x - 7 * s, y - 7 * s, x + 7 * s, y + 7 * s], fill=120)

    def leaves(d, x, y):
        d.polygon(rot_ellipse(x + 30 * s, y + 18 * s, 18 * s, 6 * s, 0.5, 12), fill=255)
        d.polygon(rot_ellipse(x - 28 * s, y - 20 * s, 16 * s, 6 * s, 0.7, 12), fill=255)

    pts = lattice(W, H, 3, 3, offset=True)
    fm = blur(motif_canvas(ctx, pts, flower), 1.5 * s, True)
    lm = blur(motif_canvas(ctx, pts, leaves), 1.5 * s, True)
    img = mix(img, "#3e4a2a", lm * 0.7)
    img = mix(img, "#9a6a40", fm * 0.75)
    img = img * weave(ctx, H, W, 128, 0.07)[..., None]
    worn = grime(r, H, W, cover=0.3, beta=2.8, sharp=0.5)
    img = mix(img, "#7a6a58", worn * 0.2)
    st = grime(ctx.sub(2), H, W, cover=0.2, beta=2.6, sharp=0.2)
    img = mix(img, "#1e120a", st * 0.5)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/fabric_dirty", T128, tile=True)
def fabric_dirty(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    folds = fft_noise(r, H, W, beta=2.8, ax=0.4, ay=3.0)
    t = clamp01(0.55 + 0.18 * folds + 0.05 * photo(ctx, "gravel_fine"))
    img = gradient_map(t, [(0, "#2e2a22"), (0.5, "#6e685a"), (1, "#8e8676")])
    img = img * weave(ctx, H, W, 128, 0.08)[..., None]
    st = grime(ctx.sub(2), H, W, cover=0.35, beta=2.6, sharp=0.4)
    img = mix(img, "#3a2e1e", st * 0.5)
    fill, ring = water_stains(ctx.sub(3), H, W, n=3, rmin=0.08, rmax=0.2)
    img = mix(img, "#4a3a20", ring * 0.4)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/concrete_rough", T128, tile=True)
def concrete_rough(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    ph = photo(ctx, "gravel", salt=7) * 0.7 + photo(ctx, "moon", salt=2) * 0.3
    t = clamp01(0.55 + 0.14 * fft_noise(r, H, W, beta=2.4) + 0.12 * ph)
    img = gradient_map(t, [(0, "#3e3c38"), (0.5, "#7e7a72"), (1, "#9e9a90")])
    # formwork board lines
    yy = (np.arange(H) + 0.5) / H
    fw = np.exp(-(((yy * 4) % 1.0 - 0.5) / 0.012) ** 2)
    img = img * (1.0 - 0.25 * fw[:, None, None])
    pits = (photo(ctx, "gravel_fine", salt=8) < -1.7).astype(np.float32)
    img = mix(img, "#2a2826", pits * 0.6)
    dr = drips(ctx.sub(2), H, W, 12, length=(0.2, 0.7), width=(2 * s, 6 * s))
    img = mix(img, "#3a3224", dr * 0.45)
    dirt = grime(ctx.sub(3), H, W, cover=0.35, beta=2.8, sharp=0.5)
    img = mix(img, "#262420", dirt * 0.4)
    return finish(ctx, img, light=0.12, grain=0.03)


@texture("Env/bark", T128, tile=True)
def bark(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    n1 = fft_noise(r, H, W, beta=2.4, ax=1, ay=6)
    n2 = fft_noise(r, H, W, beta=2.0, ax=1, ay=3)
    furrow = clamp01((1 - np.abs(n1) / 0.5)) ** 2 * 0.7 + clamp01((1 - np.abs(n2) / 0.35)) ** 2 * 0.5
    # horizontal breaks across the plates
    hb = clamp01(1 - np.abs(fft_noise(ctx.sub(2), H, W, beta=2.0, ax=4, ay=1)) / 0.15) ** 2
    hb = hb * (fft_noise(ctx.sub(3), H, W, beta=2.0) > 0.3)
    furrow = clamp01(furrow + hb * 0.6)
    ph = photo(ctx, "gravel", salt=9, stretch=(1.0, 3.0))
    t = clamp01(0.7 - 0.8 * furrow + 0.12 * ph + 0.08 * fft_noise(r, H, W, beta=1.6, ax=1, ay=6))
    img = gradient_map(t, [(0, "#0c0a08"), (0.35, "#2a2420"), (0.7, "#4a423c"), (1, "#625a52")])
    lich = grime(ctx.sub(4), H, W, cover=0.15, beta=2.0, sharp=0.3)
    img = mix(img, "#4e5440", lich * 0.4 * (1 - furrow))
    return finish(ctx, img, light=0.1, grain=0.03)


@texture("Env/meat", T128, tile=True)
def meat(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    ret = ctx.src.rgb("retina.jpg")
    crop = ret[300:620, 300:620]  # vessels on red tissue
    from .core import resize, make_tileable
    tissue = make_tileable(resize(crop, W, H, Image.BICUBIC), ctx.sub(2))
    tl = lum(tissue)
    tl = (tl - tl.mean()) / (tl.std() + 1e-6)
    fib = fft_noise(r, H, W, beta=1.2, ax=1, ay=4)
    t = clamp01(0.5 + 0.18 * tl + 0.1 * fib + 0.12 * fft_noise(r, H, W, beta=2.6))
    img = gradient_map(t, [(0, "#1e0404"), (0.4, "#5a0e0c"), (0.7, "#8a2420"), (1, "#a8443a")])
    fat = smoothstep(0.6, 1.4, fft_noise(ctx.sub(3), H, W, beta=2.2, ax=1, ay=2))
    img = mix(img, "#c8a090", fat * 0.7)
    wet = (fft_noise(ctx.sub(4), H, W, beta=1.6) > 1.8).astype(np.float32)
    img = mix(img, "#d07a6a", wet * 0.35)
    dark = grime(ctx.sub(5), H, W, cover=0.3, beta=2.4, sharp=0.4)
    img = mix(img, "#200404", dark * 0.5)
    return finish(ctx, img, light=0.1, grain=0.03)


@texture("Env/hay", T128, tile=True)
def hay(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    st = photo(ctx, "grass", salt=5, stretch=(4.0, 1.0))
    t = clamp01(0.45 + 0.15 * st + 0.1 * fft_noise(r, H, W, beta=2.4))
    img = gradient_map(t, [(0, "#20180a"), (0.4, "#5a4a26"), (0.75, "#8a7444"), (1, "#a8925c")])
    for layer, (n, vals) in enumerate(((900, (0.15, 0.45)), (1400, (0.55, 1.0)))):
        im, d = canvas(W, H)
        for _ in range(n):
            x, y = r.uniform(0, W), r.uniform(0, H)
            a = r.normal(0, 0.35) + (np.pi if r.random() < 0.5 else 0)
            L = r.uniform(20, 90) * s
            v = int(255 * r.uniform(*vals))
            for ox, oy in wrap_offsets(W, H, True):
                d.line([(x + ox, y + oy), (x + ox + np.cos(a) * L, y + oy + np.sin(a) * L * 0.6)], fill=v, width=max(1, int(r.uniform(1.5, 3.5) * s)))
        m = to_mask(im)
        cov = (m > 0).astype(np.float32)
        straw = gradient_map(m, [(0, "#2a200e"), (0.3, "#5a4a28"), (0.7, "#a08a54"), (1, "#c2aa70")])
        img = mix(img, straw, cov * (0.7 if layer == 0 else 0.85))
    dark = grime(ctx.sub(2), H, W, cover=0.25, beta=2.6, sharp=0.5)
    img = mix(img, "#2a2010", dark * 0.35)
    return finish(ctx, img, light=0.1, grain=0.03, grain_kind="grass")


@texture("Env/tarp_blue", T128, tile=True)
def tarp_blue(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    crinkle = fft_noise(r, H, W, beta=2.4)
    cr1 = clamp01(1 - np.abs(fft_noise(ctx.sub(2), H, W, beta=2.4)) / 0.2) ** 2
    cr2 = clamp01(1 - np.abs(fft_noise(ctx.sub(3), H, W, beta=2.8)) / 0.15) ** 2
    crease = clamp01(cr1 + cr2 * 0.8)
    shade = 0.82 + 0.14 * crinkle
    img = solid(H, W, "#2a4a74") * shade[..., None]
    img = img * (1.0 - 0.45 * crease)[..., None]
    img = mix(img, "#7090b0", clamp01(np.roll(crease, (int(2 * s), int(2 * s)), (0, 1)) - crease) * 0.35)
    img = img * weave(ctx, H, W, 64, 0.05)[..., None]
    dirt = grime(ctx.sub(4), H, W, cover=0.35, beta=2.6, sharp=0.5)
    img = mix(img, "#2a2a22", dirt * 0.5)
    fade = grime(ctx.sub(5), H, W, cover=0.25, beta=3.0, sharp=0.6)
    img = mix(img, "#6a7c90", fade * 0.2)
    return finish(ctx, img, light=0.1, grain=0.03)


# ======================================================================================
# cutouts
# ======================================================================================
@texture("Env/chainlink", (64, 64), tile=True, alpha="hard", k=8, dark=1.0, desat=0.05)
def chainlink(ctx):
    """galvanised diamond mesh: 4 x 4 diamonds per tile (1 m)"""
    W, H = ctx.W, ctx.H
    n = 4
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32) + 0.5
    u = (xx / W * n + yy / H * n)
    v = (xx / W * n - yy / H * n)
    du = np.abs(u - np.round(u))
    dv = np.abs(v - np.round(v))
    # slight wire wobble so it is not a perfect vector grid
    r = ctx.sub(1)
    wob = fft_noise(r, H, W, beta=2.6) * 0.012
    du = np.abs(du + wob)
    dv = np.abs(dv - wob)
    wire_w = 0.078
    wire = np.maximum(1 - smoothstep(wire_w * 0.7, wire_w, du), 1 - smoothstep(wire_w * 0.7, wire_w, dv))
    knot = (1 - smoothstep(0.0, 0.14, np.hypot(du, dv)))
    t = clamp01(0.5 + 0.15 * fft_noise(r, H, W, beta=2.0) + 0.3 * knot + 0.25 * np.cos(np.minimum(du, dv) / wire_w * np.pi / 2))
    img = gradient_map(t, [(0, "#34363a"), (0.5, "#6e7276"), (1, "#9a9ea0")])
    rm = rust_mask(ctx, H, W, cover=0.2, salt=5)
    img = mix(img, rust_color(ctx, H, W, salt=5), rm * 0.7)
    return img, wire


@texture("Env/barbed_wire", (128, 32), tile=True, alpha="hard", k=8, dark=1.0, desat=0.05)
def barbed_wire(ctx):
    W, H = ctx.W, ctx.H
    s = W / 1024.0
    im, d = canvas(W, H)
    hl_im, hd = canvas(W, H)
    cy = H / 2
    xs = np.arange(0, W + 1, 2)
    tw = 10  # twists per tile
    for ph in (0.0, np.pi):
        ys = cy + np.sin(xs / W * 2 * np.pi * tw + ph) * 9 * s
        pts = list(zip(xs, ys))
        for ox in (-W, 0, W):
            d.line([(x + ox, y) for x, y in pts], fill=255, width=int(15 * s))
        # front-facing half of each twist gets a highlight
        front = np.cos(xs / W * 2 * np.pi * tw + ph) > 0
        for i in range(len(xs) - 1):
            if front[i]:
                for ox in (-W, 0, W):
                    hd.line([(xs[i] + ox, ys[i] - 3 * s), (xs[i + 1] + ox, ys[i + 1] - 3 * s)], fill=255, width=int(5 * s))
    for bx in (W * 0.125, W * 0.625):
        for ang in (1.05, -2.1, 2.1, -1.05):
            L = 100 * s
            x2, y2 = bx + np.cos(ang) * L, cy + np.sin(ang) * L
            d.line([(bx, cy), (x2, y2)], fill=255, width=int(12 * s))
        d.ellipse([bx - 22 * s, cy - 24 * s, bx + 22 * s, cy + 24 * s], fill=255)
    a = to_mask(im)
    hl = to_mask(hl_im)
    r = ctx.sub(1)
    t = clamp01(0.4 + 0.15 * fft_noise(r, H, W, beta=1.8) + 0.4 * hl)
    img = gradient_map(t, [(0, "#1e1e1e"), (0.5, "#545452"), (1, "#8a8a86")])
    img = mix(img, rust_color(ctx, H, W, salt=6), rust_mask(ctx, H, W, cover=0.45, salt=6) * 0.75)
    return img, a