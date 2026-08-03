# Experiment A — cross-machine staging (Increment C2)

**Claim closed:** Paper 9 ("Identity Precedes Staging") Experiment A previously
ran the StageManager "duo" **co-hosted in one process**. This increment runs the
same `Well` domain across **three separate processes in three Docker containers**,
joined over **real Kestrel TLS** on a compose bridge network. Three containers =
three machines (Paper 7 §5.2 fixes the peer count at THREE — the minimum at which
the no-privileged-node claim is unambiguous). The distributed staging is now real,
not written-around.

**The measurement is a ZERO, not an adjective.** The load-bearing invariant — the
`Well` domain — is byte-for-byte unchanged by the C2 deployment work:

```
$ git diff 485b766..HEAD -- Tetris/domain     # (empty)
```

The C2 deployment increment (commit `974f62a`) also left `Tetris/actor` untouched.
A **later, separately-scoped follow-up** (verifying caveat 1 below) does make one
corrective, example-only fix to `Tetris/actor` — parametrizing the frame commands
so the push channel works; the `Well` domain stays untouched throughout. See
[§4](#4-acceptance-test-the-measurements) and [§6 caveat 1](#6-honest-caveats--what-is-and-is-not-reached).

---

## 1. What C2 is (and how it differs from C1)

| | C1 (`sm-duo-tls`, shipped) | **C2 (`sm-cluster`, this increment)** |
|---|---|---|
| Nodes | 2 `StageV2` | 3 `StageV2` |
| Processes | **1** (both in-proc) | **3** (one per container) |
| Transport | Kestrel HTTPS on **loopback** ports | Kestrel HTTPS across the **compose bridge**, dialled by service name |
| Rendezvous | shared object reference (same process) | **out-of-band** invitation + fingerprint exchange (shared volume) |
| Machines | 1 | 3 containers |

`sm-duo-tls/Program.cs` named this step in its own header — *"de-risk the network
transport before C2 (Docker cross-machine)."* This is that C2.

The domain is a parameter, exactly as in every other Tetris host:

```csharp
StageFactory.Create<StageV2>(PerformerId.New(), session, typeof(TetrisDomain).Assembly)
```

The `Well`, `TetrisActor`, and the StageManager machinery are all unchanged; only
`Tetris/sm-cluster/` and `Tetris/docker/` are new.

## 2. Files added (purely additive)

```
Tetris/sm-cluster/Program.cs                 # TetrisStageCluster — 1 StageV2/process, env-driven
Tetris/sm-cluster/TetrisStageCluster.csproj  # references ..\actor (→ engine transitively)
Tetris/docker/Dockerfile                     # aspnet:9.0, COPY build/host, EXPOSE 5443
Tetris/docker/docker-compose.yml             # tetris-a/b/c on tetris-net; 5443 internal
Tetris/docker/run-demo.sh                    # publish → compose up → wait for convergence
Tetris/docker/.gitignore                     # ignores build/ (published artifacts)
Tetris/notes/experiment-a-crossmachine.md    # this note
Tetris/notes/experiment-a-crossmachine.log   # full captured run log (appendix)
Tetris/Tetris.sln                            # + the sm-cluster project registration (additive)
```

## 3. The rendezvous — verified against the framework, not fabricated

The one piece that genuinely differs from the in-proc duo is **cross-container
rendezvous**: the in-proc duo hands `b` the invitation object `a` created by shared
reference; across containers that reference cannot cross. I read the framework
before wiring anything:

- **`ConnectionInvitation`** (`Choreography/Transport/ConnectionInvitation.cs`) is a
  plain serialisable value: `(PerformerId InviterId, ChannelPurpose Purpose, string
  Address)`.
- **`HttpsTransport.CreateInvitationAsync`** builds the address as
  `"{advertiseUrl}|{localId}|{purpose}|{guid}"` — the **advertise URL is embedded**,
  so an accepter in another container dials the right service name.
- **`AcceptInvitationAsync`** POSTs to `{remoteAdvertiseUrl}/connect` and sends back
  its **own** advertise URL, so channels are bidirectional. Both endpoints therefore
  dial each other → **fingerprint pinning must be symmetric**
  (`Stage.TrustPeerHttpsFingerprint`, keyed by advertise URL).
- The transport has **no discovery server**. So the invitation `Address`, the
  inviter's `PerformerId`, and each node's self-signed TLS fingerprint must cross
  **out of band**.

**Chosen mechanism — Paper 7's shared-volume bootstrap** (the proven 3-Docker
harness `Puppeteer-Pacifico-paper7/docker/`, which carries exactly these values over
a shared `/bootstrap` volume — its analog to the out-of-band Usher/QR hop). We reuse
that pattern. We do **not** need Paper 7's Usher: the Usher only *assigns an
identity*, and a Stage's identity is just its `PerformerId`, so `PerformerId.New()`
per process suffices — exactly as `sm-duo` / `sm-duo-tls` already do.

