#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using AOT;

namespace ChildGame.Services
{
    /// <summary>Rewarded ads in the browser via H5 Games Ads (Plugins/WebGL/ChildWebAds.jslib).</summary>
    public class WebAds : IRewardedAds
    {
        [DllImport("__Internal")] private static extern int ChildWebAdsReady();
        [DllImport("__Internal")] private static extern void ChildWebAdsShow(string name, Action<int> callback);

        private static Action<bool> pending;

        public bool Supported => true;
        public bool IsReady(string placement) => ChildWebAdsReady() == 1 && pending == null;

        public void Show(string placement, Action<bool> done)
        {
            pending = done;
            ChildWebAdsShow(placement, OnDone);
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnDone(int ok)
        {
            var done = pending;
            pending = null;
            done?.Invoke(ok == 1);
        }
    }
}
#endif
