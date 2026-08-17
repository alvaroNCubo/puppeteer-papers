using System.Reflection;
using CompanyName.MyMeetings.Modules.Payments.Domain.Payers;
using CompanyName.MyMeetings.Modules.Payments.Domain.PriceListItems;
using CompanyName.MyMeetings.Modules.Payments.Domain.PriceListItems.PricingStrategies;
using CompanyName.MyMeetings.Modules.Payments.Domain.SeedWork;
using CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments;
using CompanyName.MyMeetings.Modules.Payments.Domain.Subscriptions;
using eShop.Ordering.Domain.AggregatesModel.BuyerAggregate;
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // A whole business trajectory, told with nothing but the operations of two repertoires that
    // have never heard of each other.
    //
    // ══ WHAT THIS IS, AND WHAT IT IS NOT ══════════════════════════════════════════════════════
    //
    // This is an AUTHOR-CONSTRUCTED SCENARIO. It was written for this study; nobody who built
    // either system wrote it or would necessarily write it. It is therefore NOT observational
    // evidence about how those systems were designed, and no count taken from it says anything
    // about their authors' choices. Every claim of that kind in this work rests on their
    // artifacts, never on this one.
    //
    // What it is instead is an EXISTENCE PROOF, answering a question the observational evidence
    // cannot reach. Their own domain suites prove each repertoire SURVIVES in isolation - they
    // exercise pieces, one aggregate at a time, with zero test doubles. They do not show whether
    // those pieces suffice to tell a long story. This does.
    //
    // The four bodies of evidence answer four different questions, and keeping them apart is the
    // whole methodological point:
    //
    //     their own domain tests   →  the repertoires SURVIVE
    //     this scenario            →  the repertoires COMPOSE
    //     their architecture       →  the composition is DISPERSED
    //     the substrate labs       →  the composition becomes a NAMED VERB OF A SUBJECT
    //
    // Deliberately, this scenario does not imitate either original architecture. No handler, no
    // command, no bus, no repository, no unit of work, no mapper - because the point is precisely
    // that none of that machinery is needed to express the story. Both domain assemblies are the
    // shipped binaries, unmodified, referenced as prebuilt references. Nothing was edited to make
    // any of it fit.
    //
    // ══ WHAT THE STORY CROSSES ════════════════════════════════════════════════════════════════
    //
    // One customer, a subscription bought and paid, then a purchase with items that is itself
    // paid and shipped. The trajectory runs over FOUR aggregates in TWO assemblies written by
    // different parties, and the lab asserts what the reader would otherwise have to take on
    // trust: neither assembly names the other. There is no shared type, no shared base, no
    // shared identifier type - the customer is a Guid-bearing identity in one repertoire and a
    // string-bearing one in the other, and NOTHING IN EITHER ASSEMBLY KNOWS THEY ARE THE SAME
    // PERSON. Only the party assembling the act knows that.
    //
    // That is not a coordination the repertoires neglected. It is one they are NOT IN A POSITION
    // TO PERFORM. For either to assert that the other's customer is its own, it would have to
    // know the other - and at that moment it would stop being the independent piece this
    // scenario is composing. The gap is not an omission in the code; it is a property of what
    // each piece can know from where it stands.
    //
    // So the three questions the exhibit exists to ask, once the story is on the page:
    //
    //     what is the whole called?       neither repertoire names it
    //     where is that name recorded?    nowhere
    //     who can see the whole?          only the assembler
    //
    // The third answer is deliberately not "nobody". Somebody can: the party that wrote this
    // scenario. That party supplied the identities neither repertoire could jointly mint, invoked
    // operations across both repertoires, and HELD THE CROSS-REPERTOIRE RELATION under which their
    // results formed one trajectory. Saying "nobody" would be both false and a waste of the
    // evidence, because THE ASSEMBLER'S POSITION IS THE FINDING - the assembler is not merely the
    // party that calls the methods, it is the party holding context about the whole that no part
    // can hold individually.
    //
    // Note what is NOT claimed. Not that the order of the operations is unknowable to either
    // repertoire - some of it may well be deducible from within one. What neither holds is the
    // relation ACROSS them under which the results are one trajectory, and that is the thing the
    // assertions below measure.
    //
    // Which gives the scenario a symmetry worth stating, because it runs end to end:
    //
    //     at the start   the assembler supplies identities the pieces cannot jointly mint
    //     during         the pieces produce and name the transitions they are entitled to name
    //     at the end     only the assembler is positioned to recognise that those operations
    //                    belonged to one whole
    //
    // ══ WHAT THIS SCENARIO CLAIMS, AND WHAT IT LEAVES FOR LATER ═══════════════════════════════
    //
    // It does NOT claim that a subject of the kind this work proposes is necessary. Nothing here
    // shows that. What it shows is more elementary and much harder to argue with:
    //
    //     THE WHOLE IS KNOWABLE, BUT NOT BY ANY OF ITS PARTS.
    //
    // The question that follows - if the whole is an act, and no piece can hold it, whose act is
    // it? - is the question this study exists to answer, and it is left standing here rather than
    // answered in advance.
    //
    // And the finding should not be read as a charge of anaemia against either domain. The
    // opposite: they are let speak as fully as they legitimately can, and what shows up is the
    // silence between them.
    //
    //     The repertoires name every transition they are entitled to name. What they do not name
    //     is the trajectory that exists only in their composition.
    //
    // ══ THE EVENTS NAME THE TRANSITIONS. NONE NAMES THE TRAJECTORY. ═══════════════════════════
    //
    // The scenario below prints nine events and every one of them names a state change of a
    // single subject. That is the observation, and on its own it would be worth little, because
    // the obvious objection is that it is TRUE BY DEFINITION: a domain event is named after an
    // aggregate's state change by convention, so of course none names anything else.
    //
    // The objection is conceded where it holds and it is answered where it does not. It holds for
    // the domain layer, fully: in both systems every single site that raises a domain event is
    // inside an aggregate type, without exception, so no event there COULD name a fact that is
    // not a fact about one aggregate.
    //
    // It does not hold one layer out. Integration events are raised in handlers rather than in
    // aggregates, they cross module and service boundaries, and nothing constrains their naming -
    // they are the natural home for a business milestone. Nor does it hold for commands, which
    // may be named anything the caller wants and are written in the imperative rather than the
    // indicative, so they say what was ASKED FOR rather than what happened.
    //
    // A census over both systems, at all three layers:
    //
    //     domain events        27      naming a trajectory:  0
    //     integration events   18      naming a trajectory:  0
    //     commands             29      naming a trajectory:  0
    //
    // Every one is a subject and a past participle, or a verb and one aggregate. The clearest
    // case is a pair of commands: one asks for a subscription payment, another asks for the
    // subscription that payment entitles - two imperatives for the two halves of one act, and no
    // imperative for the pair.
    //
    // So the absence is not a naming convention showing through. It survives at the two layers
    // where a trajectory-level name was available and would have been useful.
    //
    // FALSIFIER, and it is a cheap one to run: exhibit one artifact in either system - event,
    // command, type or method - whose name refers to a trajectory spanning operations of more
    // than one subject. The count is zero across 74 named artifacts; one takes it to one.
    //
    // WHAT THIS DOES NOT SHOW, said plainly: an absent name is a fact about a vocabulary, not
    // proof that the act is absent. The act is demonstrably there - seventeen operations below
    // produce a coherent outcome. That is the whole of the point. The act exists and the
    // vocabulary has no word for it.
    //
    // ══ AND THE LIMIT, DECLARED HERE RATHER THAN LEFT TO BE FOUND ═════════════════════════════
    //
    // The trajectory reaches three times for an identity that no domain operation produces: one
    // repertoire declares an integer identity its own code never assigns, and two of its
    // operations take such an identity as a parameter. Those values are supplied here by the
    // assembler, from outside both repertoires, and they are named as such below.
    //
    // They expose A SECOND ROLE of the assembler, different in kind from the first. The first is
    // positional: it holds a relation among values the repertoires produced, and that is what the
    // assertions below measure. The second is not - in three places it SUPPLIES a value the
    // composition requires and no repertoire produces. Nothing here shows the second role to be
    // positional too, and nothing here claims it. The carrier lab meets the same gap from the
    // other end of a composition.
    [TestClass]
    public class ALongTrajectoryOverTwoUntouchedRepertoiresLab
    {
        [TestMethod, TestCategory("Lab")]
        public void TwoRepertoiresThatShareNoTypeTellOneStoryEndToEnd()
        {
            // the customer, as each repertoire is able to know them - and no more than that
            var theCustomerInOneRepertoire = new PayerId(new Guid("11111111-1111-1111-1111-111111111111"));
            const string theCustomerInTheOther = "11111111-1111-1111-1111-111111111111";

            // identities neither repertoire mints. The assembler supplies them, named so that no
            // reader has to hunt for where they came from.
            const int keyForTheBuyer = 1;
            const int keyForTheOrder = 1;
            const int keyForThePaymentMethod = 1;

            var cardExpiry = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // ─── the trajectory ───────────────────────────────────────────────────────────────

            // a subscription is bought, paid, and granted
            var subscriptionPayment = SubscriptionPayment.Buy(
                theCustomerInOneRepertoire,
                SubscriptionPeriod.Month,
                "PL",
                MoneyValue.Of(60, "PLN"),
                OneMonthAtSixty());

            subscriptionPayment.MarkAsPaid();

            var subscription = Subscription.Create(subscriptionPayment.GetSnapshot());

            // the same customer, now in a repertoire that has never heard of the first
            var buyer = new Buyer(theCustomerInTheOther, "the customer");

            var address = new Address("street", "city", "state", "country", "zipcode");

            var order = new Order(
                theCustomerInTheOther, "the customer", address,
                cardTypeId: 1, cardNumber: "4012888888881881", cardSecurityNumber: "123",
                cardHolderName: "the customer", cardExpiration: cardExpiry);

            // items are added
            order.AddOrderItem(productId: 1, productName: "first item", unitPrice: 10m, discount: 0m, pictureUrl: "u", units: 2);
            order.AddOrderItem(productId: 2, productName: "second item", unitPrice: 25m, discount: 5m, pictureUrl: "u", units: 1);
            order.AddOrderItem(productId: 3, productName: "third item", unitPrice: 7m, discount: 0m, pictureUrl: "u", units: 4);

            // and the purchase runs its course
            order.SetAwaitingValidationStatus();
            order.SetStockConfirmedStatus();

            buyer.VerifyOrAddPaymentMethod(
                cardTypeId: 1, alias: "the card", cardNumber: "4012888888881881",
                securityNumber: "123", cardHolderName: "the customer", expiration: cardExpiry,
                orderId: keyForTheOrder);

            order.SetPaymentMethodVerified(keyForTheBuyer, keyForThePaymentMethod);

            order.SetPaidStatus();
            order.SetShippedStatus();

            // ──────────────────────────────────────────────────────────────────────────────────

            var oneRepertoire = typeof(SubscriptionPayment).Assembly;
            var theOther = typeof(Order).Assembly;

            var raised = new List<string>();
            raised.AddRange(subscriptionPayment.GetDomainEvents().Select(e => e.GetType().Name));
            raised.AddRange(subscription.GetDomainEvents().Select(e => e.GetType().Name));
            raised.AddRange(buyer.DomainEvents.Select(e => e.GetType().Name));
            raised.AddRange(order.DomainEvents.Select(e => e.GetType().Name));

            Console.WriteLine();
            Console.WriteLine("=== one story, two shipped domain assemblies, neither modified ===");
            Console.WriteLine();
            Console.WriteLine("    domain operations invoked : 17");
            Console.WriteLine("    aggregates taking part    : 4   (SubscriptionPayment, Subscription, Buyer, Order)");
            Console.WriteLine("    assemblies involved       : 2   authored by different parties");
            Console.WriteLine("    test doubles used         : 0   (no handler, no bus, no store, no mapper)");
            Console.WriteLine();
            Console.WriteLine($"    subscription reached      : granted, from a payment marked paid");
            Console.WriteLine($"    order reached status      : {order.OrderStatus}");
            Console.WriteLine($"    order total               : {order.GetTotal()}");
            Console.WriteLine();
            Console.WriteLine("    identities supplied from outside both repertoires : 3");
            Console.WriteLine("    types the two assemblies share                    : 0");
            Console.WriteLine();
            Console.WriteLine($"    the two repertoires raised {raised.Count} events. Each names a TRANSITION:");
            foreach (var name in raised)
                Console.WriteLine($"        {name}");
            Console.WriteLine();
            Console.WriteLine("    and the questions the exhibit exists to ask:");
            Console.WriteLine("        what is the whole called?      - neither repertoire names it");
            Console.WriteLine("        where is that name recorded?   - nowhere");
            Console.WriteLine("        who can see the whole?         - only the assembler");
            Console.WriteLine();

            Assert.AreEqual(OrderStatus.Shipped, order.OrderStatus,
                "The repertoires carry the trajectory from start to end on their own operations.");

            // the figure follows the aggregate's own definition of a total, read from it rather
            // than assumed here: the sum over the items of units times unit price
            Assert.AreEqual(73m, order.GetTotal(),
                "And one of them computes over the whole of its own part, so the trajectory is "
                + "executable end to end. Executability is not wholeness and this assertion does "
                + "not claim it: what carries that is the assertion below, on what neither "
                + "assembly is in a position to know.");

            Assert.IsNotNull(subscription,
                "The other carried its own half to a granted subscription.");

            Assert.IsFalse(
                Names(oneRepertoire).Contains(theOther.GetName().Name)
                || Names(theOther).Contains(oneRepertoire.GetName().Name),
                "Neither assembly names the other. The customer is a Guid-bearing identity in one "
                + "repertoire and a string-bearing one in the other, and NEITHER IS IN A POSITION "
                + "TO KNOW they denote the same person. For one to assert it, the relation between "
                + "the two representations would have to be made knowledge OF THAT REPERTOIRE - by "
                + "whatever mechanism, since a shared type is not required and this very lab shows a "
                + "value crossing where a type does not. What this measures is not that such a thing "
                + "is impossible; it is that neither published repertoire occupies that position. "
                + "Only the assembler does. The whole is knowable - just not by any of its parts.");

            Assert.IsTrue(raised.Count >= 8,
                "Every step announces itself. What none of them announces is the trajectory: each "
                + "event names a transition of one aggregate.");
        }

        private static HashSet<string> Names(Assembly assembly)
            => assembly.GetReferencedAssemblies().Select(a => a.Name).ToHashSet();

        private static PriceList OneMonthAtSixty()
        {
            var items = new List<PriceListItemData>
            {
                new PriceListItemData(
                    "PL",
                    SubscriptionPeriod.Month,
                    MoneyValue.Of(60, "PLN"),
                    PriceListItemCategory.New)
            };

            return PriceList.Create(items, new DirectValueFromPriceListPricingStrategy(items));
        }
    }
}
