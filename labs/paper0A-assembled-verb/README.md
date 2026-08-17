# Paper 0A — the assembled-verb lab suite

The measurements of *The Assembled Verb* (`0A-assembled-verb.md`), as one MSTest suite. Every
count the paper prints resolves to an assertion or a printed output here; nothing in the paper's
Appendix A is described that cannot be re-run.

## Pins

| what | where | commit |
|---|---|---|
| engine | `github.com/alvaroNCubo/puppeteer`, cloned as the sibling `../../../puppeteer` every lab in this repository uses | *pinned at deposit* |
| commerce corpus | `github.com/dotnet/eShop` | `9b4f943` |
| modular-monolith corpus | `github.com/kgrzybek/modular-monolith-with-ddd` | `91c8ef2` |

The corpus commits are the same commits the dissection bundles of `paper0A-assets/` are based
on: the binaries the paper composes and the sources it dissects are one corpus, pinned once.

## Run

```
.\fetch-corpus.ps1      # once: clones both corpus repositories at the pinned commits,
                        # builds the three domain assemblies into corpus-bin/
dotnet test             # 20 labs, all green
```

Requires git and the .NET SDKs the corpus targets (net9.0 and net8.0) plus net9.0 for the
suite. Nothing of either corpus repository is redistributed in this archive.

## The labs the paper cites

| lab | paper § | what it measures |
|---|---|---|
| `TheCompositionBecomesACapabilityLab` | §2.2–2.3 | the composed verb performed twice with different arguments: **one definition, two invocations**, replay re-performs both from the one record |
| `WhoDecidesWhatCountsAsHistoryLab` | §2.4 | one body, one verb, three performances: query 2→2, command 2→3 (journaled, replays), query again 3→3 — modality is attributed per performance, not inherited |
| `TheCriterionForConstitutionLab` (+ `ProcessManagerHarness`) | §3 | the event-sourced coordination arm (the corpus's shape, author-built and labelled as such): names the whole, contains **0** of its statements; description **6**/no (nothing to replay); constitution **6**/re-performs |
| `TheEventSourcedCoordinatorOnAThirdPartyEngineLab` | §3 | the event-sourced coordinator on Orleans (in-process TestingHost, log-storage provider): **6** transitions and **0** operations in its own journal; reconstruction restores position, **re-performs nothing** |
| `TheCoordinationArmOnAThirdPartyEngineLab` | §3, §7.2 | the coordination arm on the Durable Task Framework — the engine beneath Azure Durable Functions — over its in-memory emulator, history read via the framework's public dispatch middleware: **6** invocations in the engine's own record; orchestrator code replayed across 6 episodes; **no operation re-performed** |
| `RepertoireBoundaryLab` (+ `RepertoireBoundaryFixture/`) | §4.2 | the host language is refused by the compiler (`CS0122`, obtained by invoking the compiler on a program written to fail) while the assembler reaches the internal piece (`marks:2`) |
| `RetrievalIsALookupLab` (+ `SubscriptionPaymentBridge`, `PaymentStore`) | §4.3 | across replay the assembler's key is stable and the domain-minted identity is not; the minting call site is marked `R_99` in the bridge |
| `ALongTrajectoryOverTwoUntouchedRepertoiresLab` | §5.2 | 17 operations, 4 aggregates, 2 shipped assemblies sharing no type, 0 test doubles; 9 events each naming a transition, none naming the trajectory |
| `WhatTheCarrierCarriesLab` | §6 | the same act carried three ways (direct, queued, kept) delivers identical values; only the identity a repertoire mints for itself differs |
| `EveryNameStaysWithinOneSubjectsVocabularyLab` (+ `CENSUS-CODEBOOK.md`) | §6 | the census, recoded by published rules: population **27 + 16 + 29 = 72** reproduced mechanically from the pinned clones; **0** names reference two subjects' aggregates, under a rule generous to the opposite finding; agreement with the author's reading **72/72** |
| `TheTrajectoryBecomesObservableLab` | §8 | the corollary: two reactions over unrelated patterns answer one question identically, correlated by the recorded act itself |
| `TheConstitutionPrototypedOnAThirdPartyEngineLab` | §3.2, §8 | the constitution arrangement prototyped on Orleans, the coordination row's own engine: **1** definition and **2** exercises in the engine's journal; a query performance leaves it unmoved; reconstruction **re-executes every journaled statement**; the domain-minted identity differs per replay |
| `WhatTheRecordCostsLab` | §8 | the record's price (Release, medians of 3): **554** bytes for definition + first exercise, **~112** per additional exercise; cold replay **5 ms** at 100 and **25 ms** at 1,000 exercises; **~2,100** exercises/s through the plane vs **~153,000** direct loops/s |

## Additional instruments

Retained in the suite, not cited by the paper's body: `AssembledVerbOverTwoDomainsLab`, `WhereTheComposedActIsWritableLab`,
`WhatEachRecordIsMadeOfLab`, `DisjointRepertoiresLab`, `WhatHoldsARepertoireTogetherLab`,
`FlatRepertoireNamespaceLab`, `CapturingADomainMintedIdentityLab`. They measure adjacent
properties (type-graph components, journal record contents, Eval's parameter-plane edge) and are
kept because their assertions guard the same corpus the cited labs run against.
