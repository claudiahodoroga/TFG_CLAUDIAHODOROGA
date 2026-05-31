using System;

namespace Galatea.Data
{
    // Six flavor attributes, each clamped to [0,10] after any calculation.
    [Serializable]
    public struct FlavorProfile
    {
        public float sweet;
        public float acidic;
        public float bitter;
        public float salty;
        public float spicy;
        public float neutralizer;

        public FlavorProfile(float sweet, float acidic, float bitter, float salty, float spicy, float neutralizer)
        {
            this.sweet = sweet;
            this.acidic = acidic;
            this.bitter = bitter;
            this.salty = salty;
            this.spicy = spicy;
            this.neutralizer = neutralizer;
        }

        public static FlavorProfile Zero => new FlavorProfile(0f, 0f, 0f, 0f, 0f, 0f);

        public FlavorProfile Clamp()
        {
            return new FlavorProfile(
                Math.Clamp(sweet, 0f, 10f),
                Math.Clamp(acidic, 0f, 10f),
                Math.Clamp(bitter, 0f, 10f),
                Math.Clamp(salty, 0f, 10f),
                Math.Clamp(spicy, 0f, 10f),
                Math.Clamp(neutralizer, 0f, 10f)
            );
        }

        public override string ToString() =>
            $"Sweet: {sweet:F1}, Acidic: {acidic:F1}, Bitter: {bitter:F1}, Salty: {salty:F1}, Spicy: {spicy:F1}, Neutralizer: {neutralizer:F1}";
    }
}
