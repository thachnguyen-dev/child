using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChildGame.UI
{
    /// <summary>Shows a localization key and refreshes when the language changes.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocText : MonoBehaviour
    {
        [SerializeField] private string key;
        private object[] args;

        public string Key
        {
            get => key;
            set
            {
                key = value;
                args = null;
                Refresh();
            }
        }

        public void SetFormat(string formatKey, params object[] formatArgs)
        {
            key = formatKey;
            args = formatArgs;
            Refresh();
        }

        private void OnEnable()
        {
            Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Loc.Changed -= Refresh;

        private void Refresh()
        {
            var t = GetComponent<TMP_Text>();
            if (t != null) t.text = args == null ? Loc.Get(key) : Loc.Format(key, args);
        }
    }

    /// <summary>Keeps a RectTransform inside <see cref="Screen.safeArea"/> (notches, rounded corners).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private Rect applied;
        private Vector2Int screen;

        private void OnEnable() => Apply();
        private void Update()
        {
            if (Screen.safeArea != applied || screen.x != Screen.width || screen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            applied = Screen.safeArea;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            rt.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Little squash when a button is pressed (cheerful feel, design §1).</summary>
    public class PressBounce : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private float target = 1f;

        public void OnPointerDown(PointerEventData e) => target = 0.92f;
        public void OnPointerUp(PointerEventData e) => target = 1f;
        public void OnPointerExit(PointerEventData e) => target = 1f;

        private void Update()
        {
            var s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 2.5f);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>
    /// The stack of open modal screens. Android Back closes the top one; with none open it goes to
    /// <see cref="SceneBack"/> (set by the hub / mini game session).
    /// </summary>
    public static class Screens
    {
        private static readonly List<(GameObject root, Action back)> stack = new List<(GameObject, Action)>();

        public static Action SceneBack;

        public static int Count
        {
            get
            {
                Prune();
                return stack.Count;
            }
        }

        public static void Push(GameObject root, Action onBack) => stack.Add((root, onBack));

        public static void Close(GameObject root)
        {
            stack.RemoveAll(s => s.root == root);
            if (root != null) UnityEngine.Object.Destroy(root);
        }

        public static void CloseAll()
        {
            Prune();
            foreach (var s in stack.ToArray()) Close(s.root);
        }

        public static void Back()
        {
            Prune();
            if (stack.Count > 0) stack[stack.Count - 1].back?.Invoke();
            else SceneBack?.Invoke();
        }

        private static void Prune() => stack.RemoveAll(s => s.root == null);
    }
}
