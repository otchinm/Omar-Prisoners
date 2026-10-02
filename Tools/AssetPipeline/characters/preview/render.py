#!/usr/bin/env python3
"""Software rasterizer for the preview harness dumps (JSON from Harness/Dump.cs).

    render.py scene.json out.png [--views front,side,back,q] [--px 160] [--persp] [--fov 60]
              [--cam x,y,z --target x,y,z] [--size WxH] [--grid] [--bg r,g,b]

Orthographic views (default) are framed on the scene bounds; --persp renders a camera at --cam looking at --target
(or, with --camspace, the dump is already in camera space: camera at the origin looking down +Z, like a Unity camera).
Unity conventions: left-handed, front faces clockwise on screen, nearest texture sampling, Gouraud lambert lighting.
"""
import argparse
import json
import os

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", "Assets", "PrisonersOfOmar", "Resources"))
_tex = {}


def texture(name):
    if name in _tex:
        return _tex[name]
    path = os.path.join(RES, name + ".png")
    if name == "white" or not os.path.exists(path):
        arr = np.ones((4, 4, 4), np.float32)
        if name != "white":
            arr[..., 0] = 1.0; arr[..., 1] = 0.0; arr[..., 2] = 1.0  # missing -> magenta
    else:
        arr = np.asarray(Image.open(path).convert("RGBA"), np.float32) / 255.0
    _tex[name] = arr
    return arr


def load(path):
    with open(path) as f:
        d = json.load(f)
    meshes = []
    for m in d["meshes"]:
        v = np.array(m["v"], np.float64).reshape(-1, 3)
        n = np.array(m["n"], np.float64).reshape(-1, 3)
        uv = np.array(m["uv"], np.float64).reshape(-1, 2)
        subs = []
        for s in m["subs"]:
            t = np.array(s["t"], np.int64).reshape(-1, 3)
            subs.append((s["tex"], s["surface"], np.array(s["tint"], np.float32), t))
        meshes.append((m["name"], v, n, uv, subs))
    return meshes, d.get("markers", [])


def view_basis(view):
    # returns (right, up, forward) of the camera in world space
    if view == "front":   # camera in front of the character (+Z) looking -Z
        return np.array([-1, 0, 0.]), np.array([0, 1, 0.]), np.array([0, 0, -1.])
    if view == "back":
        return np.array([1, 0, 0.]), np.array([0, 1, 0.]), np.array([0, 0, 1.])
    if view == "side":    # camera on the character's right (+X) looking -X
        return np.array([0, 0, 1.]), np.array([0, 1, 0.]), np.array([-1, 0, 0.])
    if view == "left":
        return np.array([0, 0, -1.]), np.array([0, 1, 0.]), np.array([1, 0, 0.])
    if view == "top":
        return np.array([1, 0, 0.]), np.array([0, 0, 1.]), np.array([0, -1, 0.])
    if view == "q":       # three quarter front-right, slightly above
        f = np.array([-0.6, -0.25, -0.75]); f /= np.linalg.norm(f)
        r = np.cross([0, 1, 0], f); r /= np.linalg.norm(r)
        u = np.cross(f, r)
        return r, u, f
    if view == "q2":      # three quarter back-left
        f = np.array([0.6, -0.2, 0.75]); f /= np.linalg.norm(f)
        r = np.cross([0, 1, 0], f); r /= np.linalg.norm(r)
        u = np.cross(f, r)
        return r, u, f
    raise ValueError(view)


def look_basis(cam, target):
    f = np.asarray(target, float) - np.asarray(cam, float)
    f /= np.linalg.norm(f)
    r = np.cross([0, 1, 0], f)
    if np.linalg.norm(r) < 1e-6:
        r = np.array([1, 0, 0.])
    r /= np.linalg.norm(r)
    u = np.cross(f, r)
    return r, u, f


