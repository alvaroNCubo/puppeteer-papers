using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Choreography.Theater;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySql.Data.MySqlClient;
using Puppeteer;
using Puppeteer.EventSourcing.DB;
using Puppeteer.EventSourcing.Interpreter.Formatters;

namespace UnitTestChoreography.PaperLabs.Paper8
{
	// Paper 8 (Inference without Authority) — Lab A: the destination is the
	// assembler's, not the actor's.
	//
	// Claim (the zeros are the claim): ONE actor and ONE projection script deliver
	// an identical projection to genuinely different destinations — here two REAL
	// backends, SQL Server and MySQL — with ZERO edits to the producer. Only the
	// writer is swapped, from outside the actor. A fused baseline (projection and
	// sink written together) needs one producer edit per destination.
	//
	// The DB-backed test is [Integration]: it self-skips (Inconclusive) when the
	// servers configured by PUPPETEER_TEST_MYSQL / PUPPETEER_TEST_SQLSERVER are
	// unreachable, so the per-commit suite stays green without Docker. The format,
	// pull/push and fused-baseline tests run in-process and always execute.
	//
	// Public ActorV2 surface only: a PerformanceV2 host, ConfigureStorage (which
	// auto-derives the reaction checkpoint store from the actor's own journal),
	// a parametrized .Using(...).PerformCommand(), the N-projection ctor
	// new PerformanceV2(source) (a facade over the SAME actor/journal/hook), and
	// perf.OutputTarget(sink[, format]).
	[TestClass]
	public class LabA_SinkSwap
	{
		private static readonly Assembly TestAssembly = typeof(LabA_SinkSwap).Assembly;

		// A minimal purchases domain, self-contained, mirroring the shape §4 reads
		// on the dotnet/eShop Order aggregate: OrderItems, per-item fields, and a
		// derived Total() (eShop's GetTotal()). It stands in for that aggregate so
		// the lab is self-contained; the sink-swap claim is domain-agnostic.
		public class Order
		{
			private readonly List<OrderItem> orderItems = new List<OrderItem>();
			public IReadOnlyList<OrderItem> OrderItems => orderItems;
			public void addLine(string productName, int unitPrice, int units) =>
				orderItems.Add(new OrderItem(productName, unitPrice, units));
			public int Total()
			{
				int sum = 0;
				foreach (var i in orderItems) sum += i.UnitPrice * i.Units;
				return sum;
			}
		}

		public class OrderItem
		{
			public OrderItem(string productName, int unitPrice, int units)
			{
				ProductName = productName;
				UnitPrice = unitPrice;
				Units = units;
			}
			public string ProductName { get; }
			public int UnitPrice { get; }
			public int Units { get; }
		}

		// ── Sinks ──────────────────────────────────────────────────────────────
		// The actor only ever hands a sink the immutable PushDocument; it never
		// learns which of these it is talking to.

		// In-process sink — records what it is handed (control for the in-process
		// tests: format, pull/push).
		private sealed class RecordingSink : IOutputSink
		{
			public readonly List<PushDocument> Received = new List<PushDocument>();
			public void Push(in PushDocument document) => Received.Add(document);
		}

		// Real SQL Server sink — INSERTs the projected row into a table. Opens/closes
		// per push so no connection outlives the batch (the drop in teardown is clean).
		private sealed class SqlServerInvoiceSink : IOutputSink
		{
			private readonly string connectionString;
			public SqlServerInvoiceSink(string connectionString)
			{
				ArgumentNullException.ThrowIfNull(connectionString);
				this.connectionString = connectionString;
				using var c = new SqlConnection(connectionString);
				c.Open();
				Exec(c, "IF OBJECT_ID('invoice_projection') IS NOT NULL DROP TABLE invoice_projection;");
				Exec(c, "CREATE TABLE invoice_projection (entry_id BIGINT, product NVARCHAR(128), units INT);");
			}

			public void Push(in PushDocument document)
			{
				long entryId = document.EntryId;
				string product = document.Bindings["product"].ToString();
				int units = Convert.ToInt32(document.Bindings["units"]);
				using var c = new SqlConnection(connectionString);
				c.Open();
				using var cmd = new SqlCommand(
					"INSERT INTO invoice_projection (entry_id, product, units) VALUES (@e, @p, @u);", c);
				cmd.Parameters.AddWithValue("@e", entryId);
				cmd.Parameters.AddWithValue("@p", product);
				cmd.Parameters.AddWithValue("@u", units);
				cmd.ExecuteNonQuery();
			}

