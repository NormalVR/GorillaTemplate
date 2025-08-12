using System;
using System.Collections.Generic;
using Normal.Realtime;
using UnityEngine;

namespace Normal.GorillaTemplate {
    /// <summary>
    /// An object that can be grabbed by <see cref="grabberHand"/>.
    /// If the object has a <see cref="Rigidbody"/> then the hand will be able to throw the object.
    /// </summary>
    [RequireComponent(typeof(RealtimeTransform))]
    public class GrabbableObject : RealtimeComponent<GrabbableObjectModel> {
        /// <summary>
        /// The Rigidbody of the object (optional).
        /// </summary>
        [SerializeField]
        private Rigidbody _rigidbody;

        /// <summary>
        /// The point on the object that the hand should grab (optional).
        /// If unset, then the Transform of the object is used instead.
        /// </summary>
        [SerializeField]
        private Transform _grabAnchor;

        /// <summary>
        /// Multiplies the result of <see cref="ThrowSettings.maxLinearVelocityByWeight"/>.
        /// Think of this as scaling the "muscle strength" of the character for this object specifically.
        /// </summary>
        [SerializeField]
        private float _maxLinearVelocityMultiplier = 1f;

        /// <summary>
        /// Multiplies the result of <see cref="ThrowSettings.maxAngularVelocityByWeight"/>.
        /// Think of this as scaling the "muscle strength" of the character for this object specifically.
        /// </summary>
        [SerializeField]
        private float _maxAngularVelocityMultiplier = 1f;

        /// <summary>
        /// When true, this object is released when the hand's release action is performed a second time.
        /// </summary>
        [SerializeField]
        private bool _useStickyGrab;

        /// <summary>
        /// The <see cref="Rigidbody"/> of this object, if any.
        /// </summary>
        public Rigidbody physicsBody => _rigidbody;

        /// <inheritdoc cref="_maxLinearVelocityMultiplier"/>
        public float maxLinearVelocityMultiplier => _maxLinearVelocityMultiplier;

        /// <inheritdoc cref="_maxAngularVelocityMultiplier"/>
        public float maxAngularVelocityMultiplier => _maxAngularVelocityMultiplier;

        /// <inheritdoc cref="_useStickyGrab"/>
        public bool useStickyGrab {
            get => _useStickyGrab;
            set => _useStickyGrab = value;
        }

        private RealtimeTransform _realtimeTransform;

        /// <summary>
        /// The <see cref="RealtimeTransform"/> on this object.
        /// It synchronizes the position, rotation, and physics of the object.
        /// </summary>
        public RealtimeTransform realtimeTransform => _realtimeTransform;

        /// <summary>
        /// The point on the object that the hand will grab.
        /// </summary>
        /// <seealso cref="_grabAnchor"/>
        public Transform effectiveGrabAnchor {
            get {
                if (_grabAnchor != null) {
                    return _grabAnchor;
                } else {
                    return transform;
                }
            }
        }

        /// <summary>
        /// The unique network ID of this object.
        /// This is used to synchronize grabbing over the network.
        /// </summary>
        public string networkID => model.networkID;

        private GrabberHand _grabberHand;

        /// <summary>
        /// <para>
        /// Returns the hand that is currently holding the object, if any.
        /// </para>
        ///
        /// <para>
        /// Setting this to a different hand triggers a few operations:
        /// First the current hand (if any) releases the object.
        /// Then the new hand (if any) grabs the object.
        /// </para>
        ///
        /// <para>
        /// Setting this property to null will effectively release the object from its current hand.
        /// </para>
        /// </summary>
        /// <remarks>
        /// This property is synchronized over the network.
        /// Only a hand that is owned locally can be assigned to this property.
        /// </remarks>
        /// <seealso cref="GrabberHand.grabbedObject"/>
        public GrabberHand grabberHand {
            get => _grabberHand;
            set {
                // Is it the same hand?
                if (value == _grabberHand) {
                    return;
                }

                // Release the object from the previous hand
                if (_grabberHand != null) {
                    _grabberHand.grabbedObject = null;
                }

                // Grab the object with the new hand
                if (value != null) {
                    value.grabbedObject = this;
                }
            }
        }

        /// <summary>
        /// True if a hand is currently hovering the object.
        /// </summary>
        /// <remarks>
        /// Hovering is only of interest to the local player, and isn't synchronized over the network.
        /// </remarks>
        public bool isHovered => _hoverHands.Count > 0;

        private readonly HashSet<GrabberHand> _hoverHands = new HashSet<GrabberHand>();

        /// <summary>
        /// The hands currently hovering the object.
        /// There can be more than one at a time, ex both the player's left and right hands.
        /// </summary>
        /// <remarks>
        /// Hovering is only of interest to the local player, and isn't synchronized over the network.
        /// </remarks>
        public IEnumerable<GrabberHand> hoverHands => _hoverHands;

