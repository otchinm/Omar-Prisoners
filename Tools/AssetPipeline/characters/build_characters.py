#!/usr/bin/env python3
"""Entry point: generates every character atlas and item texture (deterministic).

    python3 Tools/AssetPipeline/characters/build_characters.py [--only NAME ...] [--preview DIR]

Outputs:
    Assets/PrisonersOfOmar/Resources/Textures/Characters/<name>.png   (256x256 RGBA atlases, layout: atlas.py)
    Assets/PrisonersOfOmar/Resources/Textures/Items/<name>.png
--preview DIR also writes 4x nearest-neighbour contact sheets there (keep it outside the repo).
Requires numpy, scipy, Pillow. Photo sources are fetched once into .cache/ (see photos.py).
"""
import argparse
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

import char_textures  # noqa: E402
import item_textures  # noqa: E402
from paint import to_image  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT_CHARS = os.path.join(ROOT, "Assets", "PrisonersOfOmar", "Resources", "Textures", "Characters")
OUT_ITEMS = os.path.join(ROOT, "Assets", "PrisonersOfOmar", "Resources", "Textures", "Items")


def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, optimize=True)
    print("wrote", os.path.relpath(path, ROOT), img.size, img.mode)


def sheet(images, path, scale=4, cols=4):
    ims = [im.convert("RGBA") for im in images]
    w = max(i.width for i in ims) * scale
    h = max(i.height for i in ims) * scale
    rows = (len(ims) + cols - 1) // cols
    out = Image.new("RGBA", (cols * w, rows * h), (255, 0, 255, 255))
    for k, im in enumerate(ims):
        big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        bg = Image.new("RGBA", big.size, (255, 0, 255, 255))
        bg.alpha_composite(big)
        out.paste(bg, ((k % cols) * w, (k // cols) * h))
    out.convert("RGB").save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*", default=None)
    ap.add_argument("--preview", default=None)
    ap.add_argument("--chars-only", action="store_true")
    ap.add_argument("--items-only", action="store_true")
    args = ap.parse_args()

    char_imgs = []
    if not args.items_only:
        for name in char_textures.CHARACTERS:
            if args.only and name not in args.only:
                continue
            rgb, alpha = char_textures.build(name)
            img = to_image(rgb, alpha)
            save(img, os.path.join(OUT_CHARS, name + ".png"))
            char_imgs.append(img)
    item_imgs = []
    if not args.chars_only:
        for name, fn in item_textures.ITEMS.items():
            if args.only and name not in args.only:
                continue
            rgb, alpha = fn()
            img = to_image(rgb, alpha)
            save(img, os.path.join(OUT_ITEMS, name + ".png"))
            item_imgs.append(img)
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)
        if char_imgs:
            sheet(char_imgs, os.path.join(args.preview, "characters.png"), scale=3, cols=3)
        if item_imgs:
            sheet(item_imgs, os.path.join(args.preview, "items.png"), scale=3, cols=5)


if __name__ == "__main__":
    main()
