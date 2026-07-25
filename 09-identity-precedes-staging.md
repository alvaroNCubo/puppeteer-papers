---
title: "Identity Precedes Staging: one play, many stages"
author: Alvaro Rivera
affiliation: Ncubo Ideas, Costa Rica
date: 2026-07-23
version: 0.1-draft
status: v0.1-draft — §1–§9 drafted (domain/deployment claim; two experiments; adapters; actor and audience — an audience can only see a play already performed; identity as the common ancestor; recognition as a consequence; where the narrative lives; limits + ladder of change; related work); discovery-first structure; the demonstration is grounded in a Tetris `Well` domain run across stagings and clients, verified against code with file:line anchors (two experiments run, zero domain change; Experiment A includes a verified genuine 3-node cross-machine Docker/TLS staging with domain+actor diff empty; frame-push is the canonical channel post-fix); §10 Conclusion, Appendix A (labs + table), Code provenance, and References to be drafted — re-pin TetrisActor anchors to commit 4b473ea; not yet peer-reviewed
keywords:
  - identity
  - staging
  - domain independence
  - decoupling
  - ports and adapters
  - projection
  - narrative recognition
  - journaled systems
  - actor-native architecture
  - design theory
  - puppeteer framework
abstract: >
  In most systems the code that defines what a system does is entangled with the
  code that defines where it runs and which clients it serves. Moving a domain
  from a console to a web server, adding a new kind of client, or splitting one
  process into several typically requires editing the domain itself. This paper
  argues that the entanglement is not intrinsic, and that removing it completely
  reveals that a domain has an identity independent of its deployment.


  Throughout, a deployment is called a *staging*, by analogy with theatre: one
  script, many productions. The claim is stronger than decoupling. The dependency between a
  domain and its stagings is one-directional, and the direction is a checkable
  property of the built system: the domain compiles and runs with no staging
  bound, while every staging references the domain and the domain references
  none. In that precise sense the domain comes first — a staging is built against
  it, not the reverse. This distinguishes the result from dependency inversion as
  usually practised: in a ports-and-adapters design the domain still declares the
  ports its adapters implement and depends on them, whereas here the domain
  declares no port for its output or its clients at all. The number of domain
  edits needed to add a staging, and the number of test doubles the domain needs
  for its output, are both zero.


  Two experiments hold one domain — a Tetris board — fixed while changing, in
  turn, the two things a deployment is made of. The first changes the stage: the
  same domain runs on a console, in a browser over a real network, and across
  co-hosted actors. The second changes the client: a human at a keyboard, an
  automated player that reads the board through an adapter it builds for itself,
  and a browser rendering in a second language. Across all of them the domain is
  unchanged — measured as an empty diff over the domain's source. Every client
  reaches the domain through an adapter, and none is privileged: the on-screen
  grid a human reads is as much an adapter as the vector the automated player
  computes; the domain emits raw facts and each client projects them.


  Because the domain's identity is independent of its staging, the sequence of
  what it did is available directly from its journal and can be recognized across
  every staging — recognition, in the sense of Paper 3, follows from the identity
  rather than being a separate result. The paper is analytic in Gregor's (2006,
  Type I) sense: identity and staging are constructs by which an architecture can
  be read, sound not by how it distributes code but by whether the domain within
  it keeps an identity independent of where it is staged and to whom. A journaled
  actor system serves as a worked instantiation, and the labs of Appendix A are
  an existence proof that the separation is buildable — not a design-science
  evaluation of its cost or benefit.
---

# Identity Precedes Staging: one play, many stages

## TL;DR

Most systems entangle *what* a program does with *where* it runs and *which clients* it serves, so a new deployment or a new client means editing the domain. It need not. A deployment is one *staging* of a domain — one script, many productions — not a program in its own right, and the domain can be held completely fixed while its staging changes. The dependency is one-directional and visible in the build graph: the domain references no staging; every staging references the domain. That is stronger than ports-and-adapters decoupling, where the domain still declares and depends on the ports its adapters implement; here the domain declares no output or client port at all, and the count of domain edits to add a staging, and of test doubles its output needs, is zero. Two experiments demonstrate it on one domain — a Tetris board — by changing the *stage* (console → browser over a network → co-hosted actors) and the *client* (a human, an automated player with its own view adapter, a browser in a second runtime), measuring the domain unchanged in every case. Every client, the human UI included, sees the domain through an adapter; none is privileged. Because the domain's identity is independent of its staging, the sequence of what it did is available directly and recognizable across all of them (Paper 3). The contribution is analytic: a domain is not its deployment, and the separation is buildable.

