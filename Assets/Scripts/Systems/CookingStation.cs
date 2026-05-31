using System.Collections.Generic;
using UnityEngine;

namespace Galatea.Systems
{
    // Thin host that owns a list of StationSlot children. Each slot carries its own
    // ProcessVariant + heat level, so a single station can mix process types
    // (e.g. one sauté slot + one boil slot + one bake slot side-by-side).
    public class CookingStation : MonoBehaviour
    {
        [SerializeField] private List<StationSlot> slots = new List<StationSlot>();

        public bool IsProcessing
        {
            get
            {
                foreach (var s in slots)
                    if (s != null && s.IsProcessing) return true;
                return false;
            }
        }

        public int IngredientCount
        {
            get
            {
                int n = 0;
                foreach (var s in slots) if (s != null && s.IsOccupied) n++;
                return n;
            }
        }

        private void Awake()
        {
            if (slots == null || slots.Count == 0)
                slots = new List<StationSlot>(GetComponentsInChildren<StationSlot>(includeInactive: true));
        }
    }
}
