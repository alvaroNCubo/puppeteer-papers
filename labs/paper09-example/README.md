# Tetris

A clean, infrastructure-free domain model of Tetris — pieces, the well, the
pile, the boundary, collision, line clears, and game over — built as a showcase
of rich object-oriented modelling. There is **no Puppeteer reference** anywhere
in the domain: it is plain C# that builds and tests standalone, and its source
reads as if no framework exists. A later, separate phase will wrap the aggregate
root for the "distributed observation" labs — see
[Where this is going](#where-this-is-going) below. The solution is
[`Tetris.sln`](Tetris.sln).

## Layout

```
Tetris/
├── Tetris.sln
├── domain/         TetrisDomain library        (no Puppeteer reference)
├── domain.tests/   MSTest invariant + behaviour suite
└── console/        TetrisConsole demo           (pure domain, no Puppeteer)
```

[`domain/`](domain/) holds the model. **Exactly one type is public** — the
anchor `TetrisDomain`, an empty type that lets a host hand the assembly to a
host with `typeof(TetrisDomain).Assembly`, the same convention as HelloWorld's
`WelcomeDomain`. *Everything else is `internal`*: `Shape`, `Position`, `Piece`
and its seven subclasses, `Pile`, `Frame`, `Orientation`, `PieceType`,
`Tetromino`, the piece sources, the exceptions, and the aggregate root `Well`.
From outside the assembly the model presents no surface at all but the anchor,
so a caller cannot fabricate an invalid placement — there is no public
`Position` to hand the well a cell outside the frame. The trusted insiders are
the test suite and the console demo, granted access through
`[assembly: InternalsVisibleTo(...)]` in [`AssemblyInfo.cs`](domain/AssemblyInfo.cs);
a host reaches the verbs by reflection over the assembly.

| Project | What it is |
|---|---|
| [`domain/`](domain/) | The clean DDD model. Immutable value objects + one mutable aggregate root. Only `TetrisDomain` is public. |
| [`domain.tests/`](domain.tests/) | 42 MSTest cases covering geometry, collision, line clears, invariants, determinism, and the game-over contract. |
| [`console/`](console/) | A pure-domain demo that plays a fixed script and renders the well as ASCII. |

## The unifying abstraction: figures within figures

Everything occupied in a Tetris well is the same kind of thing — *a figure that
can answer, for any cell, "do you occupy this?"*. A falling piece is such a
figure. The accumulated pile is such a figure. And — the decision the whole
model turns on — **so is the boundary**. The walls and floor are not a special
case checked with bespoke arithmetic; they are a figure exactly like a piece or
the pile.

Membership is the primitive. That single idea is expressed as the abstract
[`Shape`](domain/Shape.cs):

```csharp
internal abstract class Shape
{
    public abstract ImmutableHashSet<Position> Cells { get; }
    public virtual bool Occupies(Position position) => Cells.Contains(position);
    public bool Intersects(Shape other) => Cells.Any(other.Occupies);
}
```

`Occupies` is the one thing every figure must answer; `Cells` enumerates the
extent for figures that have a finite one. `Intersects` iterates **this**
figure's cells and probes the **other**'s membership — so it costs one
`Occupies` call per cell of `this`. The convention is to call it as
`small.Intersects(large)`: in collision the four-cell piece is the small,
iterated figure (`piece.Intersects(frame)`, `piece.Intersects(pile)`), and the
boundary is only ever the probed party.

`Piece`, `Pile`, and `Frame` all derive from `Shape`. They are *figures within
figures*: the well is a figure (the frame) that contains figures (the pile and
the falling piece).

### The frontier is a figure — a boundary *predicate*

`Frame` is the answer to "is the boundary just another shape?" — **yes**, but it
need not enumerate anything. Its walls run as high as a piece can sit, so its
extent is unbounded; instead of materialising sentinel cells it overrides
`Occupies` as a closed-form predicate:

```csharp
public override bool Occupies(Position p) =>
    p.Column < 0 || p.Column >= Width || p.Row >= Height;
```

That is: anything left of column 0, at or beyond the width, or at or below the
floor row is boundary. The **top is open** — a row above 0 is *not* boundary, so
a piece may sit above the field while it spawns and falls in. Because collision
only ever *probes* the frame (the four-cell piece does the iterating), the
boundary never materialises a cell set at all, and `Frame.Cells` is unsupported.

### One collision rule, not three

Naïve Tetris code has three collision checks: against the left/right walls,
against the floor, and against the pile. Here there is **one**, because all
three obstacles are figures and membership is the primitive:

```csharp
private bool Collides(Piece candidate) =>
    candidate.Intersects(Frame) || candidate.Intersects(Pile);
```

A piece pressing into a wall, a piece resting on the floor, and a piece landing
on a stack are the same event: one of the piece's four cells is occupied by some
figure. Each test is O(4) — four membership probes — and `Intersects` quietly
benefits from each figure's natural form (the frame as a predicate, the pile as
an O(1) hash-set lookup) while the one-rule conceptual unity is preserved. The
rule lives once, in [`Well`](domain/Well.cs), and reads like its own definition.

