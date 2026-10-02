"""Items: Audio/Items/* (mono 3D one-shots)."""
import numpy as np

import dsp
import sfx
from common import sound
from dsp import N, T, SR

ONE = dict(ch=1, norm=("peak", -1.0))


def room(y, rng, rt=0.4, wet=0.1):
    return dsp.reverb(y, rng, rt, wet, bright=5000, dark=1200)


def fin(y, rng, rt=0.4, wet=0.1, bits=11):
    return dsp.lofi(room(dsp.norm(y), rng, rt, wet), bits, 1.4, 1.2, 10000)


def small_metal(rng, n, f0, tau=(0.05, 0.2), count=8, hard=0.9):
    return sfx.strike(n, rng, dsp.metal_modes(f0, count, rng, tau=tau), hard, noise_mix=0.3)


# --------------------------------------------------------------------------------------------- lighter
@sound("Items/lighter_open", desc="zippo lid flick open: bright clink", **ONE)
def lighter_open(rng):
    n = N(0.6)
    modes = [(2870, 0.22, 1.0), (4610, 0.16, 0.7), (6790, 0.1, 0.45), (9140, 0.07, 0.3), (1630, 0.05, 0.3)]
    y = sfx.strike(n, rng, modes, 0.95, noise_mix=0.25)
    y += 0.5 * dsp.pad(dsp.click(N(0.02), 3500, 3, 0.002, rng), n)  # cam spring snap
    dsp.place(y, small_metal(rng, N(0.15), 1900, (0.01, 0.04)), N(0.012), 0.35)
    return fin(y, rng, 0.3, 0.06)


@sound("Items/lighter_flick", desc="flint wheel scrape + ignition whoosh", **ONE)
def lighter_flick(rng):
    n = N(0.9)
    y = np.zeros(n)
    m = N(0.045)
    wheel, _ = dsp.pulse_train(np.linspace(900, 400, m), m, rng, 0.4, 0.5)
    wheel = dsp.bp(wheel + 0.3 * rng.standard_normal(m) * dsp.perc(m, 0.002, 0.02), 2000, 9000)
    dsp.place(y, dsp.norm(wheel), 0)
    sparks = sfx.grains(N(0.12), rng, np.full(N(0.12), 300.0), 4000, 10000, (0.0005, 0.0015))
    dsp.place(y, sparks, N(0.02), 0.4)
    m = N(0.8)
    whoosh = dsp.tvf(rng.standard_normal(m), "lp", np.interp(T(m), [0, 0.06, 0.25, 0.8], [150, 1500, 900, 600]), 1.2)
    whoosh *= dsp.env([(0, 0), (0.05, 1), (0.2, 0.45), (0.8, 0.15)], m) * np.exp(0.4 * dsp.smooth_rand(m, rng, 12, False))
    dsp.place(y, dsp.norm(whoosh) * 0.8, N(0.03))
    return fin(y, rng, 0.3, 0.06)


@sound("Items/lighter_close", desc="zippo lid snapped shut", **ONE)
def lighter_close(rng):
    n = N(0.3)
    modes = [(1620, 0.05, 1.0), (2750, 0.04, 0.8), (4150, 0.025, 0.5), (6100, 0.015, 0.3)]
    y = sfx.strike(n, rng, modes, 0.85, noise_mix=0.4) + 0.4 * sfx.thud(n, rng, 180, 0.015, 0.3)
    return fin(y, rng, 0.3, 0.06)


@sound("Items/fuel_pour", desc="lighter fluid squirts", **ONE)
def fuel_pour(rng):
    n = N(1.3)
    rate = np.zeros(n)
    for a, b in ((0.05, 0.45), (0.6, 1.1)):
        rate += 70 * dsp.env([(0, 0), (a, 0), (a + 0.04, 1), (b - 0.05, 0.8), (b, 0), (2, 0)], n)
    y = sfx.liquid(n, rng, rate, 1200, 3200, stream=0.6)
    return fin(y, rng, 0.3, 0.06)


