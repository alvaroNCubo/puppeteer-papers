---
title: "Anti-porous architecture: a unified design principle for CQRS + Actor + Event-Sourcing systems"
author: Alvaro Rivera
affiliation: Ncubo
date: 2026-XX-XX
version: 0.1-draft
status: draft
keywords:
  - actor model
  - CQRS
  - event sourcing
  - anti-porosity
  - domain modeling
  - DSL design
  - puppeteer framework
abstract: >
  Systems combining CQRS, the Actor Model, and Event Sourcing are typically
  described as three independent design choices. This paper argues that the
  combination, when applied consistently, enables a unified design principle —
  *anti-porosity* — that rejects, in domain, endpoint, and persistence layers
  simultaneously, representations whose shape is dictated by the storage
  substrate rather than by the operations being described. Porosity is the
  structural defect that emerges when rich, possibly recursive object graphs
  are forced into impoverished persistence formats — relational tables, flat
  documents, or naïve event payloads — and propagates upward into endpoint
  contracts and the domain model itself. Anti-porosity inverts the assumption:
  domain models are shaped for the verbs that operate on them, and persistence
  captures those verbs rather than the resulting state. We describe the
  principle and how it is sustained in the Puppeteer framework, drawing on
  practical experience at Ncubo.
canonical_url: https://[pending]/papers/anti-porosity-v1
---

# Anti-porous architecture

## TL;DR

> Architectures that shape their domain models to fit a storage substrate produce *porous* designs under structural pressure: rich object graphs — recursive, heterogeneous, with different node and edge types — do not survive a relational schema, so domain classes either fragment across joined tables that return empty cells or collapse into wide tables of redundant primitives; endpoint DTOs accumulate optional fields to serve heterogeneous use groups behind a single contract; and persistence layers record *what state exists* without recording *how it came to be*.
>
> This paper introduces **anti-porosity** as a unified design principle that addresses all three layers simultaneously by inverting the underlying assumption: domain models are shaped for the verbs that operate on them, and persistence captures those verbs rather than the state they produce. We show that a CQRS + Actor Model + Event Sourcing implementation — exemplified by the Puppeteer framework — sustains this inversion. The contribution is the *transversality* of the principle; each individual layer has prior art.

---

## Claims this paper makes

1. **Porosity has three distinct surface manifestations.**
   - *Domain*: a class hierarchy whose shape is dictated by what fits in the storage substrate rather than by the operations the domain supports — losing references, recursion, and heterogeneous typing along the way. Two visible patterns: over-normalized schemas, where classes fragment across many tables joined by type and queries return rows of mostly-empty cells; and under-normalized schemas, where classes collapse into wide tables with naming ambiguity and primitive-level redundancy because complex objects must be flattened to fit a row.
   - *Endpoint*: a single API contract serving heterogeneous use groups via optional fields, with no contractual indication of which fields belong to which group.
   - *Persistence*: representations that capture *what state exists* without capturing *how it came to be* — a row of weights, but not the operations that produced them. Rich object graphs (recursive, heterogeneous, with different node and edge types) either fragment or flatten as above; document stores partially relieve this for tree-shaped data but degrade once recursion or cross-references appear; and naïve event sourcing recreates the defect at a different scale by serializing every input parameter regardless of which the execution consumed.

2. **The three are not independent problems.** Each is a downstream effect of the same upstream decision — modeling the domain *for* the storage substrate (primarily relational, but the same pattern partially recurs with document stores when recursion or cross-references are present). Once that decision is in place, every local fix recreates the pressure in a different layer.

3. **A combined CQRS + Actor Model + Event Sourcing implementation can reject all three porosities using a small, shared set of primitives.**
   - *Domain*: one class per role, with arbitrary references — including recursion and heterogeneous typing — permitted in the in-memory model. The persistence substrate no longer constrains the domain shape.
   - *Endpoint*: DTOs with filterable optional parameters; the same contract serves heterogeneous use groups without polluting the journal.
   - *Persistence*: dense journals of *verbs* rather than state. Each entry records a high-level operation designed by the programmer and exactly the inputs that operation consumed — not the edges of the object graph, not the input parameter set as received. The state, however rich, is reconstructed deterministically by replay.
   - The three mechanisms operationalize the same notion (*"what this operation actually used"*) at three layers.

4. **The principle is not theoretical.** The Puppeteer framework, the reference implementation described in this paper, has been used in production to build structurally distinct subsystems within the core of eCommerce and payments platforms — payment hubs, account-balance ledgers, KYC pipelines, customer-facing storefronts and experiences, and payment-processor integrations. Code references throughout this paper (Appendix A) point to the public repositories; quantitative empirical results are deferred to a forthcoming case-study white paper.