## The pieces: polymorphism across the seven tetrominoes

[`Piece`](domain/Piece.cs) is an abstract `Shape` of exactly four cells with a
`PieceType` and an `Orientation`. The seven tetrominoes
([`Pieces.cs`](domain/Pieces.cs)) are concrete subclasses — `IPiece`, `OPiece`,
`TPiece`, `SPiece`, `ZPiece`, `JPiece`, `LPiece`. Each subclass supplies exactly
**one** thing: the anchor-local layout of its four cells for a given pose. The
base class owns everything else — anchoring into the well, exposing world cells,
rotating, translating, and enforcing the four-cell invariant.

The pieces differ in their rotational symmetry, and the polymorphism captures
that difference *naturally* rather than with conditionals:

| Piece | Distinct orientations | Why |
|---|---|---|
| `O` | 1 | The square looks the same from every side; rotation is a no-op. |
| `I`, `S`, `Z` | 2 | A bar or skew has only two appearances. |
| `T`, `J`, `L` | 4 | Each pose is genuinely distinct. |

[`Orientation`](domain/Orientation.cs) is a value object that knows its piece's
`DistinctCount` and cycles `Index` modulo that count, so `O` never leaves pose
0, `S` toggles 0↔1, and `T` walks 0→1→2→3→0. Rotation turns in a **single
sense**, as in the classic original: `Piece.Rotate()` steps the pose index
forward by one, wrapping at the piece's distinct count. There is no
counter-rotation — cycling `Rotate()` repeatedly visits every pose and returns
to the spawn pose, which is all the original ever offered. For the square,
`Rotate()` is a valid no-op (one pose): no effect, no error. The four-pose
pieces' poses are ordered so the single sense matches the classic clockwise
cycle; the bar and skews simply toggle between their two states (the bar's two
poses are a *bascula* — a horizontal/vertical rock that need not share a centre,
encoded directly in the layouts). Rotation is immutable: `Rotate` returns a
*new* piece; `Translate(offset)` likewise. A piece never mutates and never knows
about walls or the pile — only about its own shape. The well decides legality;
the piece only offers candidates.

## The pile mutates bottom-up

[`Pile`](domain/Pile.cs) is the accumulated landed blocks — *the floor that does
not move but mutates*. It is immutable, and it **owns the whole landing
transition**. One operation does it all:

```csharp
public (Pile pile, IReadOnlyList<int> collapsedRows) Integrate(Piece piece)
```

`Integrate` merges the landed piece's cells, removes every row the piece
completed, and returns the new pile — which by construction holds no complete
row — together with the indices of the rows that collapsed. Row-completion and
collapse are *private* helpers; the well never orchestrates them. The well's
landing is therefore just: `(Pile, var collapsed) = Pile.Integrate(landed);
ClearedLines += collapsed.Count; Active = null;` — no `if`, no row scan, no
clearing logic leaking up into the aggregate root.

The collapse is genuinely **bottom-up**. A surviving cell drops by the number of
cleared rows strictly *below* it:

```csharp
var clearedBelow = completed.Count(clearedRow => clearedRow > cell.Row);
survivors.Add(cell.Translate(new Offset(clearedBelow, 0)));
```

So a block sitting two rows above a vanished line ends one row lower; a block
above *two* vanished lines ends two rows lower; and a tower spanning a
non-adjacent pair of cleared rows collapses correctly because each surviving
cell counts only the clears beneath it. The tests pin all three cases.

## The next piece comes from outside

The well does **not** decide which tetromino comes next — it is *told*. Spawning
is an inbound operation: `Spawn(PieceType type)` places that type at the spawn
anchor. Choosing the type — at random, from a fixed script, however the caller
likes — lives entirely outside the domain. The console host rolls a `Random`;
later a Puppeteer reaction will fill exactly the same seam.

