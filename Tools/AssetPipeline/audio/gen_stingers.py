"""Stingers: jumpscares, capture, death, endings, static bursts, VHS glitches, drone hits. Audio/Stingers/* (stereo, LOUD)."""
import numpy as np

import common
import dsp
import sfx
from common import sound
from dsp import N, T, SR, TAU

LOUD = dict(ch=2, norm=("loud", -0.5), q=5)
SOFT = dict(ch=2, norm=("lufs", -22.0, -6.0), q=5)  # static bursts: short, quiet hits of tape noise
MUSIC = dict(ch=2, norm=("peak", -1.0), q=5)


# --------------------------------------------------------------------------------------------- layers
def saw_stack(freqs, n, rng, detune=0.004, pans=None):
    """Detuned saw voices -> stereo (n, 2)."""
    out = np.zeros((n, 2))
    for i, f in enumerate(freqs):
        for d in (-detune, 0.0, detune):
            v = dsp.saw(np.asarray(f) * (1 + d + 0.002 * rng.standard_normal()), n, rng.uniform())
            p = pans[i] if pans is not None else rng.uniform(-0.7, 0.7)
            out += dsp.pan(v, np.clip(p + 2 * d * 50, -1, 1))
    return out / (len(freqs) * 3)


def bowed(n, rng, f, body=(480, 1150, 2700, 4100), roughness=0.5, jitter=0.03):
    """Harsh bowed string / violin scrape: stick-slip at the string frequency through a body."""
    modes = [(b, 6 + 4 * k, 1.0 / (1 + 0.3 * k)) for k, b in enumerate(body)]
    y = dsp.stick_slip(n, rng, f, modes, jitter=jitter, roughness=roughness, grain_tau=0.0002)
    y += 0.5 * dsp.saw(f, n, rng.uniform())
    return y


def scream_layer(name, start, dur, ratio=1.0, drive=3.0, hp_hz=300):
    x = sfx.scream_fragment(name, start, dur, ratio)
    x = dsp.hp(x, hp_hz)
    return dsp.sat(dsp.norm(x) * drive, 2.0)


def boom(n, rng, f=38.0, tau=0.7, drop=1.5):
    t = T(n)
    return dsp.sine(f * (1 + drop * np.exp(-t / 0.05)), n) * dsp.perc(n, 0.003, tau)


def noise_burst(n, rng, tau=0.04):
    return rng.standard_normal(n) * dsp.perc(n, 0.0005, tau)


def wide(x, rng, rt=1.5, wet=0.4, bright=5000, dark=1000, predelay=0.01):
    """Mono or stereo -> stereo with a wide reverb."""
    mono = x if x.ndim == 1 else x.mean(1)
    w = dsp.reverb(mono, rng, rt, 1.0, dry=0.0, stereo=True, bright=bright, dark=dark, predelay=predelay)
    dry = dsp.stereo(x)
    return dsp.pad(dry, len(w)) + wet * w


def crush_st(x, bits=10, factor=1.6, drive=1.4):
    return np.stack([dsp.lofi(x[:, c], bits, factor, drive, 10000) for c in range(2)], 1)


def static_noise(n, rng, harsh=0.5):
    nz = rng.standard_normal(n)
    nz = dsp.bp(nz, 200, 7000 + 3000 * harsh)
    cr = dsp.hp(dsp.crackle(n, rng, 200 + 600 * harsh), 1000)
    hum = dsp.sat(sum(np.sin(TAU * dsp.phase(60.0 * k, n)) / k for k in range(1, 9)), 3)
    y = dsp.norm(nz) + 0.6 * cr + (0.15 + 0.2 * harsh) * dsp.norm(hum)
    y = dsp.hold(dsp.crush(dsp.norm(y), int(8 - 3 * harsh)), 2 + 2 * harsh)
    return y


def st_static(n, rng, harsh=0.5):
    return np.stack([static_noise(n, rng, harsh), static_noise(n, rng, harsh)], 1)


