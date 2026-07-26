# A worked case for the composition question: Shopping + Nutrition

Raised 2026-07-26. This is **not** Paper 9 material — that paper's discipline is measurement and
this is a design not yet built, and its §10 already states the open question precisely. It is
material for the composition paper (the one that would treat a set of domains as a repertoire).

## The case

Alvaro's wife — not a developer, not an architect — wants an app that records supermarket
purchases on a phone and, beyond budget and spend-by-category, tells her things like *more
macaroni, you're spending too much on beer*. Her own framing was two domains: **Shopping** and
**Nutrition**, with no coupling between them.

## Applying Paper 9's criteria to it

**Is Nutrition a real domain, or a projection over Shopping's record?** By the act-or-authority
criterion, a real domain, on two independent counts:

- *Non-derivable facts.* "Macaroni is a carbohydrate at ~350 kcal/100 g" cannot be derived from
  any sequence of purchase acts. Shopping's record holds items, quantities, prices, dates.
  Nutrition holds a model of food. No projection over the first yields the second.
- *Authority.* "You spend too much on beer" is a judgment against a norm, and somebody must own
  the norm and be answerable for it. Shopping knows nothing of health, so the norm is Nutrition's
  and asserting it is an act.

So the decomposition is sound, and by Paper 9's own test rather than by taste.

**But it is not zero coupling — it is zero *declared dependency*.** Something must map
`6 × beer 500 ml` in Shopping's vocabulary onto `beer, 500 ml, 43 kcal/100 ml` in Nutrition's.
That mapping belongs to neither domain: Shopping must not learn nutrition, and Nutrition must not
learn about receipts. It is the composer, and Paper 9 has only the negative half of the answer —
it knows where the mapping does *not* live (§10: whatever composes two domains is neither of
them). This case is that open question made concrete, which is exactly why it is worth keeping.

## Why the case is worth more than an illustration

Two things it can carry that Tetris cannot.

1. **Two domains, not one.** Every measurement in Paper 9 is on a single domain across many
   stagings. This is the first candidate for measuring what composition costs — and the
   composer's cost is the quantity the composition paper needs.
2. **The boundary was drawn by a non-technical person, correctly, on the first try.** She did not
   say "a shopping app with a nutrition module"; she named two things with vocabularies of their
   own. That is evidence about the *naturalness* of the cut — that domain boundaries as this
   series conceives them track how someone thinks about their problem rather than how an
   architect draws layers. Worth stating carefully and not romanticising: it is one observation,
   not a study, and the framing was elicited in conversation.

## Open questions this case would have to answer

- Where does the item→food mapping live, and who is answerable when it is wrong?
- Does Nutrition need Shopping's *acts*, or a projection of them? If a projection suffices, the
  composer is a reader and the coupling is nominal (§9's term). If Nutrition must record acts of
  its own — "advised more macaroni on this date" — it has a journal, and then two journals must
  be correlated without either domain knowing of the other.
- Is the composer a third domain, an adapter, or a staging? Paper 9's vocabulary does not settle
  this, and settling it is most of the composition paper.

Related: [[domain-growth-criterion-act-or-authority]], [[series-minimal-algebra-direction]],
[[configuration-claim-not-refuted-by-parts]].
