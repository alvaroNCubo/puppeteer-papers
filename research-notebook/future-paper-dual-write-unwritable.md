# A paper trying to get out: the dual-write problem is not solved, it is unwritable

Origin: Alvaro, 2026-08-15, after the Hello Interview distributed-transactions podcast
(youtube.com/watch?v=DOFflggE_0Q). The podcast's recommended production stack — saga with
orchestration + transactional outbox + CDC/poller + idempotency keys, with Temporal as the named
tool — is an assembly of retrofits each team builds around its database. In Puppeteer the same
guarantees are properties of the primitive. That contrast is a paper, not a section of Paper 4
(38 pages already; see `paper04-v0.2-reliability-axis-scoping.md` for what v0.2 keeps).

## The claim, calibrated

**Not** a third consistency category. On the podcast's axis the system is saga-choreography:
eventual consistency, at-least-once delivery with dedup — `OutboxRelay.cs:14-19` says so itself
("Exactly-once EFFECT therefore requires the sink/consumer to dedup on that key"). Conceding this
up front is what makes the rest defensible (configuration claim: concede every element, keep the
assembly).

The claim is about **who authors the guarantees**. The industry's question is "choreography or
orchestration — who coordinates?" The paper's answer: the question dissolves when the failure mode
is not expressible. A `tell` is only valid inside a Reaction's `.Causation.Continue(...)` body
(`TellStatement.cs:87-93`), is journaled by the same `Statement.Write` that commits the decision,
and is dispatched post-commit, outside the write lock (`TellStatement.cs:15-18`). The DSL has no
syntax for I/O in the middle of the verb. The podcast's canonical bug — the confirmation email sent
mid-saga, unrevocable when a later step fails — cannot be written.

Positive symptom, per the standing register rule: what the model *enables* is that the guarantee is
authored by construction; never "sagas shouldn't exist."

## The two subjects (from the 2026-08-15 evaluation)

**1. Durable execution faced head-on.** Temporal/Restate/DBOS claim this exact territory ("write
the flow, we make it survive crashes") and the podcast recommends Temporal by name. The delta to
argue and measure:
- no separate orchestrator service (the fourth actor in the room);
- no workflow-as-code determinism constraints (Temporal's replay demands deterministic workflow
  functions; Puppeteer's replay is of the *domain's* journal, which is the record itself);
- the flow's record is the domain's own journal, not a workflow engine's private state — this
  connects to Paper 4's flow-location result and inherits it rather than re-arguing it.

**2. Respond-at-commit as a defended design decision, not a gap.** The tempting "improvement" —
hold the requester's response until nested tells' ACKs arrive — reintroduces 2PC's blocking through
the back door: the requester's latency becomes the slowest participant's (the podcast's own
indictment of 2PC, min 4:45). The design is: respond when the local decision commits; the fate map
answers "did it land?" afterward (`TellFate.cs`, `ActorHandler.RecoverPendingTells`). If a use case
needs confirmation, it is an opt-in await on the fate — never a change to the commit path. The
paper should state this as a theorem-shaped trade: synchronous confirmation and non-blocking commit
cannot both be primitives; Puppeteer picks the second and makes the first a query.

## Why it is a paper and not a section

- Paper 4's axis is flow-location; this paper's axis is guarantee-authorship. Grafting the second
  onto the first doubles Paper 4's related-work surface and imports the communicator lens the
  series deliberately keeps out of it.
- It **rests on the Concurrency paper**: the non-blocking commit argument (subject 2) is the
  actor-boundary/concurrent-mutation result pointed at cross-actor waiting — a decision must not
  block on state another party settles concurrently.
- It **rests on the aglutinador**: "third way" as a *form/assembly* claim (Perry & Wolf style) is a
  category statement, and category statements live there. This paper cites the category; it does
  not establish it.
- Sequencing consequence: written after both. It is a consumer of their vocabulary.

## What it would have to measure that nothing does yet

- **The podcast's own scenario as a lab** (Eratóstenes, measure don't proclaim): charge / reserve /
  confirmation-email across three actors, crash injection at every point (before commit, in the
  dispatch window, after dispatch before ack, in the relay's at-least-once window at
  `OutboxRelay.cs:83-88`). Count lost and duplicated emails. Expected headline: zero lost, duplicates
  only in the documented relay window, all carrying the deterministic idempotency key
  (`OutboxCommit.cs:50-54`).
- **Moving-parts count** against a reference implementation of the podcast stack: outbox table, CDC
  process, idempotency middleware, orchestrator deployment vs zero added parts in the puppet. The
  baseline must be built, not described (pad thai rule: the evidence for a configuration claim is a
  built alternative).
- **Latency under the respond-at-commit decision** vs a simulated hold-for-acks variant, showing the
  blocking cost the design refuses.
- Possibly: the same flow on Temporal, for the determinism-constraint and private-state contrast to
  be measured rather than asserted.

## What it must NOT claim

- Exactly-once *effect* without consumer dedup (residual requirement is real and documented).
- Cross-restart fate settlement — the in-process fate-map limit stands until a durable fate store
  exists (`paper04-v0.2-g4-fate-recovery-framing.md`); a process restart leaves pending tells
  honestly `InFlight`.
- That compensation logic disappears: the origin journals a verdict the domain can react to
  (`unacknowledged by <Addressee>`), but the *refund itself* is still domain authorship. The paper's
  point is that failure handling is journaled and observable, not that it is free.

## Adoption angle (keep one paragraph, no more)

"You get the transactional outbox without building it" is the one-sentence pitch a platform engineer
understands. It belongs in the introduction as motivation, not as a recurring theme — the register
rule (sober, measured) applies with extra force in a paper whose subject is a hype-adjacent debate.

Code anchors verified 2026-08-15 (Pacífico master): `TellStatement.cs:15-18,87-93`;
`OutboxCommit.cs:5-13,50-54`; `OutboxRelay.cs:14-19,83-88`; `TellFate.cs:6-11`;
`TellRecoveryInfo.cs:3-12`; `ActorHandler.cs:766` (`RecoverPendingTells`), `:3730-3740`
(`ReissuePendingTells`).

Related: `paper04-v0.2-reliability-axis-scoping.md`, `paper04-v0.2-g4-fate-recovery-framing.md`,
`closure-vs-decoupling-and-the-aglutinador.md`, `future-paper-a-fact-must-belong-to-one-actor.md`.
