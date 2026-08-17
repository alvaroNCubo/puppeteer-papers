using System.Diagnostics;
using Puppeteer;
using Puppeteer.EventSourcing.DB;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // What the record costs, measured rather than waved at.
    //
    // The constitution arrangement pays for what it produces: every exercise becomes an entry,
    // the journal grows, rehydration replays it, and a performance through the plane costs more
    // than the same three statements as direct calls. A paper that prices none of that invites
    // the suspicion that the price is the point being hidden. So three numbers are measured,
    // against the direct baseline the criterion already uses:
    //
    //     growth      journal bytes per additional exercise, against the cost of the
    //                 first act (definition + first exercise) - reuse should be visibly
    //                 cheaper than restating, or the capability claim has no material side
    //     replay      wall time to reconstruct the subject from records of 100 and of
    //                 1,000 exercises - the price of (b) in the criterion
    //     throughput  exercises/second through the plane, live, against the same three
    //                 statements as direct in-memory calls that leave nothing behind
    //
    // The baseline comparison is asymmetric BY CONSTRUCTION and the asymmetry is the finding:
    // the direct loop buys execution only; the plane's loop buys execution plus a definition
    // exercised by reference, an attributed modality, and a record that reconstructs the
    // subject. The numbers price that difference; they do not rank the two.
    //
    // Method: one process, one machine, Stopwatch wall time, exercise counts N=100 and
    // N=1000, replay timed as the median of 3 cold reconstructions. Engine and harness build
    // configuration is printed with the numbers; the two corpus domain assemblies are consumed
    // as prebuilt Debug binaries throughout, identically in both arms.
    [TestClass]
    public class WhatTheRecordCostsBench
    {
        private const string PayerA = "6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11";

        private const string VerbScript = @"
                    payment = payments.Buy(@payerId, @country, @period, @amount, @currency);
                    payment.MarkAsPaid();
                    held.Keep(@key, payment);
                ";

        private const string CountQuery = "print held.Count() 'kept';";

        [TestMethod, TestCategory("Bench")]
        public void TheThreeCostsOfThePlane_GrowthReplayAndThroughput()
        {
            string root = Path.Combine(Path.GetTempPath(), "planecost_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var (bytesAt1_100, bytesAt100, replay100) = MeasureAt(root, "hundred", 100);
                var (bytesAt1_1000, bytesAt1000, liveSeconds, replay1000) = MeasureAt1000(root, "thousand");

                double marginal100 = (bytesAt100 - bytesAt1_100) / 99.0;
                double marginal1000 = (bytesAt1000 - bytesAt1_1000) / 999.0;

                double directSeconds = MeasureDirect(1000);

                double planeOpsPerSec = 1000.0 / liveSeconds;
                double directOpsPerSec = 1000.0 / directSeconds;

                Console.WriteLine();
                Console.WriteLine("=== what the record costs ===");
                Console.WriteLine();
                Console.WriteLine($"    build configuration (engine + harness)     : {BuildConfiguration()}");
                Console.WriteLine();
                Console.WriteLine($"    journal after definition + first exercise  : {bytesAt1_1000:N0} bytes");
                Console.WriteLine($"    marginal bytes per additional exercise      : {marginal1000:N0}  (N=1000; {marginal100:N0} at N=100)");
                Console.WriteLine();
                Console.WriteLine($"    replay, record of 100 exercises             : {replay100 * 1000:N0} ms   (median of 3, cold)");
                Console.WriteLine($"    replay, record of 1,000 exercises           : {replay1000 * 1000:N0} ms   (median of 3, cold)");
                Console.WriteLine();
                Console.WriteLine($"    through the plane, live                     : {planeOpsPerSec:N0} exercises/s  (N=1000)");
                Console.WriteLine($"    the same statements as direct calls         : {directOpsPerSec:N0} loops/s      (N=1000)");
                Console.WriteLine($"    price of definition + record + modality     : {directOpsPerSec / planeOpsPerSec:N1}x");
                Console.WriteLine();

                Assert.IsTrue(marginal1000 < bytesAt1_1000,
                    "An exercise must cost fewer bytes than the act that carried the definition: "
                    + "reuse has a material side, or the capability claim would not.");

                Assert.IsTrue(replay1000 > 0 && replay100 > 0 && liveSeconds > 0 && directSeconds > 0,
                    "All four clocks measured something.");
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }

        private (long bytesAt1, long bytesAtN, double replaySeconds) MeasureAt(string root, string name, int n)
        {
            string dir = Path.Combine(root, name);
            Directory.CreateDirectory(dir);
            var actor = NewActor(name, dir);
            actor.Using("payments = SubscriptionPaymentBridge(); held = PaymentStore();").PerformCommand();

            Exercise(actor, 0);
            long bytesAt1 = JournalBytes(dir, name);
            for (int i = 1; i < n; i++)
            {
                Exercise(actor, i);
            }
            long bytesAtN = JournalBytes(dir, name);

            Assert.IsTrue(actor.Using(CountQuery).PerformQuery().Contains($"\"kept\":{n}"));
            double replay = MedianColdReplay(name, dir, n);
            return (bytesAt1, bytesAtN, replay);
        }

        private (long bytesAt1, long bytesAtN, double liveSeconds, double replaySeconds) MeasureAt1000(string root, string name)
        {
            string dir = Path.Combine(root, name);
            Directory.CreateDirectory(dir);
            var actor = NewActor(name, dir);
            actor.Using("payments = SubscriptionPaymentBridge(); held = PaymentStore();").PerformCommand();

            var live = Stopwatch.StartNew();
            Exercise(actor, 0);
            long bytesAt1 = JournalBytes(dir, name);
            for (int i = 1; i < 1000; i++)
            {
                Exercise(actor, i);
            }
            live.Stop();
            long bytesAtN = JournalBytes(dir, name);

            Assert.IsTrue(actor.Using(CountQuery).PerformQuery().Contains("\"kept\":1000"));
            double replay = MedianColdReplay(name, dir, 1000);
            return (bytesAt1, bytesAtN, live.Elapsed.TotalSeconds, replay);
        }

        private double MedianColdReplay(string name, string dir, int expected)
        {
            var times = new List<double>();
            for (int run = 0; run < 3; run++)
            {
                var sw = Stopwatch.StartNew();
                var rehydrated = NewActor(name, dir);
                string result = rehydrated.Using(CountQuery).PerformQuery();
                sw.Stop();
                Assert.IsTrue(result.Contains($"\"kept\":{expected}"),
                    "Replay reconstructed every exercise before it was timed.");
                times.Add(sw.Elapsed.TotalSeconds);
            }
            times.Sort();
            return times[1];
        }

        private static double MeasureDirect(int n)
        {
            var payments = new SubscriptionPaymentBridge();
            var held = new PaymentStore();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < n; i++)
            {
                var payment = payments.Buy(PayerA, "PL", "Month", 50m, "EUR");
                payment.MarkAsPaid();
                held.Keep("purchase-" + i, payment);
            }
            sw.Stop();
            Assert.AreEqual(n, held.Count());
            return sw.Elapsed.TotalSeconds;
        }

        private static void Exercise(ActorV2 actor, int i)
        {
            actor.Using(VerbScript)
                .WithParameters(p =>
                {
                    p["payerId", typeof(string)] = PayerA;
                    p["country", typeof(string)] = "PL";
                    p["period", typeof(string)] = "Month";
                    p["amount", typeof(decimal)] = 50m;
                    p["currency", typeof(string)] = "EUR";
                    p["key", typeof(string)] = "purchase-" + i;
                })
                .PerformCommand();
        }

        private static ActorV2 NewActor(string actorName, string dir)
        {
            var actor = new ActorV2(actorName,
                typeof(CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments.SubscriptionPayment).Assembly,
                typeof(SubscriptionPaymentBridge).Assembly);
            actor.ConfigureStorage(DatabaseType.FileSystem, $"path={dir}");
            return actor;
        }

        private static long JournalBytes(string dir, string actorName)
        {
            string actorDir = Path.Combine(dir, actorName);
            if (!Directory.Exists(actorDir))
            {
                return 0;
            }
            return Directory.GetFiles(actorDir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
        }

        private static string BuildConfiguration()
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }
    }
}
