# Paper 2 Lab 4 — Journal density on Grzybek SubscriptionPayment (replica)

**Commit:** public Puppeteer `b42d0f7`, built Release. FileSystem journal (BinaryEventCodec). i9-13900, Windows 11 (26200), .NET 9.0.14.
**Datasets:** `run-N100-*-b42d0f7.csv`, `run-N1000-*-b42d0f7.csv`.

> *Deterministic measurement (entry counts, payload bytes): build-independent. Re-run in Release at `b42d0f7` — figures are bit-identical to the earlier Debug run. Earlier Debug CSVs superseded and removed.*

> *Bench source lives in the Puppeteer runtime repository, which is currently internal and will be released alongside the runtime itself. The CSVs in this directory are sufficient to reproduce the entry-type counts, payload sizes, and density ratio from `kgrzybek/modular-monolith-with-ddd` public source plus an instrumented build of the runtime.*

## Note on an earlier anomaly (resolved 2026-05-17)

An earlier version of this bench produced zero Action and Define entries despite the test framework reporting success. The cause was identified as a silent failure path in Pacifico's action-recording pipeline: `Parameters.CanonicalTypeName` (`Parameters.cs:315`) accepts a fixed set of primitive types — `int, string, bool, double, DateTime, decimal`, plus arrays and single-generic collections — and throws `LanguageException` for any unrecognised type, including `Guid`. The earlier facade passed `Guid` directly as a DSL parameter, and the resulting `LanguageException` surfaced *after* the script's effects had already been executed by `Perform()`. The surrounding `catch` block in `ActorHandler.ExecuteCommandWithWriteLock` (lines 1237-1250 of the canonical Pacifico master) treats post-execution exceptions as recoverable and silently records the error without writing the journal entry, so the test caller sees a successful return and no `ExecutionFailed` event fires.

**Workaround applied in this lab**: the facade accepts the payer id as a `string` and parses it to `Guid` internally. Every DSL parameter is now within `CanonicalTypeName`'s supported set. The action-recording path completes normally.

**Recommended runtime fix (out of scope for this lab, parked for a Pacifico maintainer to address)**: extend `CanonicalTypeName` to include `Guid` (and the catch block to surface unrecognised-type errors rather than swallow them) so that `Guid` parameters are first-class in the DSL action surface.

## Methodology

Runs N parametric invocations of the same measurement script against a fresh FileSystem actor and parses the resulting `journal_*.bin` files. Counts Action / Script / Define entries by type byte, sums payload bytes per type, and computes the density ratio between the actual Action payload and the hypothetical literal-script payload (Action count × Define payload). The intrinsic density claim (Paper 2 §2.3 / §4 Beat 3 / TL;DR) follows from those counts alone.

### DSL scripts

Bootstrap (1×, untimed) — non-parametric, lands as a Script entry:

```
f = SubscriptionPaymentFacade();
```

Measurement (1 warmup + N timed) — parametric, first occurrence emits one Define entry plus an Action entry; subsequent invocations emit only Action entries (cache hit on the action ID):

```
p = f.NewWaitingPayment(payerGuidStr, country, periodCode, amount, currency);
p.MarkAsPaid();
```

The facade assembles the `PriceList` internally so the `PriceOfferMustMatchPriceInPriceListRule` is satisfied; the DSL surface is reduced to five primitive parameters (the payer GUID as a string per the workaround above, three further strings, and a decimal amount).

### Per-scenario protocol

- Fresh actor with `actor.ConfigureStorage(DatabaseType.FileSystem, $"path={tempDir}")` — Guid-keyed temp dir per N so prior runs don't contaminate.
- Bootstrap → Warmup → N measurement calls, each with `WithParameters` injecting per-iteration values.
- `actor.GracefulExit()` before parsing to flush the journal writer.
- After the loop, parse all `journal_*.bin` files, categorise entries by type byte, and compute the density ratio.

## Headline numbers

### N=100

| Entry type | Count | Total bytes | Avg payload |
|---|---:|---:|---:|
| Script | 1 | 69 | 33 B |
| Action | 101 | 10,100 | 60 B |
| Define | 1 | 247 | **207 B** |
| **Total** | **103** | **10,416** | |
| Action % | 98.1% | | |

