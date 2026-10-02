"""Core helpers for the texture pipeline: registry, deterministic RNG, periodic noise,
image maths, drawing helpers and the PS1 / Puppet-Combo degradation pipeline.

All intermediate images are float32 numpy arrays in [0, 1], shape (H, W) for masks or
(H, W, 3) for colour. Images are row-major from the TOP-LEFT (PIL convention)."""
import io
import os
import zlib

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from scipy import ndimage as ndi
from scipy.spatial import cKDTree

HERE = os.path.dirname(os.path.abspath(__file__))
TOOL_DIR = os.path.dirname(HERE)
REPO = os.path.abspath(os.path.join(TOOL_DIR, "..", "..", ".."))
OUT_ROOT = os.path.join(REPO, "Assets", "PrisonersOfOmar", "Resources", "Textures")
FONT_DIR = os.path.join(TOOL_DIR, "fonts")

# --------------------------------------------------------------------------------------
# registry
# --------------------------------------------------------------------------------------
REGISTRY = []  # list of dicts


def texture(path, size, tile=False, alpha=None, k=None, **deg):
    """Register a texture builder.

    path   : relative to Resources/Textures without extension, e.g. "Env/wall_brick_basement"
    size   : (w, h) output size in pixels
    tile   : seamless tiling texture (periodic noise, wrap-mode filters)
    alpha  : None (RGB), "hard" (cutout 0/255), "soft" (8-bit alpha)
    k      : supersampling factor the builder works at (default 4, 8 for small textures)
    deg    : extra keyword args for degrade() (q, bits, dither, sharpen, desat, dark, raw ...)

    The builder gets a Ctx and returns either an RGB float array at (h*k, w*k), a tuple
    (rgb, alpha) or, for raw outputs, a PIL image already at the final size."""
    def deco(fn):
        w, h = size
        kk = k if k is not None else (8 if max(w, h) <= 64 else 4)
        REGISTRY.append(dict(path=path, size=(w, h), tile=tile, alpha=alpha, k=kk, fn=fn, deg=deg))
        return fn
    return deco


CURRENT_SRC = None  # Sources of the texture being built (used by helpers without a ctx)


class Ctx:
    def __init__(self, spec, sources):
        global CURRENT_SRC
        CURRENT_SRC = sources
        self.spec = spec
        self.name = spec["path"]
        self.k = spec["k"]
        self.w, self.h = spec["size"]
        self.W, self.H = self.w * self.k, self.h * self.k
        self.tile = spec["tile"]
        self.rng = rng_for(self.name)
        self.src = sources

    def sub(self, salt):
        return rng_for(self.name, salt)


def rng_for(name, salt=0):
    return np.random.default_rng(zlib.crc32(("%s#%d" % (name, salt)).encode()) & 0xFFFFFFFF)


# --------------------------------------------------------------------------------------
# small maths
# --------------------------------------------------------------------------------------
def clamp01(a):
    return np.clip(a, 0.0, 1.0)


def lerp(a, b, t):
    return a + (b - a) * t


def smoothstep(e0, e1, x):
    t = clamp01((x - e0) / (e1 - e0))
    return t * t * (3.0 - 2.0 * t)


def norm01(a):
    a = a - a.min()
    m = a.max()
    return a / m if m > 0 else a


def normz(a):
    s = a.std()
    return (a - a.mean()) / (s if s > 0 else 1.0)


def col(c):
    """'#rrggbb' or (r,g,b) 0-255 -> float array (3,)"""
    if isinstance(c, str):
        c = c.lstrip("#")
        c = tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))
    return np.array(c, dtype=np.float32) / 255.0


def solid(h, w, c):
    return np.ones((h, w, 3), np.float32) * col(c)[None, None, :]


def gradient_map(t, stops):
    """t: (H,W) float; stops: list of (pos, color) -> (H,W,3)"""
    pos = np.array([s[0] for s in stops], np.float32)
    cols = np.stack([col(s[1]) for s in stops])
    out = np.empty(t.shape + (3,), np.float32)
    for c in range(3):
        out[..., c] = np.interp(t, pos, cols[:, c])
    return out


