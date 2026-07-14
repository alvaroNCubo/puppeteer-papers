# Lab 4 — passive consumer / Materialize v2 — headline

- Runtime: Pacifico `99e8202` (built against the public mirror).
- Host: Microsoft Windows NT 10.0.26200.0 / 32cores / .NET 9.0.14
- Régime: compact-action journal (`AlwaysCompiled`), N ∈ {1000, 10000, 100000}, 3 reps (rep 0 warm-up).
- Wire = public `MaterializeMirror`; apply = documented internal structured-write seam; parity = public `PerformQuery`.
- Run: 20260713-125915

**Parity: 0/18 cells bit-exact** (replica over destination journal reaches the primary's `_seq`).

## Table 1 — sync + apply throughput by backend / layer / N

| backend | layer | N | sync p50 ms | apply p50 ms | total p50 ms | total p95 ms | events/sec p50 |
|---------|-----:|--:|------------:|-------------:|-------------:|-------------:|---------------:|
| FileSystem | 1 | 1000 | 20.636 | 410.832 | 431.468 | 442.058 | 2317.7 |
| FileSystem | 1 | 10000 | 31.273 | 4119.783 | 4151.056 | 4162.874 | 2409.0 |
| FileSystem | 1 | 100000 | 167.286 | 46354.227 | 46521.512 | 46814.467 | 2149.5 |
| FileSystem | 2 | 1000 | 7.823 | 410.243 | 418.067 | 429.602 | 2392.0 |
| FileSystem | 2 | 10000 | 20.727 | 3913.456 | 3934.182 | 3952.297 | 2541.8 |
| FileSystem | 2 | 100000 | 173.849 | 43514.984 | 43688.832 | 43732.994 | 2288.9 |
| MySQL | 1 | 1000 | 11.346 | 1061.537 | 1072.882 | 1073.312 | 932.1 |
| MySQL | 1 | 10000 | 29.336 | 11418.811 | 11448.147 | 11854.414 | 873.5 |
| MySQL | 1 | 100000 | 173.214 | 104845.812 | 105019.027 | 105850.935 | 952.2 |
| MySQL | 2 | 1000 | 8.218 | 1038.293 | 1046.512 | 1052.995 | 955.6 |
| MySQL | 2 | 10000 | 28.930 | 11183.946 | 11212.876 | 11218.041 | 891.8 |
| MySQL | 2 | 100000 | 167.251 | 105313.101 | 105480.352 | 105947.502 | 948.0 |
| SQLServer | 1 | 1000 | 13.037 | 1232.919 | 1245.956 | 1255.196 | 802.6 |
| SQLServer | 1 | 10000 | 23.970 | 12479.805 | 12503.773 | 12742.188 | 799.8 |
| SQLServer | 1 | 100000 | 179.190 | 130354.785 | 130533.976 | 131931.367 | 766.1 |
| SQLServer | 2 | 1000 | 8.711 | 1202.358 | 1211.068 | 1215.255 | 825.7 |
| SQLServer | 2 | 10000 | 21.338 | 12409.075 | 12430.413 | 12621.221 | 804.5 |
| SQLServer | 2 | 100000 | 166.758 | 130972.743 | 131139.502 | 132251.537 | 762.5 |

## Table 2 — catch-up after simulated retention gap

| backend | gap | catchup ms | records | events/sec |
|---------|----:|-----------:|--------:|-----------:|
| FileSystem | 1000 | 636.131 | 1000 | 1572.0 |
| MySQL | 1000 | 1133.792 | 1000 | 882.0 |
| SQLServer | 1000 | 1446.316 | 1000 | 691.4 |
