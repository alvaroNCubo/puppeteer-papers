# Paper 9 — Lab C: six clients over one domain

With the stage held fixed the client varies: a person at a keyboard reading an on-screen grid; an
automated player sending moves over a pipe and reading a view it computes for itself; two passive
observers, one pulling the board and one receiving it pushed; and a browser in which a player and a
spectator are both written in JavaScript.

Headline → §3 and Appendix A (Lab C). **0 domain edits** for any of the six. Each client adds an
input adapter, an output adapter, or both.

The evidence the paper leans on hardest is here: **one of those adapters was written by the client
that reads through it**, not by the author of the domain. The automated player is an instance of a
large language model and authored `pile-scan.ps1` — its column-height view of the board — during
the lab, to suit the form in which it reasons. Disclosed in the paper's acknowledgments.

## Run

    pwsh pile-scan.ps1 -Session <session>

against a session the automated player has played (`TetrisAi <session> new`, then `left`, `right`,
`rotate`, `tick`, `drop`).

## Contents

`pile-scan.ps1`, the client-authored view. The other five clients are hosts of the example itself
and are not copied here. Write-ups in `data/paper09-labC-clients/`.
