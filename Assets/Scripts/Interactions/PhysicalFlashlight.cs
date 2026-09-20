using System;
using UnityEngine;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Physical VR Flashlight component. Controls Spot Light beam, battery life,
    /// battery depletion/recharge, and provides physical push-button & controller toggle.
    /// </summary>
    [SelectionBase]
    public class PhysicalFlashlight : MonoBehaviour
    {
        public static PhysicalFlashlight Instance { get; private set; }

        [Header("Light Settings")]
        [SerializeField] private Light _spotLight;
        [SerializeField] private bool _startsOn = true;
        [SerializeField] private float _baseIntensity = 2.5f;

        [Header("Battery Mechanics")]
        [Tooltip("Battery level from 0 to 100.")]
        [SerializeField] private float _currentBattery = 100f;
        [Tooltip("Battery drain per second while turned on (~200 seconds full life).")]
        [SerializeField] private float _batteryDrainRate = 0.5f;
        [Tooltip("Threshold percentage below which light flickers (e.g. 25%).")]
        [SerializeField] private float _lowBatteryThreshold = 25f;

        [Header("Physical Switch")]
        [SerializeField] private Transform _switchTransform;
        [SerializeField] private Vector3 _switchOnLocalPos = new Vector3(0f, 0.022f, -0.01f);
        [SerializeField] private Vector3 _switchOffLocalPos = new Vector3(0f, 0.026f, -0.01f);

        [Header("State")]
        [SerializeField] private bool _isOn = true;

        public event Action<bool> OnFlashlightToggled;
        public event Action<float> OnBatteryChanged;
        public event Action OnBatteryDepleted;
        public event Action OnBatteryRecharged;

        public bool IsOn => _isOn && _currentBattery > 0f;
        public Light SpotLight => _spotLight;
        public float CurrentBattery => _currentBattery;
        public float BatteryPercentage => Mathf.Clamp01(_currentBattery / 100f);

        private float _lastToggleTime;
        private const float ToggleCooldown = 0.25f;
        private float _flickerTimer;

        private void Awake()
        {
            Instance = this;

            if (_spotLight == null)
            {
                _spotLight = GetComponentInChildren<Light>();
            }

            ConfigureLight();
            SetLightState(_startsOn);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_isOn && _currentBattery > 0f)
            {
                _currentBattery = Mathf.Max(0f, _currentBattery - _batteryDrainRate * Time.deltaTime);
                OnBatteryChanged?.Invoke(_currentBattery);

                if (_currentBattery <= 0f)
                {
                    SetLightState(false);
                    OnBatteryDepleted?.Invoke();
                }
                else if (_currentBattery <= _lowBatteryThreshold)
                {
                    ApplyLowBatteryFlicker();
                }
                else if (_spotLight != null && _spotLight.enabled)
                {
                    _spotLight.intensity = _baseIntensity;
                }
            }
        }

        private void ApplyLowBatteryFlicker()
        {
            if (_spotLight == null) return;

            _flickerTimer += Time.deltaTime;
            if (_flickerTimer > 0.08f)
            {
                _flickerTimer = 0f;
                // Subtle flicker between 30% and 90% intensity
                float factor = UnityEngine.Random.Range(0.3f, 0.9f);
                _spotLight.intensity = _baseIntensity * factor;
            }
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
                _spotLight.intensity = _baseIntensity;
            }
        }

        public void Toggle()
        {
            if (Time.time - _lastToggleTime < ToggleCooldown) return;
            _lastToggleTime = Time.time;

            if (_currentBattery <= 0f)
            {
                // Can't turn on with dead battery
                SetLightState(false);
                return;
            }

            SetLightState(!_isOn);
            OnFlashlightToggled?.Invoke(_isOn);
        }

        public void SetLightState(bool on)
        {
            _isOn = on && (_currentBattery > 0f || !on);
            if (_spotLight != null)
            {
                _spotLight.enabled = _isOn;
                if (_isOn) _spotLight.intensity = _baseIntensity;
            }

            if (_switchTransform != null)
            {
                _switchTransform.localPosition = _isOn ? _switchOnLocalPos : _switchOffLocalPos;
            }
        }

        /// <summary>
        /// Replenishes the battery by the specified amount (default: 100%).
        /// </summary>
        public bool Recharge(float amount = 100f)
        {
            _currentBattery = Mathf.Clamp(_currentBattery + amount, 0f, 100f);
            OnBatteryChanged?.Invoke(_currentBattery);
            OnBatteryRecharged?.Invoke();

            // Auto-turn on if was previously held/on
            if (!_isOn)
            {
                SetLightState(true);
                OnFlashlightToggled?.Invoke(true);
            }
            return true;
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
