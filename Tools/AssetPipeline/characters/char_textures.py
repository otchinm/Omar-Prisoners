"""Character atlases (256x256 RGBA, layout in atlas.py) for the 4 prisoners, Omar, mannequins and the corpse.

Everything is painted at 4x resolution in the coordinate system of the mesh (cylindrical strips), with
real-photo high-pass detail layers (photos.py), then downsampled, posterized and JPEG-crushed.
"""
import numpy as np
from scipy import ndimage

import atlas as A
from paint import (rgb, fill, uv_grid, fbm, value_noise, photo_detail, smoothstep, mix, shade, blur, text_mask,
                   downsample, degrade, fabric, skin, hair_strands, denim, plaid, blood, blood_color, grime,
                   draw_mask, desaturate, fold_layer)

S = 4  # supersampling


def canvas(rect):
    return rect[3] * S, rect[2] * S


# ----------------------------------------------------------------------------------------------- head space
def head_space(h, w):
    u, v = uv_grid(h, w)
    th = A.head_theta_from_u(u)  # degrees, + = character's right (image left)
    y = A.head_y_from_v(v)       # head units (chin 0, crown 1)
    X = np.deg2rad(th) * 85.0    # approx. mm on the surface
    Y = y * 230.0
    return th.astype(np.float32), y.astype(np.float32), X.astype(np.float32), Y.astype(np.float32)


def ell(X, Y, cx, cy, rx, ry, soft=1.0):
    d = np.sqrt(((X - cx) / rx) ** 2 + ((Y - cy) / ry) ** 2)
    return np.clip((1.0 - d) * min(rx, ry) / soft + 0.5, 0, 1).astype(np.float32)


def sym_ell(X, Y, cx, cy, rx, ry, soft=1.0):
    return np.maximum(ell(X, Y, cx, cy, rx, ry, soft), ell(X, Y, -cx, cy, rx, ry, soft))


def interp_curve(xabs, pts):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    return np.interp(xabs, xs, ys).astype(np.float32)


HAIRLINES = {
    # (|theta| degrees, y_rel threshold above which there is hair)
    "short": [(0, 0.79), (30, 0.77), (55, 0.70), (68, 0.64), (76, 0.44), (84, 0.42), (86, 0.62), (102, 0.62),
              (110, 0.30), (140, 0.12), (180, 0.08)],
    "buzz": [(0, 0.80), (30, 0.78), (55, 0.71), (68, 0.66), (76, 0.48), (84, 0.46), (86, 0.64), (102, 0.64),
             (110, 0.34), (140, 0.16), (180, 0.12)],
    "long": [(0, 0.80), (20, 0.78), (40, 0.70), (52, 0.55), (58, 0.20), (64, -0.2), (70, -0.6), (180, -0.6)],
    "bangs": [(0, 0.62), (30, 0.62), (44, 0.60), (52, 0.50), (57, 0.20), (63, -0.2), (70, -0.6), (180, -0.6)],
    # (iteration 2) jaw-length bob: fringe (swept to one side by bob_sweep()), sides over the ears to the jaw,
    # the nape shows under the cut
    "bob": [(0, 0.62), (30, 0.62), (44, 0.60), (52, 0.50), (57, 0.22), (63, 0.02), (75, -0.06), (110, -0.12),
            (150, -0.2), (180, -0.22)],
    # boy's short cut with a fringe over the forehead
    "kid": [(0, 0.70), (25, 0.70), (45, 0.67), (60, 0.62), (72, 0.55), (80, 0.44), (86, 0.58), (102, 0.58),
            (110, 0.28), (140, 0.10), (180, 0.06)],
}


def bob_sweep(th, amount):
    """Side part at theta ~ +22 (character's right): the fringe line rises there and sweeps down across the
    forehead to the other temple. MUST match BodyMeshGenerator.FringeSweep()."""
    return amount * (0.17 * np.exp(-((th - 22.0) / 15.0) ** 2) - 0.04 * np.exp(-((th + 34.0) / 16.0) ** 2))


def almond(X, Y, cx, cy, rx, ry_up, ry_lo, tilt=0.0, soft=1.0):
    """Eye shaped mask: rounder upper arc, flatter lower arc, outer corner raised by `tilt` (mm per mm, cx sign = side)."""
    dx = X - cx
    side = 1.0 if cx >= 0 else -1.0
    yy = Y - cy - tilt * dx * side
    ry = np.where(yy > 0, ry_up, ry_lo)
    d = np.sqrt((dx / rx) ** 2 + (yy / ry) ** 2)
    return np.clip((1.0 - d) * min(rx, ry_lo) / soft + 0.5, 0, 1).astype(np.float32)


def paint_feminine_face(spec, rng, th, y, X, Y):
    """(iteration 2) Pretty young woman's face: soft shading, almond eyes with liner + lashes, arched brows, full lips."""
    h, w = th.shape
    ath = np.abs(th)
    tone = rgb(spec["skin"])
    img = skin(h, w, tone, rng, mottle=spec.get("mottle", 0.05), pores=0.022, redness=spec.get("redness", 0.15))
    lum = np.ones((h, w), np.float32)
    lum *= 1 - 0.14 * smoothstep(45, 120, ath)
    lum *= np.where(Y < 0, 0.92, 1.0)
    lum *= 1 - 0.34 * smoothstep(-35, -2, Y) * (1 - smoothstep(-1, 4, Y)) * (1 - smoothstep(40, 80, ath))
    # soft eye sockets, small straight nose, gentle nostrils
    lum *= 1 - 0.12 * sym_ell(X, Y, 31, 106, 20, 11, 8)
    lum *= 1 + 0.10 * ell(X, Y, 0, 90, 4.5, 20, 4)           # bridge highlight
    lum *= 1 + 0.07 * ell(X, Y, 0, 74, 6, 5, 3)              # tip
    lum *= 1 - 0.20 * ell(X, Y, 0, 67.5, 10, 3.6, 2.5)       # under the tip
    lum *= 1 - 0.07 * sym_ell(X, Y, 8.5, 82, 3.5, 13, 4)     # nose sides
    lum *= 1 - 0.30 * sym_ell(X, Y, 5.2, 69.0, 2.4, 1.5, 0.9)  # nostrils
    # high cheekbones: highlight above, soft hollow below; small soft chin; philtrum
    lum *= 1 + 0.09 * sym_ell(X, Y, 40, 84, 17, 9, 7)
    lum *= 1 - 0.07 * sym_ell(X, Y, 50, 62, 12, 14, 9)
    lum *= 1 - 0.10 * smoothstep(52, 78, ath) * (1 - smoothstep(30, 60, Y)) * (Y > -5)
    lum *= 1 + 0.06 * ell(X, Y, 0, 14, 14, 9, 6)
    lum *= 1 - 0.10 * ell(X, Y, 0, 27, 11, 3.0, 2.5)
    lum *= 1 - 0.06 * ell(X, Y, 0, 52, 3.2, 6, 2.5)
    img = shade(img, lum)
    if spec.get("blush"):
        img = mix(img, tone * np.array([1.12, 0.74, 0.74]), sym_ell(X, Y, 40, 72, 15, 11, 8) * spec["blush"])
    if spec.get("freckles"):
        k = 220
        fm = np.zeros((h, w), np.float32)
        xs = rng.uniform(-50, 50, k)
        ys = rng.uniform(64, 98, k)
        for fx, fy in zip(xs, ys):
            fm = np.maximum(fm, ell(X, Y, fx, fy, 1.5, 1.5, 0.6))
        img = mix(img, tone * np.array([0.8, 0.58, 0.44]), fm * 0.45)
    # lips: cupid's bow upper lip, full lower lip with a highlight, corners turned up a little
    lip = rgb(spec["lips"])
    la = spec.get("lip_alpha", 0.8)
    smile = 0.7 * (np.abs(X) / 21.0) ** 2
    Yl = Y - smile
    upper = np.maximum(ell(X, Yl, -7.5, 42.2, 15.5, 3.0, 1.0), ell(X, Yl, 7.5, 42.2, 15.5, 3.0, 1.0))
    upper = np.maximum(upper, ell(X, Yl, 0, 41.0, 25.0, 2.2, 1.0)) * (1 - 0.7 * ell(X, Yl, 0, 45.0, 2.4, 1.2, 0.6))
    lower = ell(X, Yl, 0, 36.2, 21.0, 4.2, 1.1)
    img = mix(img, lip * 0.84, upper * la)
    img = mix(img, lip, lower * la)
    img = mix(img, np.clip(lip * 1.35 + 0.1, 0, 1), ell(X, Yl, 1.5, 37.0, 9, 1.3, 0.9) * 0.45 * la)
    img = mix(img, rgb("#3a1614"), ell(X, Yl, 0, 39.4, 24.5, 0.75, 0.6) * 0.6)
    img = mix(img, tone * 0.72, sym_ell(X, Yl, 25.0, 40.3, 1.6, 1.3, 0.6) * 0.45)  # mouth corners
    # eyes: big almond shape, dark upper lash line with a little wing, lashes, iris half under the lid
    sclera = rgb(spec.get("sclera", "#d4ccc0"))
    iris = rgb(spec["eyes"])
    erx, eup, elo = spec.get("eye_rx", 12.6), spec.get("eye_up", 4.6), spec.get("eye_lo", 3.3)
    liner = spec.get("liner", 1.0)
    for sx in (-1, 1):
        cx, cy = 31.5 * sx, 104.5
        if spec.get("shadow"):
            sh = almond(X, Y, cx, cy + 3.2, erx + 2.5, eup + 4.5, 2.0, 0.08, 2.5)
            img = mix(img, rgb(spec["shadow"]), sh * 0.42)
        white = almond(X, Y, cx, cy, erx, eup, elo, 0.10, 0.9)
        img = mix(img, sclera, white)
        img = mix(img, sclera * 0.72, ell(X, Y, cx + 9.5 * sx, cy, 4, 4, 1.2) * white * 0.55)  # corner shade
        ix, iy = cx - 0.8 * sx, cy + 0.2
        img = mix(img, iris, ell(X, Y, ix, iy, 5.4, 5.6, 0.8) * white)
        img = mix(img, iris * 0.55, ell(X, Y, ix, iy, 5.4, 5.6, 0.8) * (1 - ell(X, Y, ix, iy, 4.3, 4.5, 0.8)) * white * 0.8)
        img = mix(img, rgb("#060404"), ell(X, Y, ix, iy, 2.0, 2.1, 0.6) * white)
        img = mix(img, rgb("#f4f4f4"), ell(X, Y, ix - 1.6 * sx, iy + 1.8, 1.0, 1.0, 0.4) * white * 0.95)
        # upper lid: the lid crease shadow and the lash line (thicker towards the outer corner, flicked up)
        crease = almond(X, Y, cx, cy + 1.0, erx + 1.0, eup + 3.2, 0.5, 0.08, 1.2) * (1 - almond(X, Y, cx, cy + 0.8, erx + 0.6, eup + 2.0, 0.5, 0.08, 1.0))
        img = mix(img, tone * 0.62, crease * (Y > cy) * 0.5)
        outer = np.clip((X * sx - (31.5 - 4.0)) / 12.0, 0, 1)
        lash = almond(X, Y, cx, cy + 0.2, erx + 0.9, eup + 1.0 + 1.2 * liner * outer, elo, 0.10, 0.7) * (1 - white)
        lash *= (Y > cy - 0.6 + 2.2 * outer)
        img = mix(img, rgb("#120806"), lash * 0.95)
        wing = np.clip(1 - np.abs((Y - (cy + 2.2)) - 0.55 * (X * sx - (31.5 + erx - 1.0))) / 1.1, 0, 1)
        wing *= np.clip((X * sx - (31.5 + erx - 2.0)) / 1.0, 0, 1) * np.clip(1 - (X * sx - (31.5 + erx + 3.4)) / 1.0, 0, 1)
        img = mix(img, rgb("#120806"), wing * 0.9 * min(1.0, liner))
        lower_lid = almond(X, Y, cx, cy - 0.2, erx + 0.4, 1.0, elo + 0.9, 0.10, 0.7) * (1 - white) * (Y < cy)
        img = mix(img, rgb("#4a2a22"), lower_lid * 0.45)
        # brows: thin, arched, tapered tail
        rel = (X * sx - 13.0) / 30.0
        by = 116.5 + 4.2 * np.sin(np.clip(rel, 0, 1) * np.pi * 0.8) * spec.get("brow_arch", 1.0) - 2.0 * np.clip(rel - 0.75, 0, 1)
        thick = spec.get("brow_thick", 2.4) * (1.0 - 0.6 * np.clip(rel, 0, 1))
        brow = np.clip(1 - np.abs(Y - by) / thick, 0, 1) * smoothstep(-0.05, 0.08, rel) * (1 - smoothstep(0.92, 1.08, rel))
        img = mix(img, rgb(spec["brows"]), brow * 0.92)
    # ears
    ear = np.clip(1 - np.abs(ath - 94) / 10, 0, 1) * np.clip(1 - np.abs(Y - 100) / 28, 0, 1)
    img = mix(img, tone * np.array([0.94, 0.8, 0.74]), smoothstep(0.0, 0.3, ear) * 0.55)
    inner = np.clip(1 - np.abs(ath - 95) / 4, 0, 1) * np.clip(1 - np.abs(Y - 100) / 16, 0, 1)
    img = mix(img, tone * 0.55, smoothstep(0.1, 0.6, inner) * 0.5)
    return img


