using System;
using System.Diagnostics;
using Normal.Realtime;
using Normal.Realtime.Serialization;
using Normal.Utility;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Normal.GorillaTemplate.Infection {
    /// <summary>
    /// Runs logic for the infection game mode.
    /// This is a singleton and uses <see cref="AutoDistributeViewOwnership"/>, meaning it always has an owner.
    /// Ownership transfer is seamless because all of its state is contained in the datastore.
    /// </summary>
    [RequireComponent(typeof(AutoDistributeViewOwnership))]
    public class InfectionGameMode : RealtimeSingleton<InfectionGameMode, InfectionGameModeModel> {
        public enum State {
            /// <summary>
            /// Waiting for more players to join before starting the game.
            /// </summary>
            WaitingForPlayers,

            /// <summary>
            /// The game is running.
            /// </summary>
            Started,

            /// <summary>
            /// The game has ended and we're waiting to reset it.
            /// </summary>
            Ended,

            /// <summary>
            /// The gamemode is entirely disabled.
            /// </summary>
            Disabled,
        }

        public State? CurrentState => model?.state;

        public bool GameModeIsDisabled {
            get {
                if (model == null) {
                    return false;
                }

                return model.state == State.Disabled;
            }

            set {
                if (model == null) {
                    Debug.LogWarning($"{nameof(GameModeIsDisabled)} setter: No model assigned yet, skipping");
                    return;
                }

                if (!isOwnerConfirmed) {
                    Debug.LogWarning($"{nameof(GameModeIsDisabled)} setter: Only the confirmed owner can set the value, skipping");
                    return;
                }

                model.state = value ? State.Disabled : State.WaitingForPlayers;
            }
        }

        [SerializeField]
        private Realtime.Realtime _realtime;

        [SerializeField]
        private GorillaPlayerManager _gorillaPlayerManager;

        /// <summary>
        /// In tag mode only a single player needs to be infected for the match to end.
        /// This is more interesting to play than infection when there aren't a lot of players in the match.
        /// </summary>
        [SerializeField]
        private int _minNumberOfPlayersToStartTag = 2;

        /// <summary>
        /// In infection mode all players need to be infected for the match to end.
        /// </summary>
        [SerializeField]
        private int _minNumberOfPlayersToStartInfection = 4;

        /// <summary>
        /// When set to true, detailed messages are printed to the console about ex
        /// infection requests and match state changes. For debugging this class.
        /// </summary>
        [SerializeField]
        private bool _debugLog;

        private AutoDistributeViewOwnership _autoDistributeOwnership;
        private bool isOwnerConfirmed => _autoDistributeOwnership.isLocallyOwnedConfirmed;

        protected override void OnRealtimeModelReplaced(InfectionGameModeModel previousModel, InfectionGameModeModel currentModel) {
            // Are we creating this model?
            if (currentModel != null && currentModel.isFreshModel) {
                // Is the component enabled?
                if (!isActiveAndEnabled) {
                    currentModel.state = State.Disabled;
                }
            }
        }

        private void Start() {
            _autoDistributeOwnership = GetComponent<AutoDistributeViewOwnership>();

            _gorillaPlayerManager.playerJoined += OnPlayerJoined;
            _gorillaPlayerManager.playerLeft += OnPlayerLeft;
        }

        protected override void OnDestroy() {
            _gorillaPlayerManager.playerJoined -= OnPlayerJoined;
            _gorillaPlayerManager.playerLeft -= OnPlayerLeft;

            base.OnDestroy();
        }

        private void OnPlayerJoined(GorillaAvatar avatar, bool isLocalPlayer) {
            if (avatar.TryGetComponent<InfectionHitboxGroup>(out var infectionHitboxGroup)) {
                if (isLocalPlayer) {
                    // Disable hitboxes on the local player
                    infectionHitboxGroup.SetHitboxesEnabled(false);
                } else {
                    // Associate the sync component with the hitboxes
                    var infectionPlayerSync = avatar.GetComponent<InfectionPlayerSync>();
                    infectionHitboxGroup.SetSync(infectionPlayerSync);
                }
            }

            // Only proceed if we're the owner
            if (!isOwnerConfirmed) {
                return;
            }

            // Infect new players (unless playing regular tag and not infection)
            if (_gorillaPlayerManager.avatars.Count >= _minNumberOfPlayersToStartInfection && GameModeIsDisabled == false) {
                var avatarRealtimeView = avatar.GetComponent<RealtimeView>();
                SetIsInfected(avatarRealtimeView.ownerIDSelf, true);
            }
        }

        private void OnPlayerLeft(GorillaAvatar avatar, bool isLocalPlayer) {
            // Only proceed if we're the owner
            if (!isOwnerConfirmed) {
                return;
            }

            // Ensure the player is no longer in the infected set
            var avatarRealtimeView = avatar.GetComponent<RealtimeView>();
            SetIsInfected(avatarRealtimeView.ownerIDSelf, false);
        }

        private void Update() {
            if (!realtime.connected || model == null) {
                return;
            }

            // Only proceed if we're the owner
            if (!isOwnerConfirmed) {
                return;
            }

            var state = model.state;

            if (state == State.WaitingForPlayers) {
                TryStartGame();
            } else if (state == State.Started) {
                EnsureOneInfected();
                TryEndGame();
            } else if (state == State.Ended) {
                TryResetGame();
            } else if (state == State.Disabled) {
                TryDisableGame();
            }
        }

        /// <summary>
        /// If no players are infected (ex because a player left),
        /// infect a random player.
        /// </summary>
        private void EnsureOneInfected() {
            if (NumInfected() == 0) {
                InfectRandomPlayer();
            }
        }

        /// <summary>
        /// Start the game when we have enough players.
        /// </summary>
        private void TryStartGame() {
            if (_gorillaPlayerManager.avatars.Count >= _minNumberOfPlayersToStartTag) {
                model.state = State.Started;
                DebugLog($"Started game");
                EnsureOneInfected();
            }
        }

        /// <summary>
        /// If enough players are infected (according to tag or infection rules) then schedule a new round.
        /// </summary>
        private void TryEndGame() {
            var numPlayers = _gorillaPlayerManager.avatars.Count;
            var numInfected = NumInfected();

            var allInfected = numInfected == numPlayers;
            var tagInfected = numPlayers < _minNumberOfPlayersToStartInfection && numInfected >= 2;

            if (allInfected || tagInfected) {
                model.state = State.Ended;
                model.scheduledResetTime = _realtime.roomTime + 5.0; // Wait 5 seconds
                DebugLog($"Ended game");
            }
        }

        /// <summary>
        /// Starts the new round.
        /// </summary>
        private void TryResetGame() {
            if (_realtime.roomTime >= model.scheduledResetTime) {
                model.state = State.WaitingForPlayers;
                DebugLog($"Reset game");

                ClearInfected();

                // Run an iteration of TryStartGame() right away to prevent infection skin flicker due to a 1-frame delay
                TryStartGame();
            }
        }

        /// <summary>
        /// Disables the gamemode (resets state).
        /// </summary>
        private void TryDisableGame() {
            if (NumInfected() > 0) {
                DebugLog($"Reset game");

                ClearInfected();
            }
        }

        /// <summary>
        /// Infects a random player.
        /// </summary>
        private void InfectRandomPlayer() {
            var randIdx = Random.Range(0, _gorillaPlayerManager.avatars.Count);
            var idx = 0;

            // Iterate until we reach the pair at idx
            foreach (var pair in _gorillaPlayerManager.avatars) {
                if (idx == randIdx) {
                    DebugLog($"Randomly infected {pair.Key}");
                    SetIsInfected(pair.Key, true);
                    break;
                }

                idx++;
            }
        }

        /// <summary>
        /// Called when the local player touches another player's hitbox.
        /// This will generate a request that will be processed by the owner of the game mode.
        /// </summary>
        public void RequestToInfectOtherPlayer(InfectionPlayerSync remotePlaySync) {
            if (!realtime.connected || model == null) {
                return;
            }

            if (model.state != State.Started) {
                return;
            }

            // Get the local avatar and sync component
            if (_gorillaPlayerManager.avatars.TryGetValue(_realtime.clientID, out var localAvatar)) {
                if (localAvatar.TryGetComponent<InfectionPlayerSync>(out var localPlayerSync)) {
                    // Quick filter based on infection states of both players
                    if (IsInfected(localPlayerSync.ownerIDInHierarchy) &&
                        !IsInfected(remotePlaySync.ownerIDInHierarchy)) {
                        DebugLog($"Local player requested to infect {remotePlaySync.ownerIDInHierarchy}");

                        // This will generate a request that will be processed by the owner of the game mode
                        localPlayerSync.InfectOtherPlayer(remotePlaySync.ownerIDInHierarchy);
                    }
                }
            }
        }

        /// <summary>
        /// Runs on the game mode owner to process an infection request.
        /// </summary>
        public void TryInfectPlayer(int sourceClientID, int targetClientID) {
            if (!realtime.connected || model == null) {
                return;
            }

            // Only proceed if we're the owner
            if (!isOwnerConfirmed) {
                return;
            }

            // Only proceed if the game has started
            if (model.state != State.Started) {
                return;
            }

            // Quick filter based on infection states of both players
            if (IsInfected(sourceClientID) &&
                !IsInfected(targetClientID)) {
                DebugLog($"{sourceClientID} infected {targetClientID}");

                // Apply the infection
                SetIsInfected(targetClientID, true);
            }
        }

        /// <summary>
        /// Prints a debug message to the console.
        /// Note that it will be compiled out entirely in release builds and will avoid any overhead.
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void DebugLog(string message) {
            if (_debugLog) {
                Debug.Log($"{nameof(InfectionGameMode)}: {message}");
            }
        }

#region Infected Set

        /// <summary>
        /// Returns true if the specified player is infected.
        /// </summary>
        public bool IsInfected(int clientID) {
            if (model == null) {
                return false;
            }

            return model.infectedSet.ContainsKey((uint)clientID);
        }

        /// <summary>
        /// Returns the number of infected players.
        /// </summary>
        private int NumInfected() {
            if (model == null) {
                return 0;
            }

            return model.infectedSet.Count;
        }

        /// <summary>
        /// Sets the infection status on the specified player.
        /// </summary>
        /// <remarks>
        /// This should only be called on the owner of the game mode.
        /// </remarks>
        private void SetIsInfected(int clientID, bool infected) {
            if (model == null) {
                return;
            }

            // Only proceed if we're the owner
            if (!isOwnerConfirmed) {
                throw new Exception($"Only the confirmed owner should call {nameof(SetIsInfected)}");
            }

            var key = (uint)clientID;

            if (model.infectedSet.ContainsKey(key)) {
                if (!infected) {
                    model.infectedSet.Remove(key);
                }
            } else {
                if (infected) {
                    model.infectedSet.Add(key, new InfectionSetEntry());
                }
            }
        }

        /// <summary>
        /// Clears infection status on all players.
        /// </summary>
        /// <remarks>
        /// This should only be called on the owner of the game mode.
        /// </remarks>
        private void ClearInfected() {
            if (model == null) {
                return;
            }

            // Only proceed if we're the owner
            if (!isOwnerConfirmed) {
                throw new Exception($"Only the confirmed owner should call {nameof(ClearInfected)}");
            }

            foreach (var pair in model.infectedSet) {
                model.infectedSet.Remove(pair.Key);
            }
        }

#endregion
    }

    [RealtimeModel(true)]
    public partial class InfectionGameModeModel {
        /// <summary>
        /// The current state of the game.
        /// </summary>
        [RealtimeProperty(1, true)]
        private InfectionGameMode.State _state;

        /// <summary>
        /// The scheduled time of the next round start.
        /// This is only used when <see cref="_state"/> is <see cref="InfectionGameMode.State.Ended"/>.
        /// </summary>
        [RealtimeProperty(2, true)]
        private double _scheduledResetTime;

        /// <summary>
        /// A dictionary treated as a set.
        /// If a player is infected, they have an entry in this dictionary.
        /// If a player isn't infected, they don't have an entry in this dictionary.
        /// </summary>
        [RealtimeProperty(3, true)]
        private RealtimeDictionary<InfectionSetEntry> _infectedSet;
    }

    /// <summary>
    /// A placeholder model (RealtimeDictionary requires a model).
    /// </summary>
    [RealtimeModel]
    public partial class InfectionSetEntry { }
}
