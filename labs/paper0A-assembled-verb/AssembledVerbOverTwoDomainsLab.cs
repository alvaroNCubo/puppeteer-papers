using Puppeteer;
using System.Reflection;
using System.Text;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // One verb assembled from two domains that were written independently of each other and
    // of this lab.
    //
    // The two assemblies are consumed as prebuilt binaries: neither was recompiled here,
    // neither references the other, and no type declared in either appears in the other's
    // surface. Whether they can be composed at all is therefore not a question about their
    // source - there is no source in this build.
    //
    // What the lab reports is not that the composition executes. A test can execute a
    // composition, and one already does in each domain's own repository, with no assembler
    // present at all. What it reports is where the composed act ends up: whether any single
    // artifact holds it, and whether it appears in a record as one unit.
    [TestClass]
    public class AssembledVerbOverTwoDomainsLab
    {
        [TestMethod, TestCategory("Lab")]
        public void OneVerbComposesTwoIndependentlyAuthoredDomains_AndTheRecordHoldsItAsOneAct()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"assembled_verb_two_domains_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            const string actorName = "assembled_verb_two_domains";

            var ordering = typeof(eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order).Assembly;
            var payments = typeof(SubscriptionPaymentBridge).Assembly;

            try
            {
                var actor = new ActorV2(actorName, ordering, payments);
                actor.ConfigureStorage(DatabaseType.FileSystem, $"path={tempDir}");

                actor.Using("payments = SubscriptionPaymentBridge();").PerformCommand();

                // The assembled verb. Four domain operations over two domains, written from the
                // only position in this arrangement from which both are reachable without either
                // reaching the other.
                //
                // NOT "the only artifact in the build that names both", which is what this said
                // before and is false: the line above hands the actor both assemblies. The claim
                // is about the position the composition can be WRITTEN FROM, not about exclusive
                // mention.
                actor.Using(@"
                    payment = payments.Buy(@payerId, @country, @period, @amount, @currency);
                    payment.MarkAsPaid();
                    address = Address(@street, @city, @state, @country, @zip);
                    welcomeKit = Order(@userId, @userName, address, 1, @card, @cvv, @holder, @Now.AddYears(1), null, null);
                    welcomeKit.AddOrderItem(@sku, @itemName, @itemPrice, 0m, '', 1);
                ")
                .WithParameters(p => {
                    p["payerId",   typeof(string)]  = "6f1c2f7a-9b3e-4f0a-8a2d-1c5b7e9d4a11";
                    p["country",   typeof(string)]  = "PL";
                    p["period",    typeof(string)]  = "Month";
                    p["amount",    typeof(decimal)] = 50m;
                    p["currency",  typeof(string)]  = "EUR";
                    p["street",    typeof(string)]  = "street";
                    p["city",      typeof(string)]  = "city";
                    p["state",     typeof(string)]  = "state";
                    p["zip",       typeof(string)]  = "12345";
                    p["userId",    typeof(string)]  = "user-1";
                    p["userName",  typeof(string)]  = "Lab User";
                    p["card",      typeof(string)]  = "1234-5678-9012-3456";
                    p["cvv",       typeof(string)]  = "123";
                    p["holder",    typeof(string)]  = "Card Holder";
                    p["sku",       typeof(int)]     = 41;
                    p["itemName",  typeof(string)]  = "welcome kit";
                    p["itemPrice", typeof(decimal)] = 0m;
                })
                .PerformCommand();

                var definitions = ReadActionDefinitions(Path.Combine(tempDir, actorName, "journal"));

                Console.WriteLine();
                Console.WriteLine("=== One verb over two independently authored domains ===");
                Console.WriteLine($"  domain assemblies loaded: {ordering.GetName().Name}, "
                                  + "CompanyName.MyMeetings.Modules.Payments.Domain");
                Console.WriteLine($"  action definitions recorded: {definitions.Count}");
                Console.WriteLine();
                foreach (var d in definitions)
                {
                    Console.WriteLine($"  define action {d.Id}:");
                    Console.WriteLine($"    {d.Script}");
                    Console.WriteLine();
                }

                Assert.AreEqual(1, definitions.Count,
                    "The composed act must be recorded as one definition, not one per domain operation.");

                var script = definitions[0].Script;
                Assert.IsTrue(script.Contains("payments.Buy") && script.Contains("MarkAsPaid"),
                    "The recorded act must hold the operations of the first domain.");
                Assert.IsTrue(script.Contains("Order(") && script.Contains("AddOrderItem"),
                    "The recorded act must hold the operations of the second domain.");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        private record ActionDef(int Id, string Script);

        // Reads the Define records out of the journal, which is the catalog since the action
        // refactor dropped the lateral store. Same reader as the sibling lab in the eShop port.
        private static List<ActionDef> ReadActionDefinitions(string journalDir)
        {
            var definitions = new List<ActionDef>();
            if (!Directory.Exists(journalDir)) return definitions;

            foreach (var file in Directory.GetFiles(journalDir, "journal_*.bin").OrderBy(f => f))
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 32) continue;
                fs.Seek(32, SeekOrigin.Begin);

                var lenBuf = new byte[4];
                while (fs.Position < fs.Length)
                {
                    if (fs.Read(lenBuf, 0, 4) < 4) break;
                    int recordLen = BitConverter.ToInt32(lenBuf, 0);
                    if (recordLen <= 4 || fs.Position + recordLen > fs.Length) break;

                    var body = new byte[recordLen];
                    if (fs.Read(body, 0, recordLen) < recordLen) break;
                    if ((body[0] & 0x3F) != 2) continue;

                    int actionId = BitConverter.ToInt32(body, 1 + 8 + 8);
                    int offset = 1 + 8 + 8 + 4;
                    int payloadLen = BitConverter.ToInt32(body, offset);
                    if (payloadLen <= 0 || offset + 4 + payloadLen > recordLen) continue;

                    definitions.Add(new ActionDef(
                        actionId, Encoding.UTF8.GetString(body, offset + 4, payloadLen)));
                }
            }
            return definitions;
        }
    }
}
