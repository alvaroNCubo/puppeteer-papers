# Paper 2 Lab 1 Run 3 — eShop Order production verb (replay)

**Date:** 2026-05-16
**Branch:** `lab-replay/01-eshop` @ commit `2b47ffb` in `Puppeteer Pacifico` (post-bench-refactor commit forthcoming on same branch).
**Runtime config:** .NET 9.0.312 SDK, Debug build, in-memory Diary (default), single-thread bench, Windows 11 / PowerShell 5.1 host.
**Dataset:** `run3-bootstrap-20260516T171855Z-2b47ffb.csv` (2,000 rows: 1,000 compiled + 1,000 interpreted).

## Methodology

Bootstrap-then-measurement pattern (Lineamiento 3 firmado durante Lab 1 original).
External open-source domain assembly: `eShop.Ordering.Domain.dll` from
[dotnet/eShop](https://github.com/dotnet/eShop) (MIT). Built once with TFM multi-target
`net9.0;net10.0` (additive change to the upstream `Ordering.Domain.csproj`); no
source-level modifications to the aggregate code. Pacifico actor loads the assembly via
its existing reflection-based discovery (`DomainLibraries.LoadTypesFromAssembly`,
which now tolerates `ReflectionTypeLoadException` for transitive package dependencies
of external domain assemblies — mod 6 of this branch).

### Bootstrap script (1×, untimed)

```
f = OrderingFacade();
```

`OrderingFacade` is a Pacifico-side wrapper class in the test assembly that exposes
primitive-typed factory methods over eShop's `Order` aggregate. The bootstrap creates
the facade once and binds the symbol `f` in the actor's persistent symbol table.

### Measurement script (N×, timed)

```
o = f.CompleteOrder(uid, uname, pid, price, units);
```

References `f` already in the symbol table. Per-iteration values for `uid`, `uname`,
`pid`, `price`, `units` are injected via `WithParameters` — the DSL script string is
constant across iterations, so the compiled-program cache hits on every call (the
regime the paper's amortization argument names).

The C# implementation of `OrderingFacade.CompleteOrder` cascades through:

1. `new Address("street","city","state","country","12345")` — value-object construction.
2. `new Order(userId, userName, address, 1, "1234-5678-9012-3456", "123", "Card Holder", DateTime.UtcNow.AddYears(1), null, null)` — 10-arg ctor; sets status to `Submitted`, fires `AddOrderStartedDomainEvent`.
3. `order.AddOrderItem(...)` × 3 — each with the `SingleOrDefault` discount-merge logic and an `OrderItem` allocation.
4. `order.SetAwaitingValidationStatus()` → `SetStockConfirmedStatus()` → `SetPaidStatus()` → `SetShippedStatus()` — state-machine walk, each transition gated by the documented status check and each appending a domain event.

One DSL dispatch (`f.CompleteOrder(...)`) → many host-language method calls — the shape
Paper 2 §4 names "verb richness" and Paper 2 Lab 5 quantifies as DSL surface vs host
call graph.

### Bench loop

- Two cells: `AlwaysCompiled`, `AlwaysInterpreted`.
- Per cell: fresh `ActorV2` instance, then `bootstrap (1×, untimed)` + `warmup (100×, untimed)` + `measure (1000×, Stopwatch per iteration)`.
- Per-iteration values rotate `pid = i` to ensure the C# domain code does real work each call (no constant-fold trivialization).

## Headline numbers (p50, N=1000)

| Mode | p50 | p95 |
|---|---:|---:|
| Interpreted | 5.5 µs | 9.4 µs |
| Compiled | **3.7 µs** | 7.4 µs |
| **Speedup (interp/comp)** | **1.49×** | 1.27× |

Compiled executes the same DSL invocation in approximately two-thirds the wall-clock
time of interpreted execution at the median. The p95 ratio compresses (1.27× vs
1.49× at p50), consistent with the long tail being dominated by host-side work that
both modes incur identically — domain work does not benefit from DSL specialization.

## What this confirms

- **Direction of the §2.1 amortization claim**: compiled path is faster than
  interpreted on a representative production verb against a rich external aggregate.
- **Domain-bound regime**: the eShop CompleteOrder verb sits in the domain-bound end
  of Paper 2's compression curve. With the original production verb from a prior
  e-commerce system at 1.80× speedup and a depth-100 arithmetic script at 4.10×,
  the 1.49× measured here for eShop's CompleteOrder fits the predicted curve —
  speedup compresses as the verb shifts from DSL-bound to domain-bound. eShop's
  CompleteOrder has somewhat fewer cascaded calls than the prior production verb,
  so a marginally lower speedup is expected.
- **Domain-independence**: the structural speedup property holds across a host
  codebase (the open-source eShop reference) distinct from the prior production
  e-commerce system on which Paper 2's methodology was originally developed. The
  argument that compiled execution is a structural consequence (Paper 2 unified
  principle, 2026-05-04) rather than a property of any particular domain gains an
  independent data point.

## What this does NOT (yet) confirm

- **Cold compile cost on eShop**: Lab 2 territory. Not measured here.
- **Verb depth quantification on eShop**: Lab 5 territory (α DSL dispatch counter +
  β Roslyn host call graph). Not measured here.
- **Tier-1 / Tier-2 spectrum on eShop**: the original Lab 1 measured three tiers
  (arithmetic / DSL-rich / production verb). This replay measures only the production
  verb. The arithmetic and DSL-rich tiers from the original Lab 1 already publish as
  synthetic (no ***REDACTED*** dependency) and stand without replay.

## Integration text for Paper 2 §5

> *"For a representative production verb against an open-source e-commerce domain
> aggregate (the `Order.CompleteOrder` facade over the eShop Ordering aggregate, MIT-
> licensed), the runtime's compiled path executes 1.49× faster at p50 than the
> interpreted path (3.7 µs vs 5.5 µs at N=1000 invocations, after 100-iteration
> warmup, fresh actor per mode, in-memory journal, single thread). The ratio fits the
> domain-bound end of the speedup curve predicted by §4: as the verb shifts from
> DSL-bound to domain-bound, the amortizable surface shrinks against the unchanging
> host-side work, and the speedup compresses accordingly."*

## Modifications to Pacifico applied in this branch (heredables)

| Mod | File | Change |
|---|---|---|
| 6 | `Puppeteer/EventSourcing/DomainLibraries.cs` | `LoadTypesFromAssembly` catches `ReflectionTypeLoadException`, falls back to `ex.Types.Where(t => t != null)`. Tolerates transitive package dependencies missing from the host bin. |
| 2 | `Puppeteer/Actor.cs` | `internal enum CompilationModePolicy` → `public`. |
| 3 | `Puppeteer/Actor.cs` | `internal CompilationModePolicy CompiledModePolicy` field → `public`. |
| 5 | `Puppeteer/Parameters.cs` | `internal object this[string parameterName, Type parameterType]` indexer → `public`. Required for `WithParameters(p => p["name", typeof(T)] = value)` callbacks from a test assembly without `InternalsVisibleTo` plumbing. |

Mods 1, 4, 7 of the original Lab 1 plan are not needed — master upstream already
absorbed equivalents (`Actor` ctor with `params Assembly[]`, `ActorHandler` ctor with
`Assembly[]`, and `Objeto` no longer referenced by external assemblies in this setup).

## External-codebase modifications (additive only, documentable)

- `dotnet/eShop` clone @ `main`: `src/Ordering.Domain/Ordering.Domain.csproj` retargeted
  from `<TargetFramework>net10.0</TargetFramework>` to
  `<TargetFrameworks>net9.0;net10.0</TargetFrameworks>` to match Pacifico's TFM. No
  source-code changes to the aggregate. Build emits both DLLs; the net9.0 one is
  consumed.

## Files produced

- `puppeteer-papers/data/lab01-eshop/run3-bootstrap-20260516T171855Z-2b47ffb.csv` —
  per-iteration timings, both modes.
- `puppeteer-papers/data/lab01-eshop/headline.md` — this file.
- `Puppeteer Pacifico/UnitTestEShopOnPuppeteer/OrderingFacade.cs` —
  `CreateDraftWithItem` (smoke) + `CompleteOrder` (Lab 1 production verb).
- `Puppeteer Pacifico/UnitTestEShopOnPuppeteer/Lab01Run3EShopBench.cs` — bench class,
  `TestCategory("Bench")`.
