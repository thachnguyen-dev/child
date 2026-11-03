using System.Collections.Generic;
using ChildGame.UI;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.MiniGames.ChuonChuon
{
    /// <summary>
    /// Official generated art for the dragonfly hunt (owner 2026-10-08: upgrade the graphics): dragonflies with big
    /// glossy compound eyes, a striped thorax, a ringed tail and veined, faintly iridescent wings; reeds standing in the
    /// pond; a blurred background for the close-up. Cached for the session.
    /// </summary>
    public static class DragonflyArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static bool Cached(string key, out Sprite s) => cache.TryGetValue(key, out s) && s != null;

        /// <summary>
        /// The body (head, thorax, tail) facing left, in the layout of <see cref="DragonflyView"/>: head at (-1.25, 0),
        /// thorax at (-0.7, 0), tail tip at (1.75, -0.05). Pivot at the origin.
        /// </summary>
        public static Sprite Body(Color color)
        {
            var key = "body" + ColorUtility.ToHtmlStringRGB(color);
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(new Rect(-1.6f, -0.35f, 3.5f, 0.7f), 120f);
            var dark = Color.Lerp(color, Color.black, 0.45f);
            var light = Color.Lerp(color, Color.white, 0.45f);
            // Tail: ten segments, each a little capsule with a dark ring at the joint, thinning toward the tip.
            for (var i = 9; i >= 0; i--)
            {
                var t0 = i / 10f;
                var t1 = (i + 1) / 10f;
                Vector2 At(float t) => new Vector2(-0.38f + t * 2.1f, -Mathf.Pow(t, 2f) * 0.06f);
                var rad = Mathf.Lerp(0.085f, 0.045f, t0);
                var a = At(t0);
                var b = At(t1);
                r.Shape(p => Capsule(p, a, b, rad), color, dark, light, 0.022f, rad * 0.9f);
                r.Line(b + new Vector2(0, -rad), b + new Vector2(0, rad), 2.2f, dark, 0.8f, true);
            }
            // Claspers at the tip.
            r.Shape(p => Capsule(p, new Vector2(1.72f, -0.06f), new Vector2(1.82f, -0.03f), 0.02f), dark, dark, light, 0.015f, 0.02f);
            r.Shape(p => Capsule(p, new Vector2(1.72f, -0.06f), new Vector2(1.81f, -0.1f), 0.02f), dark, dark, light, 0.015f, 0.02f);
            // Thorax with two pale stripes, then the head.
            r.Shape(p => Ellipse(p, new Vector2(-0.68f, 0f), new Vector2(0.33f, 0.2f)), Color.Lerp(color, dark, 0.25f), dark, light, 0.025f, 0.12f,
                (p, c) => Mathf.Abs(Mathf.Sin((p.x + 0.68f) * 18f)) > 0.92f ? Color.Lerp(c, light, 0.5f) : c);
            r.Shape(p => Ellipse(p, new Vector2(-1.2f, 0f), new Vector2(0.16f, 0.17f)), color, dark, light, 0.025f, 0.08f);
            // Compound eyes: big glossy domes with a glint.
            foreach (var sy in new[] { -1f, 1f })
            {
                var c = new Vector2(-1.3f, sy * 0.12f);
                r.Shape(p => Ellipse(p, c, new Vector2(0.15f, 0.13f)), Color.Lerp(color, new Color32(40, 40, 56, 255), 0.6f), new Color32(24, 24, 32, 255),
                    Color.Lerp(color, Color.white, 0.3f), 0.022f, 0.1f,
                    (p, col) => Mathf.Abs(Mathf.Sin(p.x * 160f)) * Mathf.Abs(Mathf.Sin(p.y * 160f)) > 0.85f ? (Color)col * 0.85f : col); // facets
                var g = c + new Vector2(-0.05f, 0.04f);
                r.Shape(p => (p - g).magnitude - 0.03f, Color.white, Color.white, Color.white, 0f, 0.01f);
            }
            var s = r.ToSprite("DragonflyBody", Vector2.zero);
            cache[key] = s;
            return s;
        }

        /// <summary>
        /// One wing, root on the left (pivot) and 1.15 long: clear membrane with a fine vein net, a stronger leading edge,
        /// a dark spot near the tip (pterostigma) and a faint rainbow sheen.
        /// </summary>
        public static Sprite Wing()
        {
            if (Cached("wing", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.02f, -0.2f, 1.2f, 0.4f), 160f);
            float Shape(Vector2 p)
            {
                var t = Mathf.Clamp01(p.x / 1.15f);
                var half = Mathf.Lerp(0.07f, 0.16f, Mathf.Sin(t * Mathf.PI * 0.8f)) * (t > 0.85f ? Mathf.Sqrt(Mathf.Clamp01((1f - t) / 0.15f)) : 1f);
                return Mathf.Max(Mathf.Abs(p.y + (t * 0.02f)) - half, Mathf.Max(-p.x, p.x - 1.15f));
            }
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var d = Shape(p);
                if (d > 1f / r.ppu) continue;
                var t = Mathf.Clamp01(p.x / 1.15f);
                // Membrane with a rainbow sheen sliding along the wing.
                var hue = Mathf.Repeat(t * 0.7f + p.y * 2f, 1f);
                var sheen = Color.HSVToRGB(hue, 0.25f, 1f);
                var c = Color.Lerp(new Color(0.9f, 0.96f, 1f), sheen, 0.25f);
                var a = 0.32f;
                // Veins: lines along the wing and cross-veins forming small cells.
                var along = Mathf.Abs(Mathf.Sin(p.y * 95f + t * 3f));
                var cross = Mathf.Abs(Mathf.Sin(p.x * 70f + Mathf.Sin(p.y * 40f) * 0.8f));
                if (along > 0.93f || cross > 0.95f) { c = new Color(0.35f, 0.4f, 0.45f); a = 0.55f; }
                // Leading edge and the dark spot near the tip.
                if (d > -2.2f / r.ppu) { c = new Color(0.3f, 0.32f, 0.36f); a = 0.85f; }
                if (t > 0.8f && t < 0.88f && p.y > 0.0f) { c = new Color(0.2f, 0.18f, 0.16f); a = 0.9f; }
                a *= Mathf.Clamp01(0.5f - d * r.ppu);
                r.px[y * r.w + x] = new Color(c.r, c.g, c.b, a);
            }
            var s = r.ToSprite("DragonflyWing", Vector2.zero);
            cache["wing"] = s;
            return s;
        }

        /// <summary>
        /// A reed standing in the pond, <paramref name="height"/> tall from its base (pivot): a slender stalk leaning a little,
        /// two long blades from the base and a thin tip for a dragonfly to perch on.
        /// </summary>
        public static Sprite Reed(float height, int variant)
        {
            var key = $"reed{height:0.0}_{variant}";
            if (Cached(key, out var cached)) return cached;
            var rng = new System.Random(700 + variant);
            var r = new ArtRaster(new Rect(-1.0f, -0.1f, 2.0f, height + 0.4f), 60f);
            var lean = ((float)rng.NextDouble() - 0.5f) * 0.25f;
            var stalk = new Color32(108, 140, 70, 255);
            Vector2 At(float t) => new Vector2(lean * Mathf.Sin(t * Mathf.PI), t * height); // the tip stays over the base
            for (var i = 0; i < 30; i++)
            {
                var t0 = i / 30f;
                var t1 = (i + 1) / 30f;
                r.Line(At(t0), At(t1), Mathf.Lerp(6f, 2.6f, t0), Color.Lerp(stalk, new Color32(150, 170, 90, 255), t0), 1f, false);
            }
            // Nodes on the stalk.
            for (var k = 1; k < 5; k++)
            {
                var p = At(k / 5f);
                r.Line(p + new Vector2(-0.05f, 0), p + new Vector2(0.05f, 0), 2f, new Color32(80, 106, 54, 255), 0.9f, false);
            }
            // Blades sweeping out from the base.
            foreach (var side in new[] { -1f, 1f })
            {
                var len = height * (0.45f + (float)rng.NextDouble() * 0.2f);
                var prev = Vector2.zero;
                for (var i = 1; i <= 20; i++)
                {
                    var t = i / 20f;
                    var p = new Vector2(side * (Mathf.Sin(t * 1.6f) * 0.55f), t * len);
                    r.Line(prev, p, Mathf.Lerp(7f, 1.2f, t), Color.Lerp(new Color32(90, 132, 62, 255), new Color32(140, 176, 92, 255), t), 1f, false);
                    prev = p;
                }
            }
            var s = r.ToSprite("Reed", Vector2.zero);
            cache[key] = s;
            return s;
        }

        /// <summary>
        /// The near bank at the bottom of the pond scene (foreground): a grassy edge with tufts, pivot at the top-left
        /// corner of its grass line.
        /// </summary>
        public static Sprite NearBank(float width)
        {
            var key = "nearbank" + width;
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(new Rect(0f, -1.6f, width, 1.9f), 40f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var edge = 0.05f * Mathf.Sin(p.x * 2.3f) + 0.08f * (ValueNoise(p.x * 1.5f, 0.5f, 3) - 0.5f);
                if (p.y > edge) continue;
                var n = ValueNoise(p.x * 3f, p.y * 3f, 4);
                var c = Color.Lerp(new Color32(84, 134, 58, 255), new Color32(118, 166, 74, 255), n);
                if (p.y > edge - 0.08f) c = Color.Lerp(c, new Color32(150, 190, 96, 255), 0.4f);
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            var rng = new System.Random(5);
            for (var i = 0; i < (int)(width * 14); i++)
            {
                var p = new Vector2((float)rng.NextDouble() * width, -(float)rng.NextDouble() * 0.3f);
                for (var b = 0; b < 3; b++)
                {
                    var a = Mathf.PI * 0.5f + ((float)rng.NextDouble() - 0.5f) * 1.2f;
                    r.Line(p, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.18f + (float)rng.NextDouble() * 0.15f), 2f,
                        Color.Lerp(new Color32(70, 120, 50, 255), new Color32(150, 196, 96, 255), (float)rng.NextDouble()), 1f, false);
                }
            }
            var s = r.ToSprite("NearBank", Vector2.zero);
            cache[key] = s;
            return s;
        }

        /// <summary>The close-up's background: sky, then the far bank and pond melted into soft out-of-focus blobs.</summary>
        public static Sprite Bokeh(Rect world)
        {
            var key = "bokeh" + world;
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(world, 18f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var t = Mathf.InverseLerp(world.yMin, world.yMax, p.y);
                Color c = t > 0.55f ? Color.Lerp(new Color32(196, 226, 240, 255), new Color32(130, 186, 232, 255), (t - 0.55f) / 0.45f)
                    : t > 0.35f ? Color.Lerp(new Color32(120, 160, 104, 255), new Color32(196, 226, 240, 255), (t - 0.35f) / 0.2f)
                    : Color.Lerp(new Color32(96, 150, 168, 255), new Color32(120, 160, 104, 255), t / 0.35f);
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            var rng = new System.Random(9);
            for (var i = 0; i < 40; i++)
            {
                var c = new Vector2(Mathf.Lerp(world.xMin, world.xMax, (float)rng.NextDouble()), Mathf.Lerp(world.yMin + world.height * 0.3f, world.yMin + world.height * 0.62f, (float)rng.NextDouble()));
                var rad = 0.6f + (float)rng.NextDouble() * 1.4f;
                var col = Color.Lerp(new Color32(90, 140, 80, 255), new Color32(170, 206, 130, 255), (float)rng.NextDouble());
                for (var y = 0; y < r.h; y++)
                for (var x = 0; x < r.w; x++)
                {
                    var d = (r.World(x, y) - c).magnitude / rad;
                    if (d < 1f) r.Blend(x, y, col, (1f - d * d) * 0.35f);
                }
            }
            // A few soft sparkles off the water.
            for (var i = 0; i < 14; i++)
            {
                var c = new Vector2(Mathf.Lerp(world.xMin, world.xMax, (float)rng.NextDouble()), Mathf.Lerp(world.yMin, world.yMin + world.height * 0.3f, (float)rng.NextDouble()));
                var rad = 0.2f + (float)rng.NextDouble() * 0.3f;
                for (var y = 0; y < r.h; y++)
                for (var x = 0; x < r.w; x++)
                {
                    var d = (r.World(x, y) - c).magnitude / rad;
                    if (d < 1f) r.Blend(x, y, Color.white, (1f - d) * 0.5f);
                }
            }
            var s = r.ToSprite("Bokeh");
            cache[key] = s;
            return s;
        }

        /// <summary>The thick reed stem in the close-up, in soft focus: rounded, with a node, pivot at its bottom.</summary>
        public static Sprite Stem(float height)
        {
            var key = "stem" + height;
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.3f, 0f, 0.6f, height), 60f);
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x) - 0.17f, -p.y) - 0.02f, new Color32(120, 158, 80, 255), new Color32(76, 110, 50, 255),
                new Color32(186, 214, 130, 255), 0.03f, 0.14f,
                (p, c) => Mathf.Abs(p.y - height * 0.45f) < 0.04f ? (Color)c * 0.8f : c);
            var s = r.ToSprite("Stem", Vector2.zero);
            cache[key] = s;
            return s;
        }
    }
}
