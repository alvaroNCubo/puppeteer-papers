using Puppeteer;
using Puppeteer.EventSourcing.DB;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // Who decides which performances count as history?
    //
    // A domain operation that reads and computes — no state change anywhere — looks like it has
    // its modality built in: it is "a query". This lab measures that the modality is not the
    // operation's, and not the verb's either. It is attributed by the assembler, per
    // performance, and the record follows the attribution rather than the operation's effect.
    //
    // The granularity is the measurement, so the lab holds everything else fixed: ONE body -
    // one verb, under the handler's reuse seam - performed three times:
    //
    //     as a QUERY      the subject computes and speaks; the journal does not move
    //     as a COMMAND    the same computation - and the act is journaled, replayable
    //                     testimony that it happened
    //     as a QUERY      again, after the command: the head stays where the command
    //                     left it - the verb outlived both attributions unchanged
    //
    // Had modality been the verb's, the second and third performances could not have differed:
    // same body, same verb, different modalities is exactly what per-verb attribution forbids
    // and per-performance attribution predicts.
    //
    // Nothing in the domain distinguishes the two runs. What distinguishes them is a decision
    // that belongs to the party constituting the verb: whether this performance is part of the
    // subject's history. Disclosing a value and looking at a value can be the same computation
    // and different acts.
    //
    // Stated at the level the series has met before: the author of an operation is not thereby
    // the authority on the historicity of the acts that will incorporate it - the same geometry
    // as the output finding, where the producer of a value is not the authority on its
    // destination. When persistence lives inside a domain, its author necessarily takes both
    // decisions at once - what the domain means, and what survives as history. Neither decision
    // is wrong there; this lab only shows they are separable, and where the second one can live.
    //
    // No timing is measured, and no claim is made that either attribution is the right one for
    // any particular system.
    [TestClass]
    public class WhoDecidesWhatCountsAsHistoryLab
    {
        [TestMethod, TestCategory("Lab")]
        public void TheSameReadOnlyOperationLeavesHistoryOnlyWhenTheAssemblerSaysSo()
        {
            string dir = Path.Combine(Path.GetTempPath(), "historicity_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string actorName = "historicity_" + Guid.NewGuid().ToString("N");

            try
            {
                var actor = NewActor(actorName, dir);
                var hook = new StageHook(actor);

                // the world: one order with one item, so there is something to read
                actor.Using(@"
                        address = Address(@street, @city, @state, @country, @zip);
                        o = Order(@userId, @userName, address, 1, @card, @cvv, @holder, @Now.AddYears(1), null, null);
                        o.AddOrderItem(@sku, @itemName, @unitPrice, 0m, '', @units);
                    ")
                    .WithParameters(p => {
                        p["street", typeof(string)] = "street";
                        p["city", typeof(string)] = "city";
                        p["state", typeof(string)] = "state";
                        p["country", typeof(string)] = "country";
                        p["zip", typeof(string)] = "12345";
                        p["userId", typeof(string)] = "user-1";
                        p["userName", typeof(string)] = "Lab User";
                        p["card", typeof(string)] = "1234-5678-9012-3456";
                        p["cvv", typeof(string)] = "123";
                        p["holder", typeof(string)] = "Card Holder";
                        p["sku", typeof(int)] = 41;
                        p["itemName", typeof(string)] = "an item";
                        p["unitPrice", typeof(decimal)] = 25m;
                        p["units", typeof(int)] = 2;
                    })
                    .PerformCommand();

                long afterSetup = hook.CurrentEntryId;

                // ── one body, exercised as a QUERY ──────────────────────────────────────────
                string asQuery = actor.Using("o.GetTotal(); print o.GetTotal() 'total';").PerformQuery();
                long afterQuery = hook.CurrentEntryId;

                // ── the same body, performed as a COMMAND ───────────────────────────────────
                actor.Using("o.GetTotal(); print o.GetTotal() 'total';").PerformCommand();
                long afterCommand = hook.CurrentEntryId;

                // ── and queried again: the verb outlives the attribution choice ─────────────
                string queriedAgain = actor.Using("o.GetTotal(); print o.GetTotal() 'total';").PerformQuery();
                long afterSecondQuery = hook.CurrentEntryId;

                // a fresh subject over the same journal: the testimony replays, the query is gone
                var rehydrated = NewActor(actorName, dir);
                long afterReplay = new StageHook(rehydrated).CurrentEntryId;
                string totalAfterReplay = rehydrated.Using("o.GetTotal(); print o.GetTotal() 'total';").PerformQuery();

                Console.WriteLine();
                Console.WriteLine("=== one read-only operation, two attributions ===");
                Console.WriteLine();
                Console.WriteLine($"    journal head after setup                    : {afterSetup}");
                Console.WriteLine($"    after the body exercised as a QUERY         : {afterQuery}   (unchanged)");
                Console.WriteLine($"    after the same body as a COMMAND            : {afterCommand}   (advanced)");
                Console.WriteLine($"    after the same body queried again           : {afterSecondQuery}   (unchanged)");
                Console.WriteLine($"    after replaying the journal from scratch    : {afterReplay}");
                Console.WriteLine();
                Console.WriteLine($"    the domain computed the same value each time: {asQuery.Trim()}");
                Console.WriteLine();
                Console.WriteLine("    Neither the operation nor the verb changed. What changed is a decision the");
                Console.WriteLine("    assembler took per performance: whether this one counts as the subject's history.");
                Console.WriteLine();

                Assert.AreEqual(afterSetup, afterQuery,
                    "Exercised as a query, the performance leaves no history: the journal head "
                    + "does not move.");

                Assert.IsTrue(afterCommand > afterQuery,
                    "Performed as a command, the same body is journaled: the act is testimony "
                    + "that it happened, not a state change of the domain.");

                Assert.AreEqual(afterCommand, afterSecondQuery,
                    "Queried again after the command, the head stays: same body, same verb, "
                    + "different modalities - which per-verb attribution forbids and "
                    + "per-performance attribution predicts.");

                Assert.IsTrue(asQuery.Contains("50") && queriedAgain.Contains("50"),
                    "Both query performances computed and spoke the same value.");

                Assert.AreEqual(afterCommand, afterReplay,
                    "And the testimony is replayable: a fresh subject reaches the same head from "
                    + "the record alone. The query's performance is not there to replay - which is "
                    + "not a loss; it is what the assembler decided a query is.");

                Assert.IsTrue(totalAfterReplay.Contains("50"),
                    "The domain computes the same total after replay: nothing about the domain "
                    + "depended on which attribution the assembler chose.");
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        private static ActorV2 NewActor(string actorName, string dir)
        {
            var actor = new ActorV2(actorName,
                typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly);
            actor.ConfigureStorage(DatabaseType.FileSystem, $"path={dir}");
            return actor;
        }
    }
}
