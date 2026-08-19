using System.Collections.Concurrent;
using DurableTask.Core;
using DurableTask.Core.History;
using DurableTask.Core.Middleware;
using DurableTask.Emulator;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // The coordination arm, measured on a third-party engine.
    //
    // The sibling lab's coordination arm is author-built, and it mirrors one shape of
    // coordination only - the event-sourced process manager, whose own stream holds
    // transitions and no operations. Coordination as a class is wider than that
    // shape: a durable-execution orchestration JOURNALS ITS INVOCATIONS, so its record answers
    // question (a) with the full count, not zero. If the criterion of the sibling lab only
    // separates the arrangements because the coordination arm was configured into its weakest
    // form, the criterion decides nothing.
    //
    // So this lab runs the coordination arm on the Durable Task Framework - the engine
    // underneath Azure Durable Functions - using its in-memory emulator, and reports whatever
    // comes out. The same six-statement work is expressed the way that model expresses work:
    // an orchestration that schedules six activities.
    //
    // Two things are measured, and neither is this author's to configure:
    //
    //   (a) CONTENT   the orchestration's own history, read back from the engine, holds the
    //                 six scheduled invocations - names, inputs, results. The answer is 6,
    //                 not 0, and the earlier arm's 0 is thereby shown to be a property of one
    //                 coordination shape rather than of the class.
    //
    //   (b) REPLAY    the engine's replay re-executes THE ORCHESTRATOR'S CODE against the
    //                 recorded history; it does not re-perform the operations. That is not a
    //                 deficiency - it is the model's own determinism contract, the reason its
    //                 orchestrations may not read clocks or mint identifiers. Measured here:
    //                 the orchestrator body executed in multiple replay episodes; every
    //                 activity executed exactly once.
    //
    // Which refines what the criterion separates. Against this arm, content does not separate
    // constitution from coordination - both records hold all six statements. What separates
    // them is (b)'s input clause - WHERE THE DEFINITION LIVES: here the composition is compiled
    // code, and the history holds exercises and outcomes that only that code can re-walk;
    // change the code and the same history replays against a different composition. In the
    // constitution arm the definition itself is an entry, and replaying the record alone -
    // the repertoires held fixed, as the criterion fixes them - re-performs the operations.
    [TestClass]
    public class TheCoordinationArmOnAThirdPartyEngineLab
    {
        internal static int OrchestratorEpisodes;
        internal static int ReplayedEpisodes;
        internal static readonly ConcurrentDictionary<string, int> ActivityRuns = new();
        internal static readonly ConcurrentBag<string> Performed = new();
        internal static volatile IReadOnlyList<string> LastCarriedHistory = Array.Empty<string>();

        [TestMethod, TestCategory("Lab")]
        public async Task TheThirdPartyRecordHoldsTheInvocationsAndItsReplayDoesNotRePerformThem()
        {
            OrchestratorEpisodes = 0;
            ReplayedEpisodes = 0;
            ActivityRuns.Clear();
            while (Performed.TryTake(out _)) { }

            var service = new LocalOrchestrationService();
            var worker = new TaskHubWorker(service);

            // The engine's own record, read through the framework's public dispatch
            // middleware: on every episode the engine hands the orchestrator the
            // OrchestrationRuntimeState it persisted - the history it replays from.
            // Nothing here is this author's bookkeeping; the list below is what the
            // third-party engine itself carried into the final episode.
            worker.AddOrchestrationDispatcherMiddleware(async (DispatchMiddlewareContext ctx, Func<Task> next) =>
            {
                var rs = ctx.GetProperty<OrchestrationRuntimeState>();
                if (rs != null)
                    LastCarriedHistory = rs.Events
                        .Select(e => e is TaskScheduledEvent t ? $"TaskScheduled:{t.Name}" : e.EventType.ToString())
                        .ToList();
                await next();
            });

            worker.AddTaskOrchestrations(typeof(PurchaseOrchestration));
            worker.AddTaskActivities(
                typeof(BuySubscriptionPaymentActivity), typeof(MarkPaymentPaidActivity),
                typeof(MakeAddressActivity), typeof(OpenOrderActivity),
                typeof(AddOrderItemActivity), typeof(KeepInRegisterActivity));
            await worker.StartAsync();

            try
            {
                var client = new TaskHubClient(service);
                var instance = await client.CreateOrchestrationInstanceAsync(
                    typeof(PurchaseOrchestration), "purchase-1");
                var state = await client.WaitForOrchestrationAsync(instance, TimeSpan.FromSeconds(90));

                Assert.AreEqual(OrchestrationStatus.Completed, state.OrchestrationStatus,
                    "The orchestration ran to completion on the third-party engine.");

                var history = LastCarriedHistory;
                int scheduled = history.Count(e => e.StartsWith("TaskScheduled:", StringComparison.Ordinal));

                Console.WriteLine();
                Console.WriteLine("=== the coordination arm, on the engine underneath Durable Functions ===");
                Console.WriteLine();
                Console.WriteLine($"    (a) invocations in the history the engine carried   : {scheduled}");
                foreach (var e in history.Where(e => e.StartsWith("TaskScheduled:", StringComparison.Ordinal)))
                    Console.WriteLine($"        {e}");
                Console.WriteLine($"        operations performed, by the activities        : {Performed.Count}");
                Console.WriteLine($"    (b) orchestrator episodes (its code, re-executed)  : {OrchestratorEpisodes}");
                Console.WriteLine($"        of those, replay episodes                      : {ReplayedEpisodes}");
                foreach (var kv in ActivityRuns.OrderBy(k => k.Key))
                    Console.WriteLine($"        {kv.Key,-28} executed {kv.Value} time(s)");
                Console.WriteLine();
                Console.WriteLine("    The record holds all six invocations - content does not separate this");
                Console.WriteLine("    arm from constitution. Its replay re-executed the orchestrator's CODE");
                Console.WriteLine("    in every episode and re-performed no operation: the definition lives in");
                Console.WriteLine("    the program, and the history can only be re-walked by it.");
                Console.WriteLine();

                Assert.AreEqual(6, scheduled,
                    "The third-party record holds the six invocations. The earlier arm's zero is a "
                    + "property of the event-sourced process-manager shape, not of coordination as a "
                    + "class, and the paper must say so.");

                Assert.AreEqual(6, Performed.Count,
                    "Each operation was performed exactly once in total...");

                Assert.IsTrue(ActivityRuns.Values.All(v => v == 1),
                    "...and no activity was re-executed by replay.");

                Assert.IsTrue(OrchestratorEpisodes > 1 && ReplayedEpisodes > 0,
                    "While the orchestrator's code ran in multiple episodes, replayed against the "
                    + "recorded history - the model's own determinism contract, measured.");
            }
            finally
            {
                await worker.StopAsync(true);
            }
        }

        private static int CountOccurrences(string text, string needle)
        {
            int count = 0, at = 0;
            while (!string.IsNullOrEmpty(text) && (at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }
            return count;
        }

        // ── the orchestration: the six statements, the way this model writes them ───────────
        public class PurchaseOrchestration : TaskOrchestration<string, string>
        {
            public override async Task<string> RunTask(OrchestrationContext context, string input)
            {
                Interlocked.Increment(ref OrchestratorEpisodes);
                if (context.IsReplaying) Interlocked.Increment(ref ReplayedEpisodes);

                string payment = await context.ScheduleTask<string>(typeof(BuySubscriptionPaymentActivity), input);
                string paid    = await context.ScheduleTask<string>(typeof(MarkPaymentPaidActivity), payment);
                string address = await context.ScheduleTask<string>(typeof(MakeAddressActivity), input);
                string order   = await context.ScheduleTask<string>(typeof(OpenOrderActivity), address);
                string item    = await context.ScheduleTask<string>(typeof(AddOrderItemActivity), order);
                string kept    = await context.ScheduleTask<string>(typeof(KeepInRegisterActivity), paid);
                return kept;
            }
        }

        // ── the activities: each performs its piece once and records that it ran ────────────
        private static string Ran(string name)
        {
            ActivityRuns.AddOrUpdate(name, 1, (_, v) => v + 1);
            Performed.Add(name);
            return name + ":done";
        }

        public class BuySubscriptionPaymentActivity : TaskActivity<string, string>
        {
            protected override string Execute(TaskContext context, string input) => Ran("BuySubscriptionPayment");
        }
        public class MarkPaymentPaidActivity : TaskActivity<string, string>
        {
            protected override string Execute(TaskContext context, string input) => Ran("MarkPaymentPaid");
        }
        public class MakeAddressActivity : TaskActivity<string, string>
        {
            protected override string Execute(TaskContext context, string input) => Ran("MakeAddress");
        }
        public class OpenOrderActivity : TaskActivity<string, string>
        {
            protected override string Execute(TaskContext context, string input) => Ran("OpenOrder");
        }
        public class AddOrderItemActivity : TaskActivity<string, string>
        {
            protected override string Execute(TaskContext context, string input) => Ran("AddOrderItem");
        }
        public class KeepInRegisterActivity : TaskActivity<string, string>
        {
            protected override string Execute(TaskContext context, string input) => Ran("KeepInRegister");
        }
    }
}
