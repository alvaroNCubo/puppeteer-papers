# A paper trying to get out: how a closed domain grows

Alvaro, 2026-07-26: *"aquí veo otro paper intentando salir… Todo eso es interesante. Pero el paper
no trata de evolución. Trata de identidad."*

That is a scoping decision, and it is the right one. Everything below is already written, measured,
or practised — it is simply in the wrong paper.

## The inventory, and where each piece is today

| Topic | Status | Lives in |
|---|---|---|
| additive growth (the ladder: adapter → surface latent detail → compose) | argued, one rung measured | Paper 9 §8.2 |
| a score (the *authority* case — computable by any client, so it must be single-valued) | **measured**: 30 code lines, 0 deleted, no signature changed | Paper 9 Lab I |
| a difficulty level (a rule over acts already recorded) | **measured**, same commit | Paper 9 Lab I |
| the converse direction — the domain grows, do the stagings break? | **measured**: 12 of 12 kept working at 0 edits; adoption cost 0 / 1 / 3 / 32 lines, only where wanted | Paper 9 Lab I |
| obsolete verbs — mark, keep while any recorded act invokes it, delete when none does | practice in a production system, **not measured**; Tetris has retired no verb | Paper 9 §8.2, compressed to one paragraph and named as future work |
| what governs a deletion — the domain's own record, not a client and not a staging | argued | Paper 9 §8.2 |
| split a domain into two roles | **measured**: 11 of 12 hosts untouched, 12th one line; 0 divergences over 47,783 steps; record 2.32× (316 against 136) | Paper 9 Lab G |
| merge / roles born and joined | practice, **not measured** | Paper 9 §8.2, one sentence |
| re-decomposition is *not* journal arithmetic — read the acts and re-perform | argued, and the asymmetry **measured** (one role inherits the voice, the other's record is generated) | Paper 9 §8.2 + Lab G |
| replay as the reconstitution path | **measured** across labs | Paper 9 Lab G, Lab I, §9 |
| hold/swap a piece, undo, a second player — the *act* half of the ladder | **unclimbed**, named as such | Paper 9 §8.2 |
| schema evolution costs in event-sourced systems | cited, not measured | Overeem et al. 2017, 2021 (Paper 9 §8.2) |

## Why it is a paper and not a section

Paper 9 needs this material only to answer one threat — *if the domain changes, what is the identity
you measured?* — and its answer is short: a change of *staging* is not a change of domain, growth is
additive, and no staging can require either. Everything past that answer is a subject of its own.

The subject has a shape Paper 9 cannot give it, because it is about a *closed* domain specifically:

- **Growth in a domain with no ports is additive by construction**, since an operation added is a new
  act in the record rather than a reshaping of the acts already there. In a ported domain, growth can
  force a *port* to change, which changes every adapter. That contrast is the paper's spine and it is
  the closure construct applied to time — Paper 9 applies closure to *staging*.
- **Shrinking is governed by the record**, which is the surprising half: whether a verb may go is
  settled by whether any recorded act still calls it. A staging can no more require a verb to survive
  than require one to exist. That is the same direction of dependence Paper 9 measures, pointed at the
  domain's shrinking.
- **Re-decomposition is re-performance, never journal surgery.** Two hard consequences already
  established: there is no arithmetic relating the records, and the reading is *not symmetric* — one
  role inherits the original's voice, the other's record is *generated* as a consequence.

## What it would have to measure that nothing does yet

- The *act* half of the ladder: hold/swap, undo, a second player. Lab I climbed only the authority
  half.
- A verb actually retired, with the cost of carrying it while the record still needs it.
- A merge, as against the split Lab G measured.
- The growth-forces-a-port-change contrast, on the ported baseline that already exists
  (`baseline-hex/`, branch `claude/confident-satoshi-7ed985`). This is the cheap one and it is the
  comparison the paper turns on.

## Consequence for Paper 9 now

§8.2 is **1,815 words — about 11% of the argument** — for a subject the paper does not treat. Only
its threat-answering core is owed: a change of staging is not a change of domain; growth is additive;
re-decomposition changes the domain and no staging can call for one; the identity measured is
invariance under a change of *staging*, untouched by either. Perhaps 500 words. The ladder's
development, the retirement mechanism, and the split/merge practice can go to this paper — but note
that relocating detail into the appendix has already been measured as net-negative twice, so the cut
is a *cut*, with the material living here in this note until the paper is written.

Related: [[closure-vs-decoupling-and-the-aglutinador]], [[domain-growth-criterion-act-or-authority]],
[[actor-boundary-test-is-concurrent-mutation]], [[future-paper-a-fact-must-belong-to-one-actor]].
