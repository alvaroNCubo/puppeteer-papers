# Paper 2 Lab 4 — Journal Density on eShop (replay)

**Date:** 2026-05-16
**Branch:** `lab-replay/04-eshop` (from `lab-replay/03-eshop`) in `Puppeteer Pacifico`.
**Runtime config:** .NET 9.0.312 SDK, Debug build, FileSystem journal (BinaryEventCodec), Windows 11 host.
**Datasets:** `run-N100-20260516T211544Z-5efc274.csv`, `run-N1000-20260516T211545Z-5efc274.csv`.

## Methodology

Mirrors the original Lab 4 (***REDACTED***, `UnitTestPuppeteer/PaperLabs/paper2/Lab04_JournalDensity.cs`) — runs N parametric invocations of the same measurement script against a fresh FileSystem actor and parses the resulting `journal_*.bin` files. The BI-projection section of the original is dropped because it depended on ***REDACTED***-specific row counts in the `***REDACTED***` table that have no direct eShop analog; the density-ratio claim that Paper 2 §2.3 / §4 Beat 3 / TL;DR cite is independent of that projection.

**Important post-Action-refactor adjustment.** After the Action refactor (firmed 2026-05-12, master commit `5c68232`), `ActionStore.cs` and the separate `actions.bin` file no longer exist; the action definition body now lives in a dedicated **`Define`** entry (`EventRecordType.Define = 2`) inside the journal itself, alongside `Script` (0) and `Action` (1) entries. The binary parser in this bench treats all three entry types and uses the Define entry's payload as the per-call literal-script reference.

### DSL scripts

Bootstrap (1×, untimed) — non-parametric, lands as a Script entry:

```
f = OrderingFacade();
```

Measurement (1 warmup + N timed) — parametric, first occurrence emits one Define entry plus an Action entry; subsequent invocations emit only Action entries (cache hit on the action ID):

```
o = f.NewSubmittedOrder(uid, uname);
o.AddOrderItem(pid, name, price, discount, picUrl, units);
o.SetAwaitingValidationStatus();
o.SetStockConfirmedStatus();
o.SetPaidStatus();
o.SetShippedStatus();
```

The DSL invokes the eShop `Order` aggregate's domain verbs directly — the facade builds the order in Submitted state via the 10-arg ctor (with `Address` value object internally); the DSL then drives the state machine through to Shipped.

### Per-scenario protocol

- Fresh actor with `actor.ConfigureStorage(DatabaseType.FileSystem, $"path={tempDir}")` — Guid-keyed temp dir per N so prior runs don't contaminate.
- Bootstrap → Warmup → N measurement calls, each with `WithParameters` injecting per-iteration values.
- After the loop, parse all `journal_*.bin` files under `tempDir/<actorName>/journal/`, categorize entries by type byte, and compute the density ratio.

## Headline numbers

### N=100

| Entry type | Count | Total bytes | Avg payload |
|---|---:|---:|---:|
| Script | 1 | 58 | 22 B |
| Action | 101 | 8,467 | 44 B |
| Define | 1 | 370 | **330 B** |
| **Total** | **103** | **8,895** | |
| Action % | 98.1% | | |

| Action payload min / max / avg | 42 / 47 / 44 B |
|---|---:|
| Literal-script reference (Define payload) | 330 B |
| Hypothetical literal payload (101 × 330) | 33,330 B |
| Actual Action payload (sum) | 4,427 B |
| **Density ratio (literal / actual)** | **7.5×** |

### N=1000

| Entry type | Count | Total bytes | Avg payload |
|---|---:|---:|---:|
| Script | 1 | 58 | 22 B |
| Action | 1,001 | 85,867 | 46 B |
| Define | 1 | 370 | **330 B** |
| **Total** | **1,003** | **86,295** | |
| Action % | 99.8% | | |

| Action payload min / max / avg | 42 / 47 / 46 B |
|---|---:|
| Literal-script reference | 330 B |
| Hypothetical literal payload (1,001 × 330) | 330,330 B |
| Actual Action payload (sum) | 45,827 B |
| **Density ratio (literal / actual)** | **7.2×** |

