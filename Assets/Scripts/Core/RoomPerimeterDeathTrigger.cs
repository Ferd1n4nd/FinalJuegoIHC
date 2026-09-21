using UnityEngine;
using NocturnalBreach.Monster;

namespace NocturnalBreach.Core
{
    /// <summary>
    /// Monitors player head/camera position. If the player physically steps out
    /// of the safe bedroom bounds (through open door, window, or boundary walls),
    /// the monster immediately attacks and terminates the run with Game Over.
    /// </summary>
    public class RoomPerimeterDeathTrigger : MonoBehaviour
    {
        [Header("Room Safe Bounds (World Space)")]
        [Tooltip("Minimum X, Y, Z coordinates of safe bedroom area.")]
        [SerializeField] private Vector3 _roomMin = new Vector3(-3.20f, -0.20f, -4.10f);

        [Tooltip("Maximum X, Y, Z coordinates of safe bedroom area.")]
        [SerializeField] private Vector3 _roomMax = new Vector3(3.20f, 2.70f, 4.10f);

        [Header("References")]
        [SerializeField] private Transform _playerHead;
        [SerializeField] private MonsterBrain _monsterBrain;

        private bool _hasTriggered = false;

        private void Awake()
        {
            if (_playerHead == null)
            {
                var centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null) _playerHead = centerEye.transform;
                else if (Camera.main != null) _playerHead = Camera.main.transform;
            }

            if (_monsterBrain == null)
            {
                _monsterBrain = FindAnyObjectByType<MonsterBrain>();
            }
        }

        private void Update()
        {
            if (_hasTriggered) return;

            if (_playerHead == null)
            {
                var centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null) _playerHead = centerEye.transform;
                else if (Camera.main != null) _playerHead = Camera.main.transform;
                if (_playerHead == null) return;
            }

            Vector3 pos = _playerHead.position;

            // Check if player stepped beyond safe bedroom boundaries
            if (pos.x < _roomMin.x || pos.x > _roomMax.x ||
                pos.z < _roomMin.z || pos.z > _roomMax.z)
            {
                TriggerPerimeterBreachDeath();
            }
        }

        private void TriggerPerimeterBreachDeath()
        {
            if (_hasTriggered) return;
            _hasTriggered = true;

            Debug.Log("[RoomPerimeterDeathTrigger] Player stepped outside bedroom safe zone! Monster immediately attacks.");

            if (_monsterBrain == null) _monsterBrain = FindAnyObjectByType<MonsterBrain>();
            if (_monsterBrain != null)
            {
                _monsterBrain.TriggerPerimeterEscapeDeath();
            }
            else
            {
                var screamer = FindAnyObjectByType<DeathScreamerSequence>();
                if (screamer != null)
                {
                    screamer.TriggerScreamer(MonsterEventThreshold.Window);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 center = (_roomMin + _roomMax) * 0.5f;
            Vector3 size = _roomMax - _roomMin;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
