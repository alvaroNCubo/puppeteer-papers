namespace Tetris;

/// <summary>
/// Public anchor so a host can hand the assembly to the framework with
/// <c>typeof(TetrisDomain).Assembly</c>. The framework discovers the rest
/// of the domain by reflection. Mirrors the <c>WelcomeDomain</c> anchor of
/// the HelloWorld example: every verb-bearing type in this library is
/// <c>internal</c>.
/// <para>
/// A convenience, not a requirement, and Lab F of Paper 9 reports the check:
/// the framework's seam takes an <c>Assembly</c> rather than a <c>Type</c> and
/// admits internal types when it reads one, so a host may load this library by
/// path and name nothing in it. Built with this file deleted, the domain exposes
/// no public type at all and still runs. It is kept because naming an assembly
/// in C# source is easier this way, which is the whole of its job.
/// </para>
/// </summary>
public sealed class TetrisDomain
{
}