        public delegate void GrabEventDelegate(GrabberHand grabberHand);
        public delegate void HoverEventDelegate(GrabberHand grabberHand);

        /// <summary>
        /// Invoked when the object is grabbed by a hand.
        /// </summary>
        /// <remarks>
        /// This event is synchronized over the network.
        /// </remarks>
        public event GrabEventDelegate onGrab;

        /// <summary>
        /// Invoked when the object is released by a hand.
        /// </summary>
        /// <remarks>
        /// This event is synchronized over the network.
        /// </remarks>
        public event GrabEventDelegate onRelease;

        /// <summary>
        /// Invoked when this object starts being hovered by a hand.
        /// Note that an object can be hovered by multiple hands at a time (ex the player's left and right hands).
        /// </summary>
        /// <remarks>
        /// Hovering is only of interest to the local player, and isn't synchronized over the network.
        /// </remarks>
        public event HoverEventDelegate onHoverEnter;

        /// <summary>
        /// Invoked when this object stops being hovered by a hand.
        /// Note that an object can be hovered by multiple hands at a time (ex the player's left and right hands).
        /// </summary>
        /// <remarks>
        /// Hovering is only of interest to the local player, and isn't synchronized over the network.
        /// </remarks>
        public event HoverEventDelegate onHoverExit;

        /// <summary>
        /// If <see cref="_grabAnchor"/> is set, then this is the inverse offset in the object's local space (without scale).
        /// </summary>
        private Pose? _grabAnchorOffset;

        private RigidbodyInterpolation _rigidbodyInterpolation;

        protected virtual void Awake() {
            _realtimeTransform = GetComponent<RealtimeTransform>();

            if (TryGetGrabAnchorOffset(out var pose)) {
                _grabAnchorOffset = pose;
            } else {
                _grabAnchorOffset = null;
            }
        }

        protected virtual void Start() {
            if (_rigidbody != null) {
                _rigidbodyInterpolation = _rigidbody.interpolation;
            }
        }

        protected virtual void OnDestroy() {
            if (_grabberHand != null) {
                _grabberHand._NotifyGrabbableObjectDestroyedInternal(this);
            }

            foreach (var hand in _hoverHands) {
                hand._NotifyGrabbableObjectDestroyedInternal(this);
            }

            _grabberHand = null;
            _hoverHands.Clear();
        }

        protected override void OnRealtimeModelReplaced(GrabbableObjectModel previousModel, GrabbableObjectModel currentModel) {
            if (previousModel != null) {
                if (realtime != null) {
                    if (realtime.TryGetComponent(out GrabManager grabManager)) {
                        // This object no longer represents the previous model, unregister it
                        grabManager._UnregisterInternal(previousModel.networkID);
                    } else {
                        Debug.LogError($"Missing {nameof(GrabManager)} component on {realtime.gameObject.name}");
                    }
                }
            }

            if (currentModel != null) {
                // This is a fresh model - meaning it's created by the local client and we need to initialize its properties
                if (currentModel.isFreshModel) {
                    // Generate a unique ID
                    currentModel.networkID = Guid.NewGuid().ToString();
                }

                if (realtime != null) {
                    if (realtime.TryGetComponent(out GrabManager grabManager)) {
                        // This object represents the current model, register it
                        grabManager._RegisterInternal(currentModel.networkID, this);
                    } else {
                        Debug.LogError($"Missing {nameof(GrabManager)} component on {realtime.gameObject.name}");
                    }
                }
            }
        }

        public void _HoverEnterInternal(GrabberHand hand) {
            _hoverHands.Add(hand);
            onHoverEnter?.Invoke(hand);
        }

        public void _HoverExitInternal(GrabberHand hand) {
            _hoverHands.Remove(hand);
            onHoverExit?.Invoke(hand);
        }

        public void _GrabInternal(GrabberHand hand) {
            // Request exclusive ownership over this object:
            // If two clients grab the object at the same time, only one will be granted ownership.
            // The losing client will perform a correction in GrabberHand.SyncGrabbableObject().
            realtimeView.RequestOwnership();
            realtimeView.preventOwnershipTakeover = true;

            // To move a RealtimeTransform we first need to take ownership
            _realtimeTransform.RequestOwnership();

            _grabberHand = hand;

            if (_rigidbody != null) {
                // Set the Rigidbody to kinematic so that it can follow the hand
                _rigidbody.isKinematic = true;

                // Turn off interpolation to follow the hand as closely as possible
                _rigidbody.interpolation = RigidbodyInterpolation.None;
            }

            onGrab?.Invoke(_grabberHand);
        }

