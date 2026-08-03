namespace Tetris.Hex;

using System.Collections.Generic;

/// <summary>
/// A THIRD driven port, added by staging 4: where the game's state is kept
/// between operations. A client that runs one discrete operation per process —
/// the shape of the journaled example's automated player, which an external
/// commander drives by shelling out one call at a time — cannot hold the well in
/// memory, so somebody has to store it. Under ports and adapters that somebody
/// is a port the hexagon declares and an adapter outside implements.
/// <para>
/// This port is the measured cost of that client. The journaled arrangement has
/// no counterpart: its state is the journal the substrate already keeps, chosen
/// by the host with a connection string (<c>TetrisActor.Persistent(...)</c>), and
/// the domain declares nothing about it.
/// </para>
/// </summary>
public interface IGameStatePort
{
    /// <summary>The state saved for <paramref name="session"/>, or null if there is none.</summary>
    GameState? Load(string session);

    /// <summary>Record the state of <paramref name="session"/>, replacing any earlier one.</summary>
    void Save(string session, GameState state);
}

/// <summary>
/// The contract carried by <see cref="IGameStatePort"/>: everything needed to
/// reopen a well where it was left. It is part of the port, so it lives inside
/// the hexagon — which means the hexagon now describes its own storage shape as
/// well as its own output shape.
/// </summary>
public sealed record GameState(
    int Width,
    int Height,
    int ClearedLines,
    IReadOnlyList<BoardCell> PileCells,
    string? ActiveType,
    int ActiveAnchorRow,
    int ActiveAnchorColumn,
    int ActiveOrientation);
