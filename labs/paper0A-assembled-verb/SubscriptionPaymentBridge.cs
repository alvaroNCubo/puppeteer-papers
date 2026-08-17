using CompanyName.MyMeetings.Modules.Payments.Domain.Payers;
using CompanyName.MyMeetings.Modules.Payments.Domain.PriceListItems;
using CompanyName.MyMeetings.Modules.Payments.Domain.PriceListItems.PricingStrategies;
using CompanyName.MyMeetings.Modules.Payments.Domain.SeedWork;
using CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments;
using CompanyName.MyMeetings.Modules.Payments.Domain.Subscriptions;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // A bridge, not an assembler. It reaches ONE domain and composes nothing: every method
    // here produces a single aggregate of that domain, and no method of this type names a
    // type of any other domain.
    //
    // It exists because two of that domain's inputs do not cross the parameter plane, which
    // carries primitives: its identity is a Guid, and its factory takes a generic list. The
    // other domain in this lab needs no bridge at all - its aggregate is reachable from the
    // DSL directly, because its constructor takes primitives, one value object the DSL can
    // construct, and a DateTime the runtime already injects as @Now.
    //
    // The asymmetry is a fact about the two domains' surfaces, not about the two domains'
    // quality, and it is reported rather than smoothed over.
    public class SubscriptionPaymentBridge
    {
        public SubscriptionPayment Buy(
            string payerId,
            string countryCode,
            string periodCode,
            decimal amount,
            string currency)
        {
            var period = SubscriptionPeriod.Of(periodCode);
            var price = MoneyValue.Of(amount, currency);

            var offer = new PriceListItemData(countryCode, period, price, PriceListItemCategory.New);
            var items = new List<PriceListItemData> { offer };
            var priceList = PriceList.Create(items, new DirectValueFromPriceListPricingStrategy(items));

            // R_99 — the identity this act will carry.
            //
            // The number is outside the rules' numbering on purpose. A rule's hole marks a
            // dependence the dissection removed; this one marks something no rule produced and
            // no rule can fill: the domain's author decided that this factory mints its own key,
            // and the assembler has no way to supply one, because Buy accepts none.
            //
            // It is left named by number rather than by intent for the same reason the rules'
            // holes are. Calling it NewInvoiceId, or NewPaymentId, would be deciding for the
            // author what he already decided himself - and what he decided is legible without
            // help: an aggregate that names itself, for a caller that would store what came back.
            //
            // WHAT KIND OF THING THAT CALL IS, which is the part worth reading twice.
            //
            // Guid.NewGuid() is not computation. It is RECEPTION: a value entering the
            // computation that did not arrive through any declared channel. An actor's next
            // state is meant to be a function of the message it received and the state it
            // held; a value drawn from the machine is a message with no sender, and the model
            // has no account of it. So this is not a quirk to be worked around - it is an
            // undeclared inbound.
            //
            // The framework already treats one member of that category correctly, and the
            // precedent is what makes the reading principled rather than convenient: @Now is
            // a clock reading, sampled once at the invocation edge, journaled, and re-injected
            // on replay rather than re-taken. A minted identity is the same category of value
            // and has no such door. That is why the repair is an Eval parameter and not a hook:
            // an Eval resolves once, freezes into the entry's arguments, and replay reuses it.
            //
            // Which relocates the fault. It is not that the assembler cannot reach inside the
            // piece. It is that the piece takes an input without declaring it, and a caller
            // that must reproduce the act cannot supply what it was never asked for.
            //
            // The consequence is measured in RetrievalIsALookupLab: this identity does not
            // survive replay, while the key the assembler supplies for the same aggregate does.
            return SubscriptionPayment.Buy(new PayerId(Guid.Parse(payerId)), period, countryCode, price, priceList);
        }
    }
}
