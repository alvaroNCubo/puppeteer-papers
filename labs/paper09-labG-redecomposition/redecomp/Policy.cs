using Tetris.Acting;

namespace Tetris.Redecomp;

/// <summary>
/// What a driver does next. A policy is a PURE FUNCTION of the snapshot, so the
/// same policy handed the same state always issues the same thing — which is what
/// lets two cuts of the domain be driven identically without either being told what
/// the other did.
/// </summary>
internal interface IPolicy
{
    /// <summary>The op to issue given <paramref name="snapshot"/>.</summary>
    Op Next(WellSnapshot snapshot, int step);
}

/// <summary>
/// A random walk over the input alphabet, weighted toward gravity. It exercises the
/// verbs but it is a poor Tetris player: it almost never completes a row, so it
/// cannot test what happens when rows collapse.
/// </summary>
internal sealed class RandomPolicy : IPolicy
{
    private readonly Op[] ops;

    internal RandomPolicy(int seed, int length)
    {
        var random = new Random(seed);
        ops = new Op[length];
        for (var i = 0; i < length; i++)
        {
            ops[i] = random.Next(100) switch
            {
                < 18 => Op.Left,
                < 36 => Op.Right,
                < 48 => Op.Rotate,
                < 92 => Op.Tick,
                _ => Op.Drop,
            };
        }
    }

    public Op Next(WellSnapshot snapshot, int step) => ops[step % ops.Length];
}

/// <summary>
/// A policy that actually plays: it slides the falling piece over the flattest,
/// lowest span that fits its current width, then hard-drops it. It keeps the floor
/// level, so rows complete and collapse regularly — which is the point, because the
/// collapse is the transition the re-cut had to move from one role to another, and a
/// player that never triggers one tests nothing about it.
/// <para>
/// It reads nothing but the snapshot the domain already emits — column heights and
/// the falling piece's own cells — so it is a client in the paper's sense, and it
/// works identically against either cut.
/// </para>
/// </summary>
internal sealed class FlatPolicy : IPolicy
{
    private readonly Random rotations;

    internal FlatPolicy(int seed) => rotations = new Random(seed);

    public Op Next(WellSnapshot snapshot, int step)
    {
        if (snapshot.Active.Count == 0)
        {
            return Op.Tick;
        }

        // Rotate a fixed, seeded number of times per piece before committing, so the
        // run visits every pose rather than only the spawn pose. Keyed off the step
        // so it is reproducible.
        var active = snapshot.Active;
        var minColumn = active.Min(c => c.Column);
        var maxColumn = active.Max(c => c.Column);
        var span = maxColumn - minColumn + 1;

        // Settled cells = everything occupied that is not the falling piece.
        var falling = new HashSet<(int, int)>(active.Select(c => (c.Row, c.Column)));
        var heights = new int[snapshot.Width];
        foreach (var cell in snapshot.Occupied)
        {
            if (falling.Contains((cell.Row, cell.Column)) || cell.Column < 0 || cell.Column >= snapshot.Width)
            {
                continue;
            }

            heights[cell.Column] = Math.Max(heights[cell.Column], snapshot.Height - cell.Row);
        }

        // The best landing span: lowest maximum height, then flattest, then leftmost.
        var bestLeft = 0;
        var bestScore = (int.MaxValue, int.MaxValue);
        for (var left = 0; left + span <= snapshot.Width; left++)
        {
            var window = heights.Skip(left).Take(span).ToArray();
            var score = (window.Max(), window.Max() - window.Min());
            if (score.CompareTo(bestScore) < 0)
            {
                bestScore = score;
                bestLeft = left;
            }
        }

        if (bestLeft < minColumn)
        {
            return Op.Left;
        }

        if (bestLeft > minColumn)
        {
            return Op.Right;
        }

        // In position. Occasionally rotate first (a rotation in place changes the
        // span, which the next call re-evaluates); otherwise commit.
        return rotations.Next(6) == 0 ? Op.Rotate : Op.Drop;
    }
}
