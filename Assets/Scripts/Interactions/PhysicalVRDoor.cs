using UnityEngine;
using Oculus.Interaction;
using NocturnalBreach.Haptics;
using NocturnalBreach.Audio;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Physical VR Door component that works alongside Meta XR Interaction SDK
    /// (Grabbable + OneGrabRotateTransformer) on SimpleDoor_MainDoor_LOD0.
    /// Tracks rotation angle, closed/latched state, and syncs LODs.
    /// Enforces strict physical inward rotation limits ([-90, 0] parallel to wall),
    /// locked resistance + haptic & audio feedback when monster is not attacking,
    /// and full defense/push interaction during active monster assault.
    /// </summary>
    [SelectionBase]
    public class PhysicalVRDoor : MonoBehaviour
    {
        [Header("Rotation Limits (Around Local Y)")]
        [Tooltip("Local Y angle when door is fully closed.")]
        [SerializeField] private float _closedAngle = 0.0f;

        [Tooltip("Local Y angle when door is fully open inward (-90 degrees, parallel to wall).")]
        [SerializeField] private float _openAngle = -90.0f;

        [Tooltip("Tolerance in degrees for considering the door latched closed.")]
        [SerializeField] private float _latchAngleTolerance = 3.0f;

        [Header("Locked Door Rule")]
        [Tooltip("Maximum subtle physical displacement in degrees when player pulls locked door.")]
        [SerializeField] private float _maxLockedResistanceAngle = -2.2f;

        [Tooltip("Cooldown between rattle audio and haptic pulses when attempting to open locked door.")]
        [SerializeField] private float _lockedFeedbackCooldown = 0.45f;

        [Tooltip("SFX played when player attempts to force open the locked door.")]
        [SerializeField] private AudioClip _lockedDoorAudioClip;

        [SerializeField] private AudioSource _doorAudioSource;

        [Header("LOD Syncing (Optional)")]
        [SerializeField] private Transform _lod1DoorTransform;
        [SerializeField] private Transform _lod2DoorTransform;

        [Header("State")]
        [SerializeField] private bool _isClosed = true;
        [SerializeField] private float _currentAngle = 0.0f;
        [SerializeField] private float _normalizedOpen = 0.0f;
        [SerializeField] private bool _isUnderMonsterAttack = false;

        public event System.Action OnDoorClosed;
        public event System.Action OnDoorOpened;
        public event System.Action<float> OnDoorMoved;

        public bool IsClosed => _isClosed;
        public float CurrentAngle => _currentAngle;
        public float NormalizedOpen => _normalizedOpen;
        public bool IsUnderMonsterAttack => _isUnderMonsterAttack;

        public bool IsGrabbed => (_grabbable != null && _grabbable.SelectingPointsCount > 0) ||
                                 (_knobGrabbable != null && _knobGrabbable.SelectingPointsCount > 0);

        private float _lastReportedAngle;
        private Grabbable _grabbable;
        private Grabbable _knobGrabbable;

        private HorrorHapticsManager _hapticsManager;
        private HorrorAudioManager _horrorAudioManager;
        private Transform _leftHandTransform;
        private Transform _rightHandTransform;
        private float _lastLockedFeedbackTime = -10f;

        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            var knob = transform.Find("Door_Wood/Door_Knob");
            if (knob != null)
            {
                _knobGrabbable = knob.GetComponent<Grabbable>();
            }

            if (_doorAudioSource == null) _doorAudioSource = GetComponent<AudioSource>();
            if (_hapticsManager == null) _hapticsManager = FindAnyObjectByType<HorrorHapticsManager>();
            if (_horrorAudioManager == null) _horrorAudioManager = FindAnyObjectByType<HorrorAudioManager>();

            var left = GameObject.Find("LeftHandAnchor") ?? GameObject.Find("LeftController") ?? GameObject.Find("LeftHand");
            if (left != null) _leftHandTransform = left.transform;
            var right = GameObject.Find("RightHandAnchor") ?? GameObject.Find("RightHand") ?? GameObject.Find("RightController");
            if (right != null) _rightHandTransform = right.transform;

            _lastReportedAngle = transform.localEulerAngles.y;
            UpdateState();
        }

        private float _targetMonsterPushAngle = 0f;
        private bool _hasTargetPushAngle = false;

        private void Update()
        {
            UpdateMonsterPushSmoothing();
            EnforceRotationConstraints();
            UpdateState();
        }

        private void LateUpdate()
        {
            EnforceRotationConstraints();
        }

        private void EnforceRotationConstraints()
        {
            float yRot = transform.localEulerAngles.y;
            if (yRot > 180f) yRot -= 360f;

            if (!_isUnderMonsterAttack)
            {
                // Door is strictly LOCKED when monster is not assaulting
                if (IsGrabbed)
                {
                    // Player is actively attempting to move / open the locked door
                    if (yRot < -0.3f || yRot > 0.3f)
                    {
                        TriggerLockedFeedback();
                    }

                    // Constrain to tiny resistance window (never opening outward > 0, never past -2.2 inward)
                    float clamped = Mathf.Clamp(yRot, _maxLockedResistanceAngle, 0.0f);
                    // Spring back towards 0
                    float returnAngle = Mathf.MoveTowards(clamped, _closedAngle, 14.0f * Time.deltaTime);
                    ForceSetAngleInternal(returnAngle);
                }
                else
                {
                    // Not grabbed: spring firmly back to 0 (closed)
                    if (Mathf.Abs(yRot - _closedAngle) > 0.01f)
                    {
                        float returnAngle = Mathf.MoveTowards(yRot, _closedAngle, 25.0f * Time.deltaTime);
                        ForceSetAngleInternal(returnAngle);
                    }
                }
            }
            else
            {
                // Active monster assault: free physical rotation strictly bounded between 0 and -90
                // (Cannot open outward into hallway > 0, cannot penetrate wall < -90)
                float minA = Mathf.Min(_closedAngle, _openAngle); // -90
                float maxA = Mathf.Max(_closedAngle, _openAngle); // 0
                float clamped = Mathf.Clamp(yRot, minA, maxA);
                if (Mathf.Abs(clamped - yRot) > 0.01f)
                {
                    ForceSetAngleInternal(clamped);
                }
            }
        }

        private void TriggerLockedFeedback()
        {
            if (Time.time - _lastLockedFeedbackTime < _lockedFeedbackCooldown) return;
            _lastLockedFeedbackTime = Time.time;

            // 1. Haptics on the grabbing hand
            HapticTargetHand hand = DetermineGrabbingHand();
            if (_hapticsManager != null)
            {
                _hapticsManager.TriggerLockedDoorResistance(hand);
            }

            // 2. Audio: Door_Slam_sound_effect_SFX
            if (_horrorAudioManager != null)
            {
                _horrorAudioManager.PlayLockedDoorAttempt();
            }
            else if (_doorAudioSource != null && _lockedDoorAudioClip != null)
            {
                _doorAudioSource.PlayOneShot(_lockedDoorAudioClip, 0.90f);
            }
        }

        private HapticTargetHand DetermineGrabbingHand()
        {
            Vector3 grabPos = transform.position;
            var knob = transform.Find("Door_Wood/Door_Knob");
            if (knob != null) grabPos = knob.position;

            float distL = float.MaxValue;
            float distR = float.MaxValue;

            if (_leftHandTransform != null) distL = Vector3.Distance(_leftHandTransform.position, grabPos);
            if (_rightHandTransform != null) distR = Vector3.Distance(_rightHandTransform.position, grabPos);

            if (distL < 1.0f && distL < distR - 0.05f) return HapticTargetHand.Left;
            if (distR < 1.0f && distR < distL - 0.05f) return HapticTargetHand.Right;

            return HapticTargetHand.Both;
        }

        private void UpdateMonsterPushSmoothing()
        {
            if (!_hasTargetPushAngle) return;

            // Smoothly interpolate towards the target angle when monster is forcing the door
            if (Mathf.Abs(_currentAngle - _targetMonsterPushAngle) > 0.05f)
            {
                // Smooth interpolation rate: fast enough to be responsive, smooth enough to eliminate jumps
                float smoothSpeed = IsGrabbed ? 14f : 20f;
                float newAngle = Mathf.MoveTowards(_currentAngle, _targetMonsterPushAngle, smoothSpeed * Time.deltaTime);
                ForceSetAngleInternal(newAngle);
            }
            else
            {
                ForceSetAngleInternal(_targetMonsterPushAngle);
                _hasTargetPushAngle = false;
            }
        }

        private void UpdateState()
        {
            float yRot = transform.localEulerAngles.y;
            // Normalize angle to -180 .. 180 range
            if (yRot > 180f) yRot -= 360f;

            if (Mathf.Abs(yRot - _lastReportedAngle) > 0.5f)
            {
                _lastReportedAngle = yRot;
                OnDoorMoved?.Invoke(yRot);
            }

            _currentAngle = yRot;
            float totalRange = Mathf.Abs(_openAngle - _closedAngle);
            if (totalRange > 0.001f)
            {
                _normalizedOpen = Mathf.Clamp01(Mathf.Abs(yRot - _closedAngle) / totalRange);
            }

            bool wasClosed = _isClosed;
            _isClosed = Mathf.Abs(yRot - _closedAngle) <= _latchAngleTolerance;

            if (_isClosed && !wasClosed)
            {
                OnDoorClosed?.Invoke();
            }
            else if (!_isClosed && wasClosed)
            {
                OnDoorOpened?.Invoke();
            }

            // Sync LODs if assigned
            if (_lod1DoorTransform != null) _lod1DoorTransform.localRotation = transform.localRotation;
            if (_lod2DoorTransform != null) _lod2DoorTransform.localRotation = transform.localRotation;
        }

        private void ForceSetAngleInternal(float angle)
        {
            Vector3 euler = transform.localEulerAngles;
            euler.y = angle;
            transform.localEulerAngles = euler;
        }

        public void ForceSetAngle(float angle)
        {
            ForceSetAngleInternal(angle);
            UpdateState();
        }

        public void SetMonsterAttackState(bool underAttack)
        {
            _isUnderMonsterAttack = underAttack;
        }

        /// <summary>
        /// Nudges the door inward (towards openAngle) when the monster pounds/pushes against it.
        /// If the player is actively grabbing/holding the door or knob, strong physical resistance is applied.
        /// Progressively and continuously transitions rotation without instantaneous jumps.
        /// </summary>
        public void ApplyMonsterPush(float pushAngleDelta)
        {
            float effectiveDelta = pushAngleDelta;
            if (IsGrabbed)
            {
                effectiveDelta *= 0.35f; // Player actively holding door closed resists 65% of force
            }

            float baseAngle = _hasTargetPushAngle ? _targetMonsterPushAngle : _currentAngle;
            _targetMonsterPushAngle = Mathf.Clamp(baseAngle - Mathf.Abs(effectiveDelta), Mathf.Min(_closedAngle, _openAngle), Mathf.Max(_closedAngle, _openAngle));
            _hasTargetPushAngle = true;
        }
    }
}
