# Paper 9 — runbook: where the labs are, and how to run them yourself

Written 2026-07-26 so the labs can be run by the author directly rather than reported to him —
a deposit needs the author's presence. Every command below is one the author runs.

## First, a problem you should know before inspecting anything

**`main` of `puppeteer-examples` contains no lab and no lab note.** The example itself is there —
the domain, the actor, the twelve hosts — but every lab and every note lives on a branch that was
never merged. That is a reproducibility defect in its own right, and it has to be fixed before the
paper is deposited, because Code provenance promises these notes as `paper09-data.zip`.

### Where each lab lives

Lab G's harness is vendored into this repository at `labs/paper09-labG-redecomposition/` and that
is the copy to use — read its README first. The other labs (F ported baseline, I domain growth,
H recognition, C clients write-up and the mirilla's own, plus G's pre-correction first run and the
Experiment A stagings write-up) each live on their own unmerged branch of `puppeteer-examples`;
until they are merged or assembled into `paper09-data.zip`, the branch-per-lab mapping is kept
outside this note. Labs G and B share one tree, on branch `p9/labg-rerun`.

Lab G's original tree builds against the engine pinned at `dd67047`, and its csproj reaches for a
sibling `..\..\..\eng\` checkout — a Puppeteer checkout at that commit must sit beside the lab's
tree or the build fails.

### The files to inspect

**Lab G** — in its tree:

    Tetris\redecomp\                                     the harness (play / redecompose / dump / equivalence)
    Tetris\notes\redecomposition-rerun-on-master.md      the run the paper cites — 135, 219+90, 2.29x
    Tetris\notes\redecomposition-pile-and-piece.md       the original write-up
    Tetris\actor\TetrisActor.csproj                      line 41: the engine reference to ..\..\..\eng\

**Lab B** — same tree as Lab G:

    Tetris\docker\run-demo.sh                            publish, compose up, wait for convergence
    Tetris\notes\experiment-a-crossmachine.md            the write-up
    Tetris\notes\experiment-a-crossmachine.log           the captured run

**Lab F** — in its tree:

    Tetris\baseline-hex\TetrisHex.sln                    build this
    Tetris\baseline-hex\domain\model\Well.cs           line 75: the restore constructor — Table 3 row II
    Tetris\baseline-hex\domain\model\Pile.cs           line 51: the pile factory, and its comment
    Tetris\baseline-hex\domain\ports\                  the four ports: three driven, one driving
    Tetris\baseline-hex\domain\AssemblyInfo.cs          the grant, to the test suite only
    Tetris\baseline-hex\domain.tests\                   the 64 tests; the 20 that need stand-ins are here
    Tetris\baseline-hex\domain.tests\doubles\          the three stand-ins themselves
    Tetris\notes\baseline-hexagonal.md                   the write-up, with the per-staging counts

**Lab I** — in its tree:

    Tetris\domain\Scoring.cs                             39 lines, new
    Tetris\domain\Difficulty.cs                          38 lines, new
    Tetris\domain\Well.cs                                +21 -3 — the third file the +98 spans
    Tetris\notes\domain-growth-score-and-difficulty.md   the write-up
    Tetris\notes\data\replay.sh                         replays the pre-growth journals
    Tetris\notes\data\smoke.sh                          the twelve hosts, before and after
    Tetris\notes\data\journals-pre-growth\             records written before the domain grew
    Tetris\notes\data\replay-pre-change.log             and the two post-experiment logs beside it

**Lab H** — in its tree:

    Tetris\notes\recognition-across-stagings.md          the write-up
    Tetris\notes\recognition-across-stagings.log         the captured run

**Labs A, C, D, E and the mirilla** — in `puppeteer-examples`:

    Tetris\domain\                                       the domain — Lab E is read off this directory
    Tetris\domain\TetrisDomain.csproj                    four properties, no references
    Tetris\domain\AssemblyInfo.cs                        lines 8-9: the two grants
    Tetris\domain\Well.cs                                line 322: OccupiedInterior, the in-domain join
    Tetris\actor\TetrisActor.csproj                      line 11: the one declared edge into the domain
    Tetris\watch\                                        the mirilla — receives pushed frames
    Tetris\observer\                                     the poll fallback — reconstructs by re-reading
    Tetris\ai\                                           the automated player, one act per process
    Tetris\tools\pile-scan.ps1                           the view the LLM client wrote for itself
    Tetris\console\ web\ web-rest\ sm-duo\ sm-duo-tls\ sm-server\ stage\ send\   the other hosts

The write-ups for Experiments A and B, in their respective trees:

    Tetris\notes\experiment-a-topology.md        (Experiment A stagings branch)
    Tetris\notes\experiment-b-audience.md        (Lab C's tree)
    Tetris\notes\mirilla-and-tetris.md           (Lab C's tree)

## The mirilla: watch a game while it is played

This is the mechanism you remember. Two processes, two consoles. The game emits each frame through
the substrate's push channel to the session's frame file; the viewer receives it with a
`FileSystemWatcher` and prints it. It never queries or replays — it renders the game's own emitted
projection.

**Console 2 — the viewer.** Start it first and leave it running:

```bash
dotnet run --project Tetris/watch/TetrisWatch.csproj -- demo1
```

**Console 1 — play.** Each invocation is a separate short-lived process that applies one act and
exits, which is the point: the domain's state lives in the journal, not in a process.

```bash
dotnet run --project Tetris/ai/TetrisAi.csproj -- demo1 new
```

Then `left`, `right`, `rotate`, `tick`, `drop`, `view` in place of `new`. Watch console 2 print a
frame the instant each act lands.

`TetrisObserver <session>` is the documented fallback — it polls and reconstructs the board by
re-reading the journal, which is the floor the push channel improves on. Running both at once is the
clearest single demonstration in the example of §4's distinction between being *told* and
*reconstructing*.

**The automated player's own view** — the adapter written by the client rather than by the domain
(§3), which is the paper's sharpest evidence:

```bash
pwsh Tetris/tools/pile-scan.ps1 -Session demo1
```

## Logging: get everything to a file you keep

PowerShell, showing output *and* writing it:

```bash
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- equivalence clears 20 2000 2>&1 | Tee-Object -FilePath logs/labG-equivalence-clears.log
```

For a whole session, start a transcript instead and everything in that console is captured:

```bash
Start-Transcript -Path logs/labG-session.log
```

End it with `Stop-Transcript`. Prefer this when you are running several commands and want the order
and the timing preserved — which is what presence during a lab means.

## Lab G — re-decomposition (the one with the most numbers in the paper)

On branch `p9/labg-rerun`. Note that its engine reference points at `../../eng/`, a Puppeteer
checkout pinned to `dd67047`; that has to be present or the build fails.

```bash
dotnet build Tetris/redecomp/TetrisRedecomp.csproj
```

Then, in order — `<run>` is any fresh directory:

```bash
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- play <run>/orig 1 400
```

```bash
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- redecompose <run>/orig <run>/split
```

```bash
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- dump played <run>/orig
```

What the paper claims from this: **136 entries** in the original record, **225 + 91 = 316** across the
two roles, ratio **2.32×**, and 129 acts to game over. The `dump` output is where you check the 136.

Those four figures were re-taken 2026-08-06, after every command in the example became a parametrized
ActorV2 Action. Three of them moved and 129 did not, because the migration changed the record's
*encoding* — a template written once plus a compact argument per act, where a V1 literal script wrote
one full sentence per call — and not the behaviour. The paths in this file also predate the vendoring:
the harness now lives at `labs/paper09-labG-redecomposition/redecomp/` in the papers repository, and it
carries the re-cut itself in `../split/`. Read the lab's own README rather than this section.

Then the equivalence runs, which are the 47,783 steps and 0 divergences:

```bash
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- equivalence random 20 2000
```

```bash
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- equivalence flat 20 2000
```

```bash
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- equivalence clears 20 2000
```

Expect 2,614 + 5,169 + 40,000 steps and **0 divergences** in all three.

## Lab F — the ported baseline (the comparison the whole argument rests on)

In Lab F's tree, from `Tetris/baseline-hex/`:

```bash
dotnet build TetrisHex.sln
```

```bash
dotnet test domain.tests/TetrisHexDomain.Tests.csproj
```

The paper claims **64 tests, of which 20 cannot run without stand-ins for the three driven ports.**
That number is the one to check hardest, because Table 3 rests on it.

```bash
dotnet run --project console/TetrisHexConsole.csproj -- --auto
```

```bash
dotnet run --project web/TetrisHexWeb.csproj
```

Then `web-rest/TetrisHexWebRest.csproj` on :5091, and `pwsh ./tools/hex-pile-scan.ps1 -Session play1`
for the automated player's view.

Also worth reading rather than running, since it is the paper's most attackable figure: the restore
constructor at `domain/model/Well.cs:75` and the factory at `domain/model/Pile.cs:51`, whose comments
say why they exist. Table 3's row II is those two.

## Lab B — three machines

Same tree as Lab G (`p9/labg-rerun`). Needs Docker Desktop running.

```bash
bash Tetris/docker/run-demo.sh
```

It publishes the cluster host, builds the image, brings up three containers, and waits for three
convergence checkpoints. Tear down with `--down`. The check the paper reports is that the three
nodes' frames are byte-identical:

```bash
for f in a b c; do docker compose exec -T tetris-$f cat /data/tetris-$f.frame | md5sum; done
```

## Lab I — domain growth

In Lab I's tree. Its branch also carries `Tetris/notes/data/` with
the pre-growth journals and the replay logs, so you can replay records written *before* the domain
grew and see that they still rehydrate.

```bash
bash Tetris/notes/data/replay.sh
```

```bash
bash Tetris/notes/data/smoke.sh
```

The paper claims **+98 −3** over `domain/` across the growth commits, of which 30 are code, and an
adoption cost of 0 for four hosts, 1 for five, 4 for each of two browser hosts, 32 for the input
host. The 32 lands in the *adoption* commit, not the growth commit — that distinction is what an
earlier count got wrong.

## What I would check first, in your position

1. **Lab F's 20-of-64.** It carries Table 3, and Table 3 carries the paper's central term.
2. **Lab G's 135.** Every ratio in the paper divides by it, and it was 130 in an earlier draft.
3. **Lab I's adoption split.** It summed to 13 over 12 until yesterday.
4. **The mirilla, for its own sake** — not to check a number, but because seeing a frame arrive the
   instant an act lands is the paper's claim happening in front of you.
