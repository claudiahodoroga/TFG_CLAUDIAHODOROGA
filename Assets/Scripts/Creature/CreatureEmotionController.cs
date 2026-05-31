using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Galatea.Data;
using Galatea.Systems;
using Galatea.UI;

namespace Galatea.Creature
{
    // Passive recipient of plated dishes. Reads the vessel's baked DishResult,
    // evaluates the matching EmotionalResponse, and runs a tint + HUD narration
    // cue. Petting bypasses evaluation and always plays the Delighted response.
    public class CreatureEmotionController : MonoBehaviour
    {
        [SerializeField] private List<EmotionalResponse> emotionalResponses;
        [SerializeField] private Renderer creatureRenderer;
        [SerializeField] private float fadeOutDuration = 4f;
        [Tooltip("Optional. Auto-resolves to the first VisorHUD in the scene on first reaction.")]
        [SerializeField] private VisorHUD visorHUD;

        [Header("Emotion Thresholds")]
        [SerializeField, Range(0f, 0.5f)] private float disgustMaxBalance = 0.10f;
        [SerializeField] private float disappointedMaxIntensity = 3f;
        [SerializeField, Range(0f, 1f)] private float cozyMinBalance = 0.10f;
        [SerializeField] private float cozyMaxIntensity = 20f;
        [SerializeField, Range(0f, 1f)] private float refreshedMinBalance = 0.25f;
        [SerializeField, Range(0f, 1f)] private float spicyMinBalance = 0.25f;
        [SerializeField, Range(0f, 1f)] private float delightedMinBalance = 0.15f;
        [SerializeField] private float delightedMinIntensity = 8f;

        [Header("Diagnostics")]
        [Tooltip("Logs combined profile + analysis + matched emotion to the console on every reaction. Use during MEMORIA §7.8 test-case validation.")]
        [SerializeField] private bool logEvaluations = true;

        // URP Lit / Shader Graph → _BaseColor. Built-in Standard / legacy → _Color.
        // We probe both so the tint works regardless of which shader the material uses.
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId     = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _mpb;
        private Coroutine _fadeRoutine;
        private bool _hasBaseColor;
        private bool _hasColor;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();

            if (creatureRenderer == null)
            {
                creatureRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
                if (creatureRenderer == null) creatureRenderer = GetComponentInChildren<MeshRenderer>();
            }

            if (creatureRenderer != null && creatureRenderer.sharedMaterial != null)
            {
                var mat = creatureRenderer.sharedMaterial;
                _hasBaseColor = mat.HasProperty(BaseColorPropId);
                _hasColor     = mat.HasProperty(ColorPropId);
            }
        }

        // Pre: vessel has a finalized DishResult.
        // Post: creature reacts to the dish's analysis and the vessel is marked dirty
        // so the player can carry it on to a CleanupStation.
        public void FeedDish(DishVessel vessel)
        {
            if (vessel == null || vessel.DishResult == null) return;
            SoundManager.PlayCreatureFed(transform.position);
            React(vessel.DishResult.CombinedProfile, vessel.DishResult.Analysis,
                  vessel.DishResult.Ingredients, "dish");
            vessel.SetDirty();
        }

        // Legacy single-ingredient feed path. Predates DishVessel; still wired to
        // the E-key fallback when the player aims at the creature with a raw ingredient.
        public void Feed(IngredientInstance ingredient)
        {
            if (ingredient == null) return;
            FlavorAnalysis analysis = FlavorCalculator.Analyze(ingredient.currentProfile, ingredient.IsBurnt);
            SoundManager.PlayCreatureFed(transform.position);
            React(ingredient.currentProfile, analysis,
                  new[] { ingredient },
                  ingredient.source != null ? ingredient.source.IngredientName : "ingredient");
        }

        public void OnPetted() => ReactWithEmotion(EmotionType.Delighted);

        private void React(FlavorProfile profile, FlavorAnalysis analysis,
                           IReadOnlyList<IngredientInstance> ingredients, string sourceLabel)
        {
            var thresholds = new EmotionThresholds
            {
                disgustMaxBalance = disgustMaxBalance,
                disappointedMaxIntensity = disappointedMaxIntensity,
                cozyMinBalance = cozyMinBalance,
                cozyMaxIntensity = cozyMaxIntensity,
                refreshedMinBalance = refreshedMinBalance,
                spicyMinBalance = spicyMinBalance,
                delightedMinBalance = delightedMinBalance,
                delightedMinIntensity = delightedMinIntensity
            };
            EmotionalResponse response = FlavorCalculator.EvaluateEmotion(analysis, emotionalResponses, thresholds);
            LogEvaluation(sourceLabel, profile, analysis, ingredients, response);
            if (response == null) return;
            ApplyResponse(response);
        }

