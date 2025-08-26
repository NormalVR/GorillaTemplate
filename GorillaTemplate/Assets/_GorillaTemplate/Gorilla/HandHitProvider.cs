using GorillaLocomotion;
using UnityEngine;

namespace Normal.GorillaTemplate {
    /// <summary>
    /// Dispatches events when the player's hands collide with a surface.
    /// </summary>
    public class HandHitProvider : MonoBehaviour {
        /// <summary>
        /// Specifies the left or right hand.
        /// </summary>
        public enum Hand {
            Left,
            Right,
        }

        /// <summary>
        /// Represents a player's hand state.
        /// </summary>
        private struct HandState {
            /// <summary>
            /// Specifies the left or right hand.
            /// </summary>
            public Hand hand;

            /// <summary>
            /// Last cached state.
            /// </summary>
            public bool previousWasTouching;

            /// <summary>
            /// For keeping track of the cooldown.
            /// </summary>
            public float lastHitTime;

            /// <summary>
            /// The collision info.
            /// </summary>
            public RaycastHit hitInfo;
        }

        /// <summary>
        /// The player locomotion script that provides hand data.
        /// </summary>
        [SerializeField]
        private Player _player;

        /// <summary>
        /// A cooldown time before another effect can be triggered on the same hand.
        /// </summary>
        [SerializeField]
        private float _cooldownSeconds = 0.25f;

        /// <summary>
        /// The left hand configuration.
        /// </summary>
        private HandState _leftHand = new HandState() {
            hand = Hand.Left,
        };

        /// <summary>
        /// The right hand configuration.
        /// </summary>
        private HandState _rightHand = new HandState() {
            hand = Hand.Right,
        };

        public delegate void OnHitDelegate(Hand hand, RaycastHit hitInfo);
        public event OnHitDelegate onHit;

        private void Update() {
            if (ShouldHit(ref _leftHand, _player.wasLeftHandTouching)) {
                _leftHand.hitInfo = _player.leftHandHitInfo;
                DoHit(ref _leftHand);
            }

            if (ShouldHit(ref _rightHand, _player.wasRightHandTouching)) {
                _rightHand.hitInfo = _player.rightHandHitInfo;
                DoHit(ref _rightHand);
            }

            _leftHand.previousWasTouching = _player.wasLeftHandTouching;
            _rightHand.previousWasTouching = _player.wasRightHandTouching;
        }

        private bool ShouldHit(ref HandState state, bool currentlyTouching) {
            // Check if the hand has just touched something
            if (currentlyTouching && !state.previousWasTouching) {
                // Check the cooldown
                var timeDifference = Time.time - state.lastHitTime;
                return timeDifference > _cooldownSeconds;
            }

            return false;
        }

        private void DoHit(ref HandState state) {
            state.lastHitTime = Time.time;
            onHit?.Invoke(state.hand, state.hitInfo);
        }
    }
}
