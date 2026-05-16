using System.Reflection;

static void Probe(string label, string dllPath, string aggregateFqn)
{
    Console.WriteLine($"=== {label} ===");
    Console.WriteLine($"DLL: {dllPath}");

    Assembly asm;
    try { asm = Assembly.LoadFrom(dllPath); }
    catch (Exception ex) { Console.WriteLine($"  LOAD FAIL: {ex.GetType().Name}: {ex.Message}"); return; }

    Console.WriteLine($"  Loaded OK ({asm.GetName().Name} {asm.GetName().Version})");

    Type? agg;
    try { agg = asm.GetType(aggregateFqn, throwOnError: true); }
    catch (Exception ex) { Console.WriteLine($"  TYPE FAIL: {ex.GetType().Name}: {ex.Message}"); return; }

    Console.WriteLine($"  Aggregate type resolved: {agg!.FullName}");

    var publicMethods = agg.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName)
        .OrderBy(m => m.IsStatic ? 0 : 1).ThenBy(m => m.Name)
        .ToList();
    Console.WriteLine($"  Public declared methods ({publicMethods.Count}):");
    foreach (var m in publicMethods)
    {
        var sig = $"{(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})";
        Console.WriteLine($"    - {sig}");
    }

    var ctors = agg.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
    Console.WriteLine($"  Constructors ({ctors.Length}):");
    foreach (var c in ctors)
    {
        var vis = c.IsPublic ? "public" : c.IsFamily ? "protected" : c.IsAssembly ? "internal" : "private";
        var sig = $"{vis} .ctor({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})";
        Console.WriteLine($"    - {sig}");
    }
    Console.WriteLine();
}

Probe(
    "eShop / Ordering.Domain",
    @"C:\Users\alvar\source\repos\dotnet-eShop\artifacts\bin\Ordering.Domain\debug_net9.0\Ordering.Domain.dll",
    "eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order");

Probe(
    "Grzybek / Payments.Domain - SubscriptionPayment",
    @"C:\Users\alvar\source\repos\kgrzybek-modular-monolith\src\Modules\Payments\Domain\bin\Debug\net8.0\CompanyName.MyMeetings.Modules.Payments.Domain.dll",
    "CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments.SubscriptionPayment");
