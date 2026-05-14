---
title: "Operations from the substrate: deployment, replication, backup, and offline operation as replay variants of a journaled program"
author: Alvaro Rivera
affiliation: Ncubo
date: 2026-05-12
version: 0.1-draft
status: draft
keywords:
  - design theory
  - event sourcing
  - journal-based program
  - substrate
  - zero-downtime deployment
  - cross-datacenter replication
  - backup
  - offline operation
  - replay
  - actor systems
  - puppeteer framework
abstract: >
  [ABSTRACT PLACEHOLDER — D1]
  First sentence MUST classify the genre as design theory (Hevner, March,
  Park, Ram 2004). 3–5 sentences: the problem (four disciplines treated as
  separate), the observation (the separation is contingent), the structural
  property (journal-as-substrate), the consequences (four equivalences
  collapse to replay). No Puppeteer mention in the abstract.
canonical_url: https://[pending]/papers/operations-from-substrate-v1
---

# Operations from the substrate

<!-- D15: TL;DR closing line is the KEY SENTENCE. Mirror Paper 4 "Forty years…" -->

## TL;DR

> [TL;DR PLACEHOLDER — D1 drafts opening; D15 fills closing line]
>
> [KEY SENTENCE]
> *"For a journaled program, deployment is replay, replication is sharing history, backup is copying the program, and offline operation is delayed replay."*

---

<!-- D1: §1 Introduction. Capa 1 (genre paragraph) obligatoria. No Puppeteer mention. -->

## 1. Introduction

[§1 PLACEHOLDER — D1]

---

## 2. Genealogy: four separate operational disciplines

For decades, four operational concerns — deployment downtime, cross-datacenter replication, backup, and offline operation — have evolved as separate bodies of literature and practice. Each developed its own vocabulary, its own apparatus, and its own cost model. The genealogies traced below are not exhaustive; they are sufficient to establish that the canon treats these four as distinct disciplines, each with a dedicated mechanism, and that the resulting fragmentation of the operational stack has not been challenged within any of the four traditions.

### 2.1 Deployment downtime

The earliest deployment practice in production systems was the cutover: stop the running service, swap binaries, start it again. Downtime was accepted as the cost of release. The first patterned response was articulated by Fowler in *BlueGreenDeployment* [Fowler 2010], which named a procedure in which "you ensure that you have two production environments, as identical as possible" and traffic is switched from one to the other at release time. The mechanism is environmental: two parallel deployments, one apparatus for routing, one for binary distribution.

The continuous-delivery literature ratified the pattern. Humble and Farley [Humble & Farley 2010] generalized blue-green into a broader release-engineering discipline in which deployment is a build-pipeline output rather than an operator action, and downtime is a regression to be detected rather than a default to be accepted.

The modern instantiations are canary release [Sato 2014] and the orchestration-mediated rolling update [Burns et al. 2016; Kubernetes documentation]. Canary release introduces partial traffic exposure before full cutover, treating the deployed binary as a hypothesis tested against a fraction of production load. The Kubernetes rolling update replaces pods incrementally under a readiness-probe gate, making the deployment apparatus a property of the orchestrator rather than of the application.

A parallel strand of the deployment canon addresses schema and data migration. Ambler and Sadalage [Ambler & Sadalage 2006] formalized *database refactoring* as a discipline in which every change to the data store is recorded as a forward-and-backward pair of scripts versioned alongside the application code. The pattern is instantiated by migration frameworks such as Rails Migrations, Liquibase [Liquibase project], and Flyway [Flyway project], which apply versioned scripts to the data store at release time. The migration apparatus is separate from the traffic-switching apparatus: it runs adjacent to — or before — the cutover, mutates the store in place, and produces a schema or data shape that the next binary version expects.

Across these mechanisms the shape is constant: a separate apparatus is built whose function is to absorb the change — of binary, of schema, or both — without interrupting the service. The apparatus is external to the program; the program is unaware that it is being replaced or migrated.

### 2.2 Cross-datacenter replication

Replication across datacenters originates in database engines. MySQL replication [MySQL Reference Manual] established the canonical primary/replica topology in the 1990s: the primary writes a binary log of statements or row-level events, and replicas tail that log. The replication mechanism lives inside the database engine; the application program is unaware that replication is occurring.

