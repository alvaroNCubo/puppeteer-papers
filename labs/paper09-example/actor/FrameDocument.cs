using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Tetris.Acting;

/// <summary>
/// Parses a pushed frame <see cref="Puppeteer.PushDocument.Document"/> (the JSON
/// the frame reaction's <c>Program.Emit</c> produces) back into a typed
/// <see cref="WellSnapshot"/>. The push viewer renders the EMITTED projection
/// directly — it parses this document and draws the board, never re-querying the
/// journal. That is what makes the viewer a direct receiver of the game's frame
/// rather than a narrator reconstructing it.
/// </summary>
public static class FrameDocument
{
    /// <summary>
    /// The JSON shape (see TetrisActor.FrameProjection):
    /// <c>{"width":..,"height":..,"cleared":..,"over":..,"awaiting":..,
    /// "type":"T"?,"cell":[{"r":..,"c":..},..]}</c>. <c>type</c> and <c>cell</c>
    /// are absent when there is nothing to report. Returns null if the document
    /// is empty or unparseable.
    /// </summary>
    public static WellSnapshot? Parse(string document)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(document);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var cells = new List<Cell>();
            if (root.TryGetProperty("cell", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in array.EnumerateArray())
                {
                    cells.Add(new Cell(element.GetProperty("r").GetInt32(), element.GetProperty("c").GetInt32()));
                }
            }

            string? type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

            return new WellSnapshot(
                root.GetProperty("width").GetInt32(),
                root.GetProperty("height").GetInt32(),
                cells,
                Array.Empty<Cell>(), // the pushed frame folds the active piece into Occupied
                root.GetProperty("cleared").GetInt32(),
                root.GetProperty("over").GetBoolean(),
                root.GetProperty("awaiting").GetBoolean(),
                type);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
