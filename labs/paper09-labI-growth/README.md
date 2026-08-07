# Paper 9 — Lab I: the domain itself grows

A score and a difficulty level are added to the board: the first the *authority* case — a value any
client could compute, and which therefore has to be single-valued — and the second a rule over lines
already recorded.

Headline → §8.2 and Appendix A (Lab I). The change is **+98 lines and −3** across three files, of which
**30 lines are code**. And the converse the paper had never measured: when the domain grew, **all twelve
host projects kept running with no edit** — verified by running them, not by compiling them.

**This is the one lab that changes the domain of the vendored example, so it is the one lab you apply and
then revert.** Lab G also re-cuts the domain but carries its own copy in `split/` and needs no apply
step; this one cannot, because its claim is precisely that *the twelve hosts* keep working, and those
hosts reference the example's domain project. The growth has to land where they look.

## Eight steps, one console

Set `$env:PuppeteerEngine` as every lab in this suite does, `cd` to this directory, and define these two
once — the rest of the steps use them:

```powershell
$bash  = "C:\Program Files\Git\bin\bash.exe"
$probe = "..\paper09-example\tools\growth-probe\bin\Debug\net9.0\TetrisGrowthProbe.exe"
```

`$bash` is spelled out because in PowerShell a bare `bash` resolves to WSL's, which cannot see your
Windows paths and dies with `execvpe /bin/bash failed 2`. From a Git Bash prompt instead, drop the
`& $bash` and run `./smoke.sh pre-growth`.

### 1 — Measure the twelve hosts, before anything changes

```powershell
& $bash smoke.sh pre-growth
```

**14 PASS**: twelve host projects run, plus two assertions that a pushed frame arrived. The before column.

### 2 — Add the new classes to the domain

```powershell
Copy-Item Scoring.cs, Difficulty.cs, Well.cs ..\paper09-example\domain\ -Force
```

Prints nothing. `Scoring.cs` and `Difficulty.cs` are new; `Well.cs` replaces the one already there, by
+21/−3. Step 8 puts all three back.

### 3 — Compile, and run the twelve again

```powershell
dotnet build ..\paper09-example\Tetris.sln
dotnet build ..\paper09-example\tools\growth-probe\GrowthProbe.csproj
& $bash smoke.sh post-growth
```

**14 PASS again, and not one host was edited.** That is the result: the domain grew by 98 lines and every
staging kept working. Nothing in this step touches a host project.

### 4 — Observe the score, which nothing in the example shows

```powershell
& $probe play ..\paper09-example\.sessions\labIdemo labIdemo 10 20 600 7 flat
& $probe query ..\paper09-example\.sessions\labIdemo labIdemo "print well.ClearedLines cleared, well.Score score, well.Level level;"
```

```
{"cleared":1,"score":100,"level":1}
```

No host prints a score, and that is the measurement rather than an omission — none of the twelve *had* to
change, so none adopted the new concepts, which leaves the growth true and invisible. So ask the domain
directly. The probe's `query` takes its DSL as a command-line argument, which is what it was built for:
the same binary, unedited, asking for a fact the domain did not have when the journal was written.

`flat` steers each piece over the lowest column so the game actually completes a row. A score stays at
zero until one collapses, so a blindly shuffled game would show nothing.

### 5 — Read the same journal through a host that never adopted it

```powershell
dotnet run --project ..\paper09-example\ai\TetrisAi.csproj -- labIdemo view
```

```
META type=- cleared=1 awaiting=False over=True active=[]
```

The board and the cleared count, as always, and no score. Same record as step 4, read by a program that
was never told the concept exists.

### 6 — Replay journals recorded *before* the growth

```powershell
& $bash replay.sh post-growth
```

```
===== deep-w4h40 (actor=deep) =====
  A: {"w":4,"h":40,"cleared":20,"over":true}
  A: {"score":2100}
  A: {"level":3}
```

The eight fixtures in `journals-pre-growth/` were recorded by a build of the domain that had never heard
of a score. They answer both new concepts now — not merely *compatible* with an old record, **derived
from one**: the level is a function of lines the record already counted, and the score accumulates as the
same recorded acts collapse the same rows. Nothing was migrated and no fixture was rewritten.

