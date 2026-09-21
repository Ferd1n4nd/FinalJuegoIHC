using UnityEngine;

namespace NocturnalBreach.Core
{
    /// <summary>
    /// Diegetic night clock component. Synchronizes visible clock display
    /// directly with the GameDirector survival timer (12:00 AM -> 5:00 AM across 300 seconds).
    /// Pure diegetic immersion — no floating UI or artificial HUD.
    /// </summary>
    public class DiegeticNightClock : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameDirector _director;
        [SerializeField] private TextMesh _timeText;

        [Header("LED Styling")]
        [SerializeField] private Color _ledColor = new Color(0.2f, 1.0f, 0.35f, 1.0f);
        [SerializeField] private bool _enableColonBlink = true;

        private void Awake()
        {
            if (_director == null)
            {
                _director = FindAnyObjectByType<GameDirector>();
            }

            if (_timeText == null)
            {
                _timeText = GetComponentInChildren<TextMesh>();
            }
        }

        private void Start()
        {
            UpdateTimeDisplay(0f, false);
        }

        private void Update()
        {
            if (_director == null)
            {
                _director = FindAnyObjectByType<GameDirector>();
                if (_director == null) return;
            }

            float p = Mathf.Clamp01(_director.ElapsedNightTime / _director.TotalNightDuration);
            bool blinkColon = _enableColonBlink && ((int)(Time.time * 2f) % 2 == 0);

            UpdateTimeDisplay(p, blinkColon);
        }

        private void UpdateTimeDisplay(float progress, bool blinkColon)
        {
            if (_timeText == null) return;

            // 240 seconds total = 360 minutes (6 hours: 12:00 AM to 6:00 AM)
            // Exactly 1 second = 1.5 minutes in game world
            float totalMinutes = progress * 360f;
            int totalMinsInt = Mathf.Min(360, Mathf.FloorToInt(totalMinutes));

            int hoursPassed = totalMinsInt / 60;
            int minutesInHour = totalMinsInt % 60;
            int displayHour = (hoursPassed == 0) ? 12 : hoursPassed;

            string colon = blinkColon ? ":" : " ";
            string timeString = string.Format("{0}{1}{2:D2}\nAM", displayHour, colon, minutesInHour);

            if (_director != null && _director.DirectorState == GameDirectorState.NightSurvived)
            {
                timeString = "6:00\nAM";
            }

            _timeText.text = timeString;
            _timeText.color = _ledColor;
        }
    }
}
