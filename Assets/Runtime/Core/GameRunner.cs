using System;
using System.Collections;
using ChildGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChildGame
{
    /// <summary>
    /// The one persistent object: Android Back, saving on pause, the sound pool (Sfx) and coroutines for code without a
    /// MonoBehaviour.
    /// </summary>
    public class GameRunner : MonoBehaviour
    {
        public static GameRunner Instance { get; private set; }

        internal static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("Game");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameRunner>();
            if (Dev.Capture.Enabled) go.AddComponent<Dev.Capture>();
        }

        public Coroutine Delay(float seconds, Action action) => StartCoroutine(DelayRoutine(seconds, action));

        private static IEnumerator DelayRoutine(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action?.Invoke();
        }

        private void Update()
        {
            // Android's Back button arrives as Escape.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Screens.Back();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Game.Commit();
        }

        private void OnApplicationQuit() => Game.Commit();
    }
}
