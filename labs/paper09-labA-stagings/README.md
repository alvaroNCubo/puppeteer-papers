# Paper 9 — Lab A: five stagings, no change to the domain

**Headline → §2 (Experiment A) and Appendix A (Lab A): 0 domain edits across five stagings.**

## What it proves, and how to see it in two commands

The domain stopped changing, and the repository did not. That is the whole lab.

```
git log -1 --date=short --format="%ad  %s" -- Tetris/domain/
```

```
git log -1 --date=short --format="%ad  %s"
```

The first date is older than the second. Everything between them — including four of the five
stagings — was built without the domain being touched.

No commit hashes to copy and nothing to pin: both commands find their own answer, so they keep
working after you pull and after anyone commits again. If you want the size of the gap:

```
git rev-list --count "$(git log -1 --format=%H -- Tetris/domain/)..HEAD"
```

That prints how many commits happened after the domain's last change.

## Where and when

**Not in the vendored example.** This is the one lab a copy cannot carry: its claim is about a
*history*, and `labs/paper09-example/` is a copy whose only commit is the one that placed it here. Run
these two commands in a clone of the examples repository the paper names, on `main`.

If you would rather not clone anything, the same two commands are already captured in
`../../data/paper09-labA-stagings/chronology.txt`, together with the dates of the four hosts that
postdate the domain's last change.

**Any time, as often as you like** — they read history and touch nothing, so there is no session to
create, nothing to clean up, and no other lab to run first.

## Run it between the other labs, too

Used that way it is the suite's standing check that the domain has not moved — and its value is that
it is **not** always silent. Two of the nine labs are *expected* to move the date, and they move it
for different reasons:

| Lab | Expected to change the domain? | What it reports | Why |
|---|---|---|---|
| A, B, C, D, E, F, H | **no** | the domain's date stays put | none of them touches the domain; that is what they measure |
| **I** growth | **yes** | **+98 −3** over three files — `Scoring.cs`, `Difficulty.cs`, `Well.cs` | it **adds concepts**. A score and a difficulty level are things the domain did not know before |
| **G** re-decomposition | **yes** | **+643 −4** over five files — four new, plus `Pile.cs` | it **redraws a boundary**. No new concept: the same rules split across two roles |

Telling the two apart matters, and the file list does it. **Lab I adds to what the domain knows; Lab G
re-cuts what it already knew.** The signature of Lab G is that `Well.cs` is **absent** from its
change — the original rules are untouched while a new decomposition is written beside them. If
`Well.cs` ever appears there, the claim that a re-cut leaves the original rules alone has failed, and
that is the one thing this check can catch that nothing else does.

Anywhere else, a moved date means the domain was touched by something that is not one of those two
labs — which is the only reading of this check that should worry anyone.

## Output on disk

```
git log -1 --date=short --format="%ad  %s" -- Tetris/domain/ > labA.txt
git log -1 --date=short --format="%ad  %s" >> labA.txt
```

Two lines, two dates, and the older one is the domain's.

## Contents

Nothing to build. This lab's five stagings *are* hosts of the example — `console/`, `web/`,
`web-rest/`, `sm-duo/`, `sm-duo-tls/` — and its evidence is that the domain's history stops while
theirs continues. The write-up is in `data/paper09-labA-stagings/`.
