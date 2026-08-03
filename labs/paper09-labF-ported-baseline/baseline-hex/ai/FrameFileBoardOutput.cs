using Tetris.Hex;
using Tetris.Hex.Adapters;

namespace Tetris.HexAi;

/// <summary>
/// Staging 4's DRIVEN adapter for output: an <see cref="IBoardOutputPort"/> that
/// writes each presented board, as frame JSON, to a single per-session frame
/// file, OVERWRITING it each time. The counterpart of the journaled example's
/// <c>FrameFileSink</c>.
/// <para>
/// The frame file is the live screen and is ephemeral by design — a reader always
/// wants the latest board, never the history — so overwriting is correct. The
/// write goes to a temp file and is then moved, so a scanner never reads a
/// half-written frame.
/// </para>
/// </summary>
public sealed class FrameFileBoardOutput : IBoardOutputPort
{
    private readonly string framePath;

    public FrameFileBoardOutput(string framePath)
    {
        this.framePath = framePath;
        Directory.CreateDirectory(Path.GetDirectoryName(framePath)!);
    }

    /// <summary>The file a scanner should read for this session's live frame.</summary>
    public string FramePath => framePath;

    public void Present(BoardView board)
    {
        var tmp = framePath + ".tmp";
        File.WriteAllText(tmp, FrameJson.Of(board));
        File.Move(tmp, framePath, overwrite: true);
    }
}
