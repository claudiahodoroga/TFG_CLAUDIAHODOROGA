using System.Collections;
using UnityEngine;
using Galatea.Data;
using Galatea.Player;

namespace Galatea.Systems
{
    // One cooking surface on a CookingStation. Each slot owns its own ProcessVariant,
    // so a single station can mix process types (e.g. one sauté slot + one boil slot).
    //
    // Pre: an IngredientInstance has been accepted via TryAccept.
    // Post: StartProcess kicks a coroutine that mutates the ingredient's profile and
    // either its Form (discrete) or its CookState + cookingProgress (continuous).
    public class StationSlot : MonoBehaviour, IStationSlot
    {
        [SerializeField] private CookingStation station;
        [SerializeField] private Transform snapPoint;

        [Header("Slot Behavior")]
        [Tooltip("ProcessVariant this slot runs. If empty, the slot accepts ingredients but cannot cook.")]
        [SerializeField] private ProcessVariant variant;

        [Tooltip("ContinuousVariant only. 0 = cold, 1 = max heat.")]
        [SerializeField, Range(0f, 1f)] private float heatLevel = 0.5f;

        [Tooltip("DiscreteVariant only. Processing delay before the result is applied.")]
        [SerializeField, Min(0f)] private float minProcessTime = 0.5f;

        [Tooltip("Continuous slots only. Leadin between the start one-shot and the cooking loop coming in.")]
        [SerializeField, Min(0f)] private float cookLoopLeadin = 1f;

        private PickupItem _occupant;
        private Coroutine _processRoutine;
        private Coroutine _loopLeadinRoutine;

        public CookingStation Station => station;
        public ProcessVariant Variant => variant;
        public IngredientInstance OccupantInstance => _occupant?.Instance;
        public bool IsProcessing { get; private set; }

        public float HeatLevel
        {
            get => heatLevel;
            set => heatLevel = Mathf.Clamp01(value);
        }

        bool IStationSlot.IsOccupied => _occupant != null;
        public bool IsOccupied => _occupant != null;

        private void Awake()
        {
            if (station == null) station = GetComponentInParent<CookingStation>();
        }

        bool IStationSlot.TryAccept(PickupItem item)
        {
            if (_occupant != null) return false;
            if (item == null || item.Instance == null) return false;

            _occupant = item;
            if (snapPoint != null)
            {
                item.transform.SetParent(snapPoint, worldPositionStays: false);
                item.transform.localScale    = Vector3.one;
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
            }
            item.OnPickedUp();
            item.CurrentSlot = this;
            return true;
        }

        void IStationSlot.Release()
        {
            if (_occupant == null) return;

            StopProcess();
            _occupant.OnDropped();
            _occupant.transform.SetParent(null);
            _occupant.transform.localScale = Vector3.one;
            _occupant.CurrentSlot = null;
            _occupant = null;
        }

        // Pre: slot has an occupant with a valid IngredientInstance and a variant.
        // Post: returns true if a process coroutine actually started.
        public bool StartProcess()
        {
            if (_processRoutine != null) return false;
            if (_occupant == null || _occupant.Instance == null) return false;
            if (variant == null) return false;

            switch (variant)
            {
                case DiscreteVariant discrete:
                    IsProcessing = true;
                    _processRoutine = StartCoroutine(RunDiscrete(discrete));
                    return true;

                case ContinuousVariant continuous:
                    IsProcessing = true;
                    SoundManager.PlaySlotProcessOn(transform.position);
                    _loopLeadinRoutine = StartCoroutine(StartCookingLoopAfterLeadin(continuous.Category));
                    _processRoutine = StartCoroutine(RunContinuous(continuous));
                    return true;

                default:
                    return false;
            }
        }

        public void StopProcess()
        {
            bool wasContinuous = variant is ContinuousVariant;

            if (_loopLeadinRoutine != null)
            {
                StopCoroutine(_loopLeadinRoutine);
                _loopLeadinRoutine = null;
            }

            if (_processRoutine == null)
            {
                IsProcessing = false;
                if (wasContinuous)
                {
                    SoundManager.PlaySlotProcessOff(transform.position);
                    SoundManager.StopCookingLoop(GetInstanceID());
                }
                return;
            }
            StopCoroutine(_processRoutine);
            _processRoutine = null;
            IsProcessing = false;

            if (wasContinuous)
            {
                // Off one-shot first so the player hears the closure before the loop drops out.
                SoundManager.PlaySlotProcessOff(transform.position);
                SoundManager.StopCookingLoop(GetInstanceID());
            }
        }

