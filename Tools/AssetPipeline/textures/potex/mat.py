"""Material building blocks shared by Env / Props: photo grain, wood, boards, tile / brick
grids, paint chipping, rust, corrugation."""
import numpy as np
from PIL import Image

from .core import (blur, blur_xy, clamp01, col, fft_noise, gradient_map, grime_mask, lerp, make_tileable,
                   mix, normz, photo_detail, resize, smoothstep, warp, drips)

# ------------------------------------------------------------------------------------------
# photo grain sources
# ------------------------------------------------------------------------------------------
PHOTO_BOX = {
    # name: (source, crop box or None)
    "gravel": ("gravel", None),
    "gravel_fine": ("gravel", (0, 0, 256, 256)),
    "grass": ("grass", None),
    "brick": ("brick", None),
    "moon": ("moon", (40, 40, 472, 472)),
    "camera_grass": ("camera", (0, 400, 512, 512)),
}


def photo(ctx, kind, H=None, W=None, salt=0, sigma=None, rot=0, stretch=None):
    """tileable zero-mean photo detail (H,W) from one of the CC0 scikit-image photos.
    stretch=(sx, sy): take a smaller crop and stretch it (fibres / streaks)."""
    H = H or ctx.H
    W = W or ctx.W
    key = ("photo", kind, H, W, salt, sigma, rot, stretch, ctx.name)
    cache = ctx.src._cache
    if key in cache:
        return cache[key]
    srcname, box = PHOTO_BOX[kind]
    src = ctx.src.gray(srcname)
    if stretch is not None:
        sx, sy = stretch
        hh, ww = src.shape
        r = ctx.sub(1000 + salt)
        cw, ch = int(ww / sx), int(hh / sy)
        x0 = int(r.integers(0, ww - cw + 1))
        y0 = int(r.integers(0, hh - ch + 1))
        box = (x0, y0, x0 + cw, y0 + ch)
    elif salt and box is None:
        # different region per salt: roll the photo
        src = np.roll(src, (salt * 97, salt * 151), (0, 1))
    d = photo_detail(src, W, H, ctx.sub(2000 + salt), box=box, sigma=sigma, rot=rot)
    cache[key] = d
    return d


_HP = {}


def hp_detail(H, W, variant=0, kind="gravel"):
    """cached tileable photo high-pass not tied to a texture (used to break up grime edges)"""
    from . import core
    key = (kind, H, W, variant)
    if key not in _HP:
        srcname, box = PHOTO_BOX[kind]
        img = core.CURRENT_SRC.gray(srcname)
        if box is not None:
            img = img[box[1]:box[3], box[0]:box[2]]
        img = np.roll(img, (variant * 131, variant * 71), (0, 1))
        _HP[key] = photo_detail(np.ascontiguousarray(img), W, H, np.random.default_rng(4242 + variant))
    return _HP[key]


def grime(rng, h, w, cover=0.4, beta=2.8, sharp=0.25, ax=1.0, ay=1.0, damt=0.6, kind="gravel"):
    """grime_mask whose edges are broken up by real photo detail"""
    from . import core
    v = int(rng.integers(0, 6))
    return core.grime_mask(rng, h, w, cover, beta, sharp, ax, ay, detail=hp_detail(h, w, v, kind), damt=damt)


