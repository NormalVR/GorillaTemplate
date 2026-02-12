using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Normal.XR {
    /// <summary>
    /// Sets the XR tracking mode (<see cref="TrackingOriginModeFlags"/>).
    /// </summary>
    public class XRTrackingOriginMode : MonoBehaviour {
        public enum TrackingOriginMode {
            Floor,
            Device,
        }

        public TrackingOriginMode mode = TrackingOriginMode.Floor;

        private static readonly List<XRInputSubsystem> __subsystems = new List<XRInputSubsystem>();

        private void Awake() {
            ApplyMode();
        }

        private void Update() {
            ApplyMode();
        }

        private void ApplyMode() {
            SubsystemManager.GetSubsystems(__subsystems);

            var value = mode switch {
                TrackingOriginMode.Floor => TrackingOriginModeFlags.Floor,
                TrackingOriginMode.Device => TrackingOriginModeFlags.Device,
                _ => throw new ArgumentOutOfRangeException(),
            };

            foreach (var subsystem in __subsystems) {
                subsystem.TrySetTrackingOriginMode(value);
            }
        }
    }
}
