using System;
using UnityEngine;

namespace NocturnalBreach.Audio
{
    /// <summary>
    /// Detects player horizontal movement (both room-scale physical walking and
    /// joystick slide locomotion) and plays subtle wooden floor footsteps.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class PlayerFootstepsAudio : MonoBehaviour
    {
        [Header("Tracking Target")]
        [SerializeField] private Transform _headTransform;

        [Header("Footstep Parameters")]
        [Tooltip("Horizontal distance in meters traveled before triggering a footstep.")]
        [SerializeField] private float _stepDistance = 0.60f;

        [Tooltip("Minimum seconds between consecutive footsteps.")]
        [SerializeField] private float _minStepInterval = 0.35f;

        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.45f;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip[] _footstepClips;

        private AudioSource _audioSource;
        private Vector3 _lastTrackedPosition;
        private float _distanceAccumulator;
        private float _lastStepTime;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.spatialBlend = 1.0f;
            _audioSource.minDistance = 0.2f;
            _audioSource.maxDistance = 5.0f;
            _audioSource.playOnAwake = false;

            if (_headTransform == null)
            {
                var centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null) _headTransform = centerEye.transform;
                else _headTransform = transform;
            }

            _lastTrackedPosition = _headTransform.position;
        }

        private void Update()
        {
            if (_headTransform == null) return;

            Vector3 currentPos = _headTransform.position;
            // Measure horizontal XZ displacement only
            Vector3 delta = new Vector3(currentPos.x - _lastTrackedPosition.x, 0f, currentPos.z - _lastTrackedPosition.z);
            float dist = delta.magnitude;

            _lastTrackedPosition = currentPos;

            // Filter out slight tracking drift/jitter (< 1 mm/frame)
            if (dist < 0.001f) return;

            _distanceAccumulator += dist;

            if (_distanceAccumulator >= _stepDistance && (Time.time - _lastStepTime) >= _minStepInterval)
            {
                _distanceAccumulator = 0f;
                _lastStepTime = Time.time;
                PlayFootstep();
            }
        }

        private void PlayFootstep()
        {
            if (_footstepClips == null || _footstepClips.Length == 0) return;

            int index = UnityEngine.Random.Range(0, _footstepClips.Length);
            AudioClip clip = _footstepClips[index];

            if (clip != null && _audioSource != null)
            {
                _audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                _audioSource.PlayOneShot(clip, _volume);
            }
        }
    }
}
