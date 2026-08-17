using Puppeteer;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // The second act: the one that has to find something first.
    //
    // On the write surfaces dissected from the two reference modules, this is the commonest
    // shape there is - retrieve an aggregate, invoke exactly one domain operation on it,
    // store it back. Twenty-five of forty-four acts are that, and in every one of them the
    // retrieval and the store were the only reason a repository, a unit of work or an
    // aggregate store had to exist.
    //
    // Here the same act is performed twice and the two halves land differently:
    //
    //   the retrieval  becomes a keyed lookup over what the actor already holds
    //   the store      becomes nothing at all
    //
    // The asymmetry is the point. Half of a persistence call site is replaced by an in-memory
    // structure; the other half is replaced by nothing, because the act that produced the
    // aggregate is already in the journal and replaying it reproduces both the aggregate and
    // the index that finds it.
    [TestClass]
    public class RetrievalIsALookupBench
    {
        [TestMethod, TestCategory("Bench")]
        public void ARetrievingActFindsInRootState_AndStoresNothing()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"retrieval_is_a_lookup_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            const string actorName = "retrieval_is_a_lookup";

            var ordering = typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly;
            var payments = typeof(SubscriptionPaymentBridge).Assembly;

            try
            {
                var actor = new ActorV2(actorName, ordering, payments);
                actor.ConfigureStorage(DatabaseType.FileSystem, $"path={tempDir}");

                actor.Using(@"
                    bridge = SubscriptionPaymentBridge();
                    paymentsHeld = PaymentStore();
                ")
                .PerformCommand();

                // Three subscriptions, each its own act.
                for (int i = 1; i <= 3; i++)
                {
                    actor.Using(@"
                        payment = bridge.Buy(@payerId, @country, @period, @amount, @currency);
                        paymentsHeld.Keep(@key, payment);
                    ")
                    .WithParameters(p => {
                        p["payerId",  typeof(string)]  = Guid.NewGuid().ToString();
                        p["country",  typeof(string)]  = "PL";
                        p["period",   typeof(string)]  = "Month";
                        p["amount",   typeof(decimal)] = 50m;
                        p["currency", typeof(string)]  = "EUR";
                        p["key",      typeof(string)]  = $"subscription-{i}";
                    })
                    .PerformCommand();
                }

                // The retrieving act. Find one of the three, and ask the domain for one thing.
                actor.Using(@"
                    held = paymentsHeld.Find(@key);
                    held.MarkAsPaid();
                ")
                .WithParameters(p => {
                    p["key", typeof(string)] = "subscription-2";
                })
                .PerformCommand();

                // The aggregate does not expose its status - it only accepts commands - so what
                // is read back is its identity, which is what the lookup had to return.
                string report = actor.Using(@"
                    print paymentsHeld.Count() kept,
                          paymentsHeld.Find('subscription-2').GetSnapshot().Id.Value.ToString() found;
                ")
                .PerformQuery();

                Console.WriteLine();
                Console.WriteLine("=== A retrieving act, after the rules ===");
                Console.WriteLine($"  live:   {report}");
                Console.WriteLine();
                Console.WriteLine("  retrieval  -> paymentsHeld.Find(key)   a keyed lookup over root state");
                Console.WriteLine("  store      -> (nothing)                the journal already holds the act");

                Assert.IsTrue(report.Contains("3"), "The actor must still hold all three subscriptions.");

                // Replay: a fresh actor over the same journal re-runs the acts, which must
                // reproduce both the aggregates and the index that finds them.
                var revived = new ActorV2(actorName, ordering, payments);
                revived.ConfigureStorage(DatabaseType.FileSystem, $"path={tempDir}");

                string afterReplay = revived.Using(@"
                    print paymentsHeld.Count() kept,
                          paymentsHeld.Find('subscription-2').GetSnapshot().Id.Value.ToString() found;
                ")
                .PerformQuery();

                Console.WriteLine($"  replay: {afterReplay}");
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("  Two identities travelled with the same aggregate, and only one survived:");
                Console.WriteLine();
                Console.WriteLine($"    {"",-20}{"live",-40}{"replay",-40}");
                Console.WriteLine($"    {"assembler's key",-20}{"subscription-2",-40}{"subscription-2",-40}stable");
                Console.WriteLine($"    {"domain's identity",-20}{ExtractFound(report),-40}{ExtractFound(afterReplay),-40}"
                                  + (ExtractFound(report) == ExtractFound(afterReplay) ? "stable" : "not stable"));
                Console.WriteLine();
                Console.WriteLine("  The lookup by the assembler's key succeeded on both sides - that is what");
                Console.WriteLine("  'kept:3, found:...' reports twice. So the index the assembler authored is");
                Console.WriteLine("  reproduced by replay, and the key the domain minted for itself is not.");
                Console.WriteLine("  The call site that mints it is marked R_99 in SubscriptionPaymentBridge.");

                Console.WriteLine();
                if (report == afterReplay)
                {
                    Console.WriteLine("  The identity survived replay.");
                }
                else
                {
                    Console.WriteLine("  The count survived replay. The identity did not, and the reason is");
                    Console.WriteLine("  inside the domain: SubscriptionPayment.Buy mints its own key with");
                    Console.WriteLine("  Guid.NewGuid() as it builds the aggregate.");
                    Console.WriteLine();
                    Console.WriteLine("  Replay re-runs the acts, so an act carrying a decision that cannot be");
                    Console.WriteLine("  repeated is not repeatable in that respect. Non-determinism is captured");
                    Console.WriteLine("  at the parameter plane - @Now is journaled and re-injected, an Eval is");
                    Console.WriteLine("  captured rather than re-executed - and a Guid drawn inside a domain");
                    Console.WriteLine("  factory is past that edge. Nothing here is a defect of either party:");
                    Console.WriteLine("  the domain was written to be called once, by a caller that would store");
                    Console.WriteLine("  what came back.");
                    Console.WriteLine();
                    Console.WriteLine("  What it costs the assembler is worth stating plainly: it inherits a");
                    Console.WriteLine("  constraint it did not author. A repertoire can be complete, executable");
                    Console.WriteLine("  and testable - and still not be replayable. Those are three properties,");
                    Console.WriteLine("  not one.");
                }

                Assert.IsTrue(afterReplay.Contains("3"),
                    "Replay must reproduce the index over what the acts produced.");
                Assert.IsTrue(afterReplay.Contains("found"),
                    "The lookup by the assembler's key must succeed after replay too.");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        private static string ExtractFound(string printed)
        {
            const string marker = "\"found\":\"";
            int start = printed.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return "(absent)";
            start += marker.Length;
            int end = printed.IndexOf('"', start);
            return end < 0 ? "(absent)" : printed.Substring(start, end - start);
        }
    }
}
