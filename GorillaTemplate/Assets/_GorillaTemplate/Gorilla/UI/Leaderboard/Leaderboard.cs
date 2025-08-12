using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Normal.GorillaTemplate.UI.Leaderboard {
    /// <summary>
    /// Displays a list of players in the current room.
    /// </summary>
    public class Leaderboard : MonoBehaviour {
        [SerializeField]
        private GorillaPlayerManager _gorillaPlayerManager;

        /// <summary>
        /// The container that will hold the player entries.
        /// </summary>
        [SerializeField]
        private LayoutGroup _entryContainer;

        /// <summary>
        /// The prefab that represents a player entry in the leaderboard.
        /// </summary>
        [SerializeField]
        private LeaderboardEntry _entryPrefab;

        private readonly Dictionary<GorillaAvatar, LeaderboardEntry> _entries = new Dictionary<GorillaAvatar, LeaderboardEntry>();

        private void Start() {
            _gorillaPlayerManager.playerJoined += OnPlayerJoined;
            _gorillaPlayerManager.playerLeft += OnPlayerLeft;
        }

        private void OnDestroy() {
            _gorillaPlayerManager.playerJoined -= OnPlayerJoined;
            _gorillaPlayerManager.playerLeft -= OnPlayerLeft;
        }

        private void OnPlayerJoined(GorillaAvatar avatar, bool isLocalPlayer) {
            // Create an entry and add it to the dictionary
            var entry = Instantiate(_entryPrefab, _entryContainer.transform);
            entry.Initialize(avatar, isLocalPlayer);
            _entries.Add(avatar, entry);
        }

        private void OnPlayerLeft(GorillaAvatar avatar, bool isLocalPlayer) {
            // Remove from dictionary and destroy the entry GameObject
            if (_entries.Remove(avatar, out var entry)) {
                Destroy(entry.gameObject);
            }
        }
    }
}
