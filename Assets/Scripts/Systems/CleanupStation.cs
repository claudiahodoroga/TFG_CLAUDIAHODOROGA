using System.Collections;
using UnityEngine;
using Galatea.Player;

namespace Galatea.Systems
{
    // Single-slot drop target for dirty DishVessels. Once accepted, the vessel is
    // committed to destruction; the slot frees itself when the coroutine completes.
    public class CleanupStation : MonoBehaviour, IStationSlot
    {
        [SerializeField] private Transform snapPoint;
        [SerializeField] private float destroyDelay = 1.5f;

        private bool _isOccupied;

        bool IStationSlot.IsOccupied => _isOccupied;

        // Pre: item carries a DishVessel marked dirty (IsDirty == true).
        // Post: vessel is parented to snapPoint, marked kinematic, and queued for
        // destruction after destroyDelay; returns false for any other input.
        bool IStationSlot.TryAccept(PickupItem item)
        {
            if (item == null) return false;

            var vessel = item.GetComponent<DishVessel>();
            if (vessel == null || !vessel.IsDirty) return false;

            if (snapPoint != null)
            {
                item.transform.SetParent(snapPoint, worldPositionStays: false);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                item.transform.localScale = Vector3.one;
            }
            item.OnPickedUp();

            _isOccupied = true;
            StartCoroutine(DestroyAfterDelay(item));
            return true;
        }

        void IStationSlot.Release() { }

        // InteractionSystem.ReleaseToSlot calls EnableCollider() right after TryAccept
        // returns; that re-enabled, kinematic dish was depenetrating the player's
        // CharacterController, popping them up. Yield one frame so the re-enable
        // runs, then disable again for the rest of the destroy delay.
        private IEnumerator DestroyAfterDelay(PickupItem item)
        {
            GameObject obj = item != null ? item.gameObject : null;
            yield return null;
            if (item != null) item.DisableColliderForHolding();

            yield return new WaitForSeconds(destroyDelay);
            if (obj != null) Destroy(obj);
            _isOccupied = false;
        }
    }
}
