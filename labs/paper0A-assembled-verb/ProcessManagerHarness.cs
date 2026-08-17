using System.Text;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // A minimal, faithful event-sourced process manager, built to the pattern's own
    // documentation rather than to what would be convenient here.
    //
    // Two statements govern the shape, and both are quoted from the pattern's references so
    // that nothing below is this lab's invention:
    //
    //   "State is persisted as a sequence of auto-generated transition events in the event
    //    store. After each handler runs, the framework captures a snapshot of the process
    //    manager's fields and appends it to the PM's own stream."
    //                                                     - Protean, Process Managers
    //
    //   "Process managers issue commands, not mutations. Handlers in a process manager issue
    //    commands ... to trigger actions in other aggregates."
    //                                                     - Protean, Process Managers
    //
    //   "It has a correlation field - the identifier that ties incoming Events to the right
    //    Process Manager instance. And it has a lifecycle: the conditions under which a new
    //    instance starts, and the conditions under which an existing instance ends."
    //                                                     - Event-Sourced Domain Modeling
    //
    // Everything here follows from those three: aggregates own their own streams and raise
    // their own events; the process manager owns a stream of its own field snapshots; it
    // reaches other aggregates by issuing commands, never by mutating them.
    //
    // The comparison this supports is ontological, not evaluative. Both arrangements work.
    // The question is what each one's record is made of.
    //
    // ---------------------------------------------------------------------------------------
    // READ THIS ARM WITH DISTRUST, AND HERE IS THE LIST.
    //
    // The other arm of the comparison is a real system doing real work: its record is produced
    // by the runtime, and a domain-minted identity was caught changing across replay, which is
    // proof that the replay really re-executes. THIS arm is written here, by the party making
    // the argument, and that difference matters:
    //
    //   1. The assertion that the coordinator's stream holds no domain operation is an
    //      assertion ABOUT THIS FILE. It shows the harness is faithful to the documentation
    //      quoted above; it does not show that the world is this way.
    //   2. The transition events emit field snapshots, which is what the documentation
    //      prescribes and also the least flattering of the plausible variants. A real system
    //      might emit semantic entries - PaymentConfirmed, PurchaseCompleted - which would
    //      read better. It would not change where the operations end up, but it would change
    //      how this output looks.
    //   3. The flow is called inline rather than driven by a bus. Nothing about the streams
    //      depends on that, but it is a simplification.
    //   4. Counting streams as subjects is a modelling choice made here.
    //
    // What does NOT depend on this file: the three quotes above, and the structural fact they
    // state - a process manager issues commands, so the operations it causes are raised by
    // other aggregates into other streams. That is the pattern describing itself.
    //
    // Status, applied to this lab with the same severity used on any specimen written to make
    // a point: THIS ARM IS AN ILLUSTRATION OF WHAT THE DOCUMENTATION PRESCRIBES, NOT A
    // MEASUREMENT OF A SYSTEM FOUND IN THE WILD. Neither reference implementation dissected
    // for this work contains a process manager or a saga, so no such measurement was available
    // to take.
    // ---------------------------------------------------------------------------------------

    internal sealed record Recorded(string Stream, string Type, string Payload);

    internal sealed class EventStore
    {
        private readonly List<Recorded> entries = new();

        internal void Append(string stream, string type, string payload) =>
            entries.Add(new Recorded(stream, type, payload));

        internal IReadOnlyList<Recorded> All => entries;

        internal IReadOnlyList<Recorded> Stream(string stream) =>
            entries.Where(e => e.Stream == stream).ToList();

        internal IEnumerable<string> Streams => entries.Select(e => e.Stream).Distinct();

        internal string Render()
        {
            var sb = new StringBuilder();
            foreach (var stream in Streams)
            {
                sb.AppendLine($"    stream '{stream}'  ({Stream(stream).Count} entries)");
                foreach (var e in Stream(stream))
                    sb.AppendLine($"        {e.Type,-34} {e.Payload}");
            }
            return sb.ToString();
        }
    }

    // An aggregate that performs a domain operation and raises its own event into its own
    // stream. This is where the operations of the act end up in this arrangement.
    internal sealed class PaymentAggregate
    {
        private readonly EventStore store;
        internal PaymentAggregate(EventStore store) => this.store = store;

        internal string Buy(string correlation, string payerId, string country, string period,
                            decimal amount, string currency)
        {
            var payment = new SubscriptionPaymentBridge().Buy(payerId, country, period, amount, currency);
            string stream = $"payment-{correlation}";
            store.Append(stream, "SubscriptionPaymentCreated", $"correlation={correlation}");
            return stream;
        }

        internal void MarkAsPaid(string correlation) =>
            store.Append($"payment-{correlation}", "SubscriptionPaymentPaid", $"correlation={correlation}");
    }

    internal sealed class OrderAggregate
    {
        private readonly EventStore store;
        internal OrderAggregate(EventStore store) => this.store = store;

        internal void PlaceWelcomeKit(string correlation, string userId, string userName, string sku)
        {
            // The real domain operations, so that both arrangements perform the same work and
            // the comparison is about where the record of it lands - not about one arm doing
            // less than the other.
            var address = new eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Address(
                "street", "city", "state", "PL", "12345");
            var order = new eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order(
                userId, userName, address, 1, "1234-5678-9012-3456", "123", "Card Holder",
                DateTime.UtcNow.AddYears(1), null, null);
            order.AddOrderItem(41, sku, 0m, 0m, string.Empty, 1);

            string stream = $"order-{correlation}";
            store.Append(stream, "OrderStarted", $"correlation={correlation}");
            store.Append(stream, "OrderItemAdded", $"sku={sku}");
        }
    }

    // The process manager. Correlation field, state, lifecycle, and a stream of its own field
    // snapshots. It issues commands; it never performs a domain operation itself.
    internal sealed class PurchaseProcessManager
    {
        private readonly EventStore store;
        private readonly PaymentAggregate payments;
        private readonly OrderAggregate orders;

        // state
        private string correlation;
        private bool paymentCreated;
        private bool paymentPaid;
        private bool orderPlaced;
        private bool finished;

        internal PurchaseProcessManager(EventStore store, PaymentAggregate payments, OrderAggregate orders)
        {
            this.store = store;
            this.payments = payments;
            this.orders = orders;
        }

        private string Stream => $"purchase-process-{correlation}";

        // "the framework captures a snapshot of the process manager's fields and appends it to
        // the PM's own stream"
        private void AppendTransition(string trigger) =>
            store.Append(Stream, "ProcessManagerTransition",
                $"on={trigger} paymentCreated={paymentCreated} paymentPaid={paymentPaid} "
                + $"orderPlaced={orderPlaced} finished={finished}");

        // the starting condition: without one, the instance has no way to come into being
        internal void OnPurchaseRequested(string correlation, string payerId, string country,
                                          string period, decimal amount, string currency,
                                          string userId, string userName, string sku)
        {
            this.correlation = correlation;
            AppendTransition("PurchaseRequested");

            payments.Buy(correlation, payerId, country, period, amount, currency);
            OnPaymentCreated(userId, userName, sku);
        }

        private void OnPaymentCreated(string userId, string userName, string sku)
        {
            paymentCreated = true;
            AppendTransition("SubscriptionPaymentCreated");

            payments.MarkAsPaid(correlation);
            OnPaymentPaid(userId, userName, sku);
        }

        private void OnPaymentPaid(string userId, string userName, string sku)
        {
            paymentPaid = true;
            AppendTransition("SubscriptionPaymentPaid");

            orders.PlaceWelcomeKit(correlation, userId, userName, sku);
            OnOrderPlaced();
        }

        // the ending condition: without one, the instance would never finish
        private void OnOrderPlaced()
        {
            orderPlaced = true;
            finished = true;
            AppendTransition("OrderPlaced");
        }
    }
}
