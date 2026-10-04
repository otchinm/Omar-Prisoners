"""Small DSP toolkit for the Prisoners of Omar audio pipeline.

Only numpy + scipy. Everything works on float64 arrays at SR = 44100 Hz; mono signals are 1-D,
stereo signals are shaped (n, 2).  Every random choice goes through a numpy Generator that the
caller passes in, so the whole pipeline is deterministic.

Conventions
* "circular" helpers produce signals that are exactly periodic over their length - they are used to
  build seamless loops without any audible crossfade.
* Frequencies in Hz, times in seconds, gains in linear units unless the name says *_db.
"""
import zlib
from fractions import Fraction

import numpy as np
from scipy import signal

SR = 44100
TAU = 2.0 * np.pi


# --------------------------------------------------------------------------------------------- basics
def seed_of(name):
    return zlib.crc32(name.encode("utf-8"))


def rng_for(name, salt=0):
    return np.random.default_rng([seed_of(name), salt])


def N(sec):
    return max(1, int(round(sec * SR)))


def T(n):
    return np.arange(n) / SR


def db(x):
    return 20.0 * np.log10(np.maximum(np.abs(x), 1e-12))


def undb(d):
    return 10.0 ** (d / 20.0)


def qfreq(f, n):
    """Quantise a frequency so that it completes an integer number of cycles in n samples."""
    k = max(1, int(round(f * n / SR)))
    return k * SR / n


def norm(x, peak=1.0):
    m = np.max(np.abs(x)) if len(x) else 0.0
    return x * (peak / m) if m > 0 else x


def pad(x, n):
    """Zero pad (or cut) the first axis to n samples."""
    if len(x) >= n:
        return x[:n]
    shape = (n - len(x),) + x.shape[1:]
    return np.concatenate([x, np.zeros(shape)])


def mix(*parts):
    """Sum signals of different lengths (all mono or all stereo)."""
    n = max(len(p) for p in parts)
    out = None
    for p in parts:
        p = pad(np.asarray(p, float), n)
        out = p.copy() if out is None else out + p
    return out


def place(buf, x, pos, gain=1.0, circular=False):
    """Add x into buf at sample pos (in place). circular=True wraps around the buffer end."""
    pos = int(pos)
    n = len(buf)
    if circular:
        idx = (pos + np.arange(len(x))) % n
        np.add.at(buf, idx, gain * x)
        return buf
    if pos >= n:
        return buf
    if pos < 0:
        x = x[-pos:]
        pos = 0
    m = min(len(x), n - pos)
    buf[pos:pos + m] += gain * x[:m]
    return buf


def fade(x, fin=0.002, fout=0.01):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a > 0:
        r = np.linspace(0, 1, a) ** 2
        x[:a] *= r if x.ndim == 1 else r[:, None]
    if b > 0:
        r = np.linspace(1, 0, b) ** 2
        x[-b:] *= r if x.ndim == 1 else r[:, None]
    return x


def trim_tail(x, thresh_db=-62.0, keep=0.02):
    """Cut trailing near-silence."""
    mono = np.abs(x) if x.ndim == 1 else np.abs(x).max(1)
    pk = mono.max() if len(mono) else 0
    if pk <= 0:
        return x
    idx = np.nonzero(mono > pk * undb(thresh_db))[0]
    end = min(len(x), idx[-1] + N(keep)) if len(idx) else len(x)
    return x[:end]


def trim_head(x, thresh_db=-40.0, pre=0.005):
    mono = np.abs(x) if x.ndim == 1 else np.abs(x).max(1)
    idx = np.nonzero(mono > mono.max() * undb(thresh_db))[0]
    st = max(0, idx[0] - N(pre)) if len(idx) else 0
    return x[st:]


# --------------------------------------------------------------------------------------------- envelopes
def env(points, n):
    """Piecewise linear envelope from [(time_s, value), ...]."""
    t, v = zip(*points)
    return np.interp(T(n), t, v)


def expdec(n, tau, delay=0.0):
    t = T(n) - delay
    e = np.exp(-np.maximum(t, 0) / tau)
    e[t < 0] = 0.0
    return e


def perc(n, attack, tau, delay=0.0):
    """Linear attack then exponential decay."""
    t = T(n) - delay
    e = np.where(t < attack, t / max(attack, 1e-6), np.exp(-(t - attack) / tau))
    e[t < 0] = 0.0
    return e


