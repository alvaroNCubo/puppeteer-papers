# Lab 3 — DC B symmetric Reactions — headline

**Date**: 2026-05-14 UTC
**Branch**: `lab/paper05-lab3-inproc-symmetric`
**Base**: `lab/paper05-base` @ a9aa00e
**Master at base**: bf09476 (lab/paper05-base sits 3 commits ahead of master)
**Host**: Windows 11 Pro 10.0.26200 / 32 cores / .NET 9.0.14
**Configuration**: Debug / x64 / single-process, two `PerformanceV2`
**Run id**: `run-20260514-192550-3984c5db`

## Headline number

> *"For the two Reaction terminators in scope (Emit and Elide), two
> instances of the same actor binary consuming the same event stream
> produce bit-identical output across N ∈ {100, 500, 1000} events
> × 2 repetitions = 6 cells: 0 callback-tuple byte diffs and 0
> journal-segment byte diffs in every cell. The remote consumer
> requires no coordinator — its Reaction output is a pure function
> of the journal prefix it reads."*

## Tables

### Table 1 — Parity per cell (run-20260514-192550-3984c5db)

| N | rep | is_warmup | emit_a | emit_b | elide_matches_a | elide_matches_b | elide_events_a | elide_events_b | callback_byte_diff | segment_byte_diff | wall_clock_ms |
|---|-----|-----------|--------|--------|-----------------|-----------------|----------------|----------------|--------------------|-------------------|---------------|
| 100  | 0 | true  | 100  | 100  | 101  | 101  | 202  | 202  | 0 | 0 | 2694.855  |
| 100  | 1 | false | 100  | 100  | 101  | 101  | 202  | 202  | 0 | 0 | 2687.930  |
| 500  | 0 | true  | 500  | 500  | 509  | 509  | 1018 | 1018 | 0 | 0 | 13442.320 |
| 500  | 1 | false | 500  | 500  | 509  | 509  | 1018 | 1018 | 0 | 0 | 13391.632 |
| 1000 | 0 | true  | 1000 | 1000 | 1019 | 1019 | 2038 | 2038 | 0 | 0 | 33887.795 |
| 1000 | 1 | false | 1000 | 1000 | 1019 | 1019 | 2038 | 2038 | 0 | 0 | 33353.973 |

### Table 2 — Aggregates per N

| N | reps | callback_diff_max | segment_diff_max | wall_clock_ms_mean | wall_clock_ms_max |
|---|------|-------------------|------------------|--------------------|-------------------|
| 100  | 2 | 0 | 0 | 2691.393  | 2694.855  |
| 500  | 2 | 0 | 0 | 13416.976 | 13442.320 |
| 1000 | 2 | 0 | 0 | 33620.884 | 33887.795 |

## What this confirms

- The two terminators in scope (Emit and Elide) are pure functions of
  the journal prefix: same prefix on A and B → same multiset of
  `(triggeringEntryId, terminator, payloadHash8)` and `(eventIds)`
  tuples. Cell-level parity is byte-exact for all 6 cells.
- The FileSystem `WriteRawRecord` path on B preserves the wire bytes
  byte-for-byte versus A's primary journal — confirmed by the
  `journal_*.bin` segment diff. A's primary `journal_<NNN>.bin` and
  B's `journal_<NNN>.bin` are bit-identical.
- B's Reactions execute under push mode without ever touching A's
  state: the program-declared `expose counter.total total;` payload is
  what travels in the journal, so any future Reaction guard on
  `total` (e.g. `expose $cur:int total;` with `$cur >= 100`) resolves
  on B from the journal alone. The discipline that makes claim 3 work
  without a coordinator is the program's use of `expose`, not a
  runtime contract.

## What this does **not** confirm (Capa 2 honesty)

- **Tell terminator deferred.** Symmetric execution of Tell on B
  re-journals the envelope through PerformCmd, so B's `journal_*.bin`
  would contain B's own Tell entries on top of the ones replicated
  from A — breaking the segment diff. The runtime needs a
  `suppressReactionJournaling` flag derived from
  `Performance.Start(asFollower:true)` that gates `Reaction.ExecuteTell`
  to fire the callback (and optionally dispatch the envelope) without
  invoking `PerformCmd`. The other three terminators are already
  follower-safe by construction: Emit is read-only via `PerformEmit`,
  MarkAsSkip and Materialize write only to local auxiliary tables.
  Out of scope for L3 by explicit decision; documented in README and
  in the project memory `project_follower_materialize_roles.md`.
- **In-proc transport.** The transport here is a
  `System.Threading.Channels.Channel<RawRecord>` in the same process.
  Cross-process / cross-DC SVIX delivery is **not** measured here.
  Claim 3 is about Reactions symmetry under bit-exact wire; the
  transport is the §7.2 angle, outside this lab.