The pragmatic maturation came with change-data-capture (CDC). Debezium [Debezium project] and Maxwell [Maxwell project] read the database's binary log and re-emit each change as an event on an external transport, typically Kafka. CDC inserts an apparatus between the database and downstream consumers: a process that watches state changes in one system and reconstructs them as event streams for others. The reconstruction step is necessary precisely because the originating system does not emit events — it emits state changes that CDC observes and translates.

The modern instantiations extend the apparatus topologically. Cassandra's multi-datacenter replication [Lakshman & Malik 2010] embeds the replication topology in the storage engine itself, allowing writes accepted in any datacenter to propagate to peers under a configurable consistency level. Kafka MirrorMaker [Kreps, Narkhede, & Rao 2011] generalizes the pattern for log-structured systems: a dedicated process consumes from one cluster and re-produces to another, with offsets tracking progress per topic.

Across these mechanisms the shape is constant: cross-datacenter replication requires a separate apparatus — engine-internal, engine-adjacent, or fully external — whose function is to observe the changes happening in one location and reproduce them elsewhere.

### 2.3 Backup

The backup canon predates the others. Agent-based backup [Bacula project; Veeam documentation] developed in the 1990s and 2000s as a discipline in which a dedicated process periodically reads the production data store and writes a copy to an archival medium. The agent is external to the application; backup occurs at the storage layer.

The pragmatic maturation introduced snapshot semantics. The WAFL filesystem [Hitz, Lau, & Malcolm 1994] established the modern primitive: a point-in-time copy taken at the filesystem level, exploiting copy-on-write so that the snapshot is cheap and consistent with respect to the moment of capture. Database engines adopted the same pattern under the name *point-in-time recovery* [PostgreSQL documentation], in which continuous archival of the write-ahead log permits reconstruction of any committed state within the archived range.

The modern instantiations move the apparatus to object storage. Cloud-era backup [AWS S3 Lifecycle; Azure Blob Storage backup documentation] treats archival as a property of the storage tier rather than of a dedicated agent: lifecycle policies move data to colder tiers automatically, and recovery is a matter of retrieval from the archive.

Across these mechanisms the shape is constant: backup is a separate apparatus that produces a copy of state and is exercised at recovery time to reconstruct that state. The backup procedure observes the data store from the outside; the program being backed up does not participate.

### 2.4 Offline operation

The offline-operation canon is the most heterogeneous of the four, because it spans network-layer, transport-layer, and application-layer treatments of disconnection. The early precedent is store-and-forward messaging: UUCP and, later, SMTP queue undeliverable messages locally and retry until the remote endpoint is reachable. Disconnection is absorbed by a queue at the boundary of the system.

The pragmatic maturation generalized the boundary queue into a programmable apparatus. RabbitMQ [RabbitMQ documentation] introduced dead-letter exchanges to capture messages whose delivery is impeded, deferring them for retry or human inspection. Kafka [Kreps, Narkhede, & Rao 2011] generalized the pattern further: consumer offsets are explicit, durable, and per-partition, so a consumer that goes offline resumes from its last committed offset when it returns. The queue is no longer a remediation mechanism for failed delivery; it is the substrate over which the consumer's progress is tracked.

The modern instantiations carry the pattern into application-level state. Eventually-consistent stores [DeCandia et al. 2007] treat each replica as a local source of truth that converges with peers when connectivity permits. The Dynamo paper established the design vocabulary — vector clocks, hinted handoff, read-repair — that subsequent systems (Cassandra, Riak, Redis replication [Redis documentation]) instantiate in varying degrees.

Once again, the shape is constant: offline operation requires a separate apparatus — a queue, an offset, a convergence protocol — whose function is to absorb the period during which the system cannot reach its counterpart.

### 2.5 Where the operational complexity lives

The four disciplines surveyed above can be compared along a single matrix. Each row names a discipline; each column names a dimension of the apparatus the canon developed to handle that discipline.