The Action-payload distribution is tight (42 → 47 B), as expected: every action entry after the first carries only the parameter argument vector for the same 6-line script. The Define entry stores the script body (330 B) once. Action % approaches 100% as N grows (98.1% at N=100, 99.8% at N=1000) because the single Script entry from the non-parametric bootstrap is fixed cost.

## Comparison with ***REDACTED*** (original)

| Metric | ***REDACTED*** (original) | eShop CompleteOrder (this) |
|---|---:|---:|
| Action entries | 1,002 (100%) | 1,001 (99.8%) |
| Avg Action payload | 67 B | **46 B** |
| Define / NewAction payload (literal ref) | ~1,340 B | **330 B** |
| Density ratio | 20.1× | **7.2×** |

eShop's DSL surface for the measurement script is shorter (~270 chars literal → 330 B Define payload) than ***REDACTED***'s (~1,100 chars literal → ~1,340 B Define payload). The density ratio scales roughly linearly with that surface — eShop's 7.2× over a 330 B literal vs ***REDACTED***'s 20.1× over a 1,340 B literal reflects the same structural mechanism (action arguments-only vs full script body). The ratio matters at any positive value: every additional invocation pays the argument bytes only, not the script body, so the journal footprint stays sub-linear in (invocations × DSL surface).

## What this confirms

- **§2.3 dense journaling** and **§4 Beat 3 "compactness in semantic information per byte"**: the parametric workload regime stores 99.8% of entries as compact Action references; the script body is stored exactly once per unique action. The journal footprint is dominated by the Action entries' argument vectors (~46 B), not by the script body (330 B, stored once).
- **TL;DR "compact action entries"**: at production scale (10,000 invocations of the same parametric action), the journal carries ~460 KB of argument data plus one ~330 B Define entry — vs ~3.3 MB if every invocation stored the script literal.
- **Domain-independence**: the same three-tier journal structure (Script / Action / Define) and the same compaction mechanism apply against a structurally distinct external domain. The mechanism is structural, not domain-specific.

## What this does NOT (yet) confirm

- **BI-projection comparison**: the ***REDACTED*** original compared the journal to a hypothetical `***REDACTED***` BI table (~30× ratio). eShop has no direct analog (the e-commerce Order aggregate doesn't decompose into a known production BI table). The intrinsic density-ratio claim does not depend on this projection.
- **Cross-DC replication footprint**: separate paper / lab.

## Integration text for Paper 2 §5

> *"In a parametric workload over the eShop Order production verb (six-line DSL script invoking the aggregate's domain verbs), the FileSystem journal records 99.8% of entries as compact Action references with an average payload of 46 B (argument vectors only). The script body — 330 B — is stored exactly once, as a Define entry. Had each of the 1,000 invocations stored the literal script text instead, the Action payload would be 7.2× larger. The density mechanism is structural: arguments scale with invocations, the script body does not. This is observed against a different business domain than the original Paper 2 Lab 4 (***REDACTED***), confirming the journal compaction property as domain-independent."*

## Modifications to Pacifico applied in this branch (+1 on top of `lab-replay/03-eshop`)

| Mod | File | Change |
|---|---|---|
| 12 | `Puppeteer/Actor.cs` | `public void ConfigureStorage(DatabaseType, string)` added. Lab-friendly public path to the otherwise-internal `Handler.EventSourcingStorage`. Mirrors `StageHook.InitializeStorage` for the same purpose. |

Mods 8–11 inherited from prior lab-replay branches. UnitTestPuppeteer suite verified green 768/768 post-mod 12.

## Files produced

- `puppeteer-papers/data/lab04-eshop/run-N100-20260516T211544Z-5efc274.csv` — N=100 metrics.
- `puppeteer-papers/data/lab04-eshop/run-N1000-20260516T211545Z-5efc274.csv` — N=1000 metrics.
- `puppeteer-papers/data/lab04-eshop/headline.md` — this file.
- `Puppeteer Pacifico/UnitTestEShopOnPuppeteer/Lab04JournalDensityEShopBench.cs` — bench class with 3-entry-type binary parser (Script / Action / Define), `TestCategory("Bench")`.
- `Puppeteer Pacifico/UnitTestEShopOnPuppeteer/OrderingFacade.cs` — extended with `NewSubmittedOrder(userId, userName)` (returns Order in Submitted state via 10-arg ctor).
