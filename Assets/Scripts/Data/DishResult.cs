using System.Collections.Generic;

namespace Galatea.Data
{
    public enum PlateType
    {
        FlatPlate,
        Bowl,
        Tray
    }

    public class DishResult
    {
        public IReadOnlyList<IngredientInstance> Ingredients { get; }
        public FlavorProfile CombinedProfile { get; }
        public FlavorAnalysis Analysis { get; }
        public PlateType Vessel { get; }

        public DishResult(List<IngredientInstance> ingredients, FlavorProfile combinedProfile, FlavorAnalysis analysis, PlateType vessel)
        {
            Ingredients = ingredients;
            CombinedProfile = combinedProfile;
            Analysis = analysis;
            Vessel = vessel;
        }
    }
}
