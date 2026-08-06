using System;
using Choreography.StageManager;
using Choreography.Theater;
using Puppeteer;
using Puppeteer.EventSourcing.Follower;
using Puppeteer.EventSourcing.Interpreter.Formatters;

namespace Tetris.Acting;

/// <summary>
/// The minimal seam <see cref="TetrisActor"/> needs from whatever Puppeteer host
/// it runs on. The SAME clean Well domain can be hosted by a single-actor
/// <see cref="PerformanceV2"/> or by a distributed <see cref="StageV2"/>
/// (StageManager) — the host is an accidental shell. This interface is the
/// polymorphic frontier: every host detail (sync vs async commands, the
/// check-then-command shape) is adapted here so the actor's verbs stay identical
/// and host-agnostic.
/// </summary>
internal interface IGameHost : IDisposable
{
    /// <summary>Perform a command; returns the rendered output.</summary>
    string Command(string script);

    /// <summary>Perform a parametrized command — journals an Action, not a V1 Script.</summary>
    string Command(string script, Action<Parameters> configure);

    /// <summary>Perform a check-then-command (the gentle guard); no-op if the check fails.</summary>
    string CheckThenCommand(string check, string command);

    /// <summary>PROBE: the same guard, parametrized — journals an Action, not a Script.</summary>
    string CheckThenCommand(string check, string command, Action<Parameters> configure);

    /// <summary>Run a query; returns the rendered output (sync on every host).</summary>
    string Query(string script);

    /// <summary>Configure the push channel (sink + formatter) for the frame reaction.</summary>
    void WireOutput(IOutputSink sink, IOutputFormatter format);

    /// <summary>The reactions registry, for defining the frame Job reactions.</summary>
    Reactions Reactions { get; }
}

/// <summary>
/// Host adapter over a single-actor <see cref="PerformanceV2"/>. Commands and
/// queries are synchronous fluent calls — this is the original Tetris host.
/// </summary>
internal sealed class PerformanceHost : IGameHost
{
    private readonly PerformanceV2 performance;

    public PerformanceHost(PerformanceV2 performance) => this.performance = performance;

    public string Command(string script) =>
        performance.Using(script).PerformCommand();

    public string Command(string script, Action<Parameters> configure) =>
        performance.Using(script).WithParameters(configure).PerformCommand();

    public string CheckThenCommand(string check, string command) =>
        performance.Using(check, command).PerformCheckThenCommand();

    public string CheckThenCommand(string check, string command, Action<Parameters> configure) =>
        performance.Using(check, command).WithParameters(configure).PerformCheckThenCommand();

    public string Query(string script) =>
        performance.Using(script).PerformQuery();

    public void WireOutput(IOutputSink sink, IOutputFormatter format) =>
        performance.OutputTarget(sink, format);

    public Reactions Reactions => performance.Actor.Reactions;

    public void Dispose() => performance.Dispose();
}

/// <summary>
/// Host adapter over a distributed <see cref="StageV2"/> (StageManager). Commands
/// are async on the Stage (a Director executes locally; a Cast forwards to the
/// Director over the transport); we block on them here so the actor's verbs keep
/// their synchronous signatures. Blocking is safe because every runner serialises
/// its commands — one in flight at a time — so there is no sync-over-async
/// re-entrancy. Queries are synchronous on the Stage. A query needs a
/// <see cref="Parameters"/> bag (the engine injects Now); an empty one suffices.
/// </summary>
internal sealed class StageHost : IGameHost
{
    private readonly StageV2 stage;

    public StageHost(StageV2 stage) => this.stage = stage;

    // VSTHRD002: blocking on the Stage's async command is intentional — the actor's
    // verbs are synchronous and the runner serialises commands (one in flight), so
    // there is no sync-over-async re-entrancy. See the type doc above.
#pragma warning disable VSTHRD002
    public string Command(string script) =>
        stage.PerformCmd(script).GetAwaiter().GetResult();

    public string Command(string script, Action<Parameters> configure)
    {
        var parameters = new Parameters();
        configure(parameters);
        return stage.PerformCmd(script, parameters, DateTime.Now, "0.0.0.0", "Anonymous")
            .GetAwaiter().GetResult();
    }

    public string CheckThenCommand(string check, string command) =>
        stage.PerformCheckThenCommand(check, command, DateTime.Now, "0.0.0.0", "Anonymous")
            .GetAwaiter().GetResult();

    public string CheckThenCommand(string check, string command, Action<Parameters> configure)
    {
        var parameters = new Parameters();
        configure(parameters);
        return stage.PerformCheckThenCommand(check, command, parameters, DateTime.Now, "0.0.0.0", "Anonymous")
            .GetAwaiter().GetResult();
    }
#pragma warning restore VSTHRD002

    public string Query(string script) =>
        stage.PerformQry(script, new Parameters());

    public void WireOutput(IOutputSink sink, IOutputFormatter format) =>
        stage.OutputTarget(sink, format);

    public Reactions Reactions => stage.Reactions;

    // The Stage's lifecycle (IAsyncDisposable) is owned by the runner that
    // created it (await using), not by the TetrisActor — disposing the actor
    // must not tear down a shared Stage. So this is a no-op.
    public void Dispose()
    {
    }
}