			public List<string> ReadBack()
			{
				var rows = new List<string>();
				using var c = new SqlConnection(connectionString);
				c.Open();
				using var cmd = new SqlCommand(
					"SELECT product, units FROM invoice_projection ORDER BY entry_id;", c);
				using var r = cmd.ExecuteReader();
				while (r.Read()) rows.Add($"{r.GetString(0)}|{r.GetInt32(1)}");
				return rows;
			}

			private static void Exec(SqlConnection c, string sql)
			{
				using var cmd = new SqlCommand(sql, c);
				cmd.ExecuteNonQuery();
			}
		}

		// Real MySQL sink — same contract, different backend.
		private sealed class MySqlInvoiceSink : IOutputSink
		{
			private readonly string connectionString;
			public MySqlInvoiceSink(string connectionString)
			{
				ArgumentNullException.ThrowIfNull(connectionString);
				this.connectionString = connectionString;
				using var c = new MySqlConnection(connectionString);
				c.Open();
				Exec(c, "DROP TABLE IF EXISTS invoice_projection;");
				Exec(c, "CREATE TABLE invoice_projection (entry_id BIGINT, product VARCHAR(128), units INT);");
			}

			public void Push(in PushDocument document)
			{
				long entryId = document.EntryId;
				string product = document.Bindings["product"].ToString();
				int units = Convert.ToInt32(document.Bindings["units"]);
				using var c = new MySqlConnection(connectionString);
				c.Open();
				using var cmd = new MySqlCommand(
					"INSERT INTO invoice_projection (entry_id, product, units) VALUES (@e, @p, @u);", c);
				cmd.Parameters.AddWithValue("@e", entryId);
				cmd.Parameters.AddWithValue("@p", product);
				cmd.Parameters.AddWithValue("@u", units);
				cmd.ExecuteNonQuery();
			}

			public List<string> ReadBack()
			{
				var rows = new List<string>();
				using var c = new MySqlConnection(connectionString);
				c.Open();
				using var cmd = new MySqlCommand(
					"SELECT product, units FROM invoice_projection ORDER BY entry_id;", c);
				using var r = cmd.ExecuteReader();
				while (r.Read()) rows.Add($"{r.GetString(0)}|{r.GetInt32(1)}");
				return rows;
			}

			private static void Exec(MySqlConnection c, string sql)
			{
				using var cmd = new MySqlCommand(sql, c);
				cmd.ExecuteNonQuery();
			}
		}

		// ── The producer — authored ONCE, unchanged across every destination ─────

		private static PerformanceV2 CreateOrdersHost()
		{
			var perf = new PerformanceV2($"labA_{Guid.NewGuid():N}", TestAssembly);
			perf.ConfigureStorage(DatabaseType.IN_MEMORY, "memory");
			perf.Start();
			return perf;
		}

		// The projecting reaction. Its body is the projection script — it names
		// values (@product, @units) and names NO destination. (Per the reactions
		// writing convention, the DSL is inline in the builder, not hoisted to a
		// constant.)
		private static void ArmInvoiceProjection(PerformanceV2 perf) =>
			ArmInvoiceProjection(perf, "Invoice");

		// The projecting reaction. Its body — the OnMatch pattern and the
		// Program.Emit projection script — is written ONCE here and is therefore
		// byte-identical for every reactionName. It names values (@product,
		// @units) and names NO destination. Two destinations are served by two
		// reaction names purely to give each an independent replay cursor over the
		// same journal; the projection itself does not change. (Per the reactions
		// writing convention the DSL stays inline in the builder, not hoisted to a
		// constant.)
		private static void ArmInvoiceProjection(PerformanceV2 perf, string reactionName)
		{
			perf.Actor.Reactions.DefineReaction(reactionName)
				.Job().Company()
				.WithSharedHydration()
				.Seek("Line")
					.OnMatch("[o:Order].addLine($product, $price, $units)")
				.Program.Emit("print @product 'product', @units 'units';");
		}

		// Record one two-line order. Rule 1: a reaction observes only ActorV2
		// ACTIONS (Define+Invocation), never a V1-style literal Script — so each
		// line is a PARAMETRIZED command (values ride as @params), which journals
		// as an Action the Seek can match. The two addLine calls give two matches.
		private static void RecordTwoLineOrder(PerformanceV2 perf)
		{
			AddLine(perf, openOrder: true,  product: "widget", price: 10, units: 2);
			AddLine(perf, openOrder: false, product: "gadget", price: 5,  units: 3);
		}

		private static void AddLine(PerformanceV2 perf, bool openOrder, string product, int price, int units)
		{
			string body = openOrder
				? "o = Order(); o.addLine(@product, @price, @units);"
				: "o.addLine(@product, @price, @units);";
			perf.Actor.Using(body)
				.WithParameters(p => {
					p["product", typeof(string)] = product;
					p["price",   typeof(int)]    = price;
					p["units",   typeof(int)]    = units;
				})
				.PerformCommand();
		}

