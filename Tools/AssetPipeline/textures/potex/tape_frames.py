"""Textures/Props/tape_* - the pictures of the home video tape "MAMA 10/31" (iteration 3).

Each shot of the tape (Gameplay NoteTexts.TapeShots, TapeShot.Frame) has one 64x48 picture, shown on the grandmother's
TV while it plays and in the tape window (UI TapeScreen). A 1987 home video seen on a CRT: big simple shapes and
silhouettes, a blue cast, warm practical lights blooming and bleeding sideways, scanlines, noise, a green PLAY mark.
No legible digits anywhere: numbers are codes in this game.
"""
import numpy as np
from PIL import Image, ImageDraw

from .core import FONT_PIXEL, blur, clamp01, col, fft_noise, font, mix, texture

W0, H0 = 64, 48
DEG = dict(k=8, q=60, bits=5, desat=0.0, dark=1.0)


class Frame:
    """Painting helper in normalized coordinates (x right, y down, 0..1)."""

    def __init__(self, ctx, top="#0a0e1c", bottom="#05070e"):
        self.ctx = ctx
        self.W, self.H = ctx.W, ctx.H
        t = np.linspace(0, 1, self.H, dtype=np.float32)[:, None, None]
        self.img = (np.array(col(top), np.float32) * (1 - t) + np.array(col(bottom), np.float32) * t) * np.ones((1, self.W, 1), np.float32)

    def _mask(self, draw_fn, soft=0.0):
        im = Image.new("L", (self.W, self.H), 0)
        draw_fn(ImageDraw.Draw(im), self.W, self.H)
        m = np.asarray(im, np.float32) / 255.0
        if soft > 0:
            m = blur(m, soft * self.W / 64.0)
        return m

    def paint(self, m, color, alpha=1.0):
        self.img = mix(self.img, color, clamp01(m) * alpha)
        return m

    def rect(self, x0, y0, x1, y1, color, alpha=1.0, soft=0.0):
        return self.paint(self._mask(lambda d, W, H: d.rectangle([x0 * W, y0 * H, x1 * W, y1 * H], fill=255), soft), color, alpha)

    def ellipse(self, cx, cy, rx, ry, color, alpha=1.0, soft=0.0):
        return self.paint(self._mask(lambda d, W, H: d.ellipse([(cx - rx) * W, (cy - ry) * H, (cx + rx) * W, (cy + ry) * H], fill=255), soft), color, alpha)

    def poly(self, pts, color, alpha=1.0, soft=0.0):
        return self.paint(self._mask(lambda d, W, H: d.polygon([(x * W, y * H) for x, y in pts], fill=255), soft), color, alpha)

    def line(self, pts, color, width=0.01, alpha=1.0, soft=0.0):
        return self.paint(self._mask(lambda d, W, H: d.line([(x * W, y * H) for x, y in pts], fill=255, width=max(1, int(width * W))), soft), color, alpha)

    def glow(self, cx, cy, r, color, strength=0.8):
        yy, xx = np.mgrid[0:self.H, 0:self.W].astype(np.float32)
        d = np.sqrt(((xx / self.W - cx) / r) ** 2 + ((yy / self.H - cy) / (r * self.W / self.H)) ** 2)
        g = np.exp(-d * d * 2.2) * strength
        self.img = clamp01(self.img + np.array(col(color), np.float32)[None, None, :] * g[..., None])

    def noise_tex(self, amt=0.08, salt=0, beta=2.0):
        n = fft_noise(self.ctx.sub(400 + salt), self.H, self.W, beta=beta)
        self.img = clamp01(self.img * (1.0 + amt * n)[..., None])


