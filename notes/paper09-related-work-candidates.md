# Paper 9 — Related Work: exhaustive sweep (2026-07-25)

Second, deeper pass over §9's neighbourhood. Currently cited in §9: Parnas (1972),
Evans (2003), Cockburn (2005), Martin (2017), Wiggins (2011), Meyer (1988),
Young (2010), Fowler (2005) + Papers 7/8. Below are the gaps found, ranked, each
with the *delta* the paper must state. **Bibliographic details need a verification
pass before deposit** (Paper 8 had a dedicated "bibliography verification pass" commit).

---

## TIER 1 — must add; a reviewer will name these

### 1. MDA / platform independence — PIM vs PSM
*Kleppe, Warmer & Bast (2003), MDA Explained; OMG MDA Guide; Mellor & Balcer (2002), Executable UML.*

**Why it is the nearest terminological neighbour:** "platform independence" is the
established name for exactly what §1 claims — a model of *what a system does* that
holds no reference to the technology it runs on. A PIM "documents the functionality
and behaviour of an application, separating the specification from technology-specific
code"; a PSM describes it "according to a particular deployment technology." If the
paper does not engage this, the obvious reading is "PIM/PSM restated."

**The delta (sharp, and in the paper's favour):** MDA's PIM is an *abstract model*
that is **transformed** — the PSMs and the code are *generated derivatives* of it, so
what runs is never the PIM itself; there are N generated artifacts, and keeping them
faithful to the model is MDA's standing difficulty (the drift/round-trip problem, and
the reason MDA's full form is rarely practised). Here there is **one artifact**, run
as-is on every stage: no transformation step, no generated derivative, nothing to keep
in sync. The invariance is therefore a *diff over the same source*, not a regeneration
— which is why it can be measured at all. Second delta: MDA is a **methodology** to
follow; the claim here is a **property** a built system either has or does not.

**Where:** §9, immediately after the DDD paragraph (it is the "separation as method"
neighbour), or its own short paragraph before Hexagonal.

### 2. Location transparency in the actor tradition
*Agha (1986), Actors; Armstrong (2003), Making reliable distributed systems in the
presence of software errors (KTH thesis) — Erlang/OTP; Akka's location transparency.*

**Why:** the claim "the same code runs on one machine or across many, unchanged" is
the actor model's own tradition, and Experiment A looks exactly like it from the
outside. A reader from that world will say Erlang did this in 1998.
*(NB: `armstrong.pdf` is sitting untracked in this repo — this thread is already open.)*

**The delta:** location transparency is about the **address of a peer** — an actor
sends to a name and need not know where the recipient runs. It is a property of the
*messaging layer*, and it is what lets a *system of actors* be redeployed. This paper
varies more and claims something else: the **whole staging** — where it runs *and*
which client observes it, including the client's adapters — and it measures the
**domain's** diff. Note the layering: in this framework the actor is already part of
the staging-facing shell (`IGameHost`, "an accidental shell"); the domain sits *below*
the actor and is not an actor at all. Location transparency keeps *actors* portable
across machines; this keeps a *domain* invariant across machines **and** across clients,
and the evidence is an empty diff rather than a transparent address.

### 3. Waldo, Wyant, Wollrath & Kendall (1994) — "A Note on Distributed Computing"
*Sun Microsystems Labs TR-94-29; reprinted in LNCS 1222 (1997), 49–64.*

**Why it must be engaged:** it is the canonical argument that the local/remote
distinction **cannot** be papered over — latency, memory access, concurrency, and
partial failure are irreducible, and systems that hide them "fail to support basic
requirements of robustness and reliability." Experiment A (a domain moved from one
process to three containers with no domain edit) is precisely the shape Waldo warns
about. Left unaddressed, this is the strongest available objection.