# --------------------------------------------------------------------------------------------- stabs
@sound("Stingers/sting_spotted", desc="sharp screech / violin-scrape stab when Omar spots you", **LOUD)
def sting_spotted(rng):
    n = N(2.4)
    t = T(n)
    out = np.zeros((n, 2))
    targets = [1108.7, 1174.7, 1244.5, 1318.5, 1661.2, 1760.0]
    for i, f in enumerate(targets):
        bend = f * dsp.semis(-2.5 * np.exp(-t / 0.05)) * (1 + 0.006 * np.sin(TAU * dsp.phase(rng.uniform(6, 9), n)))
        v = bowed(n, rng, bend, roughness=0.6)
        out += dsp.pan(dsp.norm(v), -0.8 + 1.6 * i / (len(targets) - 1))
    env_ = dsp.env([(0, 0), (0.006, 1), (0.35, 0.8), (1.0, 0.35), (2.4, 0)], n)
    out *= env_[:, None]
    hit = boom(n, rng, 44, 0.5) * 1.2 + dsp.lp(noise_burst(n, rng, 0.05), 3000)
    out += 0.6 * dsp.stereo(hit)
    sc = scream_layer("scream_2", 0.05, 0.9, 1.12, 3.5, 500)
    sc *= dsp.env([(0, 0), (0.01, 1), (0.9, 0)], len(sc))
    out = dsp.pad(out, n)
    out[:len(sc)] += 0.45 * dsp.pan(sc, 0.15)[:n]
    out = wide(out, rng, 1.6, 0.45)
    return crush_st(dsp.sat(out / np.max(np.abs(out)) * 1.5, 2.5), 10)


def orchestral_hit(n, rng, root=65.41):
    """Dissonant stacked brass/string cluster + inharmonic metal partials."""
    freqs = [root, root * dsp.semis(13), root * dsp.semis(18), root * dsp.semis(31), root * dsp.semis(32),
             root * dsp.semis(37)]
    c = saw_stack(freqs, n, rng, 0.006)
    cut = np.interp(T(n), [0, 0.05, 0.6, 2.5], [6000, 4000, 1200, 500])
    c = np.stack([dsp.tvf(c[:, k], "lp", cut, 1.0, block=64) for k in range(2)], 1)
    c *= dsp.env([(0, 0), (0.004, 1), (0.25, 0.75), (2.5, 0)], n)[:, None]
    m = sfx.metal_hit(n, rng, root * 1.5, 18, (0.4, 1.6), 0.9, 0.3)
    return dsp.norm(c) + 0.5 * dsp.stereo(dsp.norm(m))


@sound("Stingers/sting_jumpscare", desc="extremely loud dissonant hit + distorted scream screech + sub boom", **LOUD)
def sting_jumpscare(rng):
    n = N(2.8)
    out = orchestral_hit(n, rng, 61.7)
    out += 0.9 * dsp.stereo(boom(n, rng, 36, 0.8, 2.0))
    out += 0.8 * np.stack([noise_burst(n, rng, 0.06), noise_burst(n, rng, 0.06)], 1)
    a = scream_layer("scream_1", 0.1, 1.5, 1.06, 5.0, 450)
    b = scream_layer("scream_3", 0.3, 1.5, 0.94, 5.0, 450)
    sc = dsp.pan(dsp.fold(a, 1.4), -0.5) + dsp.pan(dsp.fold(b, 1.4), 0.5)
    sc *= dsp.env([(0, 0), (0.008, 1), (0.6, 0.7), (1.5, 0)], len(sc))[:, None]
    out[:len(sc)] += 1.0 * sc[:n]
    st = st_static(N(0.35), rng, 0.9) * dsp.env([(0, 1), (0.35, 0)], N(0.35))[:, None]
    out[:len(st)] += 0.35 * st
    out = wide(out, rng, 1.8, 0.35)
    out *= dsp.env([(0, 1.0), (0.25, 1.0), (1.2, 0.55), (4.0, 0.4)], len(out))[:, None]  # hit, then let go
    out = dsp.compress(out / np.max(np.abs(out)), -14, 2.5, 0.001, 0.2)
    return crush_st(dsp.sat(out * 1.6, 3.0), 9)


