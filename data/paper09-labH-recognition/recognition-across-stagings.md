# Recognition across stagings — the lab §6 asserts and did not have

**What §6 claims.** Paper 9 §6 argues that because the domain keeps its identity
across every staging, the *routine* its acts compose is recognizable across
stagings too. It then concedes, in its own words, that the step "is argued and
not measured": none of Appendix A's six labs recognizes a routine at all, so
none recognizes the same routine on two stagings. §6 says such a lab would be
cheap because the journals already exist. This is that lab.

**The definition worked from** (Paper 3, restated in §6): a *routine* is a
pattern over journal entries together with the correlation that binds them into
one trajectory — a reaction seeks an opening entry and then a closing one, and
what it matches between them is the routine.

**Result in one line.** The same reaction, the same compiled assembly, recognizes
the same routine — the placement of one piece — on the single-process staging and
on all three nodes of the cross-container TLS staging, in the same count and the
same order, with an empty diff over `Tetris/domain`. The lab also produced two
findings that cut the other way, and they are the more interesting half: the
domain journals **no correlation handle**, so the trajectory is bound by order
alone; and the domain journals **no landing act**, so a placement that ends under
gravity has no closing entry and the order-only reaction mis-binds it. Both are
measured, not asserted.

Evidence log for the run reported here: [`recognition-across-stagings.log`](recognition-across-stagings.log).

---

## 1. What was built

Everything new is one project, `Tetris/recognize/`:

| file | what it is |
|---|---|
| `Recognizer.cs` | the reaction definitions and the `IOutputSink` they push to |
| `Program.cs` | `play` (produce a staging-1 journal) and `read` (recognize over an existing journal) |
| `run-lab.sh` | the whole lab end to end, host + containers |

