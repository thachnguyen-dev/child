using System;
using ChildGame.UI;
using UnityEngine;
using static ChildGame.UI.ArtShapes;

namespace ChildGame.MiniGames.NhoToc
{
    /// <summary>
    /// Official art for Nhổ tóc bạc (owner 2026-10-08: "only needs a visual upgrade"), generated in code in the same style
    /// as the hands (<see cref="UI.HandArt"/>): soft shading, a warm dark outline. Everything is in world units and
    /// rasterised to textures once per session:
    /// <list type="bullet">
    /// <item>a woven sedge mat (chiếu) with dyed bands under grandma;</item>
    /// <item>grandma seen from behind and above: shoulders in an áo bà ba, the nape of the neck, ears;</item>
    /// <item>the hair: thousands of anti-aliased strands flowing from the parting back to the bun, with a sheen band
    /// toward the light and shadow at the edges; a new arrangement is a new seed;</item>
    /// <item>the bun, wound round, with a hairpin;</item>
    /// <item>grey hairs: tapered silver strands (with a root bulb once plucked).</item>
    /// </list>
    /// </summary>
    public static class NhoTocArt
    {
        public static readonly Color Outline = new Color32(70, 42, 32, 255);
        private static readonly Color Skin = new Color32(238, 196, 160, 255);
        private static readonly Color SkinShade = new Color32(206, 150, 118, 255);
        private static readonly Color Shirt = new Color32(116, 76, 64, 255);      // áo bà ba, faded brown
        private static readonly Color ShirtShade = new Color32(78, 48, 42, 255);
        private static readonly Color ShirtLight = new Color32(150, 104, 86, 255);


        // ---- The mat ----

        /// <summary>
        /// A woven sedge mat: flat horizontal sedge strips, each slightly rounded, with fibre streaks, crossed by jute
        /// warp threads; two dyed bands of red and green strips with a diamond motif run across it.
        /// </summary>
        public static Sprite Mat(Rect world, int seed)
        {
            var r = new ArtRaster(world, 48f);
            const float strip = 0.12f;  // strip height (world)
            const float warp = 1.15f;  // spacing of the warp threads
            var straw = new Color32(222, 196, 140, 255);
            var strawDark = new Color32(190, 160, 104, 255);
            var red = new Color32(186, 64, 52, 255);
            var green = new Color32(70, 128, 84, 255);
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var row = Mathf.FloorToInt(p.y / strip);
                var v = p.y / strip - row; // 0..1 across the strip
                var col = Mathf.FloorToInt(p.x / warp);
                var u = p.x / warp - col;
                // Each strip has its own tone; fibres streak along it.
                var tone = 0.6f * Hash(row, 7, seed) + 0.4f * ValueNoise(p.x * 0.8f, row * 0.37f, seed + 11); // drifts along the strip
                var c = Color.Lerp(strawDark, straw, 0.45f + 0.55f * tone);
                var fibre = ValueNoise(p.x * 3f, p.y * 60f, seed + row);
                c *= 0.9f + 0.16f * fibre;
                // Dyed bands: two bands of strips across the mat with a diamond motif.
                var bandStart = p.y >= 3f && p.y < 3.8f ? 3f : p.y >= -3.2f && p.y < -2.4f ? -3.2f : float.NaN;
                if (!float.IsNaN(bandStart))
                {
                    var bandRow = Mathf.FloorToInt((p.y - bandStart) / strip);
                    // Red diamonds on green, stepped strip by strip like a woven motif.
                    var cell = Mathf.FloorToInt(p.x / 0.2f);
                    var diamond = Mathf.Abs(Mathf.Repeat(cell, 8f) - 4f) + Mathf.Abs(bandRow - 3f) <= 3f;
                    c = Color.Lerp(c, diamond ? red : green, 0.82f);
                    c *= 0.9f + 0.16f * fibre;
                }
                // Rounded strip: darker at its edges, a thin gap line between strips.
                var across = Mathf.Sin(v * Mathf.PI);
                c *= 0.78f + 0.24f * Mathf.Sqrt(across);
                if (v < 0.06f) c *= 0.72f;
                // Jute warp threads run up the mat: a fine darker line, the strips bulging a little between them.
                var thread = Mathf.Abs(u - 0.5f) * warp;
                if (thread < 0.018f) c = Color.Lerp(c, new Color32(140, 108, 70, 255), 0.7f * Mathf.Clamp01((0.018f - thread) / 0.008f));
                else c *= 0.95f + 0.05f * Mathf.Sin(u * Mathf.PI * 2f + Mathf.PI * 0.5f);
                c.a = 1f;
                r.px[y * r.w + x] = c;
            }
            return r.ToSprite("Mat");
        }

