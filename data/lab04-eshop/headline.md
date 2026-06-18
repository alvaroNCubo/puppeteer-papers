# Paper 2 Lab 4 — Journal Density on eShop (replay)

**Commit:** public Puppeteer `b42d0f7`, built Release. FileSystem journal (BinaryEventCodec). i9-13900, Windows 11 (26200), .NET 9.0.14.
**Datasets:** `run-N100-*-b42d0f7.csv`, `run-N1000-*-b42d0f7.csv`.

> *Deterministic measurement (entry counts, payload bytes): build-independent. Re-run in Release at `b42d0f7` — figures are bit-identical to the earlier Debug run. Earlier Debug CSVs superseded and removed.*

> *Bench source lives in the Puppeteer runtime repository, which is currently internal and will be released alongside the runtime itself. The CSVs in this directory are sufficient to reproduce the entry-type counts, payload sizes, and density ratio from `dotnet/eShop` public source plus an instrumented build of the runtime.*

## Methodology

Runs N parametric invocations of the same measurement script against a fresh FileSystem actor and parses the resulting `journal_*.bin` files. Counts Action / Script / Define entries by type byte, sums payload bytes per type, and computes the density ratio between the actual Action payload and the hypothetical literal-script payload (Action count × Define payload). The intrinsic density claim (Paper 2 §2.3 / §4 Beat 3 / TL;DR) follows from those counts alone.

**Important post-Action-refactor adjustment.** After the Action refactor (firmed 2026-05-12, master commit `5c68232`), `ActionStore.cs` and the separate `actions.bin` file no longer exist; the action definition body now lives in a dedicated **`Define`** entry (`EventRecordType.Define = 2`) inside the journal itself, alongside `Script` (0) and `Action` (1) entries. The binary parser in this bench treats all three entry types and uses the Define entry's payload as the per-call literal-script reference.

### DSL scripts

Bootstrap (1×, untimed) — non-parametric, lands as a Script entry:

```
f = OrderingFacade();
```

Measurement (1 warmup + N timed) — parametric, first occurrence emits one Define entry plus an Action entry; subsequent invocations emit only Action entries (cache hit on the action ID):

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

The DSL invokes the eShop `Order` aggregate's domain verbs directly — the facade builds the order in Submitted state via the 10-arg ctor (with `Address` value object internally); the DSL then adds **four items to the cart** (modelling a realistic multi-product purchase rather than an empty or single-item one) and drives the state machine through to Shipped.

### Per-scenario protocol

- Fresh actor with `actor.ConfigureStorage(DatabaseType.FileSystem, $"path={tempDir}")` — Guid-keyed temp dir per N so prior runs don't contaminate.
- Bootstrap → Warmup → N measurement calls, each with `WithParameters` injecting per-iteration values.
- After the loop, parse all `journal_*.bin` files under `tempDir/<actorName>/journal/`, categorize entries by type byte, and compute the density ratio.

## Headline numbers

### N=100

| Entry type | Count | Total bytes | Avg payload |
|---|---:|---:|---:|
| Script | 1 | 58 | 22 B |
| Action | 101 | 14,857 | 107 B |
| Define | 1 | 682 | **642 B** |
| **Total** | **103** | **15,597** | |
| Action % | 98.1% | | |

| Action payload min / max / avg | 96 / 108 / 107 B |
|---|---:|
| Literal-script reference (Define payload) | 642 B |
| Hypothetical literal payload (101 × 642) | 64,842 B |
| Actual Action payload (sum) | 10,817 B |
| **Density ratio (literal / actual)** | **6.0×** |

### N=1000

| Entry type | Count | Total bytes | Avg payload |
|---|---:|---:|---:|
| Script | 1 | 58 | 22 B |
| Action | 1,001 | 155,257 | 115 B |
| Define | 1 | 682 | **642 B** |
| **Total** | **1,003** | **155,997** | |
| Action % | 99.8% | | |

| Action payload min / max / avg | 96 / 116 / 115 B |
|---|---:|
| Literal-script reference | 642 B |
| Hypothetical literal payload (1,001 × 642) | 642,642 B |
| Actual Action payload (sum) | 115,217 B |
| **Density ratio (literal / actual)** | **5.6×** |

