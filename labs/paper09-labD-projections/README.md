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
.\verify.ps1 -Example C:\Users\alvar\source\repos\_p9\labg
```

Twelve fixed acts through `TetrisAi`, one short-lived process per act, then the frame rendered three
ways and the reading re-checked mechanically. Deterministic: piece selection is the domain's own and
replays identically, so the same acts give the same frame.

## Watch it happen — four consoles

Optional, and for seeing the mechanism rather than checking it. Same session name in all four, and
start them top to bottom.

| # | Runs | Who does what |
|---|---|---|
| 1 | `dotnet run --project Tetris\watch\TetrisWatch.csproj -- juego1` | **The substrate pushes; this console receives.** Prints the board the instant an act lands. |
| 2 | `dotnet run --project Tetris\observer\TetrisObserver.csproj -- juego1` | **This console rebuilds.** Polls every 350 ms, re-reads the journal, rehydrates. Late, and noisy with replay progress. |
| 3 | `dotnet run --project Tetris\input\TetrisStage.csproj -- juego1 --sources pipe,clock --clock-ms 5000` | **The only writer.** Turns each arriving command into a journaled act. Prints one line at startup and nothing after — it renders nothing, so it *looks* frozen and is not. |
| 4 | `dotnet run --project Tetris\send\TetrisSend.csproj -- juego1 left` | **You.** One move per invocation: `left`, `right`, `rotate`, `tick`, `drop`, `view`, `quit`. Carries the verb over a pipe and exits; writes no journal. |

A whole sequence at once, from console 4:

```powershell
'left','left','rotate','drop','right','rotate','drop','left','drop' | ForEach-Object { dotnet run --project Tetris\send\TetrisSend.csproj -- juego1 $_; Start-Sleep -Milliseconds 400 }
```

The third projection, whenever you want it:

```powershell
.\pile-scan.ps1 -FramePath C:\Users\alvar\source\repos\_p9\labg\Tetris\.sessions\juego1.frame
```

Prefer the keyboard to the pipe? Console 3 takes `--sources keyboard,clock --clock-ms 500` instead,
and then console 4 is unnecessary — but console 3 still shows nothing, and you press arrow keys into
a window that looks dead.

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