@sound("Stingers/sting_capture", desc="brutal hit + scream + static when Omar catches a prisoner", **LOUD)
def sting_capture(rng):
    n = N(4.2)
    out = orchestral_hit(n, rng, 55.0) * 0.8
    out += 1.2 * dsp.stereo(boom(n, rng, 33, 1.0, 1.8))
    from gen_omar import flesh_hit
    fh = flesh_hit(rng, 0.8, N(0.8))
    out[:len(fh)] += 0.8 * dsp.stereo(dsp.norm(fh))
    sc = scream_layer("screams_long", 1.0, 2.6, 0.9, 4.0, 350)
    sc = dsp.varispeed(sc, np.interp(np.arange(len(sc)), [0, len(sc) * 0.6, len(sc)], [1.0, 1.0, 0.55]))
    sc *= dsp.env([(0, 0), (0.02, 1), (1.6, 0.8), (len(sc) / SR, 0)], len(sc))
    p0 = N(0.05)
    out[p0:p0 + len(sc)] += 0.6 * dsp.pan(sc, 0.0)[:n - p0]
    stn = st_static(n, rng, 0.8) * dsp.env([(0, 0.8), (0.3, 0.2), (2.5, 0.35), (3.6, 0.9), (4.0, 0.9), (4.2, 0)], n)[:, None]
    out += 0.3 * stn
    out = wide(out, rng, 2.0, 0.35)
    out = dsp.compress(out / np.max(np.abs(out)), -20, 4.0, 0.001, 0.25)
    return crush_st(dsp.sat(out * 1.5, 2.8), 9)


@sound("Stingers/sting_death", desc="long distorted descending tone collapsing into static", **LOUD)
def sting_death(rng):
    n = N(7.5)
    t = T(n)
    f = 220.0 * np.exp(-np.clip(t, 0, 5.5) / 1.9)
    voices = np.zeros((n, 2))
    for i, r in enumerate((1.0, 1.0595, 1.4142, 0.5, 2.03)):
        v = dsp.saw(f * r * (1 + 0.004 * dsp.smooth_rand(n, rng, 3, False)), n, rng.uniform())
        voices += dsp.pan(v, -0.6 + 0.3 * i)
    drive = np.interp(t, [0, 5.5], [2, 9])
    voices = np.tanh(voices * drive[:, None]) / 1.0
    cut = np.interp(t, [0, 1, 5.5], [5000, 3000, 400])
    voices = np.stack([dsp.tvf(voices[:, k], "lp", cut, 1.5, block=64) for k in range(2)], 1)
    voices *= dsp.env([(0, 0), (0.02, 1), (4.5, 0.8), (5.6, 0.3), (6.2, 0)], n)[:, None]
    voices += 0.8 * dsp.stereo(boom(n, rng, 40, 1.5, 1.2))
    sc = scream_layer("scream_4", 0.0, 1.6, 0.8, 3.0, 300)
    sc = dsp.varispeed(sc, np.linspace(1.0, 0.45, len(sc)))
    voices[:len(sc)] += 0.35 * dsp.pan(sc, 0.0) * dsp.env([(0, 1), (len(sc) / SR, 0)], len(sc))[:, None]
    stn = st_static(n, rng, 0.9) * dsp.env([(0, 0), (3.5, 0), (5.2, 0.9), (7.2, 0.9), (7.5, 0)], n)[:, None]
    out = voices / np.max(np.abs(voices)) + 0.55 * stn
    out = wide(out, rng, 2.2, 0.3)
    bits = 9
    return crush_st(dsp.sat(out / np.max(np.abs(out)) * 1.3, 2.0), bits)


# --------------------------------------------------------------------------------------------- anomaly / escape
def glass_tones(n, rng, base=1480.0, count=5):
    out = np.zeros((n, 2))
    for i in range(count):
        f = base * dsp.semis(rng.choice([0, 1, 6, 7, 11, 13])) * (1 + 0.004 * rng.standard_normal())
        v = dsp.sine(f, n, rng.uniform()) + 0.3 * dsp.sine(f * 2.76, n, rng.uniform())
        out += dsp.pan(v * (0.6 + 0.4 * np.sin(TAU * dsp.phase(rng.uniform(0.3, 1.5), n))), rng.uniform(-0.8, 0.8))
    return out / count


def reverse_swell(x, rng, rt=2.5):
    w = dsp.reverb(x, rng, rt, 1.0, dry=0.2, bright=3500, dark=800)
    w = w[::-1]
    return w * np.linspace(0, 1, len(w)) ** 1.5


