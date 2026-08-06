using Tetris.Redecomp;

// Measurement harness for the Tetris re-decomposition (Well -> pile role + piece
// role). Every mode is a separate experiment; see
// Tetris/notes/redecomposition-pile-and-piece.md.

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: TetrisRedecomp <mode> [args]");
    Console.Error.WriteLine("  equivalence <random|flat|clears> [games] [ops]  exp 1: invariant + agreement of the two cuts");
    Console.Error.WriteLine("  split-game <root> [seed] [ops] [--dump]        exp 2: what the split costs on the record");
    Console.Error.WriteLine("  play <journalDir> [seed] [ops]                 exp 3a: play a game on the single Well");
    Console.Error.WriteLine("  redecompose <journalDir> <splitRoot> [--dump]  exp 3b: THE re-decomposition (fresh process)");
    Console.Error.WriteLine("  dump <actor> <journalDir> [--verbose]          read any journal, in a fresh process");
    Console.Error.WriteLine("  boards <journalDir> <splitRoot>                SEE both decompositions, side by side");
    Console.Error.WriteLine("  probe-read <actor> <journalDir>                probe: read a journal read-only");
    return 2;
}

switch (args[0])
{
    case "equivalence":
        return Equivalence.Run(
            policyName: args.Length > 1 ? args[1] : "flat",
            games: args.Length > 2 ? int.Parse(args[2]) : 20,
            maxOps: args.Length > 3 ? int.Parse(args[3]) : 2000);

    case "play":
        return Replay.Play(
            journalDirectory: args[1],
            seed: args.Length > 2 ? int.Parse(args[2]) : 1,
            maxOps: args.Length > 3 ? int.Parse(args[3]) : 400);

    case "redecompose":
        return Replay.Redecompose(
            journalDirectory: args[1],
            splitRoot: args[2],
            dump: args.Contains("--dump"));

    case "split-game":
        return SplitGame.Run(
            root: args[1],
            seed: args.Length > 2 ? int.Parse(args[2]) : 1,
            maxOps: args.Length > 3 ? int.Parse(args[3]) : 2000,
            dump: args.Contains("--dump"));

    case "dump":
        return Dump.Run(args[1], args[2], args.Contains("--verbose"));

    case "boards":
        return Show.Run(args[1], args[2]);

    case "probe-read":
        Probe.ReadJournal(args[1], args[2]);
        return 0;

    default:
        Console.Error.WriteLine($"unknown mode '{args[0]}'");
        return 2;
}
