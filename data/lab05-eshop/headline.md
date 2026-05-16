# Paper 2 Lab 5 — Operations-per-verb on eShop (replay)

**Date:** 2026-05-16
**Branch (α):** `lab-replay/05-eshop` (from `lab-replay/04-eshop`) in `Puppeteer Pacifico`.
**β tool:** `puppeteer-papers/labs/lab05-eshop-roslyn/` (console app, .NET 9).
**Datasets:** `alpha-20260516T212456Z-8d565d4.csv` (1000 rows, runtime counter), `beta-roslyn-20260516T212649Z-8d565d4.csv` (24 rows, static walker).

## Methodology

Two complementary measurements, each captures a different facet of "operations-per-verb":

- **α — DSL dispatch count (runtime)**: Pacifico-side `LabCounter.Increment()` fires from inside `DotAccess.Execute()` and `NewInstance.Execute()` (the AST nodes that dispatch into the host language). Reset per iteration; the value reported is the exact number of host-language entry points the interpreter walked for one execution of the measurement script. Runs in interpreted mode only — compiled mode emits IL that bypasses `Execute()`, but the AST shape (and thus α) is mode-invariant.

- **β — host call graph closure (static)**: Roslyn purely-syntactic forward closure from the unique entry points α touches, restricted to source files under `dotnet-eShop/src/Ordering.Domain`. Walker indexes method/constructor/property declarations by simple name; traverses InvocationExpressionSyntax + ObjectCreationExpressionSyntax + MemberAccessExpressionSyntax; filters trivial accessors. Result is a structural ceiling — no overload disambiguation, no virtual-dispatch resolution.

### Measurement script (canonical eShop production verb, same as Lab 4)

```
o = f.NewSubmittedOrder(uid, uname);
o.AddOrderItem(pid1, name1, price1, discount, picUrl, units);
o.AddOrderItem(pid2, name2, price2, discount, picUrl, units);
o.AddOrderItem(pid3, name3, price3, discount, picUrl, units);
o.AddOrderItem(pid4, name4, price4, discount, picUrl, units);
o.SetAwaitingValidationStatus();
o.SetStockConfirmedStatus();
o.SetPaidStatus();
o.SetShippedStatus();
```

### β entry points (unique methods on `Order` / `Address`)

| Class | Method / ctor | Kind |
|---|---|---|
| Order | `Order(...)` 10-arg | Constructor |
| Address | `Address(...)` 5-arg | Constructor |
| Order | `AddOrderItem` | Method |
| Order | `SetAwaitingValidationStatus` | Method |
| Order | `SetStockConfirmedStatus` | Method |
| Order | `SetPaidStatus` | Method |
| Order | `SetShippedStatus` | Method |

(The DSL invokes `f.NewSubmittedOrder` which is a facade-side wrapper in the test assembly that calls the `Order` 10-arg ctor + `Address` ctor — those are the actual eShop-domain seeds.)

## Headline numbers

### α — DSL dispatch count (runtime, interpreted)

| Metric | Value |
|---|---:|
| Dispatches per invocation | **9** |
| Variance across N=1,000 | **zero** (all 1,000 measurements equal 9) |

The 9 dispatches map exactly to the 9 DSL statements:

| # | DSL site | Dispatch site |
|---:|---|---|
| 1 | `f.NewSubmittedOrder(uid, uname)` | DotAccess |
| 2–5 | `o.AddOrderItem(...)` × 4 | DotAccess × 4 |
| 6–9 | `o.Set{AwaitingValidation,StockConfirmed,Paid,Shipped}Status()` × 4 | DotAccess × 4 |

### β — Host call graph closure (static)

| Metric | Value |
|---|---:|
| Entry points (after deduplication / class-hint matching) | 9 |
| Methods reached via forward closure | **24** |
| Trivial accessors skipped | 3 |

Breakdown by depth:

| Depth | Methods |
|---:|---:|
| 0 | 9 (entry seeds) |
| 1 | 12 |
| 2 | 3 |
| ≥3 | 0 |

Breakdown by kind:

| Kind | Count |
|---|---:|
| Constructor | 13 |
| Method | 11 |

### β / α ratio

> **β / α = 24 / 9 ≈ 2.7×**

## Comparison with ***REDACTED*** (original)