*Dependencies. This paper is part of the Puppeteer Papers, a series of self-deposited preprints, and rests on four of them: the actor's speech and `tell` (Paper 4), `Reaction` read as the recognition of a routine (Paper 3), the server treated as an accidental category rather than a place a domain lives (Paper 7), and testimony (Paper 8), whose observer receives an account it is told. Paper 8 noted, without pursuing it, that a narration received is not yet a narrative recognized; this paper takes up the recognition, and reaches it not as its subject but as a consequence of the identity it argues. Methodologically it is an analytic theory contribution in the sense of Gregor's (2006) theory for analyzing (Type I): identity and staging are constructs by which an architecture is described and judged, while the labs of Appendix A are an existence proof of realizability, not a design-science evaluation of cost or benefit.*

## 1. The Domain and the Deployment

In most systems, the code that defines what a system does is not cleanly separable from the code that defines where it runs and which clients it serves. Moving a domain from a console to a web server means editing it; adding a second kind of client means adding to the thing the client observes; splitting one process into several is treated, uncontroversially, as building a different system. Where a system runs, and whom it serves, are treated as properties of the system itself — so that a monolith and its distributed successor are counted as two systems rather than one system deployed two ways.

There is a field in which this separation is routine rather than aspirational: theatre. A play is written once and staged in many venues, for many audiences, without being rewritten, and no one counts the Broadway production and the school-gymnasium production as different plays. The term is taken from there: throughout this paper a *staging* is a deployment — a particular place a domain runs and a particular client it serves — and the domain is what is staged. It is a naming convenience, not an argument, and earns its keep only if the separation it names can be made real and measured — which is the rest of the paper.

The claim is stronger than "the domain should be decoupled from its deployment," which is ordinary advice. It is that the dependency between the two is one-directional, and that the direction is a checkable property of the built system. The domain compiles and runs with no staging bound to it. Every staging refers to the domain; the domain refers to no staging. In that precise sense the domain comes first: a staging is constructed against a domain that already exists and does not know of it. This is all the title means by *precedes* — a direction of dependence in the build, not a claim about time or metaphysics — and §2 measures it directly.

This is not dependency inversion under another name, and the difference is not rhetorical. Ports-and-adapters (hexagonal) architecture also keeps adapters outside the domain — but the domain declares the ports those adapters implement and depends on them, and a test of the domain must supply a stand-in for each. In the model examined here the domain declares no port for its output or its clients at all: it emits facts under logical names, and where those facts go, and to whom, is bound entirely outside it. The distinction is observable rather than interpretive, in the way Paper 8 made it for output: count the domain edits required to add a staging, and the test doubles the domain needs for its output — under ports-and-adapters, at least one of each; here, zero, because there is no port to reimplement or to double.

Placed in the series, this is a question of a specific kind. Paper 4 asked who may speak; Paper 8 asked who may decide what an output is; this paper asks what stays the same when the staging changes — a question about the identity of a domain, not about infrastructure or topology. The later synthesis the series is working toward will need that question answered before it can treat a set of domains as a reusable repertoire; that is not this paper's concern, which is the narrower, checkable claim that a domain's identity is prior to and independent of its staging. One consequence is developed in §5: when a domain keeps its identity in one place, the sequence of what it did is available directly from its journal — it can be read where it happened, rather than reassembled from the outside.

Two questions follow, and the rest of the paper pursues them. First (§2, §5): does anything actually hold a domain fixed while its staging changes — across both where it runs and which client it serves — can that be measured rather than asserted, and what does the invariance reveal? Second (§3–§4, §6): since a client never reads a domain directly, through what does it read, and does it read the domain's present or its past — is any client's view privileged, and what does it make of what it reads?

## 2. Two Experiments

The claim of §1 is checkable, and this section checks it against a running example: a Tetris game, whose board and rules are a small but complete domain, written as a `Well` and a set of operations over it. The domain is held fixed, and two things are varied around it in turn — where it runs, and which client it serves. The measurement in each case is the same: the number of changes the variation forces on the domain source. The evidence is the git history of the example repository, and the anchors are collected in Appendix A.

