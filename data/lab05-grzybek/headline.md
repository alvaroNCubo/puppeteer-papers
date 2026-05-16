# Paper 2 Lab 5 — Operations-per-verb on Grzybek SubscriptionPayment (replica)

**Date:** 2026-05-16
**Branch (α):** `lab-replay/grzybek-replica` in `Puppeteer Pacifico`.
**β tool:** `puppeteer-papers/labs/lab05-grzybek-roslyn/` (.NET 9 console).
**Datasets:** `alpha-20260516T214533Z-ebf289d.csv` (1000 rows, runtime counter), `beta-roslyn-20260516T220712Z-ebf289d.csv` (73 rows, static walker).

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

## Comparison with eShop and with the prior production system

| Metric | Prior production system (original) | eShop Order (this series) | Grzybek SubscriptionPayment (this) |
|---|---:|---:|---:|
| α (DSL dispatches) | 8 | 9 | **2** |
| β (host methods reachable) | ~6,977 | 24 | **73** |
| β / α | ~872× | 2.7× | **36.5×** |
| Source declarations indexed | tens of thousands | 95 | 275 |
| Persistence anchor | journaled actor-native | EF Core / RDBMS | EF Core / RDBMS |

Two observations from the three-way comparison:

1. **The structural property holds in both direction and magnitude across all three host codebases.** DSL surface < reachable host graph in every case. The mechanism is not specific to any particular domain.

2. **The Grzybek number sits between eShop and the prior production system, in a way that confirms the persistence-anchor reframing** (firmed 2026-05-16 in `project_puppeteer_persistence_anchor_shapes_ddd.md`): both Grzybek and eShop are RDBMS-anchored DDD, but Grzybek's `Payments.Domain` carries the shared `BuildingBlocks/Domain` (its `AggregateRoot`, `ValueObject`, `BusinessRuleValidationException` infrastructure) into the closure. The 36× vs eShop's 2.7× is not an indictment of eShop — it is a reflection of how deep the *included* code base is once the shared seedwork is traversed alongside the aggregate. eShop's `Ordering.Domain` is its own self-contained project (its `SeedWork/` folder is internal); Grzybek's `Payments.Domain` references the shared `BuildingBlocks/Domain` and the walker traverses both.

   Neither structure is "right" or "wrong" — they reflect two valid DDD packaging choices. The 2.7× and 36× brackets the range a paper would honestly report for "small DDD aggregates with RDBMS anchors": between a few times the DSL surface and a few dozen. Compared to the unbounded-domain prior production system's 700×–870×, both remain orders of magnitude below the ceiling.

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
