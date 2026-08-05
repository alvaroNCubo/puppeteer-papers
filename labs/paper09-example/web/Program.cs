using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Tetris.Web;

// Tetris WEB host — exposes the SAME clean Well over the browser, ZERO domain
// edits. The web host is just a shell: a WebSocket InputSource (a socket message
// {"move":...} -> a Check-guarded TetrisActor verb) plus an OutputTarget (a
// WebSocketSink -> the frame JSON pushed to the browser). Transport is RAW
// WebSockets: ASP.NET WebSocket middleware here, the browser's native WebSocket
// client-side, pages served inline (no CDN/library) so it works offline.
//
//   /          player page (arrow keys / space -> moves; renders the board)
//   /observer  observer page (watches ALL active sessions at once, read-only)
//   /ws?session=<id>&role=player|observer   the WebSocket endpoint

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders(); // keep stdout clean for the run banner
var app = builder.Build();

var hub = new GameHub();
app.Lifetime.ApplicationStopping.Register(hub.Dispose);

app.UseWebSockets();

app.MapGet("/", () => Results.Content(PlayerPage(), "text/html"));
app.MapGet("/observer", () => Results.Content(ObserverPage(), "text/html"));

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    var role = context.Request.Query["role"].ToString();
    var session = context.Request.Query["session"].ToString();
    if (string.IsNullOrWhiteSpace(session)) session = "default";

    using var socket = await context.WebSockets.AcceptWebSocketAsync();

    if (role == "observer")
    {
        await ObserverLoop(socket, hub, context.RequestAborted);
    }
    else
    {
        await PlayerLoop(socket, hub, session, context.RequestAborted);
    }
});

var url = "http://localhost:5080";
app.Urls.Add(url);
Console.WriteLine($"Tetris web host running at {url}");
Console.WriteLine($"  player   : {url}/");
Console.WriteLine($"  observer : {url}/observer");
Console.WriteLine($"  socket   : {url.Replace("http", "ws")}/ws?session=<id>&role=player|observer");
app.Run();

// ── A player socket: an InputSource (move -> verb) bound to a session room, and
// an OutputTarget (the room's sink pushes frames back to this socket). ──────────
static async Task PlayerLoop(WebSocket socket, GameHub hub, string session, CancellationToken ct)
{
    var room = hub.Room(session);
    var token = room.Sink.Attach(socket);
    try
    {
        // Show the current frame immediately on connect.
        await room.Sink.SendCurrentTo(socket, ct);

        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) break;

            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var move = ParseMove(text);
            if (move is not null)
            {
                room.Apply(move); // serial actor verb -> reaction pushes the frame to all sockets
            }
        }
    }
    catch (OperationCanceledException) { }
    catch (WebSocketException) { }
    finally
    {
        room.Sink.Detach(token);
    }
}

// ── An observer socket: a pure OutputTarget consumer of EVERY session's frames.
static async Task ObserverLoop(WebSocket socket, GameHub hub, CancellationToken ct)
{
    var oid = hub.AttachObserver(socket);
    try
    {
        await hub.SendAllCurrentTo(socket, ct); // snapshot every active game on connect

        // Observers send nothing; just keep the socket open until it closes.
        var buffer = new byte[1024];
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) break;
        }
    }
    catch (OperationCanceledException) { }
    catch (WebSocketException) { }
    finally
    {
        hub.DetachObserver(oid);
    }
}

// Accepts {"move":"left"} or a bare "left"; returns a known move or null.
static string? ParseMove(string text)
{
    string? raw = null;
    try
    {
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("move", out var m))
        {
            raw = m.GetString();
        }
    }
    catch (JsonException)
    {
        raw = text.Trim();
    }

    raw = raw?.Trim().ToLowerInvariant();
    return raw is "left" or "right" or "rotate" or "tick" or "drop" ? raw : null;
}

// ─────────────────────────────── inline pages ──────────────────────────────────
// Self-contained HTML/JS — no external CDN/library. The JS mirrors BoardRenderer:
// parse the frame JSON (width/height/cleared/over/awaiting/type/cell[]) and draw a
// grid of filled "[]" vs blank cells.