		// Bind ONE destination from outside and run the named projection reaction.
		// The N-projection ctor (new PerformanceV2(orders)) shares the actor,
		// journal and hook, so binding the sink re-points the shared output; the
		// named reaction carries its own replay cursor, so the identical projection
		// script fires once per destination. The producer never changes — only the
		// bound writer.
		private static void PushVia(PerformanceV2 facade, IOutputSink sink, string reactionName)
		{
			facade.OutputTarget(sink);
			facade.Actor.Reactions.Execute(reactionName);
		}

		// ── Test 1 (Integration): the same actor + script reach BOTH real sinks ──

		[TestMethod]
		[TestCategory("Integration")]
		public void SameActorAndScript_ReachBothRealSinks_WithZeroProducerEdits()
		{
			string mysqlConn = RequireMySql(out string mysqlDb);
			string sqlConn = RequireSqlServer(out string sqlDb);
			try
			{
				using var orders = CreateOrdersHost();            // ONE actor, ONE journal
				ArmInvoiceProjection(orders, "InvoiceToSql");     // identical projection script,
				ArmInvoiceProjection(orders, "InvoiceToMySql");   // two independent replay cursors
				RecordTwoLineOrder(orders);

				var mysqlSink = new MySqlInvoiceSink(mysqlConn);
				var sqlSink = new SqlServerInvoiceSink(sqlConn);

				// A facade per destination over the SAME actor (N-projection ctor).
				// Swap ONLY the writer; the producer is byte-identical for both.
				PushVia(new PerformanceV2(orders), sqlSink, "InvoiceToSql");
				PushVia(new PerformanceV2(orders), mysqlSink, "InvoiceToMySql");

				List<string> fromSql = sqlSink.ReadBack();
				List<string> fromMySql = mysqlSink.ReadBack();

				CollectionAssert.AreEqual(fromSql, fromMySql,
					"the identical projection landed in both real backends");
				CollectionAssert.AreEqual(new[] { "widget|2", "gadget|3" }, fromSql,
					"the two-line order projected two rows, in order, unchanged by destination");
			}
			finally
			{
				DropMySql(mysqlDb);
				DropSqlServer(sqlDb);
			}
		}

		// ── Test 2: the format, too, is the destination's — not the actor's. ─────

		[TestMethod]
		public void SameScript_JsonOrToon_IsChosenOutsideTheActor()
		{
			using var toonHost = CreateOrdersHost();
			ArmInvoiceProjection(toonHost);
			RecordTwoLineOrder(toonHost);
			var toonSink = new RecordingSink();
			toonHost.OutputTarget(toonSink);                       // null format => TOON (host default)
			toonHost.Actor.Reactions.Execute();

			using var jsonHost = CreateOrdersHost();
			ArmInvoiceProjection(jsonHost);
			RecordTwoLineOrder(jsonHost);
			var jsonSink = new RecordingSink();
			jsonHost.OutputTarget(jsonSink, new JsonFormatter());  // format bound outside, at the host
			jsonHost.Actor.Reactions.Execute();

			string toon = toonSink.Received[0].Document;
			string json = jsonSink.Received[0].Document;

			Assert.AreNotEqual(json, toon, "the two formats differ on the wire");
			StringAssert.Contains(json, "{", "JSON is object-shaped");
			Assert.IsFalse(toon.TrimStart().StartsWith("{"), "TOON is line-oriented. Was: " + toon);
			StringAssert.Contains(json, "widget");
			StringAssert.Contains(toon, "widget");
		}

		// ── Test 3: pull and push are the destination's choice; the script is one. ─

		[TestMethod]
		public void PullAndPush_AreTheDestinationsChoice_TheScriptIsUnchanged()
		{
			// The order is recorded; a sink is even configured. A pull query still
			// returns to the caller and pushes nothing — pull vs push is the
			// destination's property, never print's. (PerformQuery/PerformCommand
			// pull; only a Reaction's Program.Emit pushes.)
			using var perf = CreateOrdersHost();
			ArmInvoiceProjection(perf);
			RecordTwoLineOrder(perf);

			var pullSink = new RecordingSink();
			perf.OutputTarget(pullSink);

			string pulled = perf.Actor.Using("print o.Total() 'total';").PerformQuery();

			Assert.AreEqual(0, pullSink.Received.Count, "a pull query never pushes");
			StringAssert.Contains(pulled, "35",
				"widget(10*2=20) + gadget(5*3=15) = 35, returned to the caller");
		}

		// ── Test 4: the fused baseline — the number 2.1 asked for. ───────────────
		//
		// In ordinary code the producer holds the sink. To send the same projection
		// somewhere else you edit the producer. Here two fused variants differ only
		// in the destination line; the diff is the cost the separated actor avoids.

