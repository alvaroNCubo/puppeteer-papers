using Puppeteer;
using System.Text;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // The same composed act, twice, and what each arrangement's record is made of.
    //
    // The neighbour this lab exists for concedes almost everything an assembler could want.
    // An event-sourced process manager composes from outside, holds state, has a correlation
    // field that identifies its instance, has a lifecycle, and keeps its own event-sourced
    // stream. Any argument that separates the two arrangements by asking "does the composer
    // have identity?" is already lost, and it is better that it was lost before the paper was
    // written than after.
    //
    // ★★ GUARDRAIL, AND IT CORRECTS AN EARLIER READING OF THIS VERY COMPARISON.
    //
    // There are two structures here, not one, and they are orthogonal:
    //
    //     horizontal, across time      Act1 --then--> Act2 --then--> Act3
    //     vertical, within one act     Purchase = { op from A, op from B, op from C }
    //
    // Coordination is horizontal. Assembly is vertical. Confusing them makes a saga look like
    // an assembled verb that failed to be named, and it is not: a chain of steps may be the
    // perfectly legitimate temporal shape of an interaction. A SINGLE STEP of that chain can
    // contain the whole phenomenon - one step, five operations, three repertoires - and a saga
    // of twenty steps changes nothing about it.
    //
    //     An assembled verb is not a correlated sequence of steps. Correlation identifies which
    //     acts belong to the same process; assembly identifies which domain operations
    //     constitute one act. A single saga step may contain an assembled verb, and one saga
    //     may coordinate many assembled verbs.
    //
    //     Correlation groups acts across time. Assembly gives one act its parts.
    //
    // Which makes part of the comparison below UNFAIR AS ORIGINALLY FRAMED, and the unfairness
    // is recorded rather than quietly removed. Reproaching a process manager for leaving the
    // operations spread across streams, tied by a correlation value, is reproaching it for not
    // doing something that is not its work. Its correlation field answers "which events belong
    // to the same process instance?" - never "which operations constitute this act?". The two
    // questions are not competitors.
    //
    // ★ THIS LAB DOES NOT COMPETE. IT EXHIBITS.
    //
    // It is not here to win by counting, and the separator between the two arrangements is not
    // in any number below. The separator is in the pattern's own definition:
    //
    //     "Process managers issue commands, not mutations ... to trigger actions in other
    //      aggregates."
    //
    // A process manager acts on subjects that KEEP THE AUTHORSHIP OF THEIR OWN ACTS. That
    // relation stays coordinative however much identity, state, lifecycle and event-sourced
    // history the coordinator is granted - and it should be granted all of it, without
    // haggling, because granting it removes the weaker explanations and leaves a smaller and
    // more interesting difference:
    //
    //     Coordination relates acts. Assembly constitutes an act.
    //
    // AND THE GROUND FOR THAT, which is not a preference between two words. A process manager
    // requires the parts' acts to ALREADY HAVE IDENTITY in order to coordinate them at all:
    // its inputs are events - acts already constituted and already said by their own subjects -
    // and its outputs are commands asking for more of the same. Its raw material is acts.
    //
    // Assembly moves the other way. It takes operations that have no joint identity and gives
    // them one:  Purchase := { ... }.  Relating and constituting are two different relations,
    // and the second cannot be reduced to the first.
    //
    //     the signature is structural; the distinction is semantic.
    //
    // WHAT THE LAB DOES COUNT, and legitimately. The dissection of two reference modules found
    // a recurring signature wherever a composed act is realised without being said:
    //
    //     1. the act runs
    //     2. its operations are spread across several subjects
    //     3. no single record CONTAINS THE OPERATIONS THAT CONSTITUTE IT
    //     4. the parts are tied by something extrinsic - a correlation value, an event chain,
    //        a status machine on one participant
    //     5. the name of the whole, where it exists at all, sits on a coordinator or a handler,
    //        never on a record of the act
    //
    // Item 3 is worded that way on purpose, and an earlier wording of it was wrong. Saying "no
    // single record holds it as one unit" is defeasible in one line: a designer can append a
    // PurchaseCompleted entry to the coordinator's own stream, and the sentence breaks - while
    // nothing about the arrangement has changed. That entry ASSERTS A CONCLUSION ABOUT THE
    // COORDINATION; it does not contain what the act was made of. The operations are still
    // raised by other subjects into other streams, because that is what issuing commands means.
    //
    // So worded as containment rather than as summary, the item is guaranteed by the pattern's
    // definition instead of resting on what a particular designer chose to emit.
    //
    // The arrangement below exhibits all five. That is the point of running it: the signature
    // is not a property of poorly separated systems, nor of distribution, nor of missing
    // identity. It survives INTO THE CASE WHERE THE COORDINATOR WAS GRANTED EVERYTHING. This
    // is the fourth partition the signature has been observed in - after a method that composes
    // three domains, a conversation between services, and a chain of scheduled commands in one
    // process - and the first where the composer is a subject in its own right.
    //
    // Which is why the numbers can be honest here without being a verdict. They DISPLAY a
    // characteristic that the pattern's definition GUARANTEES; they do not establish it. A
    // competent coordinator could emit semantic entries of its own choosing and move every
    // number below - and the characteristic would be untouched, because its operations would
    // still be raised by other subjects into other streams.
    //
    //     the numbers are contingent; the signature is not.
    //
    // The one arrangement where the signature is absent is the other column, and that half is
    // measured for real elsewhere: in AssembledVerbOverTwoDomainsLab, where operations drawn
    // from two domains that share no type become the content of one entry, and in
    // RetrievalIsALookupLab, where replaying that entry re-performs them.
    //
    // Read the harness itself with the distrust list in ProcessManagerHarness.cs beside it.
    [TestClass]
    public class WhatEachRecordIsMadeOfLab
    {
        [TestMethod, TestCategory("Lab")]
        public void OneActTwoArrangements_TheRecordsAreMadeOfDifferentThings()
        {
            Console.WriteLine();
            Console.WriteLine("================ arrangement 1: event-sourced process manager ================");

            var store = new EventStore();
            var pm = new PurchaseProcessManager(store, new PaymentAggregate(store), new OrderAggregate(store));
            pm.OnPurchaseRequested(
                correlation: "purchase-1",
                payerId: "6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11",
                country: "PL", period: "Month", amount: 50m, currency: "EUR",
                userId: "user-1", userName: "Lab User", sku: "welcome-kit");

            Console.WriteLine();
            Console.Write(store.Render());

            var pmStream = store.Stream("purchase-process-purchase-1");
            var subjects = store.Streams.ToList();

            Console.WriteLine($"    subjects holding part of the act: {subjects.Count}  ({string.Join(", ", subjects)})");
            Console.WriteLine($"    entries in the coordinator's own stream: {pmStream.Count}");
            Console.WriteLine($"    of those, entries whose content is a domain operation: "
                              + pmStream.Count(e => e.Type != "ProcessManagerTransition"));

            Assert.IsTrue(subjects.Count >= 3,
                "The act is spread over the coordinator and the aggregates it commands.");
            Assert.AreEqual(0, pmStream.Count(e => e.Type != "ProcessManagerTransition"),
                "The coordinator's stream holds its own transitions, not the operations of the act.");

            Console.WriteLine();
            Console.WriteLine("    Replaying the coordinator's stream reconstitutes its coordination state:");
            Console.WriteLine("    which step it reached, what it is waiting for. It does not re-perform the");
            Console.WriteLine("    domain operations - those were commands to other subjects, and they live in");
            Console.WriteLine("    those subjects' streams, tied to this one by a correlation value.");

            Console.WriteLine();
            Console.WriteLine("================ arrangement 2: one subject, one act =========================");

            string tempDir = Path.Combine(Path.GetTempPath(), $"one_act_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            const string actorName = "one_act";
            try
            {
                var ordering = typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly;
                var payments = typeof(SubscriptionPaymentBridge).Assembly;

                var actor = new ActorV2(actorName, ordering, payments);
                actor.ConfigureStorage(DatabaseType.FileSystem, $"path={tempDir}");
                actor.Using("bridge = SubscriptionPaymentBridge();").PerformCommand();

                actor.Using(@"
                    payment = bridge.Buy(@payerId, @country, @period, @amount, @currency);
                    payment.MarkAsPaid();
                    address = Address(@street, @city, @state, @country, @zip);
                    welcomeKit = Order(@userId, @userName, address, 1, @card, @cvv, @holder, @Now.AddYears(1), null, null);
                    welcomeKit.AddOrderItem(@sku, @itemName, @itemPrice, 0m, '', 1);
                ")
                .WithParameters(p => {
                    p["payerId",   typeof(string)]  = "6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11";
                    p["country",   typeof(string)]  = "PL";
                    p["period",    typeof(string)]  = "Month";
                    p["amount",    typeof(decimal)] = 50m;
                    p["currency",  typeof(string)]  = "EUR";
                    p["street",    typeof(string)]  = "street";
                    p["city",      typeof(string)]  = "city";
                    p["state",     typeof(string)]  = "state";
                    p["zip",       typeof(string)]  = "12345";
                    p["userId",    typeof(string)]  = "user-1";
                    p["userName",  typeof(string)]  = "Lab User";
                    p["card",      typeof(string)]  = "1234-5678-9012-3456";
                    p["cvv",       typeof(string)]  = "123";
                    p["holder",    typeof(string)]  = "Card Holder";
                    p["sku",       typeof(int)]     = 41;
                    p["itemName",  typeof(string)]  = "welcome kit";
                    p["itemPrice", typeof(decimal)] = 0m;
                })
                .PerformCommand();

                var definitions = ReadActionDefinitions(Path.Combine(tempDir, actorName, "journal"));
                Assert.AreEqual(1, definitions.Count, "The act must be recorded as one definition.");

                Console.WriteLine();
                Console.WriteLine($"    subjects holding part of the act: 1  ({actorName})");
                Console.WriteLine($"    entries whose content IS the act: {definitions.Count}");
                Console.WriteLine();
                foreach (var line in definitions[0].Script.Replace(";", ";\n").Split('\n'))
                    if (line.Trim().Length > 0) Console.WriteLine($"        {line.Trim()}");

                Console.WriteLine();
                Console.WriteLine("    Replaying this subject's record re-performs the act, operation by");
                Console.WriteLine("    operation - which is how a domain-minted identity was caught changing");
                Console.WriteLine("    between the live run and the replay in the sibling lab.");

                Console.WriteLine();
                Console.WriteLine("================ what separates them =========================================");
                Console.WriteLine();
                Console.WriteLine($"                                    process manager      one subject");
                Console.WriteLine($"    subjects holding part of the act {subjects.Count,-21}1");
                Console.WriteLine($"    the composer's own entries       {pmStream.Count,-21}{definitions.Count}");
                Console.WriteLine($"    those entries are made of        its own field        the operations");
                Console.WriteLine($"                                     snapshots            themselves");
                Console.WriteLine($"    replaying that record gives      coordination state   the act");
                Console.WriteLine();
                Console.WriteLine("    Both arrangements perform the act, and NEITHER COLUMN IS A VERDICT.");
                Console.WriteLine();
                Console.WriteLine("    What the left column exhibits is the signature the dissection kept finding");
                Console.WriteLine("    wherever a composed act is realised without being said:");
                Console.WriteLine();
                Console.WriteLine("        the act runs                                            yes");
                Console.WriteLine($"        its operations are spread across subjects               {subjects.Count}");
                Console.WriteLine("        no single record contains its constituent operations     confirmed");
                Console.WriteLine("        the parts are tied by something extrinsic               correlation value");
                Console.WriteLine("        the name of the whole sits on the coordinator           purchase-process");
                Console.WriteLine();
                Console.WriteLine("    Fourth partition in which that signature has been observed - after a method");
                Console.WriteLine("    composing three domains, a conversation between services, and a chain of");
                Console.WriteLine("    scheduled commands in one process - and the FIRST in which the composer is");
                Console.WriteLine("    a subject in its own right, with identity, lifecycle and its own stream.");
                Console.WriteLine("    So the signature is not a symptom of poor separation, of distribution, or");
                Console.WriteLine("    of a missing subject.");
                Console.WriteLine();
                Console.WriteLine("    The numbers are contingent - a coordinator could emit semantic entries and");
                Console.WriteLine("    move every one of them. The signature is not: its operations would still be");
                Console.WriteLine("    raised by other subjects into other streams, because that is what the");
                Console.WriteLine("    pattern's own definition says a process manager does.");
                Console.WriteLine();
                Console.WriteLine("        Coordination relates acts.  Assembly constitutes an act.");
                Console.WriteLine("        the signature is structural;  the distinction is semantic.");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        private record ActionDef(int Id, string Script);

        private static List<ActionDef> ReadActionDefinitions(string journalDir)
        {
            var definitions = new List<ActionDef>();
            if (!Directory.Exists(journalDir)) return definitions;

            foreach (var file in Directory.GetFiles(journalDir, "journal_*.bin").OrderBy(f => f))
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 32) continue;
                fs.Seek(32, SeekOrigin.Begin);

                var lenBuf = new byte[4];
                while (fs.Position < fs.Length)
                {
                    if (fs.Read(lenBuf, 0, 4) < 4) break;
                    int recordLen = BitConverter.ToInt32(lenBuf, 0);
                    if (recordLen <= 4 || fs.Position + recordLen > fs.Length) break;

                    var body = new byte[recordLen];
                    if (fs.Read(body, 0, recordLen) < recordLen) break;
                    if ((body[0] & 0x3F) != 2) continue;

                    int actionId = BitConverter.ToInt32(body, 1 + 8 + 8);
                    int offset = 1 + 8 + 8 + 4;
                    int payloadLen = BitConverter.ToInt32(body, offset);
                    if (payloadLen <= 0 || offset + 4 + payloadLen > recordLen) continue;

                    definitions.Add(new ActionDef(
                        actionId, Encoding.UTF8.GetString(body, offset + 4, payloadLen)));
                }
            }
            return definitions;
        }
    }
}
