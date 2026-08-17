using System.Collections.Concurrent;
using System.Globalization;
using CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Hosting;
using Orleans.Providers;
using Orleans.TestingHost;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // The constitution arrangement, prototyped on a third-party engine.
    //
    // Every constitution measurement so far runs on one engine, and a single implementation
    // cannot separate two readings: "the assembly plane is a realizable arrangement" and
    // "one framework has this feature". The separation needs a second, independent
    // implementation of the plane - and the strongest substrate for it is the SAME engine
    // that already carries the coordination row, so that the substrate is held constant and
    // only the arrangement varies.
    //
    // So this lab implements the plane on Orleans, in the few hundred lines below: a
    // JournaledGrain whose journal holds def and act entries - a definition carries the
    // statements, an act carries a verb id and arguments - and whose state transition
    // INTERPRETS the act by re-executing the definition's statements against repertoires the
    // grain does not own. Reconstruction is the engine's own fold over its own journal, so
    // replay re-performs the operations by construction of the arrangement, not by a feature
    // of any framework.
    //
    // The same engine therefore realizes both cells of the criterion, and what decides the
    // cell is only what the journal holds and what the fold does with it:
    //
    //     coordination  (sibling lab)   journal holds transitions; fold restores position
    //     constitution  (this lab)      journal holds def + act;   fold re-performs
    //
    // Measured here, each against the engine's own record:
    //
    //     one definition, two exercises - the second states nothing of the constitution
    //     a query performance leaves the journal unmoved; a command advances it
    //     deactivate + reconstruct re-executes the statements (the operation count moves)
    //     the outcome exists again up to fresh values: the assembler's key finds the
    //     payment after replay, and the domain-minted identity differs per replay
    [TestClass]
    public class TheConstitutionPrototypedOnAThirdPartyEngineBench
    {
        [TestMethod, TestCategory("Bench")]
        public async Task TheSameThirdPartyEngineRealizesConstitutionWhenTheJournalHoldsTheDefinition()
        {
            PlaneInterpreter.StatementsExecuted.Clear();

            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<PlaneSiloConfig>();
            var cluster = builder.Build();
            await cluster.DeployAsync();

            try
            {
                var subject = cluster.GrainFactory.GetGrain<IAssemblyPlaneGrain>("subject-1");

                // the setup verb: the working objects, constituted as journaled content
                await subject.Define(1, 0, new List<PlaneStatement>
                {
                    new() { AssignTo = "payments", IsNew = true, Member = "SubscriptionPaymentBridge", Args = new() },
                    new() { AssignTo = "held", IsNew = true, Member = "PaymentStore", Args = new() },
                });
                await subject.PerformCommand(1, new List<string>());

                // the composed verb: operations of repertoires the subject does not own
                await subject.Define(2, 6, new List<PlaneStatement>
                {
                    new() { AssignTo = "p", Receiver = "payments", Member = "Buy",
                            Args = new() { "@0", "@1", "@2", "@3", "@4" } },
                    new() { Receiver = "p", Member = "MarkAsPaid", Args = new() },
                    new() { Receiver = "held", Member = "Keep", Args = new() { "@5", "$p" } },
                });

                // drawn on twice, with different arguments
                await subject.PerformCommand(2, new List<string>
                    { "6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11", "PL", "Month", "d:50", "EUR", "purchase-1" });
                await subject.PerformCommand(2, new List<string>
                    { "2b8d4e6f-1a3c-4d5e-9f0b-7c2a4e6d8b33", "PL", "Month", "d:30", "EUR", "purchase-2" });

                var shapeAfterCommands = await subject.JournalShape();
                int journalAfterCommands = shapeAfterCommands.Count;
                int definitionsOfTheVerb = shapeAfterCommands.Count(e => e == "def(2)");

                // a query verb: defined like any other, and performed as a query - the
                // definition enters the journal, its query performances do not
                await subject.Define(3, 0, new List<PlaneStatement>
                {
                    new() { AssignTo = "n", Receiver = "held", Member = "Count", Args = new() },
                });
                int journalBeforeQuery = (await subject.JournalShape()).Count;
                string kept = await subject.PerformQuery(3, new List<string>());
                int journalAfterQuery = (await subject.JournalShape()).Count;

                string liveIdentity = await subject.ReadPaymentIdentity("purchase-1");
                int executedLive = PlaneInterpreter.StatementsExecuted.Values.Sum();

                // the engine's own replay: deactivate, then touch - reconstruction folds the
                // journal, and folding an act entry is re-performing its statements
                await subject.DeactivateSelf();
                string keptAfterReplay = await subject.PerformQuery(3, new List<string>());
                string replayedIdentity = await subject.ReadPaymentIdentity("purchase-1");
                int executedAfterReplay = PlaneInterpreter.StatementsExecuted.Values.Sum();

                Console.WriteLine();
                Console.WriteLine("=== the constitution arrangement, on the coordination row's own engine ===");
                Console.WriteLine();
                Console.WriteLine($"    journal entries after two exercises        : {journalAfterCommands}");
                foreach (var e in shapeAfterCommands) Console.WriteLine($"        {e}");
                Console.WriteLine($"    definitions of the composed verb           : {definitionsOfTheVerb}");
                Console.WriteLine($"    journal before / after a QUERY performance : {journalBeforeQuery} / {journalAfterQuery}   unmoved");
                Console.WriteLine($"    kept, live                                 : {kept}");
                Console.WriteLine($"    statements executed, live                  : {executedLive}");
                Console.WriteLine($"    statements re-executed by reconstruction   : {executedAfterReplay - executedLive}");
                Console.WriteLine($"    kept, after deactivate + reconstruct       : {keptAfterReplay}");
                Console.WriteLine($"    the assembler's key, across replay         : finds a payment both times");
                Console.WriteLine($"    the domain-minted identity, across replay  : {(liveIdentity == replayedIdentity ? "same" : "a different value")}");
                Console.WriteLine();

                Assert.AreEqual(1, definitionsOfTheVerb,
                    "Two exercises, one definition, in a third-party engine's own journal.");

                Assert.AreEqual(5, journalAfterCommands,
                    "The journal holds exactly the definitions and the exercises: def(setup), "
                    + "act(setup), def(verb), act(verb), act(verb).");

                Assert.AreEqual(journalBeforeQuery, journalAfterQuery,
                    "The query performance executed and left the journal unmoved: modality decides "
                    + "journal membership, on this engine as on the other.");

                Assert.AreEqual("2", kept, "Both exercises performed the composition.");

                Assert.AreEqual(executedLive, executedAfterReplay - executedLive,
                    "Reconstruction re-performed every journaled statement: the record alone "
                    + "brought the outcome about again.");

                Assert.AreEqual("2", keptAfterReplay,
                    "And the outcome exists again after replay, from the journal alone.");

                Assert.AreNotEqual(liveIdentity, replayedIdentity,
                    "Up to fresh values, exactly as the criterion states it: the repertoire mints "
                    + "its identity inside a factory, so re-performance re-mints - the same "
                    + "boundary, met on a second engine.");
            }
            finally
            {
                await cluster.StopAllSilosAsync();
            }
        }

        private class PlaneSiloConfig : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder
                    .AddMemoryGrainStorageAsDefault()
                    .AddMemoryGrainStorage("PlaneLog")
                    .AddLogStorageBasedLogConsistencyProvider("PlaneLog");
            }
        }
    }

    public interface IAssemblyPlaneGrain : IGrainWithStringKey
    {
        Task Define(int verbId, int paramCount, List<PlaneStatement> body);
        Task PerformCommand(int verbId, List<string> args);
        Task<string> PerformQuery(int verbId, List<string> args);
        Task<List<string>> JournalShape();
        Task<string> ReadPaymentIdentity(string key);
        Task DeactivateSelf();
    }

    [GenerateSerializer]
    public class PlaneStatement
    {
        [Id(0)] public string? AssignTo { get; set; }
        [Id(1)] public string? Receiver { get; set; }
        [Id(2)] public string Member { get; set; } = string.Empty;
        [Id(3)] public bool IsNew { get; set; }
        [Id(4)] public List<string> Args { get; set; } = new();
    }

    [GenerateSerializer]
    public abstract class PlaneEvent
    {
    }

    [GenerateSerializer]
    public class VerbDefined : PlaneEvent
    {
        [Id(0)] public int VerbId { get; set; }
        [Id(1)] public int ParamCount { get; set; }
        [Id(2)] public List<PlaneStatement> Body { get; set; } = new();
    }

    [GenerateSerializer]
    public class VerbPerformed : PlaneEvent
    {
        [Id(0)] public int VerbId { get; set; }
        [Id(1)] public List<string> Args { get; set; } = new();
    }

    // The state is the fold. A definition extends the verb table; an act re-executes the
    // defined statements - so reconstruction from the journal re-performs by construction.
    // The copier below is copy-by-reference: the state holds live domain objects, and what
    // persists is never the state - only the journal, which rebuilds it.
    public class AssemblyPlaneState
    {
        public Dictionary<int, List<PlaneStatement>> Verbs { get; } = new();
        public Dictionary<string, object> Env { get; } = new();

        public void Apply(VerbDefined e) => Verbs[e.VerbId] = e.Body;

        public void Apply(VerbPerformed e) => PlaneInterpreter.Execute(Verbs[e.VerbId], e.Args, Env);
    }

    [RegisterCopier]
    public sealed class AssemblyPlaneStateCopier : Orleans.Serialization.Cloning.IDeepCopier<AssemblyPlaneState>
    {
        public AssemblyPlaneState DeepCopy(AssemblyPlaneState input, Orleans.Serialization.Cloning.CopyContext context) => input;
    }

    [LogConsistencyProvider(ProviderName = "PlaneLog")]
    public class AssemblyPlaneGrain : JournaledGrain<AssemblyPlaneState, PlaneEvent>, IAssemblyPlaneGrain
    {
        public async Task Define(int verbId, int paramCount, List<PlaneStatement> body)
        {
            RaiseEvent(new VerbDefined { VerbId = verbId, ParamCount = paramCount, Body = body });
            await ConfirmEvents();
        }

        public async Task PerformCommand(int verbId, List<string> args)
        {
            RaiseEvent(new VerbPerformed { VerbId = verbId, Args = args });
            await ConfirmEvents();
        }

        public Task<string> PerformQuery(int verbId, List<string> args)
        {
            var result = PlaneInterpreter.Execute(State.Verbs[verbId], args, State.Env);
            return Task.FromResult(result?.ToString() ?? string.Empty);
        }

        public async Task<List<string>> JournalShape()
        {
            var events = await RetrieveConfirmedEvents(0, Version);
            return events.Select(e => e switch
            {
                VerbDefined d => $"def({d.VerbId})",
                VerbPerformed a => $"act({a.VerbId})",
                _ => "?",
            }).ToList();
        }

        public Task<string> ReadPaymentIdentity(string key)
        {
            var held = (PaymentStore)State.Env["held"];
            SubscriptionPayment payment = held.Find(key);
            return Task.FromResult(payment.GetSnapshot().Id.Value.ToString());
        }

        public Task DeactivateSelf()
        {
            DeactivateOnIdle();
            return Task.CompletedTask;
        }
    }

    // ~80 lines of interpretation are the whole plane: statements are content, arguments are
    // either supplied values (@i), bound names ($x) or literals, and execution is reflection
    // against repertoires the subject does not own.
    public static class PlaneInterpreter
    {
        internal static readonly ConcurrentDictionary<string, int> StatementsExecuted = new();

        private static readonly Dictionary<string, Type> Constructible = new()
        {
            ["SubscriptionPaymentBridge"] = typeof(SubscriptionPaymentBridge),
            ["PaymentStore"] = typeof(PaymentStore),
        };

        public static object? Execute(List<PlaneStatement> body, List<string> args, Dictionary<string, object> env)
        {
            object? last = null;
            foreach (var statement in body)
            {
                last = ExecuteOne(statement, args, env);
                StatementsExecuted.AddOrUpdate(statement.Member, 1, (_, v) => v + 1);
            }
            return last;
        }

        private static object? ExecuteOne(PlaneStatement statement, List<string> args, Dictionary<string, object> env)
        {
            object? result;
            if (statement.IsNew)
            {
                result = Activator.CreateInstance(Constructible[statement.Member]);
            }
            else
            {
                object receiver = env[statement.Receiver!];
                var method = receiver.GetType().GetMethods()
                    .Single(m => m.Name == statement.Member && m.GetParameters().Length == statement.Args.Count);
                var parameters = method.GetParameters();
                object?[] resolved = new object?[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    resolved[i] = Resolve(statement.Args[i], args, env, parameters[i].ParameterType);
                }
                result = method.Invoke(receiver, resolved);
            }

            if (statement.AssignTo is not null && result is not null)
            {
                env[statement.AssignTo] = result;
            }
            return result;
        }

        private static object? Resolve(string arg, List<string> args, Dictionary<string, object> env, Type target)
        {
            if (arg.StartsWith("@", StringComparison.Ordinal))
            {
                return Convert(args[int.Parse(arg.Substring(1), CultureInfo.InvariantCulture)], target);
            }
            if (arg.StartsWith("$", StringComparison.Ordinal))
            {
                return env[arg.Substring(1)];
            }
            return Convert(arg, target);
        }

        private static object? Convert(string value, Type target)
        {
            if (value.StartsWith("d:", StringComparison.Ordinal))
            {
                return decimal.Parse(value.Substring(2), CultureInfo.InvariantCulture);
            }
            return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
    }
}
