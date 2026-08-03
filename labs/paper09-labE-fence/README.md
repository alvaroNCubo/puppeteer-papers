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

## Check

    dotnet list package                  # reports no packages for the domain project
    dotnet test <example>/Tetris/domain.tests/TetrisDomain.Tests.csproj

## Contents

The three files the claim is read off:

    TetrisDomain.csproj      four properties, no references
    AssemblyInfo.cs          the two authored grants — one to the test project, one to a console
                             host which no longer exercises it
    TetrisActor.csproj       line 11: the single declared edge from the running system into the domain
