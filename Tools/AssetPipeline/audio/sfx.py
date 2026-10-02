"""Higher level sound building blocks (impacts, creaks, cloth, whooshes, liquids, breath, voices...)."""
import numpy as np

import dsp
from dsp import N, T, SR, TAU


# --------------------------------------------------------------------------------------------- impacts
def thud(n, rng, f=70.0, tau=0.08, drop=0.6, drop_tau=0.02, noise=0.35, noise_lp=500.0, attack=0.0015):
    """Low body thump: sine with a falling pitch + low passed noise body."""
    t = T(n)
    fr = f * (1 + drop * np.exp(-t / drop_tau))
    s = dsp.sine(fr, n) * dsp.perc(n, attack, tau)
    nz = dsp.lp(rng.standard_normal(n) * dsp.perc(n, 0.0008, tau * 0.35), noise_lp)
    nz = dsp.norm(nz) * noise
    return s + nz


def wood_modes(rng, f0=110.0, count=9, tau=0.07, spread=0.08):
    ratios = [1.0, 1.52, 2.3, 3.2, 4.4, 6.1, 8.3, 11.5, 15.7, 21.0, 27.0]
    modes = []
    for k in range(min(count, len(ratios))):
        f = f0 * ratios[k] * (1 + rng.uniform(-spread, spread))
        modes.append((f, tau / (1 + 0.45 * k) * rng.uniform(0.7, 1.3), rng.uniform(0.5, 1.0) / (1 + 0.25 * k)))
    return modes


def strike(n, rng, modes, hardness=0.5, exc_tau=None, noise_mix=0.15, noise_lp=None):
    """Modal strike: a short noise burst through a modal impulse response."""
    tau = exc_tau if exc_tau else 0.0004 + (1 - hardness) * 0.004
    exc = dsp.burst(N(0.03), rng, 0.0002, tau, lp_hz=1500 + 12000 * hardness)
    ir = dsp.modal_ir(n, modes, rng)
    y = dsp.excite(exc, ir, n)
    y = dsp.norm(y)
    if noise_mix:
        nz = dsp.pad(exc, n)
        if noise_lp:
            nz = dsp.lp(nz, noise_lp)
        y = y + noise_mix * dsp.norm(nz)
    return y


def metal_hit(n, rng, f0=300.0, count=14, tau=(0.2, 0.9), hardness=0.8, noise_mix=0.2, spread=1.0):
    return strike(n, rng, dsp.metal_modes(f0, count, rng, spread=spread, tau=tau), hardness, noise_mix=noise_mix)


def clank(n, rng, f0=180.0, tau=(0.15, 0.6), hardness=0.7):
    """Heavy dull metal clank (door / trap): low inharmonic modes + thud + click."""
    y = metal_hit(n, rng, f0, 16, tau, hardness, 0.25)
    y += 0.6 * thud(n, rng, f0 * 0.45, 0.06, 0.5)
    return dsp.norm(y)


def shards(n, rng, count=80, start=0.0, spread=0.35, f_lo=2000, f_hi=9000, tau=(0.004, 0.03), decay=0.12,
           gain_db_spread=12.0):
    """Many small high-pitched particles (glass, gravel bits, debris)."""
    out = np.zeros(n)
    for _ in range(count):
        t0 = start + rng.exponential(spread * 0.35)
        if t0 > start + spread * 2.5:
            t0 = start + rng.uniform(0, spread)
        L = N(0.08)
        f = np.exp(rng.uniform(np.log(f_lo), np.log(f_hi)))
        modes = [(f, rng.uniform(*tau), 1.0), (f * rng.uniform(1.7, 2.9), rng.uniform(*tau) * 0.6, 0.5)]
        g = dsp.modal_ir(L, modes, rng)
        g[:N(0.0005)] += rng.standard_normal(N(0.0005)) * 0.6
        amp = dsp.undb(-rng.uniform(0, gain_db_spread)) * np.exp(-(t0 - start) / decay)
        dsp.place(out, g, N(t0), amp)
    return out


def grains(n, rng, rate_env, f_lo=1200, f_hi=7000, tau=(0.0008, 0.004)):
    """Granular crunch: micro clicks whose density follows rate_env (clicks/sec, per sample)."""
    out = np.zeros(n)
    p = np.clip(rate_env / SR, 0, 1)
    pos = np.nonzero(rng.uniform(size=n) < p)[0]
    L = N(0.02)
    for t0 in pos:
        f = np.exp(rng.uniform(np.log(f_lo), np.log(f_hi)))
        g = dsp.modal_ir(L, [(f, rng.uniform(*tau), 1.0), (f * 1.6, rng.uniform(*tau) * 0.5, 0.4)], rng)
        dsp.place(out, g, t0, rng.uniform(0.15, 1.0) ** 2)
    return out


