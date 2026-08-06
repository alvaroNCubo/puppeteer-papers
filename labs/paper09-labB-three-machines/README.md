# Paper 9 — Lab B: three machines

One domain runs as three peers in three Docker containers, joined over container-to-container TLS. The
Director plays a scripted game; the two casts receive it.

Headline → §2, §7, §9 (Table 1) and Appendix A (Lab B). **0 domain edits, 0 actor edits, and three
byte-identical journals.**

## What this lab is for, and it does two opposite jobs

**It extends §2's axis to a machine boundary, and it is the only lab that holds the *actor* still too.**
Every other staging lab measures an empty diff over the domain; here the diff over the actor directory is
empty as well, which is what makes "the host is an accidental shell" a measurement rather than a reading.

**And it is the lab that forced a claim to weaken.** §7 wanted to say the account of what happened is in
one place. Three containers keep three journals, converging by replication and a catch-up path, so the
narrative is in three places and reading it does depend on a mechanism — this lab contradicts the strong
form. What survives, and what §7 now says, is *one logical narrative, replicated, with convergence the
framework guarantees*: the three are copies **of one record**, each alone holding the whole game, so a
reader at any node joins nothing to anything. That is weaker than "one place" and it is what the labs
support. A lab that narrows the paper is worth more than one that flatters it.

Two rows of §9's Waldo table are also this lab's, and they point in opposite directions: **concurrency**
is present and its connect-readiness race is reported, and **partial failure is *not addressed*** — see
the end of this file.

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
tetris-a: convergence checkpoint reached: role=DIRECTOR entry=20 ... cells=8
tetris-b: convergence checkpoint reached: role=cast     entry=20 ... cells=8
tetris-c: convergence checkpoint reached: role=cast     entry=20 ... cells=8
```

Same entry, same cell count, on three machines. **That is the result.**

**#2** — the Director: promotion, the TLS connections to its peers, the scripted game, its checkpoint.

**#3** — a cast: it plays nothing and receives everything, and arrives at the same board.

Consoles #2 and #3 stay blocked until you press **Ctrl+C**. That is normal; they are log followers.

## Then, in console #4

**See the board** — the part that makes this a Tetris lab. Copy one node's journal out and render it
with a host that knows nothing about Docker:

`docker cp` will not create the path for you, so make it first:

```powershell
mkdir ..\paper09-example\.sessions\nodeA -Force
```

```powershell
docker cp tetris-a:/data/tetris ..\paper09-example\.sessions\nodeA\nodeA
```

```powershell
dotnet run --project ..\paper09-example\ai\TetrisAi.csproj -- nodeA view
```

One run printed this. Yours will print a board of its own — see the note below on which parts of it
are fixed and which are not:

```
|      []            |
|      []      []    |
|      [][][][][]    |
+====================+
META type=- cleared=0 awaiting=True over=False active=[]
```

**Two pieces, and only two, on purpose.** The Director plays a fixed twelve-act sequence
(`sm-cluster/Program.cs`, `PlayScriptedSequence`):

```
SpawnNext  MoveLeft  Rotate  Tick  Tick  Drop      piece 1 lands
SpawnNext  MoveRight MoveRight      Drop           piece 2 lands
```

**What is fixed, and what is not.** The acts are fixed; the *pieces* are not. `SpawnNext` asks the
domain which tetromino comes next and the domain answers from a transient source that is never
journaled, so each run lands two different shapes. So `cells=8` holds every time — two tetrominoes are
eight cells whichever two they are — and `awaiting=True` holds, and **the three nodes agree with each
other**, which is the claim. The arrangement of those eight cells, and therefore the hash, belong to
the run you just made. That is why the check below asks you to compare your three hashes against each
other and never against a number printed here.

Twelve acts plus the seeding `upgrade` are thirteen invocations, and each distinct act also writes its
template once — seven of them, the seed and the six verbs — which is the `entry=20` console #1
reported. The sequence is kept short because this lab measures *three machines reaching the same
record*, not playing Tetris: enough acts for replication to have work to do, few enough that the run
finishes fast and the journals compare byte for byte every time. Lengthen it and the three will still
agree.

The name appears twice because a session is `.sessions\<name>\<name>\journal`. And no `cd` is
needed for the render: the host finds `.sessions` by walking up from its own executable, not from where
you are standing.

Eight cells and `awaiting=True` — the figures console #1 reported. Repeat with `tetris-b` into `nodeB`
for the same board from another machine's record.

**Check the byte-identity** — the claim itself:

```powershell
foreach ($id in 'a','b','c') { docker compose -f docker/docker-compose.yml exec -T "tetris-$id" md5sum /data/tetris/journal/journal_000001.bin }
```

Three identical hashes. Compare them against **each other**, never against a number printed anywhere:
the pieces differ per run, so the hash is this run's.

**And each node's own frame**, which `run-demo.sh` now prints for all three:

```powershell
foreach ($id in 'a','b','c') { docker compose -f docker/docker-compose.yml exec -T "tetris-$id" cat "/data/tetris-$id.frame" }
```

Three identical documents. This is a different fact from the hashes and worth separating: the journals
being equal says the three received the same record, while the frames being equal says **each node
painted the board itself, from the state it holds** — the cast nodes played nothing and asked nobody.
The projection is pushed by a reaction that runs on the node it belongs to, so there are three
independent paintings that agree, not one copied twice.

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
