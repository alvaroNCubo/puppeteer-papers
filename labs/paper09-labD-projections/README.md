# Paper 9 — Lab D: three projections of one emitted frame

One frame is emitted by the domain and rendered three ways without the domain knowing of any of
them: a character grid, a column-height vector, and a browser drawing. None of the three is
privileged, and the domain produces neither — it produces the fact all three are made from.

Headline → §3 and Appendix A (Lab D). **0 domain methods for any view.**

The two viewers here also demonstrate §4's distinction directly, and running both at once is the
clearest way to see it: `watch` **receives** each frame over the substrate's push channel and
prints it as it arrives, while `observer` **reconstructs** the board by re-reading the journal on a
poll. Told, against rebuilt from stills.

## Run

Two consoles. The viewer first:

    dotnet run --project <example>/Tetris/watch/TetrisWatch.csproj -- demo1

then, in the other, one act per process:

    dotnet run --project <example>/Tetris/ai/TetrisAi.csproj -- demo1 new

## Contents

The three projections' source as it stood on `main`: `pile-scan.ps1` (the vector),
`watch-Program.cs` (the push receiver) and `observer-Program.cs` (the poll fallback, whose own
comment explains why it re-opens the journal per poll).
