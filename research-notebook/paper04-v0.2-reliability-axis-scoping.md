# Paper 4 → v0.2 notes: scope the reliability axis out, point it at the dedicated paper

Source: discussion of the Hello Interview distributed-transactions podcast (2026-08-15). Alvaro's
scoping decision: Paper 4 is already 38 pages cover-to-conclusions; the saga/outbox/durable-execution
material does **not** grow it. v0.2 gets minimal scoping edits plus a future-work pointer; the
substance moves to a dedicated paper (see `future-paper-dual-write-unwritable.md`), which sequences
*after* the Concurrency paper and the aglutinador because it leans on both.

This note **amends** the earlier review finding that Paper 4 must deep-compare against
Temporal/Restate/DBOS: the paper must *acknowledge* the incumbents' claim (so "without orchestration"
does not stand naked), but the deep-compare itself is out of scope. Facing ≠ housing.

## What Paper 4 already has — and why that is enough

The paper compares on exactly one axis: **where the joint cross-actor history is recorded**
(flow-location). §6.1 sagas (`:300`), §6.3 workflow engines incl. Temporal/Cadence/Step Functions
(`:318`), and the three-style case study (§8.3 `:488`) all answer that question, and §8.3's
disclaimer (`:492`) already states the styles are "structural, not competitive." The axis a reviewer
arriving from the sagas literature will ask about — delivery guarantees, the dual-write problem, the
transactional outbox, exactly-once effect, compensations, durable execution's recovery claims — is a
*different* axis, and the paper never claims to cover it. The fix is to say so explicitly, once, and
point forward.

## Concrete edit targets (v0.2)

**1. §6.1 Sagas (`:300`) — one scoping sentence at the end.** After the existing flow-location
contrast, add (adapt to voice):

> The saga literature also carries a second concern this paper does not treat: the reliability of the
> step itself — the dual-write problem, transactional outboxes, idempotent redelivery. Those are
> guarantees about *delivery*, not about *where the joint history is recorded*, and they are the
> subject of separate work; here we note only that the tell's journal entry and its post-commit
> dispatch are one mechanism, not two writes to two systems.

**2. §6.3 Workflow engines (`:318`) — one sentence naming durable execution's claim.** Temporal (and
Restate, DBOS) claim crash-surviving *execution*, not merely externalized flow. Acknowledge it and
scope it: their recovery story is about the engine's own program surviving; the delta against a
journal that is the domain's own record is treated in the dedicated paper. One sentence plus a
forward reference — not a comparison table.

**3. §10 Conclusion / future-work paragraph — the pointer.** Name the future paper's question in one
line: what changes when the outbox is not a pattern applied around a database but a property of the
primitive (`OutboxCommit` writes the outbox row in the same store write that advances the reaction
cursor; a `tell` cannot be written mid-verb). No claim beyond "separate work."

**4. Do NOT add:** the "third way vs 2PC/saga" category claim (that is the aglutinador's, as an
assembly/configuration claim); any exactly-once language (OutboxRelay.cs:14-19 is explicit that
exactly-once *effect* requires consumer dedup); any promise about cross-restart fate (the G4
in-process limit note stands — `paper04-v0.2-g4-fate-recovery-framing.md`).

## Checklist for the 0.2 pass

- [ ] §6.1: append the delivery-vs-flow-location scoping sentence with forward pointer.
- [ ] §6.3: one sentence acknowledging durable execution's recovery claim, scoped, with pointer.
- [ ] §10: one-line future-work pointer to the dedicated paper.
- [ ] Verify no new sentence uses "exactly-once" unqualified or implies verdict recovery across restart.
- [ ] Confirm the three edits add well under a page in the rendered PDF.

Related: `future-paper-dual-write-unwritable.md`, `paper04-v0.2-g4-fate-recovery-framing.md`,
`paper04-v0.2-transport-doctrine-oneliners.md`.
