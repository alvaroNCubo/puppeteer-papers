using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Choreography.Theater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puppeteer;
using Puppeteer.EventSourcing.DB;

namespace UnitTestChoreography.PaperLabs.Paper8
{
	// Paper 8 (Inference without Authority) — Lab B: the observer authority.
	//
	// Claim (the zeros are the claim): from ONE journaled fact, N DISTINCT
	// observations are added, each as its own projection reaction, with ZERO new
	// domain methods. The observations — including a DERIVED one (@price * @units,
	// computed in the projection) — are authored where observation happens (the
	// actor/reaction layer), not in the domain. A fused baseline pays one domain
	// method per view: adding an observer there means editing (and redeploying)
	// the domain.
	//
	// This lab runs in-process and is deterministic (the per-commit suite runs it).
	// The real-backend existence proof is Lab A; here the point is the authoring
	// locus and the method count, which need no external infra.
	//
	// (The domain type is named Sale/SaleLine, distinct from Lab A's Order, because
	// the actor resolves library classes by simple name across the whole assembly;
	// two same-named domain types would be an ambiguous reference.)
	//
	// Public ActorV2 surface only: a PerformanceV2 host, ConfigureStorage, a
	// parametrized .Using(...).PerformCommand(), and per-observer
	// perf.OutputTarget(sink) + a named projection reaction.
	[TestClass]
	public class LabB_ProjectionFanout
	{
		private static readonly Assembly TestAssembly = typeof(LabB_ProjectionFanout).Assembly;

		// The domain — a FIXED surface. Its only members are the ones the write
		// model needs (addLine, Lines, Total). Crucially it grows by ZERO methods
		// no matter how many views are added below: the views live in the reaction
		// layer, not here.
		public class Sale
		{
			private readonly List<SaleLine> lines = new List<SaleLine>();
			public IReadOnlyList<SaleLine> Lines => lines;
			public void addLine(string productName, int unitPrice, int units) =>
				lines.Add(new SaleLine(productName, unitPrice, units));
			public int Total()
			{
				int sum = 0;
				foreach (var i in lines) sum += i.UnitPrice * i.Units;
				return sum;
			}
		}

		public class SaleLine
		{
			public SaleLine(string productName, int unitPrice, int units)
			{
				ProductName = productName;
				UnitPrice = unitPrice;
				Units = units;
			}
			public string ProductName { get; }
			public int UnitPrice { get; }
			public int Units { get; }
		}

		// A fused baseline: each view is a DOMAIN method. To add an observer here
		// you edit the domain — one method per view (three views → three methods).
		// Contrast the separated Sale/SaleLine above, which add zero.
		private sealed class FusedInvoiceItem
		{
			private readonly string product;
			private readonly int unitPrice;
			private readonly int units;
			public FusedInvoiceItem(string product, int unitPrice, int units)
			{
				this.product = product;
				this.unitPrice = unitPrice;
				this.units = units;
			}
			public string FulfillmentLine() => $"{product} x{units}";         // view 1, as a domain method
			public string FinanceLine() => $"{product}: {unitPrice * units}";  // view 2 (derived), as a domain method
			public string CatalogLine() => $"{product} @ {unitPrice}";         // view 3, as a domain method
		}

		private sealed class RecordingSink : IOutputSink
		{
			public RecordingSink(string name) { Name = name; }
			public string Name { get; }
			public readonly List<PushDocument> Received = new List<PushDocument>();
			public void Push(in PushDocument document) => Received.Add(document);
		}

		private static PerformanceV2 CreateSalesHost()
		{
			var perf = new PerformanceV2($"labB_{Guid.NewGuid():N}", TestAssembly);
			perf.ConfigureStorage(DatabaseType.IN_MEMORY, "memory");
			perf.Start();
			return perf;
		}

		// One projection reaction = one observer. The OnMatch is the same fact for
		// every observer; only the Program.Emit projection differs. Adding an
		// observer is adding one of these — no domain change. (Per the reactions
		// writing convention the DSL — the match and the emit — stays inline.)
		private static void ArmView(PerformanceV2 perf, string reactionName, string emitProjection)
		{
			perf.Actor.Reactions.DefineReaction(reactionName)
				.Job().Company()
				.WithSharedHydration()
				.Seek("Line")
					.OnMatch("[s:Sale].addLine($product, $price, $units)")
				.Program.Emit(emitProjection);
		}

		private static void RecordTwoLineSale(PerformanceV2 perf)
		{
			AddLine(perf, openSale: true,  product: "widget", price: 10, units: 2);
			AddLine(perf, openSale: false, product: "gadget", price: 5,  units: 3);
		}

		private static void AddLine(PerformanceV2 perf, bool openSale, string product, int price, int units)
		{
			string body = openSale
				? "s = Sale(); s.addLine(@product, @price, @units);"
				: "s.addLine(@product, @price, @units);";
			perf.Actor.Using(body)
				.WithParameters(p => {
					p["product", typeof(string)] = product;
					p["price",   typeof(int)]    = price;
					p["units",   typeof(int)]    = units;
				})
				.PerformCommand();
		}

		private static void PushVia(PerformanceV2 perf, IOutputSink sink, string reactionName)
		{
			perf.OutputTarget(sink);
			perf.Actor.Reactions.Execute(reactionName);
		}

		// Public declared instance methods that are NOT property accessors — the
		// domain's real command/query surface.
		private static string[] DomainMethods(Type t) =>
			t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			 .Where(m => !m.IsSpecialName)
			 .Select(m => m.Name)
			 .OrderBy(n => n)
			 .ToArray();

