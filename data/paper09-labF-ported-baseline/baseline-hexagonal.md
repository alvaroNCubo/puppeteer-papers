# A ports-and-adapters baseline of the same `Well`, built and counted

**Status:** built, run, and measured. Local commits only (`a5b53e7`, `408d5f2`, `b883b44`, `676cbf4`); nothing pushed.
**Purpose:** to replace the analytic baseline of Paper 9's Appendix A ("Baselines: analytic, not built") with a measurement.

## 1. Why this exists

Paper 9 (*Identity Precedes Staging*) measures two zeros on the journaled arrangement: zero domain
edits per staging, and zero test doubles for the domain's own tests. Appendix A then states what
those zeros are set against, and marks it honestly as reasoning rather than result:

> Under a *ported* arrangement, the domain declaring the interfaces its adapters implement, each new
> staging would implement those ports instead of editing the domain, and each output test would
> supply a stand-in for them: at least one implementation per staging, and at least one double per
> side in Lab E. […] building a ports-and-adapters version of this same `Well` and counting its edits
> and its doubles is the obvious next measurement.

This note is that measurement. It reports what a genuine hexagonal arrangement of the same domain
actually cost, per staging, with git evidence. **Two of the paper's four claims about the ported
baseline survive contact with a built one; two do not.** §10 states which, and §11 proposes the
revision.

## 2. Method

The point of a baseline is to be *fair*, so the construction rules matter more than the code.

**Byte-faithful rules.** The eleven rule-bearing files were copied from `Tetris/domain` and, at the
founding commit, differed from them in exactly one line each — the `namespace` declaration. Verified:

```
Well.cs 4c4 < namespace Tetris;  --- > namespace Tetris.Hex;
Piece.cs, Pile.cs, Shape.cs, Frame.cs, Position.cs, PieceType.cs,
Tetromino.cs, Orientation.cs, Pieces.cs, TetrisRuleException.cs — same, one line each
```

So the comparison is not between two games. It is between two arrangements of one game. Nine of the
eleven files are *still* byte-identical modulo that line at the end of the exercise; §5 reports which
two moved and why.

**Non-clairvoyant construction.** This is the methodological core, and it cuts both ways. The ports
were designed for the *first* staging only, and each later staging was then added and measured. Had
all four stagings been known when the ports were drawn, every port could have been declared up front
and the per-staging domain diff would be zero by construction — which would have been a rigged
result, since it merely moves the cost to a commit the counting does not look at. Building forward
one staging at a time is what makes the numbers mean anything. It also means the numbers describe
*this* development order; §9 says what a different order would have changed.

**Orthodox, not straw-man.** The arrangement follows Cockburn and Martin as a competent practitioner
would, and every choice that could have gone the other way is listed in §9 with its effect on the
counts. Two choices in particular were made *in hexagonal's favour* and are flagged there: the wire
format was kept out of the port's DTO, and the driving-port verbs were made gentle rather than
throwing.

**Same stagings, same renderers.** The four stagings are those of Paper 9's Lab A and Lab C: console,
WebSocket web host, REST+SSE web host, and the automated player that reads a computed view. The
terminal grid and the browser JavaScript are the same code as the journaled hosts', so nothing in the
comparison rides on rendering. Lab A's two StageManager stagings and Lab B's three containers are
**not** attempted; §9 says why and what that costs the argument.

**Additive.** `git diff 485b766 HEAD` is empty for `Tetris/domain`, `Tetris/actor`,
`Tetris/Tetris.sln`, and all fifteen existing host directories. The journaled solution builds clean
and its 44 tests pass unchanged.

## 3. What was built

