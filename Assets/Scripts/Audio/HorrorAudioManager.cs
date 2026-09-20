using System;
using UnityEngine;
using NocturnalBreach.Monster;
using NocturnalBreach.Interactions;

namespace NocturnalBreach.Audio
{
    /// <summary>
    /// Dedicated Spatial Audio Manager for Nocturnal Breach.
    /// Manages 3D localized audio for Window, Under-Bed, Door, Monster creature,
    /// physical props, and subtle room ambience.
    /// Fully optimized for Meta Quest 2 (fixed AudioSources, zero runtime allocations).
    /// </summary>
    [SelectionBase]
    public class HorrorAudioManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private MonsterBrain _monsterBrain;
        [SerializeField] private PhysicalVRWindow _window;
        [SerializeField] private PhysicalVRDoor _door;
        [SerializeField] private PhysicalFlashlight _flashlight;

        [Header("Spatial Audio Sources (3D)")]
        [Tooltip("3D AudioSource located outside the north window.")]
        [SerializeField] private AudioSource _windowAudioSource;
        [Tooltip("3D AudioSource located underneath/near the bed.")]
        [SerializeField] private AudioSource _underBedAudioSource;
        [Tooltip("3D AudioSource located outside the south bedroom door.")]
        [SerializeField] private AudioSource _doorAudioSource;
        [Tooltip("3D AudioSource attached to the Monster Mutant 7 transform to follow movement.")]
        [SerializeField] private AudioSource _monsterAudioSource;
        [Tooltip("AudioSource for physical bedroom props.")]
        [SerializeField] private AudioSource _propsAudioSource;
        [Tooltip("AudioSource for subtle room ambience.")]
        [SerializeField] private AudioSource _ambienceAudioSource;