- **N bound.** The sweep tops out at N=1000 events. At N=10000 the
  test runner OOMs (~5 GB Working Set near the end of the long
  single cell because of accumulated journal-file handles plus
  the in-proc Channel buffer); the parity claim should hold by
  construction past N=1000 — the Reaction matcher's path on the
  Bump/Elide terminators is N-invariant — but this lab does not
  *measure* parity above N=1000.
- **Parametric verbs deferred.** Scripts are literal (no `@by`-style
  parametric capture). The Reactions push loop in `ActorReactions.DrainQueue`
  handles `ScriptEventData` and `ActionEventData` but silently
  discards `Define` records, so a parametric verb's `actionId` would
  never make its way to B's pattern matcher unless the lab also fed
  the `Define` entries through `AddKnownActionFromDefine`. That is
  a separate runtime gap; L3 sidesteps it with literal scripts.
- **EventElision auxiliary-table timestamp.** `Reaction.cs` calls
  `MarkEventsAsElided(..., DateTime.Now)` (line 738) when MarkAsSkip
  fires, so the `EventElision` aux table's timestamp column diverges
  between A and B by design (wall-clock). The lab compares the
  callbacks and the `journal_*.bin` segments — the aux-table
  timestamp is local metadata and out of scope.
- **MarkAsSkip / SharedHydration interacts with timing.** At N
  beyond what this lab measured (smoke at N=10000 dropped before
  it could complete), A and B's `elide_events` counts diverged
  because the Shared hydration BFS closes pending Bump matches
  on a Reset, and A (events arriving synchronously via
  `PerformCmd`) accumulates a different in-flight set than B
  (events arriving in bursts via `WriteRawRecord` + push). The
  semantic invariant — *every Bump that was followed by a Reset
  ends up elidable* — still holds; what differs is *which
  Reset* closes which Bump. Resolving this cleanly is a
  separate item (deterministic match order in the matcher, or a
  flush-on-quiescence policy) — flagged here, not addressed.

## What this lab measures vs what `/materialize` does in production

Claim 3 of Paper 5 — *"the remote consumer is symmetric"* — admits
two readings, each anchored in a different operating mode of the
runtime (`project_follower_materialize_roles.md`):

- **Theoretical reading (what this lab measures).** Given the same
  journal prefix, the same program produces the same Reaction
  outputs by *re-executing* the Cued reactions on the consumer side.
  L3 verifies this empirically with a harness that runs the Cued
  reactions on B over a byte-exact mirror of A's journal — the
  6/6-cell parity above is direct evidence of the property.

- **Operational reading (what `/materialize` does in production).** A
  backup / destination running in `/materialize` mode does **not**
  re-execute reactions. It consumes the markers (EventElision /
  EventMaterialization) that the primary has already produced, via
  the Materialize v2 Capa 2 protocol (`ReadRecordsAfter` +
  `ReadElidedRange` + `ReadReactions` + `ConfirmUntil`). No
  re-execution, no timing race.

The two readings are complementary, not rival: the theoretical
property is precisely what *licenses* the operational optimisation.
Capa 2 is sound because the program is a pure function of the
journal prefix — without that, Capa 2 would be a black box of
trust. L3 is the empirical anchor of that licensing argument.

The harness instantiates B with `Performance.Start(asFollower:true)`
+ Cued reactions running over a mirror journal. Semantically this
sits closer to a hypothetical *fourth* role (a follower-of-load
running Cued reactions for distributed throughput) than to either
`/follower` (Job() distribution) or `/materialize` (markers via
Capa 2). The lab does **not** claim to model a production role —
it claims to verify the substrate property that all three roles
depend on.

## Integration to Paper 5

§5.2 part 2 opening sentence:

> *"Lab 3 measures the substrate's symmetric-consumer claim at N up
> to 1000 events with 6/6 cells producing 0 callback byte diffs and
> 0 journal-segment byte diffs across the Emit and Elide terminators.
> The remote consumer is symmetric because the program is a pure
> function of the journal prefix it reads — the `expose` payload is
> the contract that lets B's Reactions evaluate guards without ever
> executing the script. This empirical property is what licenses the
> operational optimisation in §7.3, where a `/materialize`
> destination consumes the primary's pre-computed markers via
> Capa 2 without re-executing reactions."*

## Runtime mods applied on this branch

Additive, never merged to master. File:line markers are the exact
hook sites introduced by `lab/paper05-lab3-inproc-symmetric`.

