using System.Collections.Generic;
using UnityEngine;
using Galatea.Data;
using Galatea.Player;

namespace Galatea.Systems
{
    // Primary plating object. Lives on a PickupItem so the player physically carries
    // the dish to the creature and then to the CleanupStation. Owns the ingredient
    // list, the baked DishResult, and the clean/dirty visual swap.
    [RequireComponent(typeof(PickupItem))]
    public class DishVessel : MonoBehaviour
    {
        [SerializeField] private PlateType plateType = PlateType.FlatPlate;
        [SerializeField] private int maxIngredients = 5;

        [SerializeField] private GameObject cleanVisualRoot;

        // Child of cleanVisualRoot containing FoodSlot_0…N. Falls back to
        // cleanVisualRoot itself when unset, but then the plate mesh must not
        // be a child of cleanVisualRoot (it would get hidden in Awake).
        [SerializeField] private Transform ingredientSlotsRoot;

        [SerializeField] private GameObject dirtyVisualRoot;

        private readonly List<IngredientInstance> _ingredients = new List<IngredientInstance>();

        public DishResult DishResult { get; private set; }
        public bool IsDirty { get; private set; }
        public bool HasCapacity => _ingredients.Count < maxIngredients;
        public int IngredientCount => _ingredients.Count;
        public PlateType PlateType => plateType;

        private void Awake()
        {
            var slotsParent = ingredientSlotsRoot != null ? ingredientSlotsRoot : (cleanVisualRoot != null ? cleanVisualRoot.transform : null);
            if (slotsParent != null)
            {
                for (int i = 0; i < slotsParent.childCount; i++)
                    slotsParent.GetChild(i).gameObject.SetActive(false);
            }
            ApplyVisualState();
        }

        // Pre: dish has capacity, is clean, not yet finalized, and item has an Instance.
        // Post: ingredient added to the list, its PickupItem reparented onto a FoodSlot
        // child as a pure visual, and physics + PickupItem MonoBehaviour stripped.
        public bool AbsorbIngredient(PickupItem item)
        {
            if (item == null || item.Instance == null) return false;
            if (IsDirty || DishResult != null) return false;
            if (!HasCapacity) return false;

            int index = _ingredients.Count;
            _ingredients.Add(item.Instance);

            MountIngredientOnSlot(index, item);
            SoundManager.PlayItemPlacedOnPlate(transform.position);
            return true;
        }

        // Pre: at least one ingredient absorbed.
        // Post: DishResult is built (idempotent — repeated calls return true without
        // rebuilding). Returns false on an empty dish; callers use this to block pickup.
        public bool Finalize()
        {
            if (_ingredients.Count == 0) return false;
            if (DishResult != null) return true;

            bool anyBurnt = false;
            foreach (var ing in _ingredients)
                if (ing.IsBurnt) { anyBurnt = true; break; }

            FlavorProfile combined = FlavorCalculator.Combine(_ingredients);
            FlavorAnalysis analysis = FlavorCalculator.Analyze(combined, anyBurnt);
            DishResult = new DishResult(new List<IngredientInstance>(_ingredients), combined, analysis, plateType);
            return true;
        }

        // Pre: dish has been served to the creature.
        // Post: IsDirty == true; dirty visual replaces the clean one (or is kept
        // as a placeholder when no dirtyVisualRoot is wired). Idempotent.
        public void SetDirty()
        {
            if (IsDirty) return;
            IsDirty = true;
            if (dirtyVisualRoot == null) return;
            ApplyVisualState();
        }

        // Reparents the absorbed PickupItem onto the FoodSlot child so the dish shows
        // the actual ingredient mesh + cook state instead of a placeholder. Strips
        // Rigidbody, all Colliders, and the PickupItem MonoBehaviour itself — the
        // GameObject lives on as pure decoration.
        private void MountIngredientOnSlot(int index, PickupItem item)
        {
            var slotsParent = ingredientSlotsRoot != null ? ingredientSlotsRoot : (cleanVisualRoot != null ? cleanVisualRoot.transform : null);
            if (slotsParent == null || index >= slotsParent.childCount)
            {
                Destroy(item.gameObject);
                return;
            }

            Transform mountPoint = slotsParent.GetChild(index);
            mountPoint.gameObject.SetActive(true);

            foreach (var r in mountPoint.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
                r.enabled = false;

            Transform t = item.transform;
            t.SetParent(mountPoint, worldPositionStays: false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;

            if (item.TryGetComponent<Rigidbody>(out var rb)) Destroy(rb);
            foreach (var col in item.GetComponentsInChildren<Collider>(includeInactive: true))
                Destroy(col);

            Destroy(item);
        }

        private void ApplyVisualState()
        {
            if (cleanVisualRoot != null) cleanVisualRoot.SetActive(!IsDirty);
            if (dirtyVisualRoot != null) dirtyVisualRoot.SetActive(IsDirty);
        }
    }
}
