using UnityEngine;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Physical VR Flashlight component. Controls Spot Light beam and provides
    /// physical push-button toggle interaction compatible with both Touch controllers
    /// and Hand Tracking.
    /// </summary>
    [SelectionBase]
    public class PhysicalFlashlight : MonoBehaviour
    {
        [Header("Light Settings")]
        [SerializeField] private Light _spotLight;
        [SerializeField] private bool _startsOn = true;

        [Header("Physical Switch")]
        [SerializeField] private Transform _switchTransform;
        [SerializeField] private Vector3 _switchOnLocalPos = new Vector3(0f, 0.022f, -0.01f);
        [SerializeField] private Vector3 _switchOffLocalPos = new Vector3(0f, 0.026f, -0.01f);

        [Header("State")]
        [SerializeField] private bool _isOn = true;

        public event System.Action<bool> OnFlashlightToggled;

        public bool IsOn => _isOn;
        public Light SpotLight => _spotLight;

        private float _lastToggleTime;
        private const float ToggleCooldown = 0.25f;

        private void Awake()
        {
            if (_spotLight == null)
            {
                _spotLight = GetComponentInChildren<Light>();
            }

            ConfigureLight();
            SetLightState(_startsOn);
        }

        private void ConfigureLight()
        {
            if (_spotLight != null)
            {
                _spotLight.type = LightType.Spot;
                _spotLight.range = 7.0f;
                _spotLight.spotAngle = 40.0f;
                _spotLight.innerSpotAngle = 25.0f;
                _spotLight.shadows = LightShadows.None; // Quest 2 fill-rate friendly
                _spotLight.color = new Color(1.0f, 0.96f, 0.88f);
                _spotLight.intensity = 2.5f;
            }
        }

        public void Toggle()
        {
            if (Time.time - _lastToggleTime < ToggleCooldown) return;
            _lastToggleTime = Time.time;

            SetLightState(!_isOn);
            OnFlashlightToggled?.Invoke(_isOn);
        }

        public void SetLightState(bool on)
        {
            _isOn = on;
            if (_spotLight != null)
            {
                _spotLight.enabled = _isOn;
            }

            if (_switchTransform != null)
            {
                _switchTransform.localPosition = _isOn ? _switchOnLocalPos : _switchOffLocalPos;
            }
        }

        /// <summary>
        /// Trigger hook for physical finger / poke contact on the button collider.
        /// </summary>
        public void OnPhysicalButtonPoked()
        {
            Toggle();
        }
    }
}
