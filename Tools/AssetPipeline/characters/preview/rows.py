#!/usr/bin/env python3
"""Renders every row_*.json of a directory (from the 'allanims' scenario) and stacks them into one labelled sheet.

    rows.py DIR out.png [--view front|side|q] [--px 90]
"""
import argparse
import glob
import os
import subprocess
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dir")
    ap.add_argument("out")
    ap.add_argument("--view", default="front")
    ap.add_argument("--px", default="90")
    args = ap.parse_args()
    rows = sorted(glob.glob(os.path.join(args.dir, "row_*.json")))
    ims = []
    for r in rows:
        png = r[:-5] + ".png"
        subprocess.check_call([sys.executable, os.path.join(HERE, "render.py"), r, png, "--views", args.view, "--px", args.px],
                              stdout=subprocess.DEVNULL)
        im = Image.open(png).convert("RGB")
        d = ImageDraw.Draw(im)
        d.text((4, 4), os.path.basename(r)[7:-5], fill=(255, 255, 0))
        ims.append(im)
    W = max(i.width for i in ims)
    H = sum(i.height for i in ims)
    out = Image.new("RGB", (W, H), (20, 20, 20))
    y = 0
    for i in ims:
        out.paste(i, (0, y))
        y += i.height
    out.save(args.out)
    print("wrote", args.out, out.size)


if __name__ == "__main__":
    main()
