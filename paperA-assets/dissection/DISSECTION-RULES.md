# Dissection rules (pre-registered)

These rules are committed **before** any removal, so that the sequence of commits can be
replayed and audited. Each rule below becomes exactly one commit. The same file, with the
same rules, is committed to every system under dissection, and the set that ends up here is
the set both systems were measured with. Two of the rules were prompted by something seen in
a codebase — see Amendments — so this is not a full pre-registration, and the guarantee is
stated there at the strength it actually has.

A reader can therefore check the two things that would otherwise be a matter of opinion:

- **what was removed and why** — `git log --reverse` lists the rules in the order applied;
  `git blame` on any surviving line names the rule that spared it.
- **that the line was not drawn to fit the conclusion** — each rule was written down before
  it met any case it was applied to, and the same set is applied to an independently authored
  system.

## The rules

**R1 — persistence.** Remove every type whose reason to exist is to store or retrieve state:
object-relational contexts, entity configurations, migrations, repository implementations,
unit-of-work implementations, and the interfaces that declare them — *including any that
live inside the domain assembly*. Deletions inside the domain assembly are listed in the
commit message, file by file.

**R2 — ambient context.** Replace every call site that retrieves a value the caller already
possessed but did not pass, with a parameter. Unlike R1, this rule does not open a hole: the
value is not missing, it was being re-discovered a layer below the one that already had it.

**R3 — transport and representation.** Remove every type whose reason to exist is to carry
data across a process boundary or to render it: request and response contracts, mappers,
serializers, integration events, and request-identity wrappers.

**R4 — wiring and dispatch.** Remove the wiring: container registrations, bus and
mediator registrations, module facades, and the startup that assembles them.

**R5 — the act.** Whatever remains of a command handler is separated into two parts: the
statements that invoke domain operations, and everything else. The commit message reports
both counts.

**R6 — observability.** Relocate to the boundary every call site whose reason to exist is to
report that the act happened: traces, spans, and the log lines that narrate progress. Like
R2 and unlike R1, this opens no hole — the reporting is not lost, it moves to the party that
asked for the act, which is the only party that knows what to call it.

## How objective each rule is

The rules do not all have the same standing, and pretending they do would be worse for the
evidence than saying so. Two kinds are mixed here deliberately, and each step's commit records
which kind it applied.

**Decidable against an artifact.** R5's criterion is checkable without judgement: a statement is
a domain operation when it constructs a type THE DOMAIN ASSEMBLY DECLARES, calls a static on one,
or calls a member on a local bound from one. The assembly is the authority. R4 is close behind -
container and bus registrations and startup are identifiable by what they are, not by what they
are for.

**Identified by the role a type plays.** R1 and R3 name families of types - contexts, migrations,
repositories, contracts, mappers, serializers. In practice the members of those families are
recognised without controversy, but the criterion is a role and not a syntactic form, so a
different reader could draw a slightly different line at the margin.

**Requiring semantic classification.** R2 and R6 do not have a syntactic test at all. R2 asks
whether a value the callee fetched was already held by the caller; R6 asks whether a call site
exists in order to report that the act happened. Both were decided by reading. The counts they
produce should be read as classifications, and the call sites each one touched are listed in its
commit so a reader can disagree with a specific one rather than with the rule.

This ordering is why R5 - the only measurement - was given the criterion with the least room in
it, and why it was restated once when an earlier version of it turned out to admit types the
domain assembly does not declare.

## Order of application

R1, R2, R3, R4, R6, then R5. **R5 is always last**, because it does not remove anything: it
reports what the other rules left behind. A rule added later than R5 is therefore applied
before it.

## The shape of each step

Each rule is applied in **two commits**, never one:

1. **the substitution** — a `<Type>.R<n>.cs` partial carries the rewrite; the original file
   is not edited, so its diff across this commit is empty;
2. **the deletion** — whatever the substitution superseded is removed.

The pair keeps every step small enough to read, and it puts the reason on the right line:
`git blame` on a deleted region lands on a commit that names the rule that superseded it,
not on a distant final cleanup.

**The hole count is read at the substitution, never after the deletion.** Once a deletion leaves the
surrounding declaration unbindable, diagnostics for holes inside its method bodies are no
longer guaranteed to be emitted — not because the holes were filled, but because the compiler
need not reach them. The count is therefore taken at the substitution commit, while the
surrounding program is still bindable, and it belongs in the message of the commit that
opened it.

## Scope

The rules are applied to each system's **write surface**: the handlers that carry out a
request to change something, and the machinery they depend on. Read-side query handlers are
out of scope in both systems, and are left untouched.

This is a scope, not an exception to a rule: R1 would reach a query handler perfectly well.
It is declared so that the two systems are compared on the same surface, and so that a count
is never quietly taken over a wider or narrower set in one of them.

## When the dissection stops

**The dissection stops at observational stability with respect to composition.** A removal
remains in scope while it changes which domain operations are associated with carrying out an
act, or the relation among them. Once a further removal changes neither, it is outside this
measurement, even if one of the rules could otherwise reach it.

Neither term in that test is judged case by case; both are fixed elsewhere in this file.

- A **domain operation** is what R5 says it is: a statement that constructs a type the domain
  assembly declares, calls a static on one, or calls a member on a local bound from one.
