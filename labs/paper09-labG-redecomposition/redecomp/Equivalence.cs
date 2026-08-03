using Tetris.Acting;

namespace Tetris.Redecomp;

/// <summary>
/// Experiment 1 — did the invariant survive, and do the two cuts agree?
/// <para>
/// For each seeded game: play it on the single <c>Well</c> and on the two roles,
/// step for step, and compare what each issued and what state resulted — the board,
/// the running line count, game over, awaiting, the falling piece's type. The input
/// is a policy that is a pure function of the emitted snapshot, so neither cut is
/// told what the other did; if they ever diverge, they diverge on their own.
/// </para>
/// <para>
/// The split run is additionally checked, after EVERY op, on the three things the
/// re-cut could plausibly have broken. All three are checked from OUTSIDE both roles,
/// by asking each separately: neither role is trusted to police a property that
/// spans them.
/// </para>
/// </summary>
internal static class Equivalence
{
    internal static int Run(string policyName, int games, int maxOps)
    {
        const int width = 10;
        const int height = 20;

        var failures = 0;
        var totalSteps = 0;
        var totalClears = 0;
        var gamesOver = 0;
        var overlapChecks = 0;
        var landings = 0;

        for (var seed = 1; seed <= games; seed++)
        {
            var letters = Letters(policyName, seed, maxOps + 8);

            using var well = new TetrisActor($"eq-well-{policyName}-{seed}", width, height);
            using var split = SplitTetrisActor.InMemory($"eq-split-{policyName}-{seed}", width, height);

            var wellSteps = Play(well, Policy(policyName, seed, maxOps), letters, maxOps, inspect: null);

            var splitSteps = Play(split, Policy(policyName, seed, maxOps), letters, maxOps, inspect: (step, issued, snapshot) =>
            {
                overlapChecks++;

                // THE invariant the split had to keep: the falling piece never
                // overlaps the settled pile. The two sets come from two separate
                // roles, queried separately, and are intersected here.
                var falling = new HashSet<(int, int)>(snapshot.Active.Select(c => (c.Row, c.Column)));
                var settled = snapshot.Occupied
                    .Select(c => (c.Row, c.Column))
                    .Where(c => !falling.Contains(c))
                    .ToHashSet();
                foreach (var cell in falling)
                {
                    if (settled.Contains(cell))
                    {
                        Report(seed, step, issued, $"falling cell ({cell.Item1},{cell.Item2}) coincides with a settled cell");
                        failures++;
                    }
                }

                // No half-finished landing may be observable once a verb has
                // returned: the choreography settled inside the call.
                if (split.IsSettling())
                {
                    Report(seed, step, issued, "still settling after the verb returned");
                    failures++;
                }

                // Authority: game over is the pile role's verdict and the piece role
                // holds its word for it. They must never disagree.
                if (split.PieceRoleThinksGameOver() != snapshot.IsGameOver)
                {
                    Report(seed, step, issued,
                        $"piece role says over={split.PieceRoleThinksGameOver()}, pile role says over={snapshot.IsGameOver}");
                    failures++;
                }
            });

            failures += Compare(seed, wellSteps, splitSteps);
            totalSteps += wellSteps.Count;
            landings += splitSteps.Count(s => s.Issued is "drop" or "tick" && s.Awaiting);

            if (splitSteps.Count > 0)
            {
                totalClears += splitSteps[^1].Cleared;
                if (splitSteps[^1].Over)
                {
                    gamesOver++;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"policy           : {policyName}");
        Console.WriteLine($"games            : {games} (max {maxOps} ops each)");
        Console.WriteLine($"steps compared   : {totalSteps}");
        Console.WriteLine($"landings         : {landings}");
        Console.WriteLine($"lines cleared    : {totalClears} (summed over games)");
        Console.WriteLine($"games reaching over : {gamesOver}/{games}");
        Console.WriteLine($"overlap checks   : {overlapChecks}");
        Console.WriteLine($"divergences      : {failures}");
        return failures == 0 ? 0 : 1;
    }

    private static IPolicy Policy(string name, int seed, int length) => name switch
    {
        "random" => new RandomPolicy(seed, length),
        "flat" or "clears" => new FlatPolicy(seed),
        _ => throw new ArgumentException($"unknown policy '{name}'; expected 'random', 'flat' or 'clears'."),
    };

    private static string[] Letters(string policyName, int seed, int count)
    {
        // 'clears' feeds nothing but squares. Five squares placed on the flattest
        // span fill two whole rows across a 10-wide well, so the collapse — the one
        // transition the re-cut moved from one role to the other — fires every five
        // pieces instead of hardly ever. A random player almost never completes a
        // row, which is exactly why it cannot test this.
        var alphabet = policyName == "clears"
            ? ["O"]
            : new[] { "I", "O", "T", "S", "Z", "J", "L" };

        var random = new Random(seed * 7919);
        var letters = new string[count];
        for (var i = 0; i < count; i++)
        {
            letters[i] = alphabet[random.Next(alphabet.Length)];
        }

        return letters;
    }

    /// <summary>
    /// Drives one cut with the query-first policy every real host uses — spawn
    /// whenever the game is awaiting a piece, otherwise ask the policy — and records
    /// what it issued and what state resulted at every step.
    /// </summary>
    private static List<Step> Play(
        IGameActor game,
        IPolicy policy,
        string[] letters,
        int maxOps,
        Action<int, string, WellSnapshot>? inspect)
    {
        var steps = new List<Step>();
        var nextLetter = 0;

        for (var step = 0; step < maxOps; step++)
        {
            var before = game.Snapshot();
            if (before.IsGameOver)
            {
                break;
            }

            string issued;
            if (before.IsAwaitingPiece)
            {
                var letter = letters[nextLetter++];
                game.Spawn(letter);
                issued = $"spawn {letter}";
            }
            else
            {
                var op = policy.Next(before, step);
                switch (op)
                {
                    case Op.Left: game.MoveLeft(); break;
                    case Op.Right: game.MoveRight(); break;
                    case Op.Rotate: game.Rotate(); break;
                    case Op.Tick: game.Tick(); break;
                    case Op.Drop: game.Drop(); break;
                }

                issued = op.ToString().ToLowerInvariant();
            }

            var after = game.Snapshot();
            steps.Add(new Step(
                issued, Boards.Of(after), after.ClearedLines,
                after.IsGameOver, after.IsAwaitingPiece, after.ActiveType));

            inspect?.Invoke(steps.Count, issued, after);
        }

        return steps;
    }

    private static void Report(int seed, int step, string issued, string what) =>
        Console.WriteLine($"FAIL seed {seed} step {step} ({issued}): {what}.");

    private static int Compare(int seed, List<Step> well, List<Step> split)
    {
        var limit = Math.Min(well.Count, split.Count);
        for (var i = 0; i < limit; i++)
        {
            var a = well[i];
            var b = split[i];
            if (a == b)
            {
                continue;
            }

            Console.WriteLine($"FAIL seed {seed} step {i + 1}: the two cuts diverged.");
            Console.WriteLine($"  well  issued={a.Issued} cleared={a.Cleared} over={a.Over} awaiting={a.Awaiting} type={a.Type}");
            Console.WriteLine($"  split issued={b.Issued} cleared={b.Cleared} over={b.Over} awaiting={b.Awaiting} type={b.Type}");
            if (a.Board != b.Board)
            {
                Console.WriteLine($"  well  board={a.Board}");
                Console.WriteLine($"  split board={b.Board}");
            }

            return 1;
        }

        if (well.Count != split.Count)
        {
            Console.WriteLine($"FAIL seed {seed}: well took {well.Count} steps, split took {split.Count}.");
            return 1;
        }

        return 0;
    }
}
