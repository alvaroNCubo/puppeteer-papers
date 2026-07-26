# Paper 4 v0.2 — transport-doctrine one-liners (from guide review, 2026-07-05)

Paper 4 is published (v0.1, DOI 10.5281/zenodo.21207062), so these ride a future v0.2.

A reviewer comparing the **training guides** (built in another chat) to the papers noted
that two framings in the guides are crisper than Paper 4's paragraph-length development and
should be elevated INTO Paper 4. Both are code-grounded (`transport.md`, verified in
`ITransport.cs`) → citable as `ITransport.cs` refs.

## 1. Elevate to the ABSTRACT
> **"Delivery is the transport's problem. Correlation is the journal's problem."**

- **Status today:** the doctrine IS in the body — §8.2, the paragraph beginning *"Delivery and
  correlation are separated by design…"* — but only as prose; the abstract has no crisp form.
- **Action (v0.2):** add this one-liner to the **abstract**, and make it the topic sentence of
  that §8.2 paragraph. It is the one-line formulation of the operational-vs-programmatic
  separation the whole paper rests on — the doctrine, in the code, and citable.

## 2. Make the dedup-DOWN argument explicit (pre-empts a reviewer objection)
> **"Push dedup DOWN into the transport, never UP into the event model."**

- **The objection it answers:** a reviewer WILL ask *"why not deduplicate in the event model?"*
  Paper 4 currently states the delivery model is at-least-once with identity-based dedup (§8.2)
  but never argues *why* dedup must live below the model.
- **The argument (already in the guide):** deduplicating in the event model would make replay
  depend on dedup state — it breaks **self-contained replay** (the journal must re-execute
  identically from bytes alone; §8.5 G1/G2 depend precisely on this). So the dedup key (the
  tell's `once` identity) is applied receiver-side by the transport, NOT recorded as event-model
  state. Dedup goes DOWN (transport), never UP (event model).
- **Action (v0.2):** add a sentence/short paragraph in §8.2 near the deduplication-key mention
  making this explicit, and cross-reference G1/G2 as the property it protects.

## Broader pattern (worth carrying forward)
The guides — being concise and code-verified — are a **source of crisp, citable framings the
papers can adopt**. When cutting v0.2s (or the aglutinador), mine the guides for one-line
doctrine that the papers currently develop only in prose. Register note: these are crisp
restatements of existing, code-true doctrine — not new claims — so they are low-risk for v0.2.

## 3. The transport is provided-not-declared, with a different binding lifetime (from Paper 9, 2026-07-26)

Noticed while sharpening Paper 9's §9 sentence on where a contract sits. Paper 9's claim is
that the contract is *provided* by the substrate and *bound* in the staging, never *declared*
by the domain, and it shows that for the input side (inversion of control) and the output side
(the push sink). **The same geometry holds for the transport that carries an actor's tells**,
and Paper 4 is where it belongs — Paper 9 deliberately sets aside the axis of which actor
talks to which (its §8), so it carries only a one-sentence pointer.

Verified at engine master `dd67047`:

- `Choreography/Theater/PerformanceV2.cs:344` — `UseTellTransport(Puppeteer.Tell.ITransport)`,
  fluent, sets `actorV2.Handler.Transport`. The comment states the doctrine outright: *"the
  domain never names the wire."*
- So `ITransport` is an interface the **substrate** provides and the **staging** binds. The
  domain declares nothing about it, exactly as with the output sink.

**The difference worth writing down, because it breaks a tempting generalization.** The output
sink is re-bindable on a live actor — `StageHook.cs:186` assigns `handler.OutputTarget`, it can
be replaced while the actor runs, and passing null reverts to pull-only. The tell transport is
**single-assignment**: `PerformanceV2.cs:341-344` says the `ActorHandler` rejects swapping a
live transport because it would orphan in-flight tells, so it must be set once, before the
first tell is issued.

So the substrate is not uniformly hot-swappable, and the reason is not arbitrary: an output
sink has nothing in flight that a swap could strand, and a transport does. That asymmetry is a
Paper 4 point — it is about delivery — and it is a good concrete instance of *"delivery is the
transport's problem"*: the binding lifetime is set by what delivery guarantees require, not by
a general policy of the substrate.

**Action (v0.2):** state the provided-not-declared framing for `ITransport` where §8.2 already
develops the delivery/correlation split, and give the single-assignment rule with its reason.
Cite `PerformanceV2.cs:344` for the binding surface and the handler's rejection for the
lifetime. Cross-reference Paper 9 for the same geometry on the input and output sides.
