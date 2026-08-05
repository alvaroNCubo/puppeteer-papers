namespace Tetris;

/// <summary>
/// Public anchor so a host can hand the assembly to the framework with
/// <c>typeof(TetrisDomain).Assembly</c>. The framework discovers the rest
/// of the domain by reflection. Mirrors the <c>WelcomeDomain</c> anchor of
/// the HelloWorld example: every verb-bearing type in this library is
/// <c>internal</c>; this empty public type is the single seam a host needs.
/// </summary>
public sealed class TetrisDomain
{
}
