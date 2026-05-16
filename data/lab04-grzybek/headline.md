# Paper 2 Lab 4 — Journal density on Grzybek SubscriptionPayment (replica) — anomaly parked

**Date:** 2026-05-16
**Branch:** `lab-replay/grzybek-replica` in `Puppeteer Pacifico`.
**Status:** ⚠️ Anomaly detected; the bench tests pass at the framework level but the journal does not accumulate Action entries for the parametric Grzybek measurement. Lab 4 on Grzybek is therefore **incomplete** as a replica; the comparable Lab 5 α + β data on Grzybek (see `data/lab05-grzybek/`) carries the domain-independence test forward in the meantime.

## What runs

The Phase 2 setup itself is operational:

- `UnitTestGrzybekOnPuppeteer/SubscriptionPaymentFacade.NewWaitingPayment(...)` instantiates the value objects (PayerId, SubscriptionPeriod, MoneyValue, PriceList with matching PriceListItemData + DirectValueFromPriceListPricingStrategy) and invokes `SubscriptionPayment.Buy`.
- The combined-script smoke (`Smoke_DslInvokesGrzybekSubscriptionPaymentFacade`) — bootstrap + measurement in a single PerformCommand against the default in-memory store — passes cleanly. So DSL → C# facade → Grzybek `Payments.Domain` dispatch is operational end-to-end.

## What does not work

Split bootstrap-then-measurement (the canonical pattern firmed in `project_puppeteer_paper02_lab_methodology.md` Lineamiento 3 and used by every Lab 4/Lab 5 in this series) against the Grzybek facade:

- Bootstrap PerformCommand produces a Script entry (33 B payload, the literal `f = SubscriptionPaymentFacade();`).
- Subsequent measurement PerformCommands return successfully — no exception, no `ExecutionFailed` event, no parser error — yet **zero Action entries and zero Define entries** are written to the FileSystem journal at N=100 or N=1000.

The same pattern on `dotnet/eShop`'s `OrderingFacade` (`UnitTestEShopOnPuppeteer/Lab04JournalDensityEShopBench.cs`) produced 1001 Action entries + 1 Define entry under identical bench scaffolding. So the issue is not in the methodology, the storage configuration, or the symbol-table-persistence path.

## Observed (N=100 and N=1000)

| Entry type | Count | Total bytes |
|---|---:|---:|
| Script | 1 (bootstrap) | 58 |
| Action | **0** (expected ~N+1) | 0 |
| Define | **0** (expected 1) | 0 |
| Total | 1 | 58 |

The single journal file `journal_000001.bin` is 101 bytes total (32-byte file header + 69-byte bootstrap record). No measurement-derived entries appear.

## Hypotheses worth investigating in a focused chat

1. **Reference-typed parameters interact differently with action-recording.** The Grzybek measurement passes `Guid` directly (Pacifico value type), but the facade internally builds reference types (`PriceList`, `DirectValueFromPriceListPricingStrategy`) that an `Action` entry would need to bind by name. eShop's facade returns an `Order` (reference) too, but its measurement parameters are all primitives (string/int/decimal). The Grzybek-vs-eShop diff at the parameter-vector level is worth probing.

2. **Some part of Grzybek's domain triggers an `AggregateRoot` recognition path in Pacifico that silently treats the call as recovery / replication rather than as a new command write.** Grzybek's `BuildingBlocks.Domain.AggregateRoot` is structurally similar enough to Puppeteer's internal AggregateRoot vocabulary that name-collision is plausible.

3. **MediatR-based domain events emitted by `SubscriptionPayment.Buy` are intercepted by some Pacifico subsystem and short-circuit the journal write.** `Buy` calls `subscriptionPayment.AddDomainEvent(...)` internally; the test assembly references `MediatR.Contracts` to satisfy that path.

## What is NOT broken

- The DSL parser (the bootstrap script journals normally).
- The actor's symbol-table persistence across PerformCommands (the eShop equivalent works, and the combined smoke works).
- `SubscriptionPayment.Buy` itself (no exception, `Smoke_DslInvokesGrzybekSubscriptionPaymentFacade` passes).
- The binary-journal parser (it correctly reads the 1 bootstrap Script entry).

## Where this leaves the domain-independence test

Lab 4 Grzybek does not yet contribute to Paper 2's domain-independence claim for journal density. Lab 5 on Grzybek (`data/lab05-grzybek/headline.md`) provides the complementary structural measurement (α = 2, β = 73, β/α ≈ 36×) that does carry forward — the journal-compaction property is theorized to depend on the same upstream mechanism Lab 5 measures, so a Lab 5 result that converges with eShop's even when the per-iteration journal-density numbers are not collectible is partial but informative evidence.

## Datasets and code

- `puppeteer-papers/data/lab04-grzybek/run-N100-*.csv` — Action/Define rows are 0 (documentary, not headline).
- `puppeteer-papers/data/lab04-grzybek/run-N1000-*.csv` — same.
- `Puppeteer Pacifico/UnitTestGrzybekOnPuppeteer/Lab04JournalDensityGrzybekBench.cs` — bench, `TestCategory("Bench")`.
- `Puppeteer Pacifico/UnitTestGrzybekOnPuppeteer/SubscriptionPaymentFacade.cs` — facade.

## Cross-references

- `project_puppeteer_paper02_dual_codebase_replay.md` — Fase 2 status updated to "Lab 4 Grzybek anomaly parked; Lab 5 Grzybek done".
- `project_puppeteer_persistence_anchor_shapes_ddd.md` — the reframing that applies regardless of the Lab 4 outcome.
- `data/lab04-eshop/headline.md` — the working comparable measurement on the open-source eShop side.

## Recommendation

Park as a known issue. A focused chat with the Pacifico maintainers (or another debugging session reading Pacifico's action-emission code) should be able to identify which of the three hypotheses above is correct in a few hours. Until then, the domain-independence test of journal density rests on the eShop result alone, and the broader claim of domain-independence is carried by Lab 5 Grzybek (α + β) and by the consistent results across Labs 1–3 on eShop.