def ar(n, a, r, hold=0.0, curve=2.0):
    t = T(n)
    e = np.ones(n)
    e[t < a] = (t[t < a] / max(a, 1e-6)) ** (1 / curve)
    rel = t > a + hold
    e[rel] = np.maximum(0, 1 - (t[rel] - a - hold) / max(r, 1e-6)) ** curve
    return e


def smooth_rand(n, rng, rate, circular=True):
    """Smooth random curve (std 1) with energy below ~rate Hz. Circular (periodic over n)."""
    m = n if circular else n + N(2.0 / max(rate, 0.05))
    spec = np.fft.rfft(rng.standard_normal(m))
    f = np.fft.rfftfreq(m, 1 / SR)
    spec *= np.exp(-0.5 * (f / max(rate, 1e-3)) ** 2)
    spec[0] = 0
    y = np.fft.irfft(spec, m)[:n]
    s = np.std(y)
    return y / s if s > 0 else y


# --------------------------------------------------------------------------------------------- noise
def white(n, rng):
    return rng.standard_normal(n)


def shaped_noise(n, rng, amp_fn):
    """Circular noise with magnitude response amp_fn(freqs). Exactly periodic over n samples."""
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1 / SR)
    spec *= amp_fn(np.maximum(f, 1e-3))
    spec[0] = 0
    y = np.fft.irfft(spec, n)
    s = np.std(y)
    return y / s if s > 0 else y


def pink(n, rng, fmin=20.0):
    return shaped_noise(n, rng, lambda f: 1.0 / np.sqrt(np.maximum(f, fmin)))


def brown(n, rng, fmin=15.0):
    return shaped_noise(n, rng, lambda f: 1.0 / np.maximum(f, fmin))


def band_noise(n, rng, lo=None, hi=None, order=2, tilt=0.0):
    """Circular noise with a smooth butterworth-like band shape (+ optional dB/oct tilt)."""
    def amp(f):
        a = np.ones_like(f)
        if lo:
            a /= np.sqrt(1 + (lo / f) ** (2 * order))
        if hi:
            a /= np.sqrt(1 + (f / hi) ** (2 * order))
        if tilt:
            a *= (f / 1000.0) ** (tilt / 6.02)
        return a
    return shaped_noise(n, rng, amp)


def crackle(n, rng, rate, amp_spread=1.0):
    """Sparse random impulses (Poisson, rate per second) with log-normal amplitudes."""
    out = np.zeros(n)
    k = max(1, rng.poisson(rate * n / SR))
    pos = rng.integers(0, n, k)
    amps = np.exp(rng.normal(0, amp_spread, k)) * rng.choice([-1, 1], k)
    np.add.at(out, pos, amps)
    return out / (np.max(np.abs(out)) + 1e-12)


# --------------------------------------------------------------------------------------------- filters
def _sos(kind, f, order=2):
    nyq = SR / 2
    if isinstance(f, (list, tuple)):
        f = [min(max(v, 5.0), nyq * 0.98) for v in f]
    else:
        f = min(max(f, 5.0), nyq * 0.98)
    return signal.butter(order, f, btype=kind, fs=SR, output="sos")


def sosf(x, sos):
    return signal.sosfilt(sos, x, axis=0)


def lp(x, f, order=2):
    return sosf(x, _sos("lowpass", f, order))


def hp(x, f, order=2):
    return sosf(x, _sos("highpass", f, order))


def bp(x, lo, hi, order=2):
    return sosf(x, _sos("bandpass", [lo, hi], order))


