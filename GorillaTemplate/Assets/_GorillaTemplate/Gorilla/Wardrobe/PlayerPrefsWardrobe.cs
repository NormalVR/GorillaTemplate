using System.Collections.Generic;
using Normal.GorillaTemplate.Cosmetics;
using Normal.GorillaTemplate.Keyboard;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;

namespace Normal.GorillaTemplate.Wardrobe {
    /// <summary>
    /// A wardrobe that lets the user browse and equip one item at a time.
    /// The user's loadout is stored locally on their device using Unity's <see cref="PlayerPrefs"/>.
    /// </summary>
    public class PlayerPrefsWardrobe : Keyboard.Keyboard, IWardrobe  {
        /// <summary>
        /// The player manager - we use it to access the local avatar.
        /// </summary>
        [SerializeField]
        private GorillaPlayerManager _gorillaPlayerManager;

        /// <summary>
        /// The object to display when the user is browsing the "no cosmetic" option.
        /// </summary>
        [SerializeField]
        private GameObject _noneWardrobeItem;

        [SerializeField]
        [Tooltip("The currency code of the virtual currency used to purchase the item.")]
        private string _virtualCurrencyCode = "BN";

        [SerializeField]
        [Tooltip("The text component that displays the price of the item if it is not owned.")]
        private TMP_Text _priceText;

        [SerializeField]
        private ButtonBase _equipButton;

        [SerializeField]
        private AudioSource _purchaseSFX;

        /// <summary>
        /// The local avatar's cosmetics manager instance.
        /// </summary>
        private CosmeticsSync _cosmeticsSync;

        /// <summary>
        /// The key used to save the loadout in Unity's <see cref="PlayerPrefs"/>.
        /// </summary>
        private const string __loadoutKey = "LocalPlayerData/WardrobeLoadout";

        /// <summary>
        /// The list of all items registered with this wardrobe.
        /// </summary>
        private List<WardrobeItem> _items = new List<WardrobeItem>();

        /// <summary>
        /// The index of the item that the user is currently browsing.
        /// </summary>
        private int _browsingIndex;

        private void Awake() {
            _gorillaPlayerManager.playerJoined += OnPlayerJoined;

            // Register all items that belong to this wardrobe
            var items = GetComponentsInChildren<WardrobeItem>(true);
            foreach (var item in items) {
                _items.Add(item);
            }

            PlayFabManager.onCatalogChanged += OnCatalogChanged;
            PlayFabManager.onInventoryChanged += OnInventoryChanged;
        }

        private void OnDestroy() {
            _gorillaPlayerManager.playerJoined -= OnPlayerJoined;

            PlayFabManager.onCatalogChanged -= OnCatalogChanged;
            PlayFabManager.onInventoryChanged -= OnInventoryChanged;
        }

        /// <summary>
        /// Invoked when a player joins the room.
        /// </summary>
        private void OnPlayerJoined(GorillaAvatar avatar, bool isLocalPlayer) {
            // Only proceed for the local avatar
            if (isLocalPlayer == false) {
                return;
            }

            // The cosmetics manager exists on the root object of the avatar
            _cosmeticsSync = avatar.GetComponent<CosmeticsSync>();

            // Enable cosmetics using the data on the local device
            LoadLoadout();

            // Create a HashSet for quick lookup
            var enabledCosmetics = new HashSet<string>();
            foreach (var cosmetic in _cosmeticsSync.cosmetics) {
                if (cosmetic.cosmeticEnabled) {
                    enabledCosmetics.Add(cosmetic.cosmeticName);
                }
            }

            // Determine which item index, if any, corresponds to the equipped cosmetics
            _browsingIndex = 0;
            foreach (var item in _items) {
                foreach (var cosmeticName in item.cosmeticNames) {
                    if (enabledCosmetics.Contains(cosmeticName)) {
                        // + 1 to account for _noneWardrobeItem
                        _browsingIndex = _items.IndexOf(item) + 1;

                        // Validate that the item is owned by the user. If not, un-equip it.
                        if (!OwnsItem(item)) {
                            _cosmeticsSync.SetEnabled(cosmeticName, false);
                        }

                        break;
                    }
                }
            }

            // Save the loadout to the local device in case items were un-equipped due to ownership
            // being revoked.
            SaveLoadout();

            // Display the equipped item
            BrowseItem(_browsingIndex);
        }

        private void OnCatalogChanged(string catalog, List<CatalogItem> items) {
            // Update the pricing of the currently browsed item, if applicable
            BrowseItem(_browsingIndex);
        }

        private void OnInventoryChanged(List<ItemInstance> items) {
            // Update the ownership state of the currently browsed item, if applicable
            BrowseItem(_browsingIndex);
        }

        /// <summary>
        /// Applies the cosmetics state onto the local player using the data on the local device.
        /// </summary>
        private void LoadLoadout() {
            var data = PlayerPrefs.GetString(__loadoutKey);
            if (string.IsNullOrEmpty(data)) {
                return;
            }

            _cosmeticsSync.TryLoadJson(data);
        }