# --------------------------------------------------------------------------------------------- medical / light
def _rip(rng, dur, speed=1.0):
    n = N(dur)
    dens = 900 * speed * dsp.env([(0, 0.3), (dur * 0.2, 1), (dur * 0.85, 1.2), (dur, 0)], n)
    g = sfx.grains(n, rng, dens, 900, 6500, (0.0006, 0.003))
    nz = dsp.bp(rng.standard_normal(n), 1200, 6000) * np.abs(dsp.smooth_rand(n, rng, 80, False)) * \
        dsp.env([(0, 0), (0.02, 1), (dur, 0.7)], n)
    return dsp.norm(g) + 0.35 * dsp.norm(nz)


@sound("Items/bandage_rip", desc="tearing a strip of cloth bandage", **ONE)
def bandage_rip(rng):
    n = N(1.2)
    y = np.zeros(n)
    dsp.place(y, sfx.cloth(N(0.25), rng, dsp.env([(0, 0), (0.05, 1), (0.25, 0)], N(0.25)), 500, 4000, 200), 0, 0.4)
    dsp.place(y, _rip(rng, 0.32), N(0.2), 1.0)
    dsp.place(y, _rip(rng, 0.45, 1.3), N(0.62), 0.9)
    return fin(y, rng)


@sound("Items/flashlight_click", desc="plastic flashlight switch click", **ONE)
def flashlight_click(rng):
    n = N(0.16)
    a = sfx.strike(n, rng, [(2450, 0.012, 1.0), (4100, 0.008, 0.6), (1150, 0.015, 0.5)], 0.95, noise_mix=0.4)
    b = sfx.strike(n, rng, [(2900, 0.008, 1.0), (5200, 0.006, 0.6)], 0.95, noise_mix=0.4)
    y = a + 0.6 * np.roll(b, N(0.035))
    return fin(y, rng, 0.25, 0.05)


@sound("Items/battery_insert", desc="batteries slide into the flashlight, cap screwed on", **ONE)
def battery_insert(rng):
    n = N(1.15)
    y = np.zeros(n)
    for t in (0.0, 0.22):
        m = N(0.12)
        sl = sfx.scrape(m, rng, dsp.env([(0, 0), (0.01, 1), (0.12, 0.3)], m), 1500, 7000, 800)
        dsp.place(y, sl, N(t), 0.4)
        dsp.place(y, small_metal(rng, N(0.2), 1100, (0.02, 0.08)), N(t + 0.12), 0.9)
    m = N(0.45)
    screw, _ = dsp.pulse_train(np.full(m, 28.0), m, rng, 0.3, 0.4)
    screw = dsp.reson(screw, 2600, 5) + 0.5 * dsp.reson(screw, 1300, 4)
    screw = dsp.norm(screw) * dsp.env([(0, 0), (0.05, 1), (0.45, 0.6)], m)
    dsp.place(y, screw, N(0.5), 0.5)
    dsp.place(y, sfx.strike(N(0.15), rng, [(2200, 0.015, 1), (3600, 0.01, 0.6)], 0.9), N(0.98), 0.7)
    return fin(y, rng)


# --------------------------------------------------------------------------------------------- generic handling
@sound("Items/item_pickup", desc="grab: cloth rustle + small knock", **ONE)
def item_pickup(rng):
    n = N(0.4)
    y = sfx.cloth(n, rng, dsp.env([(0, 0), (0.02, 1), (0.18, 0.4), (0.35, 0)], n), 500, 4500, 250) * 0.7
    k = sfx.strike(N(0.2), rng, sfx.wood_modes(rng, 260, 7, 0.03), 0.6, noise_mix=0.3)
    dsp.place(y, dsp.norm(k), N(0.05), 0.7)
    return fin(y, rng)


@sound("Items/item_drop", desc="object dropped on the floor, small bounce", **ONE)
def item_drop(rng):
    n = N(0.7)
    y = np.zeros(n)
    for t, g, f in ((0.0, 1.0, 190), (0.16, 0.45, 210), (0.26, 0.2, 230), (0.31, 0.1, 240)):
        k = sfx.strike(N(0.3), rng, sfx.wood_modes(rng, f, 8, 0.05), 0.65, noise_mix=0.3)
        k += 0.5 * sfx.thud(N(0.3), rng, f * 0.45, 0.03, 0.3)
        dsp.place(y, dsp.norm(k), N(t), g)
    return fin(y, rng, 0.45, 0.12)


