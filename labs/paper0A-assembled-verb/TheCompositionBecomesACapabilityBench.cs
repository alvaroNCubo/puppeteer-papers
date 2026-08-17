using System.Text;
using Puppeteer;
using Puppeteer.EventSourcing.DB;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // Does assembly PRODUCE a capability, or merely record a use?
    //
    // Everything measured so far shows one act surviving into the record as one unit. That is
    // compatible with a weaker reading: the record holds a receipt of something that happened
    // once. A capability is more than a receipt. The test of a capability is REUSE: it can be
    // drawn on again without being restated.
    //
    //     an act        happens once, and the record keeps it
    //     a capability  is available afterwards - the next use INVOKES it, it does not
    //                   re-constitute it
    //
    // So the measurement: perform the same composed verb TWICE, with different arguments. If
    // assembly merely records uses, the journal holds two self-contained compositions. If
    // assembly produced a capability, the journal holds ONE definition and TWO invocations of
    // it - the second use draws on the verb, and states nothing about its constitution.
    //
    //     before      A  B  C            (the repertoires' operations)
    //     assembly    V := A; B; C
    //     after       A  B  C  +  V      (and V can be drawn on again)
    //
    // THE STATUS OF THE MEASUREMENT, stated exactly: "capability" is operationalized by the
    // reuse test, and this arrangement implements that operationalization - so the result is
    // not the confirmation of a hypothesis. It is a REALIZABILITY DEMONSTRATION (a record can
    // hold the definition/exercise separation as data) plus a CONFORMANCE CHECK that could
    // have failed: had the second use re-emitted a second definition, the handler's
    // known-action seam would have been broken and this bench would have counted two.
    //
    // The distinction matters because it is exactly what separates V from an inline sequence.
    // A(); B(); C(); can of course be executed twice - by writing it twice. What it cannot do
    // is exist between the two executions as a thing the second one refers to.
    //
    // And the measurement licenses a deduction stronger than the count. V is invoked with
    // values that did not exist when V was defined; two distinct invocations refer to the same
    // V; replay preserves both the definition and the distinction between its exercises. So V
    // cannot be identified with any of its executions - they differ and it does not - nor with
    // its constituents executing, which are its constitution and not V itself. Three levels,
    // none reducible to the others:
    //
    //     A, B, C                constituent capabilities
    //          |  assembly
    //          v
    //          V                 derived capability - defined once
    //         / \
    //     V(p1)   V(p2)          acts - exercises of it
    //
    // DEFINING A CAPABILITY IS NOT EXERCISING IT, and this journal holds that separation as
    // data: one definition, two exercises, replay reconstructing both from the one.
    [TestClass]
    public class TheCompositionBecomesACapabilityBench
    {
        [TestMethod, TestCategory("Bench")]
        public void TheSecondUseInvokesTheVerbWithoutRestatingItsConstitution()
        {
            string dir = Path.Combine(Path.GetTempPath(), "capability_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string actorName = "capability_" + Guid.NewGuid().ToString("N");

            try
            {
                var actor = NewActor(actorName, dir);
                actor.Using("payments = SubscriptionPaymentBridge(); held = PaymentStore();").PerformCommand();

                // the same composed verb, drawn on twice with different arguments
                Purchase(actor, payer: "6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11", amount: 50m,
                         item: "welcome kit", key: "purchase-1");
                Purchase(actor, payer: "2b8d4e6f-1a3c-4d5e-9f0b-7c2a4e6d8b33", amount: 30m,
                         item: "starter kit", key: "purchase-2");

                string live = actor.Using("print held.Count() 'kept';").PerformQuery();

                var journal = ReadJournalText(Path.Combine(dir, actorName, "journal"));
                int definitions = CountOccurrences(journal.Composed, "define action ");

                // a fresh actor over the same journal: both uses replay from the record
                var rehydrated = NewActor(actorName, dir);
                string replayed = rehydrated.Using("print held.Count() 'kept';").PerformQuery();

                Console.WriteLine();
                Console.WriteLine("=== one verb, two uses ===");
                Console.WriteLine();
                Console.WriteLine($"    times the composed verb was performed : 2   (different payers, amounts, items)");
                Console.WriteLine($"    definitions of it in the journal      : {definitions}");
                Console.WriteLine($"    state after the live run              : {live.Trim()}");
                Console.WriteLine($"    state after replaying the journal     : {replayed.Trim()}");
                Console.WriteLine();
                Console.WriteLine("    The second use did not restate the composition. It invoked it - what");
                Console.WriteLine("    travelled was the verb's identity and the new arguments, which is the");
                Console.WriteLine("    signature of drawing on a capability rather than performing a copy.");
                Console.WriteLine();

                Assert.AreEqual(1, definitions,
                    "Two uses, one definition. If assembly merely recorded uses there would be two "
                    + "self-contained compositions; a capability is constituted once and drawn on.");

                Assert.IsTrue(live.Contains("\"kept\":2"),
                    "Both uses performed the composition: two payments held under two keys.");

                Assert.IsTrue(replayed.Contains("\"kept\":2"),
                    "And replay re-performs both invocations from the one recorded definition.");
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        private static ActorV2 NewActor(string actorName, string dir)
        {
            var actor = new ActorV2(actorName,
                typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly,
                typeof(CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments.SubscriptionPayment).Assembly,
                typeof(SubscriptionPaymentBridge).Assembly);
            actor.ConfigureStorage(DatabaseType.FileSystem, $"path={dir}");
            return actor;
        }

        private static void Purchase(ActorV2 actor, string payer, decimal amount, string item, string key)
        {
            actor.Using(@"
                    payment = payments.Buy(@payerId, @country, @period, @amount, @currency);
                    payment.MarkAsPaid();
                    address = Address(@street, @city, @state, @country, @zip);
                    welcomeKit = Order(@userId, @userName, address, 1, @card, @cvv, @holder, @Now.AddYears(1), null, null);
                    welcomeKit.AddOrderItem(@sku, @itemName, @itemPrice, 0m, '', 1);
                    held.Keep(@key, payment);
                ")
                .WithParameters(p => {
                    p["payerId", typeof(string)] = payer;
                    p["country", typeof(string)] = "PL";
                    p["period", typeof(string)] = "Month";
                    p["amount", typeof(decimal)] = amount;
                    p["currency", typeof(string)] = "EUR";
                    p["street", typeof(string)] = "street";
                    p["city", typeof(string)] = "city";
                    p["state", typeof(string)] = "state";
                    p["zip", typeof(string)] = "12345";
                    p["userId", typeof(string)] = payer;
                    p["userName", typeof(string)] = "Bench User";
                    p["card", typeof(string)] = "1234-5678-9012-3456";
                    p["cvv", typeof(string)] = "123";
                    p["holder", typeof(string)] = "Card Holder";
                    p["sku", typeof(int)] = 41;
                    p["itemName", typeof(string)] = item;
                    p["itemPrice", typeof(decimal)] = 0m;
                    p["key", typeof(string)] = key;
                })
                .PerformCommand();
        }

        private sealed record JournalText(string Composed);

        private static JournalText ReadJournalText(string journalDir)
        {
            var sb = new StringBuilder();
            if (Directory.Exists(journalDir))
            {
                foreach (var file in Directory.GetFiles(journalDir, "*.bin").OrderBy(f => f))
                {
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read,
                                                  FileShare.ReadWrite | FileShare.Delete);
                    using var ms = new MemoryStream();
                    fs.CopyTo(ms);
                    sb.Append(Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
            return new JournalText(sb.ToString());
        }

        private static int CountOccurrences(string text, string needle)
        {
            int count = 0, at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }
            return count;
        }
    }
}
