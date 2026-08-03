# A domain that grew because a requirement pulled it

**Paper 9 §8, second rung — measured.** Paper 9 measures that a change of *staging*
never reaches the domain: zero domain edits across five stagings and six clients.
Its own Limits section says the converse case was never observed — "its second rung
was never climbed here" — because no client ever needed a fact the domain lacked.
Two things, §8 says, are not derivable from what the domain emits and so are the
real second rung: **a new act**, and **authority**. This is the experiment for the
second of those, plus the boundary case next to it.

Two requirements were taken, in this order, because they test different things:

1. **Score** — the *authority* case. Any client could tally it from the frames
   already emitted, and precisely for that reason it must be the domain's single
   word: two clients tallying separately would each have a number and there would be
   no fact of the matter. Maintained by an **existing** verb path (the line
   collapse), so no new act enters the record.
2. **Level** — the *boundary* case. A difficulty level as a rule over lines cleared,
   with **speed left in the stagings**, where it already lives. The domain owns
   *what level the game is at*; each host decides what that costs.

Everything below is counted, not estimated. Every count is reproducible from the
commits and the committed fixtures.

---

## 0. The evidence trail

| commit | what |
|---|---|
| `485b766` | the pre-change base: the domain as Paper 9 measured it |
| `06061aa` | measurement apparatus + eight PRE-change journals + the pre-change replay log |
| `1650765` | **experiment 1**: score in the domain, with tests |
| `4ffe9ff` | experiment 1 ripple (b): the stagings that chose to show it |
| `a766dd2` | **experiment 2**: level in the domain, with tests |
| `120c716` | experiment 2 ripple (b): the stagings that chose to act on it |

Apparatus, all of it outside the domain and outside `Tetris.sln`:

- `Tetris/tools/growth-probe/` — `play` / `step` record a real game through the
  ordinary `TetrisActor` verbs; `query` rehydrates a journal and runs an **arbitrary
  DSL query** passed on the command line. That last part is what makes the
  before/after comparison honest: the same probe binary asks for a fact the domain
  did not have when the journal was written, with no probe edit in between.
- `Tetris/notes/data/journals-pre-growth/` — eight journals recorded before either
  rule existed. Three of them were recorded on **2026-07-01 by the REST host**,
  twenty-four days before this work, and are not mine.
- `Tetris/notes/data/replay-pre-change.log` — the baseline, produced by the frozen
  pre-change build (`TetrisDomain.dll` md5 `89c84de6…`), in which `well.Score` and
  `well.Level` both answer *Unknown property or method on type 'Well'*.

Reproduce a replay with:

```bash
Tetris/tools/growth-probe/bin/Debug/net9.0/TetrisGrowthProbe.exe query Tetris/notes/data/journals-pre-growth/stepped-level2 level2 "print well.Score score, well.Level level;"
```

---

## 1. Experiment 1 — SCORE (the authority case)

### What was added

`Tetris/domain/Scoring.cs` (new) — the tariff, and nothing else: 0/100/300/500/800
points for 0..4 rows collapsed in one landing; more than four is a modelling bug and
throws `TetrisRuleException`. The multiplier is on **simultaneity**, not on the row
count, which is what makes stacking for a quadruple a decision rather than a habit.

`Tetris/domain/Well.cs` — `internal int Score { get; private set; }`, and one line
inside the existing private `Land()`:

```csharp
(Pile, var collapsed) = Pile.Integrate(Active!);
ClearedLines += collapsed.Count;
Score += Scoring.Award(collapsed.Count);   // <- the whole of the change
Active = null;
```

### Shape of the growth

`git diff --numstat 06061aa 1650765 -- Tetris/domain`:

| file | + | − |
|---|---|---|
| `domain/Scoring.cs` | 39 | 0 |
| `domain/Well.cs` | 13 | 3 |
| **total** | **52** | **3** |

Broken down by kind (52 added): **16 code**, 34 doc-comment, 2 blank. Of the 16 code
lines, exactly **two** are in `Well.cs` — the property and the accumulation.

