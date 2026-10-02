"""Registry, source loading, normalisation and Ogg Vorbis I/O shared by all generator modules."""
import os
import subprocess
from functools import lru_cache

import numpy as np
from scipy import signal

import dsp
from dsp import SR

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT_ROOT = os.path.join(REPO, "Assets", "PrisonersOfOmar", "Resources", "Audio")
SRC_ROOT = os.path.join(REPO, "SourceAssets", "sound")

# Files copied verbatim (no re-encode).  dest (relative to Resources/Audio) -> source (relative to SourceAssets/sound)
COPIES = {
    "Omar/alarm.mp3": "sfx/alarm.mp3",
    "Omar/find.mp3": "sfx/find.mp3",
    "Omar/scream_1.mp3": "sfx/scream_1.mp3",
    "Omar/scream_2.mp3": "sfx/scream_2.mp3",
    "Omar/scream_3.mp3": "sfx/scream_3.mp3",
    "Omar/scream_4.mp3": "sfx/scream_4.mp3",
    "Music/menu_theme.mp3": "menu sound/Stay Out Of The House - Menu Theme.mp3",
    "Stingers/screams_long.mp3": "stay out of the house screams _edited_.mp3",
}

# --------------------------------------------------------------------------------------------- registry
REGISTRY = {}


class Spec:
    def __init__(self, path, fn, ch, sr, loop, norm, q, desc):
        self.path, self.fn, self.ch, self.sr, self.loop, self.norm, self.q, self.desc = \
            path, fn, ch, sr, loop, norm, q, desc

    @property
    def category(self):
        return self.path.split("/")[0]


def register(path, fn, ch=1, sr=44100, loop=False, norm=("peak", -1.0), q=4, desc=""):
    """path: Resources/Audio relative name without extension, e.g. 'Steps/wood_1'.

    norm: ('peak', dBFS) | ('lufs', LUFS, ceiling_dBFS) | ('loud', ceiling_dBFS)
    """
    if path in REGISTRY:
        raise KeyError("duplicate sound " + path)
    REGISTRY[path] = Spec(path, fn, ch, sr, loop, norm, q, desc)
    return fn


def sound(path, **kw):
    def deco(fn):
        register(path, fn, **kw)
        return fn
    return deco


# --------------------------------------------------------------------------------------------- sources
def decode(path, sr=SR, ch=1):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", str(ch), "-ar", str(sr), "-"],
                         capture_output=True, check=True).stdout
    x = np.frombuffer(raw, dtype=np.float32).astype(np.float64)
    return x if ch == 1 else x.reshape(-1, ch)


@lru_cache(maxsize=None)
def _src(rel):
    return decode(os.path.join(SRC_ROOT, rel))


def src(name):
    """Mono float copy of one of the user's sounds: scream_1..4, find, alarm, screams_long, menu."""
    table = {
        "scream_1": "sfx/scream_1.mp3", "scream_2": "sfx/scream_2.mp3", "scream_3": "sfx/scream_3.mp3",
        "scream_4": "sfx/scream_4.mp3", "find": "sfx/find.mp3", "alarm": "sfx/alarm.mp3",
        "screams_long": "stay out of the house screams _edited_.mp3",
    }
    x = _src(table[name]).copy()
    return x / (np.max(np.abs(x)) + 1e-12)


def src_segment(name, start, dur):
    x = src(name)
    a = dsp.N(start)
    return x[a:a + dsp.N(dur)].copy()


# --------------------------------------------------------------------------------------------- finishing
def _seam_metric(x):
    """Loop seam jump relative to the typical sample-to-sample step (1.0 ~ invisible)."""
    m = x if x.ndim == 1 else x.mean(1)
    d = np.abs(np.diff(m))
    typ = np.percentile(d, 90) + 1e-9
    return float(np.abs(m[0] - m[-1]) / typ)


