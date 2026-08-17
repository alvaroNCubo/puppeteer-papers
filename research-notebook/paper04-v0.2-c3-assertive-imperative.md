# Paper 4 → v0.2 notes: ground C3 definitionally in assertive-vs-imperative vocabulary

Source: reader review of v3 against the training-lab guides (`topologies.md` §3, `saga.md`),
tying back to prior review comments **M-NUEVO-2** (choose: is the saga/tell contrast *definitional*
or *empirical*? — reviewer recommended definitional) and **M-NUEVO-3** (the lab collapses carrier
and receiver).

Verified against: `04-cross-actor-continuity.md` (§6.1, §7.1, §8.2, §8.5), `labs/lab04-tell/Program.cs`,
`training-lab/guides/topologies.md`, `training-lab/guides/saga.md`, `training-lab/exercises/EX-025-communicator-relay.md`.

---

## Verdict

The reviewer's central recommendation is **correct and worth taking**: make the "no external author"
property of C3 *definitional* rather than a description of the bridge's good behavior. But two of the
supporting claims are **already addressed** in the current version and should not be re-done:

- **M-NUEVO-3 is already closed.** The lab now has `G5_SeparatedReceiver` (`Program.cs:495-524`):
  a pure `InProcessBroker` carrier + an autonomous `BrokerTellConsumer` receiver that maps the
  assertion to a command it owns and acks on its own — no bridge stands in. §8.2 already narrates it
  (the parenthetical at `04-cross-actor-continuity.md:474`), and the lab README lists it (`:37-40`).
  Carrier and receiver are genuinely separated. We do **not** need the communicator relay merely to
  get a separated receiver.

- **The contrast is already in the paper — in embryo, unnamed.** §6.1 (`:308`) already ends on
  "*a compensating action itself another asserted fact (`SaleReversed`) rather than a coordinator's
  command*," and §8.2 (`:482`) already draws the assertion/command distinction by verb tense.
  The gap is not that the idea is missing; it is that the idea is **never promoted into C3's
  definition**, so C3 still reads as behavioral, not structural.

So the real work for v0.2 is a **synthesis edit**, not a new case study: name the axis the paper
already half-uses, lift it into §7.1/§8.2, and let it carry C3.

---

## The upgrade: what "no external author" actually rests on

The guides supply the structural reason the paper argues only in prose:

- **`topologies.md` §3.** An intermediary's endpoints are *assertive* or *imperative*. The
  `communicator` (a replicated `StageV2` relaying facts) has a **purely assertive vocabulary** — it
  says "there is a new coin"; **it owns no `create`**. Each `Performance` receiver has an
  **imperative vocabulary** — its own `addCoin` creates the coin locally. The carrier does not, and
  *cannot*, author what the receivers do; it can only re-state a fact for each to interpret.
- **`saga.md`.** The saga coordinator's vocabulary is the opposite: its steps are imperative
  `PerformCommand`s (`inventory.Reserve`, `inventory.Confirm`). The framework provides **no rollback
  primitive** — "compensation is a pattern YOU author," a compensating *command*. The coordinator's
  whole vocabulary is directive; it authors by construction.

Put together, this draws the author/carrier line by **vocabulary, not by behavior or by
being-a-pipe-vs-being-an-actor**:

> An external **author** owns an imperative vocabulary — it issues directives that decide each
> actor's next step (the saga: `Reserve`, `Confirm`, `Charge`). An external **carrier** owns a
> purely assertive vocabulary — it can only re-assert facts (the communicator owns no `create`).
> The distinction is not restraint; it is *which verbs the intermediary possesses*. A carrier
> **cannot** author the flow, because authoring means directing and it has no directive to issue.

