using System;
using Normal.Realtime;
using UnityEngine;

namespace Normal.Utility {
    /// <summary>
    /// <para>
    /// This is a script that ensure that its RealtimeView always has an owner.
    /// An initial owner is chosen randomly and ownership is distributed randomly
    /// to another client when the current owner leaves.
    /// </para>
    /// <para>
    /// This is useful for components that have one global instance, like gamemode managers.
    /// </para>
    /// </summary>
    /// <remarks>
    /// When the owner disconnects, migration to a new owner is seamless as long as
    /// the target component's state is entirely contained inside the datastore,
    /// and not cached locally on that owner.
    /// </remarks>
    [RequireComponent(typeof(RealtimeView))]
    public class AutoDistributeViewOwnership : MonoBehaviour {
        /// <summary>
        /// The view on this GameObject.
        /// </summary>
        private RealtimeView _view;

        /// <inheritdoc cref="_isLocallyOwnedConfirmed"/>
        private bool _isLocallyOwnedConfirmed;

        /// <summary>
        /// True when the local client is the confirmed owner of the view.
        /// So when true, it's safe to go ahead and modify the view and its child views/components.
        /// </summary>
        public bool isLocallyOwnedConfirmed {
            get => _isLocallyOwnedConfirmed;
            private set {
                if (value == _isLocallyOwnedConfirmed) {
                    return;
                }

                _isLocallyOwnedConfirmed = value;
                isLocallyOwnedConfirmedDidChange?.Invoke(value);
            }
        }

        /// <summary>
        /// Dispatched when <see cref="isLocallyOwnedConfirmed"/> changes.
        /// </summary>
        public event Action<bool> isLocallyOwnedConfirmedDidChange;

        private void Awake() {
            _view = GetComponent<RealtimeView>();
        }

        private void Update() {
            if (_view.realtime == null || _view.realtime.connected == false) {
                isLocallyOwnedConfirmed = false;
                return;
            }

            CheckOwner();
        }

        /// <summary>
        /// Check if lost ownership.
        /// Try to claim ownership if unowned.
        /// </summary>
        private void CheckOwner() {
            if (_view.isOwnedLocallySelf == false) {
                isLocallyOwnedConfirmed = false;
            }

            if (_view.isUnownedSelf) {
                TryClaimOwnership();
            }
        }

        /// <summary>
        /// Claim ownership:
        /// Normcore will optimistically update the ownership locally, but that's
        /// not guaranteed to be reflected on the server since another client can beat us to it.
        /// This is why we check again a second later to see if our client won the race or not.
        /// </summary>
        private void TryClaimOwnership() {
            _view.RequestOwnership();

            // If multiple clients are currently racing to request ownership,
            // this tells the server to grant ownership to the first request that arrives and discard the rest
            _view.preventOwnershipTakeover = true;

            Invoke(nameof(ConfirmOwnership), 1f);
        }

        /// <summary>
        /// Verify the result of our ownership request.
        /// </summary>
        private void ConfirmOwnership() {
            if (_view.isOwnedLocallySelf) {
                isLocallyOwnedConfirmed = true;
            }
        }
    }
}
