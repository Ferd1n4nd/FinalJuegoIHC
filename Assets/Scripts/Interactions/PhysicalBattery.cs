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
        private bool _isConsumed = false;

        public float ChargeAmount => _chargeAmount;
        public bool IsConsumed => _isConsumed;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _grabbable = GetComponent<Grabbable>();
        }

        private void Update()
        {
            if (_isConsumed) return;

            // Check distance to the active flashlight in the scene
            var flashlight = PhysicalFlashlight.Instance;
            if (flashlight == null) return;

            float dist = Vector3.Distance(transform.position, flashlight.transform.position);
            if (dist <= _insertionRadius)
            {
                // If flashlight needs recharge (or player is placing battery near flashlight)
                if (flashlight.CurrentBattery < 99f)
                {
                    InsertIntoFlashlight(flashlight);
                }
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
