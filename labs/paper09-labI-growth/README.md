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

## Run

    bash replay.sh    # replays the journals written BEFORE the domain grew
    bash smoke.sh     # the twelve hosts, before and after

## Contents

The three files the +98 spans — `Scoring.cs` and `Difficulty.cs`, new, and `Well.cs` at +21/−3 — as
they stood on branch `claude/trusting-tereshkova-f48ab6`. The write-up, the pre-growth journals, the
replay logs and the smoke transcripts are in `data/paper09-labI-growth/`.
