#if UNITY_WEBGL && CRAZYGAMES && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace ChildGame.Services
{
    /// <summary>The CrazyGames SDK v3 calls (Plugins/WebGL/ChildCrazyGames.jslib).</summary>
    internal static class CrazySdk
    {
        [DllImport("__Internal")] public static extern int ChildCGReady();
        [DllImport("__Internal")] public static extern int ChildCGMuted();
        [DllImport("__Internal")] public static extern string ChildCGUserName();
        [DllImport("__Internal")] public static extern string ChildCGLocale();
        [DllImport("__Internal")] public static extern void ChildCGGameplayStart();
        [DllImport("__Internal")] public static extern void ChildCGGameplayStop();
        [DllImport("__Internal")] public static extern void ChildCGHappytime();
        [DllImport("__Internal")] public static extern void ChildCGRequestAd(string kind, Action<int> callback);
        [DllImport("__Internal")] public static extern string ChildCGGetItem(string key);
        [DllImport("__Internal")] public static extern int ChildCGSetItem(string key, string value);

        public static bool Ready => ChildCGReady() == 1;

        private static Action<bool> adDone;
        private static float timeScaleBefore = 1f;

        /// <summary>
        /// Requests a CrazyGames ad. While it plays the game is paused and silent (CrazyGames rule); <paramref name="done"/>
        /// gets true only when the ad finished.
        /// </summary>
        public static void RequestAd(string kind, Action<bool> done)
        {
            if (adDone != null) { done?.Invoke(false); return; } // one ad at a time
            adDone = done;
            ChildCGRequestAd(kind, OnAd);
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnAd(int code)
        {
            if (code == 1)
            {
                // Started: pause and mute only now, not at the request (CrazyGames rule).
                timeScaleBefore = Time.timeScale;
                Time.timeScale = 0f;
                AudioListener.pause = true;
                return;
            }
            if (AudioListener.pause)
            {
                AudioListener.pause = false;
                Time.timeScale = timeScaleBefore;
            }
            var done = adDone;
            adDone = null;
            done?.Invoke(code == 2);
        }
    }

    /// <summary>CrazyGames portal hooks: gameplay events, midgame ads at round ends, mute, user name and locale.</summary>
    public class CrazyPlatform : IPlatform
    {
        private bool playing;

        public bool Muted => CrazySdk.ChildCGMuted() == 1;

        public string UserName
        {
            get
            {
                var n = CrazySdk.ChildCGUserName();
                return string.IsNullOrEmpty(n) ? null : n;
            }
        }

        public string Locale => CrazySdk.ChildCGLocale();

        public void GameplayStart()
        {
            if (playing) return;
            playing = true;
            CrazySdk.ChildCGGameplayStart();
        }

        public void GameplayStop()
        {
            if (!playing) return;
            playing = false;
            CrazySdk.ChildCGGameplayStop();
        }

        public void Happytime() => CrazySdk.ChildCGHappytime();

        // The SDK itself caps midgame ads at one every 3 minutes and simply reports an error when it is too early.
        public void Interstitial(Action done) => CrazySdk.RequestAd("midgame", _ => done?.Invoke());
    }

    /// <summary>The shop's rewarded ad through CrazyGames. The reward is given only when the ad finished.</summary>
    public class CrazyRewardedAds : IRewardedAds
    {
        private bool busy;

        public bool Supported => true;

        // Whether an ad will fill is only known when asked; an empty answer tells the player to try again later.
        public bool IsReady(string placement) => CrazySdk.Ready && !busy;

        public void Show(string placement, Action<bool> done)
        {
            busy = true;
            CrazySdk.RequestAd("rewarded", ok =>
            {
                busy = false;
                done?.Invoke(ok);
            });
        }
    }
}
#endif