# ------------------------------------------------------------------------------------------
# wood
# ------------------------------------------------------------------------------------------
def wood_grain(ctx, H, W, along="y", rings=6, salt=0, fibre=0.5, tile=True):
    """grayscale figure 0..1 (mean ~0.5). along='y': grain runs vertically."""
    r = ctx.sub(3000 + salt)
    if along == "y":
        ax, ay = 1.0, 12.0
    else:
        ax, ay = 12.0, 1.0
    long_ = fft_noise(r, H, W, beta=2.0, ax=ax, ay=ay)
    fine = fft_noise(r, H, W, beta=1.0, ax=ax * 0.5, ay=ay * 0.5)
    wn = fft_noise(r, H, W, beta=3.2, ax=ax * 0.3, ay=ay * 0.3)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    across = (xx / W) if along == "y" else (yy / H)
    ring = np.sin(2 * np.pi * (across * rings + wn * 0.35))
    ring = (ring * 0.5 + 0.5) ** 2.5
    if along == "y":
        fib = photo(ctx, "grass", H, W, salt=salt, stretch=(1.0, 5.0))
    else:
        fib = photo(ctx, "grass", H, W, salt=salt, stretch=(5.0, 1.0))
    g = 0.5 + 0.16 * long_ + 0.07 * fine - 0.14 * ring + fibre * 0.07 * fib
    return g


def boards(ctx, H, W, n, along="y", joints=(0, 1), salt=0, jitter=0.25):
    """Planks running `along` the axis, stacked across the other one.
    Returns dict: seg (int id per board segment), rand (H,W) 0..1 per segment,
    edge (H,W) px distance to nearest seam, u (H,W) 0..1 across the board, board (index)."""
    r = ctx.sub(4000 + salt)
    if along == "x":
        d = boards(ctx, W, H, n, "y", joints, salt, jitter)
        return {k: (np.ascontiguousarray(v.T) if isinstance(v, np.ndarray) and v.ndim == 2 else v) for k, v in d.items()}
    widths = r.uniform(1.0 - jitter, 1.0 + jitter, n)
    widths = widths / widths.sum() * W
    edges = np.concatenate([[0], np.cumsum(widths)])
    xs = np.arange(W) + 0.5
    bi = np.clip(np.searchsorted(edges, xs, side="right") - 1, 0, n - 1)
    u = (xs - edges[bi]) / widths[bi]
    seg = np.zeros((H, W), np.int32)
    edge = np.zeros((H, W), np.float32)
    ys = np.arange(H) + 0.5
    rand_tab = r.random(n * 8 + 8)
    for b in range(n):
        cols = bi == b
        k = int(r.integers(joints[0], joints[1] + 1))
        jy = np.sort(r.uniform(0, H, k)) if k else np.array([])
        if k:
            idx = np.searchsorted(jy, ys) % k  # segment index (wrap merges last/first)
            dist = np.min(np.abs(((ys[:, None] - jy[None, :]) + H / 2) % H - H / 2), axis=1)
        else:
            idx = np.zeros(H, int)
            dist = np.full(H, 1e9)
        dx = np.minimum(u[cols] * widths[b], (1 - u[cols]) * widths[b])
        seg[:, cols] = (b * 8 + idx)[:, None]
        edge[:, cols] = np.minimum(dx[None, :], dist[:, None])
    uu = np.broadcast_to(u[None, :], (H, W)).astype(np.float32)
    return dict(seg=seg, rand=rand_tab[seg].astype(np.float32), edge=edge, u=uu,
                board=np.broadcast_to(bi[None, :], (H, W)))


def plank_surface(ctx, H, W, n, along, base, dark, light=None, joints=(0, 1), gap_px=None, salt=0,
                  grain_amt=1.0, var=0.12, rings=5, worn=None):
    """Complete plank texture (RGB) + the board dict."""
    b = boards(ctx, H, W, n, along, joints, salt)
    g = wood_grain(ctx, H, W, along, rings=rings, salt=salt)
    # per board shift of grain so neighbours differ
    shift = (b["rand"] - 0.5)
    tone = clamp01(g + shift * 0.25)
    light = light or base
    img = gradient_map(clamp01((tone - 0.5) * grain_amt + 0.5), [(0.0, dark), (0.55, base), (1.0, light)])
    img = img * (1.0 + (b["rand"][..., None] - 0.5) * 2 * var)
    gp = gap_px if gap_px is not None else max(2.0, W / 256.0)
    groove = 1.0 - smoothstep(0.0, gp, b["edge"])
    ao = 1.0 - smoothstep(gp, gp * 4, b["edge"])
    img = img * (1.0 - 0.75 * groove[..., None]) * (1.0 - 0.18 * ao[..., None])
    return img, b