def paint_face(spec, rng, th, y, X, Y):
    if spec.get("feminine"):
        return paint_feminine_face(spec, rng, th, y, X, Y)
    h, w = th.shape
    ath = np.abs(th)
    tone = rgb(spec["skin"])
    img = skin(h, w, tone, rng, mottle=spec.get("mottle", 0.07), pores=0.04, redness=spec.get("redness", 0.25))
    lum = np.ones((h, w), np.float32)
    # side / back darkening, neck darker, jaw shadow on the neck
    lum *= 1 - 0.16 * smoothstep(45, 120, ath)
    lum *= np.where(Y < 0, 0.9, 1.0)
    lum *= 1 - 0.38 * smoothstep(-35, -2, Y) * (1 - smoothstep(-1, 4, Y)) * (1 - smoothstep(40, 80, ath))
    # eye sockets
    lum *= 1 - 0.20 * sym_ell(X, Y, 31, 106, 21, 12, 7)
    # nose
    lum *= 1 + 0.09 * ell(X, Y, 0, 88, 6, 22, 5)
    lum *= 1 + 0.06 * ell(X, Y, 0, 75, 8, 6, 3)
    lum *= 1 - 0.32 * ell(X, Y, 0, 67.5, 13, 4.5, 2.5)
    lum *= 1 - 0.12 * sym_ell(X, Y, 10, 82, 4.5, 15, 4)
    lum *= 1 - 0.55 * sym_ell(X, Y, 6.5, 69.5, 3.2, 2.0, 1.0)
    # cheekbones, jaw, chin
    lum *= 1 + 0.07 * sym_ell(X, Y, 44, 86, 18, 9, 6)
    lum *= 1 - 0.14 * smoothstep(48, 75, ath) * (1 - smoothstep(30, 60, Y)) * (Y > -5)
    lum *= 1 + 0.05 * ell(X, Y, 0, 12, 20, 11, 6)
    lum *= 1 - 0.16 * ell(X, Y, 0, 29, 15, 3.5, 2.5)
    lum *= 1 - 0.08 * ell(X, Y, 0, 51, 4, 7, 3)
    img = shade(img, lum)
    # cheeks blush
    if spec.get("blush"):
        img = mix(img, tone * np.array([1.1, 0.72, 0.72]), sym_ell(X, Y, 42, 74, 16, 12, 8) * spec["blush"])
    if spec.get("freckles"):
        k = 260
        fm = np.zeros((h, w), np.float32)
        xs = rng.uniform(-55, 55, k)
        ys = rng.uniform(62, 100, k)
        for fx, fy in zip(xs, ys):
            fm = np.maximum(fm, ell(X, Y, fx, fy, 1.6, 1.6, 0.6))
        img = mix(img, tone * np.array([0.78, 0.55, 0.4]), fm * 0.55)
    # facial hair
    if spec.get("stubble"):
        st = (1 - smoothstep(48, 62, Y)) * (1 - smoothstep(60, 78, ath)) * (Y > -8) * (1 - ell(X, Y, 0, 37, 21, 6, 2))
        img = mix(img, rgb(spec["stubble_color"]), st * spec["stubble"])
    if spec.get("mustache"):
        m = ell(X, Y, 0, 47.5, 21, 3.8, 1.5) * (1 - ell(X, Y, 0, 52, 4, 3, 1))
        m = np.maximum(m, sym_ell(X, Y, 20, 38, 3, 9, 1.2))
        m = np.maximum(m, ell(X, Y, 0, 17, 10, 10, 2.5))
        img = mix(img, rgb(spec["stubble_color"]), m * spec["mustache"])
    # mouth
    lip = rgb(spec["lips"])
    upper = ell(X, Y, 0, 42.2, 21, 3.6, 1.2) * (1 - 0.6 * ell(X, Y, 0, 45.5, 3, 1.6, 0.8))
    lower = ell(X, Y, 0, 35.8, 18, 4.3, 1.2)
    img = mix(img, lip * 0.88, upper * spec.get("lip_alpha", 0.75))
    img = mix(img, lip, lower * spec.get("lip_alpha", 0.75))
    img = mix(img, lip * 1.25 + 0.05, ell(X, Y, 0, 37.2, 9, 1.6, 1) * 0.35)
    img = mix(img, rgb("#2a1410"), ell(X, Y, 0, 39.4, 21, 1.0, 0.7) * 0.85)
    if spec.get("open_mouth"):
        img = mix(img, rgb("#140808"), ell(X, Y, 0, 38.5, 13, 6, 1.5))
    # eyes
    sclera = rgb(spec.get("sclera", "#d8d0c4"))
    iris = rgb(spec["eyes"])
    for sx in (-1, 1):
        cx, cy = 32.0 * sx, 104.0
        if spec.get("eyes_closed") or spec.get("dead_eyes"):
            pass
        white = ell(X, Y, cx, cy, 12.0, 4.2, 1.0)
        img = mix(img, sclera, white)
        img = mix(img, sclera * 0.7, ell(X, Y, cx + 9 * sx, cy, 4, 3.5, 1) * white * 0.6)
        ir = ell(X, Y, cx - 1.0 * sx, cy - 0.2, 4.4, 4.1, 0.8) * white
        img = mix(img, iris, ir)
        img = mix(img, rgb("#060404"), ell(X, Y, cx - 1.0 * sx, cy - 0.2, 1.9, 1.9, 0.6) * white)
        img = mix(img, rgb("#f0f0f0"), ell(X, Y, cx - 2.3 * sx, cy + 1.3, 0.9, 0.9, 0.4) * white * 0.9)
        lid_w = spec.get("liner", 1.0)
        lid = ell(X, Y, cx, cy + 0.8, 13.2, 4.2 + 1.2 * lid_w, 0.9) * (1 - ell(X, Y, cx, cy - 0.9, 12.4, 4.4, 0.8))
        lid *= (Y > cy - 2.5)
        img = mix(img, rgb("#1a0e0a"), lid * 0.9)
        img = mix(img, rgb("#3a2018"), ell(X, Y, cx, cy - 4.6, 10.5, 1.0, 0.8) * 0.35)
        if spec.get("shadow"):
            sh = ell(X, Y, cx, cy + 6.5, 13, 4.5, 2.5) * (1 - white)
            img = mix(img, rgb(spec["shadow"]), sh * 0.45)
        # brow
        bx = np.abs(X - 0) if False else None
        rel = (X * sx - 11.0) / 36.0
        by = 125.5 + 6.0 * np.sin(np.clip(rel, 0, 1) * np.pi * 0.85) * spec.get("brow_arch", 1.0)
        thick = spec.get("brow_thick", 3.4) * (1.0 - 0.45 * np.clip(rel, 0, 1))
        brow = np.clip(1 - np.abs(Y - by) / thick, 0, 1) * smoothstep(-0.05, 0.05, rel) * (1 - smoothstep(0.9, 1.05, rel))
        img = mix(img, rgb(spec["brows"]), brow * 0.9)
    if spec.get("dead_eyes"):
        img = mix(img, rgb("#1a1412"), sym_ell(X, Y, 32, 104, 13, 6, 2) * 0.85)
        img = mix(img, rgb("#4a4440"), sym_ell(X, Y, 32, 103, 6, 2, 1) * 0.5)
    # ears
    ear = np.clip(1 - np.abs(ath - 94) / 10, 0, 1) * np.clip(1 - np.abs(Y - 100) / 30, 0, 1)
    img = mix(img, tone * np.array([0.92, 0.78, 0.72]), smoothstep(0.0, 0.3, ear) * 0.6)
    inner = np.clip(1 - np.abs(ath - 95) / 4, 0, 1) * np.clip(1 - np.abs(Y - 100) / 18, 0, 1)
    img = mix(img, tone * 0.5, smoothstep(0.1, 0.6, inner) * 0.6)
    return img


def paint_hair_on_head(img, spec, rng, th, y, X, Y):
    h, w = th.shape
    ath = np.abs(th)
    style = spec["hair_style"]
    if style in ("none",):
        return img
    hl = interp_curve(ath, HAIRLINES[style])
    if style == "short" and spec.get("part"):
        hl = hl - 0.04 * np.exp(-((th - 18) / 14) ** 2)
    if spec.get("fringe_sweep"):
        hl = hl + bob_sweep(th, spec["fringe_sweep"])
    soft = 0.025
    m = smoothstep(hl - soft, hl + soft, y)
    # irregular edge
    m = np.clip(m + (fbm(h, w, 24, rng, octaves=2) - 0.5) * 0.5 * (m * (1 - m) * 4), 0, 1)
    hair = hair_strands(h, w, rgb(spec["hair"]), rng, highlight=rgb(spec["hair_hi"]) if spec.get("hair_hi") else None,
                        contrast=spec.get("hair_contrast", 0.4))
    # crown highlight, darker at the nape / under side
    lum = 0.85 + 0.3 * smoothstep(0.55, 0.95, y) * (1 - smoothstep(60, 140, ath) * 0.5)
    lum *= 1 - 0.25 * smoothstep(0.4, -0.4, y)
    hair = shade(hair, lum)
    if style == "buzz":
        # bleached spiky top, darker short sides
        side = 1 - smoothstep(0.80, 0.9, y)
        hair = mix(hair, rgb(spec["hair_root"]), side * 0.55)
        m = m * (1 - side * 0.25)
    if spec.get("part_line") is not None:
        # side parting: a thin line of scalp from the hairline back over the top, hair combed away from it
        pth = spec["part_line"]
        pl = np.clip(1 - np.abs(th - pth) / 2.2, 0, 1) * smoothstep(hl.max() * 0 + 0.74, 0.8, y) * (1 - smoothstep(0.93, 0.99, y))
        hair = shade(hair, 1 + 0.12 * np.clip(1 - np.abs(th - pth - 6) / 6, 0, 1) * (y > 0.76))
        hair = mix(hair, rgb(spec["skin"]) * 0.8, pl * 0.75)
    # hairline shadow on the skin
    edge = smoothstep(hl - 0.08, hl, y) * (1 - m)
    img = shade(img, 1 - 0.25 * edge)
    return mix(img, hair, m)


