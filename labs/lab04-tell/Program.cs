using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Puppeteer;
using Puppeteer.EventSourcing.DB;
using Puppeteer.Tell;
using Puppeteer.UnitTest.LoyaltyDomain;
using Choreography.Transport.Brokered;

namespace Lab04Tell
{
	// Lab 4 — cross-actor causation (Paper 4 §8). Runs the loyalty scenario under
	// three cross-actor styles (saga, choreography, tell) and four property tests
	// (G1 replay, G2 cross-DC, G3 audit, G4 tell-fate recovery), a separated-receiver
	// run (pure carrier + autonomous receiver over an in-process broker), and the
	// negative gate — printing each actor's journal so the rendered entries can be
	// diffed against the paper.
	//
	// tell is the assertive speech act: the sender asserts a fact it lived
	// (`tell PurchaseConfirmed with ... to RewardEngine('rewards-1') once '...'`);
	// it names no receiver method and no transport. Built and run against the public
	// Puppeteer runtime at the cited provenance commit (see README). Journal reading
	// uses the framework's in-memory diary; the lab is granted internals via
	// InternalsVisibleTo("Lab04Tell"), like lab02.
	public static class Program
	{
		private static readonly Assembly DomainAssembly = typeof(Seller).Assembly;
		private static readonly Assembly PuppeteerAssembly = typeof(Actor).Assembly;
		private static int _failures;

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
			Console.WriteLine(_failures == 0
				? "ALL CHECKS PASSED."
				: $"{_failures} CHECK(S) FAILED.");
			Environment.Exit(_failures == 0 ? 0 : 1);
		}

		// ---- helpers -------------------------------------------------------