| Metric | ***REDACTED*** (original) | eShop CompleteOrder (this) |
|---|---:|---:|
| α (DSL dispatches) | 8 | **9** |
| β (host methods reachable) | ~6,977 | **24** |
| β / α | ~872× | **2.7×** |
| Source files (in domain assembly) | hundreds (***REDACTED*** commercial) | 23 (Ordering.Domain DDD) |
| Total declarations indexed | tens of thousands | 95 |

The eShop number is **dramatically smaller**, and that is the honest measurement: eShop's `Ordering.Domain` is a structurally compact DDD aggregate (the canonical Microsoft reference for a clean order-management bounded context), not a sprawling commercial engine. The fact that the forward closure from the DSL entry points reaches only ~24 methods is *informative about the domain's size, not about Puppeteer's verb-richness mechanism*. ***REDACTED***'s 6,977 reflected the depth of a multi-decade commercial codebase; eShop's 24 reflects the deliberate scope of an open-source DDD demo.

## What this confirms

- **α — Determinism of DSL dispatch count**: 9 dispatches with zero variance across 1,000 invocations. The DSL surface is a deterministic, statically-knowable quantity per script; this is the property §3.1 leans on for caching.
- **β/α asymmetry direction**: even on a structurally compact domain, the host call graph reachable from a DSL invocation is several times larger than the DSL surface itself (2.7× here). The §4 Beat 2 claim — *"the verb's depth is structurally richer than its surface signature"* — holds; the magnitude is a function of the domain's own depth, not of the framework.

## What this does NOT confirm

- **"Orders of magnitude" framing of §4 Beat 3 (***REDACTED***'s 700× / 87× depending on overload-discount)**: not reachable on eShop's `Ordering.Domain` alone, because the domain itself is compact. A reviewer pointing at this number would be observing a fact about the eShop reference codebase, not about Puppeteer.

  **Interpretation for the paper**: this is the *floor* the mechanism delivers on a deliberately compact domain. The ***REDACTED*** numbers represent the same mechanism operating on a deep domain. The §4 claim about magnitudes should be stated as a function of domain depth, not as an absolute.

## Integration text for Paper 2 §4 / §5

> *"For the eShop Order production verb, the runtime's interpreter dispatches 9 host-language entry points per invocation (exactly reproducible across 1,000 measurements). Static forward closure of the call graph from those 9 entry points reaches 24 methods within `dotnet-eShop/src/Ordering.Domain` — a β/α asymmetry of approximately 2.7×, smaller than the original ***REDACTED*** verb's 700×–870× because the eShop `Ordering.Domain` is a deliberately compact DDD reference codebase (23 source files, 95 total declarations) rather than a deep commercial engine. The structural property — DSL surface < reachable host graph — holds in both directions; the magnitude is a function of domain depth, not of the mechanism."*

## Modifications to Pacifico applied in this branch (+2 on top of `lab-replay/04-eshop`)

| Mod | File | Change |
|---|---|---|
| 11b | `Puppeteer/EventSourcing/Interpreter/Libraries/DotAccess.cs` | `LabCounter.Increment()` inserted at start of `Execute()`. Covers DotAccess + ChainedDotAccess + DottedId via inheritance. |
| 12b | `Puppeteer/EventSourcing/Interpreter/Libraries/NewInstance.cs` | `LabCounter.Increment()` inserted at start of `Execute()`. |

(Mod 10 — `Puppeteer/LabCounter.cs` declaration — is **already in master upstream** from Paper 5 work; no file creation needed.)

Inherited mods from prior branches: 2, 3, 5, 5b, 6, 9, 11, 12.

UnitTestPuppeteer suite verified green 768/768 post-mods.

## Files produced

- `puppeteer-papers/data/lab05-eshop/alpha-20260516T212456Z-8d565d4.csv` — α dataset (1000 rows: iteration, dispatch_count).
- `puppeteer-papers/data/lab05-eshop/beta-roslyn-20260516T212649Z-8d565d4.csv` — β dataset (24 rows: depth, kind, namespace, class, method, file, line, source_caller).
- `puppeteer-papers/data/lab05-eshop/headline.md` — this file.
- `Puppeteer Pacifico/UnitTestEShopOnPuppeteer/Lab05OpsPerVerbEShopBench.cs` — α bench class.
- `puppeteer-papers/labs/lab05-eshop-roslyn/` — β walker (Program.cs + csproj).
