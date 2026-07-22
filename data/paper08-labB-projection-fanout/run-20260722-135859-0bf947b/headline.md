# Paper 8 — Lab B: projection fan-out (the observer authority) — headline

- Runtime: public Puppeteer `0bf947b` (Pacifico master b4eaa38, sanitized).
- Labs: `UnitTestChoreography/PaperLabs/paper8` (MSTest); the zeros are the claim.
- Run: 20260722-135859. Lab A real backends: SQL Server 2022 + MySQL 8.0 (Docker), verified round-trip.

ONE journaled fact, N distinct observers. The zeros are the claim.

| view | projection | domain methods added (separated) | domain methods added (fused) |
|---|---|---|---|
| fulfillment | @product, @units | 0 | 1 |
| finance (derived) | @product, @price*@units | 0 | 1 |
| catalog | @product, @price | 0 | 1 |
| **total** | — | **0** | **3** |

Sample derived projection (TOON): `product: "widget" / lineRevenue: 20`.
The derived value is computed in the projection, never a domain method.
Adding an observer grows the reaction layer, not the domain.

Scope: authoring locus + method count. NOT a cost/benefit measurement at scale.
