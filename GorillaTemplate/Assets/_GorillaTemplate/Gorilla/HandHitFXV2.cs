using System;
using System.Collections.Generic;
using Normal.Realtime;
using Normal.Realtime.Serialization;
using Normal.XR;
using UnityEngine;
using UnityEngine.Audio;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Normal.GorillaTemplate {
    /// <summary>
    /// Networked hit sound effects for player locomotion.
    /// </summary>
    public class HandHitFXV2 : RealtimeComponent<HandHitFXModel> {
        /// <summary>
        /// Sound data for a specific surface.
        /// </summary>
        [Serializable]
        public class SurfaceSFX {
            /// <summary>
            /// The tag used by the surface.
            /// </summary>
            [Tag]
            public string tag;

            /// <summary>
            /// The sound to play (it's recommended to use an <see cref="AudioRandomContainer"/>).
            /// </summary>
            public AudioResource sound;

            public bool isValid => string.IsNullOrEmpty(tag) == false && sound != null;
        }

        /// <summary>
        /// Specifies the left or right hand.
        /// </summary>
        [SerializeField]
        private HandHitProvider.Hand _hand;

        /// <summary>
        /// The component that plays the haptics effect.
        /// </summary>
        [SerializeField]
        private XRHaptics _haptics;

        /// <summary>
        /// The component that plays the sound effect.
        /// </summary>
        [SerializeField]
        private AudioSource _audioSource;

        /// <summary>
        /// Sound data for surfaces.
        /// </summary>
        [SerializeField]
        private SurfaceSFX[] _surfaceSounds = Array.Empty<SurfaceSFX>();

        /// <summary>
        /// Enables <see cref="_velocityHapticsCurve"/> and <see cref="_velocityVolumeCurve"/>.
        /// </summary>
        [SerializeField]
        private bool _useCurves;

        /// <summary>
        /// Controls haptics intensity based on <see cref="_averageVelocity"/>.
        /// </summary>
        [SerializeField]
        private AnimationCurve _velocityHapticsCurve;

        /// <summary>
        /// Controls sound volume based on <see cref="_averageVelocity"/>.
        /// </summary>
        [SerializeField]
        private AnimationCurve _velocityVolumeCurve;

        /// <summary>
        /// Used to calculate <see cref="_averageVelocity"/>.
        /// </summary>
        private Vector3 _lastPosition;

        /// <summary>
        /// The average velocity of this transform.
        /// </summary>
        private Vector3 _averageVelocity;

        /// <summary>
        /// The initial volume of <see cref="_audioSource"/>,
        /// which we take into account when we determine volume based on velocity.
        /// </summary>
        private float _initialAudioSourceVolume;

        /// <summary>
        /// A cache for <see cref="_surfaceSounds"/> for fast lookup.
        /// </summary>
        private readonly Dictionary<string, AudioResource> _surfaceSoundsLookup = new Dictionary<string, AudioResource>();

        /// <summary>
        /// The tag that GameObjects have by default.
        /// </summary>
        private const string __untagged = "Untagged";

        private void Awake() {
            RefreshSurfaceSoundsLookup();

            _initialAudioSourceVolume = _audioSource.volume;
        }

        private void OnValidate() {
            RefreshSurfaceSoundsLookup();
        }

        private void RefreshSurfaceSoundsLookup() {
            _surfaceSoundsLookup.Clear();

            foreach (var entry in _surfaceSounds) {
                if (entry.isValid == false) {
                    continue;
                }

                _surfaceSoundsLookup.TryAdd(entry.tag, entry.sound);
            }
        }

        private void OnDestroy() {
            if (GorillaLocalRig.instance != null) {
                GorillaLocalRig.instance.handHitProvider.onHit -= OnLocalHit;
            }
        }

        protected override void OnRealtimeModelReplaced(HandHitFXModel previousModel, HandHitFXModel currentModel) {
            if (previousModel != null) {
                GorillaLocalRig.instance.handHitProvider.onHit -= OnLocalHit;

                foreach (var surfaceModel in previousModel.surfaces) {
                    surfaceModel.counterDidChange -= OnSurfaceModelCounterChanged;
                }

                previousModel.surfaces.modelAdded -= OnSurfaceModelAdded;
            }

            if (currentModel != null) {
                // Add each surface to the list, if this is a fresh model
                if (currentModel.isFreshModel) {
                    foreach (var entry in _surfaceSounds) {
                        if (entry.isValid == false) {
                            continue;
                        }

                        var surfaceModel = new HandHitFXSurfaceModel() {
                            name = entry.tag,
                        };
                        currentModel.surfaces.Add(surfaceModel);
                    }
                }

                // Subscribe to local hits
                if (currentModel.isOwnedLocallyInHierarchy) {
                    GorillaLocalRig.instance.handHitProvider.onHit += OnLocalHit;
                }

                // Subscribe to changes on each surface
                foreach (var surfaceModel in currentModel.surfaces) {
                    surfaceModel.counterDidChange += OnSurfaceModelCounterChanged;

                    // Store the initial counter, to skip the initial change event
                    // (We don't want to play sounds for the initial change event, because the counter was probably changed some time ago)
                    surfaceModel.initialCounter = surfaceModel.counter;
                }

                // Subscribe to changes to the list of surfaces
                currentModel.surfaces.modelAdded += OnSurfaceModelAdded;
            }
        }

        private void OnSurfaceModelAdded(RealtimeArray<HandHitFXSurfaceModel> array, HandHitFXSurfaceModel surfaceModel, bool remote) {
            surfaceModel.counterDidChange += OnSurfaceModelCounterChanged;
            surfaceModel.initialCounter = surfaceModel.counter;
        }

        private void OnSurfaceModelCounterChanged(HandHitFXSurfaceModel surfaceModel, int value) {
            // We don't want to play sounds for the initial change event, because the counter was probably changed some time ago
            if (value == surfaceModel.initialCounter) {
                return;
            }

            if (_surfaceSoundsLookup.TryGetValue(surfaceModel.name, out var sound)) {
                _audioSource.resource = sound;
                _audioSource.volume = surfaceModel.velocityVolumeMultiplier * _initialAudioSourceVolume;
                _audioSource.Play();
            }
        }

        private void OnLocalHit(HandHitProvider.Hand hand, RaycastHit hitInfo) {
            // Ignore the right hand if ex this component is on the left hand
            if (hand != _hand) {
                return;
            }

            var speed = _averageVelocity.magnitude;
            // Debug.LogWarning($"Speed: {speed}");
            var velocityHapticsMultiplier = _useCurves ? _velocityHapticsCurve.Evaluate(speed) : 1f;
            var velocityVolumeMultiplier = _useCurves ? _velocityVolumeCurve.Evaluate(speed) : 1f;

            if (Mathf.Approximately(velocityHapticsMultiplier, 0f) == false) {
                _haptics.Rumble(velocityHapticsMultiplier);
            }

            if (Mathf.Approximately(velocityVolumeMultiplier, 0f)) {
                return;
            }

            // Resolve the tag of the surface that was hit
            // First using the tag on the collider
            var hitSurfaceTag = __untagged;
            if (hitInfo.collider != null) {
                hitSurfaceTag = hitInfo.collider.tag;
            }

            // Using the tag on the rigidbody as a fallback
            if (hitSurfaceTag == __untagged && hitInfo.rigidbody != null) {
                hitSurfaceTag = hitInfo.rigidbody.tag;
            }

            HandHitFXSurfaceModel surfaceModel;

            if (_surfaceSoundsLookup.ContainsKey(hitSurfaceTag)) {
                surfaceModel = GetModelForSurfaceTag(hitSurfaceTag);
            } else if (_surfaceSoundsLookup.ContainsKey(__untagged)) {
                // If no entry has been defined for that particular tag, use the "Untagged" entry (if there is one)
                surfaceModel = GetModelForSurfaceTag(__untagged);
            } else {
                return;
            }

            // Send a networked update
            surfaceModel.velocityVolumeMultiplier = velocityVolumeMultiplier;
            surfaceModel.counter++;
        }

        private HandHitFXSurfaceModel GetModelForSurfaceTag(string surfaceTag) {
            foreach (var surface in model.surfaces) {
                if (surface.name == surfaceTag) {
                    return surface;
                }
            }

            return null;
        }

        private void UpdateVelocityAverage(Vector3 currentPosition, float deltaTime, float averageDuration) {
            if (deltaTime <= 0f) {
                return;
            }

            // Smoothly update _averageVelocity toward the current instantaneousVelocity using
            // an exponential smoothing factor (alpha) that stays consistent regardless of framerate

            var instantaneousVelocity = (currentPosition - _lastPosition) / deltaTime;
            _lastPosition = currentPosition;

            var alpha = 1f - Mathf.Exp(-deltaTime / averageDuration);
            _averageVelocity = Vector3.Lerp(_averageVelocity, instantaneousVelocity, alpha);
        }

        private void Update() {
            const float averageDuration = 0.15f;
            UpdateVelocityAverage(transform.position, Time.deltaTime, averageDuration);
        }
    }

    /// <summary>
    /// The networked data for a <see cref="HandHitFXV2"/> component.
    /// </summary>
    [RealtimeModel]
    public partial class HandHitFXModel {
        [RealtimeProperty(1, true)]
        private RealtimeArray<HandHitFXSurfaceModel> _surfaces;
    }

    /// <summary>
    /// The networked data of a surface.
    /// </summary>
    [RealtimeModel]
    public partial class HandHitFXSurfaceModel {
        /// <summary>
        /// The name of the surface (<see cref="HandHitFXV2.SurfaceSFX.tag"/>).
        /// </summary>
        [RealtimeProperty(1, true)]
        private string _name;

        /// <summary>
        /// A counter that is incremented every time the sound for this surface is played by the owner of the model.
        /// It triggers a change event on all the clients.
        /// </summary>
        [RealtimeProperty(2, true, true)]
        private int _counter;

        /// <summary>
        /// The volume of the sound.
        /// </summary>
        [RealtimeProperty(3, true)]
        private float _velocityVolumeMultiplier;

        /// <summary>
        /// The initial value of <see cref="_counter"/> seen on this client.
        /// This is set in <see cref="HandHitFXV2.OnRealtimeModelReplaced"/>.
        /// </summary>
        public int initialCounter;
    }
}

namespace Normal.GorillaTemplate {
    /// <summary>
    /// When this attribute is added to a serialized property, the property is drawn as a dropdown in the inspector.
    /// This lets the user select from the list of tags defined in the project settings.
    /// </summary>
    public class TagAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(TagAttribute))]
    public class TagPropertyDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (property.propertyType == SerializedPropertyType.String) {
                property.stringValue = EditorGUI.TagField(position, label, property.stringValue);
            } else {
                EditorGUI.LabelField(position, label.text, $"[Tag] expects a string property, but this property is of type {property.propertyType}.");
            }
        }
    }
#endif
}
