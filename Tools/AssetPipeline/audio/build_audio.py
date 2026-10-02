#!/usr/bin/env python3
"""Regenerate every audio asset of The Prisoners of Omar.

    python3 Tools/AssetPipeline/audio/build_audio.py            # everything
    python3 Tools/AssetPipeline/audio/build_audio.py --only Steps/wood   # names containing a substring
    python3 Tools/AssetPipeline/audio/build_audio.py --jobs 1    # single process

Outputs go to Assets/PrisonersOfOmar/Resources/Audio/** ; the manifest + QA report to
Tools/AssetPipeline/audio/manifest.md (+ manifest.csv).  Deterministic: every sound is seeded from
its own path (crc32), so re-running produces the same files.
Requires: python3, numpy, scipy, ffmpeg (with libvorbis) on PATH.
"""
import argparse
import multiprocessing as mp
import os
import shutil
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import numpy as np  # noqa: E402

import common  # noqa: E402
import dsp  # noqa: E402
import qa  # noqa: E402
# generator modules register their sounds on import
import gen_steps  # noqa: E402,F401
import gen_ui  # noqa: E402,F401
import gen_ambience  # noqa: E402,F401
import gen_oneshots  # noqa: E402,F401
import gen_player  # noqa: E402,F401
import gen_items  # noqa: E402,F401
import gen_world  # noqa: E402,F401
import gen_omar  # noqa: E402,F401
import gen_stingers  # noqa: E402,F401


def _render(path):
    t0 = time.time()
    np.seterr(all="ignore")
    s = common.render(common.REGISTRY[path])
    s["secs"] = time.time() - t0
    return s


def copy_user_files(out_root):
    stats = []
    for dest, srcrel in common.COPIES.items():
        src = os.path.join(common.SRC_ROOT, srcrel)
        dst = os.path.join(out_root, dest)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        y = common.decode(dst, 44100, 2)
        m = y.mean(1)
        stats.append({
            "path": dest.rsplit(".", 1)[0], "ext": "mp3", "kind": "user (verbatim copy)", "dur": len(y) / 44100,
            "ch": 2, "sr": 44100, "peak_db": float(dsp.db(np.abs(y).max())), "lufs": float(dsp.lufs(y)),
            "rms_db": float(dsp.db(np.sqrt(np.mean(m ** 2)))), "centroid": common.centroid(m, 44100),
            "loop": False, "seam": None, "len_ok": True, "clip_frac": 0.0, "bytes": os.path.getsize(dst),
            "desc": "copied verbatim from SourceAssets/sound/" + srcrel,
        })
    return stats


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default=None, help="only render sounds whose path contains this substring")
    ap.add_argument("--jobs", type=int, default=max(1, min(8, os.cpu_count() or 1)))
    ap.add_argument("--out", default=common.OUT_ROOT)
    args = ap.parse_args()

    paths = sorted(common.REGISTRY)
    if args.only:
        paths = [p for p in paths if args.only in p]
    print(f"rendering {len(paths)} sounds with {args.jobs} job(s) -> {args.out}")
    t0 = time.time()
    if args.out != common.OUT_ROOT:
        common.OUT_ROOT = args.out
    if args.jobs > 1 and len(paths) > 1:
        with mp.get_context("fork").Pool(args.jobs) as pool:
            results = pool.map(_render, paths, chunksize=1)
    else:
        results = [_render(p) for p in paths]
    for s in results:
        flag = " LOOP seam=%.2f" % s["seam"] if s["loop"] else ""
        print(f"  {s['path']:<34} {s['dur']:6.2f}s ch{s['ch']} pk {s['peak_db']:6.1f} "
              f"LUFS {s['lufs']:6.1f} cen {s['centroid']:6.0f}{flag}  ({s['secs']:.1f}s)")
    if args.only:
        print(f"done in {time.time() - t0:.1f}s (partial build: manifest not rewritten)")
        return
    results += copy_user_files(args.out)
    problems = qa.check(results, args.out)
    qa.write_manifest(results, problems, os.path.join(HERE, "manifest.md"), os.path.join(HERE, "manifest.csv"))
    total = sum(s["bytes"] for s in results)
    print(f"{len(results)} files, {total / 1e6:.2f} MB, {time.time() - t0:.1f}s")
    if problems:
        print("QA PROBLEMS:")
        for p in problems:
            print("  " + p)
        sys.exit(1)
    print("QA OK")


if __name__ == "__main__":
    main()
