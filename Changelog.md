[3.0.0]
- Implemented Quickmatch
- Updated to Normcore 3
- Updated Unity to version 6000.3.6f1
- Switched the XR plugin from Oculus XR to OpenXR
  - The "Gorilla Rig" prefab now has two new Transforms: "Left Hand With Tracking Offset" and "Right Hand With Tracking Offset" which replace "Left Hand" and "Right Hand" on the RealtimeAvatarManager's "Local Player > Left Hand" "Local Player > Right Hand" fields.
  - Removed the XROrigin component from the "Gorilla Rig" prefab, replaced by the XRTrackingOriginMode component
- Updated Meta XR Platform SDK to version 83.0.1
- Fixed a bug in AutoDistributeViewOwnership.cs (isLocallyOwnedConfirmed not being cleared when switching rooms)
- Improved the material colors in the Blank Scene Template
- Improved the sample skybox and floor shaders in the Blank Scene Template

[1.5.0]
- Added networked hit sound effects for locomotion (HandHitProvider and HandHitFXV2 components).
- Added sound effects to buttons and other UI elements.
- Exposed RaycastHit (leftHandHitInfo and rightHandHitInfo) in the Player locomotion component.
- The player model's head now rotates on the X and Z axis (while Y rotates the whole body).
- Added a headOffset property to the Player locomotion component and improved the behavior of the component when the player rotates their head.
- Added the GameModeIsDisabled property to the InfectionGameMode component. It can be used to toggle the gamemode at runtime.
- A disabled InfectionGameMode component now applies GameModeIsDisabled=true on fresh models.

[1.4.0]
- Added the BannedUserManager component and the Playfab Banned scene. Users that are banned on Playfab now load that scene.
- Added the XR Device Test scene to help users debug their PCVR connection.
- Added the useStickyGrab property to the GrabbableObject component. When enabled, the object is only released when the release action is performed a second time.
- Fixed the behaviour of the grab system when two players grab the same object at the exact same time. A client who grabs an object now also requests exclusive ownership over the object's RealtimeView (in previous versions the client only requested non-exclusive ownership of the RealtimeTransform).
- Now using preventOwnershipTakeover inside the AutoDistributeOwnership component for smoother ownership changes.
- Added the AutoDistributeViewOwnership component, it's a simpler version of AutoDistributeOwnership. It only manages ownership of its view instead of a managing the ownership of a specific component.
- Fixed the leaderboard entry of the local player not working after joining a second room.
- In the forest scene: slightly lowered the mirror resolution for performance and improved the mirror activation hitbox.

[1.3.1]
- Use Oculus.Platform.Models.User.OculusID instead of SystemInfo.deviceUniqueIdentifier to identify users on PlayFab.

[1.3.0]
- Added support for PlayFab.
- Added in-app purchasing of virtual currency and cosmetics using PlayFab.
- Added catalog and itemId to WardrobeItem for PlayFab support.
- Added ButtonBase.TriggerMode.LongPress.
- Changed SimpleButton.onPressed from a UnityAction to a UnityEvent.

[1.2.0]
- Added a new map to explore (Forest).
- Added objects that can be grabbed and thrown.
- Made optimizations to the mirror script.

[1.1.0]
- Added a network-synced cosmetics system.
- Added many pre-built cosmetics.
- Added a wardrobe system.
- Added a tool for easily swapping the player model.

[1.0.3]
- Fixed invisible player bug.
- Improved finger animation sync.

[1.0.2]
- Fixed finger bone naming.
- Removed the unused collider on Speaker Icon.
- Fixed the physics collision matrix for the Hide From Local Camera layer.

[1.0.1]
- Added the XRKeyboardAndMouseMovement script to make testing easier.