def rbj(kind, f, q=0.707, gain_db=0.0):
    """RBJ cookbook biquad -> sos row."""
    f = min(max(f, 5.0), SR * 0.49)
    w = TAU * f / SR
    cw, sw = np.cos(w), np.sin(w)
    al = sw / (2 * q)
    A = 10 ** (gain_db / 40)
    if kind == "lp":
        b = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2]
        a = [1 + al, -2 * cw, 1 - al]
    elif kind == "hp":
        b = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2]
        a = [1 + al, -2 * cw, 1 - al]
    elif kind == "bp":  # constant 0 dB peak gain
        b = [al, 0, -al]
        a = [1 + al, -2 * cw, 1 - al]
    elif kind == "notch":
        b = [1, -2 * cw, 1]
        a = [1 + al, -2 * cw, 1 - al]
    elif kind == "peak":
        b = [1 + al * A, -2 * cw, 1 - al * A]
        a = [1 + al / A, -2 * cw, 1 - al / A]
    elif kind == "lowshelf":
        sA = 2 * np.sqrt(A) * al
        b = [A * ((A + 1) - (A - 1) * cw + sA), 2 * A * ((A - 1) - (A + 1) * cw), A * ((A + 1) - (A - 1) * cw - sA)]
        a = [(A + 1) + (A - 1) * cw + sA, -2 * ((A - 1) + (A + 1) * cw), (A + 1) + (A - 1) * cw - sA]
    elif kind == "highshelf":
        sA = 2 * np.sqrt(A) * al
        b = [A * ((A + 1) + (A - 1) * cw + sA), -2 * A * ((A - 1) + (A + 1) * cw), A * ((A + 1) + (A - 1) * cw - sA)]
        a = [(A + 1) - (A - 1) * cw + sA, 2 * ((A - 1) - (A + 1) * cw), (A + 1) - (A - 1) * cw - sA]
    else:
        raise ValueError(kind)
    b = np.array(b) / a[0]
    a = np.array(a) / a[0]
    return np.array([[b[0], b[1], b[2], 1.0, a[1], a[2]]])


def bq(x, kind, f, q=0.707, gain_db=0.0):
    return sosf(x, rbj(kind, f, q, gain_db))


def peq(x, f, q, gain_db):
    return bq(x, "peak", f, q, gain_db)


def reson(x, f, q):
    return bq(x, "bp", f, q)


def resonbank(x, freqs, qs, gains):
    out = np.zeros_like(x)
    for f, q, g in zip(freqs, qs, gains):
        if f < SR * 0.47:
            out += g * reson(x, f, q)
    return out


def tvf(x, kind, freq, q=0.707, block=64, stages=1, gain_db=0.0, circular=False):
    """Time-varying RBJ biquad (coefficients updated every `block` samples). freq/q scalar or per-sample."""
    n = len(x)
    freq = np.broadcast_to(np.asarray(freq, float), (n,))
    qv = np.broadcast_to(np.asarray(q, float), (n,))
    if circular:
        y = tvf(np.concatenate([x, x]), kind, np.concatenate([freq, freq]), np.concatenate([qv, qv]),
                block, stages, gain_db)
        return y[n:]
    out = np.empty(n)
    zi = np.zeros((stages, 2))
    for i in range(0, n, block):
        j = min(n, i + block)
        s = np.repeat(rbj(kind, float(freq[i:j].mean()), float(qv[i:j].mean()), gain_db), stages, axis=0)
        out[i:j], zi = signal.sosfilt(s, x[i:j], zi=zi)
    return out


def circ_filter(x, sos):
    """IIR filtering of a periodic signal so the result is still seamless."""
    n = len(x)
    return signal.sosfilt(sos, np.concatenate([x, x]), axis=0)[n:]


def fft_filter(x, amp_fn):
    """Zero-phase circular filtering by a magnitude response (exactly loop safe)."""
    n = len(x)
    f = np.fft.rfftfreq(n, 1 / SR)
    a = amp_fn(np.maximum(f, 1e-3))
    if x.ndim == 2:
        return np.fft.irfft(np.fft.rfft(x, axis=0) * a[:, None], n, axis=0)
    return np.fft.irfft(np.fft.rfft(x) * a, n)


def comb(x, delay_s, g, damp=None):
    """Feedback comb (metallic / tube resonance)."""
    d = max(1, int(round(delay_s * SR)))
    a = np.zeros(d + 1)
    a[0] = 1
    a[d] = -g
    y = signal.lfilter([1.0], a, x, axis=0)
    if damp:
        y = lp(y, damp)
    return y


# --------------------------------------------------------------------------------------------- oscillators
def phase(freq, n, ph0=0.0):
    f = np.broadcast_to(np.asarray(freq, float), (n,))
    return ph0 + np.concatenate([[0.0], np.cumsum(f[:-1])]) / SR


def sine(freq, n, ph0=0.0):
    return np.sin(TAU * phase(freq, n, ph0))


def _blep(t, dt):
    c = np.zeros_like(t)
    m = t < dt
    u = t[m] / dt[m]
    c[m] = u + u - u * u - 1
    m2 = t > 1 - dt
    u = (t[m2] - 1) / dt[m2]
    c[m2] = u * u + u + u + 1
    return c


