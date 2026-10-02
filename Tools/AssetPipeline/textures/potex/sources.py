"""Photo sources. Everything is downloaded once into a cache OUTSIDE the repo
(default ~/.cache/poc_assets, override with POC_ASSET_CACHE) and loaded from there.

* scikit-image wheel (PyPI)      -> skimage/data/*.png  (CC0 / public domain sample photos)
* @pmndrs/assets (npm, CC0)      -> hdri/*.exr.js        (Poly Haven HDRIs, 512x256 equirect)
"""
import base64
import glob
import io
import os
import re
import subprocess
import sys
import tarfile
import urllib.request
import zipfile

import numpy as np
from PIL import Image

CACHE = os.environ.get("POC_ASSET_CACHE", os.path.expanduser("~/.cache/poc_assets"))
PMNDRS_URL = "https://registry.npmjs.org/@pmndrs/assets/-/assets-1.7.0.tgz"
SKIMAGE_VERSION = "0.26.0"


def _log(msg):
    print("[sources] " + msg, flush=True)


def ensure_skimage():
    d = os.path.join(CACHE, "skdata", "skimage", "data")
    if os.path.exists(os.path.join(d, "gravel.png")):
        return d
    os.makedirs(os.path.join(CACHE, "skimg"), exist_ok=True)
    whl = glob.glob(os.path.join(CACHE, "skimg", "scikit_image-*.whl"))
    if not whl:
        _log("downloading scikit-image wheel (sample photos)...")
        subprocess.check_call([sys.executable, "-m", "pip", "download", "--no-deps", "--only-binary=:all:",
                               "scikit-image==" + SKIMAGE_VERSION, "-d", os.path.join(CACHE, "skimg")])
        whl = glob.glob(os.path.join(CACHE, "skimg", "scikit_image-*.whl"))
    with zipfile.ZipFile(whl[0]) as z:
        for n in z.namelist():
            if n.startswith("skimage/data/") and not n.endswith("/"):
                z.extract(n, os.path.join(CACHE, "skdata"))
    return d


def ensure_pmndrs():
    d = os.path.join(CACHE, "pm", "package")
    if os.path.exists(os.path.join(d, "hdri", "night.exr.js")):
        return d
    os.makedirs(CACHE, exist_ok=True)
    tgz = os.path.join(CACHE, "pm.tgz")
    if not os.path.exists(tgz):
        _log("downloading @pmndrs/assets (HDRIs)...")
        urllib.request.urlretrieve(PMNDRS_URL, tgz)
    with tarfile.open(tgz) as t:
        members = [m for m in t.getmembers() if m.name.startswith("package/hdri/") or m.name in ("package/LICENSE", "package/README.md")]
        try:
            t.extractall(os.path.join(CACHE, "pm"), members=members, filter="data")
        except TypeError:  # Python without extraction filters
            t.extractall(os.path.join(CACHE, "pm"), members=members)
    return d


class Sources:
    def __init__(self):
        self._sk = None
        self._pm = None
        self._cache = {}

    # ---- scikit-image photos ----
    def sk_path(self, name):
        if self._sk is None:
            self._sk = ensure_skimage()
        return os.path.join(self._sk, name)

    def gray(self, name):
        """grayscale float 0..1 (brick, grass, gravel, camera, moon ...)"""
        key = ("g", name)
        if key not in self._cache:
            fn = name if "." in name else name + ".png"
            im = Image.open(self.sk_path(fn)).convert("L")
            self._cache[key] = np.asarray(im, np.float32) / 255.0
        return self._cache[key]

    def rgb(self, name):
        key = ("c", name)
        if key not in self._cache:
            fn = name if "." in name else name + ".png"
            im = Image.open(self.sk_path(fn)).convert("RGB")
            self._cache[key] = np.asarray(im, np.float32) / 255.0
        return self._cache[key]

    # ---- HDRIs ----
    def hdri(self, name):
        """linear float32 (256, 512, 3) equirectangular panorama"""
        key = ("h", name)
        if key in self._cache:
            return self._cache[key]
        if self._pm is None:
            self._pm = ensure_pmndrs()
        exr = os.path.join(CACHE, "decoded", name + ".exr")
        if not os.path.exists(exr):
            os.makedirs(os.path.dirname(exr), exist_ok=True)
            txt = open(os.path.join(self._pm, "hdri", name + ".exr.js")).read()
            m = re.search(r"base64,([A-Za-z0-9+/=]+)", txt)
            open(exr, "wb").write(base64.b64decode(m.group(1)))
        import OpenEXR  # pip install OpenEXR
        with OpenEXR.File(exr) as f:
            ch = f.channels()
            if "RGB" in ch:
                px = np.asarray(ch["RGB"].pixels, np.float32)
            else:
                px = np.stack([np.asarray(ch[c].pixels, np.float32) for c in "RGB"], -1)
        px = np.nan_to_num(px, nan=0.0, posinf=0.0, neginf=0.0)
        px = np.clip(px, 0.0, 64.0)
        self._cache[key] = px
        return px

    def hdri_ldr(self, name, exposure=1.0, gamma=2.2):
        """tonemapped 0..1 version (Reinhard)"""
        h = self.hdri(name) * exposure
        t = h / (1.0 + h)
        return np.clip(t, 0, 1) ** (1.0 / gamma)
