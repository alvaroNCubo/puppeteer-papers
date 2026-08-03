using System.Text.Json;
using Tetris.HexWebRest;

// Staging 3 of the ports-and-adapters baseline: the SAME hexagon over REST + SSE,
// a sibling of staging 2 for side-by-side contrast. Over REST the hexagon's two
// sides land on physically separate channels — input is a POST, output is an SSE
// stream — whereas the WebSocket pipe conflated them. And pull versus push shows
// up as a property of the DESTINATION over one document: pushed via /events,
// pulled via /frame, both the same frame JSON.
//
//   POST /games/{session}/moves   input  (body {"move":...}) -> driving-port call
//   GET  /games/{session}/events  output (text/event-stream, push)
//   GET  /games/{session}/frame   output (application/json, pull — same document)
//   GET  /observer/events         observer SSE, fans out ALL sessions
//   /  player page   /observer  observer page   (inline HTML/JS, native EventSource)
//
// Port 5091, so it can run beside staging 2 (5090) and the journaled hosts.

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
var app = builder.Build();

var hub = new RestGameHub();
app.Lifetime.ApplicationStopping.Register(hub.Dispose);

app.MapGet("/", () => Results.Content(PlayerPage(), "text/html"));
app.MapGet("/observer", () => Results.Content(ObserverPage(), "text/html"));

// ── INPUT: the DRIVING adapter. A move arrives on a POST and becomes a call on
// the driving port. The frame does NOT come back in this response — it goes out
// the SSE channel, which is the point of the separation. ────────────────────────
app.MapPost("/games/{session}/moves", async (string session, HttpRequest req) =>
{
    string body;
    using (var reader = new StreamReader(req.Body))
    {
        body = await reader.ReadToEndAsync();
    }

    var move = ParseMove(body);
    if (move is null)
    {
        return Results.BadRequest(new { error = "move must be one of left|right|rotate|tick|drop" });
    }

    hub.Room(session).Apply(move); // create-on-first-touch; the room serialises it
    return Results.Ok(new { accepted = move }); // frame goes out /events, not here
});

// ── OUTPUT (pull): the last presented board as a plain JSON GET — the SAME
// document SSE pushes. Served from the OUTPUT ADAPTER's cache, because the
// hexagon has no query port to ask. ────────────────────────────────────────────
app.MapGet("/games/{session}/frame", (string session) =>
{
    if (hub.TryGet(session, out var room) && room.Output.LastFrame is { } frame)
    {
        return Results.Content(frame, "application/json");
    }

    return Results.NotFound(new { error = $"no active frame for session '{session}'" });
});

// ── OUTPUT (push): an SSE stream. Keep the response open; the session's
// SseBoardOutput writes each presented board as a data: event. ─────────────────
app.MapGet("/games/{session}/events", async (string session, HttpContext ctx) =>
{
    var room = hub.Room(session); // create-on-first-touch, so a viewer can open first
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.ContentType = "text/event-stream";

    await using var writer = new StreamWriter(ctx.Response.Body) { AutoFlush = false };
    if (room.Output.LastFrame is { } current)
    {
        await SseBoardOutput.WriteEvent(writer, current, ctx.RequestAborted);
    }

    var token = room.Output.Subscribe(writer);
    try
    {
        await HoldOpen(ctx.RequestAborted);
    }
    finally
    {
        room.Output.Unsubscribe(token);
    }
});

// ── OBSERVER (push): one SSE stream fanning out EVERY session's frame, tagged. ──
app.MapGet("/observer/events", async (HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.ContentType = "text/event-stream";

    await using var writer = new StreamWriter(ctx.Response.Body) { AutoFlush = false };
    await hub.SendAllCurrentTo(writer, ctx.RequestAborted); // snapshot active games

    var oid = hub.AddObserver(writer);
    try
    {
        await HoldOpen(ctx.RequestAborted);
    }
    finally
    {
        hub.RemoveObserver(oid);
    }
});

var url = "http://localhost:5091";
app.Urls.Add(url);
Console.WriteLine($"Tetris HEX REST+SSE host running at {url}");
Console.WriteLine($"  player   : {url}/");
Console.WriteLine($"  observer : {url}/observer");
Console.WriteLine($"  input    : POST {url}/games/<id>/moves   body {{\"move\":\"left|right|rotate|tick|drop\"}}");
Console.WriteLine($"  push     : GET  {url}/games/<id>/events   (text/event-stream)");
Console.WriteLine($"  pull     : GET  {url}/games/<id>/frame    (application/json)");
app.Run();

// Keep an SSE response open until the client disconnects.
static async Task HoldOpen(CancellationToken ct)
{
    try
    {
        await Task.Delay(Timeout.Infinite, ct);
    }
    catch (OperationCanceledException)
    {
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

static string PlayerPage() => """
<!doctype html><html><head><meta charset="utf-8"><title>Tetris hex — REST player</title>
<style>
 body{background:#111;color:#ddd;font-family:monospace;text-align:center}
 #board{font-size:18px;line-height:18px;white-space:pre;display:inline-block;margin-top:1em}
 .hud{margin:.5em}
</style></head><body>
<h2>Tetris (hex baseline) — REST in, SSE out</h2>
<div class="hud">session: <b id="sess"></b> &nbsp; type: <b id="type">-</b> &nbsp; cleared: <b id="cleared">0</b> <span id="over"></span></div>
<div class="hud">← → move &nbsp; ↑ rotate &nbsp; ↓ soft drop &nbsp; space hard drop</div>
<pre id="board">connecting…</pre>
<script>
 const params = new URLSearchParams(location.search);
 const session = params.get('session') || 'hexrest1';
 document.getElementById('sess').textContent = session;
 const keymap = {ArrowLeft:'left',ArrowRight:'right',ArrowUp:'rotate',ArrowDown:'tick',' ':'drop'};
 addEventListener('keydown', e => {
   const mv = keymap[e.key];
   if (!mv) return;
   e.preventDefault();
   fetch(`/games/${encodeURIComponent(session)}/moves`, {
     method:'POST', headers:{'content-type':'application/json'},
     body: JSON.stringify({move:mv})
   });
 });
 const es = new EventSource(`/games/${encodeURIComponent(session)}/events`);
 es.onmessage = ev => render(JSON.parse(ev.data));
 es.onerror = () => { document.getElementById('board').textContent = '[stream closed]'; };
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
<!doctype html><html><head><meta charset="utf-8"><title>Tetris hex — REST observer</title>
<style>
 body{background:#111;color:#ddd;font-family:monospace}
 h2{text-align:center}
 #grid{display:flex;flex-wrap:wrap;gap:1.2em;justify-content:center}
 .game{border:1px solid #333;padding:.5em}
 .game pre{font-size:11px;line-height:11px;white-space:pre;margin:.2em 0}
 .label{font-weight:bold}
</style></head><body>
<h2>Tetris hex observer (SSE) — all games at once (read-only)</h2>
<div id="grid"></div>
<script>
 const es = new EventSource('/observer/events');
 const games = {};
 es.onmessage = ev => { const e = JSON.parse(ev.data); upsert(e.session, e.frame); };
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
