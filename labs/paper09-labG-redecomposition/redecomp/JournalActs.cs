using System.Text;
using System.Text.RegularExpressions;
using Choreography.Theater;
using Puppeteer;
using Puppeteer.EventSourcing.DB;
using Tetris;

namespace Tetris.Redecomp;

/// <summary>
/// One act as the journal records it: at which position, in what form
/// (<c>script</c> — the sentence verbatim; <c>define</c> — a parametric template;
/// <c>invocation</c> — that template's arguments), the sentence, the arguments, and
/// whatever the act exposed onto its own entry.
/// </summary>
internal sealed record Act(
    long EntryId,
    string Kind,
    string Sentence,
    int ActionId = 0,
    string Arguments = "",
    string ExposeData = "");

/// <summary>
/// Reads the acts out of an existing journal — the SAME read a rehydration does,
/// and nothing more. It opens the journal READ-ONLY through the framework's
/// introspection path (<c>ConfigureStorageForIntrospection</c>, which configures
/// storage WITHOUT rehydrating and exposes only the read verbs) and walks the
/// entries in order, taking each one's sentence and its parameters as recorded.
/// <para>
/// It never writes. It never edits, splits, transforms or rewrites the journal.
/// The journal is read as the account of what happened and is kept; what the
/// re-decomposition does with the account is make each new role PERFORM its own
/// part of it, so that each ends up with a journal of its own acts in its own
/// voice.
/// </para>
/// <para>
/// This is also the reading that scales. Operating on journals as artifacts — cut
/// this one in two, transform its entries — is possible and gets rapidly worse as
/// journals grow; and the backend here is binary, so there is no text to edit
/// anyway. Reading acts and re-performing them is bounded by the acts themselves
/// and is what a follower already does.
/// </para>
/// </summary>
internal static class JournalActs
{
    private static readonly Regex Field = new("^\\s*(?<key>[a-zA-Z]+):\\s*(?<value>.*)$", RegexOptions.Compiled);

