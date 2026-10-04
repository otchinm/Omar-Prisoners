"""Textures/FX - flames, fire, smoke, sparks, blood, explosions, VHS noise sources, shadows."""
import numpy as np
from PIL import Image, ImageDraw

from .core import (blur, inner_rim, canvas, clamp01, ellipse_mask, fft_noise, gradient_map, mix, radial, rng_for, smoothstep,
                   splat_mask, texture, to_image_rgb, to_mask, warp)

FIRE_STOPS = [(0.0, "#000000"), (0.25, "#3a0602"), (0.45, "#a01e06"), (0.65, "#f06a10"), (0.82, "#ffc040"), (1.0, "#fff4d0")]
FXDEG = dict(jpeg=False, desat=0.0, dark=1.0, maxv=1.0, sharpen=0.2, bits=5, dither=0.5)


# --------------------------------------------------------------------------------------
# flames / fire (4-frame loops)
# --------------------------------------------------------------------------------------
# Lighter flame flipbook (Nun Massacre style): a handful of hand-shaped states the game switches between at a moderate,
# irregular pace. Native-resolution pixels, a five-tone warm palette and hard edges: chunky and readable, not cartoony
# (no outline, no blue base, the white-hot core fills most of the tongue like the VHS reference).
FLAME_FRAMES = 6
FLAME_PALETTE = [  # (rgb, alpha) from the outer fringe to the core
    ((160, 48, 14), 0.78),
    ((228, 104, 28), 1.0),
    ((255, 170, 56), 1.0),
    ((255, 224, 140), 1.0),
    ((255, 249, 230), 1.0),
]
#            tip  width  widest  lean   bow
FLAME_SHAPES = [
    (0.95, 0.60, 0.27, 0.00, 0.00),   # calm, tall
    (0.86, 0.66, 0.25, 0.17, 0.03),   # squats, tip drifts right
    (0.97, 0.55, 0.29, -0.10, -0.02),  # stretches thin, leans left
    (0.91, 0.61, 0.26, 0.22, -0.06),  # S-bend to the right
    (0.83, 0.68, 0.24, -0.04, 0.04),  # short and round (gulps air)
    (0.93, 0.58, 0.28, -0.19, 0.05),  # bends left
]


def lighter_flame_pixels(w, h, frame):
    tip, width, widest, lean, bow = FLAME_SHAPES[frame % len(FLAME_SHAPES)]
    k = 4  # coverage is measured on a 4x grid, then each texel takes one palette tone
    H, W = h * k, w * k
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    v = 1.0 - (yy + 0.5) / H
    u = (xx + 0.5) / W * 2 - 1
    vb = 0.5 / h  # one texel of breathing room under the base
    t = clamp01((v - vb) / (tip - vb))
    tp = (widest - vb) / (tip - vb)
    round_bottom = np.sqrt(clamp01(1.0 - ((tp - t) / tp) ** 2))
    s = clamp01((t - tp) / (1.0 - tp))
    taper = np.cos(np.pi * 0.5 * s) ** 1.15
    f = np.where(t < tp, round_bottom, taper) * (v >= vb) * (v <= tip)
    # a little ragged edge so the silhouette is not a perfect geometric drop (fixed per frame)
    r = rng_for("flame_px", frame)
    rag = fft_noise(r, H, W, beta=2.4, ax=1.0, ay=2.5) * 0.06
    centre = lean * t ** 2 + bow * np.sin(np.pi * t)
    d = np.abs(u - centre) / np.maximum(width * f, 1e-4) * (1.0 + rag)
    inside = (d < 1.0) & (f > 0.0)
    heat = (1.0 - d ** 1.35) * (1.0 - 0.55 * smoothstep(0.42, 1.0, t)) * (0.84 + 0.16 * smoothstep(0.0, 0.18, t))
    heat = np.where(inside, heat, 0.0)

    def down(a):
        return a.reshape(h, k, w, k).mean(axis=(1, 3))

    cov = down(inside.astype(np.float32))
    hm = down(heat) / np.maximum(cov, 1e-4)
    out = np.zeros((h, w, 4), np.uint8)
    bayer2 = ((0.0, 0.5), (0.75, 0.25))
    for y in range(h):
        for x in range(w):
            c = cov[y, x]
            if c < 0.3:
                continue
            e = hm[y, x] + (bayer2[y & 1][x & 1] - 0.375) * 0.1  # mottled, not flat bands
            if c < 0.62:
                lvl = 0
            elif e > 0.6:
                lvl = 4
            elif e > 0.4:
                lvl = 3
            elif e > 0.22:
                lvl = 2
            elif e > 0.08:
                lvl = 1
            else:
                lvl = 0
            rgb, a = FLAME_PALETTE[lvl]
            out[y, x] = (rgb[0], rgb[1], rgb[2], int(round(a * 255)))
    return Image.fromarray(out, "RGBA")


