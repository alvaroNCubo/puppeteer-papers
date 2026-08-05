using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace Tetris.Input;

/// <summary>
/// Input source whose medium is a NAMED PIPE ("tetris-&lt;session&gt;").
/// Transport: a one-command-per-connection <see cref="NamedPipeServerStream"/>
/// loop (extracted verbatim from the v3 TetrisServer). Routing: each line is
/// validated against the known logical commands and submitted; unknown lines are
/// dropped. The unchanged <c>TetrisSend</c> CLI is the client for this source.
/// </summary>
public sealed class PipeSource : IInputSource
{
    private static readonly string[] Known = ["left", "right", "rotate", "tick", "drop", "quit"];

    private readonly string pipeName;

    public PipeSource(string session) => pipeName = "tetris-" + session;

    public string Name => "pipe";

    public void Run(Action<string> submit, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // One command per connection (the standard named-pipe request
            // pattern). A 1-instance server stream is recreated each loop.
            using var pipe = new NamedPipeServerStream(
                pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            try
            {
                // This source owns its thread and must block until a client
                // connects or cancellation fires; the async overload is used only
                // because it honours the cancellation token. Synchronously waiting
                // here is intentional and safe (dedicated thread, no sync context).
#pragma warning disable VSTHRD002
                pipe.WaitForConnectionAsync(ct).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                continue; // a client aborted mid-handshake; await the next
            }

            string? line;
            using (var reader = new StreamReader(pipe))
            {
                line = reader.ReadLine();
            }

            if (line is null)
            {
                continue;
            }

            // Routing: validate against the known logical commands; drop unknowns.
            var command = line.Trim().ToLowerInvariant();
            if (Array.IndexOf(Known, command) < 0)
            {
                continue;
            }

            submit(command);
            if (command == "quit")
            {
                return;
            }
        }
    }
}
