# v0.2 — Brooks (1987) essential/accidental: the series' most visible citation debt

Raised by an internal reviewer on the Paper 9 draft (2026-07-25): the papers use
*accidental* as a central technical term and never cite Brooks. Verified against
the repository — **Brooks appears in zero of the nine papers**, while *accidental*
appears 14 times in Paper 7, 5 in Paper 6, 5 in Paper 9, and once each in Papers
1, 3, and 4. The reviewer is right that this is the most visible omission, and it
is series-wide rather than local to one paper.

**Citation (verified):** Brooks, F. P., Jr. (1987). No silver bullet: Essence and
accidents of software engineering. *Computer*, 20(4), 10–19.
https://doi.org/10.1109/MC.1987.1663532 (First presented at the IFIP Tenth World
Computing Conference, 1986; also UNC technical report TR86-020.)

## Already fixed: Paper 9 (unpublished, so no need to defer)

Paper 9 got the engagement in draft, in §9, on two points rather than as a
citation plug:

1. **The cut is not Brooks's cut.** Brooks partitions *difficulty* — which labour
   of construction is inherent and which is imposed by our tools. Paper 9
   partitions *identity* — which part of a built system is the domain and which is
   a staging of it — and draws the line where a dependency graph draws it. A thing
   can be accidental in Paper 9's sense (the domain does not refer to it) while
   the difficulty it presents stays entirely essential: coordinating three
   machines is hard whoever owns the hardness.
2. **The no-silver-bullet fence.** Paper 9 reports a count of edits *not* made — an
   existence proof that it can be zero — not a productivity measurement. Saying so
   against Brooks is what stops a reviewer reading "the staging is accidental and
   removable" as an unacknowledged order-of-magnitude claim.

Note what Paper 9 does *not* need: it never coins *accidental* as its own
construct. Three of its five uses quote a code comment ("an accidental shell") and
two attribute Paper 7's thesis. The debt there is inherited, not incurred.

## Paper 7 — the real debt (published; DOI 10.5281/zenodo.20398998)

Highest priority. Its central thesis — *the server is an accidental category* — is
Brooks's word doing Brooks's work, 14 times over, with no citation. A reviewer who
knows the 1987 paper will read the title as either an allusion or an oversight,
and neither is good. For v0.2:

- A Related Work paragraph placing the claim against Brooks explicitly.
- The same *difficulty vs category* distinction Paper 9 now draws: Brooks asks
  which difficulties are inherent; Paper 7 asks whether a **category** (the
  server-role) has a referent at all under a different substrate choice. That is a
  stronger and stranger claim than "this difficulty was accidental" — it says the
  category is contingent, not that the work is easy — and Brooks is the right
  foil for saying so.
- The fence: Paper 7 claims the datacenter is not a structural requirement, which
  is a claim about necessity, not about an order-of-magnitude gain. Worth stating,
  because *removing a category* sounds more like a silver bullet than it is.

## Paper 6 — adjacent, worth a paragraph (published; DOI 10.5281/zenodo.20317450)

Its construct is the *symptom*, not the accident, and the two are close enough
that the relation should be named rather than left for the reader. A layer that
compensates for a deficiency of the persistence model is accidental in Brooks's
sense — it attends our representation and vanishes when the representation
changes — whereas a layer the problem genuinely demands is essential. Paper 6's
two marks (it compensates; it dissolves on repair) are a *test* for accidentality
that Brooks did not supply, which is a contribution to state, not a coincidence to
hide.

## Papers 1, 3, 4 — one use each

Incidental usage; check the sentence in each during the v0.2 pass and cite only
if the word is load-bearing. Paper 1 also uses *essential* once — worth a look,
since it is the pair.

## The aglutinador cannot avoid him

Registered here because it outlives the v0.2 passes. The series' organizing move —
that a great deal of what the industry treats as essential to building software is
accidental to it, and demonstrably removable — is a Brooksian claim made at series
scale. Whatever synthesis the series arrives at will be read against *No Silver
Bullet*, and it is better to choose that conversation than to be placed in it.
The honest position is available: the series does not claim an order-of-magnitude
productivity gain, and its cuts are ontological (what belongs to a domain) rather
than economic (how much labour a project takes). Brooks's pessimism was about the
second; the series argues about the first.
