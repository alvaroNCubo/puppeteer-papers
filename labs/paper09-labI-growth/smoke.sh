#!/usr/bin/env bash
# Runs every staging and client in the solution and reports PASS/FAIL on a signal
# string in its output. Used to measure ripple (a): "does it still WORK after the
# domain grew", not merely "does it still compile".
#   usage: smoke.sh <label> [treeRoot]
# treeRoot defaults to this branch's Tetris dir; pass the pre-change worktree's
# Tetris dir to get the before column from an identical run.
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
T="$TETRIS"
LABEL="${1:-run}"
OUT="$SCRATCH/smoke-$LABEL"
rm -rf "$OUT"; mkdir -p "$OUT"
S="sm$RANDOM"
echo "# tree: $T"

bin() { echo "$T/$1/bin/Debug/net9.0/$2.exe"; }
report() { printf '%-12s %s\n' "$1" "$2"; }
check() { # check <name> <logfile> <signal>
  if grep -q "$3" "$2" 2>/dev/null; then report "$1" "PASS"; else report "$1" "FAIL — $2"; fi
}

# ── why this script does not use `timeout`, and does not sleep ──────────────
#
# Both were found by a reviewer running this on Windows, and both made the script
# lie about working code.
#
# `timeout` sends SIGTERM. MSYS emulates signals between its OWN processes and
# cannot deliver one to a native Windows binary, so a .exe never learns it was
# asked to stop, keeps running, and `timeout` waits on it. One run sat thirteen
# minutes on a `timeout 8`. Systematic, not intermittent. A budget therefore has
# to be enforced from the Windows side with taskkill — and the pid taskkill needs
# is the WINDOWS pid, which /proc/<pid>/winpid maps from the one bash reports.
#
# The fixed sleeps were worse, because they failed quietly. Six seconds for the
# warm server to open its named pipe is enough on an idle machine and not enough
# on one fresh from compiling twelve projects: TetrisSend then blocks on a pipe
# that does not exist yet, send.log comes back empty, and `send` reports FAIL
# against a host that works. Waiting for the line each host prints when it is
# ready costs nothing when the machine is fast and does not lie when it is slow.

winpid() { cat "/proc/$1/winpid" 2>/dev/null || echo "$1"; }

kill_tree() { # kill_tree <bash pid> — //T so a host's children go with it
  taskkill //PID "$(winpid "$1")" //T //F > /dev/null 2>&1 || true
}

spawn() { # spawn <logfile> <exe> [args...] — sets LAST_PID
  local log="$1"; shift
  "$@" > "$log" 2>&1 &
  LAST_PID=$!
}

ready() { # ready <logfile> <signal> <seconds> — 0 if the signal appeared
  local i=0
  while [ "$i" -lt "$3" ]; do
    grep -q "$2" "$1" 2>/dev/null && return 0
    sleep 1; i=$((i + 1))
  done
  return 1
}

bounded() { # bounded <seconds> <logfile> <exe> [args...] — runs, then kills if still alive
  local budget="$1"; shift
  spawn "$@"
  local pid="$LAST_PID" i=0
  while [ "$i" -lt "$budget" ] && kill -0 "$pid" 2>/dev/null; do sleep 1; i=$((i + 1)); done
  kill -0 "$pid" 2>/dev/null && kill_tree "$pid"
  wait "$pid" 2>/dev/null || true
}

viewer() { # viewer <name> <signal> <logfile> <exe> [args...] — start, wait for the
           # signal, let one frame land, stop. Replaces `timeout 8`, which never
           # stopped these and blocked the whole run.
  local name="$1" signal="$2" log="$3"; shift 3
  spawn "$log" "$@"
  ready "$log" "$signal" 30 || true
  sleep 2
  kill_tree "$LAST_PID"
  wait "$LAST_PID" 2>/dev/null || true
}

# ── console (keyboard + wall clock, in-memory) ─────────────────────────────
# These exit on their own, so the budget is only a backstop here too — but a
# backstop that cannot fire is not one, so they use the same runner as the rest.
bounded 30 "$OUT/console.log" "$(bin console TetrisConsole)" --auto
check console "$OUT/console.log" "Lines cleared"

# ── ai (one op per process, persistent journal) ─────────────────────────────
AI="$(bin ai TetrisAi)"
for op in new drop view; do
  bounded 30 "$OUT/ai-$op.log" "$AI" "$S-ai" "$op"
done
cat "$OUT"/ai-new.log "$OUT"/ai-drop.log "$OUT"/ai-view.log > "$OUT/ai.log" 2>/dev/null
check ai "$OUT/ai.log" "META"

# ── watch + observer (read-only viewers over the ai session) ────────────────
viewer watch WATCHING "$OUT/watch.log" "$(bin watch TetrisWatch)" "$S-ai"
check watch "$OUT/watch.log" "WATCHING"
grep -q "frameExists=True" "$OUT/watch.log" && report watch-frame "PASS" || report watch-frame "FAIL (no pushed frame)"
viewer observer OBSERVER "$OUT/observer.log" "$(bin observer TetrisObserver)" "$S-ai"
check observer "$OUT/observer.log" "OBSERVER"

