using System.Text.RegularExpressions;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // The census, recoded by published rules.
    //
    // A census whose construct is "names a trajectory spanning subjects" is a coding task, and
    // a coding task needs three things a single reading cannot supply: a population rule that
    // reproduces the denominator, a coding rule a second coder can apply without judgment, and
    // the disagreements between coders reported rather than resolved in private. This bench is
    // all three at once: the rules below are the codebook, the code that applies them is a
    // second coder with no judgment to exercise, and the assertion at the bottom is the
    // agreement report. Anyone who re-runs it is a third coder.
    //
    // POPULATION (rule P). Two subsystems - the two repertoires the composed verb draws on:
    // the commerce system's ordering bounded context, and the meetings system's payments
    // module, both at the pinned corpus commits. Three layers:
    //   P1  domain events       declared in the subsystem's domain layer (its events folder,
    //                           or derivation from the corpus's own domain-event base)
    //   P2  integration events  the subsystem's integration vocabulary: events it DECLARES,
    //                           plus foreign events it HANDLES (a handler class named
    //                           <Event>Handler in its application layer)
    //   P3  commands            imperative artifacts named *Command in the subsystem's
    //                           application layer; handlers excluded; generic infrastructure
    //                           wrappers excluded (generic arity > 0); queries are another
    //                           mood and are out of population
    //
    // LEXICON (rule L). Per subsystem, mechanically derived from the corpus itself: the
    // aggregate-root names - directory names under the ordering domain's aggregates folder,
    // stripped of the "Aggregate" suffix; classes deriving AggregateRoot in the payments
    // domain. No name is added by hand.
    //
    // CODING (rule T1, mechanical). Unit of analysis: the artifact's identifier. Strip the
    // layer suffix (IntegrationEvent, DomainEvent, Event, Command); consume aggregate names
    // longest-first, left to right, at token boundaries (plural 's' included). The artifact
    // is coded T iff it references two or more DISTINCT aggregates of its own system - no
    // verb, no ordering, no judgment required.
    //
    // CODING (rule T2, manual residual). Rule T1 is generous within one syntactic form and
    // blind outside it: a trajectory can be named without naming any constituent - Checkout,
    // Onboarding, CompletePurchase - and a rule that counts aggregate references cannot see
    // that form. So every identifier T1 cannot see is handed to manual coding: those
    // referencing NO aggregate at all, and those bearing a word from the published process
    // lexicon below. Each manual coding is published with its reason, and a residual
    // identifier without a published coding FAILS the run - silence is not a coding.
    //
    // AGREEMENT. The author's reading coded every artifact S. Rule T1 is independent in the
    // one axis that matters for auditability - published rules, no discretion - and rule
    // T2's rows carry their reasons for any reader to dispute. With both codings all-S,
    // kappa is undefined (no marginal variance); what is reported instead is raw agreement,
    // the manual rows, the defeat condition, and the instrument itself.
    [TestClass]
    public class EveryNameStaysWithinOneSubjectsVocabularyLab
    {
        [TestMethod, TestCategory("Lab")]
        public void RecodingTheCensusByPublishedRulesFindsNoNameSpanningTwoSubjects()
        {
            var (eshopRoot, grzybekRoot) = LocateCorpus();

            // rule L - the lexicons, from the corpus itself
            var orderingAggregates = Directory.GetDirectories(Path.Combine(eshopRoot, "src", "Ordering.Domain", "AggregatesModel"))
                .Select(d => Path.GetFileName(d)!.Replace("Aggregate", ""))
                .ToList();
            var paymentsAggregates = CsFiles(Path.Combine(grzybekRoot, "src", "Modules", "Payments", "Domain"))
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"class\s+(\w+)\s*:\s*AggregateRoot").Select(m => m.Groups[1].Value))
                .Distinct()
                .ToList();

            var artifacts = new List<(string Name, string Layer, string System)>();

            // rule P1 - domain events
            foreach (var f in CsFiles(Path.Combine(eshopRoot, "src", "Ordering.Domain", "Events")))
            {
                artifacts.AddRange(Declarations(f).Select(n => (n, "domain event", "ordering")));
            }
            foreach (var f in CsFiles(Path.Combine(grzybekRoot, "src", "Modules", "Payments", "Domain")))
            {
                artifacts.AddRange(Regex.Matches(File.ReadAllText(f), @"class\s+(\w+)\s*:\s*DomainEventBase")
                    .Select(m => (m.Groups[1].Value, "domain event", "payments")));
            }

            // rule P2 - integration events: declared, plus handled foreigns
            var orderingDeclared = CsFiles(Path.Combine(eshopRoot, "src", "Ordering.API", "Application", "IntegrationEvents", "Events"))
                .SelectMany(Declarations).Where(n => n.EndsWith("IntegrationEvent")).ToList();
            var orderingHandled = CsFiles(Path.Combine(eshopRoot, "src", "Ordering.API", "Application", "IntegrationEvents", "EventHandling"))
                .SelectMany(Declarations)
                .Where(n => n.EndsWith("IntegrationEventHandler"))
                .Select(n => n.Substring(0, n.Length - "Handler".Length));
            artifacts.AddRange(orderingDeclared.Union(orderingHandled).Select(n => (n, "integration event", "ordering")));

            var paymentsDeclared = CsFiles(Path.Combine(grzybekRoot, "src", "Modules", "Payments", "IntegrationEvents"))
                .SelectMany(Declarations).Where(n => n.EndsWith("IntegrationEvent")).ToList();
            var paymentsHandled = CsFiles(Path.Combine(grzybekRoot, "src", "Modules", "Payments", "Application"))
                .SelectMany(Declarations)
                .Where(n => n.EndsWith("IntegrationEventHandler"))
                .Select(n => n.Substring(0, n.Length - "Handler".Length));
            artifacts.AddRange(paymentsDeclared.Union(paymentsHandled).Select(n => (n, "integration event", "payments")));

            // rule P3 - commands
            foreach (var f in CsFiles(Path.Combine(eshopRoot, "src", "Ordering.API", "Application", "Commands")))
            {
                artifacts.AddRange(DeclarationsWithArity(f)
                    .Where(d => d.Name.EndsWith("Command") && !d.Name.Contains("Handler") && d.GenericArity == 0)
                    .Select(d => (d.Name, "command", "ordering")));
            }
            foreach (var f in CsFiles(Path.Combine(grzybekRoot, "src", "Modules", "Payments", "Application")))
            {
                artifacts.AddRange(DeclarationsWithArity(f)
                    .Where(d => d.Name.EndsWith("Command") && !d.Name.Contains("Handler") && d.GenericArity == 0)
                    .Select(d => (d.Name, "command", "payments")));
            }

            artifacts = artifacts.Distinct().OrderBy(a => a.System).ThenBy(a => a.Layer).ThenBy(a => a.Name).ToList();

            // rule T1 - mechanical coding; rule T2 - the residual it cannot see goes to
            // manual coding, each row with its reason published below
            var coded = artifacts.Select(a =>
            {
                var lexicon = a.System == "ordering" ? orderingAggregates : paymentsAggregates;
                var referenced = AggregatesReferenced(a.Name, lexicon);
                bool residual = referenced.Count == 0
                                || AggregatesReferenced(a.Name, ProcessLexicon.ToList()).Count > 0;
                string code = referenced.Count >= 2 ? "T"
                            : residual ? "S*"
                            : "S";
                return (a.Name, a.Layer, a.System, Referenced: referenced, Code: code);
            }).ToList();

            Console.WriteLine();
            Console.WriteLine("=== the census, recoded by the published rules ===");
            Console.WriteLine();
            Console.WriteLine($"    lexicon, ordering : {string.Join(", ", orderingAggregates)}");
            Console.WriteLine($"    lexicon, payments : {string.Join(", ", paymentsAggregates)}");
            Console.WriteLine();
            foreach (var c in coded)
            {
                Console.WriteLine($"    {c.Code}  {c.System,-9} {c.Layer,-18} {c.Name}  [{string.Join("+", c.Referenced)}]");
            }
            Console.WriteLine();
            int domainEvents = coded.Count(c => c.Layer == "domain event");
            int integrationEvents = coded.Count(c => c.Layer == "integration event");
            int commands = coded.Count(c => c.Layer == "command");
            int trajectories = coded.Count(c => c.Code == "T");
            var residuals = coded.Where(c => c.Code == "S*").ToList();
            Console.WriteLine($"    population : {domainEvents} domain events, {integrationEvents} integration events, {commands} commands = {coded.Count}");
            Console.WriteLine($"    coded T by rule one (mechanical)             : {trajectories}");
            Console.WriteLine($"    handed to rule two (manual, S*)              : {residuals.Count}");
            foreach (var r in residuals)
            {
                Console.WriteLine($"        {r.Name,-42} -> {ManualCodes[r.Name].Code}: {ManualCodes[r.Name].Reason}");
            }
            Console.WriteLine($"    agreement with the author's reading (all S) : {coded.Count(c => c.Code != "T")}/{coded.Count}");
            Console.WriteLine();

            Assert.AreEqual(0, trajectories,
                "No artifact's name references two subjects' aggregates - rule one, mechanical.");

            foreach (var r in residuals)
            {
                Assert.IsTrue(ManualCodes.ContainsKey(r.Name),
                    $"Rule two found an identifier rule one cannot see, and no manual coding is "
                    + $"published for it: {r.Name}. Code it and state the reason - or it defeats the zero.");
                Assert.AreEqual("S", ManualCodes[r.Name].Code,
                    $"The published manual coding for {r.Name} is not S: the census's zero is defeated.");
            }

            Assert.AreEqual(27, domainEvents,
                "The population rule reproduces the domain-event denominator from the pinned corpus alone.");
            Assert.AreEqual(16, integrationEvents,
                "The population rule reproduces the integration-event denominator: declared plus handled.");
            Assert.AreEqual(29, commands,
                "The population rule reproduces the command denominator; handlers, generics and queries out.");
        }

        // Rule two's process lexicon: words that name a whole without naming any constituent -
        // the form rule one is blind to, and the form of the paper's own opening example
        // (CompletePurchase carries no aggregate noun). Author-supplied, published, and open:
        // extending it is part of the defeat condition.
        private static readonly string[] ProcessLexicon =
        {
            "Checkout", "Onboarding", "Fulfillment", "Purchase", "Process",
            "Flow", "Journey", "Pipeline", "Saga", "Workflow", "Trajectory", "Lifecycle",
        };

        // The manual codings for rule two's residual, each with its reason. A residual
        // identifier missing from this table fails the run: silence is not a coding.
        private static readonly Dictionary<string, (string Code, string Reason)> ManualCodes = new()
        {
            ["GracePeriodConfirmedIntegrationEvent"] =
                ("S", "confirms one wait-state of the ordering process; no second subject's operation is named"),
            ["MeetingAttendeeAddedIntegrationEvent"] =
                ("S", "one transition of another module's aggregate, which the subsystem merely bills"),
            ["NewUserRegisteredIntegrationEvent"] =
                ("S", "one transition of the user-access subject"),
        };

        private static List<string> AggregatesReferenced(string identifier, List<string> lexicon)
        {
            string name = identifier;
            foreach (var suffix in new[] { "IntegrationEvent", "DomainEvent", "Command", "Event" })
            {
                if (name.EndsWith(suffix))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                    break;
                }
            }

            var found = new List<string>();
            var byLength = lexicon.OrderByDescending(a => a.Length).ToList();
            int i = 0;
            while (i < name.Length)
            {
                string? hit = byLength.FirstOrDefault(a =>
                    i + a.Length <= name.Length
                    && string.CompareOrdinal(name, i, a, 0, a.Length) == 0
                    && HasTokenBoundary(name, i + a.Length));
                if (hit is not null)
                {
                    if (!found.Contains(hit))
                    {
                        found.Add(hit);
                    }
                    i += hit.Length;
                }
                else
                {
                    i++;
                }
            }
            return found;
        }

        // A match ends at a token boundary: end of the identifier, an uppercase or digit
        // start of the next token, or a plural 's' followed by one of those - so that
        // ExpireSubscriptions references Subscription rather than nothing.
        private static bool HasTokenBoundary(string name, int at)
        {
            if (at == name.Length || char.IsUpper(name[at]) || char.IsDigit(name[at]))
            {
                return true;
            }
            return name[at] == 's'
                && (at + 1 == name.Length || char.IsUpper(name[at + 1]) || char.IsDigit(name[at + 1]));
        }

        private static IEnumerable<string> CsFiles(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return Enumerable.Empty<string>();
            }
            return Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        }

        private static IEnumerable<string> Declarations(string file)
            => DeclarationsWithArity(file).Select(d => d.Name);

        private static IEnumerable<(string Name, int GenericArity)> DeclarationsWithArity(string file)
        {
            string text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"(?:record\s+(?:class|struct)|class|record)\s+(\w+)\s*(<[^>]+>)?"))
            {
                yield return (m.Groups[1].Value, m.Groups[2].Success ? 1 : 0);
            }
        }

        // The corpus roots: the clones fetch-corpus.ps1 makes (corpus-src/eshop, corpus-src/grzybek),
        // or the same repositories as siblings anywhere up the tree. No absolute path is assumed.
        private static (string eshop, string grzybek) LocateCorpus()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                foreach (var (e, g) in new[]
                {
                    (Path.Combine(dir.FullName, "corpus-src", "eshop"), Path.Combine(dir.FullName, "corpus-src", "grzybek")),
                    (Path.Combine(dir.FullName, "dotnet-eShop"), Path.Combine(dir.FullName, "kgrzybek-modular-monolith")),
                })
                {
                    if (Directory.Exists(Path.Combine(e, "src")) && Directory.Exists(Path.Combine(g, "src")))
                    {
                        return (e, g);
                    }
                }
                dir = dir.Parent;
            }
            throw new InvalidOperationException(
                "Corpus sources not found. Run fetch-corpus.ps1, or place the two pinned clones as ancestors' children.");
        }
    }
}
