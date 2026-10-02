#!/usr/bin/env python3
"""The Prisoners of Omar - texture generator.

Regenerates every texture under Assets/PrisonersOfOmar/Resources/Textures/{Env,Decals,Props,
Foliage,Sky,FX,UI} deterministically (fixed seeds derived from each texture's name).

    python3 Tools/AssetPipeline/textures/build_textures.py              # build everything
    python3 Tools/AssetPipeline/textures/build_textures.py --only 'Env/*' --qa /tmp/qa
    python3 Tools/AssetPipeline/textures/build_textures.py --list

Requirements: python3, numpy, scipy, pillow, OpenEXR (pip). Photo sources are downloaded
on first run into ~/.cache/poc_assets (see potex/sources.py and README.md)."""
import argparse
import fnmatch
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from PIL import Image  # noqa: E402

from potex import core, sources  # noqa: E402
from potex import env, decals, props, foliage, sky, fx, ui  # noqa: E402,F401  (registers builders)
from potex import qa  # noqa: E402


def build_one(spec, src):
    ctx = core.Ctx(spec, src)
    res = spec["fn"](ctx)
    if isinstance(res, Image.Image):
        img = res
    else:
        if isinstance(res, tuple):
            rgb, a = res
        else:
            rgb, a = res, None
        img = core.degrade(rgb, spec["size"], tile=spec["tile"], alpha=a, alpha_mode=spec["alpha"], **spec["deg"])
    if img.size != tuple(spec["size"]):
        raise RuntimeError("%s: produced %s, expected %s" % (spec["path"], img.size, spec["size"]))
    want = "RGBA" if spec["alpha"] else "RGB"
    if img.mode != want:
        img = img.convert(want)
    out = os.path.join(core.OUT_ROOT, spec["path"] + ".png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    img.save(out, optimize=True)
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", action="append", help="glob on the texture path, e.g. 'Env/wall_*' (repeatable)")
    ap.add_argument("--qa", help="write contact sheets into this directory (keep it outside the repo)")
    ap.add_argument("--list", action="store_true")
    args = ap.parse_args()

    specs = core.REGISTRY
    if args.only:
        specs = [s for s in specs if any(fnmatch.fnmatch(s["path"], p) for p in args.only)]
    if args.list:
        for s in specs:
            print("%-40s %dx%d %s%s" % (s["path"], s["size"][0], s["size"][1], "tile " if s["tile"] else "", s["alpha"] or ""))
        print(len(specs), "textures")
        return
    src = sources.Sources()
    t0 = time.time()
    built = []
    for s in specs:
        t = time.time()
        out = build_one(s, src)
        built.append(s)
        print("  %-42s %4.1fs" % (s["path"], time.time() - t), flush=True)
    print("built %d textures in %.1fs" % (len(built), time.time() - t0))
    if args.qa:
        qa.contact_sheets(built, args.qa)
        print("contact sheets ->", args.qa)


if __name__ == "__main__":
    main()
