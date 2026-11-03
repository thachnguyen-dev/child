using System.Collections.Generic;
using ChildGame.UI;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.MiniGames
{
    /// <summary>
    /// Shared official generated art (owner 2026-10-08: upgrade every mini game's graphics): a wooden school desk, a sheet
    /// torn from a Vietnamese squared notebook (vở ô ly), a summer sky with clouds, and desk props. Drawn with
    /// <see cref="ArtRaster"/> in the style of the hand art and cached for the session.
    /// </summary>
    public static class SceneArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly Color Shadow = new Color(0.12f, 0.06f, 0.02f, 0.3f);

        private static bool Cached(string key, out Sprite s) => cache.TryGetValue(key, out s) && s != null;

        // ---- Desk ----

        /// <summary>
        /// A varnished wooden school desk filling <paramref name="world"/>: long planks with flowing grain, the odd knot,
        /// seams between planks, scratches and a pencil doodle, a soft sheen from the window.
        /// </summary>
        public static Sprite Desk(Rect world, Color wood, int seed)
        {
            var key = $"desk{world}{ColorUtility.ToHtmlStringRGB(wood)}{seed}";
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(world, 40f);
            const float plank = 2.6f;
            var dark = Color.Lerp(wood, new Color32(60, 32, 18, 255), 0.45f);
            var light = Color.Lerp(wood, new Color32(255, 224, 170, 255), 0.25f);
            var rng = new System.Random(seed);
            var knots = new List<Vector2>();
            for (var i = 0; i < 6; i++)
                knots.Add(new Vector2(Mathf.Lerp(world.xMin, world.xMax, (float)rng.NextDouble()), Mathf.Lerp(world.yMin, world.yMax, (float)rng.NextDouble())));
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var row = Mathf.FloorToInt((p.y - world.yMin) / plank);
                var v = (p.y - world.yMin) / plank - row;
                var shift = Hash(row, 3, seed) * 40f;
                // Grain: stretched noise along the plank, bent around knots.
                var warp = 0f;
                foreach (var k in knots)
                {
                    var d = p - k;
                    warp += 0.35f * Mathf.Exp(-d.sqrMagnitude * 2.5f) * Mathf.Sign(d.y);
                }
                var g = ValueNoise((p.x + shift) * 0.35f, (p.y + warp) * 9f, seed + row);
                var rings = 0.5f + 0.5f * Mathf.Sin((p.y + warp + g * 0.25f) * 26f + ValueNoise(p.x * 0.6f, row, seed) * 6f);
                var c = Color.Lerp(wood, dark, rings * 0.28f + g * 0.18f);
                c = Color.Lerp(c, light, Mathf.Clamp01(ValueNoise((p.x + shift) * 0.08f, p.y * 0.5f, seed + 9) - 0.55f) * 0.5f);
                // Knots: dark eyes in the grain.
                foreach (var k in knots)
                {
                    var d = ((p - k) / new Vector2(0.22f, 0.12f)).magnitude;
                    if (d < 1f) c = Color.Lerp(c, dark * 0.8f, (1f - d) * 0.9f);
                }
                // Plank seams and the bevel along them.
                if (v < 0.012f || v > 0.988f) c = dark * 0.75f;
                else if (v < 0.035f) c = Color.Lerp(c, light, 0.25f);
                // Window light across the varnish.
                var sheen = Mathf.Exp(-Mathf.Pow((p.x + p.y * 0.6f - world.xMin - world.width * 0.35f) / (world.width * 0.18f), 2f));
                c = Color.Lerp(c, new Color32(255, 240, 210, 255), sheen * 0.12f);
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            // Scratches, some pale and some dark.
            for (var i = 0; i < 26; i++)
            {
                var a = new Vector2(Mathf.Lerp(world.xMin, world.xMax, (float)rng.NextDouble()), Mathf.Lerp(world.yMin, world.yMax, (float)rng.NextDouble()));
                var ang = ((float)rng.NextDouble() - 0.5f) * 0.6f + (rng.NextDouble() < 0.3 ? 1.2f : 0f);
                var len = 0.2f + (float)rng.NextDouble() * 0.9f;
                var col = rng.NextDouble() < 0.6 ? light : dark;
                r.Line(a, a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len, 1.2f, col, 0.45f, false);
            }
            var s = r.ToSprite("Desk");
            cache[key] = s;
            return s;
        }

        // ---- Notebook paper ----

        /// <summary>
        /// A sheet torn from a squared school notebook (vở ô ly): the grid of big squares in violet, each divided by faint
        /// ruled lines, a red margin line, a ragged torn top edge and a soft shadow on the desk. The grid of
        /// <paramref name="cells"/> × <paramref name="cells"/> squares of <paramref name="cell"/> world units is centred on
        /// the sprite's pivot.
        /// </summary>
        public static Sprite NotebookGrid(int cells, float cell)
        {
            var key = $"nb{cells}_{cell:0.000}";
            if (Cached(key, out var cached)) return cached;
            var half = cells * cell * 0.5f;
            var margin = new Vector4(0.9f, 0.35f, 0.35f, 0.6f); // left, right, bottom, top (world)
            var area = new Rect(-half - margin.x - 0.2f, -half - margin.z - 0.25f, cells * cell + margin.x + margin.y + 0.45f, cells * cell + margin.z + margin.w + 0.45f);
            var r = new ArtRaster(area, 64f);
            var paper = new Color32(252, 250, 242, 255);
            var big = new Color32(150, 132, 200, 255);   // violet squares
            var ruled = new Color32(196, 188, 228, 255); // faint ô ly lines
            var red = new Color32(226, 104, 110, 255);
            float Sheet(Vector2 p)
            {
                var top = half + margin.w - 0.06f - 0.06f * ValueNoise(p.x * 9f, 0.5f, 5) - 0.03f * ValueNoise(p.x * 30f, 0.5f, 6);
                return Mathf.Max(Mathf.Max(-half - margin.x - p.x, p.x - half - margin.y), Mathf.Max(-half - margin.z - p.y, p.y - top));
            }
            r.Shape(p => Sheet(p - new Vector2(0.08f, -0.12f)), Shadow, Shadow, Shadow, 0f, 0.1f);
            var px = 1f / r.ppu;
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var d = Sheet(p);
                if (d > px) continue;
                var c = (Color)paper * (0.975f + 0.025f * ValueNoise(p.x * 40f, p.y * 40f, 2));
                var inGrid = p.x > -half - px && p.x < half + px && p.y > -half - px && p.y < half + px;
                if (inGrid)
                {
                    var gx = (p.x + half) / cell;
                    var gy = (p.y + half) / cell;
                    var lx = Mathf.Abs(gx - Mathf.Round(gx)) * cell * r.ppu;
                    var ly = Mathf.Abs(gy - Mathf.Round(gy)) * cell * r.ppu;
                    // Three faint ruled lines inside each square, as in a vở ô ly.
                    var sub = Mathf.Abs(gy * 4f - Mathf.Round(gy * 4f)) * cell / 4f * r.ppu;
                    if (sub < 0.8f) c = Color.Lerp(c, ruled, 0.8f - sub);
                    var line = Mathf.Min(lx, ly);
                    if (line < 1.2f) c = Color.Lerp(c, big, Mathf.Clamp01(1.2f - line));
                }
                // The red margin line down the left.
                var mx = Mathf.Abs(p.x - (-half - margin.x * 0.55f)) * r.ppu;
                if (mx < 1.3f) c = Color.Lerp(c, red, Mathf.Clamp01(1.3f - mx));
                // A faint torn fibre edge along the top.
                if (d > -2.5f * px) c = Color.Lerp(c, new Color32(232, 226, 210, 255), Mathf.Clamp01((d + 2.5f * px) / (2.5f * px)));
                c.a = Mathf.Clamp01(0.5f - d * r.ppu);
                r.Blend(x, y, c, c.a);
            }
            var s = r.ToSprite("Notebook", Vector2.zero);
            cache[key] = s;
            return s;
        }

        // ---- Desk props ----

        /// <summary>A wooden school ruler (15 cm) lying on the desk, pivot at its centre.</summary>
        public static Sprite Ruler()
        {
            if (Cached("ruler", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-2.6f, -0.45f, 5.3f, 0.95f), 60f);
            float Body(Vector2 p) => Mathf.Max(Mathf.Abs(p.x) - 2.45f, Mathf.Abs(p.y) - 0.28f) - 0.02f;
            r.Shape(p => Body(p - new Vector2(0.06f, -0.08f)), Shadow, Shadow, Shadow, 0f, 0.05f);
            r.Shape(Body, new Color32(236, 206, 140, 255), new Color32(196, 160, 96, 255), new Color32(252, 236, 196, 255), 0.03f, 0.08f);
            for (var i = 0; i <= 30; i++)
            {
                var x = -2.3f + i * (4.6f / 30f);
                var len = i % 10 == 0 ? 0.2f : i % 5 == 0 ? 0.14f : 0.08f;
                r.Line(new Vector2(x, 0.28f), new Vector2(x, 0.28f - len), 1.4f, new Color32(80, 56, 36, 255), 1f, true);
            }
            var s = r.ToSprite("Ruler");
            cache["ruler"] = s;
            return s;
        }

        /// <summary>A worn two-tone school eraser (red and blue), pivot at its centre.</summary>
        public static Sprite Eraser()
        {
            if (Cached("eraser", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.7f, -0.45f, 1.45f, 0.9f), 80f);
            float Body(Vector2 p) => Mathf.Max(Mathf.Abs(p.x) - 0.55f, Mathf.Abs(p.y) - 0.26f) - 0.06f;
            r.Shape(p => Body(p - new Vector2(0.05f, -0.07f)), Shadow, Shadow, Shadow, 0f, 0.05f);
            r.Shape(p => Mathf.Max(Body(p), p.x), new Color32(220, 70, 70, 255), new Color32(170, 44, 48, 255), new Color32(250, 150, 140, 255), 0.03f, 0.08f);
            r.Shape(p => Mathf.Max(Body(p), -p.x), new Color32(70, 110, 200, 255), new Color32(44, 70, 150, 255), new Color32(150, 180, 240, 255), 0.03f, 0.08f);
            var s = r.ToSprite("Eraser");
            cache["eraser"] = s;
            return s;
        }

        /// <summary>
        /// The open box of 12 wax crayons (Vẽ tranh), seen from above, as a UI image 6.6 × 5.9 units (100 px each): a yellow
        /// cardboard box, a dark tray with a slot per crayon (two rows of six), a printed rainbow band, and room at the
        /// bottom for the pencil, eraser and Done buttons.
        /// </summary>
        public static Sprite CrayonBox()
        {
            if (Cached("crayonbox", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-3.3f, -2.95f, 6.6f, 5.9f), 70f);
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x) - 3.22f, Mathf.Abs(p.y) - 2.87f) - 0.06f, new Color32(248, 200, 64, 255), new Color32(214, 150, 40, 255),
                new Color32(255, 230, 140, 255), 0.05f, 0.25f,
                (p, c) => (Color)c * (0.96f + 0.05f * ValueNoise(p.x * 30f, p.y * 30f, 3))); // cardboard grain
            // The tray, one slot per crayon.
            float Tray(Vector2 p) => Mathf.Max(Mathf.Abs(p.x) - 3.08f, Mathf.Abs(p.y - 0.95f) - 1.85f) - 0.08f;
            r.Shape(Tray, new Color32(96, 62, 44, 255), new Color32(60, 36, 26, 255), new Color32(130, 90, 66, 255), 0.04f, 0.12f);
            for (var row = 0; row < 2; row++)
            for (var i = 0; i < 6; i++)
            {
                var c = new Vector2(-3.3f + 0.69f + i * 1.04f, 2.95f - 0.22f - 0.85f - row * 1.86f);
                r.Shape(p => Mathf.Max(Mathf.Abs(p.x - c.x) - 0.4f, Mathf.Abs(p.y - c.y) - 0.82f) - 0.05f, new Color32(70, 44, 32, 255),
                    new Color32(46, 28, 20, 255), new Color32(110, 76, 56, 255), 0f, 0.1f);
            }
            // A printed rainbow band under the tray.
            var bands = new Color32[] { new Color32(226, 64, 60, 255), new Color32(246, 150, 40, 255), new Color32(80, 170, 80, 255), new Color32(60, 120, 210, 255) };
            for (var b = 0; b < bands.Length; b++)
            {
                var y = -0.98f - b * 0.07f;
                r.Shape(p => Mathf.Max(Mathf.Abs(p.x) - 3.0f, Mathf.Abs(p.y - y - Mathf.Sin(p.x * 2f) * 0.03f) - 0.03f), bands[b], bands[b], bands[b], 0f, 0.02f);
            }
            var s = r.ToSprite("CrayonBox");
            cache["crayonbox"] = s;
            return s;
        }

        // ---- Sky ----

        /// <summary>
        /// A sky covering <paramref name="world"/>: a gradient from <paramref name="top"/> to <paramref name="horizon"/>, a
        /// soft sun glow at <paramref name="sun"/> and a few fluffy clouds.
        /// </summary>
        public static Sprite Sky(Rect world, Color top, Color horizon, Vector2 sun, int clouds, int seed)
        {
            var key = $"sky{world}{ColorUtility.ToHtmlStringRGB(top)}{ColorUtility.ToHtmlStringRGB(horizon)}{sun}{clouds}{seed}";
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(world, 22f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var c = Color.Lerp(horizon, top, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(world.yMin, world.yMax, p.y)));
                var glow = Mathf.Exp(-(p - sun).sqrMagnitude / 5f);
                c = Color.Lerp(c, new Color32(255, 246, 214, 255), glow * 0.6f);
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            var rng = new System.Random(seed);
            for (var k = 0; k < clouds; k++)
            {
                var cx = Mathf.Lerp(world.xMin, world.xMax, (k + 0.2f + (float)rng.NextDouble() * 0.6f) / clouds);
                var cy = Mathf.Lerp(world.yMin + world.height * 0.45f, world.yMax - 1f, (float)rng.NextDouble());
                var puffs = new List<(Vector2 c, Vector2 r)>();
                var n = 4 + rng.Next(4);
                var span = 1.0f + (float)rng.NextDouble() * 1.4f;
                for (var i = 0; i < n; i++)
                {
                    var px = cx + Mathf.Lerp(-span, span, i / (float)(n - 1));
                    var rad = 0.35f + (float)rng.NextDouble() * 0.45f * (1f - Mathf.Abs(i / (float)(n - 1) - 0.5f));
                    puffs.Add((new Vector2(px, cy + rad * 0.4f), new Vector2(rad * 1.25f, rad)));
                }
                float Cloud(Vector2 p)
                {
                    var u = 10f;
                    foreach (var (c, rr) in puffs) u = SmoothUnion(u, Ellipse(p, c, rr), 0.25f);
                    return Mathf.Max(u, cy - 0.05f - p.y);
                }
                r.Shape(Cloud, Color.white, new Color32(214, 228, 240, 255), Color.white, 0f, 0.35f,
                    (p, c) => Color.Lerp(c, new Color32(206, 218, 232, 255), Mathf.Clamp01((cy + 0.25f - p.y) / 0.3f) * 0.6f),
                    new Rect(cx - span - 1.2f, cy - 0.2f, span * 2f + 2.4f, 1.8f));
            }
            var s = r.ToSprite("Sky");
            cache[key] = s;
            return s;
        }

        /// <summary>A plain sprite object (helper for the scene builders).</summary>
        public static SpriteRenderer Put(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 scale, int order, float angle = 0f)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            if (parent != null) sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            sr.transform.localRotation = Quaternion.Euler(0, 0, angle);
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
