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
  Four bodies of literature — relational database theory, domain-driven design,
  REST API contract design, and Event Sourcing — independently document a
  recurring representational defect: schemas that mix unrelated concepts,
  fields populated for some rows but not others, contracts overloading
  heterogeneous use cases behind optional parameters, event payloads recording
  what was sent rather than what was used. Each tradition names the defect
  locally and prescribes a local remedy. This paper argues that the four are
  surface manifestations of a single structural defect, which we call
  *porosity*: the shape of a representation is dictated by what fits in the
  storage substrate rather than by the operations the representation describes.
  We further argue that *anti-porosity* — the principled rejection of porosity
  simultaneously across the three layers in which it manifests (domain,
  endpoint, persistence) — inverts the underlying assumption (domain models
  shaped for verbs rather than for storage) and is achievable when CQRS, the
  Actor Model, and Event Sourcing are combined under a single discipline. The
  Puppeteer framework, drawing on practical experience at Ncubo, serves as
  proof-of-existence.
canonical_url: https://[pending]/papers/anti-porosity-v1
---

# Anti-porous architecture

## TL;DR

> Architectures that shape their domain models to fit a storage substrate produce *porous* designs under structural pressure: rich object graphs — recursive, heterogeneous, with different node and edge types — do not survive a relational schema, so domain classes either fragment across joined tables that return empty cells or collapse into wide tables of redundant primitives; endpoint DTOs accumulate optional fields to serve heterogeneous use groups behind a single contract; and persistence layers record *what state exists* without recording *how it came to be*.
>
> This paper argues that these are surface manifestations of a single structural defect — *porosity* — that four bodies of literature (database theory, domain-driven design, REST API design, Event Sourcing) have each diagnosed locally without recognizing as one. We name the unified rejection *anti-porosity* and show that a CQRS + Actor Model + Event Sourcing combination, exemplified by the Puppeteer framework, makes anti-porosity achievable across all three manifestations. **Porosity is not a database problem, nor an API problem, nor an event-sourcing problem; it is a representational sparsity problem described independently in graph theory, information theory, database normalization, and storage engine theory.**

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

5. **The contribution is the cross-literature identification, not the implementation.** Four bodies of work — relational database theory, domain-driven design, REST API contract design, and Event Sourcing — have each documented surface manifestations of porosity in their own vocabularies, without recognizing the underlying unity. CQRS, the Actor Model, and Event Sourcing are individually well-known constituent patterns; what we claim as novel is neither the patterns nor the detection of any single porosity, but the observation that the four traditions converge on a single defect, that *anti-porosity* names its unified rejection, and that the rejection is implementable rather than merely conceivable. The Puppeteer framework is presented as proof-of-existence; the conceptual claim does not depend on it.

6. **CQRS + Actor + Event Sourcing is interesting here for density preservation, not for separation of concerns.** These three patterns are conventionally introduced as separations: reads from writes (CQRS), concurrent units (Actor Model), events from projections (Event Sourcing). When applied together as a single discipline, they constitute a mechanism that preserves density across the three representational layers in which porosity manifests. This interpretation is available only once porosity is named as a unified phenomenon across substrates.

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

## 1. Introduction: the invisible cost of porous designs

In 2005, the work that eventually became the Puppeteer framework began as something simpler: a wrapper around a DLL that exposed services in the then-emerging vocabulary of *web services*. While building this wrapper, an unexpected property of the construction emerged. By intercepting the parameters of every incoming call and writing them, in order, to a log — what would now be called a *journal* — the full state of the underlying automaton could be recovered without inspecting its code. Starting from any consistent prior state and replaying the log, the automaton arrived at exactly the same final state. Persistence, traditionally treated as a separately designed concern, had emerged as a side effect of the wrapper. The phenomenon was named *autopersistence*, after its observable effect rather than from any theoretical premise.

The discovery was at first taken as a clever optimization — a way to add persistence to a service without modifying its code. Subsequent experience sharpened the point. What the wrapper-log approach made visible was something the relational-persistence approach had hidden: the log was *dense* — it contained no empty fields, recording only what each call had actually used. The relational schemas the same applications wrote to, by contrast, were full of NULLs, columns that changed meaning by row, and joins that returned mostly-empty cells. In a later iteration of the framework, the journal stopped being a side effect and became a first-class design artifact: the constraint shifted from *"the journal exists"* to *"the journal must be readable, auditable, self-explanatory."* The journal was no longer a log of inputs received; it was a structured record of operations performed. One signature property of the original wrapper survived: the framework discovered domain types by reflection, so domain classes could describe the domain itself rather than the steps of writing it to a hard drive.