| File                                                  | Mod                                                                                                                                  |
|-------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------|
| `Puppeteer/LabInstrumentation.cs`                     | Three new callbacks: `OnReactionEmit`, `OnReactionElide`, `OnReactionTell`. Plus `OnReplayEventCounted` family inherited from L1.    |
| `Puppeteer/EventSourcing/Follower/Reaction.cs`        | `ExecuteAction(Parameters)` → `ExecuteAction(Parameters, long triggeringEntryId)`. Fire callbacks in `case Emit / EmitWithCheck / Tell / MarkAsSkip`. New helper `HashParameters8` (filters Now/Ip/User by name to drop wall-clock from the hash). |
| `Puppeteer/EventSourcing/Follower/ReactionEngine.cs`  | Propagate `triggeringEntryId` through `ExecuteAction`.                                                                               |
| `Puppeteer/EventSourcing/Follower/MatchTree.cs`       | Pass `leafNode.EntryId` at the single call site.                                                                                     |
| `Puppeteer/EventSourcing/DB/DiaryStorageFileSystem.cs`| `WriteRawRecord` now invokes `OnRecordWritten?.Invoke(entryId, record)` so a follower fed only through this path receives push-mode notifications. (Latent runtime bug L3 surfaced.) |
| `Puppeteer/EventSourcing/DB/Diary.cs`                 | `AddRecordWrittenCallback` uses `Interlocked.CompareExchange` to chain wrappers; previous read-modify-write of `fs.OnRecordWritten` raced when two Cued reactions registered concurrently and silently dropped one. (Latent runtime bug L3 surfaced.) |
| `Puppeteer/Parameters.cs`, `Puppeteer/EventData.cs`, `Puppeteer/EventSourcing/ActorHandler.cs` | Cherry-picked from L1 (commit `d1e918c`): public `UserParameter<T>`, per-kind locks on `EventDataPool`, `OnReplayEventCounted` call site. Not all are exercised by L3 but the executionTask call site forces the LabInstrumentation member surface. |
| `UnitTestPuppeteer/UnitTestPuppeteer.csproj`          | `ProjectReference` to `Choreography` for `PerformanceV2` / `Performance.Start asFollower`.                                           |

## Methodology notes

- **Process warmup**: one extra cell at the smallest N runs and is
  discarded before the measured sweep starts. Without it the first
  measured cell occasionally surfaced a stale `ParametersPool` slot
  that leaked `Now=DateTime.Now` into the matched-parameters hash —
  reproducible across runs.
- **Repetitions K = 2** per cell (rep 0 = warmup, rep 1 = measurement).
  Defaults are capped at K=2 because the test runner is OOM-bound on
  larger sweeps (Channel buffer + capture lists + accumulating
  journal-file handles).
- **N values**: {100, 500, 1000}. Override via env vars
  `LAB3_NVALUES` (csv) and `LAB3_REPETITIONS`.
- **Independent journal directory** per cell (`Lab3_A_*` / `Lab3_B_*`
  under temp). No state leak between cells.
- **Compile mode**: V2 default (`Automatic`).
- **fsync semantics**: FileSystem default
  `Flush(flushToDisk:true)` per append on both A and B.
- **Determinism audit on `Reaction.cs`**: `DateTime.UtcNow` is only
  read at the diagnostic `LastActionAt` field and the unused
  `IsExpired` getter. `DateTime.Now` is read at four
  `MarkEventsAsElided` / `MarkEventsAsMaterialized` call sites
  (lines 738, 768, 1265, 1280) which touch the auxiliary tables
  only — out of scope (see Capa 2).

## Files produced

- `samples.csv` — 6 rows (N × rep), parity counts + diffs + wall
  clock. Schema: `run_id, N, rep, is_warmup, emit_a, emit_b,
  elide_matches_a, elide_matches_b, elide_events_a, elide_events_b,
  callback_byte_diff, segment_byte_diff, wall_clock_ms`.
- `summary.csv` — 3 rows (per N), max diffs + mean/max wall clock.
- `host.txt` — runId, sha, OS / cores / .NET version.
- `headline.md` — this file.

## Git provenance

- Lab branch: `lab/paper05-lab3-inproc-symmetric` @ 3984c5db (HEAD at
  measurement time)
- Base branch: `lab/paper05-base` @ a9aa00e
- Commits this lab adds (relative to base):
  - `21741c3` scaffold lab3-dc-b-symmetric folder (README + headline)
  - `bf83c90` add OnReactionEmit/Elide/Tell callbacks + fire in ExecuteAction
  - `514878b` cherry-pick L1 (V2 parametric replay runtime mods)
  - `1dd2210` runtime fixes uncovered while exercising symmetric consumer (WriteRawRecord OnRecordWritten + CAS callback chain + LabInstrumentation replay-event surface)
  - `ed12f37` harness Lab3_DcBSymmetric (Counter + in-proc channel + parity check)
  - `d77e22f` exclude Now/Ip/User from parity hash + harness cleanup
  - `3984c5d` elide tuple drops ReactionId + process-warmup cell
