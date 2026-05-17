# Paper 2 Lab 3 Tier C — Eval recompile latency on eShop (replay)

**Date:** 2026-05-16
**Branch:** `lab-replay/03-eshop` in the Puppeteer runtime repository (private; to be released alongside the runtime).
**Runtime config:** .NET 9.0.312 SDK, Debug build, in-memory Diary (default), Windows 11 host.
**Dataset:** `tierC-eval-20260516T175455Z-c915da7.csv` (2,200 rows: 100+1000 × stable + 100+1000 × mutating).

> *Bench source lives in the Puppeteer runtime repository, which is currently internal and will be released alongside the runtime itself. The CSV in this directory is sufficient to reproduce the eval-cache headline ratios from `dotnet/eShop` public source plus an instrumented build of the runtime.*

## Methodology

Mirrors the reference implementation
`UnitTestPuppeteer/PaperLabs/paper2/Lab03_EvalRecompile.cs.Lab03_TierC_Production_StableVsMutating`
structurally. Two regimes are measured against the same outer program for two
sample sizes (N=100, N=1000):

- **stable**: same eval text on every iteration → eval-cache hit, runtime
  invokes the cached delegate directly.
- **mutating**: distinct eval text on every iteration → eval-cache invalidate +
  recompile, runtime walks `CrearProgramaEval` + `programExpression.Compile()`.

The Pacifico-side instrumentation `LabInstrumentation.OnEvalCompileElapsedTicks`
(mod 11 of this branch, wrapping `Program.cs:327`) fires only when the eval cache
misses, so the per-iteration eval-compile cost is captured in isolation from the
end-to-end wall-clock.

### Outer script (constant across iterations)

```
{ authNumber = (int)(evalParam + @counter); print authNumber 'auth'; }
```

### Eval scripts

| Regime | Eval text per iteration |
|---|---|
| stable | `@facade.NextOrderSequence()` (identical every iteration) |
| mutating | `@facade.NextOrderSequence() + <i>` (distinct text per iteration) |

`@facade` is a `OrderingFacade` instance passed as an In parameter; `NextOrderSequence()`
is a small instance method that returns an `int` from an interlocked counter — an
instance-bound method short enough to make the eval-compile cost the dominant term
in the mutating regime.

### Bench loop

- One `ActorV2` per `(regime, N)` cell with `CompiledModePolicy = AlwaysCompiled`.
- Bootstrap (1×, untimed): `f = OrderingFacade();` to seat the actor.
- Warmup (1×, untimed): outer + stable eval → seeds cache.
- N timed iterations: stable reuses cached delegate; mutating triggers
  invalidate-and-recompile each call.

## Headline numbers

| N | regime | end p50 (µs) | end p95 (µs) | eval-compile p50 (µs) | eval-compile p95 (µs) |
|---:|---|---:|---:|---:|---:|
| 100 | stable | 2.20 | 6.60 | — | — |
| 100 | mutating | 263.20 | 413.90 | 189.30 | 281.60 |
| 1000 | stable | **2.10** | 5.30 | — | — |
| 1000 | mutating | **256.90** | 376.10 | 188.50 | 273.90 |

| N | Cache-miss/hit ratio (end p50) |
|---:|---:|
| 100 | **119.64×** |
| 1000 | **122.33×** |

The cache-miss-to-hit ratio sits at ≈ **120×** independent of N. The end-to-end
mutating cost (~257 µs at p50) is dominated by the isolated eval-compile cost (~189 µs
at p50); the remaining ~68 µs is the outer execute path (cast, addition, parameter
lookup, dispatch through the print statement).

## What this confirms

- **§3.1 Beat 3**: *"any region of the program with a stable textual identity admits
  the same treatment as the whole."* The eval-cache hit pays only the cost of
  invoking the cached delegate; the eval-cache miss pays a compile cost that is in
  the same ballpark as the outer Lab 2 cold-compile. Recursive separability
  observed.
- **Structural separation**: the ~2 orders of magnitude separation between
  cache hit and cache miss is the empirical signature of recursive separability.
  The eval-compile cost (~189 µs) dominates the mutating regime's end-to-end
  time, mirroring the relationship between cold compile and steady-state on the
  outer program.

## What this does NOT (yet) confirm

- **Tier A (shallow `1+1`) and Tier B (50-term arithmetic) replay**: not needed for
  Paper 2 — Tier A and Tier B are synthetic and host-independent
  dependency, so they are publishable as-is.
- **Cross-actor eval contention**: this lab uses one actor per cell. Multi-thread
  eval-cache behavior is out of scope.

## Integration text for Paper 2 §5 / §3.1 Beat 3

> *"On an eShop facade-bound eval parameter, the runtime's eval-cache hit pays only
> 2.1 µs at p50 to invoke the cached delegate; on a cache miss, the runtime re-parses,
> rebuilds, and re-compiles the sub-program for 257 µs at p50 (N=1000), of which
> 189 µs at p50 is the isolated `programExpression.Compile()` step (captured directly
> via `LabInstrumentation.OnEvalCompileElapsedTicks`). The cache-miss-to-hit ratio
> stabilizes near 120× across N=100 and N=1000 — a two-orders-of-magnitude separation
> that confirms separability extends recursively: any sub-region of a program with a
> stable textual identity is eligible for the same compile-then-cache treatment as
> the outer program."*

## Modifications to Pacifico applied in this branch (+2 on top of `lab-replay/02-eshop`)

| Mod | File | Change |
|---|---|---|
| 11 | `Puppeteer/EventSourcing/Interpreter/Libraries/Program.cs:327` | Conditional `Stopwatch.StartNew()` around `var executable = programExpression.Compile()` inside `EvaluateEvalParameters`, firing `LabInstrumentation.OnEvalCompileElapsedTicks`. Symmetric to mod 9 (outer compile). |
| 5b | `Puppeteer/Parameters.cs:158` | 3-arg indexer `this[int tipoDeParametro, string parameterName, Type parameterType]` promoted internal → public. Required so external test assemblies can set eval-typed parameters via `p[Parameter.Eval, "name", typeof(T)] = "evalScript";` without InternalsVisibleTo plumbing. The 2-arg indexer was promoted in `lab-replay/01-eshop` (mod 5); the 3-arg form is its parameter-type-aware sibling. |

Mod 10 (`LabInstrumentation.OnEvalCompileElapsedTicks` declaration) is **already in
master upstream** alongside `OnCompileElapsedTicks`. No file creation needed.

Inherited from upstream branches: mods 2, 3, 5, 6, 9.

UnitTestPuppeteer suite verified green 768/768 post-mod 11 + 5b.

## Files produced

- `puppeteer-papers/data/lab03-eshop/tierC-eval-20260516T175455Z-c915da7.csv` —
  per-iteration dataset.
- `puppeteer-papers/data/lab03-eshop/headline.md` — this file.
- `Puppeteer Pacifico/UnitTestEShopOnPuppeteer/Lab03EvalRecompileEShopBench.cs` —
  bench class, `TestCategory("Bench")`.
- `Puppeteer Pacifico/UnitTestEShopOnPuppeteer/OrderingFacade.cs` — extended with
  `NextOrderSequence()` (interlocked-counter helper for the eval target).
