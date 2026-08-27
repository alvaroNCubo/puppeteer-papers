# Paper 0A — the assembled-verb lab suite

The measurements of *The Assembled Verb* (`0A-assembled-verb.md`), as one MSTest suite. Every
count the paper prints resolves to an assertion or a printed output here; nothing in the paper's
Appendix A is described that cannot be re-run.

**Read `labs/paper0A-README.md` first** if you have not run these before. It states the three
prerequisites, what counts as reproduced, and the one trap — `dotnet test` passes twenty and prints
none of the paper's numbers.

## Pins

| what | where | commit |
|---|---|---|
| engine | `github.com/alvaroNCubo/puppeteer`, cloned as a sibling of this repository — `../../../puppeteer` counted from this directory, which is the path every lab here uses and the one the `.csproj` resolves | `3160e39` |
| commerce corpus | `github.com/dotnet/eShop` | `9b4f943` |
| modular-monolith corpus | `github.com/kgrzybek/modular-monolith-with-ddd` | `91c8ef2` |

The corpus commits are the same commits the dissection bundles of `paper0A-assets/` are based
on: the binaries the paper composes and the sources it dissects are one corpus, pinned once.

Set `-p:PuppeteerEngine=<path>` (or `$env:PuppeteerEngine`) if the engine checkout is not the
sibling. Both externals are checked before the build: a missing engine and a missing `corpus-bin`
each stop with a sentence naming what to do, rather than with a path error or several hundred
`CS0246`s.

## Run

```powershell
.\fetch-corpus.ps1      # once: clones both corpus repositories at the pinned commits, builds
                        # the three domain assemblies into corpus-bin/, records provenance
.\run-labs.ps1          # the 20 labs, with the numbers they print
```

Requires git and one .NET SDK able to target net9.0 — a 9.0.x SDK is enough. Nothing of either
corpus repository is redistributed in this archive.

Two notes the paper's own method makes load-bearing:

- **`WhatTheRecordCostsLab` needs Release.** §8's table was taken there. Its two byte counts are
  exact in either configuration; its four times are not comparable to the paper's until you run
  `.\run-labs.ps1 -Lab WhatTheRecordCosts -Release`. The lab says so itself when run in Debug.
- **The corpus commit is verified, not assumed.** `EveryNameStaysWithinOneSubjectsVocabularyLab`
  prints which two clones it read and at which commit, and fails if either is not at the pin: a
  population of 72 is a count over a corpus at a commit.

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
| `EveryNameStaysWithinOneSubjectsVocabularyLab` (+ `CENSUS-CODEBOOK.md`) | §6 | the census under two coding rules: population **27 + 16 + 29 = 72** reproduced mechanically from the pinned clones; rule one (≥2 aggregates in one name) codes **0** T; rule two hands its blind spot — zero-aggregate names, process words — to manual coding: **3** residuals, each published with its reason, all S; agreement **72/72**, between same-authorship instruments — no independent coder |
| `TheTrajectoryBecomesObservableLab` | §8 | the corollary: two reactions over unrelated patterns answer one question identically, correlated by the recorded act itself |
| `TheConstitutionPrototypedOnAThirdPartyEngineLab` | §3.2, §8 | the constitution arrangement prototyped on Orleans, the coordination row's own engine: **1** definition and **2** exercises in the engine's journal; a query performance leaves it unmoved; reconstruction **re-executes every journaled statement**; the domain-minted identity differs per replay |
| `WhereReplayEquivalenceBreaksLab` | §4.3, §8, App. C | the calculus's prediction put to the engine: a minted identity captured into a later act's recorded arguments breaks the composition's invariant on replay (true→**false**); the same composition referring to it by a supplied key holds (true→true), the mint fresh in both arms |
| `WhatTheRecordCostsLab` | §8 | the record's price (Release, n=10 after one discarded warm-up, median [min..max], environment printed): **554** bytes for definition + first exercise, **~112** per additional (exact); replay **5.0 ms** at 100 and **15.9 ms** at 1,000 exercises; **~2,430**/s through the plane vs **~533,000**/s direct — **220x**, the ratio of the printed medians, and the one figure that does not reproduce as a figure: six runs here gave 196x–220x, and an independent reproduction printed 137x and 362x on two other environments |

## Additional instruments

Retained in the suite, not cited by the paper's body: `AssembledVerbOverTwoDomainsLab`, `WhereTheComposedActIsWritableLab`,
`WhatEachRecordIsMadeOfLab`, `DisjointRepertoiresLab`, `WhatHoldsARepertoireTogetherLab`,
`FlatRepertoireNamespaceLab`, `CapturingADomainMintedIdentityLab`. They measure adjacent
properties (type-graph components, journal record contents, Eval's parameter-plane edge) and are
kept because their assertions guard the same corpus the cited labs run against.

## A reference run

One captured run of the twenty in Debug, and one of the price lab in Release, are published at
`data/paper0A-assembled-verb/` **in the papers repository**, from the author's machine at the pins
above — there to compare against, not to stand in for a run of your own. If you received this
suite as an archive rather than as a clone, check that those two logs came with it; a package that
carries only the labs will not have them, and the comparison is worth asking for.
