using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using NocturnalBreach.Monster;
using NocturnalBreach.Interactions;

namespace NocturnalBreach.Haptics
{
    public enum HapticTargetHand
    {
        Both,
        Left,
        Right
    }

    /// <summary>
    /// Dedicated Haptics Manager for Nocturnal Breach.
    /// Manages physical controller feedback for Door impacts, Window latching,
    /// Under-bed proximity, Flashlight click, and Breach events.
    /// Supports standard OpenXR (UnityEngine.XR.InputDevices) and OVRInput for 100% Quest 2 standalone compatibility.
    /// Strict safety limits ensure non-continuous, comfortable, moderate pulses.
    /// </summary>
    [SelectionBase]
    public class HorrorHapticsManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private MonsterBrain _monsterBrain;
        [SerializeField] private PhysicalVRWindow _window;
        [SerializeField] private PhysicalVRDoor _door;
        [SerializeField] private PhysicalFlashlight _flashlight;

        [Header("Door Impact Haptics")]
        [Range(0f, 1f)] [SerializeField] private float _doorImpactAmplitude = 0.70f;
        [SerializeField] private float _doorImpactDuration = 0.12f;
        [Range(0f, 1f)] [SerializeField] private float _doorBreachAmplitude = 0.90f;
        [SerializeField] private float _doorBreachDuration = 0.25f;

        [Header("Window Haptics")]
        [Range(0f, 1f)] [SerializeField] private float _windowLatchAmplitude = 0.50f;
        [SerializeField] private float _windowLatchDuration = 0.08f;
        [Range(0f, 1f)] [SerializeField] private float _windowGlassContactAmplitude = 0.40f;
        [SerializeField] private float _windowGlassContactDuration = 0.10f;

        [Header("Under-Bed Haptics")]
        [Range(0f, 1f)] [SerializeField] private float _underBedReachingAmplitude = 0.35f;
        [SerializeField] private float _underBedReachingDuration = 0.18f;
        [Range(0f, 1f)] [SerializeField] private float _underBedRepelSuccessAmplitude = 0.50f;
        [SerializeField] private float _underBedRepelSuccessDuration = 0.14f;

        [Header("Flashlight Haptics")]
        [Range(0f, 1f)] [SerializeField] private float _flashlightClickAmplitude = 0.25f;
        [SerializeField] private float _flashlightClickDuration = 0.05f;

        [Header("Safety & Master Controls")]
        [SerializeField] private bool _enableHaptics = true;
        [Range(0f, 1f)] [SerializeField] private float _masterHapticsScale = 1.0f;
        [SerializeField] private bool _enableDebugLogs = false;

        private Coroutine _leftHapticRoutine;
        private Coroutine _rightHapticRoutine;

        private void Awake()
        {
            FindReferencesIfNull();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            StopAllHaptics();
        }

        #region Setup

