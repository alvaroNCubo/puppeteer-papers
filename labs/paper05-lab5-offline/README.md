# Paper 5 — Lab 5: offline operation (local buffer + async flush, claim 5 / E4)

An actor configured with a **local buffered journal** (`localBufferPath=…`)
appends to a fast local WAL and replicates asynchronously to a remote canonical
backend (MySQL / SQL Edge). The lab measures:

1. **Buffer speedup** — online per-append latency, direct (straight-to-remote)
   vs buffered.
2. **Partition** — `docker stop` the remote, keep appending M events to the
   local buffer; latency must stay **unchanged**, backlog grows.
3. **Catch-up** — `docker start` the remote, drain the backlog, measure the
   drain rate and confirm **zero events lost** (a replica over the remote
   reaches the primary's state).

Headline → Paper 5 §5.4.

## Run

```
docker compose -f docker-compose.lab.yml up -d      # MySQL 3307, SQL Edge 1434 (named volumes)
docker compose -f docker-compose.lab.yml ps         # both must be healthy
dotnet run -c Release -- <outDir> <runTag>          # {MySQL,SQL Edge} × {direct,buffered}
docker compose -f docker-compose.lab.yml down
```

The harness stops/starts the containers itself during the partition phase. Env
overrides: `LAB5_BACKENDS` (csv), `LAB5_N`, `LAB5_M`, `LAB5_REPS`. Append
`smoke` for a fast pass. Named volumes are **required** (the remote table must
survive `docker stop`); do not switch to tmpfs.

## Guide compliance

Idioms per `training-lab/guides`: `PerformanceV2` +
`actor.Using("_seq = @val;").WithParameters(...).PerformCommand()`; compilation
via `CompiledModePolicy = AlwaysCompiled`; `Stopwatch.GetTimestamp()` per append.
The buffered-vs-direct path is selected **purely by the public connection-string
key** `localBufferPath=` — no API difference between the two modes.

**Documented internal grant — the progress observers.** The buffered-vs-direct
progress observers are purpose-built on `Diary` for this lab
(`// paper05-lab5: harness-facing observers`, `Diary.cs:34-41`) but are internal:
`LastReplicatedEntryId`, `PendingReplicationCount`, `LocalBufferLastWrittenEntryId`,
`ReplicationFailureCount`. Read via `actor.Handler.TryGetDiary()` under
`InternalsVisibleTo("Lab05L5Offline")`. The **zero-loss verdict itself is public**:
a fresh replica `PerformanceV2` over the remote backend runs a public
`PerformQuery` and must reach the primary's last `_seq`.

## Provenance

Runtime: Pacifico `c402e2a`, built against the public mirror. The committed
`.csproj` references the sibling public clone `..\..\..\puppeteer\`.

## Files produced

- `samples.csv` — per append: cell, backend, mode, phase, rep, event_idx,
  append_micros.
- `summary.csv` — per (cell, phase): p50/p95/p99/mean/max µs + events/sec.
- `catchup.csv` — per buffered cell: backlog, drain sec, drain events/sec,
  primary/replica value, zero_loss.
- `headline.md`.