        public void _GrabNetworkedInternal(GrabberHand hand) {
            _grabberHand = hand;

            onGrab?.Invoke(_grabberHand);
        }

        public void _ReleaseInternal(Vector3 linearVelocity, Vector3 angularVelocity, Vector3 handTrackingPosition, ThrowSettings throwSettings) {
            // Release ownership over this object.
            // No need to clear preventOwnershipTakeover, as unowned models will allow ownership to go through regardless.
            realtimeView.ClearOwnership();

            // We also don't clear ownership on the RealtimeTransform, because we need to keep simulating it after the release/throw.
            // But its ownership isn't exclusive, so other clients can still catch/grab the object mid-flight.

            if (_rigidbody != null) {
                var throwVelocities = throwSettings.ModifyHandVelocitiesForThrow(this, new ThrowSettings.Velocities() {
                    linearVelocity = linearVelocity,
                    angularVelocity = angularVelocity,
                });

                // Simulate the Rigidbody as a normal body
                _rigidbody.isKinematic = false;

                // Restore interpolation
                _rigidbody.interpolation = _rigidbodyInterpolation;

                // Adjust the object's linear velocity to account for the hand rotation.
                // In other words, the tangential velocity induced by rotation at the hand tracking
                // position relative to the rigidbody’s center of mass.
                var positionDelta = handTrackingPosition - _rigidbody.worldCenterOfMass;
                var additionalLinearVelocity = -Vector3.Cross(throwVelocities.angularVelocity, positionDelta);
                var objectLinearVelocity = throwVelocities.linearVelocity + additionalLinearVelocity;

                // Apply velocities
                _rigidbody.linearVelocity = objectLinearVelocity;
                _rigidbody.angularVelocity = throwVelocities.angularVelocity;
            }

            onRelease?.Invoke(_grabberHand);

            _grabberHand = null;
        }

        public void _ReleaseNetworkedInternal() {
            onRelease?.Invoke(_grabberHand);

            _grabberHand = null;
        }

        public void _StickToGrabberInternal(GrabberHand targetHand) {
            var handTransform = targetHand.transform;
            var t = transform;

            if (_grabAnchorOffset != null) {
                // This gives us the hand pose while excluding the hand scale
                var handPose = new Pose(handTransform.position, handTransform.rotation);

                // Transform the inverse offset from object-local space to world space
                var objectPoseWithAnchorOffset = _grabAnchorOffset.Value.GetTransformedBy(handPose);

                t.SetPositionAndRotation(objectPoseWithAnchorOffset.position, objectPoseWithAnchorOffset.rotation);
            } else {
                t.SetPositionAndRotation(handTransform.position, handTransform.rotation);
            }
        }

        private bool TryGetGrabAnchorOffset(out Pose offset) {
            if (_grabAnchor == null) {
                offset = default;
                return false;
            }

            var t = transform;

            // Get world-space poses
            var objectPose = new Pose(t.position, t.rotation);
            var anchorPose = new Pose(_grabAnchor.position, _grabAnchor.rotation);

            // Get the offset from anchor to object in object-local space (without scale)
            // var anchorOffset = InvertPose(anchorPose).GetTransformedBy(objectPose);
            var anchorOffset = anchorPose.GetTransformedBy(InvertPose(objectPose));

            // Invert the anchor offset since we'll be offsetting the object to line up the anchor with the hand
            offset = InvertPose(anchorOffset);
            return true;
        }

        /// <summary>
        /// Inverts a pose (returns pose⁻¹).
        /// </summary>
        private static Pose InvertPose(Pose pose) {
            var inverseRotation = Quaternion.Inverse(pose.rotation);
            var inversePosition = inverseRotation * -pose.position;
            return new Pose(inversePosition, inverseRotation);
        }

        /// <summary>
        /// Override this function to prevent the object from being hovered or grabbed.
        /// In other words, this function can selectively disable grabbing on the object.
        /// </summary>
        public virtual bool CanBeGrabbed(GrabberHand hand) {
            return true;
        }

        public void _NotifyGrabberHandDestroyedInternal(GrabberHand hand) {
            // Invoke the GrabbableObject callbacks (but skip the GrabberHand callbacks)

            if (hand == _grabberHand) {
                _grabberHand = null;
                onRelease?.Invoke(hand);
            }

            if (_hoverHands.Contains(hand)) {
                _hoverHands.Remove(hand);
                onHoverExit?.Invoke(hand);
            }
        }
    }

    /// <summary>
    /// The <see cref="GrabbableObject"/> data that is synchronized over the network.
    /// </summary>
    [RealtimeModel(true)]
    public partial class GrabbableObjectModel {
        /// <summary>
        /// A property that synchronizes <see cref="GrabbableObject.networkID"/>.
        /// </summary>
        [RealtimeProperty(1, true)]
        private string _networkID;
    }
}
