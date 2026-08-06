# Paper 9 — Lab D: three projections of one emitted frame

One frame is emitted by the domain and rendered three ways without the domain knowing of any of
them: a character grid, a column-height vector, and a browser drawing. None is privileged, and the
domain produces neither — it produces the fact all three are made from.

**Headline → §3 and Appendix A (Lab D): 0 domain methods for any view.**

## Verify the claim — by reading, not by running

Open `Tetris/domain/Well.cs` and look for a method shaped like a rendering. There is none. What is
there is `OccupiedInterior()`, the union of the pile and the active piece clipped to the interior.

Then, to see three renderings of one frame with nobody at a keyboard:

```powershell
.\verify.ps1 -Example ..\paper09-example
```

Twelve fixed acts through `TetrisAi`, one short-lived process per act, then the frame rendered three
ways and the reading re-checked mechanically. Deterministic: piece selection is the domain's own and
replays identically, so the same acts give the same frame.

## Watch it happen — four consoles

Optional, and for seeing the mechanism rather than checking it.

**Order matters: 3, then 1, then 2, then 4.** Console 3 is the writer, and the actor seeds the well
with a once-applied `upgrade` on startup, so console 3 is what creates the journal — console 2
rehydrates and refuses to start without one. Same session name in all four.

**Only console 4 is operated by a person.** The other three are started once and left alone; nothing
is typed into them again.

| # | Start this | What you see in it | Who operates it |
|---|---|---|---|
| 3 | `dotnet run --project ..\paper09-example\input\TetrisStage.csproj -- game1 --sources pipe,clock --clock-ms 5000` | **One line at startup, then nothing, ever.** It renders no board, so it looks frozen and is not. Confirm it is alive by that line: `TetrisStage: session 'game1', merged sources [pipe, clock], clock 5000ms…` | Nobody. It is the **only writer** — it turns each arriving command into a journaled act. |
| 1 | `dotnet run --project ..\paper09-example\watch\TetrisWatch.csproj -- game1` | **The board, repainted the instant an act lands**, with `[falling: S]` and `Lines cleared: N` above it. Silent between acts — that silence is the point. | Nobody. The substrate pushes each frame; this console *receives* it. |
| 2 | `dotnet run --project ..\paper09-example\observer\TetrisObserver.csproj -- game1` | **Replay progress filling the screen** — `1%2%3%…` — and the board rebuilt up to 350 ms late. It re-opens the journal and rehydrates on *every* poll. | Nobody. This console *reconstructs*; the noise is the cost of doing so. |
| 4 | `dotnet run --project ..\paper09-example\send\TetrisSend.csproj -- game1 left` | **Your prompt back after each command.** The commands you typed are the record of what you did — which the keyboard alternative does not leave. | **You.** One move per invocation. Run it with no arguments to have it list the operations it accepts. It carries the verb over a pipe and exits; it writes no journal. |

Every host here prints its own usage when run with no arguments, or with too few — `TetrisSend`,
`TetrisStage` and `TetrisAi` all do. Ask the program rather than this file: it cannot go stale.

    dotnet run --project ..\paper09-example\send\TetrisSend.csproj

Watch consoles 1 and 2 side by side after a single move in console 4. One prints at once because it
was told; the other prints later, after rebuilding from the record. That contrast is §4.

A whole sequence at once, from console 4:

```powershell
'left','left','rotate','drop','right','rotate','drop','left','drop' | ForEach-Object { dotnet run --project ..\paper09-example\send\TetrisSend.csproj -- game1 $_; Start-Sleep -Milliseconds 400 }
```

The third projection, whenever you want it:

```powershell
.\pile-scan.ps1 -Example ..\paper09-example -Session game1
```

Prefer the keyboard to the pipe? Console 3 takes `--sources keyboard,clock --clock-ms 500` instead,
and then console 4 is unnecessary — but console 3 still shows nothing, and you press arrow keys into
a window that looks dead.

### Output on disk

Nothing above needs to be trusted from a screen. The emitted fact is a file, and the three
projections are all computed from it:

    ../paper09-example/.sessions/game1.frame        one line of JSON — the fact itself
    ../paper09-example/.sessions/game1/             the journal: the acts that produced it

Open the `.frame` and you are looking at exactly what console 1 rendered as a grid, console 2
rebuilt by replay, and `pile-scan.ps1` read as a vector. Nothing else is consulted by any of them.

To keep the whole run rather than watching it go by, start a transcript in console 4 before the
first move and stop it after the last:

```powershell
Start-Transcript -Path labD-session.log
Stop-Transcript
```

And `verify.ps1` prints all three projections in sequence in one console, which is the form to
capture if you want a single artifact:

```powershell
.erify.ps1 -Example ..\paper09-example | Tee-Object -FilePath labD-verify.log
```

### Two rules, both learned the hard way

**One writer per session.** Never mix `TetrisAi` with a warm `TetrisStage` on one session. A
check-then-command journals the *command* and not the *check*, so a warm actor's stale view passes a
check the journal's true sequence contradicts; replay then hits the violation, logs it, and carries
on — and console 2 reconstructs a board that is wrong and looks fine. `TetrisSend` is safe because it
writes no journal: it is a *source*, not a writer.

**Console 3 shows nothing on purpose.** The board is in console 1. The window you type into is not
the window that renders.

## Contents

The three projections' source as it stood on `main`: `pile-scan.ps1` (the vector),
`watch-Program.cs` (the push receiver) and `observer-Program.cs` (the poll fallback, whose own
comment explains why it re-opens the journal per poll). Plus `verify.ps1`, the deterministic path.
