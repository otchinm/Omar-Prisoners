"""Ambience one-shots: creaks, distant screams, thumps, scrapes, whispers, thunder, drips, phone, dog howl."""
import numpy as np

import common
import dsp
import sfx
from common import sound
from dsp import N, T, SR

ONE = dict(ch=1, norm=("peak", -1.0))


def house_verb(y, rng, rt=0.8, wet=0.3, dry=1.0, **kw):
    return dsp.reverb(y, rng, rt, wet, dry=dry, bright=kw.pop("bright", 3500), dark=kw.pop("dark", 800), **kw)


# --------------------------------------------------------------------------------------------- creaks
_CREAKS = {
    1: dict(dur=1.4, pts=[(0, 42), (0.7, 30), (1.4, 18)], base=460, gain_env=[(0, 0), (0.2, 1), (1.0, 0.7), (1.4, 0)]),
    2: dict(dur=1.9, pts=[(0, 110), (0.6, 175), (1.3, 140), (1.9, 70)], base=880,
            gain_env=[(0, 0), (0.15, 0.8), (0.8, 1), (1.6, 0.6), (1.9, 0)]),
    3: dict(dur=1.3, pts=[(0, 65), (0.45, 38), (0.55, 0), (0.6, 85), (1.3, 35)], base=560,
            gain_env=[(0, 0), (0.08, 1), (0.45, 0.6), (0.52, 0), (0.62, 1), (1.3, 0)]),
    4: dict(dur=2.5, pts=[(0, 22), (1.2, 46), (2.5, 16)], base=300,
            gain_env=[(0, 0), (0.5, 0.8), (1.3, 1), (2.5, 0)]),
}


def _creak(rng, k):
    c = _CREAKS[k]
    n = N(c["dur"])
    t, r = zip(*c["pts"])
    rate = np.maximum(np.interp(T(n), t, r), 1.0)
    e = dsp.env(c["gain_env"], n)
    y = sfx.creak(n, rng, rate, amp_env=e, kind="wood", base=c["base"], jitter=0.3)
    y = dsp.norm(y) + 0.15 * dsp.norm(dsp.lp(rng.standard_normal(n), 800) * e)  # body rub
    y = house_verb(dsp.lp(y, 4000), rng, 0.9, 0.3)
    return dsp.lofi(y, 11, 1.5, 1.2, 9000)


for _k in range(1, 5):
    common.register(f"Ambience/creak_{_k}", (lambda k: (lambda rng: _creak(rng, k)))(_k),
                    desc="old wood creak", **ONE)


# --------------------------------------------------------------------------------------------- distant screams
def _distant(x, rng, lp_hz, rt, dry, predelay, echo=0.0):
    y = dsp.hp(dsp.lp(x, lp_hz, 3), 220)
    y = dsp.peq(y, 900, 0.8, 4)
    if echo:
        y = y + echo * np.roll(dsp.pad(y, len(y) + N(0.4)), N(0.38))[:len(y)]
    y = dsp.reverb(y, rng, rt, 1.0, dry=dry, predelay=predelay, bright=2500, dark=600)
    return dsp.lofi(y, 10, 1.8, 1.4, 6000)


@sound("Ambience/distant_scream_1", desc="far scream across the fields (derived from scream_1)", **ONE)
def distant_scream_1(rng):
    x = dsp.resample(common.src("scream_1"), 0.92)
    x *= dsp.env([(0, 0), (0.05, 1), (2.6, 0.8), (len(x) / SR, 0)], len(x))
    return _distant(x, rng, 1700, 2.6, 0.25, 0.07, echo=0.3)


@sound("Ambience/distant_scream_2", desc="scream through walls (derived from scream_3)", **ONE)
def distant_scream_2(rng):
    x = dsp.resample(common.src("scream_3"), 0.84)
    x = dsp.lp(x, 900, 2)
    return _distant(x, rng, 1000, 1.3, 0.45, 0.02)


@sound("Ambience/distant_scream_3", desc="long faint wail far away (derived from screams_long)", **ONE)
def distant_scream_3(rng):
    x = common.src_segment("screams_long", 4.2, 4.2)
    x = dsp.resample(x, 0.8)
    x *= dsp.env([(0, 0), (0.3, 1), (3.5, 0.8), (len(x) / SR, 0)], len(x))
    return _distant(x, rng, 1300, 3.2, 0.2, 0.09, echo=0.25)


# --------------------------------------------------------------------------------------------- thumps
def _wood_bang(rng, n, f=65, hard=0.4):
    y = sfx.strike(n, rng, sfx.wood_modes(rng, f * 1.4, 9, 0.12), hard, noise_mix=0.3)
    y += 1.2 * sfx.thud(n, rng, f, 0.12, 0.8, noise=0.5)
    return dsp.norm(y)


