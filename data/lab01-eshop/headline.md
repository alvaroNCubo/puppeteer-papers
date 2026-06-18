# Paper 2 Lab 1 — Compiled vs interpreted speedup (Release / BenchmarkDotNet)

**Commit:** public Puppeteer `b42d0f7` (github.com/alvaroNCubo/puppeteer), built Release.
**Instrument:** BenchmarkDotNet v0.14, steady-state job, `DOTNET_TieredCompilation=0`
(fully-optimized delegate from the first measured invocation, no Tier-0→Tier-1 bimodality).
8 warm-up + 15 measured iterations × 20,000 invocations each; mean ± 99.9% CI half-interval.
**Environment:** i9-13900 (24C/32T), 64 GB, Windows 11 (26200), .NET 9.0.14 RyuJIT AVX2.
**Harness:** `puppeteer-papers/labs/lab01-bdn-speedup/`. Full BDN report (md/csv/html + environment
block) in `bdn-b42d0f7/`.

## Headline numbers

| Workload | Interpreted | Compiled | Speedup |
|---|---:|---:|---:|
| Arithmetic depth-100 (DSL-bound) | 3.03 µs | 0.99 µs | **3.07×** |
| DSL-rich ~500 ops (for/if/arith) | 15.42 µs | 6.05 µs | **2.55×** |
| eShop `Order` production verb (domain-bound) | 1.87 µs | 1.17 µs | **1.59×** |

99.9% CI half-interval within ±3% of each mean.

## What this confirms

- The §2.1/§5.2 amortization claim, monotonic in DSL-bound work: the more of the per-invocation
  work is DSL-bound, the larger the speedup; the more domain-bound, the smaller. Compilation
  amortizes only AST-traversal overhead; host-language work runs identically in both modes.
- The eShop production verb (single dispatch into rich host code) sits at the domain-bound end
  at 1.59×; the synthetic arithmetic kernel at the DSL-bound end at 3.07×.

## Provenance note

Under Debug builds (neither path JIT-optimized) the same curve spans 1.49×–4.10×; the Release
figures above are the defensible ones and the curve's direction is unchanged. Earlier Debug CSVs
were superseded by this Release run and removed.
