using System.Collections;
using System.Collections.Generic;
using System.Text;
using Galatea.Data;
using Galatea.Player;
using Galatea.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Galatea.UI
{
    // Single controller for the retrofuturist HUD overlay. Polls InteractionSystem
    // each frame: held item wins; else aimed item; else clear. Dish vs. ingredient
    // routes to ShowDishPanel / ShowIngredientPanel.
    public class VisorHUD : MonoBehaviour
    {
        // Palette — public so other UI scripts can stay on-palette.
        public static readonly Color BgColor      = new Color32(0x0D, 0x0F, 0x14, 0xFF);
        public static readonly Color PanelColor   = new Color32(0x12, 0x16, 0x1F, 0xFF);
        public static readonly Color BorderColor  = new Color32(0x2A, 0x3A, 0x4A, 0xFF);
        public static readonly Color AccentColor  = new Color32(0x7E, 0xFF, 0xD4, 0xFF);
        public static readonly Color WarmColor    = new Color32(0xFF, 0xCF, 0x77, 0xFF);
        public static readonly Color DimTextColor = new Color32(0x5A, 0x7A, 0x8A, 0xFF);

        [Header("Systems")]
        [SerializeField] private InteractionSystem interactionSystem;

        [Header("Item Panel")]
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text itemFlavorText;
        [SerializeField] private TMP_Text itemTextureText;
        [SerializeField] private TMP_Text itemIconText;

        [Header("Flavor Map")]
        [SerializeField] private FlavorMapGraphic flavorMap;
        [SerializeField] private TMP_Text resultBalanceText;

        [Header("Cooking Progress")]
        [SerializeField] private Image cookFillBar;
        [SerializeField] private TMP_Text cookPercentText;

        [Header("Interaction Prompt")]
        [SerializeField] private TMP_Text interactionPromptText;

        [Header("NAVI AI Strip")]
        [SerializeField] private Image naviPulseImage;
        [SerializeField] private float naviPulseSpeed      = 0.5f;
        [SerializeField] private float naviPulseMinAlpha   = 0.2f;
        [SerializeField] private float naviPulseMaxAlpha   = 0.8f;

        [Header("Sol Counter")]
        [SerializeField] private TMP_Text solText;
        [SerializeField] private float solTickInterval = 1.2f;
        [SerializeField] private int   solStartValue   = 0;
        private int   _solValue;
        private float _solTimer;

        private DishResult _cachedDishResult;
        private string    _cachedBalanceLabel;

        [Header("Creature Reaction")]
        [SerializeField] private TMP_Text creatureReactionText;
        [SerializeField] private float creatureReactionHold = 2.2f;
        [SerializeField] private float creatureReactionFade = 0.4f;
        private Coroutine _creatureReactionRoutine;

        private void Awake()
        {
            _solValue = solStartValue;
            if (solText != null) solText.text = FormatSol(_solValue);
            if (interactionPromptText != null) interactionPromptText.enabled = false;
            if (creatureReactionText != null)
            {
                creatureReactionText.text = string.Empty;
                creatureReactionText.alpha = 0f;
            }
            ClearItemPanel();
        }

        private void Update()
        {
            TickSolCounter();
            PulseNavi();
            UpdateInteractionPrompt();
            UpdateItemPanel();
        }

        private void TickSolCounter()
        {
            if (solText == null || solTickInterval <= 0f) return;
            _solTimer += Time.deltaTime;
            while (_solTimer >= solTickInterval)
            {
                _solTimer -= solTickInterval;
                _solValue++;
                solText.text = FormatSol(_solValue);
            }
        }

        private static string FormatSol(int value) => $"SOL +{value:D5}";

        private void PulseNavi()
        {
            if (naviPulseImage == null) return;
            Color c = naviPulseImage.color;
            float t = Mathf.PingPong(Time.time * naviPulseSpeed, 1f);
            c.a = Mathf.Lerp(naviPulseMinAlpha, naviPulseMaxAlpha, t);
            naviPulseImage.color = c;
        }

        private void UpdateInteractionPrompt()
        {
            if (interactionPromptText == null || interactionSystem == null) return;

            string prompt = interactionSystem.GetInteractionPromptText();
            if (string.IsNullOrEmpty(prompt))
            {
                if (interactionPromptText.enabled) interactionPromptText.enabled = false;
                return;
            }
            if (!interactionPromptText.enabled) interactionPromptText.enabled = true;
            if (interactionPromptText.text != prompt) interactionPromptText.text = prompt;
        }

        // Held wins over aimed: actively holding means the player is working with that
        // item, so showing the aimed context would be misleading.
        private void UpdateItemPanel()
        {
            if (interactionSystem == null) return;

            PickupItem held  = interactionSystem.Held;
            PickupItem aimed = interactionSystem.AimedPickup;

            if (held != null)
            {
                var vessel = held.GetComponent<DishVessel>();
                if (vessel != null) ShowDishPanel(vessel);
                else                ShowIngredientPanel(held.Instance, isLive: false);
                return;
            }

            if (aimed != null)
            {
                bool stationIsProcessing = interactionSystem.AimedStation?.IsProcessing == true;

                var vessel = aimed.GetComponent<DishVessel>();
                if (vessel != null) ShowDishPanel(vessel);
                else                ShowIngredientPanel(aimed.Instance, isLive: stationIsProcessing);
                return;
            }

            ClearItemPanel();
        }

        private void ClearItemPanel()
        {
            SetText(itemNameText,    "—");
            SetText(itemFlavorText,  "");
            SetText(itemTextureText, "");
            SetText(itemIconText,    "");
            SetFlavorProfile(null);
            SetCookProgress(0f, live: false);
            SetBalanceLabel("");
        }

        // isLive: true when the ingredient is in an actively-processing station,
        // appends " ●" to the cook % label.
        private void ShowIngredientPanel(IngredientInstance inst, bool isLive)
        {
            if (inst == null || inst.source == null)
            {
                ClearItemPanel();
                return;
            }

            SetText(itemNameText, inst.source.IngredientName);

            FlavorAnalysis analysis = FlavorCalculator.Analyze(inst.currentProfile);
            SetText(itemFlavorText,  FormatDominant(analysis));
            SetText(itemTextureText, FormatFormAndCook(inst.Form, inst.CookState));
            SetText(itemIconText,    inst.source.IconGlyph ?? string.Empty);

            SetFlavorProfile(ProfileToDict(inst.currentProfile));
            SetCookProgress(inst.cookingProgress, live: isLive);
            SetBalanceLabel("");
        }

        private void ShowDishPanel(DishVessel vessel)
        {
            string dishLabel = vessel.IsDirty
                ? $"dish · {vessel.PlateType} [dirty]"
                : $"dish · {vessel.PlateType}";

            SetText(itemNameText, dishLabel);
            SetText(itemIconText, "");

            DishResult result = vessel.DishResult;

            if (result != null)
            {
                FlavorAnalysis a = result.Analysis;
                SetText(itemFlavorText,  FormatDominant(a));
                SetText(itemTextureText, $"{vessel.IngredientCount} ingredient{(vessel.IngredientCount == 1 ? "" : "s")}");
                SetFlavorProfile(ProfileToDict(result.CombinedProfile));

                if (_cachedDishResult != result)
                {
                    _cachedDishResult = result;
                    _cachedBalanceLabel = FormatCulinaryBalance(a);
                }
                SetBalanceLabel(_cachedBalanceLabel);
            }
            else
            {
                int count = vessel.IngredientCount;
                SetText(itemFlavorText,  "");
                SetText(itemTextureText, $"{count} ingredient{(count == 1 ? "" : "s")}");
                SetFlavorProfile(null);
                SetBalanceLabel("—");
            }

            SetCookProgress(0f, live: false);
        }

        private void SetFlavorProfile(Dictionary<string, float> values)
        {
            if (flavorMap != null) flavorMap.SetValues(values);
        }

        // live=true appends " ●" to indicate active processing.
        private void SetCookProgress(float value, bool live)
        {
            value = Mathf.Clamp01(value);
            if (cookFillBar != null)
            {
                cookFillBar.fillAmount = value;
                cookFillBar.color = Color.Lerp(AccentColor, WarmColor, value);
            }
            if (cookPercentText != null)
            {
                string pct = $"{Mathf.RoundToInt(value * 100f)}%";
                cookPercentText.text = live ? pct + " ●" : pct;
            }
        }

        private void SetBalanceLabel(string label)
        {
            if (resultBalanceText != null) resultBalanceText.text = label;
        }

        // Brief HUD narration cue when the creature reacts. Fades in / holds / out.
        public void ShowCreatureReaction(string message)
        {
            if (creatureReactionText == null) return;
            if (_creatureReactionRoutine != null) StopCoroutine(_creatureReactionRoutine);
            if (string.IsNullOrEmpty(message))
            {
                creatureReactionText.text = string.Empty;
                creatureReactionText.alpha = 0f;
                return;
            }
            _creatureReactionRoutine = StartCoroutine(CreatureReactionRoutine(message));
        }

        private IEnumerator CreatureReactionRoutine(string message)
        {
            creatureReactionText.text = message;
            yield return FadeAlpha(creatureReactionText, 0f, 1f, creatureReactionFade);
            yield return new WaitForSeconds(creatureReactionHold);
            yield return FadeAlpha(creatureReactionText, 1f, 0f, creatureReactionFade);
            creatureReactionText.text = string.Empty;
            _creatureReactionRoutine = null;
        }

        private IEnumerator FadeAlpha(TMP_Text text, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                text.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            text.alpha = to;
        }

        // Neutralizer excluded: the chart has 5 axes matching the tasteable dimensions.
        private static Dictionary<string, float> ProfileToDict(FlavorProfile fp) =>
            new Dictionary<string, float>
            {
                { "sweet",  fp.sweet  / 10f },
                { "acidic", fp.acidic / 10f },
                { "bitter", fp.bitter / 10f },
                { "salty",  fp.salty  / 10f },
                { "spicy",  fp.spicy  / 10f },
            };

        private static string FormatFormAndCook(Form form, CookState cookState)
        {
            if (cookState == CookState.Burnt) return "burnt";

            string formStr = form switch
            {
                Form.Whole     => "whole",
                Form.Chopped   => "chopped",
                Form.Diced     => "diced",
                Form.Julienned => "julienned",
                Form.Ground    => "ground",
                Form.Juiced    => "juiced",
                Form.Blended   => "blended",
                _              => form.ToString().ToLower(),
            };
            string cookStr = cookState switch
            {
                CookState.Raw         => "raw",
                CookState.Sauteed     => "sautéed",
                CookState.Caramelized => "caramelized",
                CookState.Boiled      => "boiled",
                CookState.Mush        => "mush",
                CookState.Fried       => "fried",
                CookState.Baked       => "baked",
                _                     => cookState.ToString().ToLower(),
            };
            return $"{cookStr} · {formStr}";
        }

        private static string FormatDominant(FlavorAnalysis a)
        {
            if (a.dominantFlavors == null || a.dominantFlavors.Count == 0)
                return "no dominant";
            var sb = new StringBuilder();
            for (int i = 0; i < a.dominantFlavors.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(a.dominantFlavors[i].ToString().ToLower());
            }
            return sb.ToString();
        }

        private static readonly string[] NaviBurnt       = { "that's charcoal.", "...was that on purpose?", "R.I.P." };
        private static readonly string[] NaviFaint       = { "barely there...", "needs more oomph.", "are you sure?" };
        private static readonly string[] NaviSharp       = { "one-note.", "a bit much.", "pick a lane?" };
        private static readonly string[] NaviOverwhelm   = { "EVERYTHING at once.", "chaotic.", "my sensors hurt." };
        private static readonly string[] NaviUneven      = { "almost...", "getting somewhere.", "not bad, not great." };
        private static readonly string[] NaviLayered     = { "interesting combo.", "oh, layers!", "I see what you did." };
        private static readonly string[] NaviIntense     = { "that's intense.", "wow, bold move.", "a lot going on." };
        private static readonly string[] NaviGentle      = { "soft.", "nice and easy.", "subtle." };
        private static readonly string[] NaviRounded     = { "oh, that's solid.", "sounds good...", "well-rounded." };
        private static readonly string[] NaviBold        = { "that's bold!", "going big.", "now we're cooking." };
        private static readonly string[] NaviDelicate    = { "delicate.", "light touch.", "elegant, maybe?" };
        private static readonly string[] NaviBalanced    = { "ooh, balanced.", "that works.", "chef's kiss?" };
        private static readonly string[] NaviHarmonious  = { "perfect harmony.", "couldn't be better.", "wow." };

        private static string Pick(string[] pool)
        {
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        private static string FormatCulinaryBalance(FlavorAnalysis a)
        {
            if (a.isBurnt) return Pick(NaviBurnt);

            float b = a.balanceScore;
            float t = a.intensity;

            if (t < 3f)    return Pick(NaviFaint);
            if (b < 0.10f) return t > 15f ? Pick(NaviOverwhelm) : Pick(NaviSharp);
            if (b < 0.18f) return t > 12f ? Pick(NaviIntense) : t < 6f ? Pick(NaviUneven) : Pick(NaviLayered);
            if (b < 0.30f) return t > 15f ? Pick(NaviBold) : t < 6f ? Pick(NaviGentle) : Pick(NaviRounded);
            return t > 15f ? Pick(NaviHarmonious) : t < 6f ? Pick(NaviDelicate) : Pick(NaviBalanced);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