@sound("Stingers/sting_anomaly_1", desc="reversed scream swell into warbling glassy dissonance", **MUSIC)
def sting_anomaly_1(rng):
    sw = reverse_swell(dsp.lp(dsp.resample(common.src("scream_4"), 0.6), 2500), rng, 2.4)
    L = len(sw)
    n = L + N(2.0)
    out = np.zeros((n, 2))
    out[:L] += 0.8 * dsp.pan(dsp.norm(sw), -0.2)
    g = glass_tones(N(2.5), rng, 1480.0) * dsp.env([(0, 0), (0.02, 1), (2.5, 0)], N(2.5))[:, None]
    out[L - N(0.05):L - N(0.05) + len(g)] += 0.6 * g[:n - L + N(0.05)]
    out += 0.4 * dsp.stereo(dsp.pad(boom(N(1.5), rng, 50, 0.6, 0.5), n)) * 0
    out = np.stack([dsp.wowflutter(out[:, c], rng, 0.006, 0.9, 0.0008, 6.0) for c in range(2)], 1)
    out = wide(out, rng, 2.0, 0.4)
    return crush_st(out / np.max(np.abs(out)), 10, 1.6, 1.3)


@sound("Stingers/sting_anomaly_2", desc="tape slowdown drone, frozen scream grains, whisper stutters", **MUSIC)
def sting_anomaly_2(rng):
    n = N(4.0)
    t = T(n)
    speed = np.interp(t, [0, 1.2, 2.2, 3.0, 4.0], [1.0, 0.6, 0.35, 0.8, 0.5])
    pad = np.zeros(n)
    for f in (146.8, 155.6, 207.7, 311.1):
        pad += dsp.tri(f * speed * (1 + 0.003 * rng.standard_normal()), n, rng.uniform())
    gr = dsp.granular(common.src("screams_long"), rng, n, grain=0.12, density=45, pitch=-9, jitter=0.4,
                      reverse_prob=0.5, circular=False)
    gr = dsp.lp(gr, 2500)
    st_ = np.zeros(n)
    w = dsp.whisperize(common.src_segment("scream_3", 0.4, 0.5), rng, 8)
    for k in range(6):
        s = w[:N(0.07)] * np.hanning(N(0.07))
        dsp.place(st_, s, N(1.5 + 0.075 * k), 0.8 - 0.1 * k)
    mono = 0.5 * dsp.norm(pad) + 0.4 * dsp.norm(gr) + 0.4 * dsp.norm(st_)
    mono *= dsp.env([(0, 0), (0.4, 1), (3.4, 0.9), (4.0, 0)], n)
    mono = dsp.wowflutter(mono, rng, 0.008, 0.5, 0.001, 7.0)
    out = wide(mono, rng, 2.4, 0.6, 4000, 900)
    return crush_st(out / np.max(np.abs(out)), 9, 1.8, 1.4)


@sound("Stingers/sting_escape", desc="swelling open chord: uneasy triumph of escaping", **MUSIC)
def sting_escape(rng):
    n = N(6.5)
    t = T(n)
    chord = [73.42, 110.0, 146.83, 220.0, 329.63, 440.0, 659.26]
    pad = saw_stack(chord, n, rng, 0.005)
    cut = np.interp(t, [0, 2.5, 4.0, 6.5], [300, 2400, 3000, 900])
    pad = np.stack([dsp.tvf(pad[:, k], "lp", cut, 0.9, block=128) for k in range(2)], 1)
    pad *= dsp.env([(0, 0), (2.4, 1), (4.5, 0.9), (6.5, 0)], n)[:, None]
    shadow = saw_stack([466.16 * 0.5, 698.46 * 0.5], n, rng, 0.01) * 0.25  # Bb / F a semitone off - unease
    shadow = np.stack([dsp.lp(shadow[:, k], 1200) for k in range(2)], 1) * dsp.env([(0, 0), (3, 0.6), (6.5, 0)], n)[:, None]
    hit = boom(n, rng, 46, 1.2, 0.8)
    out = dsp.norm(pad) + shadow + 0.5 * dsp.stereo(np.roll(hit, N(2.4)))
    wind = np.stack([dsp.lp(dsp.pink(n, rng), 900), dsp.lp(dsp.pink(n, rng), 900)], 1) * 0.04
    out = out + wind
    out = np.stack([dsp.wowflutter(out[:, c], rng, 0.0025, 0.5, 0.0004, 6) for c in range(2)], 1)
    out = wide(out, rng, 2.5, 0.35)
    return crush_st(out / np.max(np.abs(out)), 11, 1.4, 1.3)


