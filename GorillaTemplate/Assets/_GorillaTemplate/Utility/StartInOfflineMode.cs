using System;
using Normal.Realtime;
using UnityEngine;

namespace Normal.Utility {
    /// <summary>
    /// Connects to an offline room on Start().
    /// </summary>
    [Obsolete("Use Realtime.joinRoomOnStartOptions instead.")]
    [RequireComponent(typeof(Realtime.Realtime))]
    public class StartInOfflineMode : MonoBehaviour {
        /// <summary>
        /// The name to assign to the offline room.
        /// </summary>
        [SerializeField]
        private string roomName;

        public void Start() {
            // Check if enabled
            if (!isActiveAndEnabled) {
                return;
            }

            var realtime = GetComponent<Realtime.Realtime>();

            // Connect to offline room
            realtime.Connect(roomName, connectOptions: new Room.ConnectOptions() {
                offlineMode = true,
            });
        }
    }
}
