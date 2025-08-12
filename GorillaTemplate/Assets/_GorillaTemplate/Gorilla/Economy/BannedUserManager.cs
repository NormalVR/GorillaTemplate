using UnityEngine;
using UnityEngine.SceneManagement;

namespace Normal.GorillaTemplate {
    /// <summary>
    /// Handles the case where the current user is banned on Playfab.
    /// </summary>
    public class BannedUserManager : MonoBehaviour {
        /// <summary>
        /// Banned users are sent to this scene.
        /// </summary>
        [SerializeField]
        private string _sceneNameForBannedUsers;

        private void Update() {
            if (PlayFabManager.isBanned) {
                SceneManager.LoadScene(_sceneNameForBannedUsers);
            }
        }
    }
}