def paint_mask(spec, rng, th, y, X, Y):
    """Omar's sack hood: pale greenish cotton/burlap, two big black eye holes, dirt, gathered neck."""
    h, w = th.shape
    ath = np.abs(th)
    base = rgb("#a9ae8a")  # pale sickly green-grey like the reference
    img = fabric(h, w, base, rng, folds=0.16, grain=0.10, weave=0.05, fold_src=("camera", (0.3, 0.15, 0.75, 0.7)))
    burlap = photo_detail("grass", h, w, rng, zoom=2.0, sigma=1.2)
    img = shade(img, 1 + burlap * 0.06)
    n = fbm(h, w, 5, rng, octaves=5)
    img = mix(img, rgb("#d6d6be"), smoothstep(0.55, 0.8, n) * 0.35)
    img = grime(img, rng, amount=0.35, color=(0.42, 0.40, 0.30), scale=6)
    # side / back darker (baked)
    img = shade(img, 1 - 0.18 * smoothstep(50, 150, ath))
    # eye holes: big hollow ovals with soft dark rims (cut into the sack, the inside is pitch black)
    hole_y = 103.0
    for sx in (-1, 1):
        cx = 34.0 * sx
        rim = ell(X, Y, cx, hole_y - 3, 34, 38, 14)
        img = mix(img, rgb("#3a3a2c"), rim * 0.55)
        rim2 = ell(X, Y, cx, hole_y - 1, 27, 30, 6)
        img = mix(img, rgb("#1a1a14"), rim2 * 0.6)
        hole = ell(X, Y, cx, hole_y, 20.5, 24.5, 2.0)  # big hollow eye holes
        jag = (fbm(h, w, 30, rng, octaves=2) - 0.5) * 0.7
        hole = np.clip(hole + jag * hole * (1 - hole) * 4, 0, 1)
        img = mix(img, rgb("#020202"), hole)
        # grime streaks running down from the holes
        for k in range(3):
            xo = cx + rng.uniform(-10, 10)
            streak = np.clip(1 - np.abs(X - xo) / rng.uniform(2.0, 4.0), 0, 1) * (Y < hole_y - 12) * smoothstep(hole_y - rng.uniform(40, 75), hole_y - 15, Y)
            img = mix(img, rgb("#4a4636"), streak * 0.45)
    # mouth area: a faint wet stain, nose bump shadow
    img = mix(img, rgb("#6a5a44"), ell(X, Y, 0, 40, 22, 12, 8) * 0.18)
    img = mix(img, rgb("#4a0808"), ell(X, Y, 6, 30, 7, 14, 4) * 0.2)
    img = shade(img, 1 - 0.12 * ell(X, Y, 0, 68, 14, 6, 4))
    # blood spatter
    bm = blood(h, w, rng, amount=0.12, scale=10, splatter=0.6) * (Y < 90)
    img = mix(img, rgb("#4a0a0a"), bm * 0.75)
    # seams: back (theta 180) and over the crown
    seam = np.clip(1 - (180 - ath) / 3.0, 0, 1) + np.clip(1 - np.abs(Y - 214) / 2.0, 0, 1) * 0.6
    stitch = (np.sin(Y * 1.4) > 0.3) * np.clip(1 - (180 - ath) / 5.0, 0, 1)
    img = shade(img, 1 - 0.35 * np.clip(seam, 0, 1) - 0.25 * stitch)
    # gathered neck: tan / brown, vertical pinch folds, darker under the rope
    neck = 1 - smoothstep(-10, 6, Y)
    folds = np.sin(np.deg2rad(th) * 14 + fbm(h, w, 6, rng, octaves=2) * 6) * 0.5 + 0.5
    ncol = mix(fill(h, w, rgb("#9a865e")), rgb("#6a5a3c"), folds[..., None][..., 0] * 0.6)
    ncol = shade(ncol, 1 + photo_detail("grass", h, w, rng, zoom=2.0, sigma=1.5) * 0.08)
    img = mix(img, ncol, neck)
    img = shade(img, 1 - 0.45 * np.exp(-((Y + 50) / 12.0) ** 2))  # rope shadow
    return img


def paint_head(spec, rng):
    h, w = canvas(A.HEAD)
    th, y, X, Y = head_space(h, w)
    kind = spec.get("head", "face")
    if kind == "mask":
        return paint_mask(spec, rng, th, y, X, Y)
    if kind == "featureless":
        return paint_featureless_head(spec, rng, th, y, X, Y)
    img = paint_face(spec, rng, th, y, X, Y)
    img = paint_hair_on_head(img, spec, rng, th, y, X, Y)
    if spec.get("head_blood"):
        bm = blood(h, w, rng, amount=spec["head_blood"], scale=8, splatter=0.5)
        img = mix(img, blood_color(rng, h, w), bm * 0.8)
    if spec.get("head_grime"):
        img = grime(img, rng, amount=spec["head_grime"], color=(0.2, 0.18, 0.14), scale=5)
    return img


def paint_featureless_head(spec, rng, th, y, X, Y):
    h, w = th.shape
    ath = np.abs(th)
    img = spec["surface"](h, w, rng)
    lum = np.ones((h, w), np.float32)
    lum *= 1 - 0.12 * smoothstep(45, 130, ath)
    lum *= 1 - 0.18 * sym_ell(X, Y, 30, 104, 20, 11, 8)
    lum *= 1 + 0.08 * ell(X, Y, 0, 85, 7, 22, 6)
    lum *= 1 - 0.18 * ell(X, Y, 0, 66, 12, 5, 4)
    lum *= 1 - 0.3 * smoothstep(-35, -2, Y) * (1 - smoothstep(-1, 4, Y)) * (1 - smoothstep(40, 80, ath))
    return shade(img, lum)


# ----------------------------------------------------------------------------------------------- torso
def torso_space(h, w):
    u, v = uv_grid(h, w)
    th = 180.0 - 360.0 * u   # + = character's right
    return th.astype(np.float32), v.astype(np.float32)


def band(t, a, b, soft=0.004):
    return smoothstep(a - soft, a + soft, t) * (1 - smoothstep(b - soft, b + soft, t))


def ang_band(th, a, b, soft=1.5):
    ath = np.abs(th)
    return smoothstep(a - soft, a + soft, ath) * (1 - smoothstep(b - soft, b + soft, ath))


def torso_ao(h, w, th, t, female=False, under_bust=0.585):
    ath = np.abs(th)
    lum = np.ones((h, w), np.float32)
    lum *= 1 - 0.10 * smoothstep(60, 95, ath) * (1 - smoothstep(95, 130, ath))  # sides
    lum *= 1 - 0.25 * np.exp(-((t - 0.80) / 0.05) ** 2) * np.exp(-((ath - 90) / 18) ** 2)  # armpits
    lum *= 1 - 0.12 * np.exp(-(t / 0.05) ** 2)  # crotch
    if female:
        lum *= 1 - 0.18 * np.exp(-((t - under_bust) / 0.03) ** 2) * (1 - smoothstep(25, 55, ath))  # under bust
    return lum


def stripes(h, w, t_or_coord, period_px, colors, rng):
    idx = np.floor(t_or_coord / period_px).astype(int) % len(colors)
    pal = np.stack([rgb(c) for c in colors])
    out = pal[idx]
    # knit texture
    y, x = np.mgrid[0:h, 0:w]
    knit = (np.sin(x * 1.6) * np.sin(y * 1.1)) * 0.06
    out = shade(out, 1 + knit + (rng.rand(h, w).astype(np.float32) - 0.5) * 0.08)
    return out


P3_STRIPES = ["#5a3a22", "#6a6a2a", "#b0582a", "#2a6a6a", "#8a6a2a", "#3a2a1e", "#7a2a22", "#4a6a3a",
              "#c07a3a", "#2a4a5a"]


def paint_torso(spec, rng):
    h, w = canvas(A.TORSO)
    th, t = torso_space(h, w)
    ath = np.abs(th)
    fn = spec["torso"]
    img = fn(spec, rng, h, w, th, t)
    img = shade(img, torso_ao(h, w, th, t, spec.get("female", False), 0.615 if spec.get("feminine") else 0.585))
    return img


def skin_layer(spec, rng, h, w):
    return skin(h, w, rgb(spec["skin"]), rng, mottle=0.07, pores=0.035, redness=0.2)


def torso_p1(spec, rng, h, w, th, t):
    ath = np.abs(th)
    sk = skin_layer(spec, rng, h, w)
    white = fabric(h, w, rgb("#d8dcd4"), rng, folds=0.16, grain=0.06, sweat=0.6, stains=0.25,
                   stain_color=rgb("#a8a088"))
    red = rgb("#b01e24")
    # tank top coverage: front panel |th|<52 up to scoop neck, back |th|>128, straps over the shoulder
    neck_front = 0.86 - 0.06 * np.exp(-(th / 22) ** 2)
    front = (ath < 50) * (t < neck_front)
    back = (ath > 130) * (t < 0.92)
    strap = (((ath > 34) & (ath < 52)) | ((ath > 128) & (ath < 146))) * (t < 0.95)
    top_strap = ((ath > 34) & (ath < 146)) * band(t, 0.945, 0.978)
    side = (t < 0.70) * 1.0  # below the armhole the top wraps all around
    cover = np.clip(front + back + strap + top_strap + side, 0, 1)
    cover = blur(cover.astype(np.float32), 1.0)
    img = mix(sk, white, cover)
    # red trim along the edges
    edge = np.clip(np.abs(ndimage.sobel(cover, 0)) + np.abs(ndimage.sobel(cover, 1)), 0, 1)
    edge = smoothstep(0.05, 0.3, blur(edge, 1.2)) * (t > 0.3)
    img = mix(img, red, edge * 0.95)
    # chest sash + number
    sash = band(t, 0.60, 0.645) * (ath < 75) + band(t, 0.74, 0.77) * (ath < 75) * 0.0
    img = mix(img, red, sash * (ath < 70))
    img = mix(img, red * 0.9, band(t, 0.655, 0.67) * (ath > 22) * (ath < 70))
    num = text_mask(h, w, "19", w * 0.5, h * (1 - 0.70), int(h * 0.10), stroke=2, stretch_x=1.1)
    img = mix(img, rgb("#1a1416"), num * 0.95)
    num_b = text_mask(h, w, "19", w * 0.0 + 4, h * (1 - 0.70), int(h * 0.09), stroke=2)
    # shorts (light blue / cyan) below the waistband
    shorts = fabric(h, w, rgb("#7ec8cc"), rng, folds=0.2, grain=0.05)
    shorts_m = 1 - smoothstep(0.285, 0.295, t)
    img = mix(img, shorts, shorts_m)
    img = mix(img, rgb("#5aa8ae"), band(t, 0.25, 0.29))
    img = mix(img, rgb("#e8e8e8"), band(t, 0.20, 0.25) * np.clip(1 - np.abs(th) / 2.5, 0, 1) * 0.8)
    return img


def torso_p2(spec, rng, h, w, th, t):
    ath = np.abs(th)
    sk = skin_layer(spec, rng, h, w)
    red = fabric(h, w, rgb("#9e1a22"), rng, folds=0.24, grain=0.08, fold_scale=1.2)
    knit = np.sin(np.mgrid[0:h, 0:w][1] * 1.3) * 0.05
    red = shade(red, 1 + knit)
    hem = 0.50 + 0.012 * np.sin(np.deg2rad(th) * 3 + 0.4)
    neckline = 0.986 - 0.04 * np.exp(-(th / 32) ** 2)
    top = smoothstep(hem - 0.006, hem + 0.006, t) * (1 - smoothstep(neckline - 0.005, neckline + 0.005, t))
    img = mix(sk, red, top)
    img = mix(img, rgb("#7a1218"), band(t, hem, hem + 0.02) * 0.6)  # hem rib
    # bust shading on the top
    img = shade(img, 1 + 0.10 * np.exp(-((t - 0.68) / 0.05) ** 2) * np.exp(-((ath - 24) / 14) ** 2))
    # navel
    nav = np.exp(-((t - 0.37) / 0.009) ** 2) * np.exp(-(th / 2.2) ** 2)
    img = shade(img, 1 - 0.5 * nav)
    # sweatpants
    pants = fabric(h, w, rgb("#cfcfcb"), rng, folds=0.26, grain=0.06, fold_scale=1.1, stains=0.2,
                   stain_color=rgb("#a0a098"))
    pm = 1 - smoothstep(0.268, 0.278, t)
    img = mix(img, pants, pm)
    img = mix(img, rgb("#b8b8b4"), band(t, 0.21, 0.27) * 0.7)
    # drawstring
    ds = (np.abs(th - 6) < 1.2) * band(t, 0.13, 0.25) + (np.abs(th + 6) < 1.2) * band(t, 0.15, 0.25)
    img = mix(img, rgb("#f0f0f0"), ds * 0.9)
    return img


def torso_p3(spec, rng, h, w, th, t):
    ath = np.abs(th)
    y = np.mgrid[0:h, 0:w][0].astype(np.float32)
    swe = stripes(h, w, (h - y) + fbm(h, w, 8, rng, octaves=2) * 3, 4.6 * S, P3_STRIPES, rng)
    swe = shade(swe, 1 + np.tanh(fold_layer(h, w, rng, 1.0) * 0.8) * 0.18)
    rib = band(t, 0.30, 0.36)
    swe = shade(swe, 1 - 0.12 * rib * (np.sin(np.mgrid[0:h, 0:w][1] * 1.5) > 0))
    jeans = denim(h, w, rgb("#4a5c80"), rng, fade=0.35)
    jm = 1 - smoothstep(0.30, 0.31, t)
    img = mix(swe, jeans, jm)
    img = mix(img, rgb("#3a4868"), band(t, 0.25, 0.30) * jm)
    # button + fly stitch
    img = mix(img, rgb("#a89060"), np.exp(-((t - 0.275) / 0.01) ** 2) * np.exp(-(th / 2.0) ** 2))
    img = mix(img, rgb("#c09040"), (np.abs(th - 3) < 0.8) * band(t, 0.06, 0.25) * 0.6)
    # neckline
    sk = skin_layer(spec, rng, h, w)
    img = mix(img, sk, smoothstep(0.975, 0.985, t))
    return img


