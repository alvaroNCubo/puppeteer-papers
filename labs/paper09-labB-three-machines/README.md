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

Docker Desktop must be running. **Nobody plays anything in this lab** — `tetris-a` is the Director and
plays a short scripted sequence itself, which is why there is no input console.

**Order: 1 first, always.** Console 1 is what brings the containers up, so the observing consoles have
nothing to attach to until it has started. Run everything from this lab's directory.

| # | Run this | What you see in it | Who operates it |
|---|---|---|---|
| 1 | `bash docker/run-demo.sh` | Publish, image build, three containers coming up — then it **goes quiet and stays quiet**, polling the three logs for the line `convergence checkpoint reached`. **It looks stalled and is not**; it does not return until all three have converged, then prints the checkpoint line and each node's frame. | **You**, once. Then leave it. |
| 2 | `docker compose -f docker/docker-compose.yml logs -f` | All three nodes interleaved: promotion, the TLS peer connections, the Director's scripted play, and three `convergence checkpoint reached` lines. This is where the run is actually visible. | Nobody. Attach after console 1 has started. |
| 3 | `docker compose -f docker/docker-compose.yml logs -f tetris-a` | The **Director** alone — the node that plays. Useful beside console 4. | Nobody. |
| 4 | `docker compose -f docker/docker-compose.yml logs -f tetris-b` | A **cast** alone — it plays nothing and receives everything. Watching 3 and 4 side by side is the clearest thing in this lab: one acts, the other arrives at the same board over TLS. | Nobody. |

Two consoles are enough (1 and 2). Four make the point better, because the Director and a cast side by
side show one node acting and another converging without acting.

If you want to see the three containers exist before any of that:

```bash
docker compose -f docker/docker-compose.yml ps
```

## The check, and where the output lands

Each node writes its own frame into its own volume — `tetris-a-data`, `tetris-b-data`, `tetris-c-data`,
mounted at `/data` inside each container. **Their byte-identity is the result.** Console 1 prints them
when it finishes; to compare them yourself, in any free console:

```bash
for f in a b c; do docker compose -f docker/docker-compose.yml exec -T tetris-$f md5sum /data/tetris-$f.frame; done
```

Three identical hashes. To keep the frames as files rather than trust three hashes:

```bash
for f in a b c; do docker compose -f docker/docker-compose.yml exec -T tetris-$f cat /data/tetris-$f.frame > labB-$f.frame; done
```

Then `diff labB-a.frame labB-b.frame` should print nothing, and likewise for `c`. **Byte-identical is
the claim**, and three files on disk are better evidence than three matching hashes on a screen.

Capture the whole run while you are at it:

```bash
bash docker/run-demo.sh 2>&1 | tee labB-run.log
```

Tear down when finished, from any console:

```bash
bash docker/run-demo.sh --down
```

## Headline, and what is not claimed

**→ §2 (Experiment A) and Appendix A (Lab B): 0 domain edits and 0 actor edits**, and the three nodes
reaching a byte-identical board. Adding this staging left the diff of both the domain and the actor
directories empty.

**Not claimed: any test of resilience.** No peer is killed and no partition induced, so partial failure
is untouched — which the paper says at the one row of its Waldo table marked *not addressed*. If you
want to press the arrangement where it is weakest, killing `tetris-b` mid-run is the experiment this
lab deliberately does not perform.

## Contents

`docker/` as it stood on branch `p9/labg-rerun` of the examples repository. The write-up and the
captured run are in `data/paper09-labB-three-machines/`.
