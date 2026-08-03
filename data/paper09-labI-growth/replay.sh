#!/usr/bin/env bash
# Replays every pre-change journal fixture with a chosen build of the domain and
# runs the same queries against each.
#   usage: replay.sh <label> [probeDir]
# probeDir defaults to the live build; pass $SCRATCH/probe-pre for the FROZEN
# pre-change build (same binaries that recorded the fixtures).
set -u
TETRIS="C:/Users/alvar/source/repos/puppeteer-examples/Tetris/.claude/worktrees/trusting-tereshkova-f48ab6/Tetris"
SCRATCH="C:/Users/alvar/AppData/Local/Temp/claude/C--Users-alvar-source-repos-puppeteer-examples-Tetris--claude-worktrees-trusting-tereshkova-f48ab6/46ad7df4-01f4-4cc7-941d-1119fd1b3dfd/scratchpad"
LABEL="${1:-run}"
PROBEDIR="${2:-$TETRIS/tools/growth-probe/bin/Debug/net9.0}"
PROBE="$PROBEDIR/TetrisGrowthProbe.exe"
FIX="$TETRIS/notes/data/journals-pre-growth"
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
