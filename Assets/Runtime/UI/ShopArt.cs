using System.Collections.Generic;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.UI
{
    /// <summary>
    /// Official generated art for the shops (owner 2026-10-08: "upgrade store art"), in the yard's ink-and-watercolour
    /// style: the village store (striped awning over a wooden plank wall), the street vendor's woven bamboo tray, and a
    /// drawing for every item. Cached for the session.
    /// </summary>
    public static class ShopArt
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly Color Ink = new Color32(58, 42, 32, 255);
        private static readonly Color Shadow = new Color(0.2f, 0.12f, 0.06f, 0.28f);
        private static bool Cached(string key, out Sprite s) => cache.TryGetValue(key, out s) && s != null;

        private static ArtRaster Raster(Rect area, float ppu = 150f) => new ArtRaster(area, ppu) { OutlineColor = Ink };

        private static Color Wash(Vector2 p, Color c, int seed) =>
            c * (1f + (ValueNoise(p.x * 3f, p.y * 3f, seed) - 0.5f) * 0.14f + (ValueNoise(p.x * 40f, p.y * 40f, seed + 1) - 0.5f) * 0.05f);

        // ---- Backdrops ----

        /// <summary>
        /// The village store, filling a card of <paramref name="size"/> (UI units / 100): a red-and-white striped awning with
        /// a scalloped edge along the top, a wooden plank wall below with two shelves.
        /// </summary>
        public static Sprite Storefront(Vector2 size)
        {
            var key = "store" + size;
            if (Cached(key, out var c0)) return c0;
            var r = Raster(new Rect(0, 0, size.x, size.y), 90f);
            var awningBottom = size.y - 1.15f;
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                // Planks: vertical boards with grain and dark gaps.
                var board = Mathf.Repeat(p.x, 0.9f) / 0.9f;
                var grain = ValueNoise(p.x * 2f + Mathf.Floor(p.x / 0.9f) * 7f, p.y * 14f, 3);
                Color c = Color.Lerp(new Color32(150, 104, 66, 255), new Color32(184, 136, 92, 255), grain);
                if (board < 0.03f || board > 0.97f) c = new Color32(92, 60, 38, 255);
                // Shelves: two long planks across with a shadow below each.
                foreach (var sy in new[] { size.y * 0.52f, size.y * 0.2f })
                {
                    if (p.y > sy - 0.06f && p.y < sy + 0.06f) c = Color.Lerp(new Color32(120, 80, 50, 255), new Color32(170, 120, 78, 255), (p.y - sy + 0.06f) / 0.12f);
                    else if (p.y < sy - 0.06f && p.y > sy - 0.22f) c = Color.Lerp(c, Color.black, (1f - (sy - 0.06f - p.y) / 0.16f) * 0.25f);
                }
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            // The awning: stripes, a scalloped hem and an ink outline along the hem.
            float Awning(Vector2 p)
            {
                if (p.y >= awningBottom) return -(p.y - awningBottom) - 0.01f;
                var scallop = (new Vector2(Mathf.Repeat(p.x, 0.6f) - 0.3f, p.y - awningBottom)).magnitude - 0.3f;
                return scallop;
            }
            r.Shape(Awning, new Color32(210, 60, 52, 255), new Color32(150, 34, 32, 255), new Color32(244, 128, 108, 255), 0.04f, 0.2f,
                (p, c) => Wash(p, Mathf.Repeat(p.x, 0.6f) < 0.3f ? Color.Lerp(c, new Color32(250, 244, 230, 255), 0.92f) : c, 5));
            var s = r.ToSprite("Storefront");
            cache[key] = s;
            return s;
        }

        /// <summary>The street vendor's stall: a woven bamboo tray (mẹt) filling a card of <paramref name="size"/>.</summary>
        public static Sprite BambooTray(Vector2 size)
        {
            var key = "tray" + size;
            if (Cached(key, out var c0)) return c0;
            var r = Raster(new Rect(0, 0, size.x, size.y), 90f);
            var c = size * 0.5f;
            float Rim(Vector2 p) { var q = (p - c) / (c - new Vector2(0.15f, 0.15f)); return (Mathf.Max(Mathf.Abs(q.x), Mathf.Abs(q.y)) - 1f) * 0.8f; }
            r.Shape(Rim, new Color32(206, 166, 100, 255), new Color32(150, 110, 60, 255), new Color32(236, 206, 150, 255), 0.05f, 0.3f,
                (p, col) =>
                {
                    // A twill weave: strips going over and under each other on the diagonal.
                    var u = Mathf.Repeat((p.x + p.y) * 3.3f, 1f);
                    var v = Mathf.Repeat((p.x - p.y) * 3.3f, 1f);
                    var over = (Mathf.FloorToInt((p.x + p.y) * 3.3f) + Mathf.FloorToInt((p.x - p.y) * 3.3f)) % 2 == 0;
                    var edge = over ? Mathf.Min(u, 1f - u) : Mathf.Min(v, 1f - v);
                    col = over ? col : (Color)col * 0.9f;
                    return Wash(p, edge < 0.08f ? (Color)col * 0.78f : col, 9);
                });
            var s = r.ToSprite("BambooTray");
            cache[key] = s;
            return s;
        }

        // ---- Items ----

        /// <summary>The drawing for a shop item, about 1.2 units square (UI icon).</summary>
        public static Sprite Item(string id)
        {
            if (Cached("item" + id, out var c0)) return c0;
            var r = Raster(new Rect(-0.6f, -0.6f, 1.2f, 1.2f));
            switch (id)
            {
                case "paper": Paper(r); break;
                case "crayons": Crayons(r); break;
                case "kite": Kite(r); break;
                case "sandals": Sandals(r); break;
                case "keokeo": Taffy(r); break;
                case "keoc": VitaminC(r); break;
                case "mitom": Noodles(r); break;
                case "bigbabol": BubbleGum(r); break;
                case "carem": IcePop(r); break;
                default: Taffy(r); break;
            }
            var s = r.ToSprite("Item_" + id);
            cache["item" + id] = s;
            return s;
        }

        private static void Paper(ArtRaster r)
        {
            // A small stack of squared notebook sheets, the top one a little askew.
            for (var i = 2; i >= 0; i--)
            {
                var off = new Vector2(i * 0.04f, -i * 0.04f);
                var ang = (i == 0 ? -8f : -2f + i * 3f) * Mathf.Deg2Rad;
                Vector2 L(Vector2 p) { var q = p - off; return new Vector2(q.x * Mathf.Cos(ang) + q.y * Mathf.Sin(ang), -q.x * Mathf.Sin(ang) + q.y * Mathf.Cos(ang)); }
                r.Shape(p => Mathf.Max(Mathf.Abs(L(p).x) - 0.36f, Mathf.Abs(L(p).y) - 0.46f), new Color32(252, 250, 240, 255), new Color32(220, 214, 196, 255),
                    Color.white, 0.02f, 0.04f,
                    (p, c) =>
                    {
                        var q = L(p);
                        if (i > 0) return c;
                        var gx = Mathf.Abs(Mathf.Repeat(q.x * 7f, 1f) - 0.5f);
                        var gy = Mathf.Abs(Mathf.Repeat(q.y * 7f, 1f) - 0.5f);
                        if (Mathf.Abs(q.x + 0.26f) < 0.01f) return Color.Lerp(c, new Color32(226, 104, 110, 255), 0.8f);
                        return gx > 0.44f || gy > 0.44f ? Color.Lerp(c, new Color32(150, 132, 200, 255), 0.6f) : c;
                    });
            }
        }

        private static void Crayons(ArtRaster r)
        {
            // An open box with crayons standing in it, and a pencil leaning across.
            var cols = new Color32[] { new Color32(214, 40, 40, 255), new Color32(240, 130, 30, 255), new Color32(250, 210, 40, 255), new Color32(50, 150, 65, 255), new Color32(40, 85, 195, 255), new Color32(130, 70, 170, 255) };
            for (var i = 0; i < cols.Length; i++)
            {
                var x = -0.3f + i * 0.12f;
                var top = 0.42f - (i % 2) * 0.06f;
                var col = cols[i];
                r.Shape(p => Mathf.Max(Mathf.Abs(p.x - x) - 0.045f, Mathf.Max(-0.1f - p.y, p.y - top)), col, Color.Lerp(col, Ink, 0.3f), Color.Lerp(col, Color.white, 0.4f), 0.015f, 0.03f);
                r.Shape(p => Mathf.Max(Mathf.Abs(p.x - x) * 2.4f + (p.y - top) - 0.11f, top - p.y), col, Color.Lerp(col, Ink, 0.3f), Color.Lerp(col, Color.white, 0.4f), 0.015f, 0.02f);
            }
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x) - 0.42f, Mathf.Abs(p.y + 0.26f) - 0.24f), new Color32(248, 200, 64, 255), new Color32(210, 150, 40, 255),
                new Color32(255, 230, 140, 255), 0.022f, 0.08f, (p, c) => Wash(p, Mathf.Abs(p.y + 0.28f) < 0.04f ? Color.Lerp(c, new Color32(214, 60, 50, 255), 0.85f) : c, 3));
            r.Shape(p => Capsule(p, new Vector2(-0.45f, -0.5f), new Vector2(0.4f, 0.38f), 0.03f), new Color32(70, 120, 70, 255), new Color32(40, 80, 40, 255),
                new Color32(130, 180, 120, 255), 0.015f, 0.02f);
        }

        private static void Kite(ArtRaster r)
        {
            var paper = new Color32(206, 72, 56, 255);
            var second = new Color32(236, 196, 90, 255);
            float Diamond(Vector2 p) { var q = p - new Vector2(0f, 0.08f); return (Mathf.Abs(q.x) / 0.36f + Mathf.Abs(q.y) / (q.y > 0 ? 0.36f : 0.5f) - 1f) * 0.22f; }
            r.Shape(Diamond, paper, Color.Lerp(paper, Ink, 0.3f), Color.Lerp(paper, Color.white, 0.3f), 0.02f, 0.06f,
                (p, c) => Wash(p, (p.x > 0) == (p.y > 0.08f) ? c : Color.Lerp(c, second, 0.85f), 3));
            r.Line(new Vector2(0, 0.44f), new Vector2(0, -0.42f), 2.6f, new Color32(120, 86, 46, 255), 1f, true);
            r.Line(new Vector2(-0.36f, 0.08f), new Vector2(0.36f, 0.08f), 2.6f, new Color32(120, 86, 46, 255), 1f, true);
            var prev = new Vector2(0, -0.42f);
            for (var i = 1; i <= 6; i++)
            {
                var p = new Vector2(Mathf.Sin(i * 0.9f) * 0.08f + i * 0.02f, -0.42f - i * 0.025f);
                r.Line(prev, p, 1.6f, Ink, 1f, false);
                prev = p;
            }
        }

        private static void Sandals(ArtRaster r)
        {
            var blue = new Color32(56, 110, 190, 255);
            foreach (var (x, a) in new[] { (-0.18f, 8f), (0.18f, -8f) })
            {
                var ang = a * Mathf.Deg2Rad;
                Vector2 L(Vector2 p) { var q = p - new Vector2(x, 0f); return new Vector2(q.x * Mathf.Cos(ang) + q.y * Mathf.Sin(ang), -q.x * Mathf.Sin(ang) + q.y * Mathf.Cos(ang)); }
                r.Shape(p => SmoothUnion(Ellipse(L(p), new Vector2(0, 0.2f), new Vector2(0.14f, 0.24f)), Ellipse(L(p), new Vector2(0, -0.22f), new Vector2(0.12f, 0.22f)), 0.1f),
                    blue, Color.Lerp(blue, Ink, 0.35f), Color.Lerp(blue, Color.white, 0.35f), 0.02f, 0.05f);
                // The white thong strap in a V.
                foreach (var sx in new[] { -1f, 1f })
                {
                    var a0 = new Vector2(x, 0.24f);
                    var a1 = new Vector2(x + sx * 0.12f, 0.02f);
                    r.Shape(p => Capsule(p, a0, a1, 0.025f), new Color32(244, 244, 244, 255), new Color32(200, 200, 200, 255), Color.white, 0.012f, 0.02f);
                }
            }
        }

        private static void Taffy(ArtRaster r)
        {
            // Kẹo kéo: pale pulled-sugar sticks rolled in sesame, on a scrap of paper.
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x) - 0.44f, Mathf.Abs(p.y + 0.12f) - 0.3f) + 0.0f, new Color32(240, 232, 212, 255), new Color32(210, 196, 170, 255), Color.white, 0.02f, 0.04f);
            foreach (var (a, b) in new[] { (new Vector2(-0.32f, -0.2f), new Vector2(0.3f, 0.1f)), (new Vector2(-0.3f, 0.02f), new Vector2(0.32f, -0.22f)), (new Vector2(-0.28f, 0.16f), new Vector2(0.34f, 0.2f)) })
            {
                var aa = a;
                var bb = b;
                r.Shape(p => Capsule(p, aa, bb, 0.07f), new Color32(250, 240, 214, 255), new Color32(220, 196, 150, 255), Color.white, 0.02f, 0.05f,
                    (p, c) => ValueNoise(p.x * 60f, p.y * 60f, 4) > 0.82f ? Color.Lerp(c, new Color32(200, 160, 100, 255), 0.7f) : c);
            }
        }


        private static void VitaminC(ArtRaster r)
        {
            // Kẹo C: a little clear bag of small round orange candies, a few spilled out in front.
            var orange = new Color32(246, 150, 40, 255);
            var yellow = new Color32(250, 200, 60, 255);
            r.Shape(p => SmoothUnion(Ellipse(p, new Vector2(-0.06f, -0.02f), new Vector2(0.34f, 0.38f)), Mathf.Max(Mathf.Abs(p.x + 0.06f) - 0.08f, Mathf.Abs(p.y - 0.4f) - 0.08f), 0.06f),
                new Color(0.9f, 0.95f, 0.97f, 0.55f), new Color(0.6f, 0.72f, 0.78f, 0.6f), new Color(1f, 1f, 1f, 0.8f), 0.018f, 0.05f);
            r.Shape(p => Capsule(p, new Vector2(-0.2f, 0.36f), new Vector2(0.08f, 0.36f), 0.025f), new Color32(214, 60, 50, 255), new Color32(150, 34, 32, 255),
                new Color32(244, 128, 108, 255), 0.012f, 0.02f); // the twist tie
            var rng = new System.Random(11);
            var spots = new List<Vector2>();
            for (var i = 0; i < 9; i++) spots.Add(new Vector2(-0.24f + (i % 3) * 0.17f + ((float)rng.NextDouble() - 0.5f) * 0.05f, -0.26f + (i / 3) * 0.15f));
            spots.Add(new Vector2(0.3f, -0.4f));
            spots.Add(new Vector2(0.44f, -0.3f));
            spots.Add(new Vector2(0.2f, -0.5f));
            foreach (var s in spots)
            {
                var c = s;
                var col = Color.Lerp(orange, yellow, (float)rng.NextDouble());
                r.Shape(p => (p - c).magnitude - 0.075f, col, Color.Lerp(col, new Color32(180, 80, 20, 255), 0.6f), Color.Lerp(col, Color.white, 0.6f), 0.014f, 0.035f,
                    (p, cc) => ValueNoise(p.x * 90f, p.y * 90f, 5) > 0.8f ? Color.Lerp(cc, Color.white, 0.35f) : cc); // sugar dust
            }
            r.Shape(p => Ellipse(p, new Vector2(-0.24f, 0.06f), new Vector2(0.04f, 0.2f)), new Color(1, 1, 1, 0.45f), new Color(1, 1, 1, 0f), new Color(1, 1, 1, 0.45f), 0f, 0.03f);
        }

        private static void Noodles(ArtRaster r)
        {
            // Mì tôm: a torn-open packet of instant noodles, crushed noodles showing at the top, the seasoning sachet beside it.
            var pack = new Color32(244, 186, 52, 255);
            float Jag(Vector2 p) => 0.2f + Mathf.Abs(Mathf.Repeat(p.x * 14f, 1f) - 0.5f) * 0.06f;
            float Pack(Vector2 p) => Mathf.Max(Mathf.Abs(p.x + 0.08f) - 0.34f, Mathf.Max(-0.46f - p.y, p.y - Jag(p))) - 0.02f;
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x + 0.08f) - 0.3f, Mathf.Abs(p.y - 0.24f) - 0.1f), new Color32(236, 214, 150, 255), new Color32(190, 160, 90, 255),
                new Color32(255, 240, 190, 255), 0.015f, 0.03f,
                (p, c) => Mathf.Abs(Mathf.Sin(p.x * 70f + Mathf.Sin(p.y * 60f) * 2f)) < 0.3f ? Color.Lerp(c, new Color32(180, 140, 70, 255), 0.6f) : c); // wavy noodles
            r.Shape(Pack, pack, new Color32(200, 130, 30, 255), new Color32(255, 220, 120, 255), 0.022f, 0.07f,
                (p, c) =>
                {
                    if (Mathf.Abs(p.y + 0.12f) < 0.07f) c = Color.Lerp(c, new Color32(206, 50, 40, 255), 0.9f); // red band
                    if ((p - new Vector2(-0.08f, -0.32f)).magnitude < 0.09f) c = Color.Lerp(c, new Color32(150, 200, 90, 255), 0.85f); // a sprig on the label
                    return Wash(p, c, 21);
                });
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x - 0.4f) - 0.12f, Mathf.Abs(p.y + 0.32f) - 0.08f) - 0.015f, new Color32(214, 216, 222, 255), new Color32(150, 154, 164, 255),
                Color.white, 0.015f, 0.04f, (p, c) => Mathf.Abs(p.x - 0.4f) < 0.05f ? Color.Lerp(c, new Color32(206, 50, 40, 255), 0.7f) : c);
        }

        private static void BubbleGum(ArtRaster r)
        {
            // Kẹo su: a chunk of pink bubble gum on its peeled wrapper, and the tattoo sticker that came inside.
            var paper = new Color32(250, 250, 244, 255);
            var ang = -10f * Mathf.Deg2Rad;
            Vector2 W(Vector2 p) { var q = p - new Vector2(-0.08f, -0.12f); return new Vector2(q.x * Mathf.Cos(ang) + q.y * Mathf.Sin(ang), -q.x * Mathf.Sin(ang) + q.y * Mathf.Cos(ang)); }
            r.Shape(p => Mathf.Max(Mathf.Abs(W(p).x) - 0.4f, Mathf.Abs(W(p).y) - 0.22f), paper, new Color32(210, 210, 200, 255), Color.white, 0.018f, 0.04f,
                (p, c) => Mathf.Abs(W(p).y) > 0.14f ? Color.Lerp(c, new Color32(60, 110, 200, 255), 0.85f) : Mathf.Abs(W(p).y) > 0.11f ? Color.Lerp(c, new Color32(220, 50, 50, 255), 0.85f) : c);
            var pink = new Color32(246, 140, 176, 255);
            r.Shape(p => Mathf.Max(Mathf.Abs(W(p).x + 0.04f) - 0.2f, Mathf.Abs(W(p).y - 0.02f) - 0.1f) - 0.04f, pink, new Color32(210, 90, 130, 255), new Color32(255, 200, 220, 255), 0.018f, 0.06f,
                (p, c) => Mathf.Abs(Mathf.Repeat(W(p).x * 8f, 1f) - 0.5f) < 0.06f ? Color.Lerp(c, new Color32(210, 90, 130, 255), 0.5f) : c); // scored into sticks
            // The tattoo sticker: a small card with a star.
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x - 0.28f) - 0.17f, Mathf.Abs(p.y - 0.3f) - 0.17f) - 0.02f, new Color32(244, 240, 226, 255), new Color32(200, 194, 176, 255), Color.white, 0.016f, 0.04f);
            float Star(Vector2 p)
            {
                var q = p - new Vector2(0.28f, 0.3f);
                var a = Mathf.Atan2(q.y, q.x) + Mathf.PI / 2f;
                var k = Mathf.Abs(Mathf.Repeat(a / (Mathf.PI * 2f / 5f), 1f) - 0.5f) * 2f;
                return q.magnitude - Mathf.Lerp(0.055f, 0.13f, k);
            }
            r.Shape(Star, new Color32(240, 70, 60, 255), new Color32(170, 40, 34, 255), new Color32(255, 170, 150, 255), 0.012f, 0.03f);
        }
        private static void IcePop(ArtRaster r)
        {
            // Cà rem: a coconut ice cream bar on a stick, a bite taken out of the corner.
            r.Shape(p => Capsule(p, new Vector2(0f, -0.52f), new Vector2(0f, -0.1f), 0.04f), new Color32(220, 190, 130, 255), new Color32(170, 140, 90, 255),
                new Color32(240, 220, 170, 255), 0.015f, 0.02f);
            float Pop(Vector2 p) => Mathf.Max(Mathf.Max(Mathf.Abs(p.x) - 0.2f, Mathf.Abs(p.y - 0.14f) - 0.32f) - 0.05f, -((p - new Vector2(0.2f, 0.42f)).magnitude - 0.1f));
            r.Shape(Pop, new Color32(250, 246, 236, 255), new Color32(210, 200, 186, 255), Color.white, 0.02f, 0.06f,
                (p, c) => p.y < 0.0f ? Color.Lerp(c, new Color32(120, 190, 130, 255), 0.8f) : p.y < 0.2f ? Color.Lerp(c, new Color32(200, 120, 80, 255), 0.6f) : c);
        }

    }
}
