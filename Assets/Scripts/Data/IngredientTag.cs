using System;

namespace Galatea.Data
{
    [Flags]
    public enum IngredientTag
    {
        None     = 0,
        Sweet    = 1 << 0,
        Fibrous  = 1 << 1,
        Volatile = 1 << 2,
        Aqueous  = 1 << 3,
        Acidic   = 1 << 4,
        Starchy  = 1 << 5
    }
}