The domain's independence has a concrete form before anything runs — the shape of the build. Every executable host in the example reaches the domain through a single actor, and the domain reaches nothing:

```
  console, web, web-rest, server, StageManager hosts, ...
      |
      v
  TetrisActor  -->  TetrisDomain  -->  nothing
```

The only edge from the running system into the domain is `TetrisActor.csproj → TetrisDomain.csproj` (`actor/TetrisActor.csproj:11`); no host binds the domain directly, and the domain names no host. (Its one other reference is its own test project, as expected.) This is the dependency direction §1 called *precede*, read off the project graph: the domain is built first and knows nothing of what will run it. Stated as a property of the graph, it is the one this paper demonstrates: the domain is the common ancestor of every staging — the single node all their reference paths lead back to, and from which they all descend — which is what makes the stagings many productions of one thing rather than many things.

**Experiment A — change the stage.** The same `Well` is run on a console; in a browser, with input and output carried over a real WebSocket connection, and in a second browser variant over HTTP with server-sent events; across two co-hosted actors coordinated by a StageManager, once in memory and once over a real TLS channel; and across three separate Docker containers, each a peer joined to the others over real container-to-container TLS. These are genuinely different deployments — different processes, machines, transports, and wire formats. The domain source is identical across all of them: a diff of the domain directory between the first staging and the last comes back empty (Appendix A). What changes from one staging to the next is host code — the shell that binds an input source and an output sink to the actor. The framework's own name for that shell is exact: the host is "an accidental shell" (`actor/IGameHost.cs:19`), and the same `Well` runs on `PerformanceV2` or on `StageV2` without knowing which. The example builds clean and its domain tests pass, unchanged, in every staging.

The last of these is where the claim is easiest to doubt, so it is worth making concrete. Three Docker containers run the same `Well` as three StageManager peers on a private bridge network. One node is the Director and two are casts, joined over Kestrel TLS on a port never exposed to the host; the coordination and replication traffic between them is genuine container-to-container TLS. A scripted driver on the Director plays the game — it spawns pieces and moves them, taking the board to a non-trivial state — and that gameplay replicates to the two casts, which converge to a byte-identical board (Appendix A). The game was played by a script rather than a person, but it was played: the `Well` genuinely mutated on one machine and the same state reached the others. Adding this staging changed the domain not at all — the diff of both the domain and the actor directories across the whole increment is empty, and the files it added are a new host, its Docker definitions, and notes. The same StageManager machinery that ran two co-hosted actors runs three separate containers; the `Well` is handed to each as a parameter, unaware of how many machines it is spread across.

A distributed demonstration invites more credit than it earns, so its boundaries belong in plain sight — and all of them bound the deployment, not the result. The peers exchange their initial rendezvous — a peer's address, its identity, its TLS fingerprint — out of band, through a shared volume, which is the analog-bootstrap hop of the systems paper (Paper 7) rather than an on-wire discovery; everything that then moves the game is on the wire. The Director role is fixed here; rotating it among peers is that paper's concern, not this one's. The TLS is trust-on-first-use, a self-signed certificate per container carried in the bootstrap, not certificate pinning against an authority. And live replication has a connect-readiness race the framework recovers from by its own catch-up path rather than in one uninterrupted stream. None of these touches what the staging is here to show — the domain's diff is empty regardless of any of them — but each marks where the deployment, not the domain, is doing approximate work, and the paper claims only what was run.

**Experiment B — change the client.** With the stage held at one host, the client is varied instead. The same board is played by a human at a keyboard reading an on-screen grid; by an automated player that sends moves over a pipe and reads the board through a view it computes for itself; by two passive observers, one that pulls the board and one that receives it pushed; and by a browser, in which a player and a spectator both run in a second language over the network. Six clients, in three runtimes, over one `Well` — and again the domain directory does not change across any of them (Appendix A). What each client adds is an input adapter, an output adapter, or both; none of them is a change to the game.

The two experiments vary independent things. Experiment A changes where the domain runs; Experiment B changes who observes it and how. Neither touches the domain, and that independence is the result: its constancy does not depend on where it is staged or on who is watching. The identity §1 argued to be prior to the staging appears here as an invariant the two variations cannot move — and the build graph says why it cannot be moved, since nothing the domain refers to changed, because the domain refers to nothing.

