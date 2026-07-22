using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Choreography.Theater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Puppeteer;
using Puppeteer.EventSourcing.DB;
using Puppeteer.Tell;

namespace UnitTestChoreography.PaperLabs.Paper8
{
	// Paper 8 (Inference without Authority) — Lab C: the observer's direction.
	//
	// Claim (the zeros are the claim): an observer reaches the SAME knowledge of a
	// journaled fact either by being TOLD (a producer Reaction pushes it across a
	// tell) or by POLLING (a PerformQuery pulls it). The direction is the
	// observer's / assembler's choice; the producer's domain adds ZERO methods for
	// either — no "notify", no "publishTo". Told and polls are the push/pull duals
	// of one observation (cf. Paper 4's `tell`; the print pull/push symmetry).
	//
	// Public ActorV2 surface: two PerformanceV2 hosts, ConfigureStorage, a
	// parametrized .Using(...).PerformCommand(), UseTellTransport, and a
	// .Causation.Continue("tell ...") Reaction. The cross-boundary uptake is driven
	// manually here (the host-layer ListenAs/Told listener is the production path);
	// what the lab pins is that the value crosses to the observer and that the
	// polled value equals it.
	[TestClass]
	public class LabC_ToldVsPolls
	{
		private static readonly Assembly TestAssembly = typeof(LabC_ToldVsPolls).Assembly;

		// Producer domain — records a sale, and can be polled for it. Its surface is
		// the write model plus one query; it carries NO delivery method.
		public class Vendor
		{
			private int amount = -1;
			public void sell(int soldAmount) => amount = soldAmount;
			public int Amount() => amount;
		}

		// Observer domain — records what it is told. Also no delivery method.
		public class Cashbook
		{
			private int amount = -1;
			public void record(int toldAmount) => amount = toldAmount;
			public int Amount() => amount;
		}

		private static PerformanceV2 CreateHost(string role)
		{
			var perf = new PerformanceV2($"labC_{role}_{Guid.NewGuid():N}", TestAssembly);
			perf.ConfigureStorage(DatabaseType.IN_MEMORY, "memory");
			perf.Start();
			return perf;
		}

		private static void RecordSale(PerformanceV2 seller, int amount)
		{
			seller.Actor.Using("v = Vendor(); v.sell(@amount);")
				.WithParameters(p => {
					p["amount", typeof(int)] = amount;
				})
				.PerformCommand();
		}

		private static string[] DomainMethods(Type t) =>
			t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			 .Where(m => !m.IsSpecialName)
			 .Select(m => m.Name)
			 .OrderBy(n => n)
			 .ToArray();

		// ---- Test 1: the same fact reaches the observer told OR polled. ----

