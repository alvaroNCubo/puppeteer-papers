# Lab 4 — Cross-actor causation (Paper 4 §8)

A reproducibility harness for *Preserving semantic continuity across actors*
(Paper 4). Paper 4 is qualitative — its evidence is **journals**, not timings —
so this lab runs the same loyalty scenario under three cross-actor styles, four
property tests, and a separated-receiver run against the public Puppeteer
runtime, printing each actor's journal so the rendered entries can be diffed
against the journals shown in the paper.

`tell` is the assertive speech act: the sender asserts a fact it lived
(`tell PurchaseConfirmed with ... to RewardEngine('rewards-1') once '...'`); it
names no receiver method and no transport.

Run against the public Puppeteer runtime commit `6a330b0` (Paper 4 Code
Provenance).

## What it runs

- **Style 1 — Saga (orchestrator)** — the joint history lives in the
  coordinator's journal; the participants hold half-stories (§8.3 Style 1).
- **Style 2 — Choreography (event bus)** — no actor's journal holds the joint
  history; the bus log is the only joint artifact (§8.3 Style 2).
- **Style 3 — Tell** — the sender's own journal holds the joint history: the
  purchase, the assertion (journaled as a typed message-action — define +
  invocation), and the ack (§8.2, §8.3 Style 3).
- **G1 — Replay coherence** — a fresh actor reconstructs the in-flight tell
  from the journal alone (§8.5 G1, closes §5.2).
- **G2 — Cross-DC replication** — replicating the journal bytes alone carries
  the cross-actor chain (§8.5 G2, closes §5.3).
- **G3 — Audit query** — the cause-effect chain is read from the sender's
  journal (§8.5 G3, closes §5.1).
- **G4 — Tell-fate recovery** — after a crash in the window between a tell's
  journal commit and its post-commit dispatch, a rehydrated actor reconstructs
  the pending tell and the transport testifies its fate; the journal records the
  logical verdict `tell '<id>' unacknowledged by <Addressee>` (Failed), the ack
  (Delivered), or nothing while it stays pending (InFlight) (§8.5 G4).
- **G5 — Separated receiver** — a pure in-process broker carries the envelope
  while the RewardEngine runs its own consumer, maps the asserted message to a
  command it owns, and acks autonomously — the carrier/receiver split C3 defends,
  with no bridge standing in for the receiver (§8.2 C3).
- **Negative** — a `tell` outside `.Causation.Continue(...)` is rejected (§8.2).

Each check prints PASS/FAIL; the process exits non-zero if any fails.

## Contents

- `Program.cs` — the harness (one method per scenario; prints each journal and
  a PASS/FAIL line per assertion).
- `LoyaltyDomainStubs.cs` — the didactic domain (`Seller`, `RewardEngine`,
  `Campaign`).
- `lab04-tell.csproj` — references the public Puppeteer runtime and Choreography
  (the broker transport G5 uses); `AssemblyName` is `Lab04Tell`.

## Reproducing

1. Clone the runtime at the cited commit (as a sibling of this repo, the
   convention lab01/lab02/lab03 use):
   `git clone https://github.com/alvaroNCubo/puppeteer && cd puppeteer && git checkout 6a330b0`
2. From this project (its `ProjectReference`s point at
   `..\..\..\puppeteer\Puppeteer\Puppeteer.csproj` and
   `..\..\..\puppeteer\Choreography\Choreography.csproj`):
   `dotnet run -c Release`

The captured output is in `../../data/lab04-tell/run-6a330b0.txt`. The lab is
deterministic — journals and counts are exact, with no timing variance.

## Note on the journal-read grant

The lab reads the in-memory journal (`DiaryStorageInMemory` / `EventData`) to
print and count entries — the same internal surface lab02 uses. The framework
grants it via `InternalsVisibleTo("Lab04Tell")` (keyless in the public repo;
keyed in the private fork, where the sync strips the key). A pure public-API run
is not possible because the journal-inspection surface is internal; the grant is
the minimal, precedented way to exercise and inspect it.
