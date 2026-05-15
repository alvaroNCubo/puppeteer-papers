# Lab 6 — Latency budget local journal vs RDBMS — headline

**Date**: 2026-05-14 UTC
**Branch**: `lab/paper05-lab6-latency-budget` @ `86905dc`
**Base**: `lab/paper05-base` @ `a9aa00e`
**Master at base**: `7db9643` (SQL Server storage bugfix cherry-picked
to the lab branch as `b0a180f` — needed for SQL Edge bootstrap;
base predates the master fix).
**Host**: Windows 11 Pro 10.0.26200 / Intel Core i9-13900 (24c/32t) /
64 GB RAM / NVMe local / .NET 10.0.103
**Configuration**: Release / x64 / single-threaded measurement
**Run id**: `run-20260514-152147-b0a180ff`
**Total wall-clock**: 23 m 04 s for K = 5 × N = 100 000 × 3 backends
(plus warm-up).
**Total measured samples**: 1 500 000 (500 000 per backend).

## Headline number

> *"Append latency p50 across three local backends under a
> 1-event = 1-durable-commit régime: FileSystem **347 µs** /
> SQLServer **1 126 µs** (3.24× FS) / MySQL **982 µs** (2.83× FS).
> The journal-append regime favors the substrate by ~3× over both
> co-located RDBMS engines — the latency advantage is structural,
> not engine-specific. On real RDBMS disk (vs tmpfs / named-volume
> here), the gap would only widen."*

## Tables

### Table 1 — Per-append latency by backend (N = 100 000, K = 5, warmup 1 000 discarded)

| backend     | samples | mean µs | p50 µs | p95 µs  | p99 µs   | max µs     | fsync mode                                  |
|-------------|---------|---------|--------|---------|----------|------------|---------------------------------------------|
| FileSystem  | 500 000 | 407.24  | 347.10 | 581.80  | 1 919.00 | 10 750.90  | `Flush(flushToDisk: true)`                  |
| SQLServer   | 500 000 | 1 264.88| 1 125.60| 1 891.40| 2 579.90 | 150 379.00 | default (full durability)                   |
| MySQL       | 500 000 | 1 064.27| 981.50 | 1 497.80| 2 021.00 | 7 604.80   | `innodb_flush_log_at_trx_commit=1,sync_binlog=1` |

### Table 2 — Latency ratio (RDBMS / FileSystem)

| backend   | p50 ratio | p95 ratio | mean ratio |
|-----------|-----------|-----------|------------|
| SQLServer | 3.24×     | 3.25×     | 3.11×      |
| MySQL     | 2.83×     | 2.57×     | 2.61×      |

## What this confirms

- Journal-append on a local FileSystem with `fsync` per record is
  ~3× faster than autocommit `INSERT` on a co-located RDBMS — the
  headline ratio holds across two RDBMS engines (SQLServer and
  MySQL), evidencing that the advantage is structural to the
  substrate, not idiosyncratic to one engine.
- p95 / p99 confirm the same ordering: even in the long tail, FS
  stays under 2 ms while both RDBMS engines sit at 1.5–2.6 ms.
- E1–E4 (deployment, replication, backup, offline) are all replay-
  based operations; their cost budget tracks per-append latency.
  The substrate's append cost favors the program, not its
  environment.

## What this does **not** confirm (Capa 2 honesty)

