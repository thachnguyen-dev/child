using System.Collections.Generic;
using ChildGame.UI;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.MiniGames.MoCau
{
    /// <summary>
    /// Official generated art for Đua mo cau (owner 2026-10-08): the sloping dirt road at the village entrance seen from
    /// above, trees, grass and bushes on both sides; raised dirt mounds, big rocks and bushes as obstacles; a single
    /// finish line; children riding areca-palm sheaths (mo cau). Drawn with <see cref="ArtRaster"/> in the style of the
    /// hand art (soft shading, warm dark outline) and cached for the session.
    /// </summary>
    public static class MoCauArt
    {
        /// <summary>One ground tile: road and verges, seamless vertically every <see cref="TileHeight"/>.</summary>
        public const float TileWidth = 40f, TileHeight = 20f;

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly Color Shadow = new Color(0.1f, 0.12f, 0.05f, 0.28f);
        private static readonly Color Skin = new Color32(232, 184, 142, 255);
        private static readonly Color SkinShade = new Color32(196, 138, 102, 255);
        private static readonly Color SkinLight = new Color32(250, 214, 180, 255);

        private static bool Cached(string key, out Sprite s) => cache.TryGetValue(key, out s) && s != null;

        /// <summary>Value noise that repeats every <paramref name="periodY"/> lattice cells vertically.</summary>
        private static float PNoise(float x, float y, int periodY, int seed)
        {
            var xi = Mathf.FloorToInt(x);
            var yi = Mathf.FloorToInt(y);
            var fx = x - xi;
            var fy = y - yi;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            int W(int v) => ((v % periodY) + periodY) % periodY;
            return Mathf.Lerp(Mathf.Lerp(Hash(xi, W(yi), seed), Hash(xi + 1, W(yi), seed), fx),
                Mathf.Lerp(Hash(xi, W(yi + 1), seed), Hash(xi + 1, W(yi + 1), seed), fx), fy);
        }

        /// <summary>Noise in world units with <paramref name="cellsPerUnit"/> cells per unit, seamless over the tile height.</summary>
        private static float TNoise(Vector2 p, float cellsPerUnit, int seed) =>
            PNoise(p.x * cellsPerUnit, p.y * cellsPerUnit, Mathf.RoundToInt(TileHeight * cellsPerUnit), seed);

        /// <summary>Where the road's left / right edge is at height y (gently wavy, seamless).</summary>
        private static float Edge(float y, float halfWidth, int side) =>
            side * (halfWidth + 0.35f + 0.18f * Mathf.Sin(y * Mathf.PI * 2f / TileHeight * 2f + side) + 0.12f * (TNoise(new Vector2(side * 3f, y), 1f, 7) - 0.5f));

        // ---- Ground ----

        /// <summary>
        /// The ground: a packed-earth road (worn ruts from the sheaths, pebbles, cracks) between grassy verges with tufts and
        /// little flowers. Pivot at the tile centre; tiles stack vertically without seams.
        /// </summary>
        public static Sprite Ground(float halfWidth)
        {
            if (Cached("ground", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-TileWidth * 0.5f, -TileHeight * 0.5f, TileWidth, TileHeight), 22f);
            var dirt = new Color32(206, 172, 120, 255);
            var dirtDark = new Color32(176, 140, 92, 255);
            var grass = new Color32(108, 164, 74, 255);
            var grassDark = new Color32(80, 138, 60, 255);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var left = Edge(p.y, halfWidth, -1);
                var right = Edge(p.y, halfWidth, 1);
                var inRoad = p.x > left && p.x < right;
                var toEdge = Mathf.Min(p.x - left, right - p.x); // > 0 on the road
                Color c;
                if (inRoad)
                {
                    var n = 0.6f * TNoise(p, 1.5f, 1) + 0.4f * TNoise(p, 6f, 2);
                    c = Color.Lerp(dirtDark, dirt, n);
                    // Worn ruts down the slope, where the sheaths slide.
                    foreach (var rx in new[] { -2.2f, 0.1f, 2.4f })
                    {
                        var wob = rx + 0.35f * Mathf.Sin(p.y * Mathf.PI * 2f / TileHeight * 3f + rx);
                        var d = Mathf.Abs(p.x - wob);
                        c = Color.Lerp(c, new Color32(226, 196, 146, 255), Mathf.Clamp01(1f - d / 0.45f) * 0.45f);
                    }
                    // Darker, rougher near the verges.
                    c = Color.Lerp(new Color32(150, 122, 80, 255), c, Mathf.Clamp01(toEdge / 0.45f) * 0.6f + 0.4f);
                }
                else
                {
                    var n = 0.5f * TNoise(p, 0.8f, 3) + 0.35f * TNoise(p, 4f, 4) + 0.15f * TNoise(p, 14f, 5);
                    c = Color.Lerp(grassDark, grass, n);
                    // A soft line of trodden earth and short grass along the road.
                    c = Color.Lerp(new Color32(150, 150, 84, 255), c, Mathf.Clamp01(-toEdge / 0.35f) * 0.7f + 0.3f);
                }
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            foreach (var o in new[] { -TileHeight, 0f, TileHeight })
            {
                var prng = new System.Random(12);
                // Pebbles on the road.
                for (var i = 0; i < 120; i++)
                {
                    var p = new Vector2(((float)prng.NextDouble() - 0.5f) * halfWidth * 2f, ((float)prng.NextDouble() - 0.5f) * TileHeight + o);
                    var rad = new Vector2(0.05f, 0.04f) * (0.7f + (float)prng.NextDouble() * 1.2f);
                    var tone = (float)prng.NextDouble();
                    r.Shape(q => Ellipse(q, p, rad), Color.Lerp(new Color32(150, 140, 128, 255), new Color32(196, 186, 170, 255), tone),
                        new Color32(120, 110, 100, 255), new Color32(226, 220, 210, 255), 0f, 0.03f, null, new Rect(p - rad * 2f, rad * 4f));
                }
                // Hairline cracks in the dry earth.
                for (var i = 0; i < 16; i++)
                {
                    var p = new Vector2(((float)prng.NextDouble() - 0.5f) * halfWidth * 1.6f, ((float)prng.NextDouble() - 0.5f) * TileHeight + o);
                    var a = (float)prng.NextDouble() * Mathf.PI;
                    for (var k = 0; k < 4; k++)
                    {
                        var next = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.22f;
                        r.Line(p, next, 1.2f, new Color32(150, 116, 76, 255), 0.6f, true);
                        p = next;
                        a += ((float)prng.NextDouble() - 0.5f) * 1.2f;
                    }
                }
                // Grass tufts and flowers on the verges.
                var grng = new System.Random(13);
                for (var i = 0; i < 900; i++)
                {
                    var side = grng.NextDouble() < 0.5 ? -1 : 1;
                    var x = side * (halfWidth + 0.3f + (float)grng.NextDouble() * (TileWidth * 0.5f - halfWidth));
                    var p = new Vector2(x, ((float)grng.NextDouble() - 0.5f) * TileHeight + o);
                    if (Mathf.Abs(p.x) < Mathf.Abs(Edge(p.y, halfWidth, side)) + 0.05f) continue;
                    var tone = (float)grng.NextDouble();
                    var blade = Color.Lerp(new Color32(64, 120, 50, 255), new Color32(150, 196, 96, 255), tone);
                    for (var b = 0; b < 3; b++)
                    {
                        var a = Mathf.PI * 0.5f + ((float)grng.NextDouble() - 0.5f) * 1.4f;
                        r.Line(p, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.16f, 1.6f, blade, 0.9f, true);
                    }
                    if (grng.NextDouble() < 0.08)
                    {
                        var petal = grng.NextDouble() < 0.5 ? new Color32(250, 250, 240, 255) : new Color32(250, 214, 70, 255);
                        r.Shape(q => (q - p).magnitude - 0.06f, petal, Color.Lerp(petal, Color.black, 0.15f), Color.white, 0f, 0.03f, null,
                            new Rect(p - Vector2.one * 0.1f, Vector2.one * 0.2f));
                    }
                }
            }
            var s = r.ToSprite("Ground");
            cache["ground"] = s;
            return s;
        }

        // ---- Roadside ----

        /// <summary>A leafy tree from above: a lumpy canopy with light on the upper left and a shadow on the ground.</summary>
        public static Sprite Tree(int variant)
        {
            if (Cached("tree" + variant, out var cached)) return cached;
            var rng = new System.Random(100 + variant);
            var r = new ArtRaster(new Rect(-2.1f, -2.3f, 4.4f, 4.4f), 50f);
            var lobes = new List<(Vector2 c, float r)>();
            for (var i = 0; i < 9; i++)
            {
                var a = i / 9f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f;
                lobes.Add((new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.05f, 0.62f + (float)rng.NextDouble() * 0.2f));
            }
            lobes.Add((Vector2.zero, 1.1f));
            float Canopy(Vector2 p)
            {
                var d = 10f;
                foreach (var (c, rr) in lobes) d = SmoothUnion(d, (p - c).magnitude - rr, 0.25f);
                return d;
            }
            r.Shape(p => Canopy(p - new Vector2(0.3f, -0.35f)), Shadow, Shadow, Shadow, 0f, 0.2f);
            var leaf = variant % 2 == 0 ? new Color32(70, 132, 60, 255) : new Color32(92, 146, 56, 255);
            r.Shape(Canopy, leaf, Color.Lerp(leaf, Color.black, 0.35f), Color.Lerp(leaf, new Color32(230, 240, 150, 255), 0.45f), 0.05f, 0.45f,
                (p, c) => Color.Lerp(c, Color.Lerp(c, Color.black, 0.25f), Mathf.Clamp01(ValueNoise(p.x * 4f, p.y * 4f, variant) - 0.45f)));
            // Leaf clusters: small bright lobes catching the light.
            for (var i = 0; i < 14; i++)
            {
                var c = new Vector2(((float)rng.NextDouble() - 0.5f) * 2.4f, ((float)rng.NextDouble() - 0.5f) * 2.4f);
                if (c.magnitude > 1.3f) continue;
                var rr = 0.22f + (float)rng.NextDouble() * 0.15f;
                r.Shape(p => (p - c).magnitude - rr, Color.Lerp(leaf, Color.white, 0.12f), leaf, Color.Lerp(leaf, new Color32(230, 240, 150, 255), 0.5f), 0f, 0.15f);
            }
            var s = r.ToSprite("Tree", Vector2.zero);
            cache["tree" + variant] = s;
            return s;
        }

        /// <summary>An areca palm (cây cau) from above: a star of long arching fronds with leaflets, and its shadow.</summary>
        public static Sprite Areca(int variant)
        {
            if (Cached("areca" + variant, out var cached)) return cached;
            var rng = new System.Random(200 + variant);
            var r = new ArtRaster(new Rect(-2.4f, -2.6f, 4.9f, 4.9f), 50f);
            var fronds = new List<(float a, float len)>();
            for (var i = 0; i < 9; i++) fronds.Add((i / 9f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.3f, 1.6f + (float)rng.NextDouble() * 0.5f));
            // The shadow is drawn solid into its own layer, then laid down once at shadow strength (overlapping
            // fronds would otherwise darken it to black).
            var shade = new ArtRaster(new Rect(-2.4f, -2.6f, 4.9f, 4.9f), 50f);
            foreach (var pass in new[] { 0, 1 })
            {
                if (pass == 1)
                    for (var i = 0; i < r.px.Length; i++)
                        if (shade.px[i].a > 0f) r.px[i] = new Color(Shadow.r, Shadow.g, Shadow.b, shade.px[i].a * Shadow.a);
                var off = pass == 0 ? new Vector2(0.35f, -0.4f) : Vector2.zero;
                foreach (var (a, len) in fronds)
                {
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var side = new Vector2(-dir.y, dir.x);
                    var prev = off;
                    for (var k = 1; k <= 14; k++)
                    {
                        var t = k / 14f;
                        var pt = off + dir * (len * t) + side * (Mathf.Sin(t * Mathf.PI) * 0.15f);
                        var green = pass == 0 ? Color.black : (Color)Color.Lerp(new Color32(60, 116, 48, 255), new Color32(120, 170, 70, 255), t);
                        (pass == 0 ? shade : r).Line(prev, pt, pass == 0 ? 5f : 3f, green, 1f, false);
                        var leafLen = 0.42f * Mathf.Sin(Mathf.Lerp(0.2f, 1f, t) * Mathf.PI * 0.9f);
                        foreach (var sgn in new[] { -1f, 1f })
                            (pass == 0 ? shade : r).Line(pt, pt + (side * sgn * 0.9f + dir * 0.45f).normalized * leafLen, pass == 0 ? 4f : 3.2f,
                                pass == 0 ? Color.black : (Color)Color.Lerp(new Color32(70, 128, 54, 255), new Color32(140, 186, 84, 255), (float)rng.NextDouble()), 1f, false);
                        prev = pt;
                    }
                }
            }
            r.Shape(p => p.magnitude - 0.22f, new Color32(150, 160, 80, 255), new Color32(100, 110, 50, 255), new Color32(196, 200, 120, 255), 0.04f, 0.1f);
            var s = r.ToSprite("Areca", Vector2.zero);
            cache["areca" + variant] = s;
            return s;
        }

        /// <summary>A round leafy bush from above (roadside or obstacle), 1 unit across, with a shadow.</summary>
        public static Sprite Bush(int variant)
        {
            if (Cached("bush" + variant, out var cached)) return cached;
            var rng = new System.Random(300 + variant);
            var r = new ArtRaster(new Rect(-0.7f, -0.78f, 1.45f, 1.45f), 110f);
            var lobes = new List<(Vector2 c, float r)>();
            for (var i = 0; i < 7; i++)
            {
                var a = i / 7f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.5f;
                lobes.Add((new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.26f, 0.2f + (float)rng.NextDouble() * 0.07f));
            }
            lobes.Add((Vector2.zero, 0.3f));
            float Shape(Vector2 p)
            {
                var d = 10f;
                foreach (var (c, rr) in lobes) d = SmoothUnion(d, (p - c).magnitude - rr, 0.08f);
                return d;
            }
            r.Shape(p => Shape(p - new Vector2(0.09f, -0.1f)), Shadow, Shadow, Shadow, 0f, 0.1f);
            var leaf = variant % 2 == 0 ? new Color32(64, 128, 58, 255) : new Color32(84, 140, 52, 255);
            r.Shape(Shape, leaf, Color.Lerp(leaf, Color.black, 0.35f), Color.Lerp(leaf, new Color32(220, 236, 140, 255), 0.5f), 0.025f, 0.16f,
                (p, c) => Color.Lerp(c, Color.Lerp(c, Color.black, 0.2f), Mathf.Clamp01(ValueNoise(p.x * 14f, p.y * 14f, variant) - 0.5f)));
            for (var i = 0; i < 8; i++)
            {
                var c = new Vector2(((float)rng.NextDouble() - 0.5f) * 0.7f, ((float)rng.NextDouble() - 0.5f) * 0.7f);
                if (c.magnitude > 0.38f) continue;
                var rr = 0.06f + (float)rng.NextDouble() * 0.05f;
                r.Shape(p => (p - c).magnitude - rr, Color.Lerp(leaf, Color.white, 0.15f), leaf, Color.Lerp(leaf, new Color32(220, 236, 140, 255), 0.5f), 0f, 0.05f);
            }
            var s = r.ToSprite("Bush", Vector2.zero);
            cache["bush" + variant] = s;
            return s;
        }

        // ---- Obstacles ----

        /// <summary>A big rock, 1 unit across: faceted grey stone with light on top, a shadow on the road.</summary>
        public static Sprite Rock(int variant)
        {
            if (Cached("rock" + variant, out var cached)) return cached;
            var rng = new System.Random(400 + variant);
            var r = new ArtRaster(new Rect(-0.7f, -0.75f, 1.45f, 1.45f), 110f);
            var pts = new List<Vector2>();
            for (var i = 0; i < 9; i++)
            {
                var a = i / 9f * Mathf.PI * 2f;
                pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.42f + (float)rng.NextDouble() * 0.1f));
            }
            float Stone(Vector2 p)
            {
                // A rounded polygon: max over edge half-planes, softened.
                var d = -10f;
                for (var i = 0; i < pts.Count; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % pts.Count];
                    var n = new Vector2(b.y - a.y, a.x - b.x).normalized;
                    d = Mathf.Max(d, Vector2.Dot(p - a, n));
                }
                return d - 0.04f;
            }
            r.Shape(p => Stone(p - new Vector2(0.1f, -0.11f)), Shadow, Shadow, Shadow, 0f, 0.1f);
            r.Shape(Stone, new Color32(150, 146, 140, 255), new Color32(96, 92, 90, 255), new Color32(214, 210, 200, 255), 0.03f, 0.2f,
                (p, c) =>
                {
                    // Facets: the upper-left face catches the light; speckles of lichen and grit.
                    var face = Vector2.Dot(p.normalized, new Vector2(-0.6f, 0.8f)) * Mathf.Clamp01(p.magnitude / 0.3f);
                    c = Color.Lerp(c, new Color32(196, 192, 184, 255), Mathf.Clamp01(face) * 0.35f);
                    var grit = ValueNoise(p.x * 30f, p.y * 30f, variant);
                    return Color.Lerp(c, grit > 0.8f ? new Color32(150, 160, 110, 255) : (Color)c * 0.92f, grit > 0.7f ? 0.5f : 0f);
                });
            r.Line(new Vector2(-0.1f, 0.25f), new Vector2(0.05f, -0.05f), 1.6f, new Color32(96, 92, 90, 255), 0.7f, true);
            var s = r.ToSprite("Rock", Vector2.zero);
            cache["rock" + variant] = s;
            return s;
        }

        /// <summary>A raised mound of dug earth, 1 unit across: lumpy brown soil, lighter on its crest, with clods.</summary>
        public static Sprite Mound(int variant)
        {
            if (Cached("mound" + variant, out var cached)) return cached;
            var rng = new System.Random(500 + variant);
            var r = new ArtRaster(new Rect(-0.7f, -0.75f, 1.45f, 1.45f), 110f);
            var lobes = new List<(Vector2 c, float r)>();
            for (var i = 0; i < 6; i++)
            {
                var a = i / 6f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.6f;
                lobes.Add((new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.22f, 0.24f + (float)rng.NextDouble() * 0.06f));
            }
            float Shape(Vector2 p)
            {
                var d = 10f;
                foreach (var (c, rr) in lobes) d = SmoothUnion(d, (p - c).magnitude - rr, 0.12f);
                return d;
            }
            r.Shape(p => Shape(p - new Vector2(0.08f, -0.09f)), Shadow, Shadow, Shadow, 0f, 0.1f);
            // Heaped earth: lighter on its rounded crest (upper left), darker toward the foot, with grainy soil.
            r.Shape(Shape, new Color32(150, 104, 64, 255), new Color32(96, 62, 38, 255), new Color32(206, 162, 110, 255), 0.03f, 0.3f,
                (p, c) =>
                {
                    var crest = Mathf.Clamp01(1f - (p - new Vector2(-0.08f, 0.1f)).magnitude / 0.4f);
                    c = Color.Lerp(c, new Color32(190, 146, 98, 255), crest * 0.5f);
                    return (Color)c * (0.9f + 0.18f * ValueNoise(p.x * 18f, p.y * 18f, variant));
                });
            for (var i = 0; i < 10; i++)
            {
                var c = new Vector2(((float)rng.NextDouble() - 0.5f) * 0.8f, ((float)rng.NextDouble() - 0.5f) * 0.8f);
                if (Shape(c) > -0.05f) continue;
                var rr = 0.03f + (float)rng.NextDouble() * 0.03f;
                // Clods: soft bumps lit from the upper left, no outline.
                r.Shape(p => (p - c).magnitude - rr, new Color32(176, 130, 86, 255), new Color32(140, 98, 62, 255), new Color32(214, 174, 124, 255), 0f, rr,
                    (p, col) => Color.Lerp(col, new Color32(110, 74, 46, 255), Mathf.Clamp01(Vector2.Dot((p - c) / rr, new Vector2(0.6f, -0.8f))) * 0.5f));
            }
            var s = r.ToSprite("Mound", Vector2.zero);
            cache["mound" + variant] = s;
            return s;
        }

        // ---- Lines, gate, racers ----

        /// <summary>A chalk line across the road (the finish), 1 unit long: rough-edged white.</summary>
        public static Sprite ChalkLine()
        {
            if (Cached("chalk", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.5f, -0.08f, 1f, 0.16f), 400f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var edge = 0.045f + 0.012f * (ValueNoise(p.x * 60f, 0.5f, 3) - 0.5f);
                var a = Mathf.Clamp01((edge - Mathf.Abs(p.y)) * r.ppu * 0.5f);
                a *= 0.82f + 0.18f * ValueNoise(p.x * 200f, p.y * 200f, 4);
                r.px[y * r.w + x] = new Color(0.98f, 0.98f, 0.95f, a);
            }
            var s = r.ToSprite("Chalk");
            cache["chalk"] = s;
            return s;
        }

        /// <summary>
        /// The village gate (cổng làng) over the top of the slope, seen from above: a tiled roof with a ridge and upturned
        /// ends on two brick pillars. Pivot at its centre; <paramref name="span"/> = width across the road.
        /// </summary>
        public static Sprite Gate(float span)
        {
            if (Cached("gate", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-span * 0.5f - 1.9f, -1.6f, span + 3.8f, 3.0f), 60f);
            var half = span * 0.5f;
            foreach (var sx in new[] { -1f, 1f })
                // Brick pillars standing beside the roof ends, with a lighter capstone.
                r.Shape(p => Mathf.Max(Mathf.Abs(p.x - sx * (half + 1.15f)) - 0.5f, Mathf.Abs(p.y) - 0.5f) - 0.05f, new Color32(186, 96, 70, 255),
                    new Color32(130, 60, 44, 255), new Color32(220, 140, 110, 255), 0.05f, 0.15f,
                    (p, c) => Mathf.Repeat(p.y * 6f + (Mathf.Repeat(p.x * 3f, 1f) > 0.5f ? 0.5f : 0f), 1f) < 0.12f ? (Color)c * 0.8f : c); // brick courses
            foreach (var sx in new[] { -1f, 1f })
                r.Shape(p => Mathf.Max(Mathf.Abs(p.x - sx * (half + 1.15f)) - 0.3f, Mathf.Abs(p.y) - 0.3f) - 0.04f, new Color32(222, 214, 196, 255),
                    new Color32(170, 160, 140, 255), Color.white, 0.04f, 0.08f);
            float Roof(Vector2 p) => Mathf.Max(Mathf.Abs(p.x) - (half + 0.65f - Mathf.Abs(p.y) * 0.25f), Mathf.Abs(p.y) - 0.75f) - 0.05f;
            r.Shape(p => Roof(p - new Vector2(0.25f, -0.4f)), Shadow, Shadow, Shadow, 0f, 0.1f);
            r.Shape(Roof, new Color32(176, 84, 58, 255), new Color32(120, 50, 36, 255), new Color32(220, 130, 96, 255), 0.05f, 0.2f,
                (p, c) =>
                {
                    // Rows of curved tiles running down from the ridge.
                    var ridge = Mathf.Abs(p.y);
                    var tile = 0.5f + 0.5f * Mathf.Sin(p.x * Mathf.PI * 2f / 0.22f);
                    var row = Mathf.Repeat(ridge, 0.24f) / 0.24f;
                    c = Color.Lerp(c, (Color)c * 0.78f, tile * 0.35f + (row > 0.85f ? 0.3f : 0f));
                    return c;
                });
            // The ridge with curled ends.
            r.Shape(p => Capsule(p, new Vector2(-half - 0.4f, 0f), new Vector2(half + 0.4f, 0f), 0.09f), new Color32(150, 66, 46, 255),
                new Color32(100, 40, 30, 255), new Color32(210, 120, 90, 255), 0.04f, 0.05f);
            foreach (var sx in new[] { -1f, 1f })
                r.Shape(p => Mathf.Abs((p - new Vector2(sx * (half + 0.5f), 0.12f)).magnitude - 0.16f) - 0.05f, new Color32(150, 66, 46, 255),
                    new Color32(100, 40, 30, 255), new Color32(210, 120, 90, 255), 0.03f, 0.03f);
            var s = r.ToSprite("Gate");
            cache["gate"] = s;
            return s;
        }

        /// <summary>
        /// A child riding a mo cau, from above, heading up the screen: the long areca sheath with its curled front, the
        /// child sitting with knees up and both hands gripping the front edge. Pivot at the sled's centre.
        /// </summary>
        public static Sprite Sled(Color shirt, bool girl)
        {
            var key = "sled" + ColorUtility.ToHtmlStringRGB(shirt) + girl;
            if (Cached(key, out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.62f, -1.2f, 1.3f, 2.3f), 120f);
            float Sheath(Vector2 p)
            {
                // Wide at the front, narrowing to the stalk end at the back.
                var w = Mathf.Lerp(0.2f, 0.4f, Mathf.InverseLerp(-0.95f, 0.5f, p.y));
                var body = Mathf.Max(Mathf.Abs(p.x) - w, Mathf.Abs(p.y + 0.15f) - 0.88f);
                return SmoothUnion(body, Ellipse(p, new Vector2(0, 0.6f), new Vector2(0.4f, 0.22f)), 0.08f) - 0.02f;
            }
            r.Shape(p => Sheath(p - new Vector2(0.07f, -0.08f)), Shadow, Shadow, Shadow, 0f, 0.05f);
            var tan = new Color32(206, 184, 120, 255);
            r.Shape(Sheath, tan, new Color32(160, 130, 76, 255), new Color32(236, 222, 170, 255), 0.03f, 0.1f,
                // Veins along the sheath and a green tinge toward the stalk.
                (p, c) => Color.Lerp(Color.Lerp(c, (Color)c * 0.86f, Mathf.Pow(Mathf.Abs(Mathf.Sin(p.x * 70f)), 6f) * 0.5f),
                    new Color32(160, 170, 90, 255), Mathf.Clamp01((-0.3f - p.y) / 0.7f) * 0.4f));
            // The curled front lip.
            r.Shape(p => Mathf.Max(Ellipse(p, new Vector2(0, 0.7f), new Vector2(0.4f, 0.16f)), 0.62f - p.y), new Color32(176, 140, 80, 255),
                new Color32(130, 100, 56, 255), new Color32(226, 200, 140, 255), 0.03f, 0.06f);
            // Legs bent up in front of the body (shorts, then knees).
            var shorts = Color.Lerp(shirt, new Color32(40, 50, 90, 255), 0.6f);
            foreach (var sx in new[] { -1f, 1f })
            {
                r.Shape(p => Capsule(p, new Vector2(sx * 0.13f, -0.05f), new Vector2(sx * 0.15f, 0.22f), 0.09f), shorts, Color.Lerp(shorts, Color.black, 0.3f),
                    Color.Lerp(shorts, Color.white, 0.3f), 0.025f, 0.05f);
                r.Shape(p => (p - new Vector2(sx * 0.16f, 0.3f)).magnitude - 0.085f, Skin, SkinShade, SkinLight, 0.025f, 0.04f);
            }
            // Body (shirt) and arms reaching to the front edge.
            r.Shape(p => Ellipse(p, new Vector2(0, -0.28f), new Vector2(0.26f, 0.2f)), shirt, Color.Lerp(shirt, Color.black, 0.3f),
                Color.Lerp(shirt, Color.white, 0.35f), 0.03f, 0.1f);
            foreach (var sx in new[] { -1f, 1f })
            {
                r.Shape(p => Capsule(p, new Vector2(sx * 0.24f, -0.22f), new Vector2(sx * 0.32f, 0.5f), 0.055f), Skin, SkinShade, SkinLight, 0.022f, 0.04f);
                r.Shape(p => (p - new Vector2(sx * 0.32f, 0.55f)).magnitude - 0.065f, Skin, SkinShade, SkinLight, 0.022f, 0.03f);
            }
            // Head from above: mostly hair, a hint of ears.
            foreach (var sx in new[] { -1f, 1f })
                r.Shape(p => Ellipse(p, new Vector2(sx * 0.15f, -0.32f), new Vector2(0.04f, 0.055f)), Skin, SkinShade, SkinLight, 0.018f, 0.02f);
            var hair = new Color32(36, 28, 24, 255);
            r.Shape(p => Ellipse(p, new Vector2(0, -0.33f), new Vector2(0.15f, 0.17f)), hair, new Color32(18, 14, 12, 255), new Color32(110, 96, 86, 255), 0.025f, 0.06f);
            if (girl)
                foreach (var sx in new[] { -1f, 1f })
                    r.Shape(p => Capsule(p, new Vector2(sx * 0.1f, -0.45f), new Vector2(sx * 0.16f, -0.6f), 0.045f), hair, new Color32(18, 14, 12, 255),
                        new Color32(110, 96, 86, 255), 0.02f, 0.03f);
            var s = r.ToSprite("Sled");
            cache[key] = s;
            return s;
        }

        /// <summary>A small yellow chevron that marks the player's sled.</summary>
        public static Sprite Marker()
        {
            if (Cached("marker", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.3f, -0.25f, 0.6f, 0.5f), 120f);
            r.Shape(p => Mathf.Max(Mathf.Abs(p.x) * 0.9f + p.y - 0.12f, -p.y - 0.14f + Mathf.Abs(p.x) * 0.6f), new Color32(255, 200, 60, 255),
                new Color32(220, 150, 30, 255), new Color32(255, 236, 150, 255), 0.03f, 0.05f);
            var s = r.ToSprite("Marker");
            cache["marker"] = s;
            return s;
        }

        /// <summary>A puff of dust kicked up behind a sled.</summary>
        public static Sprite Dust()
        {
            if (Cached("dust", out var cached)) return cached;
            var r = new ArtRaster(new Rect(-0.3f, -0.3f, 0.6f, 0.6f), 100f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var a = Mathf.Clamp01(1f - p.magnitude / 0.28f);
                a = Mathf.Sqrt(a) * a * (0.75f + 0.25f * ValueNoise(p.x * 20f, p.y * 20f, 6));
                r.px[y * r.w + x] = new Color(0.98f, 0.95f, 0.88f, a);
            }
            var s = r.ToSprite("Dust");
            cache["dust"] = s;
            return s;
        }
    }
}
