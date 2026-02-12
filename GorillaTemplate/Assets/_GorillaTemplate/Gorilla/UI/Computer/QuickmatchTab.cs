using Normal.GorillaTemplate.Keyboard;
using UnityEngine;

namespace Normal.GorillaTemplate.UI.Computer {
    /// <summary>
    /// A tab that connects using quickmatch.
    /// </summary>
    public class QuickmatchTab : Tab {
        /// <summary>
        /// The text field that will hold the name.
        /// </summary>
        [SerializeField]
        private TMPro.TMP_Text _currentRoomLabel;

        /// <summary>
        /// The text field that will hold the disconnect event details (if any).
        /// </summary>
        [SerializeField]
        private TMPro.TMP_Text _disconnectEventLabel;

        public override string tabName => "Quick";

        private void LateUpdate() {
            var display = computer.GetComponent<SeamlessRoomCodeDisplay>();
            _currentRoomLabel.text = display.nameText;
            _disconnectEventLabel.text = display.disconnectEventText;
        }

        public override void NotifyButtonPressed(KeyboardButtonData data) {
            base.NotifyButtonPressed(data);

            switch (data.Type) {
                case KeyboardButtonType.Enter:
                    Quickmatch();
                    break;
            }
        }

        private void Quickmatch() {
            computer.doQuickmatch?.Invoke();
        }
    }
}
