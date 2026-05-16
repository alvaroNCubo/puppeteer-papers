// Lab 5 β — Roslyn forward call-graph closure over Grzybek Payments.Domain.
// Mirror of labs/lab05-eshop-roslyn/Program.cs; only the root path and entry
// points differ. Methodology identical: purely syntactic walker, no MSBuildWorkspace,
// no SemanticModel. Forward closure from the DSL entry points the measurement
// script invokes on the SubscriptionPayment aggregate, restricted to source
// files under kgrzybek-modular-monolith/src/Modules/Payments/Domain.

using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

const string PaymentsDomainRoot = @"C:\Users\alvar\source\repos\kgrzybek-modular-monolith\src\Modules\Payments\Domain";
const string OutputDir = @"C:\Users\alvar\source\repos\puppeteer-papers\data\lab05-grzybek";

// Entry points: methods/ctors the DSL measurement invokes on the
// SubscriptionPayment aggregate after the facade has assembled the value
// objects. NewWaitingPayment is the facade-side wrapper; from the domain
// perspective the seeds are SubscriptionPayment.Buy + MarkAsPaid plus the
// value-object factories the facade invokes (PayerId ctor, SubscriptionPeriod.Of,
// MoneyValue.Of, PriceListItemData ctor, PriceList.Create,
// DirectValueFromPriceListPricingStrategy ctor).
var entryPoints = new (string ClassHint, string Name, EntryKind Kind)[]
{
    ("SubscriptionPayment", "Buy",                                   EntryKind.Method),
    ("SubscriptionPayment", "MarkAsPaid",                            EntryKind.Method),
    ("PayerId",             "PayerId",                               EntryKind.Constructor),
    ("SubscriptionPeriod",  "Of",                                    EntryKind.Method),
    ("MoneyValue",          "Of",                                    EntryKind.Method),
    ("PriceListItemData",   "PriceListItemData",                     EntryKind.Constructor),
    ("PriceList",           "Create",                                EntryKind.Method),
    ("DirectValueFromPriceListPricingStrategy",
                            "DirectValueFromPriceListPricingStrategy", EntryKind.Constructor),
};

Console.WriteLine($"[Lab05β-Grzybek] Loading Payments.Domain source from {PaymentsDomainRoot}");
var sw = Stopwatch.StartNew();
var files = Directory.GetFiles(PaymentsDomainRoot, "*.cs", SearchOption.AllDirectories);
Console.WriteLine($"[Lab05β-Grzybek] Found {files.Length} .cs files");

var trees = new List<(string File, SyntaxTree Tree)>();
foreach (var f in files)
{
    try { trees.Add((f, CSharpSyntaxTree.ParseText(File.ReadAllText(f)))); }
    catch (Exception ex) { Console.WriteLine($"[Lab05β-Grzybek] WARN parse {f}: {ex.Message}"); }
}
Console.WriteLine($"[Lab05β-Grzybek] Parsed {trees.Count} trees in {sw.ElapsedMilliseconds}ms");

var index = new Dictionary<string, List<DeclEntry>>(StringComparer.Ordinal);
int totalDecls = 0;
foreach (var (file, tree) in trees)
{
    var root = tree.GetRoot();
    foreach (var member in root.DescendantNodes())
    {
        switch (member)
        {
            case MethodDeclarationSyntax m:
                AddToIndex(index, m.Identifier.Text, MakeEntry(file, m, m.Identifier.Text, DeclKind.Method));
                totalDecls++; break;
            case ConstructorDeclarationSyntax c:
                AddToIndex(index, c.Identifier.Text, MakeEntry(file, c, c.Identifier.Text, DeclKind.Constructor));
                totalDecls++; break;
            case PropertyDeclarationSyntax p:
                AddToIndex(index, p.Identifier.Text, MakeEntry(file, p, p.Identifier.Text, DeclKind.Property));
                totalDecls++; break;
        }
    }
}
Console.WriteLine($"[Lab05β-Grzybek] Indexed {totalDecls} declarations across {index.Count} unique names");

var visited = new HashSet<string>(StringComparer.Ordinal);
var nodes = new List<NodeRecord>();
var queue = new Queue<(DeclEntry Decl, int Depth, string Source)>();

