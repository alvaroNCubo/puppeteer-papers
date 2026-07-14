# Lab 1 — red-black replay (deploy = replay) — headline

**Runtime**: public commit `99e8202` (mirrors Pacifico `4a852ba`), built against the public mirror.
**Date**: 2026-07-13
**Host**: Windows NT 10.0.26200 / 32 logical cores / .NET 9.0.14
**Configuration**: Release / x64 / single-process, FileSystem substrate, `AlwaysCompiled`.
**Measurement**: public-API only — `Stopwatch` around `follower.Start(asFollower: true)` (bulk replay to near-head), the lock/sync tail, and the deploy total. No runtime instrumentation.

## Headline number

Under the compact-action régime with `AlwaysCompiled`, the substrate replays N
journal entries at a sustained **532 thousand entries/s** (p50, N = 100 000),
completing bulk replay in **187.8 ms** (p95 = 190.6 ms). The rate amortises
between N = 10 000 and N = 100 000 and **holds through the N = 1 000 000 anchor at
1.76 million entries/s** — it does not fall as the journal grows, confirming the
cost is linear in compact-event count, not in expanded state. The handover tail
(entries arriving during the pause) is **tens of microseconds** at every N, so
**bulk replay is > 99.9 % of the deploy window** in every cell. All four journal
sizes cross-check PASS (follower state equals leader state entry-for-entry).

## Table — replay rate + deploy window (p50, 3 reps; 1M = single anchor)

| N events | bulk replay p50 (ms) | replay rate p50 (ev/s) | deploy total p50 (ms) | cross-check |
|---------:|---------------------:|-----------------------:|----------------------:|:-----------:|
| 1 000     |   9.69 |   103,182 |   9.70 | PASS |
| 10 000    |  25.73 |   388,656 |  25.74 | PASS |
| 100 000   | 187.85 |   532,342 | 187.87 | PASS |
| 1 000 000 | 567.18 | 1,763,117 | 567.25 | PASS |

Per-repetition rows (incl. `handover_tail_ms`, `journal_bytes`) in `handoffs.csv`;
per-N summary in `summary-full.csv`.
