using Normal.GorillaTemplate.Keyboard;
using UnityEngine;
using UnityEngine.Events;

namespace Normal.GorillaTemplate.UI {
    public class QuickmatchBoard : MonoBehaviour {
        /// <summary>
        /// The button that triggers the connection.
        /// </summary>
        public SimpleButton submitButton;

        /// <summary>
        /// Dispatched when the user presses the button.
        /// </summary>
        public UnityEvent onSubmit;

        private void OnEnable() {
            submitButton.onPressed.AddListener(OnSubmitButtonPressed);
        }

        private void OnDisable() {
            submitButton.onPressed.RemoveListener(OnSubmitButtonPressed);
        }

        [ContextMenu("Test Quickmatch")]
        private void OnSubmitButtonPressed() {
            onSubmit?.Invoke();
        }
    }
}
