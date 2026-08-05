using System.IO;
using Puppeteer;

namespace Tetris.Acting;

/// <summary>
/// An <see cref="IOutputSink"/> that writes each pushed frame's
/// <see cref="PushDocument.Document"/> to a single per-session frame file,
/// OVERWRITING it each time. The frame file is the live screen — ephemeral by
/// design: the durable record is the journal, so overwriting is correct (a
/// reader always wants the latest frame, never the history). The viewer watches
/// this file and repaints the instant it changes.
/// <para>
/// Per <see cref="IOutputSink"/>'s contract the document is an immutable string,
/// safe to retain/forward; the write is best-effort (a dropped frame is
/// recoverable — the next push overwrites with fresh state).
/// </para>
/// </summary>
public sealed class FrameFileSink : IOutputSink
{
    private readonly string framePath;

    public FrameFileSink(string framePath)
    {
        this.framePath = framePath;
        Directory.CreateDirectory(Path.GetDirectoryName(framePath)!);
    }

    /// <summary>The file path a viewer should watch for this session's live frame.</summary>
    public string FramePath => framePath;

    public void Push(in PushDocument document)
    {
        // Overwrite-in-place to a temp file then move, so a watcher never reads a
        // half-written frame. Best-effort: swallow transient IO races (the next
        // push will overwrite with fresh state anyway).
        try
        {
            var tmp = framePath + ".tmp";
            File.WriteAllText(tmp, document.Document);
            File.Move(tmp, framePath, overwrite: true);
        }
        catch (IOException)
        {
        }
    }
}
