# Paper 5 — Lab 6: latency budget (local FileSystem vs co-located RDBMS)

Measures **per-entry append latency** of the journal substrate under a
`1 event = 1 durable commit` régime, across three storage backends with
matched fsync semantics:

| backend | durability |
|---------|-----------|
| FileSystem | `JournalWriter.AppendRecord` → `Flush(flushToDisk:true)` per append |
| MySQL 8.0 (container) | `innodb_flush_log_at_trx_commit=1`, `sync_binlog=1` |
| Azure SQL Edge (container) | default durable-log-flush per commit |

Headline → Paper 5 §5.5.

## What it does

For each backend, `K` reps × `N` appends. Each append is a single public
`PerformCommand` of the compact parametric verb `_seq = @val;` under
`CompiledModePolicy.AlwaysCompiled`. The first call journals a Define +
Invocation; every later call journals one **compact Invocation** entry (the
Paper 2 compact-action régime). `Stopwatch.GetTimestamp()` (QPC, ~100 ns)
brackets each call. Warm-up appends (JIT, page cache, connection pool, schema
creation, first Define shape) are discarded. p50/p95/p99 reported per backend
plus the RDBMS/FileSystem ratio.

## Run

```
docker compose -f docker-compose.lab.yml up -d      # MySQL 3306, SQL Edge 1433
docker compose -f docker-compose.lab.yml ps         # both must be healthy
dotnet run -c Release -- <outDir> <runTag>          # full: N=100k, K=5, 3 backends
docker compose -f docker-compose.lab.yml down
```

FileSystem always runs; a container that is unreachable is skipped with a
warning (the lab still reports the backends that work). Env overrides:
`LAB6_BACKENDS` (csv), `LAB6_N`, `LAB6_REPS`. Append `smoke` for a fast pass.
The SQL Edge database (`lab6_mssql`) is created on demand via a master
connection.

## Guide compliance

**Public-surface lab — no `LabInstrumentation`, no `InternalsVisibleTo`, no
runtime mods.** Idioms used, per `training-lab/guides`:

- `PerformanceV2` + `perf.Actor.Using(body).WithParameters(…).PerformCommand()`
  (basics, parameters, perform-enact, conventions).
- Compilation set via the public field
  `perf.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysCompiled`
  (not a lab-mod), so every append lands as a compact action entry.
- `Stopwatch.GetTimestamp()` per append; medians over K reps with warm-up
  discarded (timing discipline).

**Honesty note on what is measured.** The bracketed cost is the *full public
`PerformCommand`+append* the application pays — a small, backend-invariant DSL
constant (parameter-pool rent + interpret of the compiled action) sits above
the pure storage append. The FileSystem number is therefore an **upper bound**
on storage latency, and the RDBMS/FileSystem ratio is **conservative** (the
shared DSL constant compresses it slightly). Isolating pure `WriteScriptEntry`
would require the internal storage seam; this lab deliberately stays on the
public surface, which is the latency an application actually experiences.

## Provenance

Runtime: Pacifico `c402e2a`, built against the public mirror
(`puppeteer-github-public`). The committed `.csproj` references the sibling
public clone `..\..\..\puppeteer\` at the same commit (convention shared with
lab01–04 and paper05-lab1).

## Files produced

- `samples.csv` — one row per measured append: `run_tag, backend, rep,
  event_idx, append_ticks, append_micros`.
- `summary.csv` — one row per backend: p50/p95/p99/mean/max µs + fsync mode.
- `headline.md` — headline tables (per-append latency + RDBMS/FS ratio).
