# Paper 9 — Lab A: five stagings

The same `Well` runs on a console; in a browser with input and output over a WebSocket; in a browser
over HTTP with server-sent events; and across two StageManager actors joined once in memory and once
over a real Kestrel TLS channel. Five stagings differing in process, transport and wire format.

## There is nothing to run, and that is the point

This lab's evidence is an **absence**: five stagings were added to the example one at a time and the
domain directory never changed. So what a reviewer checks is a diff, not a program.

**When:** any time. There is no session to create, no process to start, and no other lab to run
first — the command reads history and touches nothing. **Where:** any clone or worktree of the
examples repository.

The commit that matters is `fd8d94b`, *"add TetrisActor facade over PerformanceV2"* — the **last
commit that touched the domain at all**. Every staging came after it.

| # | Run this | What you see | Who operates it |
|---|---|---|---|
| 1 | `git diff --stat fd8d94b..HEAD -- Tetris/domain/` | **Nothing.** No files, no insertions, no deletions. That empty output *is* the result. | You. It reads history and changes nothing. |

The endpoint is deliberately `HEAD` rather than a pinned commit, and that is the stronger claim: since
`fd8d94b` was the last commit to touch the domain, the diff is empty against **any** later commit, so
which one you pick cannot matter. The write-up verified it at `485b766`; the paper's provenance pins
`4b473ea`; either works, as does today's tip.

Keep the output rather than trusting the screen — an empty file is a stronger artifact than an empty
terminal, because it can be attached:

```powershell
git diff --stat fd8d94b..HEAD -- Tetris/domain/ | Tee-Object -FilePath labA-domain-diff.txt
```

**Output on disk:** `labA-domain-diff.txt`, which should be zero bytes.

To satisfy yourself that `fd8d94b` really is the last domain touch — the one thing in this lab worth
double-checking, since everything rests on it:

```powershell
git log --oneline -1 -- Tetris/domain/
```

That should print `fd8d94b`. If it prints something later, the domain has been touched since and this
lab's claim needs re-measuring rather than re-reading.

If you also want to see the five stagings run, they are hosts of the example — `console/`, `web/`,
`web-rest/`, `sm-duo/`, `sm-duo-tls/` — one console each, in any order, with no sequence between them.
That is a demonstration, not the check.

