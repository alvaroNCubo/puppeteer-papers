# Paper 9 — Lab I: the domain itself grows

A score and a difficulty level are added to the board: the first the *authority* case — a value any
client could compute, and which therefore has to be single-valued — and the second a rule over lines
already recorded.

Headline → §8.2 and Appendix A (Lab I). The change is **+98 lines and −3** across three files, of
which **30 lines are code**. And the converse the paper had never measured: when the domain grew,
**all twelve host projects kept running with no edit** — verified by running them, not by compiling
them.

**This is the one lab that changes the domain of the vendored example, so it is the one lab you apply
and then revert.** Lab G also re-cuts the domain but carries its own copy in `split/` and needs no
apply step; this one cannot, because its claim is precisely that *the twelve hosts* keep working, and
those hosts reference the example's domain project. The growth has to land where they look.

## Order — and step 4 is not optional

Set `$env:PuppeteerEngine` first, as every lab in this suite does. Run all five in one console, from
this directory.

```powershell
$bash = "C:\Program Files\Git\bin\bash.exe"
& $bash smoke.sh pre-growth
Copy-Item Scoring.cs, Difficulty.cs, Well.cs ..\paper09-example\domain\ -Force
dotnet build ..\paper09-example\Tetris.sln
& $bash smoke.sh post-growth
& $bash replay.sh post-growth
git checkout -- ..\paper09-example\domain\Well.cs
git reset -q -- ..\paper09-example\domain\Scoring.cs ..\paper09-example\domain\Difficulty.cs
Remove-Item ..\paper09-example\domain\Scoring.cs, ..\paper09-example\domain\Difficulty.cs -Force
```

**Why the explicit path to `bash`.** In PowerShell a bare `bash` resolves to WSL's, which cannot see your
Windows paths and fails with `execvpe /bin/bash failed 2`. Git Bash is the one that works. From a Git Bash
prompt instead, drop the `& $bash` and run `./smoke.sh pre-growth`.

What each should do:

| | |
|---|---|
| `smoke.sh pre-growth` | **14 PASS** — the before column: twelve hosts, plus two assertions that a pushed frame arrived |
| `Copy-Item` | nothing. **This is the apply step**: the three files go into the domain the twelve hosts reference |
| `dotnet build` + `smoke.sh post-growth` | **14 PASS again, with no host edited.** That is the result |
| `replay.sh post-growth` | journals written *before* the growth, answering `score` and `level`. The stronger half — see below |
| the last three lines | **the revert**, and it is not optional. Afterwards `git status --short ..\paper09-example\` prints nothing and the diff over the domain is empty. Rebuild the solution so the other labs run against the ungrown domain again |

Both scripts take a **label** and nothing else; the example's root is an optional second argument that
already defaults to `..\paper09-example`. Output lands in `out/` beside them.

## What the measurement mechanism reports while the lab is applied

Between steps 2 and 5 the domain is modified, and the check that says so is a diff over the vendored
domain directory. The two new files are untracked, so tell git to count them:

```powershell
git add --intent-to-add ..\paper09-example\domain\Scoring.cs ..\paper09-example\domain\Difficulty.cs
```

```powershell
git diff --stat -- ..\paper09-example\domain\
```

```
 labs/paper09-example/domain/Difficulty.cs | 38 ++++++++++++++++
 labs/paper09-example/domain/Scoring.cs    | 39 +++++++++++++++++
 labs/paper09-example/domain/Well.cs       | 24 ++++++++++---
 3 files changed, 98 insertions(+), 3 deletions(-)
```

**That is the +98 and −3**, reproduced rather than quoted. `Well.cs` reads +21/−3, and its code delta
is exactly three lines: a `Score` property, a `Level` property derived from `ClearedLines`, and one
`Score +=` where rows collapse. Everything else added is the domain's own doc-comment convention.

Anywhere outside this lab a non-empty diff there means the domain was touched by something that is not
this lab, which is the only reading of that check that should worry anyone.

## What `replay.sh` shows, and why it is the stronger half

The fixtures in `journals-pre-growth/` were recorded by a build of the domain that had never heard of a
score or a level. Replayed against the grown domain they answer both:

```
===== deep-w4h40 (actor=deep) =====
  A: {"w":4,"h":40,"cleared":20,"over":true}
  A: {"score":2100}
  A: {"level":3}
```

Not merely *compatible* with an old record — **derived from one**. The level is a function of the lines
the record already counted, and the score accumulates as the same recorded acts collapse the same rows.
Nothing was migrated and no fixture was rewritten. That is what "additive in the strict sense" buys.

## The adoption cost, which divides exactly over twelve hosts

**0 for four, 1 for five, 4 for each of the two browser hosts, 32 for the input host** — the only place
where the level changes what happens. Those figures are the sum over **both** growth commits *and*
**both** adoption commits; taking only the first two understates the input host, which is how an earlier
count came to sum to thirteen.

Adoption is not part of the block above. Those steps measure that nothing *had* to change; these figures
measure what it cost the hosts that *wanted* the new concepts.

## Contents

Everything this lab needs to run is here:

    Scoring.cs  Difficulty.cs  Well.cs    the three files the +98 spans, as they stood on branch
                                          claude/trusting-tereshkova-f48ab6
    smoke.sh                              the twelve hosts, before and after
    replay.sh                             the pre-growth journals against the grown domain
    journals-pre-growth/                  the eight fixtures replay.sh reads

One thing it needs lives in the example instead, because that is where the script looks for it and
because it is built like any other project there: `../paper09-example/tools/growth-probe/`, the apparatus
that records a journal with one build of the domain and replays it with another. It is deliberately
**outside `Tetris.sln`**, so it never enters the twelve-host count — it is measurement, not a staging.

What is in `data/paper09-labI-growth/` is only the record of the original run: the replay logs before the
change and after each of the two growth steps, and the three smoke transcripts. Those carry absolute
paths from the machine that produced them, because a log is evidence of a run and the path a binary was
invoked from is a fact of that run. Read them as transcripts, never as instructions.
