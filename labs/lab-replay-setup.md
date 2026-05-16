# Lab-replay — Fase 0 setup (✅ CERRADA 2026-05-16)

**Cierre confirmado**: branch `lab-replay/00-harness-setup` @ commit `3d97b14` en Pacifico. UnitTestPuppeteer suite verde 768/768 (excl Bench/Integration/FlakyInCI). Smoke `PhaseZeroSmokeTest.Smoke_DslInvokesEShopOrderingFacade` 1/1. **NO push.**

**Lo que requirió ajuste vs el plan original:**
- Mods 2, 3, 5 del lab plan original NO eran necesarias para smoke base; se difieren a labs específicos. Solo se aplicó mod 6 (DomainLibraries try/catch). Mods 1, 4, 7 ya absorbidas upstream en master.
- DSL syntax descubierto durante smoke: construcción es `f = OrderingFacade()` (sin `new`, sin prefijo `p`). El `p` en `pCompany` que aparecía en el codebase interno era LITERAL parte del nombre de clase. `for` usa `for (i : list)`, no range syntax.

Setup base para el replay de los labs domain-dependent de Paper 2 sobre dos codebases públicos. Reemplaza la evidencia interna de una prior e-commerce system (production domain assembly, código comercial no publicable).

Memoria firmada: `project_puppeteer_paper02_dual_codebase_replay.md`.

## Codebases extraídos

