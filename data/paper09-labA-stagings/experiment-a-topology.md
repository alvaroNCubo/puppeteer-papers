# Experiment A — "One Play, Many Stages": the topology, verified against code

**Paper 9, Experiment A.** *Change the STAGE (where it runs); the play does not change.*
This note is the code-verified evidence for that claim. It is a measurement, not an
argument (Eratóstenes): the claim is **ZERO domain change across all stagings**, and the
zeros below are that claim. Milliseconds and "it runs" only prove feasibility.

- **Obra (the play):** `domain/` — `TetrisDomain` (the `Well` aggregate and its value
  objects). One project. Framework-free.
- **Membrane (the puppet):** `actor/` — `TetrisActor`, the one Puppeteer facade over the
  `Well`. This is what *articulates* per stage (§-Limits ladder rung 1: "the cast
  articulates, ZERO domain change").
- **Stagings (puestas en escena):** the executable hosts, each a different *stage*.

**Verified:** 2026-07-22, against the working tree at `HEAD = 485b766`
(worktree `vibrant-tesla-c7d897`, branch `claude/vibrant-tesla-c7d897`). Read/measure
only — the domain was not edited.

---

## 1. The concrete meaning of "zero domain change"

The backbone states it as: *one domain project referenced by all stagings, with no domain
source diverging per staging.* The code realises this in a **stronger** form than "every
host references the domain":

```
                 domain/TetrisDomain.csproj          ← the obra (ONE project)
                          ▲
                          │  (exactly ONE production edge)
                 actor/TetrisActor.csproj             ← the membrane (ONE facade)
        ┌───────┬───────┬─┴─────┬────────┬────────┬────────┬───────┐
     console   ai   observer  watch   input   server   sm-server  web ...
        (every executable host references the ONE actor, never the domain directly)
```

There is **exactly one production reference into the obra** — the actor's — plus one test
reference. No host forks, copies, or edits the domain; no `#if`, no per-stage domain
source. A host swaps its *stage* (InputSource / OutputTarget / Performance-vs-Stage host),
never the play.

The mechanism that makes the *same* actor run on different stages is
[`actor/IGameHost.cs:19`](../actor/IGameHost.cs) — the polymorphic frontier: the same
`TetrisActor` (same `Well`) is hosted by a single-actor `PerformanceV2`
([`IGameHost.cs:41`](../actor/IGameHost.cs)) **or** a distributed `StageV2` / StageManager
([`IGameHost.cs:73`](../actor/IGameHost.cs)). Its own doc-comment: *"the host is an
accidental shell."*

---

## 2. Citable evidence — the reference lines (file:line)

### 2a. The only two edges into the obra `domain/TetrisDomain.csproj`

| Referencing project | Line |
|---|---|
| `actor/TetrisActor.csproj` (production) | [`:11`](../actor/TetrisActor.csproj) |
| `domain.tests/TetrisDomain.Tests.csproj` (tests) | [`:19`](../domain.tests/TetrisDomain.Tests.csproj) |

Verified exhaustive: a solution-wide search for `TetrisDomain.csproj` returns only these
two `ProjectReference` lines.

### 2b. Every executable host references the ONE actor `actor/TetrisActor.csproj`

| Staging (host) | Project | ProjectReference line |
|---|---|---|
| Console monolith | `console/TetrisConsole.csproj` | [`:15`](../console/TetrisConsole.csproj) |
| AI commander (Exp. B audience) | `ai/TetrisAi.csproj` | [`:14`](../ai/TetrisAi.csproj) |
| Read-only observer | `observer/TetrisObserver.csproj` | [`:14`](../observer/TetrisObserver.csproj) |
| Viewer / watch | `watch/TetrisWatch.csproj` | [`:16`](../watch/TetrisWatch.csproj) |
| InputSource merge runner | `input/TetrisStage.csproj` | [`:15`](../input/TetrisStage.csproj) |
| Warm long-lived server | `server/TetrisServer.csproj` | [`:14`](../server/TetrisServer.csproj) |
| StageManager, single node | `sm-server/TetrisStageServer.csproj` | [`:14`](../sm-server/TetrisStageServer.csproj) |
| StageManager, duo (InMemory) | `sm-duo/TetrisStageDuo.csproj` | [`:16`](../sm-duo/TetrisStageDuo.csproj) |
| StageManager, duo (HTTPS/TLS) | `sm-duo-tls/TetrisStageDuoTls.csproj` | [`:16`](../sm-duo-tls/TetrisStageDuoTls.csproj) |
| Web, WebSocket | `web/TetrisWeb.csproj` | [`:19`](../web/TetrisWeb.csproj) |
| Web, REST + SSE | `web-rest/TetrisWebRest.csproj` | [`:18`](../web-rest/TetrisWebRest.csproj) |

**11 executable hosts, one actor edge each.** All point at the identical relative path
`..\actor\TetrisActor.csproj`.

### 2c. The one host that references *nothing* — by design

`send/TetrisSend.csproj` (the per-key pipe sender) has **no** `ProjectReference`. Its own
comment at [`send/TetrisSend.csproj:12`](../send/TetrisSend.csproj) states: *"Deliberately
NO reference to the actor/domain/engine: the sender carries a verb over a named pipe and
nothing else."* It is a pure **input mirilla**, not a stage; it pairs with `server/` (the
warm-server topology: `server/` holds the `Well`, `send/` only pushes keystrokes down a
pipe). This is consistent with the claim, not a violation.

