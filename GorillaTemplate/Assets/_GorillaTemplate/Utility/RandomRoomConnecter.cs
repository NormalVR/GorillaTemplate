using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace Normal.Utility {
    /// <summary>
    /// Connects to a random room from the user-provided list.
    /// </summary>
    [Obsolete("Replaced by Realtime's quickmatch system.")]
    [RequireComponent(typeof(Realtime.Realtime))]
    public class RandomRoomConnector : MonoBehaviour {
        /// <summary>
        /// The list of room names to pick from.
        /// </summary>
        [SerializeField]
        private List<string> _roomNames = new List<string>();

        /// <summary>
        /// If set, then this action will be invoked with the selected room name instead of Realtime.Connect() directly.
        /// Use this if you have a wrapper around Realtime.Connect().
        /// </summary>
        [SerializeField]
        private UnityEvent<string> _connectAction;

        private void Start() {
            // Skip if disabled
            if (!isActiveAndEnabled) {
                return;
            }

            Connect();
        }

        private void OnValidate() {
            if (TryGetComponent(out Realtime.Realtime realtime) && realtime.joinRoomOnStartOptions.enabled) {
                Debug.LogError($"The Realtime's \"Join Room On Start\" checkbox is ticked, which conflicts with this component. Untick the checkbox to use this component.");
            }
        }

        [ContextMenu("Connect")]
        private void Connect() {
            // Skip if empty
            if (_roomNames.Count == 0) {
                Debug.LogWarning($"{nameof(RandomRoomConnector)} won't connect to a room because no room names were set in the inspector");
                return;
            }

            // Pick a random room
            var randomIndex = Random.Range(0, _roomNames.Count);
            var roomName = _roomNames[randomIndex];

            // Connect
            if (_connectAction == null) {
                var realtime = GetComponent<Realtime.Realtime>();
                realtime.Connect(roomName);
            } else {
                _connectAction.Invoke(roomName);
            }
        }
    }
}
