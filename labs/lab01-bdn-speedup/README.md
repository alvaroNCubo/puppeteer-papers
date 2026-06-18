# Lab 1 — BenchmarkDotNet speedup harness (Paper 2 §5.2/§5.3)

BenchmarkDotNet driver for the compiled-vs-interpreted speedup (§5.2) plus the
synthetic compile-cost-by-depth and eval-complexity sweeps (§5.2/§5.3). Produces
the figures in Paper 2 §5 against the public Puppeteer runtime commit `b42d0f7`.

## Contents

- `Lab1SpeedupBench.cs` — BenchmarkDotNet benchmark: Interpreted (baseline) vs
  Compiled, across three workloads (`Arith100`, `DslRich`, `Production`/eShop).
- `SyntheticSweeps.cs` — non-BDN one-shot sweeps measured via the public
  `LabInstrumentation` hooks: compile cost by statement count; eval hit/miss by
  sub-program complexity.
- `OrderingFacade.cs` — the eShop `Order.CompleteOrder` facade (MIT, copied from
  `dotnet/eShop` usage) the `Production` workload dispatches.
- `Program.cs` — entry point. No args → BenchmarkDotNet (steady-state job,
  `DOTNET_TieredCompilation=0`); `synthetic <outDir> <sha>` → the one-shot sweeps.

## Reproducing

1. Clone the runtime at the cited commit and build Release:
   `git clone https://github.com/alvaroNCubo/puppeteer && cd puppeteer && git checkout b42d0f7`
   `dotnet build Puppeteer/Puppeteer.csproj -c Release`
2. Build `dotnet/eShop`'s `Ordering.Domain` in Release (net9.0).
3. Point this project's `ProjectReference` at your `Puppeteer/Puppeteer.csproj`
   and the `Ordering.Domain` `HintPath` at your Release build, then:
   `dotnet run -c Release -- --filter '*Lab1SpeedupBench*'`   (speedup)
   `dotnet run -c Release -- synthetic ./out b42d0f7`         (compile/eval sweeps)

Environment for the published figures is recorded in Paper 2 §5.1.