def torso_p4(spec, rng, h, w, th, t):
    ath = np.abs(th)
    white = fabric(h, w, rgb("#dcd8d8"), rng, folds=0.2, grain=0.06, sweat=0.4, stains=0.2,
                   stain_color=rgb("#c0a8a8"))
    black = fabric(h, w, rgb("#1c1a1e"), rng, folds=0.3, grain=0.06)
    # raglan: black wedge from the armpit (t .78, |th| 90+-22) widening to the neck (+-60)
    half = 22 + np.clip((t - 0.78) / 0.2, 0, 1) * 42
    rag = smoothstep(0.775, 0.79, t) * (np.abs(ath - 90) < half)
    rag = blur(rag.astype(np.float32), 1.2)
    img = mix(white, black, rag)
    img = mix(img, black, smoothstep(0.972, 0.982, t))
    # purple shorts
    shorts = fabric(h, w, rgb("#64547e"), rng, folds=0.26, grain=0.08, fold_scale=1.1)
    sm = 1 - smoothstep(0.295, 0.305, t)
    img = mix(img, shorts, sm)
    img = mix(img, rgb("#4e4066"), band(t, 0.25, 0.30))
    img = shade(img, 1 - 0.15 * band(t, 0.30, 0.33))
    return img


def torso_omar(spec, rng, h, w, th, t):
    ath = np.abs(th)
    shirt = plaid(h, w, rng, period=15 * S)
    # button placket
    plk = (np.abs(th) < 4.5)
    shirt = shade(shirt, 1 - 0.25 * np.clip(1 - np.abs(np.abs(th) - 4.5) / 1.0, 0, 1))
    btn = np.zeros((h, w), np.float32)
    for bt in np.arange(0.36, 0.96, 0.11):
        btn = np.maximum(btn, np.exp(-((t - bt) / 0.008) ** 2) * np.exp(-(th / 1.8) ** 2))
    shirt = mix(shirt, rgb("#c8c0a8"), btn)
    # collar
    shirt = shade(shirt, 1 - 0.3 * band(t, 0.955, 0.965))
    bm = blood(h, w, rng, amount=0.32, scale=7, splatter=1.2)
    shirt = mix(shirt, blood_color(rng, h, w), bm * 0.85)
    jeans = denim(h, w, rgb("#2e3a56"), rng, fade=0.3)
    jb = blood(h, w, rng, amount=0.3, scale=6, splatter=0.8)
    jeans = mix(jeans, blood_color(rng, h, w) * 0.8, jb * 0.8)
    jm = 1 - smoothstep(0.258, 0.268, t)
    img = mix(shirt, jeans, jm)
    belt = band(t, 0.225, 0.265)
    img = mix(img, rgb("#2a1c14"), belt)
    img = mix(img, rgb("#8a8270"), belt * (ath < 4) * 0.9)
    img = grime(img, rng, amount=0.3, color=(0.12, 0.08, 0.06))
    return img


# ----------------------------------------------------------------------------------------------- limbs
def limb_space(h, w):
    u, v = uv_grid(h, w)
    th = 180.0 - 360.0 * u
    return th.astype(np.float32), v.astype(np.float32)


def paint_arm(spec, rng):
    h, w = canvas(A.ARM)
    th, t = limb_space(h, w)
    ath = np.abs(th)
    img = spec["arm"](spec, rng, h, w, th, t)
    lum = 1 - 0.12 * smoothstep(60, 170, ath)
    lum *= 1 - 0.18 * np.exp(-((t - 0.5) / 0.05) ** 2) * smoothstep(90, 10, ath)  # elbow pit (front)
    return shade(img, lum)


def arm_skin(spec, rng, h, w, th, t):
    img = skin_layer(spec, rng, h, w)
    # elbow (back) wrinkles, slight muscle shading
    img = shade(img, 1 + 0.06 * np.exp(-((t - 0.75) / 0.12) ** 2) * np.cos(np.deg2rad(th)))
    return img


def sleeve(color, cuff_t=0.06, folds=0.28):
    def fn(spec, rng, h, w, th, t):
        sk = skin_layer(spec, rng, h, w)
        cl = fabric(h, w, rgb(color), rng, folds=folds, grain=0.07, fold_scale=1.3)
        # bunched folds near the elbow and wrist
        cl = shade(cl, 1 + 0.12 * np.sin(t * 60 + np.deg2rad(th) * 2) * np.exp(-((t - 0.45) / 0.12) ** 2))
        img = mix(sk, cl, smoothstep(cuff_t - 0.01, cuff_t + 0.005, t))
        img = mix(img, rgb(color) * 0.8, band(t, cuff_t, cuff_t + 0.07) * 0.7)
        return img
    return fn


def arm_p3(spec, rng, h, w, th, t):
    y = np.mgrid[0:h, 0:w][0].astype(np.float32)
    sw = stripes(h, w, (h - y) + fbm(h, w, 6, rng, octaves=2) * 3, 4.6 * S, P3_STRIPES[::-1], rng)
    sk = skin_layer(spec, rng, h, w)
    img = mix(sk, sw, smoothstep(0.045, 0.055, t))
    img = shade(img, 1 - 0.15 * band(t, 0.05, 0.12))
    return img


def arm_omar(spec, rng, h, w, th, t):
    sh = plaid(h, w, rng, period=15 * S)
    bm = blood(h, w, rng, amount=0.3, scale=6, splatter=1.5) * (1 - smoothstep(0.3, 0.7, t) * 0.6)
    sh = mix(sh, blood_color(rng, h, w), bm * 0.85)
    sk = skin(h, w, rgb(spec["skin"]), rng, mottle=0.1)
    img = mix(sk, sh, smoothstep(0.035, 0.045, t))
    img = shade(img, 1 - 0.2 * band(t, 0.04, 0.10))
    return grime(img, rng, amount=0.3, color=(0.1, 0.06, 0.05))


def paint_leg(spec, rng):
    h, w = canvas(A.LEG)
    th, t = limb_space(h, w)
    ath = np.abs(th)
    img = spec["leg"](spec, rng, h, w, th, t)
    lum = 1 - 0.12 * smoothstep(60, 170, ath)
    lum *= 1 - 0.15 * np.exp(-((t - 0.47) / 0.04) ** 2) * smoothstep(80, 150, ath)  # knee pit
    lum *= 1 + 0.08 * np.exp(-((t - 0.5) / 0.04) ** 2) * smoothstep(60, 0, ath)    # kneecap
    return shade(img, lum)


def leg_p1(spec, rng, h, w, th, t):
    sk = skin_layer(spec, rng, h, w)
    sk = shade(sk, 1 + 0.08 * np.exp(-((t - 0.28) / 0.1) ** 2) * np.cos(np.deg2rad(th + 180)))  # calf
    shorts = fabric(h, w, rgb("#7ec8cc"), rng, folds=0.22, grain=0.05)
    img = mix(sk, shorts, smoothstep(0.665, 0.675, t))
    img = mix(img, rgb("#5aa8ae"), band(t, 0.67, 0.70) * 0.8)
    sock = fabric(h, w, rgb("#e0e0dc"), rng, folds=0.1, grain=0.12)
    img = mix(img, sock, 1 - smoothstep(0.155, 0.165, t))
    img = mix(img, rgb("#b01e24"), band(t, 0.125, 0.14) + band(t, 0.10, 0.112))
    return img


def leg_p2(spec, rng, h, w, th, t):
    pants = fabric(h, w, rgb("#cfcfcb"), rng, folds=0.32, grain=0.06, fold_scale=1.4, stains=0.3,
                   stain_color=rgb("#9a9a90"))
    pants = shade(pants, 1 + 0.14 * np.sin(t * 70 + np.deg2rad(th) * 1.5) * (1 - smoothstep(0.05, 0.3, t)))
    pants = shade(pants, 1 - 0.12 * band(t, 0.0, 0.06))
    # side seam
    pants = shade(pants, 1 - 0.2 * np.clip(1 - np.abs(np.abs(th) - 90) / 2.0, 0, 1))
    return grime(pants, rng, amount=0.25, color=(0.35, 0.33, 0.3))


def leg_p3(spec, rng, h, w, th, t):
    sk = skin_layer(spec, rng, h, w)
    sk = shade(sk, 1 + 0.07 * np.exp(-((t - 0.28) / 0.1) ** 2) * np.cos(np.deg2rad(th + 180)))
    jeans = denim(h, w, rgb("#4a5c80"), rng, fade=0.4)
    hem = 0.83 + 0.012 * np.sin(np.deg2rad(th) * 7) + (fbm(h, w, 20, rng, octaves=2) - 0.5) * 0.02
    img = mix(sk, jeans, smoothstep(hem - 0.004, hem + 0.004, t))
    fray = band(t, hem - 0.03, hem) * (np.sin(np.mgrid[0:h, 0:w][1] * 2.2) > 0.2)
    img = mix(img, rgb("#b8c0cc"), fray * 0.6)
    return img


def leg_p4(spec, rng, h, w, th, t):
    sk = skin_layer(spec, rng, h, w)
    sk = shade(sk, 1 + 0.05 * np.exp(-((t - 0.28) / 0.1) ** 2) * np.cos(np.deg2rad(th + 180)))
    shorts = fabric(h, w, rgb("#64547e"), rng, folds=0.3, grain=0.08, fold_scale=1.2)
    img = mix(sk, shorts, smoothstep(0.595, 0.605, t))
    img = shade(img, 1 - 0.15 * band(t, 0.6, 0.63))
    sock = fabric(h, w, rgb("#d4d4d0"), rng, folds=0.1, grain=0.1)
    img = mix(img, sock, 1 - smoothstep(0.075, 0.085, t))
    return img


def leg_omar(spec, rng, h, w, th, t):
    jeans = denim(h, w, rgb("#2e3a56"), rng, fade=0.35)
    jeans = shade(jeans, 1 + 0.14 * np.sin(t * 50 + np.deg2rad(th) * 2) * (1 - smoothstep(0.05, 0.25, t)))
    bm = blood(h, w, rng, amount=0.32, scale=6, splatter=1.0)
    jeans = mix(jeans, blood_color(rng, h, w) * 0.8, bm * 0.8)
    jeans = shade(jeans, 1 - 0.2 * np.clip(1 - np.abs(np.abs(th) - 90) / 2.0, 0, 1))
    return grime(jeans, rng, amount=0.45, color=(0.14, 0.11, 0.08))


def paint_hand(spec, rng):
    h, w = canvas(A.HAND)
    u, v = uv_grid(h, w)
    tone = rgb(spec.get("hand_skin", spec["skin"]))
    img = skin(h, w, tone, rng, mottle=0.08, pores=0.04)
    # fingers in the lower half (v < 0.5): 4 rounded finger columns (the first person hand wraps one column around
    # each finger tube, dorsal side = column centre), knuckle creases, nails at the tips
    fing = (v < 0.52)
    cu = (u * 4) % 1.0
    roundness = 1 - 0.30 * ((cu - 0.5) * 2) ** 2
    img = shade(img, np.where(fing, roundness, 1.0))
    sep = np.clip(1 - np.abs(cu - 0.0) / 0.06, 0, 1) + np.clip(1 - np.abs(cu - 1.0) / 0.06, 0, 1)
    img = shade(img, 1 - 0.22 * sep * fing * (u > 0.03) * (u < 0.97))
    img = shade(img, 1 - 0.14 * (np.abs(v - 0.52) < 0.025))
    for kv, kw in ((0.33, 0.012), (0.17, 0.010)):
        img = shade(img, 1 - 0.14 * (np.abs(v - kv) < kw) * (np.abs(cu - 0.5) < 0.3))
    # back of the hand: knuckle bumps and faint tendons running to the wrist
    knuck = np.exp(-((v - 0.56) / 0.04) ** 2) * np.exp(-((cu - 0.5) / 0.2) ** 2)
    img = shade(img, 1 + 0.10 * knuck * (v > 0.5))
    tend = np.exp(-((cu - 0.5) / 0.08) ** 2) * smoothstep(0.58, 0.7, v) * (1 - smoothstep(0.85, 1.0, v))
    img = shade(img, 1 + 0.05 * tend)
    nail_col = rgb(spec.get("nails", "#d8b0a0"))
    nail = ell(cu * 40, v * 160, 20, 7.0, 11.5, 8.5, 1.5) * (v < 0.16)
    img = mix(img, nail_col, nail * 0.85)
    img = mix(img, np.clip(nail_col * 1.25 + 0.08, 0, 1), ell(cu * 40, v * 160, 20, 2.5, 8, 2.2, 1.0) * nail * 0.6)
    img = shade(img, 1 - 0.15 * smoothstep(0.6, 1.0, v))  # wrist
    if spec.get("hand_blood"):
        img = mix(img, blood_color(rng, h, w), blood(h, w, rng, amount=spec["hand_blood"], scale=5, splatter=1) * 0.85)
    if spec.get("hand_grime"):
        img = grime(img, rng, amount=spec["hand_grime"], color=(0.12, 0.1, 0.09), scale=3)
    return img


