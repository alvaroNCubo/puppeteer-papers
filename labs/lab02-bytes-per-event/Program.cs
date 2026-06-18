using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Puppeteer;
using Puppeteer.EventSourcing.DB.FileSystem;

namespace Lab02BytesPerEvent;

internal static class Program
{
	// Filesystem framing bytes (4-byte body-length prefix + 4-byte CRC trailer)
	// that EncodeXxxEvent emits as part of the record but that are NOT part of
	// the "at-rest body" per P1 firmado 2026-05-14. Excluded from encoded_bytes.
	private const int FILESYSTEM_FRAMING_BYTES = 4 + 4;

	// CompactArgsFor returns the V2 on-wire string: comma-separated parameter
	// values, matching Parameters.ArgumentsAsString() (the runtime's actual
	// serialization for EncodeActionEvent.arguments).
	// ParamAssignmentsFor returns the V1 pre-Action concatenation: one
	// `name := value;` per parameter, prepended to the script body so the
	// resulting Script entry is self-contained (cf. CLAUDE.md note: "ActorV1
	// build the script concatenating parameters into the script").
	private record Tier(int Id, string Label, string ScriptBody, string ParamsDeclaration,
		Func<int, string> CompactArgsFor, Func<int, string> ParamAssignmentsFor);

	private static readonly Tier[] Tiers = new[]
	{
		new Tier(
			Id: 1,
			Label: "arithmetic-shallow",
			ScriptBody: "target := 0; for i := 1 to upper { target := target + i * factor; }",
			ParamsDeclaration: "upper:int, factor:int",
			CompactArgsFor: i => $"{10 + (i % 5)},{1 + (i % 3)}",
			ParamAssignmentsFor: i => $"upper := {10 + (i % 5)}; factor := {1 + (i % 3)};"),

		new Tier(
			Id: 2,
			Label: "branching-arith-medium",
			ScriptBody: "sum := 0; for i := 1 to upper { if i mod 2 = 0 then sum := sum + i * factor; else sum := sum - i; }",
			ParamsDeclaration: "upper:int, factor:int",
			CompactArgsFor: i => $"{20 + (i % 7)},{1 + (i % 4)}",
			ParamAssignmentsFor: i => $"upper := {20 + (i % 7)}; factor := {1 + (i % 4)};"),

		new Tier(
			Id: 3,
			Label: "production-verb-synthetic",
			ScriptBody: SyntheticProductionVerbBody(),
			ParamsDeclaration: "p01:string, p02:string, p03:int, p04:int, p05:int, p06:int, p07:int, p08:int, p09:string, p10:string, p11:string, p12:int, p13:int, p14:string, p15:string, p16:int, p17:int",
			CompactArgsFor: SyntheticProductionVerbCompactArgs,
			ParamAssignmentsFor: SyntheticProductionVerbParamAssignments)
	};