The three deletions are **all XML-doc prose** in `Land()`'s `<summary>`, reworded so
the sentence lists the new step ("hand the piece over, count what collapsed, score
it, and clear the active slot"). **Zero code lines were deleted.**

Was any verb removed or any signature changed? Mechanically, no. Sorting `Well`'s
declared members before and after and diffing them yields additions only:

```
$ diff <(git show 485b766:Tetris/domain/Well.cs | grep -E "^\s+(internal|private|public)\s" | sort) \
       <(git show HEAD:Tetris/domain/Well.cs     | grep -E "^\s+(internal|private|public)\s" | sort)
> internal int Level => Difficulty.LevelFor(ClearedLines);
> internal int Score { get; private set; }
```

Two lines added across **both** experiments; nothing removed, no signature altered.
The verb set is untouched — `Spawn`, `MoveLeft`, `MoveRight`, `Rotate`, `Tick`,
`Drop` — so **no new act enters the record**, which is what distinguishes this rung
from §8's other one.

### Replay of pre-existing journals

Journals recorded before the rule existed, replayed after it exists. The probe
rehydrates each (replaying every recorded act) and queries the result:

| fixture | recorded | lines cleared | pre-change answer | post-change answer |
|---|---|---|---|---|
| `stepped-level2` | 2026-07-25, probe | 18 | *Unknown property* | **score 1900** |
| `stepped-w4h40` | 2026-07-25, probe | 6 | *Unknown property* | **score 600** |
| `old-…-rest-t1` | **2026-07-01**, REST host | 0 | *Unknown property* | **score 0** |
| `old-…-rest-t2` | **2026-07-01**, REST host | 0 | *Unknown property* | **score 0** |
| `old-…-rest-s1` | **2026-07-01**, REST host | 0 | *Unknown property* | **score 0** |

**Nothing broke, and the old games acquired a score retroactively.** This is the
operation-primacy property under test: the journal holds acts (`well.Spawn('I');`,
`well.Drop();`, `well.MoveRight();`), not derived state, so re-applying them under
the new rule re-derives the new fact. Every scalar that existed before — width,
height, cleared lines, over — is bit-identical before and after.

One detail is worth more than the rest. `stepped-level2` cleared 18 lines and scores
**1900**, not 1800. Under this tariff 1900 across 18 lines has exactly one solution:
sixteen singles and **one double**. The replay did not merely count the rows the old
game cleared — it recovered the fact that two of them fell *together*, in a game
played when nothing in the system had any concept of that mattering. The score is
path-dependent history, and the record was enough to reconstruct it.

Three further fixtures (`deep-w4h40`, `mid-w4h20`, `console-w10h20`) replayed
**wrongly, and quietly** — identically before and after the change, logging the same
error at the same EntryIds and then answering anyway with a game that had lost acts:

| fixture | cleared at record time | cleared on replay | score on replay |
|---|---|---|---|
| `deep-w4h40` | 20 | **2** | 200 |
| `mid-w4h20` | 6 | **4** | 500 |
| `console-w10h20` | 0 | 0 | 0 |

The cause is an engine defect, not this experiment, and it has since been fixed
upstream: see §5.1. It is left in this report because of what it demonstrates about
*how to measure a replay* — see the note at the end of that section.

### Ripple

See §3 — measured for both experiments in one table.

---

## 2. Experiment 2 — LEVEL (the boundary case)

### What was added

`Tetris/domain/Difficulty.cs` (new) — `LinesPerLevel = 10` and
`LevelFor(clearedLines) => 1 + clearedLines / 10`; a negative count throws.

`Tetris/domain/Well.cs` — one line:

```csharp
internal int Level => Difficulty.LevelFor(ClearedLines);
```

**Derived, not stored.** This is the sharpest contrast the two experiments draw. The
score *had* to become state, because it is path-dependent: the same board with the
same cleared-line count can be worth 400 or 800 depending on how the rows fell
(`ScoringTests.TheSameRowsClearedDifferently_ScoreDifferently`). The level needs no
state at all, because it is a function of a count the well already kept. It is in the
domain not because the well must *remember* it but because the **rule** — where the
boundaries fall — must be single-valued. That is a real distinction inside §8's
"authority" category, and it was not visible until both cases were built.

### No clock — the excluded case did not arise

Paper 9 §8 names "a domain that must read a clock" as outside its evidence. The
implementation never wanted one: the level is a rule over a count, and *speed* stayed
where it already was — `Tetris/input/ClockSource.cs`, whose own comment says "the
interval is the gravity speed". Non-comment occurrences of
`DateTime|TimeSpan|Clock|Speed|Stopwatch` in `Tetris/domain/`: **0** (two matches
exist, both inside `Difficulty.cs`'s doc comment, explaining why speed is *not*
here).

This is now an executable claim rather than a grep.
`DifficultyTests.TheDomainReadsNoClock` reflects over every type the domain assembly
declares and asserts that no field, property, parameter or return type is
`DateTime`/`DateTimeOffset`/`TimeSpan`/`TimeOnly`/`DateOnly`.

### Shape of the growth

`git diff --numstat 4ffe9ff a766dd2 -- Tetris/domain`:

| file | + | − |
|---|---|---|
| `domain/Difficulty.cs` | 38 | 0 |
| `domain/Well.cs` | 8 | 0 |
| **total** | **46** | **0** |

Broken down (46 added): **14 code**, 28 doc-comment, 4 blank. In `Well.cs`: **one**
code line. **Zero deletions of any kind** — strictly additive, not merely additive in
effect.

### Replay of pre-existing journals

| fixture | lines cleared | pre-change | post-change |
|---|---|---|---|
| `stepped-level2` | 18 | *Unknown property* | **level 2** |
| `stepped-w4h40` | 6 | *Unknown property* | **level 1** |
| `old-…-rest-t1/t2/s1` (2026-07-01) | 0 | *Unknown property* | **level 1** |

A game played on 25 July under a domain with no concept of difficulty is, on replay,
**at level 2** — it had crossed the ten-line boundary before the boundary existed.

---

## 3. THE CONVERSE RIPPLE — how far a domain addition reaches into the stagings

This is the measurement Paper 9 has never taken. Two counts per staging, kept
strictly apart:

- **(a) edits to keep working** — expected zero, and *verified by running*, not by
  compiling. Every staging was run before the change (from a worktree at `485b766`)
  and after it, with the same script and the same signals.
- **(b) edits to adopt** — what it cost to show the score, and to show the level and
  speed the clock up.

### (a) Keep working: zero edits, and zero behaviour change

`git status` after each domain commit: **clean** outside `Tetris/domain`,
`Tetris/domain.tests`. `dotnet build Tetris.sln`: **0 warnings, 0 errors**, all 15
projects, at every step.

Identical smoke runs at `485b766` and after both experiments:

| staging / client | pre-change | after exp 1 | after exp 2 |
|---|---|---|---|
| console (keyboard + wall clock) | PASS | PASS | PASS |
| ai (one op per process) | PASS | PASS | PASS |
| watch (push viewer) | PASS | PASS | PASS |
| watch — pushed frame present | FAIL | FAIL | FAIL |
| observer (journal viewer) | PASS | PASS | PASS |
| server (warm host) | PASS | PASS | PASS |
| send (thin pipe client) | PASS | PASS | PASS |
| input (source merge: pipe + clock) | PASS | PASS | PASS |
| web (WebSockets) | PASS | PASS | PASS |
| web-rest (REST in, SSE out) | PASS | PASS | PASS |
| web-rest — frame endpoint | FAIL | FAIL | FAIL |
| sm-server (StageManager) | PASS | PASS | PASS |
| sm-duo (2 nodes, in-proc) | PASS | PASS | PASS |
| sm-duo-tls (2 nodes over TLS) | PASS | PASS | PASS |

**12 PASS, 2 FAIL, identical in all three columns.** Both failures are pre-existing
and unrelated (§5.2): the push sink emits nothing on this branch, so there is no
frame file for `watch` to read and no frame for the REST endpoint to serve. The
domain addition changed nothing, including not fixing them.

So: **ripple (a) = 0 edits for 12 of 12 stagings, verified by execution.**

### (b) Adopt: only those that chose to, and mostly one line

`git diff --numstat` across each adoption commit. Lines, per file:

| staging / client | adopt score | adopt level | how |
|---|---|---|---|
| console | **0** | **0** | renders through `BoardRenderer` — gets both for free |
| watch | **0** | **0** | same, via `FrameDocument` |
| observer | **0** | **0** | same |
| send | **0** | **0** | a verb carrier with no view at all |
| ai | 1 | 1 | its own `META` line |
| server | 1 | 1 | its own `applied:` line |
| sm-server | 1 | 1 | its own `applied:` line |
| sm-duo | 1 | 1 | its `Describe(WellSnapshot)` one-liner |
| sm-duo-tls | 1 | 1 | same |
| input | 1 | 1 + gravity (below) | status line, then the clock |
| web | 3 (+3/−2) | 3 (+3/−2) | HUD span, player render, observer label |
| web-rest | 3 (+3/−2) | 3 (+3/−2) | same three |
| *shared adapter* `actor/` (not a staging) | +20/−7 over 4 files | +14/−7 over 4 files | snapshot field, query term, projection term, parser, renderer |

Four of twelve stagings adopted **both** capabilities at **zero** cost, because they
render through the shared `BoardRenderer` rather than formatting their own view. Six
paid one line. The two browser hosts paid three, because their view is hand-written
HTML/JS rather than the shared renderer.

**The one adoption that is not display.** `input` is the staging that owns gravity,
and it is where the level does work:

| file | + | − | what |
|---|---|---|---|
| `input/ClockSource.cs` | 14 | 3 | a `Func<int>` ctor; the interval is re-read before every tick (the fixed-`int` ctor stays, delegating) |
| `input/Program.cs` | 10 | 2 | this host's answer: `max(60, clockMs − (level−1)×60)`, plus the banner |
| `input/TetrisStage.cs` | 8 | 1 | expose `Level` off the same snapshot (7 lines) + the status line (1) |

The domain gained no clock; the *staging* changed its clock. Measured live: with a
level standing in at 1 then 5, tick gaps moved from **507 ms** to **265 ms** against
a rule predicting 500 and 260 (§5.3 explains why this was measured in a harness
rather than in a full game).

### Stagings that live on other branches

Three stagings named in the ladder are not on this branch. They were measured **by
inspection of their source on the branches that hold them, not run** — stated
separately because that is weaker evidence than the table above.

| staging | where | (a) keep working | (b) adopt |
|---|---|---|---|
| `sm-cluster` (3 nodes, cross-container, TLS) | `claude/friendly-pare-7ffa3f` @ `974f62a` | **0** — consumes `WellSnapshot` through a `Describe(...)` one-liner exactly like `sm-duo` | **1 line**, the same edit |
| `gesture` (Python webcam client) | `claude/awesome-keller-dedb96` | **0** — an input source; it reads no frame at all | **n/a** — nothing to display |
| `scarce` (emulated ESP32-C6) | `claude/awesome-keller-dedb96` | **0** — its host-side sink keeps emitting the same packed frame | **~4 lines over 2 files, plus a wire-format version bump** |

`scarce` is the interesting one and the only place in the whole system where
adoption is **not** purely additive. Its wire format is positional and
length-bounded by design — `S1;W;H;cleared;flagsHex;rowsHex`, tens of bytes — packed
by `input/ScarceSink.cs` and unpacked by `scarce-device.py` with a fixed
`line.split(";", 5)`. Adding two fields means `S1` → `S2` on both sides, and an
un-updated device would misparse an `S2` frame. Nothing about the *domain* caused
this: it is what a scarce transport costs. Worth saying in the paper, because it is
the one client for which "the domain grew additively" does not imply "adoption was
additive".

---

## 4. Domain build graph — unchanged

| check | before | after |
|---|---|---|
| `dotnet list domain/TetrisDomain.csproj package` | No packages | **No packages** |
| `dotnet list domain/TetrisDomain.csproj reference` | No references | **No references** |
| `Reference`/`PackageReference` items in `TetrisDomain.csproj` | 0 | **0** (the csproj is byte-identical) |
| `using` directives in the two new domain files | — | **0** — `Scoring.cs` and `Difficulty.cs` import nothing at all |

Neither addition needed a dependency. The domain still references no staging and
declares no port; every staging still references the domain.

## Tests

| | before | after exp 1 | after exp 2 |
|---|---|---|---|
| `WellTests` / `PieceTests` / `PileTests` / `ShapeTests` / `DeterminismTests` | 44 | 44 | 44 |
| `ScoringTests` (new) | — | 10 | 10 |
| `DifficultyTests` (new) | — | — | 6 |
| **total, all passing** | **44** | **54** | **60** |

The 44 pre-existing tests were not touched — not one assertion changed — and all
still pass. Four of the sixteen new ones carry the argument rather than the
arithmetic:

- `TheSameRowsClearedDifferently_ScoreDifferently` — two wells, same cleared-line
  count, same (empty) board, scores 400 and 800. The score is history, not a view.
- `ReplayingACommandStream_ReconstructsTheSameScore` — the domain-level counterpart
  of the journal replay.
- `TheLevelFollowsTheLinesEvenWhenTheScoresDisagree` — the level cannot tell those
  two wells apart, and should not.
- `TheDomainReadsNoClock` — the excluded case, as a check.

---

## 5. Limits, and what went wrong that was not our doing

### 5.1 An engine defect that silently truncated three replays — found here, since fixed upstream

**Dated: measured 2026-07-26 against the engine at
`repos/Puppeteer Pacifico` @ `d719ae0` (branch `claude/adoring-wu-77d05a`), which is
what `actor/TetrisActor.csproj` resolves to. Fixed on engine master the same day;
see the end of this section.**

Three of the eight fixtures replayed to the wrong state, **identically before and
after the domain change**, logging the same error at the same EntryIds. Cause, traced
in the journal bytes: after `DEFAULT_PROMOTION_CANDIDATE_THRESHOLD = 10` occurrences
the engine promoted a repeated script to an Action
(`define action 2 () as well.Drop(); end;`), and a **nullary** Action journals its
invocations with an empty arguments blob, which the read path rejected —
`Arguments cannot be null or empty`, `Puppeteer/EventSourcing/ActorHandler.cs:1583`.
A long game played in **one process** trips it; one op per process — the `Tetris/ai`
shape — never does, because the promotion counter is in-memory per handler. That is
why the journals used for §1 and §2 were recorded one op per process, and why they
are unaffected.

The failure mode is worse than a crash, and this is the part worth carrying into the
paper. Rehydration is permissive — it logs and continues — so the actor came back
**started, and answering, with a state missing every act from the promotion onward**.
`deep-w4h40` reports 2 cleared lines and a score of 200 for a game that cleared 20.
`console-w10h20` reports 0 and 0, which is *correct*, because the acts it lost
happened to clear nothing — the truncation is invisible in the very scalars a reader
would check. The same defect was also a live-path hazard, not only a replay one: it
surfaced in `input`'s reaction path during the smoke run
(`Parameters.LoadArguments` via `Reaction.SolveActionReferences`).

**Now fixed upstream, and this lab is one of the two reports that found it.** On
engine master: the read path accepts an empty arguments blob when the Action declares
no parameter (`f67d212`, merged `dd67047`, with 5 regression cases in
`UnitTestPuppeteer/NullaryActionInvocationReplayBugReproTests.cs`), and automatic
Script→Action promotion was removed **wholesale** for an unrelated reason —
text-level rewriting cannot tell an authored *label* from a *value*, so it was
renaming `expose`/`print` aliases and breaking `upgrade('X')` guards (`e65f681`). So
the failure cannot arise in new runs at all, and the three fixtures should replay
whole. A separate defect that also bounded journal reading — a persisted sparse index
hiding committed records — was fixed in the same window (`7dec2a1`, merged
`036b972`).

Two consequences for this report, stated rather than hidden:

- The numbers above are **not reproducible on current engine master**, by design.
  Re-measuring them requires the example's engine reference to move (that checkout is
  21 commits behind master and has uncommitted work) — Alvaro's call, deliberately not
  taken here. No count anywhere else in this report is affected: nothing measured here
  is a journal-size or entry-density figure, which is what promotion's removal would
  change.