This makes the well a pure, deterministic function of *(construction + the
stream of operations applied to it)*, with no hidden randomness inside it at
all. Every value object is immutable, and a transition replaces a reference
rather than mutating in place. The `DeterminismTests` replay the same command
stream (spawns interleaved with moves) ten times and assert a single, identical
state fingerprint.

### Three states, and `IsAwaitingPiece`

Because spawning is external, an empty active slot no longer means "game over".
A well is in one of three states:

- **falling** — `Active != null`, a piece is in play;
- **between pieces** — `IsAwaitingPiece` (i.e. `Active is null && !IsGameOver`):
  the last piece has settled and the well waits for the next `Spawn`;
- **over** — `IsGameOver`.

The construction opens an *empty* well in the between-pieces state, awaiting its
first `Spawn`.

## The aggregate root and its verbs

[`Well`](domain/Well.cs) is the only mutable thing in the model. It composes the
`Frame`, the `Pile`, and the active `Piece`, and exposes an inbound surface (all
`internal`, since the class itself is internal):

- `Spawn(PieceType type)` — place a piece of that type at the spawn anchor.
  Valid only when `IsAwaitingPiece`.
- `MoveLeft()`, `MoveRight()` — shift the active piece one column and apply it
  **iff** `!Collides(candidate)`; otherwise the move is a no-op.
- `Rotate()` — turn the active piece one step in the single rotation sense and
  apply it iff the rotated pose does not collide. No wall-kicks (see the
  trade-off below).
- `Tick()` — descend one row if free; otherwise **land**.
- `Drop()` — descend until resting, then land (the hard drop).

…plus the read queries `Active`, `IsGameOver`, `IsAwaitingPiece`,
`ClearedLines`, and `OccupiedInterior`.

**Landing does not spawn.** When `Tick`/`Drop` settles a piece, the well hands
it to the pile, adds the collapsed-row count, sets `Active = null`, and stops
there — leaving the between-pieces state. It does *not* draw the next piece; the
host does, with `Spawn`, once it sees `IsAwaitingPiece`. Game-over is then simply
the host observing `IsGameOver` and never spawning again.

**Query-first contract; one exception.** The operations are valid only in the
right state, and otherwise throw the single [`TetrisRuleException`](domain/TetrisRuleException.cs):
`Spawn` only when awaiting a piece (spawning while one is falling, or when over,
throws); the move verbs only when a piece is active (moving while between pieces
or over throws). A caller checks the queries (`Active` / `IsAwaitingPiece` /
`IsGameOver`) *first* — it never relies on catching the exception. A move that
is merely **blocked** (it would collide with a wall or the pile) is a different
thing entirely: a valid no-op — the piece stays put and nothing is thrown.

**Game-over is derived, not stored.** There is no boolean flag hoped to stay in
sync. `IsGameOver` is computed: `SpawnRegion.Intersects(Pile)` — the game is over
exactly when the pile has risen into the small region a new piece would be born
into (the top two rows across the four spawn columns). The spawn region is itself
a small figure, so the check is the same membership probe as collision; and when
`IsAwaitingPiece` holds the spawn region is clear by definition, so the next
`Spawn` always fits.

`Collides`, `Land`, `Shift`, `SpawnAnchor`, `SpawnRegion`, and `AssertInvariants`
are all `private`; the surface is exactly the operations a host issues plus the
read queries. A simple `ClearedLines` counter is the only score-like state kept,
and it stays clean.

## Invariants (and where they live)

Every transition ends with `Well.AssertInvariants()`, the executable statement
of what a valid well is. It never fires in correct play; it turns any modelling
bug into a loud `TetrisRuleException` — the **single** exception the domain
throws — rather than silent corruption.

| Invariant | Enforced where |
|---|---|
| A piece has **exactly four** distinct cells | `Piece` constructor → `TetrisRuleException` |
| An orientation matches its piece's symmetry | `Piece` constructor (pose `DistinctCount` vs piece's count) |
| Game over **implies** no active piece (one-directional) | `Well.AssertInvariants()` — `IsGameOver ⟹ Active is null`; `IsGameOver` is *derived* from `SpawnRegion.Intersects(Pile)`. (The converse fails on purpose: a null active piece may just mean *between pieces*.) |
| The active piece rests in **free space** | `Well.AssertInvariants()` via the unified `Collides` |
| Every occupied cell lies **inside the frame** | `Well.AssertInvariants()` via `Frame.Contains` |
| The pile **never retains a complete row** | `Pile.Integrate` collapses completed rows by construction; re-checked in `AssertInvariants()` |
| **Determinism** of `(construction + operation stream)` | All transitions replace immutable values; the next piece is supplied from outside (`Spawn`), so there is no randomness inside the well |

