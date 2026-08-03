# Paper 9 — Lab F: the ported baseline, built and counted

An orthodox ports-and-adapters arrangement of the same game, written so the comparison in §9 is a
measurement rather than an estimate. Its eleven rule files differ from the journaled domain's by one
line each — the namespace — so the comparison is not rigged by writing a worse domain. Four stagings
were added one commit at a time so each could be counted: a console, a WebSocket host, a
REST-and-server-sent-events host, and an automated player.

Headline → §9 (Table 3) and Appendix A (Lab F). This lab **corrects the paper more than it confirms
it**: two of four estimated claims are refuted, one ties, and the difference that survives is not
the one the estimates predicted.

The two rows of Table 3 this lab supplies:

- **three driven ports** — board output, piece selection, state — against none in the journaled
  domain, and with them three stand-ins without which **20 of 64 tests do not run**;
- **a reconstitution surface**: a restore constructor and a pile factory, **56 lines added and 5
  removed** inside the rule model, against none.

The second is the paper's most attackable figure and the two files worth reading before anything is
run are `domain/model/Well.cs` (the restore constructor) and `domain/model/Pile.cs` (the factory),
whose own comments say why they had to exist.


## Order, consoles, and what each shows

**Order: 1 and 2 first, in one console; then 3 to 6, each in its own.** Steps 1 and 2 are the
verification. Steps 3 to 6 are the four stagings, and they only demonstrate.

| # | Run this | What you see in it | Who operates it |
|---|---|---|---|
| 1 | `dotnet build TetrisHex.sln` | It builds. | You. |
| 2 | `dotnet test domain.tests/TetrisHexDomain.Tests.csproj` | **64 tests, and the number that cannot run without stand-ins for the three driven ports.** This is the figure to check hardest — Table 3 rests on it. | **You.** The only step that verifies rather than demonstrates. |
| 3 | `dotnet run --project console/TetrisHexConsole.csproj -- --auto` | Staging 1 self-plays and renders, non-interactively. | You, once. |
| 4 | `dotnet run --project web/TetrisHexWeb.csproj` | Staging 2 serving on **:5090**. Leave it; open a browser. | Nobody after launch. |
| 5 | `dotnet run --project web-rest/TetrisHexWebRest.csproj` | Staging 3 serving on **:5091**. Leave it. | Nobody after launch. |
| 6 | `pwsh ./tools/hex-pile-scan.ps1 -Session play1` | Staging 4's view of the board. | You, whenever. |

**Output on disk.** Step 2 is the one to capture, because it carries the paper's number:

```powershell
dotnet test domain.tests/TetrisHexDomain.Tests.csproj | Tee-Object -FilePath labF-tests.log
```

In `labF-tests.log` a reviewer counts the total and the failures-without-doubles. The three stand-ins
themselves are files, in `domain.tests/doubles/` — open them and count: one per driven port.

**Read, do not run, for the row that matters most.** Table 3's second row is two files:

    domain/model/Well.cs    line 75  — the restore constructor, whose comment names the cost outright
    domain/model/Pile.cs    line 51  — the pile factory, added for staging 4 because, its comment
                                       says, "the only way in was through the model"

Those two are the 56 lines added and 5 removed, and they exist because reconstituting a closed
aggregate needs a way in. That is the paper's most attackable figure and the two comments are the
best evidence for it.

**This lab corrects the paper more than it confirms it**: of four estimated claims, two are refuted,
one ties, and the difference that survives is not the one the estimates predicted.

## Contents

`baseline-hex/` in full, as it stood on branch `claude/confident-satoshi-7ed985` of the examples
repository. Write-up, with the per-staging counts and the line-by-line account of what persistence
forced, in `data/paper09-labF-ported-baseline/`.
