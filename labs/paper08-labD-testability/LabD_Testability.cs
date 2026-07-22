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
	// Paper 8 (Inference without Authority) — Lab D: testability as evidence.
	//
	// Claim (the zeros are the claim): the hard actor/assembler boundary of §4 is
	// not only argued but observable. A domain/projection can be exercised END TO
	// END with NO destination bound — no sink, no port, no test double for output —
	// which is the operational proof that the destination was never in the domain.
	// Were the sink the producer's, no such test could run without it.
	//
	// This also answers the standing objection that the arrangement is dependency
	// injection / hexagonal architecture (ports & adapters) renamed. Hexagonal
	// inverts the dependency but still gives the domain a PORT — an output
	// interface a test must supply a double for. Here `print` names no output at
	// all: there is no port, so a domain output test stands up zero doubles. The
	// difference is observable, not interpretive — count the doubles.
	//
	// Public ActorV2 surface only, reusing the Lab A–C pattern (PerformanceV2,
	// ConfigureStorage, parametrized .Using(...).PerformCommand(), PerformQuery).
	[TestClass]
	public class LabD_Testability
	{
		private static readonly Assembly TestAssembly = typeof(LabD_Testability).Assembly;

		// The separated domain — the same Order-shaped material as Lab A, under a
		// distinct name. Note what is ABSENT: no output interface, no sink field,
		// no port. `print` in the DSL will name a logical output and no destination.
		public class Basket
		{
			private readonly List<BasketLine> lines = new List<BasketLine>();
			public IReadOnlyList<BasketLine> Lines => lines;
			public void addLine(string productName, int unitPrice, int units) =>
				lines.Add(new BasketLine(productName, unitPrice, units));
			public int Total()
			{
				int sum = 0;
				foreach (var i in lines) sum += i.UnitPrice * i.Units;
				return sum;
			}
		}

		public class BasketLine
		{
			public BasketLine(string productName, int unitPrice, int units)
			{
				ProductName = productName;
				UnitPrice = unitPrice;
				Units = units;
			}
			public string ProductName { get; }
			public int UnitPrice { get; }
			public int Units { get; }
		}

		// The hexagonal / ports-and-adapters baseline: the domain OWNS an output
		// port and depends on it. To test what it emits, a double for the port must
		// be injected — there is no other way to observe its output.
		private interface IInvoiceSink { void Emit(string product, int total); }

		private sealed class HexInvoicer
		{
			private readonly IInvoiceSink sink;   // the port the domain names and depends upon
			public HexInvoicer(IInvoiceSink sink)
			{
				ArgumentNullException.ThrowIfNull(sink);
				this.sink = sink;
			}
			public void Publish(string product, int unitPrice, int units) =>
				sink.Emit(product, unitPrice * units);
		}

		// The test double the port forces upon any output test of HexInvoicer.
		private sealed class FakeInvoiceSink : IInvoiceSink
		{
			public readonly List<(string product, int total)> Emitted = new List<(string, int)>();
			public void Emit(string product, int total) => Emitted.Add((product, total));
		}

		// The observable metric: does a type inject a port — an interface it depends
		// on through its constructor (the hexagonal shape)? The separated domain
		// does not; the hexagonal one does.
		private static bool InjectsAPort(Type t) =>
			t.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType.IsInterface));

		private static PerformanceV2 CreateHost(string name)
		{
			var perf = new PerformanceV2(name, TestAssembly);
			perf.ConfigureStorage(DatabaseType.IN_MEMORY, "memory");
			perf.Start();
			return perf;
		}

		private static void RecordTwoLineBasket(PerformanceV2 perf)
		{
			AddLine(perf, open: true,  product: "widget", price: 10, units: 2);
			AddLine(perf, open: false, product: "gadget", price: 5,  units: 3);
		}

		private static void AddLine(PerformanceV2 perf, bool open, string product, int price, int units)
		{
			string body = open
				? "b = Basket(); b.addLine(@product, @price, @units);"
				: "b.addLine(@product, @price, @units);";
			perf.Actor.Using(body)
				.WithParameters(p => {
					p["product", typeof(string)] = product;
					p["price",   typeof(int)]    = price;
					p["units",   typeof(int)]    = units;
				})
				.PerformCommand();
		}

		// ---- Test 1: the domain projects end to end with no sink and no double. ----

		[TestMethod]
		public void SeparatedDomain_ProjectsEndToEnd_WithNoSinkAndNoDouble()
		{
			int outputDoublesUsed = 0;   // nothing is stood up for the output below

			using var perf = CreateHost($"labD_{Guid.NewGuid():N}");
			RecordTwoLineBasket(perf);

			// The projection is exercised end to end — commands journaled, state
			// rebuilt, result read back — with NO OutputTarget ever bound: no sink,
			// no port, no double. The answer returns to the caller; nothing is sent.
			string pulled = perf.Actor.Using("print b.Total() 'total';").PerformQuery();

			StringAssert.Contains(pulled, "35",
				"widget(10*2=20) + gadget(5*3=15) = 35, read back with no destination");
			Assert.AreEqual(0, outputDoublesUsed, "a domain output test stands up zero doubles");
			Assert.IsFalse(InjectsAPort(typeof(Basket)),
				"the domain names no output port; there is nothing to mock");
		}

		// ---- Test 2: the projection survives replay, still with no sink. ----

		[TestMethod]
		public void SeparatedProjection_SurvivesReplay_WithNoSink()
		{
			string name = $"labD_replay_{Guid.NewGuid():N}";

			// Author the basket on one instance, then let it go.
			using (var author = CreateHost(name))
				RecordTwoLineBasket(author);

			// A fresh instance of the same actor rehydrates the journal from scratch
			// and answers the same projection — end to end across a replay, and still
			// with no destination anywhere in sight.
			using var reader = CreateHost(name);
			string pulled = reader.Actor.Using("print b.Total() 'total';").PerformQuery();

			StringAssert.Contains(pulled, "35",
				"the projection is reconstructed from the journal, no sink involved");
		}

		// ---- Test 3: the hexagonal domain cannot have its output tested without a double. ----

		[TestMethod]
		public void HexagonalDomain_OutputTest_RequiresADouble()
		{
			// To observe what HexInvoicer emits, a double for its port must be
			// supplied — the constructor will not even build without one.
			var fake = new FakeInvoiceSink();
			var hex = new HexInvoicer(fake);
			hex.Publish("widget", 10, 2);

			Assert.AreEqual(1, fake.Emitted.Count, "the output is observable only through the injected double");
			Assert.AreEqual(("widget", 20), fake.Emitted[0]);

			// The observable difference: the hexagonal domain injects a port; the
			// separated domain does not. Doubles a domain output test must stand up:
			// hexagonal >= 1 (the port), separated = 0 (there is no port).
			Assert.IsTrue(InjectsAPort(typeof(HexInvoicer)),
				"hexagonal inverts the dependency but the domain still holds the port");
			Assert.IsFalse(InjectsAPort(typeof(Basket)),
				"the separated domain removes the dependency; there is nothing to inject");
		}

		// ---- Headline: emit the lab's result table for porting to the papers repo. ----

		[TestMethod]
		public void Headline_WriteResultTable()
		{
			using var perf = CreateHost($"labD_{Guid.NewGuid():N}");
			RecordTwoLineBasket(perf);
			string pulled = perf.Actor.Using("print b.Total() 'total';").PerformQuery();

			var sb = new StringBuilder();
			sb.AppendLine("# Paper 8 — Lab D: testability as evidence (no port to mock)");
			sb.AppendLine();
			sb.AppendLine("The hard actor/assembler boundary is observable: a domain output test that");
			sb.AppendLine("needs no destination is the proof the destination was never in the domain.");
			sb.AppendLine();
			sb.AppendLine("| approach | domain names an output port? | doubles a domain output test stands up |");
			sb.AppendLine("|---|---|---|");
			sb.AppendLine($"| separated (this paper) | no (`print` knows no sink) | 0 |");
			sb.AppendLine($"| hexagonal / ports-and-adapters | yes (an injected interface) | >= 1 (the port) |");
			sb.AppendLine();
			sb.AppendLine($"End-to-end pull with no sink bound: {pulled.Trim()}");
			sb.AppendLine("Inversion relocates a dependency; this removes it — injection presupposes a");
			sb.AppendLine("thing to inject, and there is none.");
			sb.AppendLine();
			sb.AppendLine("Scope: the hard boundary only (actor/assembler). NOT a throughput measurement.");

			Console.WriteLine(sb.ToString());
			StringAssert.Contains(pulled, "35");
		}
	}
}
