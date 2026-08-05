# Paper 9 — Lab I: the domain itself grows

A score and a difficulty level are added to the board: the first the *authority* case — a value any
client could compute, and which therefore has to be single-valued — and the second a rule over lines
already recorded.

Headline → §8.2 and Appendix A (Lab I). Over the whole domain directory the change is **+98 lines
and −3**, across three files rather than the two new ones alone; of the 98 added, **30 are code**,
62 are the domain's own doc-comment convention and 6 are blank. All 3 removed lines are doc-comment
prose. No signature changed and no verb was removed, established by diffing a sorted inventory of
the board's members.

What the lab supplies that the paper had never measured is the **converse** direction: when the
domain grew, **all twelve host projects kept running with no edit**, verified by running them before
and after rather than by compiling them. Adoption then cost only what wanted it, and the twelve
divide exactly: **0 for four, 1 for five, 4 for each of the two browser hosts, 32 for the input
host** — the only place where the level changes what happens. Those figures are the sum over both
growth commits *and* both adoption commits; taking only the first two understates the input host.


## Order, consoles, and what each shows

One console. **Order: 1, then 2** — the replay establishes that old records still rehydrate, and the
smoke test then establishes that the stagings still run.

| # | Run this | What you see in it | Who operates it |
|---|---|---|---|
| 1 | `bash ../../data/paper09-labI-growth/replay.sh ../paper09-example` | **Journals written *before* the domain grew, rehydrating against the grown domain.** Old records, new code. | **You.** Read-only against the fixtures. |
| 2 | `bash ../../data/paper09-labI-growth/smoke.sh ../paper09-example` | **Twelve host projects, before and after.** All twelve run with no edit — that is the converse the paper had never measured. | You. |

Both scripts take the example's root as an argument, so neither needs a path edited to be usable.

**Output on disk — already captured, and worth reading before re-running.** In
`../../data/paper09-labI-growth/`:

    replay-pre-change.log                 the replay before the domain grew
    replay-post-experiment1-score.log     after the score was added
    replay-post-experiment2-level.log     after the level was added
    smoke-pre-change.txt                  the twelve hosts, before
    smoke-after-experiment1.txt           after the score
    smoke-after-experiment2-adopted.txt   after the level, with adoption
    journals-pre-growth/                  the fixtures themselves — records written before the change

Those files contain absolute paths from the machine that produced them. They are transcripts, not
instructions; the README in that directory says so.

**Read, do not run, for the counts.** The change is **+98 lines and −3** over the domain directory,
across *three* files rather than the two new ones alone — `Scoring.cs` at 39, `Difficulty.cs` at 38,
and `Well.cs` at +21/−3. Of the 98 added, **30 are code**; 62 are the domain's own doc-comment
convention and 6 are blank. All 3 removed lines are doc-comment prose.

**And the adoption cost, which divides exactly over twelve hosts:** 0 for four, 1 for five, 4 for each
of the two browser hosts, 32 for the input host — the only place where the level changes what happens.
Those figures are the sum over **both** growth commits *and* **both** adoption commits; taking only the
first two understates the input host, which is how an earlier count came to sum to thirteen.

## Contents

The three files the +98 spans — `Scoring.cs` and `Difficulty.cs`, new, and `Well.cs` at +21/−3 — as
they stood on branch `claude/trusting-tereshkova-f48ab6`. The write-up, the pre-growth journals, the
replay logs and the smoke transcripts are in `data/paper09-labI-growth/`.
