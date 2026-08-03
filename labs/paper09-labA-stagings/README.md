# Paper 9 — Lab A: five stagings

The same `Well` runs on a console; in a browser with input and output over a WebSocket; in a browser
over HTTP with server-sent events; and across two StageManager actors joined once in memory and once
over a real Kestrel TLS channel. Five stagings differing in process, transport and wire format.

## There is nothing to run, and that is the point

This lab's evidence is an **absence**: five stagings were added to the example one at a time and the
domain directory never changed. So what a reviewer checks is a diff, not a program.

**Order:** one command, in one console.

| # | Run this | What you see | Who operates it |
|---|---|---|---|
| 1 | `git diff --stat <first-staging-commit> <last-staging-commit> -- Tetris/domain/` | **Nothing.** No files listed, no insertions, no deletions. That empty output *is* the result. | You. It reads history; it changes nothing. |

The commit range is in the write-up. Keep the output rather than trusting the screen:

```powershell
git diff --stat <first> <last> -- Tetris/domain/ | Tee-Object -FilePath labA-domain-diff.txt
```

**Output on disk:** `labA-domain-diff.txt`, which should be empty. An empty file is the artifact.

If you also want to see the five stagings run, they are hosts of the example — `console/`, `web/`,
`web-rest/`, `sm-duo/`, `sm-duo-tls/` — one console each, in any order, no sequence between them.
That is a demonstration, not the check.

