using CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments;
using Puppeteer;
using Puppeteer.EventSourcing.DB;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // A prediction, and its test.
    //
    // Every other lab here confirms something the arrangement was built to do. This one is of
    // a different kind: the calculus of Appendix C predicts a failure nobody had observed in
    // the engine, and this lab goes looking for it. The prediction comes from the hypothesis
    // Proposition 4 needs - a trace is JOURNAL-CLOSED when no recorded argument carries a name
    // minted during the trace - and from what happens when that hypothesis fails:
    //
    //     a value minted inside an operation is read by the assembler and passed into a
    //     LATER act as an argument. The live argument names a value the live state holds.
    //     Replay re-mints, so the replayed state holds a different value - but the recorded
    //     argument is the old literal, which names nothing in the replayed state. No
    //     renaming can both align the states and fix the literal.
    //
    // So replay equivalence should break at the CAPTURE of the mint, not at the mint itself -
    // which the criterion's fresh-value clause absorbs (§3.1). Two arms measure it, differing
    // in one thing: how a later act refers to the value an earlier act minted.
    //
    //     CAPTURED   the minted identity itself travels as the later act's argument
    //     SUPPLIED   a key the assembler chose travels instead, and the value is found by it
    //
    // The invariant tested is the one the composition establishes in each arm: the register's
    // key equals the identity of the payment kept under it. In the captured arm that invariant
    // is written with a value only the live run could know; in the supplied arm it is written
    // with a value the assembler supplied and the record therefore carries across.
    //
    // If the captured arm survives replay, the calculus is wrong about the engine and this
    // lab reports that instead.
    [TestClass]
    public class WhereReplayEquivalenceBreaksLab
    {
        private const string PayerId = "6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11";
        private const string SuppliedKey = "purchase-1";

        [TestMethod, TestCategory("Lab")]
        public void ACapturedMintBreaksReplayEquivalenceAndASuppliedKeyDoesNot()
        {
            string root = Path.Combine(Path.GetTempPath(), "capture_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var captured = RunArm(root, "captured", capture: true);
                var supplied = RunArm(root, "supplied", capture: false);

                Console.WriteLine();
                Console.WriteLine("=== where replay equivalence breaks ===");
                Console.WriteLine();
                Console.WriteLine("    CAPTURED arm - the minted identity travels as a later act's argument");
                Console.WriteLine($"        identity minted, live                  : {Short(captured.MintedLive)}");
                Console.WriteLine($"        identity minted, replay                : {Short(captured.MintedReplayed)}   (fresh, as the axiom licenses)");
                Console.WriteLine($"        the later act's journaled argument     : {Short(captured.MintedLive)}   (the live literal, re-injected)");
                Console.WriteLine($"        invariant live   (key == its payment's id) : {captured.InvariantLive}");
                Console.WriteLine($"        invariant replay (key == its payment's id) : {captured.InvariantReplayed}");
                Console.WriteLine();
                Console.WriteLine("    SUPPLIED arm - a key the assembler chose travels instead");
                Console.WriteLine($"        identity minted, live                  : {Short(supplied.MintedLive)}");
                Console.WriteLine($"        identity minted, replay                : {Short(supplied.MintedReplayed)}   (fresh too)");
                Console.WriteLine($"        the later act's journaled argument     : '{SuppliedKey}'   (crossed the plane)");
                Console.WriteLine($"        invariant live   (found under its key) : {supplied.InvariantLive}");
                Console.WriteLine($"        invariant replay (found under its key) : {supplied.InvariantReplayed}");
                Console.WriteLine();
                Console.WriteLine("    The mint is fresh in both arms and neither is a failure of itself:");
                Console.WriteLine("    what separates the arms is whether a name minted during the trace was");
                Console.WriteLine("    recorded as an argument. Where it was, no renaming can align the two runs.");
                Console.WriteLine();

                Assert.AreNotEqual(captured.MintedLive, captured.MintedReplayed,
                    "The mint is fresh on replay - licensed by the host axiom, and absorbed by the "
                    + "criterion's fresh-value clause. It is not itself the failure.");

                Assert.IsTrue(captured.InvariantLive,
                    "Live, the captured arm's composition holds: the register's key is the identity "
                    + "of the payment kept under it.");

                Assert.IsFalse(captured.InvariantReplayed,
                    "And replay breaks it, exactly where Proposition 4's hypothesis fails: the "
                    + "recorded argument is the live run's minted name, which names nothing in the "
                    + "replayed state. The calculus predicted this before the engine was asked.");

                Assert.IsTrue(supplied.InvariantLive && supplied.InvariantReplayed,
                    "The supplied-key arm mints just as freshly and survives: its later act's "
                    + "argument crossed the plane, so the trace is journal-closed and Proposition 4 "
                    + "applies - a sufficient condition, read off the calculus and confirmed here.");
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }

        private sealed record ArmResult(string MintedLive, string MintedReplayed, bool InvariantLive, bool InvariantReplayed);

        private static ArmResult RunArm(string root, string name, bool capture)
        {
            string dir = Path.Combine(root, name);
            Directory.CreateDirectory(dir);

            var actor = NewActor(name, dir);
            actor.Using("payments = SubscriptionPaymentBridge(); held = PaymentStore();").PerformCommand();

            // act one: the payment is bought, and its identity is minted inside the factory
            actor.Using(@"
                    payment = payments.Buy(@payerId, @country, @period, @amount, @currency);
                    payment.MarkAsPaid();
                ")
                .WithParameters(p =>
                {
                    p["payerId", typeof(string)] = PayerId;
                    p["country", typeof(string)] = "PL";
                    p["period", typeof(string)] = "Month";
                    p["amount", typeof(decimal)] = 50m;
                    p["currency", typeof(string)] = "EUR";
                })
                .PerformCommand();

            string mintedLive = ReadIdentity(actor);

            // act two: how does it refer to what act one minted?
            string laterArgument = capture ? mintedLive : SuppliedKey;
            actor.Using("held.Keep(@key, payment);")
                .WithParameters(p => { p["key", typeof(string)] = laterArgument; })
                .PerformCommand();

            bool invariantLive = KeyMatchesKeptIdentity(actor, laterArgument);

            // a fresh subject over the same journal
            var rehydrated = NewActor(name, dir);
            string mintedReplayed = ReadIdentity(rehydrated);
            bool invariantReplayed = KeyMatchesKeptIdentity(rehydrated, laterArgument);

            return new ArmResult(mintedLive, mintedReplayed, invariantLive, invariantReplayed);
        }

        // The register holds the payment under some key. The composition's invariant, in both
        // arms, is that the payment found under that key is the one act one produced - and in
        // the captured arm the key IS that payment's minted identity, so the invariant is
        // checkable against the value itself.
        private static bool KeyMatchesKeptIdentity(ActorV2 actor, string key)
        {
            string result = actor.Using("print held.Find(@key).GetSnapshot().Id.Value.ToString() 'id';")
                .WithParameters(p => { p["key", typeof(string)] = key; })
                .PerformQuery();

            string keptIdentity = Between(result, "\"id\":\"", "\"");
            return key == SuppliedKey
                ? keptIdentity == ReadIdentity(actor)      // supplied: the key finds the payment act one produced
                : keptIdentity == key;                     // captured: the key is that payment's own identity
        }

        private static string ReadIdentity(ActorV2 actor)
        {
            string result = actor.Using("print payment.GetSnapshot().Id.Value.ToString() 'id';").PerformQuery();
            return Between(result, "\"id\":\"", "\"");
        }

        private static string Between(string text, string opening, string closing)
        {
            int at = text.IndexOf(opening, StringComparison.Ordinal);
            if (at < 0)
            {
                return string.Empty;
            }
            at += opening.Length;
            int end = text.IndexOf(closing, at, StringComparison.Ordinal);
            return end < 0 ? string.Empty : text.Substring(at, end - at);
        }

        private static string Short(string identity)
            => identity.Length >= 8 ? identity.Substring(0, 8) + "..." : identity;

        private static ActorV2 NewActor(string actorName, string dir)
        {
            var actor = new ActorV2(actorName,
                typeof(SubscriptionPayment).Assembly,
                typeof(SubscriptionPaymentBridge).Assembly);
            actor.ConfigureStorage(DatabaseType.FileSystem, $"path={dir}");
            return actor;
        }
    }
}
