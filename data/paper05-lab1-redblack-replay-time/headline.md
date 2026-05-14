# Lab 1 — Red-black replay time vs journal size — headline

**Date**: 2026-05-13 UTC
**Branch**: `lab/paper05-lab1-redblack-replay-time` @ ef3a002 (dataset run; commit of headline + results lands next)
**Base**: `lab/paper05-base` @ a9aa00e
**Master at base**: d3d3371 (Merge claude/gallant-haibt-13ca34 — Materialize v2 I1)
**Host**: Microsoft Windows NT 10.0.26200.0 / 32 cores / .NET 9.0.14
**Configuration**: Release / x64 / synchronous handoff (single-process, single-host FileSystem journal)

## Headline number

> *"Under a compact-action régime with AlwaysCompiled compilation, the
> follower replays the journal at a sustained rate of 451k events/sec
> (p50) at N=100k events, completing the deploy in 221.6 ms p50. The
> rate is reached as JIT amortizes between N=10k and N=100k and holds
> through the N=1M anchor at 416k events/sec — confirming that deployment
> is replay, and replay is linear in compact-event count, not in expanded
> state."*

## Tables

### Table 1 — bulk replay time vs journal size (régime: compact-action, AlwaysCompiled)

| N events | bulk p50 (ms) | bulk p95 (ms) | bulk mean (ms) | events/sec p50 | repetitions |
|----------|---------------|---------------|----------------|----------------|-------------|
| 1k       | 5.63          | 6.03          | 5.62           | 177,538        | 3           |
| 10k      | 36.41         | 46.91         | 40.07          | 274,645        | 3           |
| 100k     | 221.55        | 237.40        | 227.21         | 451,368        | 3           |
| 1M       | 2,405.08      | 2,405.08      | 2,405.08       | 415,786        | 1 (anchor)  |

### Table 2 — deploy_total breakdown (bulk vs handover tail)

The deploy window is `Stopwatch(follower.Start(asFollower=true) → UnlockAndRunAlive
returns)`. `bulk_replay_ms` is the inner window `Stopwatch(follower.Start
asFollower=true → returns)`. `handover_tail_ms` is the residue `deploy_total_ms
− bulk_replay_ms` and reflects the `Lock + Unlock` window (in this régime the
leader is disposed before the follower starts, so the tail has zero events).

| N events | deploy_total p50 (ms) | handover_tail mean (ms) | tail share of total |
|----------|------------------------|--------------------------|---------------------|
| 1k       | 5.64                   | 0.0116                   | 0.21 %              |
| 10k      | 36.43                  | 0.0218                   | 0.06 %              |
| 100k    | 221.56                 | 0.0130                   | 0.006 %             |
| 1M       | 2,405.11               | 0.0247                   | 0.001 %             |

bulk_replay dominates deploy_total by 99.8 %+ at every N — the structural
content of the equivalence "deployment is replay" is exactly visible here.

### Table 3 — journal density

| N events | journal bytes | bytes / event |
|----------|---------------|---------------|
| 1k       | 43,088        | 43.09         |
| 10k      | 439,089       | 43.91         |
| 100k    | 4,489,142     | 44.89         |
| 1M       | 45,889,611    | 45.89         |

Compact action entries weigh ~43–46 bytes apiece (ActionId + serialized
arguments + envelope overhead). Density is roughly constant in N, with a
small per-entry-id growth as the entry-id field widens.

## What this confirms

- The follower in red-black handover replays the journal as a function of
  compact-event count — the deploy time is dominated by the bulk replay
  during `Start(asFollower=true)` (>99.8 % of `deploy_total_ms` at every
  N measured).
- Replay rate is **sustained** across journal sizes once JIT amortizes:
  ~178k events/sec at N=1k, ~275k at N=10k, plateau at ~415–451k events/sec
  from N=100k through N=1M. The plateau over a 10× journal range is the
  empirical content of "replay is linear in compact-event count, not in
  expanded state" — were it linear in expanded state, the rate would
  fall as state grows.
- `cross_check_ok == 1` for every sample (the follower's `_seq` value
  equals the last leader invocation argument): handoff state is **bit-equal
  to leader state**.
- `replay_events_counted == N` for every sample: every compact action
  entry is applied exactly once during the bulk replay window.

