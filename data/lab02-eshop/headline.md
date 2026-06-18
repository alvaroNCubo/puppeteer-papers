# Paper 2 Lab 2 — Cold compile cost, eShop production verb (Release)

**Commit:** public Puppeteer `b42d0f7`, built Release. `DOTNET_TieredCompilation=0`.
**Instrument:** the duration of `programExpression.Compile()` isolated at its call site
(`Program.cs:163`) via the public `LabInstrumentation.OnCompileElapsedTicks` hook + Stopwatch —
the appropriate instrument for a single hundreds-of-µs event.
**Environment:** i9-13900, 64 GB, Windows 11 (26200), .NET 9.0.14.
**Method:** N=100 distinct script variants (`_seq_i = i;` prefix) force a cache miss → one cold
`Compile()` per variant.

## Headline numbers

Cold compile of the eShop `Order.CompleteOrder` verb (single DSL dispatch → small expression tree):

| p50 | p95 | mean |
|---:|---:|---:|
| **380 µs** | 516 µs | 400 µs |

Break-even vs the §5.2 per-invocation steady-state saving for the same verb (0.70 µs/invocation):
**≈ 540 invocations** to amortize the cold compile — within the first moments of any actor that
outlives its warm-up.

## What this confirms

§2.1/§5.2: specialization is paid once at first encounter, not per invocation, and recovers itself
quickly. The synthetic compile-cost-by-depth sweep (`data/paper2-synthetic/`) shows the cost grows
~linearly with statement count (≈39 µs/statement, 0.56 ms at depth 5 → 4.30 ms at depth 100); the
production verb is cheap to compile precisely because it is a single dispatch.

(Earlier Debug figure was 281 µs / ~156-invocation break-even; superseded by this Release run.)
