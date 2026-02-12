using UnityEngine;

namespace Normal.Utility {
    /// <summary>
    /// Exposes a ConnectToRoom function with a single room name argument,
    /// a ConnectDirectlyToQuickmatchRoom function with a single room name argument,
    /// and a Quickmatch function.
    /// Targets the <see cref="Realtime.Realtime"/> component that is on the same GameObject.
    /// These functions can be assigned to a <see cref="UnityEngine.Events.UnityEvent"/> in the editor.
    /// </summary>
    [RequireComponent(typeof(Realtime.Realtime))]
    public class RoomConnector : MonoBehaviour {
        private Realtime.Realtime _realtime;

        /// <summary>
        /// The quickmatch room group name to connect.
        /// (Used by <see cref="Quickmatch"/>.)
        /// </summary>
        public string quickmatchGroupName = "default";

        /// <summary>
        /// When creating a new quickmatch room, this will be the max number of players of the new room.
        /// (Used by <see cref="Quickmatch"/>.)
        /// </summary>
        public int quickmatchRoomCapacity = 16;

        private void Awake() {
            _realtime = GetComponent<Realtime.Realtime>();
        }

        public void ConnectToRoom(string roomName) {
            if (_realtime.disconnected == false && roomName == _realtime.room.name) {
                Debug.Log($"Already connecting or connected to {roomName}, ignoring the {nameof(ConnectToRoom)} call");
                return;
            }

            // Disconnect if necessary
            if (_realtime.connected) {
                _realtime.Disconnect();
            }

            _realtime.Connect(roomName);
        }

        public void ConnectDirectlyToQuickmatchRoom(string roomCode) {
            if (_realtime.disconnected == false && quickmatchGroupName == _realtime.room.quickmatchRoomGroupName && roomCode == _realtime.room.quickmatchRoomCode) {
                Debug.Log($"Already connecting or connected to {quickmatchGroupName} - {roomCode}, ignoring the {nameof(ConnectDirectlyToQuickmatchRoom)} call");
                return;
            }

            // Disconnect if necessary
            if (_realtime.connected) {
                _realtime.Disconnect();
            }

            _realtime.ConnectDirectlyToQuickmatchRoom(quickmatchGroupName, roomCode);
        }

        public void Quickmatch() {
            _realtime.ConnectToNextAvailableQuickmatchRoom(quickmatchGroupName, quickmatchRoomCapacity);
        }
    }
}
