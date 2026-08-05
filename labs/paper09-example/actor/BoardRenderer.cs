using System.Collections.Generic;
using System.Text;

namespace Tetris.Acting;

/// <summary>
/// Draws a <see cref="WellSnapshot"/> as the ASCII board shared by every host —
/// the human keyboard console and the AI CLI render the same grid. Hosts decide
/// how to emit the text (cursor-home redraw, append, etc.); this only builds it.
/// </summary>
public static class BoardRenderer
{
    /// <summary>
    /// The full board text: a header line, the cleared-lines line, a blank line,
    /// the framed grid, and a trailing GAME OVER banner when the game is over.
    /// <c>[]</c> marks a filled interior cell, two spaces an empty one; <c>|</c>
    /// and <c>=</c> draw the walls and floor.
    /// </summary>
    public static string Board(WellSnapshot snapshot, string header)
    {
        var occupied = new HashSet<Cell>(snapshot.Occupied);

        var sb = new StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine($"Lines cleared: {snapshot.ClearedLines}");
        sb.AppendLine();

        for (var row = 0; row < snapshot.Height; row++)
        {
            sb.Append('|'); // left wall
            for (var column = 0; column < snapshot.Width; column++)
            {
                sb.Append(occupied.Contains(new Cell(row, column)) ? "[]" : "  ");
            }

            sb.AppendLine("|"); // right wall
        }

        sb.Append('+').Append(new string('=', snapshot.Width * 2)).AppendLine("+"); // floor

        if (snapshot.IsGameOver)
        {
            sb.AppendLine();
            sb.AppendLine("            G A M E   O V E R");
        }

        return sb.ToString();
    }
}
