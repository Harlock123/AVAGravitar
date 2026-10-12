#!/usr/bin/env python3
"""Synthesizes the game's sound effects into ../Sounds as 16-bit mono WAV files.

Nothing is sampled from the original machine: every effect is built here from oscillators and noise,
so the sounds can be tweaked and regenerated. Run it from anywhere: python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Sounds")
random.seed(1982)


def write(name, samples, volume=0.6):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def n(seconds):
    return int(RATE * seconds)


def env(i, total, attack=0.005, release=1.0):
    """Linear attack, then a decay shaped by `release` (higher = faster fade)."""
    t = i / RATE
    a = min(1.0, t / attack) if attack > 0 else 1.0
    return a * (1 - i / total) ** release


def sweep(seconds, f0, f1, wave_fn, release=1.0, attack=0.005):
    total, phase, out = n(seconds), 0.0, []
    for i in range(total):
        f = f0 + (f1 - f0) * i / total
        phase += f / RATE
        out.append(wave_fn(phase) * env(i, total, attack=attack, release=release))
    return out


def square(p):
    return 1.0 if (p % 1) < 0.5 else -1.0


def tri(p):
    return 4 * abs((p % 1) - 0.5) - 1


def saw(p):
    return 2 * (p % 1) - 1


def sine(p):
    return math.sin(2 * math.pi * p)


def noise(seconds, release=1.0, smooth=0.0, attack=0.002):
    total, last, out = n(seconds), 0.0, []
    for i in range(total):
        last = last * smooth + random.uniform(-1, 1) * (1 - smooth)
        out.append(last * env(i, total, attack=attack, release=release))
    return out


def mix(*tracks):
    length = max(len(t) for t in tracks)
    return [sum(t[i] for t in tracks if i < len(t)) for i in range(length)]


def scale(track, k):
    return [s * k for s in track]


def silence(seconds):
    return [0.0] * n(seconds)


os.makedirs(OUT, exist_ok=True)

# The ship's gun: a quick, falling zap.
write("Fire.wav", sweep(0.12, 1800, 500, square, release=1.5), 0.25)

# A bunker or saucer shot: a lower, buzzier blip.
write("EnemyFire.wav", sweep(0.16, 700, 260, saw, release=1.2), 0.18)

# A bunker, tank or saucer blown up.
write("Boom.wav", mix(noise(0.7, release=2.0, smooth=0.5), scale(sweep(0.7, 160, 40, square, release=1.8), 0.5)), 0.55)

# The ship lost: a longer crash with a falling tone through it.
write("ShipBoom.wav", mix(noise(1.6, release=1.4, smooth=0.65), scale(sweep(1.6, 420, 30, saw, release=1.3), 0.5)), 0.75)

# A planet destroyed: a deep, rolling blast.
total = n(2.4)
roll, last, deep = [], 0.0, 0.0
for i in range(total):
    t = i / RATE
    last = last * 0.75 + random.uniform(-1, 1) * 0.25
    deep = deep * 0.96 + random.uniform(-1, 1) * 0.04
    shape = min(1, t / 0.03) * math.exp(-t * 1.3)
    roll.append((0.5 * last + 4 * deep + 0.4 * math.sin(2 * math.pi * (60 - 20 * t) * t)) * shape)
write("PlanetBoom.wav", roll, 0.85)

# The reactor going up: bigger and longer still.
total = n(3.5)
roar, last, deep = [], 0.0, 0.0
for i in range(total):
    t = i / RATE
    last = last * 0.8 + random.uniform(-1, 1) * 0.2
    deep = deep * 0.97 + random.uniform(-1, 1) * 0.03
    shape = min(1, t / 0.02) * math.exp(-t * 0.9)
    roar.append((0.6 * last + 5 * deep + 0.5 * math.sin(2 * math.pi * 40 * t)) * shape)
write("ReactorBoom.wav", roar, 0.95)

# The core hit: a metallic clang.
clang = mix(*(scale(sweep(0.8, f, f * 0.98, sine, release=2.5), k) for f, k in ((310, 1), (467, 0.7), (733, 0.5), (1210, 0.3))))
write("CoreHit.wav", mix(clang, scale(noise(0.2, release=3), 0.5)), 0.6)

# A fuel tank pulled aboard: a bubbling rise.
total = n(0.6)
glug, phase = [], 0.0
for i in range(total):
    t = i / RATE
    f = 300 + 900 * t / 0.6 + 120 * math.sin(2 * math.pi * 18 * t)
    phase += f / RATE
    glug.append(tri(phase) * env(i, total, attack=0.01, release=0.8))
write("Fuel.wav", glug, 0.35)

# Every bunker on a planet down: a bright arpeggio.
notes = []
for f in (392, 523, 659, 784, 1047):
    notes += sweep(0.08, f, f, square, release=0.4)
notes += sweep(0.3, 1319, 1319, square, release=1)
write("Cleared.wav", notes, 0.25)

# Diving into a planet: a long falling swoop. Climbing out: a rising one.
write("Enter.wav", mix(sweep(0.9, 1400, 120, tri, release=0.9, attack=0.05), scale(noise(0.9, release=1, smooth=0.9, attack=0.1), 0.4)), 0.35)
write("Exit.wav", mix(sweep(0.7, 150, 1300, tri, release=0.9, attack=0.05), scale(noise(0.7, release=1, smooth=0.9, attack=0.1), 0.4)), 0.35)

# The warp between systems: a rising, wavering sweep.
total = n(3.2)
warp, phase = [], 0.0
for i in range(total):
    t = i / RATE
    f = 80 * (2 ** (t * 2.2)) * (1 + 0.05 * math.sin(2 * math.pi * 9 * t))
    phase += f / RATE
    warp.append((saw(phase) * 0.5 + tri(phase * 1.5) * 0.5) * min(1, t / 0.2) * min(1, (3.2 - t) / 0.4))
write("Warp.wav", warp, 0.25)

# A new system: a short, eerie space theme on two voices.
melody = [(330, 0.18), (494, 0.18), (440, 0.18), (659, 0.36), (587, 0.18), (494, 0.18), (523, 0.18), (440, 0.5)]
tune = []
for f, d in melody:
    tune += mix(sweep(d, f, f, square, release=0.6), scale(sweep(d, f / 2, f / 2, tri, release=0.4), 0.8))
write("Tune.wav", tune, 0.22)

# An extra ship: a quick ringing chime.
chime = []
for f in (1047, 1319, 1568, 2093):
    chime += sweep(0.07, f, f, sine, release=0.5)
write("ExtraShip.wav", chime + sweep(0.3, 2093, 2093, sine, release=1.2), 0.35)

# Fuel running low: two short alarm beeps.
beep = sweep(0.1, 880, 880, square, release=0.4)
write("LowFuel.wav", beep + silence(0.08) + beep, 0.2)

# Game over: a slow, falling three-note line.
over = []
for f in (392, 330, 262):
    over += mix(sweep(0.35, f, f * 0.99, square, release=0.7), scale(sweep(0.35, f / 2, f / 2, tri, release=0.7), 0.7))
write("GameOver.wav", over + sweep(0.8, 196, 180, tri, release=1.2), 0.3)

# ----- Loops: whole cycles so they repeat without a click -----

# Thrust: a low, rough rocket roar.
total = n(1.0)
roar, last = [], 0.0
for i in range(total):
    t = i / RATE
    last = last * 0.85 + random.uniform(-1, 1) * 0.15
    roar.append(last * 3 + 0.3 * math.sin(2 * math.pi * 50 * t))
write("Thrust.wav", roar, 0.3)

# Shield and tractor: a humming buzz with a fast flutter.
total = n(1.0)
hum = []
for i in range(total):
    t = i / RATE
    hum.append((square(120 * t) * 0.4 + math.sin(2 * math.pi * 240 * t) * 0.6) * (0.7 + 0.3 * math.sin(2 * math.pi * 30 * t)))
write("Shield.wav", hum, 0.12)

# A saucer: the classic rising-and-falling warble.
total = n(1.0)
warble, phase = [], 0.0
for i in range(total):
    t = i / RATE
    f = 900 + 300 * math.sin(2 * math.pi * 4 * t)
    phase += f / RATE
    warble.append(math.sin(2 * math.pi * phase))
write("Saucer.wav", warble, 0.1)

# The reactor alarm: a two-tone siren.
total = n(1.0)
siren = []
for i in range(total):
    t = i / RATE
    f = 660 if (t % 0.5) < 0.25 else 880
    siren.append(square(f * t))
write("Alarm.wav", siren, 0.13)

print("Wrote sounds to", os.path.normpath(OUT))
