using System;
using UnityEngine;
using NocturnalBreach.Monster;

namespace NocturnalBreach.Core
{
    public enum GameDirectorState
    {
        Pacing,
        EventActive,
        MonsterBreached,
        NightSurvived
    }

    /// <summary>
    /// Controls pacing, event selection, intervals, and escalation for the single Monster Mutant 7 threat.
    /// Does NOT run repetitive random selections: uses repetition avoidance and escalating tension curves.
    /// </summary>
    [SelectionBase]
    public class GameDirector : MonoBehaviour
    {
        [Header("Monster Reference")]
        [SerializeField] private MonsterBrain _monsterBrain;

        [Header("Director State")]
        [SerializeField] private GameDirectorState _directorState = GameDirectorState.Pacing;
        [SerializeField] private float _elapsedNightTime = 0.0f;
        [SerializeField] private int _totalEventsTriggered = 0;

        [Header("Night Pacing & Escalation Parameters")]
        [Tooltip("Total duration of night survival in seconds (240s = 4 minutes).")]
        [SerializeField] private float _totalNightDuration = 240.0f;

        [Header("Event Selection Weights (Normalized dynamically)")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float _windowWeight = 0.3333f;

        [Range(0.1f, 1.0f)]
        [SerializeField] private float _underBedWeight = 0.3333f;

        [Range(0.1f, 1.0f)]
        [SerializeField] private float _doorWeight = 0.3333f;

        [Header("Victory Environment")]
        [SerializeField] private Light _sunLight;
        [SerializeField] private AudioSource _victoryAudioSource;
        [SerializeField] private AudioClip _victoryBellClip;

        public event Action OnNightSurvived;
        public event Action<MonsterEventThreshold> OnMonsterBreachedGameOver;

        [Header("Debug Status (Inspector Read-Only)")]
        [SerializeField] private float _nextEventTimer = 0.0f;
        [SerializeField] private MonsterEventThreshold _lastTriggeredThreshold = MonsterEventThreshold.None;
        [SerializeField] private string _lastResolutionReason = "None";

        public GameDirectorState DirectorState => _directorState;
        public float ElapsedNightTime => _elapsedNightTime;
        public float TotalNightDuration => _totalNightDuration;
        public MonsterEventThreshold LastTriggeredThreshold => _lastTriggeredThreshold;

        private void Awake()
        {
            if (_monsterBrain == null)
            {
                _monsterBrain = FindAnyObjectByType<MonsterBrain>();
            }
        }

        private void OnEnable()
        {
            if (_monsterBrain != null)
            {
                _monsterBrain.OnWindowDefenseSuccess += HandleWindowSuccess;
                _monsterBrain.OnUnderBedDefenseSuccess += HandleUnderBedSuccess;
                _monsterBrain.OnDoorDefenseSuccess += HandleDoorSuccess;
                _monsterBrain.OnMonsterRetreated += HandleMonsterRetreated;
                _monsterBrain.OnMonsterBreached += HandleMonsterBreached;
            }
        }

        private void OnDisable()
        {
            if (_monsterBrain != null)
            {
                _monsterBrain.OnWindowDefenseSuccess -= HandleWindowSuccess;
                _monsterBrain.OnUnderBedDefenseSuccess -= HandleUnderBedSuccess;
                _monsterBrain.OnDoorDefenseSuccess -= HandleDoorSuccess;
                _monsterBrain.OnMonsterRetreated -= HandleMonsterRetreated;
                _monsterBrain.OnMonsterBreached -= HandleMonsterBreached;
            }
        }

        private void Start()
        {
            ScheduleNextEvent();
            _nextEventTimer = 15.0f; // First attack strictly starts at 15 seconds
        }

        private void Update()
        {
            if (_directorState == GameDirectorState.MonsterBreached || _directorState == GameDirectorState.NightSurvived)
            {
                return;
            }

            _elapsedNightTime += Time.deltaTime;
            UpdateAmbientLightingProgression();

            if (_elapsedNightTime >= _totalNightDuration)
            {
                TriggerNightSurvivedVictory();
                return;
            }

            if (_directorState == GameDirectorState.Pacing)
            {
                _nextEventTimer -= Time.deltaTime;
                if (_nextEventTimer <= 0f)
                {
                    TriggerNextMonsterEvent();
                }
            }
        }

        private void UpdateAmbientLightingProgression()
        {
            if (_sunLight == null)
            {
                var dirLight = GameObject.Find("Directional Light");
                if (dirLight != null) _sunLight = dirLight.GetComponent<Light>();
            }

            if (_sunLight == null) return;

            // Progressive dawn during the final minute of the night:
            // 00:00 - 03:00 (0s - 180s): Deep pitch-black night, sun light remains off (intensity 0).
            // 03:00 - 04:00 (180s - 240s): 60-second progressive sunrise transition entering from north window.
            // 03:00 (180s): Very subtle beginning (intensity 0.0).
            // 03:15 (195s): Noticeable near window (intensity ~0.08).
            // 03:30 (210s): Clearly illuminated floor/room (intensity ~0.24).
            // 03:45 (225s): Evident morning light extending inward (intensity ~0.40).
            // 04:00 (240s / 6:00 AM): Reaches early dawn maximum (intensity ~0.48, warm neutral tone).
            const float dawnStartTime = 180.0f;
            const float dawnEndTime = 240.0f;

            if (_elapsedNightTime < dawnStartTime)
            {
                _sunLight.intensity = 0.0f;
                _sunLight.enabled = false;
            }
            else
            {
                if (!_sunLight.enabled) _sunLight.enabled = true;

                float dawnProgress = Mathf.InverseLerp(dawnStartTime, dawnEndTime, _elapsedNightTime);
                float smoothProgress = Mathf.SmoothStep(0f, 1f, dawnProgress);

                // Early horizon amber/gold to natural early dawn warm neutral
                Color earlyHorizon = new Color(0.96f, 0.62f, 0.38f);
                Color earlyDawn = new Color(1.0f, 0.88f, 0.72f);

                _sunLight.color = Color.Lerp(earlyHorizon, earlyDawn, smoothProgress);
                _sunLight.intensity = Mathf.Lerp(0.0f, 0.48f, smoothProgress);
            }
        }

        private void TriggerNightSurvivedVictory()
        {
            // If monster already breached, death has absolute priority
            if (_directorState == GameDirectorState.MonsterBreached) return;

            _directorState = GameDirectorState.NightSurvived;
            _lastResolutionReason = "6:00 AM — NIGHT SURVIVED! VICTORY!";

            // Cease all monster attacks
            if (_monsterBrain != null)
            {
                _monsterBrain.ForceRetreatToDormant();
            }

            // Maintain early dawn lighting achieved at 6:00 AM (240s) without sudden brightness spike
            if (_sunLight == null)
            {
                var dirLight = GameObject.Find("Directional Light");
                if (dirLight != null) _sunLight = dirLight.GetComponent<Light>();
            }

            if (_sunLight != null)
            {
                _sunLight.color = new Color(1.0f, 0.88f, 0.72f);
                _sunLight.intensity = 0.48f;
            }

            if (_victoryAudioSource != null && _victoryBellClip != null)
            {
                _victoryAudioSource.PlayOneShot(_victoryBellClip, 1.0f);
            }

            OnNightSurvived?.Invoke();
            Debug.Log("[GameDirector] 6:00 AM SURVIVED! Dawn has broken.");
        }

        private void ScheduleNextEvent()
        {
            _directorState = GameDirectorState.Pacing;

            // 4-minute progressive difficulty escalation (total 240s):
            // 00:00 - 01:00 (p: 0.00 - 0.25): lower frequency, intervals ~8 - 10s
            // 01:00 - 02:00 (p: 0.25 - 0.50): moderate frequency, intervals ~6 - 8s
            // 02:00 - 03:00 (p: 0.50 - 0.75): high frequency, intervals ~5 - 7s
            // 03:00 - 04:00 (p: 0.75 - 1.00): peak pressure/climax, intervals ~4 - 6s
            float p = Mathf.Clamp01(_elapsedNightTime / _totalNightDuration);
            float minI = Mathf.Lerp(8.0f, 4.0f, p);
            float maxI = Mathf.Lerp(10.0f, 6.0f, p);

            _nextEventTimer = UnityEngine.Random.Range(minI, maxI);
        }

        private void TriggerNextMonsterEvent()
        {
            if (_monsterBrain == null || _monsterBrain.IsEventActive)
            {
                ScheduleNextEvent();
                return;
            }

            MonsterEventThreshold threshold = SelectThreshold();
            bool triggered = false;

            switch (threshold)
            {
                case MonsterEventThreshold.Window:
                    triggered = _monsterBrain.TriggerWindowEvent();
                    break;
                case MonsterEventThreshold.UnderBed:
                    triggered = _monsterBrain.TriggerUnderBedEvent();
                    break;
                case MonsterEventThreshold.Door:
                    triggered = _monsterBrain.TriggerDoorEvent();
                    break;
            }

            if (triggered)
            {
                _directorState = GameDirectorState.EventActive;
                _lastTriggeredThreshold = threshold;
                _totalEventsTriggered++;
            }
            else
            {
                ScheduleNextEvent();
            }
        }

        /// <summary>
        /// Selects the next threshold.
        /// The first 3 attacks strictly follow the scripted order:
        /// 1. Window -> 2. UnderBed -> 3. Door.
        /// After the 3rd attack (_totalEventsTriggered >= 3), returns to the weighted anti-repetition system.
        /// </summary>
        private MonsterEventThreshold SelectThreshold()
        {
            // Strict fixed sequence for the first 3 attacks of the night:
            if (_totalEventsTriggered == 0) return MonsterEventThreshold.Window;
            if (_totalEventsTriggered == 1) return MonsterEventThreshold.UnderBed;
            if (_totalEventsTriggered == 2) return MonsterEventThreshold.Door;

            // Subsequent attacks (> 3) use the weighted anti-repetition system:
            float wWeight = _windowWeight;
            float bWeight = _underBedWeight;
            float dWeight = _doorWeight;

            // Heavily reduce weight of the last triggered threshold to prevent repetition
            if (_lastTriggeredThreshold == MonsterEventThreshold.Window) wWeight *= 0.15f;
            else if (_lastTriggeredThreshold == MonsterEventThreshold.UnderBed) bWeight *= 0.15f;
            else if (_lastTriggeredThreshold == MonsterEventThreshold.Door) dWeight *= 0.15f;

            float totalWeight = wWeight + bWeight + dWeight;
            float roll = UnityEngine.Random.Range(0f, totalWeight);

            if (roll < wWeight) return MonsterEventThreshold.Window;
            if (roll < wWeight + bWeight) return MonsterEventThreshold.UnderBed;
            return MonsterEventThreshold.Door;
        }

        private void HandleWindowSuccess()
        {
            _lastResolutionReason = "Window Closed by Player";
        }

        private void HandleUnderBedSuccess()
        {
            _lastResolutionReason = "Monster Repelled by Flashlight";
        }

        private void HandleDoorSuccess()
        {
            _lastResolutionReason = "Door Resisted / Held Closed by Player";
        }

        private void HandleMonsterRetreated()
        {
            ScheduleNextEvent();
        }

        private void HandleMonsterBreached(MonsterEventThreshold breachedThreshold)
        {
            // If night already survived, victory has priority — do not trigger game over
            if (_directorState == GameDirectorState.NightSurvived) return;

            _directorState = GameDirectorState.MonsterBreached;
            _lastResolutionReason = $"Monster Breached through {breachedThreshold}!";
            OnMonsterBreachedGameOver?.Invoke(breachedThreshold);
        }
    }
}
