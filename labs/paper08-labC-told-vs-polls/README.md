# Paper 8 — Lab C: told vs polls (the observer authority: *how*)

The same fact reaches an observer by two routes: it is **told** — a reaction
carries it across a `tell` to the observing role — or the observer **polls** it
with a query. Both deliver the identical observation, and the producer's domain
holds **no method for either direction**: being told is a reaction, polling is a
query, and neither is the domain's to name.

Headline → Paper 8 §4 and Appendix A (Lab C). The claim is the zeros:
**0 producer domain methods for the delivery direction**, either way.

## What it does

- One fact, one observation, two delivery directions.
- Polled (pull): a `PerformQuery` on the producer returns `{"amount":100}`.
- Told (push): a reaction's `Causation.Continue(tell)` carries the same value
  (envelope args `100`) to the observing role.
- Both reach the identical observation; the direction is chosen outside the
  actor, and the producer names no observer and no transport.

## Run

The lab lives in `UnitTestChoreography` (MSTest) and runs in the default suite:

```
dotnet test --filter "FullyQualifiedName~LabC_ToldVsPolls"
```

## Provenance

Runtime: public Puppeteer [`0bf947b`](https://github.com/alvaroNCubo/puppeteer/tree/0bf947bd6563e34cb141e3b5ba6cd13b4a811023)
(Pacifico master `b4eaa38`, sanitized).

## Files produced

- `data/paper08-labC-told-vs-polls/run-<ts>-<sha>/summary.csv` — the direction × domain-methods table.
- `data/paper08-labC-told-vs-polls/run-<ts>-<sha>/headline.md` — the emitted result block.
