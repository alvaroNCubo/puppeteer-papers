# Paper 7 F3 — Captured asciinema cast files (character-level)

**Captured 2026-05-17** via the `paper7-capture` sidecar pattern:
asciinema running inside a Linux container with the host's docker
socket mounted, recording `docker logs -f ordering-{a,b,c}` from
the host's running demo containers.

This directory is under `docker/build/` and is **gitignored** —
content here is the editor's input, not part of the repo.

## Directory structure (2×2 matrix + orthogonal property)

The cast files are organised by the cell of the Paper 7 2×2 evidence
matrix they witness, plus one directory for the orthogonal
rehydration property:

```
docker/build/casts/
├── in-process/                ← row 1 of the 2×2 matrix (dotnet test)
│   ├── inline.cast            ← ThreeStages_..._HappyPath
│   └── parametric.cast        ← ThreeStages_..._HappyPath_Parametric
├── standard/                  ← row 2 col 1 (cross-container × inline)
│   ├── ordering-a.cast
│   ├── ordering-b.cast
│   └── ordering-c.cast
├── parametric/                ← row 2 col 2 (cross-container × parametric)
│   ├── ordering-a.cast
│   ├── ordering-b.cast
│   └── ordering-c.cast
├── rehydrate/                 ← orthogonal: rehydration from local journal
│   ├── ordering-a.cast
│   ├── ordering-b.cast        ← canonical Video 3 material
│   └── ordering-c.cast
└── simplex/                   ← orthogonal: transport substitution (SMP)
    └── (not yet captured — see "Transport substitution" section below)
```

All cells above were captured against the **Kestrel/HTTPS peer
transport**, which is the default §5 baseline. The 2026-05-17
asciinema batch — `standard/`, `parametric/`, `rehydrate/`, plus the
in-process row — is HTTPS-only by design; the orthogonal SimpleX
cell (`simplex/`) is documented empirically but its asciinema casts
are still pending capture (see below).

## File inventory

### In-process row (1 process, 3 Stages in memory)

| Path | Bytes | Wall clock | Contents |
|---|---:|---:|---|
| `in-process/inline.cast` | 745 | ~1.9 s | `dotnet test` output for `ThreeStages_DirectorRotation_AllConverge_HappyPath`. 1 test passed. |
| `in-process/parametric.cast` | 756 | ~2.0 s | `dotnet test` output for `ThreeStages_DirectorRotation_AllConverge_HappyPath_Parametric`. 1 test passed. |

These wrap the `dotnet test` log files through `cat` inside the
sidecar for visual uniformity with the cross-container casts.

### Cross-container × inline (3 Dockers + `run-demo.sh`)

| Path | Bytes | Wall clock | Final entry |
|---|---:|---:|---:|
| `standard/ordering-a.cast` | 2 833 | ~6.3 s | 22 |
| `standard/ordering-b.cast` | 2 798 | ~6.3 s | 22 |
| `standard/ordering-c.cast` | 2 843 | ~6.3 s | 22 |

Director rotation a→b→c; 22 = 1 bootstrap + 3 × 7 happy-path scripts.
**Primary material for Videos 1 and 2.**

### Cross-container × parametric (3 Dockers + `run-demo.sh --parametric`)

| Path | Bytes | Wall clock | Final entry |
|---|---:|---:|---:|
| `parametric/ordering-a.cast` | 2 864 | ~6.7 s | 7 |
| `parametric/ordering-b.cast` | 2 847 | ~6.7 s | 7 |
| `parametric/ordering-c.cast` | 2 869 | ~6.7 s | 7 |

Director rotation a→b→c with parametric scripts; 7 = 1 bootstrap +
3 × (1 Define + 1 Invocation) per round. Same convergence
property, different journal density (3.1× compaction vs inline).
**Closes the 2×2 matrix; primary material for the Appendix B-roll.**

### Orthogonal rehydration property (`run-demo.sh --rehydrate-demo`)

| Path | Bytes | Wall clock | Contents |
|---|---:|---:|---|
| `rehydrate/ordering-a.cast` | 2 813 | ~5.9 s | `ordering-a` during the rehydrate run. Bystander node. |
| `rehydrate/ordering-b.cast` | 3 088 | ~4.7 s | **Canonical Video 3 material.** Container stopped, restarted; output contains the rehydration moment: `Pre-existing journal detected` / `Skipping Usher onboarding` / new TLS cert / `Rehydration complete`. |
| `rehydrate/ordering-c.cast` | 2 818 | ~5.9 s | `ordering-c` during the rehydrate run. Bystander node. |

