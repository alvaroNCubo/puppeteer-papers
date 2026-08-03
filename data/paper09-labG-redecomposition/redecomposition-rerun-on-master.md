# Re-decomposition, re-run on engine master with the actor correction merged

Run 2026-07-26 to replace figures the earlier run could not take. That run sat on a
state of the example *without* commit `4b473ea` (the actor correction that makes verbs
journal as V2 Actions), so every entry in its original record was a literal script and
its entry-count figures were sensitive to that. This run merges `4b473ea` and builds
against a dedicated worktree pinned to engine master `dd67047`, which carries all three
substrate fixes (`e65f681`, `036b972`, `dd67047`).

Branch `p9/labg-rerun`, from `7d17d07` with `4b473ea` merged. The merge needed one
semantic fix beyond the textual one: the lab's deterministic `Spawn(string letter)` still
called `CheckThenCommand` with the pre-correction two-argument signature and spliced the
letter into the DSL text. It now passes the letter as an `@type` parameter, matching
`SpawnNext` — which is also what makes that spawn journal as an Action rather than a
literal script, so it matters for the measurement and not only for the build.

The engine reference points at a worktree of its own rather than the conventional
shared checkout, which sits on another branch with uncommitted work and was not touched.

## What changed, and what did not

**The record ratio changed, and the old figure should not be carried forward.**

| | earlier run (pre-correction) | this run (post-correction, engine master) |
|---|---|---|
| original record | 130 entries, all literal scripts | **135 entries**, each verb carrying one declaration plus its invocations |
| piece role | — | **219 entries**, append-only, ids 1..219 |
| pile role | — | **90 entries**, append-only, ids 1..90 |
| two roles together | 309 | **309** |
| ratio | 2.38x | **2.29x** |

The original is 135 rather than 130 because the correction gives each verb a Define
template alongside its invocations — five declarations for `MoveRight`, `Drop`, `Spawn`,
`MoveLeft` and `Rotate`, plus the seeding upgrade. The two-role total is unchanged at
309. So the ratio falls to 2.29, and the cost still sits where the two roles have to
speak: a landing costs `piece.Take`, `tell Landed`, its ack, `pile.Absorb`,
`tell Absorbed` and its ack — six entries the undivided board never wrote, against the
one act it did.

Counts as reported by the harness:

```
played: 135 entries          piece role (recut-piece) — 219 entries
  40 well.MoveRight (1 decl)    39 piece.MoveRight   30 tell Landed
  30 well.Drop      (1 decl)    30 piece.Take        29 tell ack
  30 well.Spawn     (1 decl)    29 piece.Drop        26 piece.MoveLeft
  27 well.MoveLeft  (1 decl)    29 piece.Spawn        6 piece.Rotate
   7 well.Rotate    (1 decl)     1 upgrade
   1 upgrade
                             pile role (recut-pile) — 90 entries
                               30 pile.Absorb   30 tell Absorbed
                               29 tell ack       1 upgrade
```

Both roles hold only their own verbs, and both records are contiguous from 1.

**Everything else reproduced exactly.** The game is 129 acts to game over. The
framework's own rehydration of the original record matches on board, cleared and over —
so the record is an ordinary record and the harness's reading took nothing special out
of it. And the equivalence experiment reproduces the earlier figures to the digit:

| policy | steps | landings | rows cleared | games over | divergences |
|---|---|---|---|---|---|
| random | 2,614 | 243 | 0 | 20/20 | 0 |
| flat | 5,169 | 685 | 20 | 19/20 | 0 |
| clears | 40,000 | 8,337 | 3,316 | 0/20 | 0 |
| **total** | **47,783** | **9,265** | **3,336** | **39** | **0** |

47,783 overlap checks across the new boundary, asking each role separately, found no
overlap. So the invariance result was never sensitive to the cascade; only the
entry-count figure was.

## The frame push channel

The earlier run reported this as a cost the split incurred, which it could not have
observed, because on that state no host's push channel engaged at all. This run does not
restore it as a measurement either, and the honest form is an argument whose premises are
checked:

- a complete frame needs both roles' state, since the undivided board built it by
  unioning the pile's cells with the falling piece's, and after the cut those live in
  two actors;
- a `.Program.Emit` reaction runs read-only against its own actor's state and cannot
  reach another's;
- `SplitTetrisActor` exposes no sink, no formatter and no `WireOutput` — the split offers
  no push channel to lose.

So the consequence follows from the two premises, and what remains genuinely unmeasured
is whether some third party subscribing to both records could reassemble a frame. That
was not built and is not claimed.

## Reproducing

From this worktree, with the engine worktree beside it:

```
dotnet build Tetris/redecomp/TetrisRedecomp.csproj
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- play <run>/orig 1 400
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- redecompose <run>/orig <run>/split
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- dump played <run>/orig
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- equivalence random 20 2000
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- equivalence flat 20 2000
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- equivalence clears 20 2000
```