@sound("Ambience/thump_1", desc="heavy thud upstairs", **ONE)
def thump_1(rng):
    n = N(1.0)
    y = dsp.lp(_wood_bang(rng, n, 58, 0.5), 650)
    y = house_verb(y, rng, 1.0, 0.45)
    return dsp.lofi(dsp.sat(dsp.norm(y), 1.8), 11, 1.5)


@sound("Ambience/thump_2", desc="something heavy falls (double thud)", **ONE)
def thump_2(rng):
    n = N(1.4)
    y = _wood_bang(rng, n, 62, 0.5)
    dsp.place(y, _wood_bang(rng, N(0.9), 75, 0.4), N(0.23), 0.6)
    dsp.place(y, _wood_bang(rng, N(0.6), 90, 0.4), N(0.41), 0.25)
    y = house_verb(dsp.lp(y, 800), rng, 1.0, 0.45)
    return dsp.lofi(dsp.sat(dsp.norm(y), 1.8), 11, 1.5)


@sound("Ambience/thump_3", desc="three bangs on a door somewhere in the house", **ONE)
def thump_3(rng):
    n = N(1.9)
    y = np.zeros(n)
    t = 0.0
    for k in range(3):
        b = _wood_bang(rng, N(0.6), 85 + 10 * rng.uniform(), 0.6)
        b += 0.3 * dsp.pad(sfx.strike(N(0.2), rng, [(2300, 0.01, 1), (3400, 0.008, 0.6)], 0.8), N(0.6))  # door rattle
        dsp.place(y, b, N(t), [1.0, 0.85, 1.1][k])
        t += rng.uniform(0.32, 0.42)
    y = house_verb(dsp.lp(y, 1100), rng, 1.1, 0.4)
    return dsp.lofi(dsp.sat(dsp.norm(y), 2.0), 11, 1.5)


# --------------------------------------------------------------------------------------------- metal scrapes
@sound("Ambience/metal_scrape_1", desc="metal dragged across concrete", **ONE)
def metal_scrape_1(rng):
    n = N(2.4)
    e = dsp.env([(0, 0), (0.2, 0.8), (1.0, 1.0), (1.7, 0.6), (2.2, 0.9), (2.4, 0)], n)
    e *= np.exp(0.4 * dsp.smooth_rand(n, rng, 6, circular=False))
    res = [(f, rng.uniform(25, 60), rng.uniform(0.4, 1.0)) for f in (430, 1130, 1890, 2770, 3810)]
    y = sfx.scrape(n, rng, e, 400, 6000, grit=1500, res=res)
    sq = sfx.creak(n, rng, 160 + 80 * dsp.smooth_rand(n, rng, 2, circular=False), kind="metal", base=1300,
                   amp_env=e ** 2, jitter=0.15)
    y = dsp.norm(y) + 0.35 * sq
    y = dsp.reverb(y, rng, 1.8, 0.45, bright=4500, dark=1000, predelay=0.02)
    return dsp.lofi(dsp.sat(dsp.norm(y), 1.6), 10, 1.6)


@sound("Ambience/metal_scrape_2", desc="metal on metal screech in the dark", **ONE)
def metal_scrape_2(rng):
    n = N(1.8)
    e = dsp.env([(0, 0), (0.1, 1), (1.2, 0.8), (1.8, 0)], n)
    rate = np.interp(T(n), [0, 0.6, 1.2, 1.8], [220, 340, 290, 180])
    modes = [(f, rng.uniform(60, 140), g) for f, g in ((870, 1.0), (1440, 0.8), (2210, 0.7), (3130, 0.5),
                                                       (4400, 0.3), (5900, 0.2))]
    y = dsp.stick_slip(n, rng, rate, modes, jitter=0.12, amp_env=e, roughness=0.4)
    y = dsp.norm(y) + 0.3 * sfx.scrape(n, rng, e, 1500, 7000, grit=900)
    y = dsp.reverb(y, rng, 2.2, 0.5, bright=5000, dark=1200, predelay=0.03)
    return dsp.lofi(dsp.sat(dsp.norm(y), 2.0), 10, 1.6)


