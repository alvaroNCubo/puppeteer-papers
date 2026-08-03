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

## Run

    dotnet build TetrisHex.sln
    dotnet test domain.tests/TetrisHexDomain.Tests.csproj          # 64 tests
    dotnet run --project console/TetrisHexConsole.csproj -- --auto  # staging 1
    dotnet run --project web/TetrisHexWeb.csproj                    # staging 2, :5090
    dotnet run --project web-rest/TetrisHexWebRest.csproj           # staging 3, :5091
    pwsh ./tools/hex-pile-scan.ps1 -Session play1                   # staging 4's view

## Contents

`baseline-hex/` in full, as it stood on branch `claude/confident-satoshi-7ed985` of the examples
repository. Write-up, with the per-staging counts and the line-by-line account of what persistence
forced, in `data/paper09-labF-ported-baseline/`.
