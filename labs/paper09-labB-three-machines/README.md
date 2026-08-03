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

## Run

Needs Docker Desktop. From the example's root:

    bash docker/run-demo.sh          # publish, build the image, up 3 containers, wait for convergence
    bash docker/run-demo.sh --down   # tear down

Then the check the paper reports:

    for f in a b c; do docker compose exec -T tetris-$f cat /data/tetris-$f.frame | md5sum; done

## Contents

`docker/` as it stood on branch `p9/labg-rerun` of the examples repository. The write-up and the
captured run are in `data/paper09-labB-three-machines/`.
