namespace Tetris;

/// <summary>
/// The difficulty rule: what level a game has reached, as a function of how many
/// rows it has cleared. Ten lines to a level, starting at one.
/// <para>
/// The domain owns <em>what level the game is at</em> and says nothing whatever
/// about what that should feel like. Speed is not here, and deliberately: gravity
/// is a property of a staging, not of the well — the console has a wall clock, the
/// stage host has a <c>ClockSource</c> whose interval IS the gravity speed, the AI
/// CLI has a commander sending ticks, and a replay has no clock at all. A level is
/// a fact about the game; how fast to fall at that level is each host's decision,
/// and the well is indifferent to it. That is why this rule can be single-valued
/// and journaled while gravity stays outside.
/// </para>
/// <para>
/// Unlike the <see cref="Scoring"/> tariff, this needs no accumulation: the level
/// is recoverable from the cleared-line count at any moment, so the well stores
/// nothing new for it. It is here because the RULE — where the boundaries fall —
/// must be the domain's single word, not each observer's guess.
/// </para>
/// </summary>
internal static class Difficulty
{
    /// <summary>How many cleared rows advance the game one level.</summary>
    public const int LinesPerLevel = 10;

    /// <summary>The level a game that has cleared <paramref name="clearedLines"/> rows is at. A game that has cleared nothing is at level one.</summary>
    public static int LevelFor(int clearedLines)
    {
        if (clearedLines < 0)
        {
            throw new TetrisRuleException($"A game cannot have cleared {clearedLines} rows.");
        }

        return 1 + (clearedLines / LinesPerLevel);
    }
}
