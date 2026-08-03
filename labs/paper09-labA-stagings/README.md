# Paper 9 — Lab A: five stagings

The same `Well` runs on a console; in a browser with input and output over a WebSocket; in a browser
over HTTP with server-sent events; and across two StageManager actors joined once in memory and once
over a real Kestrel TLS channel. Five stagings differing in process, transport and wire format.

## What this lab proves

**That the domain reached its final form before four of its five stagings existed.**

Not "the domain looks decoupled", and not "no edits were needed" as an impression — a dated fact
about the repository. The last commit that touched the domain is `fd8d94b` (30 June 2026, *"add
TetrisActor facade over PerformanceV2"*). Four of the five stagings were first added **after** it:

| Staging | First added | Relative to the domain's last commit |
|---|---|---|
| console | `a32b57c`, 29 Jun | contemporaneous — it came with the example itself |
| browser over WebSocket | `6868249`, 30 Jun | **after** |
| browser over server-sent events | `f093e20`, 1 Jul | **after** |
| StageManager in memory | `e311e58`, 30 Jun | **after** |
| StageManager over TLS | `68635c3`, 30 Jun | **after** |

So the domain was finished, and then a WebSocket host, an SSE host and two StageManager hosts were
built against it without it being touched again. That is `Identity Precedes Staging` as a chronology
rather than as a reading.

## This lab is also the suite's measurement mechanism

It has two uses, and the second is the more useful one.

**Used once, on `main`, it is the chronology above.** Two commands, and neither is worth anything
alone.

**Used repeatedly, anywhere, it is the standing check that the domain has not moved.** Run it whenever
you like — between labs, during one, before and after. It reads history and touches nothing, so it
cannot disturb anything it measures. **The point is not that it always reports nothing.** The point is
that it reports nothing everywhere *except* the two labs that change the domain on purpose, and there
it reports a known amount:

| Where you run it | What it should report |
|---|---|
| `main`, or any staging branch | **empty** — no files, no insertions, no deletions |
| Lab G's tree (re-decomposition) | **+643 −4** over five files, of which 294 added lines are code. `Well.cs` **must not appear** |
| Lab I's tree (domain growth) | **+98 −3** over three files, of which 30 added lines are code |

Read that table as the mechanism's calibration. An empty diff on `main` says the stagings cost the
domain nothing. A diff of exactly +98 −3 on Lab I's tree says the *only* thing that changed the domain
was the thing the paper says changed it — which is a stronger statement than either alone. And if
`Well.cs` ever appears in Lab G's list, the claim that a re-cut leaves the original rules untouched has
failed.

## It takes two commands, and one alone proves nothing

**When:** any time, as often as you like. There is no session to create, no process to start, and no
other lab to run first. **Where:** any clone or worktree of the examples repository — and note which
one, because the expected result depends on it, per the table above.

| # | Run this | What you see on `main` | What it establishes |
|---|---|---|---|
| 1 | `git log --format="%h %ad" --date=short --diff-filter=A -1 -- Tetris/web Tetris/web-rest Tetris/sm-duo Tetris/sm-duo-tls` | The dates those four hosts **first appeared**. | That the stagings came *after*. Without this, step 2 is trivially true. |
| 2 | `git diff --stat fd8d94b..HEAD -- Tetris/domain/` | **Nothing.** | That the domain did not move while they arrived. Without step 1, this only says the domain has not changed lately. |

Step 2 alone is tautological: `fd8d94b` *is* the last commit that touched the domain, so of course the
diff after it is empty. It becomes evidence only once step 1 has shown that four stagings were built in
that same span. The conjunction is the result; neither half is.

Confirm the premise the whole thing rests on:

```powershell
git log --oneline -1 -- Tetris/domain/
```

That should print `fd8d94b` on `main`. If it prints something later, the domain has been touched since
and this lab needs re-measuring rather than re-reading. On Lab G's or Lab I's tree it will print their
commit instead, which is correct and expected.

**Output on disk:**

```powershell
Start-Transcript -Path labA-chronology.log
Stop-Transcript
```

The endpoint of step 2 is `HEAD` on purpose. Since `fd8d94b` was the last domain commit on `main`, the
diff is empty against **any** later commit there, so which one is chosen cannot matter.

If you also want to watch the five stagings run, they are hosts of the example — one console each, in
any order, no sequence between them. That is a demonstration, not the check.