        private void LogEvaluation(string sourceLabel, FlavorProfile profile, FlavorAnalysis analysis,
                                   IReadOnlyList<IngredientInstance> ingredients, EmotionalResponse response)
        {
            if (!logEvaluations) return;

            string dominant = (analysis.dominantFlavors == null || analysis.dominantFlavors.Count == 0)
                ? "—"
                : string.Join(" · ", analysis.dominantFlavors);
            string emotion = response != null ? response.Emotion.ToString() : "(no matching asset)";

            var sb = new System.Text.StringBuilder();
            sb.Append($"[Creature] Served {sourceLabel}\n");

            if (ingredients != null && ingredients.Count > 0)
            {
                sb.Append($"  Ingredients ({ingredients.Count}):\n");
                for (int i = 0; i < ingredients.Count; i++)
                {
                    var ing = ingredients[i];
                    if (ing == null) { sb.Append($"    [{i}] (null)\n"); continue; }
                    string name = ing.source != null ? ing.source.IngredientName : "(unknown)";
                    sb.Append($"    [{i}] {name} — {ing.Form}/{ing.CookState} — cook {ing.cookingProgress * 100f:F1}%\n");
                    sb.Append($"        {ing.currentProfile}\n");
                }
            }

            sb.Append($"  Combined    : {profile}\n");
            sb.Append($"  Intensity   : {analysis.intensity:F2}\n");
            sb.Append($"  BalanceScore: {analysis.balanceScore:F3}\n");
            sb.Append($"  Dominant    : {dominant}\n");
            sb.Append($"  IsBurnt     : {analysis.isBurnt}\n");
            sb.Append($"  → Emotion   : {emotion}");

            Debug.Log(sb.ToString());
        }

        private void ReactWithEmotion(EmotionType emotion)
        {
            EmotionalResponse response = FindResponse(emotion);
            if (response == null) return;
            ApplyResponse(response);
        }

        private EmotionalResponse FindResponse(EmotionType emotion)
        {
            if (emotionalResponses == null) return null;
            foreach (var r in emotionalResponses)
                if (r != null && r.Emotion == emotion) return r;
            return null;
        }

        private void ApplyResponse(EmotionalResponse response)
        {
            SoundManager.PlayCreatureReaction(response.Emotion, transform.position);
            BroadcastReactionLine(response);

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            Color peakColor = response.ColorGradient.Evaluate(0.5f);
            ApplyColor(peakColor);

            _fadeRoutine = StartCoroutine(FadeToNeutral(peakColor));
        }

        private IEnumerator FadeToNeutral(Color from)
        {
            yield return new WaitForSeconds(1.5f);

            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                float t = elapsed / fadeOutDuration;
                ApplyColor(Color.Lerp(from, Color.white, t));
                elapsed += Time.deltaTime;
                yield return null;
            }
            ApplyColor(Color.white);
            _fadeRoutine = null;
        }

        // Falls back to a hardcoded phrase per emotion when the response asset has
        // no authored reactionLine, so empty assets still produce something on screen.
        private void BroadcastReactionLine(EmotionalResponse response)
        {
            if (visorHUD == null) visorHUD = FindObjectOfType<VisorHUD>();
            if (visorHUD == null) return;

            string line = response.ReactionLine;
            if (string.IsNullOrEmpty(line)) line = DefaultLineFor(response.Emotion);
            visorHUD.ShowCreatureReaction(line);
        }

        private static string DefaultLineFor(EmotionType emotion)
        {
            switch (emotion)
            {
                case EmotionType.Cozy:         return "Mmm, that's cozy!";
                case EmotionType.Refreshed:    return "Ah, refreshing!";
                case EmotionType.Spicy:        return "Woah, spicy!";
                case EmotionType.Delighted:    return "Oh, delicious!";
                case EmotionType.Confused:     return "Hmm... curious.";
                case EmotionType.Disappointed: return "Eh, not much there.";
                case EmotionType.Disgusted:    return "Ugh, awful!";
                default:                       return "...";
            }
        }

        private void ApplyColor(Color color)
        {
            if (creatureRenderer == null) return;
            creatureRenderer.GetPropertyBlock(_mpb);
            if (_hasBaseColor) _mpb.SetColor(BaseColorPropId, color);
            if (_hasColor)     _mpb.SetColor(ColorPropId,     color);
            creatureRenderer.SetPropertyBlock(_mpb);
        }
    }
}
