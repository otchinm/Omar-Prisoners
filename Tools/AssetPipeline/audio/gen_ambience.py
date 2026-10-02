"""Seamless loops: Audio/Ambience/*_loop.

Every loop is built "circularly": noise is generated in the frequency domain over exactly the loop
length, modulators complete an integer number of cycles, events wrap around the end, IIR filters run
over two periods and keep the second, reverbs use circular convolution.  The loop seam is therefore
just another sample step (verified in the manifest).
"""
import numpy as np

import dsp
import sfx
import common
from common import sound
from dsp import N, T, SR, TAU

BED = dict(ch=2, loop=True, q=4)
EMIT = dict(ch=1, loop=True, q=4)


def L(sec, mult=2):
    n = N(sec)
    return n - n % mult


def lfo(n, rate, rng=None, ph=None):
    p = ph if ph is not None else (rng.uniform() if rng is not None else 0.0)
    return np.sin(TAU * dsp.phase(dsp.qfreq(rate, n), n, p))


def unit(x):
    return (x - x.min()) / (x.max() - x.min() + 1e-12)


def gust(n, rng, rate=0.09, power=1.6):
    g = unit(dsp.smooth_rand(n, rng, rate))
    return g ** power


def cfilt(x, kind, f, order=2):
    return dsp.circ_filter(x, dsp._sos(kind, f, order))


def cbp(x, lo, hi, order=2):
    return dsp.circ_filter(x, dsp._sos("bandpass", [lo, hi], order))


def creverb(x, rng, rt60, wet, stereo=False, **kw):
    return dsp.reverb(x, rng, rt60, wet, stereo=stereo, circular=True, **kw)


def st(l, r):
    return np.stack([l, r], 1)


def events_stereo(n, rng, items):
    """items: [(signal, pos_samples, gain, pan)] -> circularly placed stereo buffer."""
    out = np.zeros((n, 2))
    for sig, pos, g, p in items:
        s = dsp.pan(sig, p)
        for c in range(2):
            dsp.place(out[:, c], s[:, c], pos, g, circular=True)
    return out


def wind_layer(n, rng, g, lo=250, hi=900, q=0.8):
    nz = dsp.pink(n, rng)
    fc = lo + (hi - lo) * g
    y = dsp.tvf(nz, "bp", fc, q, block=128, circular=True)
    return dsp.norm(y) * (0.25 + 0.75 * g)


def cricket_bout(rng, dur=4.0, f=4600.0):
    n = N(dur)
    y = np.zeros(n)
    t = 0.2
    while t < dur - 0.3:
        for k in range(rng.integers(3, 5)):
            m = N(0.016)
            pulse = dsp.sine(f * rng.uniform(0.995, 1.005), m) * np.hanning(m)
            dsp.place(y, pulse, N(t + k * 0.03))
        t += rng.uniform(0.7, 1.1)
    return y * dsp.env([(0, 0), (0.5, 1), (dur - 0.8, 1), (dur, 0)], n)


# --------------------------------------------------------------------------------------------- beds
@sound("Ambience/amb_exterior_loop", norm=("lufs", -18.0, -3.0), desc="night wind in trees, distant rumble, sparse insects", **BED)
def amb_exterior(rng):
    n = L(32.0)
    g = gust(n, rng, 0.07)
    chans = []
    for c in range(2):
        gc = np.clip(g + 0.15 * unit(dsp.smooth_rand(n, rng, 0.2)) - 0.075, 0, 1)
        wind = wind_layer(n, rng, gc, 220, 800, 0.7)
        leaves = cbp(dsp.shaped_noise(n, rng, lambda f: np.ones_like(f)), 1800, 7000)
        leaves = dsp.norm(leaves) * (0.1 + 0.9 * gc ** 2) * np.exp(0.5 * dsp.smooth_rand(n, rng, 7.0))
        whistle = dsp.tvf(dsp.shaped_noise(n, rng, lambda f: np.ones_like(f)), "bp", 650 + 350 * gc, 25,
                          block=128, circular=True)
        rumble = cfilt(dsp.brown(n, rng), "lowpass", 90) * (0.6 + 0.4 * unit(dsp.smooth_rand(n, rng, 0.03)))
        chans.append(dsp.norm(wind) * 1.0 + 0.22 * leaves + 0.05 * dsp.norm(whistle) * gc + 0.5 * dsp.norm(rumble))
    y = st(*chans)
    ev = []
    for k in range(2):  # one far cricket, two short bouts
        ev.append((cricket_bout(rng, rng.uniform(3.5, 5.0), rng.uniform(4300, 4900)),
                   int(rng.uniform(0.1, 0.4) * n + k * n / 2), 0.05, rng.uniform(-0.8, 0.8)))
    e = events_stereo(n, rng, ev)
    e = creverb(e.mean(1), rng, 1.4, 0.6, stereo=True, bright=5000, dark=1500) * 0.5 + e * 0.5
    return y + e