def paint_foot(spec, rng):
    h, w = canvas(A.FOOT)
    out = np.zeros((h, w, 3), np.float32)
    half = w // 2
    side = spec["shoe"](spec, rng, h, half, "side")
    top = spec["shoe"](spec, rng, h, half, "top")
    out[:, :half] = side
    out[:, half:] = top
    return out


def sneaker(upper, accent, sole="#d8d8d0", dirty=0.3):
    def fn(spec, rng, h, w, view):
        u, v = uv_grid(h, w)
        img = fabric(h, w, rgb(upper), rng, folds=0.12, grain=0.1)
        if view == "side":
            soleband = v < 0.24
            img = mix(img, rgb(sole), soleband * 1.0)
            img = shade(img, 1 - 0.3 * (np.abs(v - 0.24) < 0.03))
            img = shade(img, 1 - 0.5 * (v < 0.05))
            swoosh = np.clip(1 - np.abs(v - (0.38 + 0.25 * u)) / 0.05, 0, 1) * (u > 0.25) * (u < 0.8)
            img = mix(img, rgb(accent), swoosh)
            img = mix(img, rgb(accent), (u < 0.18) * (v > 0.24) * 0.6)  # heel tab
            img = shade(img, 1 - 0.25 * (v > 0.9))
        else:
            # top: toe at v=0, ankle opening at v=1; laces in the middle
            lace = (np.abs(u - 0.5) < 0.22) * (v > 0.35) * (v < 0.82)
            img = shade(img, 1 - 0.15 * lace)
            for lv in np.arange(0.4, 0.8, 0.09):
                img = mix(img, rgb("#e8e8e4"), (np.abs(v - lv) < 0.022) * (np.abs(u - 0.5) < 0.2))
            img = mix(img, rgb("#201818"), (v > 0.86) * (np.abs(u - 0.5) < 0.3))
            img = shade(img, 1 - 0.2 * smoothstep(0.3, 0.0, v) * 0)
        return grime(img, rng, amount=dirty, color=(0.3, 0.27, 0.22), scale=3)
    return fn


def boot(color="#8a7652", sole="#2a2420"):
    def fn(spec, rng, h, w, view):
        u, v = uv_grid(h, w)
        img = fabric(h, w, rgb(color), rng, folds=0.25, grain=0.12, fold_src=("brick", None))
        img = shade(img, 1 + photo_detail("gravel", h, w, rng, zoom=3, sigma=1.5) * 0.06)
        if view == "side":
            img = mix(img, rgb(sole), v < 0.2)
            img = shade(img, 1 - 0.4 * (np.abs(v - 0.2) < 0.03))
            stitch = (np.abs(v - 0.25) < 0.012) * (np.sin(u * 120) > 0)
            img = shade(img, 1 - 0.3 * stitch)
            img = mix(img, rgb("#4a3a28"), (u > 0.72) * (v > 0.2) * (v < 0.45) * 0.5)  # toe cap scuff
        else:
            lace = (np.abs(u - 0.5) < 0.2) * (v > 0.3)
            for lv in np.arange(0.35, 0.95, 0.08):
                img = mix(img, rgb("#3a2a1a"), (np.abs(v - lv) < 0.02) * (np.abs(u - 0.5) < 0.2))
            img = shade(img, 1 - 0.15 * lace)
        bm = blood(h, w, rng, amount=0.25, scale=5, splatter=0.8)
        img = mix(img, rgb("#3a0606"), bm * 0.7)
        return grime(img, rng, amount=0.5, color=(0.15, 0.12, 0.08), scale=3)
    return fn


def barefoot(spec, rng, h, w, view):
    u, v = uv_grid(h, w)
    img = skin(h, w, rgb(spec["skin"]), rng, mottle=0.12)
    if view == "side":
        img = shade(img, 1 - 0.3 * (v < 0.12))
    else:
        toes = (v < 0.25) * (np.abs(((u * 5) % 1.0) - 0.5) > 0.4)
        img = shade(img, 1 - 0.35 * toes)
    return grime(img, rng, amount=0.5, color=(0.15, 0.12, 0.1), scale=3)


# ----------------------------------------------------------------------------------------------- cutout parts
def paint_hair_region(spec, rng):
    h, w = canvas(A.HAIR)
    u, v = uv_grid(h, w)
    if spec.get("hair_style") == "none" or "hair" not in spec:
        img = spec["surface"](h, w, rng) if "surface" in spec else fill(h, w, (0.3, 0.3, 0.3))
        return img, np.ones((h, w), np.float32)
    img = hair_strands(h, w, rgb(spec["hair"]), rng, highlight=rgb(spec["hair_hi"]) if spec.get("hair_hi") else None,
                       contrast=0.45)
    img = shade(img, 0.75 + 0.35 * v)
    # strand tips at the bottom
    tip = 0.18 + 0.12 * value_noise(1, w, 1, 22, rng)[0] + 0.05 * rng.rand(w)
    alpha = (v > tip[None, :]).astype(np.float32)
    return img, alpha


