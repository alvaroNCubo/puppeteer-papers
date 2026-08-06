using System.Text;
using Puppeteer;
using Puppeteer.EventSourcing.Follower;

namespace Tetris.Recognizing;

/// <summary>
/// One row the recognizer pushed: a recognized routine (or, for the act
/// enumeration, a single recorded act). <see cref="ClosingEntryId"/> is the
/// journal entry the match FIRED on — for a two-seek routine that is the closing
/// entry, never the opening one (see the note in <see cref="Recognizer"/>).
/// </summary>
public sealed record Recognition(string ReactionName, long ClosingEntryId, string Detail);

/// <summary>
/// The <see cref="IOutputSink"/> the reactions push to. `.Program.Emit` is the
/// read-only plane and its body must `print`, so every row arrives here as a
/// rendered document plus the `$`-captures that produced it; the sink only
/// accumulates, in arrival order, and the host reads it after
/// <c>Reactions.Execute()</c> returns.
/// </summary>
public sealed class RecognitionSink : IOutputSink
{
    private readonly List<Recognition> rows = [];

    public IReadOnlyList<Recognition> Rows => rows;

    public void Push(in PushDocument document)
    {
        var detail = new StringBuilder();
        foreach (var (key, value) in document.Bindings.OrderBy(b => b.Key, StringComparer.Ordinal))
        {
            if (detail.Length > 0)
            {
                detail.Append(' ');
            }

            detail.Append(key).Append('=').Append(value);
        }

        rows.Add(new Recognition(document.ReactionName, document.EntryId, detail.ToString()));
    }
}

/// <summary>
/// The reactions this lab defines over an EXISTING journal. Nothing here is part
/// of the domain and nothing here is part of any staging: a reaction is declared
/// against a journal from the outside, which is the whole point of the lab.
/// <para>
/// THE ROUTINE. Paper 3's definition, restated in Paper 9 §6: a routine is a
/// pattern over journal entries together with the correlation that binds them
/// into one trajectory — an opening entry, a closing one, and what is matched
/// between them. The routine recognized here is <em>the placement of one
/// piece</em>: it opens on <c>well.Spawn(@type)</c> and closes on
/// <c>well.Drop()</c>, the act that ends the piece's descent by landing it.
/// </para>
/// <para>
/// THE CORRELATION, AND WHAT IS MISSING FROM IT. Every worked multi-seek
/// reaction in the training lab correlates by a shared <c>$var</c> — the handle
/// the domain journals at both ends of the trajectory (a saleId, an orderId).
/// This domain journals none: <c>well.Spawn('T')</c> names a piece TYPE, not a
/// piece, and <c>well.Drop()</c> names nothing at all. There is no field the two
/// entries share, so there is no handle to reuse and the trajectory is bound by
/// ORDER alone — <c>.Seek</c> strictly before <c>.ThenSeek</c>, which the matcher
/// enforces for every solution. That is sound exactly while opens and closes
/// alternate; see the note for what it costs when they do not.
/// </para>
/// <para>
/// THE QUANTIFIERS. Both seeks are <c>.One()</c>. Quantifiers are mandatory on
/// every seek of a multi-seek reaction (<c>ValidateQuantifiersPresent</c>,
/// Reaction.cs:772, throws at <c>Execute()</c> otherwise), and this is the
/// one-open/one-close shape: one spawn opens a trajectory, the first following
/// drop closes it and prunes the anchor — O(N). <c>.Many()</c> at the opening
/// seek would collapse the anchors, and at the close would degenerate into
/// O(N²).
/// </para>
/// </summary>
public static class Recognizer
{
    /// <summary>
    /// A per-run suffix on the reaction NAMES only — never on a pattern, a
    /// quantifier or a body. <c>Reactions.Execute()</c> advances each reaction's
    /// checkpoint, so a second sweep under the same name delivers only entries
    /// appended since the first and a re-read of a finished journal reports
    /// nothing (reactions.md §Rebuild caveats). A fresh name per run means every
    /// run is a full-history sweep from checkpoint 0, which is what a lab that
    /// compares two records needs.
    /// </summary>
    public static string FreshTag() =>
        "_" + DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The six acts the well records. Everything the journal holds is one of these.</summary>
    private static readonly (string Verb, string Pattern, string Print)[] Acts =
    [
        ("Spawn",     "[_:Well].Spawn($type)", "print @type 'piece';"),
        ("MoveLeft",  "[_:Well].MoveLeft()",   "print 'MoveLeft' 'act';"),
        ("MoveRight", "[_:Well].MoveRight()",  "print 'MoveRight' 'act';"),
        ("Rotate",    "[_:Well].Rotate()",     "print 'Rotate' 'act';"),
        ("Tick",      "[_:Well].Tick()",       "print 'Tick' 'act';"),
        ("Drop",      "[_:Well].Drop()",       "print 'Drop' 'act';"),
    ];

    /// <summary>
    /// The lab's subject: the placement of one piece, spawn through landing.
    /// Two seeks, both quantified, correlated by order because the record offers
    /// nothing else to correlate on.
    /// </summary>
    public static void DefinePlacement(Reactions reactions, string tag)
    {
        reactions.DefineReaction("Placement" + tag)
            .Job().Company().WithSharedHydration()
            .Seek("Spawn").One()
                .OnMatch("[_:Well].Spawn($type)")
            .ThenSeek("Land").One()
                .OnMatch("[_:Well].Drop()")
            .Program.Emit("print @type 'piece';");
    }

    /// <summary>
    /// Six single-seek (per-event) reactions that enumerate the record act by
    /// act — the ground truth the placement count is checked against. A
    /// single-seek reaction is exempt from the quantifier rule (there is no
    /// trajectory to size), which is why these carry none.
    /// </summary>
    public static void DefineActs(Reactions reactions, string tag)
    {
        foreach (var (verb, pattern, print) in Acts)
        {
            reactions.DefineReaction("Act_" + verb + tag)
                .Job().Company().WithSharedHydration()
                .Seek(verb + "Seek")
                    .OnMatch(pattern)
                .Program.Emit(print);
        }
    }

    /// <summary>
    /// The order-only control. Same routine, but closed by the NEXT spawn rather
    /// than by the drop: this is the only way to close a placement that ended
    /// under gravity, because the well records no landing act of its own. It
    /// cannot close the last placement (nothing follows it), and it recognizes
    /// "the host asked for another piece" rather than "this piece landed".
    /// </summary>
    public static void DefinePlacementBySpawnToSpawn(Reactions reactions, string tag)
    {
        reactions.DefineReaction("PlacementSpawnToSpawn" + tag)
            .Job().Company().WithSharedHydration()
            .Seek("Open").One()
                .OnMatch("[_:Well].Spawn($type)")
            .ThenSeek("NextOpen").One()
                .OnMatch("[_:Well].Spawn(_)")
            .Program.Emit("print @type 'piece';");
    }
}
