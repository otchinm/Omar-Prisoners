"""Small numpy painting toolkit (float RGB images in [0,1], shape (H, W, 3), row 0 = top)."""
import io

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

import photos


def rgb(hexstr):
    hexstr = hexstr.lstrip("#")
    return np.array([int(hexstr[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32)


def fill(h, w, color):
    return np.ones((h, w, 3), np.float32) * np.asarray(color, np.float32)


def uv_grid(h, w):
    """u to the right, v up, both in [0,1] at pixel centers."""
    u = (np.arange(w, dtype=np.float32) + 0.5) / w
    v = 1.0 - (np.arange(h, dtype=np.float32) + 0.5) / h
    return np.meshgrid(u, v)


def value_noise(h, w, cells_y, cells_x, rng, tile_x=False, tile_y=False, order=3):
    """Smooth noise in [0,1] with roughly cells_x * cells_y features."""
    cy, cx = max(2, int(cells_y)), max(2, int(cells_x))
    g = rng.rand(cy + 3, cx + 3).astype(np.float32)
    if tile_x:
        g[:, cx:] = g[:, :3]
    if tile_y:
        g[cy:, :] = g[:3, :]
    z = ndimage.zoom(g, ((h * (cy + 3) / cy) / (cy + 3), (w * (cx + 3) / cx) / (cx + 3)), order=order, mode="grid-wrap")
    z = z[:h, :w]
    if z.shape != (h, w):
        z = np.pad(z, ((0, h - z.shape[0]), (0, w - z.shape[1])), mode="edge")
    return np.clip(z, 0, 1)


def fbm(h, w, base_cells, rng, octaves=4, persistence=0.5, tile_x=False):
    acc = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    cells = base_cells
    for _ in range(octaves):
        acc += amp * value_noise(h, w, cells * h / max(h, w), cells * w / max(h, w), rng, tile_x=tile_x)
        tot += amp
        amp *= persistence
        cells *= 2
    return acc / tot


def photo_detail(name, h, w, rng, zoom=1.0, sigma=5.0, crop=None, aniso=None):
    """High-pass luminance of a real photo, normalized to mean 0 / std 1, resampled to (h, w).
    crop = (x0, y0, x1, y1) fractions of the photo; zoom > 1 magnifies."""
    g = photos.gray(name)
    H, W = g.shape
    if crop is not None:
        x0, y0, x1, y1 = crop
        g = g[int(y0 * H):int(y1 * H), int(x0 * W):int(x1 * W)]
        H, W = g.shape
    # random sub-window according to zoom
    sh, sw = int(min(H, H / zoom)), int(min(W, W / zoom))
    oy = rng.randint(0, max(1, H - sh + 1))
    ox = rng.randint(0, max(1, W - sw + 1))
    g = g[oy:oy + sh, ox:ox + sw]
    hp = g - ndimage.gaussian_filter(g, sigma)
    if aniso is not None:
        hp = ndimage.gaussian_filter(hp, aniso)
    im = Image.fromarray(hp.astype(np.float32), mode="F").resize((w, h), Image.BILINEAR)
    a = np.asarray(im, dtype=np.float32)
    a = a - a.mean()
    s = a.std()
    return a / s if s > 1e-6 else a


def photo_color(name, h, w, rng, zoom=1.0, crop=None):
    a = photos.photo(name)
    H, W, _ = a.shape
    if crop is not None:
        x0, y0, x1, y1 = crop
        a = a[int(y0 * H):int(y1 * H), int(x0 * W):int(x1 * W)]
        H, W, _ = a.shape
    sh, sw = int(min(H, H / zoom)), int(min(W, W / zoom))
    oy = rng.randint(0, max(1, H - sh + 1))
    ox = rng.randint(0, max(1, W - sw + 1))
    a = a[oy:oy + sh, ox:ox + sw]
    im = Image.fromarray((a * 255).astype(np.uint8)).resize((w, h), Image.BILINEAR)
    return np.asarray(im, dtype=np.float32) / 255.0


def soft_ellipse(h, w, cx, cy, rx, ry, soft=1.5, angle=0.0):
    """Mask of an ellipse in pixel coordinates (cx, cy from top-left), soft edge in pixels."""
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    x = x + 0.5 - cx
    y = y + 0.5 - cy
    if angle:
        c, s = np.cos(angle), np.sin(angle)
        x, y = x * c + y * s, -x * s + y * c
    d = np.sqrt((x / max(rx, 1e-3)) ** 2 + (y / max(ry, 1e-3)) ** 2)
    r = (rx + ry) * 0.5
    return np.clip((1.0 - d) * r / max(soft, 1e-3) + 0.5, 0, 1)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def mix(base, color, mask):
    m = np.clip(mask, 0, 1)[..., None]
    return base * (1 - m) + np.asarray(color, np.float32) * m


def mul(base, factor):
    return base * np.asarray(factor, np.float32)[..., None] if np.ndim(factor) == 2 else base * factor


def blur(img, s):
    if img.ndim == 2:
        return ndimage.gaussian_filter(img, s)
    return np.stack([ndimage.gaussian_filter(img[..., c], s) for c in range(img.shape[2])], -1)


def shade(base, amount):
    """Multiply luminance: amount (2D array) of 1 = unchanged."""
    return base * np.clip(amount, 0, 3)[..., None]


def draw_mask(h, w, fn):
    """fn(ImageDraw) draws in white on a black L image of size (w, h); returns float mask."""
    im = Image.new("L", (w, h), 0)
    fn(ImageDraw.Draw(im))
    return np.asarray(im, dtype=np.float32) / 255.0


def font(size):
    try:
        return ImageFont.load_default(size=size)
    except TypeError:
        return ImageFont.load_default()


def text_mask(h, w, text, cx, cy, size, stroke=0, spacing=0, stretch_x=1.0):
    """Centered text mask (bundled Pillow font for determinism)."""
    f = font(size)
    big_w = int(w / max(stretch_x, 0.1)) + 4
    im = Image.new("L", (big_w, h), 0)
    d = ImageDraw.Draw(im)
    bbox = d.multiline_textbbox((0, 0), text, font=f, stroke_width=stroke, spacing=spacing, align="center")
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    d.multiline_text((cx / stretch_x - tw / 2 - bbox[0], cy - th / 2 - bbox[1]), text, font=f, fill=255,
                     stroke_width=stroke, stroke_fill=255, spacing=spacing, align="center")
    if stretch_x != 1.0:
        im = im.resize((int(big_w * stretch_x), h), Image.BILINEAR)
    a = np.asarray(im, dtype=np.float32)[:, :w] / 255.0
    if a.shape[1] < w:
        a = np.pad(a, ((0, 0), (0, w - a.shape[1])))
    return a


def downsample(img, f):
    """Area-average downsample by integer factor f."""
    if f == 1:
        return img
    h, w = img.shape[0] // f, img.shape[1] // f
    img = img[:h * f, :w * f]
    if img.ndim == 2:
        return img.reshape(h, f, w, f).mean(axis=(1, 3))
    return img.reshape(h, f, w, f, img.shape[2]).mean(axis=(1, 3))


def desaturate(img, amount):
    g = (img[..., 0] * 0.3 + img[..., 1] * 0.59 + img[..., 2] * 0.11)[..., None]
    return img * (1 - amount) + g * amount


def degrade(img, rng, bits=5, jpeg_q=55, desat=0.12, contrast=1.0, gamma=1.0):
    """PS1 / VHS degrade: desaturate, posterize, JPEG crunch."""
    img = np.clip(img, 0, 1)
    img = desaturate(img, desat)
    if contrast != 1.0:
        img = np.clip((img - 0.5) * contrast + 0.5, 0, 1)
    if gamma != 1.0:
        img = img ** gamma
    # ordered-ish dither noise before posterize
    levels = (1 << bits) - 1
    noise = (rng.rand(*img.shape[:2]).astype(np.float32)[..., None] - 0.5) / levels * 0.9
    img = np.round(np.clip(img + noise, 0, 1) * levels) / levels
    im = Image.fromarray((img * 255 + 0.5).astype(np.uint8))
    buf = io.BytesIO()
    im.save(buf, "JPEG", quality=jpeg_q, subsampling=2)
    buf.seek(0)
    out = np.asarray(Image.open(buf).convert("RGB"), dtype=np.float32) / 255.0
    out = np.round(out * levels) / levels
    return out


def to_image(img, alpha=None):
    a = (np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)
    if alpha is None:
        return Image.fromarray(a, "RGB")
    al = (np.clip(alpha, 0, 1) >= 0.5).astype(np.uint8) * 255
    return Image.fromarray(np.dstack([a, al]), "RGBA")


# Patch-free cloth crops: astronaut suit sleeve (orange nylon folds) and the cameraman's wool coat.
CLOTH_SOURCES = [("astronaut", (0.03, 0.72, 0.25, 1.0)), ("camera", (0.02, 0.45, 0.32, 0.98))]


def fold_layer(h, w, rng, scale=1.0):
    """Cloth creases: real photo high-pass (random patch-free crop) + procedural ridge creases. ~zero mean, unit-ish std."""
    name, crop = CLOTH_SOURCES[rng.randint(0, len(CLOTH_SOURCES))]
    f = photo_detail(name, h, w, rng, zoom=1.0 * scale, sigma=max(2.0, h / 20), crop=crop)
    # procedural creases: ridges of warped, stretched noise
    n = fbm(h, max(4, w // 3), 3 * scale, rng, octaves=3)
    n = np.asarray(Image.fromarray(n.astype(np.float32), mode="F").resize((w, h), Image.BILINEAR))
    ridge = 1.0 - np.abs(2.0 * n - 1.0)
    ridge = (ridge - ridge.mean()) / (ridge.std() + 1e-6)
    out = f * 0.65 + ridge * 0.55
    return (out - out.mean()) / (out.std() + 1e-6)


def fabric(h, w, color, rng, folds=0.18, grain=0.06, fold_scale=1.0, fold_src=None,
           weave=0.0, stains=0.0, stain_color=None, sweat=0.0):
    """Cloth with real photographic folds (high-pass of a cloth photo) + cotton grain + stains."""
    base = fill(h, w, color)
    lum = np.ones((h, w), np.float32)
    if folds:
        if fold_src is None:
            f = fold_layer(h, w, rng, fold_scale)
        else:
            name, crop = fold_src
            f = photo_detail(name, h, w, rng, zoom=1.2 * fold_scale, sigma=max(2.0, h / 24), crop=crop)
        lum += np.tanh(f * 0.8) * folds
    if grain:
        g = photo_detail("grass", h, w, rng, zoom=3.0, sigma=1.5)
        lum += g * grain * 0.5
        lum += (rng.rand(h, w).astype(np.float32) - 0.5) * grain
    if weave:
        y, x = np.mgrid[0:h, 0:w]
        lum += weave * (((x // 2 + y // 2) % 2) - 0.5)
    base = shade(base, lum)
    if stains:
        n = fbm(h, w, 5, rng, octaves=4)
        m = smoothstep(0.62, 0.8, n) * stains
        sc = stain_color if stain_color is not None else np.asarray(color) * 0.55
        base = mix(base, sc, m * 0.7)
    if sweat:
        n = fbm(h, w, 3, rng, octaves=3)
        base = shade(base, 1 - smoothstep(0.55, 0.75, n) * sweat * 0.25)
    return base


def skin(h, w, tone, rng, mottle=0.06, pores=0.035, redness=0.0):
    base = fill(h, w, tone)
    n = fbm(h, w, 6, rng, octaves=4)
    lum = 1 + (n - 0.5) * mottle * 2
    p = photo_detail("gravel", h, w, rng, zoom=4.0, sigma=1.2)
    lum += p * pores
    m = photo_detail("ihc", h, w, rng, zoom=1.5, sigma=8)
    lum += m * mottle * 0.4
    base = shade(base, lum)
    if redness:
        r = fbm(h, w, 4, rng, octaves=3)
        base = mix(base, np.asarray(tone) * np.array([1.08, 0.82, 0.8]), smoothstep(0.5, 0.8, r) * redness)
    return base


def hair_strands(h, w, color, rng, highlight=None, direction="v", contrast=0.35, coarse=1.0, wave=0.0, sheen=0.25):
    """Hair: fine strands (>= 1 texel wide after the 4x downsample), clumps, waviness, a sheen band, photo grain."""
    if direction != "v":
        out = hair_strands(w, h, color, rng, highlight, "v", contrast, coarse, wave, sheen)
        return np.transpose(out, (1, 0, 2))
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    # x displaced by waves / small wobble so strands are not perfectly straight
    wob = fbm(h, w, 3, rng, octaves=2) * 6.0 * coarse
    xs = xx + wob + wave * np.sin(yy / max(h, 1) * np.pi * 6.0 + xx / max(w, 1) * 3.0) * 7.0
    # fine strands: random value per 3..5 px column band (at 4x), sampled at displaced x
    n_cols = int(w / (3.5 * coarse)) + 8
    col_vals = rng.rand(n_cols).astype(np.float32)
    idx = np.clip((xs / (3.5 * coarse)).astype(int) % n_cols, 0, n_cols - 1)
    fine = col_vals[idx] - 0.5
    # strands fade in / out along their length
    seg = rng.rand(n_cols).astype(np.float32)
    fine *= 0.6 + 0.4 * np.sin(yy / h * np.pi * (2 + 3 * seg[idx]) + seg[idx] * 6.28)
    # clumps: wider bands
    n_cl = int(w / (14 * coarse)) + 4
    cl_vals = rng.rand(n_cl).astype(np.float32)
    clump = cl_vals[np.clip((xs / (14 * coarse)).astype(int) % n_cl, 0, n_cl - 1)] - 0.5
    clump = blur(clump, 1.5)
    grain = photo_detail("chelsea", h, w, rng, zoom=2.0, sigma=2.0, crop=(0.0, 0.55, 1.0, 1.0))
    grain = blur(grain, (2.0, 0.3)) if False else grain
    lum = 1.0 + contrast * (fine * 1.1 + clump * 0.9) + grain * 0.04
    # dark gaps between clumps
    lum -= 0.25 * contrast * (np.abs(clump) > 0.42)
    base = fill(h, w, color)
    base = shade(base, lum)
    if sheen:
        band = np.exp(-((yy / h - 0.72) / 0.07) ** 2) * (0.6 + 0.4 * np.sin(xs / w * 9.0))
        base = mix(base, np.clip(np.asarray(color) * 1.7 + 0.08, 0, 1), band * sheen)
    if highlight is not None:
        n = fbm(h, w, 3, rng, octaves=3)
        hl_mask = smoothstep(0.5, 0.8, n + fine * 0.3) * 0.55
        base = mix(base, highlight, hl_mask)
    return base


def denim(h, w, color, rng, fade=0.25):
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    twill = np.sin((x + y) * 2.2) * 0.5 + 0.5
    base = fabric(h, w, color, rng, folds=0.22, grain=0.08)
    base = shade(base, 0.92 + twill * 0.16)
    n = fbm(h, w, 4, rng, octaves=3)
    base = mix(base, np.asarray(color) * 1.6 + 0.08, smoothstep(0.5, 0.85, n) * fade)
    return base


def plaid(h, w, rng, period=56, colors=("#8a1418", "#140e10", "#4a0a0e", "#c0383a"), line="#d8c8b0", folds=0.22):
    """Red / black flannel check: red ground, black bands, thin light threads."""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    jitter = fbm(h, w, 6, rng, octaves=2) * 2.0
    px = ((xx + jitter) % period) / period
    py = ((yy + jitter) % period) / period
    bx = (px > 0.08) & (px < 0.46)          # black vertical band
    by = (py > 0.08) & (py < 0.46)          # black horizontal band
    c_red = rgb(colors[0]); c_blk = rgb(colors[1]); c_dark = rgb(colors[2]); c_hi = rgb(colors[3])
    out = np.zeros((h, w, 3), np.float32)
    out[:] = c_red
    out[bx ^ by] = c_dark
    out[bx & by] = c_blk
    thin = ((np.abs(px - 0.73) < 0.025) | (np.abs(py - 0.73) < 0.025)) & ~(bx | by)
    out[thin] = c_hi
    light = (np.abs(px - 0.27) < 0.012) | (np.abs(py - 0.27) < 0.012)
    out[light] = out[light] * 0.55 + rgb(line) * 0.45
    out = blur(out, 0.9)
    f = fold_layer(h, w, rng, 1.0)
    out = shade(out, 1 + np.tanh(f * 0.8) * folds + (rng.rand(h, w).astype(np.float32) - 0.5) * 0.14)
    return out


def blood(h, w, rng, amount=0.3, scale=6, dark=0.6, splatter=0.0):
    """Returns blood mask (0..1)."""
    n = fbm(h, w, scale, rng, octaves=5)
    m = smoothstep(1 - amount, 1 - amount + 0.12, n)
    if splatter:
        k = int(splatter * h * w / 600)
        sm = np.zeros((h, w), np.float32)
        ys = rng.randint(0, h, k)
        xs = rng.randint(0, w, k)
        sm[ys, xs] = 1
        sm = ndimage.grey_dilation(sm, size=(2, 2))
        sm = np.maximum(sm, ndimage.gaussian_filter(sm, 1.2) * 2.5)
        m = np.maximum(m, np.clip(sm, 0, 1))
    return np.clip(m, 0, 1)


def blood_color(rng, h, w):
    base = fill(h, w, rgb("#5a0608"))
    n = fbm(h, w, 8, rng, octaves=3)
    return mix(base, rgb("#8a0c0e"), n * 0.7)


def grime(img, rng, amount=0.25, color=(0.18, 0.15, 0.1), scale=4):
    h, w = img.shape[:2]
    n = fbm(h, w, scale, rng, octaves=5)
    d = photo_detail("moon", h, w, rng, zoom=1.5, sigma=6)
    m = smoothstep(0.45, 0.85, n + d * 0.05) * amount
    return mix(img, color, m)
