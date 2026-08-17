# Paper 4 → v0.2 notes: §6.2 / §8.5 after the causal-anchor + W3C merge

Estado: Paper 4 v0.1 está publicado; esto es colección de cambios para v0.2 (no editar el .md aún).
Origen: el chat de observabilidad mergeó y pusheó `2f7d5e1..becf8e7`. Verificado contra
`Puppeteer Pacifico` @ `becf8e7`, julio 2026.

Ver también: `notes/paper04-v0.2-c3-assertive-imperative.md` (C3).

---

## Estado del código verificado @ becf8e7 (corrige el §4 que circuló)

**Capa 1 (ancla causal) — MERGEADA** (el commit `becf8e7` = *"Merge tell causal-provenance anchor
for tell-native observability"*; el §4 que circuló la daba como "en branch, NO mergeado" — está
mergeada):
- `TellEnvelope` (`TellEnvelope.cs:29-30`) ahora lleva `CausalEventId` y `ReactionName` como
  campos poblados.
- `BuildEnvelope` (`TellStatement.cs:437-438`) estampa
  `CausalEventId: SymbolTable.CurrentCausationCausalEventId` (= id de la entry que disparó la Reaction)
  y `ReactionName: SymbolTable.CurrentCausationReactionName`.
- `envelope.Id` (once-key/content-hash) sigue siendo la correlación durable journaled; `CausalEventId`
  es el back-reference causal per-hop, hoy **en el envelope/wire**, no en la sentencia journaled.

**Capa 2 (rendering W3C) — MERGEADA como baseline:**
- `TellEnvelope` lleva `TraceParent`/`TraceState` (`TellEnvelope.cs:45-49`), con este comentario en el
  código (que es, literal, la tesis del paper):
  > *"NEVER journaled, NEVER replayed: the journal records the domain fact alone, exactly as it never
  > records ip/user/offset. Null when [tracing off]."*
- `BrokerTellTransport.SendAsync` inyecta `traceparent`/`tracestate` desde `Activity.Current`
  (`TraceContext.TryCaptureAmbient`), no-op sin tracing (`BrokerTellTransport.cs:96-106`).
- `ToldListener` abre span `Told.Uptake` re-parenteado del header (`ToldTracer.StartUptakeSpan(...,
  rt.TraceParent)`); `DispatchTracer`/`ToldTracer` llaman `SpanFactory.StartFromContext` (antes sin
  llamadores).

Punto clave para el paper: **el mismo `tell` transporta ambos** — la sentencia de dominio (journaled,
durable) y el trace context W3C (wire-only, jamás journaled ni replayed). El código ya encarna la
separación de capas que §6.2 argumenta.

---

## Cambio 1 — §6.2 (Distributed tracing): instancia auto-demostrativa *(recomendado, chico, en scope)*

§6.2 ya argumenta que el trace se construye "from artifacts that exist outside the participating
actors' programs … metadata threaded through messages by middleware" y que "a trace records that a
span occurred; a program records what was said and why." El merge le da una **instancia concreta en el
propio artefacto del paper**: el primitivo `tell` puede llevar un `traceparent` W3C en el mismo
envelope, y el framework lo mantiene **wire-only, nunca journaled ni replayed** — mientras la aserción
sí queda como sentencia en el diario.

Insert sugerido (footnote al final de §6.2, o 2-3 oraciones tras el párrafo "lossy"):

> *La instantiation de §8 exhibe la separación en un solo primitivo: un `tell` puede portar un contexto
> de traza W3C (`traceparent`/`tracestate`) en el mismo envelope que lleva la aserción, y el runtime lo
> mantiene deliberadamente fuera del diario — nunca journaled, nunca replayed, "exactamente como nunca
> registra ip/usuario/offset". La aserción sobrevive al replay porque es programa; la traza no, porque
> es el aparato externo que esta sección describe. Los dos coexisten sobre el mismo mensaje sin ser
> equivalentes: uno es el registro programático de la causación, el otro su render efímero para
> observabilidad en vivo.*

Convierte §6.2 de una afirmación general en una con instancia que se auto-demuestra, **sin** volver el
paper un paper de observabilidad. Refuerza, no expande el alcance.