### 7 — Measure the change itself

```powershell
git add --intent-to-add ..\paper09-example\domain\Scoring.cs ..\paper09-example\domain\Difficulty.cs
git diff --stat -- ..\paper09-example\domain\
```

```
 labs/paper09-example/domain/Difficulty.cs | 38 ++++++++++++++++
 labs/paper09-example/domain/Scoring.cs    | 39 +++++++++++++++++
 labs/paper09-example/domain/Well.cs       | 24 ++++++++++---
 3 files changed, 98 insertions(+), 3 deletions(-)
```

**That is the +98 and −3**, reproduced rather than quoted. The `--intent-to-add` is needed because the two
new files are untracked and git would otherwise leave them out of the count. `Well.cs`'s code delta is
exactly three lines: a `Score` property, a `Level` derived from `ClearedLines`, and one `Score +=` where
rows collapse. Everything else added is the domain's own doc-comment convention.

Anywhere outside this lab a non-empty diff there means the domain was touched by something that is not
this lab, which is the only reading of that check that should worry anyone.

### 8 — Revert, and this step is not optional

```powershell
git checkout -- ..\paper09-example\domain\Well.cs
git reset -q -- ..\paper09-example\domain\Scoring.cs ..\paper09-example\domain\Difficulty.cs
Remove-Item ..\paper09-example\domain\Scoring.cs, ..\paper09-example\domain\Difficulty.cs -Force
dotnet build ..\paper09-example\Tetris.sln
dotnet build ..\paper09-example\tools\growth-probe\GrowthProbe.csproj
& $probe query ..\paper09-example\.sessions\labIdemo labIdemo "print well.ClearedLines cleared, well.Score score, well.Level level;"
Remove-Item -Recurse -Force ..\paper09-example\.sessions\labIdemo
```

```
LanguageException: Unknown property or method 'Score' on type 'Well'.
```

Afterwards `git status --short ..\paper09-example\` prints nothing and step 7's diff is empty. Both
rebuilds matter and for different reasons: without the solution the other eight labs run against binaries
carrying the growth, and without the probe the `query` above would still be linked against a domain that
has a `Score` and would answer instead of refusing.

**That refusal is the point of the line, not an accident of it.** It is the third reading of one unchanged
record: the concept present (step 4), the concept ignored by a host that predates it (step 5), the concept
gone. Which is why the session is removed on the line *after* the query and not before — an earlier
version of this block deleted it first, and then the query failed for the boring reason that there was no
session left to read. Found in QA.

## The adoption cost, which divides exactly over twelve hosts

**0 for four, 1 for five, 4 for each of the two browser hosts, 32 for the input host** — the only place
where the level changes what happens. Those figures are the sum over **both** growth commits *and* **both**
adoption commits; taking only the first two understates the input host, which is how an earlier count came
to sum to thirteen.

Adoption is not one of the eight steps. Those measure that nothing *had* to change; these figures measure
what it cost the hosts that *wanted* the new concepts.

## Contents

Everything this lab needs to run is here:

    Scoring.cs  Difficulty.cs  Well.cs    the three files the +98 spans, as they stood on branch
                                          claude/trusting-tereshkova-f48ab6
    smoke.sh                              the twelve hosts, before and after
    replay.sh                             the pre-growth journals against the grown domain
    journals-pre-growth/                  the eight fixtures replay.sh reads

Both scripts take a **label** and nothing else; the example's root is an optional second argument that
already defaults to `..\paper09-example`. Their output lands in `out/` beside them, and `smoke.sh` deletes
the throwaway sessions it created before it exits.

One thing lives in the example instead, because that is where the script looks for it:
`../paper09-example/tools/growth-probe/`, the apparatus that records a journal with one build of the domain
and replays it with another. It is deliberately **outside `Tetris.sln`**, so it never enters the twelve-host
count — it is measurement, not a staging.

What is in `data/paper09-labI-growth/` is only the record of the original run: the replay logs before the
change and after each of the two growth steps, and the three smoke transcripts. Those carry absolute paths
from the machine that produced them, because a log is evidence of a run and the path a binary was invoked
from is a fact of that run. Read them as transcripts, never as instructions.
