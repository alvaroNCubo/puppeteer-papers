using System.Text;
using Tetris.Hex;

namespace Tetris.Hex.Adapters;

/// <summary>
/// Renders a presented <see cref="BoardView"/> as the frame JSON a client
/// parses:
/// <c>{"width":..,"height":..,"cleared":..,"over":..,"awaiting":..,"type":"T"?,
/// "cell":[{"r":..,"c":..},..]}</c> — deliberately the same wire shape the
/// journaled example's push channel produces, so the two arrangements are
/// compared over one document and one browser renderer.
/// <para>
/// This is written by hand rather than by serializing <see cref="BoardView"/>
/// directly, and the reason is worth recording. Serializing the port's own DTO
/// would put the wire's property names — <c>r</c>, <c>c</c>, <c>over</c> — on a
/// type that lives inside the hexagon, which is a domain edit for every wire
/// format a staging brings. Keeping the mapping out here costs a file per
/// staging instead. In the journaled arrangement neither cost arises: the
/// substrate's own formatter renders the emitted projection
/// (<c>Puppeteer/IOutputSink.cs:117</c>, <c>PerformanceV2.OutputTarget</c>), so
/// no type in the domain and no code in the host describes the wire at all.
/// </para>
/// <para>
/// Like <see cref="RandomPieceSelection"/>, this began inside a single staging
/// (the WebSocket host) and was forced out here by the arrival of the next one,
/// which needed the same document over a different transport. That is the second
/// time adding a staging edited the staging before it.
/// </para>
/// </summary>
public static class FrameJson
{
    public static string Of(BoardView board)
    {
        var sb = new StringBuilder(256);
        sb.Append("{\"width\":").Append(board.Width)
          .Append(",\"height\":").Append(board.Height)
          .Append(",\"cleared\":").Append(board.ClearedLines)
          .Append(",\"over\":").Append(board.IsGameOver ? "true" : "false")
          .Append(",\"awaiting\":").Append(board.IsAwaitingPiece ? "true" : "false");

        if (board.ActiveType is not null)
        {
            sb.Append(",\"type\":\"").Append(board.ActiveType).Append('"');
        }

        sb.Append(",\"cell\":[");
        for (var i = 0; i < board.Occupied.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            var cell = board.Occupied[i];
            sb.Append("{\"r\":").Append(cell.Row).Append(",\"c\":").Append(cell.Column).Append('}');
        }

        return sb.Append("]}").ToString();
    }
}
