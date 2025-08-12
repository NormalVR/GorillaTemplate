using Normal.Realtime;
using UnityEngine;

namespace Normal.Utility {
    /// <summary>
    /// This is an interface that a <see cref="RealtimeComponent{TModel}"/> should implement
    /// to be compatible with <see cref="AutoDistributeOwnership"/>.
    /// </summary>
    public interface IAutoDistributedOwnership : IRealtimeComponent {
        /// <summary>
        /// Use the getter to see if the local client is the confirmed owner of this component.
        /// So when true, it's safe to go ahead and modify the component's model.
        /// </summary>
        bool isOwnerConfirmed { get; set; }
    }

    /// <summary>
    /// <para>
    /// Note: <see cref="AutoDistributeViewOwnership"/> is a simpler and better version of this component.
    /// </para>
    /// <para>
    /// This is a script that can accompany any <see cref="RealtimeComponent{TModel}"/>
    /// and ensure that it always has an owner.
    /// An initial owner is chosen randomly and ownership is distributed randomly
    /// to another client when the current owner leaves.
    /// </para>
    /// <para>
    /// This is useful for components that have one global instance, like gamemode managers.
    /// The target component must implement the <see cref="IAutoDistributedOwnership"/> interface.
    /// </para>
    /// </summary>
    /// <remarks>
    /// When the owner disconnects, migration to a new owner is seamless as long as
    /// the target component's state is entirely contained inside the datastore,
    /// and not cached locally on that owner.
    /// </remarks>
    public class AutoDistributeOwnership : MonoBehaviour {
        /// <summary>
        /// The target component whose ownership will be managed.
        /// <remarks>
        /// This is casted to the <see cref="IAutoDistributedOwnership"/> interface at runtime,
        /// but cannot be serialized as an interface which is why it's a <see cref="MonoBehaviour"/>.
        /// </remarks>
        /// </summary>
        [SerializeField]
        private MonoBehaviour _target;

        /// <summary>
        /// <see cref="_target"/> casted to an interface.
        /// </summary>
        private IAutoDistributedOwnership _component;

        private void Awake() {
            _component = (IAutoDistributedOwnership)_target;
        }

        private void Update() {
            if (_component.realtime == null ||
                !_component.realtime.connected) {
                return;
            }

            CheckOwner();
        }

        /// <summary>
        /// Check if lost ownership.
        /// Try to claim ownership if unowned.
        /// </summary>
        private void CheckOwner() {
            if (!_component.isOwnedLocallySelf) {
                _component.isOwnerConfirmed = false;
            }

            if (_component.isUnownedSelf) {
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
            _component.realtimeView.RequestOwnership();

            // If multiple clients are currently racing to request ownership,
            // this tells the server to grant ownership to the first request that arrives and discard the rest
            _component.realtimeView.preventOwnershipTakeover = true;

            _component.RequestOwnership();

            Invoke(nameof(ConfirmOwnership), 1f);
        }

        /// <summary>
        /// Verify the result of our ownership request.
        /// </summary>
        private void ConfirmOwnership() {
            if (_component.isOwnedLocallySelf) {
                _component.isOwnerConfirmed = true;
            }
        }
    }
}
