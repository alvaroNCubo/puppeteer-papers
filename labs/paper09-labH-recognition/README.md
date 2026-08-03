# Paper 9 — Lab H: one routine, recognized on three stagings

One reaction is defined outside the domain: it seeks a spawn, then the drop that ends the piece's
descent, and what it matches between them is a placement. It is then run against three stagings —
the journal of a single process, the journal a warm server keeps while driven over a named pipe, and
the journals of the three containers, read inside each node against its own store.

Headline → §6 and Appendix A (Lab H). The same **two placements in the same order** in all three,
and **0 domain edits**. Within the third staging the three nodes' records are byte-identical; across
the three stagings the acts match verb for verb, same count and same order, while the entry
identifiers differ by a constant — the cluster writing three idempotent seeding acts where one
process writes one.

Three things this lab found matter more than the confirmation, and §6 is narrower for them: the
record offers no handle to correlate on; entry identifiers are not staging-invariant; and **a reading
can be wrong, silently** — landing is not an act, so a piece coming to rest under gravity leaves its
opening unclosed and the next piece's drop closes it, the count coming out right while the
correlation comes out wrong.

**This lab has no source of its own** beyond the reaction it defines, which is quoted in the
write-up. The write-up and the captured run are in `data/paper09-labH-recognition/`.
