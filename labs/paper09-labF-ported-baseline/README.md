# Paper 9 — Lab F: the ported baseline, built and counted

An orthodox ports-and-adapters arrangement of the same game, written so the comparison in §9 is a
measurement rather than an estimate. Its eleven rule files differ from the journaled domain's by one
line each — the namespace — so the comparison is not rigged by writing a worse domain. Four stagings
were added one commit at a time so each could be counted: a console, a WebSocket host, a
REST-and-server-sent-events host, and an automated player.

Headline → §9 (Table 3) and Appendix A (Lab F). This lab **corrects the paper more than it confirms
it**: two of four estimated claims are refuted, one ties, and the difference that survives is not
the one the estimates predicted.

The two rows of Table 3 this lab supplies:

- **three driven ports** — board output, piece selection, state — against none in the journaled
  domain, and with them three stand-ins without which **20 of 64 tests do not run**;
- **a reconstitution surface**: a restore constructor and a pile factory, **56 lines added and 5
  removed** inside the rule model, against none.

The second is the paper's most attackable figure and the two files worth reading before anything is
run are `domain/model/Well.cs` (the restore constructor) and `domain/model/Pile.cs` (the factory),
whose own comments say why they had to exist.


## What this lab settles, in three parts that are easy to confuse

Read this before opening the code, because "the domain changed" is true of one of the three and
false of another.

### 1. The ports are the shape, not a change

`domain/ports/` holds four interfaces, and they are there before any staging arrives. Three are
**driven** — the domain declares them and depends on someone else to supply them:

    IBoardOutputPort        where the board goes
    IPieceSelectionPort     which tetromino comes next
    IGameStatePort          where the game's state is kept between operations

The fourth, `IGameCommandPort`, is the **driving** port, which `GameService` *implements* rather than
depends on — so it needs no adapter and no double, and on that side the comparison with the journaled
domain is zero against zero. **The paper claims nothing on the driving side.**

The three driven ports are what you can count: **3 here, 0 in the journaled domain.** And their
consequence is countable too — `domain.tests/doubles/` holds one stand-in per driven port, and without
them **20 of the 64 tests do not run at all**. Open the folder; there are three files.

Worth noticing while you are in `domain/ports/`: the whole public surface of this domain is eight
types — the four ports, three data records (`BoardView`, `BoardCell`, `GameState`) and `GameService` —
and **not one of them carries a rule**. `Well`, `Piece`, `Pile`, `Shape`, `Frame` and `PieceType` are
all `internal`, correctly. `IPieceSelectionPort` therefore cannot speak of a piece *type*: it speaks a
`string` letter, because the enum is internal and a port an outside adapter must implement cannot name
it. From the public surface alone you cannot tell this is Tetris.

### 2. Adding stagings did **not** change the domain — and that refutes the paper's own estimate

This is the part to be honest about first. The paper used to reason that each staging would cost the
ported domain an edit. It does not, and this lab is where that was measured: the WebSocket host and
the server-sent-events host — both of which you can run — each cost the ported rule model **nothing**,
identical to the journaled side, twice over.

| Estimate a ported arrangement invites | Measured here |
|---|---|
| an implementation per staging | **confirmed** — 2, 1, 1, 2 across the four stagings |
| a stand-in per driven port | **confirmed** — three; the application service cannot be constructed without them, and 20 of 64 tests cannot run at all |
| a domain edit per staging | **refuted** — the WebSocket and server-sent-events stagings each cost the ported rule model nothing |
| a stand-in "per side" | **refuted** — the driving side is zero, a hexagon *implementing* its driving port rather than depending on it |
| a distinguishing build graph | **tie** — both rule models: zero project references, zero packages, no framework named |
| cost of a new **capability** | persistence moved the ported arrangement by 204 lines added and 9 removed, of which **61 changed lines — 56 added, 5 removed — fall inside the rule model**; the journaled side gained the same capability at nothing |