# --------------------------------------------------------------------------------------------- friction
def creak(n, rng, rate, modes=None, amp_env=None, jitter=0.25, roughness=0.35, kind="wood", base=None):
    """Wood / metal creak via stick-slip. rate: slip rate curve (Hz)."""
    if modes is None:
        if kind == "wood":
            b = base or rng.uniform(500, 900)
            modes = [(b, 12, 1.0), (b * 1.83, 16, 0.7), (b * 2.71, 20, 0.45), (b * 0.52, 8, 0.6),
                     (b * 3.9, 25, 0.25)]
        else:  # rusty metal hinge squeal
            b = base or rng.uniform(900, 1600)
            modes = [(b, 40, 1.0), (b * 2.01, 50, 0.6), (b * 2.97, 60, 0.4), (b * 4.1, 70, 0.25),
                     (b * 0.5, 25, 0.3)]
    y = dsp.stick_slip(n, rng, rate, modes, jitter=jitter, amp_env=amp_env, roughness=roughness)
    return dsp.norm(y)


def scrape(n, rng, env_, lo=300, hi=5000, grit=800.0, res=None):
    """Rough scraping noise (with optional metallic resonances)."""
    nz = dsp.band_noise(n, rng, lo, hi)
    g = grains(n, rng, grit * env_, lo, hi * 1.3)
    y = dsp.norm(nz) * 0.5 + dsp.norm(g)
    if res:
        y = y * 0.3 + dsp.resonbank(y, [r[0] for r in res], [r[1] for r in res], [r[2] for r in res])
    return dsp.norm(y * env_)


# --------------------------------------------------------------------------------------------- cloth / paper
def cloth(n, rng, env_, lo=600, hi=5000, crinkle=200.0):
    nz = dsp.band_noise(n, rng, lo, hi)
    nz *= np.exp(0.8 * dsp.smooth_rand(n, rng, 25.0, circular=False))
    cr = grains(n, rng, crinkle * env_ / (env_.max() + 1e-9), 1500, 6000, (0.0005, 0.002))
    return dsp.norm(dsp.norm(nz) * env_ + 0.35 * cr)


def whoosh(n, rng, f0=400, f1=2500, peak=0.5, q=1.2, width=0.25):
    """Air movement: band-pass noise whose centre glides and peaks at `peak` (0..1 of n)."""
    t = np.linspace(0, 1, n)
    e = np.exp(-0.5 * ((t - peak) / width) ** 2)
    fc = f0 + (f1 - f0) * e
    y = dsp.tvf(rng.standard_normal(n), "bp", fc, q, block=32, stages=2)
    return dsp.norm(y * e)


# --------------------------------------------------------------------------------------------- liquids
def bubble(f0, rng, dur=None):
    tau = 12.0 / f0 if dur is None else dur
    n = N(tau * 6)
    t = T(n)
    f = f0 * (1 + 1.8 * t / (tau * 6))
    return dsp.sine(f, n, rng.uniform()) * np.exp(-t / tau) * (1 - np.exp(-t / 0.0006))


def liquid(n, rng, rate_env, f_lo=300, f_hi=1500, stream=0.3):
    """Pouring / glugging: random bubbles following rate_env + a hissy stream."""
    out = np.zeros(n)
    p = np.clip(rate_env / SR, 0, 1)
    for t0 in np.nonzero(rng.uniform(size=n) < p)[0]:
        f0 = np.exp(rng.uniform(np.log(f_lo), np.log(f_hi)))
        dsp.place(out, bubble(f0, rng), t0, rng.uniform(0.2, 1.0))
    st = dsp.band_noise(n, rng, 800, 5000) * (rate_env / (rate_env.max() + 1e-9))
    st *= np.exp(0.6 * dsp.smooth_rand(n, rng, 15, circular=False))
    return dsp.norm(dsp.norm(out) + stream * dsp.norm(st))


def drip(rng, f0=1400.0, tail=0.6, room=None):
    """Single water drip: impact tick + rising bubble 'plink'."""
    b = bubble(f0, rng, 0.012 + rng.uniform(0, 0.01))
    n = len(b) + N(0.05)
    y = dsp.pad(b, n)
    y[:N(0.002)] += 0.4 * rng.standard_normal(N(0.002)) * np.linspace(1, 0, N(0.002))
    return y


# --------------------------------------------------------------------------------------------- body / voice
VOWELS = {  # F1, F2, F3, F4 (Hz) for a male-ish voice
    "a": (730, 1090, 2440, 3400), "e": (530, 1840, 2480, 3500), "i": (300, 2200, 3000, 3700),
    "o": (500, 850, 2400, 3300), "u": (325, 700, 2530, 3300), "uh": (640, 1190, 2390, 3300),
    "h": (600, 1300, 2500, 3600), "ae": (660, 1720, 2410, 3400),
}


def formant_filter(x, vowel_curve, bw=(80, 100, 140, 200), gains=(1.0, 0.6, 0.35, 0.2), block=64):
    """Time varying parallel formant bank. vowel_curve: array (n, 4) of formant freqs."""
    y = np.zeros_like(x)
    for k in range(4):
        f = vowel_curve[:, k]
        q = f / bw[k]
        y += gains[k] * dsp.tvf(x, "bp", f, q, block=block, stages=1)
    return y


