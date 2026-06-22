# Lab 3 — Reaction measurements (Paper 3 §5.2, §6.1, §3.6)

Two measurements against the public Puppeteer runtime commit `6a529c4` (Paper 3
provenance):

1. **Verb latency** (`VerbLatencyBench`, BenchmarkDotNet) — the fixed
   dispatch + persist overhead of a `PerformCommand` on a hot, in-memory actor.
   The verb is a minimal domain verb (`ReactionLab.Ping`), so the figure is a
   floor that isolates the actor's own overhead from domain/host work (§5.2).
2. **Cue end-to-end latency + exactly-once** (`CueProbe`, Stopwatch) — the time
   from issuing a command to the `Cue` Reaction's `Program.Emit` body firing,
   and a per-sample check that the Reaction fires exactly once per match
   (§6.1, §3.6).

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

1. Clone the runtime at the cited commit (as a sibling of this repo, the
   convention lab01/lab02 use) and build Release:
   `git clone https://github.com/alvaroNCubo/puppeteer && cd puppeteer && git checkout 6a529c4`
   `dotnet build Puppeteer/Puppeteer.csproj -c Release`
2. From this project (its `ProjectReference` points at `..\..\..\puppeteer\Puppeteer\Puppeteer.csproj`):
   `DOTNET_TieredCompilation=0 dotnet run -c Release -- --filter '*VerbLatencyBench*'`   (verb latency)
   `dotnet run -c Release -- cue 2000`                                                    (Cue latency + exactly-once)
   `dotnet run -c Release -- catchup 50`                                                  (catch-up delivery: no-loss / no-re-fire)

## Figures (published in Paper 3)

Environment: Intel Core i9-13900 (32 logical / 24 physical), .NET 9.0.14 (X64
RyuJIT AVX2), Windows 11 build 26200, Workstation GC; Release;
`DOTNET_TieredCompilation=0`.

- Verb dispatch + persist (in-memory, minimal verb): **mean ≈ 0.40 µs**
  (StdDev ≈ 0.01 µs).
- Cue end-to-end latency (freshly-activated Cue, in-memory): **median ≈ 1.3 ms,
  p99 ≈ 2 ms** over 2000 samples, 0 misses; exactly-once per match held
  (0 double-fires).

Note on the Cue figure: at `6a529c4` the catch-up replay poll that serves a
freshly-activated Cue waits on the push signal (`ActorReactions` —
`pushSignal.Wait`), so a newly journaled entry preempts the poll and the
end-to-end figure is the signal-driven path (~1.3 ms). For contrast, a naive
fixed-backoff poll (a 50 ms → 1000 ms `Thread.Sleep`, the design before the
`6a529c4` lineage) measured ~0.13 s on the identical path — a ~100× difference
from scheduling alone. The construct never bounded the latency (Paper 3 §6.9).

## Implementation notes

The lab is written against the matcher as it stands at `6a529c4` and builds and
runs clean there:

- The Cue pattern matches a **constructor event** (`ReactionLab()`).
- The `Program.Emit` body is **block-wrapped** — `{ @probe.Fire(); }` — because a
  query rejects a top-level create/call statement
  (`Parser.ParseCreateOrCallStatement`).
- The probe is injected via the Reaction's `WithParameters` and read as
  `@probe`, since the emit (a query) cannot reference an actor-state global by
  bare name.