def paint_extra(spec, rng):
    h, w = canvas(A.EXTRA)
    u, v = uv_grid(h, w)
    kind = spec.get("extra", "hair")
    if kind == "apron":
        # black leather / rubber butcher's apron with blood splashed over it (not soaked red all over)
        img = fabric(h, w, rgb("#1c1a1a"), rng, folds=0.3, grain=0.1, fold_scale=0.8,
                     fold_src=("camera", (0.3, 0.15, 0.75, 0.7)))
        bm = blood(h, w, rng, amount=0.36, scale=7, splatter=4.0)
        img = mix(img, blood_color(rng, h, w), bm * 0.9)
        drip = np.zeros((h, w), np.float32)
        for _ in range(14):
            x = rng.randint(0, w)
            y0 = rng.randint(0, h // 2)
            ln = rng.randint(h // 8, h // 2)
            drip[y0:y0 + ln, max(0, x - 1):x + 2] = 1
        img = mix(img, rgb("#5a0606"), blur(drip, 1.5) * 0.8)
        img = grime(img, rng, amount=0.3, color=(0.08, 0.06, 0.05))
        # stitched hem + frayed bottom edge
        img = shade(img, 1 - 0.3 * (np.abs(v - 0.06) < 0.006))
        edge = 0.025 + 0.02 * value_noise(1, w, 1, 30, rng)[0] + 0.012 * rng.rand(w)
        alpha = (v > edge[None, :]).astype(np.float32)
        return img, alpha
    if kind == "rags":
        img = fabric(h, w, rgb("#5a5244"), rng, folds=0.3, grain=0.12, stains=0.6, stain_color=rgb("#2a2018"))
        img = mix(img, rgb("#3a0808"), blood(h, w, rng, amount=0.3, scale=6, splatter=1) * 0.8)
        holes = smoothstep(0.7, 0.72, fbm(h, w, 6, rng, octaves=3))
        edge = 0.05 + 0.08 * value_noise(1, w, 1, 12, rng)[0]
        alpha = ((v > edge[None, :]) & (holes < 0.5)).astype(np.float32)
        return img, alpha
    if kind == "surface":
        return spec["surface"](h, w, rng), np.ones((h, w), np.float32)
    # long hair panel: strands running down, wavy for p2, straight for p3
    wav = spec.get("wavy", 0.0)
    img = hair_strands(h, w, rgb(spec["hair"]), rng, highlight=rgb(spec["hair_hi"]) if spec.get("hair_hi") else None,
                       contrast=0.55, wave=wav, sheen=0.2)
    img = shade(img, 0.7 + 0.35 * v)
    # side strands darker (depth)
    img = shade(img, 1 - 0.2 * (np.abs(u - 0.5) > 0.38))
    if spec.get("straight_cut"):
        tip = 0.05 + 0.025 * rng.rand(w) + 0.02 * np.abs(np.linspace(-1, 1, w))
    else:
        tip = 0.06 + 0.16 * value_noise(1, w, 1, 9, rng)[0] + 0.05 * rng.rand(w)
    alpha = (v > tip[None, :]).astype(np.float32)
    return img, alpha


def paint_misc(spec, rng):
    h, w = canvas(A.MISC)
    img = np.zeros((h, w, 3), np.float32)
    alpha = np.zeros((h, w), np.float32)
    gh = 24 * S  # glasses strip at the top
    gl = spec.get("glasses")
    if gl:
        gw = w
        col = rgb(spec.get("glasses_color", "#141214"))

        def frames(d):
            cx1, cx2, cy = gw * 0.27, gw * 0.73, gh * 0.5
            if gl == "round":
                r = gh * 0.40
                for cx in (cx1, cx2):
                    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=255, width=int(S * 1.6))
            else:
                rw, rh = gw * 0.20, gh * 0.34
                for cx in (cx1, cx2):
                    d.rounded_rectangle([cx - rw, cy - rh, cx + rw, cy + rh], radius=int(S * 2), outline=255,
                                        width=int(S * 2.0))
            d.line([cx1 + gw * 0.13, cy - gh * 0.12, cx2 - gw * 0.13, cy - gh * 0.12], fill=255, width=int(S * 1.6))
            d.line([0, cy - gh * 0.18, cx1 - gw * 0.2, cy - gh * 0.18], fill=255, width=int(S * 1.6))
            d.line([cx2 + gw * 0.2, cy - gh * 0.18, gw, cy - gh * 0.18], fill=255, width=int(S * 1.6))

        m = draw_mask(gh, gw, frames)
        img[:gh] = col * (0.8 + 0.4 * m[..., None])
        alpha[:gh] = m
    # lower part: Omar's gathered sack skirt (ragged), or bangs / tuft hair
    lh = h - gh
    u, v = uv_grid(lh, w)
    kind = spec.get("misc", "hair")
    if kind == "skirt":
        cl = fabric(lh, w, rgb("#a8986e"), rng, folds=0.2, grain=0.12, weave=0.06)
        folds = np.sin(u * np.pi * 18 + fbm(lh, w, 4, rng, octaves=2) * 4) * 0.5 + 0.5
        cl = shade(cl, 0.7 + 0.45 * folds)
        cl = shade(cl, 0.65 + 0.4 * v)
        cl = grime(cl, rng, amount=0.4, color=(0.3, 0.25, 0.15))
        cl = mix(cl, rgb("#4a0a0a"), blood(lh, w, rng, amount=0.15, scale=6, splatter=0.6) * 0.7)
        edge = 0.08 + 0.12 * value_noise(1, w, 1, 16, rng)[0] + 0.05 * rng.rand(w)
        img[gh:] = cl
        alpha[gh:] = (v > edge[None, :])
    elif "hair" in spec and spec.get("hair_style") != "none":
        hs = hair_strands(lh, w, rgb(spec["hair"]), rng, highlight=rgb(spec["hair_hi"]) if spec.get("hair_hi") else None,
                          contrast=0.45)
        hs = shade(hs, 0.75 + 0.35 * v)
        tip = 0.15 + 0.2 * value_noise(1, w, 1, 14, rng)[0] + 0.08 * rng.rand(w)
        img[gh:] = hs
        alpha[gh:] = (v > tip[None, :])
    return img, alpha


def paint_rope(spec, rng):
    h, w = canvas(A.ROPE)
    u, v = uv_grid(h, w)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    twist = np.sin((yy * 0.55 + xx * 1.4) / S * 1.0) * 0.5 + 0.5
    img = fill(h, w, rgb("#a08a5e"))
    img = shade(img, 0.62 + 0.55 * twist)
    img = shade(img, 1 + photo_detail("grass", h, w, rng, zoom=3, sigma=1.2) * 0.1)
    img = grime(img, rng, amount=0.35, color=(0.3, 0.24, 0.15), scale=8)
    return img


def paint_swatch(spec, rng):
    h, w = canvas(A.SWATCH)
    img = np.zeros((h, w, 3), np.float32)
    cell = 16 * S
    cols = {
        "dark": rgb("#121012"), "white": rgb("#d8d4cc"), "metal": rgb("#8a8884"), "blood": rgb("#5a0808"),
        "skin": rgb(spec.get("skin", "#b09080")), "hair": rgb(spec.get("hair", "#3a2a20")),
    }
    for name, (cx, cy) in A.SWATCH_CELLS.items():
        c = cols[name]
        tile = fill(cell, cell, c)
        tile = shade(tile, 1 + (rng.rand(cell, cell).astype(np.float32) - 0.5) * 0.08)
        img[cy * cell:(cy + 1) * cell, cx * cell:(cx + 1) * cell] = tile
    return img


# ----------------------------------------------------------------------------------------------- surfaces (figures)
def burnt_surface(h, w, rng):
    base = fill(h, w, rgb("#3a2c22"))
    ihc = photo_detail("ihc", h, w, rng, zoom=1.0, sigma=10)
    grav = photo_detail("gravel", h, w, rng, zoom=2.0, sigma=2)
    n = fbm(h, w, 5, rng, octaves=5)
    base = mix(base, rgb("#a89a88"), smoothstep(0.5, 0.75, n + ihc * 0.08))  # ash
    base = mix(base, rgb("#0c0a08"), smoothstep(0.62, 0.78, 1 - n + grav * 0.05) * 0.85)  # char
    base = mix(base, rgb("#6a4a30"), smoothstep(0.45, 0.6, fbm(h, w, 9, rng, octaves=3)) * 0.35)
    base = shade(base, 1 + ihc * 0.12 + grav * 0.08)
    return base


def pale_plastic(h, w, rng):
    base = fill(h, w, rgb("#cfc4b4"))
    n = fbm(h, w, 4, rng, octaves=4)
    base = shade(base, 0.92 + 0.12 * n)
    chips = smoothstep(0.78, 0.8, fbm(h, w, 14, rng, octaves=3))
    base = mix(base, rgb("#6a6258"), chips * 0.7)
    scuff = photo_detail("moon", h, w, rng, zoom=1.2, sigma=4)
    base = shade(base, 1 + scuff * 0.05)
    return grime(base, rng, amount=0.35, color=(0.35, 0.3, 0.24), scale=5)


def corpse_skin(h, w, rng):
    base = skin(h, w, rgb("#8a8a70"), rng, mottle=0.18, pores=0.06)
    bruise = smoothstep(0.6, 0.8, fbm(h, w, 6, rng, octaves=4))
    base = mix(base, rgb("#4a3a4a"), bruise * 0.5)
    base = mix(base, rgb("#5a6a4a"), smoothstep(0.55, 0.75, fbm(h, w, 4, rng, octaves=3)) * 0.4)
    veins = photo_detail("retina", h, w, rng, zoom=2.0, sigma=3)
    base = shade(base, 1 - np.clip(veins, 0, 3) * 0.05)
    return grime(base, rng, amount=0.3, color=(0.2, 0.18, 0.12), scale=5)


# ----------------------------------------------------------------------------------------------- specs
def surface_fn(fn):
    def torso(spec, rng, h, w, th, t):
        return fn(h, w, rng)
    return torso


def mannequin_torso(fn, seams=True):
    def torso(spec, rng, h, w, th, t):
        img = fn(h, w, rng)
        if seams:
            img = shade(img, 1 - 0.35 * band(t, 0.39, 0.405, 0.002))  # waist joint
        return img
    return torso


def mannequin_limb(fn, joints):
    def limb(spec, rng, h, w, th, t):
        img = fn(h, w, rng)
        for j in joints:
            img = shade(img, 1 - 0.35 * band(t, j, j + 0.012, 0.002))
        return img
    return limb


def corpse_torso(spec, rng, h, w, th, t):
    sk = corpse_skin(h, w, rng)
    rag = fabric(h, w, rgb("#5e5646"), rng, folds=0.3, grain=0.12, stains=0.7, stain_color=rgb("#2a2218"))
    holes = smoothstep(0.66, 0.7, fbm(h, w, 5, rng, octaves=4))
    cover = (t < 0.93) * (1 - holes)
    img = mix(sk, rag, cover)
    pants = fabric(h, w, rgb("#4a3a2a"), rng, folds=0.3, grain=0.12, stains=0.5)
    img = mix(img, pants, (t < 0.28) * 1.0)
    bm = blood(h, w, rng, amount=0.35, scale=6, splatter=1.0)
    img = mix(img, rgb("#3a0606"), bm * 0.85)
    # ribs showing on exposed skin
    ribs = (np.sin(t * 160) > 0.6) * band(t, 0.55, 0.8) * (np.abs(th) < 60) * holes
    return shade(img, 1 - 0.2 * ribs)


def corpse_arm(spec, rng, h, w, th, t):
    sk = corpse_skin(h, w, rng)
    rag = fabric(h, w, rgb("#5e5646"), rng, folds=0.3, grain=0.12, stains=0.7, stain_color=rgb("#2a2218"))
    edge = 0.62 + 0.06 * np.sin(np.deg2rad(th) * 5)
    img = mix(sk, rag, (t > edge))
    return mix(img, rgb("#3a0606"), blood(h, w, rng, amount=0.25, scale=5, splatter=1) * 0.8)


def corpse_leg(spec, rng, h, w, th, t):
    sk = corpse_skin(h, w, rng)
    pants = fabric(h, w, rgb("#4a3a2a"), rng, folds=0.3, grain=0.12, stains=0.5)
    edge = 0.3 + 0.08 * np.sin(np.deg2rad(th) * 4) + (fbm(h, w, 12, rng, octaves=2) - 0.5) * 0.08
    img = mix(sk, pants, t > edge)
    holes = smoothstep(0.7, 0.73, fbm(h, w, 5, rng, octaves=3))
    img = mix(img, corpse_skin(h, w, rng), holes * (t > edge))
    return mix(img, rgb("#3a0606"), blood(h, w, rng, amount=0.25, scale=5, splatter=0.8) * 0.8)


# --------------------------------------------------------------------------------------------- grandmother
GRANNY_DRESS = "#b08a52"          # faded mustard-yellow house dress (clearly darker than her pale skin)
GRANNY_FLOWERS = ("#a8422e", "#8e2e2a", "#c46a34", "#5e7038")


def floral(h, w, rng, base=GRANNY_DRESS, density=1.0, scale=1.0, stains=0.35):
    """Faded floral print: small orange / red blossoms with yellow hearts and olive leaves on a pale ground."""
    img = fabric(h, w, rgb(base), rng, folds=0.2, grain=0.05, stains=stains * 0.6, stain_color=rgb("#b09a64"))
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    n = max(6, int(h * w / (300.0 * S * S) * density))
    for _ in range(n):
        cx, cy = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(2.8, 4.2) * S * scale
        # two leaves
        for k in (-1, 1):
            lx, ly = cx + k * r * 1.3, cy + r * 0.6
            dl = ((xx - lx) / (r * 0.9)) ** 2 + ((yy - ly) / (r * 0.45)) ** 2
            img = mix(img, rgb("#5e7038"), np.clip((1.0 - dl) * 2.5, 0, 1) * 0.85)
        col = rgb(GRANNY_FLOWERS[rng.randint(3)])
        # five petals as a lobed disc
        ang = np.arctan2(yy - cy, xx - cx)
        rad = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / r
        petal = 0.75 + 0.3 * np.cos(5 * ang + rng.uniform(0, 6.28))
        img = mix(img, col, np.clip((petal - rad) * 4.0, 0, 1) * 0.92)
        img = mix(img, rgb("#ecd060"), np.clip((0.3 - rad) * 8.0, 0, 1))
    img = desaturate(img, 0.1)
    return img


def old_skin(spec, rng, h, w):
    sk = skin(h, w, rgb(spec["skin"]), rng, mottle=0.16, pores=0.05, redness=0.05)
    spots = smoothstep(0.72, 0.76, fbm(h, w, 14, rng, octaves=3))
    return mix(sk, rgb("#8a6a58"), spots * 0.22)  # faint liver spots


def torso_granny(spec, rng, h, w, th, t):
    img = floral(h, w, rng, density=1.8, scale=1.25)   # a busy print that still reads at 426x240 through the VHS grade
    # round collar + a row of small buttons down the front
    img = mix(img, rgb("#e8e0c8"), band(t, 0.95, 0.985) * 0.85)
    for bt in (0.86, 0.76, 0.66, 0.56):
        img = mix(img, rgb("#efe8d8"), np.exp(-((t - bt) / 0.012) ** 2) * np.exp(-(th / 2.2) ** 2))
    # waist seam, a little gathered
    img = shade(img, 1 - 0.14 * band(t, 0.40, 0.43))
    img = shade(img, 1 + 0.08 * np.sin(np.deg2rad(th) * 18) * band(t, 0.30, 0.40))
    sk = old_skin(spec, rng, h, w)
    img = mix(img, sk, smoothstep(0.985, 0.995, t))
    if spec.get("dead"):
        img = mix(img, blood_color(rng, h, w), blood(h, w, rng, amount=0.55, scale=6, splatter=2.5) * 0.9)
    return grime(img, rng, amount=0.2, color=(0.3, 0.26, 0.16))


def arm_granny(spec, rng, h, w, th, t):
    # long floral sleeves down to the wrist with a pale lace cuff (only the bony hands show)
    sk = old_skin(spec, rng, h, w)
    cl = floral(h, w, rng, density=2.0, scale=0.8)
    cl = shade(cl, 1 + 0.12 * np.sin(t * 60 + np.deg2rad(th) * 2) * np.exp(-((t - 0.45) / 0.12) ** 2))
    img = mix(sk, cl, smoothstep(0.055, 0.065, t))
    img = mix(img, rgb("#ece4d0"), band(t, 0.06, 0.11) * 0.85)
    if spec.get("dead"):
        img = mix(img, blood_color(rng, h, w), blood(h, w, rng, amount=0.4, scale=5, splatter=2.0) * 0.85)
    return img


def leg_granny(spec, rng, h, w, th, t):
    # the dress reaches mid-calf; pale shins; white socks
    sk = old_skin(spec, rng, h, w)
    dress = floral(h, w, rng, density=1.8)
    hem = 0.30 + 0.01 * np.sin(np.deg2rad(th) * 5)
    img = mix(sk, dress, smoothstep(hem - 0.005, hem + 0.005, t))
    img = mix(img, rgb("#7a5a34"), band(t, hem, hem + 0.025) * 0.7)
    sock = fabric(h, w, rgb("#e4e0d4"), rng, folds=0.1, grain=0.1, stains=0.3, stain_color=rgb("#b8b098"))
    img = mix(img, sock, 1 - smoothstep(0.14, 0.15, t))
    if spec.get("dead"):
        img = mix(img, blood_color(rng, h, w), blood(h, w, rng, amount=0.45, scale=5, splatter=2.0) * 0.8 * (t > 0.3))
    return img


# --------------------------------------------------------------------------------------------- iteration 2 prisoners
def check(h, w, rng, px, py, ground="#8e8e8c", band="#6c6c6e", cross="#3e3e42", fine="#2a2a2e", light="#b8b8b2",
          folds=0.16, phase=(0.0, 0.0)):
    """Grey glen / windowpane check (the camerawoman's dress): px / py = period in pixels (4x canvas)."""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    jit = fbm(h, w, 6, rng, octaves=2) * 1.5
    fx = ((xx + jit) / px + phase[0]) % 1.0
    fy = ((yy + jit) / py + phase[1]) % 1.0
    bx = (fx > 0.10) & (fx < 0.42)
    by = (fy > 0.10) & (fy < 0.42)
    out = np.zeros((h, w, 3), np.float32)
    out[:] = rgb(ground)
    out[bx ^ by] = rgb(band)
    out[bx & by] = rgb(cross)
    thin = (np.abs(fx - 0.72) < 0.035) | (np.abs(fy - 0.72) < 0.035)
    out[thin & ~(bx | by)] = rgb(fine)
    hl = (np.abs(fx - 0.86) < 0.02) | (np.abs(fy - 0.86) < 0.02)
    out[hl & ~(bx | by)] = out[hl & ~(bx | by)] * 0.5 + rgb(light) * 0.5
    out = blur(out, 0.8)
    f = fold_layer(h, w, rng, 1.1)
    return shade(out, 1 + np.tanh(f * 0.8) * folds + (rng.rand(h, w).astype(np.float32) - 0.5) * 0.10)


def torso_p5(spec, rng, h, w, th, t):
    """Grey plaid long-sleeve mini dress: fitted bodice, crew neck, waist seam; the bottom of the strip (t < 0.40)
    is the flared skirt mesh (waist at v 0.40, hem at v 0), so its checks are painted squashed to stay square."""
    ath = np.abs(th)
    P = 7.5 * S
    bodice = check(h, w, rng, P, P)
    skirt = check(h, w, rng, P * 1.15, P * 0.62, phase=(0.3, 0.0), folds=0.12)
    # soft flare folds running down the skirt, wider at the hem
    fl = np.sin(np.deg2rad(th) * 9 + fbm(h, w, 3, rng, octaves=2) * 3) * (1 - t / 0.4)
    skirt = shade(skirt, 1 + 0.10 * fl)
    skirt = shade(skirt, 1 - 0.18 * band(t, 0.0, 0.035, 0.006))   # hem
    img = mix(bodice, skirt, 1 - smoothstep(0.393, 0.403, t))
    img = shade(img, 1 - 0.28 * band(t, 0.395, 0.41, 0.003))       # waist seam
    # bust darts / shaping, side seams
    img = shade(img, 1 + 0.10 * np.exp(-((t - 0.69) / 0.05) ** 2) * np.exp(-((ath - 24) / 14) ** 2))
    img = shade(img, 1 - 0.16 * np.clip(1 - np.abs(ath - 90) / 2.0, 0, 1) * (t > 0.4))
    # crew neck with a narrow binding
    sk = skin_layer(spec, rng, h, w)
    neck = 0.968 - 0.03 * np.exp(-(th / 30) ** 2)
    img = shade(img, 1 - 0.25 * band(t, neck - 0.012, neck, 0.003))
    img = mix(img, sk, smoothstep(neck - 0.002, neck + 0.004, t))
    return grime(img, rng, amount=0.12, color=(0.3, 0.28, 0.26))


def arm_p5(spec, rng, h, w, th, t):
    sk = skin_layer(spec, rng, h, w)
    cl = check(h, w, rng, 7.5 * S, 7.5 * S, folds=0.2)
    cl = shade(cl, 1 + 0.10 * np.sin(t * 60 + np.deg2rad(th) * 2) * np.exp(-((t - 0.45) / 0.12) ** 2))
    img = mix(sk, cl, smoothstep(0.045, 0.055, t))
    img = shade(img, 1 - 0.22 * band(t, 0.05, 0.075, 0.004))   # cuff hem
    return img


def leg_bare(spec, rng, h, w, th, t):
    """Smooth bare legs: soft calf / knee shaping."""
    sk = skin(h, w, rgb(spec["skin"]), rng, mottle=0.05, pores=0.02, redness=0.12)
    sk = shade(sk, 1 + 0.07 * np.exp(-((t - 0.28) / 0.1) ** 2) * np.cos(np.deg2rad(th + 180)))  # calf
    sk = shade(sk, 1 + 0.06 * np.exp(-((t - 0.5) / 0.035) ** 2) * smoothstep(70, 0, np.abs(th)))  # knee
    sk = shade(sk, 1 + 0.05 * np.exp(-((t - 0.78) / 0.12) ** 2) * smoothstep(80, 0, np.abs(th)))  # thigh front
    return sk


def maryjane(color="#0e0c10", strap="#141216"):
    """Flat black patent mary-janes: low cut (the instep shows), one strap across, glossy toe."""
    def fn(spec, rng, h, w, view):
        u, v = uv_grid(h, w)
        foot = skin(h, w, rgb(spec["skin"]), rng, mottle=0.05, pores=0.02)
        leather = fabric(h, w, rgb(color), rng, folds=0.08, grain=0.05)
        if view == "side":
            # shoe line: higher at the heel, low over the instep, the toe box closed
            line = np.interp(u, [0.0, 0.25, 0.36, 0.62, 0.72, 1.0], [0.66, 0.6, 0.42, 0.42, 1.2, 1.2])
            cover = (v < line) | (np.abs(u - 0.47) < 0.045)
            img = mix(foot, leather, cover.astype(np.float32))
            img = shade(img, 1 - 0.25 * (v < 0.14))
            img = mix(img, rgb("#4a4a52"), np.exp(-((v - 0.32) / 0.04) ** 2) * (u > 0.55) * 0.5)  # patent gloss
            img = shade(img, 1 - 0.35 * (np.abs(v - line) < 0.03) * (u < 0.66))
            img = mix(img, rgb("#a8a8b0"), (np.abs(u - 0.47) < 0.02) * (np.abs(v - 0.82) < 0.05) * 0.8)  # buckle
        else:
            cover = (v < 0.42) | ((v > 0.57) & (v < 0.66))
            img = mix(foot, leather, cover.astype(np.float32))
            img = mix(img, rgb("#4a4a52"), np.exp(-((u - 0.5) / 0.12) ** 2) * np.exp(-((v - 0.2) / 0.1) ** 2) * 0.45)
            img = shade(img, 1 - 0.3 * (np.abs(v - 0.42) < 0.025))
            # toes / instep tendons faintly under the skin
            img = shade(img, 1 - 0.08 * (np.sin(u * 30) > 0.6) * (v > 0.66))
        return grime(img, rng, amount=0.18, color=(0.3, 0.27, 0.22), scale=3)
    return fn


P6_COLORS = ["#d0306a", "#18b0a8", "#e8c02c", "#8a3ab0", "#f0f0e8", "#e86a2a", "#30c050"]


def pattern_90s(h, w, rng, ground="#2a3aa0", scale=1.0):
    """Loud early-90s shirt print: triangles, squiggles, zigzags, dots and confetti on a royal blue ground."""
    from PIL import Image, ImageDraw
    im = Image.new("RGB", (w, h), tuple(int(c * 255) for c in rgb(ground)))
    d = ImageDraw.Draw(im)
    n = int(h * w / (190.0 * S * S) / (scale * scale)) + 8
    col = lambda: tuple(int(c * 255) for c in rgb(P6_COLORS[rng.randint(len(P6_COLORS))]))
    for _ in range(n):
        kind = rng.randint(5)
        cx, cy = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(3.0, 6.5) * S * scale
        c = col()
        if kind == 0:   # triangle
            a = rng.uniform(0, 6.28)
            pts = [(cx + r * np.cos(a + k * 2.094), cy + r * np.sin(a + k * 2.094)) for k in range(3)]
            d.polygon(pts, fill=c)
        elif kind == 1:  # squiggle
            pts = [(cx + (k - 3) * r * 0.45, cy + np.sin(k * 1.6) * r * 0.45) for k in range(7)]
            d.line(pts, fill=c, width=int(S * 1.6 * scale))
        elif kind == 2:  # zigzag
            a = rng.uniform(-0.6, 0.6)
            pts = [(cx + (k - 2.5) * r * 0.5 * np.cos(a) - (k % 2) * r * 0.5 * np.sin(a),
                    cy + (k - 2.5) * r * 0.5 * np.sin(a) + (k % 2) * r * 0.5 * np.cos(a)) for k in range(6)]
            d.line(pts, fill=c, width=int(S * 1.4 * scale))
        elif kind == 3:  # dot / ring
            rr = r * 0.45
            if rng.rand() < 0.5:
                d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=c)
            else:
                d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=c, width=int(S * 1.3 * scale))
        else:           # confetti bar
            a = rng.uniform(0, 3.14)
            dx, dy = np.cos(a) * r * 0.5, np.sin(a) * r * 0.5
            d.line([(cx - dx, cy - dy), (cx + dx, cy + dy)], fill=c, width=int(S * 1.8 * scale))
    out = np.asarray(im, np.float32) / 255.0
    return blur(out, 0.7)


