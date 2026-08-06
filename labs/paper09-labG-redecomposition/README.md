# Paper 9 — Lab G: the domain's own internal boundary

One role modelled too large is cut into two — a pile role and a piece role — by authoring the two,
reading the original's recorded acts (the same read rehydration performs) and driving each new role
to perform its own, so each ends holding a record in its own voice. The original journal is not cut,
transformed or rewritten: it is read as the account of what happened, and kept.

Headline → §8.2 and Appendix A (Lab G). **11 of 12 host projects untouched** while the domain
divided beneath them, the twelfth needing one line; **0 divergences over 47,783 steps**; and the
record costing **2.32×** — 316 entries across the two roles against 136 for the same game.

This lab also carries the premises of §8.4's constraint, each checked here: the undivided board
built a complete frame by unioning the pile's cells with the falling piece's *inside* the domain,
and a projection on the emitting plane reaches only its own actor's state — so after the cut neither
role can push a whole frame.


## The whole lab, in one console

Set `$env:PuppeteerEngine` to a Puppeteer checkout at or after `dd67047`, `cd` to this directory, and
run these in order. **The order is strict** — steps 4 and 5 each consume what the one before wrote.

```powershell
Start-Transcript -Path labG-session.log
dotnet build redecomp\TetrisRedecomp.csproj
dotnet run --project redecomp\TetrisRedecomp.csproj --no-build -- play out\orig 1 400
dotnet run --project redecomp\TetrisRedecomp.csproj --no-build -- redecompose out\orig out\split
dotnet run --project redecomp\TetrisRedecomp.csproj --no-build -- dump played out\orig
dotnet run --project redecomp\TetrisRedecomp.csproj --no-build -- boards out\orig out\split
dotnet run --project redecomp\TetrisRedecomp.csproj --no-build -- equivalence random 20 2000
dotnet run --project redecomp\TetrisRedecomp.csproj --no-build -- equivalence flat 20 2000
dotnet run --project redecomp\TetrisRedecomp.csproj --no-build -- equivalence clears 20 2000
Stop-Transcript
```

What each should print:

| | |
|---|---|
| `play` | `played 129 acts on the single Well: cleared=0 over=True` — a whole game on the **undivided** board, to game over |
| `redecompose` | the cut, **in a fresh process**: the original's acts are read and re-performed into two roles, ending `RESULT: the re-decomposition reproduced the state, in two records, without touching the original`. It writes two new journals and never edits the first |
| `dump` | `played: 136 entries` — the figure the paper's 2.32× divides by |
| `boards` | **the two boards, side by side, and they are the same board.** See below |
| `equivalence` ×3 | `2614`, `5169` and `40000` steps compared, **`divergences : 0`** each time |

Any step is safe to re-run: `play` and `redecompose` each delete their output directory before writing,
so the block is repeatable and no run can mix with a previous one.

Run the harness with **no arguments** to have it list its own seven sub-commands; ask it rather than this
file, since a program's usage cannot go stale.

## Seeing it, not just counting it

`0 divergences over 47,783 steps` is a number a reader has to take. Both records can be looked at
instead, and it is worth doing in this order.

**First the original, which needs no harness at all** — the same move Lab B uses on a container's
journal:

```powershell
New-Item -ItemType Directory -Force ..\paper09-example\.sessions\labG\labG | Out-Null
Copy-Item -Recurse -Force out\orig\played\* ..\paper09-example\.sessions\labG\labG\
dotnet run --project ..\paper09-example\ai\TetrisAi.csproj -- labG view
```

A host that knows nothing about this lab reads its record and draws the board, game over included. That
is the whole thesis in miniature, and it is the baseline for what follows.

**Then both together**, which is what `boards` is for:

```
THE UNDIVIDED WELL                 THE PILE ROLE + THE PIECE ROLE
|      []            |             |      []            |
|      []    []      |             |      []    []      |
|    [][][][][][][]  |             |    [][][][][][][]  |
        …                                   …
+====================+             +====================+
            G A M E   O V E R                  G A M E   O V E R
```

The left board is the one you just rendered by hand — **one** actor's record. The right one is **two**
actors' records, joined by the staging that holds them, which per §8.4 is the only place a whole board
can be assembled.

Two things about that view are the section's argument rather than conveniences of it. No renderer was
written for it: both decompositions answer `Snapshot()` with the same `WellSnapshot`, so the example's
own `BoardRenderer` draws either without knowing which it holds. And the re-cut side can be seen **only**
this way. Try the recipe above on `out\split\pile\recut-pile` instead and the engine refuses it by
name — `Class 'PileWell' is not registered in the actor's library` — because that host carries `Well` and
this record asks for a different domain. Worth noticing next to §8's finding that an incomplete record
answers *plausibly*: a wrong-domain record does not, it says what is missing.

**This lab's output is journals, not text.** After `redecompose` you have three: `out\orig`, the original
at 136 entries, and `out\split`, the two roles' records at 225 and 91, 316 together. `dump` reads any of
them in a fresh process, which is the point — they are ordinary records and the harness takes nothing
special out of them. `labG-session.log` then holds every count the paper cites from this lab: 136, 225,
91, 316, 2.32×, and three zeros for divergence.

**Read, do not run, for §8.4's premises.** They were checked here, and both are one line each: the
undivided board built a complete frame by unioning the pile's cells with the falling piece's *inside*
the domain, and a projection on the emitting plane reaches only its own actor's state. So after the
cut, neither role can push a whole frame.

## Contents

`redecomp/`, the harness, and `split/`, the re-cut itself: the pile role, the piece role, the two
cell helpers they share, one widened method on `Pile`, and the actor that drives the pair.

**Why `split/` is here and not in the example.** This lab re-cuts the domain, and the other eight
labs measure a domain that does not change — so its files must not land in `paper09-example/`. They
also cannot live in a separate assembly, because the framework finds the domain by reflection over
`typeof(TetrisDomain).Assembly`, so the two new roles have to sit beside the anchor. So the harness
compiles its own variant: the example's domain sources, minus the one file this lab replaces, plus
these five. The example is untouched, this lab is self-contained the way Lab F's `baseline-hex` is,
and **there is nothing to apply and nothing to revert** — the two decompositions are simply two
projects, and running this lab leaves the other eight exactly as they were.

Both write-ups in `data/paper09-labG-redecomposition/` predate the migration of every command to an
ActorV2 Action, so their counts are the pre-migration ones (135, 219, 90, 309, 2.29×). The figures
above are from the current code. What the migration moved is the record's *encoding* — a define plus
compact arguments where a V1 script wrote one full sentence per call — so the entry counts shift by a
handful and the ratio with them. What it did not move: 129 acts, the 47,783 steps, and the three
zeros for divergence.
