using System;
using System.Collections;
using Normal.Realtime;
using UnityEngine;

namespace Normal.SeamlessRooms {
    /// <summary>
    /// Connects to a room asynchronously.
    /// Will seamlessly swap the room on the Realtime once the new room is connected and ready.
    /// </summary>
    [RequireComponent(typeof(Realtime.Realtime))]
    public class SeamlessRoomConnecter : MonoBehaviour {
        /// <summary>
        /// The quickmatch room group name to connect with.
        /// </summary>
        public string quickmatchGroupName = "default";

        /// <summary>
        /// When creating a new quickmatch room, this will be the max number of players of the new room.
        /// </summary>
        public int quickmatchRoomCapacity = 16;

        /// <summary>
        /// The Realtime instance that this component is managing.
        /// </summary>
        public Realtime.Realtime realtime {
            get {
                if (_realtime == null) {
                    // In case this is called before Awake()
                    return GetComponent<Realtime.Realtime>();
                } else {
                    return _realtime;
                }
            }
        }

        /// <summary>
        /// The room that will be set on the Realtime once it's connected and ready.
        /// </summary>
        public Room roomInProgress => _roomInProgress;

        public event Action onWillConnect;
        public event Action<DisconnectEvent> onDisconnectEvent;

        /// <inheritdoc cref="realtime"/>
        private Realtime.Realtime _realtime;

        /// <inheritdoc cref="roomInProgress"/>
        private Room _roomInProgress;

        private Coroutine _swapRoomsCoroutine;

        private void Awake() {
            _realtime = GetComponent<Realtime.Realtime>();
        }

        private void OnDestroy() {
            // Destroy _room in case we switch scenes while connecting
            DestroyRoomIfNeeded();
        }

        /// <inheritdoc cref="ConnectToRoom"/>
        public void Connect(string roomName, Room.ConnectOptions connectOptions = default) {
            if (_realtime.disconnected == false && roomName == _realtime.room.name) {
                Debug.Log($"Already connecting or connected to {roomName}, ignoring the {nameof(Connect)} call");
                return;
            }

            if (_roomInProgress != null && roomName == _roomInProgress.name) {
                Debug.Log($"Already connecting to {roomName}, ignoring the {nameof(Connect)} call");
                return;
            }

            PrepareConnection(ref connectOptions);

            // Tell the room to connect
            _roomInProgress.Connect(roomName, connectOptions);
        }

        /// <inheritdoc cref="ConnectDirectlyToQuickmatchRoom(string)"/>
        public void ConnectDirectlyToQuickmatchRoom(string roomGroupName, string roomCode, Room.ConnectOptions connectOptions = default) {
            if (_realtime.disconnected == false && roomGroupName == _realtime.room.quickmatchRoomGroupName && roomCode == _realtime.room.quickmatchRoomCode) {
                Debug.Log($"Already connecting or connected to {roomGroupName} - {roomCode}, ignoring the {nameof(ConnectDirectlyToQuickmatchRoom)} call");
                return;
            }

            if (_roomInProgress != null && roomGroupName == _roomInProgress.quickmatchRoomGroupName && roomCode == _roomInProgress.quickmatchRoomCode) {
                Debug.Log($"Already connecting to {roomGroupName} - {roomCode}, ignoring the {nameof(ConnectDirectlyToQuickmatchRoom)} call");
                return;
            }

            PrepareConnection(ref connectOptions);

            // Tell the room to connect
            _roomInProgress.ConnectDirectlyToQuickmatchRoom(roomGroupName, roomCode, connectOptions);
        }

        /// <inheritdoc cref="Quickmatch"/>
        public void ConnectToNextAvailableQuickmatchRoom(string roomGroupName, int capacity, Room.ConnectOptions connectOptions = default) {
            if (_realtime.connecting && roomGroupName == _realtime.room.quickmatchRoomGroupName) {
                Debug.Log($"Already connecting to quickmatch group {roomGroupName}, ignoring the {nameof(ConnectToNextAvailableQuickmatchRoom)} call");
                return;
            }

            if (_roomInProgress != null && roomGroupName == _roomInProgress.quickmatchRoomGroupName) {
                Debug.Log($"Already connecting to quickmatch group {roomGroupName}, ignoring the {nameof(ConnectToNextAvailableQuickmatchRoom)} call");
                return;
            }

            PrepareConnection(ref connectOptions);

            // Tell the room to connect
            _roomInProgress.ConnectToNextAvailableQuickmatchRoom(roomGroupName, capacity, connectOptions);
        }

