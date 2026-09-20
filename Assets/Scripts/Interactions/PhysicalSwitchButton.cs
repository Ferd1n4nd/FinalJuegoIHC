using UnityEngine;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Attach to the physical flashlight button collider to detect finger / interactor poke contact.
    /// </summary>
    public class PhysicalSwitchButton : MonoBehaviour
    {
        [SerializeField] private PhysicalFlashlight _flashlight;

        private void Awake()
        {
            if (_flashlight == null)
            {
                _flashlight = GetComponentInParent<PhysicalFlashlight>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_flashlight != null)
            {
                _flashlight.OnPhysicalButtonPoked();
            }
        }
    }
}
