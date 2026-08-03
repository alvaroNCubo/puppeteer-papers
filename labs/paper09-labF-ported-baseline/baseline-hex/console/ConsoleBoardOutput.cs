using System.Text;
using Tetris.Hex;

namespace Tetris.HexConsole;

/// <summary>
/// The console's DRIVEN adapter: an <see cref="IBoardOutputPort"/> implementation
/// that draws each presented <see cref="BoardView"/> as the ASCII grid. The
/// drawing is the same as the journaled example's <c>BoardRenderer</c> — the
/// comparison is between arrangements, not between renderers.
/// <para>
/// It also keeps the last view it was presented, because the driving loop needs
/// to know when the game is over and the hexagon offers no way to ask. Caching
/// on the adapter side is the orthodox answer to that (the alternative — a query
/// port back into the hexagon — is what staging 4 ends up needing for a
/// different reason).
/// </para>
/// </summary>
public sealed class ConsoleBoardOutput : IBoardOutputPort
{
    private readonly bool interactive;
    private readonly string header;

    public ConsoleBoardOutput(bool interactive, string header)
    {
        this.interactive = interactive;
        this.header = header;
    }

    /// <summary>The most recent view presented, or null before the first one.</summary>
    public BoardView? Latest { get; private set; }

    public void Present(BoardView board)
    {
        Latest = board;
        var text = Draw(board, header);

        if (interactive)
        {
            // Cursor-home redraw rather than Console.Clear, to avoid flicker.
            System.Console.SetCursorPosition(0, 0);
            System.Console.Write(text);
        }
        else
        {
            System.Console.WriteLine(text);
        }
    }

    /// <summary>
    /// The board text: a header line, the cleared-lines line, a blank line, the
    /// framed grid, and a GAME OVER banner once the game is over. <c>[]</c> marks
    /// a filled interior cell, two spaces an empty one.
    /// </summary>
    public static string Draw(BoardView board, string header)
    {
        var occupied = new HashSet<BoardCell>(board.Occupied);

        var sb = new StringBuilder();
        sb.AppendLine(header);
        sb.AppendLine($"Lines cleared: {board.ClearedLines}");
        sb.AppendLine();

        for (var row = 0; row < board.Height; row++)
        {
            sb.Append('|'); // left wall
            for (var column = 0; column < board.Width; column++)
            {
                sb.Append(occupied.Contains(new BoardCell(row, column)) ? "[]" : "  ");
            }

            sb.AppendLine("|"); // right wall
        }

        sb.Append('+').Append(new string('=', board.Width * 2)).AppendLine("+"); // floor

        if (board.IsGameOver)
        {
            sb.AppendLine();
            sb.AppendLine("            G A M E   O V E R");
        }

        return sb.ToString();
    }
}
