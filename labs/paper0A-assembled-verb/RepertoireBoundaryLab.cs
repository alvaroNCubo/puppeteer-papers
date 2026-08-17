using Puppeteer;
using System.Diagnostics;
using System.Reflection;

namespace UnitTestAssembledVerbOnPuppeteer
{
    // Who decides what counts as a piece of a repertoire, and against whom.
    //
    // A domain author marks a type internal and puts it in its own assembly. Two parties then
    // stand in different relations to it, and neither relation is a convention: both are
    // enforced, by different machinery, and this lab makes both of them say so out loud.
    //
    //   the host language   cannot name it       the C# compiler refuses, CS0122
    //   the assembler       reaches it           the actor resolves it by reflection
    //
    // The negative half cannot live inside a passing test, because code that does not compile
    // cannot be compiled. So the test writes the offending program to a temporary directory,
    // invokes the compiler on it, and asserts the diagnostic - the same shape as the published
    // artifact that fails to build on purpose, executed here rather than described.
    //
    // ══ WHY THIS IS NOT A DETAIL ABOUT VISIBILITY ═════════════════════════════════════════════
    //
    // Elsewhere this study shows that a whole exists which no piece can name, and that a position
    // exists from which the whole IS knowable - the party assembling it. That leaves the question
    // a reader is entitled to press: by what right does that party constitute the whole as an act?
    // Answering "because we say so" would make the entitlement a philosophical assertion. It is
    // not one. It is decidable, and the decision is taken by machinery neither party controls:
    //
    //     a piece decides what may be reached inside it        - the author marks it internal
    //     the host language cannot overrule that decision      - the compiler refuses to name it
    //     the assembler composes the surfaces made composable  - resolved at the actor
    //     and the boundary is enforced, not observed           - by the compiler, both ways
    //
    // Which gives the assembler's authority a shape worth stating exactly, because the obvious
    // misreading is that it is a superuser:
    //
    //     The assembler can say more than either repertoire, because it sees both.
    //     It cannot say more about either repertoire than that repertoire permits.
    //
    // Its authority has WIDTH, NOT DEPTH. It reaches across several pieces and through none of
    // them. That is the mechanical form of standing, and it is why constituting the whole as an
    // act is a right the arrangement grants rather than a privilege the assembler claims.
    [TestClass]
    public class RepertoireBoundaryLab
    {
        [TestMethod, TestCategory("Lab")]
        public void TheHostMayNotNameAnInternalPiece_AndTheAssemblerReachesIt()
        {
            var fixtureAssembly = LocateFixture();

            Console.WriteLine();
            Console.WriteLine("=== Two parties, two entitlements, two mechanisms ===");
            Console.WriteLine($"  repertoire assembly: {Path.GetFileName(fixtureAssembly)}");

            var piece = Assembly.LoadFrom(fixtureAssembly).GetType("RepertoireBoundaryFixture.Piece");
            Assert.IsNotNull(piece, "The fixture must declare the piece.");
            Assert.IsFalse(piece.IsPublic, "The piece is internal; that is the whole fixture.");

            // The assembler's half: the actor is given the assembly and runs the piece's verbs.
            var actor = new ActorV2("repertoire_boundary", Assembly.LoadFrom(fixtureAssembly));
            string reached = actor.Using(@"
                piece = Piece();
                piece.Mark();
                piece.Mark();
                print piece.Marks() marks;
            ")
            .PerformQuery();

            Console.WriteLine($"  the assembler   reaches it        {reached}");

            Assert.IsTrue(reached.Contains("2"),
                "The actor must construct the internal piece and invoke its internal verbs.");

            // The host's half: ask the compiler.
            var (exitCode, diagnostics) = CompileAHostThatNamesThePiece(fixtureAssembly);

            Console.WriteLine($"  the host        cannot name it    exit {exitCode}");
            foreach (var d in diagnostics)
                Console.WriteLine($"                                    {d}");

            Assert.AreNotEqual(0, exitCode, "A host assembly naming an internal type must not compile.");
            Assert.IsTrue(diagnostics.Any(d => d.Contains("CS0122")),
                "The refusal must be the compiler's inaccessibility diagnostic, not some other failure.");

            Console.WriteLine();
            Console.WriteLine("  Neither half is a convention. The fence is erected against the host language");
            Console.WriteLine("  and the assembler is the one party entitled to pass it - which is why the");
            Console.WriteLine("  domain never has to be widened to public to be composed.");
        }

        private static string LocateFixture()
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, "RepertoireBoundaryFixture.dll");
            Assert.IsTrue(File.Exists(candidate), $"The fixture assembly must be built: {candidate}");
            return candidate;
        }

        private static (int ExitCode, List<string> Diagnostics) CompileAHostThatNamesThePiece(string fixtureAssembly)
        {
            string dir = Path.Combine(Path.GetTempPath(), $"repertoire_boundary_host_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "Host.csproj"), $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <EnableDefaultCompileItems>true</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""RepertoireBoundaryFixture"">
      <HintPath>{fixtureAssembly}</HintPath>
    </Reference>
  </ItemGroup>
</Project>");

                File.WriteAllText(Path.Combine(dir, "Host.cs"), @"
public static class Host
{
    public static void Reach()
    {
        var piece = new RepertoireBoundaryFixture.Piece();
        piece.Mark();
    }
}");

                var psi = new ProcessStartInfo("dotnet", $"build \"{Path.Combine(dir, "Host.csproj")}\" -v q --nologo")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit(180_000);

                var diagnostics = output.Split('\n')
                    .Where(l => l.Contains("error CS"))
                    .Select(l => l.Trim())
                    .Select(l => l.Length > 150 ? l.Substring(0, 150) : l)
                    .Distinct()
                    .ToList();

                return (process.ExitCode, diagnostics);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }
}
