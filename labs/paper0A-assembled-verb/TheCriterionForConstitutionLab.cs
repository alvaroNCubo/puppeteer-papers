using System.Text;
using Puppeteer;
using Puppeteer.EventSourcing.DB;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // A criterion that separates constitution from correlation and from description.
    //
    // Everything before this lab establishes that one arrangement records a composition as one
    // entry. That on its own decides nothing, because two other arrangements can produce a record
    // that also refers to the whole, and one of them can even give it a business name:
    //
    //     COORDINATION   a process manager with its own identity, state and stream. It may emit
    //                    PurchaseCompleted. Nothing about its name, lifecycle or durability
    //                    distinguishes it - all of that is conceded.
    //     DESCRIPTION    a trace. Its root span is literally called CompletePurchase and it lists
        //                    lists every statement underneath. It records the grouping faithfully.
    //     CONSTITUTION   the entry of the substrate lab. It is called `action 1`.
    //
    // The naming runs the wrong way for the argument: the two arrangements this lab must
    // distinguish itself from are the ones with the better names.
    //
    // ══ THE QUESTION, WHICH ANY SYSTEM CAN BE ASKED ═══════════════════════════════════════════
    //
    // Take the record that claims to be the whole. Then ask two things of it and nothing else:
    //
    //     (a) CONTENT   how many of the statements that constitute the whole does it contain?
    //     (b) REPLAY    replaying it ALONE, does the whole's outcome exist again?
    //
    // Both questions are TOTAL, and ALONE is load-bearing. (b) quantifies over the
    // arrangement's own replay semantics - an arrangement that supplies no replay operation
    // answers NO, vacuously, because nothing exists that could bring the outcome about; not
    // "undefined", since undefined is not a value and a criterion that returns one is not
    // total. And ALONE constrains the replay's INPUT: with the engine and the repertoires
    // held fixed, a replay that also reads a definition kept outside the record - compiled
    // code, an ambient program - is not a replay of the record alone, and what it produces
    // is a function of the pair. An arrangement's own recovery may well bring its result
    // back; what (b) asks is whether the RECORD ALONE can.
    //
    // OUTCOME IDENTITY IS TAKEN UP TO FRESH VALUES: two outcomes are the same when they
    // differ at most by a consistent renaming of identifiers minted during the run rather
    // than supplied to it. The clause is principled - re-performance re-mints whatever was
    // minted, so a bit-identity criterion would crown the arrangements that never re-perform -
    // and it is not free for this arm: the sibling lab (RetrievalIsALookupLab) measures the
    // domain-minted identity that differs on every replay and FORCES the clause. Without it,
    // this criterion fails this suite's own constitution arm. The clause rewards nothing that
    // does not re-perform: it only relates outcomes that exist again.
    //
    // Neither question separates the arrangements by itself, and that is the finding rather
    // than a weakness of the instrument. (a) separates the event-sourced coordinator, whose
    // record holds its own transitions and not the operations. (b) separates the rest. Only
    // together do they classify.
    //
    // ══ WHAT THIS DOES NOT CLAIM ══════════════════════════════════════════════════════════════
    //
    // Not that a description could not be made rich enough to replay from. It could - and then it
    // would be a journal, and this criterion would classify it as constitution, which is the right
    // answer. The criterion does not ask what a record is called or which tool produced it. It
    // asks what replaying it does.
    //
    // Not that coordination is deficient. A process manager answers what follows this act; it is
    // not attempting what (a) measures, and scoring zero there is not a failure at its own task.
    //
    // And no timing is measured anywhere.
    [TestClass]
    public class TheCriterionForConstitutionLab
    {
        [TestMethod, TestCategory("Lab")]
        public void ContentAndReplayTogetherSeparateTheThreeArrangements()
        {
            var coordination = MeasureCoordination();
            var description = MeasureDescription();
            var constitution = MeasureConstitution();

            var arms = new[] { coordination, description, constitution };

            Console.WriteLine();
            Console.WriteLine("=== one question, three arrangements ===");
            Console.WriteLine();
            Console.WriteLine($"    {"arrangement",-16} {"names the whole",-16} {"(a) operations",-16} {"(b) replay yields"}");
            Console.WriteLine($"    {"",-16} {"",-16} {"in the record",-16} {"the whole again"}");
            Console.WriteLine();
            foreach (var a in arms)
                Console.WriteLine($"    {a.Name,-16} {a.NamesTheWhole,-16} {a.OperationsInRecord,-16} {a.ReplayYieldsTheWhole}");

            Console.WriteLine();
            Console.WriteLine("    (a) alone would not separate description from constitution:");
            Console.WriteLine("        both records hold every statement that was performed.");
            Console.WriteLine("    (b) alone would not separate coordination from description:");
            Console.WriteLine("        both answer no - one replays without re-performing, the");
            Console.WriteLine("        other has nothing that could replay.");
            Console.WriteLine();
            foreach (var a in arms)
                Console.WriteLine($"    {a.Name,-16} {a.Note}");
            Console.WriteLine();

            // The two arrangements this one must distinguish itself from are the ones with the
            // better names. If naming carried the property, this column would decide it.
            Assert.AreEqual("yes", coordination.NamesTheWhole);
            Assert.AreEqual("yes", description.NamesTheWhole);
            Assert.AreEqual("no", constitution.NamesTheWhole,
                "The constituted act is `action 1`. Naming does not separate the three.");

            // (a) content
            Assert.AreEqual(0, coordination.OperationsInRecord,
                "A coordinator's own stream holds its transitions. The operations belong to other "
                + "subjects and live in their streams, tied to this one by a correlation value.");
            Assert.AreEqual(6, description.OperationsInRecord,
                "A trace does mention every operation performed. Content alone cannot separate it.");
            Assert.AreEqual(6, constitution.OperationsInRecord,
                "The same six statements: five domain operations across two repertoires, and the "
                + "assembler's own register entry. Both records hold all of them.");

            // (b) replay
            Assert.AreEqual("no", coordination.ReplayYieldsTheWhole,
                "Replaying the coordinator's stream restores its coordination state - which step it "
                + "reached, what it awaits. It does not re-perform the operations.");
            Assert.AreEqual("no", description.ReplayYieldsTheWhole,
                "A description supplies no replay operation, so no replay of it exists after which "
                + "the outcome obtains. The answer is no - vacuously - not undefined: (b) is a "
                + "total question.");
            Assert.AreEqual("yes", constitution.ReplayYieldsTheWhole,
                "Replaying this record alone performs the operations again, and the outcome exists "
                + "afterwards UP TO FRESH VALUES: the register count and the assembler-keyed lookup "
                + "reproduce, while the domain-minted identity differs per replay - the measured "
                + "value that forces the equivalence clause (see RetrievalIsALookupLab).");

            // and the classification only exists because both were asked
            Assert.AreEqual(1, arms.Count(a => a.OperationsInRecord == 6 && a.ReplayYieldsTheWhole == "yes"),
                "Exactly one arrangement both contains the operations and re-performs them from the "
                + "record. That conjunction is the criterion; neither half is one on its own.");
        }

        private sealed class Arm
        {
            public string Name;
            public string NamesTheWhole;
            public int OperationsInRecord;
            public string ReplayYieldsTheWhole;
            public string Note;
        }

        // ── arrangement 1: coordination ──────────────────────────────────────────────────────
        private static Arm MeasureCoordination()
        {
            var store = new EventStore();
            var payments = new PaymentAggregate(store);
            var orders = new OrderAggregate(store);
            var pm = new PurchaseProcessManager(store, payments, orders);

            pm.OnPurchaseRequested("c-1", Guid.NewGuid().ToString(), "PL", "Month", 50m, "EUR",
                                   "user-1", "Lab User", "welcome kit");

            var own = store.Stream("purchase-process-c-1");

            // Replay the coordinator's stream ALONE, into an empty store. Its transitions restore
            // its own fields; nothing else is produced, because the operations were commands to
            // other subjects.
            var replayed = new EventStore();
            foreach (var e in own) replayed.Append(e.Stream, e.Type, e.Payload);
            int domainRecordsAfterReplay = replayed.All.Count(e => e.Type != "ProcessManagerTransition");

            return new Arm
            {
                Name = "coordination",
                NamesTheWhole = "yes",
                OperationsInRecord = own.Count(e => e.Type != "ProcessManagerTransition"),
                ReplayYieldsTheWhole = domainRecordsAfterReplay > 0 ? "yes" : "no",
                Note = $"its own stream holds {own.Count} transitions; the act's operations are "
                       + $"spread over {store.Streams.Count()} streams tied by a correlation value"
            };
        }

        // ── arrangement 2: description ───────────────────────────────────────────────────────
        //
        // A trace of the same work. The root span carries the business name the constituted act
        // does not have, and every operation appears beneath it with its arguments. This arm
        // exists because §5.3 conceded that a lexical grouping CAN be recorded, and a criterion
        // that could not tell the two apart would decide nothing.
        private static Arm MeasureDescription()
        {
            var trace = new SpanRecorder("CompletePurchase");
            trace.Child("payments.Buy", "payerId, country, period, amount, currency");
            trace.Child("payment.MarkAsPaid", "");
            trace.Child("Address", "street, city, state, country, zip");
            trace.Child("Order", "userId, userName, address, …");
            trace.Child("welcomeKit.AddOrderItem", "sku, itemName, itemPrice, …");
            trace.Child("held.Keep", "key, payment");

            return new Arm
            {
                Name = "description",
                NamesTheWhole = "yes",
                OperationsInRecord = trace.Children.Count,
                // NO, vacuously: the arrangement supplies no replay operation, so no replay of
                // this record exists after which the outcome obtains. (b) is total.
                ReplayYieldsTheWhole = "no",
                Note = $"root span '{trace.Name}' names the whole and lists all "
                       + $"{trace.Children.Count} operations - and is read by no one to do anything"
            };
        }

        // ── arrangement 3: constitution ──────────────────────────────────────────────────────
        private static Arm MeasureConstitution()
        {
            string dir = Path.Combine(Path.GetTempPath(), "criterion_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string actorName = "criterion_" + Guid.NewGuid().ToString("N");

            try
            {
                int keptLive = PerformAndCount(actorName, dir, perform: true);
                int keptOnReplay = PerformAndCount(actorName, dir, perform: false);

                var definitions = ReadDefinitions(Path.Combine(dir, actorName, "journal"));
                int operations = definitions.Count == 0 ? 0 : StatementsIn(definitions[0]);

                return new Arm
                {
                    Name = "constitution",
                    NamesTheWhole = "no",
                    OperationsInRecord = operations,
                    ReplayYieldsTheWhole = keptOnReplay == keptLive && keptLive > 0 ? "yes" : "no",
                    Note = $"one entry, `action {definitions.Count}`, holding {operations} operations; "
                           + $"replaying it alone rebuilds the state they produce ({keptOnReplay})"
                };
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        private static int PerformAndCount(string actorName, string dir, bool perform)
        {
            var actor = new ActorV2(actorName,
                typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly,
                typeof(CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments.SubscriptionPayment).Assembly,
                typeof(SubscriptionPaymentBridge).Assembly);

            actor.ConfigureStorage(DatabaseType.FileSystem, $"path={dir}");

            if (perform)
            {
                actor.Using("payments = SubscriptionPaymentBridge(); held = PaymentStore();").PerformCommand();
                actor.Using(@"
                        payment = payments.Buy(@payerId, @country, @period, @amount, @currency);
                        payment.MarkAsPaid();
                        address = Address(@street, @city, @state, @country, @zip);
                        welcomeKit = Order(@userId, @userName, address, 1, @card, @cvv, @holder, @Now.AddYears(1), null, null);
                        welcomeKit.AddOrderItem(@sku, @itemName, @itemPrice, 0m, '', 1);
                        held.Keep(@key, payment);
                    ")
                    .WithParameters(p => {
                        p["payerId", typeof(string)] = "6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11";
                        p["country", typeof(string)] = "PL";
                        p["period", typeof(string)] = "Month";
                        p["amount", typeof(decimal)] = 50m;
                        p["currency", typeof(string)] = "EUR";
                        p["street", typeof(string)] = "street";
                        p["city", typeof(string)] = "city";
                        p["state", typeof(string)] = "state";
                        p["zip", typeof(string)] = "12345";
                        p["userId", typeof(string)] = "user-1";
                        p["userName", typeof(string)] = "Lab User";
                        p["card", typeof(string)] = "1234-5678-9012-3456";
                        p["cvv", typeof(string)] = "123";
                        p["holder", typeof(string)] = "Card Holder";
                        p["sku", typeof(int)] = 41;
                        p["itemName", typeof(string)] = "welcome kit";
                        p["itemPrice", typeof(decimal)] = 0m;
                        p["key", typeof(string)] = "the-purchase";
                    })
                    .PerformCommand();
            }

            // On the replay pass the actor is constructed over the same journal and performs
            // nothing new: whatever it holds afterwards came from reading the record back.
            string answer = actor.Using("print held.Count() 'kept';").PerformQuery();
            return answer.Contains("\"kept\":1") ? 1 : 0;
        }

        // The statements of the definition body: what lies between "as" and the closing "end;",
        // split on the statement terminator. Counting every ';' in the entry would include the
        // one that closes the definition itself and would report one operation too many.
        private static int StatementsIn(string definition)
        {
            int from = definition.IndexOf(" as", StringComparison.Ordinal);
            int to = definition.LastIndexOf("end;", StringComparison.Ordinal);
            if (from < 0 || to < 0 || to <= from) return 0;

            return definition.Substring(from + 3, to - from - 3)
                             .Split(';')
                             .Count(part => part.Trim().Length > 0);
        }

        private static List<string> ReadDefinitions(string journalDir)
        {
            var found = new List<string>();
            if (!Directory.Exists(journalDir)) return found;

            foreach (var file in Directory.GetFiles(journalDir, "*.bin").OrderBy(f => f))
            {
                // shared read: an actor built over this journal still holds it open
                byte[] bytes;
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read,
                                               FileShare.ReadWrite | FileShare.Delete))
                using (var ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    bytes = ms.ToArray();
                }
                string text = Encoding.UTF8.GetString(bytes);
                int at = 0;
                while ((at = text.IndexOf("define action ", at, StringComparison.Ordinal)) >= 0)
                {
                    int end = text.IndexOf("end;", at, StringComparison.Ordinal);
                    if (end < 0) break;
                    found.Add(text.Substring(at, end - at + 4));
                    at = end + 4;
                }
            }
            return found;
        }

        // A trace: a named root and its children, written alongside the execution by an observer.
        // It has no replay operation, and that absence is the measurement rather than an omission
        // in this fixture.
        private sealed class SpanRecorder
        {
            public SpanRecorder(string name) => Name = name;
            public string Name { get; }
            public List<(string Op, string Args)> Children { get; } = new();
            public void Child(string op, string args) => Children.Add((op, args));
        }
    }
}
