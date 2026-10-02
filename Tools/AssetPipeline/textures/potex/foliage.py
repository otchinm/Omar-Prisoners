"""Textures/Foliage - hard-alpha cutout silhouettes (dead trees, conifers, big tree, bushes, grass,
corn, weeds) and the wide treeline strip for the far boundary ring."""
import numpy as np
from PIL import Image, ImageDraw

from .core import (blur, canvas, clamp01, fft_noise, gradient_map, mix, smoothstep, texture, to_mask, warp,
                   wrap_offsets)
from .mat import photo

# --------------------------------------------------------------------------------------
# drawing primitives
# --------------------------------------------------------------------------------------


def draw_branch(d, rng, x, y, ang, length, width, depth, maxd, spread=0.55, up_bias=0.25, kids=(2, 3),
                shrink=(0.62, 0.8), min_w=1.6, offsets=((0, 0),), out=None):
    """recursive bare branch; angles in image space (up = -pi/2)"""
    segs = 4
    pts = [(x, y)]
    a = ang
    for i in range(segs):
        a += rng.normal(0, 0.16)
        # gently bend toward vertical-up
        a += (-np.pi / 2 - a) * up_bias * 0.25
        x += np.cos(a) * length / segs
        y += np.sin(a) * length / segs
        pts.append((x, y))
    w0, w1 = width, max(min_w, width * 0.7)
    for i in range(segs):
        w = w0 + (w1 - w0) * i / segs
        for ox, oy in offsets:
            d.line([(pts[i][0] + ox, pts[i][1] + oy), (pts[i + 1][0] + ox, pts[i + 1][1] + oy)], fill=255, width=max(1, int(round(w))))
            r = w / 2
            d.ellipse([pts[i + 1][0] + ox - r, pts[i + 1][1] + oy - r, pts[i + 1][0] + ox + r, pts[i + 1][1] + oy + r], fill=255)
    if out is not None:
        out.append((x, y))
    if depth >= maxd or width < min_w:
        return
    n = int(rng.integers(kids[0], kids[1] + 1))
    for k in range(n):
        ca = a + rng.uniform(-spread, spread) + (k - (n - 1) / 2) * spread * 0.6
        # spawn some kids from along the segment, not only the tip
        t = rng.uniform(0.55, 1.0)
        si = int(t * segs)
        sx, sy = pts[min(si, segs)]
        draw_branch(d, rng, sx, sy, ca, length * rng.uniform(*shrink), max(min_w * 0.9, w1 * rng.uniform(0.55, 0.75)),
                    depth + 1, maxd, spread, up_bias, kids, shrink, min_w, offsets, out)


def bark_color(ctx, H, W, salt=0, dark="#141210", mid="#2c2a26", light="#4a4640"):
    r = ctx.sub(50 + salt)
    t = clamp01(0.5 + 0.18 * fft_noise(r, H, W, beta=2.2) + 0.1 * photo(ctx, "gravel", H, W, salt=salt))
    return gradient_map(t, [(0, dark), (0.5, mid), (1, light)])


def leaf_color(ctx, H, W, salt=0, stops=None):
    r = ctx.sub(60 + salt)
    t = clamp01(0.5 + 0.2 * fft_noise(r, H, W, beta=1.8) + 0.12 * photo(ctx, "grass", H, W, salt=salt))
    stops = stops or [(0, "#070906"), (0.5, "#161c12"), (0.8, "#26301e"), (1, "#3a4430")]
    return gradient_map(t, stops)


def vertical_shade(img, H, top=1.15, bottom=0.7):
    yy = (np.arange(H) + 0.5) / H
    return img * (top + (bottom - top) * yy)[:, None, None]


