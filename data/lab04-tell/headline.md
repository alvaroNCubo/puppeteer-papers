# Paper 4 — Cross-actor causation labs (raw output)

Output of the Paper 4 reproducibility lab (*Preserving semantic continuity
across actors*), run against the public Puppeteer runtime at commit
**`6a330b0`** (the paper's Code Provenance), with the harness in
`labs/lab04-tell`.

Paper 4 is qualitative: the lab reproduces the **journals** the paper exhibits,
not timings. `run-6a330b0.txt` is the captured stdout — the three cross-actor
styles side by side, the four property tests, and the separated-receiver run,
every check passing.

`tell` is the assertive speech act: the sender asserts a fact it lived
(`tell PurchaseConfirmed with ... to RewardEngine('rewards-1') once '...'`).

## What it shows (Paper 4 §8.2 + §8.3 + §8.5)

- **Saga** — the joint history lives only in the coordinator's journal
  (3 entries); the Seller (1) and RewardEngine (2) hold half-stories.
- **Choreography** — no actor's journal holds the joint history; the bus log
  (1 entry), external to every program, is the only joint artifact.
- **Tell** — the Seller's own journal holds the joint history: the purchase, the
  assertion (journaled as a typed message-action — define + invocation), and the
  `tell ack` (4 entries), and the round-trip closes inside the program.
- **G1 replay** — a fresh actor reconstructs the in-flight tell from the
  journal alone (no live receiver, no shared transport).
- **G2 cross-DC** — replicating the journal bytes alone carries the cross-actor
  chain to an independent storage tier.
- **G3 audit** — *why did this happen?* is answered by reading the sender's
  journal (the assertion + the ack), with no trace store.
- **G4 tell-fate recovery** — after a crash in the window between a tell's
  journal commit and its post-commit dispatch, the rehydrated sender reconstructs
  the pending tell and the transport testifies its fate; the journal gains the
  logical verdict (`tell '...' unacknowledged by <Addressee>` when Failed, the ack
  when Delivered, nothing while InFlight) — so the journal records each tell's
  *fate*, in its own voice, not just its issuance.
- **G5 separated receiver** — a pure in-process broker carries the envelope while
  the RewardEngine runs its own consumer, maps the assertion to a command it owns,
  and acks autonomously — the carrier/receiver split C3 defends, no bridge
  standing in (§8.2 C3).
- **Negative** — a `tell` outside `.Causation.Continue(...)` is rejected.

All checks are deterministic — counts and journal contents are exact; there is
no timing, hence no run-to-run variance.
