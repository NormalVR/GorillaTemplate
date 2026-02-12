using System;
using Normal.GorillaTemplate.Keyboard;
using UnityEngine;

namespace Normal.GorillaTemplate.UI.Computer {
    /// <summary>
    /// A tab that connects to a room.
    /// </summary>
    [Obsolete("Replaced by PrivateRoomTab")]
    public class RoomTab : Tab {
        private const int __maxCodeLength = 6;

        /// <summary>
        /// The text field that will hold the name.
        /// </summary>
        [SerializeField]
        private TMPro.TMP_Text _currentRoomLabel;

        /// <summary>
        /// The text field that holds the room code.
        /// </summary>
        [SerializeField]
        private TMPro.TMP_InputField _inputField;

        public override string tabName => "Room";

        public override void NotifyVisible(bool visible) {
            base.NotifyVisible(visible);

            if (!visible) {
                return;
            }

            // Reset the input field
            _inputField.text = string.Empty;
        }

        public override void NotifyButtonPressed(KeyboardButtonData data) {
            base.NotifyButtonPressed(data);

            switch (data.Type) {
                case KeyboardButtonType.Symbol:
                    AddSymbol(data.Symbol);
                    break;

                case KeyboardButtonType.Backspace:
                    DeleteSymbol();
                    break;

                case KeyboardButtonType.Enter:
                    Submit();
                    break;
            }
        }

        /// <summary>
        /// Adds a symbol to the room code.
        /// </summary>
        private void AddSymbol(string symbol) {
            // Enforce the max length limit
            if (_inputField.text.Length >= __maxCodeLength)
                return;

            _inputField.text += symbol;
        }

        /// <summary>
        /// Removes the right-most symbol from the room code.
        /// </summary>
        private void DeleteSymbol() {
            var length = _inputField.text.Length;
            var newLength = Mathf.Max(0, length - 1);
            _inputField.text = _inputField.text.Substring(0, newLength);
        }

        /// <summary>
        /// Submits a room code to connect to it.
        /// </summary>
        private void Submit() {
            var roomCode = _inputField.text;
            if (!ValidateRoomCode(roomCode))
                return;

            // Connect with Realtime
            computer.doConnect?.Invoke(roomCode);
        }

        private bool ValidateRoomCode(string roomCode) {
            // Make sure it's not an empty code
            if (roomCode.Length == 0)
                return false;

            return true;
        }
    }
}