# --------------------------------------------------------------------------------------
# dead trees
# --------------------------------------------------------------------------------------
def dead_tree(ctx, seed_salt, trunk_w, height, spread, maxd, lean=0.0, kids=(2, 4), split=0.38, nlimbs=(3, 4)):
    W, H = ctx.W, ctx.H
    r = ctx.sub(seed_salt)
    im, d = canvas(W, H)
    base_x = W * 0.5 + r.normal(0, W * 0.02)
    # trunk with root flare, slightly crooked
    d.polygon([(base_x - trunk_w * 1.5, H), (base_x + trunk_w * 1.5, H), (base_x + trunk_w * 0.55, H - trunk_w * 2.5), (base_x - trunk_w * 0.55, H - trunk_w * 2.5)], fill=255)
    tips = []
    draw_branch(d, r, base_x, H - 1, -np.pi / 2 + lean, height * split, trunk_w, 0, 0, spread=0.1, up_bias=0.6, out=tips)
    tx, ty = tips[-1]
    nl = int(r.integers(nlimbs[0], nlimbs[1] + 1))
    min_w = W / 1024.0 * 2.6
    for k in range(nl):
        a = -np.pi / 2 + lean + (k - (nl - 1) / 2) / max(1, nl - 1) * spread * 2 + r.normal(0, 0.12)
        draw_branch(d, r, tx, ty, a, height * r.uniform(0.2, 0.27), trunk_w * r.uniform(0.6, 0.75), 1, maxd,
                    spread=0.42, up_bias=0.18, kids=kids, shrink=(0.7, 0.84), min_w=min_w)
    # a couple of lower side limbs
    for k in range(int(r.integers(1, 3))):
        sy = ty + (H - ty) * r.uniform(0.15, 0.4)
        sx = base_x + (tx - base_x) * (H - sy) / max(1.0, (H - ty))
        side = r.choice([-1, 1])
        draw_branch(d, r, sx, sy, -np.pi / 2 + side * r.uniform(0.6, 1.0), height * r.uniform(0.14, 0.2), trunk_w * 0.4, 2, maxd,
                    spread=0.45, up_bias=0.2, kids=(2, 2), shrink=(0.68, 0.8), min_w=min_w)
    m = to_mask(im)
    img = bark_color(ctx, H, W, seed_salt)
    img = vertical_shade(img, H, 1.25, 0.75)
    return img, m


@texture("Foliage/tree_dead_1", (256, 256), alpha="hard", alpha_thr=0.32, q=45)
def tree_dead_1(ctx):
    return dead_tree(ctx, 1, ctx.W * 0.032, ctx.H * 0.95, 0.75, 9)


@texture("Foliage/tree_dead_2", (256, 256), alpha="hard", alpha_thr=0.32, q=45)
def tree_dead_2(ctx):
    return dead_tree(ctx, 2, ctx.W * 0.026, ctx.H * 0.98, 0.5, 9, lean=0.06, split=0.45, nlimbs=(2, 3))


@texture("Foliage/tree_dead_3", (256, 256), alpha="hard", alpha_thr=0.32, q=45)
def tree_dead_3(ctx):
    return dead_tree(ctx, 3, ctx.W * 0.038, ctx.H * 0.88, 0.95, 8, kids=(2, 3), split=0.3, nlimbs=(4, 5))