# --------------------------------------------------------------------------------------------- whispers
def _syllables(rng, n, count):
    """Unvoiced formant 'syllables' (h / s / sh + vowel) -> breathy unintelligible words."""
    out = np.zeros(n)
    t = 0.05
    vowels = ["a", "e", "i", "o", "u", "uh", "ae"]
    for _ in range(count):
        d = rng.uniform(0.18, 0.35)
        m = N(d)
        v = sfx.vowel_track(m, [(0, rng.choice(vowels)), (d, rng.choice(vowels))], rng, 0.05)
        y = sfx.formant_filter(rng.standard_normal(m), v, bw=(120, 150, 200, 260), gains=(0.5, 1.0, 0.8, 0.5))
        y = dsp.norm(y) * dsp.env([(0, 0), (d * 0.3, 1), (d, 0)], m)
        dsp.place(out, y, N(t))
        if rng.uniform() < 0.6:  # sibilant
            sm = N(rng.uniform(0.06, 0.14))
            ss = dsp.bp(rng.standard_normal(sm), rng.uniform(3500, 5000), 9000) * np.hanning(sm)
            dsp.place(out, ss, N(t + d * rng.choice([-0.3, 0.9])), 0.5)
        t += d + rng.uniform(0.02, 0.12)
    return out


@sound("Ambience/whisper_1", desc="close breathy whisper (vocoded from the screams + formant noise)", **ONE)
def whisper_1(rng):
    x = dsp.pv_stretch(common.src("scream_4"), 1.6)
    w = dsp.whisperize(x, rng, smooth=10)
    w = dsp.hp(w, 450)
    n = len(w)
    w *= dsp.env([(0, 0), (0.15, 1), (n / SR - 0.4, 0.8), (n / SR, 0)], n)
    s = _syllables(rng, n, 6)
    y = 0.55 * dsp.norm(w) + 0.7 * dsp.norm(s)
    y = dsp.lp(y, 7500)
    y = dsp.reverb(y, rng, 0.5, 0.2, bright=6000, dark=1500)
    return dsp.lofi(y, 11, 1.4)


@sound("Ambience/whisper_2", desc="reversed ghostly whisper", **ONE)
def whisper_2(rng):
    x = common.src_segment("screams_long", 6.0, 1.8)
    w = dsp.whisperize(dsp.pv_stretch(x, 1.4), rng, smooth=12)[::-1]
    n = len(w)
    s = _syllables(rng, n, 7)[::-1]
    y = 0.6 * dsp.norm(dsp.hp(w, 400)) + 0.6 * dsp.norm(s)
    y *= dsp.env([(0, 0), (0.6, 0.7), (n / SR - 0.3, 1), (n / SR, 0)], n)
    y = dsp.reverb(dsp.lp(y, 7000), rng, 1.6, 0.55, bright=5000, dark=1200, predelay=0.02)
    return dsp.lofi(dsp.wowflutter(y, rng, 0.003, 0.7), 11, 1.4)


# --------------------------------------------------------------------------------------------- thunder
def _thunder(rng, dur, crack, lp_hz, rolls):
    n = N(dur)
    t = T(n)
    e = np.zeros(n)
    for (tc, a, w) in rolls:
        e += a * np.exp(-0.5 * ((t - tc) / w) ** 2) * (t >= tc - 3 * w)
    e += 0.25 * np.exp(-t / (dur * 0.35))
    e *= np.exp(0.5 * dsp.smooth_rand(n, rng, 3.0, circular=False))
    rum = dsp.lp(dsp.brown(n, rng), lp_hz, 2) * e
    y = dsp.norm(rum)
    # rolling crackle inside the rumble (gives it body on small speakers)
    roll = dsp.bp(dsp.crackle(n, rng, 120 * (1 + crack)) + 0.3 * rng.standard_normal(n), 120, lp_hz * 2.5) * e
    y = y + (0.25 + 0.35 * crack) * dsp.norm(roll)
    if crack:
        m = N(0.9)
        cr = rng.standard_normal(m) * dsp.perc(m, 0.002, 0.12)
        spikes = dsp.crackle(m, rng, 200) * dsp.perc(m, 0.001, 0.2)
        cr = dsp.lp(cr + 3 * spikes, 6000)
        y = dsp.pad(y, n)
        dsp.place(y, dsp.norm(cr), N(0.02), crack)
        y += 0.45 * dsp.pad(sfx.thud(N(2.0), rng, 38, 0.6, 1.0, 0.05, 0.5, 300), n)
    y = dsp.reverb(y, rng, 2.5, 0.4, bright=3000, dark=500)
    return dsp.lofi(dsp.sat(dsp.norm(y), 2.2), 11, 1.5, 1.0, 9000)


@sound("Ambience/thunder_1", desc="close thunder: crack + heavy rolling rumble", **ONE)
def thunder_1(rng):
    return _thunder(rng, 8.0, 0.8, 480, [(0.15, 1.0, 0.25), (1.2, 0.8, 0.5), (2.6, 0.6, 0.7), (4.3, 0.35, 0.9)])