def saw(freq, n, ph0=0.0):
    f = np.broadcast_to(np.asarray(freq, float), (n,))
    p = phase(f, n, ph0) % 1.0
    dt = np.clip(np.abs(f) / SR, 1e-9, 0.5)
    return 2 * p - 1 - _blep(p, dt)


def square(freq, n, pw=0.5, ph0=0.0):
    f = np.broadcast_to(np.asarray(freq, float), (n,))
    p = phase(f, n, ph0) % 1.0
    dt = np.clip(np.abs(f) / SR, 1e-9, 0.5)
    s = np.where(p < pw, 1.0, -1.0)
    s += _blep(p, dt)
    s -= _blep((p - pw) % 1.0, dt)
    return s


def tri(freq, n, ph0=0.0):
    p = phase(freq, n, ph0) % 1.0
    return 4 * np.abs(p - 0.5) - 1


def pulse_train(rate, n, rng=None, jitter=0.0, amp_jitter=0.0):
    """Unit impulses at instantaneous rate (Hz, scalar or curve). Returns (signal, positions)."""
    r = np.broadcast_to(np.asarray(rate, float), (n,)).copy()
    if rng is not None and jitter > 0:
        r *= np.exp(jitter * smooth_rand(n, rng, 30.0, circular=False) * 0.5)
        r *= np.exp(jitter * rng.standard_normal(n) * 0.3)
    ph = np.cumsum(r) / SR
    pos = np.nonzero(np.diff(np.floor(ph)) > 0)[0] + 1
    out = np.zeros(n)
    amps = np.ones(len(pos))
    if rng is not None and amp_jitter > 0:
        amps = np.clip(1 + amp_jitter * rng.standard_normal(len(pos)), 0.05, 3)
    out[pos] = amps
    return out, pos


# --------------------------------------------------------------------------------------------- modal / physical
def modal_ir(n, modes, rng=None, ph_random=True):
    """modes: iterable of (freq, decay_tau_s, amp). Sum of exponentially decaying sinusoids."""
    t = T(n)
    out = np.zeros(n)
    for f, tau, a in modes:
        if f >= SR * 0.47 or f <= 0:
            continue
        ph = rng.uniform(0, TAU) if (rng is not None and ph_random) else 0.0
        out += a * np.exp(-t / tau) * np.sin(TAU * f * t + ph)
    return out


def excite(exc, ir, n=None):
    n = n or (len(exc) + len(ir) - 1)
    return signal.fftconvolve(exc, ir)[:n]


def burst(n, rng, attack=0.0005, tau=0.004, lp_hz=None, hp_hz=None):
    """Short noise burst used as an excitation (strike / scrape grain)."""
    x = rng.standard_normal(n) * perc(n, attack, tau)
    if lp_hz:
        x = lp(x, lp_hz)
    if hp_hz:
        x = hp(x, hp_hz)
    return x


def click(n, f=3000.0, q=4.0, tau=0.002, rng=None):
    """Very short resonant click."""
    x = np.zeros(n)
    x[0] = 1.0
    if rng is not None:
        x[:N(0.001)] += 0.3 * rng.standard_normal(N(0.001))
    y = reson(x, f, q)
    return y * expdec(n, tau)


def metal_modes(f0, count, rng, spread=1.0, tau=(0.2, 1.0), amp_tilt=-0.4):
    """Inharmonic plate/bar-like modal set."""
    modes = []
    for k in range(count):
        ratio = (k + 1) ** 1.42 * (1 + rng.uniform(-0.06, 0.06) * spread)
        f = f0 * ratio
        modes.append((f, rng.uniform(*tau) / (1 + 0.25 * k), (k + 1) ** amp_tilt * rng.uniform(0.5, 1.0)))
    return modes


def stick_slip(n, rng, rate, modes, jitter=0.25, amp_env=None, grain_tau=0.0006, roughness=0.3):
    """Friction creak / squeal: jittered impulse train (stick-slip) through a resonator bank.

    rate: instantaneous slip rate (Hz) scalar or curve; modes: [(freq, q, gain)].
    """
    exc, pos = pulse_train(rate, n, rng, jitter=jitter, amp_jitter=roughness)
    g = np.exp(-np.arange(N(0.004)) / (grain_tau * SR)) * rng.standard_normal(N(0.004))
    g[0] = 2.0
    exc = signal.fftconvolve(exc, g)[:n]
    if amp_env is not None:
        exc *= amp_env
    y = np.zeros(n)
    for f, q, gain in modes:
        if f < SR * 0.47:
            y += gain * reson(exc, f, q)
    return y