# --------------------------------------------------------------------------------------
# conifers / poplars
# --------------------------------------------------------------------------------------
def conifer(ctx, salt, kind="pine", ox=0.5, hfrac=0.98, wfrac=0.42, d=None, W=None, H=None, offsets=((0, 0),)):
    W = W or ctx.W
    H = H or ctx.H
    r = ctx.sub(200 + salt)
    own = d is None
    if own:
        im, d = canvas(W, H)
    cx = ox * W
    top = H * (1 - hfrac)
    base = H
    tw = W * 0.025
    for ox_, oy_ in offsets:
        d.polygon([(cx - tw + ox_, base), (cx + tw + ox_, base), (cx + tw * 0.3 + ox_, top + oy_), (cx - tw * 0.3 + ox_, top + oy_)], fill=255)
    n = int((base - top) / (W / 128.0) / 1.2)
    for i in range(n):
        t = r.uniform(0, 1) ** 0.9
        y = top + t * (base - top) * 0.92
        if kind == "pine":
            wmax = wfrac * W * 0.5 * (0.15 + 0.85 * t ** 0.85)
        else:  # poplar / cypress: spindle
            wmax = wfrac * W * 0.5 * (np.sin(np.pi * min(1, t * 1.05)) ** 0.7 * 0.9 + 0.1)
        side = r.choice([-1, 1])
        L = wmax * r.uniform(0.6, 1.05)
        droop = r.uniform(0.05, 0.4) * L * (0.6 if kind != "pine" else 1.0)
        th = r.uniform(2.0, 5.0) * W / 128.0
        x0 = cx + side * r.uniform(0, tw)
        tipx, tipy = x0 + side * L, y + droop
        pts = [(x0, y - th)]
        k = 5
        for j in range(1, k + 1):
            u = j / k
            px = x0 + side * L * u
            py = y - th * (1 - u) + droop * u * u + r.normal(0, th * 0.4)
            pts.append((px, py - r.uniform(0, th)))
        for j in range(k, -1, -1):
            u = j / k
            px = x0 + side * L * u
            py = y + th * (1 - u) * 0.6 + droop * u * u + r.normal(0, th * 0.4)
            pts.append((px, py + r.uniform(0, th * 0.8)))
        for ox_, oy_ in offsets:
            d.polygon([(px + ox_, py + oy_) for px, py in pts], fill=255)
    if own:
        return to_mask(im)


@texture("Foliage/tree_pine_1", (128, 256), alpha="hard", alpha_thr=0.4, q=45)
def tree_pine_1(ctx):
    W, H = ctx.W, ctx.H
    m = conifer(ctx, 1, "pine", wfrac=0.9)
    r = ctx.sub(3)
    holes = (fft_noise(r, H, W, beta=1.4) > 1.5).astype(np.float32)
    m = m * (1 - holes)
    img = leaf_color(ctx, H, W, 1)
    img = vertical_shade(img, H, 1.1, 0.7)
    return img, m


@texture("Foliage/tree_pine_2", (128, 256), alpha="hard", alpha_thr=0.4, q=45)
def tree_pine_2(ctx):
    W, H = ctx.W, ctx.H
    m = conifer(ctx, 2, "poplar", wfrac=0.7)
    r = ctx.sub(3)
    holes = (fft_noise(r, H, W, beta=1.4) > 1.6).astype(np.float32)
    m = m * (1 - holes)
    img = leaf_color(ctx, H, W, 2, [(0, "#060806"), (0.5, "#141a12"), (0.8, "#222a1c"), (1, "#323c2a")])
    img = vertical_shade(img, H, 1.1, 0.7)
    return img, m


# --------------------------------------------------------------------------------------
# leafy tree / bushes
# --------------------------------------------------------------------------------------
def leafy_blob(ctx, salt, blobs, holes=1.4, edge=0.18, W=None, H=None, tile=False):
    """union of noisy discs; leaf-scale noise cuts the edges and punches holes"""
    W = W or ctx.W
    H = H or ctx.H
    r = ctx.sub(300 + salt)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    field = np.full((H, W), -10.0, np.float32)
    for (cx, cy, rad) in blobs:
        dx = xx - cx
        if tile:
            dx = (dx + W / 2) % W - W / 2
        dd = np.sqrt(dx ** 2 + (yy - cy) ** 2) / rad
        field = np.maximum(field, 1 - dd)
    leaf = fft_noise(r, H, W, beta=1.2)
    coarse = fft_noise(r, H, W, beta=2.6)
    m = ((field + edge * leaf * clamp01(field + 0.6) + 0.12 * coarse) > 0.12).astype(np.float32) * (field > -0.3)
    m = m * (leaf < holes).astype(np.float32)
    return m, field


