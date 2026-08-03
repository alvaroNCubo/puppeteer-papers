# Paper 9 — Lab G: the domain's own internal boundary

One role modelled too large is cut into two — a pile role and a piece role — by authoring the two,
reading the original's recorded acts (the same read rehydration performs) and driving each new role
to perform its own, so each ends holding a record in its own voice. The original journal is not cut,
transformed or rewritten: it is read as the account of what happened, and kept.

Headline → §8.2 and Appendix A (Lab G). **11 of 12 host projects untouched** while the domain
divided beneath them, the twelfth needing one line; **0 divergences over 47,783 steps**; and the
record costing **2.29×** — 309 entries across the two roles against 135 for the same game.

This lab also carries the premises of §8.4's constraint, each checked here: the undivided board
built a complete frame by unioning the pile's cells with the falling piece's *inside* the domain,
and a projection on the emitting plane reaches only its own actor's state — so after the cut neither
role can push a whole frame.

## Run

Its engine reference reaches for `..\..\..\eng\`, so a Puppeteer worktree pinned at or after
`dd67047` must sit beside it. `<run>` is any fresh directory.

    dotnet build redecomp/TetrisRedecomp.csproj
    dotnet run --project redecomp/TetrisRedecomp.csproj -- play <run>/orig 1 400
    dotnet run --project redecomp/TetrisRedecomp.csproj -- redecompose <run>/orig <run>/split
    dotnet run --project redecomp/TetrisRedecomp.csproj -- dump played <run>/orig       # check the 135
    dotnet run --project redecomp/TetrisRedecomp.csproj -- equivalence random 20 2000   # 2,614 steps
    dotnet run --project redecomp/TetrisRedecomp.csproj -- equivalence flat 20 2000     # 5,169
    dotnet run --project redecomp/TetrisRedecomp.csproj -- equivalence clears 20 2000   # 40,000

Expect 0 divergences in all three.

## Contents

`redecomp/` as it stood on branch `p9/labg-rerun`. Two write-ups in
`data/paper09-labG-redecomposition/` — the original run, and the re-run on engine master with the
actor correction merged, which is the one the paper cites and which moved the ratio from 2.38× to
2.29×.
