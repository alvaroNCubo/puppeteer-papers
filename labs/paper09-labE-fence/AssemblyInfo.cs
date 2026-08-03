using System.Runtime.CompilerServices;

// From outside the domain abstraction only the public anchor TetrisDomain is
// visible; every other type is internal. A host reaches the model's verbs by
// reflection over the assembly, not by a compile-time reference. The test
// suite and the console demo are trusted insiders, so they are granted access
// to the internals they drive directly.
[assembly: InternalsVisibleTo("TetrisDomain.Tests")]
[assembly: InternalsVisibleTo("TetrisConsole")]