foreach (var ep in entryPoints)
{
    var matches = ResolveEntry(index, ep.ClassHint, ep.Name, ep.Kind);
    if (matches.Count == 0) { Console.WriteLine($"[Lab05β-Grzybek] WARN entry not found: {ep.ClassHint}.{ep.Name}"); continue; }
    foreach (var m in matches)
    {
        var key = NodeKey(m);
        if (visited.Add(key)) { queue.Enqueue((m, 0, "<entry>")); nodes.Add(new NodeRecord(m, 0, "<entry>")); }
    }
}
Console.WriteLine($"[Lab05β-Grzybek] Seed: {nodes.Count} entry methods resolved");

int countTrivialSkipped = 0;
while (queue.Count > 0)
{
    var (decl, depth, source) = queue.Dequeue();
    if (depth > 50) continue;
    foreach (var node in decl.DeclarationNode.DescendantNodes())
    {
        string? targetName = null;
        DeclKind kind;
        switch (node)
        {
            case InvocationExpressionSyntax inv:
                targetName = ExtractInvocationName(inv); kind = DeclKind.Method; break;
            case ObjectCreationExpressionSyntax obj:
                targetName = ExtractTypeName(obj.Type); kind = DeclKind.Constructor; break;
            case MemberAccessExpressionSyntax mae when IsLikelyPropertyAccess(mae):
                targetName = mae.Name.Identifier.Text; kind = DeclKind.Property; break;
            default: continue;
        }
        if (string.IsNullOrEmpty(targetName)) continue;
        if (!index.TryGetValue(targetName, out var candidates)) continue;
        foreach (var cand in candidates)
        {
            if (cand.Kind != kind && !KindCompatible(cand.Kind, kind)) continue;
            var key = NodeKey(cand);
            if (!visited.Add(key)) continue;
            if (IsTrivial(cand)) { countTrivialSkipped++; continue; }
            nodes.Add(new NodeRecord(cand, depth + 1, $"{decl.Class}.{decl.Name}"));
            queue.Enqueue((cand, depth + 1, decl.Name));
        }
    }
}
Console.WriteLine($"[Lab05β-Grzybek] Walk complete in {sw.ElapsedMilliseconds}ms — visited {nodes.Count}, skipped {countTrivialSkipped} trivial");

Directory.CreateDirectory(OutputDir);
var ts = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
string sha = TryGetSha();
var path = Path.Combine(OutputDir, $"beta-roslyn-{ts}-{sha}.csv");
using (var w = new StreamWriter(path))
{
    w.WriteLine("depth,kind,namespace,class,method,file,line,source_caller");
    foreach (var node in nodes)
    {
        var rel = Path.GetRelativePath(PaymentsDomainRoot, node.Decl.File).Replace('\\', '/');
        w.WriteLine($"{node.Depth},{node.Decl.Kind},{node.Decl.Namespace},{node.Decl.Class},{node.Decl.Name},{rel},{node.Decl.Line},{node.Source}");
    }
}
Console.WriteLine($"[Lab05β-Grzybek] dataset: {path}");
Console.WriteLine();
Console.WriteLine($"========== Lab 05 β Grzybek ==========");
Console.WriteLine($"Entry points (DSL/facade seeds):      {entryPoints.Length}");
Console.WriteLine($"Methods reached via forward closure:  {nodes.Count}");
Console.WriteLine($"Trivial accessors skipped:            {countTrivialSkipped}");
Console.WriteLine();
Console.WriteLine("Breakdown by depth:");
foreach (var grp in nodes.GroupBy(n => n.Depth).OrderBy(g => g.Key))
    Console.WriteLine($"  depth {grp.Key,-3}: {grp.Count(),5}");
Console.WriteLine("Breakdown by kind:");
foreach (var grp in nodes.GroupBy(n => n.Decl.Kind).OrderBy(g => g.Key.ToString()))
    Console.WriteLine($"  {grp.Key,-15}: {grp.Count(),5}");

return 0;

