using Normal.Realtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace Normal.GorillaTemplate.UI {
    /// <summary>
    /// Displays the current room name.
    /// </summary>
    public class RoomCodeDisplay : MonoBehaviour {
        [SerializeField]
        private Realtime.Realtime _realtime;

        /// <summary>
        /// A prefix to display before the name, ex "Current Room: ".
        /// </summary>
        [SerializeField]
        private string _prefix;

        /// <summary>
        /// The text field that will hold the name.
        /// </summary>
        [FormerlySerializedAs("_visual")]
        [SerializeField]
        private TMPro.TMP_Text _nameLabel;

        /// <summary>
        /// The text field that will hold the disconnect event details (if any).
        /// </summary>
        [SerializeField]
        private TMPro.TMP_Text _disconnectEventLabel;

        /// <summary>
        /// The text that describes the room.
        /// </summary>
        public string nameText {
            get => _nameText;
            private set {
                _nameText = value;
                if (_nameLabel != null) {
                    _nameLabel.text = value;
                }
            }
        }

        private string _nameText;

        /// <summary>
        /// The text that describes the disconnect event details (if any).
        /// </summary>
        public string disconnectEventText {
            get => _disconnectEventText;
            private set {
                _disconnectEventText = value;
                if (_disconnectEventLabel != null) {
                    _disconnectEventLabel.text = value;
                }
            }
        }

        private string _disconnectEventText;

        private void Awake() {
            _realtime.didConnectToRoom += OnConnect;
            _realtime.didDisconnectFromRoomWithEvent += OnDisconnectEvent;
        }

        private void OnDestroy() {
            if (_realtime != null) {
                _realtime.didConnectToRoom -= OnConnect;
                _realtime.didDisconnectFromRoomWithEvent -= OnDisconnectEvent;
            }
        }

        private void Update() {
            string description;

            if (_realtime.TryGetComponent(out AutoReconnect autoReconnect) && autoReconnect.isReconnecting) {
                if (autoReconnect.waitTime == 0f) {
                    description = $"Reconnecting...";
                } else {
                    description = $"Reconnecting... ({Mathf.RoundToInt(autoReconnect.remainingTime)})";
                }
            } else {
                // Check connection state
                if (_realtime.disconnected) {
                    description = "Not connected";
                } else if (_realtime.connecting) {
                    description = "Connecting...";
                } else if (_realtime.connected) {
                    description = GetRoomDisplayName(_realtime.room);
                } else {
                    description = null;
                }
            }

            nameText = $"{_prefix}{description}";
        }

        private void OnConnect(Realtime.Realtime realtime) {
            disconnectEventText = string.Empty;
        }

        private void OnDisconnectEvent(Realtime.Realtime realtime, DisconnectEvent evt) {
            if (evt is DisconnectCalledByLocalClient) {
                return;
            }

            var details = evt == null ? "Unspecified disconnect reason" : evt.message;
            disconnectEventText = $"{GetRoomDisplayName(evt)}: {details}";
        }

        public static string GetRoomDisplayName(Room room) {
            return room.quickmatchRoom ? $"{room.quickmatchRoomCode} (Public)" : $"{room.name} (Private)";
        }

        public static string GetRoomDisplayName(DisconnectEvent evt) {
            return evt.quickmatchRoom ? $"{evt.quickmatchRoomCode} (Public)" : $"{evt.roomName} (Private)";
        }
    }
}