# --------------------------------------------------------------------------------------------- endings
@sound("Stingers/sting_ending_bad", desc="dark cluster swell, descending scream ghosts, dying heartbeat, cut to static", **MUSIC)
def sting_ending_bad(rng):
    n = N(10.0)
    t = T(n)
    drone = saw_stack([36.71, 38.89, 51.91, 73.42, 77.78], n, rng, 0.006)
    cut = np.interp(t, [0, 4, 8, 10], [150, 900, 600, 200])
    drone = np.stack([dsp.tvf(drone[:, k], "lp", cut, 1.4, block=128) for k in range(2)], 1)
    drone = np.tanh(drone / np.max(np.abs(drone)) * 2.5)
    drone *= dsp.env([(0, 0), (3.0, 0.8), (7.5, 1.0), (8.6, 0.4), (9.0, 0)], n)[:, None]
    ghosts = np.zeros((n, 2))
    for k, (nm, st, p) in enumerate((("scream_1", 0.0, -0.6), ("scream_2", 0.1, 0.5), ("scream_3", 0.2, -0.2))):
        s = sfx.scream_fragment(nm, st, 2.0, 1.0)
        s = dsp.varispeed(s, np.linspace(0.9, 0.4, len(s)))
        s = dsp.lp(s, 1800) * dsp.env([(0, 0), (0.5, 1), (len(s) / SR, 0)], len(s))
        s = reverse_swell(s, rng, 2.0)[::-1]  # forward with long tail
        dsp.place(ghosts[:, 0], dsp.pan(s, p)[:, 0], N(1.0 + 2.2 * k), 1.0)
        dsp.place(ghosts[:, 1], dsp.pan(s, p)[:, 1], N(1.0 + 2.2 * k), 1.0)
    hb = np.zeros(n)
    tt = 3.0
    gap = 0.75
    for k in range(6):
        dsp.place(hb, sfx.heartbeat(rng, 50.0), N(tt), 1.0 - 0.1 * k)
        tt += gap
        gap *= 1.28
    out = dsp.norm(drone) + 0.35 * ghosts / (np.max(np.abs(ghosts)) + 1e-9) + 0.7 * dsp.stereo(dsp.norm(hb))
    stn = st_static(n, rng, 0.9) * ((t > 9.0) & (t < 9.6))[:, None]
    out = out / np.max(np.abs(out)) + 0.5 * stn
    out = wide(out, rng, 2.4, 0.3)
    return crush_st(out / np.max(np.abs(out)), 10, 1.5, 1.3)


@sound("Stingers/sting_ending_good", desc="uneasy relief: warm chord with a sour note, warbling tape, grey dawn wind", **MUSIC)
def sting_ending_good(rng):
    n = N(10.0)
    t = T(n)
    chord = [73.42, 146.83, 220.0, 293.66, 369.99, 440.0, 587.33]
    pad = np.zeros((n, 2))
    for i, f in enumerate(chord):
        for d in (-0.003, 0.003):
            v = dsp.tri(f * (1 + d), n, rng.uniform()) + 0.25 * dsp.saw(f * (1 + d), n, rng.uniform())
            pad += dsp.pan(v, -0.7 + 1.4 * i / (len(chord) - 1))
    pad = np.stack([dsp.lp(pad[:, k], 1800) for k in range(2)], 1)
    pad *= dsp.env([(0, 0), (3.5, 1), (7.0, 0.9), (10.0, 0)], n)[:, None]
    sour = np.zeros((n, 2))
    v = dsp.sine(466.16 * (1 + 0.004 * np.sin(TAU * dsp.phase(0.3, n))), n) * dsp.env([(0, 0), (4.5, 0), (6.5, 1), (10, 0)], n)
    sour += dsp.pan(v, 0.4)
    wind = np.stack([dsp.bp(dsp.pink(n, rng), 200, 1200), dsp.bp(dsp.pink(n, rng), 200, 1200)], 1)
    wind *= (0.5 + 0.5 * np.abs(dsp.smooth_rand(n, rng, 0.2, False)))[:, None] * 0.12
    out = dsp.norm(pad) + 0.12 * sour + wind
    out = np.stack([dsp.wowflutter(out[:, c], rng, 0.004, 0.35, 0.0006, 5.5) for c in range(2)], 1)
    out = wide(out, rng, 3.0, 0.4, 4000, 1000)
    return crush_st(out / np.max(np.abs(out)), 11, 1.4, 1.2)


