using UnityEngine;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Amplifies physical room-scale walking movement by a configurable factor (e.g. 2.5x),
    /// while preserving 1:1 rotational tracking, 1:1 vertical/crouching movement,
    /// and zero artificial sliding or drifting when stationary.
    /// </summary>
    [DisallowMultipleComponent]
    public class PhysicalRoomScaleAmplifier : MonoBehaviour
    {
        [Header("Room-Scale Amplification")]
        [Tooltip("Total multiplier for real-world physical walking (2.5 means each physical step moves 2.5x distance).")]
        [SerializeField] private float _roomScaleMultiplier = 2.5f;

        [Tooltip("Minimum horizontal displacement per frame to apply amplification (filters out tracking jitter).")]
        [SerializeField] private float _jitterThreshold = 0.0005f;

        [Tooltip("Maximum allowed horizontal displacement per frame to prevent teleport spikes.")]
        [SerializeField] private float _maxStepDistancePerFrame = 0.15f;

        [Header("Obstacle Collision Safety")]
        [Tooltip("Layer mask for solid obstacles (walls, heavy furniture).")]
        [SerializeField] private LayerMask _collisionMask = ~0;

        [Tooltip("Check radius around player before applying extra displacement.")]
        [SerializeField] private float _collisionRadius = 0.25f;

        [Header("References")]
        [SerializeField] private Transform _rigOrigin;
        [SerializeField] private Transform _headTransform;

        private Vector3 _lastLocalHeadPos;
        private bool _isInitialized;

        private void Start()
        {
            if (_rigOrigin == null)
            {
                var rig = GameObject.Find("[BuildingBlock] Camera Rig");
                if (rig != null) _rigOrigin = rig.transform;
                else _rigOrigin = transform;
            }

            if (_headTransform == null)
            {
                var centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null) _headTransform = centerEye.transform;
            }

            if (_headTransform != null)
            {
                _lastLocalHeadPos = _headTransform.localPosition;
                _isInitialized = true;
            }
        }

        private void LateUpdate()
        {
            if (!_isInitialized || _headTransform == null || _rigOrigin == null) return;

            Vector3 currentLocalHeadPos = _headTransform.localPosition;
            Vector3 localDelta = currentLocalHeadPos - _lastLocalHeadPos;
            _lastLocalHeadPos = currentLocalHeadPos;

            // Horizontal only: ignore Y so vertical crouching / standing is 100% 1:1
            localDelta.y = 0f;

            float localDist = localDelta.magnitude;
            if (localDist < _jitterThreshold || localDist > _maxStepDistancePerFrame)
            {
                return;
            }

            // Real physical tracking already provides 1.0x displacement in world space.
            // The extra offset needed to reach the target multiplier is (multiplier - 1.0f).
            float extraMultiplier = Mathf.Max(0f, _roomScaleMultiplier - 1.0f);
            if (extraMultiplier <= 0f) return;

            // Transform local horizontal head delta to world space based on rig rotation
            Vector3 worldExtraOffset = _rigOrigin.TransformDirection(localDelta) * extraMultiplier;
            worldExtraOffset.y = 0f;

            // Prevent walking through solid geometry
            if (worldExtraOffset.sqrMagnitude > 0.00001f)
            {
                Vector3 headWorldPos = _headTransform.position;
                Vector3 checkStart = new Vector3(headWorldPos.x, headWorldPos.y - 0.2f, headWorldPos.z);
                if (Physics.SphereCast(checkStart, _collisionRadius, worldExtraOffset.normalized, out RaycastHit hit, worldExtraOffset.magnitude + 0.05f, _collisionMask, QueryTriggerInteraction.Ignore))
                {
                    return;
                }
            }

            _rigOrigin.position += worldExtraOffset;
        }

        public void SetMultiplier(float mult)
        {
            _roomScaleMultiplier = mult;
        }
    }
}
