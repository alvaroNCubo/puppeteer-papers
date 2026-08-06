# Paper 9 — Lab C: six clients over one domain

With the stage held fixed the client varies: a person at a keyboard reading an on-screen grid; an
automated player sending moves over a pipe and reading a view it computes for itself; two passive
observers, one pulling the board and one receiving it pushed; and a browser in which a player and a
spectator are both written in JavaScript.

Headline → §3 and Appendix A (Lab C). **0 domain edits** for any of the six; each adds an input
adapter, an output adapter, or both. And the evidence the paper leans on hardest: **one of those
adapters was written by the client that reads through it.** The automated player is an instance of a
large language model and authored `pile-scan.ps1`, its column-height view of the board, during the lab.
Disclosed in the paper's acknowledgments.

## Two consoles, console 1 first

| # | Run this |
|---|---|
| 1 | `dotnet run --project ../paper09-example/ai/TetrisAi.csproj -- game1 new`, then `left left drop right drop` in place of `new` |
| 2 | `..\paper09-example\tools\pile-scan.ps1 -Example ..\paper09-example -Session game1` |

Play those five acts before reading anything: after `new` alone the well holds only the falling piece,
so the profile correctly prints `skyline` all zeros and shows you nothing.

## The lab is the comparison

```
CONSOLA 1 — the grid                CONSOLA 2 — the height profile
|      []    []      |              skyline : 0 1 1 2 1 1 2 0 0 0
|  [][][][][][]      |              wells   : col0(d1)
+====================+              zeros   : 0,7,8,9
                                    metrics : maxH=2  agg=8  bumpiness=6  floating=4
```

Go column by column: column 3 stacked two high reads `2`, the empty columns 7–9 are `zeros`, the
one-cell notch at the far left is `col0(d1)`, and `agg=8` is exactly the eight landed cells the grid
draws. Both consoles name the session, so you can check they are two readings of one game.

Same fact, two vocabularies, **neither of them the domain's**: there is no renderer in the domain, and
no operation of the `Well` mentions a skyline, a well or bumpiness.

## Then drive it yourself

Play moves of your own choosing and re-read between them. The profile always shows the board **as your
last act left it** — which is also why neither console acts: only the moves move the game.

Three things will look wrong and are not. While a piece falls, `skyline` stays flat and `active` is the
line that moves. `left` into the wall gives a reading identical to the one before, because a blocked
slide is a clean no-op. And `rotate` twice on an `S` or `Z` returns the piece to where it started, since
those have two orientations rather than four. In each case the board genuinely did not move.

No verb travels in the frame, so a client reading one can report the state and cannot report what
happened. The acts are in the journal, and Lab D is where that distinction gets measured.

## Contents

Nothing to build. The emitted fact is one line of JSON at
`../paper09-example/.sessions/game1.frame`; open it, since the profile is computed from that file and
nothing else. Both programs are the vendored example's own — `ai/` and `tools/pile-scan.ps1` — and the
other four clients are hosts of it. Write-ups in `data/paper09-labC-clients/`.