def mix(base, layer, m):
    """base,layer (H,W,3) or color; m (H,W) mask"""
    if not isinstance(layer, np.ndarray) or layer.ndim == 1:
        layer = col(layer) if not isinstance(layer, np.ndarray) else layer
        layer = layer[None, None, :]
    return base + (layer - base) * m[..., None]


def mul(base, m):
    return base * m[..., None]


def lum(img):
    return img[..., 0] * 0.299 + img[..., 1] * 0.587 + img[..., 2] * 0.114


def saturate(img, s):
    l = lum(img)[..., None]
    return clamp01(l + (img - l) * s)


def blur(a, sigma, tile=False):
    if sigma <= 0:
        return a
    mode = "wrap" if tile else "reflect"
    if a.ndim == 3:
        return np.stack([ndi.gaussian_filter(a[..., c], sigma, mode=mode) for c in range(a.shape[2])], -1)
    return ndi.gaussian_filter(a, sigma, mode=mode)


def blur_xy(a, sy, sx, tile=False):
    mode = "wrap" if tile else "reflect"
    if a.ndim == 3:
        return np.stack([ndi.gaussian_filter(a[..., c], (sy, sx), mode=mode) for c in range(a.shape[2])], -1)
    return ndi.gaussian_filter(a, (sy, sx), mode=mode)


def resize(a, w, h, resample=Image.BOX):
    """float array resize via PIL 'F' mode per channel"""
    if a.ndim == 3:
        return np.stack([resize(a[..., c], w, h, resample) for c in range(a.shape[2])], -1)
    im = Image.fromarray(a.astype(np.float32), "F").resize((w, h), resample)
    return np.asarray(im, np.float32)


def downsample(a, oh, ow):
    h, w = a.shape[:2]
    if h % oh == 0 and w % ow == 0:
        fy, fx = h // oh, w // ow
        if a.ndim == 3:
            return a.reshape(oh, fy, ow, fx, a.shape[2]).mean(axis=(1, 3))
        return a.reshape(oh, fy, ow, fx).mean(axis=(1, 3))
    return resize(a, ow, oh, Image.BOX)


def outer_rim(m, px, tile=False):
    """soft band just OUTSIDE a mask's edge (drop shadows, frames, halos)"""
    m = clamp01(m)
    return clamp01(blur(m, px, tile) * 2.0) * (1.0 - m)


def inner_rim(m, px, tile=False):
    """soft band just INSIDE a mask's edge (bevels, rim light, dark plank edges)"""
    m = clamp01(m)
    return m * clamp01((1.0 - blur(m, px, tile)) * 2.0)


def warp(a, dx, dy, tile=False, order=1):
    """sample a at (y+dy, x+dx); dx,dy (H,W) displacement fields in pixels"""
    h, w = a.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    coords = [yy + dy, xx + dx]
    mode = "grid-wrap" if tile else "reflect"
    if a.ndim == 3:
        return np.stack([ndi.map_coordinates(a[..., c], coords, order=order, mode=mode) for c in range(a.shape[2])], -1)
    return ndi.map_coordinates(a, coords, order=order, mode=mode)


# --------------------------------------------------------------------------------------
# periodic noise
# --------------------------------------------------------------------------------------
def fft_noise(rng, h, w, beta=2.0, ax=1.0, ay=1.0, fmin=0.0, fmax=None):
    """Periodic 1/f^beta noise, zero mean unit variance. ax/ay > 1 stretch features along
    that axis' perpendicular (ay large => features elongated vertically)."""
    white = rng.standard_normal((h, w)).astype(np.float32)
    F = np.fft.fft2(white)
    n = max(h, w)
    fy = np.fft.fftfreq(h)[:, None] * n * ay  # cycles per image (larger side)
    fx = np.fft.fftfreq(w)[None, :] * n * ax
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1.0
    amp = f ** (-beta / 2.0)
    amp[0, 0] = 0.0
    if fmin > 0:
        amp *= smoothstep(fmin * 0.5, fmin, f)
    if fmax is not None:
        amp *= 1.0 - smoothstep(fmax, fmax * 2.0, f)
    out = np.real(np.fft.ifft2(F * amp)).astype(np.float32)
    return normz(out)