static string PlayerPage() => """
<!doctype html><html><head><meta charset="utf-8"><title>Tetris — player</title>
<style>
 body{background:#111;color:#ddd;font-family:monospace;text-align:center}
 #board{font-size:18px;line-height:18px;white-space:pre;display:inline-block;margin-top:1em;letter-spacing:0}
 .hud{margin:.5em}
 .cell{display:inline-block;width:1.1em}
</style></head><body>
<h2>Tetris (web) — same Well, web shell</h2>
<div class="hud">session: <b id="sess"></b> &nbsp; type: <b id="type">-</b> &nbsp; cleared: <b id="cleared">0</b> <span id="over"></span></div>
<div class="hud">← → move &nbsp; ↑ rotate &nbsp; ↓ soft drop &nbsp; space hard drop</div>
<pre id="board">connecting…</pre>
<script>
 const params = new URLSearchParams(location.search);
 const session = params.get('session') || 'web1';
 document.getElementById('sess').textContent = session;
 const ws = new WebSocket(`ws://${location.host}/ws?session=${encodeURIComponent(session)}&role=player`);
 const keymap = {ArrowLeft:'left',ArrowRight:'right',ArrowUp:'rotate',ArrowDown:'tick',' ':'drop'};
 addEventListener('keydown', e => {
   const mv = keymap[e.key];
   if (mv && ws.readyState === 1) { ws.send(JSON.stringify({move:mv})); e.preventDefault(); }
 });
 ws.onmessage = ev => render(JSON.parse(ev.data));
 ws.onclose = () => { document.getElementById('board').textContent = '[disconnected]'; };
 function render(f){
   document.getElementById('type').textContent = f.type || '-';
   document.getElementById('cleared').textContent = f.cleared;
   document.getElementById('over').textContent = f.over ? '  — GAME OVER' : '';
   const filled = new Set((f.cell||[]).map(c => c.r+','+c.c));
   let s = '';
   for (let r=0;r<f.height;r++){ let row='|'; for(let c=0;c<f.width;c++){ row += filled.has(r+','+c)?'[]':'  ';} s += row+'|\n'; }
   s += '+'+'='.repeat(f.width*2)+'+';
   document.getElementById('board').textContent = s;
 }
</script></body></html>
""";

static string ObserverPage() => """
<!doctype html><html><head><meta charset="utf-8"><title>Tetris — observer</title>
<style>
 body{background:#111;color:#ddd;font-family:monospace}
 h2{text-align:center}
 #grid{display:flex;flex-wrap:wrap;gap:1.2em;justify-content:center}
 .game{border:1px solid #333;padding:.5em}
 .game pre{font-size:11px;line-height:11px;white-space:pre;margin:.2em 0}
 .label{font-weight:bold}
</style></head><body>
<h2>Tetris observer — all games at once (read-only)</h2>
<div id="grid"></div>
<script>
 const ws = new WebSocket(`ws://${location.host}/ws?role=observer`);
 const games = {};
 ws.onmessage = ev => { const e = JSON.parse(ev.data); upsert(e.session, e.frame); };
 function upsert(session, f){
   let g = games[session];
   if (!g){
     const div = document.createElement('div'); div.className='game';
     const label = document.createElement('div'); label.className='label';
     const pre = document.createElement('pre');
     div.appendChild(label); div.appendChild(pre);
     document.getElementById('grid').appendChild(div);
     g = games[session] = {label, pre};
   }
   g.label.textContent = `${session}  [type ${f.type||'-'}  cleared ${f.cleared}${f.over?'  OVER':''}]`;
   const filled = new Set((f.cell||[]).map(c => c.r+','+c.c));
   let s='';
   for(let r=0;r<f.height;r++){ let row='|'; for(let c=0;c<f.width;c++){ row += filled.has(r+','+c)?'[]':'  ';} s+=row+'|\n'; }
   s += '+'+'='.repeat(f.width*2)+'+';
   g.pre.textContent = s;
 }
</script></body></html>
""";