```
Tetris/baseline-hex/
  domain/                      THE HEXAGON — one project, 18 files, 1446 lines
    model/                     the rules, ported (11 files, 1057 lines)
    ports/                     the ports the domain DECLARES (5 files, 160 lines)
      IGameCommandPort.cs        driving  — implemented by the hexagon, called by adapters
      IBoardOutputPort.cs        driven   — the board, implemented by adapters
      BoardView.cs               the output port's contract
      IPieceSelectionPort.cs     driven   — the non-deterministic piece choice
      IGameStatePort.cs          driven   — added by staging 4; + its GameState contract
    application/GameService.cs  implements the driving port, depends on the driven ones (221 lines)
  adapters/                    driven adapters every staging needs (95 lines)
  console/                     staging 1 — keyboard in, ASCII grid out
  web/                         staging 2 — WebSocket in, WebSocket frames out
  web-rest/                    staging 3 — POST in, SSE out, GET /frame for pull
  ai/                          staging 4 — one op per process, frame file out, state file
  tools/hex-pile-scan.ps1      the automated player's computed view (skyline, wells, metrics)
  domain.tests/                64 tests: 44 ported model + 20 port-level
```

The hexagon's public surface — everything a staging may know — is exactly eight types: four port
interfaces, three contracts (`BoardView`, `BoardCell`, `GameState`), and `GameService`. Every
rule-bearing type (`Well`, `Piece`, `Pile`, `Shape`, `Frame`) stays `internal`, as in the journaled
domain.

**Every staging was run, not just compiled.**

| Staging | verified how |
|---|---|
| 1 console | `--auto` self-play to `G A M E O V E R` |
| 2 WebSocket | real `ClientWebSocket`: opening frame on connect, 6 moves applied, pieces fed on landing; an observer socket received an on-connect snapshot of one session and live frames from a second |
| 3 REST+SSE | 7 pushed SSE frames over one game; `GET /frame` **byte-identical** to the last pushed frame; one observer stream carrying frames tagged with four distinct sessions |
| 4 automated player | 11 separate processes drove one game; state resumed each time; `hex-pile-scan.ps1` read the resulting frame (`skyline : 0 0 0 2 0 1 1 1 1 0`, `wells : col4(d1) col9(d1)`, `maxH=2 agg=6 bumpiness=6`) |

All four still run after staging 4 changed the hexagon; 64/64 tests pass.

## 4. Measurement 1 — domain edits per staging

`git diff --numstat` over `Tetris/baseline-hex/domain`, between consecutive staging commits:

| Increment | commit | hexagon diff | files |
|---|---|---|---|
| S1 → S2 WebSocket | `a5b53e7` → `408d5f2` | **+0 / −0** | 0 |
| S2 → S3 REST+SSE | `408d5f2` → `b883b44` | **+0 / −0** | 0 |
| S3 → S4 automated player | `b883b44` → `676cbf4` | **+204 / −9** | 5 |

Broken down, because where inside the hexagon the change lands is exactly what a reader will want to
argue about:

| Increment | `model/` | `ports/` | `application/` |
|---|---|---|---|
| S2 | +0 / −0 | +0 / −0 | +0 / −0 |
| S3 | +0 / −0 | +0 / −0 | +0 / −0 |
| S4 | +56 / −5 (2 files) | +50 / −0 (2 files) | +98 / −4 (1 file) |

**The same three stagings on the journaled side, from its own history:**

| Staging | commit | `Tetris/domain` | `Tetris/actor` |
|---|---|---|---|
| WebSocket | `6868249` | +0 / −0 | +0 / −0 |
| REST+SSE | `f093e20` | +0 / −0 | +0 / −0 |
| AI CLI **with persistence** | `4fb13c3` | **+0 / −0** | +143 / −18 |

That last row is the sharpest result in this note. The journaled arrangement added *the same client*
— an automated player driven one call at a time, with state surviving between processes — and its
domain did not move at all. The capability was paid for entirely in the accidental shell. The
hexagonal arrangement paid for it inside the hexagon, and 61 of those changed lines are in the rule
model itself.

**So the honest finding on this measurement is a split.** Adding a *transport* cost the hexagon
nothing — twice, measured. Adding a *capability the ports did not already carry* cost it a new port,
a change to an existing port, and two seams in the model. Paper 9's §1 phrasing ("at least one of
each on each side" per staging) is **refuted** for the two transport stagings and **confirmed** for
the fourth.

