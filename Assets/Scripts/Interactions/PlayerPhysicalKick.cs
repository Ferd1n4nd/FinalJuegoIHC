using UnityEngine;
using Oculus.Interaction;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Imparts a subtle physical impulse when the player's body or feet contact
    /// small tangible floor objects (toys, balls, blocks), giving the feeling of gently kicking/nudging them.
    /// Does not affect structural geometry (walls, bed, door, window) or large furniture.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayerPhysicalKick : MonoBehaviour
    {
        [Tooltip("Force of the gentle physical kick/nudge.")]
        [SerializeField] private float _kickForce = 0.22f;

        [Tooltip("Maximum mass of an object eligible to be kicked (excludes heavy furniture/structures).")]
        [SerializeField] private float _maxKickableMass = 1.5f;

        [Tooltip("Minimum horizontal velocity of player required to impart a kick.")]
        [SerializeField] private float _minKickVelocity = 0.1f;

        private Vector3 _lastPosition;
        private Vector3 _horizontalVelocity;

        private void Start()
        {
            _lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 currentPos = transform.position;
            Vector3 disp = currentPos - _lastPosition;
            disp.y = 0f;
            if (Time.deltaTime > 0f)
            {
                _horizontalVelocity = disp / Time.deltaTime;
            }
            _lastPosition = currentPos;
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            HandleCollision(collision);
        }

        private void HandleCollision(Collision collision)
        {
            Rigidbody targetRb = collision.rigidbody;
            if (targetRb == null || targetRb.isKinematic) return;

            // Only kick small tangible props (exclude structural geometry, furniture, door, window)
            if (targetRb.mass > _maxKickableMass) return;

            // Do not kick an object currently held/grabbed by the player
            var grabbable = targetRb.GetComponent<Grabbable>();
            if (grabbable != null && grabbable.SelectingPointsCount > 0) return;

            // Only kick objects in the lower body / foot zone (contact point below player center)
            if (collision.contactCount > 0)
            {
                float contactY = collision.GetContact(0).point.y;
                if (contactY > transform.position.y + 0.35f) return;
            }

            // Determine kick direction: combination of player movement velocity and push vector
            Vector3 pushDir = (collision.transform.position - transform.position);
            pushDir.y = 0f;

            if (_horizontalVelocity.sqrMagnitude > (_minKickVelocity * _minKickVelocity))
            {
                pushDir = _horizontalVelocity.normalized * 0.7f + pushDir.normalized * 0.3f;
                pushDir.y = 0.02f; // Minimal upward pop to overcome floor friction without launching into air
                pushDir.Normalize();

                float speed = Mathf.Clamp(_horizontalVelocity.magnitude, 0.4f, 1.8f);
                float nudgeSpeed = Mathf.Clamp(_kickForce * speed, 0.08f, 0.45f);
                // Multiplying by mass ensures consistent, controlled velocity across light (0.05kg) and medium (0.4kg) props
                targetRb.AddForce(pushDir * (nudgeSpeed * targetRb.mass), ForceMode.Impulse);
            }
            else if (pushDir.sqrMagnitude > 0.001f)
            {
                // Walking slowly into object
                pushDir.Normalize();
                pushDir.y = 0.02f;
                float nudgeSpeed = Mathf.Clamp(_kickForce * 0.5f, 0.05f, 0.25f);
                targetRb.AddForce(pushDir * (nudgeSpeed * targetRb.mass), ForceMode.Impulse);
            }
        }
    }
}
