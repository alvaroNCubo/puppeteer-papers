# Paper 9 — the nine labs: what to run, in what order, and one trap to avoid

Each lab has its own README with its commands, its consoles and the figure it should produce. This
file answers the three questions that only make sense across the suite.

## Does a reviewer run one lab, or all of them?

Each lab stands alone. None depends on another having been run, and none leaves state another
consumes. Run one and its own claim is checked; run all nine and the paper's Appendix A is checked.

The one you cannot skip if you only run one is **Lab F**. It is the comparison the whole argument
rests on, and the paper's central term — *closure* — is measured in it. Everything else either
supports that or bounds it.

## Is Lab A a check to run *between* the others?

No, and this is the trap. **Lab A measures that the domain did not change across the five stagings**,
as a diff over the main line's history. **Lab I and Lab G change the domain on purpose** — Lab I grows
it by 98 lines, Lab G cuts one role into two. So running Lab A's diff on either of their branches will
correctly report a non-empty diff, and that is not a failure of Lab A: it is a different measurement.

Lab A is a one-time historical read. Run it once, on a clone of the examples repository, and it is
done.

The between-runs check you are reaching for does exist, and it is inside Lab I rather than beside it:
`smoke.sh` runs the twelve host projects **before and after** the domain grows and shows all twelve
still running at zero edits. That is the converse of Lab A — Lab A holds the domain still and varies
the staging; Lab I holds the stagings still and varies the domain.

## Order

No lab requires another first, so this order is by cost and by how much rests on each — not a
dependency chain.

| | Lab | Why here | Needs |
|---|---|---|---|
| 1 | **A** stagings | seconds, reads git history, nothing to build | a clone of the examples repo |
| 2 | **E** the fence | seconds, three commands, no processes | the examples repo |
| 3 | **F** ported baseline | **the number to check hardest** — Table 3 rests on it | nothing beyond .NET; the baseline is copied here in full |
| 4 | **G** re-decomposition | the most figures in the paper: 135, 309, 2.29×, three zeros | the examples repo **and** an engine worktree at or after `dd67047` |
| 5 | **H** recognition | read-only; a write-up and its captured log | nothing |
| 6 | **C**, **D** clients and projections | demonstrations; need a session played first | the examples repo |
| 7 | **B** three machines | slowest, and the only one needing containers | Docker Desktop |
| 8 | **I** domain growth | **last**, because it is the one that changes the domain | the examples repo |

Lab I is last for a reason worth stating: after it, the domain in that tree is no longer the domain
Labs A through H measured. It is on its own branch, so nothing is spoiled — but if you are working in
one tree, do it at the end.

## What every lab assumes

**A clone or worktree of the Puppeteer examples repository**, except Labs F and H. Lab F is
self-contained — `baseline-hex/` is copied here whole. Lab H has nothing to run. Everything else
reaches into the example for the domain, the actor and the twelve hosts, and the per-lab README says
where with an `<example>` placeholder.

**Lab G additionally needs an engine worktree** pinned at or after commit `dd67047`, sitting beside
its own tree, because its project reaches for `..\..\..\eng\`. Three substrate fixes its measurements
depend on land at or before that commit.

**One writer per session, always.** Several labs write journals. Never point two writers at one
session name — a check-then-command journals the *command* and not the *check*, so a warm actor's
stale view will pass a check the journal's true sequence contradicts, and replay then logs the
violation and carries on. The reconstructed board is wrong and looks fine. Lab D's README sets this
out, because that is where it bites first.

## Where the output goes

Every README names the artifact its lab leaves behind and how to capture it. Two are worth knowing in
advance because they are not text: **Lab G's output is journals** — three directories of records,
read back with the harness's own `dump` sub-command — and **Lab B's is per-node frame files inside
container volumes**, whose byte-identity is the result. Everything else is a log you can `Tee-Object`
or a `Start-Transcript` away.

## Ask the programs, not this file

`TetrisSend`, `TetrisStage`, `TetrisAi` and the Lab G harness all print their own usage when run with
no arguments or too few. A list in a README can go stale; a program's own usage cannot.