        private void FindReferencesIfNull()
        {
            if (_monsterBrain == null) _monsterBrain = FindAnyObjectByType<MonsterBrain>();
            if (_window == null) _window = FindAnyObjectByType<PhysicalVRWindow>();
            if (_door == null) _door = FindAnyObjectByType<PhysicalVRDoor>();
            if (_flashlight == null) _flashlight = FindAnyObjectByType<PhysicalFlashlight>();
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeEvents()
        {
            if (_monsterBrain != null)
            {
                _monsterBrain.OnWindowGlassContact += HandleWindowGlassContact;
                _monsterBrain.OnWindowDefenseSuccess += HandleWindowDefenseSuccess;

                _monsterBrain.OnMonsterUnderBedReaching += HandleMonsterUnderBedReaching;
                _monsterBrain.OnUnderBedDefenseSuccess += HandleUnderBedDefenseSuccess;

                _monsterBrain.OnDoorImpact += HandleDoorImpact;
                _monsterBrain.OnDoorDefenseSuccess += HandleDoorDefenseSuccess;
                _monsterBrain.OnMonsterBreached += HandleMonsterBreached;
                _monsterBrain.OnMonsterRetreated += HandleMonsterRetreated;
            }

            if (_window != null)
            {
                _window.OnWindowClosed += HandleWindowClosed;
            }

            if (_door != null)
            {
                _door.OnDoorClosed += HandleDoorClosed;
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
                _monsterBrain.OnWindowGlassContact -= HandleWindowGlassContact;
                _monsterBrain.OnWindowDefenseSuccess -= HandleWindowDefenseSuccess;

                _monsterBrain.OnMonsterUnderBedReaching -= HandleMonsterUnderBedReaching;
                _monsterBrain.OnUnderBedDefenseSuccess -= HandleUnderBedDefenseSuccess;

                _monsterBrain.OnDoorImpact -= HandleDoorImpact;
                _monsterBrain.OnDoorDefenseSuccess -= HandleDoorDefenseSuccess;
                _monsterBrain.OnMonsterBreached -= HandleMonsterBreached;
                _monsterBrain.OnMonsterRetreated -= HandleMonsterRetreated;
            }

            if (_window != null)
            {
                _window.OnWindowClosed -= HandleWindowClosed;
            }

            if (_door != null)
            {
                _door.OnDoorClosed -= HandleDoorClosed;
            }

            if (_flashlight != null)
            {
                _flashlight.OnFlashlightToggled -= HandleFlashlightToggled;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleDoorImpact()
        {
            // Slight randomized amplitude variation for organic impact feel
            float jitter = UnityEngine.Random.Range(-0.08f, 0.08f);
            float amp = Mathf.Clamp01((_doorImpactAmplitude + jitter) * _masterHapticsScale);
            TriggerPulse(HapticTargetHand.Both, amp, _doorImpactDuration);
            LogDebug("Haptics: Door impact pulse triggered");
        }

        private void HandleDoorDefenseSuccess()
        {
            // Gentle success confirmation
            TriggerPulse(HapticTargetHand.Both, 0.35f * _masterHapticsScale, 0.1f);
            LogDebug("Haptics: Door defense success pulse");
        }

        private void HandleDoorClosed()
        {
            TriggerPulse(HapticTargetHand.Both, _doorImpactAmplitude * 0.6f * _masterHapticsScale, 0.07f);
            LogDebug("Haptics: Door latched closed");
        }

        private void HandleMonsterBreached(MonsterEventThreshold threshold)
        {
            TriggerPulse(HapticTargetHand.Both, _doorBreachAmplitude * _masterHapticsScale, _doorBreachDuration);
            LogDebug("Haptics: Monster breach shockwave pulse");
        }

        private void HandleWindowGlassContact()
        {
            TriggerPulse(HapticTargetHand.Both, _windowGlassContactAmplitude * _masterHapticsScale, _windowGlassContactDuration);
            LogDebug("Haptics: Window glass contact pulse");
        }

        private void HandleWindowDefenseSuccess()
        {
            TriggerPulse(HapticTargetHand.Both, _windowLatchAmplitude * _masterHapticsScale, _windowLatchDuration);
            LogDebug("Haptics: Window defense latch pulse");
        }

        private void HandleWindowClosed()
        {
            TriggerPulse(HapticTargetHand.Both, _windowLatchAmplitude * _masterHapticsScale, _windowLatchDuration);
            LogDebug("Haptics: Window physically latched");
        }

        private void HandleMonsterUnderBedReaching()
        {
            TriggerPulse(HapticTargetHand.Both, _underBedReachingAmplitude * _masterHapticsScale, _underBedReachingDuration);
            LogDebug("Haptics: Under-bed reaching proximity rumble");
        }

        private void HandleUnderBedDefenseSuccess()
        {
            TriggerPulse(HapticTargetHand.Both, _underBedRepelSuccessAmplitude * _masterHapticsScale, _underBedRepelSuccessDuration);
            LogDebug("Haptics: Under-bed repelled confirmation pulse");
        }

        private void HandleFlashlightToggled(bool isOn)
        {
            TriggerPulse(HapticTargetHand.Both, _flashlightClickAmplitude * _masterHapticsScale, _flashlightClickDuration);
            LogDebug("Haptics: Flashlight click pulse");
        }

        private void HandleMonsterRetreated()
        {
            StopAllHaptics();
            LogDebug("Haptics: Monster retreated, stopped active haptics");
        }

        #endregion

        #region Core Haptics Execution (OpenXR + OVRInput)

        public void TriggerPulse(HapticTargetHand hand, float amplitude, float duration)
        {
            if (!_enableHaptics) return;

            // Strict clamp to prevent high-vibration fatigue or motor burnout
            amplitude = Mathf.Clamp01(amplitude);
            duration = Mathf.Clamp(duration, 0.01f, 0.35f);

            if (hand == HapticTargetHand.Both || hand == HapticTargetHand.Left)
            {
                if (_leftHapticRoutine != null) StopCoroutine(_leftHapticRoutine);
                _leftHapticRoutine = StartCoroutine(ExecutePulseRoutine(XRNode.LeftHand, amplitude, duration));
            }

            if (hand == HapticTargetHand.Both || hand == HapticTargetHand.Right)
            {
                if (_rightHapticRoutine != null) StopCoroutine(_rightHapticRoutine);
                _rightHapticRoutine = StartCoroutine(ExecutePulseRoutine(XRNode.RightHand, amplitude, duration));
            }
        }

        private IEnumerator ExecutePulseRoutine(XRNode node, float amplitude, float duration)
        {
            // 1. Send OpenXR Haptic Impulse (Direct to XR device)
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid)
            {
                device.SendHapticImpulse(0u, amplitude, duration);
            }

            // 2. Also send via OVRInput if available
            TriggerOVRVibration(node, amplitude);

            yield return new WaitForSeconds(duration);

            // Cease OVR vibration
            StopOVRVibration(node);
        }

        private void TriggerOVRVibration(XRNode node, float amplitude)
        {
            #if !UNITY_EDITOR || UNITY_ANDROID
            try
            {
                if (node == XRNode.LeftHand)
                {
                    OVRInput.SetControllerVibration(1.0f, amplitude, OVRInput.Controller.LTouch);
                }
                else if (node == XRNode.RightHand)
                {
                    OVRInput.SetControllerVibration(1.0f, amplitude, OVRInput.Controller.RTouch);
                }
            }
            catch { /* Graceful fallback */ }
            #endif
        }

        private void StopOVRVibration(XRNode node)
        {
            #if !UNITY_EDITOR || UNITY_ANDROID
            try
            {
                if (node == XRNode.LeftHand)
                {
                    OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
                }
                else if (node == XRNode.RightHand)
                {
                    OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
                }
            }
            catch { /* Graceful fallback */ }
            #endif
        }

        public void StopAllHaptics()
        {
            if (_leftHapticRoutine != null)
            {
                StopCoroutine(_leftHapticRoutine);
                _leftHapticRoutine = null;
            }
            if (_rightHapticRoutine != null)
            {
                StopCoroutine(_rightHapticRoutine);
                _rightHapticRoutine = null;
            }

            StopOVRVibration(XRNode.LeftHand);
            StopOVRVibration(XRNode.RightHand);

            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (left.isValid) left.StopHaptics();

            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (right.isValid) right.StopHaptics();
        }

        private void LogDebug(string msg)
        {
            if (_enableDebugLogs)
            {
                Debug.Log("[HorrorHapticsManager] " + msg);
            }
        }

        #endregion
    }
}
