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


## Order, consoles, and what each shows

One console throughout, and **the order is strict** — steps 2 to 4 each consume what the one before
it wrote. `<run>` is any fresh empty directory.

Set `$env:PuppeteerEngine` to a Puppeteer checkout at or after `dd67047`, as every other lab in this
suite does. (This paragraph used to say the csproj reaches for a sibling `eng` directory by relative
path. It does not: the actor resolves the engine through that variable, and no worktree has to sit
anywhere in particular.)

| # | Run this | What you see in it | Who operates it |
|---|---|---|---|
| 0 | `dotnet run --project redecomp/TetrisRedecomp.csproj` | **The harness lists its own seven sub-commands.** Ask it rather than this file. | You, first, to see what is available. |
| 1 | `dotnet build redecomp/TetrisRedecomp.csproj` | It builds. | You. |
| 2 | `… -- play <run>/orig 1 400` | A whole game played on the **undivided** board: 129 acts to game over. | You. Writes the original journal. |
| 3 | `… -- redecompose <run>/orig <run>/split` | The cut, **in a fresh process**: the original's acts are read and re-performed into two roles. | You. Writes two new journals; never edits the first. |
| 4 | `… -- dump played <run>/orig` | **The entry counts.** `135` for the original is the figure the paper's 2.29× divides by. | You. Read-only. |
| 5 | `… -- equivalence random 20 2000` | 2,614 steps, **0 divergences**. | You. |
| 6 | `… -- equivalence flat 20 2000` | 5,169 steps, **0 divergences**. | You. |
| 7 | `… -- equivalence clears 20 2000` | 40,000 steps, **0 divergences**. | You. |

**Output on disk — this lab's output is journals, not text.** After step 3 you have three:

    <run>/orig      the original record, 135 entries
    <run>/split     the two roles' records, 219 and 90 entries, 309 together

`dump` is how you read any of them in a fresh process, which is the point: they are ordinary records
and the harness takes nothing special out of them. Capture the counts:

```powershell
Start-Transcript -Path labG-session.log
# steps 2 through 7
Stop-Transcript
```

Then `labG-session.log` holds every count the paper cites from this lab: 135, 219, 90, 309, 2.29×,
and three zeros for divergence.

**Read, do not run, for §8.4's premises.** They were checked here, and both are one line each: the
undivided board built a complete frame by unioning the pile's cells with the falling piece's *inside*
the domain, and a projection on the emitting plane reaches only its own actor's state. So after the
cut, neither role can push a whole frame.

## Contents

`redecomp/` as it stood on branch `p9/labg-rerun`. Two write-ups in
`data/paper09-labG-redecomposition/` — the original run, and the re-run on engine master with the
actor correction merged, which is the one the paper cites and which moved the ratio from 2.38× to
2.29×.
