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
  [PENDIENTE — 3 líneas: qué es la anti-porosidad,
  por qué importa, qué demuestra el paper.]
canonical_url: https://[pending]/papers/anti-porosity-v1
---

# Anti-porous architecture

## TL;DR

[PENDIENTE — 3–4 líneas. Ejemplo de tono:]

> Los sistemas DBMS-céntricos producen diseños porosos por presión estructural:
> tablas que mezclan conceptos, DTOs con campos opcionales, journals con campos
> vacíos. Este paper presenta la **anti-porosidad** como principio de diseño
> transversal — aplicable simultáneamente a dominio, endpoint y persistencia —
> implementado en el framework Puppeteer.

---

## Claims this paper makes

[PENDIENTE — bullets verificables. Borrador inicial:]

1. La porosidad es un anti-patrón observable en tres capas distintas que normalmente se tratan como problemas separados: dominio (tablas con NULLs), endpoint (DTOs con grupos de uso heterogéneos), persistencia (journals con campos no usados).
2. Los tres son síntomas de la misma presión estructural: la asunción tácita de que el modelo se diseña *para* la persistencia.
3. Un modelo basado en CQRS + Actor Model + Event Sourcing permite **rechazar la porosidad simultáneamente en las tres capas**, usando los mismos primitivos: clase por papel, DTO con parámetros opcionales filtrables, journal denso por construcción.
4. La implementación de referencia (Puppeteer) demuestra que el principio se puede sostener en producción a través de [N] dominios distintos durante [X] años.

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