		private static (ActorV1 v1, DiaryStorageInMemory journal) CreateActor(string suffix)
		{
			string uniqueName = $"lab04_{suffix}_{Guid.NewGuid():N}";
			ActorV1 v1 = new ActorV1(uniqueName, DomainAssembly, PuppeteerAssembly);
			v1.CompiledModePolicy = CompilationModePolicy.AlwaysInterpreted;
			v1.Handler.EventSourcingStorage(DatabaseType.IN_MEMORY, "memory");
			DiaryStorageInMemory journal = new DiaryStorageInMemory(v1.Handler);
			return (v1, journal);
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

		private static void DumpJournal(string label, ActorV1 actor, DiaryStorageInMemory journal)
		{
			Console.WriteLine($"{label} ({journal.GetEventCount()} entries):");
			for (int i = 0; i < journal.GetEventCount(); i++)
				Console.WriteLine($"  [{i}] {RenderEntry(actor, journal.GetEvent(i))}");
		}

		private static string RenderEntry(ActorV1 actor, EventData e)
		{
			string raw;
			if (e is ScriptEventData s) raw = s.Script;
			else if (e is DefineEventData d) raw = $"(define action {d.ActionId})";
			else if (e is ActionEventData a)
				raw = actor.Handler.TryGetAction(a.ActionId, out var cache) ? cache.Script : $"(action {a.ActionId})";
			else raw = e.GetType().Name;
			return Regex.Replace(raw, @"\s+", " ").Trim();
		}

		private static void Check(string what, bool ok)
		{
			if (!ok) _failures++;
			Console.WriteLine($"  CHECK {(ok ? "PASS" : "FAIL")}: {what}");
		}

		private static void Check(string what, int expected, int actual)
			=> Check($"{what} (expected {expected}, got {actual})", expected == actual);

		// ---- Style 1: saga (orchestrator) ----------------------------------

		private static void Style1_Saga()
		{
			Section("Style 1 — Saga (orchestrator): joint history in the coordinator's journal");
			var (saga, sagaJournal) = CreateActor("saga_orchestrator");
			var (seller, sellerJournal) = CreateActor("saga_seller");
			var (rewards, rewardsJournal) = CreateActor("saga_rewards");

			rewards.Handler.PerformCmd(@"
				loyalty = RewardEngine();
				loyalty.AddCampaign('C-newcomer',   1/1/2020, 10);
				loyalty.AddCampaign('C-bigspender', 1/1/2020, 200);
			", "", "");

			saga.Handler.PerformCmd("step = 'PurchaseRequested';", "", "");
			seller.Handler.PerformCmd("s = Seller(); s.purchase('ord-100', 5/9/2026, 250, 'cust-42');", "", "");
			saga.Handler.PerformCmd("step = 'PurchaseConfirmed';", "", "");
			rewards.Handler.PerformCmd(@"
				foreach (c in loyalty.Campaigns()) {
					if (c.Applies(5/9/2026, 250) == true) { c.Reward('ord-100', 'cust-42'); };
				};
			", "", "");
			saga.Handler.PerformCmd("step = 'RewardsApplied';", "", "");

			DumpJournal("SagaCoordinator", saga, sagaJournal);
			DumpJournal("Seller", seller, sellerJournal);
			DumpJournal("RewardEngine", rewards, rewardsJournal);
			Check("coordinator holds the orchestration trace", 3, sagaJournal.GetEventCount());
			Check("Seller records only its local purchase", 1, sellerJournal.GetEventCount());
			Check("RewardEngine records only setup + reward", 2, rewardsJournal.GetEventCount());
			Console.WriteLine("  => the joint history lives ONLY in the coordinator's journal.");
		}

		// ---- Style 2: choreography (event bus, no coordinator) -------------

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
			var (seller, sellerJournal) = CreateActor("choreo_seller");
			var (rewards, rewardsJournal) = CreateActor("choreo_rewards");
			EventBus bus = new EventBus();

			bus.Subscribe(ev =>
			{
				if (ev.StartsWith("PurchaseConfirmed:"))
					rewards.Handler.PerformCmd(@"
						foreach (c in loyalty.Campaigns()) {
							if (c.Applies(5/9/2026, 250) == true) { c.Reward('ord-100', 'cust-42'); };
						};
					", "", "");
			});

			rewards.Handler.PerformCmd(@"
				loyalty = RewardEngine();
				loyalty.AddCampaign('C-newcomer', 1/1/2020, 10);
			", "", "");
			seller.Handler.PerformCmd("s = Seller(); s.purchase('ord-100', 5/9/2026, 250, 'cust-42');", "", "");
			bus.Publish("PurchaseConfirmed:ord-100");

			DumpJournal("Seller", seller, sellerJournal);
			DumpJournal("RewardEngine", rewards, rewardsJournal);
			Console.WriteLine($"Bus log ({bus.Log.Count} entries):");
			foreach (var l in bus.Log) Console.WriteLine($"  {l}");
			Check("Seller records only the local purchase (publish invisible)", 1, sellerJournal.GetEventCount());
			Check("RewardEngine records only setup + reward", 2, rewardsJournal.GetEventCount());
			Check("the only joint artifact is the bus log (outside any program)", 1, bus.Log.Count);
			Console.WriteLine("  => the joint history lives ONLY in the external bus log.");
		}

		// ---- Style 3: tell (assertive cross-actor primitive in the program) --

		// The Seller asserts a fact it lived — PurchaseConfirmed — addressed to the
		// RewardEngine, carrying the values that fact involved, under a stable identity.
		// It names no method on the RewardEngine and no transport.
		private static (ActorV1 seller, DiaryStorageInMemory sellerJournal, DiaryStorageInMemory rewardsJournal) RunTell(
			string idLiteral, bool deliver)
		{
			var (rewards, rewardsJournal) = CreateActor("tell_rewards");
			rewards.Handler.PerformCmd(@"
				loyalty = RewardEngine();
				loyalty.AddCampaign('C-newcomer', 1/1/2020, 10);
			", "", "");

			var (seller, sellerJournal) = CreateActor("tell_seller");
			InMemoryTransport transport = new InMemoryTransport();
			seller.Handler.Transport = transport;

			seller.Reactions.DefineReaction("PurchaseFunnelToRewards")
				.Job().Company()
				.WithSharedHydration()
				.Seek("Purchase")
					.OnMatch("[s:Seller].purchase($orderId, $date, $amount, $customer)")
				.Causation.Continue($@"
					tell PurchaseConfirmed
						with @orderId, @date, @amount, @customer
						to RewardEngine('rewards-1')
						once '{idLiteral}';
				");
			seller.Reactions.SetDairyStorage(new DiaryStorageInMemory(seller.Handler));

			seller.Handler.PerformCmd("s = Seller(); s.purchase('ord-100', 5/9/2026, 250, 'cust-42');", "", "");
			seller.Reactions.Execute();

			// Bridge: the receiver maps the asserted message to a command IT owns and
			// acks after committing. The directive lives receiver-side; the envelope
			// carried only the message, the addressee, and the values.
			if (deliver)
				foreach (TellEnvelope env in transport.Sent)
				{
					rewards.Handler.PerformCmd(@"
						foreach (c in loyalty.Campaigns()) {
							if (c.Applies(5/9/2026, 250) == true) { c.Reward('ord-100', 'cust-42'); };
						};
					", "", "");
					transport.TriggerAck(new AckEnvelope(env.Id, env.Addressee, env.AddresseeInstanceId));
				}

			return (seller, sellerJournal, rewardsJournal);
		}

		private static void Style3_Tell()
		{
			Section("Style 3 — Tell (Puppeteer): joint history in the sender's own journal");
			var (seller, sellerJournal, rewardsJournal) = RunTell("tid-comp-100", deliver: true);
			DumpJournal("Seller", seller, sellerJournal);
			Check("Seller carries the full joint history (purchase + define + tell + ack)", 4, sellerJournal.GetEventCount());
			Check("RewardEngine records only setup + reward", 2, rewardsJournal.GetEventCount());
			Check("the round-trip closed inside the Seller's own program", seller.Handler.SymbolTable.IsTellEnvelopeIdAcked("tid-comp-100"));
			Console.WriteLine("  => the joint history lives in the sender's journal as DSL sentences.");
		}

		// ---- G1: replay coherence (Paper 4 §5.2 / §8.5) --------------------

		private static void G1_ReplayCoherence()
		{
			Section("G1 — Replay coherence: a fresh actor reconstructs the in-flight tell from the journal alone");
			var (originalSeller, _, _) = RunTell("tid-purchase-100", deliver: false); // in-flight: no bridge, no ack

			ActorV1 replayed = new ActorV1(originalSeller.Name, DomainAssembly, PuppeteerAssembly);
			replayed.CompiledModePolicy = CompilationModePolicy.AlwaysInterpreted;
			replayed.Handler.Transport = new InMemoryTransport();
			replayed.Handler.EventSourcingStorage(DatabaseType.IN_MEMORY, "memory"); // triggers replay over the shared in-memory store

			Check("replayed actor knows the in-flight tell (reconstructed from journal)", replayed.Handler.SymbolTable.IsTellEnvelopeIdKnown("tid-purchase-100"));
			Check("replay does not re-emit the envelope", 0, ((InMemoryTransport)replayed.Handler.Transport).Sent.Count);
		}

		// ---- G2: cross-DC replication (Paper 4 §5.3 / §8.5) ----------------

		private static void G2_CrossDcReplication()
		{
			Section("G2 — Cross-DC replication: replicating the journal bytes alone carries the cross-actor chain");
			var (dc1Seller, dc1Journal, _) = RunTell("tid-purchase-100", deliver: false);

			string dc2Name = $"lab04_g2_dc2_{Guid.NewGuid():N}";
			ActorV1 dc2Setup = new ActorV1(dc2Name, DomainAssembly, PuppeteerAssembly);
			dc2Setup.CompiledModePolicy = CompilationModePolicy.AlwaysInterpreted;
			DiaryStorageInMemory dc2Journal = new DiaryStorageInMemory(dc2Setup.Handler);

			for (int i = 0; i < dc1Journal.GetEventCount(); i++)
			{
				EventData entry = dc1Journal.GetEvent(i);
				if (entry is ScriptEventData s)
					dc2Journal.AddScriptEvent(s.Script, s.OccurredAt, s.ExposeData);
				else if (entry is DefineEventData d)
					dc2Journal.WriteDefineEntry(d.ActionId, d.DefineStatementText, d.EntryId, d.OccurredAt, d.ExposeData);
				else if (entry is ActionEventData a)
				{
					if (!dc1Seller.Handler.TryGetAction(a.ActionId, out var cache))
						throw new InvalidOperationException($"DC1 has no cache entry for ActionId={a.ActionId}");
					string parametersDeclaration = cache.Program.Parameters.ParametersAsString();
					dc2Journal.AddActionEventWithRegistration(a.ActionId, cache.Script, parametersDeclaration, a.Arguments, a.OccurredAt);
				}
			}

			ActorV1 dc2 = new ActorV1(dc2Name, DomainAssembly, PuppeteerAssembly);
			dc2.CompiledModePolicy = CompilationModePolicy.AlwaysInterpreted;
			dc2.Handler.Transport = new InMemoryTransport();
			dc2.Handler.EventSourcingStorage(DatabaseType.IN_MEMORY, "memory");

			Check("DC2 reconstructs the cross-actor state from replicated bytes alone", dc2.Handler.SymbolTable.IsTellEnvelopeIdKnown("tid-purchase-100"));
			Check("DC2 does not re-emit the envelope", 0, ((InMemoryTransport)dc2.Handler.Transport).Sent.Count);
		}

		// ---- G3: audit query from the sender's journal alone --------------

		private static void G3_AuditQuery()
		{
			Section("G3 — Audit query: 'why did this happen?' answered by reading the sender's journal");
			var (seller, sellerJournal, _) = RunTell("tid-purchase-100", deliver: true);

			// The tell sentence (with @parameter references) lives in the action cache,
			// keyed by the ActionEventData's ActionId; the ack is the final ScriptEventData.
			ActionEventData tellInvocation = null;
			ScriptEventData ackEntry = null;
			for (int i = 0; i < sellerJournal.GetEventCount(); i++)
			{
				EventData e = sellerJournal.GetEvent(i);
				if (e is ActionEventData a) tellInvocation = a;
				if (e is ScriptEventData s && s.Script.Contains("tell ack")) ackEntry = s;
			}

			bool tellFound = tellInvocation != null && seller.Handler.TryGetAction(tellInvocation.ActionId, out var cache)
				&& cache.Script.Contains("tell PurchaseConfirmed")
				&& cache.Script.Contains("to RewardEngine('rewards-1')")
				&& cache.Script.Contains("orderId");
			Check("the cross-actor assertion is reconstructable from the journal (no trace store)", tellFound);
			Check("the acknowledgment is in the journal", ackEntry != null && ackEntry.Script.Contains("tid-purchase-100"));
		}

		// ---- G4: tell-fate recovery across the crash window (Paper 4 §8.5) -

		// Stages the crash window: a Seller observes a purchase via a Reaction whose
		// .Causation.Continue body asserts a single addressed tell with an explicit id.
		// The bridge is never run, so the tell sits in-flight — journaled as issued,
		// never dispatched, never acked. Returns the actor name so a fresh actor can
		// rehydrate over the same shared in-memory store.
		private static string StageCrashWindowTell(string suffix, string envelopeId)
		{
			var (seller, _) = CreateActor(suffix);
			seller.Handler.Transport = new InMemoryTransport();

			seller.Reactions.DefineReaction("PurchaseFunnelToRewards")
				.Job().Company()
				.WithSharedHydration()
				.Seek("Purchase")
					.OnMatch("[s:Seller].purchase($orderId, $date, $amount, $customer)")
				.Causation.Continue($@"
					tell PurchaseConfirmed
						with @orderId, @date, @amount, @customer
						to RewardEngine('rewards-1')
						once '{envelopeId}';
				");
			seller.Reactions.SetDairyStorage(new DiaryStorageInMemory(seller.Handler));

			seller.Handler.PerformCmd("s = Seller(); s.purchase('ord-100', 5/9/2026, 250, 'cust-42');", "", "");
			seller.Reactions.Execute();
			// No bridge — the tell stays in-flight on the (discarded) transport.
			return seller.Name;
		}

		// Rehydrates a fresh actor over the staged journal with a transport configured
		// to testify a fate. The transport is set BEFORE EventSourcingStorage so the
		// primary's post-replay recovery can cite it. Returns the actor + a journal view.
		private static (ActorV1 actor, DiaryStorageInMemory journal) RecoverWithFate(string actorName, Action<InMemoryTransport> configure)
		{
			ActorV1 actor = new ActorV1(actorName, DomainAssembly, PuppeteerAssembly);
			actor.CompiledModePolicy = CompilationModePolicy.AlwaysInterpreted;
			DiaryStorageInMemory journal = new DiaryStorageInMemory(actor.Handler);
			InMemoryTransport transport = new InMemoryTransport();
			configure(transport);
			actor.Handler.Transport = transport;
			actor.Handler.EventSourcingStorage(DatabaseType.IN_MEMORY, "memory"); // replay + post-replay RecoverPendingTells (primary)
			return (actor, journal);
		}

		private static bool JournalHas(ActorV1 actor, DiaryStorageInMemory journal, string fragment)
		{
			for (int i = 0; i < journal.GetEventCount(); i++)
				if (RenderEntry(actor, journal.GetEvent(i)).Contains(fragment)) return true;
			return false;
		}

		private static void G4_TellFateRecovery()
		{
			Section("G4 — Tell-fate recovery: the sender's journal records the FATE of a tell stranded by a crash");
			const string Id = "tid-purchase-100";

			// Failed: the transport testifies non-delivery -> the journal gains a
			// LOGICAL verdict naming the addressee (no transport named).
			var (failed, failedJournal) = RecoverWithFate(
				StageCrashWindowTell("g4_failed", Id),
				t => t.SetFate(Id, TellFate.Failed));
			DumpJournal("Recovered (transport testifies Failed)", failed, failedJournal);
			Check("a logical non-delivery verdict is journaled (unacknowledged by the addressee)",
				JournalHas(failed, failedJournal, $"tell '{Id}' unacknowledged by RewardEngine"));
			Check("the verdict names no transport", !JournalHas(failed, failedJournal, "per "));
			Check("dedup state is terminal not-delivered", failed.Handler.SymbolTable.IsTellEnvelopeIdNotDelivered(Id));
			Check("a failed tell is not falsely acked", !failed.Handler.SymbolTable.IsTellEnvelopeIdAcked(Id));
			Check("recovery testifies, never re-emits", 0, ((InMemoryTransport)failed.Handler.Transport).Sent.Count);

			// Delivered: only the ack round-trip was lost -> the ack is journaled.
			var (delivered, deliveredJournal) = RecoverWithFate(
				StageCrashWindowTell("g4_delivered", Id),
				t => t.SetFate(Id, TellFate.Delivered));
			DumpJournal("Recovered (transport testifies Delivered)", delivered, deliveredJournal);
			Check("an ack is journaled when the transport testifies Delivered",
				JournalHas(delivered, deliveredJournal, $"tell ack '{Id}' from RewardEngine('rewards-1')"));
			Check("dedup state is acked", delivered.Handler.SymbolTable.IsTellEnvelopeIdAcked(Id));

			// InFlight (default): the transport does not know -> the tell stays pending.
			var (pending, pendingJournal) = RecoverWithFate(
				StageCrashWindowTell("g4_pending", Id),
				_ => { });
			Check("the in-flight tell is still reconstructed from the journal", pending.Handler.SymbolTable.IsTellEnvelopeIdKnown(Id));
			Check("no verdict is journaled while the fate is InFlight", !JournalHas(pending, pendingJournal, "unacknowledged"));
			Check("the tell stays pending (neither acked nor not-delivered)",
				!pending.Handler.SymbolTable.IsTellEnvelopeIdAcked(Id) && !pending.Handler.SymbolTable.IsTellEnvelopeIdNotDelivered(Id));

			Console.WriteLine("  => after a crash, the sender's journal records each tell's FATE in its own voice");
			Console.WriteLine("     (acked / unacknowledged-by-addressee / pending), not just its issuance.");
		}

		// ---- G5: separated receiver — pure carrier + autonomous receiver (§8.2 C3) --

		// The C3 configuration the paper defends, exhibited as a run: the Seller asserts
		// over a PURE in-process broker carrier (no manual bridge), and the RewardEngine
		// runs its OWN consumer that maps the asserted message to a command it owns,
		// journals that command in its own journal, and acks autonomously. No party
		// stands in for the receiver.
		private static void G5_SeparatedReceiver()
		{
			Section("G5 — Separated receiver: pure in-process broker carrier + autonomous receiver (§8.2 C3)");
			InProcessBroker broker = new InProcessBroker();

			// Autonomous receiver: its own consumer takes up the inbound assertion and
			// runs a command RewardEngine owns.
			var (rewards, rewardsJournal) = CreateActor("sep_rewards");
			rewards.Handler.PerformCmd(@"
				loyalty = RewardEngine();
				loyalty.AddCampaign('C-newcomer', 1/1/2020, 10);
			", "", "");
			using BrokerTellConsumer consumer = new BrokerTellConsumer(broker, "loyalty-v1");
			consumer.OnReceive(rt =>
			{
				rewards.Handler.PerformCmd(@"
					foreach (c in loyalty.Campaigns()) {
						if (c.Applies(5/9/2026, 250) == true) { c.Reward('ord-100', 'cust-42'); };
					};
				", "", "");
				return true;
			});

			// Sender over the broker as a pure carrier: a deployment-level binding maps
			// the addressee role to a topic; the sender names neither topic nor wire.
			var (seller, sellerJournal) = CreateActor("sep_seller");
			TellBindingTable bindings = new TellBindingTable().Bind("RewardEngine", "loyalty-v1");
			seller.Handler.Transport = new BrokerTellTransport(broker, bindings, witnessName: "broker");

			seller.Reactions.DefineReaction("PurchaseFunnelToRewards")
				.Job().Company()
				.WithSharedHydration()
				.Seek("Purchase")
					.OnMatch("[s:Seller].purchase($orderId, $date, $amount, $customer)")
				.Causation.Continue(@"
					tell PurchaseConfirmed
						with @orderId, @date, @amount, @customer
						to RewardEngine('rewards-1')
						once 'tid-sep-1';
				");
			seller.Reactions.SetDairyStorage(new DiaryStorageInMemory(seller.Handler));

			seller.Handler.PerformCmd("s = Seller(); s.purchase('ord-100', 5/9/2026, 250, 'cust-42');", "", "");
			seller.Reactions.Execute();

			DumpJournal("Seller (origin)", seller, sellerJournal);
			DumpJournal("RewardEngine (autonomous receiver)", rewards, rewardsJournal);
			Check("the receiver ran its own command autonomously (setup + reward in its own journal)", 2, rewardsJournal.GetEventCount());
			Check("the origin's journal records the ack — round-trip closed over the pure carrier", seller.Handler.SymbolTable.IsTellEnvelopeIdAcked("tid-sep-1"));
			Console.WriteLine("  => a pure in-process broker carried the envelope; the RewardEngine mapped the");
			Console.WriteLine("     assertion to its own command and acked autonomously — no bridge stood in (C3).");
		}

		// ---- Negative: a direct tell outside Causation.Continue is rejected -

		private static void Negative_DirectTellRejected()
		{
			Section("Negative — a direct tell from a top-level command is rejected");
			ActorV1 seller = new ActorV1($"lab04_neg_{Guid.NewGuid():N}", DomainAssembly, PuppeteerAssembly);
			seller.CompiledModePolicy = CompilationModePolicy.AlwaysInterpreted;
			seller.Handler.Transport = new InMemoryTransport();

			bool threw = false;
			string message = "";
			try
			{
				seller.Handler.PerformCmd("tell PurchaseConfirmed with 'x', 1/1/2026, 1, 'y' to RewardEngine('rewards-1');", "", "");
			}
			catch (LanguageException ex) { threw = true; message = ex.Message; }

			Check("a free-floating tell throws LanguageException", threw);
			Check("the error points at .Causation.Continue(...)", threw && message.Contains(".Causation.Continue(...)"));
		}
	}
}