@sound("Ambience/amb_house_loop", norm=("lufs", -20.0, -4.0), desc="old house room tone, mains hum, distant creaks", **BED)
def amb_house(rng):
    n = L(32.0)
    chans = []
    for c in range(2):
        tone = cfilt(cfilt(dsp.pink(n, rng), "lowpass", 450), "highpass", 35)
        air = dsp.band_noise(n, rng, 2000, 6000, order=3) * 0.012
        press = cfilt(dsp.brown(n, rng), "lowpass", 160) * (0.3 + 0.7 * gust(n, rng, 0.05))
        chans.append(dsp.norm(tone) * 0.6 + air + 0.5 * dsp.norm(press))
    y = st(*chans)
    hum = np.zeros(n)
    for k, a in ((1, 0.25), (2, 1.0), (3, 0.35), (4, 0.3), (6, 0.12), (8, 0.06)):
        hum += a * np.sin(TAU * dsp.phase(dsp.qfreq(60.0 * k, n), n, rng.uniform()))
    hum *= 1 + 0.15 * lfo(n, 0.11, rng)
    y += 0.12 * dsp.norm(hum)[:, None]
    ev = []
    for k in range(5):
        m = N(rng.uniform(0.4, 1.1))
        rate = np.interp(T(m), [0, m / SR], [rng.uniform(60, 120), rng.uniform(25, 50)])
        c = sfx.creak(m, rng, rate, amp_env=dsp.env([(0, 0), (0.08, 1), (m / SR * 0.7, 0.7), (m / SR, 0)], m),
                      kind="wood", base=rng.uniform(380, 700))
        c = dsp.lp(c, 2200)
        ev.append((c, int((k + rng.uniform(0.1, 0.8)) * n / 5), rng.uniform(0.25, 0.6), rng.uniform(-0.9, 0.9)))
    for k in range(2):  # something settles upstairs
        th = dsp.lp(sfx.thud(N(0.5), rng, rng.uniform(55, 75), 0.08, 0.5, noise=0.5), 400)
        ev.append((th, int(rng.uniform(0, 1) * n), 0.35, rng.uniform(-0.6, 0.6)))
    e = events_stereo(n, rng, ev)
    e = creverb(e.mean(1), rng, 0.9, 0.7, stereo=True, bright=3000, dark=800) + 0.4 * e
    return y + 0.6 * e


@sound("Ambience/amb_basement_loop", norm=("lufs", -19.0, -3.0), desc="deep drone, drips, groaning pipes", **BED)
def amb_basement(rng):
    n = L(32.0)
    drone = np.zeros(n)
    for f, a in ((41.2, 1.0), (43.7, 0.7), (58.3, 0.5), (82.4, 0.25), (87.3, 0.18)):
        drone += a * np.sin(TAU * dsp.phase(dsp.qfreq(f, n), n, rng.uniform())) * (0.7 + 0.3 * lfo(n, 0.05, rng))
    chans = []
    for c in range(2):
        rum = cfilt(dsp.brown(n, rng), "lowpass", 140) * (0.6 + 0.4 * unit(dsp.smooth_rand(n, rng, 0.05)))
        air = dsp.band_noise(n, rng, 300, 3000) * 0.04
        chans.append(0.7 * dsp.norm(drone) + 0.5 * dsp.norm(rum) + air)
    y = st(*chans)
    ev = []
    for k in range(9):  # drips
        d = sfx.drip(rng, rng.uniform(900, 2200))
        ev.append((d, int(rng.uniform(0, 1) * n), rng.uniform(0.15, 0.4), rng.uniform(-0.9, 0.9)))
    for k in range(2):  # pipe groan
        m = N(rng.uniform(1.8, 3.0))
        rate = 30 + 20 * unit(dsp.smooth_rand(m, rng, 1.5, circular=False))
        b = rng.uniform(150, 210)
        groan = sfx.creak(m, rng, rate, modes=[(b, 20, 1), (b * 2.01, 25, 0.7), (b * 3.02, 30, 0.4),
                                                 (b * 4.5, 30, 0.2)],
                          amp_env=dsp.env([(0, 0), (0.4, 1), (m / SR - 0.5, 0.8), (m / SR, 0)], m), jitter=0.15)
        ev.append((dsp.lp(groan, 1500), int((k + 0.3) * n / 2), 0.3, rng.uniform(-0.5, 0.5)))
    knocks = np.zeros(N(1.6))
    tt = 0.0
    for k in range(5):  # water hammer knocking in the pipes
        dsp.place(knocks, sfx.metal_hit(N(0.3), rng, 380, 10, (0.03, 0.12), 0.6), N(tt), 0.9 ** k)
        tt += 0.32 * 0.8 ** k
    ev.append((dsp.lp(knocks, 2500), int(rng.uniform(0.55, 0.75) * n), 0.35, rng.uniform(-0.7, 0.7)))
    e = events_stereo(n, rng, ev)
    e = creverb(e.mean(1), rng, 1.8, 0.8, stereo=True, bright=4000, dark=900) + 0.35 * e
    return y + 0.7 * e