> **What actually crosses the network vs. the volume.** The peer traffic that
> *moves the Well* — coordination (`DirectorAnnounce`/heartbeats), replication
> (`CueEvent`/`CueAck`), and command forwarding — all crosses **container-to-container
> over real TLS** on port 5443 (never exposed to the host). Only the *initial
> rendezvous bootstrap* (TLS fingerprints + invitation addresses) uses the shared
> volume. This is the same honesty Paper 7 applies to its Usher hop.

**Topology** — a fixed Director star (rotation is Paper 7's concern, deliberately
out of scope here): `tetris-a` promotes and plays; `tetris-b`/`-c` are casts that
replicate the Well live over TLS and each render their own frame.

## 4. Acceptance test — the measurements

### 4a. The `Well` plays across 3 containers over TLS; peers converge

All three nodes reach the **same journal entry (13) with byte-identical Well state**:

```
tetris-a  | [tetris-a] convergence checkpoint reached: role=DIRECTOR entry=19 snapshot=type=- cleared=0 awaiting=True over=False cells=8
tetris-b  | [tetris-b] convergence checkpoint reached: role=cast     entry=19 snapshot=type=- cleared=0 awaiting=True over=False cells=8
tetris-c  | [tetris-c] convergence checkpoint reached: role=cast     entry=19 snapshot=type=- cleared=0 awaiting=True over=False cells=8
```