# ── server (warm) + send (thin client) ─────────────────────────────────────
# Wait for the warm banner rather than for six seconds: it is the same line the
# `server` check greps for below, so the wait and the assertion agree by
# construction.
spawn "$OUT/server.log" "$(bin server TetrisServer)" "$S-srv"
SRV_PID=$LAST_PID
ready "$OUT/server.log" "TetrisServer warm" 60 || report server "SLOW — banner never appeared"
SEND="$(bin send TetrisSend)"
bounded 20 "$OUT/send.log" "$SEND" "$S-srv" drop
bounded 20 "$OUT/send-view.log" "$SEND" "$S-srv" view
bounded 20 "$OUT/send-quit.log" "$SEND" "$S-srv" quit
cat "$OUT/send-view.log" "$OUT/send-quit.log" >> "$OUT/send.log" 2>/dev/null
ready "$OUT/server.log" "applied: drop" 30 || true
kill_tree "$SRV_PID"
check server "$OUT/server.log" "TetrisServer warm"
check send "$OUT/server.log" "applied: drop"

# ── input (TetrisStage: pipe + clock source merge) ──────────────────────────
spawn "$OUT/input.log" "$(bin input TetrisStage)" "$S-stg" --sources pipe,clock --clock-ms 400
STG_PID=$LAST_PID
ready "$OUT/input.log" "TetrisStage: session" 60 || report input "SLOW — banner never appeared"
bounded 20 "$OUT/input-send.log" "$SEND" "$S-stg" drop
ready "$OUT/input.log" "applied: tick\|applied: drop" 30 || true
bounded 20 "$OUT/input-quit.log" "$SEND" "$S-stg" quit
cat "$OUT/input-send.log" "$OUT/input-quit.log" >> "$OUT/input.log" 2>/dev/null
kill_tree "$STG_PID"
check input "$OUT/input.log" "applied: tick\|applied: drop"

# ── web (WebSockets) ───────────────────────────────────────────────────────
spawn "$OUT/web.log" "$(bin web TetrisWeb)"
WEB_PID=$LAST_PID
ready "$OUT/web.log" "Tetris web host running at" 60 || report web "SLOW — banner never appeared"
curl -s -o "$OUT/web-page.html" -w "player=%{http_code} " http://localhost:5080/ > "$OUT/web-http.log" 2>&1
curl -s -o /dev/null -w "observer=%{http_code}\n" http://localhost:5080/observer >> "$OUT/web-http.log" 2>&1
cat "$OUT/web-http.log" >> "$OUT/web.log"
check web "$OUT/web.log" "player=200"

# ── web-rest (REST in, SSE out) ────────────────────────────────────────────
spawn "$OUT/web-rest.log" "$(bin web-rest TetrisWebRest)"
REST_PID=$LAST_PID
ready "$OUT/web-rest.log" "Tetris REST+SSE host running at" 60 || report web-rest "SLOW — banner never appeared"
curl -s -o /dev/null -w "player=%{http_code} " http://localhost:5081/ > "$OUT/rest-http.log" 2>&1
curl -s -X POST -H 'Content-Type: application/json' -d '{"move":"drop"}' \
     "http://localhost:5081/games/$S-rest/moves" -w " post=%{http_code}" >> "$OUT/rest-http.log" 2>&1
echo >> "$OUT/rest-http.log"
curl -s "http://localhost:5081/games/$S-rest/frame" > "$OUT/rest-frame.log" 2>&1
cat "$OUT/rest-http.log" "$OUT/rest-frame.log" >> "$OUT/web-rest.log" 2>/dev/null
check web-rest "$OUT/web-rest.log" "post=200"
check rest-frame "$OUT/rest-frame.log" "width"

# ── StageManager hosts ─────────────────────────────────────────────────────
# These three do exit on their own, so the budget is only a backstop — but it is a
# backstop that has to work, which `timeout` did not.
bounded 90 "$OUT/sm-server.log" "$(bin sm-server TetrisStageServer)" "$S-smsrv"
check sm-server "$OUT/sm-server.log" "Stage\|director\|TETRIS"
bounded 120 "$OUT/sm-duo.log" "$(bin sm-duo TetrisStageDuo)" "$S-duo"
check sm-duo "$OUT/sm-duo.log" "cast\|CAST\|replicat"
bounded 150 "$OUT/sm-duo-tls.log" "$(bin sm-duo-tls TetrisStageDuoTls)" "$S-tls"
check sm-duo-tls "$OUT/sm-duo-tls.log" "cast\|CAST\|replicat"

# ── stop anything still listening ──────────────────────────────────────────
for p in TetrisWeb TetrisWebRest TetrisServer TetrisStage; do
  taskkill //IM "$p.exe" //F > /dev/null 2>&1
done

# ── remove the sessions this run created ───────────────────────────────────
# Every host above is driven against a session named for this run's $S, so the
# journals are throwaway: the PASS/FAIL lines are the result and the logs are in
# $OUT. Left behind they accumulate nine directories plus their frame files per
# run, in the same .sessions the other labs use by name — so a reader who ran
# this twice found eighteen strangers next to his own game1.
SESSIONS="$T/.sessions"
if [ -d "$SESSIONS" ]; then
  rm -rf "$SESSIONS/$S"-* "$SESSIONS/rest-$S"-* 2>/dev/null || true
  rm -f  "$SESSIONS/$S"-*.frame "$SESSIONS/rest-$S"-*.frame 2>/dev/null || true
fi

echo "logs: $OUT"
