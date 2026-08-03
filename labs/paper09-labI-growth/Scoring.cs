namespace Tetris;

/// <summary>
/// The scoring rule: what a landing is worth. One place states the whole of it —
/// how many points a clear of <em>n</em> simultaneous rows awards — so the well
/// accumulates a total without knowing the tariff and no other figure knows it
/// at all.
/// <para>
/// The multiplier is on <em>simultaneity</em>, not on the row count: four rows
/// taken at once are worth double four rows taken one at a time, which is the
/// whole of what makes stacking for a quadruple a decision rather than a habit.
/// A landing that completes nothing is worth nothing.
/// </para>
/// <para>
/// Any observer watching the emitted frames could keep this tally itself, and
/// that is exactly why the well keeps it: two observers keeping their own would
/// each have a number and there would be no fact of the matter. The score is the
/// domain's single word on what a game was worth.
/// </para>
/// </summary>
internal static class Scoring
{
    /// <summary>
    /// The points awarded for collapsing <paramref name="simultaneousRows"/> rows
    /// in one landing. A piece is four cells, so it can complete at most four
    /// rows at once; anything outside 0..4 is a modelling bug, not a play, and
    /// says so loudly rather than scoring silently.
    /// </summary>
    public static int Award(int simultaneousRows) => simultaneousRows switch
    {
        0 => 0,
        1 => 100,   // single
        2 => 300,   // double
        3 => 500,   // triple
        4 => 800,   // quadruple
        _ => throw new TetrisRuleException(
            $"A landing cannot collapse {simultaneousRows} rows; a piece is four cells."),
    };
}
