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
    // CODING (rule T). Unit of analysis: the artifact's identifier. Tokenize on uppercase
    // boundaries; strip the layer suffix (IntegrationEvent, DomainEvent, Event, Command);
    // consume aggregate names longest-first, left to right. The artifact is coded T -
    // "names a trajectory spanning subjects" - iff it references two or more DISTINCT
    // aggregates of its own system. Otherwise S: it stays within one subject's vocabulary.
    // Referencing zero aggregates codes S: the name stays within the vocabulary of its
    // declaring subject.
    //
    // The rule is deliberately generous to T: it requires no verb, no ordering, no claim
    // about operations - two subjects' nouns in one name suffice. If the census's zero
    // survives a rule this permissive, the zero is not an artifact of strict coding.
    //
    // AGREEMENT. The author's reading coded every artifact S. This coder is independent in
    // the one axis that matters for auditability - it applies published rules with no
    // discretion - and its full row-by-row output is printed for any reader to dispute.
    // With both codings all-S, kappa is undefined (no marginal variance); what is reported
    // instead is raw agreement, the defeat condition, and the instrument itself.
    [TestClass]
    public class EveryNameStaysWithinOneSubjectsVocabularyBench
    {
        [TestMethod, TestCategory("Bench")]
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

            // rule T - the coding, and the agreement report
            var coded = artifacts.Select(a =>
            {
                var lexicon = a.System == "ordering" ? orderingAggregates : paymentsAggregates;
                var referenced = AggregatesReferenced(a.Name, lexicon);
                return (a.Name, a.Layer, a.System, Referenced: referenced, Code: referenced.Count >= 2 ? "T" : "S");
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
            Console.WriteLine($"    population : {domainEvents} domain events, {integrationEvents} integration events, {commands} commands = {coded.Count}");
            Console.WriteLine($"    coded T    : {trajectories}");
            Console.WriteLine($"    agreement with the author's reading (all S): {coded.Count(c => c.Code == "S")}/{coded.Count}");
            Console.WriteLine();

            Assert.AreEqual(0, trajectories,
                "No artifact's name references two subjects' aggregates - under a rule generous "
                + "to the opposite finding. One such name is the census's cheap defeat.");

            Assert.AreEqual(coded.Count, coded.Count(c => c.Code == "S"),
                "The mechanical coder agrees with the author's reading on every artifact.");

            Assert.AreEqual(27, domainEvents,
                "The population rule reproduces the domain-event denominator from the pinned corpus alone.");
            Assert.AreEqual(16, integrationEvents,
                "The population rule reproduces the integration-event denominator: declared plus handled.");
            Assert.AreEqual(29, commands,
                "The population rule reproduces the command denominator; handlers, generics and queries out.");
        }

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
                    && (i + a.Length == name.Length || char.IsUpper(name[i + a.Length]) || char.IsDigit(name[i + a.Length])));
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
