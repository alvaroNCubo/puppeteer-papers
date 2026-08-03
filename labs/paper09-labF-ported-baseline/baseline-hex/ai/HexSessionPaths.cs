namespace Tetris.HexAi;

/// <summary>
/// Resolves the on-disk paths for a session of the automated-player staging: the
/// state file the state port reads and writes, and the live frame file the board
/// output port overwrites for a scanner to read. Both are found from a
/// repo-stable root (a <c>.sessions</c> folder beside <c>TetrisHex.sln</c>) so
/// the writer process and the reading scanner agree on them.
/// </summary>
public static class HexSessionPaths
{
    /// <summary>The root under which every session's files live.</summary>
    public static string Root { get; } = ComputeRoot();

    private static string ComputeRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TetrisHex.sln")))
        {
            dir = dir.Parent;
        }

        var baseDir = dir?.FullName ?? AppContext.BaseDirectory;
        return Path.Combine(baseDir, ".sessions");
    }

    /// <summary>The state file for <paramref name="session"/> — what the state port keeps.</summary>
    public static string StateFile(string session) => Path.Combine(Root, Safe(session) + ".state");

    /// <summary>
    /// The live frame file for <paramref name="session"/> — the ephemeral screen
    /// the board output adapter overwrites and a scanner reads.
    /// </summary>
    public static string FrameFile(string session) => Path.Combine(Root, Safe(session) + ".frame");

    private static string Safe(string session)
    {
        var safe = string.Concat(session.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        if (safe.Length == 0)
        {
            throw new ArgumentException("Session id must contain at least one usable character.", nameof(session));
        }

        return safe;
    }
}