| Codebase | Rol | License | Path local |
|---|---|---|---|
| [dotnet/eShop](https://github.com/dotnet/eShop) | Primary (5 labs) | MIT | `C:\Users\alvar\source\repos\dotnet-eShop\` |
| [kgrzybek/modular-monolith-with-ddd](https://github.com/kgrzybek/modular-monolith-with-ddd) | Replica selectiva (Lab 4 + Lab 5) | MIT | `C:\Users\alvar\source\repos\kgrzybek-modular-monolith\` |

Ambos clonados con `--depth 1` desde `main` / `master` el 2026-05-16.

## Modificaciones aplicadas (mínimas, documentables en §5 metodología)

### eShop — multi-target net9.0 + net10.0

`src/Ordering.Domain/Ordering.Domain.csproj`:

```diff
-    <TargetFramework>net10.0</TargetFramework>
+    <TargetFrameworks>net9.0;net10.0</TargetFrameworks>
```

Razón: el `main` branch ahora targetea net10.0 (a pesar de que el README aún dice ".NET 9"); Pacifico es net9.0. El cambio es **aditivo** — preserva el target original net10 y añade net9 sin tocar el source code del aggregate.

### Grzybek — roll-forward del SDK requerido

`src/global.json`:

```diff
-    "rollForward": "latestFeature",
+    "rollForward": "latestMajor",
```

Razón: el `global.json` pinned SDK 8.0.0 con `rollForward: latestFeature` (sólo roll dentro de 8.0.x). El host tiene SDK 9.0.312 + 10.0.103. `latestMajor` permite usar SDK 9 para compilar el TFM net8.0 del proyecto. El source code y el TFM del proyecto no cambian.

## Builds verificadas

```
eShop Ordering.Domain (debug, net9.0):
  C:\Users\alvar\source\repos\dotnet-eShop\artifacts\bin\Ordering.Domain\debug_net9.0\Ordering.Domain.dll
  0 Warning(s), 0 Error(s), 7.30s

Grzybek Payments.Domain (debug, net8.0):
  C:\Users\alvar\source\repos\kgrzybek-modular-monolith\src\Modules\Payments\Domain\bin\Debug\net8.0\CompanyName.MyMeetings.Modules.Payments.Domain.dll
  0 Warning(s), 0 Error(s), 5.21s
```

Pacifico (net9.0) puede consumir ambos: el net9.0 build de eShop directamente; el net8.0 build de Grzybek vía forward-compat (el runtime net9 carga assemblies net8 sin restricción).

## Loadability + type discovery (probe verificada)

Probe console en `labs/lab-replay-probe/`. `dotnet run` 2026-05-16:

### eShop / `eShop.Ordering.Domain.AggregatesModel.OrderAggregate.Order`

- 10 métodos públicos declarados:
  - Factory: `static Order NewDraft()`
  - Verbos: `AddOrderItem(int, string, decimal, decimal, string, int)`, `SetAwaitingValidationStatus()`, `SetStockConfirmedStatus()`, `SetPaidStatus()`, `SetShippedStatus()`, `SetCancelledStatus()`, `SetCancelledStatusWhenStockIsRejected(IEnumerable<int>)`, `SetPaymentMethodVerified(int, int)`
  - Query: `GetTotal()` → decimal
- 2 constructores:
  - `protected .ctor()` — uso EF/factory
  - `public .ctor(string userId, string userName, Address address, int cardTypeId, string cardNumber, string cardSecurityNumber, string cardHolderName, DateTime cardExpiration, int? buyerId, int? paymentMethodId)`

### Grzybek / `CompanyName.MyMeetings.Modules.Payments.Domain.SubscriptionPayments.SubscriptionPayment`

- 4 métodos públicos declarados:
  - Factory: `static SubscriptionPayment Buy(PayerId, SubscriptionPeriod, string countryCode, MoneyValue priceOffer, PriceList priceList)`
  - Verbos: `MarkAsPaid()`, `Expire()`
  - Snapshot: `GetSnapshot()` → SubscriptionPaymentSnapshot
- 1 constructor: `public .ctor()` parameterless

## Pendiente para próximo chat (Fase 0 step 4-6 + Fase 1 arranque)

1. **Decidir harness pattern.** Ambos aggregates exigen value objects en sus factories (`Address` para Order; `PayerId`/`SubscriptionPeriod`/`MoneyValue`/`PriceList` para SubscriptionPayment). Para invocarlos desde DSL, dos caminos:
   - (a) **Harness wrapper** en Pacifico que tome primitivos y arme los VOs internamente. Equivalente al patrón usado para la production purchase verb en el codebase interno previo. **Probable elección.**
   - (b) Exponer los VOs como tipos DSL-construibles (más invasivo, más superficie).
2. **Branchear `lab-replay/00-harness-setup`** en `C:\Users\alvar\source\repos\Puppeteer Pacifico\`. Heredar mods 1–15 del lab plan original (sirven para todos los labs).
3. **Crear `OrderingDomainHarness.cs` + `SubscriptionPaymentDomainHarness.cs`** en Pacifico, equivalentes a los harness existentes para el production domain assembly del codebase interno previo.
4. **Test DSL smoke**: `for i = 1 to 10 { o = NewOrder(...); o.AddOrderItem(...); o.SetPaidStatus() }` corre sin error contra `Ordering.Domain.dll`. Mismo smoke para SubscriptionPayment.
5. **Commit** del harness + estas notas, en branch `lab-replay/00-harness-setup` (no push — workflow firmado).

## Status Fase 0 paso 4-6 (audit 2026-05-16, branch creado)

**Branch:** `lab-replay/00-harness-setup` en `C:\Users\alvar\source\repos\Puppeteer Pacifico\` desde master `b66b247` (sin commits aún).

### Mods al core de Pacifico — sólo 3 chicas + 1 a verificar

Master upstream ya absorbió mods 1, 4, 7 (overload `params Assembly[]` en `Actor.cs:26-32`, propagación a `ActorHandler` constructor, shim Objeto innecesario para nuestras DLLs).

**Mod 2 — `Puppeteer/Actor.cs:8`** (1 palabra):
```diff
-	internal enum CompilationModePolicy
+	public enum CompilationModePolicy
```
Razón: tests externos necesitan referenciar `CompilationModePolicy.AlwaysCompiled` / `AlwaysInterpreted`.

**Mod 5 — `Puppeteer/Parameters.cs:147`** (1 palabra):
```diff
-		internal object this[string parameterName, Type parameterType]
+		public object this[string parameterName, Type parameterType]
```
Razón: harness instala parámetros del actor desde código externo.

**Mod 3 — `Puppeteer/ActorV2.cs`**: verificar si existe `UsingCompilationMode(CompilationModePolicy)` fluent setter. Si no, agregar:
```csharp
public ActorV2 UsingCompilationMode(CompilationModePolicy mode)
{
    this.CompiledModePolicy = mode;
    return this;
}
```
También promover field `CompiledModePolicy` (Actor.cs:19) de `internal` a `public` (o agregar setter público).

**Mod 6 — `Puppeteer/EventSourcing/DomainLibraries.cs:117` `LoadTypesFromAssembly`**: leer estado actual. Si no captura `ReflectionTypeLoadException`, envolver con try/catch que retorna `ex.Types.Where(t => t != null)`. Razón: handle transitive deps faltantes (MediatR para eShop, etc.).

### Proyecto nuevo `UnitTestEShopOnPuppeteer/`

Mimetizar el proyecto de test de la prior production system (referencia en lab plan mod #8 — proyecto ya existe en la lab/01 branch interna para inspirarse).

`UnitTestEShopOnPuppeteer/UnitTestEShopOnPuppeteer.csproj`:
- TFM: `net9.0`
- ProjectReference a `Puppeteer/Puppeteer.csproj`
- File reference a `C:\Users\alvar\source\repos\dotnet-eShop\artifacts\bin\Ordering.Domain\debug_net9.0\Ordering.Domain.dll`
- File reference (transitive) a MediatR si Pacifico no la trae
- `<IsPackable>false</IsPackable>`, MSTest framework

### Harness pattern firmado — *Facade* (replica del patrón de production purchase verb del codebase interno previo)

Una clase C# en el proyecto de test, **NO** en Puppeteer core. La clase orquesta la secuencia de verbos del domain real. Equivalente exacto a lo que hace la production purchase verb con el production domain assembly interno previo.

`UnitTestEShopOnPuppeteer/OrderingFacade.cs`:
```csharp
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;

namespace UnitTestEShopOnPuppeteer;

public class OrderingFacade
{
    public Order CreateAndPay(int productId, string productName, decimal unitPrice, int units)
    {
        var order = Order.NewDraft();
        order.AddOrderItem(productId, productName, unitPrice, 0m, "", units);
        order.SetAwaitingValidationStatus();
        order.SetStockConfirmedStatus();
        order.SetPaidStatus();
        return order;
    }
}
```

Justificación:
- Una sola DSL invocation (`facade.CreateAndPay(...)`) dispatcha múltiples verbos del domain real — exactly the shape that Lab 5 (α DSL dispatch count vs β Roslyn walker) mide.
- `NewDraft()` evita el constructor de 10 args (skip the `Address` value object construction problem).
- State machine respetada: Draft → AwaitingValidation → StockConfirmed → Paid (idéntico flujo a producción real).
- No toca el código upstream del Order aggregate.

### Smoke test DSL

`UnitTestEShopOnPuppeteer/PhaseZeroSmokeTest.cs`:
```csharp
[TestClass]
public class PhaseZeroSmokeTest
{
    [TestMethod, TestCategory("Bench")]
    public void Smoke_Facade_CreateAndPay_Runs_10x()
    {
        var assembly = typeof(OrderingFacade).Assembly;  // includes Ordering.Domain transitively
        var actor = new MyTestActor("smoke-actor", assembly);
        var script = @"
            f = new OrderingFacade();
            for i = 1 to 10 {
                o = f.CreateAndPay(i, 'item', 10.0, 1);
                print o 'order';
            }
        ";
        actor.PerformCmd(script);
        Assert.IsTrue(true);  // smoke = no throw
    }

    private class MyTestActor : ActorV2
    {
        public MyTestActor(string name, Assembly libraryAssembly) : base(name, libraryAssembly) { }
    }
}
```

Verde = el actor de Pacifico cargó `Ordering.Domain.dll` vía reflection, encontró `OrderingFacade`, parseó el DSL, dispatchó 10 invocaciones a `CreateAndPay`, cada una cascadeó a `Order.NewDraft + AddOrderItem + SetAwaitingValidationStatus + SetStockConfirmedStatus + SetPaidStatus`.

### Para Grzybek (Fase 2 — chats Lab 4 y 5)

Análogo:
```csharp
// SubscriptionPaymentFacade.cs
public SubscriptionPayment BuyAndPay(Guid payerId, int monthCount, string countryCode, decimal price)
{
    var period = new SubscriptionPeriod(monthCount);
    var payer = new PayerId(payerId);
    var money = MoneyValue.Of(price, countryCode);
    var list = PriceList.Empty();  // o factory adecuado
    var p = SubscriptionPayment.Buy(payer, period, countryCode, money, list);
    p.MarkAsPaid();
    return p;
}
```
Las firmas exactas de los value objects (`SubscriptionPeriod`, `PayerId`, `MoneyValue`, `PriceList`) deben verificarse en `src/Modules/Payments/Domain/` + `src/BuildingBlocks/Domain/`. Diferido a Fase 2.

### Estimación remaining work

- Mods 2 + 5 + 3 + 6: ~30 min (verificación + 4 edits chicos).
- Proyecto + Facade + Test: ~30 min.
- Build + smoke + debug: ~30-60 min (riesgo principal: mod 6 tolerancia transitiva).
- Commit: 5 min.

**Total estimado: 1.5–2 horas de chat dedicado.**

### Cómo arrancar el chat siguiente

> *"Cerremos Fase 0 paso 4-6 del dual-codebase replay. Branch `lab-replay/00-harness-setup` ya creado desde master `b66b247` en Pacifico. Leé `puppeteer-papers/labs/lab-replay-setup.md` sección 'Status Fase 0 paso 4-6'. Aplicá las 4 mods chicas, creá `UnitTestEShopOnPuppeteer/` con OrderingFacade + smoke test, corré, commiteá al branch (no push). Si suite full sigue verde 771+ tras las mods, smoke verde = Fase 0 CERRADA y desbloquea Fase 1 Lab 1 eShop."*