        // ---- Grandma ----

        /// <summary>Shoulders and upper back in an áo bà ba, seen from behind and above, with the nape of the neck.</summary>
        public static Sprite Body(Rect world, Vector2 headCenter, Vector2 headRadius)
        {
            var r = new ArtRaster(world, 64f);
            var neck = new Vector2(headCenter.x, headCenter.y - headRadius.y + 0.15f);
            float Back(Vector2 p)
            {
                var torso = Ellipse(p, new Vector2(0, -6.2f), new Vector2(5.4f, 3.3f));
                var left = Ellipse(p, new Vector2(-5.1f, -6.4f), new Vector2(1.45f, 2.6f));
                var right = Ellipse(p, new Vector2(5.1f, -6.4f), new Vector2(1.45f, 2.6f));
                return SmoothUnion(SmoothUnion(torso, left, 0.6f), right, 0.6f);
            }
            // Folds: soft darker valleys from the neck out over the shoulders, and down the back.
            Color Folds(Vector2 p, Color c)
            {
                var fold = 0f;
                foreach (var s in new[] { -1f, 1f })
                {
                    var d = Capsule(p, new Vector2(s * 1.6f, -3.6f), new Vector2(s * 4.2f, -5.2f), 0.02f);
                    fold += Mathf.Exp(-d * d * 30f) * 0.5f;
                    var d2 = Capsule(p, new Vector2(s * 0.9f, -4.4f), new Vector2(s * 1.4f, -7.5f), 0.02f);
                    fold += Mathf.Exp(-d2 * d2 * 40f) * 0.35f;
                }
                var weave = 0.97f + 0.03f * ValueNoise(p.x * 14f, p.y * 14f, 5);
                return Color.Lerp(c, ShirtShade, Mathf.Clamp01(fold) * 0.45f) * weave;
            }
            r.Shape(Back, Shirt, ShirtShade, ShirtLight, 0.07f, 0.9f, Folds);
            // Collar: a round neckline, slightly darker cloth with a seam.
            float Collar(Vector2 p) => Ellipse(p, neck + new Vector2(0, -0.1f), new Vector2(1.55f, 0.95f));
            r.Shape(Collar, Color.Lerp(Shirt, ShirtShade, 0.35f), ShirtShade, ShirtLight, 0.06f, 0.25f);
            // The nape of the neck inside the collar.
            float Nape(Vector2 p) => Ellipse(p, neck + new Vector2(0, -0.05f), new Vector2(1.25f, 0.72f));
            r.Shape(Nape, Skin, SkinShade, Color.Lerp(Skin, Color.white, 0.4f), 0.06f, 0.35f);
            return r.ToSprite("Body");
        }

