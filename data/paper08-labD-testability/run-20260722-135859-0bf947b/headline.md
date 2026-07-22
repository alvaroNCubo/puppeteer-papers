# Paper 8 — Lab D: testability as evidence (no port to mock) — headline

- Runtime: public Puppeteer `0bf947b` (Pacifico master b4eaa38, sanitized).
- Labs: `UnitTestChoreography/PaperLabs/paper8` (MSTest); the zeros are the claim.
- Run: 20260722-135859. Lab A real backends: SQL Server 2022 + MySQL 8.0 (Docker), verified round-trip.

The hard actor/assembler boundary is observable: a domain output test that
needs no destination is the proof the destination was never in the domain.

| approach | domain names an output port? | doubles a domain output test stands up |
|---|---|---|
| separated (this paper) | no (`print` knows no sink) | 0 |
| hexagonal / ports-and-adapters | yes (an injected interface) | >= 1 (the port) |

End-to-end pull with no sink bound: `{"total":35}`.
Inversion relocates a dependency; this removes it — injection presupposes a
thing to inject, and there is none.

Scope: the hard boundary only (actor/assembler). NOT a throughput measurement.
