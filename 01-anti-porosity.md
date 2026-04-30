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

[PENDIENTE — sección crítica para credibilidad. Candidatos a discutir:]

- Sistemas que requieren joins arbitrarios sobre datos heterogéneos (BI, data warehousing).
- Equipos donde la persistencia es contractualmente compartida con otros sistemas (data lake como source of truth multi-equipo).
- Cargas dominantemente analíticas con baja escritura (OLAP).
- Dominios donde el modelo cambia tan poco que la fricción del DBMS no es percibida como costo.

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
