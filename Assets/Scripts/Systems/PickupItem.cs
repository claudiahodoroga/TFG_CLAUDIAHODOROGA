using UnityEngine;
using Galatea.Data;
using Galatea.Player;

namespace Galatea.Systems
{
    // Carriable scene object holding a runtime IngredientInstance. Swaps its visual
    // mesh whenever the ingredient's Form or CookState changes, and toggles its
    // Rigidbody between kinematic (held / in a slot) and dynamic (loose in world).
    //
    // Visuals are mesh-per-(Form, CookState): each cooked variant is its own authored
    // mesh on IngredientData.meshOverrides — there is no runtime color overlay.
    public class PickupItem : MonoBehaviour
    {
        [SerializeField] private IngredientData startingData;
        [SerializeField] private MeshFilter visualMeshFilter;
        [SerializeField] private Rigidbody rb;

        public IngredientInstance Instance { get; private set; }
        public IStationSlot CurrentSlot { get; set; }

        // Prefab-authored mesh, captured on Awake. Used as the fallback when no
        // override exists for the current (Form, CookState).
        private Mesh _initialMesh;

        private void Awake()
        {
            if (visualMeshFilter == null)
                visualMeshFilter = GetComponentInChildren<MeshFilter>();
            if (visualMeshFilter != null) _initialMesh = visualMeshFilter.sharedMesh;

            if (Instance == null && startingData != null)
            {
                BindInstance(new IngredientInstance(startingData));
                ApplyMesh(Instance.Form, Instance.CookState);
            }
        }

        // Pre: data is a non-null IngredientData asset.
        // Post: a fresh IngredientInstance is bound and the initial mesh is applied.
        public void Initialize(IngredientData data)
        {
            BindInstance(new IngredientInstance(data));
            ApplyMesh(Instance.Form, Instance.CookState);
            SoundManager.PlayIngredientSpawned(transform.position);
        }

        private void OnDestroy() => BindInstance(null);

        private void BindInstance(IngredientInstance next)
        {
            if (Instance != null)
            {
                Instance.OnFormChanged      -= HandleFormChanged;
                Instance.OnCookStateChanged -= HandleCookStateChanged;
            }
            Instance = next;
            if (Instance != null)
            {
                Instance.OnFormChanged      += HandleFormChanged;
                Instance.OnCookStateChanged += HandleCookStateChanged;
            }
        }

        private void HandleFormChanged(Form form)        => ApplyMesh(form, Instance?.CookState ?? CookState.Raw);
        private void HandleCookStateChanged(CookState s) => ApplyMesh(Instance?.Form ?? Form.Whole, s);

        public void OnPickedUp() => OnPickedUp(playSound: true);

        // playSound:false is used when something else owns the audio cue for this
        // hand-off — e.g. PlatingStation spawns the dish kinematic but plays its
        // own plateSpawned clip instead of the ingredient pickup clip.
        public void OnPickedUp(bool playSound)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            if (playSound) SoundManager.PlayIngredientPickedUp(transform.position);
        }

        public void OnDropped()
        {
            if (rb != null) rb.isKinematic = false;
            SoundManager.PlayIngredientDropped(transform.position);
        }

        public void DisableColliderForHolding()
        {
            foreach (var col in GetComponentsInChildren<Collider>(includeInactive: true))
                col.enabled = false;
        }

        public void EnableCollider()
        {
            foreach (var col in GetComponentsInChildren<Collider>(includeInactive: true))
                col.enabled = true;
        }

        // Pre: visualMeshFilter is wired (or auto-resolved) and Instance.source is set.
        // Post: the active mesh matches the override for (form, cookState), or falls
        // back to the prefab-authored mesh when no override is registered.
        public void ApplyMesh(Form form, CookState cookState)
        {
            if (visualMeshFilter == null) return;
            if (Instance == null || Instance.source == null) return;

            Mesh overrideMesh = Instance.source.GetMesh(form, cookState);
            Mesh target = overrideMesh != null ? overrideMesh : _initialMesh;
            if (target != null && visualMeshFilter.sharedMesh != target)
                visualMeshFilter.sharedMesh = target;
        }
    }
}
