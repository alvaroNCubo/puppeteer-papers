using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;

namespace BenchPaper2Bdn
{
    // Copy of UnitTestEShopOnPuppeteer/OrderingFacade.cs (CompleteOrder verb) so the
    // BenchmarkDotNet harness carries the same public, MIT-licensed eShop Order surface.
    // One DSL dispatch cascades through Address VO construction, the Order 10-arg ctor
    // (status = Submitted), 3× AddOrderItem, and a four-step state-machine walk to Shipped.
    public class OrderingFacade
    {
        public Order CompleteOrder(string userId, string userName, int productId, decimal unitPrice, int units)
        {
            var address = new Address("street", "city", "state", "country", "12345");
            var order = new Order(userId, userName, address, 1, "1234-5678-9012-3456",
                                  "123", "Card Holder", DateTime.UtcNow.AddYears(1), null, null);
            order.AddOrderItem(productId,     "item-1", unitPrice,      0m, "", units);
            order.AddOrderItem(productId + 1, "item-2", unitPrice * 2m, 0m, "", units);
            order.AddOrderItem(productId + 2, "item-3", unitPrice * 3m, 0m, "", units);
            order.SetAwaitingValidationStatus();
            order.SetStockConfirmedStatus();
            order.SetPaidStatus();
            order.SetShippedStatus();
            return order;
        }
    }
}
