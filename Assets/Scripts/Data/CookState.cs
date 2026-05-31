namespace Galatea.Data
{
    // Heat progression of an ingredient. Independent of Form; changed by
    // continuous cooking only.
    public enum CookState
    {
        Raw,
        Sauteed,
        Caramelized,
        Burnt,
        Boiled,
        Mush,
        Fried,
        Baked,
    }
}