5. **The contribution is the unification, not the constituent patterns.** CQRS, the Actor Model, and Event Sourcing are well-documented individually, and each manifestation of porosity has prior treatment in DDD, API-design, and Event Sourcing literature. This paper claims neither novel patterns nor novel detection of any single porosity. The contribution is the recognition that the three layers express a single architectural decision and admit a single architectural response.

## When NOT to use this approach

The principle of anti-porosity has limits, and the implementation pattern that realizes it has narrower limits still. We name two regimes in which the implementation is not the right fit. In each case, anti-porosity may continue to apply as a diagnostic — porosity remains a real pathology — but the CQRS + Actor + Event Sourcing realization, as instantiated in Puppeteer, imposes costs that may not be repaid.

### A. Domains where the actor cannot achieve state locality

The actor model with journal-based persistence assumes that the working set of events required to rehydrate the actor's current state can be bounded. This holds when the actor's automaton passes through cyclic states — created, modifiable, finalized, archived — such that, at some point in the cycle, prior events become safely evictable without altering the current state. Domains where the actor's lifecycle has no such evictable point produce *unbounded rehydration*: every load replays a history that grows without ceiling.

Consider an airline flight. From the moment a schedule is published, an actor we may call `FlightOperation` accumulates events: the first booking, subsequent bookings, seat assignments, gate changes, boarding events, departure, landing. Throughout this sequence the state is genuinely active — answering *"is the flight bookable?"*, *"who is aboard?"*, *"has it departed?"* requires recent history. But once the flight has landed and reconciliation completes, the active state collapses: the flight no longer changes, and the remaining consumers are analytical (capacity-utilization reports, for instance) and can be served from periodic projections. At that point the entire history of bookings and gate changes can be marked evicted without compromising correctness. The actor has reached locality: its working set is bounded.

Compare this to an `Airline` actor that consolidates every flight, every passenger, every booking the airline has ever handled. Such an actor never reaches a "done" state. Its working set grows with operational history, and each rehydration replays material that has no bearing on any decision currently being made. The two designs share the same business domain; they differ in whether the actor's responsibility has a natural end.

The structural analogy with virtual memory is exact. In an operating system, a process exhibits *locality of reference* when its accessed pages stay within a bounded working set; the OS evicts pages outside that set without affecting correctness. When locality is lost — when references span the full address space uniformly — the system enters *thrashing*: pages are reloaded as fast as they are evicted, and useful work approaches zero. The actor model with a journal exhibits the same structural property: events take the role of pages, the actor's required history takes the role of the working set, and skips (event eviction) take the role of page replacement.

| Virtual Memory | Actor + Journal |
| --- | --- |
| Working set bounded | Non-skipped events bounded |
| Locality of reference | Locality of automaton state |
| Page eviction | Skip / event eviction |
| Page size | Actor granularity |
| Thrashing | Unbounded rehydration |

The locality property of an actor is, to a substantial degree, downstream of its naming. The word *actor* evokes *action*: a `Packer`, a `Dispatcher`, a `FlightOperation` — names that denote bounded responsibility with a natural end. Names that denote aggregations (*Inventory*, *Catalog*, *Customer*) imply unbounded responsibility, with no point at which prior events become evictable. A noun-aggregator actor is the structural equivalent of an OOP god class: state accumulates because nothing about the name suggests when it should stop. The same observation applies one level further down, to the partitioning of instances: an actor scoped per-country rather than per-shipment reproduces the problem even when the type is well-named. Granularity and partitioning are not separate decisions from naming; they are the same decision expressed at different levels.

Microsoft Orleans makes this granularity choice explicit in the very name of its activations — *grains* — emphasizing each as a small, self-contained unit. The naming heuristic developed here is consistent with that view and offers a verifiable test: if the proposed actor name is a noun-aggregator rather than a verb-doer, the design will likely fail at locality.

In typical production deployments, the cost of locality failure is partly amortized. Continuous-running datacenters confine rehydration to rare events — process restart, hardware failover — rather than steady-state operation; zero-downtime release patterns (developed in Paper 3) hide cold-start latency behind warm replicas during deployment; and journal storage is operationally cheap: it is economically negligible at relevant scales, and its access pattern — sequential append, with occasional sequential reads at rehydration — does not impose the random-I/O performance demands that drive RDBMS disk specifications. A Puppeteer deployment is largely indifferent to disk speed, where a comparable RDBMS workload is bound by it. Locality is therefore best understood as a structural assumption of the model rather than a property whose violation is immediately catastrophic. The "when not to use" case bites when locality fails **and** the amortizing properties above do not hold — for instance, single-instance systems with frequent restarts, or workloads where actor cardinality is so high that eviction-and-reload occurs as part of normal operation.