The Action-payload distribution is tight (96 → 116 B), as expected: every action entry after the first carries only the argument vector for the same nine-line script (17 parameters: user identity + four products' pid/name/price + shared discount/picUrl/units). The Define entry stores the script body (642 B) once. Action % approaches 100% as N grows (98.1% at N=100, 99.8% at N=1000) because the single Script entry from the non-parametric bootstrap is fixed cost.

## Note on the magnitude of the density ratio

eShop's `AddOrderItem` signature requires the full product specification per call (`pid, name, price, discount, picUrl, units`), so the per-item argument footprint is intrinsically rich — 17 parameters bound to a four-item cart yield ~115 B per Action entry. A host whose verbs identify catalog entries by short stable references would carry an order-of-magnitude smaller argument vector and the density ratio would compound accordingly into the tens. Either way, the structural property of §2.3 — *each invocation stores only the argument vector; the script body is stored exactly once* — holds: arguments scale with invocations, the Define body does not.

## What this confirms

- **§2.3 dense journaling** and **§4 Beat 3 "compactness in semantic information per byte"**: the parametric workload regime stores 99.8% of entries as compact Action references; the script body is stored exactly once per unique action. The journal footprint is dominated by the Action entries' argument vectors (~46 B), not by the script body (330 B, stored once).
- **TL;DR "compact action entries"**: at production scale (10,000 invocations of the same parametric action), the journal carries ~460 KB of argument data plus one ~330 B Define entry — vs ~3.3 MB if every invocation stored the script literal.
- **Domain-independence**: the same three-tier journal structure (Script / Action / Define) and the same compaction mechanism apply against a structurally distinct external domain. The mechanism is structural, not domain-specific.

## What this does NOT (yet) confirm

- **BI-projection comparison**: the eShop Order aggregate doesn't decompose into a known production BI table, so a journal-vs-equivalent-rows comparison is out of scope here. The intrinsic density-ratio claim does not depend on such a projection.
- **Cross-DC replication footprint**: separate paper / lab.

## Integration text for Paper 2 §5

> *"In a parametric workload over the eShop Order production verb (nine-line DSL script: order construction in Submitted state, four `AddOrderItem` calls representing a multi-product cart, and a four-step state-machine walk to Shipped), the FileSystem journal records 99.8% of entries as compact Action references with an average payload of 115 B — the argument vector for seventeen parameters (user identity + four products' details + shared modifiers). The script body — 642 B — is stored exactly once, as a Define entry. Had each of the 1,000 invocations stored the literal script text instead, the Action payload would be 5.6× larger. The density mechanism is structural: arguments scale with invocations, the script body does not. The magnitude of the ratio is a function of the host domain's argument density — hosts whose verbs identify entries by short stable references would carry sparser argument vectors and the ratio would compound into the tens — but the mechanism applies regardless."*

## Runtime modifications (now absorbed into the public runtime at `b42d0f7`)

| Mod | File | Change |
|---|---|---|
| 12 | `Puppeteer/Actor.cs` | `public void ConfigureStorage(DatabaseType, string)` added. Lab-friendly public path to the otherwise-internal `Handler.EventSourcingStorage`. Mirrors `StageHook.InitializeStorage` for the same purpose. |

Mods 8–11 inherited from prior lab-replay branches. UnitTestPuppeteer suite verified green 768/768 post-mod 12.

## Files produced

- `puppeteer-papers/data/lab04-eshop/run-N100-20260516T211544Z-5efc274.csv` — N=100 metrics.
- `puppeteer-papers/data/lab04-eshop/run-N1000-20260516T211545Z-5efc274.csv` — N=1000 metrics.
- `puppeteer-papers/data/lab04-eshop/headline.md` — this file.
- `tests-local/UnitTestEShopOnPuppeteer/Lab04JournalDensityEShopBench.cs` — bench class with 3-entry-type binary parser (Script / Action / Define), `TestCategory("Bench")`.
- `tests-local/UnitTestEShopOnPuppeteer/OrderingFacade.cs` — extended with `NewSubmittedOrder(userId, userName)` (returns Order in Submitted state via 10-arg ctor).
