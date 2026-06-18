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
| Arithmetic depth-100 (DSL-bound) | 2.99 µs | 0.97 µs | **3.10×** |
| DSL-rich ~500 ops (for/if/arith) | 15.98 µs | 7.27 µs | **~2.2×** |
| eShop `Order` production verb (domain-bound) | 1.97 µs | 1.31 µs | **1.51×** |
| Flat-CRUD verb (single setter, α=1) — *adversarial* | 0.63 µs | 0.34 µs | **1.86×** |

Within-run 99.9% CI half-interval is a few percent; ratios carry process-to-process variation of
order ±0.2 (e.g. the production verb measured 1.51× here and 1.59× on a prior run), so they are read
to ~1 significant figure of confidence — the ordering, not the third digit.

**Adversarial reading.** The flat-CRUD verb does not collapse to 1×: even a single trivial dispatch
carries ~1.9× of removable DSL-dispatch overhead. The speedup tracks the DSL-dispatch fraction of
per-invocation time, not domain richness — its floor is the host-/I/O-bound regime (the production
verb at ~1.5×), not domain simplicity. Verb richness (β/α), by contrast, *does* collapse on flat
CRUD (α=1, β=1 → 1×); caching and journal density are unaffected (domain-independent).

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