@sound("Ambience/amb_barn_loop", norm=("lufs", -18.0, -3.0), desc="wind through planks, beam creaks, loose tin", **BED)
def amb_barn(rng):
    n = L(32.0)
    g = gust(n, rng, 0.08, 1.4)
    chans = []
    for c in range(2):
        gc = np.clip(g + 0.1 * dsp.smooth_rand(n, rng, 0.3) * 0.3, 0, 1)
        outside = cfilt(dsp.pink(n, rng), "lowpass", 700) * (0.3 + 0.7 * gc)
        gaps = np.zeros(n)
        for f in (rng.uniform(480, 560), rng.uniform(780, 900), rng.uniform(1250, 1450)):
            gaps += dsp.tvf(dsp.pink(n, rng), "bp", f * (0.9 + 0.2 * gc), 30, block=128, circular=True)
        hay = cbp(dsp.shaped_noise(n, rng, lambda f: np.ones_like(f)), 2000, 6000) * 0.05 * (0.5 + gc)
        chans.append(dsp.norm(outside) + 0.12 * dsp.norm(gaps) * gc ** 1.5 + hay)
    y = st(*chans)
    ev = []
    for k in range(6):
        m = N(rng.uniform(0.6, 1.6))
        rate = np.interp(T(m), [0, m / SR * 0.5, m / SR], [rng.uniform(25, 40), rng.uniform(50, 90), rng.uniform(20, 35)])
        c = sfx.creak(m, rng, rate, amp_env=dsp.env([(0, 0), (0.15, 1), (m / SR, 0)], m), kind="wood",
                      base=rng.uniform(280, 520))
        ev.append((c, int((k + rng.uniform(0, 0.7)) * n / 6), rng.uniform(0.3, 0.6), rng.uniform(-0.9, 0.9)))
    gpk = int(np.argmax(g))
    for k in range(2):  # loose tin rattling in the strongest gust
        m = N(rng.uniform(0.6, 1.0))
        rattle, pos = dsp.pulse_train(rng.uniform(9, 14), m, rng, 0.4, 0.5)
        rr = np.zeros(m)
        for p_ in pos:
            dsp.place(rr, sfx.metal_hit(N(0.25), rng, rng.uniform(250, 400), 10, (0.05, 0.2), 0.5), p_,
                      rng.uniform(0.3, 1.0))
        rr *= dsp.env([(0, 0), (0.1, 1), (m / SR, 0)], m)
        ev.append((dsp.lp(rr, 4000), gpk + N(k * 1.3 - 0.5), 0.25, rng.uniform(-0.7, 0.7)))
    e = events_stereo(n, rng, ev)
    e = creverb(e.mean(1), rng, 1.3, 0.6, stereo=True, bright=4500, dark=1000) + 0.5 * e
    return y + 0.6 * e


@sound("Ambience/amb_tunnel_loop", norm=("lufs", -19.0, -3.0), desc="deep resonant tunnel rumble, distant water", **BED)
def amb_tunnel(rng):
    n = L(32.0)
    chans = []
    for c in range(2):
        b = dsp.brown(n, rng)
        res = np.zeros(n)
        for f, q in ((31.0, 6), (46.5, 7), (63.0, 8), (94.0, 9)):
            res += dsp.circ_filter(b, dsp.rbj("bp", f, q))
        res *= 0.6 + 0.4 * unit(dsp.smooth_rand(n, rng, 0.04))
        air = cbp(dsp.pink(n, rng), 80, 400) * (0.5 + 0.5 * gust(n, rng, 0.05))
        chans.append(dsp.norm(res) + 0.35 * dsp.norm(air))
    y = st(*chans)
    # distant trickle of water: dense tiny bubbles, far away
    m = n
    tr = np.zeros(m)
    rate = 35 * (0.6 + 0.4 * unit(dsp.smooth_rand(m, rng, 0.1)))
    for t0 in np.nonzero(rng.uniform(size=m) < rate / SR)[0]:
        dsp.place(tr, sfx.bubble(np.exp(rng.uniform(np.log(500), np.log(1600))), rng), t0,
                  rng.uniform(0.1, 1.0), circular=True)
    tr = cfilt(tr, "lowpass", 2200)
    wet = creverb(tr, rng, 2.8, 1.0, stereo=True, bright=3000, dark=700, predelay=0.03)
    ev = []
    for k in range(5):
        ev.append((sfx.drip(rng, rng.uniform(700, 1400)), int(rng.uniform(0, 1) * n), rng.uniform(0.15, 0.3),
                   rng.uniform(-0.8, 0.8)))
    e = events_stereo(n, rng, ev)
    e = creverb(e.mean(1), rng, 3.2, 1.0, stereo=True, bright=3500, dark=700, predelay=0.04)
    return y + 0.08 * dsp.norm(wet) + 0.25 * dsp.norm(e)


def fluoro_hum(n, rng, mains=60.0):
    """Ballast hum: 120 Hz family + magnetostrictive rattle (resonated 120 Hz impulses)."""
    f = dsp.qfreq(mains, n)
    s = np.sin(TAU * dsp.phase(f, n, rng.uniform()))
    hum = np.abs(s) - 2 / np.pi + 0.1 * s
    per = SR / (2 * f)
    imp = np.zeros(n)
    k = np.arange(int(round(n / per)))
    np.add.at(imp, (np.round((k + 0.5) * per).astype(int)) % n, 1.0 + 0.15 * rng.standard_normal(len(k)))
    rattle = sum(g * dsp.circ_filter(imp, dsp.rbj("bp", fr, q)) for fr, q, g in
                 ((rng.uniform(1600, 2000), 9, 1.0), (rng.uniform(2700, 3200), 12, 0.6), (620, 4, 0.5),
                  (rng.uniform(4300, 4800), 14, 0.3)))
    buzz = dsp.norm(hum) * 0.5 + 0.9 * dsp.norm(rattle)
    buzz = dsp.sat(buzz, 1.8)
    whine = 0.01 * np.sin(TAU * dsp.phase(dsp.qfreq(7800.0, n), n))
    return dsp.circ_filter(dsp.norm(buzz), dsp._sos("highpass", 90, 2)) + whine


def flicker(n, rng, hum, count=2):
    """Gate the hum briefly and add electrical ticks (circular)."""
    gate = np.ones(n)
    ticks = np.zeros(n)
    for _ in range(count):
        p = rng.integers(0, n)
        m = N(rng.uniform(0.15, 0.4))
        g = np.ones(m)
        k = 0
        while k < m:
            seg = N(rng.uniform(0.01, 0.05))
            g[k:k + seg] = rng.choice([0.15, 1.0, 0.4])
            ticks[(p + k) % n] += rng.uniform(0.5, 1.0) * rng.choice([-1, 1])
            k += seg
        g = np.convolve(g, np.ones(40) / 40, mode="same")
        idx = (p + np.arange(m)) % n
        gate[idx] = np.minimum(gate[idx], g)
    ticks = dsp.circ_filter(ticks, dsp._sos("highpass", 1500, 2))
    return hum * gate + 0.5 * ticks


