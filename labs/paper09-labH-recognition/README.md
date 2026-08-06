# Paper 9 — Lab H: one routine, recognized on three stagings

One reaction is defined outside the domain: it seeks a spawn, then the drop that ends the piece's
descent, and what it matches between them is a placement. It is then run against three stagings — the
journal of a single process, the journal a warm server keeps while driven over a named pipe, and the
journals of the three containers, read inside each node against its own store.

Headline → §6 and Appendix A (Lab H). **The same two placements, in the same order, on every staging**,
with an empty domain diff throughout.

## The routine, which is the whole artifact

Nothing about it is in the domain. It names two of the `Well`'s verbs and nothing else:

```csharp
reactions.DefineReaction("Placement")
    .Job().Company().WithSharedHydration()
    .Seek("Spawn").One()
        .OnMatch("[_:Well].Spawn($type)")
    .ThenSeek("Land").One()
        .OnMatch("[_:Well].Drop()")
    .Program.Emit("print @type 'piece';");
```

## Three steps, one console

Set `$env:PuppeteerEngine` as every lab in this suite does, and `cd` here.

### 1 — Build the recognizer

```powershell
dotnet build recognize\TetrisRecognize.csproj
```

Run it with no arguments and it prints its own two verbs, `play` and `read`.

### 2 — Record a game through the ordinary staging

```powershell
dotnet run --project recognize\TetrisRecognize.csproj --no-build -- play out\s1 labH
```

```
[play] session 'labH' journal '…\out\s1' cleared=0 awaiting=True over=False cells=8
```

`play` drives the same `TetrisActor.Persistent` host the warm server uses, so the journal is what a real
staging records rather than one written by hand. The act sequence is the same one the containers play:
`spawn, left, rotate, tick, tick, drop, spawn, right, right, drop`.

### 3 — Recognize the routine in that record

```powershell
dotnet run --project recognize\TetrisRecognize.csproj --no-build -- read out\s1 labH
```

```
ROUTINE 'placement of one piece'  Spawn($type) -> Drop()   [2 recognized]
  #1  type=S  closes at entry 13
  #2  type=Z  closes at entry 18
CONTROL 'spawn to next spawn'     Spawn($type) -> Spawn(_) [1 recognized]
  #1  type=S  closes at entry 14
SIGNATURE placements=2 closes=[13,18] pieces=[type=S,type=Z]
```

**Two placements, in order, each opened by a `Spawn` and closed by the `Drop` that ends that piece's
descent.** The piece letters differ from run to run because the domain chooses them; the count and the
order are the claim. The `CONTROL` line is the fallback pattern and is there to be *rejected* — it
recognizes the gap between spawns, not a placement, and §5.2 of the write-up says why.

## The two stagings you cannot re-run here, and what stands in for them

The warm-server staging and the three containers were recognized the same way, and the captured run is
the evidence: `../../data/paper09-labH-recognition/recognition-across-stagings.log`. The container
readings in particular ran **in-container** on each node, against that node's own volume while its Stage
was live — `docker exec tetris-<id> dotnet /recognize/TetrisRecognize.dll read /data tetris` — which
needs the recognizer inside the image and so is not one of the three steps above. Reproducing it means
adding this project to Lab B's Dockerfile.

What those runs found, in one table — six recognitions over four records:

| staging | acts | placements | closing entries |
|---|---|---|---|
| single process | 10 | **2** | 12, 17 |
| same, gravity control | 25 | 2 *(wrong — see below)* | 32, 32 |
| warm server over a pipe | 11 | **2** | 12, 17 |
| container `tetris-a`, `-b`, `-c` | 10 each | **2** each | 14, 19 each |

Each container's `journal_000001.bin` hashed to the same 64 hex digits, so this is one record replicated
over TLS and read three times, not three readings that happen to agree.

**The distinction to hold on to.** Within the container staging the three records are **byte-identical**.
*Across* stagings the acts match **verb for verb** — same count, order and shape — while the **entry
identifiers differ by a constant**, because the cluster writes three idempotent seeding acts where one
process writes one. What held still is the acts; what moved is the bookkeeping. Do not read the first
sentence as the second.

Step 3 above closes at 13 and 18 rather than the table's 12 and 17, for the same kind of reason: every
command in this example now journals as an ActorV2 Action, which writes a template row per distinct act.
That shifts ids and leaves the placements alone — which is the finding, again, from a direction nobody
was aiming at.

## Three findings narrower than the confirmation

All three are argued in the write-up, and the third is the one that matters most:

- **there is no handle to correlate on** — a spawn names a piece type, a drop names nothing;
- **entry ids are not staging-invariant**, as the table shows;
- **a reading can be wrong, silently.** Landing is not an act, so a piece coming to rest under gravity
  leaves its opening unclosed and the *next* piece's drop closes it. The count comes out right and the
  correlation comes out wrong — which is the gravity control row above, and why it is a control.

## Contents

    recognize\    the reaction and the two verbs that run it — 349 lines, buildable here

The write-up and its captured log are in `data/paper09-labH-recognition/`: the full argument, the
per-staging detail, §5's five sub-findings, and the log as evidence the comparison was performed rather
than asserted. That log carries the author's absolute paths deliberately — it is a record of a run, and
rewriting them would make it tidier and less true.