- **No remote-RDBMS sweep.** B3 ("remote RDBMS via tc-netem ~1 ms
  LAN RTT", per the original L6 handoff) was dropped for this
  short-lab cut. Adding 1 ms LAN RTT to the RDBMS path would push
  the gap further in favor of the substrate by a flat additive
  term — call it ~4× → ~6× p50 — but is not measured here.
- **RDBMS-on-tmpfs vs FS-on-NVMe asymmetry.** MySQL runs on an
  ephemeral tmpfs (`/var/lib/mysql:rw,size=2g`); SQL Edge runs on
  a Docker named volume (`mssql_data:/var/opt/mssql`). FileSystem
  writes go through the host's NVMe controller via Windows + the
  .NET `FileStream.Flush(flushToDisk:true)` syscall. The RDBMS
  measurements are therefore conservative — on a real-disk RDBMS
  deployment (typical production), the gap would only widen.
- **Batched inserts / async commit close part of the gap.** On
  the RDBMS side, `DELAYED_DURABILITY = ON` (SQL Server) or
  `innodb_flush_log_at_trx_commit=2` (MySQL — the L4 régime) move
  the system out of the apples-to-apples baseline measured here.
  Both regimes are honest choices; this lab measures the durable-
  commit baseline.
- **Small synthetic payload (~30 bytes per script entry).** Larger
  payloads would shift RDBMS more than FileSystem (network + buffer
  pool + page-split effects), but the structural conclusion — FS
  append is bounded by one fsync per record, RDBMS commit is
  bounded by one fsync per redo-log entry plus protocol overhead —
  holds.
- **Single-threaded measurement.** Concurrent writers would expose
  different bottlenecks (RDBMS lock contention, FS WAL contention).
  Out of scope for substrate measurement of a single actor's
  append path.
- **One outlier on SQLServer (max = 150 ms).** Inspection of
  `samples.csv` shows this is a single spike, not a pattern —
  likely a checkpoint or autogrow event in SQL Edge. The p99
  (2.58 ms) and the mean (1.26 ms) are not visibly distorted by
  it. Reported transparently rather than trimmed.

## Integration to Paper 5

§5.5 opening sentence:

> *"Append latency to the local journal is 3.24× lower than to a
> co-located SQLServer and 2.83× lower than to a co-located MySQL,
> at the p50 (median over K = 5 runs × N = 100 000 events per
> backend, under fsync-on durability for all three). The corollary
> to E1–E4 is straightforward: replay is cheap because append is
> cheap, and that cheapness is structural — both RDBMS engines
> measured land in the same 3× ratio band, so the gap is a property
> of the substrate, not of any particular RDBMS implementation."*

§5.5 caveat sentence (Capa 2):

> *"The local sweep is conservative on two axes: the RDBMS engines
> run on ephemeral container storage (tmpfs / Docker volumes), and
> no remote-RDBMS rung is included. Either axis — real disk or LAN
> RTT — would widen the gap further. The reported 3× is the
> baseline favorable to the RDBMS, not the upper bound."*

## Runtime mods applied on this branch

None in the timed path. Measurement happens entirely in the harness
via `Stopwatch.GetTimestamp()` bracketing each
`diaryStorage.WriteScriptEntry` call. The same two `GetTimestamp`
calls bracket the same exact `WriteScriptEntry` symbol across all
three backends — parity of measurement is the cleanest possible.

| File | Mod |
|------|-----|
| (none) | (none — pure measurement in harness) |

Off-the-timed-path heredables on this branch:

| Commit | What |
|--------|------|
| `b0a180f` | Cherry-pick `fix(sqlserver-storage)` from master `895223b`. Needed because `lab/paper05-base @ a9aa00e` predates the master SQL Server storage bugfix (END/IF separator, COUNT_BIG, premature `EventDataPool.Return`); without it SQL Edge bootstrap fails with `Incorrect syntax near 'ENDIF'`. |
| `86905dc` | Lab scaffold (README, docker-compose, harness). |

## Methodology notes

- Warm-up: first 1 000 appends per cell discarded (JIT, page cache,
  DB connection pool warm-up, lazy table creation). Pool semantics
  are intact for both MySQL and SQLServer — `using (new
  MySqlConnection(...))` / `using (new SqlConnection(...))` return
  to the pool, not the OS.
- Repetitions: K = 5 per (backend) cell. Independent actor name +
  fresh journal path (FS) / fresh table per actor (RDBMS) per
  repetition.
- Compile mode: irrelevant — the harness bypasses the actor's
  command pipeline and writes directly via
  `DiaryStorage.WriteScriptEntry`. No DSL compilation occurs on
  the timed path.
- fsync semantics:
  - FileSystem: `Flush(flushToDisk: true)` per `AppendRecord` in
    `Puppeteer/EventSourcing/DB/FileSystem/JournalWriter.cs:79`.
  - SQLServer (Edge): default durability (one log flush per commit;
    no `DELAYED_DURABILITY`).
  - MySQL: `innodb_flush_log_at_trx_commit=1`, `sync_binlog=1`
    (tightened from L4's `=2,=0`; see
    `docker-compose.lab.yml`).
- Script payload: 30 bytes, format `INV count=<8hex> token=<8hex>`.

## Files produced

`results/run-20260514-152147-b0a180ff/`:

- `samples.csv` — 1 500 001 rows (header + 1 500 000 samples). Columns:
  `run_id, backend, run_idx, event_idx, append_ticks, append_micros`.
- `summary.csv` — 4 rows (header + 3 backends). Columns: `run_id,
  backend, samples, mean_micros, p50_micros, p95_micros, p99_micros,
  max_micros, fsync_mode`.
- `summary_inline.md` — text-rendered Table 1 + Table 2, machine-
  generated.
- `headline.md` — this file.

## Git provenance

- Lab branch: `lab/paper05-lab6-latency-budget` @ `86905dc` (run);
  HEAD will move forward by one commit for the dataset + this
  filled headline.
- Base branch: `lab/paper05-base` @ `a9aa00e`.
- Heredables this lab adds (relative to base): the cherry-pick of
  `895223b` from master as `b0a180f`, and this lab folder.
