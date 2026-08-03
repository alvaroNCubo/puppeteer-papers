namespace Tetris.Hex;

/// <summary>
/// The DRIVING (primary, inbound) port — the application-service interface a
/// driving adapter calls. Declared by the hexagon and implemented by it
/// (<see cref="GameService"/>); a keyboard, a socket message, an HTTP request
/// or a CLI argument is turned into one of these calls by an adapter outside.
/// <para>
/// Every verb is <em>gentle</em>: called in a state where it does not apply
/// (no piece falling, or the game over) it is a no-op rather than a throw, so
/// a driving adapter does not have to run the query-first protocol itself.
/// This mirrors the journaled example's check-then-command guards, which do
/// the same job in the staging rather than in the domain.
/// </para>
/// </summary>
public interface IGameCommandPort
{
    /// <summary>Begin play: supply the first piece and present the opening board.</summary>
    void Start();

    /// <summary>
    /// Present the board as it stands, changing nothing. Added by staging 4: a
    /// client that runs one operation per process starts with nothing presented,
    /// so it needs a way to ask for the current board without moving anything —
    /// and the hexagon's only entrance is this port.
    /// </summary>
    void Show();

    /// <summary>Slide the falling piece one column left.</summary>
    void MoveLeft();

    /// <summary>Slide the falling piece one column right.</summary>
    void MoveRight();

    /// <summary>Turn the falling piece one quarter-turn.</summary>
    void Rotate();

    /// <summary>One gravity step; feeds the next piece if this one lands.</summary>
    void Tick();

    /// <summary>Hard-drop the falling piece; feeds the next piece once it lands.</summary>
    void Drop();
}
