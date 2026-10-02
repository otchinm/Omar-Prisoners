"""CC0 / public-domain photo sources used as real-photo detail layers.

The scikit-image sample images (brick, grass, gravel, coffee, chelsea, moon, ihc, ...) are shipped inside the
scikit-image wheel. We download the wheel once with pip (no install), extract skimage/data into
Tools/AssetPipeline/characters/.cache/ (git-ignored) and load them from there.
"""
import glob
import os
import subprocess
import sys
import zipfile

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(HERE, ".cache")
DATA = os.path.join(CACHE, "skimage_data")
WHEEL_VERSION = "0.26.0"

_cache = {}


def ensure_photos():
    if os.path.isfile(os.path.join(DATA, "grass.png")):
        return
    os.makedirs(CACHE, exist_ok=True)
    wheels = glob.glob(os.path.join(CACHE, "scikit_image-*.whl"))
    if not wheels:
        print("downloading scikit-image wheel (photo sources) ...")
        subprocess.check_call([sys.executable, "-m", "pip", "download", "--no-deps", "--only-binary=:all:",
                               "--python-version", "3.11", "--platform", "manylinux_2_28_x86_64",
                               "scikit-image==" + WHEEL_VERSION, "-d", CACHE])
        wheels = glob.glob(os.path.join(CACHE, "scikit_image-*.whl"))
    os.makedirs(DATA, exist_ok=True)
    with zipfile.ZipFile(wheels[0]) as z:
        for n in z.namelist():
            if n.startswith("skimage/data/") and n.lower().endswith((".png", ".jpg")):
                with open(os.path.join(DATA, os.path.basename(n)), "wb") as f:
                    f.write(z.read(n))


def photo(name):
    """RGB float32 array in [0,1]."""
    if name in _cache:
        return _cache[name]
    ensure_photos()
    path = os.path.join(DATA, name)
    if not os.path.exists(path):
        for ext in (".png", ".jpg"):
            if os.path.exists(path + ext):
                path = path + ext
                break
    im = Image.open(path).convert("RGB")
    a = np.asarray(im, dtype=np.float32) / 255.0
    _cache[name] = a
    return a


def gray(name):
    a = photo(name)
    return a[..., 0] * 0.3 + a[..., 1] * 0.59 + a[..., 2] * 0.11
