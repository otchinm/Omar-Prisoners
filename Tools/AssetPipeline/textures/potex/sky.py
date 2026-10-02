"""Textures/Sky - cylindrical night panoramas built from a real Poly Haven cloud HDRI (wraps
horizontally because the source equirect does)."""
import numpy as np
from PIL import Image

from .core import clamp01, fft_noise, gradient_map, lum, mix, resize, smoothstep, texture


def cloud_layer(ctx, W, H, row0=18, row1=124):
    """cloud luminance from the 'sky' HDRI (elevation ~75deg .. horizon), periodic in x"""
    pano = ctx.src.hdri("sky")
    band = pano[row0:row1]
    l = lum(np.clip(band / (1.0 + band), 0, 1))
    # remove the blue-sky gradient so that only cloud structure remains
    base = np.percentile(l, 20, axis=1, keepdims=True)
    c = np.clip(l - base, 0, None)
    c = c / (np.percentile(c, 99.5) + 1e-6)
    tiled = np.concatenate([c, c, c], axis=1)
    big = resize(tiled.astype(np.float32), W * 3, H, Image.BICUBIC)
    return clamp01(big[:, W:2 * W])


def night_sky(ctx, darker=0.0, moon_x=0.32):
    W, H = ctx.W, ctx.H
    r = ctx.sub(1)
    clouds = cloud_layer(ctx, W, H)
    # extra periodic billows for a heavier overcast
    bill = fft_noise(r, H, W, beta=2.8, ax=1.6, ay=0.6)  # horizontally stretched billows
    yy = (np.arange(H) + 0.5) / H  # 0 top .. 1 horizon
    xx = (np.arange(W) + 0.5) / W
    cover = clamp01(0.45 + 0.55 * clouds + 0.18 * bill)
    # moon glow behind the clouds (periodic distance in x)
    dx = np.abs(((xx[None, :] - moon_x) + 0.5) % 1.0 - 0.5) * (W / H)
    dy = yy[:, None] - 0.28
    dist = np.sqrt(dx * dx * 0.6 + dy * dy)
    glow = np.exp(-(dist / 0.24) ** 2) * 0.8 + np.exp(-(dist / 0.08) ** 2) * 0.35
    # clouds are lit from the moon side, thinner clouds let more glow through
    sky_v = 0.10 + 0.12 * smoothstep(0.55, 1.0, yy)[:, None]  # horizon slightly lighter
    v = sky_v + cover * 0.17 + glow * (0.16 + 0.22 * (1 - cover)) + 0.1 * clouds * glow
    v = v * (1.0 - darker)
    t = clamp01(v / 0.55)
    img = gradient_map(t, [(0, "#020305"), (0.25, "#080b11"), (0.5, "#151a22"), (0.75, "#262c34"), (1, "#4a4e54")])
    # faint warm light pollution at the very horizon
    img = mix(img, "#2a2420", (smoothstep(0.85, 1.0, yy)[:, None] * np.ones((1, W))) * 0.25 * (1 - darker))
    return img


@texture("Sky/sky_night", (1024, 256), k=1, tile=True, q=55, bits=5, dither=0.6, sharpen=0.0, desat=0.05, dark=1.0, maxv=1.0)
def sky_night(ctx):
    return night_sky(ctx)


@texture("Sky/sky_menu", (512, 128), k=2, tile=True, q=55, bits=5, dither=0.6, sharpen=0.0, desat=0.1, dark=1.0, maxv=1.0)
def sky_menu(ctx):
    return night_sky(ctx, darker=0.4, moon_x=0.7)
