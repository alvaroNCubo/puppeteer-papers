namespace BenchPaper2Bdn
{
    // Adversarial "flat CRUD" host: a verb that is a single field setter — the case
    // a reviewer asks for, where the surface-vs-depth asymmetry should vanish.
    // α = 1 (one DSL dispatch); β = 1 (SetValue calls nothing) → verb-richness ≈ 1×.
    // The compiled path has almost no AST to amortize, so the speedup should approach 1×.
    public class CrudFacade
    {
        public int Value;
        public CrudFacade SetValue(int v) { Value = v; return this; }
    }
}
