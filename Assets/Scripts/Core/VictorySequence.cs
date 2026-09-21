using System.Collections;
using UnityEngine;

namespace NocturnalBreach.Core
{
    /// <summary>
    /// Executes the diegetic victory sequence when the player survives the 5-minute night.
    /// Transitions lighting to warm morning sunrise, plays church/clock bells and morning birds,
    /// and displays a diegetic in-world plaque: '5:00 AM — NIGHT SURVIVED'.
    /// </summary>
    public class VictorySequence : MonoBehaviour
    {
        [Header("Lighting")]
        [SerializeField] private Light _sunLight;
        [SerializeField] private Color _dawnColor = new Color(1.0f, 0.88f, 0.70f);
        [SerializeField] private float _dawnIntensity = 1.2f;

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _dawnChimeClip;

        private bool _hasTriggered = false;

        private void OnEnable()
        {
            var director = FindAnyObjectByType<GameDirector>();
            if (director != null)
            {
                director.OnNightSurvived += HandleNightSurvived;
            }
        }

        private void OnDisable()
        {
            var director = FindAnyObjectByType<GameDirector>();
            if (director != null)
            {
                director.OnNightSurvived -= HandleNightSurvived;
            }
        }

        private void HandleNightSurvived()
        {
            if (_hasTriggered) return;

            // If monster already breached and killed the player, death has priority
            var director = FindAnyObjectByType<GameDirector>();
            if (director != null && director.DirectorState == GameDirectorState.MonsterBreached)
            {
                return;
            }

            _hasTriggered = true;
            StartCoroutine(ExecuteDawnTransition());
        }

        private IEnumerator ExecuteDawnTransition()
        {
            if (_sunLight == null)
            {
                var dirLight = GameObject.Find("Directional Light");
                if (dirLight != null) _sunLight = dirLight.GetComponent<Light>();
            }

            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 0f; // 2D stereo for clear celebration
            }

            if (_dawnChimeClip != null)
            {
                _audioSource.PlayOneShot(_dawnChimeClip, 1.0f);
            }

            // Stop room dark ambient loop
            var ambientSource = GameObject.Find("AudioSource_Ambience");
            if (ambientSource != null)
            {
                var src = ambientSource.GetComponent<AudioSource>();
                if (src != null) src.Stop();
            }

            // Smooth dawn light transition over 4 seconds
            Color startColor = (_sunLight != null) ? _sunLight.color : Color.blue;
            float startIntensity = (_sunLight != null) ? _sunLight.intensity : 0.05f;

            float elapsed = 0f;
            float duration = 4.0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                if (_sunLight != null)
                {
                    _sunLight.color = Color.Lerp(startColor, _dawnColor, progress);
                    _sunLight.intensity = Mathf.Lerp(startIntensity, _dawnIntensity, progress);
                }

                yield return null;
            }

            // Create diegetic 3D text/plaque in front of the window
            CreateVictoryPlaque();
            Debug.Log("[VictorySequence] 6:00 AM — YOU SURVIVED THE NIGHT!");
        }

        private void CreateVictoryPlaque()
        {
            var plaque = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plaque.name = "Victory_Plaque";
            plaque.transform.position = new Vector3(0f, 1.80f, 3.85f);
            // Face south towards the player inside the bedroom without mirroring
            plaque.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            plaque.transform.localScale = new Vector3(1.4f, 0.45f, 1.0f);
            Object.DestroyImmediate(plaque.GetComponent<Collider>());

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(urpLit);
            mat.name = "Victory_Plaque_Mat";
            mat.SetColor("_BaseColor", new Color(0.95f, 0.90f, 0.80f));
            mat.SetFloat("_Smoothness", 0.1f);
            plaque.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var textGO = new GameObject("Plaque_Text");
            textGO.transform.SetParent(plaque.transform, false);
            textGO.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            textGO.transform.localRotation = Quaternion.identity;
            textGO.transform.localScale = new Vector3(0.015f, 0.015f, 0.015f);

            var textMesh = textGO.AddComponent<TextMesh>();
            textMesh.text = "6:00 AM\nNIGHT SURVIVED";
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.characterSize = 1.0f;
            textMesh.fontSize = 48;
            textMesh.color = new Color(0.1f, 0.35f, 0.12f);
        }
    }
}