# ------------------------------------------------------------------------------------------
# grids: tiles / bricks / blocks
# ------------------------------------------------------------------------------------------
def grid(ctx, H, W, nx, ny, offset=0.0, warp_px=0.0, salt=0, jitter=0.0):
    """regular grid of cells with optional row offset (bricks) and smooth periodic warp.
    Returns dict: id (unique per cell), rand (per cell 0..1), edge (px to nearest joint),
    fx, fy (0..1 in cell), ix, iy."""
    r = ctx.sub(5000 + salt)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    if warp_px:
        dx = fft_noise(r, H, W, beta=4.0) * warp_px
        dy = fft_noise(r, H, W, beta=4.0) * warp_px
        xx = xx + dx
        yy = yy + dy
    cw, chh = W / nx, H / ny
    Y = yy / chh
    iy = np.floor(Y)
    X = xx / cw + offset * np.mod(iy, 2)
    ix = np.floor(X)
    fx, fy = X - ix, Y - iy
    ixm = np.mod(ix, nx).astype(np.int32)
    iym = np.mod(iy, ny).astype(np.int32)
    cid = iym * nx + ixm
    tab = r.random(nx * ny + 1).astype(np.float32)
    edge = np.minimum(np.minimum(fx, 1 - fx) * cw, np.minimum(fy, 1 - fy) * chh)
    return dict(id=cid, rand=tab[cid], edge=edge.astype(np.float32), fx=fx, fy=fy, ix=ixm, iy=iym)


# ------------------------------------------------------------------------------------------
# paint / rust / metal
# ------------------------------------------------------------------------------------------
def chips(ctx, H, W, cover=0.15, salt=0, bias=None, sharp=0.04, beta=2.2):
    """paint chip / peeling mask (1 = paint missing)"""
    r = ctx.sub(6000 + salt)
    n = fft_noise(r, H, W, beta=beta)
    if bias is not None:
        n = n + bias
    thr = np.quantile(n, 1.0 - cover)
    return smoothstep(thr - sharp, thr + sharp, n)


def rust_color(ctx, H, W, salt=0):
    r = ctx.sub(7000 + salt)
    n = clamp01(0.5 + 0.2 * fft_noise(r, H, W, beta=2.0) + 0.1 * photo(ctx, "gravel", H, W, salt=salt + 3))
    return gradient_map(n, [(0.0, "#1e110a"), (0.35, "#43230f"), (0.6, "#6a3a1c"), (0.8, "#7e4a26"), (1.0, "#9a6a44")])


def rust_mask(ctx, H, W, cover=0.3, salt=0, streaks=0, tile=True):
    r = ctx.sub(7100 + salt)
    m = grime(r, H, W, cover=cover, beta=2.4, sharp=0.3)
    if streaks:
        m = np.maximum(m, drips(r, H, W, streaks, length=(0.15, 0.6), width=(W / 300, W / 80), tile=tile) * 0.9)
    return clamp01(m)


def corrugation(W, H, n, along="y", sharp=1.0):
    """shading factor for corrugated sheet (ridges run `along`)"""
    if along == "y":
        t = (np.arange(W) + 0.5) / W
        s = np.sin(2 * np.pi * n * t)
        c = np.cos(2 * np.pi * n * t)
        shade = 1.0 + 0.28 * c * sharp + 0.06 * s
        return np.broadcast_to(shade[None, :], (H, W)).astype(np.float32), np.broadcast_to(s[None, :], (H, W))
    s2, ss = corrugation(H, W, n, "y", sharp)
    return s2.T, ss.T


def dirt_layer(img, mask, color="#2a221a", amt=0.7):
    return mix(img, color, clamp01(mask) * amt)