The rehydration property is **orthogonal to the 2×2 matrix** — it is
not a fifth cell, it is a property that holds *within* a cell
(cross-container × inline as captured here, but equally so under
parametric or in-process; the wire-up landed in `PuppeteerHost` so
the property is on the host runtime, not the workload regime).

asciinema v2 format. JSON header + ndjson events `[time_delta, "o", "text"]`.

### Orthogonal transport substitution (SimpleX peer transport)

**Empirical cell exists; asciinema casts pending.** A second
empirical observation, run on **2026-05-26** in a separate lab
session, witnesses §6's transport-pluggability claim: the same §5
three-node Docker scenario, with the peer transport switched from
Kestrel/HTTPS to SimpleX SMP queues, reaches the same convergence
checkpoint with the same final journal entry (22) as the HTTPS
variant — i.e., **bit-equivalent journals under transport
substitution**.

The lab is reproducible from this repo at public commit `b42d0f7`
via the new orchestrator flag:

```bash
bash docker/run-demo.sh --simplex
```

`--simplex` brings up an additional `smp-server` container
(`simplexchat/smp-server:latest`), captures its TOFU fingerprint
from its startup log line (`Fingerprint: ...`), and exports it to
the three `ordering-*` containers via `PUPPETEER_SMP_FINGERPRINT`.
The containers then call `ConfigureTransport(TransportType.SimpleX,
"smp-server:5223", serverFingerprint: ...)` instead of the default
`ConfigureTransport(TransportType.Https, ...)`. The bootstrap
rendezvous and Director-rotation rounds proceed unchanged — only
the underlying queue/channel kind differs.

| Path | Status |
|---|---|
| `simplex/ordering-{a,b,c}.cast` | **Not yet captured** |

The 2026-05-17 asciinema batch (above) ran exclusively over HTTPS;
the SimpleX path required the `docker-compose.yml` additions,
`PuppeteerHost` env-var parameterisation, and `run-demo.sh
--simplex` flag that landed in the 2026-05-26 lab session. To add
SimpleX casts to the screencast asset pool, rerun `bash
docker/run-demo.sh --simplex` inside the `paper7-capture` sidecar
— the sidecar incantation is unchanged from the HTTPS captures;
only the demo flag is new. The bit-equivalence claim of §6 is
journal-level (same final entry, same content), so the visual
material from the existing `standard/` casts already conveys the
"3-node convergence" beat; a SimpleX panel would add cross-
transport contrast but is not load-bearing for the §6 claim itself.

Like rehydration, transport substitution is **orthogonal to the
2×2 matrix** — it is a property that holds *within* a cell
(cross-container × inline as observed on 2026-05-26), not a fifth
cell. The transport is a property of the host runtime
composition, not the workload regime, so the same orthogonality
extends in principle to the parametric and in-process cells as
well; only the cross-container × inline × SimpleX cell has been
exercised so far.

## GIF renders (sibling to each `.cast`)

Each `.cast` has a corresponding `.gif` rendered via
[`agg`](https://github.com/asciinema/agg) 1.8.1 (installed via
`winget install asciinema.agg`). The `.gif` lives in the same
directory as its source `.cast` with the same stem:

| Cell | GIFs | Total size |
|---|---|---:|
| In-process × inline | `in-process/inline.gif` | 19 KB |
| In-process × parametric | `in-process/parametric.gif` | 20 KB |
| Cross-container × inline | `standard/ordering-{a,b,c}.gif` | ~1.2 MB |
| Cross-container × parametric | `parametric/ordering-{a,b,c}.gif` | ~1.2 MB |
| Rehydration (orthogonal) | `rehydrate/ordering-{a,b,c}.gif` | ~840 KB |

Total: 11 GIFs, ~3.7 MB. Suitable for attachment to a paper draft
as ancillary material.

The in-process GIFs are small because the source casts wrap brief
`dotnet test` output (~10 lines, ~2 s wall clock) through `cat`,
producing a single burst frame. The cross-container GIFs are larger
because each captures the full 6 s+ container output (onboarding,
mesh setup, 3 rotation rounds, convergence).

