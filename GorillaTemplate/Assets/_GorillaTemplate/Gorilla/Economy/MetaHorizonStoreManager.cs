using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

namespace Normal.GorillaTemplate {
    /// <summary>
    /// A class used to purchase SKUs from the Meta Horizon Store.
    /// </summary>
    public static class MetaHorizonStoreManager {
        private static readonly Dictionary<string, Product> __productCache = new Dictionary<string, Product>();

        /// <summary>
        /// Gets the product details for a given SKU from the Meta Horizon Store.
        /// </summary>
        /// <param name="sku">The SKU to retrieve product details for.</param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains the product
        /// details if a matching SKU was found, otherwise null.
        /// </returns>
        public static async Task<Product> GetProductDetailsAsync(string sku) {
            if (!MetaPlatformManager.CheckConfiguredAndInitialized("Cannot get product details: ")) {
                return null;
            }
            if (string.IsNullOrWhiteSpace(sku)) {
                Debug.LogError("Cannot get product details for an empty SKU.");
                return null;
            }

            sku = sku.Trim();

            if (__productCache.TryGetValue(sku, out var cachedProduct)) {
                return cachedProduct;
            }

            try {
                var result = await IAP.GetProductsBySKU(new[] { sku });
                if (result.IsError) {
                    Debug.LogError($"Failed to get the product details for SKU \"{sku}\": {result.GetError().Message}");
                    return null;
                }

                var products = result.Data;

                if (products.Count == 0) {
                    Debug.LogWarning($"No product details found for SKU \"{sku}\"!");
                    return null;
                }

                // Different produces can be returned for the same SKU, to handle regional pricing.
                // By default, the first result uses the appropriate regional pricing for the user.
                var product = products[0];

                __productCache[sku] = product;
                return product;
            } catch (Exception ex) {
                Debug.LogError($"Failed to get the product details for SKU \"{sku}\": {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Purchases the specified SKU from the Meta Horizon Store.
        /// </summary>
        /// <param name="sku">The SKU to purchase.</param>
        /// <param name="consumePurchaseCallback">
        /// This callback is required when purchasing a consumable SKU, in order to finalize the purchase.
        /// From within this callback, you should add the purchased items to the user's inventory, and only
        /// if the items were successfully added to the user's inventory, return true. If the items were not
        /// successfully added to the user's inventory, return false, which will generally result in the
        /// purchase being automatically refunded by the Meta Horizon Store.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result is true if the purchase was
        /// successful, otherwise false.
        /// </returns>
        public static async Task<bool> PurchaseAsync(string sku, Func<Purchase, Task<bool>> consumePurchaseCallback = null) {
            if (!MetaPlatformManager.CheckConfiguredAndInitialized("Failed to purchase SKU: ")) {
                return false;
            }
            if (string.IsNullOrWhiteSpace(sku)) {
                Debug.LogError("A valid SKU must be provided to start a purchase!");
                return false;
            }

            sku = sku.Trim();

            Debug.Log($"Attempting to purchase SKU \"{sku}\"...");

            try {
                var result = await IAP.LaunchCheckoutFlow(sku);
                if (result.IsError) {
                    Debug.LogError($"Failed to purchase SKU \"{sku}\": {result.GetError().Message}");
                    return false;
                }

                var purchase = result.Data;

                if (purchase == null) {
                    Debug.Log($"Purchase cancelled for SKU \"{sku}\".");
                    return false;
                }

                // If the purchase type is not consumable, we can finalize the purchase immediately.
                if (purchase.Type != ProductType.CONSUMABLE) {
                    Debug.Log($"Purchase successful for SKU \"{sku}\".");
                    return true;
                }

                // If the purchase type is consumable, we need to consume it.
                if (consumePurchaseCallback == null) {
                    Debug.LogError("When purchasing a consumable SKU, the purchased items must be granted to the user from within consumePurchaseCallback. The Meta Horizon store will automatically refund this user.");
                    return false;
                }

                var success = await consumePurchaseCallback(purchase);

                if (!success) {
                    Debug.LogError($"Failed to consume purchase for SKU \"{sku}\"!");
                    return false;
                }

                var consumeResult = await IAP.ConsumePurchase(purchase.Sku);
                if (consumeResult.IsError) {
                    Debug.LogError($"Failed to consume purchase for SKU \"{sku}\": {consumeResult.GetError().Message}");
                    return false;
                }

                Debug.Log($"Purchase consumed successfully for SKU \"{sku}\".");
                return true;
            } catch (Exception ex) {
                Debug.LogError($"Failed to purchase SKU \"{sku}\": {ex.Message}");
                return false;
            }
        }
    }
}
