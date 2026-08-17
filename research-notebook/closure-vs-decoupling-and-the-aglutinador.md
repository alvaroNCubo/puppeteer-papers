# Closure vs decoupling — the construct Paper 9 was missing, and the bridge to the aglutinador

Alvaro's observation, 2026-07-26: *"lo que hexagonal no tiene es un dominio que cierre."*
Developed with an external reader. This is a framing decision, not yet applied.

## The point

Paper 9 spent many paragraphs looking for differences from hexagonal architecture, because both
reach staging invariance. That search was aimed at the wrong quantity. The difference is not how
many edits a new staging costs — a competent hexagon also pays none, and Lab F measured that.

**Hexagonal *decouples* a domain. This arrangement *closes* it.**

A hexagonal domain still declares ports, and a port is an unanswered question the domain itself
asked: who persists, who restores, who carries output. The ports are abstractions but they are
still holes the domain declares, so the domain is not finished — someone must complete those
relations. The domain here asks nothing. It does not request transport, persistence, projection,
or an output interface. It acts; the rest observes.

## Why this is a construct and not a new claim: the evidence is already measured

Five findings already in the paper are instances of one property. Under "closure" they stop being
five separate defences:

| Already measured | Baseline | Here |
|---|---|---|
| driven ports declared | 3 | 0 |
| tests that cannot run without a stand-in | 20 of 64 | 0 |
| publicly callable domain operations | ≥1 per port | 0 (one empty type) |
| reconstitution seam | 56 added / 5 removed | none — replay enters by the domain's own operations |
| state assertable from outside | `GameState` via a host-implemented port | only acts |

Each is a count of **what the domain still has to be given**. So the quantity to report is
*unfulfilled obligations*: three there, zero here — a better measure than 56 lines, and already
taken.

## Discipline this must keep

1. **"Closed" is a construct that names measurements, not a measurement.** Present it exactly as
   §5 presents identity: the structural property is checkable, calling it closure is a reading of
   it. Without that, "no remaining contractual obligations" is the same overclaiming twenty review
   rounds have been removing. See [[concession-must-not-become-thesis]].
2. **Do not say the distinction appears "only" with persistence, replay and testimony.** It also
   appears in the test doubles — 20 of 64 blocked tests have nothing to do with persistence. It
   appears wherever the domain must *ask* for something.
3. **The numbers stay.** They are not defending "zero edits" any more; they are the evidence for
   closure. What can go is the long argument about *whether* hexagonal also reaches zero edits,
   which becomes one conceded sentence. Worth roughly 400 words, not pages.

## The residual claim, restated

Today: *history and durability are not among this domain's requirements* — one instance.
Under closure: **the domain has no unfulfilled obligations**, and history and durability are one
instance of that, alongside the empty public surface, the absent stand-ins, and the absent
reconstitution seam.

## The bridge to the aglutinador

The reader's closing move, and it looks right: an **open** domain is still a *part* of a system,
while a **closed** domain begins to behave as a reusable unit. So the thesis the unifying paper
would argue is that **a domain can enter a repertoire only once it has no pending contracts with
infrastructure**. Closure is the admission criterion.

That connects Paper 9's own open question — whatever composes two domains is neither of them —
to the repertoire question, and gives the aglutinador a claim rather than a survey. The worked
case waiting for it is in [[composition-shopping-nutrition-worked-case]].
Related: [[series-minimal-algebra-direction]], [[paper9-distributed-observation-brief]].
