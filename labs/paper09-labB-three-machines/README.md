# Paper 9 — Lab B: three machines

One domain runs as three StageManager peers in three Docker containers on a private bridge
network — one Director, two casts, joined over Kestrel TLS on a port never exposed to the host, so
the coordination and replication between them is genuine container-to-container TLS. A scripted
driver on the Director plays the game to a non-trivial board and that play replicates to the casts.

Headline → §2 (Experiment A) and Appendix A (Lab B). The claim is two zeros and one convergence:
**0 domain edits and 0 actor edits**, and the three nodes reaching a **byte-identical** board.

What is *not* claimed: any test of resilience. No peer was killed and no partition induced, so
partial failure is untouched — the paper says so at the one row of its Waldo table marked as not
addressed.


## Order, consoles, and what each shows

Docker Desktop must be running. **Order: 1, then 2.** Console 1 must reach convergence before the
check in console 2 means anything.

| # | Run this | What you see in it | Who operates it |
|---|---|---|---|
| 1 | `bash docker/run-demo.sh` | Publish, image build, three containers coming up, then it **waits and prints three convergence checkpoints** — one per node. It does not return until all three converge. | **You**, once. Then leave it. |
| 2 | `for f in a b c; do docker compose exec -T tetris-$f cat /data/tetris-$f.frame ` + "`|`" + ` md5sum; done` | **Three md5 hashes that must be identical.** That identity is the whole result. | **You**, after console 1 has converged. |

Tear down when finished, from console 2:

```bash
bash docker/run-demo.sh --down
```

**Output on disk:** each node writes its own frame to its own volume — `/data/tetris-a.frame`,
`tetris-b.frame`, `tetris-c.frame`, in the per-node volumes `tetris-a-data` and its siblings. Those
three files are the evidence; the md5 check is just a fast way to compare them. To keep them:

```bash
for f in a b c; do docker compose exec -T tetris-$f cat /data/tetris-$f.frame > labB-$f.frame; done
```

Then diff them directly. **Byte-identical is the claim**, so `diff labB-a.frame labB-b.frame` should
print nothing.

**What is not claimed:** any test of resilience. No peer is killed and no partition induced, so
partial failure is untouched — the paper says so at the one row of its Waldo table marked *not
addressed*.

## Contents

`docker/` as it stood on branch `p9/labg-rerun` of the examples repository. The write-up and the
captured run are in `data/paper09-labB-three-machines/`.
