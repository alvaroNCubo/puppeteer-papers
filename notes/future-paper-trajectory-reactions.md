# Recognition, returned: reactions with a cursor

Alvaro, 2026-07-26, developed with an external reader. Recognition was compressed out of Paper 9 to
keep that paper about identity. It came back larger, and this is where it lives until it is a paper.

## The idea

A `Reaction` today answers a closed question: the pattern matched, or it did not. A **trajectory
reaction** would stay alive while a narration forms — holding a cursor on a partially realized
trajectory, estimating how far along it is, and exposing which continuations are admissible.

```
Trajectory: Dance        Journal so far:  Jump, Jump, Flex
Pattern: Jump Jump Flex Turn Step
Progress 3/5   ·   Turn 0.78   Step 0.14   Stop 0.08
```

Three moments that a boolean matcher collapses into one: **recognition** (this resembles dancing),
**anticipation** (a turn is probably next), **intervention** (prepare, suggest, or cause the
continuation).

The formulation worth keeping: *a present act changes the space of intelligible futures for the
entity.* And its consequence for the journal — it stops being only a record of what was done and
becomes the evidence from which a Puppet locates itself among partially realized trajectories.

## What exists, verified at Pacífico master

- **Shadow mode.** `PuppeteerCli attach --primary --snapshot` builds the primary actor, creates an
  isolated `Shadow`, replays the journal to the head, and enters a REPL. The primary's journal stays
  intact — *"The AI is free to make mistakes"* (`PuppeteerCli/AttachCommand.cs:17-23`, `:61-125`).
- **PlainText mode.** With `--txt` the text file **is** the canonical journal and there is no Shadow;
  each command appends a real entry (`AttachCommand.cs:151-181`). So an AI already has an *executable
  narration it can continue*, not a film to watch.
- **`--live` is future and the code says so**: *"only mode supported today; --live arrives later"*
  (`AttachCommand.cs:292`, `DescribeCommand.cs:142`, `Program.cs:117`).
- **The trajectory DSL is entirely real.** `Seek` / `ThenSeek` / `ThenFinalSeek`
  (`Puppeteer/EventSourcing/Follower/Reaction.cs:649`, `:664`, `:682`), with `Many`, `One`, `Exactly`
  and `Where` (`ReactionEngine.cs:164`, `:272`, `:278`, `:87`), and `Cue` / `Job`
  (`Reaction.cs:44`, `:49`). Order, cardinality, filters, finalization and a performative conclusion
  are all there.
- **An aggregate cursor already exists, and is already public.** Each `ReactionEngine` — one per Seek
  stage — keeps `seekEntered` and `seekMatched`, exposed as `SeekEntered` / `SeekMatched`
  (`ReactionEngine.cs:335-339`, Interlocked, with a `ResetSeekCounters`). Their stated purpose is to
  *"diagnose drop-off per stage when a multi-Seek Reaction underfires"* — that is, to see where a
  trajectory stalls. So the distance from what exists to a trajectory cursor is, at the aggregate
  level, **a change of reading rather than of machinery**, which is the move this series makes
  everywhere else.
- **What does not exist is per-candidate identity.** The counters are aggregates over the actor's
  life. *17 active candidates, best cursor 4/5* needs to know that seventeen trajectories are open and
  which is at which step, and nothing carries that. Branching, abandonment, expiry and absorption are
  all properties of a *candidate*, not of a stage, so they are downstream of that missing piece. That
  is the real work, and it is larger than the DSL suggests.

So the instrument is built and the concept is not. That is a good position to write from, and it must
be stated that way rather than blurred.

## What this inherits from Paper 9 — the part worth thinking about first

**1. It inherits the granularity gap, and makes it worse.** A trajectory reaction matches on *acts*.
Paper 9 §6 states plainly that the paper has a criterion for a record's vocabulary and **none** for
its granularity — nothing says which moments must be acts — and names that as the question it is most
exposed on. A trajectory recognizer cannot be built on an unsolved version of that, because the
pattern *is* a sequence of acts.

Worse: Paper 9's measured hazard is that an incomplete record **answers plausibly** — a gap yields a
smaller coherent story, not an error. A boolean matcher that misses is at least silent. A cursor that
misses reports a **confidence** — a number that looks like knowledge. Ninety per cent dancing,
computed over a record missing the act that would have said otherwise, is worse than no answer, and
it is the same failure mode wearing a decimal point. Any paper on this owes that hazard a section.

**2. It inherits the decomposition constraint.** A fact that is a join over two roles cannot be
emitted by either (Paper 9 §8.4). A trajectory spanning two actors is the same shape: neither actor's
reaction can hold a cursor over it, for the same reason. So trajectories bound how a domain may be
cut, exactly as joint facts do — or the recognizer has to live outside both, which is an ordinary
adapter and a different design.

## The axis: a subsystem of the entity, not an AI looking in

