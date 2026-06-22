# Paper 3 — Reaction measurements (raw data)

Measurements for *Reactions and the partition* (Paper 3), taken against the
public Puppeteer runtime at commit **`6a529c4`** (the paper's Code Provenance),
with the harness in `labs/lab03-reactions`.

Environment: Intel Core i9-13900 (32 logical / 24 physical), .NET 9.0.14 (X64
RyuJIT AVX2), Windows 11 build 26200, Workstation GC; Release;
`DOTNET_TieredCompilation=0`.

## Figures

- **Verb dispatch + persist** (in-memory, minimal verb) — BenchmarkDotNet,
  `bdn-6a529c4/`. This report: **mean ≈ 0.33 µs** (median 0.33 µs); an earlier
  run on the same machine gave ≈ 0.40 µs. The per-op time sits near
  BenchmarkDotNet's resolution floor (it warns the minimum iteration time is
  very small), so the figure varies run-to-run within ≈ 0.3–0.4 µs; Paper 3
  cites it to one significant figure as **≈ 0.4 µs**. The load-bearing point —
  sub-microsecond, orders of magnitude under any millisecond budget — holds
  across runs (§5.2, §6.9).

- **Cue end-to-end latency + exactly-once** — `cue-6a529c4.txt`. Over 2000
  freshly-activated Cue activations: **median ≈ 1.35 ms, p99 ≈ 1.9 ms**, 0
  misses; **exactly-once per match held** (0 double-fires). Signal-driven path
  (the catch-up poll waits on `pushSignal`), matching Paper 3 §6.1/§6.9
  (≈ 1.3 ms).

- **Catch-up delivery across restart** — `catchup-6a529c4.txt`. 50 actors with
  a match pre-journaled before activation: 0 misses, 0 double-fires, 0 re-fires
  across a simulated restart — **PASS** (the at-least-once / no-re-fire property
  of §3.6).

## Contents

- `bdn-6a529c4/` — BenchmarkDotNet verb-latency report (`-report.csv`, `.md`,
  `.html`) and run log.
- `cue-6a529c4.txt` — Cue latency + exactly-once probe output.
- `catchup-6a529c4.txt` — catch-up / no-re-fire probe output.
- `lab03-reactions-src/` — the harness source (build against a clone of
  `https://github.com/alvaroNCubo/puppeteer` checked out at `6a529c4`).
