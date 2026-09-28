using System;
using UnityEngine;
using Oculus.Interaction;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Physical battery that can be grabbed in VR from the nightstand drawer
    /// and inserted into the flashlight to recharge its battery level.
    /// </summary>
    [SelectionBase]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class PhysicalBattery : MonoBehaviour
    {
        [Header("Recharge Configuration")]
        [Tooltip("Amount of battery replenished (0 to 100).")]
        [SerializeField] private float _chargeAmount = 100f;

        [Tooltip("Proximity radius to flashlight to trigger snap insertion.")]
        [SerializeField] private float _insertionRadius = 0.16f;

        [Header("Audio & FX")]
        [SerializeField] private AudioClip _insertClip;

        private Rigidbody _rigidbody;
        private Grabbable _grabbable;
        private Collider _collider;
        private Collider _shelfCol;
        private Collider _nightstandCol;
        private bool _isConsumed = false;
        private bool _isRestingInDrawer = false;
        private bool _hasBeenGrabbed = false;

        public float ChargeAmount => _chargeAmount;
        public bool IsConsumed => _isConsumed;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _grabbable = GetComponent<Grabbable>();
            _collider = GetComponent<Collider>();

            IgnorePlayerKickCollision();

            if (_rigidbody != null)
            {
                _rigidbody.mass = 0.05f;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // If parented to a drawer/shelf, remain kinematic and stable while resting
                if (transform.parent != null)
                {
                    _isRestingInDrawer = true;
                    _rigidbody.isKinematic = true;
                    _rigidbody.useGravity = false;

                    // Prevent overlap depenetration with shelf / nightstand colliders on startup
                    _shelfCol = transform.parent.GetComponent<Collider>();
                    if (_shelfCol != null && _collider != null)
                    {
                        Physics.IgnoreCollision(_collider, _shelfCol, true);
                    }
                    _nightstandCol = transform.parent.parent != null ? transform.parent.parent.GetComponent<Collider>() : null;
                    if (_nightstandCol != null && _collider != null)
                    {
                        Physics.IgnoreCollision(_collider, _nightstandCol, true);
                    }
                }
                else
                {
                    _rigidbody.useGravity = true;
                    _rigidbody.isKinematic = false;
                }
            }
        }

        private void Start()
        {
            IgnorePlayerKickCollision();
        }

        private void IgnorePlayerKickCollision()
        {
            // Specifically exclude PhysicalBattery colliders from PlayerPhysicalKick and character body pushing.
            // Finds the player capsule collider and sets Physics.IgnoreCollision so the player walking
            // through or near the battery never imparts artificial kick forces or physics displacement,
            // while preserving full normal collisions with the floor, furniture, walls, and flashlight.
            var player = GameObject.Find("[BuildingBlock] Camera Rig/OVRComprehensiveInteractionRig/Locomotor/PlayerController");
            if (player != null)
            {
                var playerCols = player.GetComponentsInChildren<Collider>(true);
                var myCols = GetComponentsInChildren<Collider>(true);
                foreach (var pCol in playerCols)
                {
                    if (pCol == null) continue;
                    foreach (var myCol in myCols)
                    {
                        if (myCol == null) continue;
                        Physics.IgnoreCollision(pCol, myCol, true);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            RestoreCollision();
        }

        private void RestoreCollision()
        {
            if (_shelfCol != null && _collider != null)
            {
                Physics.IgnoreCollision(_collider, _shelfCol, false);
            }
            if (_nightstandCol != null && _collider != null)
            {
                Physics.IgnoreCollision(_collider, _nightstandCol, false);
            }
        }

        private void Update()
        {
            if (_isConsumed) return;

            // When player grabs the battery
            if (_grabbable != null && _grabbable.SelectingPointsCount > 0)
            {
                if (_isRestingInDrawer)
                {
                    _isRestingInDrawer = false;

                    // Restore physical collision with shelf and nightstand
                    if (_shelfCol != null && _collider != null)
                    {
                        Physics.IgnoreCollision(_collider, _shelfCol, false);
                    }
                    if (_nightstandCol != null && _collider != null)
                    {
                        Physics.IgnoreCollision(_collider, _nightstandCol, false);
                    }

                    if (transform.parent != null)
                    {
                        transform.SetParent(null, true);
                    }
                }
                _hasBeenGrabbed = true;
            }
            // Once grabbed and released, enable full dynamic physics
            else if (_hasBeenGrabbed)
            {
                if (_rigidbody != null && _rigidbody.isKinematic)
                {
                    _rigidbody.isKinematic = false;
                    _rigidbody.useGravity = true;
                }
            }

            // Check proximity to the active flashlight in the scene
            var flashlight = PhysicalFlashlight.Instance;
            if (flashlight == null) flashlight = FindAnyObjectByType<PhysicalFlashlight>();
            if (flashlight == null) return;

            // Accurate geometric detection: check distance to closest point on flashlight collider surface
            var flCol = flashlight.GetComponent<Collider>();
            float dist = (flCol != null)
                ? Vector3.Distance(transform.position, flCol.ClosestPoint(transform.position))
                : Vector3.Distance(transform.position, flashlight.transform.position);

            if (dist <= _insertionRadius)
            {
                TryInsert(flashlight);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            CheckFlashlightContact(collision.gameObject);
        }

        private void OnCollisionStay(Collision collision)
        {
            CheckFlashlightContact(collision.gameObject);
        }

        private void CheckFlashlightContact(GameObject hitObj)
        {
            if (_isConsumed || hitObj == null) return;
            var flashlight = PhysicalFlashlight.Instance;
            if (flashlight == null) flashlight = FindAnyObjectByType<PhysicalFlashlight>();
            if (flashlight == null) return;

            if (hitObj == flashlight.gameObject || hitObj.transform.IsChildOf(flashlight.transform))
            {
                TryInsert(flashlight);
            }
        }

        private void TryInsert(PhysicalFlashlight flashlight)
        {
            if (_isConsumed || flashlight == null) return;

            // Recharges if flashlight battery is below 99% or if in scene initial overcharge state (>100%)
            if (flashlight.CurrentBattery < 99f || flashlight.CurrentBattery > 100f)
            {
                InsertIntoFlashlight(flashlight);
            }
        }

        private void InsertIntoFlashlight(PhysicalFlashlight flashlight)
        {
            if (_isConsumed) return;
            _isConsumed = true;

            flashlight.Recharge(_chargeAmount);

            if (_insertClip != null)
            {
                AudioSource.PlayClipAtPoint(_insertClip, transform.position, 1.0f);
            }

            // Destroy the battery object after insertion
            Destroy(gameObject, 0.05f);
        }
    }
}