def finish(x, spec):
    x = np.asarray(x, float)
    if spec.ch == 2 and x.ndim == 1:
        x = np.stack([x, x], 1)
    if spec.ch == 1 and x.ndim == 2:
        x = x.mean(1)
    if spec.loop:
        # remove DC + subsonics circularly (keeps the loop seamless)
        x = dsp.fft_filter(x, lambda f: 1.0 / np.sqrt(1 + (22.0 / f) ** 8))
    else:
        x = dsp.hp(x, 18.0, 2)
        x = dsp.trim_head(x, -50.0, 0.002)
        x = dsp.trim_tail(x)
        x = dsp.fade(x, 0.0008, min(0.03, 0.15 * len(x) / SR))
        # a few ms of digital silence: ffmpeg's Vorbis decoder may drop the last partial block (<=128 samples)
        x = dsp.pad(x, len(x) + dsp.N(0.006))
    mode = spec.norm[0]
    if mode == "peak":
        x = dsp.norm(x, dsp.undb(spec.norm[1]))
    elif mode == "lufs":
        target, ceil = spec.norm[1], spec.norm[2]
        for _ in range(3):
            g = dsp.undb(target - dsp.lufs(x))
            x = x * g
            pk = np.max(np.abs(x))
            if pk > dsp.undb(ceil):
                if spec.loop:  # memoryless soft knee keeps loops seamless
                    c = dsp.undb(ceil)
                    x = np.where(np.abs(x) > 0.7 * c,
                                 np.sign(x) * (0.7 * c + 0.3 * c * np.tanh((np.abs(x) - 0.7 * c) / (0.3 * c))), x)
                else:
                    x = dsp.limiter(x, ceil)
    elif mode == "loud":
        ceil = spec.norm[1]
        x = dsp.norm(x, 1.0)
        if not spec.loop:
            x = dsp.limiter(x * dsp.undb(3.0), ceil, release=0.05)
        x = dsp.norm(x, dsp.undb(ceil))
    else:
        raise ValueError(mode)
    return x


def encode(x, path, sr_out=44100, q=4, loop=False):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if sr_out != SR:
        if loop:  # Fourier resampling is circular -> loop stays seamless
            x = signal.resample(x, int(round(len(x) * sr_out / SR)), axis=0)
        else:
            x = signal.resample_poly(x, sr_out, SR, axis=0)
    ch = 1 if x.ndim == 1 else x.shape[1]
    cmd = ["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(sr_out), "-ac", str(ch), "-i", "-",
           "-c:a", "libvorbis", "-q:a", str(q), "-fflags", "+bitexact", "-flags:a", "+bitexact",
           "-map_metadata", "-1", path]
    subprocess.run(cmd, input=np.ascontiguousarray(x, dtype=np.float32).tobytes(), check=True)
    return x


def render(spec, out_root=None):
    """Generate, finish, encode and verify one sound. Returns a stats dict."""
    out_root = out_root or OUT_ROOT
    rng = dsp.rng_for(spec.path)
    x = spec.fn(rng)
    x = finish(x, spec)
    path = os.path.join(out_root, spec.path + ".ogg")
    target_pk = spec.norm[1] if spec.norm[0] in ("peak", "loud") else spec.norm[2]
    limit = min(target_pk + 0.5, -0.1)  # Vorbis may overshoot a little; never let it reach full scale
    for _ in range(4):
        encode(x, path, spec.sr, spec.q, spec.loop)
        y = decode(path, spec.sr, spec.ch)
        pk = float(dsp.db(np.max(np.abs(y))))
        if pk <= limit:
            break
        x = x * dsp.undb(limit - pk - 0.1)
    return stats(spec, x, y, path)


def granule_length(path):
    """Sample count stored in the Ogg granule positions (what Unity / FMOD will play)."""
    out = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "a:0", "-show_entries", "stream=duration_ts",
                          "-of", "default=nw=1:nk=1", path], capture_output=True, text=True, check=True).stdout
    return int(out.strip().splitlines()[0])


def centroid(mono, sr):
    """Spectral centroid (Hz) of the power spectrum (Welch average, so onsets count fully)."""
    fr, p = signal.welch(mono, sr, nperseg=min(4096, len(mono)))
    return float((p * fr).sum() / (p.sum() + 1e-20))


def stats(spec, x, y, path):
    # ffmpeg's decoder does not always honour the final granule (it may emit or drop up to one block of
    # padding at the very end); analyse exactly the samples the granule positions declare.
    expected = int(round(len(x) * spec.sr / SR))
    gran = granule_length(path)
    y = dsp.pad(y, gran)
    mono = y if y.ndim == 1 else y.mean(1)
    cen = centroid(mono, spec.sr)
    yy = y if y.ndim == 2 else y[:, None]
    full = float(np.mean(np.abs(yy) >= 0.999))
    return {
        "path": spec.path, "ext": "ogg", "dur": len(y) / spec.sr, "ch": spec.ch, "sr": spec.sr,
        "peak_db": float(dsp.db(np.max(np.abs(y)))), "lufs": float(dsp.lufs(y, spec.sr)),
        "rms_db": float(dsp.db(np.sqrt(np.mean(mono ** 2)))), "centroid": cen,
        "loop": spec.loop, "seam": _seam_metric(y) if spec.loop else None,
        "seam_src": _seam_metric(x) if spec.loop else None,
        "len_ok": abs(gran - expected) <= 1,
        "clip_frac": full, "bytes": os.path.getsize(path), "desc": spec.desc,
    }
