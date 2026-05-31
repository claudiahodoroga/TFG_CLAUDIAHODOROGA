using System.Collections.Generic;

namespace Galatea.Data
{
    public enum FlavorType
    {
        Sweet,
        Acidic,
        Bitter,
        Salty,
        Spicy,
        Neutralizer
    }

    public class FlavorAnalysis
    {
        public List<FlavorType> dominantFlavors;
        public float balanceScore;
        public float intensity;
        public bool isBurnt;

        public FlavorAnalysis(List<FlavorType> dominantFlavors, float balanceScore, float intensity, bool isBurnt)
        {
            this.dominantFlavors = dominantFlavors;
            this.balanceScore = balanceScore;
            this.intensity = intensity;
            this.isBurnt = isBurnt;
        }
    }
}