`rehydrate/ordering-b.gif` (47 KB) is notably smaller than its
siblings (~400 KB) because the cast was produced from a buffer-
flush after `docker compose start`; everything appears in one burst,
which compresses well in GIF.

### Re-rendering

To re-render one GIF:

```powershell
agg <input.cast> <output.gif>
```

(From PowerShell, where `agg` is on PATH via winget.) To re-render
all 11 from Git Bash:

```bash
AGG="/c/Users/alvar/AppData/Local/Microsoft/WinGet/Packages/asciinema.agg_Microsoft.Winget.Source_8wekyb3d8bbwe/agg.exe"
for cast in docker/build/casts/*/*.cast; do
    "$AGG" "$cast" "${cast%.cast}.gif"
done
```

### Optional flags worth knowing

- `--theme <name>` — colour palette (e.g. `monokai`, `solarized-dark`).
  Default is fine for terminal-faithful rendering.
- `--font-size <pt>` — default 14; bump if the paper figure needs to
  read at small zoom.
- `--speed <factor>` — playback speed multiplier. `--speed 1.5` is
  useful for the in-process GIFs to give them visible motion.
- `--cols <N> --rows <M>` — overrides cast's terminal dimensions.
  Useful if the source recording was wider than fits in the paper
  figure.

The 11 GIFs in this directory are all rendered with **defaults** —
no flags. The editor can re-render with custom flags for the final
paper or the consolidated video.

## How the sidecar capture works

The Windows host cannot run asciinema directly (no `fcntl`). Instead:

1. A Linux sidecar image (`paper7-capture`) is built locally:
   `python:3.12-slim` + `docker:cli` (multi-stage copy) + `pip install
    asciinema`. Built once; tag `paper7-capture`.
2. The sidecar runs with `/var/run/docker.sock` mounted, so its
   `docker` CLI talks to the host's daemon.
3. Three background bash loops in the sidecar wait for each
   `ordering-X` container to appear, then run
   `asciinema rec -c "docker logs -f ordering-X"`. The loop re-arms
   asciinema whenever the container restarts (needed for the
   `--rehydrate-demo` scenario where ordering-b stops and restarts).
4. The demo runs from the host normally (`bash docker/run-demo.sh`).
5. Final `docker compose down -v` kills the loops; the .cast files
   are flushed to the host filesystem via the volume mount.

The cleanup-cycle of part numbering produces `ordering-b-part0`,
`-part1`, `-part2`, `-part3` files in raw form. For `--rehydrate-demo`
the canonical record is `-part2` (post-restart, contains the full
historical log including the rehydration moment), and the rest are
truncated by `docker stop` / final teardown. Renamed to plain
`ordering-b.cast` for editor consumption.

## Per-video mapping

### Videos 1 and 2 (Quad 2×2 / Tri-row layouts)

Source: `standard/ordering-{a,b,c}.cast`.

These three casts share a wall-clock origin (each container's
asciinema started just before `docker compose up` brought it
online; the `timestamp` field in each cast header is the Unix
seconds of when its recording began).

| Cast | header.timestamp | first-event delta | rough offset against ordering-a |
|---|---|---|---|
| `standard/ordering-a.cast` | 1779029173 | 0.029 s | baseline |
| `standard/ordering-b.cast` | 1779029173 | 0.029 s | ≈ 0 |
| `standard/ordering-c.cast` | 1779029173 | 0.029 s | ≈ 0 |

All three started within the same wallclock second — the editor can
align them on the playhead with no per-panel offset.

### Video 3 (Journal-view custom)

Primary source: `rehydrate/ordering-b.cast`.

This single cast carries the entire Video 3 narrative arc. Substrings
to look for (each occurs exactly once):

