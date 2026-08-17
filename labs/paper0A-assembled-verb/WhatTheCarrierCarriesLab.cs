using CompanyName.MyMeetings.Modules.Payments.Domain.Payers;
using CompanyName.MyMeetings.Modules.Payments.Domain.PriceListItems;
using CompanyName.MyMeetings.Modules.Payments.Domain.PriceListItems.PricingStrategies;
using CompanyName.MyMeetings.Modules.Payments.Domain.SeedWork;
using CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments;
using CompanyName.MyMeetings.Modules.Payments.Domain.Subscriptions;
using CompanyName.MyMeetings.Modules.Payments.Domain.Subscriptions.Events;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // What the carrier carries, and what no carrier can carry.
    //
    // Between the two operations that constitute one act there is an arrow, and in the system
    // under study that arrow is a durable enqueue: the outcome is serialised, inserted into a
    // table in the module's own schema, picked up by a poller in the same process, deserialised,
    // and used to load an aggregate back by the identity that travelled inside it.
    //
    // The concession comes first and it is not a formality. A durable enqueue buys at-least-once
    // delivery, retry, and isolation of the second half's failure from the first. Those are real
    // needs and the mechanism meets them. They are needs about WHEN the second half runs and
    // WITH WHAT GUARANTEE.
    //
    // This lab asks a different question, the one the mechanism's shape hides: OF WHAT THE ACT
    // NEEDS, how much does the carrier actually carry? It runs the same composition three times,
    // changing only how the arrow is carried:
    //
    //     direct     the outcome is passed to the next operation
    //     queued     the outcome is enqueued and dequeued in memory
    //     kept       the outcome is kept under a key and found by that key
    //
    // The third is the interesting one, because it is what the durable table reduces to once the
    // durability is accounted for elsewhere: a value put somewhere under a key, and found again
    // by that key. The identifier travelling inside the serialised message IS the key, and the
    // load that follows IS the lookup.
    //
    // The three runs are compared field by field on what the second operation produces. The
    // result splits the fields into two groups, and the split is the finding:
    //
    //   - the fields that come FROM THE ACT are identical under all three carriers. Whatever
    //     the second operation needed from the first arrives as a value, and no carrier changes
    //     it. So the carrier's whole contribution to the composition is "and then", plus a key
    //     to say which one;
    //
    //   - and the fields the operation MINTS FOR ITSELF differ under all three, including
    //     between two runs of the same carrier. They are not carried because they were never in
    //     transit: they enter the act from outside it, inside the operation, where no caller can
    //     see them and no journal can capture them by replaying the call.
    //
    // That second group is why replacing the carrier is not the whole story, and it is stated
    // here rather than left for a reader to notice. An act that mints a value inside an
    // operation is not reproducible from its own record under ANY carrier - durable table,
    // queue, or register alike. What the substrate changes is not the carrying; it is that the
    // act itself becomes the recorded unit, so the arrow no longer needs a mechanism that
    // survives a crash in order for the act to survive one.
    //
    // No timing is measured here and none should be. The claim is about what a mechanism is
    // supplying to a composition, not about what it costs.
    [TestClass]
    public class WhatTheCarrierCarriesLab
    {
        [TestMethod, TestCategory("Lab")]
        public void TheArrowCarriedThreeWaysDeliversTheSameActAndTheSameGap()
        {
            // one payment, bought and paid once. Three carriers, one act - anything else would
            // be comparing three different acts and would prove nothing about the carrying.
            var payment = BuyAndPay();

            var runs = new[]
            {
                CarryDirectly(payment),
                CarryThroughAnInMemoryQueue(payment),
                CarryThroughAKeyedRegister(payment)
            };

            Console.WriteLine();
            Console.WriteLine("=== the same act, the arrow carried three ways ===");
            Console.WriteLine();
            Console.WriteLine($"    {"carrier",-10} {"paymentId",-38} {"payerId",-38} {"period",-6} {"country",-8} {"status",-8} mintedId");
            foreach (var r in runs)
                Console.WriteLine($"    {r.Carrier,-10} {r.PaymentId,-38} {r.PayerId,-38} {r.Period,-6} {r.Country,-8} {r.Status,-8} {r.MintedSubscriptionId}");

            Console.WriteLine();
            Console.WriteLine("    FROM THE ACT   - identical under every carrier:");
            Console.WriteLine($"        paymentId  {(runs.Select(r => r.PaymentId).Distinct().Count() == 1 ? "same" : "DIFFERS")}");
            Console.WriteLine($"        payerId    {(runs.Select(r => r.PayerId).Distinct().Count() == 1 ? "same" : "DIFFERS")}");
            Console.WriteLine($"        period     {(runs.Select(r => r.Period).Distinct().Count() == 1 ? "same" : "DIFFERS")}");
            Console.WriteLine($"        country    {(runs.Select(r => r.Country).Distinct().Count() == 1 ? "same" : "DIFFERS")}");
            Console.WriteLine($"        status     {(runs.Select(r => r.Status).Distinct().Count() == 1 ? "same" : "DIFFERS")}");
            Console.WriteLine();
            Console.WriteLine("    MINTED INSIDE  - carried by none of them:");
            Console.WriteLine($"        subscriptionId  {runs.Select(r => r.MintedSubscriptionId).Distinct().Count()} distinct values across 3 runs");
            Console.WriteLine();

            Assert.AreEqual(1, runs.Select(r => r.PaymentId).Distinct().Count(),
                "The identity the first operation produced reaches the second unchanged, whichever "
                + "carrier is used. The carrier supplies sequence and a key, not content.");

            Assert.AreEqual(1, runs.Select(r => (r.PayerId, r.Period, r.Country, r.Status)).Distinct().Count(),
                "Everything the second operation needed from the first arrives as a value.");

            Assert.AreEqual(3, runs.Select(r => r.MintedSubscriptionId).Distinct().Count(),
                "And the value the operation mints for itself is carried by no carrier, because it "
                + "was never in transit. It enters the act from outside, inside the operation.");
        }

        private sealed class Outcome
        {
            public string Carrier;
            public Guid PaymentId;
            public Guid PayerId;
            public string Period;
            public string Country;
            public string Status;
            public Guid MintedSubscriptionId;
        }

        // the arrow as a call
        private static Outcome CarryDirectly(SubscriptionPayment payment)
        {
            var subscription = Subscription.Create(payment.GetSnapshot());
            return Read("direct", subscription);
        }

        // the arrow as an enqueue and a dequeue, in memory
        private static Outcome CarryThroughAnInMemoryQueue(SubscriptionPayment payment)
        {
            var pending = new Queue<SubscriptionPaymentSnapshot>();
            pending.Enqueue(payment.GetSnapshot());

            var subscription = Subscription.Create(pending.Dequeue());
            return Read("queued", subscription);
        }

        // the arrow as a keyed register - what the durable table reduces to once durability is
        // accounted for by the record of the act rather than by the carrier
        private static Outcome CarryThroughAKeyedRegister(SubscriptionPayment payment)
        {
            var paymentsHeld = new PaymentStore();
            const string key = "held";
            paymentsHeld.Keep(key, payment);

            var held = paymentsHeld.Find(key);
            var subscription = Subscription.Create(held.GetSnapshot());
            return Read("kept", subscription);
        }

        // The two operations that constitute the act's first half, run ONCE. The payer identity
        // is a fixed literal because it is an input to the act; the identity of the payment is
        // not fixed and cannot be, because it is minted inside the operation - the same gap the
        // assertion reports at the other end of the composition.
        private static readonly Guid ThePayer = new Guid("11111111-1111-1111-1111-111111111111");

        private static SubscriptionPayment BuyAndPay()
        {
            var payment = SubscriptionPayment.Buy(
                new PayerId(ThePayer),
                SubscriptionPeriod.Month,
                "PL",
                MoneyValue.Of(60, "PLN"),
                OneMonthAtSixty());

            payment.MarkAsPaid();
            return payment;
        }

        private static Outcome Read(string carrier, Subscription subscription)
        {
            var created = subscription.GetDomainEvents().OfType<SubscriptionCreatedDomainEvent>().Single();

            return new Outcome
            {
                Carrier = carrier,
                PaymentId = created.SubscriptionPaymentId,
                PayerId = created.PayerId,
                Period = created.SubscriptionPeriodCode,
                Country = created.CountryCode,
                Status = created.Status,
                MintedSubscriptionId = created.SubscriptionId
            };
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
