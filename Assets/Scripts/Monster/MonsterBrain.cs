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
        [Tooltip("Time in seconds spent moving from Distant -> Approach -> Near -> AtGlass (~2x faster approach).")]
        [SerializeField] private float _windowApproachDuration = 5.2f;
        [Tooltip("Max seconds monster forces window before breaching if player does not close it.")]
        [SerializeField] private float _windowPatienceDuration = 10.4f;
        [Tooltip("Time in seconds for monster to retreat back to Window_Distant (rapid retreat ~0.5s).")]
        [SerializeField] private float _windowRetreatDuration = 0.5f;

        [Header("Under-Bed Behavior Parameters")]
        [Tooltip("Time in seconds spent creeping under bed before reaching upward.")]
        [SerializeField] private float _underBedCrawlDuration = 3.25f;
        [Tooltip("Seconds monster reaches from under bed before breaching if player does not illuminate (3.9s).")]
        [SerializeField] private float _underBedReachTimeout = 3.9f;
        [Tooltip("Seconds required of continuous flashlight illumination to repel (0.05s = immediate reaction).")]
        [SerializeField] private float _requiredLightExposureDuration = 0.05f;
        [Tooltip("Angle in degrees within flashlight beam considered illuminated.")]
        [SerializeField] private float _flashlightDetectionAngle = 55.0f;
        [Tooltip("Max distance in meters from flashlight to monster for under-bed light repel to trigger (2.2m).")]
        [SerializeField] private float _underBedMaxLightDistance = 2.2f;
        [Tooltip("Time in seconds for monster to retreat in reverse under bed (rapid retreat ~0.5s).")]
        [SerializeField] private float _underBedRetreatDuration = 0.5f;
        [Tooltip("Direct AnimationClip for bajocama used for reverse retreat sampling.")]
        [SerializeField] private AnimationClip _underBedClip;

        [Header("Door Behavior Parameters")]
        [Tooltip("Time in seconds monster spends approaching down hallway.")]
        [SerializeField] private float _doorApproachDuration = 10.4f;
        [Tooltip("Seconds the monster pounds/forces the door before retreating if player keeps it shut.")]
        [SerializeField] private float _doorAssaultDuration = 11.7f;
        [Tooltip("Angle beyond which the door is considered breached inward.")]
        [SerializeField] private float _doorBreachAngleThreshold = -45.0f;
        [Tooltip("Time in seconds for monster to retreat down hallway (rapid retreat ~0.5s).")]
        [SerializeField] private float _doorRetreatDuration = 0.5f;

        [Header("Assault Cadence")]
        [Tooltip("Seconds between monster pounds on the door during assault.")]
        [SerializeField] private float _doorPoundInterval = 2.34f;
        [Tooltip("Seconds between glass scratches/taps while at window.")]
        [SerializeField] private float _windowTapInterval = 2.34f;

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

        // New UnderBed and Door Breach fields
        private float _underBedProgress;
        private bool _isDoorBreaching;
        private float _doorBreachStepTimer;

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

            if (_underBedClip == null)
            {
#if UNITY_EDITOR
                foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/MonsterMutant 7/Animations/bajocama.fbx"))
                {
                    if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { _underBedClip = c; break; }
                }
#endif
            }
        }

        private void Start()
        {
            SetState(MonsterState.Dormant);
        }

        private void OnDisable()
        {
            if (_door != null) _door.SetMonsterAttackState(false);
            if (_window != null) _window.SetMonsterAttackState(false);
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
            SetState(MonsterState.UnderBedCrawling);
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
                    PlayAnimation("ventana_izq");
                    _windowTapTimer = 0.4f;
                    _windowForceProgress = (_window != null) ? _window.NormalizedOpen : 0f;
                    _windowWasOpenOnArrival = (_window != null && !_window.IsClosed);
                    if (_window != null) _window.SetMonsterAttackState(true);
                    OnMonsterAtWindow?.Invoke();
                    break;

                case MonsterState.RetreatingFromWindow:
                    PlayAnimation("gethit1");
                    if (_window != null) _window.SetMonsterAttackState(false);
                    if (_stagingController != null)
                    {
                        StartInterpolatedMove(_stagingController.WindowDistant, _windowRetreatDuration);
                    }
                    break;

                case MonsterState.UnderBedDormant:
                    if (_animator != null)
                    {
                        _animator.enabled = true;
                        _animator.Play("bajocama", 0, 0f);
                        _animator.Update(0f);
                    }
                    if (_stagingController != null)
                    {
                        TeleportTo(_stagingController.UnderBedDormant);
                    }
                    transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    if (_underBedClip != null)
                    {
                        _underBedClip.SampleAnimation(gameObject, 0f);
                    }
                    OnMonsterUnderBed?.Invoke();
                    break;

                case MonsterState.UnderBedCrawling:
                    if (_animator != null)
                    {
                        _animator.enabled = true;
                        _animator.Play("bajocama", 0, 0f);
                        _animator.Update(0f);
                    }
                    if (_stagingController != null)
                    {
                        TeleportTo(_stagingController.UnderBedDormant);
                    }
                    transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    if (_underBedClip != null)
                    {
                        _underBedClip.SampleAnimation(gameObject, 0f);
                    }
                    OnMonsterUnderBed?.Invoke();
                    _underBedProgress = 0f;
                    _lightExposureTimer = 0f;
                    break;

                case MonsterState.UnderBedReaching:
                    _lightExposureTimer = 0f;
                    OnMonsterUnderBedReaching?.Invoke();
                    break;

                case MonsterState.RetreatingFromBed:
                    _underBedProgress = Mathf.Max(0.1f, _stateTimer);
                    if (_animator != null) _animator.enabled = false;
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
                    PlayAnimation("animacion_puerta");
                    _doorPoundTimer = 0.5f;
                    _isDoorBreaching = false;
                    _doorBreachStepTimer = 0f;
                    if (_door != null) _door.SetMonsterAttackState(true);
                    OnMonsterAtDoor?.Invoke();
                    break;

                case MonsterState.RetreatingFromDoor:
                    PlayAnimation("gethit4");
                    if (_door != null) _door.SetMonsterAttackState(false);
                    if (_stagingController != null)
                    {
                        StartInterpolatedMove(_stagingController.DoorDistant, _doorRetreatDuration);
                    }
                    break;

                case MonsterState.Cooldown:
                    if (_animator != null) _animator.enabled = true;
                    PlayAnimation("idle1");
                    if (_door != null) _door.SetMonsterAttackState(false);
                    if (_window != null) _window.SetMonsterAttackState(false);
                    TeleportToHiddenLair();
                    OnMonsterRetreated?.Invoke();
                    break;

                case MonsterState.Breached:
                    if (_animator != null) _animator.enabled = true;
                    PlayAnimation("rage");
                    if (_door != null) _door.SetMonsterAttackState(false);
                    if (_window != null) _window.SetMonsterAttackState(false);
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

            if (_window != null)
            {
                // Monster forces the window open from outside during assault (adjusted for 30% longer duration)
                float pushDelta = Time.deltaTime / 7.15f;
                _window.ApplyMonsterPush(pushDelta);

                // Window forced open past threshold -> Monster completes entry!
                if (_window.NormalizedOpen >= 0.88f)
                {
                    _window.SetMonsterAttackState(false);
                    SetState(MonsterState.Breached);
                    return;
                }

                // If player maintained physical defense through entire assault duration (_windowPatienceDuration)
                if (_stateTimer >= _windowPatienceDuration)
                {
                    if (_window.NormalizedOpen <= 0.35f || _window.IsClosed)
                    {
                        _window.ForceSetOpen(0f); // Latched closed
                        _window.SetMonsterAttackState(false);
                        OnWindowDefenseSuccess?.Invoke();
                        SetState(MonsterState.RetreatingFromWindow);
                        return;
                    }
                    else
                    {
                        _window.SetMonsterAttackState(false);
                        SetState(MonsterState.Breached);
                        return;
                    }
                }
            }
            else
            {
                if (_stateTimer >= _windowPatienceDuration)
                {
                    SetState(MonsterState.Breached);
                }
            }
        }

        private void UpdateRetreatingFromWindow()
        {
            _stateTimer += Time.deltaTime;
            if (_moveElapsed >= _moveDuration || _stateTimer >= _windowRetreatDuration)
            {
                SetState(MonsterState.Cooldown);
            }
        }

        private void UpdateUnderBedDormant()
        {
            SetState(MonsterState.UnderBedCrawling);
        }

        private void UpdateUnderBedCrawling()
        {
            _stateTimer += Time.deltaTime;
            _underBedProgress = _stateTimer;

            CheckFlashlightIllumination();

            // At t >= 6.5s in bajocama clip, the creature reaches upward
            if (_stateTimer >= 6.5f)
            {
                OnMonsterUnderBedReaching?.Invoke();
            }

            float totalBedDuration = (_underBedClip != null) ? _underBedClip.length : 9.5f;
            if (_stateTimer >= totalBedDuration)
            {
                SetState(MonsterState.Breached);
            }
        }

        private void UpdateUnderBedReaching()
        {
            UpdateUnderBedCrawling();
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
            Transform headT = transform.Find("Character1_Reference/Character1_Hips/Character1_Spine/Character1_Spine1/Character1_Spine2/Character1_Neck/Character1_Head");
            Vector3 targetPos3 = headT != null ? headT.position : targetPos1;

            float dist1 = Vector3.Distance(lightPos, targetPos1);
            float dist2 = Vector3.Distance(lightPos, targetPos2);
            float dist3 = Vector3.Distance(lightPos, targetPos3);
            float range = _underBedMaxLightDistance;

            bool hit = false;
            if (dist1 <= range && Vector3.Angle(lightForward, targetPos1 - lightPos) <= Mathf.Max(_flashlightDetectionAngle, 50.0f)) hit = true;
            if (!hit && dist2 <= range && Vector3.Angle(lightForward, targetPos2 - lightPos) <= Mathf.Max(_flashlightDetectionAngle, 50.0f)) hit = true;
            if (!hit && dist3 <= range && Vector3.Angle(lightForward, targetPos3 - lightPos) <= Mathf.Max(_flashlightDetectionAngle, 50.0f)) hit = true;

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
            float t = Mathf.Clamp01(_stateTimer / _underBedRetreatDuration);
            float reverseClipTime = Mathf.Lerp(_underBedProgress, 0f, t);

            if (_underBedClip != null)
            {
                _underBedClip.SampleAnimation(gameObject, reverseClipTime);
            }

            if (_stateTimer >= _underBedRetreatDuration)
            {
                if (_animator != null) _animator.enabled = true;
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
            if (_isDoorBreaching)
            {
                _doorBreachStepTimer += Time.deltaTime;
                // Allow monster to complete the final forward step of animacion-puerta through the open doorway (~1.1s)
                if (_doorBreachStepTimer >= 1.1f)
                {
                    _isDoorBreaching = false;
                    SetState(MonsterState.Breached);
                }
                return;
            }

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
                _isDoorBreaching = true;
                _doorBreachStepTimer = 0f;
                if (_door != null) _door.ForceSetAngle(-90.0f);
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
                else
                {
                    _isDoorBreaching = true;
                    _doorBreachStepTimer = 0f;
                    if (_door != null) _door.ForceSetAngle(-90.0f);
                }
            }
        }

        private void UpdateRetreatingFromDoor()
        {
            _stateTimer += Time.deltaTime;
            if (_moveElapsed >= _moveDuration || _stateTimer >= _doorRetreatDuration)
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