The mechanism by which Puppeteer evicts journal events — the *skip* — is treated in detail in a companion paper (Paper 3, on zero-downtime deployment via journal-based state handoff). For present purposes it suffices to note that skips presuppose locality: the framework can implement skips, but it cannot manufacture locality where the actor's responsibility is unbounded. Locality is a property of the design, not of the framework.

### B. Domains where the actor's verbs cannot be kept fast

Each actor processes its mailbox serially: only one verb executes at a time on a given actor instance. The framework sustains thousands of requests per second per actor, at peak, on the assumption that every verb completes within a few milliseconds. A verb that blocks for hundreds of milliseconds — synchronous I/O to a slow service, an expensive query, a costly computation — does not merely slow itself; it blocks every subsequent verb in the mailbox, and ingest throughput collapses. The single-thread invariant of the actor turns from a correctness guarantee into a throughput cap.

Two patterns sustain the fast-verb invariant in practice. **I/O hoisting**: the caller resolves external dependencies — third-party calls, slow lookups, expensive joins — before invoking `perform`, and passes the resolved values as parameters. The actor receives data; it does not fetch. **Saga-phased I/O**: when the workflow genuinely requires interleaved external calls, the work is decomposed into a Saga. The actor advances one phase, releases, an external coordinator performs the I/O, and the response re-enters the actor as the next event. The actor never waits inside a single verb.

Where neither pattern applies — synchronous I/O is intrinsic to the verb, or the underlying algorithm is unavoidably slow — the actor's serial processing is structurally incompatible with the workload. In those cases the framework's claim of high per-actor throughput cannot be honored.

In both regimes, the principle of anti-porosity may continue to apply as a diagnostic; what fails is the realization, not the principle.

---

## 1. Introducción: el costo invisible de los diseños porosos

[PENDIENTE]

Esquema:
- Definir "porosidad" en un sentido estructural: presencia sistemática de huecos no informativos en una representación (campos NULL, parámetros no usados, columnas que cambian de significado).
- Por qué la porosidad es invisible: se acepta como "el costo de hacer software".
- Tesis: la porosidad no es inevitable; es síntoma de una decisión arquitectural reversible.

## 2. Tres caras de la porosidad

### 2.1 Porosidad en el dominio: tablas porosas

[PENDIENTE — desarrollar el argumento de "ahorrar tablas"]

- DBMS empuja a consolidar conceptos relacionados en una sola tabla.
- Resultado: columnas opcionales con NULLs, nombres ambiguos, joins repetidos.
- Ejemplo concreto: [pendiente — tomar un caso real de Ncubo, anonimizado].

### 2.2 Porosidad en el endpoint: DTOs heterogéneos

[PENDIENTE]

- Mismo endpoint sirve grupos de uso distintos (admin vs cliente, distintos features).
- Solución habitual: un DTO con todos los campos, validación condicional.
- Resultado: el contrato del endpoint no documenta qué grupo usa qué campos.

### 2.3 Porosidad en la persistencia: journals con huecos

[PENDIENTE]

- Event sourcing naïve serializa todos los parámetros recibidos.
- Eventos en el journal cargan campos que esa ejecución particular no usó.
- Replay y auditoría se vuelven más difíciles.

## 3. El principio unificado: anti-porosidad

[PENDIENTE]

- Definición: una arquitectura es **anti-porosa** cuando su representación, en cada capa, contiene exactamente la información usada por la operación correspondiente — ni más, ni menos.
- Tres condiciones que un diseño debe satisfacer simultáneamente:
  1. Una clase por papel (dominio).
  2. DTOs filtrables por grupo de uso (endpoint).
  3. Journal denso (persistencia).
- Por qué no se logra parche por parche: las tres son la misma decisión vista desde tres ángulos.

## 4. Mecanismos en Puppeteer

### 4.1 Roles, no conceptos: la biblioteca como puppet

[PENDIENTE]

- El dominio se parte por papeles que actúan, no por sustantivos del negocio.
- Las clases del dominio (la "biblioteca" / puppet) son conceptos puros sin saber en qué obra van a aparecer.
- Herencia y polimorfismo permiten "una camisa a la medida" por papel.