        /// <summary>One ear seen from behind and above (the right ear; mirror it for the left).</summary>
        public static Sprite Ear()
        {
            var r = new ArtRaster(new Rect(-0.5f, -0.75f, 1f, 1.5f), 96f);
            float Outer(Vector2 p) => SmoothUnion(Ellipse(p, new Vector2(0.05f, 0.12f), new Vector2(0.32f, 0.5f)),
                Ellipse(p, new Vector2(0.02f, -0.38f), new Vector2(0.2f, 0.24f)), 0.12f);
            r.Shape(Outer, Skin, SkinShade, Color.Lerp(Skin, Color.white, 0.4f), 0.05f, 0.2f);
            // The fold of the ear: a darker inner curve.
            for (var t = 0f; t < 1f; t += 0.02f)
            {
                var a = Mathf.Lerp(-0.6f, 1.9f, t);
                var b = Mathf.Lerp(-0.6f, 1.9f, t + 0.02f);
                r.Line(new Vector2(0.06f, 0.12f) + new Vector2(Mathf.Cos(a) * 0.17f, Mathf.Sin(a) * 0.3f),
                    new Vector2(0.06f, 0.12f) + new Vector2(Mathf.Cos(b) * 0.17f, Mathf.Sin(b) * 0.3f), 3.2f, SkinShade, 0.9f, true);
            }
            return r.ToSprite("Ear");
        }