| Action payload min / max / avg | 60 / 60 / 60 B |
|---|---:|
| Literal-script reference (Define payload) | 207 B |
| Hypothetical literal payload (101 × 207) | 20,907 B |
| Actual Action payload (sum) | 6,060 B |
| **Density ratio (literal / actual)** | **3.5×** |

### N=1000

| Entry type | Count | Total bytes | Avg payload |
|---|---:|---:|---:|
| Script | 1 | 69 | 33 B |
| Action | 1,001 | 100,100 | 60 B |
| Define | 1 | 247 | **207 B** |
| **Total** | **1,003** | **100,416** | |
| Action % | 99.8% | | |

| Action payload min / max / avg | 60 / 60 / 60 B |
|---|---:|
| Literal-script reference | 207 B |
| Hypothetical literal payload (1,001 × 207) | 207,207 B |
| Actual Action payload (sum) | 60,060 B |
| **Density ratio (literal / actual)** | **3.5×** |

The Action-payload distribution is exact — every action entry after the first carries the same 60 B argument vector for the same two-line script (five parameters: payer GUID string, country, period code, amount, currency). The Define entry stores the script body (207 B) once. Action % approaches 100% as N grows (98.1% at N=100, 99.8% at N=1000) because the single Script entry from the non-parametric bootstrap is fixed cost.

## Comparison with the eShop replica

| Metric | eShop (Order, 4 items/cart) | Grzybek (SubscriptionPayment, single) |
|---|---:|---:|
| Action % | 99.8% | 99.8% |
| Avg Action payload | 115 B | 60 B |
| Define / Action body payload | 642 B | 207 B |
| **Density ratio** | **5.6×** | **3.5×** |
| Statements in measurement script | 9 | 2 |
| Parameters bound per iteration | 17 | 5 |

Two independent open-source DDD aggregates with disjoint business domains and different operational shapes — a multi-item e-commerce cart with a four-step state walk on one side, a single subscription-payment transition on the other — both confirm the structural property of §2.3: parametric workloads stay 99.8% Action, with the script body stored exactly once and per-invocation cost reduced to an argument vector. The density-ratio magnitudes (5.6× and 3.5×) differ because both DSL surfaces are short (Grzybek's especially — its production verb is intrinsically a two-statement operation); hosts whose verbs combine longer statement sequences with sparser argument vectors compound the ratio further. The mechanism applies regardless.

## What this confirms

- **§2.3 dense journaling**, **§4 Beat 3 "compactness in semantic information per byte"**, **TL;DR "compact action entries"** — confirmed independently on two open-source DDD aggregates.
- **Domain-independence**: the same three-tier journal structure (Script / Action / Define) and the same compaction mechanism apply against structurally distinct external aggregates. The mechanism is structural, not domain-specific.
- **Operational floor for an extremely narrow verb**: even at the lower bound of measurement-script length (a single facade method call plus a single state transition, on a domain whose business representation is one payment per subscription period), the density ratio remains strictly greater than 1× and approaches the asymptote as N grows.

## Integration text for Paper 2 §5.4

> *"The same journal-compaction structure appears on a second open-source DDD aggregate. Against `kgrzybek/modular-monolith-with-ddd`'s `SubscriptionPayment` (a payment-lifecycle verb whose DSL surface is two statements: `NewWaitingPayment` plus `MarkAsPaid`), a parametric workload of N=1,000 invocations produces 1,001 Action entries (99.8% of the journal), 1 Define entry of 207 B carrying the script body, and an average Action payload of 60 B carrying the five parameter values. The literal-script storage would be 3.5× larger. The script body is shorter and the argument vector smaller than the eShop case, so the ratio is more modest, but the structural mechanism — arguments scaling with invocations while the body is stored once — is identical."*

## Files produced

- `puppeteer-papers/data/lab04-grzybek/run-N100-20260517T012257Z-d7c1053.csv` — N=100 metrics.
- `puppeteer-papers/data/lab04-grzybek/run-N1000-20260517T012257Z-d7c1053.csv` — N=1000 metrics.
- `puppeteer-papers/data/lab04-grzybek/headline.md` — this file.
- `Puppeteer Pacifico/UnitTestGrzybekOnPuppeteer/Lab04JournalDensityGrzybekBench.cs` — bench class.
- `Puppeteer Pacifico/UnitTestGrzybekOnPuppeteer/SubscriptionPaymentFacade.cs` — facade (payer GUID accepted as string and parsed internally; documents the workaround).
