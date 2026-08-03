# Paper 9 — Lab D: three projections of one emitted frame

One frame is emitted by the domain and rendered three ways without the domain knowing of any of
them: a character grid, a column-height vector, and a browser drawing. None of the three is
privileged, and the domain produces neither — it produces the fact all three are made from.

Headline → §3 and Appendix A (Lab D). **0 domain methods for any view.**

The two viewers here also demonstrate §4's distinction directly, and running both at once is the
clearest way to see it: `watch` **receives** each frame over the substrate's push channel and
prints it as it arrives, while `observer` **reconstructs** the board by re-reading the journal on a
poll. Told, against rebuilt from stills.

## Verify — deterministic, and nobody plays by hand

The claim is **read**, not run: the domain has no method for any of the three views. Open
`Tetris/domain/Well.cs` and look for one. What is there is `OccupiedInterior()` — the union of the
pile and the active piece, clipped to the interior — and nothing shaped like a rendering.

The run is the *demonstration*, and it should not depend on who is at the keyboard. `verify.ps1`
plays a fixed sequence of twelve acts with `TetrisAi` — one short-lived process per act, so one
writer at a time — then renders the resulting frame three ways and re-checks the reading:

    .erify.ps1 -Example C:\path	o	he\example

Deterministic because piece selection is the domain's own and replays identically, so the same acts
in the same order produce the same frame.

## Watch it happen, if you want to

Optional, and for seeing the mechanism rather than checking it. Three consoles: `watch` and
`observer` on the same session, then a writer. **One writer only** — mixing `TetrisAi` with a warm
`TetrisStage` on one session produces a record that cannot be replayed, because a check-then-command
journals the command and not the check, and a warm actor's stale view will pass a check the
journal's true sequence contradicts. Replay then logs the violation and carries on, so the polling
viewer reconstructs a board that is wrong and looks fine.

    dotnet run --project <example>/Tetris/watch/TetrisWatch.csproj -- juego1        # console 1: receives
    dotnet run --project <example>/Tetris/observer/TetrisObserver.csproj -- juego1  # console 2: reconstructs
    dotnet run --project <example>/Tetris/input/TetrisStage.csproj -- juego1 --sources keyboard,clock --clock-ms 500

Console 1 prints the instant an act lands, because it receives the frame. Console 2 prints up to
350 ms later and fills its screen with replay progress, because it re-opens the journal and
rehydrates on every poll. The silence of the one and the noise of the other are the same
measurement seen from both sides.

## Contents

The three projections' source as it stood on `main`: `pile-scan.ps1` (the vector),
`watch-Program.cs` (the push receiver) and `observer-Program.cs` (the poll fallback, whose own
comment explains why it re-opens the journal per poll).