This is strictly stronger than the current §8.2:474 defense ("the bridge only carries, doesn't
author"), which reads as a promise about the bridge's conduct. The vocabulary framing makes it a
property of the construct.

### The sharpest form — and the integration v0.2 should make

The mood distinction the paper already draws at the **message** level (assertion vs command, §8.2:482)
is *the same distinction* that makes C3 structural at the **actor** level:

> **Assertive routing preserves receiver autonomy by construction.** A fact is *interpreted*; a
> command is *obeyed*. Because the carrier emits only facts, every receiver still decides its own
> response — which is exactly what C3 requires ("each actor decides its own response," §7.1). An
> imperative intermediary (the saga) removes that autonomy; an assertive one cannot. So the tense/
> mood distinction of §8.2 is not decoration — it is *why* the no-author property of C3 holds.

Right now §8.2's tense paragraph (`:482`) and C3 (`:474`, §7.1:`364`) sit apart. Unifying them —
mood is what grounds C3 — is the single most valuable change and the clean resolution of M-NUEVO-2:
**the contrast is definitional**, and here is the definition.

---

## Concrete edit targets

1. **§7.1, Condition C3 (`:364`).** After "not an external *carrier* of it," add one sentence
   grounding the author/carrier line in vocabulary:
   > *An author is an intermediary whose vocabulary is imperative — it issues the directives that
   > decide each actor's next step; a carrier's vocabulary is assertive — it can only relay a fact,
   > owning no command to issue. What C3 forbids is the imperative author, not the assertive carrier;
   > and because an assertive relay emits facts to be interpreted rather than commands to be obeyed,
   > each receiver still decides its own response.*
   Keep it to 1–2 sentences here; C3 is a definition, not a case study.

2. **§8.2, C3 clause (`:474`).** Replace the behavioral "the bridge only carries that assertion
   onward" framing with the structural one: the carrier *has no verb with which to author* — it
   forwards an assertion; only the receiver owns the imperative command that acts on it. Tie the
   saga contrast to vocabulary: *its state machine authors because its vocabulary is imperative
   (`Reserve`, `Confirm`), which is why folding it into a participant would dirty that domain with
   directives it does not own.* (This also lets you cite `saga.md`'s "no rollback primitive /
   compensation is author-written" as reinforcement.)

3. **§8.2, tense paragraph (`:482`).** Add the bridge sentence: the past-tense/assertive mood is not
   only about what the *sender* may truthfully say — at the routing level it is what keeps an
   intermediary a carrier rather than an author. One sentence linking `:482` back to C3.

4. **§6.1 (`:308`).** The `SaleReversed`-vs-coordinator's-command remark is the same axis; name it
   ("assertive vs imperative") so the reader meets the distinction here and recognizes it when it
   grounds C3 in §7.1/§8.2.

---

## The communicator relay: optional witness, not a new lab scenario

What the communicator relay adds **beyond G5**: G5's carrier is a *transport* (a pipe — trivially
unable to author). The communicator is an **actor** that *could* structurally host commands yet owns
only an assertive vocabulary — so it is the interesting witness that the author/carrier line is drawn
by vocabulary, not by "transport vs actor." An actor-in-the-middle that still cannot author.

**Recommendation: cite it conceptually; do not build it into Paper 4's lab.** Reasons:

- **Scope.** Paper 4 is scoped to *flow-location* (where the record of the send lives). The full
  communicator relay drags in the topologies layer — replicated `StageV2`, `.DirectorOnly()` gating,
  directed per-instance tells, the binding table. That machinery is the topologies/aglutinador's
  territory, not this paper's. Importing it would overload §8 and blur the paper's claim.
- **Honesty caveat (`topologies.md` §4).** ~~The months-later, multi-event catch-up is a known,
  unfixed bug (sender-side envelope-dedup collapse on replay, `task_d6b3dab9`).~~ **UPDATE 2026-07-10:
  that collapse was FIXED by commit `64f32e6` (per-invocation replay params) — and the fix is already
  present at the public anchor commit Paper 4 pins (`37ad9cf`), verified by regression
  `TellMultiEventReplayTests.cs`.** So multi-event replay now reconstructs every per-event tell
  identity correctly; the only remaining limit is exactly-once fate SETTLEMENT across a cold restart
  (in-process broker fate → honestly PENDING, not lost). `EX-025` still scopes the catch-up out, now
  for that narrower reason. Any communicator material in the paper can stay live-flow-only for
  simplicity, but it need NOT imply an id-collapse bug — there is none at the pinned commit.

If you want the actor-carrier witness in the paper at all, the lightweight option is one sentence in
§8.2 or §8.4: *"An intermediary can be a full actor and still be a carrier, not an author, provided
its vocabulary is assertive — a relay that owns no command."* That earns the definitional point
without importing the topology. G5 remains the runnable carrier/receiver evidence.

---

## Checklist for the 0.2 pass

- [ ] §7.1 C3: add the vocabulary grounding (author = imperative, carrier = assertive) — 1–2 sentences.
- [ ] §8.2 C3 clause (`:474`): behavioral → structural ("owns no command to issue"); tie saga to imperative vocabulary + cite `saga.md` (no rollback primitive).
- [ ] §8.2 tense paragraph (`:482`): link mood to C3 (assertive routing preserves receiver autonomy).
- [ ] §6.1 (`:308`): name the assertive/imperative axis so the reader meets it early.
- [ ] Note in the reviewer response that **M-NUEVO-3 is already closed by G5** (no new test needed).
- [ ] Do **not** import the replicated-StageV2 communicator topology; at most one sentence as a conceptual witness, live-flow only.
- [ ] Keep the register andragogical/definitional (argued, not surveyed) per M-NUEVO-2.