# --------------------------------------------------------------------------------------------- nonlinear / lo-fi
def sat(x, drive=2.0):
    return np.tanh(drive * x) / np.tanh(drive)


def asym_sat(x, drive=2.0, bias=0.2):
    y = np.tanh(drive * (x + bias)) - np.tanh(drive * bias)
    return y / (np.max(np.abs(y)) + 1e-12) * np.max(np.abs(x))


def hardclip(x, t=0.5):
    return np.clip(x, -t, t) / t


def fold(x, gain=2.0):
    y = x * gain
    return np.abs(((y - 1) % 4) - 2) - 1


def crush(x, bits):
    q = 2.0 ** (bits - 1)
    return np.round(x * q) / q


def hold(x, factor):
    """Sample-and-hold decimation (no anti-alias filter: gritty aliasing on purpose)."""
    if factor <= 1:
        return x
    n = len(x)
    idx = (np.floor(np.arange(n) / factor) * factor).astype(int)
    return x[np.minimum(idx, n - 1)]


def lofi(x, bits=12, factor=1.5, drive=1.3, lp_hz=11000):
    """House 'VHS / PS1' colouring: soft saturation, sample-rate reduction, bit-crush, tape HF loss."""
    pk = np.max(np.abs(x)) + 1e-12
    y = sat(x / pk, drive) * pk
    y = hold(y, factor)
    y = crush(y / pk, bits) * pk
    if lp_hz:
        y = lp(y, lp_hz)
    return y


def varispeed(x, rate):
    """Read x at a (possibly time-varying) speed. rate>1 = higher & shorter."""
    rate = np.asarray(rate, float)
    if rate.ndim == 0:
        m = int(len(x) / float(rate))
        pos = np.arange(m) * float(rate)
    else:
        pos = np.concatenate([[0.0], np.cumsum(rate[:-1])])
        pos = pos[pos < len(x) - 1]
    if x.ndim == 2:
        return np.stack([np.interp(pos, np.arange(len(x)), x[:, c]) for c in range(x.shape[1])], 1)
    return np.interp(pos, np.arange(len(x)), x)


def resample(x, ratio):
    """High quality pitch change by resampling. ratio>1 -> higher pitch, shorter."""
    fr = Fraction(1.0 / ratio).limit_denominator(160)
    return signal.resample_poly(x, fr.numerator, fr.denominator, axis=0)


def semis(s):
    return 2.0 ** (s / 12.0)


def wowflutter(x, rng, wow=0.0015, wow_rate=0.5, flutter=0.0003, flutter_rate=7.0, circular=False):
    """Tape speed instability as a modulated delay. Depths are in seconds of delay deviation."""
    n = len(x)
    if circular:
        wr, fr_ = qfreq(wow_rate, n), qfreq(flutter_rate, n)
        d = wow * np.sin(TAU * phase(wr, n, rng.uniform())) + \
            0.35 * wow * np.sin(TAU * phase(qfreq(wow_rate * 2.37, n), n, rng.uniform()))
        d += flutter * np.sin(TAU * phase(fr_, n, rng.uniform()))
    else:
        d = wow * smooth_rand(n, rng, wow_rate, circular=False) * 0.7
        d += wow * 0.5 * np.sin(TAU * phase(wow_rate, n, rng.uniform()))
        d += flutter * np.sin(TAU * phase(flutter_rate, n, rng.uniform()))
    d = (d - d.min()) * SR + 1.0
    pos = np.arange(n) - d
    if circular:
        idx = np.arange(-n, 2 * n)
        if x.ndim == 2:
            return np.stack([np.interp(pos, idx, np.tile(x[:, c], 3)) for c in range(2)], 1)
        return np.interp(pos, idx, np.tile(x, 3))
    if x.ndim == 2:
        return np.stack([np.interp(pos, np.arange(n), x[:, c], left=0) for c in range(2)], 1)
    return np.interp(pos, np.arange(n), x, left=0)