Referencias de código:
- `Actor.cs:56,83` — descubrimiento de puppets vía `IsSubclassOf(typeof(Objeto))`.
- `Expresion.cs:246` — compatibilidad polimórfica argumento↔parámetro.
- `TablaDeSimbolos.cs:61` — sustitución de subtipos en runtime.

### 4.2 El parámetro opcional `?` en el DSL

[PENDIENTE]

- Sintaxis: parámetros marcados como opcionales/`Out`.
- Comportamiento: el script recibe todos los valores entrantes pero el journal solo retiene los consumidos.
- Resultado: mismo endpoint, mismo DTO, persistencia diferenciada por ejecución.

Referencias de código:
- `Parameters.cs:688-691` — serialización: `?` en lugar de valor para parámetros `Out`.
- `Parameters.cs:713-722` — deserialización: `?` produce default value del tipo.
- `Lexer.cs:584` — `?` como token de primera clase.

### 4.3 Journal denso por construcción

[PENDIENTE]

- Cada entrada del journal es exactamente lo que la ejecución usó.
- Ventaja para replay determinista.
- Ventaja para auditoría: un evento en el journal es self-describing.

## 5. Resultados empíricos

[PENDIENTE — depende del white paper de caso de estudio Ncubo]

Borrador:
- N dominios verticalmente distintos en producción (crypto, gaming, loyalty, KYC, cashier, lotto).
- ~83 clases Actor.
- Tiempos de respuesta sub-100ms como norma operativa.
- [Pendientes: métricas concretas a obtener del caso de estudio.]

## 6. Counter-arguments

[PENDIENTE — incluir y refutar honestamente]

Posibles objeciones y respuestas:
- *"Esto es solo CQRS bien hecho."* — Respuesta: la novedad es el principio transversal, no cada capa por separado.
- *"En sistemas pequeños la porosidad no duele."* — Respuesta: cierto; el costo se vuelve visible a partir de cierta escala/longevidad.
- *"Los joins en el modelo poroso son a veces necesarios."* — Respuesta: el modelo anti-poroso no prohíbe joins; los traslada al lado de lectura (CQRS).

## 7. Related work

[PENDIENTE — bibliografía estructurada]

- Hewitt, C. *Actors: A Model of Concurrent Computation in Distributed Systems.* MIT, 1973.
- Young, G. *CQRS Documents.* 2010.
- Fowler, M. *Event Sourcing.* martinfowler.com, 2005.
- Evans, E. *Domain-Driven Design.* Addison-Wesley, 2003.
- [Pendiente: Akka, Orleans, Proto.Actor para diferenciación.]

## 8. Conclusión

[PENDIENTE]

- Recapitulación: una sola decisión arquitectural rechaza la porosidad en tres capas.
- Implicación: simplifica el stack (cap. siguiente paper sobre Redis-as-symptom).
- Trabajo futuro: medición empírica del impacto en productividad de equipos mixtos (paper sobre dev experience).

## Cross-references

Este paper establece vocabulario que los siguientes papers de la serie reutilizan:
- *Paper 2 — Dual compilation:* el principio anti-poroso en el momento del compile vs. interpret.
- *Paper 3 — Zero-downtime deployment:* la densidad del journal es prerrequisito para skips y rojo-negro.
- *Paper 4 — Why Redis is a symptom:* extiende el argumento de anti-porosidad al ecosistema de infraestructura.

---

## Bibliografía

[PENDIENTE — formato a definir: APA, ACM, o markdown plano con DOIs/URLs]

---

## Appendix A: Verificación en código

| Claim | Archivo | Líneas |
|---|---|---|
| `?` para parámetros `Out` en serialización | `Puppeteer Pacifico/Puppeteer/Parameters.cs` | 688–691 |
| `?` triggering default value en deserialización | `Puppeteer Pacifico/Puppeteer/Parameters.cs` | 713–722 |
| `?` como token del lexer | `Puppeteer Pacifico/Puppeteer/EventSourcing/Interprete/Lexer.cs` | 584 |
| Polimorfismo en compatibilidad arg↔param | `***REDACTED***/Puppeteer/EventSourcing/Interprete/Libraries/Expresion.cs` | 246 |
| Polimorfismo en tabla de símbolos | `***REDACTED***/Puppeteer/EventSourcing/DB/TablaDeSimbolos.cs` | 61 |
| Descubrimiento de puppets por subtipo | `***REDACTED***/Puppeteer/EventSourcing/Actor.cs` | 56, 83 |

[PENDIENTE — completar con referencias adicionales según se desarrolle el paper.]