Alvaro's correction, and it changes what this is. The first framing had an AI outside — peering in,
whispering to the entity, proposing rules. Under that framing the anticipation is an external
inference and Paper 8's question reopens: who is entitled to say this happened?

It is not that. **Anticipation is a subsystem of the entity itself.** The Puppet recognizes its own
trajectory and anticipates its own continuation; the AI is a way of building and sharpening that
subsystem, and the phenomenon exists whether or not an AI is involved — a Puppet that has seen
*Jump, Jump, Flex* many times and observed that *Turn* usually follows is already anticipating.

That resolves half of the Paper 8 problem and sharpens the other half, and the halves should not be
run together:

- **Resolved: authority.** Paper 8 established that the actor has authority over what constitutes its
  output. If the party inferring is the entity, the authority question is answered in advance rather
  than reopened.
- **Not resolved, and sharper: the record.** The entity would be journaling something it **inferred**
  rather than something it **did**. That is the one boundary the change of axis does not cross, and
  it is the good question. The substrate already has a knob adjacent to it — `ShadowConfig.CarryPlaybill`
  is off by default, journal-only replay, and opted into for a *forensic* shadow as against a
  *behavioural* experiment — so the distinction between rehearsing and recording is already
  something the machinery can express.

Worth noting too that *trajectory* already exists in the substrate's own vocabulary, in the elision
criterion: observable → elide, trajectory/audit → retain. The word arrived before the concept did.

## What this still owes Paper 8

With the axis corrected, only one half is owed, and it is the record. Is `90% dancing` an **act**?
If it is journaled, the record holds inferences alongside acts and the series' distinction between
what was done and what is projected over it is compromised. If it is not, the cursor is ephemeral and
rebuilt on every replay — coherent, probably right, and to be argued rather than assumed.

**The division of labour the reader draws is sound and it is Paper 8's own**: the model proposes
probabilistically, the Puppet validates operationally — does the verb exist, is it executable now, by
whom, with what parameters, does it violate a rule. Probability without authority to execute,
followed by validation by the party that holds the authority. That is worth stating in exactly those
terms, because it is the series' own vocabulary and it makes the AI a *proposer*, never an author.

The reader's other structural point: what the model should propose is not rules. `if A then B` is too
flat. What a journal can yield is *A tends to open T; B advances it; C forks it; D completes it* — a
structure of action, and the AI helping the Puppet acquire a vocabulary for its own conduct.

## Where Cue and Job land

Both acquire a second function under this framing, and it is not merely execution timing. A `Cue`
keeps the immediate continuity of a trajectory — prepare the turn, reserve the resource, warn the
observer — cheap, local, near. A `Job` carries recognition that exceeds the present local: compare
many trajectories, consult other Puppets, look for long-range anomalies, propose new trajectories.
So an entity can hold *distributed reflection about its own history* while its computation happens in
many places.

Stated carefully, because it is easy to get wrong: `Cue` and `Job` are **temporal and distributive
strategies**, not an ontological classification. They say *when* and *where* a consequence runs, not
*what kind of thing* it is. Reading them as two categories of recognition would be the same error as
reading relation properties as kinds of trajectory. What is true is narrower — a trajectory's
consequences can be near and cheap or deferred and distributed — and whether that supports anything
about a capacity is a question for a measurement, not for the classification.

## A register warning

The names offered — *An Anticipatory Ontology of Action*, *Ontología operacional de trayectorias* —
are precisely the grandiloquence this series has spent every review round removing. Alvaro's standing
instruction is engineer register, sober, measured, no manifesto. A paper called *Reactions with a
Cursor*, or *Recognizing a Trajectory Before It Ends*, that measures one worked recognizer on the
Tetris journal, will be worth more than one named for an ontology. Keep the ontological framing in the
aglutinador if anywhere, and keep it out of a title.

## The first thing to build

The first number needs no new engine. `SeekEntered` and `SeekMatched` are public and per stage, so a
base rate is already computable: if stage 4 was entered a hundred times and matched seventy-eight,
then *given the trajectory is at stage 3, the next act is stage 4's with about 0.78* is a statement
the substrate can already make about itself. Compute it over the Tetris journal — Lab H already
recognizes a placement retrospectively — and check it against what actually happened next.

If that number means nothing, the rest is not worth building. If it means something, the next piece is
per-candidate identity, and only then cursors, branching and abandonment.

And the three organs the DSL currently ties in one knot are worth separating before any of that, since
it invents nothing: the `Seek` chain is the **trajectory**, the action of `Continue(check, action)` is
the **reaction**, and that `check` together with the `Where` clauses is the **admissibility**. Splitting
`DefineTrajectory` from `ReactTo` is what turns *Bailar* from the name of a rule into an entity of the
repertoire.

Related: [[future-paper-a-fact-must-belong-to-one-actor]],
[[future-paper-incomplete-records-answer-plausibly]], [[paper9-distributed-observation-brief]],
[[closure-vs-decoupling-and-the-aglutinador]].
