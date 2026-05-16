# Paper 2 Lab 2 — Cold-compile cost on eShop Order production verb (replay)

**Date:** 2026-05-16
**Branch:** `lab-replay/02-eshop` (from `lab-replay/01-eshop`) in `Puppeteer Pacifico`.
**Runtime config:** .NET 9.0.312 SDK, Debug build, in-memory Diary (default), single-thread bench, Windows 11 host.
**Dataset:** `tier3-cold-20260516T174939Z-64040f5.csv` (100 rows).

## Methodology

Bootstrap-then-distinct-variants pattern. The actor's program cache keys on script
string identity, so each variant script with a unique sequence-variable prefix forces
a cache miss → fresh `programExpression.Compile()` invocation. The Pacifico-side
instrumentation `LabInstrumentation.OnCompileElapsedTicks` (subscribed in `Program.cs`
at the `_executable = programExpression.Compile()` line, mod 9 of this branch)
captures the wall-clock duration of that compile call only — not the surrounding
parse / SolveReferences / execute work.

### Bootstrap (1×, untimed, callback not yet subscribed)

```
f = OrderingFacade();
```

### Measurement variants (N=100, distinct script strings)

```
_seq_0 = 0; o = f.CompleteOrder('user-1', 'Bench User', 0, 10.0, 1);
_seq_1 = 1; o = f.CompleteOrder('user-1', 'Bench User', 1, 10.0, 1);
...
_seq_99 = 99; o = f.CompleteOrder('user-1', 'Bench User', 99, 10.0, 1);
```

Each variant is structurally identical (same statement count, same domain dispatch
shape) but lexically distinct → guaranteed cache miss → Pacifico falls into the
`_executable == null` branch of `ExecuteExpression`, calls `programExpression.Compile()`,
fires `OnCompileElapsedTicks` with the elapsed ticks.

### Bench loop

- One long-lived `ActorV2` with `CompiledModePolicy = AlwaysCompiled`.
- Bootstrap runs **before** the callback is subscribed → its own cold compile is
  excluded from the dataset.
- 100 distinct variant scripts, each via `actor.Using(script).PerformCommand()`.
- Each call deterministically produces one cold-compile event (verified by
  `Assert.AreEqual(100, compileTicks.Count)`).

## Headline numbers (N=100)

| Statistic | Cold compile (µs) |
|---|---:|
| p50 | **281.3** |
| p95 | 394.4 |
| p99 | 805.1 |
| mean | 299.0 |

## Cross-reference: cold-compile cost vs steady-state speedup (break-even)

Lab 1 Run 3 eShop measured the steady-state per-invocation delta of compiled over
interpreted execution at **1.8 µs at p50** (compiled 3.7 µs vs interpreted 5.5 µs).
The cold compile is recovered after:

> 281.3 µs / 1.8 µs·invocation⁻¹ = **≈ 156 invocations**

A production actor serving regular traffic amortizes its eShop-verb compile
investment within the first ~150 calls — well under a second of operation.

## Comparison with the prior production e-commerce system (Tier 3 original)

| Metric | Prior production system (original) | eShop CompleteOrder (this) |
|---|---:|---:|
| Cold compile p50 | 1,417 µs | **281 µs** (~5× faster) |
| Cold compile p95 | 1,824 µs | 394 µs |
| Steady-state delta (Lab 1) | 40 µs/invocation | 1.8 µs/invocation |
| Break-even invocations | ~35 | ~156 |

eShop CompleteOrder is structurally simpler than the prior production verb (fewer
cascaded domain calls, shorter AST), so both the cold-compile cost and the
steady-state delta are smaller. The break-even point shifts higher because the
per-invocation savings shrink faster than the up-front cost. Both numbers tell the
same operational story: the cold-compile investment is recovered within fractions
of a second of real load.

## What this confirms

- **The amortization argument of §2.1 / §3.1**: cold-compile cost is bounded (sub-ms
  for an eShop production verb) and recovered within ~150 calls against the same
  parametric script.
- **"The compilation itself is one line" (§3.1 Beat 2)**: the measured cost is the
  duration of `programExpression.Compile()` in isolation, captured at the exact call
  site (`Program.cs:162`). No surrounding parse / dispatch work contaminates the
  measurement.
- **Domain-independent shape**: same pattern as the original Tier 3 on the prior
  production system (cold compile bounded, break-even within seconds), against a
  structurally distinct host codebase.

## What this does NOT (yet) confirm

- **Curve across program sizes**: the original Lab 2 measured three tiers
  (small/large arithmetic, production verb). The synthetic arithmetic tiers from the
  original Lab 2 (~815 µs small / ~4,118 µs large) are publishable as-is — they have
  no ***REDACTED*** dependency. This replay only covers the production-verb tier on
  eShop.

## Integration text for Paper 2 §5

> *"On a cold cache, compiling the runtime program for the eShop Order production
> verb (one DSL dispatch cascading through `Order.NewOrder` 10-arg ctor, three
> `AddOrderItem` calls, and four state-machine transitions) takes 281 µs at p50,
> 394 µs at p95 (N=100 distinct variants, fresh cache miss per variant). Against
> the 1.8 µs per-invocation savings that compiled execution achieves over
> interpreted on the same verb (§5.1 / Lab 1 Run 3 eShop), this cost is recovered
> after approximately 156 invocations. A production actor serving regular traffic
> amortizes its compile investment within the first second of operation. The
> structural property — bounded one-time cost, linear amortization in invocation
> count — holds independently of business domain, with the same shape observed on
> the original production verb measured on a prior commercial e-commerce system."*

## Modifications to Pacifico applied in this branch (heredables, +1 on top of `lab-replay/01-eshop`)

| Mod | File | Change |
|---|---|---|
| 9 | `Puppeteer/EventSourcing/Interpreter/Libraries/Program.cs:162` | Wrap `_executable = programExpression.Compile()` with conditional `Stopwatch.StartNew()` + `LabInstrumentation.OnCompileElapsedTicks?.Invoke(sw.ElapsedTicks)`. Production code path unaffected (callback null → no Stopwatch allocation). |

Mod 8 (`LabInstrumentation.cs` with `OnCompileElapsedTicks`) is **already in master
upstream** (absorbed from Paper 5 work). No file creation needed.

Inherited mods from `lab-replay/01-eshop` and `lab-replay/00-harness-setup`: mods 2,
3, 5, 6 (CompilationModePolicy enum + Actor.CompiledModePolicy field public,
Parameters indexer public, DomainLibraries ReflectionTypeLoadException tolerance).

UnitTestPuppeteer suite verified green 768/768 post-mod 9 (excluding
Bench/Integration/FlakyInCI).

## Files produced

- `puppeteer-papers/data/lab02-eshop/tier3-cold-20260516T174939Z-64040f5.csv` —
  per-variant compile-ticks dataset.
- `puppeteer-papers/data/lab02-eshop/headline.md` — this file.
- `Puppeteer Pacifico/UnitTestEShopOnPuppeteer/Lab02ColdCompileEShopBench.cs` — bench
  class, `TestCategory("Bench")`.
