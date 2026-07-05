using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using Puppeteer;
using Puppeteer.EventSourcing.DB;
using Puppeteer.Tell;
using Puppeteer.UnitTest.LoyaltyDomain;
using Choreography.Theater;
using Choreography.Transport.Brokered;

namespace Lab04Tell
{
	// Lab 4 — cross-actor causation (Paper 4 §8). Runs the loyalty scenario under
	// three cross-actor styles (saga, choreography, tell) and four property tests
	// (G1 replay, G2 cross-DC, G3 audit, G4 tell-fate recovery), a separated-receiver
	// run (G5), and the negative gate.
	//
	// This is a PURE public-API Puppeteer program. Actors are PerformanceV2 (the V2
	// hosting shell). Every command is issued as
	//     perf.Using("...@p...").WithParameters(p => { p["p", typeof(T)] = v; }).PerformCommand()
	// — all values cross the DSL boundary as TYPED @parameters, never string-interpolated.
	// Cross-actor causation is a Reaction whose .Causation.Continue body issues `tell`.
	// The journal is read back ONLY through the public introspection surface
	// (perf.Actor.Introspection: ShowEntry / FindPattern) and domain outcomes through
	// perf.PerformQry(...). No DiaryStorageInMemory, no ActorHandler, no InternalsVisibleTo.
	//
	// tell is the assertive speech act: the sender asserts a fact it lived
	// (`tell PurchaseConfirmed with ... to RewardEngine('rewards-1') once '...'`);
	// it names no receiver method and no transport.
	public static class Program
	{
		private static readonly Assembly DomainAssembly = typeof(Seller).Assembly;
		private static readonly Assembly PuppeteerAssembly = typeof(Actor).Assembly;
		private static int _failures;

		// The one purchase the whole lab is about. Values are constants here; every
		// scenario passes them across the DSL boundary as typed parameters.
		private const string Order = "ord-100";
		private const string Customer = "cust-42";
		private static readonly DateTime PurchaseDate = new DateTime(2026, 9, 5);
		private const decimal PurchaseAmount = 250m;
		private static readonly DateTime CampaignStart = new DateTime(2020, 1, 1);

		public static void Main(string[] args)
		{
			Banner("Lab 4 — Cross-actor causation: where the joint history lives");
			Style1_Saga();
			Style2_Choreography();
			Style3_Tell();
			G1_ReplayCoherence();
			G2_CrossDcReplication();
			G3_AuditQuery();
			G4_TellFateRecovery();
			G5_SeparatedReceiver();
			Negative_DirectTellRejected();

			Console.WriteLine();
			Console.WriteLine(_failures == 0 ? "ALL CHECKS PASSED." : $"{_failures} CHECK(S) FAILED.");
			Environment.Exit(_failures == 0 ? 0 : 1);
		}

		// ==== host + domain steps (public surface only) =====================

		// A running PerformanceV2 over an in-memory journal.
		private static PerformanceV2 CreateActor(string suffix)
			=> CreateActor(suffix, DatabaseType.IN_MEMORY, "memory");

		// A running PerformanceV2 over the given store. A caller that needs the
		// journal to survive the actor (cross-DC replication) passes a FileSystem
		// connection and a stable name.
		private static PerformanceV2 CreateActor(string name, DatabaseType db, string connection)
		{
			PerformanceV2 perf = new PerformanceV2(name, DomainAssembly, PuppeteerAssembly);
			perf.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysInterpreted;
			perf.ConfigureStorage(db, connection);
			perf.Start();
			return perf;
		}

		// Domain step: build a RewardEngine with a registry of campaigns. Creating
		// `loyalty` and adding each campaign are separate commands. Two campaigns are
		// registered — C-newcomer (min 10) qualifies for the 250 purchase, C-highroller
		// (min 1000) does not — so the receiver's foreach selects one of the two.
		private static void CreateRewardEngine(PerformanceV2 rewards)
		{
			rewards.Using("loyalty = RewardEngine();").PerformCommand();
			AddCampaign(rewards, "C-newcomer", 10m);
			AddCampaign(rewards, "C-highroller", 1000m);
		}