	private static int Main(string[] args)
	{
		string repoRoot = args.Length > 0 ? args[0] : ResolveRepoRoot();
		string sha = ResolveLabBranchSha(repoRoot);
		string utc = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
		string runDir = Path.Combine(
			repoRoot,
			"UnitTestPuppeteer", "PaperLabs", "paper5", "lab2-bytes-per-event",
			"results",
			$"run-{utc}-{sha}");
		Directory.CreateDirectory(runDir);

		int[] nValues = { 100, 1000 };

		var samples = new List<SampleRow>(nValues.Sum() * Tiers.Length * 3);
		var definitions = new List<DefinitionRow>();

		foreach (var tier in Tiers)
		{
			byte[] scriptBytes = Encoding.UTF8.GetBytes(tier.ScriptBody);
			byte[] paramsBytes = Encoding.UTF8.GetBytes(tier.ParamsDeclaration);

			string defineStatement = $"define action {tier.Id} ({tier.ParamsDeclaration}) as {tier.ScriptBody} end;";
			byte[] defineRecord = BinaryEventCodec.EncodeDefineEvent(
				entryId: 0,
				fechaHora: DateTime.UnixEpoch,
				ip: IpAddress.DEFAULT.Ip,
				user: UserInLog.ANONYMOUS.Id,
				actionId: tier.Id,
				defineStatementText: defineStatement);

			definitions.Add(new DefinitionRow(
				Tier: tier.Id,
				ActionId: tier.Id,
				ScriptBytes: scriptBytes.Length,
				ParamsBytes: paramsBytes.Length,
				TotalDefBytes: defineRecord.Length - FILESYSTEM_FRAMING_BYTES,
				ScriptPreviewAnonymized: PreviewOf(tier.ScriptBody)));
		}

		foreach (int n in nValues)
		{
			foreach (var tier in Tiers)
			{
				for (int i = 1; i <= n; i++)
				{
					string compactArgs = tier.CompactArgsFor(i);
					byte[] compactArgsBytes = Encoding.UTF8.GetBytes(compactArgs);

					byte[] compactRecord = BinaryEventCodec.EncodeActionEvent(
						entryId: i,
						fechaHora: DateTime.UnixEpoch,
						ip: IpAddress.DEFAULT.Ip,
						user: UserInLog.ANONYMOUS.Id,
						actionId: tier.Id,
						arguments: compactArgs);
					int compactBody = compactRecord.Length - FILESYSTEM_FRAMING_BYTES;
					int compactPayload = compactArgsBytes.Length;
					samples.Add(new SampleRow(
						Tier: tier.Id,
						TierLabel: tier.Label,
						N: n,
						Iteration: i,
						Format: "compact",
						Compression: "none",
						EncodedBytes: compactBody,
						PayloadBytes: compactPayload,
						FramingOverheadBytes: compactBody - compactPayload,
						GitSha: sha));

					string literalScript = $"{tier.ParamAssignmentsFor(i)} {tier.ScriptBody}";
					byte[] literalScriptBytes = Encoding.UTF8.GetBytes(literalScript);

					byte[] literalRecord = BinaryEventCodec.EncodeScriptEvent(
						entryId: i,
						fechaHora: DateTime.UnixEpoch,
						ip: IpAddress.DEFAULT.Ip,
						user: UserInLog.ANONYMOUS.Id,
						script: literalScript);
					int literalBody = literalRecord.Length - FILESYSTEM_FRAMING_BYTES;
					int literalPayload = literalScriptBytes.Length;
					samples.Add(new SampleRow(
						Tier: tier.Id,
						TierLabel: tier.Label,
						N: n,
						Iteration: i,
						Format: "literal",
						Compression: "none",
						EncodedBytes: literalBody,
						PayloadBytes: literalPayload,
						FramingOverheadBytes: literalBody - literalPayload,
						GitSha: sha));

					byte[] literalGzRecord = BinaryEventCodec.EncodeScriptEvent(
						entryId: i,
						fechaHora: DateTime.UnixEpoch,
						ip: IpAddress.DEFAULT.Ip,
						user: UserInLog.ANONYMOUS.Id,
						script: literalScript,
						compression: PayloadCompression.GZip);
					int literalGzBody = literalGzRecord.Length - FILESYSTEM_FRAMING_BYTES;
					// Framing bytes of a literal Script record (envelope around the payload, excluding payload itself).
					// For the gzip variant the same framing applies; only the payload bytes shrink.
					int literalFramingFixed = literalBody - literalPayload;
					int literalGzPayload = literalGzBody - literalFramingFixed;
					samples.Add(new SampleRow(
						Tier: tier.Id,
						TierLabel: tier.Label,
						N: n,
						Iteration: i,
						Format: "literal",
						Compression: "gzip",
						EncodedBytes: literalGzBody,
						PayloadBytes: literalGzPayload,
						FramingOverheadBytes: literalFramingFixed,
						GitSha: sha));
				}
			}
		}

		WriteSamples(Path.Combine(runDir, "samples.csv"), samples);
		WriteSummary(Path.Combine(runDir, "summary.csv"), samples, definitions);
		WriteDefinitions(Path.Combine(runDir, "definitions.csv"), definitions);

		Console.WriteLine($"L2 dataset written to: {runDir}");
		Console.WriteLine($"  samples.csv:     {samples.Count} rows");
		Console.WriteLine($"  summary.csv:     {nValues.Length * Tiers.Length * 3} rows");
		Console.WriteLine($"  definitions.csv: {definitions.Count} rows");
		return 0;
	}