Here the difference from ports-and-adapters becomes a number rather than a claim. A hexagonal version of the same system would have the domain declare an output port and an input port and depend on them; adding the browser client, or the automated player, would mean implementing those ports, and a test of the domain would mean standing up a double for each. In this system the count is zero on both: no domain edit was made to add any staging or client above, and a test of the domain stands up no double for its output, because there is no port there to double. Dependency inversion relocates a dependency; this removes it, and the empty diff is what removal looks like.

## 3. What a Client Sees

Experiment B added clients by adding adapters, and the word carries the second half of the argument, so it is worth making precise. A client never reads the domain directly. It reads through an adapter, and the domain is the same behind every one of them.

What the domain emits is not a view but a raw fact. The board exposes its occupied cells as a bitmap — a grid of filled and empty squares (`domain/Well.cs:322`) — and the domain project, as §2 noted, refers to nothing: it has no notion of a screen, a renderer, or a client. The substrate pushes that raw frame outward on each move (`actor/TetrisActor.cs:43-47`); it, too, forwards the fact, not a projection of it.

Each client turns the fact into something it can use, and the turning is the client's work, not the domain's. The human's on-screen grid is produced by a renderer that lays the bitmap out as squares (`actor/BoardRenderer.cs:19-48`). The browser produces the same grid a second time, independently, in JavaScript — its code is commented as mirroring that renderer (`web/Program.cs:169-178`) — so the terminal grid and the browser grid are two adapters over one frame. The automated player needs something else: it reasons poorly over a bitmap, so it lifts the same frame into a column-height profile — a skyline, the gaps and wells between columns, a few aggregate figures (`tools/pile-scan.ps1:39-68`) — a form suited to deciding where a piece should go. None of these is the board's own appearance; the board has none to offer. It emits a fact, and each client projects it through an adapter chosen by the host that runs it.

This is where the on-screen grid loses its privilege. It is natural to treat the pixels a human sees as the real board and the automated player's vector as a derived abstraction of it. The system does not: both are adapters over the same emitted fact, and neither is closer to the domain than the other. The grid feels primary only because it is the one a person happens to read; the domain has no such preference, because it produces neither — it produces the fact both are made from. The automated player makes this hard to miss precisely because its adapter looks nothing like a human's. Faced with a client that reads the board as a vector of column heights, one notices that a human, too, only ever reads it through a rendering, and had simply stopped noticing.

This connects directly to the boundary Paper 8 drew. There, what an output *is* — its projection — was shown to belong to the actor that authors it, not to the domain that holds the material; a domain object that renders itself reaches past what it knows into a decision it does not own. Here the same boundary is seen from the client's side: the projection is authored per client, outside the domain, and the domain that would have rendered itself instead emits a fact and lets each client author its own view. The adapters are the input sources and output sinks of the earlier papers — a keyboard or a pipe on the input side; a terminal grid, a numeric vector, or an HTML canvas on the output side — bound to the actor from outside and exchanged without the domain's knowledge. That the domain does not know which adapter is attached is the same fact, seen once more, that let the stage and the client vary in §2 without moving it.

## 4. Actor and Audience

Section 3 answered *through what* a client reads the domain: an adapter, never the raw domain, and no adapter privileged. Hidden in the same place is a stronger fact, and it settles the matter before any architecture is discussed. **An audience can only see a play that has already been performed.** To watch is to receive what was done; a performance not yet performed is nothing to watch. The actor is acting and the audience is watching — distinct roles — and the audience's is downstream of the act by necessity, not by arrangement.

This is why the obvious shortcut cannot work. Each mutating command could return the board it produced — render in the command's own response, and the client that issued the move would have its picture at once. But that response is the actor's: it is the act as it happens, handed back to the one performing it. To serve an audience from it is to seat the audience in the actor's chair, offering as something to watch an act still in the doing. A client that only watches issues no command and so has no response to receive at all; the one that does command receives, in its response, its own action — not a performance to observe. The failure is not architectural but logical: a return value belongs to the actor, and watching is a different role. Every client in §2 and §3 that was not driving the game — the two casts of the replicated staging, the spectator, the automated player reading over a pipe, the browser onlooker — could see only because it read the record of what had already been performed. Rendering in the present serves an audience of one — the actor, who is also the only one acting; rendering from the record serves any audience at all, because a record is a performance already given. The present is the single seat where watcher and actor coincide; the past is every other seat.

