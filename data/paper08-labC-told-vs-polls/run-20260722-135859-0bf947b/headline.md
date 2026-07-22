# Paper 8 — Lab C: told vs polls (the observer's direction) — headline

- Runtime: public Puppeteer `0bf947b` (Pacifico master b4eaa38, sanitized).
- Labs: `UnitTestChoreography/PaperLabs/paper8` (MSTest); the zeros are the claim.
- Run: 20260722-135859. Lab A real backends: SQL Server 2022 + MySQL 8.0 (Docker), verified round-trip.

One fact, one observation, two directions. The zeros are the claim.

| direction | mechanism | producer domain methods for delivery |
|---|---|---|
| polls (pull) | PerformQuery on the producer | 0 |
| told (push) | a Reaction's Causation.Continue(tell) | 0 |

Polled value: `{"amount":100}`. Told envelope args: `100`.
Told and polls reach the identical observation; the direction is chosen
outside the actor, and the producer names no observer and no transport.

Scope: authoring locus of the delivery direction. NOT a throughput measurement.
