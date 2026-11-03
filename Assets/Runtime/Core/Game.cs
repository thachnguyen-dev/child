using ChildGame.Services;
using UnityEngine;

namespace ChildGame
{
    /// <summary>
    /// Global state: the save, services and the persistent <see cref="GameRunner"/>. Boots automatically before the
    /// first scene loads, so any scene can be played directly in the editor.
    /// </summary>
    public static class Game
    {
        public static SaveData Save { get; private set; }
        public static IRewardedAds Ads { get; set; }
        public static IStore Store { get; set; }
        /// <summary>Game-portal hooks (CrazyGames); does nothing elsewhere.</summary>
        public static IPlatform Platform { get; set; } = new NoPortal();

        public static bool Booted => Save != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Dev.Capture.ReadCommandLine();
            Save = SaveStore.Load();
            ApplyProfile();
            Loc.Load(Save.language);
            GameRunner.Create();
            CreateServices();
            Debug.Log($"[CHILD] Booted. Save: {SaveStore.FilePath}");
        }

        /// <summary>
        /// Real ads and purchases on Android and iOS, ads only on the web, simulated ones in the editor and on Windows.
        /// </summary>
        private static void CreateServices()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // No LevelPlay app key or rewarded unit for this platform yet: no ad button rather than a broken one.
            if (Dev.Capture.Enabled) Ads = new SimulatedAds();
            else if (string.IsNullOrEmpty(MonetizationIds.CurrentLevelPlayAppKey) ||
                     string.IsNullOrEmpty(MonetizationIds.CurrentLevelPlayRewardedAdUnit)) Ads = new NoAds();
            else Ads = new LevelPlayAds();
            Store = new IapStore();
#elif UNITY_WEBGL && CRAZYGAMES && !UNITY_EDITOR
            // CrazyGames (docs/5-crazygames.md): ads only through their SDK, no purchases on the web.
            Ads = new CrazyRewardedAds();
            Store = new NoStore();
            Platform = new CrazyPlatform();
            if (SaveStore.FreshStart)
            {
                // First visit: the language follows the player's CrazyGames locale (Vietnamese for vi-*, else English).
                var locale = Platform.Locale ?? "";
                Save.language = locale.StartsWith("vi") ? Language.Vietnamese : Language.English;
                Loc.Load(Save.language);
            }
#elif UNITY_WEBGL && !UNITY_EDITOR
            Ads = string.IsNullOrEmpty(MonetizationIds.AdSenseWebClient) ? new NoAds() : (IRewardedAds)new WebAds();
            Store = new NoStore();
#else
            Ads = new SimulatedAds();
            Store = new SimulatedStore();
#endif
        }

        /// <summary>Writes the save to disk.</summary>
        public static void Commit() => SaveStore.Write(Save);

        public static void SetLanguage(Language language)
        {
            Save.language = language;
            Commit();
            Loc.Load(language);
        }

        public static void SetProfile(string name, Gender gender)
        {
            Save.playerName = name.Trim();
            Save.gender = gender;
            Save.profileDone = true;
            ApplyProfile();
            Commit();
        }

        private static void ApplyProfile()
        {
            Loc.PlayerName = Save.playerName;
            Loc.PlayerGender = Save.gender;
        }

        /// <summary>Starts over with an empty save (tests and the capture harness).</summary>
        public static void ResetSave()
        {
            Save = new SaveData { language = Save?.language ?? Language.Vietnamese };
            ApplyProfile();
            Commit();
        }
    }
}