def tape(x, rng, amount=1.0, hiss=0.0, circular=False):
    y = wowflutter(x, rng, wow=0.0012 * amount, flutter=0.00025 * amount, circular=circular)
    pk = np.max(np.abs(y)) + 1e-12
    y = sat(y / pk, 1.0 + amount) * pk
    if hiss > 0:
        h = band_noise(len(y), rng, 2000, 9000) * hiss * pk
        y = y + (h if y.ndim == 1 else np.stack([h, np.roll(h, 777)], 1))
    return y


def vhs(x, rng, bits=9, factor=2.0, drive=1.8, lp_hz=6200, hiss=0.03, wow=0.0012, mids=3.0):
    """Puppet Combo tape look for one-shots (iteration 2): wobbly tape, cheap-speaker mid push, saturation,
    crushed bits + rate reduction (aliasing), dull top and a hiss bed under everything."""
    y = norm(x)
    y = wowflutter(y, rng, wow=wow, flutter=0.0005)
    y = hp(y, 110)
    y = bq(y, "peak", 1300, 0.8, mids)
    y = lofi(y, bits, factor, drive, lp_hz)
    y = norm(y)
    n = len(y)
    if hiss > 0:
        bed = norm(band_noise(n, rng, 1500, 8500)) * hiss
        bed *= 0.85 + 0.15 * smooth_rand(n, rng, 3.0, circular=False)
        # the hiss rides on the sound (like a recorded clip) instead of hanging on after it
        follow = np.clip(lp(np.abs(y), 25.0) * 6.0, 0.0, 1.0)
        y = y + bed * (0.2 + 0.8 * follow)
    return y


def am(x, rate, depth, n=None, shape="sine", ph0=0.0):
    n = len(x)
    if shape == "sine":
        m = 0.5 * (1 + np.sin(TAU * phase(rate, n, ph0)))
    else:
        m = (phase(rate, n, ph0) % 1.0 < 0.5).astype(float)
    g = 1 - depth + depth * m
    return x * (g if x.ndim == 1 else g[:, None])


# --------------------------------------------------------------------------------------------- spectral
def stft(x, nfft=1024, hop=256):
    w = np.hanning(nfft)
    n = len(x)
    pad_ = np.concatenate([np.zeros(nfft), x, np.zeros(nfft)])
    frames = 1 + (len(pad_) - nfft) // hop
    idx = np.arange(nfft)[None, :] + hop * np.arange(frames)[:, None]
    return np.fft.rfft(pad_[idx] * w, axis=1), n


def istft(S, n, nfft=1024, hop=256):
    w = np.hanning(nfft)
    frames = S.shape[0]
    out = np.zeros(nfft + hop * (frames - 1))
    wsum = np.zeros_like(out)
    y = np.fft.irfft(S, nfft, axis=1) * w
    for i in range(frames):
        out[i * hop:i * hop + nfft] += y[i]
        wsum[i * hop:i * hop + nfft] += w * w
    out /= np.maximum(wsum, 1e-3)
    return out[nfft:nfft + n]


def whisperize(x, rng, smooth=6, nfft=1024, hop=256, tilt=0.0):
    """Replace the excitation of a voice by noise, keeping its spectral envelope (-> whisper)."""
    S, n = stft(x, nfft, hop)
    mag = np.abs(S)
    k = np.ones(smooth) / smooth
    mag = np.apply_along_axis(lambda r: np.convolve(r, k, mode="same"), 1, mag)
    if tilt:
        f = np.fft.rfftfreq(nfft, 1 / SR)
        mag *= (np.maximum(f, 50) / 1000.0) ** (tilt / 6.02)
    ph = rng.uniform(0, TAU, S.shape)
    return istft(mag * np.exp(1j * ph), n, nfft, hop)


def pv_stretch(x, factor, nfft=2048, hop=512):
    """Phase-vocoder time stretch (factor>1 = longer), pitch preserved."""
    S, n = stft(x, nfft, hop)
    frames = S.shape[0]
    tpos = np.arange(0, frames - 1, 1.0 / factor)
    out = np.zeros((len(tpos), S.shape[1]), complex)
    omega = TAU * hop * np.arange(S.shape[1]) / nfft
    ph = np.angle(S[0])
    for i, tp in enumerate(tpos):
        a = int(tp)
        fr = tp - a
        m = (1 - fr) * np.abs(S[a]) + fr * np.abs(S[a + 1])
        out[i] = m * np.exp(1j * ph)
        dp = np.angle(S[a + 1]) - np.angle(S[a]) - omega
        dp -= TAU * np.round(dp / TAU)
        ph = ph + omega + dp
    return istft(out, int(n * factor), nfft, hop)