	private static string PreviewOf(string s) => s.Length > 60 ? s.Substring(0, 60) + "..." : s;

	// Synthetic DSL-shaped stand-in calibrated to match the byte length of the Paper 2 Lab 4
	// production verb (def 2: script_bytes = 681 B). Anonymized per unified_principle: no
	// prior-system commercial identifiers appear here. Bytes-per-event ratios are
	// invariant to the actual text content; gzip ratio depends on entropy and is reported
	// with caveats in headline.md.
	private static string SyntheticProductionVerbBody()
	{
		string body =
			"{ context = domain.LookupContext(p01, p02); " +
			"purchase = context.NewPurchase(p03, p04); " +
			"for sub := 1 to p05 { ticket = purchase.NewSubticket(p06, p07); " +
			"for drawing := 1 to p08 { ticket.AddDrawing(p09, p10, p11); " +
			"for line := 1 to p12 { ticket.AddLine(p13, drawing, line); } } " +
			"ticket.AssignReference(p14, p15); } " +
			"purchase.ApplyFees(p16, p17); " +
			"purchase.Confirm(p01, p02); " +
			"context.Audit.Record(purchase.Id, p03, p04, FixedNow); " +
			"context.SaveAndIndex(purchase.Id, p01); }";
		// Pad / truncate to match the Paper 2 Lab 4 def 2 reference length (681 bytes UTF-8).
		const int target = 681;
		byte[] currentBytes = Encoding.UTF8.GetBytes(body);
		if (currentBytes.Length < target)
		{
			body = body + new string(' ', target - currentBytes.Length);
		}
		else if (currentBytes.Length > target)
		{
			body = Encoding.UTF8.GetString(currentBytes, 0, target);
		}
		return body;
	}

	// V2 on-wire arguments: comma-separated parameter values
	// (Parameters.ArgumentsAsString format). Target ~63 B avg to match Paper 2
	// Lab 4 production verb's measured compact payload (67 B = 4 actionId + 63 args).
	private static string SyntheticProductionVerbCompactArgs(int iteration)
	{
		return $"'oh','ga',{1000 + iteration},{(iteration % 9000) + 1000},2,3,{iteration},2,'PB',{iteration * 7 % 90},'std',{iteration % 4},5,'ref{iteration}','batch',1,2";
	}

	// V1 pre-Action concatenation: one `name := value;` per user parameter,
	// prepended to the script body so the Script entry is self-contained.
	// 17 assignments × ~37 B avg ≈ 630 B; combined with the 681 B body the
	// resulting literal payload tracks Paper 2 Lab 4's ~1,313 B/event projection.
	private static string SyntheticProductionVerbParamAssignments(int iteration)
	{
		StringBuilder sb = new StringBuilder(640);
		sb.Append("p01 := 'oh'; ");
		sb.Append("p02 := 'ga'; ");
		sb.Append($"p03 := {1000 + iteration}; ");
		sb.Append($"p04 := {(iteration % 9000) + 1000}; ");
		sb.Append("p05 := 2; ");
		sb.Append("p06 := 3; ");
		sb.Append($"p07 := {iteration}; ");
		sb.Append("p08 := 2; ");
		sb.Append("p09 := 'PB'; ");
		sb.Append($"p10 := {iteration * 7 % 90}; ");
		sb.Append("p11 := 'std'; ");
		sb.Append($"p12 := {iteration % 4}; ");
		sb.Append("p13 := 5; ");
		sb.Append($"p14 := 'ref{iteration}'; ");
		sb.Append("p15 := 'batch'; ");
		sb.Append("p16 := 1; ");
		sb.Append("p17 := 2;");
		return sb.ToString();
	}