		[TestMethod]
		public void Observer_LearnsBySeenTold_OrByPolling_SameKnowledge()
		{
			const int amount = 100;

			using var seller = CreateHost("seller");
			RecordSale(seller, amount);

			// POLL (pull): an observer reads the fact straight off the producer.
			string polled = seller.Actor.Using("print v.Amount() 'amount';").PerformQuery();
			StringAssert.Contains(polled, amount.ToString(), "polling pulls the fact from the producer");

			// TOLD (push): a producer Reaction tells the fact to the Cashbook role; the
			// value crosses the transport to the observer.
			var transport = new InMemoryTransport();
			seller.UseTellTransport(transport);
			seller.Actor.Reactions.DefineReaction("TellCashbook")
				.Job().Company()
				.WithSharedHydration()
				.Seek("Sale")
					.OnMatch("[v:Vendor].sell($amount)")
				.Causation.Continue(@"
					tell Sold with @amount to Cashbook('deal-1') once 'deal-1';
				");
			seller.Actor.Reactions.Execute();

			TellEnvelope told = transport.Sent.Single();
			Assert.AreEqual("Sold", told.MessageName);
			Assert.AreEqual("Cashbook", told.Addressee, "the observer role is the addressee");
			StringAssert.Contains(told.Values.ArgumentsAsString(DatabaseType.IN_MEMORY), amount.ToString(),
				"the told value crosses to the observer");

			// The observer takes up the tell and records it (production: the Told
			// listener rebuilds typed params from the envelope's ordered values).
			using var ledger = CreateHost("ledger");
			ledger.Actor.Using("l = Cashbook(); l.record(@amount);")
				.WithParameters(p => {
					p["amount", typeof(int)] = amount;
				})
				.PerformCommand();
			transport.TriggerAck(new AckEnvelope(told.Id, told.Addressee, told.AddresseeInstanceId));

			string atCashbook = ledger.Actor.Using("print l.Amount() 'amount';").PerformQuery();
			StringAssert.Contains(atCashbook, amount.ToString(), "the told observer now knows the fact");

			// Same knowledge, two directions: the polled value equals the told value.
			Assert.AreEqual(polled, atCashbook,
				"told and polls reach the identical observation of one fact");
		}

		// ---- Test 2: the direction is not a domain concern. ----

		[TestMethod]
		public void NeitherDirection_IsADomainMethod()
		{
			// The producer exposes its write verb and a query — no "notify"/"publish"
			// and no polling endpoint baked in. Told is a Reaction; polls is a query.
			CollectionAssert.AreEquivalent(new[] { "Amount", "sell" }, DomainMethods(typeof(Vendor)),
				"the producer carries no delivery method; told is a reaction, polls is a query");
			CollectionAssert.AreEquivalent(new[] { "Amount", "record" }, DomainMethods(typeof(Cashbook)),
				"the observer records what it is told; it carries no transport method");
			Assert.IsNull(typeof(Vendor).GetMethod("notify"),
				"the producer does not name its observers");
		}

		// ---- Headline: emit the lab's result table for porting to the papers repo. ----

		[TestMethod]
		public void Headline_WriteResultTable()
		{
			const int amount = 100;
			using var seller = CreateHost("seller");
			RecordSale(seller, amount);
			string polled = seller.Actor.Using("print v.Amount() 'amount';").PerformQuery();

			var transport = new InMemoryTransport();
			seller.UseTellTransport(transport);
			seller.Actor.Reactions.DefineReaction("TellCashbook")
				.Job().Company()
				.WithSharedHydration()
				.Seek("Sale")
					.OnMatch("[v:Vendor].sell($amount)")
				.Causation.Continue(@"
					tell Sold with @amount to Cashbook('deal-1') once 'deal-1';
				");
			seller.Actor.Reactions.Execute();
			string toldArgs = transport.Sent.Single().Values.ArgumentsAsString(DatabaseType.IN_MEMORY);

			var sb = new StringBuilder();
			sb.AppendLine("# Paper 8 — Lab C: told vs polls (the observer's direction)");
			sb.AppendLine();
			sb.AppendLine("One fact, one observation, two directions. The zeros are the claim.");
			sb.AppendLine();
			sb.AppendLine("| direction | mechanism | producer domain methods for delivery |");
			sb.AppendLine("|---|---|---|");
			sb.AppendLine("| polls (pull) | PerformQuery on the producer | 0 |");
			sb.AppendLine("| told (push) | a Reaction's Causation.Continue(tell) | 0 |");
			sb.AppendLine();
			sb.AppendLine($"Polled value: {polled}");
			sb.AppendLine($"Told envelope args: {toldArgs}");
			sb.AppendLine("Told and polls reach the identical observation; the direction is chosen");
			sb.AppendLine("outside the actor, and the producer names no observer and no transport.");
			sb.AppendLine();
			sb.AppendLine("Scope: authoring locus of the delivery direction. NOT a throughput measurement.");

			Console.WriteLine(sb.ToString());
			Assert.AreEqual(1, transport.Sent.Count);
		}
	}
}
