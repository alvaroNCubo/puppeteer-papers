#!/usr/bin/env bash
# Replays every pre-change journal fixture with a chosen build of the domain and
# runs the same queries against each.
#   usage: replay.sh <label> [exampleRoot] [probeDir]
# probeDir defaults to the live build; pass $SCRATCH/probe-pre for the FROZEN
# pre-change build (same binaries that recorded the fixtures).
set -u

# The example's Tetris directory. Pass it as $2, or set TETRIS_EXAMPLE, or leave
# both unset and this resolves to the vendored example beside this lab:
#   labs/paper09-example
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TETRIS="${2:-${TETRIS_EXAMPLE:-$HERE/../paper09-example}}"
if [ ! -f "$TETRIS/Tetris.sln" ]; then
  echo "no Tetris.sln under '$TETRIS'." >&2
  echo "pass the example's root as the second argument, or set TETRIS_EXAMPLE." >&2
  exit 2
fi
# Output lands beside this script, not in a temp directory from the original run.
SCRATCH="$HERE/out"
mkdir -p "$SCRATCH"
LABEL="${1:-run}"
PROBEDIR="${2:-$TETRIS/tools/growth-probe/bin/Debug/net9.0}"
PROBE="$PROBEDIR/TetrisGrowthProbe.exe"
FIX="$HERE/journals-pre-growth"
WORK="$SCRATCH/replay-$LABEL"
rm -rf "$WORK"; mkdir -p "$WORK"

echo "# probe: $PROBE"
echo "# domain dll: $(md5sum "$PROBEDIR/TetrisDomain.dll" | cut -c1-32)"

# fixture-dir:actor-name
CASES="stepped-level2:level2 stepped-w4h40:stepped old-2026-07-01-rest-t1:t1 old-2026-07-01-rest-t2:t2 old-2026-07-01-rest-s1:s1 deep-w4h40:deep mid-w4h20:mid console-w10h20:console"

for case in $CASES; do
  dir="${case%%:*}"; actor="${case##*:}"
  cp -r "$FIX/$dir" "$WORK/$dir"
  echo "===== $dir (actor=$actor) ====="
  for q in "print well.Frame.Width w, well.Frame.Height h, well.ClearedLines cleared, well.IsGameOver over;" \
           "print well.Score score;" \
           "print well.Level level;"; do
    out=$("$PROBE" query "$WORK/$dir" "$actor" "$q" 2>&1 \
          | grep -v "^Starting Puppeteer" | tr '\n' ' ' | sed 's/[0-9]*%//g')
    printf '  Q: %s\n  A: %s\n' "$q" "$(echo "$out" | cut -c1-260)"
  done
done
