# Paper 9 — Lab H: one routine, recognized on three stagings

One reaction is defined outside the domain: it seeks a spawn, then the drop that ends the piece's
descent, and what it matches between them is a placement. It is then run against three stagings —
the journal of a single process, the journal a warm server keeps while driven over a named pipe, and
the journals of the three containers, read inside each node against its own store.

## There is nothing to run here, and what to read instead

This lab's artifact is a **reaction defined outside the domain** plus the record of running it against
three stagings. The reaction is quoted in the write-up rather than shipped as a project, so there is
no build in this directory.

**What to inspect, in order:**

| # | Open this | What you are looking for |
|---|---|---|
| 1 | `../../data/paper09-labH-recognition/recognition-across-stagings.md` | The reaction itself, and the three stagings it was run against. |
| 2 | the same file, the comparison section | **The same two placements in the same order in all three.** That is the result. |
| 3 | `../../data/paper09-labH-recognition/recognition-across-stagings.log` | The captured run, as evidence the comparison was performed rather than asserted. |

**Output on disk:** the `.log` beside the write-up *is* the output — it was captured when the lab ran.
It contains the author's absolute paths, deliberately: a log is evidence of a run that happened, and
rewriting the paths would make the record tidier and less true.

**The distinction to hold on to while reading.** Within the third staging the three nodes' records are
**byte-identical**. *Across* the three stagings the acts match **verb for verb** — same count, same
order, same shape — while the **entry identifiers differ by a constant**, because the cluster writes
three idempotent seeding acts where one process writes one. What held still is the acts; what moved is
the bookkeeping. Do not read the first sentence as the second.

**And three findings narrower than the confirmation**, all in the write-up: the record offers no handle
to correlate on; entry ids are not staging-invariant; and a reading can be **wrong, silently** —
landing is not an act, so a piece coming to rest under gravity leaves its opening unclosed and the
next piece's drop closes it, the count coming out right while the correlation comes out wrong.