### 2d. The engine edge lives only in the actor

The Puppeteer engine is referenced once, from the membrane:
[`actor/TetrisActor.csproj:25`](../actor/TetrisActor.csproj) (`Puppeteer.csproj`) and
[`:26`](../actor/TetrisActor.csproj) (`Choreography.csproj`), pointing at the Pacifico
**master** checkout. The obra never references the engine.

---

## 3. Git evidence — the obra stayed untouched while stagings were added

The single strongest citation:

```
git diff --stat fd8d94b..HEAD -- domain/        →   (empty)
```

`fd8d94b` ("add TetrisActor facade over PerformanceV2") is the **last** commit that
touched `domain/` — and it only added the `NextPieceLetter` query to `Well.cs` (+19 lines)
so the facade could ask which piece is next. Since that commit, **every staging added zero
domain lines.**

**The obra `domain/` — frozen** (`git log --oneline -- domain/`, newest first):

| Commit | Subject | Stage-related? |
|---|---|---|
| `fd8d94b` | add TetrisActor facade; console drives the actor | facade enabler (last domain touch) |
| `a7c2d42` | externalize spawning, one exception, pile owns collapse | pure DDD, pre-staging |
| `2da7237` | single-direction rotation, membership collision, derived game-over | pure DDD, pre-staging |
| `1535120` | DDD encapsulation pass | pure DDD, pre-staging |
| `a32b57c` | Add Tetris example: clean DDD model, no infrastructure | genesis |

**The membrane `actor/` — articulated** (`git log --oneline -- actor/`, newest first):
`485b766` (engine → master) · `68635c3` (SM over TLS) · `e311e58` (StageManager host) ·
`a3c6ca6` (warm server v3) · `e06721d` (PUSH observer) · `4fb13c3` (AI commander +
observer) · `fd8d94b` (facade). **7 commits.**

So across the entire staging campaign the picture is: **membrane changes, obra does not.**
Concretely, the StageManager commit `e311e58` changed 8 files — `Tetris.sln`,
`actor/IGameHost.cs`, `actor/TetrisActor.cs`, `sm-duo/*`, `sm-server/*`,
`tools/pile-scan.ps1` — and **not one file under `domain/`**
(`git show --stat e311e58`). This is the §-Limits ladder rung 1 made literal: a new stage
is met by the membrane articulating, with zero domain change.

---

## 4. Honest inventory — what is real, what is a gap

### 4a. Real Experiment-A stagings (the obra on a different *stage*)

Each is wired to the one actor (§2b) and its build/run was verified **in the main checkout
at commit time** (the commit messages are the record). Mapping to the backbone's
Experiment-A line *console → browser → cluster → StageManager → Ensemble*:

