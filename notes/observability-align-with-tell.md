# Alinear la observabilidad con `tell` — nota para el chat de observabilidad

Propósito: reorientar el trabajo de observabilidad que se desarrolló con énfasis en W3C.
**No se descarta W3C.** Se lo re-ubica una capa abajo: la identidad causal del propio `tell`
(durable, en el diario) es el *modelo*; W3C/OTLP/Datadog es un *target de rendering* de ese modelo,
no el vocabulario primario ni un rival.

Verificado contra `Puppeteer Pacifico` (master) y el mirror público, julio 2026.

---

## 1. La corrección de encuadre (el sesgo a arreglar)

Frase original (sesgada hacia rivalizar con W3C):

> "encuadrar el tema como *APM / observabilidad* arrastra W3C como respuesta por defecto"

Reformulación (anillo al dedo de observar `tell`, sin contradecir W3C):

> El encuadre "APM/observabilidad" hace que **W3C aparezca como el modelo primario**, porque
> `traceparent` es la respuesta genérica a *observar cualquier mensaje en cualquier transporte*. La
> corrección no es descartar W3C, sino **invertir la capa**: la **identidad causal del `tell`**
> (durable, journaled) es el modelo, y **W3C es uno de sus targets de rendering**. Observabilidad
> *tell-native* = observar exactamente los actos que Paper 4 define — assertion, uptake, ack, hop,
> saga-step — *keyed por la identidad del propio tell*; W3C se monta encima sin rivalizar, porque
> vive en otra capa y sirve a otro amo (monitoreo en vivo, no el registro-de-verdad).

---

## 2. Por qué — el paradigma de Paper 4

- **C2 (causación como sentencia de programa).** La arista cruzada es una sentencia en el diario del
  emisor, no un artefacto de infraestructura. → **la fuente de verdad de la cadena es el diario**, no
  el trace.
- **§6.2 (distributed tracing).** El tracing reconstruye la causación *desde afuera de todo programa*
  y es **efímero**. Si la observabilidad de `tell` tomara W3C como *fuente*, reinstauraría la
  asunción que §3 nombra y el paper disuelve. Por eso W3C tiene que ser **vista/rendering, nunca
  verdad**.
- **§8.5 (multi-hop).** El identificador causal es **per-hop-local, no un chain id hilvanado**.
  Recomponer A→B→C→D = **componer los diarios por edge** siguiendo el id per-hop, no filtrar por un
  `traceId` plano único.
- **Paralelo con el routing (la analogía clave).** El `tell` nombra el **rol** del destinatario; un
  *binding* de deployment resuelve el transporte, fuera del programa. La observabilidad debe
  espejar esto: **nombra el acto causal (tell-native); el binding APM/W3C lo renderiza.**
  > La observabilidad es a la cadena causal lo que el binding de transporte es al routing.
- **Ventaja que solo el modelo tell-native da:** una traza **reconstruible después del hecho desde el
  diario** (replay-reproducible). Una traza W3C ya no existe cuando terminó el request. Esto no es un
  downgrade del APM — es una capacidad que W3C solo no tiene.

---

## 3. El modelo en dos capas

| Capa | Qué es | Propiedades | Ejemplo |
|---|---|---|---|
| **1 — Verdad (tell-native)** | diario + identidad del `tell` | durable, programática, replay-reproducible | `envelope.Id` (once-key/content-hash), `CausalEventId`, `ReactionName`, addressee role, message name, saga instance key |
| **2 — Rendering (opcional)** | spans / OTLP / Datadog / Jaeger / W3C | efímera, para el operador en vivo, sembrada por capa 1 | waterfall A→B→C→D en Datadog |

W3C es un **adaptador de rendering**. El seam ya existe: `SpanFactory.StartFromContext(traceContext)`
→ `ActivityFlowTrace.StartSpan(..., parentContext)` que parsea W3C con `ActivityContext.TryParse`
(hoy **sin llamadores**). Ése es el lugar correcto para el puente W3C — no el camino primario.

