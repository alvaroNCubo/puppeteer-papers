# Paper 8 v0.2 — the closing test is normative, and the paper declares itself Type I

Verified 2026-07-26 while checking a series-level continuity claim. Paper 8 is published, so this
rides a future v0.2.

## The defect

Paper 8's §8 Conclusion contains, verbatim:

> *"an architecture is **sound not by** its divisions of code but by whether the authorities within
> it match the decisions each part has the standing to make."*

That is a judgment about what makes an architecture **good** — a prescription. Paper 9 carried the
identical formulation ("sound not by how it distributes code but by whether the domain within it
keeps an identity") and removed it, because a paper declaring itself an analytic contribution in
Gregor's (2006) Type I sense supplies constructs for *describing and comparing*, not a rule for
judging. A reviewer raised it against Paper 9 and it was a fair hit; Paper 8 has the same sentence
and has not been corrected.

Note the sentence immediately before it makes the problem sharper, not milder: *"The usual measure —
how many layers, how clean the separation of code — misses what matters."* That is explicitly
ranking measures of architectural quality.

## The fix, on Paper 9's model

Paper 9 replaced the prescription with an *instrument* — a question that can be put to a system
already built and answered by measurement rather than opinion. For Paper 8 the parallel is direct
and the paper already almost has it in the next clause (*"a test a reader can carry back to a system
already built"*):

- keep: **do the authorities within this system match the decisions each part has the standing to
  make?** — asked of a built system, answered by reading where each decision is taken.
- drop: the claim that an architecture *is sound* by that and not by its divisions of code.

That preserves everything Paper 8 established and removes the only sentence in it that a reviewer
can call a category error against its own declared contribution type.

## Related v0.2 debts in the same paper

- Brooks (1987) is uncited while *accidental* does structural work — see
  [[paper-review-v02-brooks-essential-accidental]] (Paper 7 is the larger case; Paper 8 is adjacent).

Related: [[paper-review-v02-register-overclaiming]], [[configuration-claim-not-refuted-by-parts]].