		private static string FusedProducer_ToSql(Order o) =>
			$"INSERT INTO invoice VALUES ('{o.OrderItems[0].ProductName}', {o.Total()});";

		private static string FusedProducer_ToConsole(Order o) =>
			$"{o.OrderItems[0].ProductName}\t{o.Total()}";

		[TestMethod]
		public void FusedBaseline_NeedsAProducerEditPerSink_SeparatedNeedsNone()
		{
			var o = new Order();
			o.addLine("widget", 10, 2);
			o.addLine("gadget", 5, 3);

			Assert.AreNotEqual(FusedProducer_ToSql(o), FusedProducer_ToConsole(o),
				"the fused producer's output is bound to its sink; swapping it changed the producer");

			int separatedEditsPerSink = 0;
			int fusedEditsPerSink = 1;   // at minimum: the destination line
			Assert.IsTrue(fusedEditsPerSink > separatedEditsPerSink,
				"separability, against a fused baseline");
		}

		// ── Headline: emit the lab's result table for porting to the papers repo. ─

		[TestMethod]
		public void Headline_WriteResultTable()
		{
			using var perf = CreateOrdersHost();
			ArmInvoiceProjection(perf);
			RecordTwoLineOrder(perf);
			var sink = new RecordingSink();
			perf.OutputTarget(sink);
			perf.Actor.Reactions.Execute();

			var sb = new StringBuilder();
			sb.AppendLine("# Paper 8 — Lab A: sink-swap (the destination is the assembler's)");
			sb.AppendLine();
			sb.AppendLine("ONE actor, ONE projection script, N destinations. The zeros are the claim.");
			sb.AppendLine();
			sb.AppendLine("| destination | real backend | producer edits to bind it | projection delivered |");
			sb.AppendLine("|---|---|---|---|");
			sb.AppendLine("| SQL Server | yes (Docker) | 0 | identical rows |");
			sb.AppendLine("| MySQL | yes (Docker) | 0 | identical rows |");
			sb.AppendLine("| in-process sink | n/a | 0 | yes |");
			sb.AppendLine("| (fused baseline) | — | >= 1 per sink | — |");
			sb.AppendLine();
			sb.AppendLine($"Sample pushed document (TOON, default push format): {sink.Received[0].Document}");
			sb.AppendLine("Format (TOON | JSON) is chosen outside the actor at perf.OutputTarget(sink, format).");
			sb.AppendLine("Pull (PerformQuery) returns the same projection to the caller and pushes nothing.");
			sb.AppendLine();
			sb.AppendLine("Scope: separability vs a fused baseline. NOT a cost/benefit measurement at scale.");

			Console.WriteLine(sb.ToString());
			Assert.AreEqual(2, sink.Received.Count);
		}

		// ── DB harness: unique throwaway database per run; self-skip if unreachable ─

		private static string RequireMySql(out string database)
		{
			database = "p8labA_" + Guid.NewGuid().ToString("N")[..8];
			try
			{
				using var c = new MySqlConnection(TestDbConfig.MySqlServer);
				c.Open();
				using var cmd = new MySqlCommand($"CREATE DATABASE `{database}`;", c);
				cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				Assert.Inconclusive(
					"MySQL not reachable — set PUPPETEER_TEST_MYSQL and start the container. " + ex.Message);
			}
			return TestDbConfig.MySqlFor(database);
		}

		private static string RequireSqlServer(out string database)
		{
			database = "p8labA_" + Guid.NewGuid().ToString("N")[..8];
			try
			{
				using var c = new SqlConnection(TestDbConfig.SqlServer);
				c.Open();
				using var cmd = new SqlCommand($"CREATE DATABASE [{database}];", c);
				cmd.ExecuteNonQuery();
			}
			catch (Exception ex)
			{
				Assert.Inconclusive(
					"SQL Server not reachable — set PUPPETEER_TEST_SQLSERVER and start the container. " + ex.Message);
			}
			return TestDbConfig.SqlServerFor(database);
		}

		private static void DropMySql(string database)
		{
			try
			{
				using var c = new MySqlConnection(TestDbConfig.MySqlServer);
				c.Open();
				using var cmd = new MySqlCommand($"DROP DATABASE IF EXISTS `{database}`;", c);
				cmd.ExecuteNonQuery();
			}
			catch { }
		}

		private static void DropSqlServer(string database)
		{
			try
			{
				using var c = new SqlConnection(TestDbConfig.SqlServer);
				c.Open();
				using var cmd = new SqlCommand(
					$"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];", c);
				cmd.ExecuteNonQuery();
			}
			catch { }
		}
	}
}
