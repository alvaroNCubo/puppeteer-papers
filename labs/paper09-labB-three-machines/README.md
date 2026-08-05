# Paper 9 — Lab B: three machines

One domain runs as three StageManager peers in three Docker containers on a private bridge
network — one Director, two casts, joined over Kestrel TLS on a port never exposed to the host, so
the coordination and replication between them is genuine container-to-container TLS. A scripted
driver on the Director plays the game to a non-trivial board and that play replicates to the casts.

Headline → §2 (Experiment A) and Appendix A (Lab B). The claim is two zeros and one convergence:
**0 domain edits and 0 actor edits**, and the three nodes reaching a **byte-identical** board.

What is *not* claimed: any test of resilience. No peer was killed and no partition induced, so
partial failure is untouched — the paper says so at the one row of its Waldo table marked as not
addressed.


## How many consoles, and which of them block

Docker Desktop must be running, and `$env:PuppeteerEngine` must be set for the publish step. **Nobody
plays anything in this lab** — `tetris-a` is the Director and plays a short scripted sequence itself,
so there is no input console.

**One console is enough.** `run-demo.sh` goes quiet while it polls, then prints the convergence line
each node logged and returns, leaving the containers up. Those three lines are the result. Everything
below is for watching it happen, which is optional.

**Before opening anything: every `logs -f` takes its console and does not give it back until Ctrl+C.**
Drop the `-f` and the same command prints what exists and returns. Four blocked consoles is what
happens if you open every command in this file at once, and none of them needed to block.

| Consoles | Run this | Blocks? |
|---|---|---|
| **1** | `& "C:\Program Files\Git\bin\bash.exe" docker/run-demo.sh` | until convergence, **then frees itself** |
| **2** (optional) | `docker compose -f docker/docker-compose.yml logs -f` | yes, until Ctrl+C — all three nodes interleaved |
| **2 and 3** *instead of* the above | `… logs -f tetris-a` and `… logs -f tetris-b` | yes, both — the **Director** beside a **cast**, one acting and the other arriving at the same board without acting |

Following all three interleaved and following two separately are two ways of watching one run, so
**rows 2 and 3 replace row 2, they do not add to it.** You never need a fourth console: console 1 frees
itself once convergence happens, and every check below runs there.

Any console must be in this lab's directory, since `-f docker/docker-compose.yml` is relative. Only
console 1 needs `$env:PuppeteerEngine`; that variable is for building.

## The check, and where the output lands

Each node keeps its own journal in its own volume — `tetris-a-data`, `tetris-b-data`, `tetris-c-data`,
mounted at `/data`. **The byte-identity of those journals is the result.** In console 1, which is free:

```powershell
$env:MSYS_NO_PATHCONV=1
```

```powershell
foreach ($id in 'a','b','c') { docker compose -f docker/docker-compose.yml exec -T "tetris-$id" md5sum /data/tetris/journal/journal_000001.bin }
```

Three identical hashes. The same holds for `/data/tetris/meta.bin`.

The convergence line each node logs carries the same fact in readable form, and is what `run-demo.sh`
prints:

```
tetris-a: convergence checkpoint reached: role=DIRECTOR entry=13 snapshot=type=- cleared=0 awaiting=True over=False cells=8
tetris-b: convergence checkpoint reached: role=cast     entry=13 ... cells=8
tetris-c: convergence checkpoint reached: role=cast     entry=13 ... cells=8
```

Same journal entry, same cell count, same board, on three machines. To keep the journals as files
rather than trust three hashes:

```powershell
foreach ($id in 'a','b','c') { docker compose -f docker/docker-compose.yml exec -T "tetris-$id" cat /data/tetris/journal/journal_000001.bin > "labB-$id.bin" }
```

Then compare them on the host — `fc /b labB-a.bin labB-b.bin` should report no differences.

Tear down when finished:

```powershell
& "C:\Program Files\Git\bin\bash.exe" docker/run-demo.sh --down
```

## See the board — the part that makes this a Tetris lab

Everything above is logs and hashes. To actually *see* the Well that was replicated, copy one node's
journal out and render it with a host that knows nothing about Docker. From this lab's directory:

```powershell
$env:MSYS_NO_PATHCONV=1
```

```powershell
docker cp tetris-a:/data/tetris ..\paper09-example\.sessions
odeA
odeA
```

```powershell
cd ..\paper09-example
```

```powershell
dotnet run --project ai\TetrisAi.csproj -- nodeA view
```

You get the board:

```
|    [][]  []        |
|    [][]  [][][]    |
+====================+
META type=- cleared=0 awaiting=True over=False active=[]
```

Eight cells, `awaiting=True`, `cleared=0` — the same figures the convergence line reported as
`cells=8 awaiting=True`. Repeat with `tetris-b` and `tetris-c` into `nodeB` and `nodeC` and you get the
same board three times, drawn from three separate machines' records.

**This shows more than the hash does.** The three hashes prove the files are identical; this proves the
files are *ordinary journals*. A host that has never heard of containers, TLS or replication reads one
and reconstructs the board — the same `TetrisAi` used in Lab D, with no special tooling and nothing
exported. The node ran in a container; its record is just a record.

The extra `nodeA
odeA` in the copy target is the session layout the hosts expect —
`.sessions\<session>\<session>\journal\`. And `MSYS_NO_PATHCONV` keeps Git Bash from rewriting the
container's leading-slash path if you are in a bash shell; in PowerShell it is harmless.

Clean up afterwards:

```powershell
Remove-Item -Recurse -Force ..\paper09-example\.sessions
ode*
```

## Headline, and what is not claimed

**→ §2 (Experiment A) and Appendix A (Lab B): 0 domain edits and 0 actor edits**, and the three nodes
reaching a byte-identical board. Adding this staging left the diff of both the domain and the actor
directories empty.

**Not claimed: any test of resilience.** No peer is killed and no partition induced, so partial failure
is untouched — which the paper says at the one row of its Waldo table marked *not addressed*. If you
want to press the arrangement where it is weakest, killing `tetris-b` mid-run is the experiment this
lab deliberately does not perform.

## Contents

`docker/` as it stood on branch `p9/labg-rerun` of the examples repository. The write-up and the
captured run are in `data/paper09-labB-three-machines/`.