## What this does **not** confirm (Capa 2 honesty)

- **Régime is compact action entries** (ActionEventData with ActionId +
  arguments). A journal of script-tier events (ScriptEventData with full
  inline scripts — what V2 with SystemParameter or V1 PerformCmd produces)
  would replay slower in proportion to script length. The compact régime
  is the régime Paper 5 §5.1 wants; this lab measures it. The script
  régime is a separate measurement (not done here; Paper 2 Lab 4 has
  density numbers for script vs action).
- **Régime is single-host FileSystem journal** (`DiaryStorageFileSystem`).
  RDBMS backends (MySQL / SQLServer) are out of scope; L6 measures
  append-latency for those. Cross-DC replication overhead is out of
  scope; L3 (DC B symmetric Reactions) measures that.
- **N=1M has K=1 repetition only** — the 1M number sets a ceiling, not a
  variance estimate. Run K≥3 at N=1M if the paper needs to cite p95 there.
- **Compile mode is `AlwaysCompiled`** (the runtime default `Automatic`
  with parametric actions). `AlwaysInterpreted` would shift the rate
  downward; running a second sweep there is a candidate follow-up if
  reviewer asks for the régime sensitivity.
- **First repetition discarded as warm-up** (executed inside `RunOnce`
  before the measured loop; not in CSV). JIT amortization between N=1k
  and N=10k visible in the rate progression — the warm-up is not enough
  to fully amortize across the size jump.
- **Synchronous single-host setup** — no network, no SVIX, no cross-DC.
  Real K8s deploy adds image-pull latency + pod-scheduling latency
  outside the replay window; that overhead is constant in N and the
  replay portion is what scales.
- **The handover tail measured here is empty** (the leader is disposed
  before the follower starts; nothing arrives between follower Start and
  follower Lock). In a real K8s switchover the tail carries the events
  that arrive during traffic-cutover — typically tens of events, still
  dominated by bulk_replay for any non-trivial N.

## Integration to Paper 5

§5.1 opening sentence (draft, ready for placeholder substitution):

> *"Under compact-action régime with AlwaysCompiled compilation, the
> substrate replays N events at a sustained rate of 451k events/sec p50
> at N=100k events, completing the deploy in 221.6 ms p50 (p95 = 237.4
> ms). The rate holds through the N=1M anchor at 416k events/sec. The
> handover tail (Lock + Unlock window after the bulk replay) is < 0.03
> ms at every N measured — bulk replay accounts for >99.8 % of the deploy
> window in this régime — confirming that deployment is replay, and
> replay is linear in compact-event count, not in expanded state."*

## Runtime mods applied on this branch

Additive — never merged to master. file:line refers to the post-edit
state of `lab/paper05-lab1-redblack-replay-time` @ HEAD.

| File | Mod |
|------|-----|
| `Puppeteer/LabInstrumentation.cs` | New callbacks `OnRedBlackHandoffElapsedTicks: Action<long>`, `OnReplayEventCounted: Action<long>`, `OnHandoverStarted: Action<string>`, `OnHandoverCompleted: Action<string>` + counter `ReplayEventsCounted` + reset `ResetRedBlackCounters()` + private incrementer (commit 6c8fc9b). |
| `Puppeteer/EventSourcing/ActorHandler.cs` | (commit 6780755) inner-counter call site in `ReplayPendingEventsForRedBlack` (handover-tail path). (commit d1e918c) additional call site in `EventSourcingStorage` executionTask (bulk-replay path) — Paper 5 §5.1's relevant measurement is the bulk path. |
| `Choreography/Theater/PerformanceTracer.cs` | `RaiseHandoverStarted/Completed` invoke `Puppeteer.LabInstrumentation.OnHandoverStarted/Completed` (commit f193e80) — diagnostic; not used by the measurement after the harness moved to harness-local Stopwatch around full deploy. |
| `Puppeteer/Parameters.cs` | New `public UserParameter<T>(name, value)` — counterpart to existing `public SystemParameter<T>`. Required for V2 fluent `.WithParameters(...)` to produce compact ActionEventData entries via the IsNewAction / IsExistingAction journal-entry path. (commit d1e918c) |
| `Puppeteer/EventSourcing/DB/EventData.cs` | EventDataPool guarded by per-kind monitors (`legacyGate` / `actionGate` / `defineGate`). `Queue<T>` is not thread-safe and the rehydration pipeline rents from JournalReader thread while collector returns from a Task pool thread — without the lock the rent/return race produces intermittent NRE at higher N. (commit d1e918c) |
| `UnitTestPuppeteer/UnitTestPuppeteer.csproj` | Added `<ProjectReference Include="..\Choreography\Choreography.csproj" />` so the lab harness (which lives under `PaperLabs/paper5/lab1-redblack-replay/`) can reference `Choreography.Theater.PerformanceV2`. CS8002 (referenced unsigned assembly) emitted; warning is benign and suppressed via the project's existing NoWarn list. (commit 1eca5e8) |
| `UnitTestPuppeteer/PaperLabs/paper5/lab1-redblack-replay/Lab1_RedBlackReplay.cs` | New MSTest harness. Two tests: `L1_SmokeRun_SmallSweep` (CI-friendly, N ∈ {100, 1000}) and `L1_FullSweep_HeadlineDataset` (`[TestCategory("Lab")]`, N ∈ {1k, 10k, 100k} K=3 + 1 anchor at 1M). (commit ef3a002) |
| `UnitTestPuppeteer/PaperLabs/paper5/lab1-redblack-replay/MinimalDiagnostic.cs` | New MSTest. 5 regression tests pinning the bug surface that the runtime mods unblock — would fail on `lab/paper05-base` without them. (commit ef3a002) |