@sound("Ambience/amb_restroom_loop", norm=("lufs", -19.0, -3.0), desc="fluorescent hum, sink drip, tiled room", **BED)
def amb_restroom(rng):
    n = L(24.0)
    hum = flicker(n, rng, fluoro_hum(n, rng), 2)
    chans = []
    for c in range(2):
        tone = cfilt(dsp.pink(n, rng), "lowpass", 600) * 0.4
        pipe = dsp.band_noise(n, rng, 2500, 6000) * 0.03 * (0.5 + 0.5 * unit(dsp.smooth_rand(n, rng, 0.1)))
        chans.append(tone + pipe)
    y = st(*chans) + 0.35 * creverb(hum, rng, 0.9, 0.5, stereo=True, bright=8000, dark=2500)
    ev = []
    t = 0.3
    while t < n / SR - 0.3:
        ev.append((sfx.drip(rng, rng.uniform(1500, 1750)), N(t), rng.uniform(0.3, 0.45), 0.35))
        t += rng.uniform(2.0, 2.6)
    e = events_stereo(n, rng, ev)
    e = creverb(e.mean(1), rng, 1.1, 0.8, stereo=True, bright=9000, dark=2500) + 0.5 * e
    return y + 0.6 * e


@sound("Ambience/amb_menu_loop", norm=("lufs", -25.0, -8.0), desc="very low dark room tone under the menu music", **BED)
def amb_menu(rng):
    n = L(30.0)
    chans = []
    sub = np.zeros(n)
    for f, a in ((36.7, 1.0), (37.3, 0.8), (55.0, 0.3)):
        sub += a * np.sin(TAU * dsp.phase(dsp.qfreq(f, n), n, rng.uniform()))
    for c in range(2):
        tone = cfilt(dsp.brown(n, rng), "lowpass", 170) * (0.6 + 0.4 * unit(dsp.smooth_rand(n, rng, 0.04)))
        hiss = dsp.band_noise(n, rng, 2500, 9000) * 0.012
        chans.append(dsp.norm(tone) + 0.35 * dsp.norm(sub) + hiss)
    return st(*chans)


# --------------------------------------------------------------------------------------------- static
def vhs_static(n, rng, harsh=0.0):
    nz = dsp.shaped_noise(n, rng, lambda f: np.ones_like(f))
    cut = 4500 + 2500 * lfo(n, 2.3, rng) * (0.6 + 0.4 * dsp.smooth_rand(n, rng, 4.0) * 0.5)
    cut = np.clip(cut, 1500, 9000) * (1 + 0.3 * harsh)
    y = dsp.tvf(nz, "lp", cut, 0.9 + 1.5 * harsh, block=64, circular=True)
    y = cfilt(y, "highpass", 180)
    y = dsp.norm(y) * (1 + (0.3 + 0.4 * harsh) * dsp.smooth_rand(n, rng, 8.0) * 0.5)
    cr = dsp.crackle(n, rng, 50 + 150 * harsh)
    cr = cfilt(cr, "highpass", 900)
    pops = dsp.crackle(n, rng, 3 + 8 * harsh)
    pops = dsp.circ_filter(pops, dsp.rbj("bp", 700, 1.5))
    hum = np.zeros(n)
    for k in range(1, 12 if harsh else 6):
        hum += np.sin(TAU * dsp.phase(dsp.qfreq(60.0 * k, n), n)) / k ** (1.0 - 0.3 * harsh)
    hum = dsp.sat(hum, 2 + 4 * harsh)
    y = y + (0.6 + 0.4 * harsh) * dsp.norm(cr) + 0.6 * dsp.norm(pops) + (0.12 + 0.2 * harsh) * dsp.norm(hum)
    return y


@sound("Ambience/static_loop", norm=("lufs", -17.0, -2.0), desc="low quality analog static (Omar proximity)", **BED)
def static_loop(rng):
    n = L(8.0, 6)
    chans = []
    for c in range(2):
        y = vhs_static(n, rng, 0.0)
        y = dsp.hold(dsp.crush(dsp.norm(y) * 0.9, 7), 3)
        chans.append(y)
    return st(*chans)


@sound("Ambience/static_heavy_loop", norm=("lufs", -13.0, -1.0), desc="harsh broken-VHS static", **BED)
def static_heavy_loop(rng):
    n = L(6.0, 12)
    chans = []
    bursts = np.ones(n)
    for _ in range(10):
        p, m = rng.integers(0, n), N(rng.uniform(0.04, 0.25))
        idx = (p + np.arange(m)) % n
        bursts[idx] *= rng.choice([0.15, 1.8, 2.4])
    bursts = np.convolve(np.concatenate([bursts[-50:], bursts, bursts[:50]]), np.ones(101) / 101, "same")[50:-50]
    for c in range(2):
        y = vhs_static(n, rng, 1.0)
        y = dsp.fold(dsp.norm(y) * bursts, 1.6) * 0.5 + 0.5 * dsp.sat(dsp.norm(y) * bursts, 4)
        y = dsp.hold(dsp.crush(dsp.norm(y) * 0.95, 5), 4)
        chans.append(y)
    return st(*chans)


