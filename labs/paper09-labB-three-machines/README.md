# Paper 9 — Lab B: three machines

One domain runs as three peers in three Docker containers, joined over container-to-container TLS. The
Director plays a scripted game; the two casts receive it. **The claim: 0 domain edits, 0 actor edits,
and three byte-identical journals.**

Needs Docker Desktop running, and `$env:PuppeteerEngine` pointing at a Puppeteer checkout.

## Four consoles, in this order

Open four PowerShell windows and `cd` all of them here:

```powershell
cd <this directory>
```

Then type the command into each, and press Enter **in this order**:

| | Console | Command |
|---|---|---|
| **1st** | #1 | `& "C:\Program Files\Git\bin\bash.exe" docker/run-demo.sh` |
| **2nd** | #2 | `docker compose -f docker/docker-compose.yml logs -f tetris-a` |
| **3rd** | #3 | `docker compose -f docker/docker-compose.yml logs -f tetris-b` |
| **4th** | #4 | *nothing yet — leave it free for the checks below* |

Console #1 must go first: it is what builds the image and starts the containers, so #2 and #3 have
nothing to attach to until it has.

## What you will see in each

**#1** — publish, image build, three containers starting. Then it **goes quiet and stays quiet** while
it polls; it looks stalled and is not. When all three have converged it prints three lines and returns:

```
tetris-a: convergence checkpoint reached: role=DIRECTOR entry=13 ... cells=8
tetris-b: convergence checkpoint reached: role=cast     entry=13 ... cells=8
tetris-c: convergence checkpoint reached: role=cast     entry=13 ... cells=8
```

Same entry, same cell count, on three machines. **That is the result.**

**#2** — the Director: promotion, the TLS connections to its peers, the scripted game, its checkpoint.

**#3** — a cast: it plays nothing and receives everything, and arrives at the same board.

Consoles #2 and #3 stay blocked until you press **Ctrl+C**. That is normal; they are log followers.

## Then, in console #4

**See the board** — the part that makes this a Tetris lab. Copy one node's journal out and render it
with a host that knows nothing about Docker:

```powershell
docker cp tetris-a:/data/tetris ..\paper09-example\.sessions\nodeA\nodeA
```

```powershell
dotnet run --project ..\paper09-example\ai\TetrisAi.csproj -- nodeA view
```

```
|      []            |
|      [][][][]      |
|        [][][]      |
+====================+
META type=- cleared=0 awaiting=True over=False active=[]
```

Eight cells and `awaiting=True` — the figures console #1 reported. Repeat with `tetris-b` into `nodeB`
for the same board from another machine's record.

**Check the byte-identity** — the claim itself:

```powershell
foreach ($id in 'a','b','c') { docker compose -f docker/docker-compose.yml exec -T "tetris-$id" md5sum /data/tetris/journal/journal_000001.bin }
```

Three identical hashes.

**Tear down:**

```powershell
& "C:\Program Files\Git\bin\bash.exe" docker/run-demo.sh --down
```

```powershell
Remove-Item -Recurse -Force ..\paper09-example\.sessions\node*
```

## Two things worth knowing

**What is not claimed: any test of resilience.** No peer is killed and no partition induced, so partial
failure is untouched — the row of the paper's Waldo table marked *not addressed*. Killing `tetris-b`
mid-run is the experiment this lab deliberately does not perform.

**Watching a node live** is possible but off by default, because the named volumes the paper measured
in hide the journals from the host. `docker/docker-compose.observe.yml` bind-mounts them instead and
explains itself in its header; add it with a second `-f` and each node's journal appears under
`docker/data/`.

## Contents

`docker/` — compose file, the observe override, Dockerfile, `run-demo.sh` — and `sm-cluster/`, the
cluster host `run-demo.sh` publishes. Both from branch `p9/labg-rerun` of the examples repository;
`sm-cluster` was never on its `main`, which is why it lives here and not in the vendored example. Its
actor reference points at `labs/paper09-example`, so building it needs `$env:PuppeteerEngine`.

Write-up and captured run: `data/paper09-labB-three-machines/`.