## Build and run

From this folder:

```
dotnet build Tetris.sln
dotnet test Tetris.sln
dotnet run --project console/TetrisConsole.csproj
```

The domain has no Puppeteer dependency, so it builds standalone; the test
project uses MSTest from nuget.org.

The console is an **interactive, keyboard-driven** game — the "console
monolith". It references only the domain and drives the `Well` directly, with
the host supplying everything the domain externalizes: the keyboard (inbound
commands), the clock (a ~500 ms gravity `Tick`), the randomness (a
`System.Random` choosing which piece to `Spawn` — the exact seam a Puppeteer
reaction will later fill), and the rendering. It honours the query-first
contract by construction: it inspects `IsGameOver` / `IsAwaitingPiece` /
`Active` before every operation and never catches `TetrisRuleException`.
Controls: ←/→ move, ↑ rotate, ↓ soft drop, Space hard drop, Q/Esc quit.
`dotnet run --project console/TetrisConsole.csproj -- --auto` self-plays random
moves for a few seconds as a headless rendering smoke-test.

## Trade-offs and open questions

- **Single-direction rotation, no wall-kicks.** `Rotate()` turns one way, as in
  the classic original, and a rotation that would collide is simply rejected (a
  no-op). Modern Tetris adds counter-rotation and SRS wall-kicks (nudge the
  piece by small offsets to find a legal pose); both are clean extensions but add
  surface and a kick table that muddy the "one collision rule" story, so they are
  left out here.
- **Game-over as "pile reached the spawn region".** `IsGameOver` is derived from
  the pile intersecting a fixed spawn region, independent of which piece is next.
  This is faithful and simple; a variant could test the *actual* next piece's
  spawn pose instead, but that couples game-over to the draw order for no real
  gain in this spatial model.
- **Minimum well size.** A well must be at least 4 columns wide and 2 rows tall
  so a piece's spawn pose fits; the constructor rejects anything smaller. Classic
  Tetris is 10×20.
- **Spawn placement.** Pieces spawn with their 4-wide bounding box centred and
  anchored at row 0; the open sky above is interior. A different "spawn in the
  vanish zone above the field" convention is possible but does not change the
  spatial model.
- **The pile owns the collapse.** `Pile.Integrate(piece)` absorbs the landed
  piece *and* clears completed rows in one call, returning the collapsed-row
  indices. The well does no row-scanning or clearing orchestration. An
  alternative would split absorb and clear into two steps the well sequences,
  but that scatters the "a pile never keeps a complete row" invariant across two
  callers; keeping it one operation keeps the invariant local.
- **Rotation layouts.** The layouts follow the common SRS cell positions, but
  with simple pivot-free rotation (each pose is an independent layout). For a
  pure spatial model this is enough; a true SRS pivot is an extension.

## Where this is going

The domain above is deliberately framework-free; nothing in its source mentions
Puppeteer, actors, or journals. This section is the forward-looking framing that
those source files deliberately omit.

In a later, separate phase the `Well` aggregate root becomes a Puppeteer V2
actor. The deterministic shape is what makes that wrapping clean: an actor's
journal records the sequence of inbound operations and is *replayed* to rebuild
state, so any nondeterminism would make two replays of the same journal diverge.
The model is already a pure function of *(construction + operation stream)*, and
the one source of randomness — *which piece to spawn* — has been pushed entirely
outside the domain, behind the inbound `Spawn(type)` operation. The console host
fills that seam with a `System.Random`; under the framework a reaction fills it
instead, and the random draw it makes is **captured** (via the framework's
`Eval` mechanism, which records a nondeterministic result the first time and
replays the recorded value thereafter), so a replayed journal reproduces the
very same game even though the live game spawned random pieces. The same actor
will then be observed across three topologies — a console monolith, two
decentralised phones, and a web screen with several simultaneous viewers — for
the distributed-observation labs.

## Conceptual entry point

The design conditions this example illustrates — a clean domain with zero
infrastructure, immutable value objects with a single mutable aggregate root,
and determinism as a precondition for journal replay — are developed in the
companion papers repository
[`alvaroNCubo/puppeteer-papers`](https://github.com/alvaroNCubo/puppeteer-papers).
Paper 1 (*Anti-porosity*) is the entry point; the distributed-observation labs
this model is destined for belong to Paper 9.
