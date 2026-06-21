# Lab 3 — Reaction measurements (Paper 3 §5.2, §6.1, §3.6)

Two measurements against the public Puppeteer runtime commit `2f31f96` (Paper 3
provenance):

1. **Verb latency** (`VerbLatencyBench`, BenchmarkDotNet) — the fixed
   dispatch + persist overhead of a `PerformCommand` on a hot, in-memory actor.
   The verb is a minimal domain verb (`ReactionLab.Ping`), so the figure is a
   floor that isolates the actor's own overhead from domain/host work (§5.2).
2. **Cue end-to-end latency + exactly-once** (`CueProbe`, Stopwatch) — the time
   from issuing a command to the `Cue` Reaction's `Program.Emit` body firing,
   and a per-sample check that the Reaction fires exactly once per match
   (§6.1, §3.6, claim 12).

## Contents

- `VerbLatencyBench.cs` — BenchmarkDotNet benchmark for verb latency.
- `CueProbe.cs` — Stopwatch driver for Cue latency. A single-`Seek` Reaction is
  one-shot (fires once per trajectory), so latency is measured across fresh hot
  actors, one timed trigger each; the timed window is `PerformCommand` →
  Reaction firing, with actor/loop setup excluded.
- `ReactionLab.cs` — the domain facade (`ReactionLab`) and the probe
  (`ReactionProbe`/`ProbeState`) the Reaction's emit signals.
- `Program.cs` — entry point. No args → BenchmarkDotNet (verb latency, in-process
  toolchain, `DOTNET_TieredCompilation=0`); `cue <n>` → the Cue latency +
  exactly-once driver; `catchup <n>` → pure catch-up delivery (match
  pre-journaled before activation), a no-loss / no-re-fire check across a
  simulated restart.

## Reproducing

1. Clone the runtime at the cited commit and build Release:
   `git clone https://github.com/alvaroNCubo/puppeteer && cd puppeteer && git checkout 2f31f96`
   `dotnet build Puppeteer/Puppeteer.csproj -c Release`
2. Point this project's `ProjectReference` at your `Puppeteer/Puppeteer.csproj`, then:
   `DOTNET_TieredCompilation=0 dotnet run -c Release -- --filter '*VerbLatencyBench*'`   (verb latency)
   `dotnet run -c Release -- cue 200`                                                     (Cue latency + exactly-once)
   `dotnet run -c Release -- catchup 50`                                                  (catch-up delivery: no-loss / no-re-fire)

The ~1 ms Cue figure below is the same `cue 200` run against commit `a82f67a`
(the scheduling fix described in Paper 3 §6.9) — i.e. check out `a82f67a`
instead of `2f31f96` at step 1. Code anchors and the verb figure are at
`2f31f96`; only the Cue latency was re-measured at `a82f67a`.

## Figures (published in Paper 3)

Environment: Intel Core i9-13900 (32 logical / 24 physical), .NET 9.0.14 (X64
RyuJIT AVX2), Windows 11 build 26200, Workstation GC; Release;
`DOTNET_TieredCompilation=0`.

- Verb dispatch + persist (in-memory, minimal verb): **mean ≈ 0.40 µs**
  (StdDev ≈ 0.01 µs).
- Cue end-to-end latency (freshly-activated Cue, in-memory) **at `2f31f96`**:
  **median ≈ 0.13 s, p99 ≈ 0.15 s** over ~350 samples; exactly-once per match
  held (0 double-fires).
- Cue end-to-end latency **at `a82f67a`** (the scheduling fix): **median ≈ 1 ms,
  p99 ≈ 2 ms** over 200 samples, 0 misses; exactly-once per match held
  (0 double-fires).

Note on the Cue figure: at `2f31f96` a freshly-activated Cue is served by the
catch-up replay poll (`ActorReactions.CanContinueReplay`, a 50 ms → 1000 ms
backoff `Thread.Sleep`), so the ~0.13 s is backoff-bound, not the steady
signal-driven push path (`RunPushLoop` / `EnqueuePushEvent`), which wakes near
instantly. Commit `a82f67a` replaces that `Thread.Sleep` with a signal-aware
`pushSignal.Wait`, so a newly journaled entry preempts the backoff — the ~1 ms
figure. The construct never bounded the latency; it was a scheduling property.

## Implementation notes (runtime constraints at `2f31f96`)

- The Cue pattern matches a **constructor event** (`ReactionLab()`). At this
  commit the matcher does not implement `ChainedDotAccess` (`X().M()`) in
  `PreparePatternMatching`, and does not resolve an instance-variable receiver
  (`p.M()`) when the action script is parsed in isolation.
- The `Program.Emit` body is **block-wrapped** — `{ @probe.Fire(); }` — because a
  query rejects a top-level create/call statement
  (`Parser.ParseCreateOrCallStatement`).
- The probe is injected via the Reaction's `WithParameters` and read as
  `@probe`, since the emit (a query) cannot reference an actor-state global by
  bare name.
