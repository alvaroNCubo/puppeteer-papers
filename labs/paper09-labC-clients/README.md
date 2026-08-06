# Paper 9 — Lab C: six clients over one domain

With the stage held fixed the client varies: a person at a keyboard reading an on-screen grid; an
automated player sending moves over a pipe and reading a view it computes for itself; two passive
observers, one pulling the board and one receiving it pushed; and a browser in which a player and a
spectator are both written in JavaScript.

Headline → §3 and Appendix A (Lab C). **0 domain edits** for any of the six. Each client adds an
input adapter, an output adapter, or both.

The evidence the paper leans on hardest is here: **one of those adapters was written by the client
that reads through it**, not by the author of the domain. The automated player is an instance of a
large language model and authored `pile-scan.ps1` — its column-height view of the board — during
the lab, to suit the form in which it reasons. Disclosed in the paper's acknowledgments.


## Order, consoles, and what each shows

**Order: 1, then 2.** There must be a played session before there is a view to compute.

| # | Run this | What you see in it | Who operates it |
|---|---|---|---|
| 1 | `dotnet run --project ../paper09-example/ai/TetrisAi.csproj -- game1 new` | One act applied, then the process **exits**. Repeat with `left`, `right`, `rotate`, `tick`, `drop`. Run it with no arguments and it lists the operations it accepts. | **You.** Each invocation is a separate short-lived process — one writer at a time. |
| 2 | `..\paper09-example\tools\pile-scan.ps1 -Example ..\paper09-example -Session game1` | **The client-authored view**: `skyline`, `diffs`, `zeros`, `wells`, `metrics` — heights and gaps, not a grid. | **You**, whenever you want a reading. It only reads the frame file. |

Play a few acts in console 1 before reading, so there is a pile to see. Console 2 then prints this —
one run's figures; yours differ, since the domain chooses the pieces:

```
== MIRILLA  frame=game1.frame ==
state     : piece=I  cleared=0  over=False  awaiting=False
cols      : 0 1 2 3 4 5 6 7 8 9
skyline   : 0 0 0 3 0 0 0 0 0 0
diffs     :  +0 +0 +3 -3 +0 +0 +0 +0 +0
zeros     : 0,1,2,4,5,6,7,8,9   (lowest = fill priority)
wells     : none
metrics   : maxH=3  agg=3  bumpiness=6  floating=9
active    : type=I  (1,3) (1,4) (1,5) (1,6) (17,4) (17,5) (18,4) (18,5) (18,6)
```

No grid anywhere in it. Column 3 stands three high and the rest are at zero, so `zeros` names the nine
columns worth filling and `bumpiness=6` prices the unevenness. `floating` counts what sits above the
first gap — the falling `I` plus the overhang of what already landed.

**Output on disk:** the emitted fact itself is at `../paper09-example/.sessions/game1.frame` — one line
of JSON. Open it. The view in console 2 is computed *from that file* and nothing else, which is the
point: the client derives what it needs from a fact it did not shape.

```powershell
..\paper09-example\tools\pile-scan.ps1 -Example ..\paper09-example -Session game1 | Tee-Object -FilePath labC-view.txt
```

**If that file is not there**, the push channel is not running, and there is one likely cause worth
naming because it is silent: a mutating command that journals as a V1 literal Script is invisible to a
domain reaction by design, so the frame reaction never fires and no frame is ever written. Every
command in this example is a parametrized ActorV2 Action for that reason — see the note on
`GuardedVerb` in `actor/TetrisActor.cs`.

**The row that matters.** One of the six clients wrote its own adapter: the automated player is an
instance of a large language model and authored `pile-scan.ps1` during the lab, to suit the form in
which it reasons. Disclosed in the paper's acknowledgments. The other five clients are hosts of the
example and are not copied here.

## Contents

Nothing to build. This lab runs two of the vendored example's own files: the automated player at
`../paper09-example/ai/` and the client-authored view at `../paper09-example/tools/pile-scan.ps1`. The
other four clients are hosts of that same example. Write-ups in `data/paper09-labC-clients/`.