def finish(f, track=False, salt=0, gain=1.0):
    """The VHS / CRT look: blacks lifted into blue, warm lights bleeding sideways, scanlines, noise, vignette, PLAY mark."""
    ctx, W, H = f.ctx, f.W, f.H
    img = clamp01(f.img * gain)
    img = img * np.array([0.86, 0.92, 1.12], np.float32) + np.array([0.02, 0.035, 0.08], np.float32)
    # chroma bleed: red smears right, blue left, everything a touch soft horizontally
    k = max(1, W // 64)
    r = np.roll(blur(img[..., 0], 1.5 * k), 2 * k, axis=1)
    b = np.roll(blur(img[..., 2], 1.5 * k), -k, axis=1)
    g = blur(img[..., 1], 0.6 * k)
    img = np.stack([r, g, b], -1)
    # bloom of the bright parts
    lum = img.mean(-1)
    bloom = blur(clamp01(lum - 0.55) * 2.0, 3.0 * k)
    img = clamp01(img + bloom[..., None] * np.array([0.35, 0.3, 0.25], np.float32))
    # scanlines at the output resolution, fine noise, vignette
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    img = img * (1.0 - 0.08 * ((yy // (H / H0)) % 2))[..., None]
    rng = ctx.sub(700 + salt)
    img = clamp01(img + (rng.random((H, W)).astype(np.float32) - 0.5)[..., None] * 0.07)
    u, v = xx / W * 2 - 1, yy / H * 2 - 1
    img = img * (1.0 - 0.25 * np.clip(u * u * 0.6 + v * v * 0.8, 0, 1))[..., None]
    if track:
        y0 = rng.uniform(0.78, 0.9) * H
        band = np.exp(-((yy - y0) / (H * 0.012)) ** 2) * (0.6 + 0.4 * rng.random((1, W)).astype(np.float32))
        img = clamp01(img + band[..., None] * 0.55)
        img = np.where((np.abs(yy - y0) < H * 0.03)[..., None], np.roll(img, int(W * 0.03), axis=1), img)
    # the VCR's green PLAY mark, top left
    im = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(im)
    d.polygon([(0.04 * W, 0.06 * H), (0.04 * W, 0.16 * H), (0.1 * W, 0.11 * H)], fill=255)
    fnt = font(FONT_PIXEL, int(H * 0.11))
    d.text((0.12 * W, 0.045 * H), "PLAY", font=fnt, fill=255)
    m = np.asarray(im, np.float32) / 255.0
    img = mix(img, "#8cff9a", m * 0.85)
    return clamp01(img)


# ------------------------------------------------------------------ the shots
@texture("Props/tape_kitchen", (W0, H0), **DEG)
def tape_kitchen(ctx):
    f = Frame(ctx, "#1a2034", "#0e1018")
    f.rect(0.04, 0.12, 0.26, 0.44, "#3a4a70")                  # the window, moonlight
    f.line([(0.15, 0.12), (0.15, 0.44)], "#101420", 0.015)
    f.line([(0.04, 0.28), (0.26, 0.28)], "#101420", 0.015)
    f.rect(0.3, 0.06, 1.0, 0.4, "#3a3440")                     # upper cabinets
    for x in (0.47, 0.64, 0.81):
        f.line([(x, 0.06), (x, 0.4)], "#141218", 0.014)
    for x in (0.42, 0.59, 0.76, 0.93):
        f.rect(x, 0.22, x + 0.015, 0.27, "#a0a0a8")
    f.rect(0, 0.64, 1, 1, "#4a3628")                           # counter and its edge
    f.rect(0, 0.6, 1, 0.65, "#6a5440")
    f.line([(0.6, 0), (0.6, 0.3)], "#0a0a0a", 0.01)           # the hanging lamp
    f.poly([(0.53, 0.38), (0.67, 0.38), (0.64, 0.3), (0.56, 0.3)], "#2a2418")
    f.glow(0.6, 0.42, 0.1, "#ffcf7a", 0.9)
    f.ellipse(0.6, 0.66, 0.22, 0.05, "#e0b070", 0.5, soft=3)    # warm pool on the counter
    f.rect(0.74, 0.46, 0.8, 0.6, "#6a6a78")                    # a kettle, a jar
    f.ellipse(0.4, 0.54, 0.04, 0.06, "#5a5e70")
    f.noise_tex(0.1)
    return finish(f, gain=1.3)

@texture("Props/tape_porch", (W0, H0), **DEG)
def tape_porch(ctx):
    f = Frame(ctx, "#060914", "#0a0c16")
    f.rect(0.36, 0.1, 0.64, 0.72, "#3a2c1a")                   # lit doorway
    f.rect(0.39, 0.13, 0.61, 0.72, "#8a6a3a", 0.7)
    f.glow(0.5, 0.4, 0.22, "#ffc070", 0.35)
    f.rect(0.3, 0.08, 0.36, 0.74, "#1a1612")                   # frame
    f.rect(0.64, 0.08, 0.7, 0.74, "#1a1612")
    for i, y in enumerate((0.74, 0.83, 0.92)):                 # steps
        f.rect(0.18 - i * 0.06, y, 0.82 + i * 0.06, y + 0.05, "#2a2830" if i % 2 == 0 else "#1c1a22")
    for cx in (0.26, 0.76):                                    # two jack-o'-lanterns
        f.ellipse(cx, 0.74, 0.09, 0.07, "#d86a14")
        f.glow(cx, 0.74, 0.12, "#ff8a20", 0.5)
        f.poly([(cx - 0.05, 0.71), (cx - 0.02, 0.71), (cx - 0.035, 0.68)], "#ffe080")
        f.poly([(cx + 0.02, 0.71), (cx + 0.05, 0.71), (cx + 0.035, 0.68)], "#ffe080")
        f.poly([(cx - 0.05, 0.76), (cx + 0.05, 0.76), (cx + 0.03, 0.79), (cx - 0.03, 0.79)], "#ffe080")
    f.noise_tex(0.12)
    return finish(f, track=True, salt=1)


@texture("Props/tape_livingtv", (W0, H0), **DEG)
def tape_livingtv(ctx):
    f = Frame(ctx, "#0c0e18", "#07080e")
    f.rect(0, 0.7, 1, 1, "#1a1416")                            # floor
    f.rect(0.52, 0.34, 0.86, 0.7, "#2a1c12")                   # the TV cabinet
    scr = f.rect(0.57, 0.39, 0.81, 0.62, "#9aa2b0")
    n = ctx.sub(5).random((f.H, f.W)).astype(np.float32)
    f.img = mix(f.img, "#e8eef8", scr * (n > 0.5) * 0.7)
    f.glow(0.69, 0.5, 0.25, "#8aa0d8", 0.45)
    f.poly([(0.1, 0.95), (0.12, 0.5), (0.22, 0.42), (0.36, 0.44), (0.4, 0.6), (0.44, 0.95)], "#050508")   # empty armchair
    f.rect(0.06, 0.62, 0.14, 0.95, "#050508")
    f.rect(0.38, 0.62, 0.46, 0.95, "#050508")
    f.noise_tex(0.1)
    return finish(f, salt=2)


@texture("Props/tape_cake", (W0, H0), **DEG)
def tape_cake(ctx):
    f = Frame(ctx, "#1a1418", "#0c0a0c")
    f.ellipse(0.5, 0.26, 0.09, 0.11, "#2a2224")                # her head, white hair
    f.ellipse(0.5, 0.18, 0.1, 0.06, "#c8c4c0", 0.8, soft=1.5)
    f.poly([(0.32, 0.62), (0.36, 0.36), (0.64, 0.36), (0.68, 0.62)], "#1a1416")   # shoulders, shawl
    f.ellipse(0.2, 0.62, 0.13, 0.2, "#0a0a0c", 1.0)             # a big wheel
    f.ellipse(0.2, 0.62, 0.1, 0.16, "#1a1a1e", 1.0)
    f.rect(0.22, 0.66, 0.78, 1.0, "#e8dcc8")                   # the cake
    f.rect(0.22, 0.66, 0.78, 0.72, "#f4ece0")
    f.line([(0.22, 0.8), (0.78, 0.8)], "#c87a8a", 0.02)
    for i in range(6):                                         # candles
        x = 0.3 + i * 0.08
        f.line([(x, 0.56), (x, 0.66)], "#d8c0e0", 0.012)
        f.ellipse(x, 0.53, 0.012, 0.025, "#ffe8a0")
        f.glow(x, 0.53, 0.05, "#ffc060", 0.35)
    f.noise_tex(0.1)
    return finish(f, salt=3)


@texture("Props/tape_drawing", (W0, H0), **DEG)
def tape_drawing(ctx):
    f = Frame(ctx, "#8a8c90", "#6a6c70")                       # fridge door
    f.rect(0.86, 0.1, 0.9, 0.9, "#c8cacc")                     # handle
    f.poly([(0.16, 0.12), (0.76, 0.08), (0.8, 0.86), (0.2, 0.9)], "#e8e2d0")   # the paper, taped
    f.rect(0.4, 0.04, 0.54, 0.12, "#c8c0a0", 0.7)
    f.poly([(0.26, 0.42), (0.38, 0.24), (0.5, 0.42)], "#2a3aa0", 0.0)
    f.line([(0.26, 0.42), (0.38, 0.24), (0.5, 0.42), (0.26, 0.42)], "#2a3aa0", 0.02)   # crayon house
    f.line([(0.28, 0.42), (0.28, 0.66), (0.48, 0.66), (0.48, 0.42)], "#2a3aa0", 0.02)
    for x in (0.56, 0.64):                                     # two stick figures
        f.ellipse(x, 0.52, 0.025, 0.035, "#1a7a2a", 0.0)
        f.line([(x, 0.56), (x, 0.7), (x - 0.03, 0.78)], "#1a7a2a", 0.015)
        f.line([(x, 0.7), (x + 0.03, 0.78)], "#1a7a2a", 0.015)
        f.ellipse(x, 0.52, 0.025, 0.035, "#1a7a2a")
    f.poly([(0.66, 0.3), (0.76, 0.3), (0.78, 0.8), (0.64, 0.8)], "#141414")   # the huge dark one
    f.ellipse(0.71, 0.24, 0.05, 0.06, "#141414")
    f.line([(0.3, 0.76), (0.36, 0.72), (0.4, 0.8), (0.46, 0.73), (0.5, 0.79)], "#c01818", 0.025)   # red scribble
    f.noise_tex(0.08)
    return finish(f, salt=4)


@texture("Props/tape_doorway", (W0, H0), **DEG)
def tape_doorway(ctx):
    f = Frame(ctx, "#0e0e16", "#08080c")
    f.rect(0.3, 0.06, 0.7, 1.0, "#c89850")                     # the lit doorway
    f.glow(0.5, 0.4, 0.3, "#ffb860", 0.4)
    f.ellipse(0.5, 0.2, 0.08, 0.1, "#060406")                  # him, filling it
    f.poly([(0.29, 0.34), (0.38, 0.27), (0.62, 0.27), (0.71, 0.34), (0.7, 1.0), (0.3, 1.0)], "#060406")
    f.poly([(0.66, 0.6), (0.74, 0.58), (0.76, 0.7), (0.68, 0.72)], "#d8dce0")   # the cleaver catching the light
    f.rect(0.25, 0.04, 0.3, 1.0, "#1a140e")
    f.rect(0.7, 0.04, 0.75, 1.0, "#1a140e")
    f.noise_tex(0.1)
    return finish(f, salt=5)


@texture("Props/tape_reflection", (W0, H0), **DEG)
def tape_reflection(ctx):
    f = Frame(ctx, "#0c0d12", "#08090c")
    f.rect(0.06, 0.06, 0.94, 0.94, "#232a38")                  # the switched-off screen, grey-blue glass
    f.ellipse(0.5, 0.5, 0.46, 0.5, "#2e3646", 0.6, soft=6)
    f.ellipse(0.5, 0.28, 0.1, 0.12, "#0e1018")                 # a huge figure reflected in it
    f.poly([(0.24, 0.94), (0.28, 0.46), (0.4, 0.38), (0.6, 0.38), (0.72, 0.46), (0.76, 0.94)], "#0e1018")
    f.rect(0.42, 0.42, 0.6, 0.54, "#1a1e28")                   # the camera at his face
    f.ellipse(0.56, 0.46, 0.014, 0.018, "#ff2010")             # its red REC light
    f.glow(0.56, 0.46, 0.05, "#ff3020", 0.5)
    f.line([(0.12, 0.3), (0.2, 0.14), (0.36, 0.09)], "#8a94a8", 0.025, 0.7, soft=1)   # curved glare on the glass
    f.noise_tex(0.12)
    return finish(f, salt=6, gain=1.4)

@texture("Props/tape_hand", (W0, H0), **DEG)
def tape_hand(ctx):
    f = Frame(ctx, "#d0b090", "#a08060")                       # light all round the hand
    f.ellipse(0.5, 0.78, 0.46, 0.4, "#3a2418", 1.0, soft=3)     # the palm
    for i, x in enumerate((0.2, 0.36, 0.52, 0.68, 0.84)):      # spread fingers, light between them
        top = 0.06 if i in (1, 2, 3) else 0.2
        f.poly([(x - 0.06, 0.62), (x - 0.05, top), (x + 0.05, top), (x + 0.06, 0.62)], "#3a2418", 1.0, soft=2)
        f.ellipse(x, top, 0.05, 0.05, "#3a2418", 1.0, soft=2)
    f.line([(0.3, 0.76), (0.5, 0.72), (0.7, 0.78)], "#1a0e0a", 0.014, 0.7, soft=1)   # creases
    f.line([(0.28, 0.86), (0.55, 0.84)], "#1a0e0a", 0.012, 0.6, soft=1)
    f.noise_tex(0.14)
    return finish(f, track=True, salt=7)

@texture("Props/tape_padlock", (W0, H0), **DEG)
def tape_padlock(ctx):
    f = Frame(ctx, "#2a1c12", "#1a120c")                       # the drawer front
    for y in (0.3, 0.55, 0.8):
        f.line([(0, y), (1, y + 0.02)], "#140c08", 0.008, 0.6)
    f.rect(0.43, 0.08, 0.53, 0.42, "#3a3630")                  # the hasp
    f.line([(0.42, 0.46), (0.42, 0.34), (0.48, 0.28), (0.54, 0.34), (0.54, 0.46)], "#b0b0b0", 0.03)   # shackle
    f.rect(0.36, 0.46, 0.6, 0.76, "#a08030")                   # brass body
    f.rect(0.36, 0.46, 0.6, 0.5, "#d0b060", 0.8)
    f.ellipse(0.48, 0.6, 0.02, 0.03, "#140c06")
    f.glow(0.42, 0.5, 0.1, "#ffe0a0", 0.25)
    f.poly([(1.02, 0.5), (0.7, 0.56), (0.6, 0.62), (0.62, 0.68), (0.72, 0.68), (1.02, 0.66)], "#c8a088")   # an old finger
    f.line([(0.75, 0.6), (0.77, 0.66)], "#7a5a48", 0.008)
    f.line([(0.85, 0.58), (0.87, 0.66)], "#7a5a48", 0.008)
    f.noise_tex(0.1)
    return finish(f, salt=8)


@texture("Props/tape_clock", (W0, H0), **DEG)
def tape_clock(ctx):
    f = Frame(ctx, "#2a1a10", "#160e08")
    f.ellipse(0.5, 0.5, 0.34, 0.45, "#3a2414")                 # case around the dial
    f.ellipse(0.5, 0.5, 0.29, 0.39, "#d8ccb0")
    f.ellipse(0.5, 0.5, 0.29, 0.39, "#8a7a5a", 0.25, soft=4)
    for k in range(12):                                        # tick marks only
        a = k / 12 * 2 * np.pi
        x0, y0 = 0.5 + np.sin(a) * 0.24, 0.5 - np.cos(a) * 0.32
        x1, y1 = 0.5 + np.sin(a) * 0.27, 0.5 - np.cos(a) * 0.36
        f.line([(x0, y0), (x1, y1)], "#2a2018", 0.014)
    f.line([(0.5, 0.5), (0.5 + np.sin(2.3) * 0.15, 0.5 - np.cos(2.3) * 0.2)], "#120c08", 0.022)
    f.line([(0.5, 0.5), (0.5 + np.sin(-0.4) * 0.22, 0.5 - np.cos(-0.4) * 0.3)], "#120c08", 0.014)
    f.ellipse(0.5, 0.5, 0.02, 0.026, "#120c08")
    f.glow(0.35, 0.3, 0.12, "#fff0d0", 0.2)
    f.noise_tex(0.1)
    return finish(f, salt=9)


@texture("Props/tape_mama", (W0, H0), **DEG)
def tape_mama(ctx):
    f = Frame(ctx, "#5a4a44", "#3a2c28")
    f.ellipse(0.5, 0.55, 0.55, 0.62, "#8a6e60", 0.95, soft=6)   # her face, far too close
    f.rect(0, 0, 1, 0.12, "#c8c4c0", 0.6, soft=6)              # white hair at the top
    f.ellipse(0.32, 0.38, 0.11, 0.07, "#2a1c18")               # one eye
    f.ellipse(0.32, 0.38, 0.05, 0.05, "#0a0606")
    f.ellipse(0.3, 0.36, 0.015, 0.015, "#e8e8e8")
    f.line([(0.2, 0.48), (0.32, 0.5), (0.44, 0.47)], "#4a3428", 0.012, 0.8, soft=1)   # wrinkles
    f.line([(0.6, 0.3), (0.78, 0.28)], "#4a3428", 0.012, 0.8, soft=1)
    f.ellipse(0.58, 0.8, 0.2, 0.08, "#3a1a1a", 0.95, soft=2)    # lips at the lens
    f.line([(0.4, 0.8), (0.58, 0.83), (0.76, 0.79)], "#140808", 0.02)
    f.noise_tex(0.14)
    return finish(f, salt=10)


@texture("Props/tape_cage", (W0, H0), **DEG)
def tape_cage(ctx):
    f = Frame(ctx, "#0a0c14", "#06070c")
    f.glow(0.62, 0.42, 0.22, "#f0f0e8", 0.7)                   # the camera light in the bars
    f.rect(0, 0.12, 1, 0.16, "#3a3a40")                        # top rail
    for i in range(9):
        x = 0.06 + i * 0.11
        f.rect(x, 0.12, x + 0.025, 1.0, "#2a2a30")
        f.rect(x, 0.12, x + 0.008, 1.0, "#6a6a72", 0.6)
    f.ellipse(0.42, 0.52, 0.05, 0.035, "#c8b8a8")              # a pale hand round a bar
    for k in range(3):
        f.ellipse(0.42 + (k - 1) * 0.02, 0.56, 0.012, 0.03, "#b8a898")
    f.noise_tex(0.1)
    return finish(f, track=True, salt=11)


@texture("Props/tape_corn", (W0, H0), **DEG)
def tape_corn(ctx):
    f = Frame(ctx, "#24305a", "#0c1020")                       # the night sky over the field
    rng = ctx.sub(12)
    for i in range(70):                                        # corn stalks against it
        x = rng.uniform(-0.05, 1.05)
        top = rng.uniform(0.32, 0.55)
        f.line([(x, 1.0), (x + rng.uniform(-0.03, 0.03), top)], "#06080a", 0.014)
        f.line([(x, top + 0.12), (x + rng.uniform(-0.1, 0.1), top + rng.uniform(0.04, 0.18))], "#080a0a", 0.01)
    f.rect(0, 0.62, 1, 1, "#040506", 0.8)
    f.ellipse(0.66, 0.5, 0.016, 0.022, "#ffffe8")              # a flashlight far away
    f.glow(0.66, 0.5, 0.07, "#fff8d0", 0.8)
    f.ellipse(0.1, 0.1, 0.05, 0.06, "#010102")                 # a very tall figure at the edge
    f.poly([(0.02, 0.17), (0.18, 0.17), (0.2, 1.0), (0.0, 1.0)], "#010102")
    f.noise_tex(0.12)
    return finish(f, salt=12, gain=1.2)

@texture("Props/tape_stairs", (W0, H0), **DEG)
def tape_stairs(ctx):
    f = Frame(ctx, "#020204", "#0a0a0e")
    for i in range(8):                                         # steps narrowing down into the dark
        t0, t1 = i / 8, (i + 1) / 8
        y0, y1 = 1.0 - t0 * 0.7, 1.0 - t1 * 0.7
        w0, w1 = 0.48 - t0 * 0.3, 0.48 - t1 * 0.3
        f.poly([(0.5 - w0, y0), (0.5 + w0, y0), (0.5 + w1, y1), (0.5 - w1, y1)], "#5a4630" if i % 2 == 0 else "#2a2018", 1.0 - t0 * 0.7)
        f.line([(0.5 - w0, y0), (0.5 + w0, y0)], "#8a7050", 0.012, 1.0 - t0 * 0.8)
    f.rect(0.0, 0.0, 0.16, 1.0, "#141010")                     # the walls
    f.rect(0.84, 0.0, 1.0, 1.0, "#141010")
    f.rect(0.36, 0.0, 0.64, 0.32, "#000000")                   # black at the bottom of the stairs
    f.poly([(0.5, 1.0), (0.32, 0.5), (0.6, 0.46)], "#f0e8c8", 0.4, soft=3)   # the flashlight cone
    f.ellipse(0.47, 0.5, 0.11, 0.04, "#fff0c8", 0.6, soft=2)
    f.noise_tex(0.12)
    return finish(f, salt=13, gain=1.2)

@texture("Props/tape_smile", (W0, H0), **DEG)
def tape_smile(ctx):
    f = Frame(ctx, "#3a2a24", "#1a1210")
    f.ellipse(0.5, 0.5, 0.6, 0.7, "#6a5044", 0.95, soft=6)      # a huge face at the lens
    f.ellipse(0.3, 0.3, 0.14, 0.12, "#0a0606", 0.95, soft=3)    # eyes lost in shadow
    f.ellipse(0.7, 0.3, 0.14, 0.12, "#0a0606", 0.95, soft=3)
    f.poly([(0.16, 0.6), (0.84, 0.6), (0.72, 0.82), (0.28, 0.82)], "#1a0808")   # the smile
    for i in range(9):
        x = 0.22 + i * 0.07
        f.rect(x, 0.61, x + 0.055, 0.68, "#d8d0b8")
    f.line([(0.16, 0.6), (0.5, 0.64), (0.84, 0.6)], "#2a1010", 0.012)
    f.noise_tex(0.14)
    return finish(f, track=True, salt=14)


@texture("Props/tape_fallen", (W0, H0), **DEG)
def tape_fallen(ctx):
    f = Frame(ctx, "#0a0c12", "#06070a")
    f.rect(0.0, 0.0, 0.28, 1.0, "#2a1e16")                     # the floor, on its side
    for x in (0.07, 0.14, 0.21):
        f.line([(x, 0), (x, 1)], "#140e0a", 0.01)
    f.rect(0.28, 0.0, 0.31, 1.0, "#3a2c20")
    for i in range(6):                                         # cage bars, horizontal now
        y = 0.08 + i * 0.16
        f.rect(0.31, y, 1.0, y + 0.03, "#2a2a30")
        f.rect(0.31, y, 1.0, y + 0.01, "#5a5a62", 0.6)
    f.ellipse(0.42, 0.56, 0.06, 0.035, "#b8a898")              # a bare foot that does not move
    f.ellipse(0.36, 0.58, 0.025, 0.02, "#a89888")
    f.glow(0.8, 0.5, 0.2, "#c8ccd8", 0.25)
    f.noise_tex(0.12)
    return finish(f, track=True, salt=15)


@texture("Props/tape_wheelchair", (W0, H0), **DEG)
def tape_wheelchair(ctx):
    f = Frame(ctx, "#080a14", "#05060a")
    f.rect(0, 0.72, 1, 1, "#100e14")                           # floor
    f.rect(0.56, 0.24, 0.9, 0.62, "#1a120c")                   # the TV, a flat blue screen
    f.rect(0.6, 0.28, 0.86, 0.56, "#2840d0")
    f.glow(0.73, 0.42, 0.3, "#3050ff", 0.55)
    f.ellipse(0.32, 0.72, 0.13, 0.18, "#020204")               # an empty wheelchair, its back to us
    f.ellipse(0.32, 0.72, 0.1, 0.14, "#0e1020")
    f.rect(0.22, 0.42, 0.42, 0.66, "#03040a")
    f.line([(0.2, 0.42), (0.2, 0.3)], "#03040a", 0.02)
    f.line([(0.44, 0.42), (0.44, 0.3)], "#03040a", 0.02)
    f.line([(0.2, 0.42), (0.44, 0.42)], "#3a50c0", 0.006, 0.6)   # rim light from the screen
    f.noise_tex(0.1)
    return finish(f, salt=16)
