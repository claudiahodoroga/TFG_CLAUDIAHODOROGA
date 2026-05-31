using UnityEngine;
using UnityEngine.InputSystem;
using Galatea.Creature;
using Galatea.Data;
using Galatea.Systems;

namespace Galatea.Player
{
    // Implemented by anything the player can place a PickupItem into (StationSlot,
    // CleanupStation). The trigger collider may live on a child — callers resolve
    // the slot via GetComponentInParent.
    public interface IStationSlot
    {
        bool TryAccept(PickupItem item);
        void Release();
        bool IsOccupied { get; }
    }

    // E-key + left-click interaction state machine, driven by a per-frame raycast
    // from the camera. Priority order is documented on PerformInteraction.
    public class InteractionSystem : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private float maxDistance = 2.5f;
        [SerializeField] private float dropForwardOffset = 0.8f;
        [SerializeField] private InputAction interactAction;
        [SerializeField] private InputAction petAction;

        private int _interactRaycastMask;

        public PickupItem Held { get; private set; }
        public PickupItem AimedPickup  => _aimedPickup;
        public CookingStation AimedStation => _aimedStation;

        private PickupItem _aimedPickup;
        private IStationSlot _aimedSlot;
        private CookingStation _aimedStation;
        private CreatureEmotionController _aimedCreature;
        private PlatingStation _aimedPlatingStation;
        private DishVessel _aimedDishVessel;
        private IngredientBasket _aimedBasket;

        private int _lastInteractFrame = -1;
        private int _lastPetFrame = -1;

        public bool IsLookingAtInteractable =>
            _aimedPickup != null   ||
            _aimedStation != null  ||
            _aimedSlot != null     ||
            _aimedCreature != null ||
            _aimedDishVessel != null ||
            _aimedBasket != null   ||
            (_aimedPlatingStation != null && !_aimedPlatingStation.HasDish);

        // Pre: per-frame raycast state is current (Update has run this frame).
        // Post: returns the HUD prompt string for the current aim + held state.
        // Priority mirrors PerformInteraction so the prompt matches what E will do.
        public string GetInteractionPromptText()
        {
            if (Held != null && _aimedSlot != null && !_aimedSlot.IsOccupied)
            {
                var heldVesselDirty = Held.GetComponent<DishVessel>();
                if (heldVesselDirty != null && heldVesselDirty.IsDirty) return "E to clean";
                return "E to place";
            }

            if (Held != null && _aimedCreature != null) return "E to feed";

            if (Held != null && _aimedDishVessel != null && Held.GetComponent<DishVessel>() == null)
                return "E to add to plate";

            if (Held == null && _aimedPickup != null) return "E to pick up";

            if (Held == null && _aimedBasket != null) return "R to spawn";

            if (Held == null && _aimedSlot is StationSlot cookingSlot
                             && cookingSlot.IsOccupied
                             && cookingSlot.Variant != null)
            {
                string verb = cookingSlot.IsProcessing ? "stop" : "start";
                return $"E to {verb} {FriendlyVariantName(cookingSlot.Variant)}";
            }

            if (Held == null && _aimedPlatingStation != null && !_aimedPlatingStation.HasDish)
                return "E to spawn plate";

            if (Held != null) return "E to drop";

            return string.Empty;
        }

        private void Awake()
        {
            _interactRaycastMask = ~LayerMask.GetMask("Player");
            if (interactAction.bindings.Count == 0)
                interactAction.AddBinding("<Keyboard>/e");
            if (petAction.bindings.Count == 0)
                petAction.AddBinding("<Mouse>/leftButton");
        }

        private void OnEnable()
        {
            interactAction.Enable();
            interactAction.performed += OnInteract;
            petAction.Enable();
            petAction.performed += OnPet;
        }

        private void OnDisable()
        {
            interactAction.performed -= OnInteract;
            interactAction.Disable();
            petAction.performed -= OnPet;
            petAction.Disable();
        }

        private void Update()
        {
            if (cameraTransform == null)
            {
                ClearAimedTargets();
                return;
            }

            // QueryTriggerInteraction.Collide so trigger colliders on station slots
            // are also detected.
            bool hasHit = Physics.Raycast(
                cameraTransform.position, cameraTransform.forward,
                out RaycastHit hit, maxDistance, _interactRaycastMask, QueryTriggerInteraction.Collide);

            if (hasHit)
            {
                _aimedPickup         = hit.collider.GetComponentInParent<PickupItem>();
                _aimedSlot           = FindStationSlot(hit.collider);
                _aimedStation        = hit.collider.GetComponentInParent<CookingStation>();
                _aimedCreature       = hit.collider.GetComponentInParent<CreatureEmotionController>();
                _aimedPlatingStation = hit.collider.GetComponentInParent<PlatingStation>();
                _aimedDishVessel     = hit.collider.GetComponentInParent<DishVessel>();
                _aimedBasket         = hit.collider.GetComponentInParent<IngredientBasket>();
            }
            else
            {
                ClearAimedTargets();
            }

            // Keyboard/mouse fallbacks run unconditionally so input stays alive even
            // if the InputAction callbacks fail to re-register (e.g. after scene reload).
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                TryPerformInteraction();
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                TryPerformPet();
        }