@sound("Items/item_equip", desc="cloth rustle + grip click", **ONE)
def item_equip(rng):
    n = N(0.42)
    y = sfx.cloth(n, rng, dsp.env([(0, 0), (0.03, 1), (0.25, 0.3), (0.4, 0)], n), 500, 4000, 200) * 0.6
    c = sfx.strike(N(0.15), rng, [(1700, 0.02, 1), (2900, 0.012, 0.6), (650, 0.02, 0.5)], 0.8, noise_mix=0.3)
    dsp.place(y, dsp.norm(c), N(0.16), 0.8)
    return fin(y, rng)


@sound("Items/bottle_throw", desc="bottle whooshing through the air", **ONE)
def bottle_throw(rng):
    n = N(0.5)
    y = sfx.whoosh(n, rng, 400, 2600, 0.38, 1.6, 0.17)
    y += 0.3 * sfx.whoosh(n, rng, 900, 4500, 0.4, 3.0, 0.12)  # spinning flutter
    y *= 1 + 0.5 * np.sin(2 * np.pi * dsp.phase(np.linspace(9, 14, n), n))
    y += 0.25 * dsp.pad(sfx.cloth(N(0.08), rng, np.ones(N(0.08)), 600, 3000, 0), n)  # arm swing
    return fin(y, rng, 0.3, 0.04)


@sound("Items/glass_break", desc="glass bottle shattering", **ONE)
def glass_break(rng):
    n = N(1.5)
    y = np.zeros(n)
    hit = sfx.strike(N(0.3), rng, [(1450, 0.03, 1), (2630, 0.025, 0.8), (3980, 0.02, 0.6), (5600, 0.012, 0.4)], 1.0,
                     noise_mix=0.6)
    dsp.place(y, dsp.norm(hit) + 0.5 * sfx.thud(N(0.3), rng, 120, 0.03, 0.4), 0, 1.0)
    y += 1.4 * sfx.shards(n, rng, 160, 0.005, 0.18, 2200, 11000, (0.006, 0.05), 0.15)
    y += 0.6 * sfx.shards(n, rng, 50, 0.25, 0.45, 2500, 9000, (0.01, 0.06), 0.5, 18)  # bouncing pieces
    burst = dsp.hp(rng.standard_normal(N(0.08)), 2500) * dsp.perc(N(0.08), 0.0005, 0.015)
    dsp.place(y, burst, 0, 0.6)
    return fin(dsp.sat(dsp.norm(y), 1.5), rng, 0.5, 0.15)


# --------------------------------------------------------------------------------------------- locks
def latch_clack(rng, n=None, f=900, hard=0.9):
    n = n or N(0.25)
    y = small_metal(rng, n, f, (0.02, 0.08), 9, hard)
    return dsp.norm(y + 0.4 * sfx.thud(n, rng, f / 6, 0.015, 0.3))


@sound("Items/key_unlock", desc="key slides in over the pins, turns, bolt retracts", **ONE)
def key_unlock(rng):
    n = N(1.0)
    y = np.zeros(n)
    m = N(0.28)
    ins = sfx.scrape(m, rng, dsp.env([(0, 0.3), (0.28, 1)], m), 2000, 8000, 400) * 0.3
    dsp.place(y, ins, 0)
    for k in range(5):  # pins
        dsp.place(y, dsp.click(N(0.02), rng.uniform(3500, 5200), 5, 0.003, rng), N(0.03 + k * 0.05 + rng.uniform(0, 0.01)), 0.5)
    dsp.place(y, small_metal(rng, N(0.2), 1500, (0.02, 0.06)), N(0.3), 0.5)  # key seated
    m = N(0.2)
    turn = sfx.creak(m, rng, np.linspace(120, 70, m), kind="metal", base=2400,
                     amp_env=dsp.env([(0, 0), (0.05, 1), (0.2, 0)], m))
    dsp.place(y, turn, N(0.42), 0.25)
    dsp.place(y, latch_clack(rng, N(0.35), 650), N(0.6), 1.0)
    return fin(y, rng, 0.45, 0.12)


