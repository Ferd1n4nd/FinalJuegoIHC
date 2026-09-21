using UnityEngine;
using Oculus.Interaction;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Physical VR Window component that works alongside Meta XR Interaction SDK
    /// (Grabbable + OneGrabTranslateTransformer) to track sash position and closed/latched state.
    /// Supports progressive physical monster breach resistance and slow environmental ambient creep.
    /// </summary>
    [SelectionBase]
    public class PhysicalVRWindow : MonoBehaviour
    {
        [Header("Height Limits (Local Y)")]
        [Tooltip("Local Y coordinate when window is fully closed/latched.")]
        [SerializeField] private float _closedLocalY = 0.0f;

        [Tooltip("Local Y coordinate when window is fully open.")]
        [SerializeField] private float _openLocalY = 0.60f;

        [Tooltip("Tolerance for considering the window fully closed.")]
        [SerializeField] private float _latchTolerance = 0.03f;

        [Header("Ambient Environmental Creep")]
        [Tooltip("When enabled, the window slowly creeps open over time when not attacked by the monster.")]
        [SerializeField] private bool _enableAmbientCreep = true;

        [Tooltip("Normalized opening speed per second during ambient creep (very subtle).")]
        [SerializeField] private float _ambientOpenSpeed = 0.006f;

        [Tooltip("Maximum opening percentage reached via ambient creep (never reaches breach threshold).")]
        [SerializeField] private float _maxAmbientOpen = 0.30f;

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
        private float _lastReportedY;
        private Grabbable _grabbable;

        private void Awake()
        {
            _grabbable = GetComponent<Grabbable>();
            _initialLocalPosition = transform.localPosition;
            _lastReportedY = transform.localPosition.y;
            UpdateState();
        }

        private void Update()
        {
            UpdateState();
            UpdateAmbientCreep();
        }

        private void UpdateState()
        {
            float currentY = transform.localPosition.y;
            float totalRange = Mathf.Max(0.001f, _openLocalY - _closedLocalY);
            _normalizedOpen = Mathf.Clamp01((currentY - _closedLocalY) / totalRange);

            if (Mathf.Abs(currentY - _lastReportedY) > 0.01f)
            {
                _lastReportedY = currentY;
                OnWindowMoved?.Invoke(_normalizedOpen);
            }

            bool wasClosed = _isClosed;
            _isClosed = (currentY - _closedLocalY) <= _latchTolerance;

            if (_isClosed && !wasClosed)
            {
                OnWindowClosed?.Invoke();
            }
            else if (!_isClosed && wasClosed)
            {
                OnWindowOpened?.Invoke();
            }
        }

        private void UpdateAmbientCreep()
        {
            // Suspended during monster attacks or when the player is physically grabbing/holding the window
            if (!_enableAmbientCreep || _isUnderMonsterAttack || IsGrabbed) return;

            if (_normalizedOpen < _maxAmbientOpen)
            {
                float nextNorm = Mathf.MoveTowards(_normalizedOpen, _maxAmbientOpen, _ambientOpenSpeed * Time.deltaTime);
                ForceSetOpen(nextNorm);
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
            pos.y = Mathf.Lerp(_closedLocalY, _openLocalY, normalized);
            transform.localPosition = pos;
            UpdateState();
        }

        /// <summary>
        /// Nudges the window sash upward when the monster forces it open from outside.
        /// If the player is actively grabbing and defending the window, strong resistance is applied.
        /// </summary>
        public void ApplyMonsterPush(float pushNormalizedDelta)
        {
            float effectiveDelta = pushNormalizedDelta;

            // Physical defense resistance:
            if (IsGrabbed)
            {
                // Player actively gripping the sash down near the sill:
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