# --------------------------------------------------------------------------------------------- static bursts / glitches
@sound("Stingers/static_burst_1", desc="short harsh static burst", **SOFT)
def static_burst_1(rng):
    n = N(0.38)
    y = st_static(n, rng, 1.0) * dsp.env([(0, 0.3), (0.005, 1), (0.3, 0.9), (0.38, 0)], n)[:, None]
    return y


@sound("Stingers/static_burst_2", desc="stuttering static burst", **SOFT)
def static_burst_2(rng):
    n = N(0.65)
    y = st_static(n, rng, 0.9)
    gate = (np.sin(TAU * dsp.phase(np.interp(T(n), [0, 0.65], [28, 12]), n)) > -0.3).astype(float)
    gate = np.convolve(gate, np.ones(40) / 40, "same")
    buzz = dsp.stereo(dsp.sat(dsp.saw(np.interp(T(n), [0, 0.65], [180, 90]), n), 3)) * 0.4
    return (y + buzz) * gate[:, None] * dsp.env([(0, 1), (0.6, 0.8), (0.65, 0)], n)[:, None]


@sound("Stingers/static_burst_3", desc="swelling static burst with a buried scream", **SOFT)
def static_burst_3(rng):
    n = N(0.95)
    y = st_static(n, rng, 0.8) * dsp.env([(0, 0.2), (0.7, 1), (0.93, 1), (0.95, 0)], n)[:, None]
    sc = scream_layer("scream_3", 0.4, 0.9, 1.0, 3.0, 600)
    sc = dsp.crush(dsp.norm(sc), 5) * dsp.env([(0, 0), (0.6, 1), (0.9, 0.8)], len(sc))
    y[:len(sc)] += 0.35 * dsp.pan(sc, 0.0)[:n]
    return y


def tape_tone(n, rng):
    x = dsp.sine(440.0, n) * 0.4 + 0.3 * dsp.saw(110.0, n) + 0.3 * dsp.band_noise(n, rng, 300, 5000)
    return dsp.lp(x, 4000)


@sound("Stingers/vhs_glitch_1", desc="tape warble + dropout", **MUSIC)
def vhs_glitch_1(rng):
    n = N(0.7)
    x = tape_tone(n, rng) + 0.3 * static_noise(n, rng, 0.4)
    rate = 1 + 0.25 * np.sin(TAU * dsp.phase(np.interp(T(n), [0, 0.7], [6, 14]), n))
    y = dsp.pad(dsp.varispeed(dsp.pad(x, n * 2), rate), n)
    drop = np.ones(n)
    drop[N(0.28):N(0.4)] = 0.05
    drop = np.convolve(drop, np.ones(60) / 60, "same")
    y = dsp.crush(dsp.norm(y * drop), 7)
    return dsp.widen(y, rng, 0.35, 4.0)


@sound("Stingers/vhs_glitch_2", desc="digital buffer stutter crunch", **MUSIC)
def vhs_glitch_2(rng):
    src = tape_tone(N(0.3), rng) + 0.6 * dsp.pad(scream_layer("scream_2", 0.2, 0.3, 1.0, 2.0), N(0.3))
    n = N(0.55)
    y = np.zeros(n)
    t = 0
    k = 0
    while t < n:
        sl = N(rng.choice([0.018, 0.025, 0.035, 0.05]))
        a = N(0.02) * (k % 3)
        seg = src[a:a + sl]
        seg = dsp.resample(seg, 1.0 + 0.15 * (k % 4)) if k % 2 else seg
        dsp.place(y, seg * np.hanning(len(seg)) ** 0.2, t)
        t += len(seg)
        k += 1
    y = dsp.hold(dsp.crush(dsp.norm(y), 5), 3)
    return dsp.widen(y, rng, 0.35, 2.0)


