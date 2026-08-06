using System.Text.RegularExpressions;
using Tetris.Acting;

namespace Tetris.Redecomp;

/// <summary>
/// Experiment 3 — THE RE-DECOMPOSITION. A game is played on the single
/// <c>Well</c> and its journal is kept. Then the two new roles are authored (they
/// already are), the original's record is READ, and each new role is put to PERFORM
/// ITS OWN part of what that record says happened. Each ends with a journal of its
/// own acts in its own voice.
/// <para>
/// What is deliberately NOT done: the original journal is not cut, not transformed,
/// not rewritten, and not copied into the new roles. Transplanting entries would put
/// acts a role never performed into that role's record, which is precisely what a
/// first-person journal must not contain. So the original is opened read-only, read
/// as the account of what happened, and kept — and the new records are produced by
/// performing, not by editing.
/// </para>
/// <para>
/// The reading is the same reading a rehydration does: entry by entry in order, each
/// act with its parameters, joining an invocation to the define that names its
/// sentence. That is also why this direction scales. Manipulating journals as
/// artifacts is possible and gets worse as they grow — and here it is not even
/// available, the backend being binary. Reading acts and re-performing them is
/// bounded by the acts themselves.
/// </para>
/// </summary>
internal static class Replay
{
    internal const string Session = "played";