## 5. What staging 4 actually forced, line by line

Worth itemising, because it is the one place the ported domain moved and the argument now rests on it.

A client that runs one operation per process cannot hold the well in memory. Under ports and adapters
that capability arrives as a port the domain declares:

- `ports/IGameStatePort.cs` — **new** (+42): `Load`/`Save` plus the `GameState` record. The hexagon
  now describes its own *storage* shape as well as its own output shape.
- `ports/IGameCommandPort.cs` — **changed** (+8): a `Show()` verb. A fresh process has been presented
  nothing, and an adapter cannot query the hexagon, so the driving port had to grow a way to ask for
  the current board without moving anything.
- `application/GameService.cs` — **changed** (+98 / −4): a second constructor taking the state port, a
  `Reopen` that rebuilds the well from saved state, and a `Persist` on every write.
- `model/Well.cs` — **changed** (+40 / −5): a restore constructor. This is persistence reaching the
  rules.
- `model/Pile.cs` — **changed** (+16): `Pile.Of`, the seam a restored pile needs.

Final drift of the rule model from the journaled rules, counting `diff` lines (2 = the namespace line
alone):

```
Well.cs 47   Pile.cs 18   Piece.cs 2   Shape.cs 2   Frame.cs 2   Position.cs 2
PieceType.cs 2   Tetromino.cs 2   Orientation.cs 2   Pieces.cs 2   TetrisRuleException.cs 2
```

Nine of eleven files never moved. The two that did moved for persistence, not for rules — no rule of
the game differs between the two arrangements at any commit.

## 6. Measurement 2 — port implementations per staging

| Staging | new driven implementations | which | consumes driving port |
|---|---|---|---|
| 1 console (founding) | **2** | `ConsoleBoardOutput`, `RandomPieceSelection` | yes |
| 2 WebSocket | **1** | `WebSocketBoardOutput` | yes |
| 3 REST+SSE | **1** | `SseBoardOutput` | yes |
| 4 automated player | **2** | `FrameFileBoardOutput`, `JsonFileGameState` | yes |

Paper 9's "at least one implementation per staging" is **confirmed exactly** — it is the one claim the
built baseline reproduces without qualification. No staging escaped with zero, and none needed more
than two.

Two costs the paper did not predict, both measured:

**Adding a staging edited the staging before it — twice.**

- S2 forced `RandomPieceSelection` out of `console/` into a shared `adapters/` project
  (`{console => adapters}/RandomPieceSelection.cs`, and `console/Program.cs` +1,
  `console/TetrisHexConsole.csproj` +4/−2). Otherwise the same policy would have been written twice,
  and by S4 four times.
- S3 forced `FrameJson` out of `web/` into the same place (`{web => adapters}/FrameJson.cs`, and
  `web/WebSocketBoardOutput.cs` +1).

Neither is a domain edit, and neither is large. But both are churn the journaled arrangement never
incurs, and for the same reason in both cases: the policy the adapters had to share is, over there,
*inside the domain* (`Well.NextPieceLetter()`, whose resolved letter the journal records) or
*provided by the substrate* (the push channel's formatter — `Puppeteer/IOutputSink.cs:117`,
`Choreography/Theater/PerformanceV2.cs:386`). Under ports and adapters both became the stagings'
property, so the stagings had to organise them among themselves.

**The domain's own policy became unreachable.** `Well.NextPieceLetter()` still exists in the ported
model and is still covered by a ported test, but no adapter can call it — `Well` is `internal`. So
`RandomPieceSelection` re-implements, outside the hexagon, a policy the hexagon already contains. The
ported arrangement holds the piece-selection policy twice.

## 7. Measurement 3 — test doubles

The hexagonal suite is 64 tests. The split is the measurement:

| Group | tests | doubles required |
|---|---|---|
| `model/` — the ported rules (`WellTests` 22, `PieceTests` 12, `PileTests` 4, `ShapeTests` 4, `DeterminismTests` 2) | **44** | **0** |
| `GameServiceTests` — the hexagon through its ports | 13 | 2 |
| `PersistedGameServiceTests` — the hexagon over a state port | 7 | 3 |
| **port-level subtotal** | **20** | **cannot run with none** |

