# Paper 9 — Lab I data: a note on the absolute paths in these files

The logs, transcripts and shell scripts in this directory contain the author's absolute paths —
worktree roots under `C:\Users\alvar\source\repos\...` and scratch directories under `AppData\Local\Temp`.

**They are left as they were captured, deliberately.** A log is evidence of a run that happened, and
the path a binary was invoked from is a fact of that run. Rewriting them would make the record
tidier and less true. So read them as a transcript, not as instructions.

What a reader *runs* carries no such path: `replay.sh` and `smoke.sh` both take the example's root as
an argument, and `labs/paper09-labC-clients/pile-scan.ps1` resolves the example from `-Example`, from
`TETRIS_EXAMPLE`, or by walking up from the working directory to the folder holding `Tetris.sln`.

The same applies to the write-ups in the other `data/paper09-*` directories: where one names an
absolute path it is recording where something was, not telling anyone where to put it.
