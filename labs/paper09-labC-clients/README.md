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
| 1 | `dotnet run --project ../paper09-example/ai/TetrisAi.csproj -- game1 new` | One act applied, then the process **exits**. Then play a real sequence before you read anything — `left left drop right drop` will do. Run it with no arguments and it lists the operations it accepts. | **You.** Each invocation is a separate short-lived process — one writer at a time. |
| 2 | `..\paper09-example\tools\pile-scan.ps1 -Example ..\paper09-example -Session game1` | **The client-authored view**: `skyline`, `diffs`, `zeros`, `wells`, `metrics` — heights and gaps, not a grid. | **You**, whenever you want a reading. It only reads the frame file. |

**Land at least one piece before you read anything.** A well holding nothing but the piece that is
falling prints `skyline` all zeros and `maxH=0` — a correct reading of an empty pile, and a useless
illustration of the lab. `new` on its own leaves you exactly there.

**Read the two consoles side by side.** That is the whole lab, and it is checkable rather than merely
illustrative. After `new left left drop right drop` one run showed this — yours differs, since the
domain chooses the pieces:

```
CONSOLA 1 — the grid                CONSOLA 2 — the height profile
|      []    []      |              skyline : 0 1 1 2 1 1 2 0 0 0
|  [][][][][][]      |              wells   : col0(d1)
+====================+              zeros   : 0,7,8,9
                                    metrics : maxH=2  agg=8  bumpiness=6  floating=4
```

Both consoles name the session they read — `TETRIS (AI) — session demo1` above the grid, `frame=demo1.frame`
above the profile — which is what lets you claim the two are readings of one game rather than two.

Go column by column and the two say the same thing. Column 3 has two cells stacked and the skyline reads
`2`; columns 7, 8 and 9 are empty and `zeros` names them; the one-cell notch at the far left is
`col0(d1)`. `agg=8` is exactly the eight landed cells the grid draws, and `floating=4` is the piece still
in the air, which the grid puts at the top of the board and the profile refuses to count as pile.

Same fact, two vocabularies, and **neither of them the domain's**. There is no renderer in the domain,
and no operation of the `Well` mentions a skyline, a well or bumpiness — those words exist only in the
client that needed them.

## Then drive it yourself, one act at a time

The pairing above is a snapshot. Convince yourself it is live: play moves **of your own choosing** in
console 1 and re-run the same command in console 2 between them. What you should find is that the
profile always describes the board as your last act left it.

Note what that already tells you: **neither console acts.** `view` reads and `pile-scan.ps1` reads, so
only the moves move the board. If a reading changed the game, one of the two observers would be writing,
which is the opposite of what this lab claims.

**Watch the right line.** While a piece is falling, `skyline` stays flat — a falling piece is not pile,
which is the whole point of that reading — so watch `active`. Once you `drop`, `skyline` and `agg` move
and `active` becomes the *next* piece.

**Two acts look like a failure and are not**, and it is worth provoking both, because they are the
sharpest thing this lab can show you about what a reading is:

| What you do | What you see | Why |
|---|---|---|
| `left` until the piece meets the wall | the reading **stops changing**, identical to the one before | a blocked slide is a clean no-op; the domain's state did not change, so the frame did not, so neither did the profile |
| `rotate` twice on an `S` or `Z` | the second reading is the **same as before the first** | those pieces have two orientations, not four; the second rotation returns the piece to where it was |

Neither is the view failing to follow you. In both cases the board genuinely did not move, and the
profile said so. Which is the precise form of the claim, and stronger than the loose one: the profile
shows **the board as your last act left it** — it does not name the act. No verb travels in the frame at
all, so a client reading it can tell you the state and cannot tell you what happened. If you want the
acts themselves, they are in the journal, and Lab D is where that distinction gets measured.

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
