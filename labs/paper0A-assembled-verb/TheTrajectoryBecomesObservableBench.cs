using System.Reflection;
using Puppeteer;
using Puppeteer.EventSourcing.DB;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // ══ A COROLLARY, NOT A CONTRIBUTION - AND THE LABEL IS LOAD-BEARING ═══════════════════════
    //
    // This lab does not argue that constituting an act is worthwhile. Read that way it would
    // invert the causality it depends on, into: the act deserves to exist because it is easier to
    // recognise afterwards. The order runs the other way. A composition can be recognised as a
    // unit downstream BECAUSE IT WAS FIRST A UNIT FOR THE SUBJECT THAT PERFORMED IT, and what
    // makes that so belongs before this file, not in it.
    //
    // So what follows is a corollary, and it shows one thing only: once something exists as a
    // semantic unit in the history, the rest of the system can refer to THAT SAME UNIT without
    // reconstituting it.
    //
    // The mechanism is small. A Seek may carry more than one OnMatch, and every OnMatch of one
    // Seek must match THE SAME SCRIPT. So a reaction binding a value from one constituent
    // operation and a value from another is referring to the act as a whole - and what unifies
    // the two bindings is not a shared variable, it is THE ENTRY. Measured while building this:
    // a receiver naming a binding from a sibling OnMatch does not resolve, so the correlation
    // here is carried by the recorded act itself and by nothing else. Over a record of
    // transitions there is no such entry: the two facts are two rows, and no row is the act.
    //
    // Two hosts, two domain assemblies written by parties who never met, each running the acts
    // of its own repertoire:
    //
    //     one records purchases   - an order is opened and an item is added to it
    //     the other records buys  - a subscription payment is bought and marked paid
    //
    // Then two reactions, one per host, written against completely different patterns because
    // the domains have nothing in common - and asked the SAME QUESTION: what did each customer
    // spend? Neither domain has that notion. One has a total over an order's items and no notion
    // of a customer across orders; the other has a money value and no notion of a customer's
    // spending. The question lives in neither repertoire, and it is answered from the record of
    // the acts.
    //
    // THE ASSERTION IS THE EQUIVALENCE. The two reactions emit, from two unrelated domains
    // through two unrelated patterns, THE SAME ROWS. That is what makes the plane real rather
    // than a convenience: it is not that a projection can be written, it is that the same
    // projection can be written over repertoires that share no type.
    //
    // ══ WHAT THIS DOES NOT CLAIM ══════════════════════════════════════════════════════════════
    //
    // Not that this is the only way to obtain such a view. A process manager correlating the
    // transitions would reach an equivalent answer, and that is conceded without reservation.
    // The difference is the one this study keeps meeting and it is about what the record holds:
    // to refer to the act here is to refer to ONE entry, and the correlation rule a
    // transition-level record needs in order to reconstitute the act lives in no row of it.
    //
    // Not that a name is what matters, either. A method can name a sequence; it does not thereby
    // make that sequence an act of a subject. CompletePurchase() may be a lexical container for
    // five calls and nothing more. What is at stake is not whether the name exists but whether
    // the runtime treats it as the verb performed by a subject and the record keeps it as one -
    // which is what makes this a corollary of that, rather than evidence for it.
    //
    // And no timing is measured. The claim is about what can be referred to, not what it costs.
    [TestClass]
    public class TheTrajectoryBecomesObservableBench
    {
        private static readonly Assembly Ordering =
            typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly;

        private static readonly Assembly Payments =
            typeof(CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments.SubscriptionPayment).Assembly;

        private static readonly Assembly Bridge = typeof(SubscriptionPaymentBridge).Assembly;

        // the same two customers and the same two amounts, expressed by two repertoires that
        // have no type in common
        private const string CustomerOne = "6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11";
        private const string CustomerTwo = "2b8d4e6f-1a3c-4d5e-9f0b-7c2a4e6d8b33";

        [TestMethod, TestCategory("Bench")]
        public void OneQuestionIsAnsweredFromTwoRepertoiresThatShareNoType()
        {
            var fromPurchases = ObservePurchases();
            var fromSubscriptions = ObserveSubscriptions();

            Console.WriteLine();
            Console.WriteLine("=== one question, two repertoires with no type in common ===");
            Console.WriteLine();
            Console.WriteLine("    what did each customer spend?  - a notion neither domain declares");
            Console.WriteLine();
            Console.WriteLine("    from the purchase repertoire      from the subscription repertoire");
            for (int i = 0; i < Math.Max(fromPurchases.Count, fromSubscriptions.Count); i++)
            {
                string left = i < fromPurchases.Count ? fromPurchases[i] : "-";
                string right = i < fromSubscriptions.Count ? fromSubscriptions[i] : "-";
                Console.WriteLine($"      {left,-32}  {right}");
            }
            Console.WriteLine();

            Assert.AreNotEqual(0, fromPurchases.Count,
                "The reaction observed the purchase acts.");

            CollectionAssert.AreEqual(fromPurchases, fromSubscriptions,
                "Two reactions, written against unrelated patterns over unrelated domains, answer "
                + "one question identically. The plane the answer lives in is declared by neither "
                + "repertoire - it is a plane of the record of the acts.");
        }

        // ── the purchase repertoire ──────────────────────────────────────────────────────────
        //
        // The reaction binds the customer from the operation that OPENS the order and the price
        // and units from the operation that ADDS AN ITEM to it. Two operations, one Seek, and
        // therefore one recorded act - which is the only reason the two bindings can meet.
        private static List<string> ObservePurchases()
        {
            var actor = new ActorV2($"purchases_{Guid.NewGuid():N}", Ordering);
            actor.ConfigureStorage(DatabaseType.IN_MEMORY, "memory");

            actor.Reactions.DefineReaction("SpendPerCustomer")
                .Job().Company()
                .WithSharedHydration()
                .Seek("Purchase")
                    .OnMatch("_ = Order($customer, _, _, _, _, _, _, _, _, _);")
                    .OnMatch("[_:Order].AddOrderItem(_, _, $unitPrice, _, _, $units)")
                .Program.Emit("print @customer 'customer', @unitPrice * @units 'spent';");

            RecordPurchase(actor, CustomerOne, unitPrice: 25m, units: 2);
            RecordPurchase(actor, CustomerTwo, unitPrice: 30m, units: 1);

            return Drain(actor, "SpendPerCustomer");
        }

        private static void RecordPurchase(ActorV2 actor, string customer, decimal unitPrice, int units)
        {
            actor.Using(@"
                    address = Address(@street, @city, @state, @country, @zip);
                    o = Order(@customer, @customerName, address, 1, @card, @cvv, @holder, @Now.AddYears(1), null, null);
                    o.AddOrderItem(@sku, @itemName, @unitPrice, 0m, '', @units);
                ")
                .WithParameters(p => {
                    p["street", typeof(string)] = "street";
                    p["city", typeof(string)] = "city";
                    p["state", typeof(string)] = "state";
                    p["country", typeof(string)] = "country";
                    p["zip", typeof(string)] = "12345";
                    p["customer", typeof(string)] = customer;
                    p["customerName", typeof(string)] = "the customer";
                    p["card", typeof(string)] = "1234-5678-9012-3456";
                    p["cvv", typeof(string)] = "123";
                    p["holder", typeof(string)] = "Card Holder";
                    p["sku", typeof(int)] = 41;
                    p["itemName", typeof(string)] = "an item";
                    p["unitPrice", typeof(decimal)] = unitPrice;
                    p["units", typeof(int)] = units;
                })
                .PerformCommand();
        }

        // ── the subscription repertoire ──────────────────────────────────────────────────────
        //
        // The same shape over a domain that shares nothing with the one above: the customer and
        // the amount are bound from the operation that BUYS, and the act is only recognised as
        // this act because the operation that MARKS IT PAID is in the same recorded entry.
        private static List<string> ObserveSubscriptions()
        {
            var actor = new ActorV2($"subscriptions_{Guid.NewGuid():N}", Payments, Bridge);
            actor.ConfigureStorage(DatabaseType.IN_MEMORY, "memory");

            actor.Reactions.DefineReaction("SpendPerCustomer")
                .Job().Company()
                .WithSharedHydration()
                .Seek("Buy")
                    .OnMatch("[_:SubscriptionPaymentBridge].Buy($customer, _, _, $amount, _)")
                    .OnMatch("[_:SubscriptionPayment].MarkAsPaid()")
                .Program.Emit("print @customer 'customer', @amount 'spent';");

            actor.Using("payments = SubscriptionPaymentBridge();").PerformCommand();

            RecordSubscription(actor, CustomerOne, amount: 50m);
            RecordSubscription(actor, CustomerTwo, amount: 30m);

            return Drain(actor, "SpendPerCustomer");
        }

        private static void RecordSubscription(ActorV2 actor, string customer, decimal amount)
        {
            actor.Using(@"
                    payment = payments.Buy(@customer, @country, @period, @amount, @currency);
                    payment.MarkAsPaid();
                ")
                .WithParameters(p => {
                    p["customer", typeof(string)] = customer;
                    p["country", typeof(string)] = "PL";
                    p["period", typeof(string)] = "Month";
                    p["amount", typeof(decimal)] = amount;
                    p["currency", typeof(string)] = "EUR";
                })
                .PerformCommand();
        }

        private static List<string> Drain(ActorV2 actor, string reactionName)
        {
            var sink = new RecordingSink();
            new StageHook(actor).SetOutputTarget(sink);
            actor.Reactions.Execute(reactionName);
            return sink.Received;
        }

        private sealed class RecordingSink : IOutputSink
        {
            public readonly List<string> Received = new();

            public void Push(in PushDocument document) => Received.Add(document.Document);
        }
    }
}
