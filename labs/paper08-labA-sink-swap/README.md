# Paper 8 — Lab A: sink-swap (the destination is the assembler's authority)

One actor records an order and projects it with a single `print` script; the
projection is then bound, **from outside the actor**, to one destination after
another — a real **SQL Server** table, then a real **MySQL** table — changing
only the writer. Both backends receive identical rows, and the actor is neither
recompiled nor reread. The claim is the zeros: **0 producer edits per
destination**, against a fused baseline that pays **1 edit per sink**.

Headline → Paper 8 §4 and Appendix A (Lab A).

## What it does

- A `PerformanceV2` host over a self-contained purchases domain (`Order` /
  `OrderItem` / derived `Total()`), standing in for the shape §4 reads on the
  Microsoft `dotnet/eShop` `Order` aggregate; the sink-swap claim is
  domain-agnostic.
- One `print` projection script. `perf.OutputTarget(sink[, format])` binds the
  destination from outside the actor.
- In-process tests (always run): format (TOON | JSON) is chosen outside the
  actor; pull vs push is the destination's choice with the script unchanged; the
  fused baseline needs one producer edit per sink.
- `[Integration]` test (self-skips with `Inconclusive` if the servers are
  unreachable): the same actor and the same script reach **real SQL Server and
  MySQL**, with zero producer edits, delivering identical rows.

## Run

The labs live in `UnitTestChoreography` (MSTest). The in-process tests run in the
default suite. To run the real-backend `[Integration]` test, start a SQL Server
and a MySQL container, then:

```
export PUPPETEER_TEST_SQLSERVER="Server=localhost,<port>;User Id=sa;Password=<pwd>;TrustServerCertificate=true;Encrypt=false;Connection Timeout=10"
export PUPPETEER_TEST_MYSQL="persistsecurityinfo=True;port=<port>;Server=localhost;user id=root;password=<pwd>;SslMode=none;AllowPublicKeyRetrieval=true"
dotnet test --filter "FullyQualifiedName~LabA_SinkSwap"   # run without the default exclusion runsettings
```

Each run creates a throwaway `p8labA_<guid>` database per backend and drops it
afterward.

## Provenance

Runtime: public Puppeteer [`0bf947b`](https://github.com/alvaroNCubo/puppeteer/tree/0bf947bd6563e34cb141e3b5ba6cd13b4a811023)
(Pacifico master `b4eaa38`, sanitized). `TestDbConfig.cs` (shared helper) reads
both connection strings from the environment variables above and carries no real
credentials.

## Files produced

- `data/paper08-labA-sink-swap/run-<ts>-<sha>/summary.csv` — the destination × producer-edits table.
- `data/paper08-labA-sink-swap/run-<ts>-<sha>/headline.md` — the emitted result block.
