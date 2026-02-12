using UnityEngine;

#if UNITY_ANDROID
using Unity.Collections;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.Features.Meta;
#endif

#pragma warning disable CS0162
#pragma warning disable CS0414
namespace Normal.XR {
    /// <summary>
    /// Sets the display FPS on the current XR device.
    /// </summary>
    /// <remarks>
    /// Only works on Meta Quest.
    /// Relies on Meta Quest Display Utilities in OpenXR (https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.4/manual/features/display-utilities.html).
    /// </remarks>
    public class XRFPSManager : MonoBehaviour {
        /// <summary>
        /// The display FPS that the device should use.
        /// </summary>
        [SerializeField]
        private int _targetDisplayRefreshRate = 90;

#if UNITY_ANDROID
        private void Awake() {
#if UNITY_EDITOR
            // The editor manages the FPS
            return;
#endif
            var displaySubsystem = XRGeneralSettings.Instance
                .Manager
                .activeLoader
                .GetLoadedSubsystem<XRDisplaySubsystem>();

            if (displaySubsystem.TryGetSupportedDisplayRefreshRates(Allocator.Temp, out var availableRefreshRates) == false) {
                Debug.LogError("Failed to get the available display refresh rates on this device");
                return;
            }

            var targetFPSIsAvailable = false;
            foreach (var rate in availableRefreshRates) {
                if (Mathf.Approximately(rate, _targetDisplayRefreshRate)) {
                    targetFPSIsAvailable = true;
                    break;
                }
            }

            if (targetFPSIsAvailable == false) {
                Debug.LogError($"The requested display refresh rate ({_targetDisplayRefreshRate}) is not available on the device");
                return;
            }

            if (displaySubsystem.TryRequestDisplayRefreshRate(_targetDisplayRefreshRate) == false) {
                Debug.LogError($"Failed to change the display refresh rate");
                return;
            }

            Application.targetFrameRate = _targetDisplayRefreshRate;
            Debug.Log($"Set the display refresh rate to {_targetDisplayRefreshRate} Hz");
        }
#else
        // Quest Link doesn't support the TryRequestDisplayRefreshRate API.
        // The user must select the refresh rate in the PC Meta Quest Link app instead.
        private void Awake() {
            Application.targetFrameRate = _targetDisplayRefreshRate;
        }
#endif
    }
}
#pragma warning restore CS0414
#pragma warning restore CS0162