def granular(x, rng, out_n, grain=0.08, density=40.0, pos=None, pitch=0.0, jitter=0.02, reverse_prob=0.0,
              circular=True):
    """Granular cloud from x. pos: per-output-sample read position (0..1) or None for random.
    circular=True folds grains that run past the end back to the start (loop safe)."""
    out = np.zeros(out_n + N(grain * 4))
    count = int(density * out_n / SR)
    for _ in range(count):
        t0 = rng.integers(0, out_n)
        g = N(grain * rng.uniform(0.6, 1.4))
        p = pos[t0] if pos is not None else rng.uniform()
        p = np.clip(p + rng.normal(0, jitter), 0, 1)
        r = semis(pitch + rng.normal(0, 0.15))
        src_len = int(g * r) + 2
        s0 = int(p * max(1, len(x) - src_len - 1))
        seg = x[s0:s0 + src_len]
        if len(seg) < 4:
            continue
        seg = np.interp(np.arange(g) * r, np.arange(len(seg)), seg)
        if rng.uniform() < reverse_prob:
            seg = seg[::-1]
        seg = seg * np.hanning(len(seg))
        place(out, seg, t0, rng.uniform(0.5, 1.0))
    if circular:
        extra = len(out) - out_n
        out[:extra] += out[out_n:]
    return out[:out_n]


# --------------------------------------------------------------------------------------------- space
def reverb_ir(rt60, rng, stereo=False, predelay=0.008, bright=7000.0, dark=1200.0, early=6, size=1.0):
    L = N(rt60 * 1.15 + predelay + 0.05)
    t = T(L)
    ch = 2 if stereo else 1
    irs = []
    for c in range(ch):
        nz = rng.standard_normal(L)
        a = lp(nz, bright)
        b = lp(nz, dark)
        k = np.clip(t / max(rt60, 1e-3), 0, 1) ** 0.7
        ir = (a * (1 - k) + b * k * 1.3) * np.exp(-6.91 * t / rt60)
        fade_in = np.clip((t - predelay) / 0.012, 0, 1)
        ir *= fade_in
        for e in range(early):
            d = predelay + rng.uniform(0.002, 0.045) * size
            place(ir, np.array([rng.uniform(0.4, 1.0) * rng.choice([-1, 1]) * 3.0]), N(d))
        irs.append(ir / np.sqrt(np.sum(ir ** 2)))
    return np.stack(irs, 1) if stereo else irs[0]


def reverb(x, rng, rt60=0.8, wet=0.3, dry=1.0, stereo=False, predelay=0.008, bright=7000.0, dark=1200.0,
           early=6, size=1.0, tail=True, circular=False):
    """Convolution reverb with a synthetic IR. Mono in -> mono or stereo out."""
    ir = reverb_ir(rt60, rng, stereo, predelay, bright, dark, early, size)
    src = x if x.ndim == 1 else x.mean(1)
    n = len(src)
    if circular:
        L = len(ir)
        if L > n:  # fold IR onto the loop length
            reps = int(np.ceil(L / n))
            ir = pad(ir, reps * n)
            ir = ir.reshape((reps, n) + ir.shape[1:]).sum(0)
        Xf = np.fft.rfft(src, n)
        Hf = np.fft.rfft(ir, n, axis=0)
        w = np.fft.irfft(Xf[:, None] * Hf, n, axis=0) if stereo else np.fft.irfft(Xf * Hf, n)
        dryp = x if (x.ndim == 2 or not stereo) else np.stack([x, x], 1)
        return dry * dryp + wet * w
    w = signal.fftconvolve(src[:, None], ir if stereo else ir[:, None], axes=0)
    if not stereo:
        w = w[:, 0]
    if not tail:
        w = w[:n]
    out = pad(x if (x.ndim == 2 or not stereo) else np.stack([x, x], 1), len(w)) * dry + wet * w
    return out


# --------------------------------------------------------------------------------------------- stereo
def pan(x, p):
    """Equal power pan, p in [-1, 1]."""
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], 1)


def stereo(x):
    return x if x.ndim == 2 else np.stack([x, x], 1)


