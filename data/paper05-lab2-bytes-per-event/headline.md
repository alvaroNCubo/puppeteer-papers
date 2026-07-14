# Lab 2 — Bytes-per-event compact format — headline

**Runtime**: public commit `99e8202` (mirrors Pacifico `4a852ba`), built against the public mirror.
**Date**: 2026-07-13
**Host**: Windows 11 Pro 10.0.26200 / 13th Gen Intel i9-13900 (32 logical cores) / .NET 9.0.14
**Configuration**: Release / x64 / single-process, in-memory encoder (no I/O)

## Headline number

Across three verb tiers — arithmetic-shallow, branching-arithmetic-medium, and a
production-shaped synthetic verb — encoding events as `ActionId + parameters`
(compact) rather than `script DSL + parameters` (literal) yields bytes-per-event
of **33.0, 33.0, and 96.7** vs **118.0, 150.0, and 910.7**. The density ratio
grows from **3.6× at tier 1** to **9.4× at tier 3**, confirming that compaction is
structural — proportional to verb richness — not a constant. Applying `gzip` to
the literal form closes the gap only to **4.0× at tier 3** (gzipped literal
387.3 B vs compact 96.7 B): the densification is by construction (the verb name
plus its argument tuple), not by redundancy a general-purpose compressor recovers.

The Action definition is journaled **once** (definition_bytes_once: 144 / 176 /
909 B per tier); every subsequent invocation pays only the compact per-event cost.

## Table — bytes/event (n = 1000)

| tier | label | compact | literal (none) | literal (gzip) | ratio none | ratio gzip |
|-----:|-------|--------:|---------------:|---------------:|-----------:|-----------:|
| 1 | arithmetic-shallow        | 33.00 | 118.00 | 106.27 | 3.58× | 3.22× |
| 2 | branching-arith-medium    | 33.00 | 150.00 | 129.53 | 4.55× | 3.93× |
| 3 | production-verb-synthetic | 96.67 | 910.67 | 387.25 | 9.42× | 4.01× |

Full per-(tier, n, format) rows in `summary-full.csv`; per-event samples in
`samples.csv`; definition sizes in `definitions.csv`.