		// One campaign added as a parameterized command — same action shape for every
		// campaign, so the registry is one define invoked once per campaign.
		private static void AddCampaign(PerformanceV2 rewards, string campaign, decimal minAmount)
		{
			rewards.Using("loyalty.AddCampaign(@campaign, @validFrom, @minAmount);")
				.WithParameters(p =>
				{
					p["campaign", typeof(string)] = campaign;
					p["validFrom", typeof(DateTime)] = CampaignStart;
					p["minAmount", typeof(decimal)] = minAmount;
				})
				.PerformCommand();
		}

		// Domain step: the Seller confirms a purchase — order/date/amount/customer all
		// cross as typed parameters.
		private static void ConfirmPurchase(PerformanceV2 seller)
		{
			seller.Using("s = Seller();").PerformCommand();
			seller.Using("s.purchase(@order, @date, @amount, @customer);")
				.WithParameters(p =>
				{
					p["order", typeof(string)] = Order;
					p["date", typeof(DateTime)] = PurchaseDate;
					p["amount", typeof(decimal)] = PurchaseAmount;
					p["customer", typeof(string)] = Customer;
				})
				.PerformCommand();
		}

		// Domain step: the RewardEngine applies every campaign that qualifies for the
		// purchase. The command a receiver runs on its own state; values as parameters.
		private static void ApplyRewards(PerformanceV2 rewards)
		{
			rewards.Using(@"
				foreach (c in loyalty.Campaigns()) {
					if (c.Applies(@date, @amount) == true) { c.Reward(@order, @customer); };
				};")
				.WithParameters(p =>
				{
					p["order", typeof(string)] = Order;
					p["customer", typeof(string)] = Customer;
					p["date", typeof(DateTime)] = PurchaseDate;
					p["amount", typeof(decimal)] = PurchaseAmount;
				})
				.PerformCommand();
		}

		// Cross-actor: a standing Reaction that, when a purchase lands, asserts
		// PurchaseConfirmed to the RewardEngine — capturing the purchase's arguments
		// positionally and forwarding them as the assertion's values. The tell's
		// per-utterance identity is `once @order`: the identity IS the captured order
		// id, so ONE compiled action issues a distinctly-identified tell per purchase
		// (never an action per order), and the ack correlates back by that same id.
		private static void DefinePurchaseFunnel(PerformanceV2 seller)
		{
			seller.Actor.Reactions.DefineReaction("PurchaseFunnelToRewards")
				.Job().Company()
				.WithSharedHydration()
				.Seek("Purchase")
					.OnMatch("[s:Seller].purchase($order, $date, $amount, $customer)")
				.Causation.Continue(@"
					tell PurchaseConfirmed
						with @order, @date, @amount, @customer
						to RewardEngine('rewards-1')
						once @order;
				");
		}

		// The tell flow end to end: the Seller asserts PurchaseConfirmed; when
		// `deliver`, a bridge maps the assertion to the reward command the receiver
		// owns and acks. Returns the sender, the receiver, and the sender's transport.
		private static (PerformanceV2 seller, PerformanceV2 rewards, InMemoryTransport transport) RunTell(
			string sellerSuffix, string rewardsSuffix, bool deliver)
		{
			PerformanceV2 rewards = CreateActor(rewardsSuffix);
			CreateRewardEngine(rewards);

			PerformanceV2 seller = CreateActor(sellerSuffix);
			InMemoryTransport transport = new InMemoryTransport();
			seller.UseTellTransport(transport);
			DefinePurchaseFunnel(seller);
			ConfirmPurchase(seller);
			seller.Actor.Reactions.Execute();

			if (deliver)
				foreach (TellEnvelope env in transport.Sent)
				{
					ApplyRewards(rewards);
					transport.TriggerAck(new AckEnvelope(env.Id, env.Addressee, env.AddresseeInstanceId));
				}

			return (seller, rewards, transport);
		}

		// ==== display + assertions (public introspection only) ==============

		// The actor's journal, rendered through the public introspection surface
		// (Toon, one record per entry). Used both to print the journal and — via
		// Contains — to assert what it holds.
		private static string Journal(PerformanceV2 perf)
		{
			StringBuilder sb = new StringBuilder();
			for (long id = 0; id <= perf.Actor.CurrentEntryId; id++)
			{
				try { sb.Append(perf.Actor.Introspection.ShowEntry(id)); }
				catch (LanguageException) { /* gap — no entry at this id */ }
			}
			return sb.ToString();
		}

		private static void DumpJournal(string label, PerformanceV2 perf)
		{
			Console.WriteLine($"{label} journal:");
			foreach (string line in Journal(perf).Replace("\r\n", "\n").Split('\n'))
				if (line.Length > 0) Console.WriteLine("    " + line);
		}

		// Read a value out of the actor with an Out parameter: the query assigns the
		// method's result to the @-prefixed Out parameter (`@total = ...`), so we read
		// the typed value directly from our Parameters — no print, no deserialize.
		private static int TotalRewards(PerformanceV2 rewards)
		{
			Parameters p = new Parameters { [Parameter.Out, "total", typeof(int)] = default(int) };
			rewards.Using("@total = loyalty.TotalRewards();").WithParameters(p).PerformQuery();
			return (int)p["total"].GetValue();
		}

		private static void Banner(string title)
		{
			Console.WriteLine(new string('=', 72));
			Console.WriteLine(title);
			Console.WriteLine(new string('=', 72));
		}

		private static void Section(string title)
		{
			Console.WriteLine();
			Console.WriteLine("--- " + title + " ---");
		}

		private static void Check(string what, bool ok)
		{
			if (!ok) _failures++;
			Console.WriteLine($"  CHECK {(ok ? "PASS" : "FAIL")}: {what}");
		}

		// ==== Style 1: saga (orchestrator) ==================================

		private static void Style1_Saga()
		{
			Section("Style 1 — Saga (orchestrator): joint history in the coordinator's journal");
			PerformanceV2 saga = CreateActor("saga_orchestrator");
			PerformanceV2 seller = CreateActor("saga_seller");
			PerformanceV2 rewards = CreateActor("saga_rewards");
			CreateRewardEngine(rewards);

			// The coordinator drives the workflow via direct commands to participants.
			saga.Using("step = 'PurchaseRequested';").PerformCommand();
			ConfirmPurchase(seller);
			saga.Using("step = 'PurchaseConfirmed';").PerformCommand();
			ApplyRewards(rewards);
			saga.Using("step = 'RewardsApplied';").PerformCommand();

			DumpJournal("SagaCoordinator", saga);
			DumpJournal("Seller", seller);
			DumpJournal("RewardEngine", rewards);
			Check("the coordinator's journal holds the whole workflow narrative",
				Journal(saga).Contains("PurchaseRequested") && Journal(saga).Contains("RewardsApplied"));
			Check("the Seller's journal holds only its own purchase, not the coordination",
				Journal(seller).Contains("purchase") && !Journal(seller).Contains("step"));
			Check("the RewardEngine's journal holds only its own reward, not the coordination",
				Journal(rewards).Contains("Reward") && !Journal(rewards).Contains("step"));
			Check("the reward was applied", TotalRewards(rewards) == 1);
			Console.WriteLine("  => the joint history lives ONLY in the coordinator's journal.");
		}

		// ==== Style 2: choreography (event bus, no coordinator) =============

		private sealed class EventBus
		{
			private readonly List<Action<string>> subscribers = new();
			internal readonly List<string> Log = new();
			public void Subscribe(Action<string> handler) => subscribers.Add(handler);
			public void Publish(string ev) { Log.Add($"published: {ev}"); foreach (var s in subscribers) s(ev); }
		}

		private static void Style2_Choreography()
		{
			Section("Style 2 — Choreography (event bus): joint history in no actor's journal");
			PerformanceV2 seller = CreateActor("choreo_seller");
			PerformanceV2 rewards = CreateActor("choreo_rewards");
			CreateRewardEngine(rewards);
			EventBus bus = new EventBus();
			bus.Subscribe(ev => { if (ev.StartsWith("PurchaseConfirmed:")) ApplyRewards(rewards); });

			ConfirmPurchase(seller);
			bus.Publish("PurchaseConfirmed:ord-100");

			DumpJournal("Seller", seller);
			DumpJournal("RewardEngine", rewards);
			Console.WriteLine($"Bus log ({bus.Log.Count} entries):");
			foreach (var l in bus.Log) Console.WriteLine($"  {l}");
			Check("the Seller's journal holds only the local purchase (the publish is invisible to it)",
				Journal(seller).Contains("purchase") && !Journal(seller).Contains("PurchaseConfirmed"));
			Check("the RewardEngine's journal holds only its own reward",
				Journal(rewards).Contains("Reward") && !Journal(rewards).Contains("purchase"));
			Check("the only joint artifact is the bus log, external to every program", bus.Log.Count == 1);
			Check("the reward was applied", TotalRewards(rewards) == 1);
			Console.WriteLine("  => the joint history lives ONLY in the external bus log.");
		}

		// ==== Style 3: tell (Puppeteer) =====================================

		private static void Style3_Tell()
		{
			Section("Style 3 — Tell (Puppeteer): joint history in the sender's own journal");
			var (seller, rewards, _) = RunTell("tell_seller", "tell_rewards", deliver: true);

			DumpJournal("Seller", seller);
			DumpJournal("RewardEngine", rewards);

			// (1) the Seller's own journal holds its purchase — matched, with its
			// arguments captured, by a pattern query over its own journal.
			Check("the Seller's journal holds its purchase (captured via a pattern query)",
				seller.Actor.Introspection
					.FindPattern("[s:Seller].purchase($order, $date, $amount, $customer)")
					.Contains("matchesFound: 1"));
			// (2) the joint cross-actor history — assertion + ack — is in the sender's journal.
			Check("the Seller's journal holds the assertion PurchaseConfirmed and the ack",
				Journal(seller).Contains("PurchaseConfirmed") && Journal(seller).Contains($"tell ack '{Order}'"));
			// (3) the receiver's journal holds only its own operations.
			Check("the RewardEngine's journal does not hold the Seller's purchase",
				!Journal(rewards).Contains("purchase"));
			Check("the reward was applied", TotalRewards(rewards) == 1);
			Console.WriteLine("  => the joint history lives in the sender's journal as DSL sentences.");
		}

		// ==== G1: replay coherence (Paper 4 §5.2 / §8.5) ====================

		private static void G1_ReplayCoherence()
		{
			Section("G1 — Replay coherence: a fresh actor reconstructs the in-flight tell from the journal alone");
			var (original, _, _) = RunTell("g1_seller", "g1_rewards", deliver: false); // in-flight: no bridge, no ack
			string name = original.Actor.Name;

			// A fresh Performance over the SAME in-memory store name: Start replays the
			// journal. The transport is plugged so replay COULD cite it — but must not re-emit.
			InMemoryTransport replayTransport = new InMemoryTransport();
			PerformanceV2 replayed = new PerformanceV2(name, DomainAssembly, PuppeteerAssembly);
			replayed.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysInterpreted;
			replayed.UseTellTransport(replayTransport);
			replayed.ConfigureStorage(DatabaseType.IN_MEMORY, "memory");
			replayed.Start();

			Check("the replayed actor reconstructs the in-flight tell from the journal",
				Journal(replayed).Contains("PurchaseConfirmed") && Journal(replayed).Contains($"'{Order}'"));
			Check("replay does not re-emit the envelope", replayTransport.Sent.Count == 0);
		}

		// ==== G2: cross-DC replication (Paper 4 §5.3 / §8.5) ================

		private static void G2_CrossDcReplication()
		{
			Section("G2 — Cross-DC replication: replicating the journal bytes alone carries the cross-actor chain");
			string name = $"lab04_g2_{Guid.NewGuid():N}";
			string dc1Root = Path.Combine(Path.GetTempPath(), $"lab04_g2_dc1_{Guid.NewGuid():N}");
			string dc2Root = Path.Combine(Path.GetTempPath(), $"lab04_g2_dc2_{Guid.NewGuid():N}");
			try
			{
				// DC1 persists to disk and stages the in-flight tell (no bridge, no ack).
				PerformanceV2 dc1 = CreateActor(name, DatabaseType.FileSystem, $"path={dc1Root}");
				dc1.UseTellTransport(new InMemoryTransport());
				DefinePurchaseFunnel(dc1);
				ConfirmPurchase(dc1);
				dc1.Actor.Reactions.Execute();
				dc1.Dispose(); // flush + release the journal files before we copy the bytes

				// Replicate: copy the persisted journal bytes to DC2's store, byte for byte.
				CopyDirectory(dc1Root, dc2Root);

				// DC2 opens over the replicated bytes — a different store, no shared
				// transport, no live DC1 — and Start replays the cross-actor chain.
				InMemoryTransport dc2Transport = new InMemoryTransport();
				PerformanceV2 dc2 = CreateActor(name, DatabaseType.FileSystem, $"path={dc2Root}");
				dc2.UseTellTransport(dc2Transport);

				DumpJournal("DC2 (replicated bytes)", dc2);
				Check("DC2 reconstructs the cross-actor chain from replicated bytes alone",
					Journal(dc2).Contains("PurchaseConfirmed") && Journal(dc2).Contains($"'{Order}'"));
				Check("DC2 does not re-emit the envelope on replay", dc2Transport.Sent.Count == 0);
				dc2.Dispose();
			}
			finally
			{
				TryDeleteDirectory(dc1Root);
				TryDeleteDirectory(dc2Root);
			}
		}

		private static void CopyDirectory(string source, string destination)
		{
			foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
			{
				string target = Path.Combine(destination, Path.GetRelativePath(source, file));
				Directory.CreateDirectory(Path.GetDirectoryName(target));
				File.Copy(file, target, overwrite: true);
			}
		}

		private static void TryDeleteDirectory(string path)
		{
			try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
			catch { /* best-effort temp cleanup */ }
		}

		// ==== G3: audit query from the sender's journal alone ===============

		private static void G3_AuditQuery()
		{
			Section("G3 — Audit query: 'why did this happen?' answered by reading the sender's journal");
			var (seller, _, _) = RunTell("g3_seller", "g3_rewards", deliver: true);

			// The audit answer is read from the sender's own journal — no trace store.
			string assertion = seller.Actor.Introspection.FindPattern(
				"[s:Seller].purchase($order, $date, $amount, $customer)");
			Check("the cause (the purchase) is reconstructable from the journal, with its arguments",
				assertion.Contains("matchesFound: 1"));
			Check("the effect (the assertion to RewardEngine) is in the same journal",
				Journal(seller).Contains("tell PurchaseConfirmed") && Journal(seller).Contains("RewardEngine('rewards-1')"));
			Check("the acknowledgment closing the chain is in the same journal",
				Journal(seller).Contains($"tell ack '{Order}'"));
		}

		// ==== G4: tell-fate recovery across the crash window (Paper 4 §8.5) =

		// Stages the crash window: a Seller observes a purchase and its Reaction asserts
		// a single addressed tell. The bridge is never run, so the tell sits in-flight —
		// journaled as issued, never dispatched, never acked. Returns the actor name so
		// a fresh actor can rehydrate over the same in-memory store.
		private static string StageCrashWindowTell(string suffix)
		{
			PerformanceV2 seller = CreateActor(suffix);
			seller.UseTellTransport(new InMemoryTransport());
			DefinePurchaseFunnel(seller);
			ConfirmPurchase(seller);
			seller.Actor.Reactions.Execute();
			return seller.Actor.Name; // in-flight on the discarded transport
		}

		// Rehydrates a fresh actor over the staged journal with a transport configured to
		// testify a fate. Transport is plugged BEFORE Start so post-replay recovery cites it.
		private static (PerformanceV2 actor, InMemoryTransport transport) RecoverWithFate(
			string actorName, Action<InMemoryTransport> configure)
		{
			InMemoryTransport transport = new InMemoryTransport();
			configure(transport);
			PerformanceV2 actor = new PerformanceV2(actorName, DomainAssembly, PuppeteerAssembly);
			actor.Actor.CompiledModePolicy = CompilationModePolicy.AlwaysInterpreted;
			actor.UseTellTransport(transport);
			actor.ConfigureStorage(DatabaseType.IN_MEMORY, "memory");
			actor.Start(); // replay + post-replay RecoverPendingTells (primary)
			return (actor, transport);
		}

		private static void G4_TellFateRecovery()
		{
			Section("G4 — Tell-fate recovery: the sender's journal records the FATE of a tell stranded by a crash");

			// Failed: the transport testifies non-delivery -> a LOGICAL verdict naming
			// the addressee is journaled (no transport named).
			var (failed, failedTransport) = RecoverWithFate(
				StageCrashWindowTell("g4_failed"), t => t.SetFate(Order, TellFate.Failed));
			DumpJournal("Recovered (transport testifies Failed)", failed);
			string failedJournal = Journal(failed);
			Check("a logical non-delivery verdict is journaled (unacknowledged by the addressee)",
				failedJournal.Contains($"tell '{Order}' unacknowledged by RewardEngine"));
			Check("the verdict names no transport", !failedJournal.Contains(" per "));
			Check("a failed tell is not falsely acked", !failedJournal.Contains($"tell ack '{Order}'"));
			Check("recovery testifies, never re-emits", failedTransport.Sent.Count == 0);

			// Delivered: only the ack round-trip was lost -> the ack is journaled.
			var (delivered, _) = RecoverWithFate(
				StageCrashWindowTell("g4_delivered"), t => t.SetFate(Order, TellFate.Delivered));
			DumpJournal("Recovered (transport testifies Delivered)", delivered);
			Check("an ack is journaled when the transport testifies Delivered",
				Journal(delivered).Contains($"tell ack '{Order}' from RewardEngine('rewards-1')"));

			// InFlight (default): the transport does not know -> the tell stays pending.
			var (pending, _) = RecoverWithFate(StageCrashWindowTell("g4_pending"), _ => { });
			string pendingJournal = Journal(pending);
			Check("the in-flight tell is still reconstructed from the journal",
				pendingJournal.Contains("PurchaseConfirmed") && pendingJournal.Contains($"'{Order}'"));
			Check("no verdict and no ack are journaled while the fate is InFlight",
				!pendingJournal.Contains("unacknowledged") && !pendingJournal.Contains("tell ack"));

			Console.WriteLine("  => after a crash, the sender's journal records each tell's FATE in its own voice");
			Console.WriteLine("     (acked / unacknowledged-by-addressee / pending), not just its issuance.");
		}

		// ==== G5: separated receiver — pure carrier + autonomous receiver (§8.2 C3) ==

		private static void G5_SeparatedReceiver()
		{
			Section("G5 — Separated receiver: pure in-process broker carrier + autonomous receiver (§8.2 C3)");
			InProcessBroker broker = new InProcessBroker();

			// Autonomous receiver: its own consumer takes up the inbound assertion and
			// runs a command the RewardEngine owns.
			PerformanceV2 rewards = CreateActor("sep_rewards");
			CreateRewardEngine(rewards);
			using BrokerTellConsumer consumer = new BrokerTellConsumer(broker, "loyalty-v1");
			consumer.OnReceive(rt => { ApplyRewards(rewards); return true; });

			// Sender over the broker as a pure carrier: a deployment binding maps the
			// addressee role to a topic; the sender names neither topic nor wire.
			PerformanceV2 seller = CreateActor("sep_seller");
			TellBindingTable bindings = new TellBindingTable().Bind("RewardEngine", "loyalty-v1");
			seller.UseTellTransport(new BrokerTellTransport(broker, bindings, witnessName: "broker"));
			DefinePurchaseFunnel(seller);
			ConfirmPurchase(seller);
			seller.Actor.Reactions.Execute();

			DumpJournal("Seller (origin)", seller);
			DumpJournal("RewardEngine (autonomous receiver)", rewards);
			Check("the receiver ran its own reward command autonomously",
				TotalRewards(rewards) == 1);
			Check("the origin's journal records the ack — round-trip closed over the pure carrier",
				Journal(seller).Contains($"tell ack '{Order}'"));
			Console.WriteLine("  => a pure in-process broker carried the envelope; the RewardEngine mapped the");
			Console.WriteLine("     assertion to its own command and acked autonomously — no bridge stood in (C3).");
		}

		// ==== Negative: a direct tell outside Causation.Continue is rejected =

		private static void Negative_DirectTellRejected()
		{
			Section("Negative — a direct tell from a top-level command is rejected");
			PerformanceV2 seller = CreateActor("neg");
			seller.UseTellTransport(new InMemoryTransport());

			bool threw = false;
			string message = "";
			try
			{
				seller.Using("tell PurchaseConfirmed with @order, @date, @amount, @customer to RewardEngine('rewards-1');")
					.WithParameters(p =>
					{
						p["order", typeof(string)] = Order;
						p["date", typeof(DateTime)] = PurchaseDate;
						p["amount", typeof(decimal)] = PurchaseAmount;
						p["customer", typeof(string)] = Customer;
					})
					.PerformCommand();
			}
			catch (LanguageException ex) { threw = true; message = ex.Message; }

			Check("a free-floating tell throws LanguageException", threw);
			Check("the error points at .Causation.Continue(...)", threw && message.Contains(".Causation.Continue(...)"));
		}
	}
}