| Discipline | Where the complexity lives | Apparatus | Cost |
|---|---|---|---|
| Deployment | Outside the program, in the release pipeline | Two-environment switch (blue-green); incremental pod replacement under a readiness gate (rolling update); fractional traffic exposure (canary); versioned schema and data migration scripts (Liquibase, Flyway, Rails Migrations) | Duplicate production environment; orchestration layer; manual or automated cutover procedure; forward-and-backward migration pairs |
| Cross-datacenter replication | Inside the storage engine, or in an adjacent CDC layer | Binary-log shipping (engine-internal); change-data-capture (engine-adjacent); multi-DC topology (engine-aware) | Replication lag; reconstruction overhead in CDC; consistency-versus-availability configuration surface |
| Backup | Outside the program, in a dedicated archival pipeline | Agent-based copy; filesystem or engine snapshots; continuous log archival (PITR); object-store lifecycle | Recovery-time objective and recovery-point objective; storage-tier cost; periodic exercise of the recovery procedure |
| Offline operation | At the boundary between system and counterpart | Persistent queue with retry semantics; durable consumer offsets; convergence protocol over replicated stores | Buffer sizing; offset management; eventual-consistency reasoning at the application layer |

Three observations follow from the table. The location of the complexity differs across rows: the deployment apparatus lives in the release pipeline, the replication apparatus lives inside or beside the engine, the backup apparatus lives in a separate archival pipeline, and the offline-operation apparatus lives at the system's boundary. The mechanism in each row was developed without reference to the others: the blue-green literature does not cite the CDC literature; the snapshot literature does not cite the consumer-offset literature. The four literatures evolved in parallel, each solving what it believed to be a different problem. And the cost in each row is paid separately: a system that wishes to be zero-downtime, cross-datacenter, recoverable, and tolerant of disconnection budgets independently for each of the four columns.

The canon, taken as a whole, presents these four as four different problems requiring four different apparatuses. §3 names the assumption that made this fragmentation appear necessary.

---

<!-- D3: §3 The assumption named. Short, sharp. 2-3 paragraphs. -->
<!-- Mirror: Paper 4 §3. No Puppeteer mention. -->

## 3. The assumption named

[§3 PLACEHOLDER — D3]

---

<!-- D4: §4 The substrate theorem. Backbone of the paper. -->
<!-- Capa 2 (theorem-voice) and Capa 3 (central diagram) load-bearing. -->
<!-- No framework names. Theory only. -->

## 4. The substrate theorem

### 4.1 The substrate

The first two papers in this series established two structural properties of the journal that this paper takes as preconditions. [Paper 1](01-anti-porosity.md), §3 named *anti-porosity*: the journal records the operations of the program rather than the states they produce, and admits no representational sparsity at the boundary between domain code and persisted form. The journal is therefore homoiconic — entries are sentences in the same language the program is written in — and dense — every effect that crosses the boundary is named in that language, not summarised as state delta. [Paper 2](02-program-value-separability.md), §1.2 and §3 named *separability*: programs that are parametric in their arguments admit a compiled form in which the operation's body is cached under an identifier and the journal entry reduces to that identifier plus the call's arguments. Together the two conditions yield a journal whose entries are compact, named operations rather than serialised states.

Under these two conditions the journal is not a record kept by the program; it is the program written out over time. Each entry is a sentence the program has uttered; the journal is the corpus of those sentences in the order they were uttered. The naming move of this paper is to call this artifact *the substrate of the program*: the journal does not store the program's outputs, it constitutes the program's existence over time. The program and the journal are the same artifact under two readings — one synchronous, one diachronic. Under the synchronous reading, the runtime is the program. Under the diachronic reading, the journal is the program.

### 4.2 The theorem statement

The theorem of this paper can be stated as a single proposition.

> *Given a program whose execution is recorded as a journal satisfying the anti-porosity condition of Paper 1 and the separability condition of Paper 2 — that is, a journal that is homoiconic, dense, and made of compact named operations rather than serialised states — the following four equivalences hold.*

**E1 — Deployment is replay.** A new version of the runtime, started from the substrate, reconstructs the program by reading the journal sequentially and applying each entry. Nothing about the new version's instantiation differs in kind from what the previous version did at every entry up to that point; the act of bringing a process to a state in which it can serve requests is the act of replaying the substrate up to its present head. Deployment therefore differs from steady-state operation only in the position of the read cursor over the same corpus.

**E2 — Replication is sharing history.** A second instance of the program, running on a different machine or in a different site, reconstructs the program by reading the same sequence of entries the first instance wrote. The act of replicating is the act of transmitting the substrate from one site to another and replaying it at the destination; no state extraction, no schema mapping, and no change-data inference enter the act. Replication therefore differs from deployment only in the site at which the same corpus is replayed.