**Dependencia de provenance:** el paper fija un commit (`37ad9cf`/README, lab04 `6a330b0`); el código
W3C es posterior (`becf8e7`). Si el insert va como **conceptual/footnote**, no necesita citar código.
Si se quiere afirmar como hecho del artefacto ("el envelope lleva `TraceParent` wire-only"), hay que
**bumpear el commit de provenance** a ≥ `becf8e7` y, si se exhibe, extender lab04. Recomiendo la vía
conceptual para v0.2 salvo que ya se vaya a re-anclar el lab.

---

## Cambio 2 — §8.5: el "causal identifier per-hop-local" ahora tiene referente *(mínimo, opcional)*

§8.5 dice: *"the envelope's causal identifier is per-hop-local, not a threaded chain id"* y que un
multi-hop se reconstruye *"composing A's and B's journals (linkable by envelope identifier across
them)."* Ambas siguen correctas y **no requieren cambio de prosa**:
- "linkable by envelope identifier" = `envelope.Id` (once-key), que **sí** está journaled — el link
  cross-journal durable. Mantener así.
- El nuevo `CausalEventId` es el back-reference **intra-journal** (tell → entry que lo causó),
  per-hop-local — encaja con la frase, pero **no** es el link cross-journal.

Opción (solo si se bumpea provenance): una footnote aclarando que el runtime materializa ese
identificador per-hop como `CausalEventId` en el envelope. Si no se bumpea, **no tocar §8.5**.

---

## Decisiones de paper a registrar (las dos marcadas "decisión de paper")

**(a) ¿`CausalEventId` entra en la sentencia journaled del emisor? → Recomendación: NO.**
- El principio de §8.2 ya lo dicta: la sentencia registra "only what that actor could itself have
  said" y no nombra transporte. El Seller *no dice* "…causado por la entry 2"; el id de entry es un
  artefacto mecánico, no algo que el actor asevera. Meterlo sería porosidad (filtrar el direccionamiento
  interno del diario en la sentencia de dominio).
- Es **recomputable por replay**: replay re-ejecuta la Reaction (cuya definición está journaled), que
  re-matchea la entry disparadora → el link causal ya está en el diario *estructuralmente*, sin
  almacenarlo como dato. Journalar-lo sería materializar estado derivado.
- No hace falta para la reconstrucción de §8.5: el link cross-journal durable es `envelope.Id`
  (journaled). `CausalEventId` es conveniencia de la **capa de rendering** (que un consumidor en vivo
  tenga el padre sin replayar).
- Consistente con la decisión que los devs YA tomaron para el análogo (trace context): "never
  journaled". `CausalEventId` cae del mismo lado para la *sentencia*; vive en el envelope.
- Corolario para v0.2: **ningún cambio a §8.2** — su redacción actual ya implica la decisión. Solo
  registrarla explícitamente en el changelog de diseño.

**(b) Formato de `CausalEventId` (raw entry id vs prefijar actor/rol) → raw entry id basta.**
- §8.5 dice per-hop-local; la unicidad cross-actor la da `envelope.Id`, no `CausalEventId`.
- El consumidor en vivo ya conoce el actor emisor por el contexto del span. Sin prefijo. Registrar.

---

## Guarda de alcance + dependencia de provenance (resumen)

- Paper 4 está scopeado a *flow-location*. Estos son refuerzos chicos a §6.2/§8.5, **no** una sección
  de observabilidad nueva. No importar el modelo de spans, ToldTracer, ni W3C-plumbing al paper.
- Nada citable como "instantiation" hasta bumpear el commit de provenance a ≥ `becf8e7` (y, si se
  exhibe, extender lab04). Para v0.2, preferir insert conceptual en §6.2.
- Re-scope de la capa-2 (W3C como adaptador etiquetado, no camino primario) es trabajo del **runtime/
  otro chat**, no del paper. El paper solo observa que trace = capa de render; no prescribe el API.

## Checklist v0.2

- [ ] §6.2: footnote/oraciones de la instancia auto-demostrativa (conceptual; o factual si se bumpea provenance).
- [ ] §8.5: sin cambio de prosa; footnote opcional solo si se bumpea provenance.
- [ ] §8.2: sin cambio; registrar la decisión (a) — `CausalEventId` NO journaled.
- [ ] Registrar decisión (b) — formato raw entry id.
- [ ] Si se decide re-anclar: bump provenance ≥ `becf8e7`, extender lab04, y recién ahí volver factual §6.2/§8.5.
