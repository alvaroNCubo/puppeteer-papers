using CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // Where the puppet keeps the creatures one of its repertoires produced, so that a later
    // act can reach one. It is authored by the assembler, not by either domain, and it names
    // exactly one of them.
    //
    // This is what replaces a retrieval that used to be a query: a keyed lookup over what the
    // actor already holds. It replaces no store, because there is nothing to store - the act
    // that produced the aggregate is already in the journal, and replaying the journal
    // reproduces both the aggregate and this index.
    public class PaymentStore
    {
        private readonly Dictionary<string, SubscriptionPayment> byKey = new();

        public void Keep(string key, SubscriptionPayment payment)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (payment == null) throw new ArgumentNullException(nameof(payment));

            byKey[key] = payment;
        }

        public SubscriptionPayment Find(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            return byKey.TryGetValue(key, out var payment) ? payment : null;
        }

        public int Count() => byKey.Count;
    }
}
