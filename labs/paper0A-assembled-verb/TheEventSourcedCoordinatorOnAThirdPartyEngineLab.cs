using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Hosting;
using Orleans.Providers;
using Orleans.TestingHost;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // The event-sourced coordinator, measured on a third-party engine.
    //
    // The criterion's first coordination row - the event-sourced process manager whose own
    // journal holds transitions while the operations are other subjects' acts - was measured on
    // an author-built harness, labelled as mirroring the corpus's shape. An author-built
    // baseline deserves better than a label where a third-party measurement is
    // available, and one is: an Orleans JournaledGrain IS that shape, shipped - the grain
    // journals its own events, its state is reconstructed from that journal, and the operations
    // it coordinates are calls on other grains.
    //
    // So the same six-statement work runs here as an Orleans coordinator grain that advances
    // through six steps, journaling one transition per step in its own event log (the way the
    // corpus's sagas snapshot their fields), while six worker-grain calls perform the
    // operations. Then the engine's own record is read back through the framework's public
    // surface (RetrieveConfirmedEvents), and the grain is deactivated and reconstructed so the
    // engine's own replay runs.
    //
    //   (a) CONTENT   the coordinator's journal holds its transitions - progress marks. The
    //                 operations appear in it zero times: they were other subjects' acts.
    //   (b) REPLAY    reconstruction reads the journal and restores the coordinator's position;
    //                 no operation is re-performed - the worker grains' counters do not move.
    //
    // The row's zero therefore stops being a property of anyone's harness: measured on Orleans,
    // the shape itself puts nothing of the constitution in the coordinator's record.
    [TestClass]
    public class TheEventSourcedCoordinatorOnAThirdPartyEngineLab
    {
        internal static readonly ConcurrentDictionary<string, int> OperationRuns = new();

        [TestMethod, TestCategory("Lab")]
        public async Task TheCoordinatorsOwnJournalHoldsTransitionsAndItsReplayRePerformsNothing()
        {
            OperationRuns.Clear();

            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfig>();
            var cluster = builder.Build();
            await cluster.DeployAsync();

            try
            {
                var coordinator = cluster.GrainFactory.GetGrain<IPurchaseCoordinatorGrain>("purchase-1");
                await coordinator.RunPurchase();

                var journal = await coordinator.ReadOwnJournal();
                int transitions = journal.Count;
                // an entry would count as an operation of the act if it carried an invocation -
                // a statement with arguments to re-perform. Every entry here is a progress mark.
                int operationsInJournal = journal.Count(e => !e.StartsWith("step:", StringComparison.Ordinal));

                int performedBeforeReplay = OperationRuns.Values.Sum();

                // the engine's own replay: deactivate, then touch - the new activation
                // reconstructs the grain from its journaled events
                await coordinator.DeactivateSelf();
                var position = await coordinator.ReadPosition();
                int performedAfterReplay = OperationRuns.Values.Sum();

                Console.WriteLine();
                Console.WriteLine("=== the event-sourced coordinator, on a third-party engine (Orleans) ===");
                Console.WriteLine();
                Console.WriteLine($"    transitions in the coordinator's own journal : {transitions}");
                foreach (var e in journal) Console.WriteLine($"        {e}");
                Console.WriteLine($"    (a) operations of the act in that journal    : {operationsInJournal}");
                Console.WriteLine($"    operations performed, total                  : {performedBeforeReplay}");
                Console.WriteLine($"    (b) position after deactivate + reconstruct  : {position}");
                Console.WriteLine($"        operations re-performed by that replay   : {performedAfterReplay - performedBeforeReplay}");
                Console.WriteLine();

                Assert.AreEqual(6, transitions,
                    "The coordinator journaled one transition per step, in its own log.");

                Assert.AreEqual(0, operationsInJournal,
                    "And none of the act's operations is in it: the zero of the criterion's first "
                    + "row, measured on a third-party engine rather than on this author's harness.");

                Assert.AreEqual(6, performedBeforeReplay,
                    "The six operations were performed - as other grains' acts.");

                Assert.AreEqual("completed:6", position,
                    "Reconstruction restored the coordinator's position from its journal alone...");

                Assert.AreEqual(0, performedAfterReplay - performedBeforeReplay,
                    "...and re-performed no operation: the record can restore where the "
                    + "coordinator was, not what the act did.");
            }
            finally
            {
                await cluster.StopAllSilosAsync();
            }
        }

        private class SiloConfig : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder
                    .AddMemoryGrainStorageAsDefault()
                    .AddMemoryGrainStorage("LogStorage")
                    .AddLogStorageBasedLogConsistencyProvider("LogStorage");
            }
        }
    }

    public interface IPurchaseCoordinatorGrain : IGrainWithStringKey
    {
        Task RunPurchase();
        Task<List<string>> ReadOwnJournal();
        Task<string> ReadPosition();
        Task DeactivateSelf();
    }

    public interface IPurchaseStepGrain : IGrainWithStringKey
    {
        Task Perform(string step);
    }

    [GenerateSerializer]
    public class CoordinatorState
    {
        [Id(0)] public int StepsDone { get; set; }
        public void Apply(StepCompleted e) => StepsDone++;
    }

    [GenerateSerializer]
    public class StepCompleted
    {
        [Id(0)] public string Step { get; set; } = string.Empty;
    }

    // The coordinator: journals ITS transitions; the operations are other grains' acts.
    [LogConsistencyProvider(ProviderName = "LogStorage")]
    public class PurchaseCoordinatorGrain : JournaledGrain<CoordinatorState, StepCompleted>, IPurchaseCoordinatorGrain
    {
        private static readonly string[] Steps =
        {
            "BuySubscriptionPayment", "MarkPaymentPaid", "MakeAddress",
            "OpenOrder", "AddOrderItem", "KeepInRegister"
        };

        public async Task RunPurchase()
        {
            foreach (var step in Steps)
            {
                var worker = GrainFactory.GetGrain<IPurchaseStepGrain>(step);
                await worker.Perform(step);

                RaiseEvent(new StepCompleted { Step = step });
                await ConfirmEvents();
            }
        }

        public async Task<List<string>> ReadOwnJournal()
        {
            var events = await RetrieveConfirmedEvents(0, Version);
            return events.Select(e => $"step:{e.Step}:done").ToList();
        }

        public Task<string> ReadPosition() => Task.FromResult($"completed:{State.StepsDone}");

        public Task DeactivateSelf()
        {
            DeactivateOnIdle();
            return Task.CompletedTask;
        }
    }

    public class PurchaseStepGrain : Grain, IPurchaseStepGrain
    {
        public Task Perform(string step)
        {
            TheEventSourcedCoordinatorOnAThirdPartyEngineLab.OperationRuns
                .AddOrUpdate(step, 1, (_, v) => v + 1);
            return Task.CompletedTask;
        }
    }
}