- **A methodological lesson for replay evidence.** A truncated replay answers
  plausibly. Had these fixtures not carried a recorded expected value from play time
  (`PLAYED … cleared=20`) to check the replay against, the wrong answers would have
  read as findings — and for one of the three, the wrong answer was indistinguishable
  from the right one. Any future replay measurement in this series should record the
  expected state at record time and assert against it, not merely observe that the
  replay produced a number.

### 5.2 The push sink is inert on this branch (pre-existing)

`FrameFileSink` emits nothing here: the frame reactions skip literal `ScriptEvent`s
("domain reactions observe ActorV2 Actions … not V1-style literal Script commands"),
and this branch's `TetrisActor` issues literal commands. So `watch` sees no frame
file and the REST `frame` endpoint has nothing to serve — in **both** the before and
after columns. Consequence for this experiment: the adoption edits on the *pushed*
frame path (`FrameProjection`, `FrameDocument`) are verified by code and by the typed
query path that every host actually uses, **not** by a live pushed frame on this
branch.

### 5.3 The gravity acceleration was measured in a harness, not in a full game

Every .NET host opens a 10×20 well. To watch gravity speed up in `input` a game must
clear ten lines in a 10-wide well; the probe's policy is far too weak (best of 20
attempts: 3 lines), and the 4-wide pre-change journals that *do* reach level 2 cannot
be rehydrated by a 10×20 host — the engine rejects a differing `upgrade('seed')` text,
which is correct behaviour and not a domain constraint. So the acceleration was
measured by driving the staging's own `ClockSource` with a stand-in level: 507 ms per
tick at level 1, 265 ms at level 5, against a rule predicting 500 and 260. The wiring
from `well.Level` to that `Func<int>` is three lines and was verified by inspection
and by the banner printing `level 1 -> gravity 400ms` in a live run.

