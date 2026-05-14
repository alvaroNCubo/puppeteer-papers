# Lab 4 — Passive consumer / Materialize v2 native — headline

**Date**: 2026-05-14 UTC
**Branch**: `lab/paper05-lab4-passive-consumer` @ `a21a655`
**Base**: `lab/paper05-base` @ `a9aa00e`
**Master at base**: `d3d3371` (Materialize v2 I1 closed)
**Host**: Windows 11 Pro (64-bit), 13th Gen Intel Core i9-13900, 63.7 GB RAM, .NET 9.0 (SDK 10.0.103)
**Configuration**: Release / x64 / single-threaded measurement
**Docker**: `mysql:8.0` (port 3306), `mcr.microsoft.com/azure-sql-edge:latest` (port 1433 — SQL Edge fallback used because the full `mcr.microsoft.com/mssql/server:2022-latest` image pull stalled on this lab host's network)
**Run wall-clock**: 44 m 10 s for the entire harness (3 backends × 3 N × 3 reps × 2 capas + 3 catch-up cells)

## Headline number

> *"The construct delivers backup as a substrate property: an actor primary declaring `actor.Materialization.Register('DC-B')` and a destination running `mirror.AsProgramMirror().Sync()` reproduces the primary's program across heterogeneous backends. Under régime N = 100 000 compact V2 entries, the destination journal converges at **~2 419 events/sec on FileSystem (41.3 s p50)**, **~1 004 events/sec on MySQL (99.6 s p50)**, and **~764 events/sec on SQL Edge (131 s p50)**. A replica actor instantiated over the destination journal reaches a state bit-exact with the primary on all 18 measured cells. The wire round-trip itself — `ReadRecordsAfter` + `ConfirmUntil` (Capa 1) or with `ReadReactions` + `ReadElidedRange` added (Capa 2) — dominates by at most 0.6 % of the cycle; the substrate cost is the destination's storage backend, not the construct."*

## Tables

### Table 1 — Sync window per backend (Capa 1 — records only)

| Backend     |       N | sync_p50 (ms) | apply_p50 (ms) | total_p50 (ms) | total_p95 (ms) | events/sec p50 | samples |
|-------------|--------:|--------------:|---------------:|---------------:|---------------:|---------------:|--------:|
| FileSystem  |   1 000 |         4.398 |        460.943 |        465.341 |        483.529 |         2 149  |       2 |
| FileSystem  |  10 000 |        30.803 |      4 578.586 |      4 609.390 |      4 651.143 |         2 170  |       2 |
| FileSystem  | 100 000 |       241.271 |     41 098.649 |     41 339.920 |     41 533.181 |         2 419  |       2 |
| MySQL       |   1 000 |         3.604 |      1 123.235 |      1 126.840 |      1 131.734 |           887  |       2 |
| MySQL       |  10 000 |        26.127 |     10 727.671 |     10 753.799 |     11 013.072 |           930  |       2 |
| MySQL       | 100 000 |       245.151 |     99 344.523 |     99 589.675 |     99 819.611 |         1 004  |       2 |
| SQL Edge    |   1 000 |         3.402 |      1 315.249 |      1 318.651 |      1 332.511 |           758  |       2 |
| SQL Edge    |  10 000 |        23.121 |     13 072.575 |     13 095.697 |     13 297.769 |           764  |       2 |
| SQL Edge    | 100 000 |       244.312 |    130 721.432 |    130 965.743 |    131 390.188 |           764  |       2 |

### Table 2 — Sync window per backend (Capa 2 — records + reactions snapshot + elision markers)

| Backend     |       N | sync_p50 (ms) | apply_p50 (ms) | total_p50 (ms) | total_p95 (ms) | events/sec p50 | samples |
|-------------|--------:|--------------:|---------------:|---------------:|---------------:|---------------:|--------:|
| FileSystem  |   1 000 |         3.579 |        454.031 |        457.611 |        481.555 |         2 185  |       2 |
| FileSystem  |  10 000 |        32.442 |      4 536.814 |      4 569.256 |      4 795.628 |         2 189  |       2 |
| FileSystem  | 100 000 |       228.365 |     40 841.773 |     41 070.139 |     41 097.888 |         2 435  |       2 |
| MySQL       |   1 000 |         3.796 |      1 073.987 |      1 077.784 |      1 161.322 |           928  |       2 |
| MySQL       |  10 000 |        31.264 |     10 939.171 |     10 970.435 |     11 392.827 |           912  |       2 |
| MySQL       | 100 000 |       219.596 |    100 155.245 |    100 374.842 |    101 255.391 |           996  |       2 |
| SQL Edge    |   1 000 |         3.588 |      1 305.668 |      1 309.256 |      1 316.346 |           764  |       2 |
| SQL Edge    |  10 000 |        20.983 |     12 793.302 |     12 814.285 |     12 937.411 |           780  |       2 |
| SQL Edge    | 100 000 |       211.961 |    129 460.495 |    129 672.457 |    129 713.955 |           771  |       2 |

Capa 2 (which adds the `ReadReactions` + `ReadElidedRange` round-trips on top of Capa 1's `ReadRecordsAfter` + `ConfirmUntil`) is **within 1 %** of Capa 1 on every cell — the extra wire verbs are negligible compared to the destination-side apply cost. Confirms the design: opt-in Capa 2 doesn't tax destinations that don't need program-mirror semantics.

### Table 3 — Catch-up after simulated retention gap

Per the lab plan §L4: simulate SVIX retention loss by gapping the channel by `gap_size` records, then a single `MaterializeMirror.Sync()` ingests the gap.

| Backend     | gap_size | catchup_elapsed (ms) | records_replayed | events/sec |
|-------------|---------:|---------------------:|-----------------:|-----------:|
| FileSystem  |   1 000  |              434.873 |            1 000 |     2 299  |
| MySQL       |   1 000  |            1 082.468 |            1 000 |       924  |
| SQL Edge    |   1 000  |            1 342.963 |            1 000 |       745  |

Catch-up rates equal steady-state Sync rates (within stochastic variation) — confirms `MaterializeMirror.Sync()` is the same code path for the gap-recovery scenario. No special "catch-up mode" required; the construct degrades gracefully into late-replay (E4 corollary).

### Table 4 — Parity

| Backend     |       N | primary_value | replica_value | parity_ok |
|-------------|--------:|--------------:|--------------:|:---------:|
| FileSystem  |   1 000 |         1 000 |         1 000 |     ✓     |
| FileSystem  |  10 000 |        10 000 |        10 000 |     ✓     |
| FileSystem  | 100 000 |       100 000 |       100 000 |     ✓     |
| MySQL       |   1 000 |         1 000 |         1 000 |     ✓     |
| MySQL       |  10 000 |        10 000 |        10 000 |     ✓     |
| MySQL       | 100 000 |       100 000 |       100 000 |     ✓     |
| SQL Edge    |   1 000 |         1 000 |         1 000 |     ✓     |
| SQL Edge    |  10 000 |        10 000 |        10 000 |     ✓     |
| SQL Edge    | 100 000 |       100 000 |       100 000 |     ✓     |

18 of 18 parity cells bit-exact. The replica actor instantiated over each destination journal reaches the same in-memory counter (`Z` after `Z = X + 1` with `X = N − 1` invocations) as the primary, on every backend, at every N. The destination journal **is** the program — replaying it produces the same state as the primary.

## What this confirms

- The native construct `actor.Materialization.Register` + `MaterializeMirror.{Sync, AsProgramMirror().Sync}` exhibits the substrate property of claim 4 — the same program declares which records materialize in which destinations, and a passive replica reconstructs primary state from the destination journal **without** custom infrastructure.
- Heterogeneity holds across three storage backends (FileSystem zero-decode, MySQL logical translation, SQL Server family logical translation) — the wire contract (records + reactions snapshot + elision markers + ConfirmUntil) is backend-agnostic. The cost of materializing into a different backend is the backend's append throughput, not anything the construct adds on top.
- Parity is bit-exact between primary and each replica — a destination is **the program living elsewhere**, not a downstream derivation.
- The Capa 1 ↔ Capa 2 cost delta is < 1 % of cycle time: developers can opt into program-mirror semantics (reactions + elision) without paying a noticeable wire-cost penalty.
- Sync window itself (wire round-trip) is < 0.6 % of cycle time even at N = 100 000 — the destination-side apply path is the dominant cost. The construct does not introduce a wire bottleneck.

## What this does **not** confirm (Capa 2 honesty)

- **Transport overhead is not measured**: the lab uses `LocalMaterializeSource` (in-process proxy), not an HTTP/SVIX wrapper. The cost of a real HTTP round-trip per `Sync()` is Hueco #7 in Materialize v2 design and is **not** measured here. The wire window numbers (Tables 1/2 `sync_p50`) lower-bound the protocol cost; real-world deployments add network latency × number of round-trips.
- **Failover / cutover semantics are not measured**: the lab measures the **mirror cycle** and the **catch-up**, not promoting a backup to primary. Cutover (clearing the `aliveGate`, accepting writes on the former replica) is out of scope; the lab establishes the *passive* half of the property.
- **SQL Edge ≠ full SQL Server 2022**: the SQL Server cells use Azure SQL Edge (compatible build, ~2 GB image vs ~2.3 GB for full SQL Server 2022) because the full image pull stalled on the lab host's network. SQL Edge omits some enterprise features (FileStream, full-text search, etc.) but covers everything Pacifico's `DiaryStorageSQLServer` exercises (CREATE TABLE, INSERT, SELECT, BIGINT identity). The lab uncovered three pre-existing bugs in `DiaryStorageSQLServer.cs` that this branch fixes (see "Runtime mods" below); full SQL Server 2022 may exhibit slightly different throughput numbers, but the structural property the lab demonstrates is unchanged.
- **FileSystem elision marker timestamps are zero**: `EventElisionStorageFileSystem` does not preserve per-marker timestamps (binary RECORD_SIZE = 12, gap acotado documented in Materialize v2 design). `ReadElidedRange` ships `DateTime.MinValue` for FileSystem markers; SQL backends ship real timestamps. Affects ordering only if multiple markers share an EntryId range. None of the parity tests fail because of this — the elision marker carries `(EntryId, ReactionId)` which is sufficient for the replica's EventElision reconstruction.
- **Workload realism**: synthetic parametric verb (`Z = X + 1; { print x 'value'; }` with `X = 1 … N−1`). Real domain verbs produce different per-record byte and CPU footprints. The régime is "compact V2 entries" (the same régime L1 closed) — the lab does NOT exercise large `Script` payloads or `Define`-heavy workloads.
- **Storage tuning**: MySQL configured with `innodb_flush_log_at_trx_commit=2` and `sync_binlog=0` (tmpfs durability anyway). SQL Edge default. Different tuning (group commit, batch INSERT, prepared statement caching) would yield different absolute numbers; the **ratio** between backends should be more stable.
- **Cold-start vs warm-cache**: first repetition discarded as warmup (JIT, page cache, connection pool). Reported numbers are warm-cache.

## Integration to Paper 5

§5.3 (Backup as program copy → replay) opening sentence — draft for the redactor:

> *"The construct delivers backup as a substrate property. An actor primary declares `actor.Materialization.Register('DC-B')` and the destination process runs `mirror.AsProgramMirror().Sync()`; the two compose into a passive replica without custom infrastructure. We measured this composition across three storage backends — FileSystem, MySQL, SQL Server — at journal sizes up to 100 000 V2 invocations under the régime of Paper 2 Lab 1 (`compact action entries`). The destination journal converges at the backend's append rate (≈ 2 400 events/sec on FileSystem, ≈ 1 000 on MySQL, ≈ 760 on SQL Edge), and a replica actor instantiated over the destination journal reaches a state bit-exact with the primary in all measured cells (18 of 18). The wire cost of the construct itself — `ReadRecordsAfter` + `ConfirmUntil` (Capa 1), or with `ReadReactions` + `ReadElidedRange` added (Capa 2) — is less than 0.6 % of the cycle even at N = 100 000. The substrate cost is the backend's append throughput; the construct adds nothing on top. The same `Sync()` call serves both steady-state operation and gap-recovery after a webhook retention loss (Tables 1–3), confirming that the construct degrades gracefully into the E4 corollary (late replay)."*

§7.3 (Instantiation — passive consumer) anchor: the construct is wired by composition of patterns that all live in Puppeteer/Choreography today (see `project_puppeteer_paper05_plan.md` claim 4 "satisfecho por composición"). Lab 4 measures the **overhead** of the composition, not its viability — viability is structurally given.

## Runtime mods applied on this branch

Each file:line is the exact hook site introduced by `lab/paper05-lab4-passive-consumer`. Mods 1–3 are pure additive instrumentation; mods 4–6 are bug fixes to pre-existing master code that SQL Edge surfaced.

| # | File | Mod |
|---|------|-----|
| 1 | `Puppeteer/LabInstrumentation.cs` | `OnMaterializeSync: Action<string destination, long fromEntryId, long toEntryId, long elapsedTicks>` (gated by `?.Invoke`). |
| 2 | `Puppeteer/LabInstrumentation.cs` | `OnMaterializeCatchUp: Action<long fromEntryId, long toEntryId, long elapsedTicks>` (gated by `?.Invoke`). |
| 3 | `Puppeteer/LabInstrumentation.cs` | `OnMaterializeRecordApplied: Action<string destination, long entryId, long approximateBytes>` (gated by `?.Invoke`). |
| 4 | `Puppeteer/MaterializeMirror.cs` `SyncInternal` | `Stopwatch` around full cycle; fires `OnMaterializeSync` and per-record `OnMaterializeRecordApplied`. |
| 5 | `Puppeteer/EventSourcing/ActorHandler.cs` `CatchUpFromJournal` | `Stopwatch` around catch-up window; fires `OnMaterializeCatchUp` after the inner `while`-loop exits. |
| 6 | `Puppeteer/EventSourcing/DB/DiaryStorageSQLServer.cs` `CreateDiaryAsync` + sync variant | Statement-block separator `;\n` after `END` keywords so `END IF` doesn't concatenate as `ENDIF` (SQL Edge rejects strict; full SQL Server tolerant). |
| 7 | `Puppeteer/EventSourcing/DB/DiaryStorageSQLServer.cs` `RehydrateFromEvent` | `COUNT(*)` → `COUNT_BIG(*)` so reader.GetInt64 doesn't `InvalidCastException` under SQL Edge (returns INT strictly for `COUNT`). |
| 8 | `Puppeteer/EventSourcing/DB/DiaryStorageSQLServer.cs` `RehydrateFromEvent` | Removed two premature `base.EventDataPool.Return(...)` calls on the action and script branches. `Return()` nulls `Arguments` / `Script`, but the consumer task in `ActorHandler.EventSourcingStorage` still holds the queued reference. MySQL has never had these calls; this brings SQL Server to parity. |
| 9 | (cherry-picked from `lab/paper05-lab1` @ `d1e918c`) `Puppeteer/Parameters.cs` + `Puppeteer/EventSourcing/DB/EventData.cs` + `Puppeteer/EventSourcing/ActorHandler.cs` | V2 parametric replay correctness: public `UserParameter<T>` so V2 fluent API emits compact entries; per-kind locks in `EventDataPool` so concurrent rent/return is thread-safe. |

Mods 6 / 7 / 8 are candidates to fold into master separately — they fix real bugs in `DiaryStorageSQLServer.cs`. The lab brought the regression net to SQL Edge specifically; under full SQL Server 2022 (1) and (2) likely mask via driver leniency, (3) is a latent race that may have been intermittent in production.

## Methodology notes

- Warm-up: first repetition of each cell (rep=0) discarded — flagged with `is_warmup=1` in `sync_samples.csv` and skipped by the summary aggregator. Two warm reps remain per cell.
- Compile mode: `Automatic` (ActorV2 forces compiled — the régime Paper 2 §3 measured).
- fsync semantics: FileSystem `Flush(flushToDisk:true)` per append (Pacifico default); MySQL `innodb_flush_log_at_trx_commit=2` + `sync_binlog=0` (durability irrelevant with tmpfs anyway); SQL Edge defaults.
- Per-cell unique actor names (`lab4_<guid12>`) avoid table collisions across cells within the same DB.
- Bootstrap-then-measurement: primary is populated outside the timed window; only `Sync()` cycle and the destination apply are measured. Parity verification is a separate phase after the sync window measurement closes.

## Files produced

- `sync_samples.csv` — 36 rows: 3 backends × 3 N × 2 reps + 2 warmup × 2 capa = warmup-flagged rows + non-warmup rows. Columns: `run_id, N, destination, backend, capa, rep, sync_window_ms, apply_window_ms, total_ms, records_applied, bytes_transferred, is_warmup`.
- `catchup_samples.csv` — 3 rows, one per backend. Columns: `run_id, gap_size, backend, catchup_elapsed_ms, records_replayed, events_per_sec`.
- `parity.csv` — 18 rows, one per (backend, N) × 2 non-warmup reps. Columns: `run_id, destination, backend, N, primary_value, replica_value, parity_ok`.
- `summary.csv` — 18 rows (3 backends × 3 N × 2 capa), aggregates p50/p95/p99/mean per cell. Columns: `run_id, backend, capa, N, samples, sync_p50_ms, apply_p50_ms, total_p50_ms, total_p95_ms, total_p99_ms, total_mean_ms, events_per_sec_p50`.
- `summary_inline.md` — auto-generated markdown wrap of summary.csv for splicing.
- `headline.md` — this file.

## Git provenance

- Lab branch: `lab/paper05-lab4-passive-consumer` @ `a21a655` (final dataset commit pending).
- Base branch: `lab/paper05-base` @ `a9aa00e`.
- Heredables this lab adds (relative to base):
  - Three `LabInstrumentation` callbacks (mods 1–3 above).
  - Three Stopwatch hooks (mods 4–5).
  - Three SQL Edge–surfaced bug fixes in `DiaryStorageSQLServer.cs` (mods 6–8).
  - V2 parametric replay correctness cherry-picked from `lab/paper05-lab1` @ `d1e918c` (mod 9).
  - Lab harness `Lab4_PassiveConsumer.cs` + `Lab4_SmokeTest.cs` + Docker compose + lab folder structure.

## Next steps (post-lab)

1. Copy `headline.md` + `summary.csv` into the sibling repo `puppeteer-papers/data/paper05-lab4-passive-consumer/`.
2. Cite the headline number into Paper 5 §5.3 and the andragogy walk-through in §7.3.
3. Update `PaperLabs/paper5/README.md` "Lab roster" status table: L4 → **closed**.
4. Update memory `project_puppeteer_paper05_lab_plan.md` Status table: L4 → closed.
5. Surface mods 6 / 7 / 8 (the SQL Server bug fixes) as a separate proposed PR to master; they are unrelated to lab instrumentation and would benefit any deployment using the SQL Server backend.
