#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace ChildGame.Services
{
    /// <summary>
    /// Google Play Billing (Android) and the App Store (iOS) through Unity IAP 5. The cash packages are consumables.
    /// Each purchase is granted and saved before it is confirmed; an unconfirmed purchase is delivered again on the next
    /// start, and the order ID ledger in the save stops it being credited twice.
    /// </summary>
    public class IapStore : IStore
    {
        private readonly StoreController store;
        private readonly Dictionary<string, Action<bool>> waiting = new Dictionary<string, Action<bool>>();
        private bool connected;

        public bool Available => true;

        public IapStore()
        {
            store = UnityIAPServices.StoreController();
            // Every event is subscribed before Connect: purchases left pending last time arrive straight away.
            store.OnPurchasePending += OnPending;
            store.OnPurchaseConfirmed += order => Debug.Log("[CHILD-IAP] Confirmed " + (order is FailedOrder ? "with a failure" : "ok"));
            store.OnPurchaseFailed += failed =>
            {
                Debug.LogWarning($"[CHILD-IAP] Failed: {failed.FailureReason} {failed.Details}");
                Answer(ProductId(failed.CartOrdered), false);
            };
            store.OnPurchaseDeferred += deferred =>
            {
                // Paid later (e.g. cash at a shop): granted when it arrives as a pending order.
                Debug.Log("[CHILD-IAP] Deferred");
                Answer(ProductId(deferred.CartOrdered), false);
            };
            store.OnStoreConnected += () =>
            {
                connected = true;
                store.FetchProducts(Shop.CashPackages.Select(a => new ProductDefinition(MonetizationIds.CashProduct(a), ProductType.Consumable)).ToList());
            };
            store.OnStoreDisconnected += failure =>
            {
                connected = false;
                Debug.LogWarning("[CHILD-IAP] Store disconnected: " + failure.Message);
            };
            store.OnProductsFetched += products =>
            {
                Debug.Log($"[CHILD-IAP] {products.Count} products");
                store.ProcessPendingOrdersOnPurchasesFetched(true);
                store.FetchPurchases();
            };
            store.OnProductsFetchFailed += failure => Debug.LogWarning("[CHILD-IAP] Products failed: " + failure.FailureReason);
            store.OnPurchasesFetched += orders => { };
            store.OnPurchasesFetchFailed += failure => Debug.LogWarning("[CHILD-IAP] Purchases fetch failed: " + failure.Message);
            Connect();
        }

        private async void Connect()
        {
            try { await store.Connect(); }
            catch (Exception e) { Debug.LogWarning("[CHILD-IAP] Connect failed: " + e.Message); }
        }

        public string Price(string productId) => store.GetProductById(productId)?.metadata.localizedPriceString;

        public void Purchase(string productId, Action<bool> done)
        {
            var product = connected ? store.GetProductById(productId) : null;
            if (product == null || !product.availableToPurchase)
            {
                done?.Invoke(false);
                return;
            }
            waiting[productId] = done;
            store.PurchaseProduct(product);
        }

        private void OnPending(PendingOrder order)
        {
            var id = ProductId(order.CartOrdered);
            var tx = order.Info.TransactionID;
            var ledger = Game.Save.grantedOrders;
            var ok = true;
            if (!string.IsNullOrEmpty(tx) && ledger.Contains(tx)) Debug.Log("[CHILD-IAP] Already granted " + tx);
            else
            {
                if (!string.IsNullOrEmpty(tx))
                {
                    ledger.Add(tx);
                    if (ledger.Count > 100) ledger.RemoveAt(0);
                }
                ok = Grant.Product(id); // saves
            }
            store.ConfirmPurchase(order);
            Answer(id, ok);
        }

        private void Answer(string productId, bool ok)
        {
            if (productId == null || !waiting.TryGetValue(productId, out var done)) return;
            waiting.Remove(productId);
            done?.Invoke(ok);
        }

        private static string ProductId(ICart cart) => cart?.Items().FirstOrDefault()?.Product?.definition.id;
    }
}
#endif