def render(meshes, W, H, project, light_dir, bg=(40, 36, 44), ambient=0.38):
    color = np.zeros((H, W, 3), np.float32)
    color[:] = np.array(bg, np.float32) / 255.0
    zbuf = np.full((H, W), np.inf, np.float32)
    L = np.asarray(light_dir, float); L /= np.linalg.norm(L)
    for name, v, n, uv, subs in meshes:
        sx, sy, sz = project(v)
        shade = ambient + (1 - ambient) * np.clip(n @ L, 0, 1) + 0.12 * np.clip(n @ np.array([0.3, 0.5, 0.8]), 0, 1)
        for texname, surf, tint, tris in subs:
            tex = texture(texname)
            th, tw = tex.shape[:2]
            cutout = "Cutout" in surf
            double = cutout or "DoubleSided" in surf or "Additive" in surf
            for (a, b, c) in tris:
                if not (np.isfinite(sz[a]) and np.isfinite(sz[b]) and np.isfinite(sz[c])):
                    continue
                if sz[a] <= 0 or sz[b] <= 0 or sz[c] <= 0:
                    continue
                x0, y0, x1, y1, x2, y2 = sx[a], sy[a], sx[b], sy[b], sx[c], sy[c]
                # screen y grows downward here; front face = clockwise with y up = counter-clockwise with y down
                area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
                if abs(area) < 1e-9:
                    continue
                if not double and area < 0:
                    continue
                minx = max(int(np.floor(min(x0, x1, x2))), 0); maxx = min(int(np.ceil(max(x0, x1, x2))), W - 1)
                miny = max(int(np.floor(min(y0, y1, y2))), 0); maxy = min(int(np.ceil(max(y0, y1, y2))), H - 1)
                if minx > maxx or miny > maxy:
                    continue
                ys, xs = np.mgrid[miny:maxy + 1, minx:maxx + 1]
                px = xs + 0.5; py = ys + 0.5
                w0 = ((x1 - px) * (y2 - py) - (x2 - px) * (y1 - py)) / area
                w1 = ((x2 - px) * (y0 - py) - (x0 - px) * (y2 - py)) / area
                w2 = 1 - w0 - w1
                inside = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
                if not inside.any():
                    continue
                z = w0 * sz[a] + w1 * sz[b] + w2 * sz[c]
                zb = zbuf[miny:maxy + 1, minx:maxx + 1]
                m = inside & (z < zb)
                if not m.any():
                    continue
                u = w0 * uv[a, 0] + w1 * uv[b, 0] + w2 * uv[c, 0]
                vv = w0 * uv[a, 1] + w1 * uv[b, 1] + w2 * uv[c, 1]
                tx = np.clip((u * tw).astype(int), 0, tw - 1)
                ty = np.clip(((1 - vv) * th).astype(int), 0, th - 1)
                texel = tex[ty, tx]
                if cutout:
                    m = m & (texel[..., 3] >= 0.5)
                    if not m.any():
                        continue
                lit = w0 * shade[a] + w1 * shade[b] + w2 * shade[c]
                if double and area < 0:
                    lit = ambient + (lit - ambient) * 0.5
                rgb = texel[..., :3] * tint[:3] * np.clip(lit, 0, 1.4)[..., None]
                cb = color[miny:maxy + 1, minx:maxx + 1]
                cb[m] = rgb[m]
                zb[m] = z[m]
    return color


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("scene")
    ap.add_argument("out")
    ap.add_argument("--views", default="front,side,back,q")
    ap.add_argument("--px", type=float, default=170.0, help="pixels per meter (ortho)")
    ap.add_argument("--size", default=None, help="WxH")
    ap.add_argument("--persp", action="store_true")
    ap.add_argument("--camspace", action="store_true")
    ap.add_argument("--fov", type=float, default=60.0)
    ap.add_argument("--cam", default="0,1.5,4")
    ap.add_argument("--target", default="0,1,0")
    ap.add_argument("--grid", action="store_true")
    ap.add_argument("--markers", action="store_true")
    ap.add_argument("--bg", default="40,36,44")
    ap.add_argument("--scale", type=int, default=1, help="nearest upscale of the final image")
    args = ap.parse_args()
    meshes, markers = load(args.scene)
    bg = tuple(int(x) for x in args.bg.split(","))
    allv = np.concatenate([m[1] for m in meshes]) if meshes else np.zeros((1, 3))
    images = []
    if args.persp or args.camspace:
        W, H = (int(x) for x in (args.size or "426x240").split("x"))
        if args.camspace:
            r, u, f = np.array([1, 0, 0.]), np.array([0, 1, 0.]), np.array([0, 0, 1.])
            cam = np.zeros(3)
        else:
            cam = np.array([float(x) for x in args.cam.split(",")])
            r, u, f = look_basis(cam, [float(x) for x in args.target.split(",")])
        fy = (H / 2) / np.tan(np.radians(args.fov) / 2)

        def project(v):
            d = v - cam
            x, y, z = d @ r, d @ u, d @ f
            zz = np.where(z > 1e-4, z, np.nan)
            return W / 2 + x / zz * fy, H / 2 - y / zz * fy, z

        img = render(meshes, W, H, project, light_dir=-f + u * 0.6 + r * 0.3, bg=bg)
        images.append(img)
    else:
        for view in args.views.split(","):
            r, u, f = view_basis(view)
            xs, ys = allv @ r, allv @ u
            pad = 0.15
            x0, x1, y0, y1 = xs.min() - pad, xs.max() + pad, ys.min() - pad, ys.max() + pad
            W = int((x1 - x0) * args.px); H = int((y1 - y0) * args.px)

            def project(v, r=r, u=u, f=f, x0=x0, y1=y1):
                return (v @ r - x0) * args.px, (y1 - v @ u) * args.px, v @ f + 100.0

            img = render(meshes, W, H, project, light_dir=-f + u * 0.7 + r * 0.4, bg=bg)
            if args.grid:
                for gy in np.arange(np.floor(y0 * 10) / 10, y1, 0.1):
                    row = int((y1 - gy) * args.px)
                    if 0 <= row < H:
                        k = 0.5 if abs(round(gy * 10) % 5) == 0 else 0.25
                        img[row, :, :] = img[row, :, :] * (1 - k) + np.array([0.3, 0.8, 0.3]) * k
            if args.markers:
                for mk in markers:
                    p = np.array(mk["p"])
                    cx, cy = (p @ r - x0) * args.px, (y1 - p @ u) * args.px
                    for axis, col in (("x", (1, 0, 0)), ("y", (0, 1, 0)), ("z", (0, 0.5, 1))):
                        d = np.array(mk[axis]) * 0.08
                        for t in np.linspace(0, 1, 12):
                            qx, qy = int(cx + (d @ r) * args.px * t), int(cy - (d @ u) * args.px * t)
                            if 0 <= qx < W and 0 <= qy < H:
                                img[qy, qx] = col
            images.append(img)
    Hm = max(i.shape[0] for i in images)
    canvas = np.zeros((Hm, sum(i.shape[1] for i in images) + 4 * (len(images) - 1), 3), np.float32)
    x = 0
    for i in images:
        canvas[:i.shape[0], x:x + i.shape[1]] = i
        x += i.shape[1] + 4
    out = Image.fromarray((np.clip(canvas, 0, 1) * 255).astype(np.uint8))
    if args.scale > 1:
        out = out.resize((out.width * args.scale, out.height * args.scale), Image.NEAREST)
    out.save(args.out)
    print("wrote", args.out, out.size)


if __name__ == "__main__":
    main()
