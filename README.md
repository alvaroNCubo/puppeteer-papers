# Research Program Overview

## What this is

A series of seven design theory papers, each naming a structural property of software systems that prior literature has documented piecewise without recognizing as one, and each tracing the consequences that follow.

## Single-volume reading

For a continuous reading — preface plus the seven papers as numbered chapters, with table of contents — see the unified monograph:

**[Puppeteer: Journaled Programs and the Dissolution of Infrastructure (PDF)](puppeteer-monograph.pdf)**

Individual papers below are the canonical citable artifacts; the monograph is the recommended entry point for a first read.

## The papers

| # | Title | Version | One-line summary |
|---|---|---|---|
| 1 | [Anti-porous architecture: a unified design principle for CQRS + Actor + Event-Sourcing systems](01-anti-porosity.md) | 0.2-draft | Names *porosity* — the representational sparsity defect that database theory, domain-driven design, REST API design, and Event Sourcing each document under a different name — and the conditions under which it is rejected. |
| 2 | [Program–value separability: the structural precondition for compilation, caching, and dense journaling in a DSL runtime](02-program-value-separability.md) | 0.2-draft | Names the structural property of DSL programs that makes compilation, cross-invocation caching, and dense journaling possible at all; what is commonly read as three runtime optimizations is one consequence. |
| 3 | [Reactions and the partition: opt-in eventual consistency in actor-native systems](03-reactions-and-partition.md) | 0.4-draft | Names the developer-controlled partition of an event's implied work into *now* and *deferred*, exercised through *Reactions*; opt-in eventual consistency falls out as a downstream consequence, not a design choice. |
| 4 | [Preserving semantic continuity across actors: a tell-based approach without orchestration](04-cross-actor-continuity.md) | 0.2-draft | Identifies the assumption naturalized across fifty years of actor-systems literature — that causation between actors is operational, not programmatic — and shows that it is a contingent historical choice rather than a necessity of the actor model. |
| 5 | [The Journal as Substrate: Unifying Deployment, Replication, Backup, and Offline Operation in Distributed Systems](05-substrate-operations.md) | 0.2-draft | Identifies four operational disciplines documented separately in the distributed-systems literature as instances of a single structural condition, and names the property under which they dissolve into one mechanism. |
| 6 | [Most infrastructure layers are symptoms of the persistence model: a construct for auditing production stacks](06-infrastructural-symptom.md) | 0.1-draft | Names the property by which a layer of infrastructure can be discriminated as compensating for a deficiency of the persistence model rather than serving a requirement of the problem domain. Redis is the canonical worked example. |
| 7 | [After the substrate: building software without a datacenter](07-after-the-substrate.md) | 0.1-draft | Observes the joint operational consequence of the prior six papers: under a journaled-program substrate, cloud and microservice ecosystems become libraries the program invokes rather than habitats the program inhabits, and the datacenter ceases to be a structural requirement of running production software. The server-role becomes ephemeral as a side observation; the practical consequence is the headline. |

## How they relate

The papers form a chain of preconditions: each construct names a property that becomes available once the prior papers' properties hold.

Paper 1 establishes the vocabulary of *porosity* and *anti-porosity*. Paper 2 names *program–value separability* as the structural property that compilation, caching, and dense journaling all depend on. Paper 3 names the *now/deferred partition* exercised through *Reactions*; the partition is exercisable because the journal is dense (Paper 1) and the Reactions are programs (Paper 2). Paper 4 extends semantic continuity across actor boundaries through *tell*, which sits inside the Reactions surface of Paper 3. Paper 5 reframes deployment, replication, backup, and offline operation as instances of a single substrate property. Paper 6 names *infrastructural symptom* as the property by which compensatory layers can be diagnosed in any architecture. Paper 7 observes the joint operational consequence of the prior six: under a journaled-program substrate the datacenter ceases to be a structural requirement of running production software, and software construction passes back into the hands of those who model the domain.

## What is Puppeteer, and why it appears here

*Puppeteer* is a runtime that combines CQRS, the Actor Model, and Event Sourcing with a domain-specific language whose programs are journaled as the unit of persistence. Across the seven papers it appears as the instantiation — the existence proof that the conditions each construct defines can be realized in a working system. It is neither the subject of the papers nor required by them. Each construct could in principle be instantiated by other systems: an extension of Akka or Microsoft Orleans, a fresh runtime built around a journaled DSL, or a framework that has not yet been written. The role of the instantiation is to demonstrate realizability; the contribution lies in the constructs themselves and the conditions they name.

## How to read

If your question is specific, an entry point may answer it without reading the whole series:

- *"Which of the layers in my production stack are actually structural, and which are compensating for something?"* → Paper 6.
- *"What becomes operationally possible when production software no longer needs a datacenter?"* → Paper 7. (For the auxiliary question — *"is the server-role in my architecture a necessity, or a contingent artifact?"* — Paper 7 §7 offers a diagnostic lens.)
- *"How does a journaled program actually run — across actor boundaries, across machines, across failures, across deployments?"* → Papers 4 and 5.
- *"What are the structural preconditions on which the rest of the series rests — vocabulary, compilation, consistency model?"* → Papers 1 through 3.

For the full argument, read in order from Paper 1 through Paper 7. The papers are cumulative — each relies on vocabulary and conditions established by its predecessors. Paper 1 is the entry point; Paper 7 is the closing claim.

All seven are working drafts (versions 0.1 through 0.4) and are open to feedback. Issues are welcome at https://github.com/alvaroNCubo/puppeteer-papers/issues.

## How to cite

The following papers are archived in Zenodo with citable DOIs:

**Paper 6** — *Most infrastructure layers are symptoms of the persistence model*:

> Rivera, A. (2026). *Most infrastructure layers are symptoms of the persistence model: a construct for auditing production stacks* (v0.1) [Preprint]. Zenodo. https://doi.org/10.5281/zenodo.20317450

**Paper 7** — *After the substrate*:

> Rivera, A. (2026). *After the substrate: building software without a datacenter* (v0.1) [Preprint]. Zenodo. https://doi.org/10.5281/zenodo.20398998

The remaining papers can be cited by their GitHub URLs while their Zenodo depositions are pending.

## License

This repository contains two kinds of content, licensed separately.

**Papers and accompanying assets** — the seven `0X-*.md` files, this README, and the asciinema recordings and GIFs under `paper7-assets/` — are licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](LICENSE-papers). You may copy, redistribute, adapt, and build upon them, including for commercial purposes, provided you give appropriate attribution to the author.

**Code** — the C# projects under `labs/`, used as reproducibility artifacts for the papers' empirical sections — is licensed under the [Apache License 2.0](LICENSE-code).

The two licenses apply independently. Citing or quoting from the papers is governed by CC BY 4.0; using or modifying the code is governed by Apache 2.0.

Copyright © 2026 Alvaro Rivera.