Double types, one per driven port plus one inline liar:

```
domain.tests/doubles/RecordingBoardOutput.cs    spy   : IBoardOutputPort
domain.tests/doubles/ScriptedPieceSelection.cs  stub  : IPieceSelectionPort
domain.tests/doubles/InMemoryGameState.cs       fake  : IGameStatePort      (added by S4)
PersistedGameServiceTests.BadStateStub          stub  : IGameStatePort      (one negative test)
```

**Driven side: 3 doubles, and they are not optional.** `GameService` — the hexagon's only entrance —
throws `ArgumentNullException` if any driven port is absent. That is asserted as a test rather than
described:

```csharp
Assert.ThrowsException<ArgumentNullException>(
    () => new GameService(10, 20, null!, ScriptedPieceSelection.Always("O")));
```

There is no way to obtain the ported application, and no way to observe what it did, without one
stand-in per driven port. The doubles count also *grew with a staging*: 2 until S4, 3 after.

**Driving side: 0 doubles.** This is where the paper's estimate is wrong, and the reason is
structural rather than incidental. The hexagon *implements* its driving port; it does not depend on
one. A test therefore calls `IGameCommandPort` directly and stands nothing in. "At least one double
per side" holds on the driven side and is **refuted** on the driving side.

**Can the ported domain's tests run with no double at all?** Partly, and the partition is exact: 44
of 64 yes — the rule model declares no port, so it needs nothing, which is the same 44 tests and the
same zero the journaled domain reports. 20 of 64 no. The journaled arrangement's comparable number is
0 doubles for all 44, because `new Well(10, 20)` is all a test needs, and there is no second group of
tests to write.

## 8. Measurement 4 — the build graph

| | project refs | package refs | `dotnet list package` | framework named in sources |
|---|---|---|---|---|
| `Tetris/domain/TetrisDomain.csproj` | 0 | 0 | "No packages were found" | 0 occurrences |
| `Tetris/baseline-hex/domain/TetrisHexDomain.csproj` | 0 | 0 | "No packages were found" | 0 occurrences |

**An exact tie, and it should be reported as one.** A hexagonal domain that declares its ports as
plain C# interfaces adds *nothing* to its build graph. Paper 9's Lab E result — "0 references, 0
packages" — therefore does **not** distinguish the two arrangements, and any reading of the paper
that takes it to is mistaken. The paper does not quite claim otherwise, but §2's emphasis on the
dependency-free `.csproj` invites the misreading, and the honest correction is available now.

What Lab E *does* distinguish survives, in narrowed form. §9 argues:

> the domain of §2 compiles and passes its tests with the framework absent from its build graph,
> which a ported domain cannot do, since a port with neither implementation nor double is not
> something a test can run against at all.

As stated of the ported domain **as a whole**, this is refuted: the ported domain compiles with an
empty build graph and 44 of its tests run with no double. As stated of the **ports and the
application service**, it is confirmed decisively — those 20 tests cannot exist without 3 doubles,
and the entrance type cannot be constructed at all.

A residue was recorded here as running the other way, in hexagonal's favour: the empty public
`TetrisDomain` anchor type, for which the hexagon needs no counterpart, its seam being its ports.
That residue was withdrawn on checking. The framework's seam takes an *assembly*, not a type
(`Choreography/Theater/PerformanceV2.cs:60`), and reads one with `GetTypes()` under a filter that
admits internal types (`Puppeteer/EventSourcing/DomainLibraries.cs:136,151`), so no public type is
required to find, construct or invoke anything in it; the framework's own CLI loads domain libraries
by full path (`PuppeteerCli/AttachCommand.cs:309`), and in `--txt` mode auto-loads every DLL beside
the journal without naming a type at all. Verified by building it: with `TetrisDomain.cs` removed the
domain compiles and exposes zero public types, and a host with no reference to the domain, loading it
by path, seeds a `Well` and spawns a piece to the same reported state
(`{"width":10,"height":20,"cleared":0,"awaiting":false,"type":"T"}`). `typeof(TetrisDomain).Assembly`
is the most ergonomic way to name an assembly in C# and this example uses it; it is not a cost the
arrangement imposes.