def fbm(rng, h, w, beta=2.6, fmin=0.0, **kw):
    """Smooth fractal noise (periodic). fmin in cycles per image removes features larger than 1/fmin."""
    return fft_noise(rng, h, w, beta=beta, fmin=fmin, **kw)


def blob_noise(rng, h, w, sigma, tile=True):
    n = blur(rng.standard_normal((h, w)).astype(np.float32), sigma, tile)
    return normz(n)


def white(rng, h, w):
    return rng.random((h, w)).astype(np.float32)


def worley(rng, h, w, n, tile=True, k=2):
    """Periodic cellular noise. Returns (F1, F2, cell_id) distance arrays in pixels."""
    pts = rng.random((n, 2)) * [w, h]
    if tile:
        tree = cKDTree(pts, boxsize=[w, h])
    else:
        tree = cKDTree(pts)
    yy, xx = np.mgrid[0:h, 0:w]
    q = np.stack([xx.ravel() + 0.5, yy.ravel() + 0.5], -1)
    if tile:
        q = np.mod(q, [w, h])
    d, i = tree.query(q, k=k)
    F1 = d[:, 0].reshape(h, w).astype(np.float32)
    F2 = d[:, 1].reshape(h, w).astype(np.float32)
    ids = i[:, 0].reshape(h, w)
    return F1, F2, ids


def bayer4():
    m = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], np.float32)
    return (m + 0.5) / 16.0 - 0.5