// ===== Helpers (copied verbatim from labs/lab05-eshop-roslyn/Program.cs) =====
static DeclEntry MakeEntry(string file, MemberDeclarationSyntax syntax, string name, DeclKind kind)
{
    string ns = ExtractNamespace(syntax);
    string cls = ExtractContainingTypeName(syntax) ?? "<global>";
    int line = syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    return new DeclEntry(file, ns, cls, name, kind, syntax, line);
}
static void AddToIndex(Dictionary<string, List<DeclEntry>> index, string name, DeclEntry entry)
{
    if (!index.TryGetValue(name, out var list)) { list = new(); index[name] = list; }
    list.Add(entry);
}
static string ExtractNamespace(SyntaxNode node)
{
    var current = node.Parent;
    while (current != null)
    {
        if (current is BaseNamespaceDeclarationSyntax ns) return ns.Name.ToString();
        current = current.Parent;
    }
    return "<no-namespace>";
}
static string? ExtractContainingTypeName(SyntaxNode node)
{
    var current = node.Parent;
    while (current != null)
    {
        if (current is TypeDeclarationSyntax t) return t.Identifier.Text;
        current = current.Parent;
    }
    return null;
}
static List<DeclEntry> ResolveEntry(Dictionary<string, List<DeclEntry>> index, string classHint, string name, EntryKind kind)
{
    if (!index.TryGetValue(name, out var candidates)) return new List<DeclEntry>();
    var result = candidates.Where(c => c.Class == classHint).ToList();
    if (result.Count == 0) result = candidates;
    return result;
}
static string NodeKey(DeclEntry e) => $"{e.Namespace}.{e.Class}.{e.Name}#{e.Kind}@{e.Line}";
static string? ExtractInvocationName(InvocationExpressionSyntax inv) => inv.Expression switch
{
    MemberAccessExpressionSyntax mae => mae.Name.Identifier.Text,
    IdentifierNameSyntax id => id.Identifier.Text,
    _ => null,
};
static string? ExtractTypeName(TypeSyntax t) => t switch
{
    IdentifierNameSyntax id => id.Identifier.Text,
    QualifiedNameSyntax qn => qn.Right.Identifier.Text,
    GenericNameSyntax gn => gn.Identifier.Text,
    _ => null,
};
static bool IsLikelyPropertyAccess(MemberAccessExpressionSyntax mae)
{
    if (mae.Parent is InvocationExpressionSyntax invParent && invParent.Expression == mae) return false;
    if (mae.Parent is MemberAccessExpressionSyntax outerMae && outerMae.Expression == mae) return false;
    return true;
}
static bool KindCompatible(DeclKind candidate, DeclKind requested) => (candidate, requested) switch
{
    (DeclKind.Method, DeclKind.Property) => true,
    (DeclKind.Property, DeclKind.Method) => true,
    _ => false,
};
static bool IsTrivial(DeclEntry e)
{
    var node = e.DeclarationNode;
    switch (node)
    {
        case PropertyDeclarationSyntax p:
            if (p.AccessorList != null && p.AccessorList.Accessors.All(a => a.Body == null && a.ExpressionBody == null)) return true;
            if (p.ExpressionBody != null && p.ExpressionBody.Expression is IdentifierNameSyntax) return true;
            if (p.AccessorList != null && p.AccessorList.Accessors.Count == 1)
            {
                var acc = p.AccessorList.Accessors[0];
                if (acc.ExpressionBody?.Expression is IdentifierNameSyntax) return true;
                if (acc.Body != null && acc.Body.Statements.Count == 1
                    && acc.Body.Statements[0] is ReturnStatementSyntax ret
                    && ret.Expression is IdentifierNameSyntax) return true;
            }
            return false;
        case MethodDeclarationSyntax m:
            if (m.Body != null && m.Body.Statements.Count == 0) return true;
            if (m.Body != null && m.Body.Statements.Count == 1 && m.Body.Statements[0] is ThrowStatementSyntax) return true;
            if (m.ExpressionBody?.Expression is IdentifierNameSyntax) return true;
            if (m.Body != null && m.Body.Statements.Count == 1
                && m.Body.Statements[0] is ReturnStatementSyntax ret2
                && ret2.Expression is IdentifierNameSyntax) return true;
            return false;
        case ConstructorDeclarationSyntax: return false;
        default: return false;
    }
}
static string TryGetSha()
{
    try
    {
        var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
        {
            WorkingDirectory = @"C:\Users\alvar\source\repos\Puppeteer Pacifico",
            RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var sha = p.StandardOutput.ReadToEnd().Trim();
        p.WaitForExit(2000);
        return string.IsNullOrEmpty(sha) ? "nosha" : sha;
    }
    catch { return "nosha"; }
}
enum DeclKind { Method, Constructor, Property }
enum EntryKind { Method, Constructor, Property }
record DeclEntry(string File, string Namespace, string Class, string Name, DeclKind Kind, MemberDeclarationSyntax DeclarationNode, int Line);
record NodeRecord(DeclEntry Decl, int Depth, string Source);