        /// <summary>
        /// The hair in head space (the sprite covers the head ellipse). Pixels only, so it can run on a worker thread;
        /// <see cref="HairSprite"/> turns the result into a sprite on the main thread.
        /// </summary>
        public static Color[] HairPixels(int size, int seed, out int width, out int height)
        {
            var r = new ArtRaster(new Rect(-1.03f, -1.03f, 2.06f, 2.06f), size / 2.06f);
            width = r.w;
            height = r.h;
            var rng = new System.Random(seed);
            var dark = new Color32(26, 21, 20, 255);
            var sheenColor = new Color32(128, 116, 112, 255);
            // Base: dark, deeper toward the edge (the head curves away), a little lift toward the light.
            float Head(Vector2 p) => p.magnitude - 1f;
            Color Base(Vector2 p, Color c)
            {
                var k = p.magnitude;
                var ao = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.7f) / 0.3f));
                var col = Color.Lerp(dark, new Color32(12, 10, 10, 255), ao * 0.8f);
                return Color.Lerp(col, new Color32(48, 40, 38, 255), Mathf.Clamp01(Vector2.Dot(p, new Vector2(-0.6f, 0.8f))) * 0.4f * (1f - ao));
            }
            r.Shape(Head, dark, dark, dark, 0f, 0.1f, Base);
            // Parting: a line of scalp from the crown forward, softened, then hairs lie over its edges.
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                if (p.y < -0.12f || p.sqrMagnitude > 0.97f) continue;
                var line = Mathf.Abs(p.x + Mathf.Sin(p.y * 3f + seed) * 0.012f);
                var widthAt = 0.013f + 0.008f * Mathf.Clamp01(p.y);
                var a = Mathf.Clamp01((widthAt - line) * r.ppu + 0.5f) * Mathf.Clamp01((p.y + 0.12f) * 10f);
                if (a <= 0f) continue;
                var i = y * r.w + x;
                var scalp = Color.Lerp(SkinShade, Skin, 0.6f);
                r.px[i] = new Color(Mathf.Lerp(r.px[i].r, scalp.r, a), Mathf.Lerp(r.px[i].g, scalp.g, a), Mathf.Lerp(r.px[i].b, scalp.b, a), r.px[i].a);
            }
            // Strands: follow the flow field; their colour picks up a sheen where they cross the light band.
            var strands = (int)(size * size / 26f);
            var step = 2.4f / r.ppu;
            for (var s = 0; s < strands; s++)
            {
                var p = new Vector2((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
                if (p.sqrMagnitude > 0.98f) continue;
                // Near the parting, strands start right at its edge so they cover it neatly.
                if (p.y > -0.1f && Mathf.Abs(p.x) < 0.03f) p.x = Mathf.Sign(p.x == 0 ? 1 : p.x) * 0.018f;
                var tone = (float)rng.NextDouble();
                var strandWidth = 0.9f + (float)rng.NextDouble() * 0.9f;
                var steps = 10 + rng.Next(18);
                var alpha = 0.55f + 0.4f * (float)rng.NextDouble();
                for (var k = 0; k < steps; k++)
                {
                    var dir = Flow(p);
                    var next = p + dir * step;
                    if (next.sqrMagnitude > 0.985f) break;
                    // Sheen: brightest on a ring around the crown on the side facing the light, for strands running
                    // across that light (anisotropic, like real hair).
                    var ring = Mathf.Exp(-Mathf.Pow((p.magnitude - 0.5f) / 0.16f, 2f));
                    var facing = Mathf.Clamp01(0.35f + Vector2.Dot(p.normalized, new Vector2(-0.6f, 0.8f)) * 0.65f);
                    var across = Mathf.Abs(Vector2.Dot(dir, new Vector2(0.8f, 0.6f)));
                    var sheen = ring * facing * (0.4f + 0.6f * across);
                    var edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p.magnitude - 0.78f) / 0.2f));
                    var c = Color.Lerp(Color.Lerp(new Color32(20, 16, 15, 255), new Color32(58, 48, 44, 255), tone), sheenColor, sheen * 0.75f);
                    c = Color.Lerp(c, new Color32(8, 7, 7, 255), edge * 0.6f);
                    r.Line(p, next, strandWidth, c, alpha, true);
                    p = next;
                }
            }
            // A dark outline around the head, in the art's style.
            for (var y = 0; y < r.h; y++)
            for (var x = 0; x < r.w; x++)
            {
                var p = r.World(x, y);
                var d = (p.magnitude - 1f) * r.ppu;
                var i = y * r.w + x;
                if (d < -4f || r.px[i].a <= 0f) continue;
                var k = Mathf.Clamp01((d + 4f) / 2f);
                r.px[i] = new Color(Mathf.Lerp(r.px[i].r, Outline.r * 0.5f, k), Mathf.Lerp(r.px[i].g, Outline.g * 0.5f, k), Mathf.Lerp(r.px[i].b, Outline.b * 0.5f, k), r.px[i].a);
            }
            return r.px;
        }

        public static Sprite HairSprite(Color[] px, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Hair" };
            tex.SetPixels(px);
            tex.Apply(false, true);
            // 2.06 head-space units across: scale the sprite by the head radius to place it.
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width / 2.06f);
        }

        /// <summary>Hair direction in head space (-1..1): away from the parting, sweeping back toward the bun.</summary>
        public static Vector2 Flow(Vector2 p)
        {
            var away = new Vector2(Mathf.Sign(p.x == 0 ? 1 : p.x), 0.15f);
            var back = new Vector2(-p.x * 0.6f, -1f);
            var t = Mathf.Clamp01((0.6f - p.y) / 1.4f);
            return Vector2.Lerp(away, back, t).normalized;
        }

        /// <summary>The bun: hair wound round and pinned with a hairpin (trâm) topped by a small flower.</summary>
        public static Sprite Bun(Vector2 size)
        {
            var r = new ArtRaster(new Rect(-size.x * 0.75f, -size.y * 0.62f, size.x * 1.5f, size.y * 1.24f), 110f);
            var rad = size * 0.5f;
            float Shape(Vector2 p) => Ellipse(p, Vector2.zero, rad);
            Color Wound(Vector2 p, Color c)
            {
                // Hair wound round the bun: fine arcs following the circle, a sheen on the upper left.
                var q = p / rad;
                var ang = Mathf.Atan2(q.y, q.x);
                var k = q.magnitude;
                var strand = 0.5f + 0.5f * Mathf.Sin(k * 46f + ang * 3f + ValueNoise(ang * 3f, k * 6f, 3) * 4f);
                var sheen = Mathf.Exp(-Mathf.Pow((k - 0.55f) / 0.18f, 2f)) * Mathf.Clamp01(Vector2.Dot(q.normalized, new Vector2(-0.6f, 0.8f)));
                var col = Color.Lerp(new Color32(18, 15, 14, 255), new Color32(46, 39, 36, 255), strand);
                col = Color.Lerp(col, new Color32(140, 128, 122, 255), sheen * 0.55f);
                return Color.Lerp(col, new Color32(6, 5, 5, 255), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.75f) / 0.25f)) * 0.7f);
            }
            r.Shape(Shape, Color.black, Color.black, Color.black, 0.06f, 0.2f, Wound);
            // Hairpin: a long dark-wood stick across the bun, a little brass flower at one end.
            var a = new Vector2(-rad.x * 1.25f, -rad.y * 0.35f);
            var b = new Vector2(rad.x * 1.3f, rad.y * 0.45f);
            r.Shape(p => Capsule(p, a, b, 0.07f), new Color32(132, 86, 52, 255), new Color32(88, 54, 32, 255), new Color32(196, 150, 104, 255), 0.04f, 0.06f);
            var flower = b + (b - a).normalized * 0.05f;
            for (var i = 0; i < 5; i++)
            {
                var ang = i * Mathf.PI * 2f / 5f + 0.3f;
                var c = flower + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.12f;
                r.Shape(p => Ellipse(p, c, new Vector2(0.1f, 0.1f)), new Color32(226, 184, 84, 255), new Color32(172, 122, 46, 255), new Color32(255, 236, 170, 255), 0.035f, 0.05f);
            }
            r.Shape(p => Ellipse(p, flower, new Vector2(0.07f, 0.07f)), new Color32(200, 70, 60, 255), new Color32(150, 40, 36, 255), new Color32(250, 150, 130, 255), 0.03f, 0.04f);
            return r.ToSprite("Bun");
        }

        /// <summary>
        /// A grey hair, root on the left: a tapered silver strand with a slight wave, a thin darker edge so it reads on
        /// black hair, and a highlight. <paramref name="bulb"/> adds the little white root of a plucked hair.
        /// </summary>
        public static Sprite GreyHair(float length, bool bulb)
        {
            var r = new ArtRaster(new Rect(-0.08f, -0.1f, length + 0.14f, 0.2f), 220f);
            Vector2 At(float t) => new Vector2(t * length, Mathf.Sin(t * Mathf.PI * 1.3f) * 0.025f);
            var segs = 40;
            // Dark edge first, then the silver core, then a highlight along the upper side.
            for (var pass = 0; pass < 3; pass++)
                for (var i = 0; i < segs; i++)
                {
                    var t0 = i / (float)segs;
                    var t1 = (i + 1) / (float)segs;
                    var taper = Mathf.Lerp(1f, 0.25f, t0 * t0);
                    var width = (pass == 0 ? 13f : pass == 1 ? 9f : 2.6f) * taper;
                    var color = pass == 0 ? new Color32(90, 88, 86, 255) : pass == 1 ? new Color32(226, 226, 222, 255) : new Color32(255, 255, 255, 255);
                    var offset = pass == 2 ? new Vector2(0, 0.004f) : Vector2.zero;
                    r.Line(At(t0) + offset, At(t1) + offset, width, color, pass == 2 ? 0.8f : 1f, pass > 0);
                }
            if (bulb)
                r.Shape(p => Ellipse(p, At(0f) + new Vector2(-0.01f, 0f), new Vector2(0.035f, 0.022f)), new Color32(250, 246, 236, 255), new Color32(214, 206, 192, 255), Color.white, 0.008f, 0.012f);
            var s = r.ToSprite(bulb ? "GreyHairPlucked" : "GreyHair");
            return s;
        }

        /// <summary>Where the root end of <see cref="GreyHair"/> is, from the sprite's centre (world units).</summary>
        public static Vector2 GreyRootOffset(float length) => new Vector2(-(length + 0.14f) * 0.5f + 0.08f, 0f);
    }
}