- **Association** is positional, not interpretive: an operation is associated with an act when
  it appears in the handler that carries out that request. Which handlers those are is fixed by
  the Scope section above, before any removal.
- The **relation among them** is their order and the values passed between them.

So the stopping condition is a test on the answer, not on the amount removed. Earlier drafts of
this file said "remove only what is necessary to expose the composition boundary", which was
worse in a way worth recording: EXPOSE presupposes that there is something to reveal, and a
reader could fairly hear it as removing until the wanted result appeared and then stopping. The
test above stops for the opposite reason — because the observed variable stopped changing.

Two consequences follow, and both are intentional.

**The end state is not minimal.** Machinery that no longer affects the measured boundary is left
standing. The procedure does not produce a purified domain; it produces a stopping point for one
measurement. Leaving something in is not an oversight, and it is the same discipline that
withdrew R7: seeing something does not by itself confer the right to cut it.

**Successful compilation is not an invariant.** I1 and I2 are; this is not. Intermediate states
may not compile, because the procedure is meant to expose and count a boundary rather than to
produce a migrated system. Nothing here measures or predicts the cost of such a migration.

## Amendments

Rules may be added once the procedure is under way, on one condition: **an amendment is
committed before it is applied beyond the observation that prompted it.**

Stating that accurately matters, because it is weaker than pre-registering every rule in
advance and the difference is real. An amendment could be PROMPTED by an uncovered case — R2
was, by something seen in the first system — so the rules were not all fixed before any code
was read. What the procedure rules out is narrower: once a rule is committed, its criterion is
fixed before it meets any further case, in either system. A rule can be provoked by a codebase.
It cannot be adjusted to accommodate what it subsequently finds.

Amendments are listed here with what prompted them.

- **R2 added, and R2–R4 renumbered to R3–R5.** Applying R1 to the first system surfaced a
  call site that no rule covered: a handler asking an ambient service for the identity of
  the caller. It is not persistence — nothing is being stored or retrieved from a record —
  and removing it would have narrowed what the system can be asked to do, breaking I1. It
  is its own kind of move, so it became its own rule.
- **The two-commit shape recorded.** The original procedure did not say when superseded code
  is removed.
- **R4 relabelled from "composition and dispatch" to "wiring and dispatch".** A LABEL CHANGE ONLY:
  the criterion below is untouched, the rule was already applied under the old name, and the commits
  that applied it keep it. The old label collided with the word this study reserves for the relation
  it is investigating, so a reader could reasonably ask whether composition had been removed by rule
  and then reported as absent. It had not: what R4 removes is registration and startup wiring. The
  collision was in the name, and renaming it is cheaper than explaining it.
- **R6 added.** R3 was applied twice while deliberately leaving trace and log call sites
  standing, on the grounds that the rule's subject is contracts and serialisation and that a
  stark result is the moment to under-reach. Under-reaching was the right call and the
  grounds were wrong: those call sites are not representation to be deleted, they are
  reporting to be **relocated**, and the place they belong is the boundary that requested the
  act. Recorded here rather than folded into R3, because a rule that removes and a rule that
  relocates produce different evidence: one leaves a hole, the other leaves nothing missing.

## Invariants checked at every commit

**I1 — the input surface is unchanged.** The parameters the system accepts from a caller are
the same before and after. A dissection that quietly narrows what the system can be asked to
do proves nothing.

**I2 — domain logic is never edited.** Inside the domain assembly the only permitted change
is the deletion of a declaration covered by a rule, and every such deletion is counted. No
type, member, or statement of domain logic is modified.

## What does not hold

Intermediate states are **not expected to build**. Removing persistence leaves the layers
above it referring to types that are gone. That is the observation, not an accident of the
procedure: it is what it means for those layers to have been resting on it.

## A rule that was proposed and withdrawn

**R7 - shape concessions. PROPOSED, AND WITHDRAWN BEFORE IT WAS APPLIED.**

It would have removed from a domain type every member that exists so something outside can
construct or populate it: parameterless constructors that no surviving code calls, and setters
whose only reason was to be written from outside. The candidates were real and were counted -
four in one system, none in the other:

    Buyer                protected Buyer()
    PaymentMethod        protected PaymentMethod()
    OrderItem            protected OrderItem()
    Address              public    Address()      the only public one

Withdrawn for two reasons, the second of which breaks the rule rather than merely disagreeing
with it.

**It decides for the author what the author decided.** A parameterless constructor is
persistence-shaped, and it is also something its author wrote, permitted and meant. Nothing in
the code says which. The whole procedure rests on not supplying intent where the code does not
state it - the same reason a hole is numbered rather than named.

**And its criterion is not decidable here.** "No surviving caller" assumes callers can be
enumerated. In this substrate the assembler reaches the repertoire BY REFLECTION, and no static
analysis sees it: `address = Address();` is a legitimate line of an assembled verb. So the door
is not the ORM's door - it is A DOOR, and the party entitled to reach the repertoire may use it.

That is the other face of a property this work measured elsewhere: the mechanism that lets a
domain stay internal and still be composed is the same one that makes unused-member analysis
undecidable from the host's side. One property, two faces, and the second is the price of the
first.

The count stays as an observation about the two codebases. It stops being a deletion.
