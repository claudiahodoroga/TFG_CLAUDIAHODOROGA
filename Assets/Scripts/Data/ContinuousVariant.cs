using UnityEngine;

namespace Galatea.Data
{
    // Per-tick heat process. Cook progression crosses three CookState bands at
    // two thresholds:
    //   0 …………… Stage2Threshold       → ResultCookState  (Stage 1)
    //   Stage2Threshold … Stage3Threshold → Stage2CookState
    //   Stage3Threshold … 1.0           → Stage3CookState
    // Per-tick flavor delta is pulled from the ingredient's per-Category fields
    // (DryHeat / WetHeat) on IngredientData. Mesh is NOT changed by cooking.
    [CreateAssetMenu(fileName = "NewContinuousVariant", menuName = "Galatea/Continuous Variant")]
    public class ContinuousVariant : ProcessVariant
    {
        [Header("Stage 1 — initial cook")]
        [SerializeField] private CookState resultCookState = CookState.Sauteed;

        [Header("Stage 2")]
        [SerializeField] private CookState stage2CookState = CookState.Caramelized;
        [SerializeField, Range(0f, 1f)] private float stage2Threshold = 0.6f;

        [Header("Stage 3")]
        [SerializeField] private CookState stage3CookState = CookState.Burnt;
        [SerializeField, Range(0f, 1f)] private float stage3Threshold = 0.9f;

        [SerializeField, Min(0f)] private float heatLevelMultiplier = 1f;

        public CookState ResultCookState => resultCookState;
        public CookState Stage2CookState => stage2CookState;
        public CookState Stage3CookState => stage3CookState;
        public float Stage2Threshold => stage2Threshold;
        public float Stage3Threshold => stage3Threshold;
        public float HeatLevelMultiplier => heatLevelMultiplier;
    }
}
