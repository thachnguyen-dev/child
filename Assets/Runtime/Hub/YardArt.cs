using System;
using System.Collections.Generic;
using ChildGame.UI;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.Hub
{
    /// <summary>
    /// The objects in the yard that stand for each mini game (owner 2026-10-08: "the items in the yard will be replaced
    /// with drawings representing the corresponding mini game"), drawn to sit in the owner's ink-and-watercolour yard
    /// painting: dark ink outlines, a mottled watercolour wash with paper grain, hatching on the shadow side, soft cast
    /// shadows on the dirt. Things lying on the ground are drawn in the painting's perspective (seen from a little above).
    /// Cached for the session.
    /// </summary>
    public static class YardArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly Color Ink = new Color32(48, 36, 28, 255);
        private static readonly Color Shadow = new Color(0.28f, 0.17f, 0.08f, 0.32f);
        private const float Flat = 0.42f; // how much the ground is foreshortened in the painting

        private static bool Cached(string key, out Sprite s) => cache.TryGetValue(key, out s) && s != null;

        private static ArtRaster Raster(Rect area) => new ArtRaster(area, 110f) { OutlineColor = Ink };

        /// <summary>Watercolour: the wash pools a little, with paper grain, and hatching on the lower right.</summary>
        private static Func<Vector2, Color, Color> Wash(Vector2 centre, int seed, float hatch = 0.6f) => (p, c) =>
        {
            var pool = ValueNoise(p.x * 3f, p.y * 3f, seed) - 0.5f;
            var grain = ValueNoise(p.x * 40f, p.y * 40f, seed + 1) - 0.5f;
            c = c * (1f + pool * 0.16f + grain * 0.06f);
            var side = Vector2.Dot(p - centre, new Vector2(0.7f, -0.7f));
            if (side > 0f && Mathf.Abs(Mathf.Sin((p.x + p.y) * 38f)) > 0.86f) c = Color.Lerp(c, Ink, Mathf.Clamp01(side * 2f) * 0.35f * hatch);
            c.a = 1f;
            return c;
        };

        /// <summary>A soft shadow on the dirt, an ellipse under the object.</summary>
        private static void GroundShadow(ArtRaster r, Vector2 c, Vector2 rad) => r.Shape(p => Ellipse(p, c, rad), Shadow, Shadow, Shadow, 0f, 0.15f);

        // ---- Thả diều: a kite hung on the post ----

        /// <summary>A paper kite hanging by its bridle from the post's arm, string and tail drooping; pivot at the hook.</summary>
        public static Sprite Kite()
        {
            if (Cached("kite", out var c0)) return c0;
            var r = Raster(new Rect(-1.0f, -2.2f, 2.0f, 2.35f));
            var paper = new Color32(206, 72, 56, 255);
            var second = new Color32(236, 196, 90, 255);
            // String from the hook down to the bridle, then the kite tilted a little.
            r.Line(Vector2.zero, new Vector2(0.02f, -0.35f), 2f, Ink, 1f, false);
            var centre = new Vector2(0.05f, -0.85f);
            var tilt = 0.18f;
            Vector2 K(Vector2 p) { var q = p - centre; return new Vector2(q.x * Mathf.Cos(tilt) + q.y * Mathf.Sin(tilt), -q.x * Mathf.Sin(tilt) + q.y * Mathf.Cos(tilt)); }
            float Diamond(Vector2 p) { var q = K(p); return (Mathf.Abs(q.x) / 0.42f + Mathf.Abs(q.y) / (q.y > 0 ? 0.42f : 0.62f) - 1f) * 0.28f; }
            r.Shape(Diamond, paper, Color.Lerp(paper, Ink, 0.3f), Color.Lerp(paper, Color.white, 0.3f), 0.022f, 0.08f,
                (p, c) => Wash(centre, 3)(p, (K(p).x > 0) == (K(p).y > 0) ? c : Color.Lerp(c, second, 0.85f)));
            var bamboo = new Color32(120, 86, 46, 255);
            r.Line(centre - (Vector2)(Quaternion.Euler(0, 0, -tilt * Mathf.Rad2Deg) * new Vector2(0, 0.42f)) * -1f, centre + (Vector2)(Quaternion.Euler(0, 0, -tilt * Mathf.Rad2Deg) * new Vector2(0, -0.62f)), 2.6f, bamboo, 1f, true);
            r.Line(centre + (Vector2)(Quaternion.Euler(0, 0, -tilt * Mathf.Rad2Deg) * new Vector2(-0.42f, 0)), centre + (Vector2)(Quaternion.Euler(0, 0, -tilt * Mathf.Rad2Deg) * new Vector2(0.42f, 0)), 2.6f, bamboo, 1f, true);
            // The tail of paper bows drooping and curling.
            var tail = centre + (Vector2)(Quaternion.Euler(0, 0, -tilt * Mathf.Rad2Deg) * new Vector2(0, -0.62f));
            var prev = tail;
            for (var i = 1; i <= 8; i++)
            {
                var t = i / 8f;
                var p = tail + new Vector2(Mathf.Sin(t * 3.5f) * 0.18f, -t * 0.75f);
                r.Line(prev, p, 1.6f, Ink, 1f, false);
                if (i % 2 == 0)
                {
                    var col = i % 4 == 0 ? new Color32(70, 120, 200, 255) : new Color32(236, 196, 90, 255);
                    var b = p;
                    r.Shape(q => Mathf.Min(Ellipse(q, b + new Vector2(-0.05f, 0), new Vector2(0.055f, 0.035f)), Ellipse(q, b + new Vector2(0.05f, 0), new Vector2(0.055f, 0.035f))),
                        col, Color.Lerp(col, Ink, 0.3f), col, 0.015f, 0.02f);
                }
                prev = p;
            }
            var s = r.ToSprite("YardKite", Vector2.zero);
            cache["kite"] = s;
            return s;
        }

        // ---- Ô ăn quan: the board scratched on the dirt ----

        /// <summary>The ô ăn quan board in chalk on the yard, seen at the painting's slant, with pebbles and two quan.</summary>
        public static Sprite OAnQuan()
        {
            if (Cached("oaq", out var c0)) return c0;
            var r = Raster(new Rect(-1.9f, -0.55f, 3.8f, 1.1f));
            const float cell = 0.5f;
            Vector2 Plan(Vector2 p) => new Vector2(p.x, p.y / Flat);
            float Lines(Vector2 p)
            {
                var q = Plan(p);
                var halfW = 2.5f * cell;
                var d = Mathf.Abs(Mathf.Max(Mathf.Abs(q.x) - halfW, Mathf.Abs(q.y) - cell));
                if (Mathf.Abs(q.x) <= halfW) d = Mathf.Min(d, Mathf.Abs(q.y));
                if (Mathf.Abs(q.y) <= cell) for (var i = -2; i <= 1; i++) d = Mathf.Min(d, Mathf.Abs(q.x - (i + 0.5f) * cell));
                foreach (var s in new[] { -1f, 1f })
                    if ((q.x - s * halfW) * s >= 0f) d = Mathf.Min(d, Mathf.Abs((q - new Vector2(s * halfW, 0)).magnitude - cell));
                return d * Mathf.Lerp(Flat, 1f, 0.3f);
            }
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var d = Lines(p) - 0.014f;
                var a = Mathf.Clamp01(0.5f - d * r.ppu) * Mathf.Clamp01(0.4f + ValueNoise(p.x * 60f, p.y * 60f, 2) * 0.8f);
                if (a > 0f) r.px[y * r.w + x] = new Color(0.97f, 0.95f, 0.88f, a * 0.9f);
            }
            var rng = new System.Random(5);
            for (var cx = -2; cx <= 2; cx++)
            for (var row = 0; row < 2; row++)
            for (var k = 0; k < 4; k++)
            {
                var c = new Vector2(cx * cell + ((float)rng.NextDouble() - 0.5f) * 0.28f, ((row - 0.5f) * cell + ((float)rng.NextDouble() - 0.5f) * 0.25f) * Flat);
                var tone = (float)rng.NextDouble();
                var col = Color.Lerp(new Color32(110, 104, 98, 255), new Color32(180, 170, 156, 255), tone);
                r.Shape(q => Ellipse(q, c, new Vector2(0.045f, 0.03f)), col, Color.Lerp(col, Ink, 0.35f), Color.Lerp(col, Color.white, 0.4f), 0.01f, 0.02f);
            }
            foreach (var s in new[] { -1f, 1f })
            {
                var c = new Vector2(s * (2.5f * cell + 0.22f), 0f);
                r.Shape(q => Ellipse(q - new Vector2(0.03f, -0.03f), c, new Vector2(0.15f, 0.08f)), Shadow, Shadow, Shadow, 0f, 0.05f);
                r.Shape(q => Ellipse(q, c, new Vector2(0.14f, 0.09f)), new Color32(92, 84, 100, 255), new Color32(56, 50, 62, 255), new Color32(160, 154, 170, 255), 0.016f, 0.06f);
            }
            var sprite = r.ToSprite("YardOAnQuan");
            cache["oaq"] = sprite;
            return sprite;
        }

        // ---- Đua mo cau: an areca sheath lying in the yard ----

        public static Sprite MoCau()
        {
            if (Cached("mocau", out var c0)) return c0;
            var r = Raster(new Rect(-1.3f, -0.45f, 2.6f, 0.95f));
            Vector2 Plan(Vector2 p) => new Vector2(p.x, p.y / 0.5f);
            float Sheath(Vector2 p)
            {
                var q = Plan(p);
                var w = Mathf.Lerp(0.18f, 0.42f, Mathf.InverseLerp(-1.1f, 0.6f, q.x));
                var body = Mathf.Max(Mathf.Abs(q.y) - w, Mathf.Abs(q.x + 0.15f) - 0.95f);
                return SmoothUnion(body, Ellipse(q, new Vector2(0.75f, 0f), new Vector2(0.28f, 0.44f)), 0.1f) * 0.5f;
            }
            GroundShadow(r, new Vector2(0.05f, -0.12f), new Vector2(1.15f, 0.2f));
            var tan = new Color32(196, 168, 108, 255);
            r.Shape(Sheath, tan, new Color32(150, 118, 70, 255), new Color32(230, 210, 160, 255), 0.022f, 0.08f,
                (p, c) =>
                {
                    var q = Plan(p);
                    c = Color.Lerp(c, new Color32(150, 160, 84, 255), Mathf.Clamp01((-0.4f - q.x) / 0.7f) * 0.45f); // green toward the stalk
                    if (Mathf.Abs(Mathf.Sin(q.y * 40f)) > 0.93f) c = Color.Lerp(c, Ink, 0.25f); // veins
                    return Wash(Vector2.zero, 7, 0.3f)(p, c);
                });
            // The curled lip at the wide end, and the stalk.
            r.Shape(p => Mathf.Max(Ellipse(Plan(p), new Vector2(0.85f, 0f), new Vector2(0.2f, 0.42f)), 0.78f - Plan(p).x) * 0.5f, new Color32(170, 136, 80, 255),
                new Color32(120, 90, 50, 255), new Color32(220, 196, 140, 255), 0.02f, 0.03f);
            r.Shape(p => Capsule(p, new Vector2(-1.05f, 0f), new Vector2(-1.25f, 0.02f), 0.035f), new Color32(140, 140, 70, 255), new Color32(100, 100, 50, 255),
                new Color32(180, 180, 100, 255), 0.015f, 0.02f);
            var s = r.ToSprite("YardMoCau");
            cache["mocau"] = s;
            return s;
        }

        // ---- Trộm xoài: a basket of mangoes and a pair of flip-flops ----

        public static Sprite MangoBasket()
        {
            if (Cached("xoai", out var c0)) return c0;
            var r = Raster(new Rect(-1.0f, -0.12f, 2.0f, 1.25f));
            GroundShadow(r, new Vector2(0.05f, 0.04f), new Vector2(0.8f, 0.14f));
            // Woven bamboo basket (rổ): a wide bowl with a rim.
            var bamboo = new Color32(196, 156, 92, 255);
            float Bowl(Vector2 p) => Mathf.Max(Ellipse(p, new Vector2(-0.12f, 0.42f), new Vector2(0.62f, 0.42f)), p.y - 0.46f);
            r.Shape(Bowl, bamboo, new Color32(140, 104, 56, 255), new Color32(230, 200, 140, 255), 0.022f, 0.12f,
                (p, c) => Wash(new Vector2(-0.12f, 0.3f), 11)(p, Mathf.Abs(Mathf.Sin(p.x * 30f + p.y * 30f)) > 0.8f || Mathf.Abs(Mathf.Sin(p.x * 30f - p.y * 30f)) > 0.8f ? (Color)c * 0.85f : c));
            // Mangoes heaped in the basket.
            var rng = new System.Random(4);
            foreach (var m in new[] { new Vector2(-0.45f, 0.5f), new Vector2(-0.12f, 0.56f), new Vector2(0.2f, 0.5f), new Vector2(-0.3f, 0.7f), new Vector2(0.02f, 0.74f) })
            {
                var ripe = (float)rng.NextDouble();
                var col = Color.Lerp(new Color32(150, 180, 70, 255), new Color32(236, 190, 60, 255), ripe);
                var c = m;
                r.Shape(q => SmoothUnion(Ellipse(q, c, new Vector2(0.17f, 0.13f)), Ellipse(q, c + new Vector2(0.1f, -0.04f), new Vector2(0.09f, 0.08f)), 0.05f),
                    col, Color.Lerp(col, new Color32(90, 110, 40, 255), 0.45f), Color.Lerp(col, Color.white, 0.45f), 0.02f, 0.08f,
                    (q, cc) => Wash(c, 13, 0.4f)(q, Color.Lerp(cc, new Color32(226, 120, 60, 255), Mathf.Clamp01(1f - (q - c - new Vector2(-0.06f, 0.05f)).magnitude / 0.09f) * 0.4f * ripe)));
            }
            r.Shape(p => Mathf.Abs(Ellipse(p, new Vector2(-0.12f, 0.46f), new Vector2(0.62f, 0.12f))) - 0.025f + (p.y > 0.47f ? 0.03f : 0f), bamboo,
                new Color32(140, 104, 56, 255), new Color32(230, 200, 140, 255), 0.018f, 0.02f); // the rim in front
            // Blue flip-flops beside the basket.
            var blue = new Color32(56, 110, 190, 255);
            foreach (var (c, a) in new[] { (new Vector2(0.72f, 0.05f), 14f), (new Vector2(0.78f, 0.17f), 8f) })
            {
                var ang = a * Mathf.Deg2Rad;
                Vector2 L(Vector2 p) { var q = p - c; return new Vector2(q.x * Mathf.Cos(ang) + q.y * Mathf.Sin(ang), (-q.x * Mathf.Sin(ang) + q.y * Mathf.Cos(ang)) / 0.45f); }
                r.Shape(p => Ellipse(L(p), Vector2.zero, new Vector2(0.24f, 0.1f)) * 0.5f, blue, Color.Lerp(blue, Ink, 0.35f), Color.Lerp(blue, Color.white, 0.35f), 0.016f, 0.03f);
                r.Line(c + new Vector2(0.06f, 0f), c + new Vector2(-0.06f, 0.03f), 2.2f, new Color32(240, 240, 240, 255), 1f, true);
            }
            var s = r.ToSprite("YardMangoes", new Vector2(0f, 0f));
            cache["xoai"] = s;
            return s;
        }

        // ---- Cờ caro: a notebook page on a low wooden stool ----

        public static Sprite CaroStool()
        {
            if (Cached("caro", out var c0)) return c0;
            var r = Raster(new Rect(-0.85f, -0.08f, 1.7f, 1.2f));
            GroundShadow(r, new Vector2(0.06f, 0.04f), new Vector2(0.7f, 0.12f));
            var wood = new Color32(150, 100, 60, 255);
            foreach (var x in new[] { -0.48f, 0.48f })
                r.Shape(p => Mathf.Max(Mathf.Abs(p.x - x - (p.y - 0.3f) * -0.08f * Mathf.Sign(x)) - 0.06f, Mathf.Abs(p.y - 0.28f) - 0.28f), wood,
                    Color.Lerp(wood, Ink, 0.35f), Color.Lerp(wood, Color.white, 0.3f), 0.02f, 0.03f, Wash(new Vector2(x, 0.3f), 17));
            // The seat, a plank seen slightly from above.
            float Seat(Vector2 p) => Mathf.Max(Mathf.Abs(p.x) - 0.66f + (p.y - 0.62f) * 0.4f, Mathf.Abs(p.y - 0.66f) - 0.1f);
            r.Shape(Seat, new Color32(176, 124, 76, 255), new Color32(120, 80, 48, 255), new Color32(214, 170, 120, 255), 0.022f, 0.05f,
                (p, c) => Wash(new Vector2(0, 0.66f), 19)(p, Mathf.Abs(Mathf.Sin(p.x * 24f + Mathf.Sin(p.y * 30f))) > 0.94f ? (Color)c * 0.85f : c));
            // A torn notebook page with the grid, a few X and O, and a ballpoint pen.
            Vector2 Page(Vector2 p) => new Vector2((p.x + 0.05f) / 0.42f, (p.y - 0.8f) / 0.13f);
            r.Shape(p => { var q = Page(p); return Mathf.Max(Mathf.Abs(q.x) - 1f, Mathf.Abs(q.y) - 1f) * 0.12f; }, new Color32(250, 248, 238, 255),
                new Color32(220, 214, 196, 255), Color.white, 0.012f, 0.02f,
                (p, c) =>
                {
                    var q = Page(p);
                    var gx = Mathf.Abs(Mathf.Repeat(q.x * 4f, 1f) - 0.5f);
                    var gy = Mathf.Abs(Mathf.Repeat(q.y * 2f, 1f) - 0.5f);
                    return gx > 0.44f || gy > 0.42f ? Color.Lerp(c, new Color32(150, 132, 200, 255), 0.7f) : c;
                });
            var blue = new Color32(40, 80, 200, 255);
            var red = new Color32(205, 45, 45, 255);
            foreach (var (c, x) in new[] { (new Vector2(-0.22f, 0.83f), true), (new Vector2(0.0f, 0.77f), false), (new Vector2(0.11f, 0.84f), true), (new Vector2(-0.11f, 0.76f), false) })
            {
                if (x)
                {
                    r.Line(c + new Vector2(-0.035f, -0.025f), c + new Vector2(0.035f, 0.025f), 2f, blue, 1f, true);
                    r.Line(c + new Vector2(-0.035f, 0.025f), c + new Vector2(0.035f, -0.025f), 2f, blue, 1f, true);
                }
                else
                    for (var k = 0; k < 16; k++)
                    {
                        var a0 = k / 16f * Mathf.PI * 2f;
                        var a1 = (k + 1) / 16f * Mathf.PI * 2f;
                        r.Line(c + new Vector2(Mathf.Cos(a0) * 0.035f, Mathf.Sin(a0) * 0.028f), c + new Vector2(Mathf.Cos(a1) * 0.035f, Mathf.Sin(a1) * 0.028f), 2f, red, 1f, true);
                    }
            }
            r.Shape(p => Capsule(p, new Vector2(0.28f, 0.72f), new Vector2(0.62f, 0.86f), 0.022f), new Color32(236, 240, 244, 255), new Color32(180, 188, 198, 255), Color.white, 0.012f, 0.02f);
            r.Shape(p => Capsule(p, new Vector2(0.56f, 0.835f), new Vector2(0.64f, 0.87f), 0.026f), blue, Color.Lerp(blue, Ink, 0.3f), Color.Lerp(blue, Color.white, 0.4f), 0.012f, 0.02f);
            var s = r.ToSprite("YardCaro", Vector2.zero);
            cache["caro"] = s;
            return s;
        }

        // ---- Vẽ tranh: a crayon drawing on the ground with crayons beside it ----

        public static Sprite Drawing()
        {
            if (Cached("ve", out var c0)) return c0;
            var r = Raster(new Rect(-1.0f, -0.42f, 2.0f, 0.85f));
            Vector2 Plan(Vector2 p) => new Vector2(p.x, p.y / Flat);
            float Sheet(Vector2 p) { var q = Plan(p - new Vector2(-0.2f, 0f)); return Mathf.Max(Mathf.Abs(q.x + q.y * 0.15f) - 0.55f, Mathf.Abs(q.y) - 0.42f) * Flat; }
            r.Shape(p => Sheet(p - new Vector2(0.04f, -0.03f)), Shadow, Shadow, Shadow, 0f, 0.05f);
            r.Shape(Sheet, new Color32(250, 248, 238, 255), new Color32(220, 214, 196, 255), Color.white, 0.016f, 0.03f,
                (p, c) =>
                {
                    // A child's crayon picture: a sun, a house, green grass.
                    var q = Plan(p - new Vector2(-0.2f, 0f));
                    if ((q - new Vector2(0.3f, 0.2f)).magnitude < 0.11f) return Color.Lerp(c, new Color32(246, 196, 50, 255), 0.85f);
                    if (Mathf.Abs(q.x + 0.1f) < 0.16f && q.y > -0.2f && q.y < 0.06f) return Color.Lerp(c, new Color32(220, 90, 70, 255), 0.75f);
                    if (q.y > 0.06f && q.y < 0.22f - Mathf.Abs(q.x + 0.1f) * 0.9f) return Color.Lerp(c, new Color32(140, 70, 50, 255), 0.75f);
                    if (q.y < -0.22f && q.y > -0.36f) return Color.Lerp(c, new Color32(90, 170, 80, 255), 0.6f * (Mathf.Abs(Mathf.Sin(q.x * 60f)) > 0.3f ? 1f : 0.5f));
                    return c;
                });
            var colours = new Color32[] { new Color32(214, 40, 40, 255), new Color32(250, 210, 40, 255), new Color32(50, 150, 65, 255), new Color32(40, 85, 195, 255) };
            for (var i = 0; i < colours.Length; i++)
            {
                var a = new Vector2(0.5f + i * 0.09f, -0.18f + i * 0.04f);
                var b = a + new Vector2(0.06f, 0.2f);
                var col = colours[i];
                r.Shape(p => Capsule(p, a, b, 0.03f), col, Color.Lerp(col, Ink, 0.35f), Color.Lerp(col, Color.white, 0.4f), 0.012f, 0.02f);
            }
            var s = r.ToSprite("YardDrawing");
            cache["ve"] = s;
            return s;
        }

        // ---- Chuồn chuồn: a dragonfly perched on a plant ----

        public static Sprite Dragonfly()
        {
            if (Cached("chuon", out var c0)) return c0;
            var r = Raster(new Rect(-0.75f, -0.4f, 1.5f, 0.8f));
            var body = new Color32(200, 60, 44, 255);
            foreach (var (root, angle) in new[] { (new Vector2(-0.22f, 0.04f), 75f), (new Vector2(-0.12f, 0.04f), 55f), (new Vector2(-0.22f, -0.04f), -75f), (new Vector2(-0.12f, -0.04f), -55f) })
            {
                var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var tip = root + dir * 0.36f;
                r.Shape(p => Capsule(p, root, tip, 0.05f), new Color(0.88f, 0.95f, 1f, 0.55f), new Color(0.7f, 0.8f, 0.9f, 0.55f), new Color(1, 1, 1, 0.6f), 0.008f, 0.03f);
            }
            for (var i = 0; i < 8; i++)
            {
                var a = new Vector2(-0.1f + i * 0.08f, -i * 0.006f);
                var b = a + new Vector2(0.08f, -0.006f);
                r.Shape(p => Capsule(p, a, b, Mathf.Lerp(0.032f, 0.018f, i / 8f)), body, Color.Lerp(body, Ink, 0.4f), Color.Lerp(body, Color.white, 0.4f), 0.01f, 0.02f);
            }
            r.Shape(p => Ellipse(p, new Vector2(-0.17f, 0f), new Vector2(0.09f, 0.055f)), body, Color.Lerp(body, Ink, 0.4f), Color.Lerp(body, Color.white, 0.4f), 0.012f, 0.03f);
            r.Shape(p => Ellipse(p, new Vector2(-0.3f, 0f), new Vector2(0.06f, 0.065f)), new Color32(90, 40, 40, 255), Ink, new Color32(200, 120, 110, 255), 0.012f, 0.03f);
            var s = r.ToSprite("YardDragonfly");
            cache["chuon"] = s;
            return s;
        }

        // ---- Nhổ tóc bạc: grandma's rolled mat and fan by the door ----

        public static Sprite GrandmaMat()
        {
            if (Cached("toc", out var c0)) return c0;
            var r = Raster(new Rect(-0.9f, -0.1f, 1.8f, 0.85f));
            GroundShadow(r, new Vector2(0.03f, 0.05f), new Vector2(0.8f, 0.12f));
            // A rolled straw mat (chiếu) lying on its side, tied with a cord.
            var straw = new Color32(220, 192, 128, 255);
            float Roll(Vector2 p) => Capsule(p, new Vector2(-0.6f, 0.2f), new Vector2(0.45f, 0.2f), 0.18f);
            r.Shape(Roll, straw, new Color32(170, 136, 80, 255), new Color32(244, 226, 180, 255), 0.02f, 0.1f,
                (p, c) => Wash(new Vector2(0, 0.2f), 23)(p, Mathf.Abs(Mathf.Sin(p.x * 70f)) > 0.85f ? (Color)c * 0.88f : (Mathf.Abs(p.x + 0.1f) < 0.12f && Mathf.Abs(p.y - 0.2f) < 0.06f ? Color.Lerp(c, new Color32(180, 60, 50, 255), 0.7f) : c)));
            r.Shape(p => Ellipse(p, new Vector2(0.45f, 0.2f), new Vector2(0.07f, 0.18f)), new Color32(200, 168, 104, 255), new Color32(150, 118, 70, 255), straw, 0.018f, 0.04f,
                (p, c) => Mathf.Abs(Mathf.Sin((p - new Vector2(0.45f, 0.2f)).magnitude * 90f)) > 0.8f ? (Color)c * 0.8f : c); // the spiral end
            foreach (var x in new[] { -0.35f, 0.2f })
                r.Line(new Vector2(x, 0.03f), new Vector2(x + 0.02f, 0.38f), 2.6f, new Color32(170, 50, 44, 255), 1f, true);
            // A round woven bamboo fan (quạt nan) leaning on the mat.
            var fanC = new Vector2(0.55f, 0.48f);
            r.Shape(p => Capsule(p, fanC + new Vector2(0.02f, -0.1f), fanC + new Vector2(0.1f, -0.42f), 0.022f), new Color32(150, 110, 60, 255), new Color32(100, 70, 40, 255),
                new Color32(200, 160, 100, 255), 0.012f, 0.02f);
            r.Shape(p => Ellipse(p, fanC, new Vector2(0.2f, 0.22f)), new Color32(214, 180, 112, 255), new Color32(164, 128, 70, 255), new Color32(240, 220, 170, 255), 0.018f, 0.06f,
                (p, c) => Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.y - fanC.y, p.x - fanC.x) * 14f)) > 0.85f ? (Color)c * 0.85f : c);
            var s = r.ToSprite("YardGrandma", Vector2.zero);
            cache["toc"] = s;
            return s;
        }

        // ---- The street vendor ----

        /// <summary>
        /// The street vendor walking right (pivot at her hip, 1 unit ≈ her height / 2.6): a conical hat, a brown áo bà ba,
        /// black trousers, a bamboo pole on her shoulder with two baskets of sweets on ropes. Her legs are separate
        /// (<see cref="VendorLeg"/>) so they can step.
        /// </summary>
        public static Sprite Vendor()
        {
            if (Cached("vendor", out var c0)) return c0;
            var r = Raster(new Rect(-1.6f, -0.9f, 3.2f, 2.2f));
            var skin = new Color32(222, 176, 134, 255);
            var shirt = new Color32(120, 82, 70, 255);
            var basket = new Color32(196, 156, 92, 255);
            // Baskets on ropes from the pole ends, with sweets heaped in them.
            foreach (var x in new[] { -1.15f, 1.15f })
            {
                r.Line(new Vector2(x - 0.18f, -0.25f), new Vector2(x, 0.82f), 1.8f, Ink, 1f, false);
                r.Line(new Vector2(x + 0.18f, -0.25f), new Vector2(x, 0.82f), 1.8f, Ink, 1f, false);
                var bx = x;
                r.Shape(p => Mathf.Max(Ellipse(p, new Vector2(bx, -0.42f), new Vector2(0.34f, 0.24f)), p.y + 0.24f), basket, new Color32(140, 104, 56, 255),
                    new Color32(230, 200, 140, 255), 0.022f, 0.08f,
                    (p, c) => Mathf.Abs(Mathf.Sin(p.x * 34f + p.y * 34f)) > 0.82f ? (Color)c * 0.85f : c);
                foreach (var (dx, col) in new[] { (-0.13f, new Color32(240, 130, 160, 255)), (0.02f, new Color32(250, 220, 120, 255)), (0.15f, new Color32(150, 210, 230, 255)) })
                {
                    var cc = new Vector2(bx + dx, -0.2f);
                    r.Shape(p => Ellipse(p, cc, new Vector2(0.09f, 0.07f)), col, Color.Lerp(col, Ink, 0.25f), Color.Lerp(col, Color.white, 0.4f), 0.016f, 0.03f);
                }
                r.Shape(p => Mathf.Abs(Ellipse(p, new Vector2(bx, -0.24f), new Vector2(0.34f, 0.07f))) - 0.02f, basket, new Color32(140, 104, 56, 255), basket, 0.016f, 0.02f);
            }
            // Body: trousers, then the áo bà ba (slightly flared), the arm reaching up to steady the pole.
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x - 0.0f) - 0.17f, Mathf.Max(-0.05f - p.y, p.y - 0.2f)), new Color32(40, 36, 40, 255), new Color32(24, 22, 26, 255),
                new Color32(80, 76, 84, 255), 0.022f, 0.04f);
            float Shirt(Vector2 p) => Mathf.Max(Mathf.Abs(p.x - 0.02f) - (0.2f - (p.y - 0.2f) * 0.12f), Mathf.Max(0.12f - p.y, p.y - 0.78f)) - 0.02f;
            r.Shape(Shirt, shirt, Color.Lerp(shirt, Ink, 0.35f), Color.Lerp(shirt, Color.white, 0.3f), 0.022f, 0.08f, Wash(new Vector2(0, 0.45f), 31, 0.4f));
            r.Shape(p => Capsule(p, new Vector2(0.12f, 0.66f), new Vector2(0.3f, 0.86f), 0.055f), shirt, Color.Lerp(shirt, Ink, 0.35f), shirt, 0.02f, 0.03f);
            r.Shape(p => (p - new Vector2(0.33f, 0.9f)).magnitude - 0.05f, skin, Color.Lerp(skin, Ink, 0.3f), skin, 0.018f, 0.02f);
            // The carrying pole across the shoulder.
            r.Shape(p => Capsule(p, new Vector2(-1.2f, 0.84f), new Vector2(1.2f, 0.84f), 0.035f), new Color32(170, 136, 70, 255), new Color32(120, 92, 46, 255),
                new Color32(220, 196, 130, 255), 0.018f, 0.02f);
            // Head and the conical hat (nón lá) shading her face.
            r.Shape(p => Ellipse(p, new Vector2(0.06f, 0.92f), new Vector2(0.12f, 0.13f)), skin, Color.Lerp(skin, Ink, 0.3f), Color.Lerp(skin, Color.white, 0.3f), 0.02f, 0.05f);
            float Hat(Vector2 p) { var q = p - new Vector2(0.05f, 1.0f); return Mathf.Max(Mathf.Abs(q.x) * 0.55f + q.y - 0.22f, -q.y - 0.02f); }
            r.Shape(Hat, new Color32(232, 214, 156, 255), new Color32(180, 156, 96, 255), new Color32(250, 240, 200, 255), 0.022f, 0.05f,
                (p, c) => Mathf.Abs(Mathf.Sin((p.x - 0.05f) * 40f)) > 0.9f ? (Color)c * 0.88f : c); // the ribs of the leaf hat
            var s = r.ToSprite("YardVendor", Vector2.zero);
            cache["vendor"] = s;
            return s;
        }

        /// <summary>One leg of the vendor (pivot at the hip): a black trouser leg and a bare foot in a sandal.</summary>
        public static Sprite VendorLeg()
        {
            if (Cached("vendorleg", out var c0)) return c0;
            var r = Raster(new Rect(-0.15f, -0.95f, 0.36f, 1.0f));
            r.Shape(p => Capsule(p, Vector2.zero, new Vector2(0f, -0.72f), 0.075f), new Color32(40, 36, 40, 255), new Color32(24, 22, 26, 255), new Color32(80, 76, 84, 255), 0.02f, 0.03f);
            r.Shape(p => Capsule(p, new Vector2(-0.02f, -0.82f), new Vector2(0.12f, -0.84f), 0.04f), new Color32(222, 176, 134, 255), new Color32(180, 130, 96, 255),
                new Color32(240, 206, 170, 255), 0.016f, 0.02f);
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x - 0.05f) - 0.11f, Mathf.Abs(p.y + 0.885f) - 0.015f), new Color32(60, 110, 170, 255), new Color32(40, 80, 130, 255),
                new Color32(110, 160, 210, 255), 0.012f, 0.01f);
            var s = r.ToSprite("YardVendorLeg", Vector2.zero);
            cache["vendorleg"] = s;
            return s;
        }

        /// <summary>The drawing for a mini game id (null = none).</summary>
        public static Sprite For(string id) => id switch
        {
            "dieu" => Kite(),
            "oanquan" => OAnQuan(),
            "mocau" => MoCau(),
            "xoai" => MangoBasket(),
            "caro" => CaroStool(),
            "ve" => Drawing(),
            "chuon" => Dragonfly(),
            "toc" => GrandmaMat(),
            _ => null,
        };
    }
}
