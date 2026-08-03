#!/usr/bin/env bash
# Runs every staging and client in the solution and reports PASS/FAIL on a signal
# string in its output. Used to measure ripple (a): "does it still WORK after the
# domain grew", not merely "does it still compile".
#   usage: smoke.sh <label> [treeRoot]
# treeRoot defaults to this branch's Tetris dir; pass the pre-change worktree's
# Tetris dir to get the before column from an identical run.
set -u
T="${2:-C:/Users/alvar/source/repos/puppeteer-examples/Tetris/.claude/worktrees/trusting-tereshkova-f48ab6/Tetris}"
SCRATCH="C:/Users/alvar/AppData/Local/Temp/claude/C--Users-alvar-source-repos-puppeteer-examples-Tetris--claude-worktrees-trusting-tereshkova-f48ab6/46ad7df4-01f4-4cc7-941d-1119fd1b3dfd/scratchpad"
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

# ── console (keyboard + wall clock, in-memory) ─────────────────────────────
timeout 30 "$(bin console TetrisConsole)" --auto > "$OUT/console.log" 2>&1
check console "$OUT/console.log" "Lines cleared"

# ── ai (one op per process, persistent journal) ─────────────────────────────
AI="$(bin ai TetrisAi)"
timeout 30 "$AI" "$S-ai" new  > "$OUT/ai.log" 2>&1
timeout 30 "$AI" "$S-ai" drop >> "$OUT/ai.log" 2>&1
timeout 30 "$AI" "$S-ai" view >> "$OUT/ai.log" 2>&1
check ai "$OUT/ai.log" "META"

# ── watch + observer (read-only viewers over the ai session) ────────────────
timeout 8 "$(bin watch TetrisWatch)" "$S-ai" > "$OUT/watch.log" 2>&1
check watch "$OUT/watch.log" "WATCHING"
grep -q "frameExists=True" "$OUT/watch.log" && report watch-frame "PASS" || report watch-frame "FAIL (no pushed frame)"
timeout 8 "$(bin observer TetrisObserver)" "$S-ai" > "$OUT/observer.log" 2>&1
check observer "$OUT/observer.log" "OBSERVER"

# ── server (warm) + send (thin client) ─────────────────────────────────────
"$(bin server TetrisServer)" "$S-srv" > "$OUT/server.log" 2>&1 &
sleep 6
SEND="$(bin send TetrisSend)"
timeout 20 "$SEND" "$S-srv" drop > "$OUT/send.log" 2>&1
timeout 20 "$SEND" "$S-srv" view >> "$OUT/send.log" 2>&1
timeout 20 "$SEND" "$S-srv" quit >> "$OUT/send.log" 2>&1
sleep 2
check server "$OUT/server.log" "TetrisServer warm"
check send "$OUT/server.log" "applied: drop"

# ── input (TetrisStage: pipe + clock source merge) ──────────────────────────
"$(bin input TetrisStage)" "$S-stg" --sources pipe,clock --clock-ms 400 > "$OUT/input.log" 2>&1 &
sleep 8
timeout 20 "$SEND" "$S-stg" drop >> "$OUT/input.log" 2>&1
sleep 2
timeout 20 "$SEND" "$S-stg" quit >> "$OUT/input.log" 2>&1
sleep 3
check input "$OUT/input.log" "applied: tick\|applied: drop"

# ── web (WebSockets) ───────────────────────────────────────────────────────
"$(bin web TetrisWeb)" > "$OUT/web.log" 2>&1 &
sleep 8
curl -s -o "$OUT/web-page.html" -w "player=%{http_code} " http://localhost:5080/ > "$OUT/web-http.log" 2>&1
curl -s -o /dev/null -w "observer=%{http_code}\n" http://localhost:5080/observer >> "$OUT/web-http.log" 2>&1
cat "$OUT/web-http.log" >> "$OUT/web.log"
check web "$OUT/web.log" "player=200"

# ── web-rest (REST in, SSE out) ────────────────────────────────────────────
"$(bin web-rest TetrisWebRest)" > "$OUT/web-rest.log" 2>&1 &
sleep 8
curl -s -o /dev/null -w "player=%{http_code} " http://localhost:5081/ > "$OUT/rest-http.log" 2>&1
curl -s -X POST -H 'Content-Type: application/json' -d '{"move":"drop"}' \
     "http://localhost:5081/games/$S-rest/moves" -w " post=%{http_code}" >> "$OUT/rest-http.log" 2>&1
echo >> "$OUT/rest-http.log"
curl -s "http://localhost:5081/games/$S-rest/frame" > "$OUT/rest-frame.log" 2>&1
cat "$OUT/rest-http.log" "$OUT/rest-frame.log" >> "$OUT/web-rest.log" 2>/dev/null
check web-rest "$OUT/web-rest.log" "post=200"
check rest-frame "$OUT/rest-frame.log" "width"

# ── StageManager hosts ─────────────────────────────────────────────────────
timeout 90 "$(bin sm-server TetrisStageServer)" "$S-smsrv" > "$OUT/sm-server.log" 2>&1
check sm-server "$OUT/sm-server.log" "Stage\|director\|TETRIS"
timeout 120 "$(bin sm-duo TetrisStageDuo)" "$S-duo" > "$OUT/sm-duo.log" 2>&1
check sm-duo "$OUT/sm-duo.log" "cast\|CAST\|replicat"
timeout 150 "$(bin sm-duo-tls TetrisStageDuoTls)" "$S-tls" > "$OUT/sm-duo-tls.log" 2>&1
check sm-duo-tls "$OUT/sm-duo-tls.log" "cast\|CAST\|replicat"

# ── stop anything still listening ──────────────────────────────────────────
for p in TetrisWeb TetrisWebRest TetrisServer TetrisStage; do
  taskkill //IM "$p.exe" //F > /dev/null 2>&1
done
echo "logs: $OUT"
