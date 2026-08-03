using System.Runtime.CompilerServices;

// From outside the hexagon what is visible is exactly its ports, the contract
// they carry, and the application service that implements the driving port.
// Every rule-bearing type — Well, Piece, Pile, Shape, Frame — stays internal,
// as in the journaled domain. The test suite is a trusted insider so it can
// exercise the model directly as well as through the ports.
[assembly: InternalsVisibleTo("TetrisHexDomain.Tests")]
