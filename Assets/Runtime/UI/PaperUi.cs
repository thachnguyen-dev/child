using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.UI
{
    /// <summary>
    /// The yard's UI look (owner 2026-10-08: "complete the UI of the yard"), matching the owner's ink-and-watercolour
    /// painting: paper tags with an ink border (9-sliced, any size), and drawn icons — the đồng coin (an old coin with a
    /// square hole), the village shop, settings, a child's face for the profile. Cached for the session.
    /// </summary>
    public static class PaperUi
    {
        public static readonly Color InkColor = new Color32(58, 42, 32, 255);
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static bool Cached(string key, out Sprite s) => cache.TryGetValue(key, out s) && s != null;

        /// <summary>A sheet of cream paper with an ink border and a soft shadow, 9-sliced so it stretches cleanly.</summary>
        public static Sprite Paper(Color tint)
        {
            var key = "paper" + ColorUtility.ToHtmlStringRGB(tint);
            if (Cached(key, out var c0)) return c0;
            const int size = 160, border = 48;
            var r = new ArtRaster(new Rect(0, 0, size, size), 1f) { OutlineColor = InkColor };
            float Rounded(Vector2 p) { var q = new Vector2(Mathf.Abs(p.x - 80f) - 50f, Mathf.Abs(p.y - 81f) - 50f); return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 22f; }
            r.Shape(p => Rounded(p - new Vector2(3f, -4f)), new Color(0.2f, 0.12f, 0.06f, 0.25f), new Color(0.2f, 0.12f, 0.06f, 0.25f), new Color(0.2f, 0.12f, 0.06f, 0.25f), 0f, 4f);
            r.Shape(Rounded, tint, Color.Lerp(tint, new Color32(200, 170, 120, 255), 0.35f), Color.Lerp(tint, Color.white, 0.4f), 4f, 14f,
                (p, c) => (Color)c * (0.97f + 0.05f * ValueNoise(p.x * 0.08f, p.y * 0.08f, 3))); // paper grain
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels(r.px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            cache[key] = s;
            return s;
        }

        /// <summary>Puts the paper look on a panel image (sliced, white-tinted so the paper's own colour shows).</summary>
        public static Image Style(Image img, Color paper)
        {
            img.sprite = Paper(paper);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1.6f;
            img.color = Color.white;
            return img;
        }

        /// <summary>A paper button: the paper look, an icon on the left and ink-coloured text.</summary>
        public static Button StyleButton(Button b, Color paper, Sprite icon)
        {
            Style((Image)b.targetGraphic, paper);
            var text = b.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = InkColor;
                if (icon != null) text.margin = new Vector4(70, 0, 10, 0);
            }
            if (icon != null)
            {
                var img = Ui.Icon(b.transform, "Icon", icon, Color.white, 70);
                ((RectTransform)img.transform).Place(new Vector2(0f, 0.5f), new Vector2(70, 70), new Vector2(18, 0));
            }
            var colors = b.colors;
            colors.highlightedColor = new Color(1f, 0.97f, 0.9f);
            colors.pressedColor = new Color(0.88f, 0.84f, 0.76f);
            b.colors = colors;
            return b;
        }

        /// <summary>The đồng: an old bronze coin with a square hole in the middle.</summary>
        public static Sprite Coin()
        {
            if (Cached("coin", out var c0)) return c0;
            var r = new ArtRaster(new Rect(-0.55f, -0.55f, 1.1f, 1.1f), 128f) { OutlineColor = InkColor };
            var brass = new Color32(226, 176, 70, 255);
            r.Shape(p => Mathf.Max(p.magnitude - 0.48f, -(Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - 0.13f)), brass, new Color32(176, 120, 40, 255),
                new Color32(255, 230, 150, 255), 0.04f, 0.12f,
                (p, c) =>
                {
                    // A raised rim, and four marks around the hole like the characters on old coins.
                    var rim = Mathf.Abs(p.magnitude - 0.4f) < 0.025f;
                    var mark = p.magnitude > 0.2f && p.magnitude < 0.32f && (Mathf.Abs(p.x) < 0.035f || Mathf.Abs(p.y) < 0.035f);
                    return rim || mark ? Color.Lerp(c, new Color32(150, 96, 30, 255), 0.6f) : c;
                });
            r.Shape(p => Mathf.Abs(Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) - 0.13f) - 0.02f, new Color32(120, 76, 24, 255), new Color32(120, 76, 24, 255), new Color32(120, 76, 24, 255), 0f, 0.01f);
            var s = r.ToSprite("Coin");
            cache["coin"] = s;
            return s;
        }

        /// <summary>The village shop: a little stall with a striped awning and jars on the counter.</summary>
        public static Sprite ShopIcon()
        {
            if (Cached("shop", out var c0)) return c0;
            var r = new ArtRaster(new Rect(-0.55f, -0.5f, 1.1f, 1.05f), 128f) { OutlineColor = InkColor };
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x) - 0.42f, Mathf.Abs(p.y + 0.18f) - 0.27f), new Color32(170, 116, 70, 255), new Color32(120, 80, 48, 255),
                new Color32(210, 160, 110, 255), 0.035f, 0.06f);
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x) - 0.36f, Mathf.Abs(p.y + 0.1f) - 0.12f), new Color32(80, 56, 40, 255), new Color32(60, 40, 30, 255), new Color32(110, 80, 60, 255), 0.02f, 0.03f);
            foreach (var x in new[] { -0.22f, 0f, 0.22f })
            {
                var col = x < -0.1f ? new Color32(240, 120, 150, 255) : x > 0.1f ? new Color32(250, 200, 70, 255) : new Color32(120, 190, 120, 255);
                r.Shape(p => Mathf.Max(Mathf.Abs(p.x - x) - 0.07f, Mathf.Abs(p.y + 0.1f) - 0.09f), col, Color.Lerp(col, Color.black, 0.3f), Color.Lerp(col, Color.white, 0.4f), 0.02f, 0.02f);
            }
            // The awning: red and white stripes with a scalloped edge.
            float Awning(Vector2 p)
            {
                var top = Mathf.Max(Mathf.Abs(p.x) - 0.5f + (p.y - 0.12f) * 0.25f, Mathf.Max(p.y - 0.38f, 0.12f - p.y));
                var scallop = (new Vector2(Mathf.Repeat(p.x + 0.5f, 0.2f) - 0.1f, p.y - 0.12f)).magnitude - 0.1f;
                return p.y < 0.12f ? Mathf.Max(scallop, Mathf.Abs(p.x) - 0.5f) : top;
            }
            r.Shape(Awning, new Color32(214, 62, 52, 255), new Color32(160, 36, 34, 255), new Color32(246, 130, 110, 255), 0.035f, 0.05f,
                (p, c) => Mathf.Repeat(p.x + 0.5f, 0.2f) < 0.1f ? Color.Lerp(c, new Color32(250, 244, 230, 255), 0.9f) : c);
            var s = r.ToSprite("ShopIcon");
            cache["shop"] = s;
            return s;
        }

        /// <summary>Settings: a cog, inked.</summary>
        public static Sprite GearIcon()
        {
            if (Cached("gear", out var c0)) return c0;
            var r = new ArtRaster(new Rect(-0.55f, -0.55f, 1.1f, 1.1f), 128f) { OutlineColor = InkColor };
            float Gear(Vector2 p)
            {
                var a = Mathf.Atan2(p.y, p.x);
                var teeth = Mathf.Clamp(Mathf.Cos(a * 8f) * 3f, -1f, 1f) * 0.06f;
                return Mathf.Max(p.magnitude - (0.36f + teeth), -(p.magnitude - 0.13f));
            }
            r.Shape(Gear, new Color32(120, 150, 180, 255), new Color32(80, 104, 130, 255), new Color32(180, 204, 226, 255), 0.04f, 0.1f);
            var s = r.ToSprite("Gear");
            cache["gear"] = s;
            return s;
        }

        /// <summary>A child's face for the profile card (boy: short hair; girl: a bob with a ribbon).</summary>
        public static Sprite Face(bool girl)
        {
            var key = "face" + girl;
            if (Cached(key, out var c0)) return c0;
            var r = new ArtRaster(new Rect(-0.55f, -0.55f, 1.1f, 1.1f), 128f) { OutlineColor = InkColor };
            var skin = new Color32(238, 194, 154, 255);
            var hair = new Color32(44, 34, 28, 255);
            if (girl) r.Shape(p => Ellipse(p, new Vector2(0, -0.02f), new Vector2(0.44f, 0.44f)), hair, Color.Lerp(hair, Color.black, 0.3f), new Color32(110, 96, 88, 255), 0.03f, 0.06f);
            r.Shape(p => Ellipse(p, new Vector2(0, -0.04f), new Vector2(0.34f, 0.38f)), skin, new Color32(206, 150, 112, 255), new Color32(252, 220, 190, 255), 0.03f, 0.1f);
            foreach (var x in new[] { -0.35f, 0.35f })
                r.Shape(p => Ellipse(p, new Vector2(x, -0.05f), new Vector2(0.06f, 0.09f)), skin, new Color32(206, 150, 112, 255), skin, 0.025f, 0.03f);
            // Hair over the top.
            r.Shape(p => Mathf.Max(Ellipse(p, new Vector2(0, 0.06f), new Vector2(0.37f, 0.36f)), (girl ? 0.02f : 0.08f) - p.y + Mathf.Sin(p.x * 18f) * 0.025f),
                hair, Color.Lerp(hair, Color.black, 0.3f), new Color32(110, 96, 88, 255), 0.03f, 0.06f);
            if (girl) r.Shape(p => Mathf.Min(Ellipse(p, new Vector2(0.24f, 0.32f), new Vector2(0.09f, 0.06f)), Ellipse(p, new Vector2(0.38f, 0.3f), new Vector2(0.09f, 0.06f))),
                new Color32(226, 70, 80, 255), new Color32(170, 40, 50, 255), new Color32(250, 140, 150, 255), 0.02f, 0.03f);
            // Eyes, cheeks, a smile.
            foreach (var x in new[] { -0.13f, 0.13f })
                r.Shape(p => Ellipse(p, new Vector2(x, -0.03f), new Vector2(0.035f, 0.05f)), hair, hair, hair, 0f, 0.01f);
            foreach (var x in new[] { -0.2f, 0.2f })
                r.Shape(p => Ellipse(p, new Vector2(x, -0.13f), new Vector2(0.06f, 0.035f)), new Color32(244, 160, 140, 255), new Color32(244, 160, 140, 255), new Color32(244, 160, 140, 255), 0f, 0.02f);
            for (var i = 0; i < 10; i++)
            {
                var a0 = Mathf.Lerp(-2.4f, -0.74f, i / 10f);
                var a1 = Mathf.Lerp(-2.4f, -0.74f, (i + 1) / 10f);
                r.Line(new Vector2(Mathf.Cos(a0) * 0.1f, -0.1f + Mathf.Sin(a0) * 0.08f), new Vector2(Mathf.Cos(a1) * 0.1f, -0.1f + Mathf.Sin(a1) * 0.08f), 3f, new Color32(150, 70, 60, 255), 1f, true);
            }
            var s = r.ToSprite("Face");
            cache[key] = s;
            return s;
        }
    }
}