(The journal reaches entry 19 now that each mutating verb journals as a V2 Action —
Define+Invocation — rather than a single literal Script; see [§6 caveat 1](#6-honest-caveats--what-is-and-is-not-reached).
The three nodes still converge to the identical entry and Well state.)

The cross-container TLS handshake (director view; heartbeat noise removed):

```
tetris-a | [tetris] Stage started; local TLS fingerprint cf39405438508d77…
tetris-a | [tetris] pinned peer https://tetris-b:5443/ → fp adea71497ecb9f36…
tetris-a | [tetris] pinned peer https://tetris-c:5443/ → fp 488c4cd2725c5305…
tetris-a | [tetris] coordination up with tetris-b
tetris-a | [tetris] coordination up with tetris-c
tetris-a | [tetris] promoted to Director (IsDirector=True) over real TLS
tetris-a | [tetris] data star up with tetris-b (replication+command)
tetris-a | [tetris] data star up with tetris-c (replication+command)
tetris-a | [tetris] scripted sequence done; final journal entry = 19
tetris-a | [tetris] catch-up sent to 2 cast(s) up to entry 19
```

Cast `tetris-c` (self-signed cert distinct per container; pins both peers;
replicates over TLS):

```
tetris-c | [tetris] Stage started; local TLS fingerprint 488c4cd2725c5305…
tetris-c | [tetris] pinned peer https://tetris-a:5443/ → fp cf39405438508d77…
tetris-c | [tetris] coordination up with director
tetris-c | [Stage …] DirectorAnnounce from 593a207917ff4dd7… (peerMax=0)
tetris-c | [tetris] data star up with director (replication+command); director announced.
tetris-c | [tetris] caught up to entry 19 (target 19)
tetris-c | [tetris]   cast sees: type=- cleared=0 awaiting=True over=False cells=8   <- REPLICATED over TLS
```

**Each node writes its frame** to its per-node `/data` volume — and this is the
real **push channel**: the frame reaction fires on each mutating verb and PUSHES
the rendered frame to that node's `FrameFileSink` (working now that the verbs
journal as V2 Actions — see [§6 caveat 1](#6-honest-caveats--what-is-and-is-not-reached)).
The pushed frame is the `print`ed projection (JSON), and it is **byte-identical
across all three nodes** (the sink writes the raw projection — no per-node header —
so identical replicated state ⇒ identical bytes):

```
$ for f in a b c; do docker compose exec -T tetris-$f cat /data/tetris-$f.frame | md5sum; done
5ec7093bed92239198a3add00ad88282  -   # tetris-a
5ec7093bed92239198a3add00ad88282  -   # tetris-b
5ec7093bed92239198a3add00ad88282  -   # tetris-c
```

The converged frame (two dropped pieces, 8 occupied cells), pushed to every node:

```json
{"width":10,"height":20,"cleared":0,"over":false,"awaiting":true,
 "cell":[{"r":18,"c":3},{"r":18,"c":7},{"r":17,"c":3},
         {"r":19,"c":3},{"r":19,"c":4},{"r":19,"c":5},{"r":19,"c":6},{"r":19,"c":7}]}
```

Full run log: [`experiment-a-crossmachine.log`](experiment-a-crossmachine.log).

### 4b. `Well` domain untouched; two commits (C2 deploy, then the frame-sink fix)

Base commit: **`485b766`** (`Tetris: point engine reference at Pacifico master`).

**The load-bearing invariant — the `Well` domain — is untouched across everything:**

```
$ git diff 485b766..HEAD -- Tetris/domain   →  (empty)   exit 0
```

**Commit 1 — the C2 deployment (`974f62a`): purely additive, actor untouched.**

```
$ git diff 485b766..974f62a -- Tetris/domain   →  (empty)   exit 0
$ git diff 485b766..974f62a -- Tetris/actor      →  (empty)   exit 0   # C2 deploy added no actor change
$ git diff 485b766..974f62a --stat   # only: sm-cluster/, docker/, notes/, +sln registration
```

The C2 deployment is a new host (`sm-cluster`) + docker files + notes, plus the
additive `.sln` registration — no domain, no actor, no existing host.

**Commit 2 — the frame-sink correction (this follow-up): touches the actor, NOT the domain.**
Verifying caveat 1 (below) showed the inert frame push was a non-canonical command
form in `TetrisActor`, not a framework limit. The fix parametrizes the frame
commands and is confined to the example host adapter:

```
$ git diff 974f62a..HEAD -- Tetris/domain   →  (empty)   exit 0   # domain STILL untouched
$ git diff 974f62a..HEAD --stat
  Tetris/actor/IGameHost.cs                   | …   (parametrized CheckThenCommand overload)
  Tetris/actor/TetrisActor.cs                 | …   (Spawn(@type) + nominal @step on nullary verbs)
  Tetris/sm-cluster/Program.cs                | …   (rely on the now-working sink; drop the render fallback)
  Tetris/notes/experiment-a-crossmachine.{md,log} | …
```

So: the paper's "zero domain changes" claim holds unconditionally (the `Well` is
byte-for-byte identical); the actor received one small, deliberate, example-only
correction. (Both commits are LOCAL to the worktree; not pushed.)

## 5. How to reproduce

From `Tetris/` (Docker Desktop running, .NET SDK on PATH):

```bash
docker/run-demo.sh          # publish → build image → up 3 containers → wait for convergence
docker/run-demo.sh --down   # tear down (docker compose down -v)
```

`run-demo.sh` publishes `sm-cluster` to `docker/build/host` (framework-dependent;
the engine lives on the host by project path and is not compiled inside Docker),
then `docker compose up --build`, then waits for three `convergence checkpoint
reached` lines.

## 6. Honest caveats — what is and is not reached

1. **Frame push sink — RESOLVED. It was a non-canonical pattern in `TetrisActor`,
   NOT a framework limit** (an earlier draft of this note wrongly called it an
   engine/actor "drift/limit"; corrected here after verifying against the framework).

   *Symptom.* On current engine master the `FrameFileSink` push channel never fired
   on any host — `[Reaction 'Frame_*'] skipped a literal ScriptEvent` — so no host
   wrote a `.frame` file (verified on `sm-cluster`, on the shipped `sm-duo-tls`, and
   on the PerformanceV2 `ai` writer alike).

   *Root cause (verified in framework code).* `Reaction.ResolveEventForMatching`
   (`Puppeteer/EventSourcing/Follower/Reaction.cs:1693`) skips a `ScriptEventData`
   for a *pure-domain* reaction **by design** — a domain reaction observes V2
   **Actions** (Define+Invocation), never literal Scripts (`IsPureDomainReaction`,
   line 1794). `TetrisActor` issued every verb as a **bare literal Script**
   (`well.MoveLeft();`, `well.Spawn('T');` with no `@parameters`), so each journaled
   as a `ScriptEvent` and was skipped. The framework's own advisory says exactly this:
   *"Migrate the producing endpoint to a parametrized V2 command."*

   *Both suspicions settled by a controlled probe* (PerformanceV2 + the real `Well`,
   one reaction per verb):
   - The receiver `[_:Well].verb()` (aggregate root-var method, not an actor role) is
     **fine** — it matches and pushes once the command is an Action.
   - The **Script-vs-Action** distinction was the blocker. `well.Spawn(@t)` +
     `WithParameters(t='T')` → Action → `[_:Well].Spawn($p)` pushed; a nullary
     `well.MoveLeft()` + a nominal `@param` → Action → `[_:Well].MoveLeft()` pushed;
     the bare `well.Spawn('T')` → Script → skipped.

   *Fix (example only; domain untouched).* `TetrisActor` now issues its verbs as
   parametrized V2 Actions: `well.Spawn(@type)` carries the resolved letter as a
   parameter (this also removes a DSL-string-concat anti-pattern — parameters.md §5),
   and the nullary move verbs carry a nominal `@step` purely to force Action
   journaling. `IGameHost.CheckThenCommand` gained an `Action<Parameters>` overload;
   both adapters (`PerformanceHost`, `StageHost`) route through it.

   *Result — the push revives on every host.* The `FrameFileSink` now fires: all
   three cluster nodes push byte-identical JSON frames (§4a, md5 `5ec7093b…`), the
   shipped `sm-duo-tls` writes its `-d`/`-c` frames, and the `ai` PerformanceV2 writer
   writes its frame. `sm-cluster` therefore now relies on the **real push channel**
   (the earlier direct-from-`Snapshot` `BoardRenderer` fallback was removed). Domain
   tests remain green (44/44). The only literal Script left is the one-time `seed`
   `upgrade` (journal entry 1), which no frame reaction observes — its single Debug
   advisory is benign.

2. **Rendezvous bootstrap uses a shared Docker volume**, not the network. This is
   deliberate and mirrors Paper 7. The *peer data plane* (coordination, replication,
   command forwarding — everything that moves the Well) is genuine
   container-to-container TLS; only the initial fingerprint + invitation exchange is
   out-of-band over the volume. A fully-networked bootstrap (Paper 7's Usher on
   :6443) was **not** wired — it adds an identity-assignment authority C2 does not
   need. See [§3](#3-the-rendezvous--verified-against-the-framework-not-fabricated).

3. **Live replication has a connect-readiness race the framework does not
   auto-recover from.** `ListenReplication` drops out-of-order `CueEvent`s and never
   requests catch-up, so a cast that misses one live entry (entry 1 in an early run,
   here) stalls forever. The fix is the framework's own `SendCatchUpAsync`, which
   the Director issues to each cast after play (paced 10 ms/entry → in order). This
   is the framework-idiomatic repair, not a workaround bolted outside it — but it is
   worth recording that gap-free live delivery is **not** guaranteed by the
   handshake alone.

4. **Fixed Director; no rotation.** `tetris-a` is the Director for the whole run.
   Director rotation across peers is Paper 7's F1/F2 territory and is intentionally
   out of scope for the C2 "distributed staging exists" claim.

5. **TLS trust is TOFU (unpinned CA), fingerprints exchanged over the volume.** Each
   container generates a fresh self-signed cert per start (note the fingerprints
   differ run-to-run); peers pin by SHA-256 fingerprint. Production pinning against a
   real CA is not exercised (the same posture as `sm-duo-tls`).

6. **Engine-reference junction (local build-env only).** This work was done in a git
   worktree nested under `Tetris/.claude/worktrees/`, where the actor's relative
   engine reference (`..\..\..\Puppeteer Pacifico`) does not resolve. A directory
   **junction** outside the repo tree (`.claude/worktrees/Puppeteer Pacifico` →
   `C:\Users\alvar\source\repos\Puppeteer Pacifico`) bridges it. This is a local
   environment fix, invisible to git; from the main checkout the relative path
   resolves natively and no junction is needed.

7. **Commits are LOCAL to the worktree; nothing is pushed** (publication pass
   deferred, per the task).
