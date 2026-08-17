namespace RepertoireBoundaryFixture
{
    // A piece of a repertoire, declared internal on purpose.
    //
    // Its author's decision is that the host language may not reach it: no other assembly can
    // name this type or call this method, and the compiler enforces that rather than a
    // convention asking politely. What the fixture exists to show is that the same decision
    // does not shut out the assembler, which reaches the repertoire by reflection.
    internal class Piece
    {
        private int marks;

        internal void Mark()
        {
            marks = marks + 1;
        }

        internal int Marks()
        {
            return marks;
        }
    }
}
