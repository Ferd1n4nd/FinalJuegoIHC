using System.Collections;
using UnityEngine;
using NocturnalBreach.Monster;
using NocturnalBreach.Haptics;

namespace NocturnalBreach.Core
{
    /// <summary>
    /// Handles the immediate high-intensity jumpscare screamer and Game Over transition
    /// when the Monster Mutant 7 breaches any threshold into the room.
    /// </summary>
    public class DeathScreamerSequence : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioClip _screamerRoarClip;
        [SerializeField] private float _screamerVolume = 1.0f;

        [Header("Haptics")]
        [SerializeField] private HorrorHapticsManager _hapticsManager;

        [Header("Visual Overlay")]
        [SerializeField] private Color _bloodColor = new Color(0.6f, 0.05f, 0.05f, 0.95f);

        private bool _hasTriggered = false;

        private void OnEnable()
        {
            var brain = FindAnyObjectByType<MonsterBrain>();
            if (brain != null)
            {
                brain.OnMonsterBreached += TriggerScreamer;
            }
        }

        private void OnDisable()
        {
            var brain = FindAnyObjectByType<MonsterBrain>();
            if (brain != null)
            {
                brain.OnMonsterBreached -= TriggerScreamer;
            }
        }

        public void TriggerScreamer(MonsterEventThreshold threshold)
        {
            if (_hasTriggered) return;
            _hasTriggered = true;

            StartCoroutine(ExecuteScreamerRoutine());
        }

        private IEnumerator ExecuteScreamerRoutine()
        {
            var camera = Camera.main;
            Vector3 camPos = (camera != null) ? camera.transform.position : transform.position;

            // 1. Audio jumpscare blast
            if (_screamerRoarClip != null)
            {
                AudioSource.PlayClipAtPoint(_screamerRoarClip, camPos, _screamerVolume);
            }

            // 2. Controller maximum haptics
            if (_hapticsManager != null)
            {
                _hapticsManager.TriggerPulse(HapticTargetHand.Both, 1.0f, 0.35f);
            }

            // 3. Create diegetic red flash / death overlay in front of camera
            GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = "Death_Overlay";
            Object.DestroyImmediate(overlay.GetComponent<Collider>());

            if (camera != null)
            {
                overlay.transform.SetParent(camera.transform, false);
                overlay.transform.localPosition = new Vector3(0f, 0f, 0.30f);
                overlay.transform.localRotation = Quaternion.identity;
                overlay.transform.localScale = new Vector3(1.0f, 0.8f, 1.0f);
            }

            var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(urpUnlit != null ? urpUnlit : Shader.Find("Unlit/Color"));
            mat.SetColor("_BaseColor", new Color(0.8f, 0.0f, 0.0f, 0.85f));
            overlay.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Disable player locomotion
            var locomotor = GameObject.Find("Locomotor");
            if (locomotor != null) locomotor.SetActive(false);

            // Pulse blood red to dark black
            float t = 0f;
            while (t < 3.0f)
            {
                t += Time.deltaTime;
                float alpha = Mathf.Clamp01(t / 1.5f);
                mat.SetColor("_BaseColor", Color.Lerp(new Color(0.8f, 0.0f, 0.0f, 0.85f), new Color(0.05f, 0f, 0f, 0.98f), alpha));
                yield return null;
            }

            Debug.Log("[DeathScreamerSequence] GAME OVER — Monster Breached the bedroom.");
        }
    }
}
