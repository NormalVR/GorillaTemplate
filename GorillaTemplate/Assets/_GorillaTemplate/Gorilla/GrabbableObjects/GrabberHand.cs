using Normal.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Normal.GorillaTemplate {
    /// <summary>
    /// A hand that can grab a <see cref="GrabbableObject"/>.
    /// </summary>
    public class GrabberHand : RealtimeComponent<GrabberHandModel> {
        /// <summary>
        /// The character that this hand belongs to.
        /// </summary>
        [SerializeField]
        private Grabber _grabber;

        /// <inheritdoc cref="_grabber"/>
        public Grabber grabber => _grabber;

        /// <summary>
        /// Settings for object hover detection.
        /// </summary>
        [SerializeField]
        private GrabbableObjectDetector.Settings _objectDetectorSettings = GrabbableObjectDetector.Settings.defaultValue;

        /// <summary>
        /// True if this is the right hand of the <see cref="grabber">character</see>.
        /// </summary>
        public bool isRightHand {
            get;
            private set;
        }

        /// <summary>
        /// <para>
        /// Returns the object that the hand is currently holding, if any.
        /// </para>
        ///
        /// <para>
        /// Setting this to a different object triggers a few operations:
        /// First, the currently held object is released.
        /// Then the new object (if any) is released from the hand holding it (if any).
        /// Finally the new object (if any) is grabbed by this hand.
        /// </para>
        ///
        /// <para>
        /// Setting this property to null will effectively release the currently held object.
        /// </para>
        /// </summary>
        /// <remarks>
        /// This property is synchronized over the network.
        /// This property can only be changed on the client that has ownership of the hand.
        /// </remarks>
        public GrabbableObject grabbedObject {
            get => _grabbedObject;
            set {
                // Is it the same object?
                if (value == _grabbedObject) {
                    return;
                }

                // Release the previous object
                if (_grabbedObject != null) {
                    Release(_grabbedObject);
                }

                if (value != null) {
                    // Release the new object from the other hand
                    if (value.grabberHand != null) {
                        value.grabberHand.grabbedObject = null;
                    }

                    // Grab the new object
                    Grab(value);
                }
            }
        }

        public delegate void GrabEventDelegate(GrabbableObject grabbableObject);
        public delegate void HoverEventDelegate(GrabbableObject grabbableObject);

        /// <summary>
        /// Invoked when the hand grabs an object.
        /// </summary>
        /// <remarks>
        /// This event is synchronized over the network.
        /// </remarks>
        public event GrabEventDelegate onGrab;

        /// <summary>
        /// Invoked when the hand releases an object.
        /// </summary>
        /// <remarks>
        /// This event is synchronized over the network.
        /// </remarks>
        public event GrabEventDelegate onRelease;

        /// <summary>
        /// Invoked when this hand starts hovering an object.
        /// Note that a hand can only hover one object at a time, but an object can be hovered by multiple hands
        /// (ex the player's left and right hands).
        /// </summary>
        /// <remarks>
        /// Hovering is only of interest to the local player, and isn't synchronized over the network.
        /// </remarks>
        public event HoverEventDelegate onHoverEnter;

        /// <summary>
        /// Invoked when this hand stops hovering an object.
        /// Note that a hand can only hover one object at a time, but an object can be hovered by multiple hands
        /// (ex the player's left and right hands).
        /// </summary>
        /// <remarks>
        /// Hovering is only of interest to the local player, and isn't synchronized over the network.
        /// </remarks>
        public event HoverEventDelegate onHoverExit;

        private GrabbableObject _grabbedObject;

        private readonly GrabbableObjectDetector _grabbableObjectDetector = new GrabbableObjectDetector();

        /// <summary>
        /// The object currently being hovered by this hand.
        /// </summary>
        /// <remarks>
        /// Hovering is only of interest to the local player, and isn't synchronized over the network.
        /// </remarks>
        public GrabbableObject hoveredObject => _grabbableObjectDetector.hoveredObject;

        /// <summary>
        /// The input action that makes the hand grab the currently hovered object (when performed),
        /// and release the currently grabbed object (when canceled).
        /// </summary>
        [SerializeField]
        private InputActionProperty _grabInputAction;

        /// <summary>
        /// The input action that provides the XR controller's position (in room space).
        /// This is used to correctly apply the XR controller angular velocity.
        /// </summary>
        [SerializeField]
        private InputActionProperty _handPositionAction;

        /// <summary>
        /// The input action that provides the XR controller's rotation (in room space).
        /// This is used to correctly interpret the XR controller angular velocity.
        /// </summary>
        [SerializeField]
        private InputActionProperty _handRotationAction;

        /// <summary>
        /// The input action that provides the XR controller's linear velocity (in room space).
        /// </summary>
        [SerializeField]
        private InputActionProperty _handLinearVelocityAction;

        /// <summary>
        /// The input action that provides the XR controller's angular velocity.
        /// The coordinate system varies from platform to platform, this is handled by <see cref="GetAngularVelocityInWorldSpace"/>.
        /// </summary>
        [SerializeField]
        private InputActionProperty _handAngularVelocityAction;

        private int _stickyGrabReleaseCounter;

        protected virtual void Awake() {
            isRightHand = this == _grabber.rightHand;

            _grabbableObjectDetector.onHoverEnter += OnHoverEnter;
            _grabbableObjectDetector.onHoverExit += OnHoverExit;
        }

        protected void OnDestroy() {
            if (_grabbedObject != null) {
                _grabbedObject._NotifyGrabberHandDestroyedInternal(this);
            }

            if (hoveredObject != null) {
                hoveredObject._NotifyGrabberHandDestroyedInternal(this);
            }

            _grabbedObject = null;
        }

        protected override void OnRealtimeModelReplaced(GrabberHandModel previousModel, GrabberHandModel currentModel) {
            if (currentModel.isOwnedLocallyInHierarchy) {
                // Only listen to inputs on the hands that we own
                SubscribeToActions();
            } else {
                UnsubscribeFromActions();
            }
        }

        private void SubscribeToActions() {
            if (_grabInputAction.action != null) {
                _grabInputAction.action.Enable();
                _grabInputAction.action.performed += OnGrabAction;
                _grabInputAction.action.canceled += OnReleaseAction;
            } else {
                Debug.LogError($"{nameof(_grabInputAction)} is unassigned");
            }

            if (_handPositionAction.action != null) {
                _handPositionAction.action.Enable();
            } else {
                Debug.LogError($"{nameof(_handPositionAction)} is unassigned");
            }

            if (_handRotationAction.action != null) {
                _handRotationAction.action.Enable();
            } else {
                Debug.LogError($"{nameof(_handRotationAction)} is unassigned");
            }

            if (_handLinearVelocityAction.action != null) {
                _handLinearVelocityAction.action.Enable();
            } else {
                Debug.LogError($"{nameof(_handLinearVelocityAction)} is unassigned");
            }

            if (_handAngularVelocityAction.action != null) {
                _handAngularVelocityAction.action.Enable();
            } else {
                Debug.LogError($"{nameof(_handAngularVelocityAction)} is unassigned");
            }
        }

        private void UnsubscribeFromActions() {
            if (_grabInputAction.action != null) {
                _grabInputAction.action.performed -= OnGrabAction;
                _grabInputAction.action.canceled -= OnReleaseAction;
            }
        }

        private void OnHoverEnter(GrabbableObject grabbableObject) {
            onHoverEnter?.Invoke(grabbableObject);
            grabbableObject._HoverEnterInternal(this);
        }

        private void OnHoverExit(GrabbableObject grabbableObject) {
            grabbableObject._HoverExitInternal(this);
            onHoverExit?.Invoke(grabbableObject);
        }

        private void OnGrabAction(InputAction.CallbackContext context) {
            // Is the hand free?
            if (_grabbedObject != null) {
                return;
            }

            // Is the hand hovering an object?
            if (hoveredObject == null) {
                return;
            }

            // Has no other hand grabbed the hovered object since we last checked?
            if (hoveredObject.grabberHand != null) {
                return;
            }

            // All conditions have passed, grab it
            Grab(hoveredObject);
        }

        private void OnReleaseAction(InputAction.CallbackContext context) {
            // Is the hand holding an object?
            if (_grabbedObject == null) {
                return;
            }

            // Skip the first release action when the object is sticky
            _stickyGrabReleaseCounter++;
            if (_grabbedObject.useStickyGrab && _stickyGrabReleaseCounter < 2) {
                return;
            }

            Release(_grabbedObject);
        }

        private void Grab(GrabbableObject grabbableObject) {
            if (isOwnedLocallyInHierarchy == false) {
                Debug.LogError($"Only the owner can invoke the {nameof(GrabberHand)}.{nameof(Grab)} method.");
                return;
            }

            _stickyGrabReleaseCounter = 0;

            // Synchronize the grab over the network
            model.grabbedObjectNetworkID = grabbableObject.networkID;

            _grabbedObject = grabbableObject;
            _grabbedObject._GrabInternal(this);
            onGrab?.Invoke(grabbableObject);
        }

        private void Release(GrabbableObject grabbableObject) {
            if (isOwnedLocallyInHierarchy == false) {
                Debug.LogError($"Only the owner can invoke the {nameof(GrabberHand)}.{nameof(Release)} method.");
                return;
            }

            var positionInRoomSpace = _handPositionAction.action.ReadValue<Vector3>();
            var positionInWorldSpace = _grabber.bodyRoot.TransformPoint(positionInRoomSpace);

            var linearVelocityInRoomSpace = _handLinearVelocityAction.action.ReadValue<Vector3>();
            var linearVelocityInWorldSpace = _grabber.bodyRoot.TransformDirection(linearVelocityInRoomSpace);

            var angularVelocityInWorldSpace = GetAngularVelocityInWorldSpace();

            // Synchronize the release over the network
            model.grabbedObjectNetworkID = null;

            _grabbedObject = null;
            grabbableObject._ReleaseInternal(linearVelocityInWorldSpace, angularVelocityInWorldSpace, positionInWorldSpace, _grabber.throwSettings);
            onRelease?.Invoke(grabbableObject);
        }

        /// <summary>
        /// Returns the value of <see cref="_handAngularVelocityAction"/> in world-space.
        /// </summary>
        /// <remarks>
        /// Written for Meta Quest and the Oculus XR plugin.
        /// Might not work with the OpenXR plugin.
        /// </remarks>
        private Vector3 GetAngularVelocityInWorldSpace() {
            // Following the approach outlined in https://discussions.unity.com/t/different-angular-velocity-readouts-on-quest-vs-rift/810727/2

            var rawAngularVelocity = _handAngularVelocityAction.action.ReadValue<Vector3>();

            if (Application.platform == RuntimePlatform.Android) {
                // Assume this is Quest standalone

                // Quest standalone returns the angular velocity relative to the controller itself
                // It must also be negated
                var angularVelocityInControllerSpace = -rawAngularVelocity;

                // Convert to world-space
                var controllerRotation = _handRotationAction.action.ReadValue<Quaternion>();
                var angularVelocityInRoomSpace = Quaternion.Inverse(controllerRotation) * angularVelocityInControllerSpace;
                var angularVelocityInWorldSpace = _grabber.bodyRoot.TransformDirection(angularVelocityInRoomSpace);

                return angularVelocityInWorldSpace;
            } else {
                // Assume this is Quest Link

                // Quest Link returns the angular velocity in room space, but it must be negated
                var angularVelocityInRoomSpace = -rawAngularVelocity;

                // Convert to world-space
                var angularVelocityInWorldSpace = _grabber.bodyRoot.TransformDirection(angularVelocityInRoomSpace);

                return angularVelocityInWorldSpace;
            }
        }

        protected virtual void Update() {
            SyncGrabbableObject();
            if (_grabbedObject != null) {
                _grabbedObject._StickToGrabberInternal(this);
            }

            if (model != null && isOwnedLocallyInHierarchy) {
                _grabbableObjectDetector.UpdateHover(this, _objectDetectorSettings);
            }
        }

        protected virtual void LateUpdate() {
            // We repeat the GrabberHand.Update() loop here because we want to stick the held object as close as possible to the hand.
            // If the hand has been moved since GrabberHand.Update(), we'll have the opportunity to process that here inside GrabberHand.LateUpdate().

            SyncGrabbableObject();
            if (_grabbedObject != null) {
                _grabbedObject._StickToGrabberInternal(this);
            }
        }

        private void SyncGrabbableObject() {
            if (model == null || realtime == null) {
                return;
            }

            if (realtime.TryGetComponent(out GrabManager grabManager) == false) {
                Debug.LogError($"Missing {nameof(GrabManager)} component on {realtime.gameObject.name}");
                return;
            }

            // Resolve the network ID to an object instance.
            // It's possible that it doesn't exist, ex if the object hasn't been deserialized yet.
            // That's ok - eventually it will be registered and we'll reflect that since we call this function every frame.
            GrabbableObject syncedObject = null;
            if (string.IsNullOrEmpty(model.grabbedObjectNetworkID) == false) {
                grabManager.TryGetGrabbableObject(model.grabbedObjectNetworkID, out syncedObject);
            }

            if (syncedObject != null) {
                if (syncedObject.ownerIDInHierarchy != ownerIDInHierarchy) {
                    // This hand has grabbed the object on the client's screen and sent a request to the server,
                    // but another client's request reached the server first.
                    // Consider this client empty-handed.
                    syncedObject = null;
                }
            }

            var previouslyGrabbedObject = _grabbedObject;
            var hasChanged = syncedObject != previouslyGrabbedObject;
            if (hasChanged == false) {
                return;
            }

            // Release the previous object
            if (previouslyGrabbedObject != null) {
                _grabbedObject = null;
                previouslyGrabbedObject._ReleaseNetworkedInternal();
                onRelease?.Invoke(previouslyGrabbedObject);
            }

            // Grab the new object
            if (syncedObject != null) {
                // Although previousHand would be able to release the object when it runs its own SyncGrabbableObject,
                // we do it here right away to ensure consistency (we always want to release before grabbing).
                var previousHand = syncedObject.grabberHand;
                if (previousHand != null) {
                    previousHand._grabbedObject = null;
                    syncedObject._ReleaseNetworkedInternal();
                    previousHand.onRelease?.Invoke(syncedObject);
                }

                _grabbedObject = syncedObject;
                syncedObject._GrabNetworkedInternal(this);
                onGrab?.Invoke(syncedObject);
            }
        }

        public void _NotifyGrabbableObjectDestroyedInternal(GrabbableObject grabbableObject) {
            // Invoke the GrabberHand callbacks (but skip the GrabbableObject callbacks)

            if (grabbableObject == _grabbedObject) {
                _grabbedObject = null;
                onRelease?.Invoke(grabbableObject);
            }

            if (grabbableObject == hoveredObject) {
                _grabbableObjectDetector._NotifyGrabbableObjectDestroyedInternal(grabbableObject);
                onHoverExit?.Invoke(grabbableObject);
            }
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected() {
            _grabbableObjectDetector.OnDrawGizmosSelected(this, _objectDetectorSettings);
        }
#endif
    }

    /// <summary>
    /// The <see cref="GrabberHand"/> data that is synchronized over the network.
    /// </summary>
    [RealtimeModel]
    public partial class GrabberHandModel {
        /// <summary>
        /// A property that synchronizes <see cref="GrabberHand.grabbedObject"/>.
        /// </summary>
        [RealtimeProperty(1, true)]
        private string _grabbedObjectNetworkID;
    }
}