This is not a new mechanism, and it is worth saying so plainly. The field already separates the two: command-query separation, and command-query responsibility segregation after it, hold that a view is not served from the write (Meyer, 1988; Young, 2010), and event sourcing builds a view as a projection over the recorded acts rather than a snapshot of current state (Fowler, 2005). The step here stands on that lineage and takes it one further, from mechanism to meaning: if the view is a projection over the record, then what a client sees is the *past* — not the state as it is, but the acts as they were — which is not a performance choice but a statement about what seeing a domain is. What a domain reads of *itself*, to decide its own next act, is a different reading: the game's host queries the current board — is a piece falling? — to choose a move, and that read is a live pull, in the present, belonging to the actor deciding. The two must not be run together. To render the audience's view from the command's response is to confuse the actor reading itself to act with an audience watching what was done — and once they are confused, the audience can be no one but the actor.

Naming the two apart is where this paper needs the one before it. An observer given only the present is given a snapshot — the configuration as it stands — from which, to learn how that configuration arose, it must reconstruct the history; that reconstruction is the defect Paper 8 traced. An observer given the past is given the account itself, the sequence of acts the domain performed, which Paper 8 named *testimony*: knowledge held on the word of the one that lived it, not rebuilt from stills. The past-tense view of this paper is therefore not merely consistent with Paper 8 — it is Paper 8's resolution made operational. To see a domain by reading its record is to receive its narrative as testimony, and it is the only way an audience that is not the actor can see the domain at all.

This returns to what the paper is about. Because what is seen is the recorded narrative and not a live state, the thing seen is a standalone object: it exists in the record, the same for every audience, independent of who reads it and when. And it is the same for every audience for the same reason the board was — one domain, referenced by every staging, produced it. The invariant §5 names — the domain as the common ancestor of its stagings — is what makes the narrative one narrative, readable the same from every seat. What remains the same when the staging changes is, in the end, what there was to see.

## 5. Identity

The two experiments leave a single fact to explain. A domain was run on five kinds of stage and read by six kinds of client, and through all of it nothing about the domain changed. A fact that survives that much variation is saying something about the thing that holds still, and §1 named it: the domain has an identity independent of where it is staged and to whom.

It is worth being precise about what that identity is, because the word invites more metaphysics than the evidence supports. The identity here is not a hidden essence; it is a position in the dependency graph. Every staging refers to the domain; the domain refers to no staging. Follow the references from any deployment — a container, a browser bundle, a console host — and they lead back to the same place; follow them from the domain and they lead nowhere. The domain is the common ancestor of all the stagings: the single node they descend from and share. That is the whole of the identity claim, and it is checkable, not interpreted — the graph either has that shape or it does not.

Read this way, *precede* is not a claim about time or ontology but the direction of that descent. A staging is built against a domain that already exists and does not know of it — the way a subclass is built against a base class it cannot alter. The domain does not *survive* its stagings; surviving would make the stagings the primary thing and grant the domain the modest virtue of enduring them. The relation runs the other way: the stagings exist because the domain does, and each is one descent from it. Nothing the domain refers to changed across the experiments because the domain refers to nothing — its identity was never at risk from a change downstream of it.

This has a second face, less formal than the graph and worth stating because it is what makes the identity legible to a person rather than only to a build tool. A domain with an identity is one whose parts can be understood as stable roles. To know what the board of the game *is* — that it holds a pile of settled cells, spawns and moves a falling piece, clears full rows, ends when a new piece cannot enter — is to know, structurally, what it does and does not do, what may be asked of it, and what it will never answer. This is not a psychological claim about how people understand stories; it is the observation that a role with a fixed identity fixes its own boundary, and that boundary is what domain-driven design reached for with its aggregates and bounded contexts — arrived at here by asking what stays the same, rather than by drawing a diagram of layers. The invariance measured in §2 and the legibility described here are one property seen twice: a thing that keeps its identity across every staging is a thing whose identity was there to keep.

## 6. Recognition

If a domain keeps its identity across every staging, then whatever it did keeps its identity too. The sequence of a domain's own acts — a piece spawned, moved, dropped; a row cleared — is recorded in its journal as the domain performed them, and because the domain is the same on every stage, that record reads the same from every one. This is the first, plain consequence of §5: the account of what happened is available directly, where it happened, and does not have to be reassembled from the outside.