	private static string ResolveRepoRoot()
	{
		// The helper lives in the sibling repo puppeteer-papers; the runtime is a
		// sibling clone of the public Puppeteer repo at <repos-parent>/puppeteer/.
		// Walk up from the executing assembly until we find a sibling of that name.
		string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
		const string LAB_REPO_NAME = "puppeteer";
		DirectoryInfo? cursor = new DirectoryInfo(assemblyDir);
		while (cursor != null)
		{
			string candidate = Path.Combine(cursor.FullName, LAB_REPO_NAME);
			if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "Puppeteer.sln")))
			{
				return candidate;
			}
			cursor = cursor.Parent;
		}
		throw new InvalidOperationException(
			$"Could not locate sibling lab repo '{LAB_REPO_NAME}' from helper assembly location. " +
			$"Pass the lab repo root as the first CLI argument.");
	}

	private static string ResolveLabBranchSha(string repoRoot)
	{
		try
		{
			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = "git",
				Arguments = "rev-parse --short HEAD",
				WorkingDirectory = repoRoot,
				RedirectStandardOutput = true,
				UseShellExecute = false
			};
			using Process? p = Process.Start(psi);
			if (p == null) return "unknown";
			p.WaitForExit();
			return p.StandardOutput.ReadToEnd().Trim();
		}
		catch
		{
			return "unknown";
		}
	}

	private record SampleRow(int Tier, string TierLabel, int N, int Iteration, string Format, string Compression, int EncodedBytes, int PayloadBytes, int FramingOverheadBytes, string GitSha);
	private record DefinitionRow(int Tier, int ActionId, int ScriptBytes, int ParamsBytes, int TotalDefBytes, string ScriptPreviewAnonymized);

	private static void WriteSamples(string path, List<SampleRow> rows)
	{
		using StreamWriter w = new StreamWriter(path);
		w.WriteLine("tier,tier_label,n,iteration,format,compression,encoded_bytes,payload_bytes,framing_overhead_bytes,git_sha");
		foreach (var r in rows)
		{
			w.WriteLine(string.Create(CultureInfo.InvariantCulture,
				$"{r.Tier},{r.TierLabel},{r.N},{r.Iteration},{r.Format},{r.Compression},{r.EncodedBytes},{r.PayloadBytes},{r.FramingOverheadBytes},{r.GitSha}"));
		}
	}

	private static void WriteSummary(string path, List<SampleRow> samples, List<DefinitionRow> defs)
	{
		using StreamWriter w = new StreamWriter(path);
		w.WriteLine("tier,tier_label,n,format,compression,sum_bytes,mean_bytes_per_event,p50_bytes,p95_bytes,definition_bytes_once");
		var grouped = samples
			.GroupBy(r => (r.Tier, r.TierLabel, r.N, r.Format, r.Compression))
			.OrderBy(g => g.Key.N).ThenBy(g => g.Key.Tier).ThenBy(g => g.Key.Format).ThenBy(g => g.Key.Compression);

		foreach (var g in grouped)
		{
			List<int> sortedBytes = g.Select(x => x.EncodedBytes).OrderBy(x => x).ToList();
			int sum = sortedBytes.Sum();
			double mean = sortedBytes.Average();
			int p50 = sortedBytes[sortedBytes.Count / 2];
			int p95Index = (int)Math.Ceiling(0.95 * sortedBytes.Count) - 1;
			if (p95Index < 0) p95Index = 0;
			int p95 = sortedBytes[p95Index];
			int defBytes = defs.First(d => d.Tier == g.Key.Tier).TotalDefBytes;
			w.WriteLine(string.Create(CultureInfo.InvariantCulture,
				$"{g.Key.Tier},{g.Key.TierLabel},{g.Key.N},{g.Key.Format},{g.Key.Compression},{sum},{mean:F2},{p50},{p95},{defBytes}"));
		}
	}

	private static void WriteDefinitions(string path, List<DefinitionRow> rows)
	{
		using StreamWriter w = new StreamWriter(path);
		w.WriteLine("tier,action_id,script_bytes,params_bytes,total_def_bytes,script_preview_anonymized");
		foreach (var r in rows)
		{
			string preview = r.ScriptPreviewAnonymized.Replace("\"", "\"\"");
			w.WriteLine(string.Create(CultureInfo.InvariantCulture,
				$"{r.Tier},{r.ActionId},{r.ScriptBytes},{r.ParamsBytes},{r.TotalDefBytes},\"{preview}\""));
		}
	}
}
