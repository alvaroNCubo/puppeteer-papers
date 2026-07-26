# Parked for a paper of its own: an incomplete record answers plausibly

Parked 2026-07-26, to be written **after** the Puppeteer series is complete. Paper 9 reports it in
§8.4 in about 250 words, subordinated to its own claim, and says explicitly that it is not the
paper's result. That is the right scope for Paper 9 and the wrong scope for the finding.

## The finding

Where a record is incomplete, a journaled arrangement does not fail loudly — **it answers
plausibly**. A gap yields not an error but a smaller, internally coherent story.

## Why it deserves its own paper

**The evidence structure is stronger than anything else in Paper 9.** Three labs met it
independently, by three different routes, none of them investigating it, and each took it for a
local incident before the others were known. Convergent evidence from independent instruments, with
no confirmation bias in the finding, because nobody was looking.

Three symptoms, all measured (Paper 9, Appendix A):

| Lab | Symptom | Why it passed unnoticed |
|---|---|---|
| H | a routine whose closing act was never recorded matched the *wrong* closing entry | the count of routines came out right |
| I | a replay truncated by a defect in the substrate's sparse index | the board was internally consistent, and in one case exactly correct — the lost acts happened to clear no rows |
| G | a rehydration dropping every zero-argument invocation | it logged each one, carried on, and returned a started actor with quietly incomplete state |

**It is distinct from the nearest named failure mode, and worse in one respect.** *Gray failure* is
defined by *differential observability* — the application's view diverging from the observer's
(Huang et al., 2017, HotOS). Here there is none: every reader reads the same record and it is
consistent, so no second view exists to disagree. What detects it cannot be another observer. It has
to be a **ground truth carried forward** from when the acts were performed.

**And it yields a transferable rule, already stated and cheap:** a replay measurement must carry the
state recorded at play time and assert against it, or it will pass while being wrong.

## What the paper would have to add that Paper 9 does not have

- Scope: Paper 9's rule is about *measuring*, not *running*. Whether production readers of a record
  need the same discipline is untested and is the obvious first question.
- Generality: the exposure belongs to any reader reconstructing state from an append-only record, not
  to this substrate. That claim wants evidence outside Puppeteer — an event-sourced system, a CDC
  pipeline, a Kafka-log projection.
- Detection: is a carried ground truth the only detector? What about checksums over act counts,
  or a reader that can tell "no acts between t1 and t2" from "no acts recorded between t1 and t2"?
- Taxonomy: the three symptoms differ in *where* the gap is (record, index, replay path). A fourth
  and fifth probably exist.

Related: [[future-paper-a-fact-must-belong-to-one-actor]], [[paper9-distributed-observation-brief]].