That availability is what lets a client do more than watch. Given the sequence of acts, a reader can recognize in it the routine the acts compose — that these three moves and a drop were the placement of one piece, that this run of placements filled a row. Paper 3 built exactly this reading as a first-class operation: a reaction seeks a pattern across journal entries and matches the trajectory they form, so that a routine is not inferred after the fact from snapshots but recognized in the record as it stands. Recognition, in this paper, is that operation applied to a domain whose identity holds: because the acts are the same across stagings, the routine they compose is recognizable across stagings too.

This is where the paper meets the one before it. Paper 8 followed an output to the party that receives it and showed that, within a trust boundary, the receiver comes to know the state as *testimony* — an account it is told rather than one it reconstructs. But an account received is not yet a routine recognized: being told the sequence of acts is prior to seeing, in that sequence, what was done. This paper takes the next step, and reaches it as a consequence rather than a new claim — an account whose subject keeps its identity can be recognized as the same routine wherever it is told.

And the recognition is the client's, performed through the same adapter §3 described. The board does not announce "a row was cleared"; it records the acts, and each client recognizes the routine through the view it holds. The automated player reads its column-height vector and recognizes a well being filled; a person reads the grid and recognizes the same thing; neither recognition is the domain's, and neither is privileged over the other. What both recognize is one routine, because beneath both adapters is one domain with one identity — which is where the paper began.

## 7. Where the Narrative Lives

Section 6 observed that when a domain keeps its identity, the record of what it did is available directly, in one place, and can be recognized without reconstruction. This has a counterpart worth naming — and naming carefully.

When a system's identity is not kept in one place — when what a single process does is spread across several services, each with its own store and its own deployment — the sequence of what happened is no longer in one place to be read. It has to be reassembled after the fact, from the outside, by correlating records that were produced separately. This is, in part, what distributed tracing, correlation identifiers, and service maps do: they recover a narrative that the arrangement of the system did not keep in one legible place.

The observation is not that such tooling is unnecessary, or that a system should do without it. Where a process genuinely spans autonomous services, reconstructing its trajectory is real and useful work, and the tools that do it answer a real need. The point is narrower, and only about location: keeping a domain's identity in one place keeps its narrative in one place, so the account is read where it happened rather than rebuilt from scattered traces. The model does not remove the need to know what a system did; it changes where that knowledge already sits.

## 8. Limits

The claim is bounded, and the boundaries are worth drawing, the more so because the result is easy to overstate into a promise it does not make.

It holds only where there is something for it to hold of. A domain with a genuine identity, staged more than one way, is where the invariance has teeth; a program too small to have a domain distinct from its deployment, or one that will only ever run one way, has no second staging to be invariant across, and the apparatus is overhead. The claim is not that every program's domain is separable from its deployment in practice, but that where the two are genuinely distinct, the separation is real, buildable, and measurable — and that collapsing them, where they were distinct, is what the ordinary practice does.

Nor is the claim that a domain never changes. It says that a *change of staging* does not change the domain — not that the domain is frozen. Requirements grow, and some genuinely pull in new domain: a rule that did not exist, a distinction the model did not draw. When they do, the domain grows, and the honest account of how is a short ladder, never a rewrite. Most often the change is met below the domain entirely — a new client or a new stage is a new adapter, and the domain does not move, which is the whole of §2 and §3. Sometimes it is met by surfacing more of the domain that was already latent — a detail the model held but did not expose — which is additive: a new operation, a new entry in the record, nothing rewritten. Least often, a genuinely new domain is composed from existing ones. What the ladder never has is a bottom rung of starting over: growth is additive because an operation added is a new act in the record, not a reshaping of the acts already there. So "the staging changes, the domain does not" is not contradicted by a domain that grows — a growing domain still owes nothing to its stagings, and grows by accretion rather than by being torn down and rebuilt for each new way of running it.

Two narrower limits belong here as well. The demonstration is one domain on one framework, and it is an existence proof, not a measurement: it shows the separation is buildable and that its mechanical cost is a set of edits not made — a domain diff that stays empty — not that the separation is worth its apparatus at scale, or cheaper in production, which are questions for other work. And the distributed staging of §2 is bounded as that section stated: its peers meet through an out-of-band bootstrap, its coordinator role does not rotate, its transport trusts a certificate on first use, and its replication reaches agreement through a catch-up path rather than an uninterrupted stream. Each bounds the deployment; none touches the domain's invariance, which is the only thing the staging was there to test.

