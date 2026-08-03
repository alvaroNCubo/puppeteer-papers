using Tetris.Acting;

namespace Tetris.Redecomp;

/// <summary>
/// Experiment 2 — what the split COSTS on the record. Plays one game on the two
/// roles over persistent journals, then reads both journals back and tallies, per
/// role, every verb each one performed. That answers three things at once: how many
/// utterances a landing costs, how many a move costs, and whether each role's
/// journal contains only its own acts.
/// </summary>
internal static class SplitGame
{
    internal static int Run(string root, int seed, int maxOps, bool dump)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        Directory.CreateDirectory(root);

        var alphabet = new[] { "I", "O", "T", "S", "Z", "J", "L" };
        var random = new Random(seed * 7919);
        var policy = new FlatPolicy(seed);

        var roles = new List<(string Role, string Actor, string Journal)>();
        var moves = 0;
        var landings = 0;
        var cleared = 0;
        var over = false;

        using (var split = SplitTetrisActor.Persistent($"sg{seed}", 10, 20, root))
        {
            roles.Add(("piece", split.PieceActorName, split.PieceJournal!));
            roles.Add(("pile", split.PileActorName, split.PileJournal!));

            for (var step = 0; step < maxOps; step++)
            {
                var before = split.Snapshot();
                if (before.IsGameOver)
                {
                    over = true;
                    break;
                }

                if (before.IsAwaitingPiece)
                {
                    split.Spawn(alphabet[random.Next(alphabet.Length)]);
                    moves++;
                    continue;
                }

                var wasFalling = before.Active.Count > 0;
                switch (policy.Next(before, step))
                {
                    case Op.Left: split.MoveLeft(); break;
                    case Op.Right: split.MoveRight(); break;
                    case Op.Rotate: split.Rotate(); break;
                    case Op.Tick: split.Tick(); break;
                    case Op.Drop: split.Drop(); break;
                }

                moves++;
                var after = split.Snapshot();
                if (wasFalling && after.Active.Count == 0)
                {
                    landings++;
                }

                cleared = after.ClearedLines;
            }

            var final = split.Snapshot();
            cleared = final.ClearedLines;
            over = final.IsGameOver;
        }

        Console.WriteLine($"ops issued       : {moves}");
        Console.WriteLine($"landings         : {landings}");
        Console.WriteLine($"lines cleared    : {cleared}");
        Console.WriteLine($"game over        : {over}");

        foreach (var (role, actor, journal) in roles)
        {
            var acts = JournalActs.Read(actor, journal);
            Console.WriteLine();
            Console.WriteLine($"=== {role} role journal ({actor}) — {acts.Count} entries ===");

            var byVerb = acts
                .GroupBy(JournalActs.VerbOf)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal);
            foreach (var group in byVerb)
            {
                Console.WriteLine($"  {group.Count(),5}  {group.Key}");
            }

            // Append-only: the entry ids must be the contiguous run 1..N, with
            // nothing missing and nothing rewritten in place.
            var contiguous = acts.Select((a, i) => a.EntryId == i + 1).All(x => x);
            Console.WriteLine($"  entry ids contiguous 1..{acts.Count}: {contiguous}");

            if (JournalActs.Gaps.Count > 0)
            {
                // A gap means the record read is not the record written. It used to be
                // routine past 100 entries — a stale persisted sparse index hid the
                // tail (engine defect (b), fixed in 036b972) — so it is worth saying out
                // loud rather than letting short counts pass as evidence.
                Console.WriteLine($"  WARNING: gaps at entry ids {string.Join(", ", JournalActs.Gaps)} — "
                    + "these counts are the readable record, not the written one.");
            }

            if (dump)
            {
                Console.WriteLine("  --- first 14 entries ---");
                foreach (var act in acts.Take(14))
                {
                    Console.WriteLine($"  {act.EntryId,4}  {act.Kind,-10}  {JournalActs.Collapse(act.Sentence)}");
                    if (act.Arguments.Length > 0)
                    {
                        Console.WriteLine($"            args: {JournalActs.Collapse(act.Arguments)}");
                    }

                    if (act.ExposeData.Length > 0)
                    {
                        Console.WriteLine($"            expose: {JournalActs.Collapse(act.ExposeData)}");
                    }
                }
            }
        }

        return 0;
    }
}
