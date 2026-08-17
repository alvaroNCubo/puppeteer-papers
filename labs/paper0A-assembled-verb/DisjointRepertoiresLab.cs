using System.Reflection;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // Counting sets instead of arrows.
    //
    // Everything measured so far counted call sites: how many operations an act invokes, how
    // many holes a rule opened, how many entries a record holds. Those are ARROWS, and arrows
    // answer a different question from the one that decides whether an act is assembled.
    //
    // What decides it is whether the types the act touches fall into SETS THAT DO NOT
    // INTERSECT. Two repertoires are disjoint when no type of one names a type of the other -
    // in a field, a property, a parameter, a return, or a base type. That is a property of the
    // type graph, and it has nothing to do with where the assembly boundary was drawn:
    //
    //   - one assembly can hold several disjoint repertoires, and a system that puts every
    //     endpoint in one giant library still has them;
    //   - and a boundary drawn between two clusters that DO reference each other does not make
    //     them disjoint - it makes them fail to compile, which is what the published attempt at
    //     splitting a three-domain example into three projects demonstrates.
    //
    // So an earlier scope decision here - "one module is one repertoire" - was an assumption
    // dressed as a measurement. This lab computes the connected components of each domain's
    // own type graph and reports what is actually there.
    [TestClass]
    public class DisjointRepertoiresLab
    {
        [TestMethod, TestCategory("Lab")]
        public void EachDomainAssemblyIsPartitionedIntoItsDisjointRepertoires()
        {
            Report("eShop Ordering.Domain",
                typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly);

            Report("Grzybek Payments.Domain",
                typeof(CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments.SubscriptionPayment).Assembly);
        }

        private static void Report(string label, Assembly assembly)
        {
            var domain = Admitted(assembly);
            var arcs = ReferenceArcs(domain);
            var components = ConnectedComponents(domain, arcs);

            Console.WriteLine();
            Console.WriteLine($"=== {label} ===");
            Console.WriteLine($"    types considered: {domain.Count}   (base-type inheritance not counted as a link)");
            Console.WriteLine($"    reference arcs between them: {arcs.Count}   (directed; components taken undirected)");
            Console.WriteLine($"    DISJOINT REPERTOIRES (connected components): {components.Count}");
            Console.WriteLine();

            foreach (var c in components.OrderByDescending(c => c.Count))
            {
                var names = c.Select(t => t.Name).OrderBy(n => n).ToList();
                Console.WriteLine($"      [{names.Count,2}]  {string.Join(", ", names.Take(12))}"
                                  + (names.Count > 12 ? $", … (+{names.Count - 12})" : ""));
            }

            // cross-folder edges: which types tie one author-declared area to another
            foreach (var (a, b) in arcs)
            {
                string fa = a.Namespace?.Split('.').Last() ?? "?";
                string fb = b.Namespace?.Split('.').Last() ?? "?";
                if (fa != fb) Console.WriteLine($"XEDGE|{label}|{fa}.{a.Name}|{fb}.{b.Name}");
            }

            // machine-readable map, so an act can be joined against the partition
            Console.WriteLine();
            int id = 0;
            foreach (var c in components.OrderByDescending(c => c.Count))
            {
                foreach (var t in c) Console.WriteLine($"MAP	{label}	{id}	{t.Name}");
                id++;
            }

            Assert.IsTrue(components.Count >= 1, "A domain assembly has at least one repertoire.");
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

        // An ARC from X to Y when Y appears anywhere in X's declared surface. Generic arguments
        // count, so a List<OrderItem> gives an arc from Order to OrderItem.
        //
        // The direction is kept rather than normalised away. "X names Y" is not "Y names X", and
        // discarding that in a study about what a piece is in a POSITION TO KNOW would throw away
        // the very asymmetry being measured. Components below are then taken over the underlying
        // undirected graph, which yields the stronger statement: between two components there is
        // not merely no direct reference, there is NO CHAIN OF DECLARED REFERENCES connecting
        // them at all, of any length, in either direction.
        //
        // What that is good for, stated at its actual strength: the separation cannot be
        // dismissed as a missing direct reference whose surrounding type structure already
        // bridges the two. It is NOT a claim about epistemic reach. If A names B and B names C,
        // nothing measured here puts A in a position to hold a fact about C - nominal
        // connectivity is not transitive knowledge, and this lab does not measure knowledge.
        private static HashSet<(Type From, Type To)> ReferenceArcs(List<Type> domain)
        {
            var inDomain = new HashSet<Type>(domain);
            var edges = new HashSet<(Type, Type)>();

            void Link(Type from, Type to)
            {
                foreach (var t in Unwrap(to))
                    if (inDomain.Contains(t) && t != from)
                        edges.Add((from, t));
            }

            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic
                                     | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var type in domain)
            {
                // Base types are deliberately NOT linked, and the reason is the question rather
                // than the answer it produces. If two aggregates both derive from a shared
                // Entity, each names Entity; NEITHER THEREBY NAMES THE OTHER:
                //
                //     A ──▶ Entity ◀── B     does not give     A ──▶ B
                //
                // The relation measured here is direct nominal reachability between the pieces
                // whose separation is in question. Counting ancestry as an arc between
                // descendants would answer a different question - whether two types share an
                // abstraction - and it is the first that bears on whether either is in a
                // position to hold a fact about the other.
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

        private static List<List<Type>> ConnectedComponents(List<Type> nodes, HashSet<(Type From, Type To)> arcs)
        {
            var adjacency = nodes.ToDictionary(n => n, _ => new List<Type>());
            foreach (var (a, b) in arcs)
            {
                adjacency[a].Add(b);
                adjacency[b].Add(a);
            }

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
                    foreach (var next in adjacency[n])
                        if (seen.Add(next)) stack.Push(next);
                }
                components.Add(component);
            }
            return components;
        }
    }
}