# --------------------------------------------------------------------------------------
# photo helpers
# --------------------------------------------------------------------------------------
def make_tileable(img, rng, band=0.32, soft=0.05, rough=0.12):
    """Two pass offset-and-blend: periodic result, original content kept in the middle,
    transitions are irregular (noise-perturbed) cut lines with soft edges."""
    h, w = img.shape[:2]
    out = img

    def pass_axis(a, axis):
        n = a.shape[axis]
        rolled = np.roll(a, n // 2, axis=axis)
        t = np.abs(np.linspace(-1.0, 1.0, n, dtype=np.float32))  # 0 centre .. 1 edge
        nz = fft_noise(rng, h, w, beta=2.4) * rough
        if axis == 1:
            d = t[None, :] + nz * (1.0 - t[None, :] ** 8)
        else:
            d = t[:, None] + nz * (1.0 - t[:, None] ** 8)
        m = 1.0 - smoothstep(1.0 - band - soft, 1.0 - band + soft, d)
        edge = (t > 0.985)
        if axis == 1:
            m[:, edge] = 0.0
        else:
            m[edge, :] = 0.0
        if a.ndim == 3:
            m = m[..., None]
        return a * m + rolled * (1.0 - m)

    out = pass_axis(out, 1)
    out = pass_axis(out, 0)
    return out


def highpass(a, sigma, tile=False):
    return a - blur(a, sigma, tile)


def crop_resize(a, box, w, h, resample=Image.BOX):
    """box = (x0,y0,x1,y1) in source pixels"""
    x0, y0, x1, y1 = [int(round(v)) for v in box]
    return resize(a[y0:y1, x0:x1], w, h, resample)


def photo_detail(src, w, h, rng, box=None, sigma=None, tileable=True, rot=0, flip=False):
    """Zero-mean, unit-variance high-pass detail from a grayscale photo, resized to (w,h)."""
    a = src
    if rot:
        a = np.rot90(a, rot)
    if flip:
        a = a[:, ::-1]
    if box is not None:
        a = a[box[1]:box[3], box[0]:box[2]]
    a = resize(np.ascontiguousarray(a), w, h, Image.BICUBIC if a.shape[1] < w else Image.BOX)
    if sigma is None:
        sigma = max(w, h) / 16.0
    d = highpass(a, sigma)
    if tileable:
        d = make_tileable(d, rng)
    return normz(d)


# --------------------------------------------------------------------------------------
# drawing helpers (PIL on 'L' canvases -> float masks)
# --------------------------------------------------------------------------------------
def canvas(w, h, v=0):
    im = Image.new("L", (w, h), v)
    return im, ImageDraw.Draw(im)


def to_mask(im):
    return np.asarray(im, np.float32) / 255.0


def wrap_offsets(w, h, tile):
    if not tile:
        return [(0, 0)]
    return [(ox, oy) for ox in (-w, 0, w) for oy in (-h, 0, h)]


def font(name, size):
    return ImageFont.truetype(os.path.join(FONT_DIR, name), size)


FONT_PIXEL = "VT323-Regular.woff"
FONT_BOLD = "Anton-Regular.woff"


def text_mask(w, h, lines, fnt, fill=255, align="center", spacing=0, box=None, stretch=None):
    """Draw text lines centred into a w x h 'L' canvas (box = (x0,y0,x1,y1) region)."""
    if box is None:
        box = (0, 0, w, h)
    bw, bh = box[2] - box[0], box[3] - box[1]
    # measure first (uniform line pitch from the font, not per-line ink), then render and fit
    probe = ImageDraw.Draw(Image.new("L", (8, 8), 0))
    ref = probe.textbbox((0, 0), "AgjpqyÅ|", font=fnt)
    pitch = ref[3] - ref[1]
    widths = [(probe.textbbox((0, 0), ln, font=fnt)[2] - probe.textbbox((0, 0), ln, font=fnt)[0]) if ln else 0 for ln in lines]
    maxw = max(widths + [1])
    total_h = pitch * len(lines) + spacing * (len(lines) - 1)
    tmp = Image.new("L", (int(maxw + 40), int(total_h + 40)), 0)
    d = ImageDraw.Draw(tmp)
    y = 0
    for ln, lw in zip(lines, widths):
        if ln:
            bb = d.textbbox((0, 0), ln, font=fnt)
            if align == "center":
                x = (maxw - lw) / 2 - bb[0]
            elif align == "left":
                x = -bb[0]
            else:
                x = maxw - lw - bb[0]
            d.text((x + 20, y - ref[1] + 20), ln, font=fnt, fill=fill)
        y += pitch + spacing
    content = tmp
    content = content.crop(content.getbbox() or (0, 0, 1, 1))
    cw, ch = content.size
    if stretch is None:
        s = min(bw / cw, bh / ch)
        nw, nh = max(1, int(cw * s)), max(1, int(ch * s))
    else:
        nw, nh = bw, bh
    content = content.resize((nw, nh), Image.LANCZOS)
    out = Image.new("L", (w, h), 0)
    out.paste(content, (box[0] + (bw - nw) // 2, box[1] + (bh - nh) // 2))
    return to_mask(out)


def poly_mask(w, h, polys, tile=False, blur_px=0):
    im, d = canvas(w, h)
    for p in polys:
        for ox, oy in wrap_offsets(w, h, tile):
            d.polygon([(x + ox, y + oy) for x, y in p], fill=255)
    m = to_mask(im)
    return blur(m, blur_px, tile) if blur_px else m


def rect_mask(w, h, rects, tile=False):
    im, d = canvas(w, h)
    for r in rects:
        d.rectangle(r, fill=255)
    return to_mask(im)


def lines_mask(w, h, polylines, width, tile=False):
    """polylines: list of point lists"""
    im, d = canvas(w, h)
    for pl in polylines:
        for ox, oy in wrap_offsets(w, h, tile):
            d.line([(x + ox, y + oy) for x, y in pl], fill=255, width=int(max(1, width)), joint="curve")
    return to_mask(im)


def ellipse_mask(w, h, cx, cy, rx, ry, soft=0.0):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((xx + 0.5 - cx) / rx) ** 2 + ((yy + 0.5 - cy) / ry) ** 2)
    if soft <= 0:
        return (d <= 1.0).astype(np.float32)
    return 1.0 - smoothstep(1.0 - soft, 1.0, d)


def radial(w, h, cx=None, cy=None, r=None):
    cx = w / 2 if cx is None else cx
    cy = h / 2 if cy is None else cy
    r = min(w, h) / 2 if r is None else r
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    return np.sqrt((xx + 0.5 - cx) ** 2 + (yy + 0.5 - cy) ** 2) / r


def edge_distance(mask, tile=False):
    """distance (px) from each inside pixel to the outside of a binary mask"""
    m = mask > 0.5
    if tile:
        h, w = m.shape
        big = np.tile(m, (3, 3))
        d = ndi.distance_transform_edt(big)[h:2 * h, w:2 * w]
        return d.astype(np.float32)
    return ndi.distance_transform_edt(np.pad(m, 1))[1:-1, 1:-1].astype(np.float32)


# --------------------------------------------------------------------------------------
# grime library
# --------------------------------------------------------------------------------------
def grime_mask(rng, h, w, cover=0.4, beta=2.8, sharp=0.25, ax=1.0, ay=1.0, detail=None, damt=0.35):
    """blotchy dirt mask 0..1 (fractal threshold). `detail` (zero-mean photo high-pass) breaks
    up the edges photographically."""
    n = fft_noise(rng, h, w, beta=beta, ax=ax, ay=ay)
    if detail is not None:
        n = n + damt * detail
    thr = np.quantile(n, 1.0 - cover)
    return smoothstep(thr - sharp, thr + sharp, n)


def drips(rng, h, w, n, length=(0.2, 0.8), width=(1.0, 4.0), tile=True, from_top=None):
    """vertical dirt / rust streak mask"""
    m = np.zeros((h, w), np.float32)
    yy = np.arange(h, dtype=np.float32)
    for _ in range(n):
        x = rng.uniform(0, w)
        y0 = rng.uniform(0, h) if from_top is None else from_top * h + rng.uniform(-0.05, 0.05) * h
        L = rng.uniform(*length) * h
        wd = rng.uniform(*width)
        strength = rng.uniform(0.4, 1.0)
        dy = (yy - y0) % h if tile else (yy - y0)
        prof = np.where((dy >= 0) & (dy < L), np.clip(1.0 - dy / L, 0, 1) ** 1.3, 0.0) * strength
        # meander
        xoff = np.cumsum(rng.normal(0, 0.15, h)).astype(np.float32)
        xoff = np.roll(xoff - xoff.mean(), int(y0))
        xs = np.arange(w, dtype=np.float32)
        dx = xs[None, :] - (x + xoff[:, None])
        if tile:
            dx = (dx + w / 2) % w - w / 2
        cols = np.exp(-(dx / wd) ** 2)
        m = np.maximum(m, cols * prof[:, None])
    return m


def scratches_mask(rng, h, w, n, length=(10, 60), angle=None, width=1, tile=True):
    im, d = canvas(w, h)
    for _ in range(n):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        a = rng.uniform(0, np.pi) if angle is None else angle + rng.normal(0, 0.25)
        L = rng.uniform(*length)
        pts = []
        steps = 6
        for s in range(steps + 1):
            t = s / steps
            pts.append((x + np.cos(a) * L * t + rng.normal(0, 0.6), y + np.sin(a) * L * t + rng.normal(0, 0.6)))
        v = int(rng.uniform(120, 255))
        for ox, oy in wrap_offsets(w, h, tile):
            d.line([(px + ox, py + oy) for px, py in pts], fill=v, width=width)
    return to_mask(im)


def water_stains(rng, h, w, n=3, rmin=0.15, rmax=0.4, tile=True, ring_w=0.035):
    """returns (fill, ring) masks: stained area and darker tide-line ring"""
    fill = np.zeros((h, w), np.float32)
    ring = np.zeros((h, w), np.float32)
    nz = fft_noise(rng, h, w, beta=2.2)
    for i in range(n):
        cx, cy = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(rmin, rmax) * min(w, h)
        yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
        dx = xx - cx
        dy = (yy - cy) * rng.uniform(0.7, 1.3)
        if tile:
            dx = (dx + w / 2) % w - w / 2
            dy = (dy + h / 2) % h - h / 2
        d = np.sqrt(dx * dx + dy * dy) / r + nz * 0.18
        inside = smoothstep(1.02, 0.95, d)
        rg = np.exp(-((d - 0.98) / ring_w) ** 2)
        # secondary inner rings
        rg += 0.5 * np.exp(-((d - 0.75) / (ring_w * 0.85)) ** 2) * (rng.random() < 0.7)
        fill = np.maximum(fill, inside * rng.uniform(0.5, 1.0) * clamp01(0.35 + 0.65 * d))
        ring = np.maximum(ring, rg)
    return clamp01(fill), clamp01(ring)


def cracks_mask(rng, h, w, n=6, seg=(8, 30), width=2, tile=True, branch=0.3, steps=12):
    im, d = canvas(w, h)

    def crack(x, y, a, depth, nsteps, wd):
        pts = [(x, y)]
        for s in range(nsteps):
            a += rng.normal(0, 0.45)
            L = rng.uniform(*seg)
            x, y = x + np.cos(a) * L, y + np.sin(a) * L
            pts.append((x, y))
            if depth < 2 and rng.random() < branch:
                crack(x, y, a + rng.choice([-1, 1]) * rng.uniform(0.5, 1.2), depth + 1, max(2, nsteps // 2), max(1, wd - 1))
        for ox, oy in wrap_offsets(w, h, tile):
            d.line([(px + ox, py + oy) for px, py in pts], fill=255, width=int(wd), joint="curve")

    for _ in range(n):
        crack(rng.uniform(0, w), rng.uniform(0, h), rng.uniform(0, 2 * np.pi), 0, steps, width)
    return to_mask(im)


def splat_mask(rng, w, h, cx, cy, r, drops=20, tile=False, irregular=0.35):
    """irregular blob with satellite droplets (blood / paint)"""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    nz = fft_noise(rng, h, w, beta=2.0)
    dx, dy = xx - cx, yy - cy
    if tile:
        dx = (dx + w / 2) % w - w / 2
        dy = (dy + h / 2) % h - h / 2
    ang = np.arctan2(dy, dx)
    lobes = 1.0 + irregular * (np.sin(ang * rng.integers(3, 7) + rng.uniform(0, 6)) * 0.5
                               + np.sin(ang * rng.integers(7, 13) + rng.uniform(0, 6)) * 0.3)
    d = np.sqrt(dx * dx + dy * dy) / (r * lobes) + nz * 0.08
    m = (d < 1.0).astype(np.float32)
    im = Image.fromarray((m * 255).astype(np.uint8))
    dr = ImageDraw.Draw(im)
    for _ in range(drops):
        a = rng.uniform(0, 2 * np.pi)
        dist = r * rng.uniform(1.0, 2.6)
        rr = max(1.0, r * rng.uniform(0.03, 0.14) * (2.6 - dist / r))
        px, py = cx + np.cos(a) * dist, cy + np.sin(a) * dist
        dr.ellipse([px - rr, py - rr, px + rr, py + rr], fill=255)
        if rng.random() < 0.5:  # streak toward centre
            dr.line([(px, py), (cx + np.cos(a) * r * 0.9, cy + np.sin(a) * r * 0.9)], fill=255, width=max(1, int(rr * 0.8)))
    return to_mask(im)


# --------------------------------------------------------------------------------------
# lighting / photo feel
# --------------------------------------------------------------------------------------
def uneven_light(rng, h, w, amount=0.18, tile=True, beta=3.2):
    """low-frequency multiplicative light variation (periodic if tile)"""
    n = fft_noise(rng, h, w, beta=beta)
    return 1.0 + n * amount


def vignette(h, w, strength=0.4, power=2.0):
    r = radial(w, h, r=np.hypot(w, h) / 2)
    return 1.0 - strength * clamp01(r) ** power


def photo_grain(rng, h, w, amount=0.04, sigma=0.6):
    g = rng.standard_normal((h, w)).astype(np.float32)
    if sigma > 0:
        g = ndi.gaussian_filter(g, sigma, mode="wrap")
        g = normz(g)
    return g * amount


# --------------------------------------------------------------------------------------
# degradation pipeline
# --------------------------------------------------------------------------------------
def bleed_rgb(rgb, a, iters=8):
    """fill colour of transparent pixels from nearest opaque ones (avoids dark fringes)"""
    m = a > 0.5
    if m.all() or not m.any():
        return rgb
    idx = ndi.distance_transform_edt(~m, return_distances=False, return_indices=True)
    return rgb[idx[0], idx[1]]


def jpeg_roundtrip(rgb, q):
    u8 = (clamp01(rgb) * 255.0 + 0.5).astype(np.uint8)
    buf = io.BytesIO()
    Image.fromarray(u8, "RGB").save(buf, "JPEG", quality=int(q), subsampling=2, optimize=False)
    buf.seek(0)
    return np.asarray(Image.open(buf).convert("RGB"), np.float32) / 255.0


def degrade(rgb, size, tile=False, alpha=None, alpha_mode=None, q=38, bits=5, dither=0.35,
            sharpen=0.5, desat=0.12, dark=0.92, maxv=0.9, jpeg=True, gamma=1.0, alpha_thr=0.5,
            **_):
    """The Puppet Combo look: box downscale -> unsharp -> posterize (+bayer) -> JPEG crunch ->
    desaturate/darken. Returns PIL Image (RGB or RGBA)."""
    w, h = size
    x = downsample(rgb.astype(np.float32), h, w)
    a = None
    if alpha is not None:
        a = downsample(alpha.astype(np.float32), h, w)
        if alpha_mode == "hard":
            a = (a >= alpha_thr).astype(np.float32)
        # un-premultiply-ish: colour where alpha exists from weighted average
        if alpha_mode == "hard":
            wsum = downsample(alpha.astype(np.float32), h, w)
            prem = downsample(rgb.astype(np.float32) * alpha[..., None], h, w)
            x = np.where(wsum[..., None] > 1e-4, prem / np.maximum(wsum[..., None], 1e-4), x)
    if sharpen > 0:
        bl = blur(x, 0.8, tile)
        x = x + (x - bl) * sharpen
    x = clamp01(x)
    if gamma != 1.0:
        x = x ** gamma
    if bits and bits < 8:
        levels = (1 << bits) - 1
        dm = np.tile(bayer4(), (h // 4 + 1, w // 4 + 1))[:h, :w][..., None]
        x = np.floor(x * levels + 0.5 + dm * dither) / levels
        x = clamp01(x)
    if a is not None:
        x = bleed_rgb(x, a if alpha_mode == "hard" else (a > 0.02).astype(np.float32))
    if jpeg and q:
        if tile:
            # compress a 3x3 tiled copy and keep the centre so block / chroma context wraps
            big = np.tile(x, (3, 3, 1))
            x = jpeg_roundtrip(big, q)[h:2 * h, w:2 * w]
        else:
            x = jpeg_roundtrip(x, q)
    if desat:
        x = saturate(x, 1.0 - desat)
    x = clamp01(x * dark)
    if maxv is not None:
        x = np.minimum(x, maxv)
    u8 = (x * 255.0 + 0.5).astype(np.uint8)
    if a is not None:
        au8 = (clamp01(a) * 255.0 + 0.5).astype(np.uint8)
        if alpha_mode == "hard":
            au8 = np.where(au8 >= 128, 255, 0).astype(np.uint8)
        return Image.fromarray(np.dstack([u8, au8]), "RGBA")
    return Image.fromarray(u8, "RGB")


def to_image_rgba(rgb, a):
    u8 = (clamp01(rgb) * 255.0 + 0.5).astype(np.uint8)
    au8 = (clamp01(a) * 255.0 + 0.5).astype(np.uint8)
    return Image.fromarray(np.dstack([u8, au8]), "RGBA")


def to_image_rgb(rgb):
    return Image.fromarray((clamp01(rgb) * 255.0 + 0.5).astype(np.uint8), "RGB")
