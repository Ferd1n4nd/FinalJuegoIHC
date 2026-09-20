using System;
using UnityEngine;
using Oculus.Interaction;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Component for physical VR sliding drawers.
    /// Operates on local Z translation using Meta XR Interaction SDK (Grabbable + OneGrabTranslateTransformer).
    /// Prevents penetration, enforces travel limits, and triggers audio/haptic events.
    /// </summary>
    [SelectionBase]
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicalVRDrawer : MonoBehaviour
    {
        [Header("Slide Limits (Local Z)")]
        [Tooltip("Local Z position when fully closed.")]
        [SerializeField] private float _closedLocalZ = 0.03f;

        [Tooltip("Local Z position when fully open (pulled outward).")]
        [SerializeField] private float _openLocalZ = 0.28f;

        [Tooltip("Tolerance in meters to consider drawer fully closed.")]
        [SerializeField] private float _closeTolerance = 0.02f;

        [Header("State")]
        [SerializeField] private bool _isClosed = true;
        [SerializeField] private float _normalizedOpen = 0.0f;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _slideOpenClip;
        [SerializeField] private AudioClip _slideCloseClip;

        public event Action OnDrawerOpened;
        public event Action OnDrawerClosed;
        public event Action<float> OnDrawerMoved;

        public bool IsClosed => _isClosed;
        public float NormalizedOpen => _normalizedOpen;
        public float CurrentLocalZ => transform.localPosition.z;

        private float _lastReportedZ;
        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
            }

            _lastReportedZ = transform.localPosition.z;
            UpdateState(true);
        }

        private void Update()
        {
            ClampTransformToAxis();
            UpdateState(false);
        }

        /// <summary>
        /// Enforces strict 1D translation along local Z axis within [closedLocalZ, openLocalZ],
        /// locking local X and Y to prevent the drawer from popping out of the nightstand.
        /// </summary>
        private void ClampTransformToAxis()
        {
            Vector3 localPos = transform.localPosition;
            float clampedZ = Mathf.Clamp(localPos.z, Mathf.Min(_closedLocalZ, _openLocalZ), Mathf.Max(_closedLocalZ, _openLocalZ));
            
            // Keep local X and Y locked to their intended alignment
            transform.localPosition = new Vector3(0f, localPos.y, clampedZ);
            transform.localRotation = Quaternion.identity;
        }

        private void UpdateState(bool initial)
        {
            float curZ = transform.localPosition.z;
            float travelRange = Mathf.Abs(_openLocalZ - _closedLocalZ);

            if (travelRange > 0.001f)
            {
                _normalizedOpen = Mathf.Clamp01(Mathf.Abs(curZ - _closedLocalZ) / travelRange);
            }

            if (Mathf.Abs(curZ - _lastReportedZ) > 0.01f)
            {
                _lastReportedZ = curZ;
                OnDrawerMoved?.Invoke(_normalizedOpen);
            }

            bool wasClosed = _isClosed;
            _isClosed = Mathf.Abs(curZ - _closedLocalZ) <= _closeTolerance;

            if (!initial)
            {
                if (_isClosed && !wasClosed)
                {
                    OnDrawerClosed?.Invoke();
                    PlayAudio(_slideCloseClip);
                }
                else if (!_isClosed && wasClosed)
                {
                    OnDrawerOpened?.Invoke();
                    PlayAudio(_slideOpenClip);
                }
            }
        }

        private void PlayAudio(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip, 0.7f);
            }
        }
    }
}
