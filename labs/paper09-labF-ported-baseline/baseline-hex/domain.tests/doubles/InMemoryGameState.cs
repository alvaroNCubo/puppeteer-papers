using System.Collections.Generic;

namespace Tetris.Hex.Tests;

/// <summary>
/// TEST DOUBLE #3 — a stand-in for the driven port <see cref="IGameStatePort"/>,
/// added when staging 4 added the port. A fake: a dictionary standing in for a
/// store, with a counter so a test can assert that the application recorded once
/// per operation.
/// <para>
/// The doubles count of this suite went from two to three the moment a staging
/// needed state to outlive a process. That is the shape of the cost ports and
/// adapters charges: a capability arrives as a port, and every port is one more
/// thing a test of the domain has to supply.
/// </para>
/// </summary>
internal sealed class InMemoryGameState : IGameStatePort
{
    private readonly Dictionary<string, GameState> saved = [];

    /// <summary>How many times the application recorded state.</summary>
    public int Saves { get; private set; }

    /// <summary>How many times the application asked to reload.</summary>
    public int Loads { get; private set; }

    public GameState? Load(string session)
    {
        Loads++;
        return saved.TryGetValue(session, out var state) ? state : null;
    }

    public void Save(string session, GameState state)
    {
        Saves++;
        saved[session] = state;
    }
}
