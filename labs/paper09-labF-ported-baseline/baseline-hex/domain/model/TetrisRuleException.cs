using System;

namespace Tetris.Hex;

/// <summary>
/// The single exception the domain throws when a rule is broken — a malformed
/// piece, an invalid operation on the well (operating when there is no active
/// piece, or spawning when one is already falling), or an internal invariant
/// that has been violated. Each throw site supplies a message describing the
/// specific case.
/// <para>
/// A rule violation is distinct from a <em>blocked</em> move: a move or rotation
/// that would collide with a wall or the pile is a valid operation that simply
/// has no effect, and never throws.
/// </para>
/// </summary>
internal sealed class TetrisRuleException : Exception
{
    public TetrisRuleException(string message) : base(message)
    {
    }
}
