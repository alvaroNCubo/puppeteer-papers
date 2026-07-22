# Paper 8 — Lab A: sink-swap (the destination is the assembler's) — headline

- Runtime: public Puppeteer `0bf947b` (Pacifico master b4eaa38, sanitized).
- Labs: `UnitTestChoreography/PaperLabs/paper8` (MSTest); the zeros are the claim.
- Run: 20260722-135859. Lab A real backends: SQL Server 2022 + MySQL 8.0 (Docker), verified round-trip.

ONE actor, ONE projection script, N destinations. The zeros are the claim.

| destination | real backend | producer edits to bind it | projection delivered |
|---|---|---|---|
| SQL Server | yes (Docker) | 0 | identical rows |
| MySQL | yes (Docker) | 0 | identical rows |
| in-process sink | n/a | 0 | yes |
| (fused baseline) | — | >= 1 per sink | — |

Sample pushed document (TOON, default push format): `product: "widget" / units: 2`.
Format (TOON | JSON) is chosen outside the actor at `perf.OutputTarget(sink, format)`.
Pull (PerformQuery) returns the same projection to the caller and pushes nothing.

Scope: separability vs a fused baseline. NOT a cost/benefit measurement at scale.
