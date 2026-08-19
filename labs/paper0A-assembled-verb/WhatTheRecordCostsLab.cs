using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
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
    // the direct loop buys execution only - it performs the same three statements and
    // allocates the same domain objects - while the plane's loop buys execution plus a
    // definition exercised by reference, an attributed modality, and a record that
    // reconstructs the subject. The numbers price that difference; they do not rank the two.
    //
    // Method: one process, one machine, Stopwatch wall time. Journal bytes are exact counts,
    // not samples. Every timed quantity is measured over n=10 runs and reported as
    // median [min..max]: ten cold reconstructions per journal size (fresh actor each time,
    // process warm), ten separately built 1,000-exercise journals for live throughput, ten
    // direct loops. The throughput multiplier is computed from the same two medians the
    // table prints. The environment - OS, runtime, processor, GC mode, build configuration -
    // is printed with the numbers; the two corpus domain assemblies are consumed as prebuilt
    // Debug binaries throughout, identically in both arms.
    [TestClass]
    public class WhatTheRecordCostsLab
    {
        private const int Runs = 10;

        private const string PayerA = "6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11";

        private const string VerbScript = @"
                    payment = payments.Buy(@payerId, @country, @period, @amount, @currency);
                    payment.MarkAsPaid();
                    held.Keep(@key, payment);
                ";

        private const string CountQuery = "print held.Count() 'kept';";

        private sealed record Sample(double Median, double Min, double Max)
        {
            public static Sample Of(List<double> values)
            {
                values.Sort();
                double median = values.Count % 2 == 1
                    ? values[values.Count / 2]
                    : (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2.0;
                return new Sample(median, values[0], values[^1]);
            }

            public string Ms() => $"{Median * 1000:N1} ms  [{Min * 1000:N1}..{Max * 1000:N1}]";

            public string PerSecond(int n) => $"{n / Median:N0}/s  [{n / Max:N0}..{n / Min:N0}]";
        }

        [TestMethod, TestCategory("Lab")]
        public void TheThreeCostsOfThePlane_GrowthReplayAndThroughput()
        {
            string root = Path.Combine(Path.GetTempPath(), "planecost_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                // growth - exact bytes, from one journal of each size
                var (bytesAt1_100, bytesAt100) = BuildJournal(root, "growth100", 100, out _);
                var (bytesAt1_1000, bytesAt1000) = BuildJournal(root, "growth1000", 1000, out _);
                double marginal100 = (bytesAt100 - bytesAt1_100) / 99.0;
                double marginal1000 = (bytesAt1000 - bytesAt1_1000) / 999.0;

                // replay - ten cold reconstructions per journal size
                var replay100 = Sample.Of(ColdReplays("growth100", Path.Combine(root, "growth100"), 100));
                var replay1000 = Sample.Of(ColdReplays("growth1000", Path.Combine(root, "growth1000"), 1000));

                // throughput through the plane - ten separately built 1,000-exercise journals
                var liveTimes = new List<double>();
                for (int run = 0; run < Runs; run++)
                {
                    BuildJournal(root, $"live{run}", 1000, out double seconds);
                    liveTimes.Add(seconds);
                }
                var live = Sample.Of(liveTimes);

                // the same statements as direct calls - ten loops
                var directTimes = new List<double>();
                for (int run = 0; run < Runs; run++)
                {
                    directTimes.Add(MeasureDirect(1000));
                }
                var direct = Sample.Of(directTimes);

                double multiplier = live.Median / direct.Median;

                Console.WriteLine();
                Console.WriteLine("=== what the record costs ===");
                Console.WriteLine();
                Console.WriteLine($"    build configuration (engine + harness)     : {BuildConfiguration()}");
                Console.WriteLine($"    runtime                                    : {RuntimeInformation.FrameworkDescription}");
                Console.WriteLine($"    OS                                         : {RuntimeInformation.OSDescription}");
                Console.WriteLine($"    processor                                  : {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unspecified"} x{Environment.ProcessorCount}");
                Console.WriteLine($"    GC                                         : {(GCSettings.IsServerGC ? "server" : "workstation")}, {GCSettings.LatencyMode}");
                Console.WriteLine($"    runs per timed quantity                    : {Runs}, median [min..max]");
                Console.WriteLine();
                Console.WriteLine($"    journal after definition + first exercise  : {bytesAt1_1000:N0} bytes (exact)");
                Console.WriteLine($"    marginal bytes per additional exercise      : {marginal1000:N0}  (N=1000; {marginal100:N0} at N=100; exact)");
                Console.WriteLine();
                Console.WriteLine($"    replay, record of 100 exercises             : {replay100.Ms()}");
                Console.WriteLine($"    replay, record of 1,000 exercises           : {replay1000.Ms()}");
                Console.WriteLine();
                Console.WriteLine($"    through the plane, live (1,000 exercises)   : {live.PerSecond(1000)}");
                Console.WriteLine($"    the same statements as direct calls          : {direct.PerSecond(1000)}");
                Console.WriteLine($"    price of definition + record + modality      : {multiplier:N1}x  (ratio of the two medians above)");
                Console.WriteLine();

                Assert.IsTrue(marginal1000 < bytesAt1_1000,
                    "An exercise must cost fewer bytes than the act that carried the definition: "
                    + "reuse has a material side, or the capability claim would not.");

                Assert.IsTrue(replay1000.Median > 0 && replay100.Median > 0 && live.Median > 0 && direct.Median > 0,
                    "All four clocks measured something.");
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }

        private static (long bytesAt1, long bytesAtN) BuildJournal(string root, string name, int n, out double liveSeconds)
        {
            string dir = Path.Combine(root, name);
            Directory.CreateDirectory(dir);
            var actor = NewActor(name, dir);
            actor.Using("payments = SubscriptionPaymentBridge(); held = PaymentStore();").PerformCommand();

            var sw = Stopwatch.StartNew();
            Exercise(actor, 0);
            long bytesAt1 = JournalBytes(dir, name);
            for (int i = 1; i < n; i++)
            {
                Exercise(actor, i);
            }
            sw.Stop();
            liveSeconds = sw.Elapsed.TotalSeconds;
            long bytesAtN = JournalBytes(dir, name);

            Assert.IsTrue(actor.Using(CountQuery).PerformQuery().Contains($"\"kept\":{n}"));
            return (bytesAt1, bytesAtN);
        }

        private static List<double> ColdReplays(string name, string dir, int expected)
        {
            var times = new List<double>();
            for (int run = 0; run < Runs; run++)
            {
                var sw = Stopwatch.StartNew();
                var rehydrated = NewActor(name, dir);
                string result = rehydrated.Using(CountQuery).PerformQuery();
                sw.Stop();
                Assert.IsTrue(result.Contains($"\"kept\":{expected}"),
                    "Replay reconstructed every exercise before it was timed.");
                times.Add(sw.Elapsed.TotalSeconds);
            }
            return times;
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