@sound("Items/locked_rattle", desc="handle jiggles on a locked door", **ONE)
def locked_rattle(rng):
    n = N(0.85)
    y = np.zeros(n)
    t = 0.0
    for k in range(4):
        dsp.place(y, latch_clack(rng, N(0.2), rng.uniform(800, 1000), 0.8), N(t), rng.uniform(0.6, 1.0))
        door = sfx.strike(N(0.3), rng, sfx.wood_modes(rng, 120, 8, 0.05), 0.4, noise_mix=0.2)
        dsp.place(y, dsp.lp(door, 1500), N(t + 0.005), 0.7)
        t += rng.uniform(0.12, 0.17)
    return fin(y, rng, 0.5, 0.14)


@sound("Items/lockpick", desc="picking a lock: scratches, tension ticks, pins setting", **ONE)
def lockpick(rng):
    n = N(1.7)
    y = np.zeros(n)
    e = dsp.env([(0, 0), (0.05, 1), (1.6, 1), (1.7, 0)], n) * np.abs(dsp.smooth_rand(n, rng, 6, False))
    y += 0.25 * sfx.scrape(n, rng, e, 3000, 9000, 900)
    for t in np.sort(rng.uniform(0.05, 1.55, 18)):
        dsp.place(y, dsp.click(N(0.015), rng.uniform(4000, 7000), 6, 0.0015, rng), N(t), rng.uniform(0.15, 0.45))
    for t in (0.42, 0.8, 1.15, 1.5):  # pin sets
        dsp.place(y, small_metal(rng, N(0.1), rng.uniform(2200, 2800), (0.01, 0.03)), N(t), 0.8)
    return fin(y, rng, 0.3, 0.06)


# --------------------------------------------------------------------------------------------- tools
@sound("Items/crowbar_pry", desc="crowbar wedged in, wood strains and nails squeal", **ONE)
def crowbar_pry(rng):
    n = N(2.2)
    y = np.zeros(n)
    dsp.place(y, sfx.metal_hit(N(0.6), rng, 520, 12, (0.1, 0.4), 0.8), 0, 0.8)  # wedge in
    dsp.place(y, sfx.strike(N(0.3), rng, sfx.wood_modes(rng, 150, 8, 0.05), 0.6), 0, 0.6)
    m = N(1.5)
    rate = np.interp(T(m), [0, 0.8, 1.5], [18, 45, 70])
    strain = sfx.creak(m, rng, rate, kind="wood", base=380, amp_env=dsp.env([(0, 0), (0.3, 0.7), (1.5, 1)], m),
                       jitter=0.35)
    dsp.place(y, strain, N(0.25), 0.8)
    m = N(0.5)
    nail = sfx.creak(m, rng, np.linspace(250, 420, m), kind="metal", base=1900,
                     amp_env=dsp.env([(0, 0), (0.1, 1), (0.5, 0)], m), jitter=0.1)
    dsp.place(y, nail, N(1.35), 0.6)
    crack = sfx.strike(N(0.3), rng, sfx.wood_modes(rng, 180, 9, 0.04), 1.0, noise_mix=0.6)
    dsp.place(y, dsp.norm(crack), N(1.75), 0.9)
    dsp.place(y, sfx.grains(N(0.3), rng, np.linspace(3000, 0, N(0.3)), 800, 5000, (0.001, 0.004)), N(1.76), 0.4)
    return fin(dsp.sat(dsp.norm(y), 1.5), rng, 0.6, 0.15)


@sound("Items/wood_break", desc="boards cracking and splintering", **ONE)
def wood_break(rng):
    n = N(1.5)
    y = np.zeros(n)
    crack = sfx.strike(N(0.4), rng, sfx.wood_modes(rng, 140, 10, 0.06), 1.0, noise_mix=0.8)
    dsp.place(y, dsp.norm(crack) + 0.7 * sfx.thud(N(0.4), rng, 90, 0.05, 0.6), 0, 1.0)
    dens = 4000 * np.exp(-T(N(0.6)) / 0.12)
    dsp.place(y, sfx.grains(N(0.6), rng, dens, 600, 5000, (0.001, 0.006)), N(0.005), 0.7)
    dsp.place(y, sfx.strike(N(0.3), rng, sfx.wood_modes(rng, 200, 8, 0.05), 0.8), N(0.09), 0.5)
    for t, f in ((0.45, 170), (0.62, 210)):  # pieces land
        k = sfx.strike(N(0.35), rng, sfx.wood_modes(rng, f, 8, 0.06), 0.6, noise_mix=0.3)
        dsp.place(y, dsp.norm(k), N(t), 0.45)
    return fin(dsp.sat(dsp.norm(y), 1.8), rng, 0.6, 0.15)