@sound("Ambience/thunder_2", desc="distant rolling thunder", **ONE)
def thunder_2(rng):
    return _thunder(rng, 9.0, 0.0, 240, [(0.9, 0.6, 0.6), (2.2, 1.0, 0.8), (3.9, 0.7, 0.9), (5.8, 0.4, 1.0)])


# --------------------------------------------------------------------------------------------- drips
def _drip(rng, f0, rt, wet, bright):
    y = sfx.drip(rng, f0)
    y = dsp.pad(y, len(y) + N(0.02))
    y = dsp.reverb(y, rng, rt, wet, bright=bright, dark=bright / 4, predelay=0.01)
    return dsp.lofi(y, 11, 1.4)


@sound("Ambience/drip_1", desc="water drip in the basement", **ONE)
def drip_1(rng):
    return _drip(rng, 1250, 1.5, 0.5, 5000)


@sound("Ambience/drip_2", desc="drip into a sink (tiled room)", **ONE)
def drip_2(rng):
    return _drip(rng, 1750, 0.9, 0.4, 8000)


@sound("Ambience/drip_3", desc="deep echoing drip in the tunnel", **ONE)
def drip_3(rng):
    return _drip(rng, 880, 2.6, 0.8, 3500)


# --------------------------------------------------------------------------------------------- phone
@sound("Ambience/phone_ring", desc="old bell telephone ringing twice in another room", **ONE)
def phone_ring(rng):
    n = N(6.2)
    gongs = []
    for f0 in (1060.0, 1190.0):
        modes = [(f0, 0.9, 1.0), (f0 * 2.76, 0.45, 0.6), (f0 * 5.40, 0.25, 0.35), (f0 * 8.93, 0.12, 0.2),
                 (f0 * 0.5, 0.5, 0.15)]
        gongs.append(dsp.modal_ir(N(1.5), modes, rng))
    y = np.zeros(n)
    for start in (0.05, 3.55):
        k = 0
        t = start
        while t < start + 1.8:
            exc = dsp.burst(N(0.004), rng, 0.0001, 0.0006, lp_hz=9000)
            dsp.place(y, dsp.excite(exc, gongs[k % 2]), N(t), 0.9 + 0.2 * rng.uniform())
            k += 1
            t += 1 / 20.0 * (1 + 0.04 * rng.standard_normal())
    y = dsp.sat(dsp.norm(y) * 1.2, 1.5)
    y = dsp.bp(y, 350, 3200)  # heard through a door
    y = house_verb(y, rng, 1.1, 0.6, dry=0.6)
    return dsp.lofi(y, 10, 1.6, 1.2, 7000)


# --------------------------------------------------------------------------------------------- dog howl
def _howl(rng, dur, f_pts):
    n = N(dur)
    t, f = zip(*f_pts)
    f0 = np.interp(T(n), t, f) * (1 + 0.018 * np.sin(2 * np.pi * dsp.phase(5.2, n)))
    src = sfx.glottal(f0, n, rng, 0.006, 0.08)
    src += 0.25 * dsp.norm(dsp.hp(rng.standard_normal(n), 1500))
    v = sfx.vowel_track(n, [(0, "u"), (dur * 0.25, "o"), (dur * 0.55, "a"), (dur, "u")], rng, 0.04)
    v[:, 0] *= 1.25
    v[:, 1] *= 1.3  # smaller vocal tract than a man
    y = sfx.formant_filter(src, v, bw=(90, 120, 180, 250), gains=(1.0, 0.7, 0.3, 0.15))
    e = dsp.env([(0, 0), (0.25, 1), (dur * 0.7, 0.85), (dur, 0)], n)
    return dsp.norm(y) * e


@sound("Ambience/dog_howl", desc="distant dog howling across the fields", **ONE)
def dog_howl(rng):
    a = _howl(rng, 3.6, [(0, 420), (0.5, 600), (2.2, 585), (3.0, 520), (3.6, 400)])
    b = _howl(rng, 2.8, [(0, 480), (0.4, 650), (1.8, 630), (2.8, 470)])
    y = dsp.pad(a, N(5.0))
    dsp.place(y, b, N(1.6), 0.45)
    y = dsp.lp(dsp.hp(y, 300), 2600, 2)
    y = dsp.reverb(y, rng, 2.8, 1.0, dry=0.3, predelay=0.09, bright=2500, dark=600)
    return dsp.lofi(y, 10, 1.6, 1.3, 6000)
