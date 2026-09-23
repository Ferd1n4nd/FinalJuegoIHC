using UnityEngine;
using Oculus.Interaction;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Physical VR Window component that works alongside Meta XR Interaction SDK
    /// (Grabbable + OneGrabTranslateTransformer) to track sash position and closed/latched state.
    /// Operates on horizontal sliding motion (local Z axis).
    /// Supports progressive physical monster breach resistance.
    /// Ambient creep has been completely removed so the window remains strictly stationary when not manipulated.
    /// </summary>
    [SelectionBase]
    public class PhysicalVRWindow : MonoBehaviour
    {
        [Header("Horizontal Travel Limits (Local Z)")]
        [Tooltip("Local Z coordinate when window is fully closed/latched.")]
        [SerializeField] private float _closedLocalZ = 0.0f;

        [Tooltip("Local Z coordinate when window is fully open horizontally.")]
        [SerializeField] private float _openLocalZ = 0.85f;

        [Tooltip("Tolerance for considering the window fully closed.")]
        [SerializeField] private float _latchTolerance = 0.03f;

        [Header("State")]
        [SerializeField] private bool _isClosed = true;
        [SerializeField] private float _normalizedOpen = 0.0f;
        [SerializeField] private bool _isUnderMonsterAttack = false;

        public event System.Action OnWindowClosed;
        public event System.Action OnWindowOpened;
        public event System.Action<float> OnWindowMoved;

        public bool IsClosed => _isClosed;
        public float NormalizedOpen => _normalizedOpen;
        public bool IsUnderMonsterAttack => _isUnderMonsterAttack;
        public bool IsGrabbed => _grabbable != null && _grabbable.SelectingPointsCount > 0;

        private Vector3 _initialLocalPosition;
        private float _lastReportedZ;
        private Grabbable _grabbable;

        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _initialLocalPosition = transform.localPosition;
            _lastReportedZ = transform.localPosition.z;
            UpdateState();
        }

        private void Update()
        {
            UpdateState();
        }

        private void UpdateState()
        {
            float currentZ = transform.localPosition.z;
            float totalRange = Mathf.Max(0.001f, Mathf.Abs(_openLocalZ - _closedLocalZ));
            _normalizedOpen = Mathf.Clamp01(Mathf.Abs(currentZ - _closedLocalZ) / totalRange);

            if (Mathf.Abs(currentZ - _lastReportedZ) > 0.01f)
            {
                _lastReportedZ = currentZ;
                OnWindowMoved?.Invoke(_normalizedOpen);
            }

            bool wasClosed = _isClosed;
            _isClosed = Mathf.Abs(currentZ - _closedLocalZ) <= _latchTolerance;

            if (_isClosed && !wasClosed)
            {
                OnWindowClosed?.Invoke();
            }
            else if (!_isClosed && wasClosed)
            {
                OnWindowOpened?.Invoke();
            }
        }

        public void SetMonsterAttackState(bool underAttack)
        {
            _isUnderMonsterAttack = underAttack;
        }

        public void ForceSetOpen(float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            Vector3 pos = transform.localPosition;
            pos.z = Mathf.Lerp(_closedLocalZ, _openLocalZ, normalized);
            pos.x = 0f;
            pos.y = 0f;
            transform.localPosition = pos;
            UpdateState();
        }

        /// <summary>
        /// Nudges the window sash horizontally open when the monster forces it from outside.
        /// If the player is actively grabbing/holding the window sash, strong physical resistance is applied.
        /// </summary>
        public void ApplyMonsterPush(float pushNormalizedDelta)
        {
            float effectiveDelta = pushNormalizedDelta;

            // Physical defense resistance:
            if (IsGrabbed)
            {
                // Player actively gripping the sash near the closed position:
                if (_normalizedOpen <= 0.20f)
                {
                    effectiveDelta *= 0.12f; // 88% resisted
                }
                else
                {
                    effectiveDelta *= 0.35f; // 65% resisted
                }
            }

            ForceSetOpen(_normalizedOpen + effectiveDelta);
        }
    }
}
