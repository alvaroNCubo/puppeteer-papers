using System.Text.Json;
using Tetris.Hex;

namespace Tetris.HexAi;

/// <summary>
/// Staging 4's DRIVEN adapter for state: an <see cref="IGameStatePort"/> that
/// keeps each session's <see cref="GameState"/> as a JSON file. It is the whole
/// implementation of the port the hexagon had to grow — a load, a save, and an
/// atomic replace.
/// <para>
/// Worth stating plainly for the comparison: what this file does, the journaled
/// arrangement gets from the substrate's journal without declaring anything. Here
/// it is a port in the domain plus this adapter plus the model seam that lets the
/// well be rebuilt from it.
/// </para>
/// </summary>
public sealed class JsonFileGameState : IGameStatePort
{
    private static readonly JsonSerializerOptions Format = new() { WriteIndented = false };

    public GameState? Load(string session)
    {
        var path = HexSessionPaths.StateFile(session);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<GameState>(json, Format);
    }

    public void Save(string session, GameState state)
    {
        var path = HexSessionPaths.StateFile(session);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Write-then-move, so a reader never sees a half-written state.
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, Format));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Whether a session already has state — used to refuse to overwrite a game.</summary>
    public static bool Exists(string session) => File.Exists(HexSessionPaths.StateFile(session));
}
