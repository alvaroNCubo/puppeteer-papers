# Paper 9 — Lab E: both sides testable, with the framework absent

The domain's own tests run with no staging bound at all — no host, no transport, no sink, and no
double, neither for what drives the domain nor for what it emits. Stronger, and this is the form in
which §2's claim is checkable rather than interpretive: **the framework is absent from the domain's
build graph altogether.**

Headline → §2 and Appendix A (Lab E). **0 project references, 0 packages, 0 test doubles**, and the
suite passes on that graph.

What this count does *not* separate is settled by Lab F: a ported rule model has the same clean
graph, so "zero references, zero packages" distinguishes a clean domain from an entangled one
rather than this arrangement from that one. What does not tie is the driven side.


## Order, consoles, and what each shows

**Order: 1, then 2, then 3.** One console is enough; the order is what makes the third meaningful.

| # | Run this | What you see in it | Who operates it |
|---|---|---|---|
| 1 | `dotnet list package --project <example>/Tetris/domain/TetrisDomain.csproj` | **"No packages were found"** — literally nothing to list. | You. |
| 2 | `dotnet build <example>/Tetris/domain/TetrisDomain.csproj` | **It builds** with the framework absent from its build graph entirely. | You. |
| 3 | `dotnet test <example>/Tetris/domain.tests/TetrisDomain.Tests.csproj` | **The suite passes** with no host, no transport, no sink and **no test double** — not for what drives the domain and not for what it emits. | You. |

Keep all three in one transcript, since it is the *sequence* that is the argument:

```powershell
Start-Transcript -Path labE-fence.log
# the three commands above
Stop-Transcript
```

**Output on disk:** `labE-fence.log`. What a reviewer looks for in it is three absences — no packages,
no framework in the graph, no doubles — and one presence: a passing suite.

**Read, do not run, for the rest.** The four files in this directory are the claim: the domain project
is four properties and no references; `AssemblyInfo.cs` lines 8–9 are the two authored grants, one of
them to a console host that no longer exercises it; and `TetrisActor.csproj` line 11 is the single
declared edge from the running system into the domain.

**What this count does not separate** is settled by Lab F: a ported rule model has the same clean
graph, so "zero references, zero packages" distinguishes a clean domain from an entangled one rather
than this arrangement from that one. What does not tie is the driven side — three ports there, and
twenty of sixty-four tests that cannot run without stand-ins for them.

## Contents

The three files the claim is read off:

    TetrisDomain.csproj      four properties, no references
    AssemblyInfo.cs          the two authored grants — one to the test project, one to a console
                             host which no longer exercises it
    TetrisActor.csproj       line 11: the single declared edge from the running system into the domain