		// ---- Test 1: one fact, three distinct views, zero new domain methods. ----

		[TestMethod]
		public void OneFact_ThreeDistinctViews_AddedWithZeroDomainMethods()
		{
			using var sales = CreateSalesHost();

			// Three observers of the SAME addLine fact — each a projection reaction.
			// The finance view DERIVES its value (@price * @units) in the projection;
			// in a fused design that derivation would be a domain method.
			ArmView(sales, "FulfillmentView", "print @product 'product', @units 'units';");
			ArmView(sales, "FinanceView",     "print @product 'product', @price * @units 'lineRevenue';");
			ArmView(sales, "CatalogView",     "print @product 'product', @price 'unitPrice';");

			RecordTwoLineSale(sales);

			var fulfillment = new RecordingSink("fulfillment");
			var finance = new RecordingSink("finance");
			var catalog = new RecordingSink("catalog");

			PushVia(sales, fulfillment, "FulfillmentView");
			PushVia(sales, finance, "FinanceView");
			PushVia(sales, catalog, "CatalogView");

			// Every observer saw the whole sale (two lines → two rows each).
			Assert.AreEqual(2, fulfillment.Received.Count, "fulfillment observed both lines");
			Assert.AreEqual(2, finance.Received.Count, "finance observed both lines");
			Assert.AreEqual(2, catalog.Received.Count, "catalog observed both lines");

			// The three observations are genuinely different projections of one fact.
			string f0 = fulfillment.Received[0].Document;
			string m0 = finance.Received[0].Document;
			string c0 = catalog.Received[0].Document;

			StringAssert.Contains(f0, "units");
			StringAssert.Contains(m0, "lineRevenue");
			StringAssert.Contains(c0, "unitPrice");

			// The DERIVED view is computed in the projection, not by the domain:
			// widget = price 10 * units 2 = 20.
			StringAssert.Contains(m0, "20", "line revenue is derived in the projection. Was: " + m0);
			Assert.AreNotEqual(f0, m0, "fulfillment and finance are different projections");
			Assert.AreNotEqual(m0, c0, "finance and catalog are different projections");
		}

		// ---- Test 2: adding the observers touched no domain method. ----

		[TestMethod]
		public void AddingObservers_GrowsTheReactionLayer_NotTheDomain()
		{
			// The domain surface is exactly the write model's — the three views did
			// not add a method to it.
			CollectionAssert.AreEquivalent(new[] { "Total", "addLine" }, DomainMethods(typeof(Sale)),
				"Sale exposes only its write-model verbs; the views added none");
			CollectionAssert.AreEquivalent(Array.Empty<string>(), DomainMethods(typeof(SaleLine)),
				"SaleLine exposes only fields; no per-view method was added");

			// The derived 'lineRevenue' view is NOT a domain method — it lives in the
			// projection script.
			Assert.IsNull(typeof(SaleLine).GetMethod("LineRevenue"),
				"the derived view is a projection, not a domain method");

			// Fused baseline: the same three views cost three domain methods.
			int fusedDomainMethodsForTheThreeViews = DomainMethods(typeof(FusedInvoiceItem)).Length;
			int separatedDomainMethodsForTheThreeViews = 0;   // they are DSL reactions
			Assert.AreEqual(3, fusedDomainMethodsForTheThreeViews,
				"the fused design pays one domain method per view");
			Assert.IsTrue(fusedDomainMethodsForTheThreeViews > separatedDomainMethodsForTheThreeViews,
				"observer authority: views are added where observation happens, not in the domain");
		}

		// ---- Headline: emit the lab's result table for porting to the papers repo. ----

		[TestMethod]
		public void Headline_WriteResultTable()
		{
			using var sales = CreateSalesHost();
			ArmView(sales, "FulfillmentView", "print @product 'product', @units 'units';");
			ArmView(sales, "FinanceView",     "print @product 'product', @price * @units 'lineRevenue';");
			ArmView(sales, "CatalogView",     "print @product 'product', @price 'unitPrice';");
			RecordTwoLineSale(sales);

			var finance = new RecordingSink("finance");
			PushVia(sales, finance, "FinanceView");

			var sb = new StringBuilder();
			sb.AppendLine("# Paper 8 — Lab B: projection fan-out (the observer authority)");
			sb.AppendLine();
			sb.AppendLine("ONE journaled fact, N distinct observers. The zeros are the claim.");
			sb.AppendLine();
			sb.AppendLine("| view | projection | domain methods added (separated) | domain methods added (fused) |");
			sb.AppendLine("|---|---|---|---|");
			sb.AppendLine("| fulfillment | @product, @units | 0 | 1 |");
			sb.AppendLine("| finance (derived) | @product, @price*@units | 0 | 1 |");
			sb.AppendLine("| catalog | @product, @price | 0 | 1 |");
			sb.AppendLine("| **total** | — | **0** | **3** |");
			sb.AppendLine();
			sb.AppendLine($"Sample derived projection (TOON): {finance.Received[0].Document}");
			sb.AppendLine("The derived value is computed in the projection, never a domain method.");
			sb.AppendLine("Adding an observer grows the reaction layer, not the domain.");
			sb.AppendLine();
			sb.AppendLine("Scope: authoring locus + method count. NOT a cost/benefit measurement at scale.");

			Console.WriteLine(sb.ToString());
			Assert.AreEqual(2, finance.Received.Count);
		}
	}
}
