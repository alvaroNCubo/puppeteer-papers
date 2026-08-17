using System.Reflection;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // What is actually holding each repertoire together.
    //
    // The previous lab partitioned each domain into disjoint repertoires and found one dominant
    // connected mass in each. This one asks a harder question about that mass: WHICH EDGES ARE
    // HOLDING IT TOGETHER, and would it still be one repertoire without them?
    //
    // The criterion is not invented here. Domain-driven design already says that an aggregate
    // should reference another aggregate BY IDENTITY, never by object reference - so an edge
    // where a type names another AGGREGATE ROOT is, by that rule's own standard, the edge that
    // should not have been there. In the published dissection of a three-domain example the
    // same shape is what makes the split fail to compile: a warehouse receives a whole cart to
    // read a list of product ids, and an accounting ledger receives the same cart to read three
    // decimals. Neither needed the body. Both received it.
    //
    //     A wire is a dependency that could have been a value.
    //
    // So this lab computes the partition twice - once with every edge, once with the edges that
    // cross an aggregate root removed - and reports whether the mass survives as one repertoire
    // or falls apart into the sets it was made of.
    //
    // Removing an edge here is not a proposal to change anyone's code. It is a question asked
    // of the type graph: is this repertoire one thing, or several things wired together?
    //
    // ★ WHAT THE FIVE EDGES OF THE FIRST DOMAIN TURN OUT TO BE, checked one by one in its
    // sources rather than inferred from the graph:
    //
    //   Order —— Buyer                        Order declares BOTH forms, on adjacent lines:
    //                                           public int? BuyerId { get; private set; }
    //                                           public Buyer Buyer { get; }
    //                                         The identity the rule prescribes is already
    //                                         there. The body sits beside it - and it is an
    //                                         object-relational NAVIGATION PROPERTY, wired in
    //                                         OrderEntityTypeConfiguration:
    //                                           orderConfiguration.HasOne(o => o.Buyer)
    //                                                             .HasForeignKey(o => o.BuyerId);
    //                                         The domain never assigns it. It is persistence,
    //                                         living in the SHAPE of the domain rather than in
    //                                         its declared ports - and the file that configures
    //                                         it is one the persistence rule already deleted.
    //
    //   OrderStarted —— Order                 three events carrying a whole aggregate so that
    //   OrderCancelled —— Order               a handler somewhere else can read it. Every other
    //   OrderShipped —— Order                 member of those events is already a primitive:
    //   BuyerAndPaymentMethodVerified —— Buyer  seven primitives, and one body.
    //
    // So the five edges are one persistence wire and four workflow wires - which is exactly the
    // list the dissection's rules were written against, met here one layer deeper: not in the
    // call sites of the domain, nor in the ports it declares, but in the shape of its types.
    [TestClass]
    public class WhatHoldsARepertoireTogetherBench
    {
        [TestMethod, TestCategory("Bench")]
        public void RemovingTheEdgesThatCarryAnAggregateSplitsTheRepertoire()
        {
            Report("eShop Ordering.Domain",
                typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly);

            Report("Grzybek Payments.Domain",
                typeof(CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments.SubscriptionPayment).Assembly);
        }

        private static void Report(string label, Assembly assembly)
        {
            var domain = Admitted(assembly);
            var roots = domain.Where(IsAggregateRoot).ToHashSet();
            var all = Edges(domain, includeAggregateCrossings: true);
            var structural = Edges(domain, includeAggregateCrossings: false);

            var before = Components(domain, all);
            var after = Components(domain, structural);

            Console.WriteLine();
            Console.WriteLine($"=== {label} ===");
            Console.WriteLine($"    aggregate roots: {roots.Count}  ({string.Join(", ", roots.Select(r => r.Name).OrderBy(n => n))})");
            Console.WriteLine($"    edges in total: {all.Count}");
            Console.WriteLine($"    of those, edges that carry an aggregate root: {all.Count - structural.Count}");
            Console.WriteLine();
            Console.WriteLine($"    repertoires WITH those edges:    {before.Count}");
            Console.WriteLine($"    repertoires WITHOUT them:        {after.Count}");
            Console.WriteLine();

            var biggestBefore = before.OrderByDescending(c => c.Count).First();
            Console.WriteLine($"    the dominant mass held {biggestBefore.Count} types. Without the crossing edges it");
            Console.WriteLine($"    falls into:");
            foreach (var c in after.Where(c => c.Any(t => biggestBefore.Contains(t)))
                                   .OrderByDescending(c => c.Count).Take(8))
                Console.WriteLine($"        [{c.Count,2}]  {string.Join(", ", c.Select(t => t.Name).OrderBy(n => n).Take(9))}"
                                  + (c.Count > 9 ? $", … (+{c.Count - 9})" : ""));

            Console.WriteLine();
            Console.WriteLine("    The edges removed, one line each - each is a place where a type receives or");
            Console.WriteLine("    holds another aggregate whole, and DDD's own rule says it should have been an");
            Console.WriteLine("    identity:");
            foreach (var (a, b) in all.Except(structural).Take(14))
                Console.WriteLine($"        {a.Name} —— {b.Name}");

            // Is that set of arcs a MINIMAL disconnecting set? Removing all of them separates the
            // mass; the question is whether every one of them was needed for that. For each arc,
            // put it back alone and see whether the partition is coarser than with all of them
            // gone. If it is, that arc was carrying a join by itself.
            var carriers = all.Except(structural).ToList();
            if (carriers.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("    minimality: is every one of them needed to separate the clusters?");
                int needed = 0;
                foreach (var kept in carriers)
                {
                    var allButThisOne = new HashSet<(Type, Type)>(structural) { kept };
                    int n = Components(domain, allButThisOne).Count;
                    bool isNeeded = n < after.Count;
                    if (isNeeded) needed++;
                    Console.WriteLine($"        put back {kept.Item1.Name} → {kept.Item2.Name,-24} components {n}"
                                      + (isNeeded ? "   NEEDED" : "   redundant"));
                }
                Console.WriteLine($"        {needed} of {carriers.Count} are individually necessary");
            }

            Assert.IsTrue(after.Count >= before.Count,
                "Removing arcs can only split, never merge.");
        }

        private static bool IsAggregateRoot(Type t)
        {
            for (var b = t; b != null; b = b.BaseType)
                if (b.Name is "AggregateRoot" or "Entity" && b != t && b.Name == "AggregateRoot") return true;
            return t.GetInterfaces().Any(i => i.Name == "IAggregateRoot")
                   || (t.BaseType != null && t.BaseType.Name == "AggregateRoot");
        }

        private static List<Type> Admitted(Assembly assembly)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
            return types
                .Where(t => (t.IsPublic || t.IsNestedPublic || (t.IsNotPublic && !t.IsNestedPrivate))
                            && ((t.IsClass && t.IsSubclassOf(typeof(object))) || t.IsEnum))
                .ToList();
        }

        // Arcs, not edges: the direction is kept. See DisjointRepertoiresBench for why.
        private static HashSet<(Type, Type)> Edges(List<Type> domain, bool includeAggregateCrossings)
        {
            var inDomain = new HashSet<Type>(domain);
            var roots = domain.Where(IsAggregateRoot).ToHashSet();
            var edges = new HashSet<(Type, Type)>();

            void Link(Type from, Type to)
            {
                foreach (var t in Unwrap(to))
                {
                    if (!inDomain.Contains(t) || t == from) continue;
                    // the edge carries an aggregate root when the thing named is one, and the
                    // namer is not that aggregate itself
                    bool carriesAggregate = roots.Contains(t);
                    if (!includeAggregateCrossings && carriesAggregate) continue;
                    edges.Add((from, t));
                }
            }

            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var type in domain)
            {
                foreach (var f in type.GetFields(all)) Link(type, f.FieldType);
                foreach (var p in type.GetProperties(all)) Link(type, p.PropertyType);
                foreach (var c in type.GetConstructors(all))
                    foreach (var p in c.GetParameters()) Link(type, p.ParameterType);
                foreach (var m in type.GetMethods(all))
                {
                    Link(type, m.ReturnType);
                    foreach (var p in m.GetParameters()) Link(type, p.ParameterType);
                }
            }
            return edges;
        }

        private static IEnumerable<Type> Unwrap(Type t)
        {
            if (t == null) yield break;
            if (t.IsByRef || t.IsArray || t.IsPointer)
            {
                foreach (var inner in Unwrap(t.GetElementType())) yield return inner;
                yield break;
            }
            yield return t;
            if (t.IsGenericType)
                foreach (var arg in t.GetGenericArguments())
                    foreach (var inner in Unwrap(arg)) yield return inner;
        }

        private static List<List<Type>> Components(List<Type> nodes, HashSet<(Type, Type)> edges)
        {
            var adjacency = nodes.ToDictionary(n => n, _ => new List<Type>());
            foreach (var (a, b) in edges) { adjacency[a].Add(b); adjacency[b].Add(a); }

            var seen = new HashSet<Type>();
            var components = new List<List<Type>>();
            foreach (var start in nodes)
            {
                if (!seen.Add(start)) continue;
                var component = new List<Type>();
                var stack = new Stack<Type>();
                stack.Push(start);
                while (stack.Count > 0)
                {
                    var n = stack.Pop();
                    component.Add(n);
                    foreach (var next in adjacency[n]) if (seen.Add(next)) stack.Push(next);
                }
                components.Add(component);
            }
            return components;
        }
    }
}
