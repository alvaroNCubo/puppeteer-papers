#!/usr/bin/env bash
# Tetris Increment C2 — end-to-end 3-container demo orchestrator.
#
# Publishes TetrisStageCluster on the host (the engine, Puppeteer Pacifico, lives
# on the host by project path — it is not compiled inside Docker), then brings up
# three containers (tetris-a Director, tetris-b/-c casts) on the tetris-net bridge
# and waits until all three report convergence — the same Well, replicated over
# real TLS across three machines.
#
# Prerequisites:
#   - .NET SDK (targets net9.0) on the host
#   - Docker + docker compose plugin (Docker Desktop is fine)
#   - bash 4+  (Git Bash / WSL on Windows)
#
# Usage (from anywhere):
#   Tetris/docker/run-demo.sh            # publish, up, wait for convergence
#   Tetris/docker/run-demo.sh --down     # tear the cluster down (compose down -v)
#   Tetris/docker/run-demo.sh --keep     # do not tear down on exit (default)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"          # the Tetris/ dir
DOCKER_DIR="$SCRIPT_DIR"
BUILD_DIR="$DOCKER_DIR/build/host"
PROJECT="$REPO_ROOT/sm-cluster/TetrisStageCluster.csproj"

LOG()  { printf '\n[demo] %s\n' "$*"; }
FAIL() { printf '\n[demo] FATAL: %s\n' "$*" >&2; exit 1; }

# --down: just tear the cluster down and exit.
if [ "${1:-}" = "--down" ]; then
    LOG "Tearing down (docker compose down -v)"
    (cd "$DOCKER_DIR" && docker compose down -v)
    exit 0
fi

# -----------------------------------------------------------------------------
# 1. Publish the host runtime artifacts (framework-dependent; runs on aspnet:9.0).
# -----------------------------------------------------------------------------
LOG "Publishing TetrisStageCluster → $BUILD_DIR"
# rm can hit a transient handle from an MSBuild node on Windows; publish
# overwrites in place, so a failed clean is non-fatal.
rm -rf "$BUILD_DIR" 2>/dev/null || true
dotnet publish "$PROJECT" -c Release -o "$BUILD_DIR" --nologo --verbosity minimal \
    || FAIL "publish failed"

# -----------------------------------------------------------------------------
# 2. Bring up the three containers on a clean slate.
# -----------------------------------------------------------------------------
cd "$DOCKER_DIR"
LOG "Cleaning any previous run (docker compose down -v)"
docker compose down -v 2>/dev/null || true

LOG "Starting docker compose (tetris-a Director, tetris-b/-c casts)"
docker compose up --build -d || FAIL "docker compose up failed"

# -----------------------------------------------------------------------------
# 3. Wait until all three nodes log the convergence checkpoint.
# -----------------------------------------------------------------------------
LOG "Waiting for all three nodes to converge (the Well, replicated over TLS)..."
MARK="convergence checkpoint reached"
A_OK=0; B_OK=0; C_OK=0
for _ in $(seq 1 90); do
    docker compose logs tetris-a 2>&1 | grep -q "$MARK" && A_OK=1
    docker compose logs tetris-b 2>&1 | grep -q "$MARK" && B_OK=1
    docker compose logs tetris-c 2>&1 | grep -q "$MARK" && C_OK=1
    [ "$A_OK" = 1 ] && [ "$B_OK" = 1 ] && [ "$C_OK" = 1 ] && break
    sleep 2
done

if [ "$A_OK" != 1 ] || [ "$B_OK" != 1 ] || [ "$C_OK" != 1 ]; then
    LOG "Timed out. Last 40 lines of each container:"
    docker compose logs --tail=40 tetris-a
    docker compose logs --tail=40 tetris-b
    docker compose logs --tail=40 tetris-c
    FAIL "convergence not reached (a=$A_OK b=$B_OK c=$C_OK)"
fi

# -----------------------------------------------------------------------------
# 4. Report the evidence: the checkpoint line + the frame each node wrote.
# -----------------------------------------------------------------------------
LOG "====================================================================="
LOG "Tetris C2 (3-node cross-container Well over TLS) — CONVERGED"
LOG "---------------------------------------------------------------------"
for id in a b c; do
    line="$(docker compose logs "tetris-$id" 2>&1 | grep "$MARK" | tail -1)"
    printf '[demo]   %s\n' "$line"
done
LOG "---------------------------------------------------------------------"
LOG "Per-node frame files (each node painted its own from the state it holds):"
SESSION="${TETRIS_SESSION:-tetris}"
# MSYS_NO_PATHCONV: on Git Bash (Windows) the leading-slash container path would
# otherwise be rewritten to a host path; disable that just for these exec calls.
export MSYS_NO_PATHCONV=1
for id in a b c; do
    printf '[demo]   tetris-%s:/data/%s-%s.frame\n' "$id" "$SESSION" "$id"
    docker compose exec -T "tetris-$id" cat "/data/${SESSION}-${id}.frame" 2>/dev/null \
        | sed 's/^/[demo]     /' || true
done
LOG "====================================================================="
LOG "Containers left running. Inspect / tear down with:"
LOG "  docker compose -f $DOCKER_DIR/docker-compose.yml logs tetris-a"
LOG "  $SCRIPT_DIR/run-demo.sh --down"
