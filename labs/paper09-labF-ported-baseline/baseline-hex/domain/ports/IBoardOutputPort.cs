namespace Tetris.Hex;

/// <summary>
/// The DRIVEN (secondary, outbound) port for the board — declared BY the
/// hexagon and depended upon BY it. Every adapter that shows the board to
/// anybody (a terminal, a browser, a file a scanner reads) implements this.
/// <para>
/// This is the port the journaled arrangement does not have: there, the domain
/// emits a fact under a logical name and the staging binds where it goes, so
/// nothing about a destination is declared inside the domain. Here the domain
/// declares the shape of the call its destinations must satisfy, and
/// <see cref="GameService"/> holds a reference to one.
/// </para>
/// </summary>
public interface IBoardOutputPort
{
    /// <summary>
    /// Present the board as it stands after an operation. Called by the
    /// application on every mutation, and once when a game starts.
    /// </summary>
    void Present(BoardView board);
}
