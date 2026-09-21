using UnityEngine;

namespace NocturnalBreach.Interactions
{
    /// <summary>
    /// Visual flashlight button. Poking/triggering is disabled as flashlight toggle
    /// is exclusively controlled by Meta Quest Left Controller Y button.
    /// </summary>
    public class PhysicalSwitchButton : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            // Disabled: No physical poke/touch toggles allowed.
        }
    }
}
