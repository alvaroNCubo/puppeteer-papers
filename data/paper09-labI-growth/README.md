# Paper 9 — Lab I data: a note on the absolute paths in these files

The logs, transcripts and shell scripts in this directory contain the author's absolute paths —
worktree roots under `C:\Users\alvar\source\repos\...` and scratch directories under `AppData\Local\Temp`.

**They are left as they were captured, deliberately.** A log is evidence of a run that happened, and
the path a binary was invoked from is a fact of that run. Rewriting them would make the record
tidier and less true. So read them as a transcript, not as instructions.

What a reader *runs* carries no such path. `replay.sh` and `smoke.sh` resolve the example from their
second argument, or from `TETRIS_EXAMPLE`, or by defaulting to the vendored `labs/paper09-example/`;
they stop with a message naming both options if none resolves. Their output lands in an `out/`
directory beside them rather than in a temp path from the original run, and `replay.sh` reads its
fixtures from `journals-pre-growth/` in this same directory. `labs/paper09-example/tools/pile-scan.ps1`
resolves the same three ways.

The same applies to the write-ups in the other `data/paper09-*` directories: where one names an
absolute path it is recording where something was, not telling anyone where to put it.
