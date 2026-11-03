using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChildGame.UI
{
    /// <summary>
    /// Builds the placeholder uGUI in code: canvases, panels, labels, buttons and inputs, with one cheerful palette and
    /// a Vietnamese-capable font (Be Vietnam Pro, OFL). Real art replaces the generated sprites later.
    /// </summary>
    public static class Ui
    {
        // ---- Palette (placeholder until the art direction exists) ----
        public static readonly Color Cream = new Color32(255, 248, 231, 255);
        public static readonly Color Ink = new Color32(61, 44, 41, 255);
        public static readonly Color InkSoft = new Color32(122, 98, 90, 255);
        public static readonly Color Sun = new Color32(255, 196, 61, 255);
        public static readonly Color Leaf = new Color32(88, 178, 92, 255);
        public static readonly Color Sky = new Color32(79, 167, 226, 255);
        public static readonly Color Brick = new Color32(226, 92, 72, 255);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color StarOn = new Color32(255, 190, 30, 255);
        public static readonly Color StarOff = new Color32(210, 200, 190, 255);

        public const float ReferenceHeight = 1080f;

        // ---- Fonts ----
        private static TMP_FontAsset bold, regular;
        public static TMP_FontAsset Bold => bold ??= MakeFont("BeVietnamPro-Bold");
        public static TMP_FontAsset Regular => regular ??= MakeFont("BeVietnamPro-Regular");

        private static TMP_FontAsset MakeFont(string resource)
        {
            var font = Resources.Load<Font>("Fonts/" + resource);
            var asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = resource;
            return asset;
        }

        // ---- Generated sprites ----
        private static Sprite round, star, circle;
        public static Sprite Round => round ??= MakeRound(64, 24);
        public static Sprite Star => star ??= MakeStar(128);
        public static Sprite Circle => circle ??= MakeCircle(128);

        private static Sprite MakeRound(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                var cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                var a = Mathf.Clamp01(radius - d + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        private static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            var r = size * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite MakeStar(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            var c = new Vector2(size * 0.5f, size * 0.48f);
            var pts = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var ang = Mathf.PI / 2 + i * Mathf.PI / 5;
                var rad = (i % 2 == 0 ? 0.5f : 0.21f) * size;
                pts[i] = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
            }
            // 4x4 supersampling for smooth edges.
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var hits = 0;
                for (var sy = 0; sy < 4; sy++)
                for (var sx = 0; sx < 4; sx++)
                    if (Inside(pts, new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f))) hits++;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static bool Inside(Vector2[] poly, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        // ---- Canvas ----

        /// <summary>A screen-space canvas; returns its safe-area root (children stay clear of notches).</summary>
        public static RectTransform Canvas(string name, int order)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            var safe = Rect(go.transform, "Safe");
            safe.gameObject.AddComponent<SafeArea>();
            return safe;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(go);
        }

        // ---- Layout helpers ----

        /// <summary>A child RectTransform that fills its parent.</summary>
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Anchors at <paramref name="anchor"/> (0..1) with a fixed size, offset by <paramref name="pos"/>.</summary>
        public static T Place<T>(this T c, Vector2 anchor, Vector2 size, Vector2 pos = default) where T : Component
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return c;
        }

        /// <summary>Stretches between two anchors with an inset.</summary>
        public static T Stretch<T>(this T c, Vector2 min, Vector2 max, float inset = 0) where T : Component
        {
            var rt = (RectTransform)c.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return c;
        }

        public static T Size<T>(this T c, float width, float height) where T : Component
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (width > 0) le.preferredWidth = width;
            if (height > 0) le.preferredHeight = height;
            return c;
        }

        public static VerticalLayoutGroup Column(Transform parent, string name, float spacing, int padding = 0)
        {
            var rt = Rect(parent, name);
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = new RectOffset(padding, padding, padding, padding);
            g.childAlignment = TextAnchor.MiddleCenter;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup Row(Transform parent, string name, float spacing)
        {
            var rt = Rect(parent, name);
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.childAlignment = TextAnchor.MiddleCenter;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            return g;
        }

        // ---- Elements ----

        public static Image Panel(Transform parent, string name, Color color, bool rounded = true)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = Round;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Image Icon(Transform parent, string name, Sprite sprite, Color color, float size)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(size, size));
            return img.Size(size, size);
        }

        /// <summary>A label showing a localization key; it follows language switches.</summary>
        public static TextMeshProUGUI Label(Transform parent, string key, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
        {
            var text = Text(parent, "", size, color, align, bold);
            text.name = "txt." + key;
            text.gameObject.AddComponent<LocText>().Key = key;
            return text;
        }

        /// <summary>A label with fixed text (names, numbers).</summary>
        public static TextMeshProUGUI Text(Transform parent, string content, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = true)
        {
            var rt = Rect(parent, "txt");
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold ? Bold : Regular;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = content;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>A rounded button with a localized label. Its GameObject is named <c>btn.&lt;key&gt;</c>.</summary>
        public static Button Button(Transform parent, string key, Action onClick, Color color, float fontSize = 46)
        {
            var img = Panel(parent, "btn." + key, color);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(() => Sfx.Play(Snd.Tap, 0.5f));
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var label = Label(img.transform, key, fontSize, Color.white);
            label.Stretch(Vector2.zero, Vector2.one, 12);
            img.gameObject.AddComponent<PressBounce>();
            return button;
        }

        /// <summary>A text input with the placeholder from <paramref name="placeholderKey"/>.</summary>
        public static TMP_InputField Input(Transform parent, string name, string placeholderKey, int limit)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources { inputField = Round });
            go.name = name;
            go.transform.SetParent(parent, false);
            var field = go.GetComponent<TMP_InputField>();
            var bg = go.GetComponent<Image>();
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.fontAsset = Bold;
            field.pointSize = 52;
            field.textComponent.color = Ink;
            field.textComponent.alignment = TextAlignmentOptions.Center;
            var placeholder = (TextMeshProUGUI)field.placeholder;
            placeholder.color = new Color(Ink.r, Ink.g, Ink.b, 0.35f);
            placeholder.alignment = TextAlignmentOptions.Center;
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.gameObject.AddComponent<LocText>().Key = placeholderKey;
            return field;
        }

        public static Slider Slider(Transform parent, string name, float value, Action<float> changed)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources { standard = Round, background = Round, knob = Circle });
            go.name = name;
            go.transform.SetParent(parent, false);
            var slider = go.GetComponent<Slider>();
            slider.value = value;
            foreach (var img in go.GetComponentsInChildren<Image>())
            {
                if (img.name == "Background") img.color = StarOff;
                else if (img.name == "Fill") img.color = Leaf;
                else if (img.name == "Handle") img.color = Sun;
            }
            ((RectTransform)slider.handleRect).sizeDelta = new Vector2(48, 0);
            slider.onValueChanged.AddListener(v => changed?.Invoke(v));
            return slider;
        }

        /// <summary>Three stars, lit for each difficulty already won.</summary>
        public static HorizontalLayoutGroup Stars(Transform parent, MiniGameRecord record, float size)
        {
            var row = Row(parent, "Stars", size * 0.15f);
            for (var i = 0; i < Progress.PointsPerGame; i++)
                Icon(row.transform, "star" + i, Star, record != null && record.Won((Difficulty)i) ? StarOn : StarOff, size);
            return row;
        }

        public static string DifficultyKey(Difficulty d) => "difficulty." + d.ToString().ToLowerInvariant();

        public static Color DifficultyColor(Difficulty d) => d == Difficulty.Easy ? Leaf : d == Difficulty.Normal ? Sky : Brick;

        // ---- Modal screens ----

        /// <summary>
        /// A modal screen: a dimmed full-screen canvas with a centered card. Registered with <see cref="Screens"/>; Back
        /// calls <paramref name="onBack"/> (null = Back does nothing on this screen).
        /// </summary>
        public static RectTransform Modal(string name, Vector2 cardSize, Action onBack, out GameObject root, int order = 20)
        {
            var safe = Canvas(name, order);
            root = safe.root.gameObject;
            var dim = Panel(safe.parent, "Dim", Dim, false);
            dim.transform.SetAsFirstSibling();
            var card = Panel(safe, "Card", Cream).Place(new Vector2(0.5f, 0.5f), cardSize);
            Screens.Push(root, onBack);
            return (RectTransform)card.transform;
        }
    }
}
