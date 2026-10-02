#!/usr/bin/env python3
"""
Top-down floor-plan renderer for "The Base of the Second Class" (QA, no Unity needed).

The data comes from the REAL map builder: Tools/AssetPipeline/map/sim runs Scripts/Map/MapBuilder.Build against a
managed UnityEngine stand-in and exports map.json (colliders, doors, spawns, traps, hiding spots, nav graph...).

    cd Tools/AssetPipeline/map/sim && dotnet run -c Release -- OUT_DIR [seed]        # writes OUT_DIR/map.json + views/
    python3 Tools/AssetPipeline/map/plan.py OUT_DIR/map.json OUT_DIR/plans            # writes the plan PNGs

Each plan slices the colliders at walking height of one level (basement / ground / upper / exterior) and overlays:
doors (closed leaf + swing arc), item spawns (red = Key tier, yellow = Common), tripwires (magenta lines),
bear traps (magenta X), hiding spots (blue, cyan arrow = exit pose), cages, spawns (green = prisoners, red = Omar),
notes (white), lights (small rings), nav nodes/edges (blue; orange = through a door, red = through a gate).
Also prints sanity checks (items inside solid geometry, tripwire spans, door clearance, unreachable areas).
Requires Pillow only.
"""
import json
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

LAYER_WORLD, LAYER_INTERACT, LAYER_CORPSE, LAYER_DOOR, LAYER_FOLIAGE, LAYER_DEFAULT = 8, 11, 17, 18, 16, 0

# name, x0, z0, x1, z1, px/m, slice y0, y1, point filter y0, y1
VIEWS = [
    ("basement", -46, -10, 13, 11, 22, -2.3, -1.0, -2.9, -0.5),
    ("ground", -16, -14, 16, 13, 32, 0.9, 2.0, 0.3, 3.4),
    ("upper", -13, -10, 13, 10, 36, 4.1, 5.2, 3.6, 6.5),
    ("exterior", -115, -90, 100, 130, 4.2, 0.25, 1.6, -0.6, 3.0),
    ("farm", -50, 15, 50, 90, 9, 0.25, 1.6, -0.6, 3.0),
    ("lot", -78, -66, -32, 12, 11, 0.25, 1.6, -0.6, 3.0),
    ("yard", -30, -60, 32, 15, 11, 0.25, 1.6, -0.6, 3.0),
]