        /// <summary>
        /// Connects to a room asynchronously.
        /// Will seamlessly swap the room on the Realtime once the new room is connected and ready.
        /// </summary>
        public void ConnectToRoom(string roomName) {
            // Connect using the default ConnectOptions
            Connect(roomName);
        }

        /// <summary>
        /// Connects to a quickmatch room asynchronously.
        /// Will seamlessly swap the room on the Realtime once the new room is connected and ready.
        /// </summary>
        public void ConnectDirectlyToQuickmatchRoom(string roomCode) {
            // Connect using the default ConnectOptions
            ConnectDirectlyToQuickmatchRoom(quickmatchGroupName, roomCode);
        }

        /// <summary>
        /// Connects to a room asynchronously using quickmatch.
        /// Will seamlessly swap the room on the Realtime once the new room is connected and ready.
        /// </summary>
        public void Quickmatch() {
            // Connect using the default options
            ConnectToNextAvailableQuickmatchRoom(quickmatchGroupName, quickmatchRoomCapacity);
        }

        private void PrepareConnection(ref Room.ConnectOptions connectOptions) {
            // If we were still connecting to a different room, destroy it
            DestroyRoomIfNeeded();

            // Usually Realtime.Connect() will fill these in, so we must do it manually here:
            connectOptions.appKey ??= _realtime.normcoreAppSettings.normcoreAppKey;
            connectOptions.matcherURL ??= _realtime.normcoreAppSettings.matcherURL;

            // Create room
            _roomInProgress = new Room();

            // Subscribe to state changes
            _roomInProgress.connectionStateChanged += OnConnectionStateChanged;

            onWillConnect?.Invoke();
        }

        private void OnConnectionStateChanged(Room room, Room.ConnectionState previousState, Room.ConnectionState currentState) {
            if (currentState == Room.ConnectionState.Ready) {
                // Disconnect if necessary
                if (_realtime.connected) {
                    _realtime.Disconnect();
                }

                _swapRoomsCoroutine = StartCoroutine(SwapRooms(room));
            } else if (currentState == Room.ConnectionState.Disconnected || currentState == Room.ConnectionState.Error) {
                onDisconnectEvent?.Invoke(room.disconnectEvent);

                // The connection did not succeed, so we destroy the new room and stay connected to the current room
                DestroyRoomIfNeeded();
            }
        }

        private IEnumerator SwapRooms(Room room) {
            // Wait a frame to let OnDestroy() be called on objects from the old room we disconnected from.
            // This is particularly useful for RealtimeAvatar, otherwise the old avatars' OnDestroy()
            // and the new avatars' Start() calls can overlap and cause exceptions.
            yield return null;

            // Unsubscribe from future callbacks
            _roomInProgress.connectionStateChanged -= OnConnectionStateChanged;

            // We'll be assigning this room to the Realtime which will manage it going forward.
            // So we clear our reference (to avoid processing it inside our OnDestroy() or Update() functions).
            _roomInProgress = null;

            // Swap rooms
            _realtime.room = room;
        }

        private void Update() {
            if (_roomInProgress != null) {
                // Normally Realtime calls this while connecting, but we must do it
                // manually since the Room isn't assigned to a Realtime yet.
                _roomInProgress.Tick(Time.unscaledDeltaTime);
            }
        }

        private void DestroyRoomIfNeeded() {
            if (_swapRoomsCoroutine != null) {
                StopCoroutine(_swapRoomsCoroutine);
                _swapRoomsCoroutine = null;
            }

            if (_roomInProgress == null) {
                return;
            }

            _roomInProgress.connectionStateChanged -= OnConnectionStateChanged;
            _roomInProgress.Dispose();
            _roomInProgress = null;
        }
    }
}