Two refuted, one tied. **The paper reports all of it**, in §9 and in its own version of this table; the
estimates are attributed to what the pattern invites rather than claimed by the paper, and the
refutations are reported as results. If you find the paper asserting a per-staging difference anywhere,
that is a defect worth reporting — it should not.

### 3. One capability **did** change the domain, and that is the surviving measurement

Persistence. To let a game outlive its process, the ported rule model had to grow a way back in:

    domain/model/Well.cs   line 75   the restore constructor — its comment calls itself
                                     "the point at which persistence reached the model"
    domain/model/Pile.cs   line 51   the pile factory — its comment says it was added for
                                     staging 4 because "the only way in was through the model"

Those two are the 56 added and 5 removed. The journaled arrangement gained the same capability, for
the same client, at **no cost to its domain**, because a record of the acts already existed.

So the difference is not *per staging* but *per capability of the kind a record supplies for free* —
and, in the paper's later framing, one of the two measurements that separate a domain which **declares
obligations** from one which **leaves none open**.

## Order, consoles, and what each shows

**Order: 1 and 2 first, in one console; then 3 to 7, each in its own.** Steps 1 and 2 verify. Steps 3
to 7 demonstrate — one per staging, and then the view the fourth one's output feeds.

```powershell
cd baseline-hex
```

| # | Run this | What you see in it | Who operates it |
|---|---|---|---|
| 1 | `dotnet build TetrisHex.sln` | It builds, self-contained. | You. |
| 2 | `dotnet test domain.tests\TetrisHexDomain.Tests.csproj` | **`Passed! Failed: 0, Passed: 64, Total: 64`.** The 64 is the denominator; 20 of them are the ones that need the three stand-ins. | **You.** The only step that verifies rather than demonstrates. |
| 3 | `dotnet run --project console\TetrisHexConsole.csproj -- --auto` | Staging 1 self-plays and renders, non-interactively. | You, once. |
| 4 | `dotnet run --project web\TetrisHexWeb.csproj` | Staging 2 on **:5090**. Its banner tells you where to play and what each URL shows. | Nobody after launch. |
| 5 | `dotnet run --project web-rest\TetrisHexWebRest.csproj` | Staging 3 on **:5091**, SSE. Its banner carries pasteable lines for the POST, the stream and the pull. | Nobody after launch. |
| 6 | `dotnet run --project ai\TetrisHexAi.csproj -- play1 new`, then `left`, `drop`, `right`, `drop` in place of `new` | **Staging 4.** One op per process, each printing the frame it wrote. Land at least one piece. | **You**, several times. |
| 7 | `.\tools\hex-pile-scan.ps1 -Session play1` | Staging 4's computed view, read off the frame step 6 left behind. | You, whenever. |

**Step 6 is not optional if you want step 7**, and it used to be missing: the table ran stagings 1, 2 and
3 and then asked the scan tool for staging 4's output, which from a clean tree does not exist yet — the
tool answers `no frame file at …\.sessions\play1.frame` and is right to. Found in QA.

Both web hosts print a self-documenting banner: open the page marked **PLAY HERE** to move pieces, and
use the `input` line only for scripting.

**Output on disk.** Step 2 is the one to keep, because it carries the paper's figure:

```powershell
dotnet test domain.tests\TetrisHexDomain.Tests.csproj | Tee-Object -FilePath labF-tests.log
```

## The twenty-second experiment that shows part 3

`RestGameRoom.cs:32` constructs `new GameService(Width, Height, Output, new RandomPieceSelection())` —
four arguments, **no state port**. So staging 3 keeps its games in a dictionary in the process.

Play a little, then stop the host with Ctrl+C and start it again. Open `/observer`: the games are not
empty, they are **gone**. For them to survive, this staging would need `IGameStatePort` wired — and
wiring it is what forced those 56 lines into the rule model. Meanwhile any journaled session on disk
replays from its record.

## Contents

`baseline-hex/` in full, as it stood on branch `claude/confident-satoshi-7ed985` of the examples
repository. Write-up, with the per-staging counts and the line-by-line account of what persistence
forced, in `data/paper09-labF-ported-baseline/`.
