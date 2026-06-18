# Paper 2 Lab 3 Tier C — Eval-parameter cache hit vs miss, eShop (Release)

**Commit:** public Puppeteer `b42d0f7`, built Release. `DOTNET_TieredCompilation=0`.
**Instrument:** end-to-end Stopwatch per iteration + isolated eval-recompile ticks via the public
`LabInstrumentation.OnEvalCompileElapsedTicks` hook.
**Environment:** i9-13900, 64 GB, Windows 11 (26200), .NET 9.0.14.
**Method:** outer script `{ authNumber = (int)(evalParam + @counter); print authNumber 'auth'; }`,
eval target `@facade.NextOrderSequence()`. Stable regime = identical eval text (cache hit);
mutating regime = distinct eval text per iteration (cache miss + recompile).

## Headline numbers

| N | hit (stable) p50 | miss (mutating) p50 | ratio |
|---:|---:|---:|---:|
| 100 | 0.50 µs | 233 µs | 466× |
| 1000 | 0.60 µs | 222 µs | 370× |

The synthetic eval-complexity sweep (`data/paper2-synthetic/`) confirms the same separation across
sub-program sizes: 2-term 0.50 µs / 214 µs / 427×; 50-term 0.60 µs / 243 µs / 405×.

## What this confirms

§5.3: the amortization principle extends recursively to any region with a stable textual identity.
The miss-to-hit ratio is ~400× regardless of sub-program complexity — nearly three orders of
magnitude. (Earlier Debug figures: hit 1.7–2.2 µs, miss 223–263 µs, ~120×; the faster Release hit
widens the ratio. Superseded by this Release run.)
