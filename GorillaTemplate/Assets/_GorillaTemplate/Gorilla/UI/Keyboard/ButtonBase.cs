using UnityEngine;

namespace Normal.GorillaTemplate.Keyboard {
    /// <summary>
    /// A base class for a pressable button.
    /// Override <see cref="HandlePress"/> to implement specific behavior on button press.
    /// </summary>
    public abstract class ButtonBase : MonoBehaviour {
        /// <summary>
        /// The modes of interaction a button can use.
        /// </summary>
        public enum TriggerMode {
            /// <summary>
            /// The button is considered pressed immediately when the user presses down on it.
            /// </summary>
            Press,

            /// <summary>
            /// The button is considered pressed after the user holds down on it for a certain duration.
            /// If the user releases before the duration is complete, it is not pressed.
            /// </summary>
            LongPress,
        }

        [SerializeField]
        private TriggerMode _triggerMode = TriggerMode.Press;

        /// <summary>
        /// The modes of interaction the button can use.
        /// </summary>
        public TriggerMode triggerMode {
            get => _triggerMode;
            set => _triggerMode = value;
        }

        [SerializeField]
        [Tooltip("The duration, in seconds, that the user must hold down on the button for it to be considered pressed. Only used when \"Trigger Mode\" is set to \"LongPress\".")]
        private float _longPressDuration = 1f;

        /// <summary>
        /// The duration, in seconds, that the user must hold down on the button for it to be considered pressed. Only used when <see cref="TriggerMode.LongPress"/> is selected.
        /// </summary>
        public float longPressDuration => _longPressDuration;

        /// <summary>
        /// The child GameObject that holds the visual representation of the button.
        /// </summary>
        [SerializeField]
        private GameObject _visuals;

        /// <summary>
        /// How far the button moves after being pressed.
        /// </summary>
        [SerializeField]
        private float _pushOffset = 0.1f;

        /// <summary>
        /// A factor that controls how the button returns to its initial position after being pressed.
        /// </summary>
        [SerializeField]
        private float _lerpFactor = 20.0f;

        /// <summary>
        /// An optional sound effect to play when the button is pressed.
        /// </summary>
        [SerializeField]
        private AudioSource _pressSFX;

        /// <summary>
        /// The original position of the button that it will return to after being pressed.
        /// </summary>
        private Vector3 _originalPosition;

        /// <summary>
        /// To resolve ex multiple fingers resting on the same keyboard button.
        /// </summary>
        private int _triggerCount;

        private bool _hasPressed;

        /// <summary>
        /// The duration, in seconds, for which the button has been pressed.
        /// </summary>
        public float currentPressDuration { get; private set; }

        /// <summary>
        /// The progress, from 0 to 1, until this long press button is considered pressed.
        /// </summary>
        /// <seealso cref="TriggerMode.LongPress"/>
        public float longPressProgress {
            get {
                if (Mathf.Approximately(longPressDuration, 0f)) {
                    return 0f;
                }

                return Mathf.Clamp01(currentPressDuration / longPressDuration);
            }
        }

        protected virtual void Awake() {
            _originalPosition = _visuals.transform.localPosition;
        }

        /// <summary>
        /// The button should have a trigger collider on its root GameObject,
        /// which won't move when the visuals GameObject (which is a child) is animated.
        ///
        /// The collider should be on a layer that interacts with non-trigger colliders on
        /// the local player's hands. The hand colliders need to be non-triggers, but
        /// they can have a layer specific to them to ensure they only collide with
        /// buttons and no other layers. All of this is managed at the prefab
        /// level, and not in the code.
        /// </summary>
        private void OnTriggerEnter(Collider other) {
            // Ignore presses from other fingers
            if (_triggerCount == 0) {
                currentPressDuration = 0f;

                if (_triggerMode == TriggerMode.Press) {
                    OnPressed();
                }
            }

            _triggerCount++;
        }

        private void OnTriggerExit(Collider other) {
            _triggerCount--;

            if (_triggerCount == 0) {
                currentPressDuration = 0f;
                _hasPressed = false;
            }
        }

        private void OnPressed() {
            _hasPressed = true;
            HandlePress();
            ApplyOnPressedFX();

            if (_pressSFX != null) {
                _pressSFX.Play();
            }
        }

        /// <summary>
        /// Override this method to react to a button press.
        /// </summary>
        protected abstract void HandlePress();

        protected virtual void ApplyOnPressedFX() {
            // Push button back
            _visuals.transform.localPosition = _originalPosition + Vector3.forward * _pushOffset;
        }

        protected virtual void Update() {
            if (_triggerCount > 0) {
                currentPressDuration += Time.deltaTime;

                if (_triggerMode == TriggerMode.LongPress && !_hasPressed && currentPressDuration >= _longPressDuration) {
                    OnPressed();
                }
            }

            UpdateFX();
        }

        protected virtual void UpdateFX() {
            // Animate towards original position
            _visuals.transform.localPosition = Vector3.Lerp(_visuals.transform.localPosition, _originalPosition, Time.deltaTime * _lerpFactor);
        }
    }
}