Finally, one axis this paper deliberately leaves aside. It varies where a domain runs and who observes it, and holds the domain invariant across both; it does not treat which actor talks to which — the addressing and topology of a system of many domains — a separate question with its own answer, not folded into this one.

## 9. Related Work

The separation of a domain from the machinery that runs it is not a new aspiration; reading it as a *measured invariance* rather than a design prescription is where this paper sits among its neighbours.

The oldest of them is information hiding: a module should encapsulate the decisions most likely to change, so that a change to one is not a change to all (Parnas, 1972). Where a system is deployed, and to which clients, are such decisions; but Parnas's concern is the internal decomposition of a program into modules, and the counsel is a principle to follow, not a property to check. This paper's difference from all of the work below is of that kind — it does not prescribe a decomposition but measures whether one holds.

Domain-driven design draws the boundary that §5 arrives at from the other direction (Evans, 2003). Its aggregates and bounded contexts mark what a part of a model *is* and where its responsibility ends — the same fixed boundary a stable identity was seen to fix. The routes differ: domain-driven design reaches the boundary by modeling, drawing it deliberately; here it is reached by asking what stays the same across every staging, so the boundary is what the invariance reveals rather than what a diagram declares.

Closest, and the comparison the result must survive, is hexagonal architecture — ports and adapters (Cockburn, 2005) — and its restatement as clean architecture, the domain at the center with every dependency pointing inward toward it (Martin, 2017). It would be natural to read this paper as one more instance of that idea. It is not, and the difference is geometric, not incremental. Hexagonal still keeps a *port* — an output interface the domain declares and depends on. The adapter is pushed outside, but the port, which is the contract, stays at the domain's edge, owned by the domain. Here there is no port. The domain declares no interface for its output or its clients; it emits facts under logical names and depends on nothing, so the whole contract — what an output means, where it goes, to whom — sits in the staging, outside the domain.

```
   Ports and adapters:
       Domain --declares--> Port --> Adapter
       (the contract is the Port, on the domain's edge, owned by the domain)

   Here:
       Domain        Staging --binds--> sink / client
       (no port; the contract is not inside the domain at all)
```

The contract has not been inverted; it has moved — off the domain's boundary and into the staging. That move is what lets the domain be the common ancestor of §5: a port is a thread from the domain back to the shape of its clients, a fragment of a staging lodged inside the domain, and while it is there the identity is not clean. The count §2 reported — zero domain edits per staging, zero test doubles for a domain output, against at least one of each under ports and adapters — is the observable trace of the move, not the point of it. The point is that the boundary the contract sits on is no longer inside the domain.

Operational portability has its own discipline in the twelve-factor app, which separates configuration from code and treats backing services as attached resources, so that one build runs unchanged across environments (Wiggins, 2011). That discipline makes a single application portable across deployments of the same shape; the claim here is broader and of a different type — one domain is the common ancestor not only of its environments but of different clients and a genuinely distributed topology, and the evidence is a diff that stays empty rather than a checklist of practices.

The nearest neighbour on the mechanism of §4 is the command-query lineage. Command-query separation (Meyer, 1988), and its architectural form, command-query responsibility segregation (Young, 2010), hold that a view is not served from the write; event sourcing (Fowler, 2005) builds a view as a projection over a log of recorded acts. This paper stands on that lineage rather than beside it (§4), and what it adds is not a mechanism but a reading of one: that a view built over the record is a view of the *past*; that this is what lets an audience which is not the actor see at all; and that serving the view from a command's response is the confusion of an actor's self-reading with an audience's watching. The mechanism is theirs; the epistemic and temporal reading of it is what is claimed here.

Two of this series' own papers hold pieces of the present result. Paper 7 argued that the server is an accidental category — that where a domain runs need not be a place the domain lives — which is one axis of the staging this paper varies. Paper 8 located, from the side of output, the boundary between what a domain holds and what an actor projects for a client, which is the adapter boundary of §3 seen from within a single output. This paper generalizes both: it varies the whole staging — where a domain runs and who observes it — and names what stays fixed across all of it as the domain's identity.