def vowel_track(n, seq, rng=None, jitter=0.03):
    """seq: [(time_s, vowel)] -> (n, 4) formant curve (linear interpolation between targets)."""
    ts = [s[0] for s in seq]
    out = np.zeros((n, 4))
    for k in range(4):
        vals = [VOWELS[s[1]][k] for s in seq]
        if rng is not None:
            vals = [v * (1 + rng.uniform(-jitter, jitter)) for v in vals]
        out[:, k] = np.interp(T(n), ts, vals)
    return out


def glottal(f0, n, rng, jitter=0.01, shimmer=0.05, open_q=0.6):
    """Band-limited-ish glottal pulse train (rounded saw + jitter)."""
    f = np.broadcast_to(np.asarray(f0, float), (n,)) * (1 + jitter * dsp.smooth_rand(n, rng, 40, circular=False))
    s = dsp.saw(f, n)
    s = dsp.lp(s, 3500)
    s *= 1 + shimmer * dsp.smooth_rand(n, rng, 30, circular=False)
    return s


def breath(n, rng, segs, voice=0.0, f0=110.0, sack=False):
    """Breathing. segs: [(start, dur, 'in'|'out', intensity)]."""
    out = np.zeros(n)
    for (st, du, kind, amp) in segs:
        m = N(du)
        nz = rng.standard_normal(m)
        if kind == "in":
            v = vowel_track(m, [(0, "h"), (du * 0.5, "a"), (du, "uh")], rng, 0.08)
            v[:, 1] *= np.linspace(1.0, 1.25, m)[:]
            e = dsp.env([(0, 0), (du * 0.25, 1), (du * 0.75, 0.85), (du, 0)], m) ** 1.3
            src = nz
            y = formant_filter(src, v, bw=(250, 300, 400, 500), gains=(0.6, 0.9, 0.8, 0.5))
            y = dsp.hp(y, 700) * 1.0
        else:
            v = vowel_track(m, [(0, "uh"), (du * 0.6, "h"), (du, "uh")], rng, 0.08)
            e = dsp.env([(0, 0), (du * 0.12, 1), (du * 0.5, 0.7), (du, 0)], m) ** 1.6
            y = formant_filter(nz, v, bw=(200, 260, 350, 450), gains=(1.0, 0.8, 0.5, 0.3))
            y = dsp.hp(y, 300)
            if voice > 0:
                g = glottal(f0 * np.linspace(1.05, 0.9, m), m, rng, 0.04, 0.3)
                vy = formant_filter(g, v, gains=(1.0, 0.5, 0.25, 0.1))
                y = dsp.norm(y) + voice * dsp.norm(vy) * np.clip(dsp.smooth_rand(m, rng, 6, False) * 0.5 + 0.8, 0, 1.4)
        y = dsp.norm(y) * e * amp
        y *= np.exp(0.35 * dsp.smooth_rand(m, rng, 18, circular=False))
        dsp.place(out, y, N(st))
    if sack:
        out = dsp.lp(out, 1600, 2)
        out = dsp.peq(out, 420, 1.2, 5)
    return out


def scream_fragment(name, start, dur, ratio=1.0, from_src=None):
    """`dur` seconds (output time) of a user scream starting at `start`, re-pitched by `ratio` (varispeed)."""
    import common
    x = common.src_segment(name, start, dur * ratio + 0.02) if from_src is None else from_src
    if ratio != 1.0:
        x = dsp.resample(x, ratio)
    return dsp.pad(x, N(dur))


def radio(x, rng, lo=350.0, hi=2800.0, drive=3.0, noise=0.12, crackle_rate=12.0):
    """Cheap walkie / shortwave radio colouring."""
    y = dsp.bp(x, lo, hi, 3)
    y = dsp.sat(dsp.norm(y), drive)
    n = len(y)
    st = dsp.band_noise(n, rng, 500, 4500) * noise
    cr = dsp.crackle(n, rng, crackle_rate)
    cr = dsp.hp(cr, 800) * 0.6
    return dsp.bp(y + st + cr, lo * 0.8, hi * 1.2, 2)


def heartbeat(rng, f=52.0, strength=1.0):
    """Single realistic heartbeat 'lub-dub' (muffled, chest-resonant; saturation adds audible harmonics)."""
    n = N(0.75)
    lub = thud(n, rng, f, 0.075, 0.8, 0.012, 0.4, 220)
    dub = thud(n, rng, f * 1.28, 0.055, 0.6, 0.01, 0.3, 260)
    y = lub + 0.65 * np.roll(dub, N(0.24 + rng.uniform(-0.01, 0.01)))
    y = dsp.lp(y, 200, 2)
    y = dsp.sat(dsp.norm(y), 2.6)
    y = dsp.peq(dsp.lp(y, 420, 2), 110, 1.0, 4) * strength
    return y