def qrot(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    x2, y2, z2 = x * 2, y * 2, z * 2
    xx, yy, zz, xy, xz, yz, wx, wy, wz = x * x2, y * y2, z * z2, x * y2, x * z2, y * z2, w * x2, w * y2, w * z2
    return ((1 - (yy + zz)) * vx + (xy - wz) * vy + (xz + wy) * vz,
            (xy + wz) * vx + (1 - (xx + zz)) * vy + (yz - wx) * vz,
            (xz - wy) * vx + (yz + wx) * vy + (1 - (xx + yy)) * vz)


def box_corners(c):
    cx, cy, cz = c["c"]
    hx, hy, hz = c["h"]
    pts = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                o = qrot(c["q"], (sx * hx, sy * hy, sz * hz))
                pts.append((cx + o[0], cy + o[1], cz + o[2]))
    return pts


def hull2d(points):
    pts = sorted(set(points))
    if len(pts) <= 2:
        return pts

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def yaw_rotate(v, deg):
    """Unity rotation around +Y by deg (positive = clockwise seen from above)."""
    t = math.radians(deg)
    x, z = v[0], v[2]
    return (x * math.cos(t) + z * math.sin(t), v[1], -x * math.sin(t) + z * math.cos(t))


def point_in_box(c, p, margin=0.0):
    cx, cy, cz = c["c"]
    q = c["q"]
    inv = (-q[0], -q[1], -q[2], q[3])
    l = qrot(inv, (p[0] - cx, p[1] - cy, p[2] - cz))
    return all(abs(l[i]) <= c["h"][i] - margin for i in range(3))


def render(d, view, out_dir):
    name, x0, z0, x1, z1, s, sy0, sy1, py0, py1 = view
    W, H = int((x1 - x0) * s), int((z1 - z0) * s)
    img = Image.new("RGB", (W, H), (24, 26, 30))
    dr = ImageDraw.Draw(img, "RGBA")
    try:
        font = ImageFont.load_default()
    except Exception:
        font = None

    def P(x, z):
        return ((x - x0) * s, (z1 - z) * s)

    def inside_y(y):
        return py0 <= y <= py1

    # grid (5 m)
    for gx in range(int(math.floor(x0 / 5)) * 5, int(x1) + 1, 5):
        dr.line([P(gx, z0), P(gx, z1)], fill=(40, 43, 50), width=1)
    for gz in range(int(math.floor(z0 / 5)) * 5, int(z1) + 1, 5):
        dr.line([P(x0, gz), P(x1, gz)], fill=(40, 43, 50), width=1)
    # areas
    for a in d["areas"]:
        mn, mx = a["b"]["min"], a["b"]["max"]
        if mx[1] < py0 or mn[1] > py1 + 3:
            continue
        if a["name"] in ("Yard", "House", "Basement"):
            continue
        dr.rectangle([P(mn[0], mx[2]), P(mx[0], mn[2])], outline=(70, 90, 70, 160), width=1)
        if font:
            tx, ty = P(mn[0], mx[2])
            dr.text((tx + 3, ty + 2), a["name"], fill=(120, 150, 120), font=font)
    # colliders sliced at walking height
    for c in d["colliders"]:
        corners = box_corners(c)
        ys = [p[1] for p in corners]
        if max(ys) < sy0 or min(ys) > sy1:
            continue
        poly = hull2d([(round(p[0], 3), round(p[2], 3)) for p in corners])
        if len(poly) < 3:
            continue
        pts = [P(x, z) for x, z in poly]
        if c["t"]:
            col, fill = (80, 220, 120, 255), None
        elif c["l"] == LAYER_DOOR:
            col, fill = (230, 150, 50, 255), (230, 150, 50, 140)
        elif c["l"] == LAYER_CORPSE:
            col, fill = (190, 90, 220, 255), (190, 90, 220, 150)
        else:
            low = max(ys) < sy0 + 0.9
            fill = (120, 120, 125, 200) if not low else (90, 90, 95, 160)
            col = (175, 175, 180, 255)
        dr.polygon(pts, fill=fill, outline=col)
    # nav
    nodes = d["nav"]["nodes"]
    for a, b, door in d["nav"]["edges"]:
        pa, pb = nodes[a], nodes[b]
        if not (inside_y(pa[1]) or inside_y(pb[1])):
            continue
        col = (70, 110, 200, 120) if door == -1 else ((240, 140, 40, 230) if door >= 0 else (240, 50, 50, 230))
        dr.line([P(pa[0], pa[2]), P(pb[0], pb[2])], fill=col, width=1 if door == -1 else 2)
    for p in nodes:
        if inside_y(p[1]):
            x, y = P(p[0], p[2])
            dr.ellipse([x - 2, y - 2, x + 2, y + 2], fill=(90, 140, 255, 220))
    # doors: closed leaf + arc to the open position
    for dd in d["doors"]:
        pv = dd["pivot"]
        if not inside_y(pv[1] + 0.3):
            continue
        L = dd["leafW"]
        ld = dd["leafDir"]
        closed = (pv[0] + ld[0] * L, pv[2] + ld[2] * L)
        col = (255, 90, 90) if dd["locked"] else (255, 200, 80)
        dr.line([P(pv[0], pv[2]), P(*closed)], fill=col, width=3)
        prev = closed
        for k in range(1, 13):
            r = yaw_rotate(ld, dd["open"] * k / 12)
            cur = (pv[0] + r[0] * L, pv[2] + r[2] * L)
            dr.line([P(*prev), P(*cur)], fill=col + (150,), width=1)
            prev = cur
        if font:
            tx, ty = P(dd["center"][0], dd["center"][2])
            dr.text((tx + 4, ty + 4), dd["name"], fill=(255, 220, 140), font=font)
    # lights
    for l in d["lights"]:
        p = l["p"]
        if not inside_y(p[1] - 1.0) and not (name in ("exterior", "farm", "lot", "yard") and p[1] < 12):
            continue
        x, y = P(p[0], p[2])
        col = {"power": (255, 230, 120), "radio": (120, 255, 255), "none": (255, 140, 60)}[l["group"]]
        rr = 4
        dr.ellipse([x - rr, y - rr, x + rr, y + rr], outline=col, width=2)
        rpx = l["r"] * s
        if rpx < 400:
            dr.ellipse([x - rpx, y - rpx, x + rpx, y + rpx], outline=col + (40,), width=1)
    # hiding spots
    for h in d["hiding"]:
        p = h["root"]
        if not inside_y(p[1] + 0.3):
            continue
        x, y = P(p[0], p[2])
        dr.rectangle([x - 6, y - 6, x + 6, y + 6], outline=(80, 160, 255), width=2)
        e = h["exit"]
        ex, ey = P(e["pos"][0], e["pos"][2])
        dr.line([(ex, ey), P(e["pos"][0] + e["fwd"][0] * 0.6, e["pos"][2] + e["fwd"][2] * 0.6)], fill=(80, 255, 255), width=2)
        dr.ellipse([ex - 3, ey - 3, ex + 3, ey + 3], fill=(80, 255, 255))
        v = h["view"]
        vx, vy = P(v["pos"][0], v["pos"][2])
        dr.line([(vx, vy), P(v["pos"][0] + v["fwd"][0] * 0.8, v["pos"][2] + v["fwd"][2] * 0.8)], fill=(80, 160, 255), width=1)
    # cages + spawns
    for c in d["cages"]:
        i = c["inside"]["pos"]
        o = c["outside"]["pos"]
        if inside_y(i[1] + 0.3):
            dr.ellipse([*[v - 5 for v in P(i[0], i[2])], *[v + 5 for v in P(i[0], i[2])]], fill=(80, 230, 80))
            ox, oy = P(o[0], o[2])
            dr.ellipse([ox - 3, oy - 3, ox + 3, oy + 3], outline=(80, 230, 80))
    om = d["omar"]["pos"]
    if inside_y(om[1] + 0.3):
        x, y = P(om[0], om[2])
        dr.ellipse([x - 7, y - 7, x + 7, y + 7], fill=(255, 40, 40))
        f = d["omar"]["fwd"]
        dr.line([(x, y), P(om[0] + f[0], om[2] + f[2])], fill=(255, 40, 40), width=2)
    # items
    for it in d["items"]:
        p = it["p"]
        if not inside_y(p[1]):
            continue
        x, y = P(p[0], p[2])
        if it["tier"] == "Key":
            dr.polygon([(x, y - 5), (x + 5, y), (x, y + 5), (x - 5, y)], fill=(255, 60, 60), outline=(0, 0, 0))
        else:
            dr.ellipse([x - 4, y - 4, x + 4, y + 4], fill=(255, 230, 60), outline=(0, 0, 0))
    # traps
    for t in d["traps"]:
        a = t["a"]
        if not inside_y(a[1]):
            continue
        if t["kind"] == "Tripwire":
            b = t["b"]
            dr.line([P(a[0], a[2]), P(b[0], b[2])], fill=(255, 60, 255), width=3)
        else:
            x, y = P(a[0], a[2])
            dr.line([(x - 5, y - 5), (x + 5, y + 5)], fill=(255, 60, 255), width=2)
            dr.line([(x - 5, y + 5), (x + 5, y - 5)], fill=(255, 60, 255), width=2)
    # notes + markers
    for n in d["notes"]:
        p = n["p"]
        if inside_y(p[1] - 1.0) or inside_y(p[1]):
            x, y = P(p[0], p[2])
            dr.rectangle([x - 3, y - 3, x + 3, y + 3], fill=(240, 240, 240))
    for k, m in d["markers"].items():
        p = m["pos"]
        if inside_y(p[1] - 1.2) or inside_y(p[1]):
            x, y = P(p[0], p[2])
            dr.line([(x - 4, y), (x + 4, y)], fill=(200, 200, 255))
            dr.line([(x, y - 4), (x, y + 4)], fill=(200, 200, 255))
            if font:
                dr.text((x + 5, y - 10), k, fill=(200, 200, 255), font=font)
    # special zones
    def zone(b, col, label):
        mn, mx = b["min"], b["max"]
        dr.rectangle([P(mn[0], mx[2]), P(mx[0], mn[2])], outline=col, width=2)
        if font:
            tx, ty = P(mn[0], mn[2])
            dr.text((tx + 2, ty - 12), label, fill=col, font=font)
    if name in ("exterior", "farm", "yard", "lot"):
        zone(d["gate"]["exit"], (255, 80, 80), "ROAD EXIT")
        zone(d["radio"]["lz"], (80, 255, 255), "LANDING")
        zone(d["fire"]["exit"], (255, 160, 60), "BREACH EXIT")
        path = d["car"]["path"]
        for i in range(len(path) - 1):
            dr.line([P(path[i][0], path[i][2]), P(path[i + 1][0], path[i + 1][2])], fill=(255, 255, 255, 180), width=2)
    if name == "basement":
        zone(d["shelter"]["exit"], (80, 255, 120), "TUNNEL EXIT")
    dr.text((6, 6), "%s  slice y[%.1f, %.1f]" % (name, sy0, sy1), fill=(255, 255, 255), font=font)
    out = os.path.join(out_dir, "plan_%s.png" % name)
    img.save(out)
    return out


def checks(d):
    """Sanity checks on the exported data (printed)."""
    issues = []
    solid = [c for c in d["colliders"] if not c["t"] and c["l"] in (LAYER_WORLD, LAYER_DEFAULT)]
    # items must not be buried in solid geometry and must have a surface just below
    for it in d["items"]:
        p = it["p"]
        probe = (p[0], p[1] + 0.06, p[2])
        if any(point_in_box(c, probe, 0.005) for c in solid):
            issues.append("item inside geometry at %s (%s)" % (p, it["area"]))
        below = (p[0], p[1] - 0.04, p[2])
        if not any(point_in_box(c, below, -0.03) for c in solid) and p[1] > 0.05:
            issues.append("item floating at %s (%s)" % (p, it["area"]))
    for t in d["traps"]:
        if t["kind"] == "Tripwire":
            a, b = t["a"], t["b"]
            span = math.dist((a[0], a[2]), (b[0], b[2]))
            if not 0.8 <= span <= 2.5:
                issues.append("tripwire span %.2f at %s" % (span, a))
    # doors: something must exist on both sides
    for dd in d["doors"]:
        c = dd["center"]
        sw = dd["swing"]
        for side in (1, -1):
            p = (c[0] + sw[0] * 0.75 * side, c[1] + 1.0, c[2] + sw[2] * 0.75 * side)
            if any(point_in_box(cc, p) for cc in solid):
                issues.append("door %s blocked on side %d at %s" % (dd["name"], side, p))
    # poses where a character is placed must be free (sample a capsule: 0.3 m radius, 0.4..1.6 m high)
    def blocked(pos, radius=0.3):
        for h in (0.4, 1.0, 1.6):
            for ox, oz in ((0, 0), (radius, 0), (-radius, 0), (0, radius), (0, -radius)):
                p = (pos[0] + ox, pos[1] + h, pos[2] + oz)
                for c in solid:
                    if point_in_box(c, p):
                        return c["n"]
        return None
    poses = [("exit " + h["name"], h["exit"]["pos"]) for h in d["hiding"]]
    poses += [("cage outside %d" % i, c["outside"]["pos"]) for i, c in enumerate(d["cages"])]
    poses += [("omar spawn", d["omar"]["pos"])]
    poses += [("spectator %d" % i, (s["pos"][0], s["pos"][1] - 1.6, s["pos"][2])) for i, s in enumerate(d["spectators"])]
    for i, m in enumerate(d["mannequins"]):
        poses += [("mannequin %d alt %d" % (i, k), a["pos"]) for k, a in enumerate(m["alts"])]
    for label, p in poses:
        b = blocked(p, 0.25 if label.startswith("mannequin") else 0.3)
        if b:
            issues.append("%s at %s overlaps %s" % (label, [round(v, 2) for v in p], b))
    # nav areas reached
    areas = set(d["nav"]["areas"])
    for a in d["areas"]:
        if a["name"] not in areas and a["name"] not in ("House", "Basement", "Road", "Breach", "Windmill"):
            issues.append("no nav node in area " + a["name"])
    return issues


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else "map.json"
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(src) or ".", "plans")
    os.makedirs(out, exist_ok=True)
    with open(src) as f:
        d = json.load(f)
    for v in VIEWS:
        print("wrote", render(d, v, out))
    issues = checks(d)
    print("%d issue(s)" % len(issues))
    for i in issues:
        print("  -", i)


if __name__ == "__main__":
    main()