        [Header("3D Spatial Settings (Quest 2 Optimized)")]
        [SerializeField] private float _minDistance = 1.0f;
        [SerializeField] private float _maxDistance = 14.0f;
        [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float _ambienceVolume = 0.4f;

        [Header("Window Audio Clips")]
        [SerializeField] private AudioClip _windowStalkingClip;
        [SerializeField] private AudioClip[] _windowGlassScratchClips;
        [SerializeField] private AudioClip[] _windowGlassTapClips;
        [SerializeField] private AudioClip _windowCloseLatchClip;
        [SerializeField] private AudioClip _windowMonsterReactionClip;
        [SerializeField] private AudioClip[] _windowRetreatClips;

        [Header("Under-Bed Audio Clips")]
        [SerializeField] private AudioClip[] _underBedCrawlingClips;
        [SerializeField] private AudioClip _underBedBreathingClip;
        [SerializeField] private AudioClip _underBedReachingClip;
        [SerializeField] private AudioClip _underBedFlashlightRepelClip;
        [SerializeField] private AudioClip _underBedRetreatClip;

        [Header("Door Audio Clips")]
        [SerializeField] private AudioClip[] _doorApproachFootstepsClips;
        [SerializeField] private AudioClip[] _doorPoundingClips;
        [SerializeField] private AudioClip _doorHandleRattleClip;
        [SerializeField] private AudioClip _doorBreachClip;
        [SerializeField] private AudioClip _doorDefenseSuccessClip;
        [SerializeField] private AudioClip[] _doorRetreatFootstepsClips;

        [Header("Monster Creature Audio Clips")]
        [SerializeField] private AudioClip _monsterIdleBreathingClip;
        [SerializeField] private AudioClip _monsterAggressionGrowlClip;
        [SerializeField] private AudioClip _monsterRecoilClip;

        [Header("Prop Audio Clips")]
        [SerializeField] private AudioClip _flashlightSwitchOnClick;
        [SerializeField] private AudioClip _flashlightSwitchOffClick;
        [SerializeField] private AudioClip _doorCreakClip;
        [SerializeField] private AudioClip _doorLatchClip;
        [SerializeField] private AudioClip _windowSlideClip;

        [Header("Ambience Audio Clips")]
        [SerializeField] private AudioClip _ambientNightRoomLoop;
        [SerializeField] private AudioClip[] _houseCreakClips;

        [Header("Debug")]
        [SerializeField] private bool _enableDebugLogs = false;

        private void Awake()
        {
            FindReferencesIfNull();
            ConfigureAudioSources();
        }

        private void Start()
        {
            StartAmbience();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        #region Setup & Configuration

        private void FindReferencesIfNull()
        {
            if (_monsterBrain == null) _monsterBrain = FindAnyObjectByType<MonsterBrain>();
            if (_window == null) _window = FindAnyObjectByType<PhysicalVRWindow>();
            if (_door == null) _door = FindAnyObjectByType<PhysicalVRDoor>();
            if (_flashlight == null) _flashlight = FindAnyObjectByType<PhysicalFlashlight>();
        }

        public void ConfigureAudioSources()
        {
            ConfigureSource(_windowAudioSource, true);
            ConfigureSource(_underBedAudioSource, true);
            ConfigureSource(_doorAudioSource, true);
            ConfigureSource(_monsterAudioSource, true);
            ConfigureSource(_propsAudioSource, true);
            ConfigureSource(_ambienceAudioSource, false); // Ambience can be 2D/stereo
        }

        private void ConfigureSource(AudioSource source, bool is3D)
        {
            if (source == null) return;

            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.spread = 0f;

            if (is3D)
            {
                source.spatialBlend = 1.0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = _minDistance;
                source.maxDistance = _maxDistance;
            }
            else
            {
                source.spatialBlend = 0.0f;
            }
        }

        public void AssignAudioSources(
            AudioSource windowSrc,
            AudioSource underBedSrc,
            AudioSource doorSrc,
            AudioSource monsterSrc,
            AudioSource propsSrc,
            AudioSource ambienceSrc)
        {
            _windowAudioSource = windowSrc;
            _underBedAudioSource = underBedSrc;
            _doorAudioSource = doorSrc;
            _monsterAudioSource = monsterSrc;
            _propsAudioSource = propsSrc;
            _ambienceAudioSource = ambienceSrc;
            ConfigureAudioSources();
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeEvents()
        {
            if (_monsterBrain != null)
            {
                _monsterBrain.OnMonsterApproachingWindow += HandleMonsterApproachingWindow;
                _monsterBrain.OnMonsterAtWindow += HandleMonsterAtWindow;
                _monsterBrain.OnWindowGlassContact += HandleWindowGlassContact;
                _monsterBrain.OnWindowDefenseSuccess += HandleWindowDefenseSuccess;

                _monsterBrain.OnMonsterUnderBed += HandleMonsterUnderBed;
                _monsterBrain.OnMonsterUnderBedReaching += HandleMonsterUnderBedReaching;
                _monsterBrain.OnUnderBedDefenseSuccess += HandleUnderBedDefenseSuccess;

                _monsterBrain.OnMonsterApproachingDoor += HandleMonsterApproachingDoor;
                _monsterBrain.OnMonsterAtDoor += HandleMonsterAtDoor;
                _monsterBrain.OnDoorImpact += HandleDoorImpact;
                _monsterBrain.OnDoorDefenseSuccess += HandleDoorDefenseSuccess;

                _monsterBrain.OnMonsterRetreated += HandleMonsterRetreated;
                _monsterBrain.OnMonsterBreached += HandleMonsterBreached;
            }

            if (_window != null)
            {
                _window.OnWindowClosed += HandleWindowPhysicalClosed;
            }

            if (_door != null)
            {
                _door.OnDoorClosed += HandleDoorPhysicalClosed;
            }

            if (_flashlight != null)
            {
                _flashlight.OnFlashlightToggled += HandleFlashlightToggled;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_monsterBrain != null)
            {
                _monsterBrain.OnMonsterApproachingWindow -= HandleMonsterApproachingWindow;
                _monsterBrain.OnMonsterAtWindow -= HandleMonsterAtWindow;
                _monsterBrain.OnWindowGlassContact -= HandleWindowGlassContact;
                _monsterBrain.OnWindowDefenseSuccess -= HandleWindowDefenseSuccess;

                _monsterBrain.OnMonsterUnderBed -= HandleMonsterUnderBed;
                _monsterBrain.OnMonsterUnderBedReaching -= HandleMonsterUnderBedReaching;
                _monsterBrain.OnUnderBedDefenseSuccess -= HandleUnderBedDefenseSuccess;

                _monsterBrain.OnMonsterApproachingDoor -= HandleMonsterApproachingDoor;
                _monsterBrain.OnMonsterAtDoor -= HandleMonsterAtDoor;
                _monsterBrain.OnDoorImpact -= HandleDoorImpact;
                _monsterBrain.OnDoorDefenseSuccess -= HandleDoorDefenseSuccess;

                _monsterBrain.OnMonsterRetreated -= HandleMonsterRetreated;
                _monsterBrain.OnMonsterBreached -= HandleMonsterBreached;
            }

            if (_window != null)
            {
                _window.OnWindowClosed -= HandleWindowPhysicalClosed;
            }

            if (_door != null)
            {
                _door.OnDoorClosed -= HandleDoorPhysicalClosed;
            }

            if (_flashlight != null)
            {
                _flashlight.OnFlashlightToggled -= HandleFlashlightToggled;
            }
        }

        #endregion

        #region Window Audio Handlers

        private void HandleMonsterApproachingWindow()
        {
            LogDebug("Audio: Monster approaching window");
            if (_windowAudioSource != null && _windowStalkingClip != null)
            {
                PlayClip(_windowAudioSource, _windowStalkingClip, 0.7f * _sfxVolume, false);
            }
        }

        private void HandleMonsterAtWindow()
        {
            LogDebug("Audio: Monster at window");
            PlayRandomClip(_windowAudioSource, _windowGlassScratchClips, 0.9f * _sfxVolume);
            if (_monsterAudioSource != null && _monsterAggressionGrowlClip != null)
            {
                PlayClip(_monsterAudioSource, _monsterAggressionGrowlClip, 0.6f * _sfxVolume, false);
            }
        }

        private void HandleWindowGlassContact()
        {
            LogDebug("Audio: Window glass contact/tap");
            PlayRandomClip(_windowAudioSource, _windowGlassTapClips, 0.85f * _sfxVolume);
        }

        private void HandleWindowDefenseSuccess()
        {
            LogDebug("Audio: Window defense success");
            StopSource(_windowAudioSource);
            if (_windowAudioSource != null && _windowMonsterReactionClip != null)
            {
                PlayClip(_windowAudioSource, _windowMonsterReactionClip, 1.0f * _sfxVolume, false);
            }
            PlayRandomClip(_windowAudioSource, _windowRetreatClips, 0.75f * _sfxVolume);
        }

        private void HandleWindowPhysicalClosed()
        {
            LogDebug("Audio: Window physically closed");
            if (_propsAudioSource != null && _windowCloseLatchClip != null)
            {
                PlayClip(_propsAudioSource, _windowCloseLatchClip, 0.8f * _sfxVolume, false);
            }
        }

        #endregion

        #region Under-Bed Audio Handlers

        private void HandleMonsterUnderBed()
        {
            LogDebug("Audio: Monster under bed crawling/breathing");
            PlayRandomClip(_underBedAudioSource, _underBedCrawlingClips, 0.8f * _sfxVolume);
            if (_underBedAudioSource != null && _underBedBreathingClip != null)
            {
                PlayClip(_underBedAudioSource, _underBedBreathingClip, 0.65f * _sfxVolume, true);
            }
        }

        private void HandleMonsterUnderBedReaching()
        {
            LogDebug("Audio: Monster under bed reaching upward");
            if (_underBedAudioSource != null && _underBedReachingClip != null)
            {
                PlayClip(_underBedAudioSource, _underBedReachingClip, 1.0f * _sfxVolume, false);
            }
        }

        private void HandleUnderBedDefenseSuccess()
        {
            LogDebug("Audio: Under-bed repelled by flashlight");
            StopSource(_underBedAudioSource);
            if (_underBedAudioSource != null && _underBedFlashlightRepelClip != null)
            {
                PlayClip(_underBedAudioSource, _underBedFlashlightRepelClip, 1.0f * _sfxVolume, false);
            }
            if (_underBedAudioSource != null && _underBedRetreatClip != null)
            {
                PlayClip(_underBedAudioSource, _underBedRetreatClip, 0.8f * _sfxVolume, false);
            }
        }

        #endregion

        #region Door Audio Handlers

        private void HandleMonsterApproachingDoor()
        {
            LogDebug("Audio: Monster approaching door down hallway");
            PlayRandomClip(_doorAudioSource, _doorApproachFootstepsClips, 0.75f * _sfxVolume);
        }

        private void HandleMonsterAtDoor()
        {
            LogDebug("Audio: Monster at door, handle rattle");
            if (_doorAudioSource != null && _doorHandleRattleClip != null)
            {
                PlayClip(_doorAudioSource, _doorHandleRattleClip, 0.85f * _sfxVolume, false);
            }
        }

        private void HandleDoorImpact()
        {
            LogDebug("Audio: Door pound impact");
            PlayRandomClip(_doorAudioSource, _doorPoundingClips, 1.0f * _sfxVolume);
        }

        private void HandleDoorDefenseSuccess()
        {
            LogDebug("Audio: Door defense success, creature retreating");
            StopSource(_doorAudioSource);
            if (_doorAudioSource != null && _doorDefenseSuccessClip != null)
            {
                PlayClip(_doorAudioSource, _doorDefenseSuccessClip, 0.9f * _sfxVolume, false);
            }
            PlayRandomClip(_doorAudioSource, _doorRetreatFootstepsClips, 0.7f * _sfxVolume);
        }

        private void HandleDoorPhysicalClosed()
        {
            LogDebug("Audio: Door physically latched closed");
            if (_propsAudioSource != null && _doorLatchClip != null)
            {
                PlayClip(_propsAudioSource, _doorLatchClip, 0.75f * _sfxVolume, false);
            }
        }

        #endregion

        #region Common Monster & Global Handlers

        private void HandleMonsterRetreated()
        {
            LogDebug("Audio: Monster retreated, resetting threat audio");
            StopSource(_windowAudioSource);
            StopSource(_underBedAudioSource);
            StopSource(_doorAudioSource);
            StopSource(_monsterAudioSource);
        }

        private void HandleMonsterBreached(MonsterEventThreshold threshold)
        {
            LogDebug("Audio: Monster breached at " + threshold);
            AudioSource targetSource = _monsterAudioSource;
            if (threshold == MonsterEventThreshold.Door) targetSource = _doorAudioSource;
            else if (threshold == MonsterEventThreshold.Window) targetSource = _windowAudioSource;
            else if (threshold == MonsterEventThreshold.UnderBed) targetSource = _underBedAudioSource;

            if (targetSource != null && _doorBreachClip != null)
            {
                PlayClip(targetSource, _doorBreachClip, 1.0f * _sfxVolume, false);
            }
        }

        private void HandleFlashlightToggled(bool isOn)
        {
            LogDebug("Audio: Flashlight toggled " + (isOn ? "ON" : "OFF"));
            AudioClip clip = isOn ? _flashlightSwitchOnClick : _flashlightSwitchOffClick;
            if (_propsAudioSource != null && clip != null)
            {
                PlayClip(_propsAudioSource, clip, 0.6f * _sfxVolume, false);
            }
        }

        #endregion

        #region Ambience

        private void StartAmbience()
        {
            if (_ambienceAudioSource != null && _ambientNightRoomLoop != null)
            {
                _ambienceAudioSource.clip = _ambientNightRoomLoop;
                _ambienceAudioSource.loop = true;
                _ambienceAudioSource.volume = _ambienceVolume * _masterVolume;
                _ambienceAudioSource.Play();
            }
        }

        #endregion

        #region Helper Methods (Allocation-free)

        private void PlayClip(AudioSource source, AudioClip clip, float volume, bool loop)
        {
            if (source == null || clip == null) return;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume * _masterVolume);
            source.loop = loop;
            source.Play();
        }

        private void PlayRandomClip(AudioSource source, AudioClip[] clips, float volume)
        {
            if (source == null || clips == null || clips.Length == 0) return;
            int idx = UnityEngine.Random.Range(0, clips.Length);
            AudioClip clip = clips[idx];
            if (clip != null)
            {
                source.PlayOneShot(clip, Mathf.Clamp01(volume * _masterVolume));
            }
        }

        private void StopSource(AudioSource source)
        {
            if (source != null && source.isPlaying)
            {
                source.Stop();
            }
        }

        private void LogDebug(string msg)
        {
            if (_enableDebugLogs)
            {
                Debug.Log("[HorrorAudioManager] " + msg);
            }
        }

        #endregion
    }
}
