using System.Drawing.Text;
using Tetris.Acting;

namespace Tetris.Desktop;

/// <summary>
/// The desktop staging's one window. It turns key presses and timer ticks into
/// the actor's verbs and paints whatever <see cref="IGameActor.Snapshot"/> returns.
/// It follows the query-first contract of <c>console/Program.cs</c>: a verb is
/// issued only while a piece is falling, and when a landing leaves the well
/// between pieces the window asks the actor for the next one. It holds an
/// <see cref="IGameActor"/>, so it does not know which cut of the domain it drives.
/// </summary>
internal sealed class GameWindow : Form
{
    private static readonly Color Backdrop = Color.FromArgb(17, 17, 17);
    private static readonly Color Interior = Color.FromArgb(26, 26, 26);
    private static readonly Color GridLine = Color.FromArgb(40, 40, 40);
    private static readonly Color Wall = Color.FromArgb(150, 150, 150);
    private static readonly Color Settled = Color.FromArgb(200, 200, 200);
    private static readonly Color Ink = Color.FromArgb(221, 221, 221);

    // The falling piece is tinted by its type, the one thing about it the
    // snapshot names; landed cells carry no type, so the pile keeps one tone.
    private static readonly Dictionary<string, Color> PieceTint = new()
    {
        ["I"] = Color.FromArgb(0, 200, 230),
        ["O"] = Color.FromArgb(240, 205, 0),
        ["T"] = Color.FromArgb(170, 70, 240),
        ["S"] = Color.FromArgb(40, 200, 70),
        ["Z"] = Color.FromArgb(235, 50, 50),
        ["J"] = Color.FromArgb(40, 100, 240),
        ["L"] = Color.FromArgb(245, 150, 0),
    };

    private readonly IGameActor game;
    private readonly System.Windows.Forms.Timer gravity = new() { Interval = 500 };
    private WellSnapshot snapshot;

    public GameWindow(IGameActor game)
    {
        this.game = game;

        Text = "Tetris";
        BackColor = Backdrop;
        ClientSize = new Size(1280, 720);
        MinimumSize = new Size(480, 480);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        ResizeRedraw = true;

        snapshot = game.Snapshot();
        Settle(); // supply the first piece

        // The clock: each timer tick asks the actor to advance one row.
        gravity.Tick += (_, _) => Act(game.Tick);
        gravity.Start();
    }

    // --- input ---------------------------------------------------------------

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Left: Act(game.MoveLeft); return true;
            case Keys.Right: Act(game.MoveRight); return true;
            case Keys.Up: Act(game.Rotate); return true;
            case Keys.Down: Act(game.Tick); return true;   // soft drop
            case Keys.Space: Act(game.Drop); return true;  // hard drop
            case Keys.Escape:
            case Keys.Q: Close(); return true;
            default: return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // Query-first, by construction: the window only moves a piece that is
    // falling, so the domain's rule exception is never used for control flow.
    // A blocked move is the domain's no-op, not something the window checks.
    private void Act(Action verb)
    {
        if (snapshot.IsAwaitingPiece || snapshot.IsGameOver)
        {
            return;
        }

        verb();
        snapshot = game.Snapshot();
        Settle();
    }

    // A landing leaves the well between pieces; the host feeds the next one.
    // Game over is the domain's verdict — the window only stops its clock.
    private void Settle()
    {
        if (snapshot.IsAwaitingPiece)
        {
            game.SpawnNext();
            snapshot = game.Snapshot();
        }

        if (snapshot.IsGameOver)
        {
            gravity.Stop();
        }

        Invalidate();
    }

    // --- rendering -----------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var s = snapshot;

        // The well fills the window's height and sits in the middle; the HUD
        // takes the space to its right.
        var margin = ClientSize.Height / 16;
        var cell = Math.Max(4, Math.Min(
            (ClientSize.Height - 2 * margin) / s.Height,
            (ClientSize.Width - 2 * margin) / (s.Width + 8)));
        var well = new Rectangle(
            (ClientSize.Width - s.Width * cell) / 2,
            (ClientSize.Height - s.Height * cell) / 2,
            s.Width * cell,
            s.Height * cell);

        using (var interior = new SolidBrush(Interior))
        {
            g.FillRectangle(interior, well);
        }

        using (var grid = new Pen(GridLine))
        {
            for (var column = 1; column < s.Width; column++)
            {
                g.DrawLine(grid, well.Left + column * cell, well.Top, well.Left + column * cell, well.Bottom);
            }

            for (var row = 1; row < s.Height; row++)
            {
                g.DrawLine(grid, well.Left, well.Top + row * cell, well.Right, well.Top + row * cell);
            }
        }

        var falling = new HashSet<Cell>(s.Active);
        var tint = s.ActiveType is { } type && PieceTint.TryGetValue(type, out var color) ? color : Settled;
        foreach (var occupied in s.Occupied)
        {
            var square = new Rectangle(well.Left + occupied.Column * cell, well.Top + occupied.Row * cell, cell, cell);
            DrawBlock(g, square, falling.Contains(occupied) ? tint : Settled);
        }

        // Two walls and a floor, open at the top — as the domain's frame is.
        var thickness = Math.Max(2, cell / 6);
        var half = thickness / 2f;
        using (var wall = new Pen(Wall, thickness))
        {
            g.DrawLines(wall,
            [
                new PointF(well.Left - half, well.Top),
                new PointF(well.Left - half, well.Bottom + half),
                new PointF(well.Right + half, well.Bottom + half),
                new PointF(well.Right + half, well.Top),
            ]);
        }

        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var dim = new SolidBrush(Wall);
        using var ink = new SolidBrush(Ink);
        using var label = new Font("Segoe UI", cell * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var value = new Font("Segoe UI", cell * 1.2f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var hint = new Font("Segoe UI", cell * 0.36f, GraphicsUnit.Pixel);

        var hudLeft = well.Right + thickness + cell;
        g.DrawString("LINES CLEARED", label, dim, hudLeft, well.Top);
        g.DrawString(s.ClearedLines.ToString(), value, ink, hudLeft, well.Top + cell * 0.6f);

        const string keys = "← →   move\n↑   rotate\n↓   soft drop\nSpace   hard drop\nEsc   quit";
        g.DrawString(keys, hint, dim, hudLeft, well.Bottom - g.MeasureString(keys, hint).Height);

        if (s.IsGameOver)
        {
            using var veil = new SolidBrush(Color.FromArgb(170, Backdrop));
            using var banner = new Font("Segoe UI", cell * 0.9f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var centred = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.FillRectangle(veil, well);
            g.DrawString("GAME OVER", banner, ink, well, centred);
        }
    }

    // One cell as a block: inset from the grid, with a lighter top edge so a
    // stack reads as separate blocks rather than one slab.
    private static void DrawBlock(Graphics g, Rectangle square, Color color)
    {
        var inset = Math.Max(1, square.Width / 16);
        var block = Rectangle.Inflate(square, -inset, -inset);
        using var fill = new SolidBrush(color);
        using var shine = new SolidBrush(ControlPaint.Light(color, 0.5f));
        g.FillRectangle(fill, block);
        g.FillRectangle(shine, block.Left, block.Top, block.Width, Math.Max(1, block.Height / 8));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            gravity.Dispose();
        }

        base.Dispose(disposing);
    }
}
