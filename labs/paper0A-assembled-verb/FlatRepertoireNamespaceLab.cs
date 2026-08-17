using System.Reflection;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // The repertoire is indexed by simple type name.
    //
    // When a puppet draws on more than one domain, every type each of them admits enters one
    // flat index keyed by `Type.Name`, case-insensitively. Where two domains use the same
    // simple name, the first assembly registered wins and the second name is not reachable
    // from the DSL at all. Nothing reports it.
    //
    // This lab measures how much of that happens between two domains that were written
    // independently by different authors, and says which names are affected.
    //
    // It also records why the sibling composition lab did not meet the problem: there the
    // second domain is reached entirely through a bridge, so its types are returned as
    // instances and never enter the index. Mediation avoids the collision by accident, not by
    // design - which is worth knowing before anyone concludes the flat index is harmless.
    [TestClass]
    public class FlatRepertoireNamespaceLab
    {
        [TestMethod, TestCategory("Lab")]
        public void TwoIndependentlyAuthoredDomainsCollideOnSimpleNames()
        {
            var ordering = typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly;
            var payments = typeof(CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments.SubscriptionPayment).Assembly;
            var buildingBlocks = typeof(CompanyName.MyMeetings.BuildingBlocks.Domain.Entity).Assembly;

            var byAssembly = new (string Name, List<string> Types)[]
            {
                (ordering.GetName().Name, Admitted(ordering)),
                (payments.GetName().Name, Admitted(payments)),
                (buildingBlocks.GetName().Name, Admitted(buildingBlocks)),
            };

            Console.WriteLine();
            Console.WriteLine("=== The repertoire is one flat index, keyed by simple name ===");
            foreach (var (name, types) in byAssembly)
                Console.WriteLine($"  {types.Count,4} admitted types   {name}");

            var seen = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (assemblyName, types) in byAssembly)
                foreach (var type in types)
                {
                    if (!seen.TryGetValue(type, out var owners))
                        seen[type] = owners = new List<string>();
                    if (!owners.Contains(assemblyName)) owners.Add(assemblyName);
                }

            var collisions = seen.Where(e => e.Value.Count > 1)
                                 .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                                 .ToList();

            Console.WriteLine();
            Console.WriteLine($"  names claimed by more than one assembly: {collisions.Count}");
            Console.WriteLine();
            foreach (var c in collisions)
                Console.WriteLine($"    {c.Key,-34} {string.Join(" | ", c.Value)}");

            Console.WriteLine();
            Console.WriteLine("  Each of these resolves to whichever assembly was registered first. The other");
            Console.WriteLine("  is unreachable by name from the DSL, and no diagnostic is raised: the index");
            Console.WriteLine("  keeps both under FindClassesByName and returns the first from the lookup the");
            Console.WriteLine("  script uses.");

            Assert.IsTrue(byAssembly.All(a => a.Types.Count > 0),
                "Each domain assembly must admit types into the repertoire.");
        }

        // The same visibility filter the runtime applies when it ingests an assembly:
        // classes and enums that are public, nested public, or internal at the top level.
        private static List<string> Admitted(Assembly assembly)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

            return types
                .Where(t => (t.IsPublic || t.IsNestedPublic || (t.IsNotPublic && !t.IsNestedPrivate))
                            && ((t.IsClass && t.IsSubclassOf(typeof(object))) || t.IsEnum))
                .Select(t => t.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
