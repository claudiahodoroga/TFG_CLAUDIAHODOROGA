using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Galatea.Data
{
    // One (Form, CookState) → Mesh entry. Each authored mesh carries its own
    // material/texture, so the "cooked look" is fully visual via mesh swap.
    [Serializable]
    public class MeshOverride
    {
        // Legacy "state" YAML key predates the Form/CookState split; values line up.
        [FormerlySerializedAs("state")]
        public Form form;
        public CookState cookState = CookState.Raw;
        public Mesh mesh;
    }

    [CreateAssetMenu(fileName = "NewIngredient", menuName = "Galatea/Ingredient")]
    public class IngredientData : ScriptableObject
    {
        [SerializeField] private string ingredientName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private IngredientTag tags;
        [SerializeField] private FlavorProfile baseProfile;

        [Header("Cut Response — applied once on discrete completion")]
        [SerializeField] private FlavorProfile cutMultiplier = new FlavorProfile(1f,1f,1f,1f,1f,1f);
        [SerializeField] private FlavorProfile cutModifier;

        [Header("Dry Heat Response — per-tick delta")]
        [SerializeField] private FlavorProfile dryHeatStage1Delta;
        [SerializeField] private FlavorProfile dryHeatStage2Delta;
        [SerializeField] private FlavorProfile dryHeatStage3Delta;

        [Header("Wet Heat Response — per-tick delta")]
        [SerializeField] private FlavorProfile wetHeatStage1Delta;
        [SerializeField] private FlavorProfile wetHeatStage2Delta;
        [SerializeField] private FlavorProfile wetHeatStage3Delta;

        [SerializeField] private Sprite icon;
        [Tooltip("Single-character Unicode glyph shown in the HUD's ItemIcon slot.")]
        [SerializeField] private string iconGlyph;
        [SerializeField] private GameObject prefab;
        [SerializeField] private List<MeshOverride> meshOverrides;

        public string IngredientName => ingredientName;
        public string Description => description;
        public IngredientTag Tags => tags;
        public FlavorProfile BaseProfile => baseProfile;
        public FlavorProfile CutMultiplier      => cutMultiplier;
        public FlavorProfile CutModifier        => cutModifier;
        public FlavorProfile DryHeatStage1Delta => dryHeatStage1Delta;
        public FlavorProfile DryHeatStage2Delta => dryHeatStage2Delta;
        public FlavorProfile DryHeatStage3Delta => dryHeatStage3Delta;
        public FlavorProfile WetHeatStage1Delta => wetHeatStage1Delta;
        public FlavorProfile WetHeatStage2Delta => wetHeatStage2Delta;
        public FlavorProfile WetHeatStage3Delta => wetHeatStage3Delta;
        public Sprite Icon => icon;
        public string IconGlyph => iconGlyph;
        public GameObject Prefab => prefab;
        public IReadOnlyList<MeshOverride> MeshOverrides => meshOverrides;

        // Pre: meshOverrides may be null/empty.
        // Post: returns the best-matching mesh for (form, cookState) via a 4-tier
        // fallback (exact → same form + Raw → chopped family → liquid family) or
        // null when no override fits; PickupItem then falls back to its prefab mesh.
        public Mesh GetMesh(Form form, CookState cookState)
        {
            if (meshOverrides == null || meshOverrides.Count == 0) return null;

            Mesh exact = FindOverride(form, cookState);
            if (exact != null) return exact;

            if (cookState != CookState.Raw)
            {
                Mesh rawSameForm = FindOverride(form, CookState.Raw);
                if (rawSameForm != null) return rawSameForm;
            }

            if (form == Form.Chopped || form == Form.Diced ||
                form == Form.Julienned || form == Form.Ground)
            {
                Mesh f =  FindOverride(Form.Chopped, cookState)
                       ?? FindOverride(Form.Diced, cookState)
                       ?? FindOverride(Form.Julienned, cookState)
                       ?? FindOverride(Form.Ground, cookState)
                       ?? FindOverride(Form.Chopped, CookState.Raw)
                       ?? FindOverride(Form.Diced, CookState.Raw)
                       ?? FindOverride(Form.Julienned, CookState.Raw)
                       ?? FindOverride(Form.Ground, CookState.Raw);
                if (f != null) return f;
            }

            if (form == Form.Juiced || form == Form.Blended)
            {
                Mesh f =  FindOverride(Form.Juiced, cookState)
                       ?? FindOverride(Form.Blended, cookState)
                       ?? FindOverride(Form.Juiced, CookState.Raw)
                       ?? FindOverride(Form.Blended, CookState.Raw);
                if (f != null) return f;
            }

            return null;
        }

        private Mesh FindOverride(Form form, CookState cookState)
        {
            foreach (var entry in meshOverrides)
            {
                if (entry != null && entry.form == form && entry.cookState == cookState && entry.mesh != null)
                    return entry.mesh;
            }
            return null;
        }
    }
}
