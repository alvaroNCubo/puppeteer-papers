# Paper 9 — Lab E: both sides testable, with the framework absent

The domain's own tests run with no staging bound at all — no host, no transport, no sink, and no
double, neither for what drives the domain nor for what it emits. Stronger, and this is the form in
which §2's claim is checkable rather than interpretive: **the framework is absent from the domain's
build graph altogether.**

Headline → §2 and Appendix A (Lab E). **0 project references, 0 packages, 0 test doubles**, and the
suite passes on that graph.

## Four commands, one console, in this order

The order is what makes the last one mean anything: nothing to list, then it builds, then it passes.

```powershell
dotnet list ..\paper09-example\domain\TetrisDomain.csproj package
```

```powershell
dotnet build ..\paper09-example\domain\TetrisDomain.csproj
```

```powershell
dotnet test ..\paper09-example\domain.tests\TetrisDomain.Tests.csproj
```

```powershell
Get-ChildItem ..\paper09-labF-ported-baseline\baseline-hex\domain.tests\doubles\
```

What each should print:

| | |
|---|---|
| 1 | `No packages were found for this framework.` — literally nothing to list |
| 2 | `Build succeeded. 0 Warning(s) 0 Error(s)`, with the framework absent from the graph entirely |
| 3 | `Passed! — Failed: 0, Passed: 44` |
| 4 | three stand-ins — `RecordingBoardOutput`, `ScriptedPieceSelection`, `InMemoryGameState`. **The domain's own tests have no such directory**, which is the third zero of the headline made into a comparison you can run rather than a claim you have to take |

Keep all four in one transcript, since it is the sequence that is the argument:

```powershell
Start-Transcript -Path labE-fence.log
# the four commands above
Stop-Transcript
```

## Read, do not run — three files, all in the example

Nothing is copied into this directory; these are the files the claim is read off, in place:

    ..\paper09-example\domain\TetrisDomain.csproj      four properties and no references at all
    ..\paper09-example\domain\AssemblyInfo.cs          lines 8-9: the two authored grants — one to the
                                                      test project, one to a console host that no
                                                      longer exercises it
    ..\paper09-example\actor\TetrisActor.csproj        line 11: the single declared edge from the
                                                      running system into the domain

## What this lab does not settle

A ported rule model has the same clean graph — Lab F built one and counted it — so "zero references,
zero packages" separates a clean domain from an entangled one, not this arrangement from that one. The
side that does **not** tie is the driven one: three ports there, and twenty of its sixty-four tests
cannot run without stand-ins for them. Which is what command 4 above puts in front of you.