    /// <summary>
    /// Step 1, in its OWN process: play a game on the original single-<c>Well</c>
    /// domain and leave its journal behind. It is a separate process on purpose. The
    /// journal must be a genuinely pre-existing record, closed and finished, before
    /// anything reads it — a first attempt that played and read in one process read a
    /// journal whose tail the writer had not yet flushed, and so read a shorter game
    /// than had been played, and saw the file change afterwards. The record has to be
    /// closed to be the record.
    /// </summary>
    internal static int Play(string journalDirectory, int seed, int maxOps)
    {
        if (Directory.Exists(journalDirectory))
        {
            Directory.Delete(journalDirectory, recursive: true);
        }

        Directory.CreateDirectory(journalDirectory);

        Step final;
        int acts;
        var issued = new Dictionary<string, int>(StringComparer.Ordinal);
        var sequence = new List<string>();
        List<Step> steps;
        using (var well = TetrisActor.Persistent(Session, 10, 20, journalDirectory))
        {
            steps = PlayOriginal(well, seed, maxOps, issued, sequence);
            acts = steps.Count;
            final = steps[^1];
        }

        // The board after EVERY act, so a record that turns out to hold only the
        // first N acts can still be checked exactly — against the state the well was
        // in after its N-th act, rather than only against where it finished.
        File.WriteAllLines(Path.Combine(journalDirectory, "played-boards.txt"),
            steps.Select((s, i) => $"{i + 1}|{sequence[i]}|{s.Cleared}|{s.Over}|{s.Awaiting}|{s.Type ?? "-"}|{s.Board}"));

        // The state the well was in when it was played, recorded at play time. The
        // re-decomposition is compared against this, and against a rehydration of the
        // very same journal (see Redecompose).
        File.WriteAllLines(Path.Combine(journalDirectory, "played-state.txt"),
        [
            $"acts={acts}",
            $"cleared={final.Cleared}",
            $"over={final.Over}",
            $"awaiting={final.Awaiting}",
            $"type={final.Type ?? "-"}",
            $"issued={string.Join(" ", issued.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}:{p.Value}"))}",
            $"board={final.Board}",
        ]);

        Console.WriteLine($"played {acts} acts on the single Well: cleared={final.Cleared} over={final.Over}");
        Console.WriteLine($"journal: {journalDirectory}");
        return 0;
    }

    /// <summary>
    /// Step 2, in a fresh process: the re-decomposition proper, over the journal the
    /// previous process left behind.
    /// </summary>
    internal static int Redecompose(string journalDirectory, string splitRoot, bool dump)
    {
        if (Directory.Exists(splitRoot))
        {
            Directory.Delete(splitRoot, recursive: true);
        }

        Directory.CreateDirectory(splitRoot);

        var session = Session;
        var failures = 0;

        var played = File.ReadAllLines(Path.Combine(journalDirectory, "played-state.txt"))
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => parts[1]);

        var originalFinal = new Step(
            "played",
            played["board"],
            int.Parse(played["cleared"]),
            bool.Parse(played["over"]),
            bool.Parse(played["awaiting"]),
            played["type"] == "-" ? null : played["type"]);

        Console.WriteLine("── the game that had been played, on the single Well ──");
        Console.WriteLine($"acts performed   : {played["acts"]}");
        Console.WriteLine($"lines cleared    : {originalFinal.Cleared}");
        Console.WriteLine($"game over        : {originalFinal.Over}");

        var originalJournal = journalDirectory;

        // ── 2. The original journal, as it stands before anything reads it ─────
        var before = Fingerprint.Of(originalJournal);

        // ── 3. Read the record: the acts and their parameters, in order ────────
        var acts = JournalActs.Read(session, originalJournal);
        var reading = acts.Select(Interpret).ToList();
        var untranslatable = reading.Where(r => r.Verb is null).ToList();

        Console.WriteLine();
        Console.WriteLine("── the record, read (not transformed) ──");
        Console.WriteLine($"entries read     : {acts.Count}");
        if (JournalActs.Gaps.Count > 0)
        {
            Console.WriteLine($"  GAPS at entry ids: {string.Join(", ", JournalActs.Gaps)} — the record read is not the record written");
            failures++;
        }

        Console.WriteLine($"  script         : {acts.Count(a => a.Kind == "script")}");
        Console.WriteLine($"  define         : {acts.Count(a => a.Kind == "define")}");
        Console.WriteLine($"  invocation     : {acts.Count(a => a.Kind == "invocation")}");
        // The acts the record holds, next to the verbs the staging actually issued.
        // They should agree exactly: a verb whose Check guard rejected it leaves no
        // act (state untouched, nothing recorded), so any difference is either a
        // rejected verb or a record that is not complete.
        var recorded = reading
            .Where(r => r.Act.Kind != "define")
            .GroupBy(r => JournalActs.VerbOf(r.Act))
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var issued = played.TryGetValue("issued", out var issuedText)
            ? issuedText.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split(':'))
                .ToDictionary(parts => parts[0], parts => int.Parse(parts[1]), StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);

        // Is what was recorded the FIRST N acts of what was issued, or acts missing
        // from the middle? A faithful prefix can still be checked exactly, against the
        // state the well was in after its N-th act. Scattered losses could not be.
        var issuedSequence = File.ReadAllLines(Path.Combine(journalDirectory, "played-boards.txt"))
            .Select(line => line.Split('|'))
            .ToList();
        var recordedSequence = reading
            .Where(r => r.Verb is not null && r.Verb != "declaration" && r.Verb != "seed")
            .Select(r => r.Verb == "spawn" ? $"well.Spawn({r.Argument})" : "well." + Capitalise(r.Verb!))
            .ToList();

        var prefixLength = 0;
        while (prefixLength < recordedSequence.Count
            && prefixLength < issuedSequence.Count
            && Normalise(recordedSequence[prefixLength]) == Normalise(issuedSequence[prefixLength][1]))
        {
            prefixLength++;
        }

        Console.WriteLine($"issued acts      : {issuedSequence.Count}");
        Console.WriteLine($"recorded acts    : {recordedSequence.Count}");
        Console.WriteLine($"identical prefix : {prefixLength} acts");
        var isPrefix = prefixLength == recordedSequence.Count;
        Console.WriteLine(isPrefix
            ? $"  the record is the first {recordedSequence.Count} acts of the game, in order — a faithful PREFIX"
            : $"  the record DIVERGES from the issued sequence at act {prefixLength + 1}: " +
              $"recorded '{recordedSequence.ElementAtOrDefault(prefixLength)}', issued '{issuedSequence.ElementAtOrDefault(prefixLength)?[1]}'");

        Console.WriteLine();
        Console.WriteLine("acts by verb — issued by the staging / recorded in the journal / where each goes:");
        foreach (var verb in recorded.Keys.Union(issued.Keys).OrderBy(v => v, StringComparer.Ordinal))
        {
            var target = reading.FirstOrDefault(r => JournalActs.VerbOf(r.Act) == verb)?.Target ?? "-";
            var issuedCount = issued.GetValueOrDefault(verb);
            var recordedCount = recorded.GetValueOrDefault(verb);
            var flag = verb == "upgrade" || issuedCount == recordedCount ? " " : "!";
            Console.WriteLine($" {flag} {issuedCount,5} issued  {recordedCount,5} recorded  {verb,-18} -> {target}");
            if (flag == "!")
            {
                failures++;
            }
        }

        if (untranslatable.Count > 0)
        {
            Console.WriteLine($"UNTRANSLATABLE: {untranslatable.Count} acts");
            foreach (var act in untranslatable.Take(5))
            {
                Console.WriteLine($"  entry {act.Act.EntryId}: {JournalActs.Collapse(act.Act.Sentence)}");
            }

            failures++;
        }

        // ── 4. Put each new role to PERFORM its own part of what was read ──────
        var dimensions = reading.Select(r => r.Dimensions).FirstOrDefault(d => d is not null)
            ?? throw new InvalidOperationException("The record does not say how big the well was.");

        Step recutFinal;
        var replayed = 0;
        var seeds = 0;
        string pileActor, pieceActor, pileJournal, pieceJournal;
        using (var split = SplitTetrisActor.Persistent("recut", dimensions.Width, dimensions.Height, splitRoot))
        {
            pileActor = split.PileActorName;
            pieceActor = split.PieceActorName;
            pileJournal = split.PileJournal!;
            pieceJournal = split.PieceJournal!;

            foreach (var step in reading)
            {
                switch (step.Verb)
                {
                    case "declaration":
                        // Nothing to perform: the entry declares an action's template.
                        break;

                    case "seed":
                        // The well's opening act. Both roles were opened with the
                        // dimensions this act records; there is nothing further to
                        // perform. (The record holds several, because the staging that
                        // played the game re-issued the seed on every rehydration and
                        // the engine recognised it as already applied each time.)
                        seeds++;
                        break;

                    case "spawn": split.Spawn(step.Argument!); replayed++; break;
                    case "left": split.MoveLeft(); replayed++; break;
                    case "right": split.MoveRight(); replayed++; break;
                    case "rotate": split.Rotate(); replayed++; break;
                    case "tick": split.Tick(); replayed++; break;
                    case "drop": split.Drop(); replayed++; break;
                }
            }

            var snapshot = split.Snapshot();
            recutFinal = new Step("final", Boards.Of(snapshot), snapshot.ClearedLines,
                snapshot.IsGameOver, snapshot.IsAwaitingPiece, snapshot.ActiveType);
        }

        Console.WriteLine();
        Console.WriteLine("── re-performed onto the two roles ──");
        Console.WriteLine($"acts re-performed: {replayed} (plus {seeds} seed acts, which open the roles rather than being performed)");

        // ── 5. Is the state the two roles reached the state the well was in? ───
        // Compared against the well's state AFTER THE LAST ACT THE RECORD HOLDS. When
        // the record is the whole game that is the final state; when the record is a
        // prefix — as it is here, for reasons that have nothing to do with the re-cut
        // and are reported below — it is the state at the end of that prefix. Either
        // way the question is the same and it is the right one: does re-performing the
        // recorded acts on the two roles reach the state the well was in when it had
        // performed exactly those acts?
        var comparison = isPrefix && prefixLength > 0 && prefixLength <= issuedSequence.Count
            ? Parse(issuedSequence[prefixLength - 1])
            : originalFinal;

        Console.WriteLine();
        Console.WriteLine($"── equivalence of the state reached (well after act {(isPrefix ? prefixLength : issuedSequence.Count)}) ──");
        failures += Check("board", comparison.Board, recutFinal.Board);
        failures += Check("lines cleared", comparison.Cleared.ToString(), recutFinal.Cleared.ToString());
        failures += Check("game over", comparison.Over.ToString(), recutFinal.Over.ToString());
        failures += Check("awaiting piece", comparison.Awaiting.ToString(), recutFinal.Awaiting.ToString());
        failures += Check("falling piece", comparison.Type ?? "-", recutFinal.Type ?? "-");

        // ── 6. Was the original journal touched? ──────────────────────────────
        // Two questions, and they have different answers, so they are asked
        // separately. (a) Are the recorded ACTS the same after the re-decomposition
        // as before it — is the record still the same record? (b) Is the FILE
        // byte-identical?
        var after = Fingerprint.Of(originalJournal);
        var actsAfter = JournalActs.Read(session, originalJournal);
        var afterSecondRead = Fingerprint.Of(originalJournal);

        Console.WriteLine();
        Console.WriteLine("── the original journal, after the re-decomposition ──");

        var sameActs = acts.Count == actsAfter.Count
            && acts.Zip(actsAfter).All(pair =>
                pair.First.EntryId == pair.Second.EntryId
                && pair.First.Kind == pair.Second.Kind
                && pair.First.Sentence == pair.Second.Sentence
                && pair.First.Arguments == pair.Second.Arguments);

        Console.WriteLine($"the RECORD: {acts.Count} acts before, {actsAfter.Count} after, identical: {sameActs}");
        if (!sameActs)
        {
            Console.WriteLine("  the recorded acts CHANGED — the re-decomposition altered the record it read");
            failures++;
        }

        Console.WriteLine($"nothing appended, nothing removed (file lengths): {Lengths(before) == Lengths(after)}");

        if (before == after)
        {
            Console.WriteLine("the FILE: byte-identical");
        }
        else
        {
            // Not byte-identical, and it is worth being exact about why rather than
            // calling it a modification. Reported, not worked around.
            Console.WriteLine("the FILE: NOT byte-identical. Which bytes moved:");
            foreach (var file in Directory.GetFiles(originalJournal, "*.bin", SearchOption.AllDirectories))
            {
                var name = Path.GetRelativePath(originalJournal, file).Replace('\\', '/');
                Console.WriteLine($"  {name}: {DiffSummary(before, after, name)}");
            }

            Console.WriteLine($"and a SECOND read changes it again (so it is the act of opening, not this experiment): "
                + $"{after != afterSecondRead}");
        }

        // ── 7. Does each new journal contain only its own role's acts? ─────────
        Console.WriteLine();
        Console.WriteLine("── the two new records, each in its own voice ──");
        failures += ReportRole("piece", pieceActor, pieceJournal, "piece.", dump);
        failures += ReportRole("pile", pileActor, pileJournal, "pile.", dump);

        // ── 8. And the framework's own rehydration of the original agrees ──────
        // Read on a COPY, because rehydrating writes (the staging re-issues its seed
        // upgrade, which the engine recognises as already applied but still records).
        // The original stays as step 6 left it; the copy carries the write.
        Console.WriteLine();
        Console.WriteLine("── cross-check: the framework's own rehydration of that same record ──");
        var copy = Path.Combine(splitRoot, "original-copy");
        CopyDirectory(originalJournal, copy);
        using (var rehydrated = TetrisActor.Persistent(session, 10, 20, copy))
        {
            var snapshot = rehydrated.Snapshot();
            var rehydratedFinal = new Step("rehydrated", Boards.Of(snapshot), snapshot.ClearedLines,
                snapshot.IsGameOver, snapshot.IsAwaitingPiece, snapshot.ActiveType);
            failures += Check("rehydrated board", originalFinal.Board, rehydratedFinal.Board);
            failures += Check("rehydrated cleared", originalFinal.Cleared.ToString(), rehydratedFinal.Cleared.ToString());
            failures += Check("rehydrated over", originalFinal.Over.ToString(), rehydratedFinal.Over.ToString());
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "RESULT: the re-decomposition reproduced the state, in two records, without touching the original."
            : $"RESULT: {failures} problem(s) — see above.");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>The name+length half of a fingerprint, ignoring the hashes.</summary>
    private static string Lengths(string fingerprint) =>
        string.Join("\n", fingerprint.TrimEnd().Split('\n')
            .Select(line => string.Join("  ", line.Trim().Split("  ").Take(2))));

    /// <summary>Whether one file's hash moved between two fingerprints.</summary>
    private static string DiffSummary(string before, string after, string name)
    {
        var b = before.TrimEnd().Split('\n').FirstOrDefault(l => l.Trim().StartsWith(name, StringComparison.Ordinal));
        var a = after.TrimEnd().Split('\n').FirstOrDefault(l => l.Trim().StartsWith(name, StringComparison.Ordinal));
        if (b is null || a is null)
        {
            return "missing from one side";
        }

        var bLength = b.Trim().Split("  ")[1];
        var aLength = a.Trim().Split("  ")[1];
        var bHash = b.Trim().Split("  ")[2];
        var aHash = a.Trim().Split("  ")[2];
        return bHash == aHash
            ? $"unchanged ({bLength} bytes)"
            : $"same length ({bLength} -> {aLength} bytes), different content";
    }

    /// <summary>One recorded board line: <c>n|verb|cleared|over|awaiting|type|board</c>.</summary>
    private static Step Parse(string[] fields) =>
        new(fields[1], fields[6], int.Parse(fields[2]), bool.Parse(fields[3]), bool.Parse(fields[4]),
            fields[5] == "-" ? null : fields[5]);

    private static string Capitalise(string verb) => verb switch
    {
        "left" => "MoveLeft",
        "right" => "MoveRight",
        "rotate" => "Rotate",
        "tick" => "Tick",
        "drop" => "Drop",
        _ => verb,
    };

    private static string Normalise(string verb) => verb.Replace("'", string.Empty);

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
        }
    }

    private static int ReportRole(string role, string actorName, string journal, string ownPrefix, bool dump)
    {
        var acts = JournalActs.Read(actorName, journal);
        Console.WriteLine();
        Console.WriteLine($"{role} role ({actorName}) — {acts.Count} entries");

        var failures = 0;
        var foreign = new List<Act>();
        foreach (var group in acts.GroupBy(JournalActs.VerbOf)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {group.Count(),5}  {group.Key}");
        }

        // A first-person record: every domain call in it must be on this role's own
        // aggregate. Speech acts (tell / tell ack) and the opening upgrade are this
        // role's own too — it is the one that spoke, and the one that was answered.
        foreach (var act in acts)
        {
            var verb = JournalActs.VerbOf(act);
            var isOwnCall = verb.StartsWith(ownPrefix, StringComparison.Ordinal);
            var isOwnSpeech = verb.StartsWith("tell", StringComparison.Ordinal) || verb == "upgrade";
            if (!isOwnCall && !isOwnSpeech)
            {
                foreign.Add(act);
            }
        }

        if (foreign.Count == 0)
        {
            Console.WriteLine($"  every domain call is on '{ownPrefix.TrimEnd('.')}' — no act of the other role appears");
        }
        else
        {
            Console.WriteLine($"  FOREIGN ACTS: {foreign.Count}");
            foreach (var act in foreign.Take(5))
            {
                Console.WriteLine($"    entry {act.EntryId}: {JournalActs.Collapse(act.Sentence)}");
            }

            failures++;
        }

        var contiguous = acts.Select((a, i) => a.EntryId == i + 1).All(x => x);
        Console.WriteLine($"  append-only: entry ids are the contiguous run 1..{acts.Count}: {contiguous}");
        if (!contiguous)
        {
            failures++;
        }

        if (dump)
        {
            foreach (var act in acts.Take(12))
            {
                Console.WriteLine($"    {act.EntryId,4}  {act.Kind,-10}  {JournalActs.Collapse(act.Sentence)}");
                if (act.Arguments.Length > 0)
                {
                    Console.WriteLine($"              args: {JournalActs.Collapse(act.Arguments)}");
                }
            }
        }

        return failures;
    }

    private static int Check(string what, string expected, string actual)
    {
        if (expected == actual)
        {
            Console.WriteLine($"  MATCH  {what}: {Shorten(actual)}");
            return 0;
        }

        Console.WriteLine($"  DIFFER {what}:");
        Console.WriteLine($"    well : {expected}");
        Console.WriteLine($"    recut: {actual}");
        return 1;
    }

    private static string Shorten(string value) =>
        value.Length <= 90 ? value : value[..87] + "...";

    private static List<Step> PlayOriginal(
        IGameActor game, int seed, int maxOps, Dictionary<string, int> issued, List<string> sequence)
    {
        // A real game: squares onto the flattest span, so rows complete and collapse.
        var policy = new FlatPolicy(seed);
        var random = new Random(seed * 7919);
        var alphabet = new[] { "I", "O", "T", "S", "Z", "J", "L" };
        var steps = new List<Step>();

        for (var step = 0; step < maxOps; step++)
        {
            var before = game.Snapshot();
            if (before.IsGameOver)
            {
                break;
            }

            string verb;
            if (before.IsAwaitingPiece)
            {
                var letter = alphabet[random.Next(alphabet.Length)];
                game.Spawn(letter);
                verb = $"well.Spawn({letter})";
                Count(issued, "well.Spawn");
            }
            else
            {
                switch (policy.Next(before, step))
                {
                    case Op.Left: game.MoveLeft(); verb = "well.MoveLeft"; Count(issued, "well.MoveLeft"); break;
                    case Op.Right: game.MoveRight(); verb = "well.MoveRight"; Count(issued, "well.MoveRight"); break;
                    case Op.Rotate: game.Rotate(); verb = "well.Rotate"; Count(issued, "well.Rotate"); break;
                    case Op.Tick: game.Tick(); verb = "well.Tick"; Count(issued, "well.Tick"); break;
                    default: game.Drop(); verb = "well.Drop"; Count(issued, "well.Drop"); break;
                }
            }

            sequence.Add(verb);

            var after = game.Snapshot();
            steps.Add(new Step("act", Boards.Of(after), after.ClearedLines,
                after.IsGameOver, after.IsAwaitingPiece, after.ActiveType));
        }

        return steps;
    }

    private static void Count(Dictionary<string, int> tally, string key) =>
        tally[key] = tally.GetValueOrDefault(key) + 1;

    /// <summary>
    /// One act read out of the original record, and what it means in the two new
    /// vocabularies. <see cref="Verb"/> is null when the act does not translate —
    /// which is the finding, if it ever happens.
    /// </summary>
    private sealed record Reading(Act Act, string? Verb, string? Argument, string Target, (int Width, int Height)? Dimensions);

    private static readonly Regex SeedDimensions = new(@"Well\(\s*(?<w>\d+)\s*,\s*(?<h>\d+)\s*\)", RegexOptions.Compiled);

    /// <summary>
    /// Translates one recorded act into the roles' vocabularies. The whole of the
    /// original record turns out to be the PIECE role's: spawn, the four move verbs,
    /// tick and drop are all things the falling piece did. The pile role's own act —
    /// absorbing a landing — appears nowhere in it, because under the well the absorb
    /// was never an act: it was a private consequence of a tick. So the pile role's
    /// record is not translated from the original at all; it arises from the piece
    /// role performing, and telling.
    /// </summary>
    private static Reading Interpret(Act act)
    {
        var text = JournalActs.Body(act.Sentence);
        var verb = JournalActs.VerbOf(act);

        // A define DECLARES a template; it is not an act performed. The act is the
        // invocation that names it. Re-performing on a define would double every
        // promoted verb.
        if (act.Kind == "define")
        {
            return new Reading(act, "declaration", null, "nothing (declares a template)", null);
        }

        if (verb == "upgrade")
        {
            return new Reading(act, "seed", null, "both roles (opens them)", Dimensions(act, text));
        }

        return verb switch
        {
            "well.Spawn" => new Reading(act, "spawn", Argument(act, text), "piece role: piece.Spawn", null),
            "well.MoveLeft" => new Reading(act, "left", null, "piece role: piece.MoveLeft", null),
            "well.MoveRight" => new Reading(act, "right", null, "piece role: piece.MoveRight", null),
            "well.Rotate" => new Reading(act, "rotate", null, "piece role: piece.Rotate", null),
            "well.Tick" => new Reading(act, "tick", null, "piece role: piece.Tick", null),
            "well.Drop" => new Reading(act, "drop", null, "piece role: piece.Drop", null),
            _ => new Reading(act, null, null, "UNTRANSLATABLE", null),
        };
    }

    /// <summary>
    /// How big the well was, read off the seed act. Same two places as
    /// <see cref="Argument"/>: an Invocation carries the dimensions as its arguments
    /// — <c>define action 1 (width:int, height:int) as … Well(width,height) … end;</c>
    /// — and only a V1 literal Script would carry them inline as <c>Well(10, 20)</c>.
    /// Reading the arguments first is what lets this harness read a record written by
    /// a parametrized staging; reading only the text is what made it fail with "the
    /// record does not say how big the well was" once the seed became an Action.
    /// </summary>
    private static (int Width, int Height)? Dimensions(Act act, string text)
    {
        if (act.Arguments.Length > 0)
        {
            var values = SplitArguments(act.Arguments).Select(v => Unquote(v).Trim()).ToArray();
            if (values.Length >= 2
                && int.TryParse(values[0], out var width)
                && int.TryParse(values[1], out var height))
            {
                return (width, height);
            }
        }

        var match = SeedDimensions.Match(text);
        return match.Success
            ? (int.Parse(match.Groups["w"].Value), int.Parse(match.Groups["h"].Value))
            : null;
    }

    /// <summary>
    /// The act's parameter. A Script carries it inline in the sentence; an Invocation
    /// carries it in its arguments, the sentence being the parametric template the
    /// define holds — so this is where the define/invocation join pays off.
    /// </summary>
    private static string? Argument(Act act, string text)
    {
        if (act.Arguments.Length > 0)
        {
            return Unquote(SplitArguments(act.Arguments).FirstOrDefault() ?? string.Empty);
        }

        var literal = Regex.Match(text, @"\(\s*'(?<value>[^']*)'\s*\)");
        return literal.Success ? literal.Groups["value"].Value : null;
    }

    /// <summary>Splits a recorded argument list on commas that are not inside quotes.</summary>
    private static IEnumerable<string> SplitArguments(string arguments)
    {
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var character in arguments)
        {
            switch (character)
            {
                case '\'':
                    inQuotes = !inQuotes;
                    current.Append(character);
                    break;

                case ',' when !inQuotes:
                    yield return current.ToString();
                    current.Clear();
                    break;

                default:
                    current.Append(character);
                    break;
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\''
            ? trimmed[1..^1]
            : trimmed;
    }
}
