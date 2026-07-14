# Lab 5 — offline operation (local buffer + async flush) — headline

- Runtime: Pacifico `99e8202` (built against the public mirror).
- Host: Microsoft Windows NT 10.0.26200.0 / 32cores / .NET 9.0.14
- N = 10000 online appends, M = 10000 partition appends, 2 reps, warm-up = 1000.
- Appends = public `PerformCommand`; buffer selected by the `localBufferPath=` connection key; progress observers via the internal Diary grant; zero-loss = public replica `PerformQuery` over the remote.
- Run: 20260713-135237

## Table 1 — per-append latency by cell × phase

| cell | phase | samples | mean us | p50 us | p95 us | p99 us | max us | events/sec |
|------|-------|--------:|--------:|-------:|-------:|-------:|-------:|-----------:|
| MySQL_direct | Online | 20000 | 3204.22 | 3094.10 | 4199.32 | 5110.54 | 18258.60 | 312 |
| MySQL_buffered | Online | 20000 | 573.76 | 515.60 | 668.20 | 3854.68 | 15738.60 | 1743 |
| MySQL_buffered | Partition | 20000 | 419.96 | 333.30 | 514.21 | 3219.66 | 12430.60 | 2381 |
| SQLServer_direct | Online | 20000 | 1327.07 | 1168.30 | 1933.41 | 2513.12 | 17305.50 | 754 |
| SQLServer_buffered | Online | 20000 | 538.21 | 429.30 | 632.42 | 3920.96 | 13518.30 | 1858 |
| SQLServer_buffered | Partition | 20000 | 417.11 | 339.40 | 525.83 | 3113.82 | 12220.30 | 2397 |

## Table 2 — buffer speedup (direct ÷ buffered, online p50)

| backend | direct p50 us | buffered p50 us | speedup |
|---------|--------------:|----------------:|--------:|
| MySQL | 3094.10 | 515.60 | 6.0x |
| SQLServer | 1168.30 | 429.30 | 2.7x |

## Table 3 — catch-up after reconnect (buffered cells)

| cell | backlog | drain sec | drain events/sec | primary | replica | zero loss |
|------|--------:|----------:|-----------------:|--------:|--------:|:---------:|
| MySQL_buffered | 10000 | 33.73 | 296 | 20999 | 20999 | yes |
| MySQL_buffered | 10000 | 33.81 | 296 | 20999 | 20999 | yes |
| SQLServer_buffered | 10000 | 20.09 | 498 | 20999 | 20999 | yes |
| SQLServer_buffered | 10000 | 19.32 | 518 | 20999 | 20999 | yes |