@sound("Items/bolt_cut", desc="bolt cutters: squeeze, violent snap, ring", **ONE)
def bolt_cut(rng):
    n = N(0.9)
    y = np.zeros(n)
    m = N(0.22)
    strain = sfx.creak(m, rng, np.linspace(40, 90, m), kind="metal", base=1100,
                       amp_env=dsp.env([(0, 0), (0.2, 1), (0.22, 0)], m))
    dsp.place(y, strain, 0, 0.3)
    snap = sfx.strike(N(0.6), rng, dsp.metal_modes(1350, 12, rng, tau=(0.05, 0.25)), 1.0, noise_mix=0.7)
    snap += dsp.pad(dsp.hp(rng.standard_normal(N(0.01)), 2000) * np.linspace(1, 0, N(0.01)), N(0.6)) * 1.5
    dsp.place(y, dsp.norm(snap), N(0.22), 1.0)
    dsp.place(y, latch_clack(rng, N(0.25), 600), N(0.3), 0.5)  # handles slam together
    return fin(dsp.sat(dsp.norm(y), 1.6), rng, 0.45, 0.12)


@sound("Items/chain_drop", desc="heavy chain slides off and drops", **ONE)
def chain_drop(rng):
    n = N(1.4)
    y = np.zeros(n)
    t = T(n)
    dens = 140 * np.exp(-t / 0.35) * (t < 0.9)
    pos = np.nonzero(rng.uniform(size=n) < dens / SR)[0]
    for p in pos:
        link = small_metal(rng, N(0.15), rng.uniform(1500, 2600), (0.01, 0.06), 6, 0.9)
        dsp.place(y, dsp.norm(link), p, rng.uniform(0.2, 0.8))
    dsp.place(y, sfx.thud(N(0.4), rng, 75, 0.05, 0.5, noise=0.6), N(0.04), 0.9)
    dsp.place(y, sfx.thud(N(0.4), rng, 90, 0.04, 0.4, noise=0.6), N(0.22), 0.5)
    return fin(y, rng, 0.5, 0.12)


@sound("Items/soundmeter_tick", desc="sound meter needle tick + tiny blip", **ONE)
def soundmeter_tick(rng):
    n = N(0.09)
    y = sfx.strike(n, rng, [(3100, 0.006, 1), (5200, 0.004, 0.5)], 0.95, noise_mix=0.3)
    b = dsp.square(2200, N(0.018), 0.5) * np.hanning(N(0.018))
    dsp.place(y, dsp.lp(b, 6000), N(0.008), 0.25)
    return dsp.lofi(dsp.norm(y), 9, 1.6, 1.0, 9000)


@sound("Items/pills", desc="pill bottle rattle + cap pop", **ONE)
def pills(rng):
    n = N(1.0)
    y = np.zeros(n)
    for st_ in (0.0, 0.28):
        m = N(0.2)
        dens = 600 * dsp.env([(0, 0), (0.03, 1), (0.08, 0.3), (0.12, 1), (0.2, 0)], m)
        g = sfx.grains(m, rng, dens, 2000, 5500, (0.002, 0.006))
        body = dsp.reson(g, 1250, 4) * 2.5
        dsp.place(y, g + body, N(st_), 1.0)
    m = N(0.25)
    twist = sfx.creak(m, rng, np.full(m, 60.0), kind="wood", base=1800, amp_env=dsp.env([(0, 0), (0.1, 1), (0.25, 0)], m))
    dsp.place(y, twist, N(0.6), 0.25)
    pop = sfx.strike(N(0.2), rng, [(1500, 0.02, 1), (2600, 0.015, 0.6), (700, 0.02, 0.5)], 0.9, noise_mix=0.4)
    dsp.place(y, dsp.norm(pop), N(0.85), 0.8)
    return fin(y, rng, 0.35, 0.08)