@sound("Ambience/tv_static_loop", norm=("lufs", -17.0, -2.0), desc="old CRT TV snow + flyback whine + vertical buzz", **EMIT)
def tv_static_loop(rng):
    n = L(5.0)
    nz = dsp.band_noise(n, rng, 300, 9000)
    nz *= 1 + 0.25 * lfo(n, 59.94, rng)  # vertical-rate buzz in the snow
    buzz = dsp.circ_filter(np.sign(np.sin(TAU * dsp.phase(dsp.qfreq(59.94, n), n))), dsp.rbj("bp", 240, 1.0))
    whine = np.sin(TAU * dsp.phase(dsp.qfreq(15734.0, n), n))
    y = dsp.norm(nz) + 0.15 * dsp.norm(buzz) + 0.03 * whine
    return dsp.crush(dsp.norm(y) * 0.9, 8)


@sound("Ambience/radio_static_loop", sr=22050, norm=("lufs", -18.0, -2.0), desc="AM/shortwave radio static with heterodyne whistles", **EMIT)
def radio_static_loop(rng):
    n = L(6.0)
    nz = dsp.band_noise(n, rng, 250, 3500, order=3)
    fadeq = 0.55 + 0.45 * unit(dsp.smooth_rand(n, rng, 0.7))
    cr = cfilt(dsp.crackle(n, rng, 25), "highpass", 1000)
    whf = 1150 + 180 * lfo(n, 0.17, rng) + 40 * lfo(n, 1.3, rng)
    # fix integer cycles of the wandering whistle (keeps the loop seamless)
    ph = np.cumsum(whf) / SR
    ph -= (ph[-1] - np.round(ph[-1])) * np.arange(1, n + 1) / n
    whistle = np.sin(TAU * ph) * (0.5 + 0.5 * unit(dsp.smooth_rand(n, rng, 0.3)))
    carrier = np.sin(TAU * dsp.phase(dsp.qfreq(420.0, n), n)) * 0.5 * unit(dsp.smooth_rand(n, rng, 0.2)) ** 3
    y = dsp.norm(nz) * fadeq + 0.5 * dsp.norm(cr) + 0.08 * whistle + 0.06 * carrier
    y = dsp.sat(y * 0.8, 2.0)
    return cbp(y, 200, 4200)


@sound("Ambience/fluorescent_buzz_loop", norm=("lufs", -17.0, -2.0), desc="buzzing fluorescent tube with flickers", **EMIT)
def fluorescent_loop(rng):
    n = L(4.0)
    hum = fluoro_hum(n, rng)
    return flicker(n, rng, hum, 1)