def widen(x, rng, amount=0.5, delay_ms=11.0):
    """Mono -> stereo by adding a decorrelated (delayed, filtered) side signal."""
    if x.ndim == 2:
        x = x.mean(1)
    d = N(delay_ms / 1000.0)
    side = np.concatenate([np.zeros(d), x[:-d] if d else x])
    side = bq(side, "peak", rng.uniform(800, 2500), 0.7, 4.0) - x * 0.0
    side = hp(side, 250)
    return np.stack([x + amount * side, x - amount * side], 1) / (1 + 0.5 * amount)


def circ_widen(x, amount=0.5, delay_ms=11.0):
    d = N(delay_ms / 1000.0)
    side = circ_filter(np.roll(x, d), _sos("highpass", 250, 2))
    return np.stack([x + amount * side, x - amount * side], 1) / (1 + 0.5 * amount)


# --------------------------------------------------------------------------------------------- loudness
def _kweight_sos(fs=SR):
    f0, G, Q = 1681.974450955533, 3.999843853973347, 0.7071752369554196
    K = np.tan(np.pi * f0 / fs)
    Vh = 10 ** (G / 20)
    Vb = Vh ** 0.4996667741545416
    a0 = 1 + K / Q + K * K
    s1 = [(Vh + Vb * K / Q + K * K) / a0, 2 * (K * K - Vh) / a0, (Vh - Vb * K / Q + K * K) / a0,
          1, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0]
    f0, Q = 38.13547087602444, 0.5003270373238773
    K = np.tan(np.pi * f0 / fs)
    a0 = 1 + K / Q + K * K
    s2 = [1, -2, 1, 1, 2 * (K * K - 1) / a0, (1 - K / Q + K * K) / a0]
    return np.array([s1, s2])


def lufs(x, fs=SR):
    """ITU-R BS.1770-4 integrated loudness (gated)."""
    x = x if x.ndim == 2 else x[:, None]
    y = signal.sosfilt(_kweight_sos(fs), x, axis=0)
    blk, hop = int(0.4 * fs), int(0.1 * fs)
    if len(y) < blk:
        z = np.mean(y ** 2, 0).sum()
        return -0.691 + 10 * np.log10(z + 1e-12)
    starts = np.arange(0, len(y) - blk + 1, hop)
    cs = np.concatenate([np.zeros((1, y.shape[1])), np.cumsum(y ** 2, 0)])
    z = ((cs[starts + blk] - cs[starts]) / blk).sum(1)
    l = -0.691 + 10 * np.log10(z + 1e-12)
    z = z[l > -70]
    if not len(z):
        return -70.0
    rel = -0.691 + 10 * np.log10(z.mean()) - 10
    z2 = z[(-0.691 + 10 * np.log10(z)) > rel]
    return -0.691 + 10 * np.log10(z2.mean() if len(z2) else z.mean())


def limiter(x, ceiling_db=-1.0, release=0.08, lookahead=0.003):
    """Simple look-ahead peak limiter (non circular)."""
    c = undb(ceiling_db)
    mono = np.abs(x) if x.ndim == 1 else np.abs(x).max(1)
    la = N(lookahead)
    need = np.maximum(mono / c, 1.0)
    need = np.concatenate([need[la:], np.ones(la)])
    # running max over look-ahead window then smooth release
    from scipy.ndimage import maximum_filter1d
    need = maximum_filter1d(need, la * 2 + 1)
    g = 1.0 / need
    a = np.exp(-1.0 / (release * SR))
    g = signal.lfilter([1 - a], [1, -a], g - 1.0) + 1.0
    g = np.minimum(g, 1.0 / need)
    y = x * (g if x.ndim == 1 else g[:, None])
    return np.clip(y, -c, c)


def compress(x, thresh_db=-18.0, ratio=4.0, attack=0.003, release=0.12, makeup=True):
    mono = np.abs(x) if x.ndim == 1 else np.abs(x).max(1)
    a_a = np.exp(-1.0 / (attack * SR))
    a_r = np.exp(-1.0 / (release * SR))
    # envelope follower (vectorised approximation: smoothed peak)
    e = signal.lfilter([1 - a_r], [1, -a_r], mono)
    e = np.maximum(e, signal.lfilter([1 - a_a], [1, -a_a], mono))
    ed = db(e)
    gr = np.minimum(0, (thresh_db - ed) * (1 - 1 / ratio))
    g = undb(gr)
    y = x * (g if x.ndim == 1 else g[:, None])
    if makeup:
        y = norm(y, np.max(np.abs(x)))
    return y
