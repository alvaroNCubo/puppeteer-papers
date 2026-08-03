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

Any clone or worktree of the examples repository, on `main`. **Any time, as often as you like** —
these commands read history and touch nothing, so there is no session to create, nothing to clean up,
and no other lab to run first.

## Run it between the other labs, too

Used that way it is the suite's standing check that the domain has not moved, and its value is that it
is *not* always silent:

- on `main` or any staging branch, the domain's date stays put;
- on **Lab G**'s tree the domain's last change is Lab G's own commit, because a re-decomposition
  changes the domain on purpose — `Well.cs` should still be absent from it;
- on **Lab I**'s tree likewise, and that lab's README gives the exact size of the change.

A moved date is information, not a failure: it says the only thing which changed the domain was the
lab that says it changed it.

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
