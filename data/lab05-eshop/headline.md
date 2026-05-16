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
| Persistence anchor | journaled actor-native (no RDBMS) | EF Core / RDBMS |

The eShop number is dramatically smaller, but the right reading of *why* is not "eShop is better-designed and ***REDACTED*** is bloated." The compactness of eShop's `Ordering.Domain` is **a consequence of its persistence anchor, not a property of its authors' diligence**. Marks visible directly in the source:

- A comment in `Order.cs` declares `Address` *"a Value Object pattern example persisted as EF Core 2.0 owned entity"* — the ORM is in the designer's mind at the moment the value object is shaped.
- Every property is `private set` (a shape required for EF hydration, not for domain reasons).
- References are RDBMS-style `int? BuyerId` / `int? PaymentId` foreign keys, not object references; the choice anticipates a row-and-column store.
- `_orderItems` is a single-level collection; deeper graphs would multiply the serialization surface and are avoided.
- No polymorphism on `Order` itself; the RDBMS would handle it poorly, so the design preempts the cost.

***REDACTED***/***REDACTED*** was developed under journaled actor-native persistence — the journal of invocations is the persistent thing; the aggregate's runtime state is intermediate. With no serialization boundary pressing on it, the domain grew the polymorphism, the depth, and the cross-class behavior the problem actually has (`Coinage`, `Movements`, `***REDACTED***`, multi-tiered authorizations, etc.).

**The β/α magnitude is therefore not a measurement of either framework's verb-richness mechanism; it is a measurement of how much the host's persistence anchor compresses the domain it represents.** Same DDD style in both cases; different shape because different anchor.

This reframing is parked as a paper-wide methodological observation in `project_puppeteer_persistence_anchor_shapes_ddd.md` (firmed 2026-05-16). It connects to Paper 1's porosity formalism (eShop's `Ordering.Domain` is a paradigmatic high-Π case) and to Paper 2's unified principle (the magnitudes we measure in labs are consequences of upstream structural choices, not of the runtime we are characterizing).

## What this confirms

- **α — Determinism of DSL dispatch count**: 9 dispatches with zero variance across 1,000 invocations. The DSL surface is a deterministic, statically-knowable quantity per script; this is the property §3.1 leans on for caching.
- **β/α asymmetry direction**: even on a structurally compact domain, the host call graph reachable from a DSL invocation is several times larger than the DSL surface itself (2.7× here). The §4 Beat 2 claim — *"the verb's depth is structurally richer than its surface signature"* — holds; the magnitude is a function of the domain's own depth, not of the framework.

## What this does NOT confirm

- **"Orders of magnitude" framing of §4 Beat 3 (***REDACTED***'s 700× / 87× depending on overload-discount)**: not reachable on eShop's `Ordering.Domain` alone, because the domain has been pre-compressed by its persistence anchor. A reviewer pointing at this number would be observing a fact about RDBMS-anchored DDD, not about Puppeteer.

  **Interpretation for the paper**: the §4 claim about magnitudes must be stated as a function of how much the host's persistence model has compressed the domain, not as an absolute. eShop demonstrates the floor of the mechanism on a domain that has been flattened for the ORM; ***REDACTED*** demonstrates the ceiling on a domain that has not. The mechanism is identical; the observable magnitude is a downstream signal of an upstream architectural commitment.

## Integration text for Paper 2 §4 / §5

> *"For the eShop Order production verb, the runtime's interpreter dispatches 9 host-language entry points per invocation (exactly reproducible across 1,000 measurements). Static forward closure of the call graph from those 9 entry points reaches 24 methods within `dotnet-eShop/src/Ordering.Domain`. The β/α asymmetry of 2.7× is smaller than the original ***REDACTED*** verb's 700×–870× — but not because the eShop reference codebase is "better designed" than the ***REDACTED*** domain. Visible marks in `Order.cs` (`private set` everywhere, an explicit `EF Core 2.0 owned entity` annotation on `Address`, FK-style `int?` references on `BuyerId` and `PaymentId`, no polymorphism on Order) show that the eShop domain was shaped from the start by the anticipation of RDBMS persistence; that anticipation flattens polymorphism, caps graph depth, and externalizes references as foreign keys. ***REDACTED***'s ***REDACTED***, developed under journaled actor-native persistence with no such pressure on the domain, grew to the depth the problem actually has. The β/α magnitude is therefore a function of how much the host's persistence anchor has compressed its domain, not of the mechanism the runtime applies once a verb is invoked."*

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