What the journal lacked, the relational schema produced systematically. The journal was *dense*; the schema was *porous*. We call this defect *porosity*: the shape of a representation is dictated by what fits in the storage substrate rather than by the operations the representation describes. Its visible symptom is widespread structural emptiness — fields without values, columns whose meaning depends on the row, queries returning rows with cells that the application never reads. The defect emerges most visibly when complex or polymorphic abstractions are forced into relational tables, and intensifies as the abstractions become richer.

Porosity has been largely invisible to the literature, but not because it is rare. It has, in fact, been documented repeatedly — under different names, in different traditions, by authors who did not recognize they were describing the same phenomenon. Database theory describes its surface effects as normalization-induced anomalies; domain-driven design describes them as persistence-driven anemic models; REST API design describes them as overloaded contracts and proliferating optional fields; Event Sourcing describes them as bloated event payloads coupled to commands. Each tradition names a local defect and prescribes a local remedy. None observes the underlying common cause. The vocabulary for that cause has not existed: developers do not say *this design is porous*; they say *this design is fine; the empty fields are normal.* Decades of relational-first modeling have made the trade-offs that produce porosity (over-normalization versus under-normalization, joins versus wide rows, NULLs versus duplicate tables) feel like inherent properties of software construction. They are not. They are the consequence of one architectural decision — that the domain model must be shaped for the database — repeated millions of times. Naming the phenomenon is the first step toward seeing it.

This paper argues that porosity is not inevitable, that the four traditions cited above describe surface manifestations of a single structural defect, and that their independent local remedies are partial solutions to a problem that admits a unified one. We name the unified rejection of porosity *anti-porosity*, and we describe how a CQRS + Actor Model + Event Sourcing combination, exemplified by the Puppeteer framework, sustains anti-porosity simultaneously across the three manifestations in which the defect appears (domain, endpoint, persistence). The paper is organized as follows. §2 enumerates three layers of porosity and argues that each is a face of the same defect. §3 introduces anti-porosity as a unified design principle. §4 describes the mechanisms by which Puppeteer realizes the principle. §5 reports operational experience drawn from production deployments. §6 addresses anticipated counter-arguments. §7 places this work in the context of related literature, and §8 concludes.

### 1.1 A formal characterization

*Porosity is not a database problem, nor an API problem, nor an event-sourcing problem. It is a representational sparsity problem described independently in graph theory, information theory, database normalization, and storage engine theory.*

The four formalisms that establish this characterization are summarized below; their intersection makes the term derivable rather than invented.

**Graph theory.** Domain models can be formalized as *semantic graphs*: nodes typed by entity class, edges typed by relation, with both nodes and edges potentially heterogeneous in type (Diestel, 2017). Polymorphism corresponds to heterogeneous node typing; recursion corresponds to self-referencing edge structure. The graph-database and Semantic Web literatures (Berners-Lee et al., 2001; Robinson et al., 2015) acknowledge this directly; the relational literature does not.

**Information theory.** Following Shannon (1948), a representation can be characterized by its *structural capacity* — the bits available to encode states — and its *informational content* — the bits actually used. When structural capacity exceeds informational content, the representation is *sparse*: most of its bits carry no information for any given instance. Empty fields, NULLs, and unused parameters are concrete realizations of representational sparsity in software systems. Unused parameters in a serialized event are, in the strict information-theoretic sense, *structural noise*; preserving only the consumed parameters is a noise-suppression operation, not a stylistic preference.

**Storage engines.** Each storage substrate exposes a *structural alphabet*: the set of shapes it can natively express (Hellerstein et al., 2007). Relational stores express rows × columns × types; document stores express nested trees of typed values; event stores express ordered logs of typed records; graph stores express labeled directed graphs. The alphabet of a substrate determines what can be written densely; whatever exceeds the alphabet must either be projected, with loss of structure, or padded, with empty cells. The relational case is particularly sharp: the SQL row is a projection onto a homogeneous vector space — every row a tuple of fixed dimension and uniform schema — and any semantic structure that does not fit (heterogeneous types, recursion, sparse attributes) is paid for in NULLs, joins, or schema duplication.

