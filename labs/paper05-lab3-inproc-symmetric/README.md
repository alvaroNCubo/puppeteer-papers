# Paper 5 — Lab 3: in-proc symmetric consumer (§5.2 part 2)

Two instances of the **same actor binary** consume the **same event stream** and
produce byte-identical output. A is the primary; B is a passive consumer whose
journal is built by materializing A's corpus via the public `MaterializeMirror`
surface. Both replay their journal through the identical reactions.

Headline → Paper 5 §5.2 part 2.

## What it does (per N × rep)

The workload is the everyday **shopping-cart** shape: a shopper **adds** items to
an order one at a time (each a separate Action), then **checks out** the whole
order once (the single group-close).

1. **A** (FileSystem, `AlwaysCompiled`): `Register("B")`, then drive ~N events in
   order cycles as compiled **Actions** (Rule 1: reactions match Actions, not
   Scripts) — `ItemsPerOrder` calls to `cart.Add(@order, @item)`, then one
   `cart.Checkout(@order)` that closes the order. A then replays its own journal
   through two `.Job()` reactions and captures their output.
2. **Feed (public)**: `new MaterializeMirror(new LocalMaterializeSource(
   A.Actor.Materialization, "B")).AsProgramMirror().Sync()` pulls A's corpus.
3. **Apply (documented internal seam)**: A's records are written into B's journal
   preserving EntryId / OccurredAt / ExposeData (same seam as L4).
4. **B**: instantiated over the materialized journal, defines the identical
   reactions, and replays.

Symmetry is asserted on three axes, all from the **public** surface:

- **Emit terminator** — captured on each side via a public `IOutputSink`
  (`perf.OutputTarget(sink)`); the ordered `(EntryId, ReactionName, Document)`
  stream must be byte-identical (`callback_byte_diff = 0`).
- **Elide terminator** — compared via `actor.Introspection.ShowReaction(...)`,
  normalised to drop wall-clock fields (`elide_show_diff = 0`).
- **Journal segments** — `journal_*.bin` bytes of A vs B (plain file reads;
  `segment_byte_diff = 0`).

## Run

```
dotnet run -c Release -- <outDir> <runTag>     # N ∈ {100,500,1000} × 2 reps; no Docker
```

Env overrides: `LAB3_NVALUES` (csv), `LAB3_REPS`. Append `smoke` for a fast pass.

## Guide compliance

Idioms per `training-lab/guides`: `PerformanceV2` +
`actor.Using(...).WithParameters(...).PerformCommand()`; `CompiledModePolicy =
AlwaysCompiled`; two reaction shapes —

- a **single-seek Emit** (one push per item added):
  `.Seek("AddSeek").OnMatch("[_:Cart].Add($order, $item)").Program.Emit(...)`;
- a **`.Many()` + `.One()` elide** (one elide per checked-out order):
  `.Seek("Adds").OnMatch("[_:Cart].Add($order, $item)").Many()
  .ThenFinalSeek("CheckedOut").OnMatch("[_:Cart].Checkout($order)").One().Metadata.Elide()`;

plus the public `IOutputSink` sink via `perf.OutputTarget(sink)`; the public
introspection surface `actor.Introspection.ShowReaction`; and the public
Materialize surface (`Materialization.Register`, `LocalMaterializeSource`,
`MaterializeMirror`).

**Why `.Many()` + `.One()`:** `.Many()` on the adds is the existential quantifier —
it accumulates all the adds of an order into a **single** trajectory (collapsing the
multiplicity). `.One()` on the closing `Checkout` (a `ThenFinalSeek`) tells the
matcher the order closes on exactly one event, so it fires the elide there and closes
the trajectory's cursors immediately instead of scanning on. `$order` correlates each
order's adds with its own checkout, so N independent orders each elide on their own
close. The closing quantifier is not optional: without it the multi-seek elide does
not resolve per-order, and `.One()`/`.Exactly(1)` also cut the reaction-solve cost
(cursor close-out) substantially.

## Scope / caveat

The **Tell** terminator is out of scope: a symmetric Tell on B re-journals an
envelope (the `PerformCmd` path), which would break `journal_*.bin` parity. Emit
and Elide are the follower-safe terminators; they are sufficient to land the
symmetric-consumer claim for §5.2 part 2. No Docker (in-process, FileSystem).

**Documented internal grant — the apply seam only.** As in L4, `MaterializeMirror`
fetches but does not apply (a documented Hole, `MaterializeMirror.cs:19-26`); B's
journal is built via the structured write API (`DiaryStorage.Write{Script,Define,
Invocation}Entry`), requiring `InternalsVisibleTo("Lab05L3InprocSymmetric")`.
Everything else — the feed, the reactions, the Emit capture, the elision readout,
and the journal comparison — is public surface.

## Provenance

Runtime: public commit `99e8202` (mirrors Pacifico `4a852ba`), built against the public mirror. The committed
`.csproj` references the sibling public clone `..\..\..\puppeteer\`.

## Files produced

- `samples.csv` — per (N, rep): emit counts, callback/segment/elide byte diffs,
  wall ms.
- `summary.csv` — per N: max diffs + emit total.
- `headline.md`.