## 9. Where the same work landed

Comparing the hexagon against `Tetris/domain` alone would be unfair, because the journaled
arrangement's orchestration and projection live in `TetrisActor`, which the paper calls an accidental
shell. The like-for-like comparison:

| Role | journaled | hexagonal |
|---|---|---|
| the rules | `domain/` 1027 lines | `domain/model/` 1057 lines |
| orchestration + the board's projection | `actor/` **638 lines, outside the domain** | `domain/ports/` + `domain/application/` **381 lines, inside it** |
| shared adapter infrastructure | — (substrate) | `adapters/` 95 lines |

The ported arrangement is not bigger. It is *differently placed*: 381 lines of contract and
orchestration that sit inside the domain in one arrangement and 638 lines that sit outside it in the
other. Every count in this note is a consequence of that placement rather than of volume — which is,
in the end, the paper's own thesis restated from the baseline's side.

## 10. Fairness caveats

A rigged baseline is worthless, so here is every choice that could have gone the other way, and what
it would have done to a number.

1. **Development order.** Declaring `IGameStatePort` at S1 would make S4's domain diff zero and S1's
   larger. The port still has to be declared inside the domain; only *when* changes. No ordering
   makes it live outside.
2. **Where "the domain" ends.** Model, ports, and application are one project — the standard
   small-system hexagonal layout, and the layout that makes the directory comparable to
   `Tetris/domain`. A reader who counts only the pure model would score S4 at **+56 / −5**, not
   +204 / −9. Both figures are in §4 precisely so either reading can be taken. Note that splitting
   the projects changes no other measurement: the doubles are required by whichever project owns the
   port.
3. **The persistence route — the largest single judgement call.** I used a state/repository port, the
   common practitioner choice. The alternative is to journal the driving-port calls in an adapter and
   replay them, which needs no `Well` change. But replay requires the *spawn choice* to be
   reproducible, and in this hexagon the application resolves it through `IPieceSelectionPort` at
   spawn time — so that route needs the driving port to carry the resolved letter, or the selection
   port to become replayable. Both are also changes inside the hexagon. **I did not build that
   variant, so this paragraph is argument, not measurement** — the honest statement is that some
   change inside the hexagon appears unavoidable, and that its size is unmeasured.
4. **`IPieceSelectionPort` at all.** Pushing non-determinism out through a port is orthodox (the same
   move as an injected clock) and it is what makes the application deterministic under test. A
   practitioner who instead left the application calling `Well.NextPieceLetter()` would drop the
   driven-port count from 2 to 1, the per-staging implementation count for S1 from 2 to 1, and the
   doubles count by one — at the price of a non-deterministic application. **This is the single choice
   with the largest effect on the doubles count**, and a reviewer who rejects it should read §7's
   driven-side count as 2, not 3.
5. **The `Show()` verb.** Forced by the `view` op of a process-per-operation client. A practitioner
   could instead render from the state file in the adapter — but that duplicates `GameService.View()`
   outside the hexagon. Some change was needed; a smaller one may exist.
6. **Wire format — decided in hexagonal's favour.** `FrameJson` maps `BoardView` to the wire by hand.
   A practitioner who annotated the port's own DTO with `[JsonPropertyName("r")]` would incur a domain
   edit *per wire format*, which would have made S2 and S3 non-zero. I did not do that, because
   avoiding it is the better design. The journaled arrangement incurs neither cost: the substrate's
   formatter renders the emitted projection.
7. **Gentle verbs — also decided in hexagonal's favour.** The driving-port verbs are no-ops out of
   state, mirroring the journaled check-guards, so the comparison is not skewed by exception-driven
   control flow in the adapters.
