# Experiment B — change the AUDIENCE, the play does not change

*Evidence note for Paper 9 ("One Play, Many Stages"), Experiment B. Verified
against code on 2026-07-22 in the Tetris lab. Every claim below carries a
`file:line` a reader can open; every number is measured, not asserted.*

> **The canonical phrase, Experiment-B half:** *the audience changes — who
> receives the obra, and through what instrument — and the play does not.*
> The measure is **zero domain change**. The zeros are the claim; everything
> else is mechanism.

Companion: the first-person account [`notes/mirilla-and-tetris.md`](mirilla-and-tetris.md)
("the eye that reads it as a skyline, not a bitmap"). *(Provenance note: that
file was committed on branch `tetris-example` at `bf8d45a`; it is not on this
branch's history. This note lives beside it.)*

---

## 0. The setup in one line

One **obra** — the clean `Well` domain (`TetrisDomain`, no infrastructure, no
references: [`Tetris/domain/TetrisDomain.csproj`](../domain/TetrisDomain.csproj))
— is received by **multiple distinct audiences, each through its own instrument
(mirilla)**: four in-process (.NET / PowerShell) plus the **browser
(JavaScript)** over two real network transports. The domain is byte-for-byte
identical across all of them.

| # | Audience | Instrument (mirilla) | Reaches the domain via | Projection is chosen by |
|---|----------|----------------------|------------------------|-------------------------|
| 1 | Human    | keyboard + ASCII `[]` grid | `TetrisActor` (in-mem) | `BoardRenderer` (the crew) |
| 2 | AI       | pipe + **skyline vector** | `TetrisSend`→`TetrisServer` (warm) / `TetrisAi` (cold) | `pile-scan.ps1` (the crew) |
| 3 | Observer | poll-pull "narrator" grid | `TetrisActor` (re-open per poll) | `BoardRenderer` (the crew) |
| 4 | Watch    | **push receiver** of the raw frame | *frame file only — never the domain* | `BoardRenderer` (the crew) |
| 5 | Browser player | keydown + **JS-rendered grid** | WebSocket / SSE → `TetrisActor` verb | `render()` in the page (the crew) |
| 6 | Browser observer | JS grid of **all** live games | WebSocket / SSE → *frames only* | `render()` in the page (the crew) |

Rows 1–4 are co-located (one machine, .NET/PS, local IPC); rows 5–6 cross a real
network to a **different runtime** (§3b). Not one of these projections lives in
the obra. The obra emits **facts**; each audience projects them through its own
instrument, chosen by the crew.

---

## 1. The AI path drives the SAME domain — zero domain change

**Same domain assembly, reached the same way as the human console.** Every host
builds its actor over `typeof(TetrisDomain).Assembly` by reflection — there is no
second Tetris:

- [`Tetris/actor/TetrisActor.cs:106`](../actor/TetrisActor.cs) — `new PerformanceV2(actorName, typeof(TetrisDomain).Assembly)`.
- The verbs are DSL commands against `well`: [`Tetris/actor/TetrisActor.cs:167-193`](../actor/TetrisActor.cs) (`Spawn`, `MoveLeft/Right`, `Rotate`, `Tick`, `Drop`) — the exact public verbs of [`Tetris/domain/Well.cs:116-222`](../domain/Well.cs).

**Two AI wirings, both the same `Well`:**

- **Cold (v2)** — one process per op, rehydrate-apply-append-exit:
  [`Tetris/ai/Program.cs:45-46`](../ai/Program.cs) → `TetrisActor.Persistent(session, width, height, journalDir, sink)`.
- **Warm (v3)** — a long-lived host + a thin per-command **pipe** client:
  - server: [`Tetris/server/Program.cs:37-38`](../server/Program.cs) → the same `TetrisActor.Persistent(...)`, rehydrated once, kept warm.
  - sender: [`Tetris/send/Program.cs:22-26`](../send/Program.cs) — a named-pipe client that carries **one verb** and exits; *"No game logic, no rehydration, no engine reference"* ([`Tetris/send/Program.cs:4-6`](../send/Program.cs)).

The pipe is the mirilla's **inbound** half (the AI commands through it); the pushed
frame is the **outbound** half (the AI observes through it). Both are transport
around the domain, not the domain.

**MEASUREMENT — the zeros (this IS the claim).** The domain was last modified at
`fd8d94b` ("add TetrisActor facade"). Every audience/instrument that followed is a
descendant of that commit and changed **zero** domain files:

```
git diff --numstat fd8d94b..HEAD -- Tetris/domain   ->  (no output: 0 files, 0 lines)
```

| Commit | What it added | Domain files touched |
|--------|---------------|----------------------|
| `4fb13c3` | AI commander CLI (`ai/`) + observer (`observer/`) | **0** |
| `e06721d` | live PUSH watch (`watch/`) + reaction frame emit | **0** |
| `a3c6ca6` | warm server + thin pipe sender (`server/`, `send/`) | **0** |
| `e311e58` | StageManager host **+ the mirilla `tools/pile-scan.ps1`** | **0** |
| `6868249` | **browser** audience over WebSockets (`web/`) | **0** |
| `f093e20` | **browser** audience over REST + SSE (`web-rest/`) | **0** |

Each verified with `git merge-base --is-ancestor fd8d94b <commit>` → YES, and the
per-commit file lists contain no `Tetris/domain/*` entry. The mirilla itself
(`pile-scan.ps1`, born in `e311e58`) was added with **zero domain change**; so
were both browser audiences (`6868249`, `f093e20` — the commit messages say
"zero domain changes" in their own words).

---

## 2. The mirilla mechanism, precisely

### 2a. What the obra emits: nothing but facts

The `Well` has **no notion of a frame, a projection, a sink, or an observer**. It
exposes a query surface and mutating verbs, and that is all:

- [`Tetris/domain/Well.cs:322-331`](../domain/Well.cs) — `OccupiedInterior()` returns a raw `ImmutableHashSet<Position>` of occupied cells (pile ∪ active, clipped). A **bitmap**, not a skyline.
- scalars only: `IsGameOver`, `IsAwaitingPiece`, `ClearedLines`, `Frame.Width/Height` ([`Tetris/domain/Well.cs:37-85`](../domain/Well.cs)).
- The domain assembly references **nothing** ([`Tetris/domain/TetrisDomain.csproj`](../domain/TetrisDomain.csproj)); the substrate reaches its `internal` types by reflection ([`Tetris/domain/AssemblyInfo.cs:8-9`](../domain/AssemblyInfo.cs)).

### 2b. What the substrate pushes: the raw frame (still not a projection)

A **substrate** reaction — not the domain — assembles the pushed frame by `print`ing
those domain queries. It is a flat bitmap + scalars:

- [`Tetris/actor/TetrisActor.cs:43-47`](../actor/TetrisActor.cs) — `FrameProjection`: `print` of `width, height, cleared, over, awaiting`, an optional `type`, and `foreach (cell in well.OccupiedInterior()) print cell.Row r, cell.Column c`.
- Fired by one Job reaction per mutating verb ([`Tetris/actor/TetrisActor.cs:126-134`](../actor/TetrisActor.cs)), rendered as JSON ([`JsonFormatter`, TetrisActor.cs:125](../actor/TetrisActor.cs)), pushed by [`Tetris/actor/FrameFileSink.cs:32-46`](../actor/FrameFileSink.cs) to `Tetris/.sessions/<session>.frame` ([`SessionPaths.cs:47`](../actor/SessionPaths.cs)).

JSON shape (confirmed against the parser [`FrameDocument.cs:18-20`](../actor/FrameDocument.cs)):
`{"width","height","cleared","over","awaiting","type"?,"cell":[{"r","c"},…]}`.

### 2c. What pile-scan reads and emits: the projection is the CONSUMER's

The mirilla [`Tetris/tools/pile-scan.ps1`](../tools/pile-scan.ps1) is a pure
consumer of that frame file — the domain never hears of it:

- **reads:** the frame's `width`, `height`, `cell[].c/.r` ([`pile-scan.ps1:30-37`](../tools/pile-scan.ps1)) and the state line `type/cleared/over/awaiting` ([`:73`](../tools/pile-scan.ps1)).
- **emits** (all computed *inside the tool*, nowhere in the domain):
  - **skyline** — per-column pile height, run of filled cells up from the floor ([`:39-52`](../tools/pile-scan.ps1));
  - **diffs** — step profile between columns ([`:55`](../tools/pile-scan.ps1));
  - **zeros** — empty columns = fill priority ([`:56`](../tools/pile-scan.ps1));
  - **wells** — local minima + depth, where an I-piece goes ([`:57-65`](../tools/pile-scan.ps1));
  - **metrics** — maxH / aggregate / bumpiness ([`:66-68`](../tools/pile-scan.ps1));
  - **floating / active** — cells above the first gap = the falling piece ([`:48-52`](../tools/pile-scan.ps1), rendered [`:80-85`](../tools/pile-scan.ps1)).

**Two layers of mediation, and NEITHER is in the obra:** (1) the substrate projects
domain *queries* → a raw frame; (2) the mirilla projects that raw frame → the
skyline. The `Well` emits neither; it only answers questions.

### 2d. MEASUREMENT — the mirilla lift, run with the real tool

Feeding `pile-scan.ps1` a frame in the exact emitted shape (a 38-cell bitmap: an
uneven pile with an empty column 5 and a floating **T** piece) produced:

```
== MIRILLA ==
state     : piece=T  cleared=2  over=False  awaiting=False
cols      : 0 1 2 3 4 5 6 7 8 9
skyline   : 3 3 5 4 4 0 2 2 6 5
diffs     :  +0 +2 -1 +0 -4 +2 +0 +4 -1
zeros     : 5   (lowest = fill priority)
wells     : col5(d2) col9(d1)
metrics   : maxH=6  agg=34  bumpiness=14  floating=4
active    : type=T  (1,4) (1,5) (1,6) (2,5)
```

38 flat cells in → a 10-integer skyline + zeros + wells + 3 metrics out, with the
falling T correctly separated from the pile (`floating=4`; the pile heights exclude
it). That is *"the eye that reads it as a skyline, not a bitmap"* — a real
transform, not a claim. *(Input frame synthesized to the verified emit shape,
`actor/TetrisActor.cs:43-47`; the tool run is real and unmodified.)*

---

## 3. Observer and Watch are additional mirillas — no view is privileged

- **Observer** ([`Tetris/observer/Program.cs`](../observer/Program.cs)) — the pull "narrator": a read-only loop that re-opens the same domain and re-renders on change ([`:126`](../observer/Program.cs) `TetrisActor.Persistent(session, …)`; [`:140`](../observer/Program.cs) via `BoardRenderer`). Self-described as *"the floor — a narrator that reconstructs the board by re-reading the journal"* ([`:20-24`](../observer/Program.cs)). It never issues a verb.
- **Watch** ([`Tetris/watch/Program.cs`](../watch/Program.cs)) — the push receiver: a `FileSystemWatcher` on the pushed frame file ([`:39-44`](../watch/Program.cs)), parsing the emitted document ([`:87`](../watch/Program.cs) `FrameDocument.Parse`) and drawing it ([`:96`](../watch/Program.cs) `BoardRenderer`). It **never touches the domain or the journal** — the purest case that all reception is mediated: this audience only ever sees the crew's frame.
- **The human grid is itself a mirilla.** The console reaches the same domain ([`Tetris/console/Program.cs:29`](../console/Program.cs) `new TetrisActor("console", …)`) and the retina reads a *crew-chosen projection*: [`Tetris/actor/BoardRenderer.cs:19-48`](../actor/BoardRenderer.cs) turns the same raw occupied-cell set (`WellSnapshot.Occupied`) into `[]`/space glyphs, walls, and floor. Structurally that is the same act as `pile-scan`'s skyline — a projection of the same facts. The AI did not *introduce* mediation; its instrument is so unlike the grid that it made visible that the human, too, only ever looks through a mirilla.

The one raw fact-set (`OccupiedInterior()`) is projected four ways in-process
(`[]`-grid ×3, skyline ×1) — and a fifth in the browser (§3b). The obra has no
"true appearance"; the projection is always the receiver's, chosen by the crew.

---

## 3b. Browser audiences — cross-runtime, cross-network (the co-location answer)

The four audiences above share one machine, the .NET/PowerShell substrate, and
local IPC (a pipe; a frame file). The **browser** labs answer the co-location
objection: an audience on a *different runtime* (JavaScript), reached over a
*real network transport*, projecting the **same raw frame** its own way.

- **WebSocket host** ([`Tetris/web/Program.cs`](../web/Program.cs)) — serves inline player/observer pages ([`:26-27`](../web/Program.cs)); a browser `WebSocket` sends `{move}` (keydown→verb, [`:161-166`](../web/Program.cs)) and receives frames via `ws.onmessage` ([`:167`](../web/Program.cs)). The output shell is [`WebSocketSink`](../web/WebSocketSink.cs) — an `IOutputSink` that broadcasts the **same `PushDocument.Document`** as `FrameFileSink`, over sockets instead of a file ([`WebSocketSink.cs:70-75`](../web/WebSocketSink.cs)); *"The clean Well is untouched; this is purely the OutputTarget shell"* ([`WebSocketSink.cs:9-15`](../web/WebSocketSink.cs)).
- **REST+SSE host** ([`Tetris/web-rest/Program.cs`](../web-rest/Program.cs)) — player/observer pages ([`:24-25`](../web-rest/Program.cs)) driven by the browser's **native `EventSource`** ([`:172`](../web-rest/Program.cs)); output shell [`SseSink : IOutputSink`](../web-rest/SseSink.cs), the same seam over a medium that doesn't natively push (input is a separate `POST /moves`).
- **The browser's projection is a SEPARATE mirilla, in another language.** The player page's `render()` rebuilds the `[]`/space grid in JS from the raw `cell[]` bitmap ([`web/Program.cs:169-178`](../web/Program.cs)) — explicitly *"mirrors BoardRenderer"* ([`:141-143`](../web/Program.cs)). Same facts, a projection written independently in a second runtime, chosen by the crew — the human `BoardRenderer` and this JS `render()` are two mirillas of one frame.

So the audience crosses **machine, runtime, and transport** (WS and SSE), and the
obra is still untouched (`6868249`, `f093e20` — §1). This is the pata that a
skeptic would demand; it exists and is cited.

---

## 4. Measurements captured (for citation)

| Measurement | Value | Source |
|-------------|-------|--------|
| Domain files changed across the whole audience span | **0** | `git diff --numstat fd8d94b..HEAD -- Tetris/domain` |
| Domain lines changed across the whole audience span | **0** | same |
| Audience/instrument commits, each with domain-diff | **0** | `4fb13c3`, `e06721d`, `a3c6ca6`, `e311e58`, `6868249`, `f093e20` |
| Distinct audiences over the one `Well` | **6** | console / ai / observer / watch + browser player & observer |
| Distinct runtimes an audience runs on | **3** | .NET, PowerShell (`pile-scan`), JavaScript (browser) |
| Browser network transports (same raw frame) | **2** | WebSocket (`web/`), SSE + POST (`web-rest/`) |
| Distinct instruments (mirillas) | **5** | `[]`-grid ×3 (human/observer/watch), skyline-vector (AI), JS-grid (browser) |
| Mirilla lift (real `pile-scan.ps1` run) | 38-cell bitmap → 10-int skyline + zeros + wells + 3 metrics | §2d |
| Lived clock:command ratio (operational, *not* the claim) | ~87 ticks : ~17 commands ≈ **5:1** @ 12 s clock | [`notes/mirilla-and-tetris.md`](mirilla-and-tetris.md) |

**The zeros are the claim** (domain-diff = 0 across every audience). The 5:1 and the
lift counts are operational honesty — feasibility, not superiority.

---

## 5. What Experiment B demonstrates

Across six audiences on three runtimes — keyboard/grid, pipe/skyline, poll/grid,
push/grid, and the browser (player & observer) over WebSocket and SSE — the
`Well` is unchanged (`git` domain-diff = 0). There is **no unmediated
reception**: even the human sees a projection (`BoardRenderer`); the purest local
audience (`watch`) sees *only* the crew's frame, never the domain; and the
browser reprojects the same frame in a second language across a real network.
The obra emits facts; the crew chooses each mirilla; the obra never knows. The
audience changes, the instrument changes — **the play does not.**
