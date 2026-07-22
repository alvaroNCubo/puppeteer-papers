# Paper 8 — Lab B: projection fan-out (the observer authority: *what*)

From one journaled fact, three distinct observers are added — a **fulfilment**
view, a **finance** view whose figure is *derived in the projection*
(unit price × units), and a **catalog** view — each a projection reaction over
the same fact, each authored **without adding a single method to the domain**.
The domain's surface is unchanged (confirmed by reflection); a fused baseline
pays one domain method per view.

Headline → Paper 8 §4 and Appendix A (Lab B). The claim is the zeros:
**0 new domain methods for 3 views**, against **3** for the fused baseline.

## What it does

- One journaled fact; three projection reactions produce three views.
- The finance view's `lineRevenue` is computed in the projection
  (`@price*@units`), never stored as a domain method.
- Adding an observer grows the reaction layer, not the domain; the domain surface
  is asserted unchanged by reflection.

## Run

The lab lives in `UnitTestChoreography` (MSTest) and runs in the default suite:

```
dotnet test --filter "FullyQualifiedName~LabB_ProjectionFanout"
```

## Provenance

Runtime: public Puppeteer [`0bf947b`](https://github.com/alvaroNCubo/puppeteer/tree/0bf947bd6563e34cb141e3b5ba6cd13b4a811023)
(Pacifico master `b4eaa38`, sanitized).

## Files produced

- `data/paper08-labB-projection-fanout/run-<ts>-<sha>/summary.csv` — the view × domain-methods table.
- `data/paper08-labB-projection-fanout/run-<ts>-<sha>/headline.md` — the emitted result block.
