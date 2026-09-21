using System;
using UnityEngine;
using UnityEngine.XR;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Physical VR Flashlight component. Controls Spot Light beam, battery life,
    /// battery depletion/recharge. Toggle is EXCLUSIVELY controlled by Meta Quest
    /// Left Controller Button Y.
    /// </summary>
    [SelectionBase]
    public class PhysicalFlashlight : MonoBehaviour
    {
        public static PhysicalFlashlight Instance { get; private set; }

        [Header("Light Settings")]
        [SerializeField] private Light _spotLight;
        [SerializeField] private bool _startsOn = false;
        [SerializeField] private float _baseIntensity = 2.5f;

        [Header("Diegetic Battery Indicator")]
        [Tooltip("MeshRenderers for the 3 battery status LEDs on the flashlight barrel.")]
        [SerializeField] private MeshRenderer _ledHigh; // Green (100% - 66%)
        [SerializeField] private MeshRenderer _ledMid;  // Yellow (66% - 33%)
        [SerializeField] private MeshRenderer _ledLow;  // Red (33% - 0%)

        [Header("Battery Mechanics")]
        [Tooltip("Battery level from 0 to 100.")]
        [SerializeField] private float _currentBattery = 100f;
        [Tooltip("Battery drain per second while turned on (~200 seconds full life).")]
        [SerializeField] private float _batteryDrainRate = 0.5f;
        [Tooltip("Threshold percentage below which light flickers (e.g. 25%).")]
        [SerializeField] private float _lowBatteryThreshold = 25f;

        [Header("Physical Switch Visual")]
        [SerializeField] private Transform _switchTransform;
        [SerializeField] private Vector3 _switchOnLocalPos = new Vector3(0f, 0.022f, -0.01f);
        [SerializeField] private Vector3 _switchOffLocalPos = new Vector3(0f, 0.026f, -0.01f);

        [Header("State")]
        [SerializeField] private bool _isOn = false;

        public event Action<bool> OnFlashlightToggled;
        public event Action<float> OnBatteryChanged;
        public event Action OnBatteryDepleted;
        public event Action OnBatteryRecharged;

        public bool IsOn => _isOn && _currentBattery > 0f;
        public Light SpotLight => _spotLight;
        public float CurrentBattery => _currentBattery;
        public float BatteryPercentage => Mathf.Clamp01(_currentBattery / 100f);

        private float _lastToggleTime;
        private const float ToggleCooldown = 0.20f;
        private float _flickerTimer;
        private bool _wasSecondaryPressedLastFrame = false;
        private Oculus.Interaction.Grabbable _grabbable;

        private void Awake()
        {
            Instance = this;

            _grabbable = GetComponent<Oculus.Interaction.Grabbable>();

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
            // Exclusive input: Meta Quest Left Controller Y button (ONLY when flashlight is currently grabbed)
            CheckLeftYInput();

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

            UpdateBatteryLeds();
        }

        /// <summary>
        /// Reads Left Controller Y button exclusively (Button.Two on LTouch / SecondaryButton in OpenXR / KeyCode.Y in Editor).
        /// STRICT CONDITION: Only toggles if the flashlight is CURRENTLY GRABBED by the player.
        /// </summary>
        private void CheckLeftYInput()
        {
            // If the flashlight is not grabbed, button Y does nothing
            bool isGrabbed = (_grabbable != null && _grabbable.SelectingPointsCount > 0);
            if (!isGrabbed) return;

            bool yDown = false;

            // 1. OVRInput for Meta Quest Touch Controllers
            #if UNITY_ANDROID || UNITY_EDITOR
            try
            {
                if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch) ||
                    OVRInput.GetDown(OVRInput.RawButton.Y))
                {
                    yDown = true;
                }
            }
            catch
            {
                // Fallback to OpenXR InputDevices
            }
            #endif

            // 2. OpenXR InputDevices fallback (SecondaryButton on Left Hand is Y)
            if (!yDown)
            {
                InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                if (leftHand.isValid && leftHand.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed))
                {
                    if (pressed && !_wasSecondaryPressedLastFrame)
                    {
                        yDown = true;
                    }
                    _wasSecondaryPressedLastFrame = pressed;
                }
                else
                {
                    _wasSecondaryPressedLastFrame = false;
                }
            }

            // 3. Editor testing shortcut
            #if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Y))
            {
                yDown = true;
            }
            #endif

            if (yDown)
            {
                Toggle();
            }
        }

        private void UpdateBatteryLeds()
        {
            if (_ledLow == null && _ledMid == null && _ledHigh == null) return;

            bool showLow = _currentBattery > 0f;
            bool showMid = _currentBattery > 33f;
            bool showHigh = _currentBattery > 66f;

            // When low battery, flicker the red LED
            if (_currentBattery > 0f && _currentBattery <= _lowBatteryThreshold)
            {
                showLow = (Mathf.Sin(Time.time * 12f) > 0f);
            }

            SetLedState(_ledHigh, showHigh, new Color(0.1f, 1.0f, 0.2f));
            SetLedState(_ledMid, showMid, new Color(1.0f, 0.85f, 0.1f));
            SetLedState(_ledLow, showLow, new Color(1.0f, 0.15f, 0.1f));
        }

        private void SetLedState(MeshRenderer renderer, bool active, Color color)
        {
            if (renderer == null) return;
            Material mat = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
            if (mat != null)
            {
                if (active)
                {
                    mat.SetColor("_BaseColor", color);
                    mat.SetColor("_EmissionColor", color * 2.5f);
                    mat.EnableKeyword("_EMISSION");
                }
                else
                {
                    mat.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.15f));
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
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
        /// Does NOT toggle or turn on the light (exclusive to Left Controller Y).
        /// </summary>
        public bool Recharge(float amount = 100f)
        {
            _currentBattery = Mathf.Clamp(_currentBattery + amount, 0f, 100f);
            OnBatteryChanged?.Invoke(_currentBattery);
            OnBatteryRecharged?.Invoke();
            return true;
        }

        /// <summary>
        /// Disabled: Toggle is exclusively controlled by Meta Quest Left Controller Y button.
        /// </summary>
        public void OnPhysicalButtonPoked()
        {
            // Intentionally no-op: no physical/touch/grab toggles allowed.
        }
    }
}
