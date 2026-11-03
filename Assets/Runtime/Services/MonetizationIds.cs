namespace ChildGame.Services
{
    /// <summary>Every account-specific ID for ads and purchases, in one place (docs/4-release.md).</summary>
    public static class MonetizationIds
    {
        /// <summary>LevelPlay app key (Android), from the owner's LevelPlay dashboard (2026-10-09).</summary>
        public const string LevelPlayAppKey = "2898d16c5";

        /// <summary>LevelPlay rewarded ad unit: the shop's "watch an ad" button (owner 2026-10-09; h4xxrmisd6hvutyt was not a rewarded unit).</summary>
        public const string LevelPlayRewardedAdUnit = "dlefbzdvrcikhlhs";

        /// <summary>
        /// LevelPlay app key and rewarded ad unit for the iOS app (LevelPlay keeps a separate app per platform). The
        /// owner's iOS interstitial unit (5z30lsn92gezd5a4) is not used: iOS, like Android, has rewarded ads only.
        /// </summary>
        public const string LevelPlayIosAppKey = "28a960c1d"; // owner 2026-10-12
        public const string LevelPlayIosRewardedAdUnit = "jculme7hb1coky27"; // owner 2026-10-12

        /// <summary>The LevelPlay IDs for the platform this build runs on (Android or iOS).</summary>
#if UNITY_IOS
        public const string CurrentLevelPlayAppKey = LevelPlayIosAppKey;
        public const string CurrentLevelPlayRewardedAdUnit = LevelPlayIosRewardedAdUnit;
#else
        public const string CurrentLevelPlayAppKey = LevelPlayAppKey;
        public const string CurrentLevelPlayRewardedAdUnit = LevelPlayRewardedAdUnit;
#endif

        /// <summary>
        /// Web: the AdSense publisher ID ("ca-pub-…") for H5 Games Ads rewarded breaks. Empty = the web build shows no ad
        /// button. Set it in web/index.html's AdSense script tag as well.
        /// </summary>
        public const string AdSenseWebClient = "";

        /// <summary>Google Play in-app product ID for a cash package (consumable): dong_5000, dong_10000, dong_20000.</summary>
        public static string CashProduct(int amount) => "dong_" + amount;
    }
}
