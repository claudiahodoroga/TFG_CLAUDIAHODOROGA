using System.Collections.Generic;

namespace Galatea.Data
{
    public static class FlavorCalculator
    {
        // Pre: ingredients is non-null (may be empty).
        // Post: returns the per-axis sum of all currentProfiles, clamped to [0,10].
        public static FlavorProfile Combine(List<IngredientInstance> ingredients)
        {
            var result = FlavorProfile.Zero;
            foreach (var ing in ingredients)
            {
                result.sweet       += ing.currentProfile.sweet;
                result.acidic      += ing.currentProfile.acidic;
                result.bitter      += ing.currentProfile.bitter;
                result.salty       += ing.currentProfile.salty;
                result.spicy       += ing.currentProfile.spicy;
                result.neutralizer += ing.currentProfile.neutralizer;
            }
            return result.Clamp();
        }

        public static FlavorAnalysis Analyze(FlavorProfile profile) => Analyze(profile, false);

        // Pre: profile values are in [0,10].
        // Post: returns dominant flavors (top 1–2 within codominantTolerance),
        // total intensity, and balanceScore = 1 / (1 + variance) over the 6 axes.
        public static FlavorAnalysis Analyze(FlavorProfile profile, bool isBurnt)
        {
            var pairs = new List<(FlavorType type, float value)>
            {
                (FlavorType.Sweet,  profile.sweet),
                (FlavorType.Acidic, profile.acidic),
                (FlavorType.Bitter, profile.bitter),
                (FlavorType.Salty,  profile.salty),
                (FlavorType.Spicy,  profile.spicy),
            };

            pairs.Sort((a, b) => b.value.CompareTo(a.value));

            var dominant = new List<FlavorType>();
            const float codominantTolerance = 1f;
            if (pairs[0].value > 0f)
            {
                int tiedCount = 1;
                for (int i = 1; i < pairs.Count; i++)
                    if (pairs[0].value - pairs[i].value <= codominantTolerance) tiedCount++;

                if (tiedCount <= 2)
                {
                    dominant.Add(pairs[0].type);
                    if (tiedCount == 2 && pairs[1].value > 0f)
                        dominant.Add(pairs[1].type);
                }
            }

            float rawIntensity = profile.sweet + profile.acidic + profile.bitter
                               + profile.salty + profile.spicy;
            float intensity = System.Math.Max(0f, rawIntensity - profile.neutralizer);

            float mean = rawIntensity / 5f;
            float variance =
                Sq(profile.sweet  - mean) +
                Sq(profile.acidic - mean) +
                Sq(profile.bitter - mean) +
                Sq(profile.salty  - mean) +
                Sq(profile.spicy  - mean);
            variance /= 5f;
            float balanceScore = 1f / (1f + variance);

            return new FlavorAnalysis(dominant, balanceScore, intensity, isBurnt);
        }

        // Pre: profile, multiplier, modifier are valid FlavorProfiles.
        // Post: returns profile * multiplier + modifier, clamped to [0,10].
        public static FlavorProfile ApplyDiscreteModifier(FlavorProfile profile, FlavorProfile multiplier, FlavorProfile modifier)
        {
            return new FlavorProfile(
                profile.sweet       * multiplier.sweet       + modifier.sweet,
                profile.acidic      * multiplier.acidic      + modifier.acidic,
                profile.bitter      * multiplier.bitter      + modifier.bitter,
                profile.salty       * multiplier.salty       + modifier.salty,
                profile.spicy       * multiplier.spicy       + modifier.spicy,
                profile.neutralizer * multiplier.neutralizer + modifier.neutralizer
            ).Clamp();
        }

        // Pre: delta is the per-tick rate; deltaTime scales the application.
        // Post: returns profile + delta * deltaTime, clamped to [0,10].
        public static FlavorProfile ApplyContinuousDelta(FlavorProfile profile, FlavorProfile delta, float deltaTime)
        {
            return new FlavorProfile(
                profile.sweet       + delta.sweet       * deltaTime,
                profile.acidic      + delta.acidic      * deltaTime,
                profile.bitter      + delta.bitter      * deltaTime,
                profile.salty       + delta.salty       * deltaTime,
                profile.spicy       + delta.spicy       * deltaTime,
                profile.neutralizer + delta.neutralizer * deltaTime
            ).Clamp();
        }

        // Priority-ordered emotion rules. Walks Disgusted → Disappointed → Delighted →
        // Cozy → Refreshed → Spicy → Confused (always-fallback) and returns the first
        // matching asset; missing assets fall through to the next rule.
        public static EmotionalResponse EvaluateEmotion(FlavorAnalysis analysis, List<EmotionalResponse> responses, EmotionThresholds thresholds)
        {
            var triggered = new List<EmotionType>();

            bool extremelyImbalanced = analysis.balanceScore < thresholds.disgustMaxBalance;
            if (analysis.isBurnt || extremelyImbalanced) triggered.Add(EmotionType.Disgusted);

            if (analysis.intensity < thresholds.disappointedMaxIntensity) triggered.Add(EmotionType.Disappointed);

            bool hasDominant = analysis.dominantFlavors != null && analysis.dominantFlavors.Count > 0;
            FlavorType primary = hasDominant ? analysis.dominantFlavors[0] : default;

            if (analysis.dominantFlavors != null && analysis.dominantFlavors.Count == 2
                && analysis.balanceScore > thresholds.delightedMinBalance
                && analysis.intensity >= thresholds.delightedMinIntensity)
                triggered.Add(EmotionType.Delighted);

            if (hasDominant && (primary == FlavorType.Sweet || primary == FlavorType.Salty)
                && analysis.balanceScore > thresholds.cozyMinBalance
                && analysis.intensity <= thresholds.cozyMaxIntensity)
                triggered.Add(EmotionType.Cozy);
            if (hasDominant && primary == FlavorType.Acidic && analysis.balanceScore > thresholds.refreshedMinBalance)
                triggered.Add(EmotionType.Refreshed);
            if (hasDominant && primary == FlavorType.Spicy && analysis.balanceScore > thresholds.spicyMinBalance)
                triggered.Add(EmotionType.Spicy);

            triggered.Add(EmotionType.Confused);

            foreach (var emotion in triggered)
            {
                if (responses == null) continue;
                foreach (var r in responses)
                {
                    if (r != null && r.Emotion == emotion) return r;
                }
            }
            return null;
        }

        private static float Sq(float x) => x * x;
    }
}