# --------------------------------------------------------------------------------------------- music-ish loops
@sound("Ambience/chase_loop", norm=("lufs", -13.5, -1.0), desc="112 bpm chase: heartbeat pulse, detuned saws, metal scrapes, screech cluster", **BED)
def chase_loop(rng):
    bpm = 112
    beat = int(60 * SR / bpm)  # 23625 samples exactly
    bars = 8
    n = beat * 4 * bars
    t = T(n)
    pos_beats = np.arange(n) / beat
    # 1. heartbeat pulse on every beat
    pulse = np.zeros(n)
    hb = sfx.heartbeat(rng, 46.0, 1.0)
    for b in range(4 * bars):
        acc = 1.0 if b % 4 == 0 else 0.75
        dsp.place(pulse, hb, b * beat, acc, circular=True)
    # 2. detuned low saw drone with filter pulsing on eighth notes
    drone = np.zeros(n)
    for f, a in ((36.71, 1.0), (36.95, 0.9), (36.45, 0.9), (38.89, 0.55), (51.91, 0.45), (73.42, 0.3)):
        drone += a * dsp.saw(dsp.qfreq(f, n), n, rng.uniform())
    eighth = (pos_beats * 2) % 1.0
    cut = 140 + 900 * np.exp(-eighth * 5.0) * (0.6 + 0.4 * ((pos_beats % 4) < 0.5))
    drone = dsp.tvf(drone, "lp", cut, 2.5, block=32, circular=True)
    drone = dsp.sat(dsp.norm(drone) * 1.0, 2.5)
    # 3. bowed-metal scrapes (bars 3-4 and 7-8)
    scr = np.zeros(n)
    for start in (2, 6):
        m = beat * 8
        rate = np.interp(T(m), [0, m / SR], [rng.uniform(180, 260), rng.uniform(320, 420)])
        modes = [(f, rng.uniform(60, 120), rng.uniform(0.4, 1.0)) for f in
                 sorted(rng.uniform(500, 4200, 9))]
        sc = dsp.stick_slip(m, rng, rate, modes, jitter=0.12, roughness=0.4)
        sc *= dsp.env([(0, 0), (m / SR * 0.7, 1), (m / SR * 0.97, 1), (m / SR, 0)], m)
        dsp.place(scr, dsp.norm(sc), start * 4 * beat, 1.0, circular=True)
    # 4. high screeching string cluster with 16th tremolo, crescendo every 4 bars
    clus = np.zeros(n)
    for f in (1174.7, 1244.5, 1661.2, 1760.0 * 1.01):
        v = dsp.saw(dsp.qfreq(f, n) * (1 + 0.003 * lfo(n, 5.3, rng)), n, rng.uniform())
        clus += v
    trem = 0.55 + 0.45 * np.cos(TAU * pos_beats * 4)
    cres = ((pos_beats % 16) / 16.0) ** 2.2
    clus = cbp(clus, 900, 5000) * trem * cres
    # 5. hits on bar 1 and 5
    hits = np.zeros(n)
    for b in (0, 4):
        hm = N(2.5)
        h = sfx.metal_hit(hm, rng, 97, 18, (0.3, 1.2), 0.9, 0.4) + 1.3 * sfx.thud(hm, rng, 38, 0.4, 1.2, 0.03, 0.6)
        h = dsp.sat(dsp.norm(h), 3.0)
        dsp.place(hits, h, b * 4 * beat, 1.0, circular=True)
    hits = creverb(hits, rng, 1.6, 0.35, bright=5000, dark=900)
    # 6. noise risers into bar 1 and bar 5
    ris = np.zeros(n)
    for end in (4, 8):
        m = beat * 4
        r = dsp.tvf(rng.standard_normal(m), "bp", np.geomspace(300, 6000, m), 2.0, block=64)
        r *= np.linspace(0, 1, m) ** 3
        dsp.place(ris, r, (end - 1) * 4 * beat, 1.0, circular=True)
    # 7. sixteenth ticking
    tick = np.zeros(n)
    tk = sfx.metal_hit(N(0.08), rng, 2600, 6, (0.01, 0.03), 0.9, 0.5)
    for k in range(16 * bars):
        dsp.place(tick, tk, k * beat // 4, 1.0 if k % 4 == 2 else 0.45, circular=True)
    mono_mix = (0.9 * dsp.norm(pulse) + 0.5 * drone + 0.3 * dsp.norm(scr) + 0.28 * dsp.norm(clus)
                + 0.75 * dsp.norm(hits) + 0.12 * dsp.norm(ris) + 0.08 * dsp.norm(tick))
    side = 0.22 * dsp.norm(scr) - 0.12 * dsp.norm(clus) + 0.06 * dsp.norm(ris) - 0.05 * dsp.norm(tick)
    side += 0.15 * creverb(dsp.norm(clus) + dsp.norm(scr), rng, 1.2, 1.0, bright=6000, dark=1500)
    y = st(mono_mix + side, mono_mix - side)
    y = dsp.sat(y * 0.9, 1.6)
    return dsp.crush(y / np.max(np.abs(y)), 10)


@sound("Ambience/anomaly_loop", norm=("lufs", -18.0, -2.0), desc="warbling tape drone with reversed scream textures", **BED)
def anomaly_loop(rng):
    n = L(16.0)
    pad = np.zeros(n)
    for f, a in ((110.0, 1.0), (116.54, 0.8), (164.81, 0.6), (233.08, 0.4), (349.23, 0.3), (369.99, 0.25)):
        pad += a * dsp.tri(dsp.qfreq(f, n), n, rng.uniform())
    pad = cfilt(pad, "lowpass", 1400) * (0.7 + 0.3 * lfo(n, 0.125, rng))
    # reversed reverb swells of the scream material
    tex = np.zeros(n)
    for k, name in enumerate(("scream_1", "scream_3", "scream_4", "scream_2")):
        s = common.src(name)
        s = dsp.resample(s[:N(1.2)], rng.uniform(0.45, 0.6))
        s = dsp.lp(s, 1800)
        w = dsp.reverb(s, rng, 2.5, 1.0, dry=0.1, bright=3000, dark=800)
        w = w[::-1]
        w *= np.linspace(0, 1, len(w)) ** 2
        dsp.place(tex, w, int(k * n / 4 + rng.uniform(0, 0.1) * n), rng.uniform(0.6, 1.0), circular=True)
    gran = dsp.granular(common.src("screams_long"), rng, n, grain=0.25, density=14, pitch=-14, jitter=0.3,
                        reverse_prob=0.6)
    gran = cfilt(gran, "lowpass", 1500)
    hiss = dsp.band_noise(n, rng, 2000, 8000) * 0.02
    mixm = 0.6 * dsp.norm(pad) + 0.45 * dsp.norm(tex) + 0.25 * dsp.norm(gran)
    mixm = dsp.wowflutter(mixm, rng, wow=0.006, wow_rate=0.37, flutter=0.0008, flutter_rate=5.5, circular=True)
    drop = 1 - 0.7 * (unit(dsp.smooth_rand(n, rng, 1.5)) > 0.93)
    drop = np.convolve(np.concatenate([drop[-200:], drop, drop[:200]]), np.ones(401) / 401, "same")[200:-200]
    mixm = mixm * drop + hiss
    y = creverb(mixm, rng, 2.0, 0.5, stereo=True, bright=4000, dark=900)
    y = dsp.wowflutter(y, rng, wow=0.002, wow_rate=0.6, circular=True)
    return dsp.sat(y / np.max(np.abs(y)), 1.5)


# --------------------------------------------------------------------------------------------- machines / emitters
@sound("Ambience/generator_loop", norm=("lufs", -15.0, -1.0), desc="single cylinder generator ~1800 rpm", **EMIT)
def generator_loop(rng):
    per = 1470  # 30 firings / s
    count = 120
    n = per * count
    y = np.zeros(n)
    pop = np.zeros(N(0.05))
    for k in range(count):
        pop = dsp.burst(N(0.04), rng, 0.0004, 0.004, lp_hz=1400) + 0.8 * sfx.thud(N(0.04), rng, 62, 0.012, 0.4, noise=0.2)
        dsp.place(y, pop, k * per + rng.integers(-15, 15), rng.uniform(0.8, 1.1), circular=True)
    clat = np.zeros(n)
    for k in range(count * 2):
        dsp.place(clat, dsp.click(N(0.01), rng.uniform(3000, 4500), 6, 0.0015, rng), k * per // 2 + 300,
                  rng.uniform(0.3, 0.6), circular=True)
    whine = np.zeros(n)
    for f, a in ((60, 0.5), (120, 1.0), (180, 0.4), (1200, 0.12)):
        whine += a * np.sin(TAU * dsp.phase(dsp.qfreq(f, n), n))
    body = dsp.circ_filter(y, dsp.rbj("bp", 220, 4)) + dsp.circ_filter(y, dsp.rbj("bp", 470, 6))
    m = dsp.norm(y) + 0.5 * dsp.norm(body) + 0.12 * dsp.norm(clat) + 0.15 * dsp.norm(whine)
    m += 0.05 * dsp.band_noise(n, rng, 1000, 6000)
    return dsp.sat(dsp.norm(m), 1.8)


@sound("Ambience/fire_loop", norm=("lufs", -16.0, -1.0), desc="burning fire: roar, crackles, pops", **EMIT)
def fire_loop(rng):
    n = L(8.0)
    roar = cfilt(dsp.brown(n, rng), "lowpass", 500) * (0.6 + 0.4 * unit(dsp.smooth_rand(n, rng, 0.6)))
    mid = cbp(dsp.pink(n, rng), 250, 1600) * np.exp(0.6 * dsp.smooth_rand(n, rng, 7.0))
    hiss = dsp.band_noise(n, rng, 3000, 9000) * 0.035
    cr = np.zeros(n)
    for t0 in np.nonzero(rng.uniform(size=n) < 28 / SR)[0]:
        size = rng.uniform()
        if size > 0.85:  # snapping wood
            g = sfx.strike(N(0.08), rng, [(rng.uniform(900, 2000), 0.01, 1), (rng.uniform(2500, 4500), 0.006, 0.6)],
                           0.9, noise_mix=0.6)
            amp = 1.0
        else:
            g = dsp.click(N(0.02), rng.uniform(1500, 6000), 2, 0.001, rng)
            amp = rng.uniform(0.1, 0.6)
        dsp.place(cr, g, t0, amp, circular=True)
    y = dsp.norm(roar) + 0.3 * dsp.norm(mid) + hiss + 0.5 * dsp.norm(cr)
    return y


@sound("Ambience/car_idle_loop", norm=("lufs", -15.0, -1.0), desc="old 4-cylinder car idling", **EMIT)
def car_idle_loop(rng):
    per = 1600
    count = 110
    n = per * count
    y = np.zeros(n)
    pattern = [1.0, 0.82, 0.95, 0.78]
    for k in range(count):
        m = N(0.06)
        p = dsp.burst(m, rng, 0.001, 0.009, lp_hz=600) + 0.9 * sfx.thud(m, rng, 85, 0.02, 0.3, noise=0.3)
        dsp.place(y, p, k * per + rng.integers(-25, 25), pattern[k % 4] * rng.uniform(0.85, 1.1), circular=True)
    muff = dsp.circ_filter(y, dsp.rbj("peak", 120, 1.2, 8))
    muff = cfilt(muff, "lowpass", 900)
    tick = np.zeros(n)
    for k in range(count * 2):
        dsp.place(tick, dsp.click(N(0.006), rng.uniform(2500, 4000), 5, 0.001, rng), k * per // 2 + 500,
                  rng.uniform(0.2, 0.5), circular=True)
    belt = np.sin(TAU * dsp.phase(dsp.qfreq(690.0, n), n)) * (0.8 + 0.2 * lfo(n, 2.0, rng))
    m = dsp.norm(muff) + 0.08 * dsp.norm(tick) + 0.03 * belt + 0.04 * dsp.band_noise(n, rng, 800, 5000)
    return dsp.sat(dsp.norm(m), 1.6)


@sound("Ambience/helicopter_loop", norm=("lufs", -14.0, -1.0), desc="helicopter hover: rotor chop + turbine whine", **EMIT)
def helicopter_loop(rng):
    per = 4410  # 10 blade passes / s
    count = 40
    n = per * count
    slaps = np.zeros(n)
    for k in range(count):
        m = N(0.09)
        s = dsp.bp(rng.standard_normal(m), 150, 1800) * dsp.perc(m, 0.003, 0.018)
        s += 1.2 * sfx.thud(m, rng, 48, 0.03, 0.5, noise=0.3)
        dsp.place(slaps, s, k * per + rng.integers(-40, 40), rng.uniform(0.85, 1.05), circular=True)
    wash = cfilt(dsp.pink(n, rng), "lowpass", 900)
    chop = 0.45 + 0.55 * (0.5 + 0.5 * np.cos(TAU * np.arange(n) / per)) ** 3
    wash = dsp.norm(wash) * chop
    whf = 3150 * (1 + 0.003 * lfo(n, 0.25, rng))
    ph = np.cumsum(whf) / SR
    ph -= (ph[-1] - np.round(ph[-1])) * np.arange(1, n + 1) / n
    turb = np.sin(TAU * ph) + 0.5 * np.sin(TAU * 2 * ph) + 0.25 * np.sin(TAU * 3.0 * ph)
    jet = dsp.band_noise(n, rng, 2000, 8000)
    tail = cbp(dsp.shaped_noise(n, rng, lambda f: np.ones_like(f)), 400, 1600) * \
        (0.5 + 0.5 * np.sin(TAU * dsp.phase(dsp.qfreq(52.0, n), n)))
    m = dsp.norm(slaps) + 0.7 * wash + 0.06 * turb + 0.12 * dsp.norm(jet) + 0.12 * dsp.norm(tail)
    return dsp.sat(dsp.norm(m), 1.8)


@sound("Ambience/flame_loop", norm=("lufs", -19.0, -3.0), desc="soft fluttering flame hiss (lighter / torch)", **EMIT)
def flame_loop(rng):
    n = L(3.0)
    hiss = cbp(dsp.shaped_noise(n, rng, lambda f: np.ones_like(f)), 500, 4500)
    flut = np.exp(0.45 * dsp.smooth_rand(n, rng, 11.0))
    low = cfilt(dsp.brown(n, rng), "lowpass", 300) * np.exp(0.5 * dsp.smooth_rand(n, rng, 3.0))
    cr = np.zeros(n)
    for t0 in np.nonzero(rng.uniform(size=n) < 3 / SR)[0]:
        dsp.place(cr, dsp.click(N(0.01), rng.uniform(2000, 5000), 3, 0.0008, rng), t0, rng.uniform(0.2, 0.6),
                  circular=True)
    return dsp.norm(hiss) * flut * 0.6 + 0.5 * dsp.norm(low) + 0.3 * cr


@sound("Ambience/omar_breath_loop", norm=("lufs", -15.0, -1.0), desc="Omar's heavy wet breathing through the sack (~0.25 Hz)", **EMIT)
def omar_breath_loop(rng):
    n = L(8.0)
    y = np.zeros(n)
    for c in range(2):
        base = c * n // 2
        ins = rng.uniform(1.3, 1.5)
        outs = rng.uniform(1.7, 1.9)
        b = sfx.breath(N(ins + 0.1), rng, [(0.0, ins, "in", 0.75)])
        b2 = sfx.breath(N(outs + 0.1), rng, [(0.0, outs, "out", 1.0)], voice=0.55, f0=rng.uniform(68, 78))
        dsp.place(y, b, base + N(0.05), 1.0, circular=True)
        dsp.place(y, b2, base + N(0.05 + ins + 0.25), 1.0, circular=True)
        # cloth of the sack sucked in on the inhale
        m = N(ins)
        cl = sfx.cloth(m, rng, dsp.env([(0, 0), (ins * 0.5, 1), (ins, 0)], m), 800, 3000, 300)
        dsp.place(y, cl, base + N(0.05), 0.12, circular=True)
        # wet mouth clicks
        for _ in range(rng.integers(3, 6)):
            dsp.place(y, dsp.click(N(0.01), rng.uniform(1800, 3200), 6, 0.0015, rng),
                      base + N(rng.uniform(0.0, 3.8)), rng.uniform(0.04, 0.1), circular=True)
    # constant floor under the pauses: cloth against the mask + a faint throat rumble
    floor = cfilt(dsp.pink(n, rng), "lowpass", 900) * 0.012
    rum = dsp.circ_filter(dsp.brown(n, rng), dsp.rbj("bp", 70, 2.0)) * 0.02
    y = dsp.norm(y) + floor + rum * (0.6 + 0.4 * lfo(n, 0.25, rng))
    y = cfilt(y, "lowpass", 1700)
    y = dsp.circ_filter(y, dsp.rbj("peak", 380, 1.0, 6))
    y = dsp.circ_filter(y, dsp.rbj("peak", 120, 0.8, 4))
    return dsp.sat(dsp.norm(y), 1.5)


@sound("Ambience/wind_gust_loop", norm=("lufs", -17.0, -2.0), desc="gusty whistling wind", **BED)
def wind_gust_loop(rng):
    n = L(12.0)
    g = gust(n, rng, 0.25, 1.8)
    chans = []
    for c in range(2):
        gc = np.clip(g * (0.85 + 0.3 * unit(dsp.smooth_rand(n, rng, 0.4))), 0, 1)
        w = wind_layer(n, rng, gc, 300, 1400, 0.9)
        wh = dsp.tvf(dsp.pink(n, rng), "bp", 700 + 650 * gc, 28, block=64, circular=True)
        wh2 = dsp.tvf(dsp.pink(n, rng), "bp", 1150 + 900 * gc, 35, block=64, circular=True)
        low = cfilt(dsp.brown(n, rng), "lowpass", 150) * (0.3 + gc)
        chans.append(dsp.norm(w) * (0.3 + 0.7 * gc) + 0.2 * dsp.norm(wh + 0.7 * wh2) * gc ** 2 + 0.4 * dsp.norm(low))
    return st(*chans)


@sound("Ambience/windmill_creak_loop", norm=("lufs", -16.0, -1.0), desc="farm windmill: bearing squeal, vane clank, pump rod", **EMIT)
def windmill_loop(rng):
    rot = N(3.0)
    n = rot * 2
    y = np.zeros(n)
    for r in range(2):
        b = r * rot
        m = N(0.9)
        rate = np.interp(T(m), [0, 0.45, 0.9], [30, rng.uniform(70, 95), 25])
        sq = sfx.creak(m, rng, rate, kind="metal", base=rng.uniform(850, 1000),
                       amp_env=dsp.env([(0, 0), (0.3, 1), (0.7, 0.8), (0.9, 0)], m), jitter=0.2)
        dsp.place(y, sq, b + N(0.3), 0.45, circular=True)
        cl = sfx.clank(N(0.6), rng, 210, (0.08, 0.3), 0.6)
        dsp.place(y, cl, b + N(1.7), 0.5, circular=True)
        m = N(1.0)
        wood = sfx.creak(m, rng, np.interp(T(m), [0, 1.0], [45, 28]), kind="wood", base=420,
                         amp_env=dsp.env([(0, 0), (0.2, 1), (1.0, 0)], m))
        dsp.place(y, wood, b + N(1.9), 0.3, circular=True)
        rod = sfx.thud(N(0.3), rng, 90, 0.05, 0.3, noise=0.6, noise_lp=900)
        dsp.place(y, rod, b + N(2.6), 0.4, circular=True)
    blades = cbp(dsp.pink(n, rng), 300, 2500) * (0.5 + 0.5 * np.sin(TAU * dsp.phase(dsp.qfreq(6.0, n), n))) ** 2
    wind = cfilt(dsp.pink(n, rng), "lowpass", 600) * (0.6 + 0.4 * unit(dsp.smooth_rand(n, rng, 0.3)))
    y = dsp.norm(y) + 0.15 * dsp.norm(blades) + 0.25 * dsp.norm(wind)
    return creverb(y, rng, 0.8, 0.15, bright=5000, dark=1500)
