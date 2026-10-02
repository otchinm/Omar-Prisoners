# Audio pipeline: The Prisoners of Omar

This folder holds every sound in `Assets/PrisonersOfOmar/Resources/Audio/**` except the user's own files,
which are copied verbatim. The sounds are synthesised with numpy and scipy, or derived from the user's
scream recordings, then encoded to Ogg Vorbis with ffmpeg. The target style is Puppet Combo: loud, harsh,
lo-fi, VHS-degraded, and still intelligible.

```
python3 Tools/AssetPipeline/audio/build_audio.py              # rebuild everything (~1 min on 4 cores)
python3 Tools/AssetPipeline/audio/build_audio.py --only Steps/  # iterate on a subset (no manifest rewrite)
python3 Tools/AssetPipeline/audio/build_audio.py --jobs 1       # single process
```

Requirements: python3, `pip install -r requirements.txt` (numpy, scipy), and `ffmpeg` with libvorbis on
PATH. The script exits non-zero if QA fails.

## Determinism

Each sound is seeded from its own path (`crc32("Steps/wood_1")`), so adding or removing a sound never
changes the others. ffmpeg runs with `-fflags +bitexact -flags:a +bitexact -map_metadata -1`, so a rebuild
on the same machine produces byte-identical files.

## Files

| file | contents |
|---|---|
| `build_audio.py` | Entry point. Renders all registered sounds in parallel, copies the user files, runs QA and writes the manifest. |
| `dsp.py` | DSP toolkit: shaped and circular noise, RBJ biquads and time-varying filters, oscillators, modal synthesis, stick-slip friction, saturation, bitcrush, sample-and-hold, wow and flutter, STFT whisperiser, phase vocoder, granular, convolution reverb (also circular), BS.1770 loudness, limiter, compressor. |
| `sfx.py` | Building blocks: thuds, modal strikes, wood and metal modes, creaks, scrapes, cloth, whooshes, bubbles and liquids, drips, formant breath and voice, radio colouring, heartbeat. |
| `common.py` | Registry (`@sound("Category/name", ch=, sr=, loop=, norm=)`), user-source loading, finishing and normalisation, Vorbis encode, decode-back verification. |
| `gen_steps.py` | `Steps/*`: 9 surfaces x 4 variants, plus Omar's boots. |
| `gen_ui.py` | `UI/*`: VCR mechanics (clunks, motors, capstan, rewind whine) and menu blips. |
| `gen_ambience.py` | All `Ambience/*_loop` (beds, static, chase, anomaly, machines, breath). |
| `gen_oneshots.py` | Ambience one-shots (creaks, distant screams, thumps, scrapes, whispers, thunder, drips, phone, dog). |
| `gen_player.py`, `gen_items.py`, `gen_world.py`, `gen_omar.py`, `gen_stingers.py` | The remaining categories. |
| `qa.py` | Parses the Audio section of `Docs/ASSETS.md`, checks every file exists, flags silent, clipped or seamed files, and writes the manifest. |
| `manifest.md` / `manifest.csv` | Generated report: duration, channels, sample rate, decoded peak, LUFS, spectral centroid, loop flag and seam metric, size. |

## Format and level conventions

* **Mono 44.1 kHz** for anything that may be played in 3D: steps, items, world, Omar, player, ambience
  one-shots and positional emitters such as `generator_loop`, `tv_static_loop` and `omar_breath_loop`.
  The radio sounds (`radio_*`) are deliberately **22.05 kHz**.
* **Stereo** for 2D content: UI, ambience beds (`amb_*`), `static*`, `chase_loop`, `anomaly_loop`,
  `wind_gust_loop` and all stingers.
* One-shots are peak normalised to about -1 dBFS. Impact-heavy Omar sounds (`cleaver_hit_*`, `trap_snap`,
  `hiding_rip`), `explosion` and the stingers use the `loud` mode: limited and dense, at about -0.5 dBFS
  peak.
* Loops are loudness normalised:

  | loops | integrated loudness |
  |---|---|
  | ambience beds | -18 to -20 LUFS |
  | `amb_menu_loop` | -25 LUFS (sits under the menu music) |
  | `static_loop` | -17 LUFS |
  | `static_heavy_loop` | -13 LUFS |
  | `chase_loop` | -13.5 LUFS |
  | emitters | -15 to -19 LUFS |

* **Seamless loops.** Every loop is built circularly, with no crossfade:
  * noise is generated in the frequency domain over exactly the loop length;
  * LFOs and tones are quantised to an integer number of cycles;
  * events wrap around the end;
  * IIR filters run over two periods and keep the second;
  * reverbs use circular convolution;
  * resampling for 22.05 kHz uses the Fourier method.

  The manifest's `loop_seam` is the jump at the wrap divided by the 90th percentile sample step. Values
  are about 0 to 1.5, so the wrap looks like any other sample step.
* **Lo-fi colour** (`dsp.lofi`): soft saturation, sample-and-hold rate reduction (aliasing on purpose),
  10 to 12 bit quantisation and tape HF loss. The static and glitch assets go down to 5 to 8 bits.

## Sounds derived from the user's recordings

| derived sound | user source and treatment |
|---|---|
| `Ambience/distant_scream_1` | `scream_1`, pitched, band-limited, given an outdoor reverb and an echo |
| `Ambience/distant_scream_2` | `scream_3`, heard through a wall |
| `Ambience/distant_scream_3` | `screams_long` |
| `Ambience/whisper_1` | `scream_4`, phase-vocoder stretched, then turned into noise with the scream's spectral envelope (STFT whisperiser) plus formant "syllables" |
| `Ambience/whisper_2` | `screams_long`, same treatment, reversed |
| `Omar/growl_1` | `scream_2`, about an octave down, plus a sub-octave layer, vocal-fry gating, saturation and a burlap-sack muffle filter |
| `Omar/growl_2` | `screams_long`, same treatment |
| `Omar/grab`, `Omar/hiding_rip`, `Omar/cleaver_swing_2` | short "Omar voice" grunts made the same way |
| `Player/hurt_1..3`, `Player/gasp`, `Player/struggle` | short scream fragments pitched down 3 to 6 semitones, with breath noise |
| `Stingers/sting_spotted`, `sting_jumpscare`, `sting_capture`, `sting_death`, `sting_ending_bad`, `static_burst_3`, `vhs_glitch_2` | distorted, folded, pitched or varispeed scream layers |
| `Ambience/anomaly_loop`, `Stingers/sting_anomaly_1`, `sting_anomaly_2` | reversed reverb swells and granular clouds of the screams |

## Notes for the AudioManager and import settings (owned elsewhere)

* Loops are sample-exact (the Ogg granule length equals the rendered length) and need no crossfade.
* `Stingers/heartbeat_fast` is also seamless: 8 beats at 150 bpm, so it can be looped while the player
  panics.
* Suggested import settings:

  | assets | load type |
  |---|---|
  | long beds and stingers (more than about 5 s) | Streaming or Compressed In Memory |
  | short one-shots | Decompress On Load |

  The mono files need no "Force To Mono".
