using System;
using System.Threading.Tasks;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;
using Application = UnityEngine.Application;

namespace Normal.GorillaTemplate {
    /// <summary>
    /// A class used to manage the Meta Platform SDK.
    /// </summary>
    public static class MetaPlatformManager {
        private static string __configureMessage = "Meta Platform SDK is not configured. To enable Meta Platform features, assign your App ID using \"Meta > Platform > Edit Settings\" in the Unity Editor.";

        private static bool __hasWarnedAboutConfiguration = false;
        private static Task __initializationTask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init() {
            _ = InitializeAsync();
        }

        /// <summary>
        /// Whether the Meta Platform SDK has been configured for this application by assigning an App ID.
        /// </summary>
        public static bool isConfigured => !string.IsNullOrWhiteSpace(GetAppID());

        /// <summary>
        /// Whether the Meta Platform SDK is initialized and ready to use.
        /// </summary>
        public static bool isInitialized => Core.IsInitialized();

        /// <summary>
        /// The details of the logged-in user.
        /// </summary>
        /// <remarks>
        /// Before accessing this property, await <see cref="InitializeAsync"/> to ensure the SDK is initialized.
        /// </remarks>
        public static User user { get; private set; }

        /// <summary>
        /// Initializes the Meta Platform SDK.
        /// </summary>
        /// <remarks>
        /// This is called automatically when the game starts, but can also be called manually if you need
        /// to ensure the SDK is initialized at a specific point in your code.
        /// </remarks>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task InitializeAsync() {
            // If the Core is already initialized, we don't need to do anything.
            if (__initializationTask == null && isInitialized) {
                return;
            }

            // If the initialization task is already running, wait for it to complete, rather than initializing multiple times.
            if (__initializationTask == null) {
                __initializationTask = DoInitializeAsync();
            }

            await __initializationTask;
        }

        private static async Task DoInitializeAsync() {
            // An App ID is required to use the Meta Platform SDK.
            if (!isConfigured) {
                if (!__hasWarnedAboutConfiguration) {
                    Debug.LogWarning(__configureMessage);
                    __hasWarnedAboutConfiguration = true;
                }
                return;
            }

            // Initialize the Core SDK.
            try {
                Debug.Log("Meta Platform SDK initializing...");
                Core.Initialize();
                Debug.Log("Meta Platform SDK initialized.");
            } catch (Exception e) {
                Debug.LogError($"Failed to initialize Meta Platform SDK: {e.Message}");
                return;
            }

            // Check that the user has access to the application, and get the logged-in user details.
            await Task.WhenAll(
                EntitlementsCheckAsync(),
                UpdateUserAsync()
            );

            __initializationTask = null;
        }

        /// <summary>
        /// Refreshes the <see cref="user"/> details.
        /// </summary>
        /// <remarks>
        /// This is called automatically when the game starts, but can also be called manually if you need
        /// to refresh the user details later.
        /// </remarks>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task UpdateUserAsync() {
            if (!CheckConfiguredAndInitialized("Failed to update Meta Platform user details: ")) {
                return;
            }

            Debug.Log("Getting user details from Meta Platform...");

            try {
                var result = await Users.GetLoggedInUser();
                if (result.IsError) {
                    Debug.LogError($"Failed to get user details from Meta Platform: {result.GetError().Message}");
                    return;
                }

                user = result.Data;
                Debug.Log("Finished getting user details from Meta Platform.");
            } catch (Exception ex) {
                Debug.LogError($"Failed to get user details from Meta Platform: {ex.Message}");
            }
        }

        private static async Task EntitlementsCheckAsync() {
            if (!CheckConfiguredAndInitialized("Failed to check Meta Platform entitlements: ")) {
                return;
            }

            // Only run the entitlement check in release builds.
            if (Application.isEditor) {
                Debug.Log("Skipping Meta Platform entitlement check: Entitlement check is not performed in the Unity Editor.");
                return;
            }
            if (Debug.isDebugBuild) {
                Debug.Log("Skipping Meta Platform entitlement check: Entitlement check is not performed for development builds.");
                return;
            }

            Debug.Log("Checking if the user is entitled to this application via the Meta Platform...");

            try {
                var result = await Entitlements.IsUserEntitledToApplication();
                if (result.IsError) {
                    Debug.LogError($"Meta Platform entitlement check failed: {result.GetError().Message}");

                    // Quit the application, the user doesn't have permission to run this app.
                    Debug.LogError("Quitting application due to failed entitlement check!");
                    Application.Quit();
                    return;
                }

                Debug.Log("User is entitled to this application via the Meta Platform.");
            } catch (Exception e) {
                Debug.LogError($"Failed to check Meta Platform entitlements: {e.Message}");
            }
        }

        internal static bool CheckConfiguredAndInitialized(string prefix = "") {
            if (!isConfigured) {
                Debug.LogError(prefix + __configureMessage);
                return false;
            }
            if (!isInitialized) {
                Debug.LogError(prefix + $"Meta Platform SDK is not initialized. Please call {nameof(MetaPlatformManager)}.{nameof(InitializeAsync)}() before using Meta Platform features.");
                return false;
            }
            return true;
        }

        private static string GetAppID() {
            if (Application.platform == RuntimePlatform.Android) {
                return PlatformSettings.MobileAppID;
            }
            if (PlatformSettings.UseMobileAppIDInEditor) {
                return PlatformSettings.MobileAppID;
            }
            return PlatformSettings.AppID;
        }
    }
}
