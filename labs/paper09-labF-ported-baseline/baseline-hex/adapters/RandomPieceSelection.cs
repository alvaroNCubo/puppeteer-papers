using Tetris.Hex;

namespace Tetris.Hex.Adapters;

/// <summary>
/// A DRIVEN adapter shared by every staging: the piece-selection policy the
/// hexagon declares as <see cref="IPieceSelectionPort"/>, implemented with a
/// uniform random pick over the seven letters.
/// <para>
/// Note what has happened to this policy. In the journaled domain it is a
/// method ON the well (<c>Well.NextPieceLetter()</c>), inside the model, and the
/// resolved letter is recorded in the journal. Here the model's copy of the
/// policy is unreachable from outside — <c>Well</c> is internal — so the policy
/// has to be written a second time, out here, by whoever stages the game.
/// </para>
/// <para>
/// It started life inside the console staging. Adding the second staging forced
/// it out into this shared project, because otherwise the same policy would
/// have been written twice — and by the fourth staging, four times. The
/// journaled arrangement never needs the extraction, because no staging of it
/// owns any part of the policy.
/// </para>
/// </summary>
public sealed class RandomPieceSelection : IPieceSelectionPort
{
    private static readonly string[] Letters = ["I", "O", "T", "S", "Z", "J", "L"];

    private readonly Random chooser;

    public RandomPieceSelection(int? seed = null) =>
        chooser = seed is null ? new Random() : new Random(seed.Value);

    public string NextPieceLetter() => Letters[chooser.Next(Letters.Length)];
}