def torso_p6(spec, rng, h, w, th, t):
    ath = np.abs(th)
    shirt = pattern_90s(h, w, rng)
    shirt = shade(shirt, 1 + np.tanh(fold_layer(h, w, rng, 1.2) * 0.8) * 0.2)
    # blousing over the belt, collar band
    shirt = shade(shirt, 1 + 0.12 * np.sin(np.deg2rad(th) * 14) * band(t, 0.30, 0.38))
    shirt = shade(shirt, 1 - 0.3 * band(t, 0.955, 0.968, 0.003))
    sk = skin_layer(spec, rng, h, w)
    img = mix(shirt, sk, smoothstep(0.978, 0.988, t))
    jeans = denim(h, w, rgb("#262e4c"), rng, fade=0.28)
    jm = 1 - smoothstep(0.296, 0.306, t)
    img = mix(img, jeans, jm)
    belt = band(t, 0.262, 0.298)
    img = mix(img, rgb("#3a2618"), belt)
    img = mix(img, rgb("#a89878"), belt * (ath < 4.5) * 0.9)
    img = mix(img, rgb("#c09040"), (np.abs(th - 3) < 0.8) * band(t, 0.06, 0.25) * 0.5)  # fly stitch
    return grime(img, rng, amount=0.15, color=(0.2, 0.18, 0.16))


def arm_p6(spec, rng, h, w, th, t):
    sk = skin_layer(spec, rng, h, w)
    cl = pattern_90s(h, w, rng, scale=0.85)
    cl = shade(cl, 1 + np.tanh(fold_layer(h, w, rng, 1.3) * 0.8) * 0.22)
    cl = shade(cl, 1 + 0.12 * np.sin(t * 60 + np.deg2rad(th) * 2) * np.exp(-((t - 0.45) / 0.12) ** 2))
    img = mix(sk, cl, smoothstep(0.045, 0.055, t))
    img = mix(img, rgb("#22307a"), band(t, 0.05, 0.10) * 0.75)  # cuff
    return img


def leg_jeans(color, fade=0.3, stack=True):
    def fn(spec, rng, h, w, th, t):
        jeans = denim(h, w, rgb(color), rng, fade=fade)
        if stack:
            jeans = shade(jeans, 1 + 0.14 * np.sin(t * 70 + np.deg2rad(th) * 1.5) * (1 - smoothstep(0.03, 0.2, t)))
        jeans = shade(jeans, 1 - 0.2 * np.clip(1 - np.abs(np.abs(th) - 90) / 2.0, 0, 1))  # side seam
        jeans = shade(jeans, 1 - 0.15 * band(t, 0.0, 0.04))
        return grime(jeans, rng, amount=0.2, color=(0.16, 0.14, 0.12))
    return fn


def torso_p7(spec, rng, h, w, th, t):
    ath = np.abs(th)
    shirt = fabric(h, w, rgb("#e2e0da"), rng, folds=0.2, grain=0.05, fold_scale=1.1, sweat=0.35, stains=0.15,
                   stain_color=rgb("#c8c0b0"))
    # placket + buttons down the front
    shirt = shade(shirt, 1 - 0.18 * np.clip(1 - np.abs(ath - 4.2) / 0.9, 0, 1) * (t > 0.3))
    for bt in np.arange(0.38, 0.93, 0.1):
        shirt = mix(shirt, rgb("#f4f2ee"), np.exp(-((t - bt) / 0.008) ** 2) * np.exp(-(th / 1.7) ** 2))
        shirt = shade(shirt, 1 - 0.25 * np.exp(-((t - bt + 0.008) / 0.005) ** 2) * np.exp(-(th / 1.9) ** 2))
    # breast pocket on the left chest
    pk = band(t, 0.66, 0.75, 0.003) * ang_band(np.clip(-th, 0, None), 14, 34, 0.8)
    edge = np.clip(np.abs(ndimage.sobel(pk, 0)) + np.abs(ndimage.sobel(pk, 1)), 0, 1)
    shirt = shade(shirt, 1 - 0.22 * smoothstep(0.1, 0.4, edge))
    # collar: band round the neck + two points at the front
    collar_pts = (t > 0.90 - 0.0 * ath) * (t < 0.97) * np.clip(1 - np.abs(ath - (6 + (t - 0.9) * 260)) / 7.0, 0, 1)
    shirt = shade(shirt, 1 - 0.3 * band(t, 0.952, 0.962, 0.003))
    shirt = shade(shirt, 1 - 0.25 * np.clip(1 - np.abs(ath - (4 + (0.965 - t) * 300)) / 1.2, 0, 1) * band(t, 0.89, 0.965))
    shirt = shade(shirt, 1 + 0.06 * collar_pts)
    sk = skin_layer(spec, rng, h, w)
    vneck = 0.975 - 0.05 * np.exp(-(th / 9) ** 2)  # open top button
    img = mix(shirt, sk, smoothstep(vneck - 0.003, vneck + 0.004, t))
    # blousing over the belt
    img = shade(img, 1 + 0.12 * np.sin(np.deg2rad(th) * 16 + 0.5) * band(t, 0.30, 0.37))
    slacks = fabric(h, w, rgb("#24262c"), rng, folds=0.22, grain=0.06, fold_scale=1.1)
    sm = 1 - smoothstep(0.296, 0.306, t)
    img = mix(img, slacks, sm)
    belt = band(t, 0.262, 0.298)
    img = mix(img, rgb("#121012"), belt)
    img = mix(img, rgb("#a8a8a4"), belt * (ath < 5) * 0.9)
    img = shade(img, 1 - 0.3 * (np.abs(th - 2.5) < 0.6) * band(t, 0.08, 0.26))  # fly
    return grime(img, rng, amount=0.12, color=(0.3, 0.27, 0.22))


