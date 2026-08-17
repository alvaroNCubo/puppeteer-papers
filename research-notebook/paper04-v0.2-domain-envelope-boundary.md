# Paper 4 → v0.2 notes: the domain / audit-envelope boundary at the cross-actor seam

**Status: EDITS ALREADY APPLIED to `04-cross-actor-continuity.md` in the working tree
(this session), pending the v0.2 release pass (tex/pdf/monograph regen).** Unlike the
sibling v0.2 notes, which are proposals, this note records changes that are *already in
the `.md`* — the v0.2 pass only needs to keep them and regenerate artifacts.

Source: a reader discrepancy between what the training-lab guides permit and what Papers 4
and 5 say. The guides expose two distinct channels — the domain channel (`WithParameters`,
values that cross the wire) and an **audit-metadata envelope** (`WithPlaybill`, In-only,
frozen, an EntryId-linked side-store) — while Papers 4/5 speak of "arguments" / "content"
monolithically, as if the journal held everything the program does. The gap was closed in
code first (Pacifico master `2f1b295`: `WithPlaybill` now guards `Parameter.In` only via
`PlaybillParameterGuard`; `ShadowConfig.CarryPlaybill` makes the envelope's travel to a
shadow opt-in). These notes close the *prose* gap so Papers 4/5 match the guides.

Verified against: `04-cross-actor-continuity.md` (§3, §5.1, §8.2, §8.5 G3), the reconciled
travel map (replication opt-in per-Cast; backup by DB; **tell A→B carries no envelope**;
shadow opt-in), and the companion edits in `05-substrate-operations.md` (§4.1, §5.2).
Cross-repo memory: `playbill-domain-envelope-boundary.md`.

---

## Verdict

No factual error to retract. Paper 4 is already careful — §3 (`:159`) classifies
correlation IDs as *operational* (not in any program), and §5.x scopes auditability to *the
causal chain*. The gap is **imprecision of a single channel**: the paper never states, at the
cross-actor seam, that only domain values cross a tell and that the sender's operational
envelope stays local. Two small additions make the two-channel model explicit and reconcile
the paper with the guides. The tell stays scoped to flow-location; no communicator topology
imported (consistent with `paper04-v0.2-c3-assertive-imperative.md`).

---

## What changed (already applied)

**C — §8.2, what crosses in a tell (`:470`, new paragraph after the define-and-invocation
paragraph).** Makes explicit that the RewardEngine receives *the asserted fact and its
values, and only those*; parameter names, the message definition, and any operational
envelope the sender keeps about the act do **not** travel. A value the receiver's domain
needs must be *in the assertion* (an argument), because the receiver maps the message to a
command it owns and can read nothing the assertion did not say. Framed as the cross-actor
face of the *operational*-vs-program distinction of §3 — no new code cited; it extends the
"operation and its values separately" anchor already at `:468`.

**D — §8.5 G3, causal vs identity audit (`:695`, new paragraph after the G3 result).**
Scopes claim 8: the audit G3 closes is the *causal* one (what was said, to whom, when, with
what ack) — what the program records. The *identity* audit (which user/tenant/request
context) is operational metadata (§3), in a separate envelope keyed to the entries, not the
cross-actor causal record. The two audits answer different questions and travel on different
channels. Cross-ref to Paper 5 §5.2.

Both additions are argued/definitional (register per M-NUEVO-2), anchored to existing §3 and
to the existing values-separately citation; neither cites the Pacifico playbill code (out of
Paper 4's pinned snapshot and out of scope — the envelope is named, not specified).

## Companion edits in Paper 5 (coordinate with the Paper 5 publication chat)

These are already applied in `05-substrate-operations.md` and are the other half of the same
reconciliation:

- **§4.1 (`:180`)** — argument-capture precondition: replay is deterministic because
  arguments are *values frozen at write time, not expressions re-evaluated on read* (grounded
  in `Eval` capture-at-execution, Paper 1 §4.2, `Parameter.cs:33–36, 163–224, 228–247`); §5.6's
  "Replay is deterministic" now points to it.
- **§5.2 (`:288`)** — the audit envelope is a distinct channel keyed to the entries, outside
  the equivalences' scope; the two channels part ways at the cross-actor boundary
  (cross-ref → Paper 4 §8.2).

Bidirectional cross-refs now exist: Paper 4 §8.5 → Paper 5 §5.2; Paper 5 §5.2 → Paper 4 §8.2.
If either paper renumbers sections at layout, re-check these.

---

## Reviewer follow-up (2026-07-05): elevate the demarcation to its one-line test

The same reviewer (guide vs papers) singled out the **playbill demarcation rule** as *the best
operational articulation in the whole corpus* of the programmatic/operational distinction —
and it serves Papers 4 and 5 equally. The rule is the placement decision for any single value:

> **"Does a domain rule depend on this value?"** — Yes → it is a **parameter** (domain input,
> crosses the wire). No → it is **playbill** (operational envelope: who/when/why, for audit,
> routing, correlation; In-only, frozen, stays local).

This is the same move as the transport one-liners (`paper04-v0.2-transport-doctrine-oneliners.md`):
§8.2 already states the *content* of the two-channel model ("only domain values cross"), but
never states the **rule as a reusable, testable question**. v0.2 should surface that question as
the crisp form — it is the finest-grain drawing of the operational-vs-program line the corpus has.

- **Placement:** Paper 4 §8.2 (as the demarcation test alongside the what-crosses paragraph)
  **and** the aglutinador (where it is the corpus-wide statement of the distinction, spanning
  Papers 1/4/5 by altitude). **Paper 5 stays as is** — user-confirmed 2026-07-05; do not import.
- Register: definitional/andragogical; do not cite the playbill code in-paper (out of pinned
  snapshot). Cross-ref memory `playbill-demarcation-articulates-prog-operational.md`.

## Checklist for the 0.2 pass

- [x] §8.2 (`:470`): what-crosses paragraph — only domain values cross; envelope stays local. **Applied.**
- [ ] §8.2: surface the demarcation as the one-line test ("does a domain rule depend on this value?").
- [x] §8.5 G3 (`:695`): causal-vs-identity audit scope note. **Applied.**
- [x] Companion Paper 4↔5 cross-refs wired. **Applied.**
- [ ] Regenerate `.tex` / `.pdf` + monograph at v0.2 release (papers-pdf-pipeline).
- [ ] Reviewer response: note that the two-channel model now matches the guides (`WithParameters` vs `WithPlaybill`); no claim retracted.
- [ ] Keep register andragogical/definitional; do not specify the playbill code in-paper (out of pinned-snapshot scope).
