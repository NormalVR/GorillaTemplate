using Normal.Realtime;
using Normal.SeamlessRooms;
using UnityEngine;
using UnityEngine.Serialization;

namespace Normal.GorillaTemplate.UI {
    /// <summary>
    /// Displays the current room name for a <see cref="SeamlessRoomConnecter"/>.
    /// </summary>
    public class SeamlessRoomCodeDisplay : MonoBehaviour {
        [SerializeField]
        private SeamlessRoomConnecter _connecter;

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
            _connecter.onWillConnect     += OnWillConnect;
            _connecter.onDisconnectEvent += OnDisconnectEvent;
            _connecter.realtime.didDisconnectFromRoomWithEvent += OnRealtimeDisconnectEvent;
        }

        private void OnDestroy() {
            if (_connecter != null) {
                _connecter.onWillConnect     -= OnWillConnect;
                _connecter.onDisconnectEvent -= OnDisconnectEvent;

                if (_connecter.realtime != null) {
                    _connecter.realtime.didDisconnectFromRoomWithEvent -= OnRealtimeDisconnectEvent;
                }
            }
        }

        private void Update() {
            string description;

            if (_connecter.TryGetComponent(out AutoReconnect autoReconnect) && autoReconnect.isReconnecting) {
                if (autoReconnect.waitTime == 0f) {
                    description = $"Reconnecting...";
                } else {
                    description = $"Reconnecting... ({Mathf.RoundToInt(autoReconnect.remainingTime)})";
                }
            } else {
                if (_connecter.roomInProgress == null) {
                    description = DisplayRealtimeStatus();
                } else {
                    // A connection to a different room is in progress
                    description = DisplayRoomInProgressStatus();
                }
            }

            nameText = $"{_prefix}{description}";
        }

        private string DisplayRealtimeStatus() {
            var realtime = _connecter.realtime;

            // Check connection state
            if (realtime.disconnected) {
                return "Not connected";
            } else if (realtime.connecting) {
                return "Connecting...";
            } else if (realtime.connected) {
                return RoomCodeDisplay.GetRoomDisplayName(realtime.room);
            } else {
                return null;
            }
        }

        private string DisplayRoomInProgressStatus() {
            return "Connecting...";
        }

        private void OnWillConnect() {
            disconnectEventText = string.Empty;
        }

        private void OnDisconnectEvent(DisconnectEvent evt) {
            if (evt is DisconnectCalledByLocalClient) {
                return;
            }

            var details = evt == null ? "Unspecified disconnect reason" : evt.message;
            disconnectEventText = $"{RoomCodeDisplay.GetRoomDisplayName(evt)}: {details}";
        }

        private void OnRealtimeDisconnectEvent(Realtime.Realtime realtime, DisconnectEvent evt) {
            OnDisconnectEvent(evt);
        }
    }
}
