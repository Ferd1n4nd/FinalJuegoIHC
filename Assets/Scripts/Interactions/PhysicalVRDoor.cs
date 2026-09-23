using UnityEngine;
using Oculus.Interaction;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Physical VR Door component that works alongside Meta XR Interaction SDK
    /// (Grabbable + OneGrabRotateTransformer) on SimpleDoor_MainDoor_LOD0.
    /// Tracks rotation angle, closed/latched state, and syncs LODs.
    /// </summary>
    [SelectionBase]
    public class PhysicalVRDoor : MonoBehaviour
    {
        [Header("Rotation Limits (Around Local Y)")]
        [Tooltip("Local Y angle when door is fully closed.")]
        [SerializeField] private float _closedAngle = 0.0f;

        [Tooltip("Local Y angle when door is fully open inward (-85 degrees).")]
        [SerializeField] private float _openAngle = -85.0f;

        [Tooltip("Tolerance in degrees for considering the door latched closed.")]
        [SerializeField] private float _latchAngleTolerance = 3.0f;

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

        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            var knob = transform.Find("Door_Wood/Door_Knob");
            if (knob != null)
            {
                _knobGrabbable = knob.GetComponent<Grabbable>();
            }

            _lastReportedAngle = transform.localEulerAngles.y;
            UpdateState();
        }

        private void Update()
        {
            UpdateState();
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

        public void ForceSetAngle(float angle)
        {
            Vector3 euler = transform.localEulerAngles;
            euler.y = angle;
            transform.localEulerAngles = euler;
            UpdateState();
        }

        public void SetMonsterAttackState(bool underAttack)
        {
            _isUnderMonsterAttack = underAttack;
        }

        /// <summary>
        /// Nudges the door inward (towards openAngle) when the monster pounds/pushes against it.
        /// If the player is actively grabbing/holding the door or knob, strong physical resistance is applied.
        /// </summary>
        public void ApplyMonsterPush(float pushAngleDelta)
        {
            float effectiveDelta = pushAngleDelta;
            if (IsGrabbed)
            {
                effectiveDelta *= 0.35f; // Player actively holding door closed resists 65% of force
            }

            float targetAngle = Mathf.Clamp(_currentAngle - Mathf.Abs(effectiveDelta), Mathf.Min(_closedAngle, _openAngle), Mathf.Max(_closedAngle, _openAngle));
            ForceSetAngle(targetAngle);
        }
    }
}
