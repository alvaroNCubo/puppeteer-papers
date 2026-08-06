# Paper 9 — Lab H: one routine, recognized on three stagings

## What this settles, and it is one step in one argument

§6 argues a chain: the domain keeps its identity across stagings, therefore its *acts* are the same on
every stage, therefore **the routine those acts compose is recognizable from any of them**. The first two
links are Labs A through E. This lab measures the third, and only the third.

The measurement takes a **pair** of results, and the paper's sentence is the one to read the lab against:

> what held still across the three stagings was not the record's representation, which shifted, but what
> the act was and what performing it left behind.

So there are two things to look for here, not one:

| | | |
|---|---|---|
| **the acts held still** | two placements, same order, every staging | the routine is recognizable |
| **the bookkeeping moved** | closing entry ids differ by a constant | which is a fact about *where* the domain ran |

Neither half is the interesting one alone. Together they say what a staging is: it changes who is present
when a domain acts, and changes nothing about what the acting does. If both had held still the lab would
prove less — it would be consistent with the stagings being the same thing under different names.

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

`play` drives the same `TetrisActor.Persistent` host the warm server uses, so the journal is what a real
staging records rather than one written by hand. The acts are the sequence the containers play:
`spawn, left, rotate, tick, tick, drop, spawn, right, right, drop`.

### 3 — Recognize the routine in that record

```powershell
dotnet run --project recognize\TetrisRecognize.csproj --no-build -- read out\s1 labH
```

```
ROUTINE 'placement of one piece'  Spawn($type) -> Drop()   [2 recognized]
  #1  type=O  closes at entry 13
  #2  type=L  closes at entry 18
CONTROL 'spawn to next spawn'     Spawn($type) -> Spawn(_) [1 recognized]
SIGNATURE placements=2 closes=[13,18] pieces=[type=O,type=L]
```

**`placements=2`, in order, each opened by a `Spawn` and closed by the `Drop` that ends that piece's
descent.** That is the left column of the table above. The piece letters vary run to run because the
domain chooses them. The `CONTROL` line is a rival pattern, there to be *rejected*: it matches the gap
between two spawns, which is not a placement.

## The other half: what moved

The same routine was recognized on the two stagings you cannot re-run here — a warm server driven over a
named pipe, and the three containers, read **in-container** on each node against its own live volume
(`docker exec tetris-<id> dotnet /recognize/TetrisRecognize.dll read /data tetris`, which needs this
project inside Lab B's image). The captured evidence is
`../../data/paper09-labH-recognition/recognition-across-stagings.log`.

| staging | acts | placements | closing entries |
|---|---|---|---|
| single process | 10 | **2** | 12, 17 |
| warm server over a pipe | 11 | **2** | 12, 17 |
| container `tetris-a`, `-b`, `-c` | 10 each | **2** each | **14, 19** each |
| *same, gravity control* | 25 | *2 — and wrong, see below* | 32, 32 |

**+2 on the containers**, because the cluster writes three idempotent seeding acts where one process
writes one, and everything after shifts. And your step 3 above closes at **13 and 18** rather than 12 and
17 — a third displacement, from a fourth direction: every command in this example now journals as an
ActorV2 Action, which writes one template row per distinct act. Representation moved again; the two
placements did not. The lab's central pair, replicated by accident.

Within the container staging the three records are **byte-identical** — the same 64 hex digits on the
Director and both casts — so that row is one record replicated over TLS and read three times, not three
readings that happen to agree. Do not read that sentence as the row above it: byte-identity holds *within*
one staging, and *across* stagings what matches is the acts while the identifiers do not.

## What the lab bounds, which §6 is narrower for

- **There is no handle to correlate on.** A spawn names a piece type; a drop names nothing. The
  trajectory between them is tied by order alone, not by any identity the acts carry.
- **Entry ids are not staging-invariant** — the table — and the closing identifier is the only handle a
  match hands its reader.
- **A reading can be wrong, and silently.** This is the one that answers a question §6 leaves standing.
  Landing is not an act: the board lands a piece privately, inside a tick or a drop. So a piece that comes
  to rest under gravity leaves its opening unclosed and the *next* piece's drop closes it — two
  placements matched at one closing entry, count right, correlation wrong, nothing to signal it. That is
  the gravity control row, and why it is a control rather than a comparison.

And the scope: the trajectory is **two placements long**. This is an existence result about
recognizability, not a measurement of it at scale.

## Contents

    recognize\    the reaction and the two verbs that run it — 349 lines, buildable here

`data/paper09-labH-recognition/` holds the write-up and the captured log: the full argument, the
per-staging detail, and five sub-findings this file does not repeat. The log carries the author's absolute
paths deliberately — it is a record of a run, and rewriting them would make it tidier and less true.