        private void ClearAimedTargets()
        {
            _aimedPickup         = null;
            _aimedSlot           = null;
            _aimedStation        = null;
            _aimedCreature       = null;
            _aimedPlatingStation = null;
            _aimedDishVessel     = null;
            _aimedBasket         = null;
        }

        private void OnInteract(InputAction.CallbackContext ctx) => TryPerformInteraction();
        private void OnPet(InputAction.CallbackContext ctx) => TryPerformPet();

        private void TryPerformInteraction()
        {
            if (Time.frameCount == _lastInteractFrame) return;
            _lastInteractFrame = Time.frameCount;
            PerformInteraction();
        }

        private void TryPerformPet()
        {
            if (Time.frameCount == _lastPetFrame) return;
            _lastPetFrame = Time.frameCount;
            if (_aimedCreature == null) return;
            _aimedCreature.OnPetted();
        }

        // E-key cases, evaluated in order; first match wins:
        //  1. Held + slot accepts                → ReleaseToSlot (place / clean).
        //  2. Held DishVessel + creature         → FeedDish (vessel goes dirty, kept).
        //  3. Held ingredient + creature         → FeedCreature (legacy single-ingredient).
        //  4. Held ingredient + aimed DishVessel → AbsorbIngredient.
        //  5. Empty + PickupItem                 → Pickup (empty DishVessel blocks).
        //  6. Empty + cooking slot (occupied, variant) → toggle THAT slot's process.
        //  7. Empty + empty PlatingStation       → SpawnDish.
        //  8. Held + no match                    → Drop.
        private void PerformInteraction()
        {
            if (Held != null && _aimedSlot != null && _aimedSlot.TryAccept(Held))
            {
                ReleaseToSlot();
                return;
            }

            if (Held != null && _aimedCreature != null)
            {
                var heldVessel = Held.GetComponent<DishVessel>();
                if (heldVessel != null)
                {
                    if (heldVessel.DishResult == null) return;
                    _aimedCreature.FeedDish(heldVessel);
                    return;
                }
                FeedCreature(_aimedCreature);
                return;
            }

            if (Held != null && _aimedDishVessel != null && Held.GetComponent<DishVessel>() == null)
            {
                if (_aimedDishVessel.AbsorbIngredient(Held)) Held = null;
                return;
            }

            if (Held == null && _aimedPickup != null)
            {
                var vesselOnPickup = _aimedPickup.GetComponent<DishVessel>();
                if (vesselOnPickup != null)
                {
                    if (!vesselOnPickup.Finalize()) return;
                    if (_aimedPlatingStation != null) _aimedPlatingStation.NotifyDishPickedUp();
                }
                Pickup(_aimedPickup);
                return;
            }

            if (Held == null && _aimedSlot is StationSlot cookingSlot
                             && cookingSlot.IsOccupied
                             && cookingSlot.Variant != null)
            {
                if (cookingSlot.IsProcessing) cookingSlot.StopProcess();
                else                          cookingSlot.StartProcess();
                return;
            }

            if (Held == null && _aimedPlatingStation != null && !_aimedPlatingStation.HasDish)
            {
                _aimedPlatingStation.SpawnDish();
                return;
            }

            if (Held != null) Drop();
        }

        private void Pickup(PickupItem item)
        {
            if (item.CurrentSlot != null)
            {
                item.CurrentSlot.Release();
                item.CurrentSlot = null;
            }
            if (holdPoint != null)
            {
                item.transform.SetParent(holdPoint, worldPositionStays: false);
                item.transform.localScale = Vector3.one;
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
            }
            item.OnPickedUp();
            // Disable colliders so the held item doesn't block the interaction raycast.
            item.DisableColliderForHolding();
            Held = item;
        }

        private void Drop()
        {
            var item = Held;
            if (item == null) return;
            // worldPositionStays:false preserves the item's original localScale rather
            // than baking holdPoint's lossy scale into the item on each drop cycle.
            item.transform.SetParent(null, worldPositionStays: false);
            item.transform.localScale = Vector3.one;
            if (cameraTransform != null)
                item.transform.position = cameraTransform.position + cameraTransform.forward * dropForwardOffset;
            item.EnableCollider();
            item.OnDropped();
            Held = null;
        }

        // Returns the exact slot the raycast hit — no silent re-routing to a free
        // sibling. Tier 1: collider itself. Tier 2: walk up parents.
        private static IStationSlot FindStationSlot(Collider c)
        {
            var slot = c.GetComponent<IStationSlot>();
            if (slot != null) return slot;
            return c.GetComponentInParent<IStationSlot>();
        }

        private void FeedCreature(CreatureEmotionController creature)
        {
            var item = Held;
            if (item == null) return;
            creature.Feed(item.Instance);
            Held = null;
            Destroy(item.gameObject);
        }

        // "Chop_Diced" → "chop diced". Used in HUD prompts.
        private static string FriendlyVariantName(ProcessVariant variant)
        {
            if (variant == null) return "process";
            string raw = variant.VariantName;
            if (string.IsNullOrEmpty(raw)) return "process";
            return raw.Replace('_', ' ').ToLowerInvariant();
        }

        // Slot took over parenting/physics via TryAccept — we just clear Held and
        // re-enable colliders so the item can be aimed at while sitting in the slot.
        private void ReleaseToSlot()
        {
            var item = Held;
            Held = null;
            item?.EnableCollider();
        }
    }
}