@texture("Foliage/tree_big", (256, 256), alpha="hard", alpha_thr=0.4, q=45)
def tree_big(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    im, d = canvas(W, H)
    bx = W * 0.47
    tw = W * 0.045
    d.polygon([(bx - tw * 1.6, H), (bx + tw * 1.6, H), (bx + tw * 0.8, H * 0.6), (bx - tw * 0.7, H * 0.6)], fill=255)
    for k in range(5):
        a = -np.pi / 2 + r.uniform(-0.9, 0.9)
        draw_branch(d, r, bx, H * (0.62 + r.uniform(-0.04, 0.04)), a, H * r.uniform(0.2, 0.3), tw * r.uniform(0.5, 0.8), 0, 3,
                    spread=0.5, kids=(2, 2), min_w=W / 512 * 3)
    trunk = to_mask(im)
    blobs = []
    for k in range(26):
        a = r.uniform(0, 2 * np.pi)
        rr = np.sqrt(r.uniform(0, 1))
        cx = W * 0.5 + np.cos(a) * rr * W * 0.33
        cy = H * 0.36 + np.sin(a) * rr * H * 0.24
        blobs.append((cx, cy, W * r.uniform(0.07, 0.14)))
    crown, field = leafy_blob(ctx, 1, blobs, holes=1.25)
    m = np.maximum(trunk * (1 - crown * 0.0), crown)
    leaves = leaf_color(ctx, H, W, 3)
    # light from above-left on the crown
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    lit = clamp01(0.5 + field * 1.5 - (yy / H - 0.2) * 0.8)
    leaves = leaves * (0.6 + 0.6 * lit)[..., None]
    bark = bark_color(ctx, H, W, 4, "#0a0806", "#1e1a16", "#36302a")
    img = mix(bark, leaves, crown)
    return img, m


def bush(ctx, salt):
    W, H = ctx.W, ctx.H
    r = ctx.sub(400 + salt)
    blobs = []
    for k in range(10):
        cx = W * 0.5 + r.normal(0, W * 0.16)
        cy = H * 0.68 + r.normal(0, H * 0.1)
        blobs.append((cx, cy, W * r.uniform(0.12, 0.22)))
    blobs.append((W * 0.5, H * 0.95, W * 0.3))
    m, field = leafy_blob(ctx, 10 + salt, blobs, holes=1.35, edge=0.2)
    yy = (np.arange(H) + 0.5) / H
    m = m * (yy < 0.995)[:, None]
    img = leaf_color(ctx, H, W, 10 + salt, [(0, "#060705"), (0.5, "#141810"), (0.8, "#242a1c"), (1, "#343a28")])
    img = img * (0.55 + 0.7 * clamp01(field * 2.0))[..., None]
    img = vertical_shade(img, H, 1.1, 0.6)
    return img, m


@texture("Foliage/bush_dark_1", (128, 128), alpha="hard", alpha_thr=0.4, q=45)
def bush_dark_1(ctx):
    return bush(ctx, 1)


@texture("Foliage/bush_dark_2", (128, 128), alpha="hard", alpha_thr=0.4, q=45)
def bush_dark_2(ctx):
    return bush(ctx, 2)


# --------------------------------------------------------------------------------------
# grass / corn / weeds
# --------------------------------------------------------------------------------------
def blades(ctx, salt, n, hmin, hmax, wmin, wmax, bend=0.5, palette=None, W=None, H=None):
    W = W or ctx.W
    H = H or ctx.H
    r = ctx.sub(500 + salt)
    im, d = canvas(W, H)
    cim, cd = canvas(W, H)
    for i in range(n):
        x = r.uniform(0.02, 0.98) * W
        h = r.uniform(hmin, hmax) * H
        w = r.uniform(wmin, wmax)
        lean = r.normal(0, bend)
        segs = 6
        left, right = [], []
        for k in range(segs + 1):
            t = k / segs
            px = x + lean * h * t * t + r.normal(0, w * 0.1)
            py = H - h * t
            ww = w * (1 - t) * 0.5 + 0.6
            left.append((px - ww, py))
            right.append((px + ww, py))
        poly = left + right[::-1]
        v = int(r.uniform(40, 255))
        d.polygon(poly, fill=255)
        cd.polygon(poly, fill=v)
    m = to_mask(im)
    shade = to_mask(cim)
    palette = palette or [(0, "#1a170c"), (0.4, "#3e3820"), (0.75, "#6a5e38"), (1, "#8a7c50")]
    img = gradient_map(clamp01(shade * 0.8 + 0.2 * clamp01(0.5 + 0.3 * fft_noise(ctx.sub(510 + salt), H, W, beta=2.0))), palette)
    img = vertical_shade(img, H, 1.15, 0.55)
    return img, m


@texture("Foliage/grass_tall_1", (128, 128), alpha="hard", alpha_thr=0.35, q=45)
def grass_tall_1(ctx):
    s = ctx.W / 512.0
    return blades(ctx, 1, 160, 0.35, 0.95, 5 * s, 12 * s, bend=0.35)


@texture("Foliage/grass_tall_2", (128, 128), alpha="hard", alpha_thr=0.35, q=45)
def grass_tall_2(ctx):
    s = ctx.W / 512.0
    img, m = blades(ctx, 2, 130, 0.25, 0.8, 5 * s, 14 * s, bend=0.5,
                    palette=[(0, "#12140a"), (0.4, "#2e3420"), (0.75, "#4e5232"), (1, "#6e6a46")])
    # a few seed heads
    W, H = ctx.W, ctx.H
    r = ctx.sub(9)
    im, d = canvas(W, H)
    for k in range(9):
        x = r.uniform(0.1, 0.9) * W
        y = H * r.uniform(0.05, 0.35)
        d.line([(x, H), (x + r.normal(0, 20 * s), y)], fill=255, width=max(1, int(3 * s)))
        d.ellipse([x - 6 * s, y - 22 * s, x + 6 * s, y + 4 * s], fill=255)
    sm = to_mask(im)
    img = mix(img, "#6a5a38", sm)
    return img, np.maximum(m, sm)


@texture("Foliage/cornstalks", (128, 256), alpha="hard", alpha_thr=0.35, q=45)
def cornstalks(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    r = ctx.sub(1)
    im, d = canvas(W, H)
    cim, cd = canvas(W, H)
    for k in range(5):
        x = W * (0.12 + 0.19 * k) + r.normal(0, 10 * s)
        top = H * r.uniform(0.04, 0.2)
        lean = r.normal(0, 30 * s)
        sw = r.uniform(10, 14) * s
        stem = [(x + lean * ((H - y) / H) ** 2, y) for y in np.linspace(H, top, 12)]
        d.line(stem, fill=255, width=int(sw))
        cd.line(stem, fill=int(r.uniform(120, 200)), width=int(sw))
        # tassel
        tx, ty = stem[-1]
        for t in range(5):
            a = -np.pi / 2 + r.uniform(-0.8, 0.8)
            d.line([(tx, ty), (tx + np.cos(a) * 40 * s, ty + np.sin(a) * 40 * s + 20 * s)], fill=255, width=int(3 * s))
        # leaves: long drooping ribbons, dead and torn
        for j in range(9):
            t = r.uniform(0.15, 0.85)
            idx = int(t * (len(stem) - 1))
            lx, ly = stem[idx]
            side = r.choice([-1, 1])
            L = r.uniform(120, 260) * s
            lw = r.uniform(10, 18) * s
            up = r.uniform(0.2, 0.9)
            pts_top, pts_bot = [], []
            for q in range(9):
                u = q / 8
                px = lx + side * L * u
                py = ly - up * L * 0.6 * u + (L * 0.9 * u * u) * r.uniform(0.6, 1.0)
                ww = lw * (1 - u) * 0.5 + 0.8
                pts_top.append((px, py - ww))
                pts_bot.append((px, py + ww))
            poly = pts_top + pts_bot[::-1]
            d.polygon(poly, fill=255)
            cd.polygon(poly, fill=int(r.uniform(60, 255)))
        # dried ear
        if r.random() < 0.7:
            ex, ey = stem[int(len(stem) * 0.5)]
            d.ellipse([ex - 9 * s, ey - 40 * s, ex + 13 * s, ey + 10 * s], fill=255)
            cd.ellipse([ex - 9 * s, ey - 40 * s, ex + 13 * s, ey + 10 * s], fill=90)
    m = to_mask(im)
    tears = (fft_noise(ctx.sub(2), H, W, beta=1.2) > 1.7).astype(np.float32)
    m = m * (1 - tears)
    sh = to_mask(cim)
    img = gradient_map(clamp01(sh * 0.85 + 0.15 * clamp01(0.5 + 0.3 * fft_noise(ctx.sub(3), H, W, beta=2.0))),
                       [(0, "#1a140a"), (0.4, "#3e3220"), (0.7, "#6a5a3a"), (1, "#8a7a56")])
    img = vertical_shade(img, H, 1.1, 0.6)
    return img, m


@texture("Foliage/weeds", (64, 64), k=8, alpha="hard", alpha_thr=0.35, q=45)
def weeds(ctx):
    W, H = ctx.W, ctx.H
    s = W / 512.0
    img, m = blades(ctx, 3, 40, 0.2, 0.7, 14 * s, 30 * s, bend=0.7,
                    palette=[(0, "#0e100a"), (0.4, "#262c1a"), (0.75, "#424a2c"), (1, "#5a6040")])
    r = ctx.sub(4)
    im, d = canvas(W, H)
    for k in range(5):
        x = r.uniform(0.2, 0.8) * W
        y = H * r.uniform(0.15, 0.45)
        d.line([(x, H), (x + r.normal(0, 30 * s), y)], fill=255, width=int(5 * s))
        for q in range(4):
            a = r.uniform(0, 2 * np.pi)
            d.ellipse([x + np.cos(a) * 14 * s - 8 * s, y + np.sin(a) * 14 * s - 8 * s, x + np.cos(a) * 14 * s + 8 * s, y + np.sin(a) * 14 * s + 8 * s], fill=255)
    sm = to_mask(im)
    img = mix(img, "#4a4030", sm)
    return img, np.maximum(m, sm)


# --------------------------------------------------------------------------------------
# treeline strip (tiles horizontally around the map)
# --------------------------------------------------------------------------------------
@texture("Foliage/treeline", (1024, 256), k=2, tile=True, alpha="hard", alpha_thr=0.4, q=45)
def treeline(ctx):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    im, d = canvas(W, H)
    offs = ((-W, 0), (0, 0), (W, 0))
    # solid undergrowth band
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    band_top = H * (0.62 + 0.06 * fft_noise(r, 1, W, beta=2.0)[0])
    base = (yy > band_top[None, :]).astype(np.float32)
    # trees: mix of conifer spires and rounded crowns
    n = 56
    for i in range(n):
        x = (i + r.uniform(-0.4, 0.4)) / n * W
        h = r.uniform(0.4, 0.97) if r.random() < 0.7 else r.uniform(0.3, 0.5)
        kind = "pine" if r.random() < 0.5 else "poplar"
        wpx = r.uniform(26, 60) * W / 2048
        # draw a conifer scaled into this strip
        sub_w = int(wpx * 2.4)
        conifer(ctx, 1000 + i, kind, ox=x / W, hfrac=h, wfrac=sub_w / W * 1.2, d=d, W=W, H=H, offsets=offs)
    # rounded deciduous crowns
    blobs = []
    for i in range(45):
        x = r.uniform(0, W)
        cy = H * r.uniform(0.28, 0.6)
        blobs.append((x, cy, H * r.uniform(0.07, 0.15)))
    crowns, field = leafy_blob(ctx, 99, blobs, holes=1.5, edge=0.22, W=W, H=H, tile=True)
    # bare tree tops poking out
    for i in range(18):
        x = r.uniform(0, W)
        draw_branch(d, r, x, H * 0.65, -np.pi / 2 + r.normal(0, 0.12), H * r.uniform(0.2, 0.32), W / 2048 * 7, 0, 6,
                    spread=0.5, kids=(2, 3), shrink=(0.7, 0.82), min_w=1.6, offsets=offs)
    m = np.maximum(np.maximum(to_mask(im), crowns), base)
    img = leaf_color(ctx, H, W, 7, [(0, "#050605"), (0.5, "#10140e"), (0.8, "#1a2018"), (1, "#262c22")])
    img = vertical_shade(img, H, 1.25, 0.55)
    return img, m