for _i in range(FLAME_FRAMES):
    def _mk(i):
        @texture("FX/flame_%d" % i, (16, 32), alpha="soft")
        def _f(ctx):
            return lighter_flame_pixels(ctx.w, ctx.h, i)
        return _f
    _mk(_i)


def big_fire(ctx, frame):
    W, H = ctx.W, ctx.H
    r = rng_for("fire_shared")
    n1 = fft_noise(r, H, W, beta=2.6, ax=1.0, ay=2.2, fmin=3.0)  # ay > 1: tongues elongated vertically
    n2 = fft_noise(r, H, W, beta=2.2, ax=1.0, ay=1.8, fmin=5.0)
    s1 = np.roll(n1, -int(frame * H / 4), axis=0)          # scrolls up, loops after 4 frames
    s2 = np.roll(n2, -int(frame * H / 2), axis=0)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    v = 1.0 - (yy + 0.5) / H
    u = (xx + 0.5) / W * 2 - 1
    wob = 0.12 * s2 * v
    body = clamp01(1.0 - np.abs(u + wob) / (0.95 - 0.6 * v))  # wider at the base
    heat = body * (1.35 - 1.15 * v) + 0.28 * s1 * (0.3 + 0.7 * v) + 0.1 * s2
    heat = clamp01(heat) * smoothstep(0.0, 0.08, v)
    t = clamp01((heat - 0.25) / 0.75)
    img = gradient_map(t, FIRE_STOPS)
    a = smoothstep(0.08, 0.3, t)
    return img, a


for _i in range(4):
    def _mk(i):
        @texture("FX/fire_%d" % i, (64, 128), k=4, alpha="soft", **FXDEG)
        def _f(ctx):
            return big_fire(ctx, i)
        return _f
    _mk(_i)