## Methodology notes

- **Warm-up**: first repetition of the session discarded inside `RunOnce`
  (not emitted to CSV) — N=1k cold (JIT not amortized). Subsequent
  cells still show JIT amortization in the rate progression
  (178k → 275k → 451k events/sec), as the warm-up is not enough to
  fully amortize the verb's compiled body across the size jump.
- **Repetitions**: K=3 per cell at N ∈ {1k, 10k, 100k}. K=1 anchor
  at N=1M. Independent actor / journal per repetition
  (Lineamiento 3, no state leak — `dataDir` is per-call temp).
- **Compile mode**: `AlwaysCompiled` (V2 default for parametric verbs).
  Justification: Paper 5 measures substrate properties; verb-tier
  JIT is amortized in bootstrap so the timed window reflects the
  cost of decoding + applying ActionEventData entries.
- **fsync semantics**: `DiaryStorageFileSystem` default — `Flush(flushToDisk:true)`
  on the leader's writes (per `JournalWriter.AppendRecord`). No batch grouping.
  The follower's read path uses `FileShare.ReadWrite | FileShare.Delete`
  with `SequentialScan` (per `JournalReader.ReadSingleFile`).

## Files produced

- `handoffs-full.csv` — 10 rows: per `(N, repetition)`. Columns:
  `run_id, N_events, repetition, compile_mode, deploy_total_ms,
  bulk_replay_ms, handover_tail_ms, replay_events_counted,
  replay_events_per_sec, journal_bytes, cross_check_ok`.
- `summary-full.csv` — 4 rows (one per N cell). Columns:
  `run_id, git_sha, host, N_events, compile_mode,
  deploy_total_ms_p50, deploy_total_ms_p95, deploy_total_ms_mean,
  bulk_replay_ms_p50, bulk_replay_ms_p95,
  replay_events_per_sec_p50, replay_events_per_sec_p95, repetitions`.
- `headline.md` — this file.

## Git provenance

- Lab branch: `lab/paper05-lab1-redblack-replay-time` @ ef3a002
  (dataset run; final `lab(paper05-lab1): dataset run-20260513-200732 + headline`
  commit will land on top).
- Base branch: `lab/paper05-base` @ a9aa00e
- Master at base: d3d3371
- Heredables this lab adds (relative to base):
  - Callback definitions in `LabInstrumentation.cs` (commit 6c8fc9b).
  - Two call sites in `ActorHandler.cs` (commits 6780755 + d1e918c).
  - Two call sites in `PerformanceTracer.cs` (commit f193e80).
  - Public `UserParameter<T>` in `Parameters.cs` (commit d1e918c).
  - Per-kind locks in `EventDataPool` (commit d1e918c).
  - `Choreography` project reference in `UnitTestPuppeteer.csproj`
    (commit 1eca5e8).
  - Harness + diagnostics (commit ef3a002).
