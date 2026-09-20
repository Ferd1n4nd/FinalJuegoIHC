using UnityEngine;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Physical VR Window component that works alongside Meta XR Interaction SDK
    /// (Grabbable + OneGrabTranslateTransformer) to track sash position and closed/latched state.
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

        [Header("State")]
        [SerializeField] private bool _isClosed = true;
        [SerializeField] private float _normalizedOpen = 0.0f;

        public event System.Action OnWindowClosed;
        public event System.Action OnWindowOpened;
        public event System.Action<float> OnWindowMoved;

        public bool IsClosed => _isClosed;
        public float NormalizedOpen => _normalizedOpen;

        private Vector3 _initialLocalPosition;
        private float _lastReportedY;

        private void Awake()
        {
            _initialLocalPosition = transform.localPosition;
            _lastReportedY = transform.localPosition.y;
            UpdateState();
        }

        private void Update()
        {
            UpdateState();
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
        /// </summary>
        public void ApplyMonsterPush(float pushNormalizedDelta)
        {
            ForceSetOpen(_normalizedOpen + pushNormalizedDelta);
        }
    }
}