**E3 — Backup is copying the program.** A consumer that reads entries from the substrate and persists them locally — without instantiating the runtime that would replay them — holds a copy of the program in the form in which it was written. The act of backing up is the act of duplicating the substrate; recovery is the act of replaying the duplicate. Backup therefore differs from replication only in whether the destination chooses to replay the same corpus or merely to hold it.

**E4 — Offline operation is delayed replay.** A consumer that is unreachable when entries are written, and that catches up by reading them later from a persistent buffer or directly from the source, applies the same replay to the same sequence as a consumer that received the entries in real time. The act of operating offline is the act of replaying the substrate after a delay; the delay does not enter the program, only the cursor of the consumer. Offline operation therefore differs from replication only in the elapsed time between the write and the read of the same corpus.

The four equivalences are not parallel restatements of a single mechanism. Each addresses one classical operation of distributed systems (§2) and shows that the operation, as the canon presents it, dissolves into a single primitive — replay — once the substrate's properties are taken as given. E1 addresses the time axis (a new version replacing an old one in place); E2 addresses the space axis (a new site running the same program); E3 addresses the storage axis (a consumer holding the corpus without running it); E4 addresses the connectivity axis (a consumer reading later than the writer wrote). The four together exhaust the operational variations that the canonical literature in §2 treats as separate disciplines. The operational specifics of how each replay is realised — handoff, transmission, passive ingestion, catch-up — are taken up in §5; §4 confines itself to the structural claim.

### 4.3 Apparatus: the substrate and its four replay arrows

The shape of the theorem can be rendered as a simple spatial arrangement. No new content is introduced; the arrangement makes the four equivalences of §4.2 visible at a glance, with the substrate at the centre and the four conditions arrayed around it.

The substrate — the journal as program — sits at the centre. From it radiate four equivalences, each corresponding to one classical operational discipline:

- **E1 — Deployment**: replay to the present head.
- **E2 — Replication**: replay of the same corpus at another site.
- **E3 — Backup**: the corpus held without replay.
- **E4 — Offline operation**: replay after a delay.

The vertical pair (E1, E4) varies the temporal position of the replay — at the present head, or after a delay. The horizontal pair (E2, E3) varies the spatial position of the corpus — replayed at a different site, or merely held at one. The canonical literature in §2 names four disciplines by looking at the destinations; viewed from the substrate, only replay occurs, under four conditions on when and where.

---

<!-- §5: Consequences. One sub-§ per equivalence. Each gated by its lab. -->

## 5. Consequences: four equivalences as variants of replay

<!-- D5: §5.1 E1 Deployment is replay — fulfilled by L1 (closed 2026-05-13). -->

### 5.1 E1 — Deployment is replay

§5.1 opening sentence (draft, ready for placeholder substitution):

Under compact-action régime with AlwaysCompiled compilation, the substrate replays N events at a sustained rate of 451k events/sec p50 at N=100k events, completing the deploy in 221.6 ms p50 (p95 = 237.4 ms). The rate holds through the N=1M anchor at 416k events/sec. The handover tail (Lock + Unlock window after the bulk replay) is < 0.03 ms at every N measured — bulk replay accounts for >99.8 % of the deploy window in this régime — confirming that deployment is replay, and replay is linear in compact-event count, not in expanded state.

<!-- D6: §5.2 E2 Replication is sharing history — gated by L2 + L3 -->

### 5.2 E2 — Replication is sharing history

[§5.2 PLACEHOLDER — D6; cite `[L2 HEADLINE]` and `[L3 HEADLINE]` when labs close]

<!-- D7: §5.3 E3 Backup is copying the program — fulfilled by L4 (closed 2026-05-14) + F1 (closed 2026-05-12 PM, satisfied by composition). -->

### 5.3 E3 — Backup is copying the program

§5.3 opening sentence (draft, ready for placeholder substitution):

Under the same compact-action régime as §5.1, a destination journal converges at the backend's append rate: ~2 400 events/sec on FileSystem, ~1 000 on MySQL, and ~760 on SQL Server at N=100 000 events. The wire round-trip — pulling records, confirming a watermark, optionally augmented by a reaction-state snapshot — accounts for less than 0.6 % of the cycle even at N=100 000; the substrate cost is the backend's append throughput, not the operation. A replica reconstructed over the destination journal reaches the same in-memory state as the primary in 18 of 18 measured cells across three backends and three journal sizes — confirming that backup is the program copied to another substrate, and replay against that copy is indistinguishable from replay against the primary.