| # | Stage | Host(s) | Verification (commit message, main checkout) |
|---|---|---|---|
| 1 | **Console monolith** | `console/` | `fd8d94b`: build 0/0, test 44/44, `run console -- --auto` to GAME OVER exit 0 |
| 2 | **Warm server + pipe** | `server/` + `send/` | `a3c6ca6`: started server, fired keys down the pipe, same frame file pushed; tests 44/44 |
| 3 | **InputSource merge runner** | `input/` | `682d6a7`: AI mode (pipe+clock) merges pipe moves with autonomous clock on the serial channel |
| 4 | **StageManager, single node** | `sm-server/` | `e311e58`: plays under `StageV2`; `send`+`watch` work over the SM host; zero domain edits |
| 5 | **StageManager, duo (InMemory)** | `sm-duo/` | `e311e58`: 2-node StageManager, glue=0, no Tell |
| 6 | **StageManager, duo (HTTPS/TLS)** | `sm-duo-tls/` | `68635c3`: two nodes joined over real TLS (two Kestrel ports); build 0 warnings; tests 44/44 |
| 7 | **Browser — WebSocket** | `web/` | `6868249`: verified with a `ClientWebSocket` probe (5 frames); tests 44/44 |
| 8 | **Browser — REST + SSE** | `web-rest/` | `f093e20`: POST input, SSE push (5 data frames), GET pull — the adversarial medium |

Supporting hosts (real, but observers/audiences rather than distinct *stages*):
`observer/` and `watch/` (read-only projections — mirillas onto a running game),
`ai/` (Experiment **B** — the AI *audience*, not an Experiment-A stage).

**Honest count for Experiment A: 8 real stagings** across 4 media families (in-process
console, named pipe, StageManager/`StageV2` on InMemory + TLS, HTTP as WebSocket + REST/SSE)
— every one driving the **same** obra with a **zero** domain diff.

### 4b. Gap — the "Ensemble" stage does not exist for Tetris

The backbone's Experiment-A enumeration ends *"→ Ensemble."* **There is no Ensemble Tetris
host.** A whole-solution search for `Ensemble` returns nothing; the 15 projects are only
the ones in §2. Puppeteer's three assemblies are Performance / StageManager / **Ensemble**,
and Tetris has hosts for the first two only. Ensemble is **aspirational** for this lab.

This is partly reconciled by the backbone's own scoping note (brief line 161: *"do NOT tell
the cross-machine distributed story with Tetris"* — a single-player game has no honest
cross-container use case). But the Experiment-A **sentence still names Ensemble**, so the
paper must do one of:
- **(recommended)** drop "Ensemble" from the *Tetris* Experiment-A enumeration and let
  Tetris claim *console → browser → StageManager* (which is real), reserving Ensemble (and
  the cross-machine story) for the coordination-needing domain the backbone already
  earmarks; or
- explicitly mark Ensemble as *not yet staged for Tetris* if it is kept in the sentence.

Either way: **do not claim a Tetris Ensemble staging — it is not in the code.**

### 4c. Note on the task's host list