8. **Increment scope is not perfectly matched.** The journaled `4fb13c3` bundled a read-only observer
   and a `console/Program.cs` change alongside the AI CLI, so its `actor` +143 / −18 covers slightly
   more than this baseline's S4. Its `domain` +0 / −0 is unaffected, and that is the number in
   contention.
9. **Two stagings and one lab were not attempted.** Lab A's two StageManager stagings (co-hosted
   actors, in memory and over TLS) and Lab B's three containers have no hexagonal analogue here. A
   hexagonal arrangement has no distribution story of its own, so building one would have meant
   inventing an architecture rather than porting one, and any count I produced would be mine rather
   than the pattern's. **No claim in this note covers distribution**, which is where Paper 9's most
   striking zero (Lab B: domain *and* actor diffs both empty across three machines) remains
   uncontested by measurement. The optional gesture and scarce clients are likewise absent.
10. **Scope of the whole exercise.** One domain, one framework, one developer, one ordering. This is
    an existence measurement, not a cost study: nothing here says which arrangement is cheaper to
    build, run, or maintain.

## 11. Verdict

**The measurement complicates Paper 9's estimate. It confirms two of its four claims, refutes two,
and — more usefully — replaces the wrong unit of account.**

Claim by claim:

| Paper 9's claim | Verdict | Measured |
|---|---|---|
| "at least one implementation per staging" (App. A) | **CONFIRMED** exactly | 2, 1, 1, 2 across four stagings |
| "each new staging would implement those ports **instead of** editing the domain" (App. A) | **CONFIRMED for 2 of 3**, refuted for the third | +0/−0, +0/−0, **+204/−9** |
| "at least one of each on each side" per staging (§1) | **REFUTED** as a per-staging rule | two stagings cost the hexagon nothing at all |
| "at least one double per side" (App. A, Lab E) | **CONFIRMED on the driven side, REFUTED on the driving side** | driven 3, mandatory; driving **0** |
| "a ported domain cannot [compile and test with an empty build graph]" (§9) | **REFUTED as stated, CONFIRMED of the ports** | build graph an exact tie; 44/64 tests need no double, 20/64 cannot run without 3 |

**The finding that matters is not in that table.** Paper 9 accounts for the cost of a port *per
staging*, and the built baseline says that is the wrong unit. Adding a staging that needs only what
the ports already carry costs the ported domain **nothing** — measured twice, on a real transport
change each time, which is precisely the case the paper's §1 says costs at least one edit. What costs
the ported domain is a staging that needs a *capability* the ports do not carry. Persistence was such
a capability: it cost a new port, a change to an existing port, and two seams in the rule model. A new
transport was not.

Restated in the paper's own vocabulary, the difference is sharper than the estimate it replaces:

> A port is a fragment of a staging's *shape* lodged in the domain. While the stagings stay within
> that shape, a ported domain is as invariant as a journaled one and the counts are identical. The
> moment a staging needs a capability the shape does not cover, the ported domain must be opened and
> the journaled one need not — because in the journaled arrangement the capability is *provided by the
> substrate* and *bound in the staging*, and in the ported arrangement it must be *declared by the
> domain*.

Paper 9 says this already, in §9, about the contract's location. The baseline shows it is not merely
a matter of where a contract sits: it is a matter of what happens when a new requirement arrives that
the contract did not anticipate. The journaled arrangement's real advantage is not that it survives
new *transports* — hexagonal does too, measurably — but that it survives new *capabilities*. The
strongest evidence is a paired pair of commits, on the same domain, doing the same thing:

- `4fb13c3` — journaled, automated player with cross-process persistence: **domain +0 / −0**
- `676cbf4` — hexagonal, automated player with cross-process persistence: **domain +204 / −9**, of
  which **+56 / −5 in the rule model**

And two honest concessions run the other way: the build graph is a tie, and it is the journaled domain
— not the ported one — that carries a type existing purely for its framework's benefit.

## 12. Recommended revision to Paper 9

