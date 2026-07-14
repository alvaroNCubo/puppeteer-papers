# Paper 5 — Lab 4: passive consumer / Materialize v2 (backup as program copy → replay)

A primary actor declares `actor.Materialization.Register("DC-B")`; a destination
runs the public `MaterializeMirror` client to pull the primary's corpus, and a
**replica instantiated over the destination journal reaches the same in-memory
state as the primary** — across FileSystem / MySQL / SQL Edge × N ∈ {1k, 10k,
100k}, bit-exact, plus per-backend convergence throughput (events/sec).

Headline → Paper 5 §5.3.

## What it does (per backend × N × rep, Layer 1 and Layer 2)

1. **Primary** (FileSystem, `AlwaysCompiled`): `Register` two destinations
   *before* populating (forward-fidelity — a destination reads only records after
   its `RegisteredAtEntryId`), then journal N compact invocations of
   `_seq = @val;`.
2. **Wire (public)**: `new MaterializeMirror(new LocalMaterializeSource(
   primary.Actor.Materialization, dest))` then `mirror.Sync()` (Layer 1 = records
   + `ConfirmUntil`) or `mirror.AsProgramMirror().Sync()` (Layer 2 = + `ReadReactions`
   + `ReadElidedRange`). Stopwatch brackets the sync (wire) window.
3. **Apply (documented internal seam)**: the fetched `MaterializationRecord`s are
   written into the destination backend, preserving EntryId / OccurredAt /
   ExposeData / Define+Invocation data. Stopwatch brackets the apply window.
4. **Parity (public)**: a replica `PerformanceV2` is instantiated over the
   destination journal; a public `PerformQuery` reads `_seq` and compares to the
   primary.

A catch-up cell (largest N) gaps the primary by `CatchupGap` further events,
then a single `Sync()` recovers the gap — confirming the same code path serves
steady-state and retention-gap recovery (E4 corollary).

## Run

```
docker compose -f docker-compose.lab.yml up -d      # MySQL 3306, SQL Edge 1433
docker compose -f docker-compose.lab.yml ps         # both must be healthy
dotnet run -c Release -- <outDir> <runTag>          # full: 3 backends × 3 N × 3 reps × 2 layers
docker compose -f docker-compose.lab.yml down
```

FileSystem always runs; an unreachable container is skipped with a warning. Env
overrides: `LAB4_BACKENDS` (csv), `LAB4_NVALUES` (csv), `LAB4_REPS`. Append
`smoke` for a fast pass. The `lab4_mysql` / `lab4_mssql` databases are created on
demand (so the lab can reuse any co-located MySQL / SQL Edge container).

## Guide compliance

Idioms per `training-lab/guides`: `PerformanceV2` +
`actor.Using(...).WithParameters(...).PerformCommand()` / `PerformQuery`;
compilation via the public field `CompiledModePolicy = AlwaysCompiled`; the
public Materialize v2 surface (`actor.Materialization.Register`,
`LocalMaterializeSource`, `MaterializeMirror.{Sync, AsProgramMirror().Sync}`).

**Documented internal grant — the apply seam.** `MaterializeMirror` returns the
fetched corpus but deliberately does **not** apply it locally — the source calls
this "a Hole for a future phase" (`MaterializeMirror.cs:19-26`): fetch+confirm is
the mirror's job; local application belongs to the destination operator. There is
no public apply verb yet, so this lab writes the records via the same structured
API the runtime's own shadow/replay path uses
(`ActorHandler.CopyPrimaryRecordsToShadow` → `DiaryStorage.Write{Script,Define,
Invocation}Entry`), preserving EntryId / OccurredAt / ExposeData. This requires
`InternalsVisibleTo("Lab05L4PassiveConsumer")` on the runtime (same convention as
the `Lab02BytesPerEvent` codec grant). Everything else — the measured wire, the
workload, and the parity check — is public surface.

## Provenance

Runtime: Pacifico `c402e2a`, built against the public mirror. The committed
`.csproj` references the sibling public clone `..\..\..\puppeteer\`.

## Files produced

- `sync_samples.csv` — per (backend, N, layer, rep): sync/apply/total ms,
  records, events/sec, is_warmup.
- `parity.csv` — per (backend, N, layer): primary vs replica `_seq`, parity_ok.
- `catchup_samples.csv` — per backend: gap recovery ms / records / events/sec.
- `summary.csv`, `headline.md`.
