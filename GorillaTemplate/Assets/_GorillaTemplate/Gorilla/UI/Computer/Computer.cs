using System.Collections.Generic;
using Normal.GorillaTemplate.Keyboard;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Normal.GorillaTemplate.UI.Computer {
    /// <summary>
    /// A computer with a keyboard to display menus for the local player.
    /// </summary>
    public class Computer : Keyboard.Keyboard {
        /// <summary>
        /// An event that is dispatched when the user picks a private room to connect to.
        /// </summary>
        [SerializeField]
        private UnityEvent<string> _doConnect;

        /// <summary>
        /// An event that is dispatched when the user picks a public room to connect to.
        /// </summary>
        [SerializeField]
        private UnityEvent<string> _doConnectPublic;

        /// <summary>
        /// An event that is dispatched when the user selects quickmatch.
        /// </summary>
        [SerializeField]
        private UnityEvent _doQuickmatch;

        /// <summary>
        /// The list of tabs in this computer.
        /// </summary>
        [SerializeField]
        private List<Tab> _tabs = new List<Tab>();

        /// <summary>
        /// The UI element that will contain the tab headers.
        /// </summary>
        [SerializeField]
        private LayoutGroup _tabHeadersContainer;

        /// <summary>
        /// The prefab for a tab header.
        /// </summary>
        [SerializeField]
        private TabHeader _tabHeaderPrefab;

        /// <summary>
        /// An event that is dispatched when the user picks a private room to connect to.
        /// </summary>
        public UnityEvent<string> doConnect => _doConnect;

        /// <summary>
        /// An event that is dispatched when the user picks a public room to connect to.
        /// </summary>
        public UnityEvent<string> doConnectPublic => _doConnectPublic;

        /// <summary>
        /// An event that is dispatched when the user selects quickmatch.
        /// </summary>
        public UnityEvent doQuickmatch => _doQuickmatch;

        /// <summary>
        /// The index of the currently displayed tab.
        /// </summary>
        private int _currentTabIndex;

        /// <summary>
        /// A list of all tab headers (in the same order as <see cref="_tabs"/>).
        /// </summary>
        private readonly List<TabHeader> _tabHeaders = new List<TabHeader>();

        private void Awake() {
            // Initialize all tabs
            foreach (var tab in _tabs) {
                tab.Initialize(this);
            }

            // Create and initialize all tab headers
            _tabHeaders.Clear();
            foreach (var tab in _tabs) {
                var tabHeader = Instantiate(_tabHeaderPrefab, _tabHeadersContainer.transform);
                tabHeader.Initialize(tab);
                _tabHeaders.Add(tabHeader);
            }

            DisplayTabIndex(_currentTabIndex);
        }

        public override void NotifyButtonPressed(KeyboardButtonData data) {
            base.NotifyButtonPressed(data);

            switch (data.Type) {
                case KeyboardButtonType.ArrowDown:
                    // Display next tab
                    DisplayTabIndex(_currentTabIndex + 1);
                    break;

                case KeyboardButtonType.ArrowUp:
                    // Display previous tab
                    DisplayTabIndex(_currentTabIndex - 1);
                    break;

                default:
                    // Forward the button press to the current tab
                    var currentTab = _tabs[_currentTabIndex];
                    currentTab.NotifyButtonPressed(data);
                    break;
            }
        }

        /// <summary>
        /// Sets the tab at the specified index as the currently displayed tab.
        /// </summary>
        private void DisplayTabIndex(int index) {
            // Loop the tab around 0 and (_tabs.Count - 1)
            _currentTabIndex = (index + _tabs.Count) % _tabs.Count;

            for (var i = 0; i < _tabs.Count; i++) {
                var isCurrentTab = i == _currentTabIndex;
                var tab = _tabs[i];
                var tabHeader = _tabHeaders[i];

                // Update tab GameObject
                tab.gameObject.SetActive(isCurrentTab);

                // Update tab
                tab.NotifyVisible(isCurrentTab);

                // Update header
                tabHeader.SetIsCurrentTab(isCurrentTab);
            }
        }
    }
}
