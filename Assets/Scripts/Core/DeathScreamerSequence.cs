using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using NocturnalBreach.Monster;
using NocturnalBreach.Haptics;

namespace NocturnalBreach.Core
{
    /// <summary>
    /// Executes the 4-phase VR horror death sequence:
    /// Phase 1 — Jumpscare close-up: Monster Mutant 7 appears directly in front of the player's face/chest.
    /// Phase 2 — Blood / red screen: Progressive red blood surge (no white flash) fading into dark crimson.
    /// Phase 3 — Game Over: Diegetic animated "GAME OVER" screen with death audio.
    /// Phase 4 — Auto Restart: Reloads the active scene cleanly from 0.
    /// </summary>
    public class DeathScreamerSequence : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioClip _screamerRoarClip;
        [SerializeField] private float _screamerVolume = 1.0f;

        [Header("Haptics")]
        [SerializeField] private HorrorHapticsManager _hapticsManager;

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

            // If victory was already reached, do not start jumpscare death sequence
            var director = FindAnyObjectByType<GameDirector>();
            if (director != null && director.DirectorState == GameDirectorState.NightSurvived)
            {
                return;
            }

            _hasTriggered = true;
            StartCoroutine(ExecuteDeathSequenceRoutine());
        }

        private IEnumerator ExecuteDeathSequenceRoutine()
        {
            // Determine player camera position
            Camera cam = Camera.main;
            if (cam == null)
            {
                var centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null) cam = centerEye.GetComponent<Camera>();
            }

            Vector3 camPos = (cam != null) ? cam.transform.position : new Vector3(0f, 1.4f, 0f);
            Vector3 camFwd = (cam != null) ? cam.transform.forward : Vector3.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 0.01f) camFwd = Vector3.forward;
            camFwd.Normalize();

            // Disable player locomotion during death
            var locomotor = GameObject.Find("Locomotor");
            if (locomotor != null) locomotor.SetActive(false);

            // =========================================================
            // FASE 1 — JUMPSCARE AGRESIVO TIPO FOXY (Abalanzarse hacia la cámara)
            // =========================================================
            var monster = GameObject.Find("Base mesh MonsterMutant7 skin1");
            if (monster != null)
            {
                // Play fast aggressive attack animation
                var animator = monster.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.CrossFadeInFixedTime("attack1", 0.02f);
                }

                // Monster leaps from 2.8m away right into the player's face (0.42m)
                Vector3 startPos = camPos + camFwd * 2.8f;
                startPos.y = camPos.y - 1.45f;

                Vector3 closeImpactPos = camPos + camFwd * 0.42f;
                closeImpactPos.y = camPos.y - 1.48f; // Align eyes/snarl right at camera level

                Quaternion monsterFaceCamRot = Quaternion.LookRotation(-camFwd, Vector3.up);

                monster.transform.position = startPos;
                monster.transform.rotation = monsterFaceCamRot;

                // High-speed aggressive leap forward (0.35 seconds)
                float leapElapsed = 0f;
                float leapDuration = 0.35f;

                while (leapElapsed < leapDuration)
                {
                    leapElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(leapElapsed / leapDuration);
                    // Fast ease-in curve for physical pounce momentum
                    float curve = t * t;
                    monster.transform.position = Vector3.Lerp(startPos, closeImpactPos, curve);
                    monster.transform.rotation = monsterFaceCamRot;
                    yield return null;
                }

                monster.transform.position = closeImpactPos;
                if (animator != null)
                {
                    animator.CrossFadeInFixedTime("rage", 0.02f);
                }
            }

            // Audio jumpscare blast at impact
            if (_screamerRoarClip != null)
            {
                AudioSource.PlayClipAtPoint(_screamerRoarClip, camPos, _screamerVolume);
            }

            // High-intensity physical controller vibration
            if (_hapticsManager != null)
            {
                _hapticsManager.TriggerPulse(HapticTargetHand.Both, 1.0f, 0.45f);
            }

            // Brief visceral moment with monster face right at camera (0.15s)
            yield return new WaitForSeconds(0.15f);

            // =========================================================
            // FASE 2 — SANGRE / PANTALLA ROJA (Red surge -> dark crimson)
            // =========================================================
            GameObject bloodOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bloodOverlay.name = "Blood_Death_Overlay";
            Object.DestroyImmediate(bloodOverlay.GetComponent<Collider>());

            if (cam != null)
            {
                bloodOverlay.transform.SetParent(cam.transform, false);
                bloodOverlay.transform.localPosition = new Vector3(0f, 0f, 0.20f);
                bloodOverlay.transform.localRotation = Quaternion.identity;
                bloodOverlay.transform.localScale = new Vector3(1.4f, 1.1f, 1.0f);
            }

            // Transparent URP Unlit Material for blood overlay (renderQueue = 3000)
            var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            Material bloodMat = new Material(urpUnlit != null ? urpUnlit : Shader.Find("Unlit/Color"));
            bloodMat.SetFloat("_Surface", 1f); // Transparent
            bloodMat.SetFloat("_Blend", 0f);   // Alpha blend
            bloodMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            bloodMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            bloodMat.SetInt("_ZWrite", 0);
            bloodMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            bloodMat.renderQueue = 3000;
            bloodMat.SetColor("_BaseColor", new Color(0.70f, 0.02f, 0.02f, 0f));
            bloodOverlay.GetComponent<MeshRenderer>().sharedMaterial = bloodMat;

            float bloodElapsed = 0f;
            float surgeDuration = 0.18f;
            Color deepBlood = new Color(0.75f, 0.02f, 0.02f, 0.92f);

            // Fast blood surge
            while (bloodElapsed < surgeDuration)
            {
                bloodElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(bloodElapsed / surgeDuration);
                bloodMat.SetColor("_BaseColor", new Color(deepBlood.r, deepBlood.g, deepBlood.b, t * 0.92f));
                yield return null;
            }

            // Fade into dark ominous crimson/black
            float fadeElapsed = 0f;
            float fadeDuration = 0.85f;
            Color darkCrimson = new Color(0.04f, 0.005f, 0.005f, 0.98f);

            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(fadeElapsed / fadeDuration);
                bloodMat.SetColor("_BaseColor", Color.Lerp(deepBlood, darkCrimson, t));
                yield return null;
            }

            bloodMat.SetColor("_BaseColor", darkCrimson);

            // =========================================================
            // FASE 3 — GAME OVER (Renderizado garantizado por encima de todo)
            // =========================================================
            // Positioned closer to camera than blood overlay (Z=0.15m vs Z=0.20m)
            // RenderQueue = 4000 (Overlay queue) with depth test Off so nothing can occlude it
            GameObject gameOverGO = new GameObject("GameOver_Text");
            if (cam != null)
            {
                gameOverGO.transform.SetParent(cam.transform, false);
                gameOverGO.transform.localPosition = new Vector3(0f, 0f, 0.15f);
                gameOverGO.transform.localRotation = Quaternion.identity;
                gameOverGO.transform.localScale = Vector3.one;
            }

            var textMesh = gameOverGO.AddComponent<TextMesh>();
            textMesh.text = "GAME OVER";
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.fontSize = 54;
            textMesh.characterSize = 0.0028f;
            Color textColor = new Color(0.95f, 0.15f, 0.15f, 1f);
            textMesh.color = textColor;

            // Ensure text material renders in Overlay queue above the blood quad
            var textRenderer = gameOverGO.GetComponent<MeshRenderer>();
            if (textRenderer != null && textRenderer.material != null)
            {
                textRenderer.material.renderQueue = 4000;
            }

            // Animate GAME OVER text entrance and breathing pulse
            float textElapsed = 0f;
            float textDuration = 3.2f;

            while (textElapsed < textDuration)
            {
                textElapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(textElapsed / 0.5f);

                float pulse = 1.0f + 0.05f * Mathf.Sin(textElapsed * 3f);
                gameOverGO.transform.localScale = new Vector3(pulse, pulse, 1.0f);

                textMesh.color = new Color(textColor.r, textColor.g, textColor.b, alpha);
                yield return null;
            }

            // =========================================================
            // FASE 4 — REINICIO AUTOMÁTICO (Nueva partida desde 0)
            // =========================================================
            Debug.Log("[DeathScreamerSequence] Level restarting cleanly from 0.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
