using Puppeteer;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // The sibling lab found that an aggregate minting its own key with Guid.NewGuid() inside a
    // factory does not survive replay. The framework's answer to non-determinism is an Eval
    // parameter: it resolves once at the actor, freezes its value into the journal entry's
    // arguments, and on replay re-injects the frozen value instead of re-evaluating it.
    //
    // This lab asks whether that answer reaches this case, and reports what it finds rather
    // than what would be convenient.
    [TestClass]
    public class CapturingADomainMintedIdentityLab
    {
        [TestMethod, TestCategory("Lab")]
        public void AnEvalParameterCarriesAValue_NotAnAggregate()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"eval_capture_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            const string actorName = "eval_capture";

            var ordering = typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly;
            var payments = typeof(SubscriptionPaymentBridge).Assembly;

            try
            {
                var actor = new ActorV2(actorName, ordering, payments);
                actor.ConfigureStorage(DatabaseType.FileSystem, $"path={tempDir}");

                actor.Using("bridge = SubscriptionPaymentBridge();").PerformCommand();

                Console.WriteLine();
                Console.WriteLine("=== Can an Eval parameter carry the aggregate itself? ===");

                string carryingTheAggregate = null;
                try
                {
                    actor.Using("held = @bought;")
                    .WithParameters(p => {
                        p[Parameter.Eval, "bought", typeof(object)] =
                            "bridge.Buy('6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11', 'PL', 'Month', 50m, 'EUR')";
                    })
                    .PerformCommand();
                    carryingTheAggregate = "accepted";
                }
                catch (Exception ex)
                {
                    carryingTheAggregate = $"{ex.GetType().Name}: {ex.Message}";
                }
                Console.WriteLine($"  {carryingTheAggregate}");

                Console.WriteLine();
                Console.WriteLine("=== Can an Eval parameter carry a freshly minted identity? ===");

                string carryingTheIdentity;
                try
                {
                    string first = actor.Using("print @minted 'id';")
                        .WithParameters(p => {
                            p[Parameter.Eval, "minted", typeof(string)] = "Guid.NewGuid().ToString()";
                        })
                        .PerformQuery();
                    carryingTheIdentity = first;
                }
                catch (Exception ex)
                {
                    carryingTheIdentity = $"{ex.GetType().Name}: {ex.Message}";
                }
                Console.WriteLine($"  {carryingTheIdentity}");

                Console.WriteLine();
                Console.WriteLine("  What this decides: whether the repair for a domain that mints its own key");
                Console.WriteLine("  is available to the assembler, or has to be asked of the domain's author.");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
