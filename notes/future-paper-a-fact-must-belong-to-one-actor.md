# Parked for a paper of its own: a fact a domain emits must be derivable within one actor's state

Parked 2026-07-26, to be written **after** the Puppeteer series is complete. Paper 9 reports it in
§8.4 in about 200 words, and files the half that bears on its own claim in §8.3 (Threats to
validity) rather than with the finding.

## The finding

**A fact that is a join over two roles cannot be emitted by either of them.** So emitting a joint
fact is a constraint on the *admissible decompositions* of a domain: a boundary may be cut wherever
the concurrency test permits, **except across a fact the domain must emit whole.**

Premises, both verified at engine master `dd67047` (Paper 9, Lab G):

- the undivided board built a complete frame by unioning the pile's cells with the falling piece's,
  clipped to the interior — a derivation performed *inside* the domain (`domain/Well.cs:322`);
- a projection on the emitting plane is produced by a reaction belonging to *one* actor, running on
  that reaction's thread once that actor's read lock is released (`Puppeteer/IOutputSink.cs:106`).

Cut the board in two and the halves of the union sit in two actors, so neither role can push a whole
frame.

## Why it deserves its own paper

**It is a design rule, not an incident**, and it is the only rule Paper 9 found that constrains the
*author of a domain* rather than the author of a staging.

**It contains an honest concession Paper 9 makes nowhere else**: this is the one place where
influence runs *against* the direction that paper measures — a property of the substrate constrains a
modelling decision inside the domain. No dependency is declared, nothing imported, the diff stays
empty; but *declaring no dependency on the framework is not the same as being unshaped by it.*

## What the paper would have to separate, which Paper 9 only gestures at

Paper 9 says the constraint is partly intrinsic and partly this substrate's, and does not develop it:

- **intrinsic**: a fact needing two parties' state cannot be produced by a read of one. True of any
  arrangement.
- **this substrate's**: that the constraint lands on the *push* plane specifically, because a
  reaction runs under one actor's read lock. Another substrate could offer a joint emitter — and
  what that would cost is the paper's real question.
- **unmeasured**: a party reading *both* records and joining them. That is an ordinary adapter, which
  is what Paper 9's §3 argues for, and it was never built. Building it is where the paper starts.

Open: is the constraint a *limit* or a *criterion*? If a fact must be derivable within one actor,
then joint facts identify boundaries that should not be cut — which reads less like a restriction
than like a test for whether a proposed decomposition is sound.

Related: [[future-paper-incomplete-records-answer-plausibly]],
[[actor-boundary-test-is-concurrent-mutation]], [[closure-vs-decoupling-and-the-aglutinador]].