1. **Appendix A, "Baselines: analytic, not built."** Keep the fused paragraph as reasoning. Replace
   the ported paragraph with the measured table of §11 and cite this note. The column can move from
   analytic to measured.
2. **§1 and §2.** Drop "at least one of each on each side" as a per-staging claim; it is refuted.
   Replace with the capability formulation of §11 — which is a stronger claim, not a weaker one, and
   is now measured on both arrangements.
3. **§9.** Narrow the build-graph/testability sentence to what holds: the *ports and application
   service* of a ported domain cannot be tested without a double per driven port (measured: 3
   doubles, 20 tests, an uninstantiable entrance), while its rule model and its build graph are
   indistinguishable from the journaled domain's. Note explicitly that the driven/driving symmetry
   does not hold — a hexagon implements its driving port and so needs no double for it.
4. **Lab E.** Report the tie. "0 references, 0 packages" is true of both arrangements and should stop
   carrying comparative weight; the weight belongs on the doubles count and on the anchor-type
   asymmetry, which favours hexagonal and should be said.
5. **§8, Limits.** Add that the invariance measured across *transports* is matched by a ported
   arrangement, and that the measured difference appears at new *capabilities*. Note that
   distribution (Lab B) remains untested against any baseline.

## 13. Reproducing

```bash
cd Tetris/baseline-hex
dotnet build TetrisHex.sln
dotnet test domain.tests/TetrisHexDomain.Tests.csproj        # 64 passed

dotnet run --project console/TetrisHexConsole.csproj -- --auto   # staging 1
dotnet run --project web/TetrisHexWeb.csproj                     # staging 2, :5090
dotnet run --project web-rest/TetrisHexWebRest.csproj            # staging 3, :5091

# staging 4 — one process per operation, then read the computed view
./ai/bin/Debug/net9.0/TetrisHexAi.exe play1 new
for op in left rotate drop drop right drop; do ./ai/bin/Debug/net9.0/TetrisHexAi.exe play1 $op; done
pwsh ./tools/hex-pile-scan.ps1 -Session play1
```

The counts:

```bash
# domain edits per staging (hexagonal)
git diff --numstat a5b53e7 408d5f2 -- Tetris/baseline-hex/domain    # S2: empty
git diff --numstat 408d5f2 b883b44 -- Tetris/baseline-hex/domain    # S3: empty
git diff --numstat b883b44 676cbf4 -- Tetris/baseline-hex/domain    # S4: 5 files, +204/-9

# the same stagings on the journaled side, from its own history
git diff --numstat 6868249^ 6868249 -- Tetris/domain               # WebSocket: empty
git diff --numstat f093e20^ f093e20 -- Tetris/domain               # REST+SSE:  empty
git diff --numstat 4fb13c3^ 4fb13c3 -- Tetris/domain               # AI + persistence: empty

# the rules never diverged (2 diff lines = the namespace line alone)
for f in Well Piece Pile Shape Frame Position PieceType Tetromino Orientation Pieces TetrisRuleException; do
  echo "$f $(diff Tetris/domain/$f.cs Tetris/baseline-hex/domain/model/$f.cs | grep -c '^[<>]')"
done

# build graph, both domains
dotnet list Tetris/domain/TetrisDomain.csproj package
dotnet list Tetris/baseline-hex/domain/TetrisHexDomain.csproj package

# this work was additive
git diff --numstat 485b766 HEAD -- Tetris/domain Tetris/actor Tetris/Tetris.sln   # empty
```

## 14. Commits

| Commit | Increment |
|---|---|
| `a5b53e7` | S1 — the hexagon (model, ports, `GameService`) + console staging + 57 tests |
| `408d5f2` | S2 — WebSocket staging; hexagon diff empty |
| `b883b44` | S3 — REST+SSE staging; hexagon diff empty |
| `676cbf4` | S4 — automated player; hexagon diff **+204 / −9**, doubles 2 → 3 |

Local only, on `claude/confident-satoshi-7ed985`. Publication and any Software Heritage identifier
belong to Paper 9's deposit pass, along with the journaled example's own commits.
