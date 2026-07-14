namespace Lab05L3InprocSymmetric
{
    // Minimal shopping-cart domain for Lab 3 — the everyday business shape: a shopper
    // ADDs items to an order one at a time (each a separate Action), then CHECKS OUT the
    // whole order once (the single group-close). One workload cycle is one order:
    // ItemsPerOrder Adds followed by one Checkout. Driven as compiled Actions so the
    // reaction patterns [_:Cart].Add($order,$item) / [_:Cart].Checkout($order) match
    // Actions (Rule 1: reactions match Actions, not legacy Scripts). State is trivial:
    // the lab asserts on reaction output + journal parity, not on domain state.
    public class Cart
    {
        public int Added { get; private set; }
        public int CheckedOut { get; private set; }
        public void Add(string order, string item) { Added++; }
        public void Checkout(string order) { CheckedOut++; }
    }
}
