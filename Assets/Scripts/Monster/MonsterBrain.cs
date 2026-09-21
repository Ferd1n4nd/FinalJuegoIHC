using System;
using UnityEngine;
using NocturnalBreach.Interactions;

namespace NocturnalBreach.Monster
{
    public enum MonsterState
    {
        Dormant,
        ApproachingWindow,
        AtWindow,
        RetreatingFromWindow,
        UnderBedDormant,
        UnderBedCrawling,
        UnderBedReaching,
        RetreatingFromBed,
        ApproachingDoor,
        AtDoor,
        RetreatingFromDoor,
        Breached,
        Cooldown
    }

    public enum MonsterEventThreshold
    {
        None,
        Window,
        UnderBed,
        Door
    }

    /// <summary>
    /// Single Monster Brain controlling Monster Mutant 7's state machine,
    /// threshold movement between staging points, animation cues, and defense resolution.
    /// </summary>
    [SelectionBase]
    public class MonsterBrain : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MonsterStagingController _stagingController;
        [SerializeField] private PhysicalVRWindow _window;
        [SerializeField] private PhysicalVRDoor _door;
        [SerializeField] private PhysicalFlashlight _flashlight;
        [SerializeField] private Animator _animator;

        [Header("State Tracking")]
        [SerializeField] private MonsterState _currentState = MonsterState.Dormant;
        [SerializeField] private MonsterEventThreshold _currentThreshold = MonsterEventThreshold.None;

        [Header("Window Behavior Parameters")]
        [Tooltip("Time in seconds spent moving from Distant -> Approach -> Near -> AtGlass.")]
        [SerializeField] private float _windowApproachDuration = 8.0f;
        [Tooltip("Max seconds monster forces window before breaching if player does not close it.")]
        [SerializeField] private float _windowPatienceDuration = 8.0f;
        [Tooltip("Time in seconds for monster to retreat back to Window_Distant.")]
        [SerializeField] private float _windowRetreatDuration = 3.5f;

        [Header("Under-Bed Behavior Parameters")]
        [Tooltip("Time in seconds spent creeping under bed before reaching upward.")]
        [SerializeField] private float _underBedCrawlDuration = 2.5f;
        [Tooltip("Seconds monster reaches from under bed before breaching if player does not illuminate (3.0s).")]
        [SerializeField] private float _underBedReachTimeout = 3.0f;
        [Tooltip("Seconds required of continuous flashlight illumination to repel (0.05s = immediate reaction).")]
        [SerializeField] private float _requiredLightExposureDuration = 0.05f;
        [Tooltip("Angle in degrees within flashlight beam considered illuminated.")]
        [SerializeField] private float _flashlightDetectionAngle = 55.0f;
        [Tooltip("Time in seconds for monster to retreat under floor.")]
        [SerializeField] private float _underBedRetreatDuration = 1.8f;

        [Header("Door Behavior Parameters")]
        [Tooltip("Time in seconds monster spends approaching down hallway.")]
        [SerializeField] private float _doorApproachDuration = 8.0f;
        [Tooltip("Seconds the monster pounds/forces the door before retreating if player keeps it shut.")]
        [SerializeField] private float _doorAssaultDuration = 9.0f;
        [Tooltip("Angle beyond which the door is considered breached inward.")]
        [SerializeField] private float _doorBreachAngleThreshold = -45.0f;
        [Tooltip("Time in seconds for monster to retreat down hallway.")]
        [SerializeField] private float _doorRetreatDuration = 4.0f;

        [Header("Assault Cadence")]
        [Tooltip("Seconds between monster pounds on the door during assault.")]
        [SerializeField] private float _doorPoundInterval = 1.8f;
        [Tooltip("Seconds between glass scratches/taps while at window.")]
        [SerializeField] private float _windowTapInterval = 1.8f;

        [Header("Cooldown Parameters")]
        [Tooltip("Post-retreat idle duration before returning to Dormant.")]
        [SerializeField] private float _cooldownDuration = 3.0f;

        // Public Events for Audio / Haptics / GameDirector hooks
        public event Action<MonsterEventThreshold> OnMonsterEventStarted;
        public event Action OnMonsterApproachingWindow;
        public event Action OnMonsterAtWindow;
        public event Action OnWindowGlassContact;
        public event Action OnWindowDefenseSuccess;