---

## 4. Estado actual del código (verificado @ `becf8e7`, POST-MERGE) — qué hay y qué falta

> ACTUALIZADO tras el push `2f7d5e1..becf8e7`. La versión previa de esta sección se escribió
> pre-merge (decía que el transporte no inyectaba `traceparent`, que `StartFromContext` no tenía
> llamadores y que `CausalEventId` estaba en `null`) — **todo eso cambió con el merge.**

**Capa 1 (ancla causal) — MERGEADA** (`becf8e7` = *"Merge tell causal-provenance anchor…"*):
- `TellEnvelope` lleva `Id`, `Addressee`, `AddresseeInstanceId`, `MessageName`, `Check`, `Values`,
  y ahora **`CausalEventId` + `ReactionName` poblados** (`TellEnvelope.cs:29-30`).
- `BuildEnvelope` (`TellStatement.cs:437-438`) estampa
  `CausalEventId: SymbolTable.CurrentCausationCausalEventId` (id de la entry que disparó la Reaction) y
  `ReactionName: SymbolTable.CurrentCausationReactionName`. El back-reference causal hop-a-hop ahora es
  **explícito** — en el envelope/wire, no (aún) en la sentencia journaled del emisor.
- **`envelope.Id`** (once-key/content-hash) sigue siendo la correlación **durable journaled** HOY
  (Paper 4 entry `[4] ... once 'ord-100'`, ack `[5]` correlaciona por ella) — el link cross-journal.

