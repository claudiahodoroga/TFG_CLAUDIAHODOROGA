using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Galatea.Data;

namespace Galatea.Systems
{
    // Spawns IngredientInstances on demand. R-key binding is a placeholder for the
    // future AI-assistant UI request flow; round-robin order makes test playthroughs
    // predictable.
    public class IngredientBasket : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField, Min(0f)] private float spawnInterval = 0.4f;
        [SerializeField] private List<IngredientData> availableIngredients = new List<IngredientData>();
        [SerializeField] private InputAction spawnAction;

        private readonly Queue<IngredientData> _queue = new Queue<IngredientData>();
        private Coroutine _spawnRoutine;
        private int _roundRobinIndex = -1;

        private void Awake()
        {
            if (spawnAction.bindings.Count == 0)
                spawnAction.AddBinding("<Keyboard>/r");
        }

        private void OnEnable()
        {
            spawnAction.Enable();
            spawnAction.performed += OnSpawnPressed;
        }

        private void OnDisable()
        {
            spawnAction.performed -= OnSpawnPressed;
            spawnAction.Disable();
        }

        private void OnSpawnPressed(InputAction.CallbackContext ctx) => RequestNext();

        // Pre: availableIngredients has at least one entry.
        // Post: advances the round-robin index and enqueues the next ingredient.
        public void RequestNext()
        {
            if (availableIngredients == null || availableIngredients.Count == 0) return;
            _roundRobinIndex = (_roundRobinIndex + 1) % availableIngredients.Count;
            RequestIngredient(availableIngredients[_roundRobinIndex]);
        }

        // Pre: data is a non-null IngredientData.
        // Post: data is queued; the drain coroutine spawns one ingredient per
        // spawnInterval seconds until the queue empties.
        public void RequestIngredient(IngredientData data)
        {
            if (data == null) return;
            _queue.Enqueue(data);
            if (_spawnRoutine == null) _spawnRoutine = StartCoroutine(DrainQueue());
        }

        private IEnumerator DrainQueue()
        {
            while (_queue.Count > 0)
            {
                SpawnIngredient(_queue.Dequeue());
                yield return new WaitForSeconds(spawnInterval);
            }
            _spawnRoutine = null;
        }

        // Pre: data + data.Prefab + spawnPoint are all wired.
        // Post: a PickupItem is instantiated near spawnPoint and bound to a fresh
        // IngredientInstance; returns null on misconfiguration.
        public PickupItem SpawnIngredient(IngredientData data)
        {
            if (data == null || data.Prefab == null || spawnPoint == null) return null;

            Vector2 jitter = Random.insideUnitCircle * 0.05f;
            Vector3 spawnPos = spawnPoint.position + new Vector3(jitter.x, 0f, jitter.y);

            var go = Instantiate(data.Prefab, spawnPos, spawnPoint.rotation);
            var pickup = go.GetComponent<PickupItem>();
            if (pickup == null) return null;
            pickup.Initialize(data);
            return pickup;
        }
    }
}