        private IEnumerator StartCookingLoopAfterLeadin(ProcessCategory category)
        {
            if (cookLoopLeadin > 0f) yield return new WaitForSeconds(cookLoopLeadin);
            SoundManager.StartCookingLoop(GetInstanceID(), category);
            _loopLeadinRoutine = null;
        }

        private void OnDisable()
        {
            if (_loopLeadinRoutine != null)
            {
                StopCoroutine(_loopLeadinRoutine);
                _loopLeadinRoutine = null;
            }
            SoundManager.StopCookingLoop(GetInstanceID());
        }

        // Pre: occupant has a valid IngredientInstance with a source.
        // Post: applies the ingredient's cut response and sets its Form.
        private IEnumerator RunDiscrete(DiscreteVariant discrete)
        {
            yield return new WaitForSeconds(minProcessTime);

            var ingredient = _occupant?.Instance;
            if (ingredient != null && ingredient.source != null)
            {
                var source = ingredient.source;
                ingredient.currentProfile = FlavorCalculator.ApplyDiscreteModifier(
                    ingredient.currentProfile, source.CutMultiplier, source.CutModifier);
                ingredient.Form = discrete.ResultForm;

                SoundManager.PlayIngredientChopped(transform.position);
            }

            _processRoutine = null;
            IsProcessing = false;
        }

        // Pre: occupant has a valid IngredientInstance with a source.
        // Post: each frame advances cookingProgress and lerps CookState through the
        // variant's three stages while accumulating per-tick flavor deltas.
        private IEnumerator RunContinuous(ContinuousVariant continuous)
        {
            while (true)
            {
                float dt = Time.deltaTime;

                var ingredient = _occupant?.Instance;
                if (ingredient == null || ingredient.source == null)
                {
                    yield return null;
                    continue;
                }

                if (ingredient.cookingProgress < 1f)
                {
                    float effectiveDt = dt * heatLevel;
                    FlavorProfile delta = GetContinuousDelta(continuous, ingredient);
                    ingredient.currentProfile = FlavorCalculator.ApplyContinuousDelta(
                        ingredient.currentProfile, delta, effectiveDt);

                    ingredient.cookingProgress = Mathf.Clamp01(
                        ingredient.cookingProgress + heatLevel * continuous.HeatLevelMultiplier * dt);

                    CookState next = ResolveStage(continuous, ingredient.cookingProgress, ingredient.CookState);
                    if (next != ingredient.CookState) ingredient.CookState = next;
                }

                yield return null;
            }
        }

        private static FlavorProfile GetContinuousDelta(ContinuousVariant variant, IngredientInstance ingredient)
        {
            bool isStage3 = ingredient.CookState == variant.Stage3CookState;
            bool isStage2 = ingredient.CookState == variant.Stage2CookState;
            var source = ingredient.source;

            switch (variant.Category)
            {
                case ProcessCategory.WetHeat:
                    return isStage3 ? source.WetHeatStage3Delta
                         : isStage2 ? source.WetHeatStage2Delta
                                    : source.WetHeatStage1Delta;
                default:
                    return isStage3 ? source.DryHeatStage3Delta
                         : isStage2 ? source.DryHeatStage2Delta
                                    : source.DryHeatStage1Delta;
            }
        }

        // Don't downgrade once past stage 2/3 — heat dropping mid-cook shouldn't
        // un-caramelize an ingredient.
        private static CookState ResolveStage(ContinuousVariant variant, float progress, CookState current)
        {
            if (progress >= variant.Stage3Threshold) return variant.Stage3CookState;
            if (progress >= variant.Stage2Threshold) return variant.Stage2CookState;
            if (current == variant.Stage3CookState || current == variant.Stage2CookState)
                return current;
            return variant.ResultCookState;
        }
    }
}
