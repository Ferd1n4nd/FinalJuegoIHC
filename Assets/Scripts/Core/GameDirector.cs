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
        [Tooltip("Minimum gap in seconds between monster events.")]
        [SerializeField] private float _minEventInterval = 12.0f;

        [Tooltip("Maximum gap in seconds between monster events at start of night.")]
        [SerializeField] private float _maxEventInterval = 25.0f;

        [Tooltip("Total duration of night survival in seconds (e.g. 360s = 6 minutes).")]
        [SerializeField] private float _totalNightDuration = 360.0f;

        [Header("Event Selection Weights (Normalized dynamically)")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float _windowWeight = 0.35f;

        [Range(0.1f, 1.0f)]
        [SerializeField] private float _underBedWeight = 0.35f;

        [Range(0.1f, 1.0f)]
        [SerializeField] private float _doorWeight = 0.30f;

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

            if (_elapsedNightTime >= _totalNightDuration)
            {
                _directorState = GameDirectorState.NightSurvived;
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

        private void ScheduleNextEvent()
        {
            _directorState = GameDirectorState.Pacing;

            // Escalate pacing: as night progresses, gap between events decreases
            float nightProgress = Mathf.Clamp01(_elapsedNightTime / _totalNightDuration);
            float currentMaxInterval = Mathf.Lerp(_maxEventInterval, _minEventInterval + 4.0f, nightProgress);
            float currentMinInterval = Mathf.Lerp(_minEventInterval, 6.0f, nightProgress);

            _nextEventTimer = UnityEngine.Random.Range(currentMinInterval, currentMaxInterval);
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
            _directorState = GameDirectorState.MonsterBreached;
            _lastResolutionReason = $"Monster Breached through {breachedThreshold}!";
        }
    }
}
