using System;
using UnityEngine;

namespace ChildGame.Services
{
    /// <summary>
    /// Rewarded ads (design §3). Ads only ever play when the player taps a button that offers a reward; there are no
    /// forced or interstitial ads.
    /// </summary>
    public interface IRewardedAds
    {
        /// <summary>False where this platform has no ads at all: the shop hides the ad button.</summary>
        bool Supported { get; }
        bool IsReady(string placement);
        /// <summary>Shows a rewarded ad; <paramref name="done"/> gets true only if the reward was earned.</summary>
        void Show(string placement, Action<bool> done);
    }

    /// <summary>
    /// In-app purchases: the đồng cash packages, all consumable. The store itself grants the purchase through
    /// <see cref="Grant"/> and saves before confirming it, so a purchase made while the app was closed is still credited.
    /// </summary>
    public interface IStore
    {
        /// <summary>False where purchases are not offered (the web build): the shop hides "Nạp đồng".</summary>
        bool Available { get; }
        /// <summary>The store's price text for a product ("25.000 ₫"), or null until the store has answered.</summary>
        string Price(string productId);
        /// <summary>Buys a product; <paramref name="done"/> gets true once it has been granted and saved.</summary>
        void Purchase(string productId, Action<bool> done);
    }

    public static class Grant
    {
        /// <summary>Credits a purchased product to the save and writes it. Unknown products are ignored.</summary>
        public static bool Product(string productId)
        {
            foreach (var amount in Shop.CashPackages)
            {
                if (productId != MonetizationIds.CashProduct(amount)) continue;
                Shop.AddCash(Game.Save, amount);
                Game.Commit();
                Debug.Log($"[CHILD-IAP] Granted {productId}");
                return true;
            }
            Debug.LogWarning($"[CHILD-IAP] Unknown product {productId}");
            return false;
        }
    }

    /// <summary>Editor and Windows development builds: always ready, always grants the reward after a short delay.</summary>
    public class SimulatedAds : IRewardedAds
    {
        public bool Supported => true;
        public bool IsReady(string placement) => true;

        public void Show(string placement, Action<bool> done)
        {
            Debug.Log($"[CHILD-ADS] Simulated rewarded ad '{placement}'");
            GameRunner.Instance.Delay(0.5f, () => done?.Invoke(true));
        }
    }

    /// <summary>A platform without ads.</summary>
    public class NoAds : IRewardedAds
    {
        public bool Supported => false;
        public bool IsReady(string placement) => false;
        public void Show(string placement, Action<bool> done) => done?.Invoke(false);
    }

    /// <summary>Editor and Windows development builds: purchases succeed at once.</summary>
    public class SimulatedStore : IStore
    {
        public bool Available => true;
        public string Price(string productId) => null;

        public void Purchase(string productId, Action<bool> done)
        {
            Debug.Log($"[CHILD-IAP] Simulated purchase '{productId}'");
            done?.Invoke(Grant.Product(productId));
        }
    }

    /// <summary>A platform without purchases (the web build).</summary>
    public class NoStore : IStore
    {
        public bool Available => false;
        public string Price(string productId) => null;
        public void Purchase(string productId, Action<bool> done) => done?.Invoke(false);
    }
}
