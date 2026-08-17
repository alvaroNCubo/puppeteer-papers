# Paper 5 handoff: edits applied this session (for the publication pass)

**Status: EDITS ALREADY APPLIED to `05-substrate-operations.md` in the working tree.**
Paper 5 is being prepared for publication in another chat; this note hands off the changes
so that chat keeps them and regenerates artifacts. Only the `.md` was touched — **`.tex` /
`.pdf` / monograph still need regeneration at the publication pass** (papers-pdf-pipeline).

Two origins:
- **Reconciliation with the training-lab guides** (the domain / audit-envelope boundary the
  playbill work surfaced): edits **A** and **B**. Companion Paper 4 edits are tracked in
  `paper04-v0.2-domain-envelope-boundary.md`.
- **The guide-chat auditor's Paper 5 report** (author-decision tensions, not guide bugs):
  edits **#4** and **#3**. The auditor also confirmed **zero contradictions / zero stale
  claims** against the guides; everything else in that report was expected GAPS (no Paper 5
  guide yet → future substrate-ops guide, Topic K).

---

## Edits (all applied)

**A — argument-capture precondition grounds replay determinism.**
- §4.1 (`:180`, new paragraph): the equivalences rest on arguments being *values captured at
  execution time, not expressions re-evaluated on read* — grounded in `Eval`
  capture-at-execution ([Paper 1](01-anti-porosity.md) §4.2, `Parameter.cs:33–36, 163–224,
  228–247`).
- §5.6 (`:466`): "Replay is deterministic" now points to that precondition (was an ungrounded
  assertion).

**B — the audit envelope is a distinct channel, out of scope.**
- §5.2 (`:288`, new paragraph): transmitted entries are the domain program; operational/identity
  metadata is a separate channel keyed to the same entries, outside the equivalences' scope;
  the two channels part ways at the cross-actor boundary → cross-ref Paper 4 §8.2.

**#4 (auditor tension, medium) — carve out tell re-emission from the catch-up claim.**
- §5.4 (`:416`): "Two caveats" → **"Three caveats"**; the third scopes *zero-events-lost* to
  journal drain to the canonical backend and states the measured path does **not** re-emit
  cross-actor tells. Tell re-delivery on replay-style catch-up is a separate tell-primitive
  path (Paper 4), not exercised here.
  - *Why:* the auditor flagged that §5.4's clean-catch-up doesn't carve out the guide's case
    (`topologies.md` §4: multi-event catch-up with tell re-emission). Different subsystems — the
    paper measures journal→state drain, no tell re-emission — so the number stays honest; the edit
    only bounds the generalization. The scoping edit stands on its own (distinct subsystem); no
    in-paper advisory needed. **UPDATE 2026-07-10:** the `task_d6b3dab9` replay-collapse the note
    originally cited was FIXED by commit `64f32e6` (per-invocation replay params; present at the
    pinned anchor `37ad9cf`), so the carve-out is now conservative scoping, not avoidance of a live
    bug — keep the caveat as subsystem-boundary hygiene, drop any "known bug" phrasing.

**#3 (auditor tension, medium-high) — bound E2 to the passive-consumer mode.**
- §5.2 (`:304`): a sentence naming this as the *passive-consumer* arrangement (no coordinator)
  and distinguishing it from a synchronous replicated-actor topology (Director/Cast, elected
  role) — a different mechanism, not measured here. "E2 is the claim that the *passive* path
  needs no coordinator, not that every replication topology dispenses with one."
  - *Why:* `topologies.md` presents replication only via StageV2 Director/Cast + election bus,
    so a reader gets the opposite intuition about whether replication needs a coordinator. The
    edit prevents the misread without importing the topology machinery (kept out per the
    "don't import the communicator topology" discipline).

## Not touched (owned elsewhere)

- **#8 (auditor, low):** hosting guide says `OnHydrated` replaces legacy `OnFirstHydration`,
  paper §7.1 says both are raised. The guide-chat agent is verifying this in code during its
  **sync pass** — left to that agent.

---

## For the publication pass

- [x] A (§4.1 `:180`, §5.6 `:466`) — determinism grounding. **Applied.**
- [x] B (§5.2 `:288`) — audit-envelope out-of-scope channel. **Applied.**
- [x] #4 (§5.4 `:416`) — third caveat, tell re-emission carve-out. **Applied.**
- [x] #3 (§5.2 `:304`) — passive-consumer scope for E2. **Applied.**
- [ ] Regenerate `.tex` / `.pdf` + monograph.
- [ ] Bidirectional cross-refs to re-check if sections renumber at layout: Paper 5 §5.2 ↔ Paper 4 §8.2; Paper 5 §5.4 → Paper 4; Paper 4 §8.5 → Paper 5 §5.2.
- [ ] Nothing retracted: tesis, four equivalences, labs, claims intact — these are grounding + scope clauses in the paper's own Capa-2-honesty style.
