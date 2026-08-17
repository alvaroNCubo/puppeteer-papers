using CompanyName.MyMeetings.Modules.Payments.Domain.Payers;
using CompanyName.MyMeetings.Modules.Payments.Domain.PriceListItems;
using CompanyName.MyMeetings.Modules.Payments.Domain.PriceListItems.PricingStrategies;
using CompanyName.MyMeetings.Modules.Payments.Domain.SeedWork;
using CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments;
using CompanyName.MyMeetings.Modules.Payments.Domain.Subscriptions;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // Where the composed act is writable, and where it was written.
    //
    // Two operations of two different repertoires constitute one act in the system under study:
    // a payment is bought and paid, and a subscription is created FROM THAT PAYMENT. The second
    // operation takes what the first produced.
    //
    // This lab does not dissect anything and does not remove anything. It asks one question of
    // the SHIPPED BINARY, unmodified, referenced as a prebuilt assembly by a foreign host that
    // the authors never anticipated:
    //
    //     can the joined act be written using only what the domain assembly declares?
    //
    // The answer, checked by the compiler below rather than argued, is YES. Every member the
    // joined block needs is public; the assembly declares no InternalsVisibleTo; nothing is
    // simulated, replaced or stood in for. The block runs, and the aggregate it produces raises
    // the same event the system raises in production.
    //
    // That answer is the interesting one, because it removes the comfortable explanation. The
    // domain never prevented the act from being said. No visibility rule, no missing member, no
    // abstraction gap. It was sayable in the domain's own vocabulary the whole time.
    //
    // And it is said nowhere.
    //
    //   - Not in the system. There the arrow from the first operation to the second is not a
    //     call: the first half publishes, a message is serialised and enqueued, a poller picks
    //     it up, a handler deserialises it, and an aggregate is loaded back from a stream by the
    //     identity that travelled inside the message. The act exists; it is spread over five
    //     mechanisms and named by none of them.
    //
    //     It is worth being exact about what that enqueue is, because the obvious word for it is
    //     the wrong one. The queue is a table in the module's OWN SCHEMA, written by an insert
    //     and read by a poller in the same process. Nothing crosses a boundary: source and
    //     destination are the same place. So the mechanism is transport, but the work it is
    //     doing here is not transport - it is SEQUENCING. It supplies "and then", and "and then",
    //     between two operations that constitute one act, is a property of the act rather than
    //     of how the system is deployed.
    //
    //         A wire is a dependency that could have been a value.
    //         A queued message is an "and then" that could have been a semicolon.
    //
    //     None of which makes the mechanism gratuitous, and the concession comes first: an
    //     enqueue through a durable table buys at-least-once delivery, retry, and isolation of
    //     the second half's failure from the first. That is real, and it is a need about WHEN
    //     and WITH WHAT GUARANTEE. It is not a need about what the act is. One mechanism carries
    //     both because only one was available.
    //
    //   - Not in the tests either, and that is the sharper half. The authors' own domain suite
    //     covers both halves, with zero test doubles - a discipline worth conceding plainly,
    //     because it is uncommon and it is what makes this measurable at all. One test buys the
    //     payment. Another creates the subscription. NO TEST JOINS THEM.
    //
    //   - And where the join would have been, the suite types a literal. The value that carries
    //     the first half's outcome into the second is CONSTRUCTED BY HAND in the Arrange block,
    //     with a fresh identity that belongs to nothing:
    //
    //         new SubscriptionPaymentSnapshot(
    //             new SubscriptionPaymentId(Guid.NewGuid()), ...)
    //
    //     The lab prints both seams side by side. The fabricated one carries an identity no
    //     payment ever had. The produced one carries the identity of the payment that was
    //     actually bought two lines above. Same type, same shape, and one of them is the act's
    //     own history while the other is a plausible stand-in for it.
    //
    // So the obstacle was never capability. It was PLACE: there was nowhere to put the sentence.
    // A test method is the one context in which the infrastructure is allowed to be absent, and
    // that is exactly where the block becomes writable - which is why the block a test author
    // writes is the closest thing the system has to a statement of what it does.
    //
    // The falsifier is stated and it is easy to run: exhibit, in the system or its suites, one
    // artifact that names this act and contains its operations. The count is currently zero, and
    // one such artifact would take it to one.
    [TestClass]
    public class WhereTheComposedActIsWritableBench
    {
        [TestMethod, TestCategory("Bench")]
        public void TheJoinedActRunsAgainstTheShippedDomainWithNothingStandingIn()
        {
            var payerId = new PayerId(Guid.NewGuid());
            var priceList = OneMonthAtSixty();

            // ---- the joined act, written once, in the domain's own vocabulary --------------
            var payment = SubscriptionPayment.Buy(
                payerId,
                SubscriptionPeriod.Month,
                "PL",
                MoneyValue.Of(60, "PLN"),
                priceList);

            payment.MarkAsPaid();

            var produced = payment.GetSnapshot();
            var subscription = Subscription.Create(produced);
            // -------------------------------------------------------------------------------

            // the seam as the authors' own suite writes it: same type, invented identity
            var fabricated = new SubscriptionPaymentSnapshot(
                new SubscriptionPaymentId(Guid.NewGuid()),
                new PayerId(Guid.NewGuid()),
                SubscriptionPeriod.Month,
                "PL");

            Console.WriteLine();
            Console.WriteLine("=== the joined act over a prebuilt domain assembly ===");
            Console.WriteLine("    test doubles used: 0        (no store, no bus, no inbox, no clock)");
            Console.WriteLine("    domain operations invoked: 3");
            Console.WriteLine();
            Console.WriteLine("    seam PRODUCED by the first half:");
            Console.WriteLine($"        payment id  : {produced.Id.Value}");
            Console.WriteLine($"        payer id    : {produced.PayerId.Value}");
            Console.WriteLine("    seam FABRICATED where the suite joins nothing:");
            Console.WriteLine($"        payment id  : {fabricated.Id.Value}");
            Console.WriteLine($"        payer id    : {fabricated.PayerId.Value}");
            Console.WriteLine();

            Assert.IsNotNull(subscription,
                "The joined act runs against the shipped binary with nothing standing in.");

            Assert.AreEqual(payerId.Value, produced.PayerId.Value,
                "The produced seam carries the payer of the payment that was actually bought.");

            Assert.AreNotEqual(produced.Id.Value, fabricated.Id.Value,
                "The fabricated seam carries an identity that belongs to no payment. "
                + "It is not a shortcut in the test: it is the act's history, replaced by a value "
                + "of the right shape.");
        }

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
