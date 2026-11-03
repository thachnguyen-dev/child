#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using System;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace ChildGame.Services
{
    /// <summary>
    /// Rewarded ads through Unity LevelPlay on Android and iOS (owner 2026-10-09: LevelPlay account). One ad is kept loaded; after it is shown
    /// the next one loads. The reward counts only when LevelPlay reports it was earned. LevelPlay delivers its events on
    /// the main thread.
    /// </summary>
    public class LevelPlayAds : IRewardedAds
    {
        private LevelPlayRewardedAd ad;
        private Action<bool> pending;
        private bool earned;

        public bool Supported => true;

        public LevelPlayAds()
        {
#if UNITY_IOS
            GameRunner.Instance.StartCoroutine(AskTrackingThenInit());
#else
            Init();
#endif
        }

#if UNITY_IOS
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern int ChildAttStatus();
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void ChildAttRequest();

        /// <summary>
        /// iOS: the App Tracking Transparency prompt comes first (App Store privacy answers declare tracking), then the ads
        /// start whatever the answer; without permission they run without the device's advertising ID. The prompt needs
        /// the app to be active, so it waits for the first scene.
        /// </summary>
        private System.Collections.IEnumerator AskTrackingThenInit()
        {
            yield return new WaitForSecondsRealtime(1f);
            if (ChildAttStatus() == 0)
            {
                ChildAttRequest();
                while (ChildAttStatus() == 0) yield return new WaitForSecondsRealtime(0.25f);
            }
            Debug.Log("[CHILD-ADS] Tracking status " + ChildAttStatus());
            Init();
        }
#endif

        private void Init()
        {
            LevelPlay.OnInitSuccess += _ =>
            {
                ad = new LevelPlayRewardedAd(MonetizationIds.CurrentLevelPlayRewardedAdUnit);
                ad.OnAdLoadFailed += error =>
                {
                    Debug.LogWarning("[CHILD-ADS] Load failed: " + error);
                    GameRunner.Instance.Delay(30f, () => ad?.LoadAd()); // try again later
                };
                ad.OnAdRewarded += (_, _) => earned = true;
                ad.OnAdClosed += _ => Done();
                ad.OnAdDisplayFailed += (_, error) =>
                {
                    Debug.LogWarning("[CHILD-ADS] Show failed: " + error);
                    earned = false;
                    Done();
                };
                ad.LoadAd();
            };
            LevelPlay.OnInitFailed += error => Debug.LogWarning("[CHILD-ADS] LevelPlay init failed: " + error);
            // Development builds only: LevelPlay's Test Suite (Settings → "LevelPlay Test Suite") shows the ad units and networks
            // the dashboard sends for this app key, to diagnose ads on a real phone.
            if (Debug.isDebugBuild) LevelPlay.SetMetaData("is_test_suite", "enable");
            LevelPlay.Init(MonetizationIds.CurrentLevelPlayAppKey);
        }

        /// <summary>Opens LevelPlay's diagnostic Test Suite (development builds).</summary>
        public static void LaunchTestSuite() => LevelPlay.LaunchTestSuite();

        public bool IsReady(string placement) => ad != null && pending == null && ad.IsAdReady();

        public void Show(string placement, Action<bool> done)
        {
            if (!IsReady(placement))
            {
                done?.Invoke(false);
                return;
            }
            pending = done;
            earned = false;
            ad.ShowAd();
        }

        private void Done()
        {
            var done = pending;
            pending = null;
            done?.Invoke(earned);
            ad?.LoadAd();
        }
    }
}
#endif