# --------------------------------------------------------------------------------------
# soft particles
# --------------------------------------------------------------------------------------
@texture("FX/smoke", (64, 64), k=4, alpha="soft", **FXDEG)
def smoke(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    n = fft_noise(r, H, W, beta=2.4)
    rad = radial(W, H)
    a = clamp01(1.0 - smoothstep(0.25, 1.0, rad + 0.2 * n)) * clamp01(0.75 + 0.3 * n)
    img = gradient_map(clamp01(0.5 + 0.25 * n - 0.2 * rad), [(0, "#2a2a2a"), (1, "#7a7a76")])
    return img, clamp01(a * 1.1)


@texture("FX/spark", (16, 16), k=16, alpha="soft", **FXDEG)
def spark(ctx):
    W, H = ctx.W, ctx.H
    rad = radial(W, H)
    core = np.exp(-(rad / 0.25) ** 2)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = np.abs(xx / W - 0.5) * 2, np.abs(yy / H - 0.5) * 2
    rays = np.exp(-(u / 0.08) ** 2) * (1 - v) + np.exp(-(v / 0.08) ** 2) * (1 - u)
    i = clamp01(core + rays * 0.6)
    img = gradient_map(i, [(0, "#802000"), (0.5, "#ffa030"), (1, "#fffbe0")])
    return img, smoothstep(0.05, 0.5, i)


@texture("FX/glow", (64, 64), k=4, alpha="soft", **FXDEG)
def glow(ctx):
    W, H = ctx.W, ctx.H
    rad = radial(W, H)
    a = clamp01(1 - rad) ** 2
    img = np.ones((H, W, 3), np.float32)
    return img, a


@texture("FX/blob_shadow", (64, 64), k=4, alpha="soft", **FXDEG)
def blob_shadow(ctx):
    W, H = ctx.W, ctx.H
    rad = radial(W, H)
    a = clamp01(1 - smoothstep(0.2, 1.0, rad)) * 0.85
    return np.zeros((H, W, 3), np.float32), a


@texture("FX/glint", (16, 16), k=16, alpha="soft", **FXDEG)
def glint(ctx):
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = (xx / W - 0.5) * 2, (yy / H - 0.5) * 2
    star = np.exp(-(np.abs(u) * np.abs(v)) / 0.004) * clamp01(1 - np.hypot(u, v))
    diag = np.exp(-(np.abs(u + v) * np.abs(u - v)) / 0.004) * clamp01(1 - np.hypot(u, v) * 1.6) * 0.5
    i = clamp01(star + diag + np.exp(-(np.hypot(u, v) / 0.15) ** 2))
    return np.ones((H, W, 3), np.float32) * np.array([1.0, 0.98, 0.9], np.float32), smoothstep(0.03, 0.6, i)


@texture("FX/dust", (32, 32), k=8, alpha="soft", **FXDEG)
def dust(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    a = np.zeros((H, W), np.float32)
    for _ in range(14):
        cx, cy = r.normal(0.5, 0.18) * W, r.normal(0.5, 0.18) * H
        rr = r.uniform(0.02, 0.06) * W
        a = np.maximum(a, ellipse_mask(W, H, cx, cy, rr, rr, soft=0.8) * r.uniform(0.3, 0.8))
    a = a * clamp01(1 - radial(W, H))
    img = np.ones((H, W, 3), np.float32) * np.array([0.62, 0.6, 0.55], np.float32)
    return img, a


@texture("FX/glass_shard", (16, 16), k=16, alpha="hard", q=80, desat=0.0, dark=1.0, maxv=1.0)
def glass_shard(ctx):
    W, H = ctx.W, ctx.H
    im, d = canvas(W, H)
    pts = [(0.2 * W, 0.85 * H), (0.55 * W, 0.08 * H), (0.85 * W, 0.7 * H), (0.5 * W, 0.62 * H)]
    d.polygon(pts, fill=255)
    m = to_mask(im)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    t = clamp01(0.4 + 0.5 * (1 - yy / H) * (xx / W))
    img = gradient_map(t, [(0, "#3a4a50"), (0.6, "#8aa0a8"), (1, "#e0f0f4")])
    edge = inner_rim(m, W * 0.03) + (1 - smoothstep(0, W * 0.04, np.abs(xx - (0.2 * W + (yy - 0.85 * H) * (0.35 / -0.77)))))
    img = mix(img, "#f0faff", clamp01(edge) * m * 0.6)
    return img, m


# --------------------------------------------------------------------------------------
# blood
# --------------------------------------------------------------------------------------
@texture("FX/blood_drop", (32, 32), k=8, alpha="hard", q=80, desat=0.0, dark=1.0)
def blood_drop(ctx):
    W, H = ctx.W, ctx.H
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    u, v = (xx + 0.5) / W - 0.5, (yy + 0.5) / H
    # teardrop: circle at bottom, pointed tip at top
    r = 0.26
    cy = 0.66
    circle = (u ** 2 + (v - cy) ** 2) < r ** 2
    tip = (v > 0.1) & (v < cy) & (np.abs(u) < r * ((v - 0.1) / (cy - 0.1)) ** 1.3)
    m = (circle | tip).astype(np.float32)
    shade = clamp01(0.5 - u * 1.2 - (v - cy) * 0.6)
    img = gradient_map(shade, [(0, "#2a0202"), (0.5, "#6a0806"), (1, "#a01a14")])
    img = mix(img, "#e08070", ellipse_mask(W, H, W * 0.4, H * 0.58, W * 0.06, H * 0.08, soft=0.6) * 0.8)
    return img, m


@texture("FX/blood_spray", (64, 64), k=8, alpha="hard", q=70, desat=0.0, dark=1.0)
def blood_spray(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    im, d = canvas(W, H)
    ox, oy = W * 0.1, H * 0.55
    for _ in range(140):
        a = r.normal(-0.15, 0.32)
        dist = r.uniform(0.05, 0.85) ** 0.8 * W
        x, y = ox + np.cos(a) * dist, oy + np.sin(a) * dist + (dist / W) ** 2 * H * 0.15
        rr = r.uniform(0.004, 0.022) * W * (1.3 - dist / W)
        d.ellipse([x - rr * 1.6, y - rr, x + rr * 1.6, y + rr], fill=255)
        if r.random() < 0.3:
            d.line([(x, y), (x - np.cos(a) * rr * 5, y - np.sin(a) * rr * 5)], fill=255, width=max(1, int(rr)))
    m = to_mask(im)
    img = gradient_map(clamp01(0.5 + 0.3 * fft_noise(r, H, W, beta=2.0)), [(0, "#3a0202"), (1, "#9a1610")])
    return img, m


# --------------------------------------------------------------------------------------
# explosions
# --------------------------------------------------------------------------------------
def explosion(ctx, frame):
    W, H = ctx.W, ctx.H
    r = rng_for("explosion_shared")
    n = fft_noise(r, H, W, beta=2.8)
    n2 = fft_noise(r, H, W, beta=2.2)
    rad = radial(W, H)
    size = [0.45, 0.75, 0.92, 1.0][frame]
    heat_amt = [1.0, 0.85, 0.5, 0.15][frame]
    shape = 1.0 - smoothstep(size * 0.6, size, rad + 0.18 * n * size)
    heat = clamp01(shape * heat_amt * (1.2 - rad / size) + 0.25 * n2 * shape)
    smoke_m = clamp01(shape * (1 - heat * 1.5)) * [0.1, 0.4, 0.8, 1.0][frame]
    t = clamp01(heat)
    img = gradient_map(t, FIRE_STOPS)
    smoke_col = gradient_map(clamp01(0.4 + 0.3 * n), [(0, "#0a0808"), (1, "#3a3430")])
    img = mix(img, smoke_col, smoke_m)
    if frame == 3:  # embers in the smoke
        emb = (n2 > 2.0).astype(np.float32) * shape
        img = mix(img, "#ff7020", emb)
    a = clamp01(smoothstep(0.02, 0.2, shape) * (0.6 + 0.4 * clamp01(heat * 2 + smoke_m)))
    return img, a


for _i in range(4):
    def _mk(i):
        @texture("FX/explosion_%d" % i, (128, 128), k=4, alpha="soft", **FXDEG)
        def _f(ctx):
            return explosion(ctx, i)
        return _f
    _mk(_i)


# --------------------------------------------------------------------------------------
# VHS shader sources (raw, not degraded)
# --------------------------------------------------------------------------------------
@texture("FX/noise_rgb", (256, 256), k=1)
def noise_rgb(ctx):
    r = ctx.sub(1)
    return Image.fromarray(r.integers(0, 256, (256, 256, 3), dtype=np.uint8), "RGB")


@texture("FX/noise_gray", (256, 256), k=1)
def noise_gray(ctx):
    r = ctx.sub(1)
    g = r.integers(0, 256, (256, 256), dtype=np.uint8)
    return Image.fromarray(np.dstack([g, g, g]), "RGB")


@texture("FX/scratches", (256, 256), k=1)
def scratches(ctx):
    """film / tape scratches and dropouts on black (grayscale stored as RGB). Tiles."""
    W = H = 256
    r = ctx.sub(1)
    a = np.zeros((H, W), np.float32)
    # thin vertical scratches
    for _ in range(26):
        x = r.uniform(0, W)
        y0 = r.uniform(0, H)
        L = r.uniform(0.2, 1.0) * H
        v = r.uniform(0.25, 1.0)
        ys = (np.arange(int(L)) + int(y0)) % H
        wob = np.cumsum(r.normal(0, 0.15, len(ys)))
        xs = (x + wob).astype(int) % W
        fade = np.sin(np.linspace(0, np.pi, len(ys))) ** 0.5
        a[ys, xs] = np.maximum(a[ys, xs], v * fade)
    # horizontal tape dropouts: short bright streaks with noisy breakup
    for _ in range(18):
        y = int(r.uniform(0, H))
        x0 = r.uniform(0, W)
        L = r.uniform(8, 90)
        xs = (np.arange(int(L)) + int(x0)) % W
        v = r.uniform(0.4, 1.0) * (r.random(len(xs)) > 0.25)
        a[y, xs] = np.maximum(a[y, xs], v)
        if r.random() < 0.4:
            a[(y + 1) % H, xs] = np.maximum(a[(y + 1) % H, xs], v * 0.5)
    # dust specks
    sp = r.random((H, W)) > 0.9985
    a = np.maximum(a, sp * r.uniform(0.5, 1.0, (H, W)))
    a = clamp01(a)
    g = (a * 255 + 0.5).astype(np.uint8)
    return Image.fromarray(np.dstack([g, g, g]), "RGB")
