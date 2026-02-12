using UnityEngine;

namespace Normal.XR {
    /// <summary>
    /// Sets Unity's FixedUpdate rate (which is the physics tick rate).
    /// The simplest way to ensure smoothness and responsiveness in an XR app is to set it to the device refresh rate.
    /// </summary>
    public class XRPhysicsTickRateManager : MonoBehaviour {
        [SerializeField]
        private int _tickRate = 50;

        private void Update() {
            Time.fixedDeltaTime = 1f / _tickRate;
        }
    }
}
