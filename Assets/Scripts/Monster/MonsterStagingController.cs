using UnityEngine;

namespace NocturnalBreach.Monster
{
    /// <summary>
    /// Lightweight project-owned component that organizes and exposes reference Transform
    /// staging points for Monster Mutant 7 across the three threat thresholds:
    /// Window, Under Bed, and Door.
    /// Does NOT implement full AI, attacks, or game director logic.
    /// </summary>
    [SelectionBase]
    public class MonsterStagingController : MonoBehaviour
    {
        [Header("Window Staging Transforms (North Exterior)")]
        [Tooltip("Distant exterior stalk point (e.g. yard perimeter).")]
        [SerializeField] private Transform _windowDistant;

        [Tooltip("Intermediate approach point toward the window.")]
        [SerializeField] private Transform _windowApproach;

        [Tooltip("Proximity point near the window sill.")]
        [SerializeField] private Transform _windowNear;

        [Tooltip("Directly against the window glass.")]
        [SerializeField] private Transform _windowAtGlass;

        [Header("Under-Bed Staging Transforms (West Floor)")]
        [Tooltip("Dormant/sub-floor lurking position beneath the bed frame.")]
        [SerializeField] private Transform _underBedDormant;

        [Tooltip("Creeping forward position under the mattress edge.")]
        [SerializeField] private Transform _underBedCrawling;

        [Tooltip("Reaching upward position where monster arm reaches over the bed rail.")]
        [SerializeField] private Transform _underBedReaching;

        [Header("Door Staging Transforms (South Hallway Exterior)")]
        [Tooltip("Distant hallway stalk point.")]
        [SerializeField] private Transform _doorDistant;

        [Tooltip("Intermediate hallway approach point outside the bedroom door.")]
        [SerializeField] private Transform _doorApproach;

        [Tooltip("Directly in front of the door handle and slab outside.")]
        [SerializeField] private Transform _doorAtDoor;

        [Header("Monster Reference")]
        [SerializeField] private Transform _monsterTransform;

        // Public Accessors
        public Transform WindowDistant => _windowDistant;
        public Transform WindowApproach => _windowApproach;
        public Transform WindowNear => _windowNear;
        public Transform WindowAtGlass => _windowAtGlass;

        public Transform UnderBedDormant => _underBedDormant;
        public Transform UnderBedCrawling => _underBedCrawling;
        public Transform UnderBedReaching => _underBedReaching;

        public Transform DoorDistant => _doorDistant;
        public Transform DoorApproach => _doorApproach;
        public Transform DoorAtDoor => _doorAtDoor;

        public Transform MonsterTransform => _monsterTransform;

        /// <summary>
        /// Utility helper to place the monster directly at a designated staging point.
        /// </summary>
        public void PlaceMonsterAt(Transform targetPoint)
        {
            if (_monsterTransform != null && targetPoint != null)
            {
                _monsterTransform.position = targetPoint.position;
                _monsterTransform.rotation = targetPoint.rotation;
            }
        }
    }
}
