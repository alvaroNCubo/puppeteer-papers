using System;
using System.IO;
using System.Linq;

namespace Tetris.Acting;

/// <summary>
/// Resolves the on-disk journal directory for a persistent session. The AI CLI
/// (writer) and the observer (reader) both compute it the same way from a shared,
/// machine-stable root, so a session id names the same journal across the two
/// separate processes.
/// </summary>
public static class SessionPaths
{
    /// <summary>The root under which every session's journal directory lives.</summary>
    public static string Root { get; } = ComputeRoot();

    // Sessions live INSIDE the repo (a ".sessions" folder beside Tetris.sln),
    // not in LocalApplicationData. The journal + live frame file must be visible
    // to a viewer running in a DIFFERENT environment that shares only the repo
    // directory (e.g. a remote/sandboxed session whose workspace is the repo but
    // whose AppData is a separate filesystem from the user's terminal). The repo
    // is the one path both sides see; AppData is not. Found by walking up from the
    // running exe to the directory that holds Tetris.sln, so every project
    // (ai / watch / observer / console) resolves the same root.
    private static string ComputeRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tetris.sln")))
        {
            dir = dir.Parent;
        }

        var baseDir = dir?.FullName ?? AppContext.BaseDirectory;
        return Path.Combine(baseDir, ".sessions");
    }

    /// <summary>The journal directory for <paramref name="session"/> (one subdirectory per id).</summary>
    public static string For(string session) => Path.Combine(Root, Safe(session));

    /// <summary>
    /// The live-frame file for <paramref name="session"/> — the ephemeral screen
    /// the push sink overwrites and the viewer watches. Kept beside (not inside)
    /// the journal directory so a <see cref="System.IO.FileSystemWatcher"/> sees a
    /// simple single-file change.
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
