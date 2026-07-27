# Paper 9 — runbook: where the labs are, and how to run them yourself

Written 2026-07-26 because the labs had been run for you rather than by you, and a deposit needs
your presence. Every command below is one you run; nothing here needs me.

## First, a problem you should know before inspecting anything

**`main` of `puppeteer-examples` contains no lab and no lab note.** The example itself is there —
the domain, the actor, the twelve hosts — but every lab and every note lives on a branch that was
never merged. That is a reproducibility defect in its own right, and it has to be fixed before the
paper is deposited, because Code provenance promises these notes as `paper09-data.zip`.

| Lab | Branch | What is there |
|---|---|---|
| A stagings, C clients, D projections | `main` | the 12 hosts and `tools/pile-scan.ps1`; no notes |
| B three machines | `p9/labg-rerun` | `Tetris/docker/`, `notes/experiment-a-crossmachine.md` + its `.log` |
| E fence / no references | `main` | the `domain/` project itself; assertions are read off the code |
| F ported baseline | `claude/confident-satoshi-7ed985` | `Tetris/baseline-hex/` (54 files), `notes/baseline-hexagonal.md` |
| G re-decomposition | `p9/labg-rerun` | `Tetris/redecomp/`, `notes/redecomposition-pile-and-piece.md`, `notes/redecomposition-rerun-on-master.md` |
| G (first run) | `claude/agitated-brattain-e35650` | the earlier, pre-correction state — useful only for comparison |
| I domain growth | `claude/trusting-tereshkova-f48ab6` | `notes/domain-growth-score-and-difficulty.md`, plus `notes/data/` with the pre-growth journals, replay logs and smoke transcripts |
| H recognition | `claude/jovial-goldstine-a03293` | `notes/recognition-across-stagings.md` |
| A stagings (write-up) | `claude/vibrant-tesla-c7d897` | `notes/experiment-a-topology.md` |
| C clients (write-up) | `claude/upbeat-pare-d0594f` | `notes/experiment-b-audience.md`, and `notes/mirilla-and-tetris.md` — the viewer's own write-up |

Use a **worktree per lab** rather than switching branches in the shared checkout, which sits on
`f7-ensemble-consume` and belongs to other work:

```bash
git -C C:/Users/alvar/source/repos/puppeteer-examples worktree add C:/Users/alvar/source/repos/_labs/labF claude/confident-satoshi-7ed985
```

## The mirilla: watch a game while it is played

This is the mechanism you remember. Two processes, two consoles. The game emits each frame through
the substrate's push channel to the session's frame file; the viewer receives it with a
`FileSystemWatcher` and prints it. It never queries or replays — it renders the game's own emitted
projection.

**Console 2 — the viewer.** Start it first and leave it running:

```bash
dotnet run --project C:/Users/alvar/source/repos/puppeteer-examples/Tetris/watch/TetrisWatch.csproj -- demo1
```

**Console 1 — play.** Each invocation is a separate short-lived process that applies one act and
exits, which is the point: the domain's state lives in the journal, not in a process.

```bash
dotnet run --project C:/Users/alvar/source/repos/puppeteer-examples/Tetris/ai/TetrisAi.csproj -- demo1 new
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
pwsh C:/Users/alvar/source/repos/puppeteer-examples/Tetris/tools/pile-scan.ps1 -Session demo1
```

## Logging: get everything to a file you keep

PowerShell, showing output *and* writing it:

```bash
dotnet run --project Tetris/redecomp/TetrisRedecomp.csproj -- equivalence clears 20 2000 2>&1 | Tee-Object -FilePath C:/Users/alvar/source/repos/_labs/logs/labG-equivalence-clears.log
```

For a whole session, start a transcript instead and everything in that console is captured:

```bash
Start-Transcript -Path C:/Users/alvar/source/repos/_labs/logs/labG-session.log
```

End it with `Stop-Transcript`. Prefer this when you are running several commands and want the order
and the timing preserved — which is what presence during a lab means.

## Lab G — re-decomposition (the one with the most numbers in the paper)

Worktree on `p9/labg-rerun`. Note that its engine reference points at `../../eng/`, a worktree of
Puppeteer pinned to `dd67047`; that has to be present or the build fails.

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

What the paper claims from this: **135 entries** in the original record, **219 + 90 = 309** across the
two roles, ratio **2.29×**, and 129 acts to game over. The `dump` output is where you check the 135.

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

Worktree on `claude/confident-satoshi-7ed985`, then from `Tetris/baseline-hex/`:

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

Worktree on `p9/labg-rerun`. Needs Docker Desktop running.

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

Worktree on `claude/trusting-tereshkova-f48ab6`. This branch also carries `Tetris/notes/data/` with
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