<!-- D8: §5.4 E4 Offline operation is delayed replay — gated by L5 -->

### 5.4 E4 — Offline operation is delayed replay

[§5.4 PLACEHOLDER — D8; cite `[L5 HEADLINE]` when L5 closes]

<!-- D9: §5.5 The latency budget — gated by L6 -->

### 5.5 The latency budget

[§5.5 PLACEHOLDER — D9; cite `[L6 HEADLINE]` when L6 closes]

<!-- D10: §5.6 Forensic operations as substrate consequences (brief, no claim number). -->

### 5.6 Forensic operations as substrate consequences

[§5.6 PLACEHOLDER — D10; max 2 paragraphs]

---

<!-- D11: §6 Why existing approaches duplicate work. Mirror Paper 4 §6. -->

## 6. Why existing approaches duplicate work the substrate already does

### 6.1 Blue-green vs substrate

[§6.1 PLACEHOLDER — D11]

### 6.2 Change-data-capture vs event streaming

[§6.2 PLACEHOLDER — D11]

### 6.3 Snapshot backup vs passive consumer

[§6.3 PLACEHOLDER — D11]

### 6.4 Message queue buffer vs persistent local journal

[§6.4 PLACEHOLDER — D11]

### 6.5 The substrate match table

<!-- Table 4×2 style Paper 4 §6.4: Pattern / Does it match the substrate property? -->

[§6.5 TABLE PLACEHOLDER — D11]

---

<!-- D12: §7 Instantiation. Voice shifts from neutral to authorial here. -->
<!-- Gated by all labs closed + F1 shipped. -->
<!-- §7.0 is brief; cross-ref Paper 3 §6 for full architecture vocabulary. -->

## 7. Instantiation: realization in Puppeteer

### 7.0 Origins

<!-- One paragraph. Cross-ref Paper 3 §6. Do NOT re-present Choreography / Theater / Ensemble / StageManager. -->

[§7.0 PLACEHOLDER — D12]

### 7.1 Theater.aliveGate: the red-black mechanism

<!-- State machine: Recovering / Recovered / Alive. Load-bearing for claim 1. -->

[§7.1 PLACEHOLDER — D12]

### 7.2 Event streaming: push-to-pull inversion

<!-- SVIX as witness. Capa 2 strict: "we observe that an off-the-shelf webhook delivery -->
<!-- service satisfies the substrate's consumer-pull requirement" -->

[§7.2 PLACEHOLDER — D12]

### 7.3 Passive consumer: replicate without an actor running

<!-- Gated by F1. If F1 not shipped, mark as proposal (Capa 2 — witness, not derived proposal). -->

[§7.3 PLACEHOLDER — D12; gated by F1]

### 7.4 Numbers

<!-- Pointer to §5.5 without duplicating. -->

[§7.4 PLACEHOLDER — D12]

---

<!-- D13: §8 Relation to previous work in this paper series. Short. -->

## 8. Relation to previous work in this paper series

[§8 PLACEHOLDER — D13]

<!-- Cross-references table: paper / section / what it provides to Paper 5. -->

[§8 CROSS-REF TABLE PLACEHOLDER — D13]

---

<!-- D14: §9 Counter-arguments. 3-4 objections refuted from construct, not framework. -->

## 9. Counter-arguments

### 9.1 "Loading a large journal takes time"

<!-- MySQL 1-hr anecdote; replay over compact journal is linear in events, not state. L1 materializes. -->

[§9.1 PLACEHOLDER — D14]

### 9.2 "Change-data-capture already solves replication"

<!-- CDC reconstructs events from state changes; substrate emits them directly. Entropy comparison (L2). -->

[§9.2 PLACEHOLDER — D14]

### 9.3 "Snapshot backup is simpler"

<!-- Snapshot captures state at a point; replay reconstructs program + permits re-derivation. -->

[§9.3 PLACEHOLDER — D14]

### 9.4 "Append does not scale beyond N writes/sec" (optional)

<!-- L6 reference + honest caveats on batched-write regimes. -->