def arm_p7(spec, rng, h, w, th, t):
    sk = skin_layer(spec, rng, h, w)
    cl = fabric(h, w, rgb("#e2e0da"), rng, folds=0.26, grain=0.05, fold_scale=1.3)
    cl = shade(cl, 1 + 0.12 * np.sin(t * 60 + np.deg2rad(th) * 2) * np.exp(-((t - 0.45) / 0.12) ** 2))
    img = mix(sk, cl, smoothstep(0.055, 0.065, t))
    cuff = band(t, 0.06, 0.13)
    img = mix(img, rgb("#ecebe6"), cuff * 0.6)
    img = shade(img, 1 - 0.2 * band(t, 0.128, 0.138, 0.003))
    img = mix(img, rgb("#f6f4f0"), np.exp(-((t - 0.095) / 0.012) ** 2) * np.exp(-((np.abs(th) - 150) / 6) ** 2))  # button
    return img


def leg_slacks(spec, rng, h, w, th, t):
    sl = fabric(h, w, rgb("#24262c"), rng, folds=0.24, grain=0.06, fold_scale=1.3)
    sl = shade(sl, 1 + 0.18 * np.clip(1 - np.abs(th) / 2.0, 0, 1) * (t < 0.9))      # front crease
    sl = shade(sl, 1 + 0.12 * np.clip(1 - np.abs(np.abs(th) - 180) / 2.0, 0, 1) * (t < 0.45))
    sl = shade(sl, 1 + 0.12 * np.sin(t * 60 + np.deg2rad(th) * 1.5) * (1 - smoothstep(0.02, 0.14, t)))  # break
    sl = shade(sl, 1 - 0.18 * np.clip(1 - np.abs(np.abs(th) - 90) / 2.0, 0, 1))
    return grime(sl, rng, amount=0.15, color=(0.2, 0.18, 0.16))


def oxford(color="#141214", sole="#0a0808"):
    """Black leather dress shoes: laced, a welt line, a shine on the toe."""
    def fn(spec, rng, h, w, view):
        u, v = uv_grid(h, w)
        img = fabric(h, w, rgb(color), rng, folds=0.1, grain=0.06)
        if view == "side":
            img = mix(img, rgb(sole), (v < 0.16).astype(np.float32))
            img = shade(img, 1 + 0.4 * (np.abs(v - 0.19) < 0.02))        # welt
            img = mix(img, rgb("#5a5a62"), np.exp(-((v - 0.42) / 0.07) ** 2) * smoothstep(0.6, 0.9, u) * 0.5)
            img = shade(img, 1 - 0.3 * (np.abs(u - 0.55) < 0.015) * (v > 0.2))  # vamp seam
        else:
            lace = (np.abs(u - 0.5) < 0.16) * (v > 0.45) * (v < 0.85)
            img = shade(img, 1 - 0.2 * lace)
            for lv in np.arange(0.5, 0.84, 0.08):
                img = mix(img, rgb("#2a2420"), (np.abs(v - lv) < 0.018) * (np.abs(u - 0.5) < 0.14))
            img = mix(img, rgb("#5a5a62"), np.exp(-((u - 0.5) / 0.14) ** 2) * np.exp(-((v - 0.16) / 0.09) ** 2) * 0.5)
        return grime(img, rng, amount=0.15, color=(0.25, 0.22, 0.18), scale=3)
    return fn


CHARACTERS = {
    "prisoner1": dict(
        seed=101, skin="#c99472", redness=0.3, eyes="#4a6070", brows="#7a6a3a", lips="#a86a5a", lip_alpha=0.5,
        hair_style="buzz", hair="#b4b844", hair_hi="#dcdc8c", hair_root="#5a6028", stubble=0.25, mustache=0.55,
        stubble_color="#5a5226", torso=torso_p1, arm=arm_skin, leg=leg_p1, shoe=sneaker("#d8d8d4", "#b01e24"),
        extra="hair", misc="hair"),
    "prisoner2": dict(
        seed=202, female=True, feminine=True, skin="#b8876a", redness=0.12, eyes="#3a2416", brows="#2a1a12",
        lips="#b0141e", lip_alpha=0.95, blush=0.22, liner=1.6, shadow="#6a3a48", brow_thick=2.3, brow_arch=1.25,
        hair_style="long", hair="#6a4a32", hair_hi="#a87e54", torso=torso_p2, arm=sleeve("#9e1a22", 0.05),
        leg=leg_p2, shoe=sneaker("#d8d4cc", "#c0c0c0", dirty=0.4), nails="#a01820", extra="hair", wavy=1.0,
        misc="hair"),
    "prisoner3": dict(
        seed=303, female=True, feminine=True, skin="#dcb8a0", redness=0.22, eyes="#3a6a3e", brows="#8a3a1e",
        lips="#bc666a", lip_alpha=0.75, blush=0.16, freckles=True, liner=0.8, shadow="#7a5048", brow_thick=2.0,
        brow_arch=1.1, hair_style="bangs", hair="#9a2c1a", hair_hi="#c8502a", torso=torso_p3, arm=arm_p3,
        leg=leg_p3, shoe=boot("#3a2a20", "#1a1210"), nails="#d0a090", glasses="rect", glasses_color="#181418",
        extra="hair", straight_cut=True, misc="hair"),
    "prisoner4": dict(
        seed=404, skin="#d6ae94", redness=0.25, eyes="#4a3a2a", brows="#2a1e16", lips="#a8706a", lip_alpha=0.5,
        hair_style="short", part=True, hair="#3a2a1e", hair_hi="#5a4430", torso=torso_p4,
        arm=sleeve("#1c1a1e", 0.045, folds=0.32), leg=leg_p4, shoe=sneaker("#2a2a30", "#e0e0e0", sole="#e0e0d8"),
        glasses="round", glasses_color="#0e0c0e", extra="hair", misc="hair", stubble=0.08, stubble_color="#4a3a30"),
    # (iteration 2) SourceAssets/players/more/EyAqyzjWQAUE23f.webp
    "prisoner5": dict(
        seed=1505, female=True, feminine=True, skin="#dcb89e", redness=0.14, eyes="#4a3624", brows="#5a2618",
        lips="#a8404e", lip_alpha=0.85, blush=0.18, liner=1.2, shadow="#5a4048", brow_thick=2.2, brow_arch=1.2,
        hair_style="bob", fringe_sweep=1.0, hair="#7a2c1a", hair_hi="#a84a2a", hair_contrast=0.36,
        torso=torso_p5, arm=arm_p5, leg=leg_bare, shoe=maryjane(), nails="#c88a8a", extra="hair",
        straight_cut=True, misc="hair"),
    "prisoner6": dict(
        seed=1606, skin="#e2b498", redness=0.32, blush=0.14, eyes="#4a3424", brows="#4a2c1a", brow_thick=2.8,
        brow_arch=0.8, lips="#b47468", lip_alpha=0.45, hair_style="kid", hair="#5a3220", hair_hi="#7e4c2c",
        torso=torso_p6, arm=arm_p6, leg=leg_jeans("#262e4c", 0.28), shoe=sneaker("#6a4a32", "#c8b490", sole="#cfc4b0", dirty=0.45),
        nails="#d0a48c", extra="hair", misc="hair"),
    "prisoner7": dict(
        seed=1707, skin="#c79878", redness=0.3, eyes="#3a2a20", brows="#1a1210", brow_thick=3.6, lips="#9c6a5e",
        lip_alpha=0.45, hair_style="short", part=True, part_line=18.0, hair="#1a1410", hair_hi="#3a2e26",
        stubble=0.3, stubble_color="#2a221c", torso=torso_p7, arm=arm_p7, leg=leg_slacks, shoe=oxford(),
        nails="#c09078", extra="hair", misc="hair"),
    "omar": dict(
        seed=505, head="mask", skin="#4a403c", hand_skin="#3a3432", nails="#2a2422", hand_blood=0.55, hand_grime=0.6,
        hair_style="none", torso=torso_omar, arm=arm_omar, leg=leg_omar, shoe=boot(), extra="apron", misc="skirt",
        hair="#3a3428"),
    "mannequin_burnt": dict(
        seed=606, head="featureless", surface=burnt_surface, skin="#3a2c22", hair_style="none",
        torso=mannequin_torso(burnt_surface, seams=False), arm=mannequin_limb(burnt_surface, []),
        leg=mannequin_limb(burnt_surface, []), extra="surface", misc="none",
        shoe=lambda spec, rng, h, w, view: burnt_surface(h, w, rng)),
    "mannequin": dict(
        seed=707, head="featureless", surface=pale_plastic, skin="#cfc4b4", hair_style="none",
        torso=mannequin_torso(pale_plastic), arm=mannequin_limb(pale_plastic, [0.06, 0.5, 0.94]),
        leg=mannequin_limb(pale_plastic, [0.08, 0.5, 0.95]), extra="surface", misc="none",
        shoe=lambda spec, rng, h, w, view: pale_plastic(h, w, rng)),
    "grandma": dict(
        seed=909, female=True, skin="#d4ccc8", redness=0.0, mottle=0.2, eyes="#1e1a14", sclera="#d0c8b4",
        brows="#b8b2a4", brow_thick=1.8, lips="#8a6060", lip_alpha=0.5, shadow="#4a3434", liner=0.6,
        hair_style="bob", fringe_sweep=1.0, hair="#c4c2bc", hair_hi="#f0eeea", hair_contrast=0.32, straight_cut=True,
        torso=torso_granny, arm=arm_granny, leg=leg_granny, shoe=sneaker("#e2ded4", "#c8c4b8", sole="#d8d4c8", dirty=0.5),
        nails="#b8a888", hand_grime=0.25, extra="hair", misc="hair"),
    "grandma_dead": dict(
        seed=909, female=True, dead=True, skin="#c4b0a2", redness=0.0, mottle=0.3, eyes="#1e1a14", sclera="#c0b8a4",
        brows="#b8b2a4", brow_thick=1.8, lips="#5a3a34", lip_alpha=0.7, dead_eyes=True, open_mouth=True,
        head_blood=0.35, shadow="#3a2a1e", hair_style="bob", fringe_sweep=1.0, hair="#bcb8b0", hair_hi="#e2ddd6", hair_contrast=0.3,
        straight_cut=True, torso=torso_granny, arm=arm_granny, leg=leg_granny,
        shoe=sneaker("#d8d0c4", "#c8c4b8", sole="#d8d4c8", dirty=0.6), nails="#b8a888", hand_blood=0.5, hand_grime=0.3,
        extra="hair", misc="hair"),
    "corpse": dict(
        seed=808, skin="#8a8a70", redness=0.0, mottle=0.2, eyes="#2a2a24", brows="#3a3028", lips="#4a3434",
        lip_alpha=0.8, dead_eyes=True, open_mouth=True, hair_style="short", hair="#2a241e", hair_hi="#3a3228",
        head_blood=0.25, head_grime=0.4, torso=corpse_torso, arm=corpse_arm, leg=corpse_leg, shoe=barefoot,
        hand_blood=0.3, hand_grime=0.6, extra="rags", misc="hair", nails="#4a4038"),
}


def paste(atlas_rgb, atlas_a, rect, img, alpha=None):
    x, y, w, h = rect
    atlas_rgb[y * S:(y + h) * S, x * S:(x + w) * S] = img
    if alpha is not None:
        atlas_a[y * S:(y + h) * S, x * S:(x + w) * S] = alpha


def build(name):
    spec = CHARACTERS[name]
    rng = np.random.RandomState(spec["seed"])
    N = A.SIZE * S
    rgb_ = np.zeros((N, N, 3), np.float32)
    al = np.ones((N, N), np.float32)
    if "surface" in spec and "hair" not in spec:
        pass
    paste(rgb_, al, A.HEAD, paint_head(spec, rng))
    paste(rgb_, al, A.TORSO, paint_torso(spec, rng))
    paste(rgb_, al, A.ARM, paint_arm(spec, rng))
    paste(rgb_, al, A.LEG, paint_leg(spec, rng))
    paste(rgb_, al, A.HAND, paint_hand(spec, rng))
    paste(rgb_, al, A.FOOT, paint_foot(spec, rng))
    paste(rgb_, al, A.ROPE, paint_rope(spec, rng))
    paste(rgb_, al, A.SWATCH, paint_swatch(spec, rng))
    img, a = paint_hair_region(spec, rng)
    paste(rgb_, al, A.HAIR, img, a)
    img, a = paint_extra(spec, rng)
    paste(rgb_, al, A.EXTRA, img, a)
    img, a = paint_misc(spec, rng)
    paste(rgb_, al, A.MISC, img, a)
    small = downsample(rgb_, S)
    small_a = downsample(al, S)
    out = degrade(small, rng, bits=5, jpeg_q=60, desat=0.1)
    # re-impose exact flat swatches after JPEG so the glasses / frames stay clean
    return out, small_a
