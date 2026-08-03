namespace Tetris.Hex;

/// <summary>
/// The DRIVEN (secondary, outbound) port for the piece-selection policy —
/// which tetromino comes next. Piece selection is non-deterministic, and the
/// orthodox hexagonal treatment of non-determinism is to push it out through a
/// port so the application stays deterministic and testable (the same reason
/// a clock or an id generator is injected).
/// <para>
/// The letter, not the enum, is the currency of the contract: the model's
/// <c>PieceType</c> is internal, so a port an outside adapter must implement
/// cannot speak it without making it public. This mirrors the journaled
/// domain's own <c>Well.NextPieceLetter()</c>, which returns a letter for the
/// same reason.
/// </para>
/// </summary>
public interface IPieceSelectionPort
{
    /// <summary>
    /// The letter of the next piece — one of "I", "O", "T", "S", "Z", "J", "L".
    /// Must not mutate anything; the application resolves the letter and then
    /// spawns it.
    /// </summary>
    string NextPieceLetter();
}
