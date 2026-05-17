# Paper 2 Lab 5 — Operations-per-verb on Grzybek SubscriptionPayment (replica)

**Date:** 2026-05-16
**Branch (α):** `lab-replay/grzybek-replica` in the Puppeteer runtime repository (private; to be released alongside the runtime).
**β tool:** `puppeteer-papers/labs/lab05-grzybek-roslyn/` (.NET 9 console) — **public**, in this repository.
**Datasets:** `alpha-20260516T220730Z-ebf289d.csv` (1000 rows, runtime counter), `beta-roslyn-20260516T220712Z-ebf289d.csv` (73 rows, static walker).

> *The α bench source lives in the Puppeteer runtime repository, which is currently internal and will be released alongside the runtime itself. The β walker source is published in this repository (`labs/lab05-grzybek-roslyn/`) and is fully reproducible against `kgrzybek/modular-monolith-with-ddd` public source. The α CSV in this directory is sufficient to verify the dispatch-count headline; the β walker can be re-run directly.*

## Methodology

Identical to the eShop Lab 5 (`data/lab05-eshop/headline.md`); only the host
codebase, the facade, and the Roslyn root path differ. Two complementary
measurements:

- **α — DSL dispatch count (runtime, interpreted)**: `LabCounter` reset per
  iteration; the value reported is the exact number of host-language entry
  points the interpreter walked through `DotAccess.Execute()` and
  `NewInstance.Execute()` for one execution of the measurement script.

- **β — host call graph closure (static)**: Roslyn forward closure from the
  unique entry points α touches, restricted to source files under
  `kgrzybek-modular-monolith/src/Modules/Payments/Domain` (which includes the
  shared `BuildingBlocks/Domain` referenced by the `Payments.Domain.csproj`).
  Same filtering rules as the eShop walker.

### Measurement script (canonical Grzybek production verb)

```
p = f.NewWaitingPayment(payerGuid, country, periodCode, amount, currency);
p.MarkAsPaid();
```

The DSL invokes the Pacifico-side `SubscriptionPaymentFacade`, which assembles
the value objects (PayerId, SubscriptionPeriod, MoneyValue,
PriceListItemData, DirectValueFromPriceListPricingStrategy, PriceList) and
invokes the static `SubscriptionPayment.Buy(...)` factory — Grzybek's
canonical entry point for the Payments module. The aggregate's lifecycle then
advances via `MarkAsPaid()`.

### β entry points (8 facade-and-domain seeds)

| Class | Method / ctor | Kind |
|---|---|---|
| SubscriptionPayment | `Buy` | static method |
| SubscriptionPayment | `MarkAsPaid` | method |
| PayerId | `PayerId(Guid)` | ctor |
| SubscriptionPeriod | `Of` | static factory |
| MoneyValue | `Of` | static factory |
| PriceListItemData | `PriceListItemData(...)` | ctor |
| PriceList | `Create` | static factory |
| DirectValueFromPriceListPricingStrategy | ctor | ctor |

## Headline numbers

### α — DSL dispatch count (runtime, interpreted)

| Metric | Value |
|---|---:|
| Dispatches per invocation | **2** |
| Variance across N=1,000 | **zero** |

The 2 dispatches map exactly to the 2 DSL statements: `f.NewWaitingPayment(...)`
and `p.MarkAsPaid()`. The Pacifico-side facade absorbs the value-object
assembly into a single host-language method call from the DSL's perspective.

### β — Host call graph closure (static)

| Metric | Value |
|---|---:|
| Entry points (after deduplication) | 8 |
| Methods reached via forward closure | **73** |
| Trivial accessors skipped | 105 |
| Source files indexed | 275 declarations across 118 unique names |

Breakdown by depth (deeper than eShop's max of 2 — Grzybek's domain has
internal call chains through pricing strategies, rules, snapshots, and
shared BuildingBlocks):

| Depth | Methods |
|---:|---:|
| 0 | 8 (entry seeds) |
| 1 | 23 |
| 2 | 27 |
| 3 | 10 |
| 4 | 5 |

### β / α ratio

> **β / α = 73 / 2 ≈ 36.5×**

## Comparison with eShop

| Metric | eShop Order (this series) | Grzybek SubscriptionPayment (this) |
|---|---:|---:|
| α (DSL dispatches) | 9 | **2** |
| β (host methods reachable) | 24 | **73** |
| β / α | 2.7× | **36.5×** |
| Source declarations indexed | 95 | 275 |
| Persistence anchor | EF Core / RDBMS | EF Core / RDBMS |

Two observations from the two-way comparison:

1. **The structural property holds in direction across both host codebases.** DSL surface < reachable host graph in each case. The mechanism is not specific to any particular domain.

2. **The magnitude reflects packaging choice, not framework behaviour.** Both aggregates are RDBMS-anchored DDD, but eShop's `Ordering.Domain` is a self-contained project (its `SeedWork/` folder is internal) while Grzybek's `Payments.Domain` references a shared `BuildingBlocks/Domain` (`AggregateRoot`, `ValueObject`, `BusinessRuleValidationException` infrastructure) that the walker traverses alongside. The 36× vs 2.7× is therefore a reflection of how deep the *included* code base is once shared seedwork is counted alongside the aggregate. Neither structure is "right" or "wrong" — they are two valid DDD packaging choices. The 2.7× and 36× together bracket the range a paper would honestly report for small DDD aggregates with RDBMS anchors: between a few times the DSL surface and a few dozen.

## What this confirms

- **Determinism of α**: 2 dispatches with zero variance across 1,000 invocations. The structural property of statically-knowable DSL surface holds independently of host domain.
- **β / α direction and magnitude across three host codebases**: the asymmetry has the same sign in every case (DSL < graph), and the magnitudes form a meaningful ordering tied to the *packaging shape* and *persistence anchor* of each host, not to any property of Puppeteer's runtime mechanism.
- **Domain-independence**: the same instrumentation, the same bench scaffolding, the same DSL pattern produce coherent measurements against three structurally distinct host codebases.

## Modifications to Pacifico applied in this branch (additive over `lab-replay/05-eshop`)

No new mods. Inherits mods 2, 3, 5, 5b, 6, 9, 11, 12, 11b, 12b from `lab-replay/05-eshop`. The α bench reuses `LabCounter.Increment()` in `DotAccess.Execute()` + `NewInstance.Execute()` (mods 11b + 12b) that the eShop Lab 5 added.

The Phase 2 setup added a new test project (`UnitTestGrzybekOnPuppeteer/`) and a Roslyn walker (`puppeteer-papers/labs/lab05-grzybek-roslyn/`); no further changes to the Puppeteer runtime.

UnitTestPuppeteer suite remains green 768/768.

## Files produced

- `puppeteer-papers/data/lab05-grzybek/alpha-*.csv` — α dataset.
- `puppeteer-papers/data/lab05-grzybek/beta-roslyn-*.csv` — β dataset.
- `puppeteer-papers/data/lab05-grzybek/headline.md` — this file.
- `Puppeteer Pacifico/UnitTestGrzybekOnPuppeteer/Lab05OpsPerVerbGrzybekBench.cs` — α bench.
- `Puppeteer Pacifico/UnitTestGrzybekOnPuppeteer/SubscriptionPaymentFacade.cs` — facade.
- `puppeteer-papers/labs/lab05-grzybek-roslyn/` — β walker.