        /// <summary>
        /// Saves the cosmetics state of the local player on the local device.
        /// </summary>
        private void SaveLoadout() {
            var data = _cosmeticsSync.SaveJson();
            PlayerPrefs.SetString(__loadoutKey, data);
        }

        public void RegisterItem(WardrobeItem item) {
            if (_items.Contains(item)) {
                return;
            }

            _items.Add(item);
        }

        public override void NotifyButtonPressed(KeyboardButtonData data) {
            base.NotifyButtonPressed(data);

            if (data.Type == KeyboardButtonType.ArrowLeft) {
                BrowseItem(_browsingIndex - 1);
            } else if (data.Type == KeyboardButtonType.ArrowRight) {
                BrowseItem(_browsingIndex + 1);
            } else if (data.Type == KeyboardButtonType.Enter) {
                EquipItem();
            }
        }

        /// <summary>
        /// Displays the item at the specified index.
        /// </summary>
        /// <param name="index">
        /// The index will be wrapped within the bounds of <see cref="_items"/>.
        /// An index of 0 corresponds to <see cref="_noneWardrobeItem"/>.
        /// </param>
        private void BrowseItem(int index) {
            // + 1 to account for _noneWardrobeItem
            var totalCount = _items.Count + 1;

            // Wraps value between 0 and _items.Count, handles negative values too
            _browsingIndex = (index + totalCount) % totalCount;

            if (_browsingIndex == 0) {
                HideAllItems();

                DisplayPrice(0);

                _noneWardrobeItem.SetActive(true);
            } else {
                HideAllItems();

                // - 1 to account for _noneWardrobeItem
                var itemIndex = _browsingIndex - 1;
                var item = _items[itemIndex];

                DisplayItem(item);
            }
        }

        private void HideAllItems() {
            _noneWardrobeItem.SetActive(false);

            foreach (var item in _items) {
                item.gameObject.SetActive(false);
            }
        }

        private async void DisplayItem(WardrobeItem itemToDisplay) {
            // Show the item.
            itemToDisplay.gameObject.SetActive(true);

            // Get the price of the item from PlayFab, if applicable.
            var price = 0;

            if (!OwnsItem(itemToDisplay)) {
                var catalogItem = await PlayFabManager.GetItemFromCatalogAsync(itemToDisplay.itemId, itemToDisplay.catalog);

                if (catalogItem != null) {
                    // If no price is set, show a price of zero
                    price = (int)catalogItem.VirtualCurrencyPrices.GetValueOrDefault(_virtualCurrencyCode, 0u);
                }
            }

            DisplayPrice(price);
        }

        private void DisplayPrice(int price) {
            if (price > 0) {
                _priceText.text = $"Price: {price}";
                _equipButton.triggerMode = ButtonBase.TriggerMode.LongPress;
            } else {
                _priceText.text = string.Empty;
                _equipButton.triggerMode = ButtonBase.TriggerMode.Press;
            }
        }

        /// <summary>
        /// Equips the item that the user is currently browsing onto their loadout.
        /// </summary>
        /// <remarks>
        /// Un-equips all other items registered to this wardrobe.
        /// </remarks>
        private async void EquipItem() {
            // - 1 to account for _noneWardrobeItem
            var itemIndex = _browsingIndex - 1;

            // Check if the user is entitled to the item they are trying to equip
            if (itemIndex >= 0) {
                var itemToEquip = _items[itemIndex];

                // If the item is not in the player's inventory, try to purchase it
                if (!OwnsItem(itemToEquip)) {
                    if (await PlayFabManager.PurchaseItemAsync(itemToEquip.catalog, itemToEquip.itemId, _virtualCurrencyCode)) {
                        // Clear the price text if the item was purchased successfully
                        DisplayPrice(0);

                        if (_purchaseSFX != null) {
                            _purchaseSFX.Play();
                        }
                    } else {
                        // If the purchase failed, do not equip the item
                        return;
                    }
                }
            }

            // Loop over all registered items
            for (var i = 0; i < _items.Count; i++) {
                var item = _items[i];

                // Equip the item that is being browsed, and un-equip the other items
                var shouldBeEquipped = i == itemIndex;

                // Loop over all the cosmetics that the item represents
                foreach (var cosmeticName in item.cosmeticNames) {
                    _cosmeticsSync.SetEnabled(cosmeticName, shouldBeEquipped);
                }
            }

            // Save the loadout to the local device
            SaveLoadout();
        }

        private bool OwnsItem(WardrobeItem item) {
            // If playFab is not configured, make all items freely available
            if (!PlayFabManager.isConfigured) {
                return true;
            }

            // If the item does not have an ID, it is freely available to wear
            if (string.IsNullOrEmpty(item.itemId)) {
                return true;
            }

            // Check if the item is in the player's inventory
            return PlayFabManager.IsItemInInventory(item.itemId);
        }
    }
}
