using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tetris.Tests;

/// <summary>
/// The property the whole model is shaped around: a well is a pure function of
/// its construction and the sequence of operations applied to it. Because the
/// next piece is supplied from outside (an explicit <see cref="Well.Spawn"/>),
/// a command stream fully determines the outcome — replaying it yields a
/// byte-for-byte identical state, with no hidden randomness anywhere.
/// </summary>
[TestClass]
public sealed class DeterminismTests
{
    /// <summary>One command in the replayable stream — either a move or a spawn.</summary>
    private sealed record Command(string Verb, PieceType Type = default)
    {
        public static Command Left { get; } = new("left");
        public static Command Right { get; } = new("right");
        public static Command Rotate { get; } = new("rotate");
        public static Command Tick { get; } = new("tick");
        public static Command Drop { get; } = new("drop");
        public static Command Spawn(PieceType type) => new("spawn", type);
    }

    // A self-consistent stream: each piece is spawned, manoeuvred, and dropped
    // before the next is spawned — the host's job of feeding pieces, made
    // explicit and deterministic.
    private static readonly Command[] Stream =
    [
        Command.Spawn(PieceType.T), Command.Left, Command.Rotate, Command.Drop,
        Command.Spawn(PieceType.I), Command.Rotate, Command.Right, Command.Drop,
        Command.Spawn(PieceType.O), Command.Left, Command.Left, Command.Drop,
        Command.Spawn(PieceType.S), Command.Tick, Command.Right, Command.Drop,
        Command.Spawn(PieceType.Z), Command.Rotate, Command.Drop,
        Command.Spawn(PieceType.J), Command.Left, Command.Drop,
        Command.Spawn(PieceType.L), Command.Right, Command.Right, Command.Drop,
    ];

    private static Well Replay()
    {
        var well = new Well(8, 16);
        foreach (var command in Stream)
        {
            switch (command.Verb)
            {
                case "left": well.MoveLeft(); break;
                case "right": well.MoveRight(); break;
                case "rotate": well.Rotate(); break;
                case "tick": well.Tick(); break;
                case "drop": well.Drop(); break;
                case "spawn": well.Spawn(command.Type); break;
            }
        }

        return well;
    }

    /// <summary>A stable, order-independent fingerprint of the well's full state.</summary>
    private static string Fingerprint(Well well)
    {
        var cells = string.Join(
            "|",
            well.OccupiedInterior()
                .OrderBy(c => c.Row)
                .ThenBy(c => c.Column)
                .Select(c => $"{c.Row},{c.Column}"));

        var active = well.Active is null
            ? "none"
            : $"{well.Active.Type}:{well.Active.Orientation.Index}:{well.Active.Anchor.Row},{well.Active.Anchor.Column}";

        return $"over={well.IsGameOver};awaiting={well.IsAwaitingPiece};cleared={well.ClearedLines};active={active};cells={cells}";
    }

    [TestMethod]
    public void SameCommandStream_YieldsIdenticalState()
    {
        var first = Fingerprint(Replay());
        var second = Fingerprint(Replay());

        Assert.AreEqual(first, second, "two replays of the same stream must match exactly");
    }

    [TestMethod]
    public void TenReplays_AllAgree()
    {
        var fingerprints = Enumerable.Range(0, 10).Select(_ => Fingerprint(Replay())).Distinct().ToList();
        Assert.AreEqual(1, fingerprints.Count, "every replay collapses to a single state");
    }
}