**Capa 2 (rendering W3C) — MERGEADA como baseline:**
- `TellEnvelope` lleva `TraceParent`/`TraceState`, **wire-only, nunca journaled/replayed** (comentario
  en código `TellEnvelope.cs:45-49`: *"NEVER journaled, NEVER replayed: the journal records the domain
  fact alone, exactly as it never records ip/user/offset"*).
- `BrokerTellTransport.SendAsync` **SÍ inyecta** `traceparent`/`tracestate` desde `Activity.Current`
  (`TraceContext.TryCaptureAmbient`), no-op sin tracing (`BrokerTellTransport.cs:96-106`).
- `ToldListener` **SÍ abre** span `Told.Uptake` re-parenteado del header (`ToldTracer.StartUptakeSpan(...,
  rt.TraceParent)`); `DispatchTracer`/`ToldTracer` llaman `SpanFactory.StartFromContext` (antes sin
  llamadores). → capa-2 shipeada como baseline; la corrección es **re-scopearla como adaptador
  etiquetado**, no camino primario.

**Falta (lo que sigue del plan):**
- **Journaling emisor**: decidir si `CausalEventId` entra además en la sentencia journaled del emisor.
  *Decisión de paper (anti-porosidad) — recomendación en `notes/paper04-v0.2-observability-tracing.md`:
  NO journalar (recomputable por replay; el link durable ya es `envelope.Id`).*
- **Formato** del `CausalEventId`: hoy raw entry id. §8.5 → per-hop-local basta; sin prefijo actor/rol.
- **Vocabulario tell-native de spans** (assertion/uptake/ack/hop/saga-step) keyed por `CausalEventId`,
  no por el `traceparent` W3C (hoy `Told.Uptake` cuelga de W3C).
- **Herramienta de recomposición** offline/replay-driven (entregable central, sin empezar).
- **Naming legible** que aparte el puente W3C como capa de rendering.

---

## 5. Correcciones concretas para alinear (acción para el otro chat)

1. **Poblar la identidad causal en `BuildEnvelope`** (`TellStatement.cs:377-378`):
   `CausalEventId` = id de la entrada de diario que **causó** el tell (la entry que matcheó la
   Reaction); `ReactionName` = nombre de la Reaction. Esto hace explícita la arista causal que hoy se
   infiere, y es **el ancla de toda la observabilidad tell-native**. (Confirmar que se journalea del
   lado del emisor, no solo en el envelope de wire.)
2. **Vocabulario de observabilidad tell-native** — los "métodos legibles que orientan cuál es nuestro
   W3C": una API de spans/tags cuyo vocabulario sea el del paper —
   **assertion / uptake / ack / causal-edge / hop / saga-step** — keyed por
   (`envelope.Id`, `CausalEventId`, addressee role, message name, saga instance key). NO "genérico de
   cualquier mensaje en cualquier transporte".
3. **Spans por acto, linkeados por causal id** — assertion span en el emisor, uptake span en el
   receptor, ack span en el round-trip, saga-step span; linkeados **hop-a-hop** por
   `CausalEventId`/`envelope.Id`. **Sin** exigir un `traceId` W3C plano único.
4. **Puente W3C como adaptador opcional** — si hay/quiere contexto W3C, derivarlo o cargarlo y
   renderizar los spans tell-native al backend vía `StartFromContext`. W3C **nunca** es la fuente de
   verdad; es el rendering. Marcarlo legiblemente (p.ej. un `ToW3CContext()` / `FromW3CContext()`
   explícitamente etiquetado como rendering).
5. **Herramienta de recomposición offline / replay-driven** — lee N diarios, une por
   `envelope.Id`/`CausalEventId` → cadena A→B→C→D y corridas de saga (instance key). Reproducible
   desde estado durable, a diferencia de una traza W3C. **Éste es el entregable central**; el
   waterfall APM en vivo es una proyección opcional sobre la misma key.
6. **Naming legible ("nuestro W3C" en términos de tell")** — exponer la identidad causal con nombres
   de dominio (`CausalEdge`, `Hop`, `Utterance`, `CausalEventId`) y dejar el adaptador W3C claramente
   apartado como capa de rendering. Que el código *deje leer* qué es la correlación tell-native y
   dónde empieza el puente a W3C.

---

## 6. Qué NO hacer

- **No** hacer de `traceparent`/W3C la **fuente de verdad** de la recomposición (efímero, se pierde en
  replay, reconstruye desde afuera del programa = la asunción §3 que el paper disuelve).
- **No** hacer la observabilidad *transport-generic* ("cualquier mensaje en cualquier transporte")
  cuando el target es específicamente `tell` / cadenas causales / sagas.
- **No** exigir un `traceId` plano único; la identidad causal es **per-hop** (§8.5).
- **No** meter el trace context en el dominio/diario como coordinación. Distinción fina: el
  `CausalEventId` **sí** es parte del registro causal (va en diario/envelope, es programa); el
  **trace context W3C no** (es efímero, wire-only, rendering). El envelope/headers/W3C viven fuera del
  vocabulario de dominio, igual que el transporte.

---

## 7. Cómo queda en armonía con W3C (lo pedido)

- W3C sigue **soportado** y es un rendering válido: un operador puede tener su waterfall en
  Datadog/Jaeger.
- La única diferencia es **cuál capa es autoritativa**: la identidad del `tell` (durable) es la
  fuente; W3C es la proyección. **No rivalizan** porque operan en capas distintas y sirven a amos
  distintos — auditoría/replay (diario) vs monitoreo en vivo (APM).
- El modelo tell-native, además, **alimenta** a W3C: el mismo `CausalEventId` que recompone la cadena
  offline puede sembrar los span-links del waterfall en vivo. Un solo modelo de identidad, dos
  rendimientos.

---

## 8. Conexión con Paper 4 (para la nota de §6.2)

Esto confirma y afila §6.2: la key causal durable vive en el diario (programática, replay-reproducible);
el APM es una **proyección efímera** sobre ella. Observar `tell` vía W3C-como-fuente sería reinstaurar
la reconstrucción-desde-afuera que el paper nombra — mientras que observar `tell` vía su identidad
causal journaled *es* leer el programa. Candidato a nota de §6.2 (paralela a la de C3):
`notes/paper04-v0.2-c3-assertive-imperative.md`.
