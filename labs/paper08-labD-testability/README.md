# Paper 8 — Lab D: testability as evidence (the hard boundary is observable)

The separation of §4 is not only argued; it can be **tested for**. A domain
projection is exercised end to end — an order recorded, its total read back —
with **no destination bound at all**: no sink, no port, no test double for
output. That the test needs none is the operational proof that the sink was never
in the domain; were it the producer's, the test could not run without it.

Headline → Paper 8 §4 and Appendix A (Lab D). The claim is the zeros:
**0 test doubles for output**, against a hexagonal (ports-and-adapters) baseline
that must stand up **≥ 1** (the port).

## What it does

- Exercises the separated domain projection end to end (pull path), returning
  `{"total":35}` with no destination bound.
- Confirms the projection survives replay with no sink.
- Contrasts a hexagonal domain, whose output test must stand up at least one
  double (the port), against the separated domain, which stands up zero — there
  is no port to double. Inversion relocates a dependency; this removes it.

## Run

The lab lives in `UnitTestChoreography` (MSTest) and runs in the default suite:

```
dotnet test --filter "FullyQualifiedName~LabD_Testability"
```

## Provenance

Runtime: public Puppeteer [`0bf947b`](https://github.com/alvaroNCubo/puppeteer/tree/0bf947bd6563e34cb141e3b5ba6cd13b4a811023)
(Pacifico master `b4eaa38`, sanitized).

## Files produced

- `data/paper08-labD-testability/run-<ts>-<sha>/summary.csv` — the approach × doubles table.
- `data/paper08-labD-testability/run-<ts>-<sha>/headline.md` — the emitted result block.
