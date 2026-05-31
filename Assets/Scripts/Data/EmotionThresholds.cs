namespace Galatea.Data
{
    // Inspector-tunable thresholds consumed by FlavorCalculator.EvaluateEmotion.
    public struct EmotionThresholds
    {
        public float disgustMaxBalance;
        public float disappointedMaxIntensity;
        public float cozyMinBalance;
        public float cozyMaxIntensity;
        public float refreshedMinBalance;
        public float spicyMinBalance;
        public float delightedMinBalance;
        public float delightedMinIntensity;
    }
}