[§9.4 PLACEHOLDER — D14 — optional]

---

<!-- D15: §10 Conclusion. KEY SENTENCE repeated. No new material. -->

## 10. Conclusion

[§10 PLACEHOLDER — D15]

> [KEY SENTENCE — repeated as structural closing]
> *"For a journaled program, deployment is replay, replication is sharing history, backup is copying the program, and offline operation is delayed replay."*

---

<!-- Appendix A: code refs for verification. Populated by D12 + each Dx as it cites. -->

## Appendix A. Source-code verification

[APPENDIX A PLACEHOLDER]

<!-- Anchors (verify against current master of Puppeteer Pacifico before publish): -->
<!-- - Puppeteer/EventSourcing/ActorHandler.cs — LockWhileNotSyncronized, UnlockAndRunAlive, -->
<!--   ReplayPendingEventsForRedBlack, CatchUpFromJournal, state machine Recovering/Recovered/Alive -->
<!-- - Puppeteer/EventSourcing/ActorHandler.cs:1993 — PerformArchive (F1 anchor) -->
<!-- - Puppeteer/StageHook.cs — public delegation -->
<!-- - Choreography/Theater/Performance.cs — Start(asFollower:), IsAlive -->
<!-- - Choreography/Ensemble/EnsemblePerformance.cs — LockAllWhileNotSyncronized, UnlockAllAndRunAlive, AreAllAlive -->
<!-- - Choreography/Ensemble/EnsembleDeployment.cs — ActorLocation routing, drain orchestration -->
<!-- - Puppeteer/EventSourcing/JournalWriter.cs — local persistence -->
<!-- - ApplyReplicatedEvent in ActorHandler.cs — base pattern for ReplayPendingEventsForRedBlack -->
<!-- - SVIX integration — NOT LOCATED in current memories. Locate before drafting §5.2 and §7.2. -->

---

<!-- Appendix B: bibliography. Populated incrementally. -->

## Appendix B. Bibliography

[APPENDIX B PLACEHOLDER]

<!-- Canonical references expected: -->
<!-- - Hevner, March, Park, Ram (2004) — Design Science in Information Systems Research -->
<!-- - Papers 1-4 of this series -->
<!-- Cited in §2 (D2 closed 2026-05-12): -->
<!-- - Fowler, M. (2010) — BlueGreenDeployment, martinfowler.com/bliki -->
<!-- - Humble, J., & Farley, D. (2010) — Continuous Delivery (Addison-Wesley) -->
<!-- - Sato, D. (2014) — CanaryRelease, martinfowler.com/bliki -->
<!-- - Burns, B., Grant, B., Oppenheimer, D., Brewer, E., & Wilkes, J. (2016) — Borg, Omega, and Kubernetes (CACM 59(5)) -->
<!-- - Kubernetes documentation — Rolling Update Deployment Strategy -->
<!-- - Ambler, S. W., & Sadalage, P. J. (2006) — Refactoring Databases: Evolutionary Database Design (Addison-Wesley) -->
<!-- - Liquibase project — liquibase.org -->
<!-- - Flyway project — flywaydb.org -->
<!-- - MySQL Reference Manual — Replication -->
<!-- - Debezium project — debezium.io -->
<!-- - Maxwell project — maxwells-daemon.io -->
<!-- - Lakshman, A., & Malik, P. (2010) — Cassandra: A Decentralized Structured Storage System (ACM SIGOPS 44(2)) -->
<!-- - Kreps, J., Narkhede, N., & Rao, J. (2011) — Kafka: a Distributed Messaging System for Log Processing (NetDB Workshop) -->
<!-- - Bacula project — bacula.org -->
<!-- - Veeam documentation -->
<!-- - Hitz, D., Lau, J., & Malcolm, M. (1994) — File System Design for an NFS File Server Appliance (USENIX) -->
<!-- - PostgreSQL documentation — Continuous Archiving and Point-in-Time Recovery -->
<!-- - AWS S3 Lifecycle documentation -->
<!-- - Azure Blob Storage backup documentation -->
<!-- - RabbitMQ documentation — Dead Letter Exchanges -->
<!-- - DeCandia, G., et al. (2007) — Dynamo: Amazon's Highly Available Key-value Store (SOSP) -->
<!-- - Redis documentation — Replication -->