**Database normalization theory.** Codd's relational model (Codd, 1970), refined through successive normal forms (Codd, 1971; Fagin, 1977), is the canonical historical formalism for representing data in the relational alphabet. Normalization is prescribed to eliminate update, insert, and delete anomalies. The procedures that resolve these anomalies — splitting wide tables, introducing join columns, accepting NULLs in optional attributes — themselves produce the representational sparsity that, decades later, would be accepted in practice as the cost of doing software.

**Synthesis.** The four formalisms converge on a single definition:

> *Porosity is the architectural manifestation of representational sparsity that arises when a semantic graph is projected onto substrates whose structural alphabet exceeds the information actually required by operations.*

The dual:

> *Density preservation occurs when representations encode only the information actually consumed by operations, keeping structural capacity proportional to semantic content.*

Each phrase in these definitions is borrowed from one of the four formalisms; the contribution is the act of synthesis — observing that the formalisms converge on a defect that none of them names with the generality needed to see across substrates.

## 2. Three faces of porosity

### 2.1 Porosity in the domain layer

In object-oriented modeling, the canonical pattern for representing entities that pass through distinct lifecycle states is inheritance: each state is a subtype, with its own fields, invariants, and methods. A purchase order, for example, naturally decomposes into `Draft`, `Requested`, `Paid`, `Dispatched` — each subtype carrying only the attributes and operations that its state admits. The transition from one state to another is the construction of a new subtype instance, not the mutation of fields on a single class.

The relational model does not support this pattern cleanly. A type hierarchy can be projected onto a relational schema via one of three known idioms — single-table, class-table, or concrete-table inheritance (Fowler, 2002) — each of which produces porosity in a different form. In practice, single-table inheritance dominates: the schema collapses the four subtypes into one wide table with a `status` column and optional fields populated only for some rows. Attributes that belong logically to `Paid` are nullable for rows still in `Draft`; attributes that belong to `Dispatched` are nullable for everything else. The hierarchy is recovered at read time by inspecting the `status` field and conditionally interpreting the rest. The semantic graph — a clean polymorphic structure — has been projected onto a homogeneous vector space, and the gap between them surfaces as NULLs.

The choice is not driven by ignorance of the alternative. Developers familiar with object-oriented design recognize that inheritance is the structurally cleaner pattern; they adopt the property-field approach because the substrate makes it the path of least resistance. The other two idioms impose joins by type, schema duplication, and migration overhead severe enough to outweigh the modeling benefit. Porosity here is the signature of a forced compromise — not an absence of OOP wisdom, but a substrate that does not admit it.

The downstream effects extend beyond schema shape. State transitions on the porous table become updates against a row that other transactions may also read or modify; lock contention emerges as a structural cost of the design choice, not as an accident of high traffic. The broader observation — that a domain whose lifecycle was never logically concurrent now pays for transactional concurrency it did not request — is treated separately, as a distinct symptom of persistence-as-source-of-truth, in Paper 4.

### 2.2 Porosity in the endpoint layer

The endpoint layer's primary contractual artifact is the DTO — a fixed-shape tuple of fields defining what flows between client and server. When an endpoint serves a single, homogeneous use case, the DTO is well-defined. When it serves heterogeneous use groups behind a single endpoint, the DTO accumulates optional fields, parallel to the wide table with `status`-conditioned columns of §2.1. The structural defect is the same; only the layer differs.

Consider an endpoint `POST /orders` that serves both a customer self-checkout flow and an administrative batch-insertion flow. The customer flow specifies items, a delivery address, and a payment token; the administrative flow specifies override pricing, source attribution, and an internal correlation ID. A unified DTO carries the union of all fields, validates conditionally based on caller identity or feature flags, and provides no contract-level indication of which fields belong to which use group. Documentation shoulders the burden the schema cannot.

The defect manifests symmetrically on the response side. The same DTO that returns an order's full detail to a desktop client returns more than a mobile client requires; clients either over-fetch or fragment the operation into chatty round-trips, neither of which the original schema admits. The DTO, as a fixed-shape projection of the underlying entity, repeats the porosity of a homogeneous vector space at the level of the wire contract.

Costs accumulate at multiple levels. Validation logic becomes conditional. DTO families proliferate as variants are introduced to handle slightly different use groups. Client code absorbs out-of-band knowledge about which fields apply when. The contract — meant to be the disciplined surface between systems — becomes a porous one whose meaning depends on context the contract itself does not capture.

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
