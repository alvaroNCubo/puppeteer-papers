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

## It takes two commands, and one alone proves nothing

This is worth being exact about, because either half on its own is worthless.

**When:** any time. There is no session to create, no process to start, and no other lab to run
first — both commands read history and touch nothing. **Where:** any clone or worktree of the
examples repository, on `main`.

| # | Run this | What you see | What it establishes |
|---|---|---|---|
| 1 | `git log --format="%h %ad" --date=short --diff-filter=A -1 -- Tetris/web Tetris/web-rest Tetris/sm-duo Tetris/sm-duo-tls` | The dates those four hosts **first appeared**. | That the stagings came *after*. Without this, step 2 is trivially true. |
| 2 | `git diff --stat fd8d94b..HEAD -- Tetris/domain/` | **Nothing.** No files, no insertions, no deletions. | That the domain did not move while they arrived. Without step 1, this only says the domain has not changed lately. |

Step 2 alone is tautological: `fd8d94b` *is* the last commit that touched the domain, so of course the
diff after it is empty. It becomes evidence only once step 1 has shown that four stagings were built
in that same span. The conjunction is the result; neither half is.

Confirm the premise the whole thing rests on:

```powershell
git log --oneline -1 -- Tetris/domain/
```

That should print `fd8d94b`. If it prints something later, the domain has been touched since and this
lab needs re-measuring rather than re-reading.

**Output on disk:**

```powershell
Start-Transcript -Path labA-chronology.log
Stop-Transcript
```

`labA-chronology.log` then holds four dates and one empty diff — which is the whole lab.

The endpoint of step 2 is `HEAD` on purpose. Since `fd8d94b` was the last domain commit, the diff is
empty against **any** later commit, so which one is chosen cannot matter. The write-up verified it at
`485b766`; the paper pins `4b473ea`; today's tip works too.

If you also want to watch the five stagings run, they are hosts of the example — one console each, in
any order, no sequence between them. That is a demonstration, not the check.