### 5.4 Same author, again

§8's own threat to validity applies unchanged: the domain, the stagings and this
experiment share an author, so "the stagings did not need editing" is gameable in
principle. What is *not* author-controlled here is the three 2026-07-01 journals:
they were written by a different session's REST host, three weeks before either rule
was conceived, and they replay into scored, levelled wells without modification.

---

## 6. Verdict

**Both additions were additive, and the claim in §8 survives contact with the case it
had never been tested on.** Precisely:

- **Purely additive at the vocabulary level.** Across both experiments the domain
  gained 2 files and 98 lines (30 of them code) and lost **three lines of doc-comment
  prose and no code at all**. The member inventory of `Well` gained two entries and
  lost none; no signature changed; no verb was removed or renamed; no state was
  reshaped. Experiment 2 was additive in the strict sense — zero deletions.
- **Growth touched existing code without reshaping it.** One line inside the existing
  `Land()` is the entire behavioural change of experiment 1. "Additive" does not mean
  "no existing file was opened" — it means nothing already there had to be
  reconsidered, and that is what the diff shows.
- **The record was enough.** Journals written before either rule existed replay
  under the new rules and acquire the new facts retroactively, down to recovering
  that one of eighteen cleared lines fell as a double. Nothing needed migrating,
  because the journal holds acts, not derived state. (§8's caveat about the real cost
  of event-schema evolution is untouched by this: no schema changed here.)
- **The converse ripple is real but small, and only where invited.** A domain
  addition reached **zero** stagings to keep them working (12 of 12, run before and
  after) and reached only those that chose to adopt it — four of them at zero cost,
  six at one line, two at three. The one staging that wanted the new fact to *do*
  something (gravity per level) paid 32 lines across three of its own files, and the
  domain still has no clock.

Two things should not be smoothed over.

**First, the honest qualification.** The place where "additive" required care was not
the domain but the **adapter**. `WellSnapshot` is a positional record, and its two
new fields had to be *defaulted and placed last* for every existing construction to
keep compiling. Chosen deliberately, and cheap — but it is a design decision that had
to be made, and a different choice there would have turned a zero into a dozen
mechanical edits. Paper 9's build graph has nothing to say about that seam, and it is
where a real project would feel this kind of growth.

**Second, what this experiment does *not* settle.** It climbs the authority half of
§8's second rung. The other half — a **new act**, a verb the domain lacks (hold and
swap, undo a placement, a second player in one well) — is still unclimbed, and it is
the half that adds an entry to the record rather than a rule over the entries already
there. That is the experiment that would test whether *growth of the vocabulary of
acts* is as cheap as growth of the rules over them, and nothing measured here
predicts it.
