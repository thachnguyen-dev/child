using System.Collections.Generic;
using ChildGame.UI;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.MiniGames.ChuonChuon
{
    /// <summary>
    /// Official generated art for the swimming part of MG07 (owner 2026-10-08: "a natural river instead of a swimming
    /// pool, so there should be no lane-divider lines"). A side view across a village river, slightly from above:
    /// <list type="bullet">
    /// <item>sky with soft clouds;</item>
    /// <item>the far bank: a bamboo hedge (lũy tre), coconut palms, banana plants and a thatched hut, hazy with
    /// distance, and their reflection on the water;</item>
    /// <item>murky green river water, lighter toward the far bank, with ripple glints (tiles side by side, seamless);</item>
    /// <item>swimmers in parts (body, arms, legs) so strokes and kicks animate; splashes, bubbles, wake rings;</item>
    /// <item>bamboo stakes with a red scarf marking the finish.</item>
    /// </list>
    /// Sprites are cached for the session, so the three first-time dives and the race do not redraw them.
    /// </summary>
    public static class RiverArt
    {
        /// <summary>The water's far edge (the far bank's waterline).</summary>
        public const float FarWater = 1.75f;
        /// <summary>The bottom of the water art (below the screen).</summary>
        public const float WaterBottom = -6.2f;
        /// <summary>Width of one water / bank tile (they repeat seamlessly).</summary>
        public const float WaterTile = 16f, BankTile = 24f;

        private const float BankBottom = FarWater - 0.6f, BankTop = FarWater + 3.9f;

        private static readonly Color Skin = new Color32(232, 184, 142, 255);
        private static readonly Color SkinShade = new Color32(196, 138, 102, 255);
        private static readonly Color SkinLight = new Color32(250, 214, 180, 255);
        private static readonly Color DeepWater = new Color32(38, 92, 98, 255);
        private static readonly Color FarWaterColor = new Color32(128, 174, 172, 255);
        private static readonly Color Haze = new Color32(196, 220, 222, 255);

        private static Sprite water, bank, splash, bubble, ring, stake;
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static List<(Vector2 a, float len, float t)> dashes;

        // ---- Sky ----

        /// <summary>The sky behind everything (follows the camera): a soft gradient, a sun glow and a few clouds.</summary>
        public static Sprite Sky(float width)
        {
            var key = "sky" + Mathf.RoundToInt(width);
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var r = new ArtRaster(new Rect(-width * 0.5f, -5.8f, width, 11.6f), 24f);
            var top = new Color32(104, 168, 224, 255);
            var horizon = new Color32(206, 232, 240, 255);
            var sun = new Vector2(width * 0.28f, 4.2f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var c = Color.Lerp(horizon, top, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 5.8f, p.y)));
                var glow = Mathf.Exp(-(p - sun).sqrMagnitude / 6f);
                c = Color.Lerp(c, new Color32(255, 248, 220, 255), glow * 0.55f);
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            var rng = new System.Random(17);
            for (var k = 0; k < 6; k++)
            {
                var cx = Mathf.Lerp(-width * 0.5f, width * 0.5f, (k + (float)rng.NextDouble() * 0.6f) / 6f);
                var cy = 3.1f + (float)rng.NextDouble() * 2.1f;
                var puffs = new List<(Vector2 c, Vector2 r)>();
                var n = 4 + rng.Next(4);
                var span = 1.2f + (float)rng.NextDouble() * 1.6f;
                for (var i = 0; i < n; i++)
                {
                    var px = cx + Mathf.Lerp(-span, span, i / (float)(n - 1));
                    var rad = 0.35f + (float)rng.NextDouble() * 0.45f * (1f - Mathf.Abs(i / (float)(n - 1) - 0.5f));
                    puffs.Add((new Vector2(px, cy + rad * 0.4f), new Vector2(rad * 1.25f, rad)));
                }
                float Cloud(Vector2 p)
                {
                    var d = Mathf.Max(cy - 0.05f - p.y, -10f); // flat underside
                    var u = 10f;
                    foreach (var (c, rr) in puffs) u = SmoothUnion(u, Ellipse(p, c, rr), 0.25f);
                    return Mathf.Max(u, d);
                }
                r.Shape(Cloud, new Color(1f, 1f, 1f, 1f), new Color32(214, 228, 240, 255), Color.white, 0f, 0.35f,
                    (p, c) => Color.Lerp(c, new Color32(206, 220, 234, 255), Mathf.Clamp01((cy + 0.25f - p.y) / 0.3f) * 0.6f),
                    new Rect(cx - span - 1.2f, cy - 0.2f, span * 2f + 2.4f, 1.8f));
            }
            s = r.ToSprite("Sky");
            cache[key] = s;
            return s;
        }

        // ---- Far bank ----

        /// <summary>
        /// One tile of the far bank (pivot at its bottom-left corner, at <see cref="BankBottom"/>): hazy bamboo, palms,
        /// a hut and bushes on a grassy bank, with their dark reflection on the water below the waterline.
        /// </summary>
        public static Sprite Bank()
        {
            if (bank != null) return bank;
            var r = new ArtRaster(new Rect(0f, BankBottom, BankTile, BankTop - BankBottom), 40f);
            var rng = new System.Random(5);
            Color Hazy(Color c, float k) => Color.Lerp(c, Haze, k);
            var w = FarWater;
            // Distant tree line: soft hazy blobs.
            for (var i = 0; i < 26; i++)
            {
                var x = i / 26f * BankTile + (float)rng.NextDouble() * 0.6f;
                var rad = new Vector2(0.7f + (float)rng.NextDouble() * 0.6f, 0.6f + (float)rng.NextDouble() * 0.5f);
                var c = new Vector2(x, w + 1.2f + (float)rng.NextDouble() * 0.6f);
                foreach (var o in new[] { -BankTile, 0f, BankTile })
                {
                    var cc = c + new Vector2(o, 0);
                    r.Shape(p => Ellipse(p, cc, rad), Hazy(new Color32(96, 138, 92, 255), 0.55f), Hazy(new Color32(86, 124, 84, 255), 0.55f),
                        Hazy(new Color32(130, 170, 110, 255), 0.5f), 0f, 0.3f, null, new Rect(cc - rad, rad * 2f));
                }
            }
            // A thatched hut.
            foreach (var o in new[] { -BankTile, 0f, BankTile })
            {
                var hx = 15.5f + o;
                r.Shape(p => Mathf.Max(Mathf.Abs(p.x - hx) - 0.85f, Mathf.Abs(p.y - (w + 0.62f)) - 0.4f), Hazy(new Color32(176, 142, 100, 255), 0.25f),
                    Hazy(new Color32(140, 108, 74, 255), 0.25f), Hazy(new Color32(200, 170, 128, 255), 0.25f), 0f, 0.12f, null, new Rect(hx - 1f, w, 2f, 1.2f));
                r.Shape(p => Mathf.Max(Mathf.Abs(p.x - (hx + 0.25f)) - 0.17f, Mathf.Abs(p.y - (w + 0.5f)) - 0.27f), Hazy(new Color32(70, 52, 40, 255), 0.25f),
                    Hazy(new Color32(60, 44, 34, 255), 0.25f), Hazy(new Color32(70, 52, 40, 255), 0.25f), 0f, 0.05f, null, new Rect(hx - 0.2f, w, 1f, 1f));
                // Roof: a wide straw triangle with a ridge.
                float Roof(Vector2 p)
                {
                    var q = p - new Vector2(hx, w + 1.0f);
                    var slope = Mathf.Abs(q.x) * 0.62f + q.y - 0.62f;
                    return Mathf.Max(slope, -q.y - 0.05f) * 0.85f;
                }
                r.Shape(Roof, Hazy(new Color32(196, 164, 98, 255), 0.25f), Hazy(new Color32(150, 118, 64, 255), 0.25f),
                    Hazy(new Color32(226, 200, 140, 255), 0.25f), 0f, 0.15f,
                    (p, c) => c * (0.92f + 0.08f * ValueNoise(p.x * 30f, p.y * 4f, 9)), new Rect(hx - 1.3f, w + 0.8f, 2.6f, 1f));
            }
            // Coconut palms: a leaning trunk and a crown of drooping fronds.
            foreach (var px0 in new[] { 4.2f, 11f, 20.4f })
                foreach (var o in new[] { -BankTile, 0f, BankTile })
                {
                    var b = new Vector2(px0 + o, w + 0.25f);
                    var lean = px0 > 10f ? -0.5f : 0.45f;
                    var topP = b + new Vector2(lean, 3.0f);
                    for (var i = 0; i < 20; i++)
                    {
                        var t0 = i / 20f;
                        var t1 = (i + 1) / 20f;
                        Vector2 T(float t) => Vector2.Lerp(b, topP, t) + new Vector2(Mathf.Sin(t * Mathf.PI) * lean * 0.35f, 0);
                        r.Line(T(t0), T(t1), Mathf.Lerp(6.5f, 4.5f, t0), Hazy(new Color32(128, 112, 90, 255), 0.35f), 1f, false);
                    }
                    for (var f = 0; f < 8; f++)
                    {
                        var ang = f / 8f * Mathf.PI * 2f + 0.2f;
                        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.55f + 0.15f);
                        var prev = topP;
                        for (var i = 1; i <= 10; i++)
                        {
                            var t = i / 10f;
                            var pt = topP + dir * (1.15f * t) + Vector2.down * (t * t * 0.75f);
                            r.Line(prev, pt, 3f, Hazy(new Color32(70, 112, 60, 255), 0.35f), 1f, false);
                            var side = new Vector2(-(pt - prev).y, (pt - prev).x).normalized;
                            r.Line(pt, pt + side * 0.16f + Vector2.down * 0.12f, 2f, Hazy(new Color32(84, 128, 68, 255), 0.35f), 1f, false);
                            r.Line(pt, pt - side * 0.16f + Vector2.down * 0.12f, 2f, Hazy(new Color32(84, 128, 68, 255), 0.35f), 1f, false);
                            prev = pt;
                        }
                    }
                }
            // Bamboo hedge clumps: tall arching culms with feathery leaves.
            var clumps = new[] { 1.4f, 7.6f, 13.2f, 18f, 22.6f };
            foreach (var cx in clumps)
            {
                var crng = new System.Random((int)(cx * 100));
                var culms = new List<(Vector2 b, Vector2 m, Vector2 e)>();
                for (var i = 0; i < 14; i++)
                {
                    var bx = cx + ((float)crng.NextDouble() - 0.5f) * 0.9f;
                    var h = 2.3f + (float)crng.NextDouble() * 1.2f;
                    var bend = ((float)crng.NextDouble() - 0.5f) * 2.4f;
                    culms.Add((new Vector2(bx, w + 0.25f), new Vector2(bx + bend * 0.2f, w + h * 0.6f), new Vector2(bx + bend, w + h)));
                }
                foreach (var o in new[] { -BankTile, 0f, BankTile })
                {
                    foreach (var (b0, m, e) in culms)
                    {
                        var off = new Vector2(o, 0);
                        var prev = b0 + off;
                        for (var i = 1; i <= 16; i++)
                        {
                            var t = i / 16f;
                            var pt = (1 - t) * (1 - t) * b0 + 2 * (1 - t) * t * m + t * t * e + off;
                            r.Line(prev, pt, Mathf.Lerp(3.2f, 1.4f, t), Hazy(new Color32(112, 134, 70, 255), 0.3f), 1f, false);
                            prev = pt;
                        }
                    }
                    // Foliage: soft masses of leaves hanging from the upper culms, then single leaves over them.
                    var mrng = new System.Random((int)(cx * 53));
                    foreach (var (b0, m, e) in culms)
                        for (var k = 0; k < 3; k++)
                        {
                            var t = 0.55f + k * 0.18f;
                            var pt = (1 - t) * (1 - t) * b0 + 2 * (1 - t) * t * m + t * t * e + new Vector2(o, -0.12f);
                            var rad = new Vector2(0.32f + (float)mrng.NextDouble() * 0.2f, 0.2f + (float)mrng.NextDouble() * 0.1f);
                            var tone = (float)mrng.NextDouble();
                            r.Shape(p => Ellipse(p, pt, rad), Hazy(Color.Lerp(new Color32(74, 118, 58, 255), new Color32(104, 148, 72, 255), tone), 0.32f),
                                Hazy(new Color32(62, 100, 52, 255), 0.32f), Hazy(new Color32(140, 176, 96, 255), 0.3f), 0f, 0.12f, null, new Rect(pt - rad, rad * 2f));
                        }
                    var lrng = new System.Random((int)(cx * 77));
                    foreach (var (b0, m, e) in culms)
                        for (var k = 0; k < 16; k++)
                        {
                            var t = 0.45f + (float)lrng.NextDouble() * 0.55f;
                            var pt = (1 - t) * (1 - t) * b0 + 2 * (1 - t) * t * m + t * t * e + new Vector2(o, 0);
                            pt += new Vector2(((float)lrng.NextDouble() - 0.5f) * 0.5f, ((float)lrng.NextDouble() - 0.5f) * 0.4f);
                            var tone = (float)lrng.NextDouble();
                            var leaf = Hazy(Color.Lerp(new Color32(66, 112, 56, 255), new Color32(120, 160, 80, 255), tone), 0.3f);
                            var a = ((float)lrng.NextDouble() - 0.5f) * 1.6f;
                            r.Line(pt, pt + new Vector2(Mathf.Cos(a), Mathf.Sin(a) - 0.4f) * 0.22f, 3.4f, leaf, 0.95f, false);
                        }
                }
            }
            // Banana plants and bushes along the bank.
            var brng = new System.Random(23);
            for (var i = 0; i < 30; i++)
            {
                var x = i / 30f * BankTile + (float)brng.NextDouble() * 0.5f;
                var rad = new Vector2(0.35f + (float)brng.NextDouble() * 0.35f, 0.25f + (float)brng.NextDouble() * 0.2f);
                var tone = (float)brng.NextDouble();
                foreach (var o in new[] { -BankTile, 0f, BankTile })
                {
                    var c = new Vector2(x + o, w + 0.35f);
                    r.Shape(p => Ellipse(p, c, rad), Hazy(Color.Lerp(new Color32(70, 118, 58, 255), new Color32(110, 150, 70, 255), tone), 0.2f),
                        Hazy(new Color32(56, 96, 50, 255), 0.2f), Hazy(new Color32(150, 186, 100, 255), 0.2f), 0f, 0.15f, null, new Rect(c - rad, rad * 2f));
                }
            }
            // The grassy bank and its muddy edge at the waterline.
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var edge = w + 0.12f + 0.05f * ValueNoise(p.x * 2f, 0.5f, 4);
                var i = y * r.w + x;
                if (p.y >= w && p.y < edge + 0.2f)
                {
                    var c = p.y < edge ? new Color32(126, 104, 70, 255) : new Color32(104, 144, 74, 255);
                    var a = p.y < edge ? 1f : Mathf.Clamp01((edge + 0.2f - p.y) / 0.08f);
                    var col = Hazy((Color)c * (0.9f + 0.15f * ValueNoise(p.x * 9f, p.y * 9f, 2)), 0.15f);
                    r.Blend(x, y, col, a);
                }
                else if (p.y < w)
                {
                    // Reflection: the dark bank mirrored in the water, broken into horizontal streaks.
                    var k = 1f - (w - p.y) / 0.6f;
                    var streak = 0.6f + 0.4f * ValueNoise(p.x * 3f, p.y * 40f, 6);
                    r.px[i] = new Color(0.22f, 0.34f, 0.26f, Mathf.Clamp01(k * 0.55f * streak));
                }
            }
            bank = r.ToCornerSprite("Bank");
            return bank;
        }

        // ---- Water ----

        private static Color WaterColor(Vector2 p)
        {
            var t = Mathf.InverseLerp(WaterBottom, FarWater, p.y);
            var c = Color.Lerp(DeepWater, FarWaterColor, Mathf.Pow(t, 1.4f));
            c *= 0.97f + 0.06f * ValueNoise(3.7f, p.y * 6f, 31); // varies with depth only, so tiles join seamlessly
            c.a = 1f;
            return c;
        }

        private static void EnsureDashes()
        {
            if (dashes != null) return;
            dashes = new List<(Vector2, float, float)>();
            var rng = new System.Random(41);
            for (var i = 0; i < 170; i++)
            {
                var t = Mathf.Pow((float)rng.NextDouble(), 0.7f); // more glints toward the far side
                var y = Mathf.Lerp(WaterBottom + 0.4f, FarWater - 0.05f, t);
                var x = (float)rng.NextDouble() * WaterTile;
                var len = Mathf.Lerp(1.1f, 0.3f, t) * (0.6f + (float)rng.NextDouble() * 0.6f);
                dashes.Add((new Vector2(x, y), len, t));
            }
        }

        /// <summary>Paints river water over a raster in world space (seamless every <see cref="WaterTile"/>).</summary>
        private static void PaintWater(ArtRaster r, float alpha)
        {
            EnsureDashes();
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var c = WaterColor(r.World(x, y));
                c.a = alpha;
                r.px[y * r.w + x] = c;
            }
            foreach (var (a, len, t) in dashes)
                foreach (var o in new[] { -WaterTile, 0f, WaterTile })
                {
                    var p = a + new Vector2(o, 0);
                    var width = Mathf.Lerp(3.2f, 1.4f, t);
                    // Ripple: a dark trough under a light crest.
                    r.Line(p + new Vector2(0, -0.035f), p + new Vector2(len, -0.035f), width, new Color32(30, 74, 80, 255), 0.25f, true);
                    r.Line(p, p + new Vector2(len, 0), width, new Color32(210, 236, 232, 255), Mathf.Lerp(0.3f, 0.55f, t), true);
                }
        }

        /// <summary>One tile of river water (pivot at its bottom-left corner, at x = 0, y = <see cref="WaterBottom"/>).</summary>
        public static Sprite Water()
        {
            if (water != null) return water;
            var r = new ArtRaster(new Rect(0f, WaterBottom, WaterTile, FarWater - WaterBottom), 40f);
            PaintWater(r, 1f);
            water = r.ToCornerSprite("Water");
            return water;
        }

        /// <summary>
        /// The water in front of a swimmer's row: the same water as the background, mostly opaque, from the row's surface
        /// down <paramref name="depth"/>, with a light surface line. Drawn over the swimmer, it hides what is under water.
        /// Pivot at the bottom-left corner (x = 0, y = surface - depth).
        /// </summary>
        public static Sprite Overlay(float surface, float depth)
        {
            var key = $"ov{surface:0.00}_{depth:0.00}";
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var r = new ArtRaster(new Rect(0f, surface - depth, WaterTile, depth), 40f);
            PaintWater(r, 0.84f);
            // Soft top edge.
            for (var y = 0; y < r.h; y++)
            {
                var fromTop = (r.h - 1 - y) / r.ppu;
                for (var x = 0; x < r.w; x++)
                {
                    var i = y * r.w + x;
                    var c = r.px[i];
                    // A soft top edge, no line: the water only shows where it covers a swimmer.
                    if (fromTop < 0.06f) c.a *= fromTop / 0.06f;
                    r.px[i] = c;
                }
            }
            s = r.ToCornerSprite("WaterFront");
            cache[key] = s;
            return s;
        }

        // ---- Swimmers ----

        public struct SwimmerSprites
        {
            public Sprite body, arm, leg;
        }

        /// <summary>
        /// A swimming child seen from the side, facing right, in parts: the body (head, torso, swimwear; pivot at the
        /// origin), an arm (pivot at the shoulder) and a leg (pivot at the hip). <paramref name="girl"/> gives a
        /// one-piece swimsuit and a ponytail.
        /// </summary>
        public static SwimmerSprites Swimmer(Color wear, bool girl)
        {
            var key = $"sw{ColorUtility.ToHtmlStringRGB(wear)}{girl}";
            if (cache.TryGetValue(key + "b", out var cachedBody) && cachedBody != null)
                return new SwimmerSprites { body = cachedBody, arm = cache[key + "a"], leg = cache[key + "l"] };

            var head = new Vector2(0.17f, 0.07f);
            var headR = new Vector2(0.27f, 0.25f);
            float BodyShape(Vector2 p)
            {
                var torso = Capsule(p, new Vector2(-1.05f, -0.03f), new Vector2(-0.22f, 0.0f), 0.2f);
                var neck = Capsule(p, new Vector2(-0.22f, 0.0f), new Vector2(0.02f, 0.04f), 0.13f);
                return SmoothUnion(SmoothUnion(torso, neck, 0.08f), Ellipse(p, head, headR), 0.08f);
            }
            var body = new ArtRaster(new Rect(-1.4f, -0.4f, 2.1f, 0.85f), 170f);
            body.Shape(BodyShape, Skin, SkinShade, SkinLight, 0.025f, 0.12f);
            var wearShade = Color.Lerp(wear, Color.black, 0.3f);
            var wearLight = Color.Lerp(wear, Color.white, 0.35f);
            if (girl)
            {
                // One-piece swimsuit over the torso, a thin strap over the shoulder.
                body.Shape(p => Mathf.Max(BodyShape(p) - 0.008f, p.x + 0.3f), wear, wearShade, wearLight, 0.022f, 0.08f);
                body.Shape(p => Capsule(p, new Vector2(-0.3f, 0.12f), new Vector2(-0.14f, 0.06f), 0.025f), wear, wearShade, wearLight, 0.015f, 0.02f);
            }
            else body.Shape(p => Mathf.Max(BodyShape(p) - 0.008f, Mathf.Abs(p.x + 1.0f) - 0.17f), wear, wearShade, wearLight, 0.022f, 0.08f);
            // Ear, then wet hair over the top and back of the head.
            body.Shape(p => Ellipse(p, head + new Vector2(-0.05f, -0.02f), new Vector2(0.05f, 0.07f)), Skin, SkinShade, SkinLight, 0.018f, 0.03f);
            var hairColor = new Color32(32, 26, 24, 255);
            body.Shape(p => Mathf.Max(Ellipse(p, head, headR + new Vector2(0.02f, 0.02f)), 0.04f + 0.45f * (p.x - head.x) - (p.y - head.y) + 0.07f),
                hairColor, new Color32(18, 14, 13, 255), new Color32(120, 110, 104, 255), 0.022f, 0.06f);
            if (girl)
                body.Shape(p => Capsule(p, head + new Vector2(-0.2f, 0.12f), head + new Vector2(-0.48f, 0.08f), 0.065f),
                    hairColor, new Color32(18, 14, 13, 255), new Color32(120, 110, 104, 255), 0.022f, 0.04f);
            // Face: a closed eye squeezed against the water, a little nose and cheek.
            body.Line(head + new Vector2(0.1f, 0.03f), head + new Vector2(0.17f, 0.02f), 3f, new Color32(60, 38, 30, 255), 1f, true);
            body.Shape(p => Ellipse(p, head + new Vector2(0.26f, -0.02f), new Vector2(0.04f, 0.035f)), Skin, SkinShade, SkinLight, 0.016f, 0.02f);
            body.Shape(p => Ellipse(p, head + new Vector2(0.12f, -0.08f), new Vector2(0.05f, 0.03f)), new Color32(240, 160, 140, 255),
                new Color32(240, 160, 140, 255), new Color32(240, 160, 140, 255), 0f, 0.02f);
            var bodySprite = body.ToSprite("SwimmerBody", Vector2.zero);

            var arm = new ArtRaster(new Rect(-0.14f, -0.14f, 1.08f, 0.28f), 170f);
            arm.Shape(p => SmoothUnion(SmoothUnion(Capsule(p, Vector2.zero, new Vector2(0.42f, 0f), 0.085f),
                    Capsule(p, new Vector2(0.42f, 0f), new Vector2(0.74f, 0.02f), 0.072f), 0.04f),
                Ellipse(p, new Vector2(0.83f, 0.02f), new Vector2(0.085f, 0.065f)), 0.03f), Skin, SkinShade, SkinLight, 0.022f, 0.06f);
            var armSprite = arm.ToSprite("SwimmerArm", Vector2.zero);

            var leg = new ArtRaster(new Rect(-1.2f, -0.15f, 1.34f, 0.3f), 170f);
            leg.Shape(p => SmoothUnion(SmoothUnion(Capsule(p, Vector2.zero, new Vector2(-0.5f, -0.02f), 0.1f),
                    Capsule(p, new Vector2(-0.5f, -0.02f), new Vector2(-0.95f, -0.03f), 0.08f), 0.04f),
                Ellipse(p, new Vector2(-1.05f, -0.01f), new Vector2(0.1f, 0.055f)), 0.03f), Skin, SkinShade, SkinLight, 0.022f, 0.06f);
            var legSprite = leg.ToSprite("SwimmerLeg", Vector2.zero);

            cache[key + "b"] = bodySprite;
            cache[key + "a"] = armSprite;
            cache[key + "l"] = legSprite;
            return new SwimmerSprites { body = bodySprite, arm = armSprite, leg = legSprite };
        }

        // ---- Effects and props ----

        /// <summary>A splash: white foam with droplets thrown up (pivot at the water line).</summary>
        public static Sprite Splash()
        {
            if (splash != null) return splash;
            var r = new ArtRaster(new Rect(-0.6f, -0.15f, 1.2f, 0.9f), 110f);
            var rng = new System.Random(3);
            r.OutlineColor = new Color32(120, 170, 180, 255);
            float Foam(Vector2 p)
            {
                var d = 10f;
                for (var i = 0; i < 5; i++)
                {
                    var c = new Vector2(-0.36f + i * 0.18f, 0.02f + Mathf.Sin(i * 1.7f) * 0.03f);
                    d = SmoothUnion(d, Ellipse(p, c, new Vector2(0.13f, 0.1f)), 0.06f);
                }
                return d;
            }
            r.Shape(Foam, Color.white, new Color32(200, 228, 232, 255), Color.white, 0.02f, 0.06f);
            for (var i = 0; i < 12; i++)
            {
                var ang = Mathf.Lerp(0.35f, Mathf.PI - 0.35f, (float)rng.NextDouble());
                var dist = 0.25f + (float)rng.NextDouble() * 0.4f;
                var c = new Vector2(Mathf.Cos(ang) * dist * 0.9f, 0.08f + Mathf.Sin(ang) * dist);
                var rad = 0.025f + (float)rng.NextDouble() * 0.035f;
                r.Shape(p => Ellipse(p, c, new Vector2(rad, rad * 1.25f)), Color.white, new Color32(196, 226, 232, 255), Color.white, 0.012f, 0.02f);
            }
            splash = r.ToSprite("Splash", Vector2.zero);
            return splash;
        }

        /// <summary>An air bubble: a clear ring with a glint.</summary>
        public static Sprite Bubble()
        {
            if (bubble != null) return bubble;
            var r = new ArtRaster(new Rect(-0.12f, -0.12f, 0.24f, 0.24f), 200f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var d = p.magnitude;
                var ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.085f) * r.ppu / 3f);
                var inside = d < 0.085f ? 0.18f : 0f;
                var glint = Mathf.Clamp01(1f - (p - new Vector2(-0.03f, 0.035f)).magnitude * r.ppu / 4f);
                r.px[y * r.w + x] = new Color(0.9f, 0.97f, 1f, Mathf.Max(ring * 0.9f, Mathf.Max(inside, glint)));
            }
            bubble = r.ToSprite("Bubble", Vector2.zero);
            return bubble;
        }

        /// <summary>A wake ring on the surface, seen at a slant (an ellipse outline).</summary>
        public static Sprite Ring()
        {
            if (ring != null) return ring;
            var r = new ArtRaster(new Rect(-0.55f, -0.18f, 1.1f, 0.36f), 120f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var d = Ellipse(p, Vector2.zero, new Vector2(0.5f, 0.14f)) * r.ppu;
                var a = Mathf.Clamp01(1.5f - Mathf.Abs(d));
                r.px[y * r.w + x] = new Color(0.9f, 0.97f, 0.96f, a * (p.y > 0 ? 0.55f : 0.9f));
            }
            ring = r.ToSprite("Ring", Vector2.zero);
            return ring;
        }

        private static Sprite waterline;

        /// <summary>
        /// The water lapping around a swimmer: a short bright line with a little foam, fading at both ends (it travels
        /// with the swimmer, so the river itself has no lines).
        /// </summary>
        public static Sprite Waterline()
        {
            if (waterline != null) return waterline;
            var r = new ArtRaster(new Rect(-1.6f, -0.1f, 3.2f, 0.2f), 100f);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var along = 1f - Mathf.Pow(Mathf.Abs(p.x) / 1.6f, 2f);
                var line = Mathf.Clamp01(1f - Mathf.Abs(p.y) * r.ppu / 2.2f);
                var foam = Mathf.Clamp01(1f - Mathf.Abs(p.y + 0.02f) * r.ppu / 5f) * ValueNoise(p.x * 9f, 0.5f, 12);
                r.px[y * r.w + x] = new Color(0.9f, 0.97f, 0.96f, Mathf.Clamp01(Mathf.Max(line * 0.9f, foam * 0.55f) * along));
            }
            waterline = r.ToSprite("Waterline", Vector2.zero);
            return waterline;
        }

        /// <summary>A bamboo stake with a red scarf tied near the top, marking the finish (pivot at the water line).</summary>
        public static Sprite Stake()
        {
            if (stake != null) return stake;
            var r = new ArtRaster(new Rect(-0.3f, -1.4f, 1.1f, 3.0f), 110f);
            var bamboo = new Color32(196, 174, 100, 255);
            r.Shape(p => Capsule(p, new Vector2(0, -1.35f), new Vector2(0, 1.4f), 0.075f), bamboo, new Color32(150, 128, 66, 255),
                new Color32(232, 214, 150, 255), 0.022f, 0.05f);
            for (var y = -1.1f; y < 1.4f; y += 0.42f)
                r.Line(new Vector2(-0.07f, y), new Vector2(0.07f, y + 0.01f), 3f, new Color32(130, 108, 56, 255), 1f, true);
            // The scarf: knotted at the stake, two tails waving out to the right.
            var red = new Color32(214, 48, 44, 255);
            var redShade = new Color32(150, 26, 30, 255);
            var redLight = new Color32(246, 120, 104, 255);
            float Tail(Vector2 p, float lift, float len)
            {
                var d = 10f;
                for (var i = 0; i < 8; i++)
                {
                    var t0 = i / 8f;
                    var t1 = (i + 1) / 8f;
                    Vector2 T(float t) => new Vector2(0.06f + t * len, 1.12f + lift * t + Mathf.Sin(t * 5f) * 0.05f);
                    d = Mathf.Min(d, Capsule(p, T(t0), T(t1), Mathf.Lerp(0.07f, 0.035f, t0)));
                }
                return d;
            }
            r.Shape(p => Tail(p, -0.25f, 0.62f), red, redShade, redLight, 0.02f, 0.04f);
            r.Shape(p => Tail(p, 0.05f, 0.7f), red, redShade, redLight, 0.02f, 0.04f);
            r.Shape(p => Ellipse(p, new Vector2(0.03f, 1.13f), new Vector2(0.11f, 0.08f)), red, redShade, redLight, 0.02f, 0.04f);
            stake = r.ToSprite("Stake", Vector2.zero);
            return stake;
        }
    }
}
