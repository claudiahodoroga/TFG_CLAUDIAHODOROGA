using UnityEngine;
using Galatea.Player;

namespace Galatea.Systems
{
    // Holds a single DishVessel. Starts empty; the player presses E while
    // empty-handed and aimed at the station to spawn the default plate.
    public class PlatingStation : MonoBehaviour
    {
        [SerializeField] private GameObject flatPlatePrefab;
        [SerializeField] private Transform snapPoint;

        private DishVessel _currentDish;

        public bool HasDish => _currentDish != null;

        // Pre: no current dish; flatPlatePrefab and snapPoint are wired.
        // Post: a kinematic DishVessel sits on snapPoint and HasDish == true.
        public void SpawnDish()
        {
            if (HasDish) return;
            if (flatPlatePrefab == null || snapPoint == null) return;

            var go = Instantiate(flatPlatePrefab, snapPoint.position, snapPoint.rotation);
            go.transform.SetParent(snapPoint);
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            _currentDish = go.GetComponent<DishVessel>();
            if (_currentDish == null) return;

            // playSound:false — PlayPlateSpawned below owns the audio cue for this
            // hand-off, so we suppress PickupItem's ingredientPickedUp clip.
            var pickupItem = go.GetComponent<PickupItem>();
            if (pickupItem != null) pickupItem.OnPickedUp(playSound: false);

            SoundManager.PlayPlateSpawned(snapPoint.position);
        }

        // Called by InteractionSystem after picking the dish off the station so
        // HasDish returns false and a new dish can be spawned.
        public void NotifyDishPickedUp() => _currentDish = null;
    }
}