        public event Action OnMonsterUnderBed;
        public event Action OnMonsterUnderBedReaching;
        public event Action OnUnderBedDefenseSuccess;

        public event Action OnMonsterApproachingDoor;
        public event Action OnMonsterAtDoor;
        public event Action OnDoorImpact;
        public event Action OnDoorDefenseSuccess;

        public event Action OnMonsterRetreated;
        public event Action<MonsterEventThreshold> OnMonsterBreached;

        // Internal Timers & Targets
        private float _stateTimer;
        private float _doorPoundTimer;
        private float _windowTapTimer;
        private float _lightExposureTimer;
        private float _windowForceProgress;
        private bool _windowWasOpenOnArrival;
        private Vector3 _moveStartPos;
        private Quaternion _moveStartRot;
        private Transform _moveTargetTransform;
        private float _moveDuration;
        private float _moveElapsed;

        public MonsterState CurrentState => _currentState;
        public MonsterEventThreshold CurrentThreshold => _currentThreshold;
        public bool IsEventActive => _currentState != MonsterState.Dormant && _currentState != MonsterState.Cooldown && _currentState != MonsterState.Breached;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_stagingController == null) _stagingController = FindAnyObjectByType<MonsterStagingController>();
            if (_window == null) _window = FindAnyObjectByType<PhysicalVRWindow>();
            if (_door == null) _door = FindAnyObjectByType<PhysicalVRDoor>();
            if (_flashlight == null) _flashlight = FindAnyObjectByType<PhysicalFlashlight>();
        }

        private void Start()
        {
            SetState(MonsterState.Dormant);
        }

        private void Update()
        {
            UpdateMovementInterpolation();

            switch (_currentState)
            {
                case MonsterState.Dormant:
                    // Managed by GameDirector
                    break;

                // --- WINDOW BEHAVIOR ---
                case MonsterState.ApproachingWindow:
                    UpdateApproachingWindow();
                    break;
                case MonsterState.AtWindow:
                    UpdateAtWindow();
                    break;
                case MonsterState.RetreatingFromWindow:
                    UpdateRetreatingFromWindow();
                    break;

                // --- UNDER-BED BEHAVIOR ---
                case MonsterState.UnderBedDormant:
                    UpdateUnderBedDormant();
                    break;
                case MonsterState.UnderBedCrawling:
                    UpdateUnderBedCrawling();
                    break;
                case MonsterState.UnderBedReaching:
                    UpdateUnderBedReaching();
                    break;
                case MonsterState.RetreatingFromBed:
                    UpdateRetreatingFromBed();
                    break;

                // --- DOOR BEHAVIOR ---
                case MonsterState.ApproachingDoor:
                    UpdateApproachingDoor();
                    break;
                case MonsterState.AtDoor:
                    UpdateAtDoor();
                    break;
                case MonsterState.RetreatingFromDoor:
                    UpdateRetreatingFromDoor();
                    break;

                case MonsterState.Cooldown:
                    UpdateCooldown();
                    break;

                case MonsterState.Breached:
                    // Terminal state for current round
                    break;
            }
        }

        #region Public Trigger APIs (invoked by GameDirector)

        public bool TriggerWindowEvent()
        {
            if (IsEventActive) return false;
            _currentThreshold = MonsterEventThreshold.Window;
            SetState(MonsterState.ApproachingWindow);
            OnMonsterEventStarted?.Invoke(_currentThreshold);
            return true;
        }

        public bool TriggerUnderBedEvent()
        {
            if (IsEventActive) return false;
            _currentThreshold = MonsterEventThreshold.UnderBed;
            SetState(MonsterState.UnderBedDormant);
            OnMonsterEventStarted?.Invoke(_currentThreshold);
            return true;
        }

        public bool TriggerDoorEvent()
        {
            if (IsEventActive) return false;
            _currentThreshold = MonsterEventThreshold.Door;
            SetState(MonsterState.ApproachingDoor);
            OnMonsterEventStarted?.Invoke(_currentThreshold);
            return true;
        }

        /// <summary>
        /// Triggered when the player attempts to physically step out of the bedroom into the perimeter.
        /// Monster instantly breaches and attacks.
        /// </summary>
        public void TriggerPerimeterEscapeDeath()
        {
            if (_currentState == MonsterState.Breached) return;
            _currentThreshold = MonsterEventThreshold.Window;
            SetState(MonsterState.Breached);
        }

        /// <summary>
        /// Instantly forces any active monster assault to cease and retreat (used on Night Survived / Victory).
        /// </summary>
        public void ForceRetreatToDormant()
        {
            if (_currentState == MonsterState.Dormant) return;
            SetState(MonsterState.Cooldown);
        }

        #endregion

        #region State Transitions

        private void SetState(MonsterState newState)
        {
            _currentState = newState;
            _stateTimer = 0f;

            switch (_currentState)
            {
                case MonsterState.Dormant:
                    _currentThreshold = MonsterEventThreshold.None;
                    PlayAnimation("idle1");
                    // Teleport to hidden lair completely out of window sightline
                    TeleportToHiddenLair();
                    break;

                case MonsterState.ApproachingWindow:
                    PlayAnimation("walk2");
                    if (_stagingController != null)
                    {
                        TeleportTo(_stagingController.WindowDistant);
                        StartInterpolatedMove(_stagingController.WindowAtGlass, _windowApproachDuration);
                    }
                    OnMonsterApproachingWindow?.Invoke();
                    break;

                case MonsterState.AtWindow:
                    PlayAnimation("rage");
                    _windowTapTimer = 0.4f;
                    _windowForceProgress = (_window != null) ? _window.NormalizedOpen : 0f;
                    _windowWasOpenOnArrival = (_window != null && !_window.IsClosed);
                    OnMonsterAtWindow?.Invoke();
                    break;

                case MonsterState.RetreatingFromWindow:
                    PlayAnimation("gethit1");
                    if (_stagingController != null)
                    {
                        StartInterpolatedMove(_stagingController.WindowDistant, _windowRetreatDuration);
                    }
                    break;

                case MonsterState.UnderBedDormant:
                    PlayAnimation("idle2");
                    if (_stagingController != null)
                    {
                        TeleportTo(_stagingController.UnderBedDormant);
                    }
                    OnMonsterUnderBed?.Invoke();
                    break;

                case MonsterState.UnderBedCrawling:
                    PlayAnimation("walk4");
                    if (_stagingController != null)
                    {
                        StartInterpolatedMove(_stagingController.UnderBedReaching, _underBedCrawlDuration);
                    }
                    break;

                case MonsterState.UnderBedReaching:
                    PlayAnimation("attack2RLSpike");
                    _lightExposureTimer = 0f;
                    OnMonsterUnderBedReaching?.Invoke();
                    break;

                case MonsterState.RetreatingFromBed:
                    PlayAnimation("gethit2");
                    if (_stagingController != null)
                    {
                        StartInterpolatedMove(_stagingController.UnderBedDormant, _underBedRetreatDuration);
                    }
                    break;

                case MonsterState.ApproachingDoor:
                    PlayAnimation("walk3");
                    if (_stagingController != null)
                    {
                        TeleportTo(_stagingController.DoorDistant);
                        StartInterpolatedMove(_stagingController.DoorAtDoor, _doorApproachDuration);
                    }
                    OnMonsterApproachingDoor?.Invoke();
                    break;

                case MonsterState.AtDoor:
                    PlayAnimation("attack4");
                    _doorPoundTimer = 0.5f;
                    OnMonsterAtDoor?.Invoke();
                    break;

                case MonsterState.RetreatingFromDoor:
                    PlayAnimation("gethit4");
                    if (_stagingController != null)
                    {
                        StartInterpolatedMove(_stagingController.DoorDistant, _doorRetreatDuration);
                    }
                    break;

                case MonsterState.Cooldown:
                    PlayAnimation("idle1");
                    TeleportToHiddenLair();
                    OnMonsterRetreated?.Invoke();
                    break;

                case MonsterState.Breached:
                    PlayAnimation("rage");
                    var mainCam = Camera.main;
                    if (mainCam != null)
                    {
                        Vector3 faceForward = mainCam.transform.forward;
                        faceForward.y = 0f;
                        if (faceForward.sqrMagnitude > 0.01f) faceForward.Normalize();
                        else faceForward = Vector3.forward;

                        transform.position = mainCam.transform.position + faceForward * 0.75f - Vector3.up * 0.35f;
                        transform.rotation = Quaternion.LookRotation(-faceForward, Vector3.up);
                    }
                    OnMonsterBreached?.Invoke(_currentThreshold);
                    break;
            }
        }

        #endregion

        #region State Update Routines

        private void UpdateApproachingWindow()
        {
            _stateTimer += Time.deltaTime;

            if (_moveElapsed >= _moveDuration)
            {
                SetState(MonsterState.AtWindow);
            }
        }

        private void UpdateAtWindow()
        {
            _stateTimer += Time.deltaTime;

            // Cadence for scratching/tapping against glass
            _windowTapTimer += Time.deltaTime;
            if (_windowTapTimer >= _windowTapInterval)
            {
                _windowTapTimer = 0f;
                OnWindowGlassContact?.Invoke();
            }

            // If the window was already wide open when monster arrived, breach quickly
            if (_windowWasOpenOnArrival)
            {
                if (_window != null && _window.IsClosed)
                {
                    OnWindowDefenseSuccess?.Invoke();
                    SetState(MonsterState.RetreatingFromWindow);
                    return;
                }

                if (_stateTimer >= 3.5f)
                {
                    SetState(MonsterState.Breached);
                    return;
                }
            }
            else
            {
                // Monster progressively forces the window open from outside!
                _windowForceProgress += (Time.deltaTime / _windowPatienceDuration);
                if (_window != null)
                {
                    _window.ApplyMonsterPush(Time.deltaTime / _windowPatienceDuration);

                    // Player pushed/closed the window back down!
                    if (_stateTimer > 0.6f && _window.IsClosed)
                    {
                        OnWindowDefenseSuccess?.Invoke();
                        SetState(MonsterState.RetreatingFromWindow);
                        return;
                    }

                    // Window forced open past threshold -> Monster completes entry!
                    if (_window.NormalizedOpen >= 0.88f || _windowForceProgress >= 1.0f)
                    {
                        SetState(MonsterState.Breached);
                        return;
                    }
                }
            }
        }

        private void UpdateRetreatingFromWindow()
        {
            _stateTimer += Time.deltaTime;
            if (_moveElapsed >= _moveDuration)
            {
                SetState(MonsterState.Cooldown);
            }
        }

        private void UpdateUnderBedDormant()
        {
            _stateTimer += Time.deltaTime;
            if (_stateTimer >= 1.0f)
            {
                SetState(MonsterState.UnderBedCrawling);
            }
        }

        private void UpdateUnderBedCrawling()
        {
            _stateTimer += Time.deltaTime;

            CheckFlashlightIllumination();

            if (_moveElapsed >= _moveDuration)
            {
                SetState(MonsterState.UnderBedReaching);
            }
        }

        private void UpdateUnderBedReaching()
        {
            _stateTimer += Time.deltaTime;

            CheckFlashlightIllumination();

            // If not repelled after reach timeout (3.0s), breach under bed
            if (_stateTimer >= _underBedReachTimeout)
            {
                SetState(MonsterState.Breached);
            }
        }

        private void CheckFlashlightIllumination()
        {
            if (_flashlight == null || !_flashlight.IsOn || _flashlight.SpotLight == null)
            {
                _lightExposureTimer = Mathf.Max(0f, _lightExposureTimer - Time.deltaTime);
                return;
            }

            Vector3 lightPos = _flashlight.SpotLight.transform.position;
            Vector3 lightForward = _flashlight.SpotLight.transform.forward;

            // Target the visible reaching head/claws AND the body under the bed
            Vector3 targetPos1 = new Vector3(-0.65f, 0.12f, 2.30f);
            Vector3 targetPos2 = transform.position + Vector3.up * 0.15f;

            float dist1 = Vector3.Distance(lightPos, targetPos1);
            float dist2 = Vector3.Distance(lightPos, targetPos2);
            float range = _flashlight.SpotLight.range;

            bool hit = false;
            if (dist1 <= range)
            {
                float angle1 = Vector3.Angle(lightForward, targetPos1 - lightPos);
                if (angle1 <= Mathf.Max(_flashlightDetectionAngle, 50.0f)) hit = true;
            }
            if (!hit && dist2 <= range)
            {
                float angle2 = Vector3.Angle(lightForward, targetPos2 - lightPos);
                if (angle2 <= Mathf.Max(_flashlightDetectionAngle, 50.0f)) hit = true;
            }

            if (hit)
            {
                _lightExposureTimer += Time.deltaTime;
                if (_lightExposureTimer >= _requiredLightExposureDuration)
                {
                    OnUnderBedDefenseSuccess?.Invoke();
                    SetState(MonsterState.RetreatingFromBed);
                }
                return;
            }

            _lightExposureTimer = Mathf.Max(0f, _lightExposureTimer - Time.deltaTime);
        }

        private void UpdateRetreatingFromBed()
        {
            _stateTimer += Time.deltaTime;
            if (_moveElapsed >= _moveDuration)
            {
                SetState(MonsterState.Cooldown);
            }
        }

        private void UpdateApproachingDoor()
        {
            _stateTimer += Time.deltaTime;

            if (_moveElapsed >= _moveDuration)
            {
                SetState(MonsterState.AtDoor);
            }
        }

        private void UpdateAtDoor()
        {
            _stateTimer += Time.deltaTime;

            // Cadence for pounding and physically forcing the door open
            _doorPoundTimer += Time.deltaTime;
            if (_doorPoundTimer >= _doorPoundInterval)
            {
                _doorPoundTimer = 0f;
                OnDoorImpact?.Invoke();

                // Monster pushes the door inward!
                if (_door != null)
                {
                    _door.ApplyMonsterPush(10.0f);
                }
            }

            // Check if door was forced open beyond breach angle (-45 degrees)
            if (_door != null && _door.CurrentAngle <= _doorBreachAngleThreshold)
            {
                SetState(MonsterState.Breached);
                return;
            }

            // Player held door closed through the entire assault duration!
            if (_stateTimer >= _doorAssaultDuration)
            {
                if (_door == null || _door.IsClosed || _door.CurrentAngle > _doorBreachAngleThreshold)
                {
                    OnDoorDefenseSuccess?.Invoke();
                    SetState(MonsterState.RetreatingFromDoor);
                }
            }
        }

        private void UpdateRetreatingFromDoor()
        {
            _stateTimer += Time.deltaTime;
            if (_moveElapsed >= _moveDuration)
            {
                SetState(MonsterState.Cooldown);
            }
        }

        private void UpdateCooldown()
        {
            _stateTimer += Time.deltaTime;
            if (_stateTimer >= _cooldownDuration)
            {
                SetState(MonsterState.Dormant);
            }
        }

        #endregion

        #region Movement & Animation Helpers

        private void TeleportToHiddenLair()
        {
            // Position behind the North-West exterior wall corner, completely outside window & door sightlines
            transform.position = new Vector3(-8.5f, 0f, 6.5f);
            transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        }

        private void TeleportTo(Transform target)
        {
            if (target != null)
            {
                transform.position = target.position;
                transform.rotation = target.rotation;
            }
        }

        private void StartInterpolatedMove(Transform target, float duration)
        {
            if (target == null) return;
            _moveStartPos = transform.position;
            _moveStartRot = transform.rotation;
            _moveTargetTransform = target;
            _moveDuration = Mathf.Max(0.01f, duration);
            _moveElapsed = 0f;
        }

        private void UpdateMovementInterpolation()
        {
            if (_moveTargetTransform == null || _moveElapsed >= _moveDuration) return;

            _moveElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_moveElapsed / _moveDuration);
            // Smooth ease
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            transform.position = Vector3.Lerp(_moveStartPos, _moveTargetTransform.position, smoothT);
            transform.rotation = Quaternion.Slerp(_moveStartRot, _moveTargetTransform.rotation, smoothT);
        }

        private void PlayAnimation(string stateName)
        {
            if (_animator != null && _animator.HasState(0, Animator.StringToHash(stateName)))
            {
                _animator.CrossFadeInFixedTime(stateName, 0.25f);
            }
        }

        #endregion
    }
}