Nothing else moved. `Tetris/domain` was not touched, `Well.cs` was not opened for
editing, and no existing host was modified — see [§4](#4-measurement-2--domain-cost).
`TetrisRecognize` is deliberately **not** registered in `Tetris.sln`: two other
tasks are in flight on this example and the solution file is the one place they
would all collide. Build it by project path.

**The reaction** (`recognize/Recognizer.cs`):

```csharp
reactions.DefineReaction("Placement")
    .Job().Company().WithSharedHydration()
    .Seek("Spawn").One()
        .OnMatch("[_:Well].Spawn($type)")
    .ThenSeek("Land").One()
        .OnMatch("[_:Well].Drop()")
    .Program.Emit("print @type 'piece';");
```

Following `training-lab/guides/reactions.md`: storage is configured before the
reactions are defined (`ConfigureStorage` auto-wires the reaction diary);
`.WithSharedHydration()` precedes the first `.Seek()`; the plane is
`.Program.Emit`, which is read-only and whose body **must** `print` or nothing
reaches the sink (`Reaction.cs:1404-1407`); and every seek of a multi-seek
reaction carries a quantifier, or `ValidateQuantifiersPresent`
(`Reaction.cs:772`) throws at `Execute()`.

Alongside it, six single-seek reactions (`Act_Spawn` … `Act_Drop`) enumerate the
record act by act. They are the ground truth the placement count is checked
against, and they are exempt from the quantifier rule because a single-seek
reaction has no trajectory to size.

---

## 2. The finding that came first: there is no correlation handle

The training lab's worked multi-seek reactions all correlate on a field the
domain journals at **both** ends of the trajectory — a `$saleId`, a `$orderId`.
Reusing that `$var` at the later seek turns it into a constraint, and the
constraint is what ties *this* close to *that* open.

**This domain journals no such field.** The acts are:

```
well.Spawn(@type)     — names a piece TYPE ('J'), not a piece
well.MoveLeft()       — names nothing
well.MoveRight()      — names nothing
well.Rotate()         — names nothing
well.Tick()           — names nothing
well.Drop()           — names nothing
```

`$type` is not a handle. Two `J` pieces in a session are two placements with the
same value, and `Drop()` carries no value at all, so there is nothing to unify
against. The only thing binding the pair into one trajectory is **order**: a
solution must place the `.Seek` match strictly before the `.ThenSeek` match.

This is not a defect in the reaction. It is a fact about what the record
supports, and it bounds the routines this record can express:

* **Expressible.** "A piece was spawned and later hard-dropped" — a shape over
  positions in the record.
* **Not expressible.** "*This* piece was spawned and *this same* piece was
  dropped" — the record contains no piece identity to say *same* with. Nor
  "which moves belonged to which piece", "how far this piece travelled", or
  "the third `I` piece of the game": all of those need an identity the acts do
  not carry.

**How order-only correlation behaves, verified.** With `.WithSharedHydration()`
the matcher runs breadth-first (`MatchTree.cs:313`, `ProcessBreadthFirst`), and
BFS keeps every open branch alive at once — the source contrasts DFS explicitly
as the mode that "does not keep multiple branches active simultaneously"
(`MatchTree.cs:383`). So each `Spawn` opens an anchor and every anchor still open
when a `Drop` arrives is completed by it. With a shared `$var` the wrong tuples
would be discarded on unification; with order alone nothing discards them.

That is sound **exactly while opens and closes alternate**, which is the case in
ordinary play: the well is `IsAwaitingPiece` after a landing and every host
spawns before moving again. The quantifiers follow from that shape and not from
taste — `.One()` on both seeks, because one spawn opens a trajectory and the
first following drop closes it and prunes the anchor (O(N)). `.Many()` at the
opening seek would collapse the anchors and destroy even the order correlation;
at the close it would be O(N²) for no gain. See §5 for what happens when the
alternation breaks.

---

## 3. Measurement 1 — the same routine on both stagings

Six recognitions over four records. The input **verb** sequence is the same in
every row except the control: `spawn, left, rotate, tick, tick, drop, spawn,
right, right, drop` — the sequence `sm-cluster/Program.cs::PlayScriptedSequence`
plays on the containers. S1b substitutes a run of `tick`s for the first piece's
`drop`, and is the control, not a comparison.

| # | staging | how it ran | acts | placements | closing entries | pieces |
|---|---|---|---|---|---|---|
| S1a | single-process `PerformanceV2` | `TetrisActor.Persistent`, FileSystem journal | 10 | **2** | 12, 17 | I, L |
| S1b | same, **gravity control** | first piece landed by `Tick`, not `Drop` | 25 | 2 *(wrong — §5)* | 32, 32 | L, T |
| S1c | **warm server** | `TetrisServer` over a named pipe, one `TetrisSend` process per command | 11 | **2** | 12, 17 | Z, L |
| S2a | container `tetris-a` (Director) | reaction run **inside** the container over its own `/data` | 10 | **2** | 14, 19 | J, L |
| S2b | container `tetris-b` (cast) | same | 10 | **2** | 14, 19 | J, L |
| S2c | container `tetris-c` (cast) | same | 10 | **2** | 14, 19 | J, L |

**Same count, same order, same shape.** Setting the control aside, every staging
recognized exactly two placements, in the same order, each opened by a `Spawn` and
closed by the `Drop` that ends that piece's descent. The recorded act sequence is
identical between S1a and S2 — ten acts, verb for verb.

**All three nodes agree, and their records are byte-identical.** Each container's
`journal_000001.bin` hashes to `990fa6a8…` — the same 64 hex digits on the
Director and both casts. The recognition is not "three readings that happen to
agree"; it is one record replicated over TLS and read three times.

The recognizer ran **in-container** on each node (`docker exec tetris-<id> dotnet
/recognize/TetrisRecognize.dll read /data tetris`), against that node's own
volume, while the node's Stage was live. Nothing was copied out to be read
elsewhere.

### The one discrepancy, and what it is

The closing entry ids differ by a constant **+2** between the single-process
staging (12, 17) and the containers (14, 19). This is not a difference in the
routine; it is a difference in where the routine starts in the journal. The
framework's own `puppeteer show entry` says why:

```
--- cluster entry 1 ---     kind: "script"   script: "upgrade('seed') { well = Well(10,20); }"
--- cluster entry 2 ---     kind: "script"   script: "upgrade('seed') { well = Well(10,20); }"
--- cluster entry 3 ---     kind: "script"   script: "upgrade('seed') { well = Well(10,20); }"
--- cluster entry 4 ---     kind: "define"   define: "define action 1 (type:string) as well.Spawn(type); end;"
--- cluster entry 5 ---     kind: "invocation"  arguments: "'J'"
```

Three seed `upgrade`s, one per node: each node's `TetrisActor` issues the same
idempotent seed, and the casts' are forwarded to the Director. The single-process
staging writes one. Three nodes cost two extra entries at the head, and every
later id shifts by two. A reaction observes Actions, not literal Scripts
(`Reaction.cs:1701`), so none of the seed entries is part of any recognized
routine on either staging — but they are in the record, and they move the
addresses.

**The consequence worth naming.** The routine is invariant across stagings; the
*entry ids* are not. The only identity a `.Program.Emit` match hands the reader is
`PushDocument.EntryId`, which is the **closing** entry (`Reaction.cs:1406` passes
`triggeringEntryId`). So the natural key for "the same recognized routine" is not
portable between stagings, and the comparison above is by ordinal and shape, not
by id. §6 says the routine is recognizable across stagings; it is. It does not
follow that a recognition can be *named* the same way on both.

### Piece letters differ, and that is the domain being itself

The letters differ per run (I/L, Z/L, J/L) because `Well.NextPieceLetter()` is a
transient process-wide `Random` that is never journaled — only the *resolved*
letter is (`Well.cs:132-149`). So the two stagings share an identical verb
sequence and differ in the spawn argument. Making the letters match would require
either editing the domain or editing `sm-cluster`, and this lab is allowed to do
neither. The claim measured is therefore about the **routine** — its count,
order and shape — not about the argument values, which are per-session by design.

---

## 4. Measurement 2 — domain cost

Expected zero, and it is zero.

```
$ git diff --stat HEAD -- Tetris/domain
(no output)

$ git status --porcelain -- Tetris
?? Tetris/recognize/
```

Recognizing a routine required **no domain edit**. A reaction is declared from
outside the domain and outside every staging: it names the domain's own verbs in
a pattern string, and the domain neither knows nor can know that anything is
reading its record. §6 is not contradicted on this point — it is confirmed with a
diff.

Two honest riders:

1. **The reaction had to name the domain's verbs.** `[_:Well].Spawn($type)` is
   coupled to the domain's vocabulary — as it must be, since that vocabulary is
   what the record is written in. The dependency runs the same direction as
   everything else in the paper: the reader depends on the domain, never the
   reverse.
2. **A read leaves a trace.** `Reactions.Execute()` advances a checkpoint, so
   running the recognizer inside a container writes to that node's `/data`
   volume. The lab works around this by suffixing the reaction *names* — never a
   pattern, quantifier or body — with a per-run tag, so each run is a full sweep
   from checkpoint 0 (`reactions.md` §Rebuild caveats). Without the tag a second
   read of a finished journal correctly reports nothing, which looks exactly like
   a failed recognition. That trap cost a false negative during this lab.

---

## 5. Measurement 3 — what the record could NOT express

### 5.1 There is no landing act (the load-bearing gap)

The routine asked for is "the trajectory from a spawn through its moves to **the
landing** that ends it". The well *has* a landing — `Well.Land()`, `Well.cs:255` —
but it is **private**, called from inside `Tick()` and `Drop()`. It is not a verb,
so it is not an act, so it is not in the record.

`Drop()` is a usable proxy because a hard drop always lands. `Tick()` is not: it
descends one row, and only its last invocation lands. From the journal, a
descending `Tick()` and a landing `Tick()` are the identical entry.

The gravity control (S1b) measures the damage. The first piece was ticked to the
floor and landed; the second was hard-dropped:

```
entry  3  Spawn  type=L
entry  5  MoveLeft
entry  7  Rotate
entry  9..26  Tick   × 18        <- the 18th one landed the L. Nothing says which.
entry 27  Spawn  type=T
entry 29  MoveRight
entry 30  MoveRight
entry 32  Drop                   <- lands the T

ROUTINE 'placement of one piece'  [2 recognized]
  #1  type=L  closes at entry 32
  #2  type=T  closes at entry 32
```

Two placements "recognized", **both closing on the same entry**. The L's
trajectory was closed by the T's drop. This is order-only correlation failing
exactly where predicted: two anchors were open when the single close arrived, BFS
kept both alive, and nothing could tell the matcher that entry 32 belonged to the
T. A shared handle would have discarded the L's tuple on unification. There is no
handle, so it did not.

The failure is silent. The count is right (2), the ordinals are right, the piece
types are right — only the correlation is wrong, and only the duplicated closing
entry gives it away. A reader trusting the count alone would never notice.

### 5.2 The `Spawn → Spawn` fallback, and what it actually recognizes

The only close available for a gravity landing is the *next* spawn, so the lab
also runs a control reaction closing on `[_:Well].Spawn(_)`. It is strictly
weaker:

* it cannot close the **last** placement (nothing follows it) — S1a and S2
  recognize 1 where the drop-closed reaction recognizes 2;
* it recognizes "the host asked for another piece", not "this piece landed".
  Those coincide only because every Tetris host here spawns immediately on
  `IsAwaitingPiece`. That is a property of the hosts, not of the domain, so the
  fallback reads a staging's habit into the domain's record.

The S1c warm-server run shows the difference cleanly: it recognizes 2
spawn-to-spawn routines where S1a and S2 recognize 1, purely because
`TetrisServer` spawns a third piece after the final drop.

### 5.3 The routine's interior is not matchable

"These three moves and a drop were the placement of one piece" (§6's own example)
is **not** reachable. A two-seek reaction matches the endpoints; the moves between
them are not in the match. Recovering them would need a middle seek — and the
guide's own rule for a three-seek correlated chain is `One → Many → Many`, where
the `.Many()` is O(N²) and, with no handle, would accumulate every move in the
journal rather than this piece's. The interior is in the record; the reaction
cannot bind it to the trajectory.

### 5.4 The opening entry is not available to the body

`PushDocument` carries one `EntryId`, the triggering (closing) one. The framework
is explicit that per-seek entry ids are not cross-seek symbols: only
`SeekName.@OccurredAt` is, and only inside a `Where` predicate — "EntryId is a
Program-level property, so [it is] not a valid cross-seek symbol"
(`Reaction.cs:2110-2113`). So a recognized routine can report where it ended but
not where it began, and the span of a trajectory has to be reconstructed
downstream from consecutive closes.

### 5.5 Row clears are recorded, but not as acts

§6's second example is "this run of placements filled a row". `Well.ClearedLines`
counts them and `Pile.Integrate` collapses them, but a clear is a *consequence*
inside `Land()`, not a verb — so no entry says a row was cleared, and no reaction
can seek one. It is visible only in queried state, which is a snapshot, which is
the thing §6 says recognition avoids needing.

**Summary.** The record supports one routine well (spawn → hard drop), supports a
second badly (spawn → next spawn), and does not support three that §6's own prose
names: which moves belonged to a piece, that a piece landed under gravity, and
that a row was filled. All three are missing for the same reason — the domain's
verbs are the record's vocabulary, and these facts are not verbs.

---

## 6. Reproduction

```bash
Tetris/recognize/run-lab.sh
```

Prerequisites: .NET SDK on the host, Docker running, bash 4+. From a nested git
worktree the engine reference needs the junction described in
`experiment-a-crossmachine.md` §caveats. `--no-cluster` skips S2.

Individually:

```bash
dotnet run --project Tetris/recognize/TetrisRecognize.csproj -- play /tmp/lab tetris
dotnet run --project Tetris/recognize/TetrisRecognize.csproj -- read /tmp/lab tetris
```

`<journalDir>` is the FileSystem `path=`; the store lives at
`<journalDir>/<session>` because `DiaryStorageFileSystem` appends the actor name
(`DiaryStorageFileSystem.cs:49`). Pointing `read` one directory too high silently
opens a *new empty* store and reports zero recognitions — the second trap this
lab hit.

---

## 7. Threats to validity

* **Base commit.** This branch fast-forwarded from `485b766` to `4b473ea` before
  starting, and the lab depends on both commits it brings in. Without `974f62a`
  there is no `sm-cluster` or `docker/` to run against; without `4b473ea` the
  Tetris verbs journal as literal Scripts, which a pure-domain reaction skips by
  design (`Reaction.cs:1693-1701`), so **every reaction in this lab would have
  matched nothing**. Neither commit touches `Tetris/domain`
  (`git diff --stat 485b766 4b473ea -- Tetris/domain` is empty), and neither is
  one of the two in-flight domain-changing tasks.
* **Two pieces is a small sample.** The scripted sequence is the one the
  containers already play. The lab shows the recognition is *identical* across
  stagings, not that it scales; the O(N) argument for `.One()` is from the
  framework's own cost table, not measured here.
* **Author confounder, inherited.** Same hand wrote the domain, the stagings and
  the reaction. §8 of the paper already declares this; nothing here weakens it.
  What is new is that the reaction was written against a vocabulary it could not
  adjust: the domain's verbs — which are what the record is written in — were last
  changed on 2026-06-29 (`fd8d94b`), 27 days before this lab, and this branch is
  forbidden from touching them. That is how §5.1 became a finding rather than a
  design choice.
* **Two known engine limits are avoided rather than answered.** (a) A journal
  reopened after a clean exit can read short at exactly 100 entries when a stale
  `index/index.bin` is present — every journal in this lab is 17–32 entries, well
  under it, and the acts count is cross-checked against the driven verb sequence
  in every row of the table. (b) A host issuing more than ten of the same
  *literal nullary* verb in one process writes a journal that cannot be replayed
  (promoted nullary Action). That one does not bite here and the gravity control
  is the proof: it issues 18 `well.Tick()`s in a single process and the journal
  rehydrates and reads back all 25 acts, because since `4b473ea` the verbs are
  parametric Actions (`define action N (step:int) as well.Tick(); end;`) rather
  than literal nullary scripts. A reproduction on a pre-`4b473ea` host would hit
  both problems before it hit any of this note's findings.
* **The gravity control is one shape of failure, not all of them.** It shows
  order-only correlation mis-binding when two anchors are open. It does not
  characterise what happens with many concurrent anchors, and no such case exists
  in this domain (one well, one falling piece).

---

## 8. What this settles for §6

§6 can now say that the recognition was performed rather than argued: the same
reaction, over two stagings and three nodes, recognized the same routine, at zero
cost to the domain. It should say two more things it currently does not.

First, that recognition depends on the record naming what you want to recognize.
The Tetris well happens to name spawning and hard-dropping, and happens not to
name landing, moving-this-piece, or clearing. The routines §6 uses as examples are
partly outside what its own example domain can express, and that has nothing to do
with staging — it is prior to staging, and it does not go away by keeping the
domain's identity intact.

Second, that correlation is a property of the record, not of the reader. Paper 3's
definition needs the correlation that binds entries into one trajectory; where the
domain journals a handle, the reader gets it for free, and where it does not, the
reader is left with order — which is correct while the routine's opens and closes
alternate and silently wrong the moment they do not. "The acts are the same across
stagings, therefore the routine they compose is recognizable" holds. It does not
also guarantee that the routine is *correctly* recognizable, and §5.1 above is a
counterexample that is invariant across stagings too.
