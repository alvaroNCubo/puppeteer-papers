# Paper 9 — Lab A: five stagings, no change to the domain

**Headline → §2 (Experiment A) and Appendix A (Lab A): 0 domain edits across five stagings.**

The paper's row for this lab carries a second half — *the domain suite passes unchanged, a regression
check and not coverage* — and **these two commands do not show it.** They read history; nothing here runs
a test. The claim is that the suite kept passing as the five stagings were added, which is a fact about
the same history the diff is read from. Lab E is where a suite is actually run, and note that it
establishes a *different* property: that the 44 tests pass with the framework absent from the build graph.
Neither lab measures coverage, and the paper says so of both.

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
these commands in a clone of `github.com/alvaroNCubo/puppeteer-examples`, on `main`.

That is worth one sentence of its own, because it was not true until 2026-08-07: the example and its
whole history lived on unmerged branches and fifteen unpushed commits, so a reader following this
instruction would have found a repository containing a HelloWorld and nothing else. They are on `main`
now, at `ec9a2d4`.

If you would rather not clone anything, all of it is captured in
`../../data/paper09-labA-stagings/chronology.txt`.

## The longer gap, which is the one §8.3 uses

Four of the five stagings postdate the domain's last commit by a day or two. That is a short gap, and
the author-confounder argument in §8.3 does not rest on it. It rests on three things added on
**23 July, twenty-four days later**, in working sessions separate from the one that authored the domain:

```
git log --date=short --format="%ad  %h  %s" --diff-filter=A -1 -- Tetris/gesture Tetris/scarce Tetris/sm-cluster
```

A webcam gesture client, an emulated ESP32-C6, and the three-machine deployment — `33f151c`, `8d7536e`
and `974f62a`, all dated `2026-07-23`. Two of the three are not labs of this appendix; what is cited
from them is only their effect on the domain. And that effect is checkable in one command, which is the
strongest form this lab has:

```
git diff --name-only fd8d94b..main -- Tetris/domain/
```

Empty. The domain directory is untouched across every one of the fifteen commits that follow its last
change.

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

**But run it in the right repository, and only one of those two rows will ever appear.** The two
commands above read the *examples* repository's history, so they see a change only if it was committed
there. Neither of the two labs commits anything: Lab G carries its re-cut in its own `split/` directory
and compiles it separately, so nothing in the example moves at all, and Lab I copies three files into
the vendored example's domain and takes them out again. So in this repository the check that catches
either is a diff over the vendored copy —

```
git diff --stat -- labs/paper09-example/domain/
```

— which is empty for seven of the nine labs, empty for Lab G too, and reports `3 files changed, 98
insertions(+), 3 deletions(-)` while Lab I is applied. Lab I's own README gives the two commands that
apply and revert it, and the `--intent-to-add` the two new files need in order to be counted.

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