The task brief referred to an `f7.tests/` (Ensemble) host. **No such directory exists.**
The test project present is `domain.tests/` (the obra's own MSTest suite, §2a) — it is not
an Ensemble staging. Recording this so the paper's host list matches the tree.

---

## 5. Build / run reality — reproducible here vs. main checkout

Being precise so the paper claims only what is real:

- **Obra builds standalone, here, now.** `dotnet build domain/TetrisDomain.csproj` in this
  worktree → **Build succeeded, 0 Warning(s), 0 Error(s)** (no engine dependency).
- **Obra tests pass, here, now.** `dotnet test domain.tests/` in this worktree →
  **Passed! Failed: 0, Passed: 44, Skipped: 0, Total: 44** (≈77 ms). (The README still says
  "42"; the live count is **44** — README is stale on the number, not on the claim.)
- **The hosts do NOT build inside this worktree.** The actor's engine reference is the
  relative path `..\..\..\Puppeteer Pacifico\...` calibrated for the **main checkout**
  depth. A git worktree sits 3 directories deeper
  (`…\.claude\worktrees\<name>\Tetris\actor`), so the path resolves to a non-existent
  `…\worktrees\Puppeteer Pacifico\…` and the actor (hence every host) fails with `CS0246`
  (`PerformanceV2`/`StageV2`/`IOutputSink` not found). This is a **worktree path artifact,
  not a domain-invariance problem** — the obra edge is unaffected.
- **Host build/run evidence is therefore documentary** (commit messages, §4a), captured in
  the main checkout at commit time, each reporting build-green + domain tests 44/44 +
  nothing under `Puppeteer Pacifico` modified. To reproduce a host build/run, use the main
  checkout `C:\Users\alvar\source\repos\puppeteer-examples\Tetris` (where the engine path
  resolves), not this worktree.

---

## 6. Bottom line

- **Claim holds.** One obra (`domain/`), one membrane (`actor/`), 11 executable hosts each
  referencing the single actor; **exactly one production edge into the domain**; and
  `git diff fd8d94b..HEAD -- domain/` is **empty** — every staging added zero domain change.
  The zeros are real and citable (§2, §3).
- **8 real Experiment-A stagings**, across in-process / pipe / StageManager(InMemory+TLS) /
  HTTP(WebSocket+REST-SSE), all driving the same obra unchanged.
- **1 gap:** no Tetris **Ensemble** host exists though the backbone's Experiment-A sentence
  names it — trim or flag it; the code says *console → browser → StageManager*, not
  *→ Ensemble*.
- **1 correction:** the referenced `f7.tests/` host does not exist; the tree has
  `domain.tests/` (the obra's own suite), which is not a staging.
- **Measurement honesty:** obra build 0/0 and tests 44/44 reproduce **here**; host
  build/run reproduce in the **main checkout** (the worktree breaks only the engine's
  relative path, not the obra).

---

## 7. Is it sufficiently proven WITHOUT Docker / specific hardware? — verdict

**Yes, for what Experiment A claims — and neither Docker nor hardware would strengthen the
load-bearing claim.** The verdict, split by claim so the paper doesn't over- or under-sell:

1. **Invariance (the claim: zero domain change) — proven, and independent of process
   topology.** One production edge into the obra + empty `domain/` diff + live 44/44. Where
   the play runs (same process / container / machine) does not touch invariance. The zeros
   are the zeros regardless. Docker would re-demonstrate the *same* InputSource/OutputTarget
   seam on one more transport — Eratóstenes-redundant.

2. **Transport feasibility — proven across genuinely different media.** in-process · **real
   Kestrel/HTTPS TLS** · WebSocket · REST+SSE. The TLS leg is not simulated: commit
   `68635c3` verified a cast `MoveRight` crossing the **HTTPS command channel** to advance
   the director's entry. The technically hard part (a real network socket) is de-risked.

3. **Genuine cross-process / cross-machine distribution — NOT proven, and honestly so — but
   out of scope for Tetris by the backbone's own decision.** The StageManager duo runs in
   **one process** (two loopback Kestrel ports): the data plane (replication + command)
   crosses a real TLS socket, but the node rendezvous passes the invitation **in-process, by
   object reference** (`sm-duo-tls/Program.cs:112` `ConnectCoordination` — `a.CreateInvitationAsync`
   → `b.AcceptInvitationAsync(inv)` within one `Program`). Two separate processes would need
   an out-of-band bootstrap. This is exactly the unbuilt increment the engine team labelled
   in `68635c3`: *"C2 (Docker cross-machine) next."* The backbone (brief line 161) already
   rules this **out of scope for Tetris** — a single-player game has no honest cross-container
   use case; the true cross-machine story is deferred to a coordination-needing domain. So
   this gap is a *deliberate scope boundary*, not a missing proof.

4. **Experiment B hardware (ESP32 / gesture camera) — not needed.** The AI mirilla already
   carries axis B, real and lived; hardware are "additional shadows," explicitly low
   priority / non-blocking (brief line 67).

**Consequence for the paper (wording, not code):** the code proves *console → browser (WS +
REST/SSE) → StageManager (InMemory + real TLS, co-hosted)*. It does **not** prove a
multi-machine "cluster" nor an "Ensemble." Trim/soften "cluster" and drop/flag "Ensemble"
from the Tetris Experiment-A enumeration; keep the genuine cross-machine narrative for the
coordination-needing domain, as the backbone already decided.

**Cheapest optional strengthening (only if answering a "you never crossed a process
boundary" skeptic) — needs NO Docker, NO hardware:** split the duo into two `dotnet run`
processes on one machine, exchanging the invitation over a file/stdout instead of by object
reference ("C2 minus Docker"). Zero new infra, zero domain change. Per the backbone's scope
decision, even this is optional — Tetris is complete for Experiment A as it stands.