**The answer — and it strengthens the paper (rebuttal by scoping, in the style of
Paper 8's algebraic-effects rebuttal):** the paper does **not** claim the four
differences vanish, or that distribution is free. It claims they are absorbed by the
**staging**, not by the domain. Every one of Waldo's four is visible in §2's own
caveats and §8's limits — the rendezvous bootstrap, the connect-readiness race and the
catch-up path (partial failure and concurrency), the TLS trust decision, the transport
itself. The `Well` did not have to learn about any of them; the host did. So Waldo's
thesis is *accepted*, and the result is stated against it: the distinction between
local and remote is real and irreducible — it simply is not a fact about the domain.
That is a narrower and more defensible claim than location transparency's, and Waldo
is the reason to say so explicitly.

**Where:** §9, and a sentence in §8 Limits pointing at it.

### 4. Reflexion models / architecture conformance checking
*Murphy, Notkin & Sullivan (1995), Software reflexion models, ACM SIGSOFT SEN 20(4),
18–28; plus the constraint-language line and its practitioner tool, ArchUnit.*

**Why:** this is the **methodological** neighbour for the paper's central move —
"the direction of dependence is a checkable property of the built system." Reflexion
models define a high-level model, map source entities onto it, and compare the two
graphs algorithmically, surfacing convergences, divergences, and absences. §2's
build-graph reading (host → actor → domain → nothing) is a reflexion-style measurement
in all but name, and citing this line is what makes "checkable, not interpreted"
credible rather than rhetorical.

**The delta:** conformance checking asks *does the code obey the architecture someone
prescribed?* — the intended model is an input, and the output is a violation count.
Here the dependency direction is not a rule imposed and then policed; it is the
**substance of the claim**, and the measured quantity is the **domain's diff across
stagings** — an invariance under variation, not conformance to a prescription. The
paper's evidence is closer to an experiment (vary the staging, measure the domain)
than to an audit (fix the model, count violations).

### 5. Functional core, imperative shell
*Bernhardt (2012), Destroy All Software screencast (practitioner source, of the same
kind as Cockburn 2005 and Wiggins 2011).*

**Why it is the closest thing to §9's "no ports" geometry:** the pattern is a core
with **no dependencies at all**, wrapped by an imperative shell that owns stdin,
stdout, the database, and the network — and its best-known claim is that the core
"naturally allows isolated testing **with no test doubles**." That is the same zero
the paper reports. **Honesty requirement:** the zero-doubles observation is therefore
*not* novel here, and §9 should say so rather than let a reader discover it.

**The delta:** FC/IS is a claim about **purity** — the core is pure functions and the
shell holds the mutation — and it is a *within-process code-structure* discipline; its
"shell" is one imperative wrapper around one program. Here (a) the invariant thing is
**not pure**: the domain mutates and keeps a journaled history, so the separation
cannot be purchased with purity; (b) the "shell" is **many stagings**, including a
three-machine one, and the claim is about the domain's **identity across them**, not
about where mutation lives; (c) the shell/core split is a design one adopts, while the
common-ancestor property is read off the built artifact afterwards. Same zero, different
claim: FC/IS explains why a *test* needs no double; this explains why a *staging* needs
no domain edit.

---

## TIER 2 — worth adding, cheap

### 6. The immutability / derived-data line — for §4 and §6
*Helland (2015), Immutability changes everything, CIDR (also ACM Queue / CACM);
Kleppmann (2015), Turning the database inside-out with Apache Samza.*

**Why:** these are the strongest neighbours for §4's "what a client sees is the past."
Helland's append-only computing — observed facts recorded and kept, all results derived
from them on demand, the accountant who never erases an entry — and his
*data on the inside / data on the outside* split are the data-management form of
exactly the arrangement §4 argues for. Kleppmann's "inside-out" is the same idea as a
system-composition strategy.

**The delta:** both argue immutability + derived views as a **data-management strategy**
— it scales, it avoids locking, it makes derivation cheap. §4 reads the same
arrangement as a statement about **who can see at all**: an audience that is not the
actor has only the past available to it, so serving views from the record is not an
optimization but the only seat there is. Their claim is about the properties of
immutable data; this one is about the roles of actor and audience, and it yields a
prohibition their framing does not (never serve an audience's view from a command's
response — §4).

### 7. Dapper — cite the real tracing paper in §7
*Sigelman, Barroso, Burrows, Stephenson, Plakal, Beaver, Jaspan & Shanbhag (2010),
Dapper, a large-scale distributed systems tracing infrastructure, Google TR.*

**Why:** §7 currently says "distributed tracing, correlation identifiers, and service
maps" generically. Dapper's own stated motivation *is* §7's observation, in its
authors' words: systems built from modules "developed by different teams, perhaps in
different programming languages… spanning thousands of machines," where tools to
reconstruct behaviour are indispensable. Citing it makes §7 precise **and** reinforces
the non-prescriptive register — the paper is agreeing with Dapper about the need, and
only observing where the narrative sits. (OpenTelemetry descends from it.)

### 8. Software product lines / variability
*Clements & Northrop (2001); Apel, Batory, Kästner & Saake (2013).*

**Why:** "one codebase, many deployments" is the SPL slogan, so a reviewer may read
Experiment A as a product line. Worth one sentence to separate.

**The delta:** an SPL varies **the product** — a feature is present or absent per
variant, and a variant is *generated* from a configuration, so the artifact differs per
deployment. Here nothing about the domain varies and no variant of it is produced: the
same domain is run, unmodified, and only the staging around it differs. SPL manages
*intended difference*; this measures *absence of difference*.

---

## TIER 3 — optional, low marginal value
- **Onion architecture** (Palermo, 2008) — fold into the Cockburn/Martin sentence if a
  third name helps; it adds no new geometry.
- **Architecture erosion / drift** (Perry & Wolf, 1992) — could support §7's "identity
  spread across services," but §7 is deliberately short and non-prescriptive.
- **JVM/"write once, run anywhere"** — runtime portability of a *platform*, not of a
  domain; MDA covers the concept better. Skip unless a reviewer raises it.

---

## Net effect on the paper

Three of these (MDA, location transparency, Waldo) are **objections in waiting** — each
is a reading under which the result looks already-known. Answering them does not weaken
the claim; it narrows it to the one thing that is actually new and measured: not that
deployment can be late-bound (Unix, MDA, actors, twelve-factor all have that), and not
that a core can be dependency-free (FC/IS has that), but that **the contract sits
outside the domain entirely**, so the domain is the *common ancestor* of every staging
— and that this is a property read off a built system rather than a discipline claimed
for it. Two of them (Waldo, FC/IS) require an explicit concession, which is what makes
the rest credible.
