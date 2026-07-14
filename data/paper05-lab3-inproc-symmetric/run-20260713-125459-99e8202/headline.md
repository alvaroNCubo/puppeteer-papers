# Lab 3 — in-proc symmetric consumer — headline

- Runtime: Pacifico `99e8202` (built against the public mirror).
- Host: Microsoft Windows NT 10.0.26200.0 / 32cores / .NET 9.0.14
- N ∈ {100, 500, 1000} × 2 reps.
- Feed = public `MaterializeMirror.AsProgramMirror().Sync()`; reactions = public `.Program.Emit` / `.Metadata.Elide`; Emit captured via a public `IOutputSink`; elision via `actor.Introspection.ShowReaction`; journal parity via `journal_*.bin` byte compare.
- Run: 20260713-125459

**6/6 cells parity-clean** (0 Emit-byte diffs, 0 journal-segment byte diffs, 0 elide-state diffs).

## Table — parity per N

| N | reps | callback_diff_max | segment_diff_max | elide_diff_max | emit events |
|--:|-----:|------------------:|-----------------:|---------------:|------------:|
| 100 | 2 | 0 | 0 | 0 | 90 |
| 500 | 2 | 0 | 0 | 0 | 450 |
| 1000 | 2 | 0 | 0 | 0 | 900 |

> Caveat: the **Tell** terminator is out of scope — symmetric Tell on B would re-journal an envelope, breaking journal-segment parity. Emit + Elide are the follower-safe terminators measured here.
