# Lab 6 — latency budget (local FileSystem vs co-located RDBMS) — headline

- Runtime: Pacifico `99e8202` (built against the public mirror).
- Host: Microsoft Windows NT 10.0.26200.0 / 32cores / .NET 9.0.14
- Régime: compact-action journal, `CompiledModePolicy.AlwaysCompiled`, 1 event = 1 durable commit.
- N = 100000 measured appends per rep, K = 5 reps, warm-up = 1000 discarded.
- Measurement: public `PerformCommand` bracketed by `Stopwatch.GetTimestamp()` (QPC, ~100 ns).
- Run: 20260713-140150

## Table 1 — per-append latency by backend

| backend | samples | mean us | p50 us | p95 us | p99 us | max us | fsync mode |
|---------|--------:|--------:|-------:|-------:|-------:|-------:|------------|
| FileSystem | 500000 | 440.59 | 328.30 | 539.60 | 3434.00 | 308897.30 | Flush(flushToDisk:true) |
| MySQL | 500000 | 1073.56 | 1005.00 | 1536.10 | 1861.00 | 6947.70 | innodb_flush_log_at_trx_commit=1,sync_binlog=1 |
| SQLServer | 500000 | 1303.41 | 1169.60 | 1916.40 | 2478.80 | 60084.60 | default(full durability) |

## Table 2 — latency ratio (RDBMS / FileSystem)

| backend | p50 ratio | p95 ratio | mean ratio |
|---------|----------:|----------:|-----------:|
| MySQL | 3.06x | 2.85x | 2.44x |
| SQLServer | 3.56x | 3.55x | 2.96x |