| Substring | Approx. cast time | Storyboard beat |
|---|---|---|
| `[host] Onboarding via Usher` | ~0.02 s | First bootstrap |
| `[host] Onboarded as stageId=...` | ~0.02 s | Identity assignment |
| `=== Round 1/3: Director = a ===` | ~0.02 s | Rotation start |
| `=== Round 3/3: Director = c ===` | ~0.02 s | Rotation end |
| `convergence checkpoint reached` | ~0.02 s | Pre-stop steady state |
| `Pre-existing journal detected at /data` | ~0.02 s | **Rehydration begins** |
| `Skipping Usher onboarding; rehydrating Stage` | ~0.02 s | The §"design property" claim |
| `Stage started; local TLS fingerprint: edd...` | ~0.02 s | Fresh TLS cert, same identity |
| `Rehydration complete. Journal at entry 22` | ~0.02 s | **Canonical landing** |

*(All deltas read ~0.02 s because the cast captures `docker logs`
dumping the historical buffer at start-up after `docker compose
start`; the per-line timing inside the dump is dictated by how
fast `docker logs` flushes its buffered output, not by the
original real-time pacing of the events. The wall-clock pacing
of the original events lives in the `ordering-b.log` file under
`docker/build/logs/rehydrate/`, with ISO 8601 timestamps to the
nanosecond — pair the two for fidelity if the storyboard requires
real-time playback.)*

Supporting context for Video 3 (the topology overlay and the
"two-up, one-down" beat):

- `rehydrate/ordering-a.cast` — same content as standard run; shows
  `a` operating throughout.
- `rehydrate/ordering-c.cast` — same; shows `c` operating throughout.
- `rehydrate/orchestrator.log` (under `docker/build/logs/`) —
  contains the `docker compose stop ordering-b` and `docker compose
  start ordering-b` commands with timestamps; useful as the cue
  for when the topology diagram's `b` node turns grey and back to
  green in post.

## Replay / convert to other formats

asciinema cast files can be:

- **Played in a terminal**: `asciinema play standard/ordering-a.cast`
  (requires asciinema installed locally; not feasible on Windows
  without the sidecar, but viable on macOS/Linux/WSL Ubuntu).
- **Converted to GIF**: `agg standard/ordering-a.cast a.gif` (where
  `agg` is the [asciinema gif generator](https://github.com/asciinema/agg)).
- **Converted to SVG**: `svg-term --in standard/ordering-a.cast --out
  a.svg` (npm package `svg-term-cli`).
- **Embedded on web**: asciinema-player.js renders the .cast file
  natively in HTML.
- **Imported into video editors**: render via `agg` → GIF → drag
  into DaVinci/Premiere/Final Cut as an image sequence or video
  clip.

The recommended editor pipeline for the Paper 7 videos:

1. `agg --speed 1.0 standard/ordering-a.cast standard/ordering-a.gif`
   (and same for b, c).
2. Drag the three GIFs into the editor as separate panels.
3. Compose the Quad 2×2 / Tri-row / Journal-view layout per the
   shot list in `notes/paper7_phase3_shotlist.md`.
4. Overlay the mesh-view elements (Director badge, entry counter,
   beat phrase, topological diagram) in post.
5. Drop the QR PNGs (`docker/build/share-link-{a,b,c}.png`) as
   cutaways during the opening rhetorical move.
6. Record voiceover separately and lay it over.

## Reproducing this capture

The sidecar image is built locally; rebuild if it disappears:

```bash
docker build -t paper7-capture -f docker/Dockerfile.capture docker/
```

The full capture run (both standard and rehydrate) is documented
inline in the script `docker/run-demo.sh` (the demo orchestrator,
unchanged from the runtime-fixes branch) plus the sidecar
incantations in this session's transcript. Time budget on warm
Docker: ~35 seconds per run, ~70 seconds total for both runs.

## Limitations honest

- **Same-second wall-clock origin only.** asciinema cast `timestamp`
  field is integer Unix seconds; the three per-container recordings
  all begin within the same second, but if the rotation rounds
  drift, the editor will need the line-level `--timestamps` from
  the `.log` files (also captured) for sub-second sync.
- **rehydrate/ordering-b.cast event-delta is buffer-flush time, not
  original real-time pacing.** Use `docker/build/logs/rehydrate/ordering-b.log`
  for the real-time deltas of the rehydration beat (ISO 8601 stamps
  to the nanosecond).
- **No screen capture of the operator's terminal.** Just container
  output. The QR PNGs are separate assets; the orchestrator's
  meta-output is in `docker/build/logs/{standard,rehydrate}/orchestrator.log`.
- **No voiceover, no audio.** Recorded separately, layered in post.
