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
        [Tooltip("Minimum gap in seconds between monster events at peak difficulty.")]
        [SerializeField] private float _minEventInterval = 5.0f;

        [Tooltip("Maximum gap in seconds between monster events at start of night.")]
        [SerializeField] private float _maxEventInterval = 25.0f;

        [Tooltip("Total duration of night survival in seconds (300s = 5 minutes).")]
        [SerializeField] private float _totalNightDuration = 300.0f;

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
            float p = Mathf.Clamp01(_elapsedNightTime / _totalNightDuration);

            if (_sunLight == null)
            {
                var dirLight = GameObject.Find("Directional Light");
                if (dirLight != null) _sunLight = dirLight.GetComponent<Light>();
            }

            if (_sunLight != null)
            {
                // Smoothly evolve the room from midnight to early dawn:
                // 0.0 - 0.5 (12:00 - 2:30 AM): Pitch black night with cool dim moonlight
                // 0.5 - 0.8 (2:30 - 4:00 AM): Deep twilight indigo
                // 0.8 - 0.95 (4:00 - 4:45 AM): Early morning twilight
                // 0.95 - 1.0 (4:45 - 5:00 AM): Pre-dawn warm horizon
                Color midnightColor = new Color(0.20f, 0.28f, 0.55f);
                Color twilightColor = new Color(0.40f, 0.35f, 0.60f);
                Color preDawnColor = new Color(0.85f, 0.60f, 0.40f);

                if (p < 0.5f)
                {
                    _sunLight.color = midnightColor;
                    _sunLight.intensity = Mathf.Lerp(0.06f, 0.10f, p * 2f);
                }
                else if (p < 0.85f)
                {
                    float t = (p - 0.5f) / 0.35f;
                    _sunLight.color = Color.Lerp(midnightColor, twilightColor, t);
                    _sunLight.intensity = Mathf.Lerp(0.10f, 0.25f, t);
                }
                else
                {
                    float t = (p - 0.85f) / 0.15f;
                    _sunLight.color = Color.Lerp(twilightColor, preDawnColor, t);
                    _sunLight.intensity = Mathf.Lerp(0.25f, 0.55f, t);
                }
            }
        }

        private void TriggerNightSurvivedVictory()
        {
            // If monster already breached, death has absolute priority
            if (_directorState == GameDirectorState.MonsterBreached) return;

            _directorState = GameDirectorState.NightSurvived;
            _lastResolutionReason = "5:00 AM — NIGHT SURVIVED! VICTORY!";

            // Cease all monster attacks
            if (_monsterBrain != null)
            {
                _monsterBrain.ForceRetreatToDormant();
            }

            // Morning dawn transition
            if (_sunLight == null)
            {
                var dirLight = GameObject.Find("Directional Light");
                if (dirLight != null) _sunLight = dirLight.GetComponent<Light>();
            }

            if (_sunLight != null)
            {
                _sunLight.color = new Color(1.0f, 0.90f, 0.72f);
                _sunLight.intensity = 1.0f;
            }

            if (_victoryAudioSource != null && _victoryBellClip != null)
            {
                _victoryAudioSource.PlayOneShot(_victoryBellClip, 1.0f);
            }

            OnNightSurvived?.Invoke();
            Debug.Log("[GameDirector] 5:00 AM SURVIVED! Dawn has broken.");
        }

        private void ScheduleNextEvent()
        {
            _directorState = GameDirectorState.Pacing;

            // 5-minute pacing escalation:
            // Progress 0.0 - 0.2 (0:00 - 1:00): calm, intervals 18 - 26s
            // Progress 0.2 - 0.5 (1:00 - 2:30): moderate, intervals 12 - 18s
            // Progress 0.5 - 0.8 (2:30 - 4:00): tense, intervals 8 - 14s
            // Progress 0.8 - 1.0 (4:00 - 5:00): climax, intervals 5 - 9s
            float p = Mathf.Clamp01(_elapsedNightTime / _totalNightDuration);
            float minI = Mathf.Lerp(_maxEventInterval * 0.7f, _minEventInterval, p);
            float maxI = Mathf.Lerp(_maxEventInterval, _minEventInterval + 4.0f, p);

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
        /// Selects the next threshold with strict anti-repetition weighting.
        /// </summary>
        private MonsterEventThreshold SelectThreshold()
        {
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