    // define action <n> (<params>) as <body> end;  — the parametric template an
    // invocation names. What the act SAYS is the body.
    private static readonly Regex DefineWrapper = new(
        @"^define\s+action\s+\d+\s*\([^)]*\)\s+as\s+(?<body>.*?)\s*end;\s*$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Entry ids the last <see cref="Read"/> found missing and stepped over. Empty in
    /// a healthy journal; non-empty means the record read is not the record written.
    /// </summary>
    internal static IReadOnlyList<long> Gaps { get; private set; } = Array.Empty<long>();

    /// <summary>
    /// Every act in the journal at <paramref name="journalDirectory"/>, in
    /// recorded order. <paramref name="actorName"/> must be the name of the actor
    /// that WROTE it: the FileSystem backend nests each actor's journal under its
    /// own name, so the reader has to ask for the same one.
    /// </summary>
    internal static List<Act> Read(string actorName, string journalDirectory)
    {
        // A reader, not a participant: storage is configured for introspection, so
        // the actor is never rehydrated and cannot perform, tell, or react. The
        // read path is the only thing open.
        using var reader = new PerformanceV2(actorName, typeof(TetrisDomain).Assembly);
        reader.Actor.ConfigureStorageForIntrospection(
            DatabaseType.FileSystem, $"path={journalDirectory};maxFileSize=4194304");

        // There is no public "what is the head" after an introspection-only open
        // (CurrentEntryId is the rehydration counter and stays 0), so the walk runs
        // until entries stop coming. It tolerates a short run of misses before
        // concluding the end, and REPORTS any gap it stepped over, because a reader
        // that stops at the first miss silently truncates the record it is reading —
        // which would look exactly like a game that ended earlier than it did.
        const int missTolerance = 8;
        var acts = new List<Act>();
        var gaps = new List<long>();
        var misses = 0;
        for (long entryId = 1; misses <= missTolerance; entryId++)
        {
            string toon;
            try
            {
                toon = reader.Actor.Introspection.ShowEntry(entryId);
            }
            catch (LanguageException)
            {
                misses++;
                continue;
            }

            if (misses > 0)
            {
                gaps.Add(entryId - misses);
                misses = 0;
            }

            acts.Add(Parse(entryId, toon));
        }

        Gaps = gaps;

        // An invocation records only its actionId and its arguments; the SENTENCE it
        // invokes lives in the define entry that actionId names. Joining the two is
        // the same join a rehydration performs — it is how a replay knows what an
        // invocation says. Later defines for the same actionId win, as the framework's
        // own redefinition policy has it.
        var templates = new Dictionary<int, string>();
        for (var i = 0; i < acts.Count; i++)
        {
            var act = acts[i];
            if (act.Kind == "define" && act.ActionId != 0)
            {
                templates[act.ActionId] = act.Sentence;
            }
            else if (act.Kind == "invocation"
                && act.ActionId != 0
                && templates.TryGetValue(act.ActionId, out var template))
            {
                acts[i] = act with { Sentence = template };
            }
        }

        return acts;
    }

    private static Act Parse(long entryId, string toon)
    {
        var kind = string.Empty;
        var sentence = string.Empty;
        var arguments = string.Empty;
        var exposeData = string.Empty;
        var actionId = 0;

        foreach (var line in toon.Split('\n'))
        {
            var match = Field.Match(line.TrimEnd('\r'));
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value.Trim();

            switch (key)
            {
                case "kind":
                    kind = Unquote(value);
                    break;

                case "actionId":
                    int.TryParse(Unquote(value), out actionId);
                    break;

                // A Script entry carries the sentence verbatim; a Define carries the
                // parametric template. Both are the thing that was said.
                case "script":
                case "define":
                    sentence = Unescape(Unquote(value));
                    break;

                case "arguments":
                    arguments = Unescape(Unquote(value));
                    break;

                case "exposeData":
                    exposeData = Unescape(Unquote(value));
                    break;
            }
        }

        return new Act(entryId, kind, sentence, actionId, arguments, exposeData);
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;

    private static string Unescape(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 == value.Length)
            {
                builder.Append(value[i]);
                continue;
            }

            i++;
            builder.Append(value[i] switch
            {
                'r' => '\r',
                'n' => '\n',
                't' => '\t',
                '"' => '"',
                '\\' => '\\',
                var other => other,
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// The verb an act names, normalised for counting: the receiver and method of
    /// a domain call (<c>well.Tick</c>), or the speech act
    /// (<c>tell Landed</c>, <c>tell ack</c>), or <c>upgrade</c> for a seed.
    /// Everything else comes back as <c>?</c> plus the sentence, so nothing is
    /// silently classified.
    /// </summary>
    internal static string VerbOf(Act act)
    {
        var text = Body(act.Sentence);

        if (text.StartsWith("upgrade(", StringComparison.Ordinal))
        {
            return "upgrade";
        }

        if (text.StartsWith("tell ack", StringComparison.Ordinal))
        {
            return "tell ack";
        }

        if (text.StartsWith("tell ", StringComparison.Ordinal))
        {
            var name = text[5..].Split(' ', 2)[0];
            return "tell " + name;
        }

        var call = Regex.Match(text, @"^(?<receiver>[a-zA-Z_][a-zA-Z0-9_]*)\.(?<method>[a-zA-Z_][a-zA-Z0-9_]*)\s*\(");
        if (call.Success)
        {
            return call.Groups["receiver"].Value + "." + call.Groups["method"].Value;
        }

        return "? " + text;
    }

    /// <summary>
    /// What the act says, on one line and with the <c>define action … as … end;</c>
    /// wrapper removed. A define declares a template and an invocation names one, but
    /// the ACT in both cases is the body — so classification has to look through the
    /// wrapper, not at it.
    /// </summary>
    internal static string Body(string sentence)
    {
        var text = Collapse(sentence);
        var wrapper = DefineWrapper.Match(text);
        return wrapper.Success ? wrapper.Groups["body"].Value.Trim() : text;
    }

    /// <summary>The sentence on one line, for logging and classification.</summary>
    internal static string Collapse(string sentence) =>
        Regex.Replace(sentence.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' '), @"\s+", " ").Trim();
}