@sound("Stingers/vhs_glitch_3", desc="tape speed dive then snap back", **MUSIC)
def vhs_glitch_3(rng):
    n = N(1.0)
    x = tape_tone(N(2.0), rng) + 0.3 * static_noise(N(2.0), rng, 0.5)
    rate = np.interp(T(n), [0, 0.15, 0.55, 0.62, 1.0], [1.0, 1.0, 0.12, 1.0, 1.0])
    y = dsp.pad(dsp.varispeed(x, rate), n)
    y[N(0.55):N(0.62)] *= 0.1
    y = dsp.sat(dsp.norm(y), 2.0)
    y = dsp.crush(y, 7)
    return dsp.widen(y, rng, 0.35, 3.0)


@sound("Stingers/drone_hit_1", desc="deep impact + long metallic drone tail", **LOUD)
def drone_hit_1(rng):
    n = N(5.5)
    hit = boom(n, rng, 34, 1.2, 1.5) * 1.2 + 1.0 * sfx.metal_hit(n, rng, 92, 22, (0.8, 3.0), 0.9, 0.3)
    hit += 0.5 * dsp.lp(noise_burst(n, rng, 0.1), 3000)
    grit = saw_stack([46.25, 69.3, 92.5], n, rng, 0.008).mean(1)
    hit += 0.5 * dsp.norm(np.tanh(dsp.lp(grit, 900) * 6)) * dsp.perc(n, 0.01, 1.5)
    tail = np.zeros(n)
    for f in (41.2, 43.65, 61.74, 82.41):
        tail += dsp.sine(f, n, rng.uniform())
    tail = dsp.norm(tail + 0.3 * dsp.lp(dsp.brown(n, rng), 200)) * dsp.env([(0, 0), (0.3, 0.6), (3.5, 0.5), (5.5, 0)], n)
    out = wide(dsp.norm(hit) + 0.5 * tail, rng, 3.0, 0.45, 3000, 600)
    return crush_st(dsp.sat(out / np.max(np.abs(out)) * 1.4, 2.0), 10)


@sound("Stingers/drone_hit_2", desc="distorted low brass 'braam' with closing filter", **LOUD)
def drone_hit_2(rng):
    n = N(5.0)
    t = T(n)
    b = saw_stack([36.71, 55.0, 73.42, 77.78, 110.0], n, rng, 0.007)
    cut = np.interp(t, [0, 0.02, 0.4, 5.0], [200, 2600, 1200, 180])
    b = np.stack([dsp.tvf(b[:, k], "lp", cut, 1.8, block=64) for k in range(2)], 1)
    b = np.tanh(b / np.max(np.abs(b)) * 4)
    b *= dsp.env([(0, 0), (0.015, 1), (1.5, 0.7), (5.0, 0)], n)[:, None]
    b += 0.8 * dsp.stereo(boom(n, rng, 36.7, 1.0, 1.0))
    out = wide(b, rng, 2.5, 0.3, 3000, 600)
    return crush_st(out / np.max(np.abs(out)), 10)


@sound("Stingers/heartbeat_fast", loop=True, desc="panicked heartbeat, 150 bpm (seamless, may be looped)", ch=2, norm=("peak", -1.0), q=5)
def heartbeat_fast(rng):
    beat = N(0.4)  # 150 bpm
    n = beat * 8
    y = np.zeros(n)
    for k in range(8):
        hb = sfx.heartbeat(rng, 50.0)
        # compress the lub-dub spacing for a fast heart
        hb = dsp.pad(hb, N(0.75))
        lub, dub = hb[:N(0.16)], hb[N(0.24):N(0.5)]
        b = np.zeros(N(0.6))
        dsp.place(b, lub * np.linspace(1, 0.2, len(lub)) ** 0.5, 0)
        dsp.place(b, dub, N(0.15), 0.8)
        dsp.place(y, b * np.hanning(len(b) * 2)[len(b):] ** 0.3, k * beat + rng.integers(-60, 60), 1.0, circular=True)
    y = dsp.circ_filter(y, dsp._sos("lowpass", 400, 2))
    y = dsp.sat(dsp.norm(y), 2.0)
    return np.stack([y, y], 1)
