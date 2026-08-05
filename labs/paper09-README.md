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

Lab A is a one-time historical read, and it is the **one lab that cannot be self-contained**: its claim
is about a *history*, and the vendored copy in `labs/paper09-example/` has none of its own — it is a
copy, so its only commit is the one that placed it here. To check Lab A live you need the examples
repository, which the paper names. Its output is captured in
`data/paper09-labA-stagings/chronology.txt` for a reader who does not want to clone anything.

The between-runs check you are reaching for does exist, and it is inside Lab I rather than beside it:
`smoke.sh` runs the twelve host projects **before and after** the domain grows and shows all twelve
still running at zero edits. That is the converse of Lab A — Lab A holds the domain still and varies
the staging; Lab I holds the stagings still and varies the domain.

## Order

No lab requires another first, so this order is by cost and by how much rests on each — not a
dependency chain.

| | Lab | Why here | Needs |
|---|---|---|---|
| 1 | **A** stagings | seconds; reads history, builds nothing | the **examples repository** — the one lab a copy cannot carry. Captured output in `data/` |
| 2 | **E** the fence | seconds, three commands, no processes | `labs/paper09-example/` + the engine variable |
| 3 | **F** ported baseline | **the number to check hardest** — Table 3 rests on it | **nothing but .NET.** Fully self-contained |
| 4 | **G** re-decomposition | the most figures in the paper: 135, 309, 2.29×, three zeros | `labs/paper09-example/` + the engine variable |
| 5 | **H** recognition | read-only; a write-up and its captured log | **nothing** |
| 6 | **C**, **D** clients and projections | demonstrations; need a session played first | `labs/paper09-example/` + the engine variable |
| 7 | **B** three machines | slowest, and the only one needing containers | Docker Desktop + `labs/paper09-example/` |
| 8 | **I** domain growth | **last**, because it is the one that changes the domain | `labs/paper09-example/` + the engine variable |

Lab I is last for a reason worth stating: after it, the domain in that tree is no longer the domain
Labs A through H measured. It is on its own branch, so nothing is spoiled — but if you are working in
one tree, do it at the end.

## What every lab assumes

**The example is vendored here**, at `labs/paper09-example/` — the domain, the actor and the twelve
hosts, 66 files, in this repository's own history. So the labs and the paper are one git history and
publication is one commit mark, not two. Labs F and H need nothing at all beyond that: Lab F carries
`baseline-hex/` whole, and Lab H has nothing to run.

**One external remains, and only one: the engine.** The vendored example's actor is the single project
that reaches outside, and it now does so through a variable rather than a hardcoded path. Set it once
per session:

```powershell
$env:PuppeteerEngine = "C:\path\to\a\Puppeteer\checkout"
```

or pass `-p:PuppeteerEngine=<path>` per build. It must be a checkout at or after commit `dd67047` —
three substrate fixes these measurements depend on land at or before it. If it is unset the build stops
with that sentence rather than a mysterious path error.

The engine stays external deliberately. Papers 1 to 8 cite framework source against the public
Puppeteer repository under a Software Heritage identifier, and vendoring it here would duplicate a
codebase that is not this paper's contribution while contradicting that provenance model. What is
vendored is the paper's own artifact; what is cited is the framework.

**Lab A is the exception to self-containment**, and unavoidably: its claim is about a history, and a
copy has none. See above.

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
