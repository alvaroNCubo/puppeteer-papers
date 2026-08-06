using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Tetris;

/// <summary>
/// Renders a set of cells as one canonical string, and reads it back. This is the
/// price the re-decomposition pays for splitting a figure across two roles: a
/// cross-role utterance carries ORDERED SCALARS, and a cell set is neither
/// ordered nor of fixed length — so the pile, on its way to the piece role, has
/// to travel as a rendering of itself.
/// <para>
/// The rendering is CANONICAL — cells sorted by row then column, so the same set
/// always renders to the same text. That matters twice: an utterance's identity
/// may be a content hash of its values, and a journal stays dense only when the
/// same fact renders the same way.
/// </para>
/// <para>
/// It is a value-object rendering, not a wire format: the domain both writes and
/// reads it, and nothing outside the domain needs to understand it. Grammar:
/// <c>row,column</c> pairs separated by spaces; the empty set is the empty
/// string.
/// </para>
/// </summary>
internal static class CellCodec
{
    /// <summary>The canonical rendering of <paramref name="cells"/>.</summary>
    internal static string Encode(IEnumerable<Position> cells)
    {
        var builder = new StringBuilder();
        foreach (var cell in cells.OrderBy(c => c.Row).ThenBy(c => c.Column))
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(cell.Row).Append(',').Append(cell.Column);
        }

        return builder.ToString();
    }

    /// <summary>
    /// The cells <paramref name="text"/> renders. A malformed rendering is a
    /// modelling bug, not a bad input, so it throws
    /// <see cref="TetrisRuleException"/> rather than returning a partial set.
    /// </summary>
    internal static ImmutableHashSet<Position> Decode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ImmutableHashSet<Position>.Empty;
        }

        var cells = ImmutableHashSet.CreateBuilder<Position>();
        foreach (var pair in text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(',');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out var row)
                || !int.TryParse(parts[1], out var column))
            {
                throw new TetrisRuleException($"'{pair}' is not a cell; expected 'row,column'.");
            }

            cells.Add(new Position(row, column));
        }

        return cells.ToImmutable();
    }
}
